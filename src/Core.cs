using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Web.Script.Serialization;
namespace NwafuConnect {
 public enum AuthResult { Unknown, Online, Success, PasswordError, DeviceLimit, Rejected, NetworkError, Cancelled }
 public enum PortalState { Invalid, Online, Offline }
 public static class Policy {
  public static AuthResult Classify(string value) {
   string s=(value??"").ToLowerInvariant();
   if(Regex.IsMatch(s,@"\be2553\b|\be2531\b|ldapbinderror|ldapfirstentryerror|password_error|password[^\r\n]*(wrong|error|incorrect)|密码[^\r\n]*(错误|不正确)")) return AuthResult.PasswordError;
   if(Regex.IsMatch(s,@"\be2620\b|online_user_full|usernum_error|too many|设备[^\r\n]*(上限|超限)|在线用户数[^\r\n]*超")) return AuthResult.DeviceLimit;
   if(s.Contains("登录成功")) return AuthResult.Success;
   if(s.Contains("已登录")) return AuthResult.Online;
   if(Regex.IsMatch(s,@"timeout|timed out|deadline exceeded|no such host|network is unreachable|connection refused|connectex|tls|certificate|连接超时")) return AuthResult.NetworkError;
   if(Regex.IsMatch(s,@"login failed with error:\s*e\d+|authentication rejected|账户.*停用"))return AuthResult.Rejected; return AuthResult.Unknown;
  }
  public static PortalState ParsePortal(string value) {
   try {
    string s=(value??"").Trim();
    if(s.StartsWith("nwafuProbe(")) { s=s.Substring(11).TrimEnd(';', '\r','\n',' '); if(!s.EndsWith(")")) return PortalState.Invalid; s=s.Substring(0,s.Length-1); }
    var d=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(s);
    if(d==null || !d.ContainsKey("error")) return PortalState.Invalid;
    string error=Convert.ToString(d["error"]);
    bool hasIp=(d.ContainsKey("client_ip") && IsCampusAddress(Convert.ToString(d["client_ip"]))) || (d.ContainsKey("online_ip") && IsCampusAddress(Convert.ToString(d["online_ip"])));
    if(error=="ok" && (hasIp || (d.ContainsKey("user_name") && !String.IsNullOrEmpty(Convert.ToString(d["user_name"]))))) return PortalState.Online;
    if(error=="not_online_error" && hasIp) return PortalState.Offline;
   } catch {} return PortalState.Invalid;
  }
  public static int RetrySeconds(int failures) {
   int[] delays={3,3,5,5,10,10,15,30,60,120,300,600};
   return delays[Math.Max(0,Math.Min(delays.Length-1,failures-1))];
  }
  public static bool IsCampusAddress(string value) {
   IPAddress ip; if(!IPAddress.TryParse(value,out ip) || ip.AddressFamily!=AddressFamily.InterNetwork) return false;
   byte[] b=ip.GetAddressBytes(); return b[0]==10 || (b[0]==172 && b[1]>=16 && b[1]<=31) || (b[0]==192 && b[1]==168);
  }
  public static string Describe(AuthResult result) {
   switch(result) {
    case AuthResult.Online: return "校园网已认证";
    case AuthResult.Success: return "认证成功";
    case AuthResult.PasswordError: return "账号或密码错误，已暂停重试；修改后点击启动";
    case AuthResult.DeviceLimit: return "在线设备数达到上限，已暂停；请先下线其他设备";
    case AuthResult.NetworkError: return "网络暂不可用，稍后自动重试";
    case AuthResult.Cancelled: return "已停止自动连接";
    default: return "认证未通过，已暂停；请检查账号、套餐或网页登录提示";
   }
  }
 }
 public sealed class MonitorState {
  public bool Enabled, Blocked; public int Failures;
  public bool CanAttempt { get { return Enabled && !Blocked; } }
  public void Start() { Enabled=true; Blocked=false; Failures=0; }
  public void Stop() { Enabled=false; }
  public void Observe(AuthResult result) {
   if(result==AuthResult.PasswordError || result==AuthResult.DeviceLimit || result==AuthResult.Rejected) Blocked=true;
   if(result==AuthResult.NetworkError) Failures=Math.Min(Failures+1,20);
   if(result==AuthResult.Online || result==AuthResult.Success) Failures=0;
  }
 }
 public sealed class Account { public string Username {get;set;} public string Password {get;set;} }
 public sealed class Preferences { public bool Paused {get;set;} public bool Notifications {get;set;} public Preferences(){Notifications=true;} }
 public sealed class Store {
  public readonly string Root;
  static readonly byte[] entropy=Encoding.UTF8.GetBytes("NWAFU-CampusConnect-1");
  public Store(string root) { Root=Path.GetFullPath(root); Directory.CreateDirectory(Root); var acl=new DirectorySecurity(); acl.SetAccessRuleProtection(true,false);
   foreach(var sid in new SecurityIdentifier[]{WindowsIdentity.GetCurrent().User,new SecurityIdentifier("S-1-5-18"),new SecurityIdentifier("S-1-5-32-544")})
    acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
   Directory.SetAccessControl(Root,acl);
  }
  public string FilePath(string name){return Path.Combine(Root,name);}
  public bool HasAccount {get{return File.Exists(FilePath("account.dat"));}}
  public void Save(Account account) {
   if(account==null || String.IsNullOrWhiteSpace(account.Username) || String.IsNullOrEmpty(account.Password)) throw new ArgumentException("请填写账号和密码");
   byte[] clear=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(account));
   try { File.WriteAllBytes(FilePath("account.dat"),ProtectedData.Protect(clear,entropy,DataProtectionScope.CurrentUser)); } finally {Array.Clear(clear,0,clear.Length);}
  }
  public Account Load() {
   if(!HasAccount) return null;
   byte[] clear=ProtectedData.Unprotect(File.ReadAllBytes(FilePath("account.dat")),entropy,DataProtectionScope.CurrentUser);
   try {return new JavaScriptSerializer().Deserialize<Account>(Encoding.UTF8.GetString(clear));} finally {Array.Clear(clear,0,clear.Length);}
  }
  public Preferences LoadPreferences() { try{return new JavaScriptSerializer().Deserialize<Preferences>(File.ReadAllText(FilePath("settings.json")))??new Preferences();} catch{return new Preferences();} }
  public void SavePreferences(Preferences p) {
   string path=FilePath("settings.json"),temp=FilePath("settings-"+Guid.NewGuid().ToString("N")+".tmp");
   try{File.WriteAllText(temp,new JavaScriptSerializer().Serialize(p),new UTF8Encoding(false));if(File.Exists(path))File.Replace(temp,path,null);else File.Move(temp,path);}
   finally{if(File.Exists(temp))File.Delete(temp);}
  }
  public void ClearAccount(){if(HasAccount) File.Delete(FilePath("account.dat"));}
  public void RemoveRuntime(){foreach(string file in Directory.GetFiles(Root,"runtime-*.json")) {try{File.Delete(file);}catch{}}}
 }
 public static class EngineConfig {
  public static string Create(Account a) {return Create(a,2);}
  public static string Create(Account a,int requestTimeoutSeconds) {
   return new JavaScriptSerializer().Serialize(new {
    form=new {domain="portal.nwafu.edu.cn", username=a.Username, user_type="", password=a.Password},
    meta=new {n="200",type="1",acid="1",enc="srun_bx1",os="Windows",name="windows",info_prefix="SRBX1",double_stack=false},
    settings=new {
     basic=new {https=true,skip_cert_verify=false,timeout=requestTimeoutSeconds},guardian=new {enable=false,duration=60},
     backoff=new {enable=false,max_retries=1,initial_duration=10,max_duration=60,exponent_factor=1},
     log=new {debug_level=false,write_file=false},ddns=new {enable=false},reality=new {enable=false},
     custom_header=new Dictionary<string,string>{{"User-Agent","NWAFU-CampusConnect/1.0"}}
    }
   });
  }
 }
 public sealed class EngineRunner {
  public const string ExpectedHash="509e8842167a479a563f24d154d9d4536b2541b46f1231d13ee37a51a1107785";
  public static bool Verify(string engine) {using(var f=File.OpenRead(engine))using(var sha=SHA256.Create()){return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant()==ExpectedHash;}}
 }
}