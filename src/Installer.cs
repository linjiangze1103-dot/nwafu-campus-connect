using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using System.Web.Script.Serialization;
namespace NwafuConnect {
 public sealed class InstallResult {public bool Success,RolledBack;public string ErrorCode;}
 public static class Installer {
  public static bool CanUseStartup(StartupStatus value){return !value.Enabled || value.Operational;}
  public static string LaunchArguments(string nonce,bool enabled,bool hasAccount,bool showWindow){return "--ready "+nonce+(enabled&&hasAccount&&!showWindow?" --autostart":"");}
  public static bool IsWithin(string root,string path){root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;return Path.GetFullPath(path).StartsWith(root,StringComparison.OrdinalIgnoreCase);}
  static string Hash(string file){using(var f=File.OpenRead(file))using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(f)).Replace("-","").ToLowerInvariant();}
  static Dictionary<string,string> Manifest(string source){
   var entries=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
   foreach(string line in File.ReadAllLines(Path.Combine(source,"SHA256SUMS.txt"))){if(String.IsNullOrWhiteSpace(line))continue;if(line.Length<67 || line.Substring(64,2)!="  ")throw new InvalidDataException();string name=line.Substring(66).Replace('/',Path.DirectorySeparatorChar);string path=Path.GetFullPath(Path.Combine(source,name));if(!IsWithin(source,path)||entries.ContainsKey(name))throw new InvalidDataException();entries[name]=line.Substring(0,64);}
   return entries;
  }
  public static InstallResult ValidatePackage(string source){try{
   var entries=Manifest(source);foreach(string required in new[]{"西农校园网助手.exe","安装或更新.exe",Path.Combine("engine","bitsrun.exe")})if(!entries.ContainsKey(required))throw new InvalidDataException();
   foreach(var pair in entries){string p=Path.Combine(source,pair.Key);if((File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0 || Hash(p)!=pair.Value)throw new InvalidDataException();}
   if(!EngineRunner.Verify(Path.Combine(source,"engine","bitsrun.exe")))throw new InvalidDataException();
   return new InstallResult{Success=true};
  }catch{return new InstallResult{ErrorCode="软件包不完整或校验失败，请先全部解压"};}}
  static void CopyPackage(string source,string target){foreach(var pair in Manifest(source)){string p=Path.Combine(target,pair.Key);if(!IsWithin(target,p))throw new InvalidDataException();Directory.CreateDirectory(Path.GetDirectoryName(p));File.Copy(Path.Combine(source,pair.Key),p,true);}File.Copy(Path.Combine(source,"SHA256SUMS.txt"),Path.Combine(target,"SHA256SUMS.txt"),true);}
  static void RemoveOwned(string path,string parent){if(!IsWithin(parent,path)||!Path.GetFileName(path).StartsWith("NWAFU-CampusConnect",StringComparison.Ordinal))throw new InvalidOperationException();if(Directory.Exists(path)){foreach(string d in Directory.GetDirectories(path,"*",SearchOption.AllDirectories))if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException();Directory.Delete(path,true);}}
  static bool StopMatchingChildren(int parentPid,string executable){
   using(var search=new System.Management.ManagementObjectSearcher("SELECT ProcessId,ExecutablePath FROM Win32_Process WHERE ParentProcessId="+parentPid))foreach(System.Management.ManagementObject child in search.Get()){
    if(!String.Equals(Convert.ToString(child["ExecutablePath"]),Path.Combine(Path.GetDirectoryName(executable),"engine","bitsrun.exe"),StringComparison.OrdinalIgnoreCase))continue;
    try{using(var engine=Process.GetProcessById(Convert.ToInt32(child["ProcessId"]))){if(!engine.WaitForExit(1000)){engine.Kill();if(!engine.WaitForExit(1000))return false;}}}catch(ArgumentException){}
   }
   return true;
  }
  static bool StopOld(string target,string oldExe){
   try{using(var ev=EventWaitHandle.OpenExisting(Program.ExitName))ev.Set();}catch(WaitHandleCannotBeOpenedException){}
   var wait=Stopwatch.StartNew();
   var allowed=new HashSet<string>(StringComparer.OrdinalIgnoreCase){Path.GetFullPath(target)};if(!String.IsNullOrEmpty(oldExe))allowed.Add(Path.GetFullPath(oldExe));
   foreach(var p in Process.GetProcessesByName("西农校园网助手"))using(p){
    if(p.Id==Process.GetCurrentProcess().Id || p.SessionId!=Process.GetCurrentProcess().SessionId)continue;
    string path;try{path=p.MainModule.FileName;}catch{return false;}
    if(!allowed.Contains(Path.GetFullPath(path)))return false;
    var version=FileVersionInfo.GetVersionInfo(path);if(version.ProductName!="西农校园网助手")return false;
    bool exited=p.WaitForExit(1500);
    if(!StopMatchingChildren(p.Id,path))return false;
    if(!exited){
     p.Kill();if(!p.WaitForExit(1000))return false;
    }
    if(!StopMatchingChildren(p.Id,path))return false;
   }
   try{using(var existing=Mutex.OpenExisting(Program.MutexName)){if(!existing.WaitOne(1000))return false;existing.ReleaseMutex();}}catch(WaitHandleCannotBeOpenedException){}catch(AbandonedMutexException){}
   return true;
  }
  public static InstallResult Install(string sourceDirectory,string targetDirectory,bool showWindow=false){
   string source=Path.GetFullPath(sourceDirectory),target=Path.GetFullPath(targetDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
   if(!String.Equals(target,StartupManager.StableDirectory,StringComparison.OrdinalIgnoreCase))return new InstallResult{ErrorCode="安装目标必须是当前用户的固定应用目录"};
   var validation=ValidatePackage(source);if(!validation.Success)return validation;
   if(String.Equals(source,target,StringComparison.OrdinalIgnoreCase))return new InstallResult{Success=true};
   string parent=Path.GetDirectoryName(target),stage=target+"-stage-"+Guid.NewGuid().ToString("N");
   string exe=Path.Combine(target,"西农校园网助手.exe");var startup=new StartupManager(exe);StartupBackup config=null;var swap=new DirectorySwap(target);Process launched=null;
   bool owner;using(var mutex=new Mutex(true,@"Local\NWAFU_CampusConnect_Install_"+Program.Sid,out owner)){
    if(!owner)return new InstallResult{ErrorCode="另一个安装正在进行"};
    try{
     Directory.CreateDirectory(parent);Directory.CreateDirectory(stage);CopyPackage(source,stage);
     if(!ValidatePackage(stage).Success)throw new InvalidDataException();
     config=startup.Capture();bool enabled=config.Marker??(StartupManager.ParseRun(config.Run)!=null);
     if(!swap.Activate(stage,()=>StopOld(exe,StartupManager.ParseRun(config.Run))))throw new InvalidOperationException("old_process");
     
     var status=startup.SetEnabled(enabled);if(status.Enabled!=enabled || !CanUseStartup(status))throw new InvalidOperationException("startup");
     string nonce=Guid.NewGuid().ToString("N");var store=new Store(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NWAFU-CampusConnect"));string ready=store.FilePath("ready-"+nonce+".txt");
     bool hasAccount=store.HasAccount;launched=Process.Start(new ProcessStartInfo(exe,LaunchArguments(nonce,enabled,hasAccount,showWindow)){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=target});
     var watch=Stopwatch.StartNew();bool success=false;while(watch.ElapsedMilliseconds<15000){if(File.Exists(ready)){try{success=File.ReadAllText(ready)==Program.Version+"|"+launched.Id;}catch{}if(success)break;}if(launched.HasExited)break;Thread.Sleep(50);}
     try{File.Delete(ready);}catch{}if(!success)throw new InvalidOperationException("readiness");
     // Retain the backup for user recovery. No credentials are copied into it.
     return new InstallResult{Success=true};
    }catch{
     bool rollback=true;
     try{if(launched!=null){if(!launched.HasExited && !StopOld(exe,exe))throw new InvalidOperationException();if(!StopMatchingChildren(launched.Id,exe))throw new InvalidOperationException();}
      swap.Rollback();if(config!=null)startup.Restore(config);
      string old=config==null?null:StartupManager.ParseRun(config.Run);if(old!=null&&File.Exists(old))Process.Start(new ProcessStartInfo(old,"--autostart"){UseShellExecute=false,CreateNoWindow=true});
     }catch{rollback=false;}
     return new InstallResult{RolledBack=rollback,ErrorCode=rollback?"安装未完成，已恢复原设置":"安装未完成，回滚需检查；备份保留"};
    }finally{if(launched!=null)launched.Dispose();try{RemoveOwned(stage,parent);}catch{}}
   }
  }
 }
 public static class InstallerProgram {
  [STAThread] public static int Main(string[] args){
   var result=Installer.Install(AppDomain.CurrentDomain.BaseDirectory,StartupManager.StableDirectory,Array.IndexOf(args,"--quiet")<0);
   if(!result.Success && Array.IndexOf(args,"--quiet")<0)MessageBox.Show(result.ErrorCode,"西农校园网助手安装");
   return result.Success?0:1;
  }
 }
}

