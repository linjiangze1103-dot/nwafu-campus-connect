using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
namespace NwafuConnect {
 public interface IConnectionTransport {
  Task<PortalState> QueryAsync(TimeSpan budget,CancellationToken token);
  Task<AuthResult> AuthenticateAsync(Account account,bool slowPath,CancellationToken token);
  Task<bool> CheckInternetAsync(CancellationToken token);
 }
 public static class Deadline {
  public static void Observe(Task task){task.ContinueWith(t=>{var ignored=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
  public static async Task<T> Run<T>(Func<CancellationToken,Task<T>> work,TimeSpan budget,CancellationToken token){
   using(var limit=CancellationTokenSource.CreateLinkedTokenSource(token)){
    Task<T> operation=work(limit.Token);
    try{
     var timeout=Task.Delay(budget,token);
     if(await Task.WhenAny(operation,timeout).ConfigureAwait(false)!=operation){token.ThrowIfCancellationRequested();throw new TimeoutException();}
     return await operation.ConfigureAwait(false);
    }finally{limit.Cancel();Deadline.Observe(operation);}
   }
  }
 }
 public sealed class DnsLookup {
  readonly object gate=new object();readonly Func<Task<IPAddress[]>> resolve;Task<IPAddress[]> pending;
  public DnsLookup(Func<Task<IPAddress[]>> resolve){this.resolve=resolve;}
  public Task<IPAddress[]> Lookup(){lock(gate){if(pending==null || pending.IsCompleted)pending=resolve();return pending;}}
 }
 public sealed class ProcessOutcome {public bool TimedOut;public string Output;}
 public sealed class EngineStuckException:Exception {}
 public static class ProcessExecutor {
  public static void RemoveProxy(ProcessStartInfo info){var keys=new List<string>();foreach(string key in info.EnvironmentVariables.Keys)if(key.Equals("http_proxy",StringComparison.OrdinalIgnoreCase)||key.Equals("https_proxy",StringComparison.OrdinalIgnoreCase)||key.Equals("all_proxy",StringComparison.OrdinalIgnoreCase)||key.Equals("no_proxy",StringComparison.OrdinalIgnoreCase))keys.Add(key);foreach(string key in keys)info.EnvironmentVariables.Remove(key);}
  static readonly EngineProcessGate execution=new EngineProcessGate();
  public static async Task<ProcessOutcome> RunAsync(ProcessStartInfo info,TimeSpan budget,CancellationToken token){
   token.ThrowIfCancellationRequested();info.UseShellExecute=false;info.CreateNoWindow=true;info.RedirectStandardOutput=true;info.RedirectStandardError=true;info.StandardOutputEncoding=Encoding.UTF8;info.StandardErrorEncoding=Encoding.UTF8;RemoveProxy(info);
   var output=new StringBuilder();object gate=new object();
   var stdout=new TaskCompletionSource<bool>();var stderr=new TaskCompletionSource<bool>();
   var p=new Process{StartInfo=info};bool started=false,stopAttempted=false;
   try{execution.Enter();}catch{p.Dispose();throw;}
   try{
    Action<string> append=text=>{lock(gate){int room=65536-output.Length;if(room>0)output.Append(text.Substring(0,Math.Min(room,text.Length))).Append('\n');}};
    p.OutputDataReceived+=(s,e)=>{if(e.Data==null)stdout.TrySetResult(true);else append(e.Data);};
    p.ErrorDataReceived+=(s,e)=>{if(e.Data==null)stderr.TrySetResult(true);else append(e.Data);};
    var watch=Stopwatch.StartNew();started=p.Start();if(!started)throw new InvalidOperationException();p.BeginOutputReadLine();p.BeginErrorReadLine();
    bool expired=false;
    while(!p.HasExited){if(token.IsCancellationRequested || watch.Elapsed>=budget){expired=true;break;}await Task.Delay(20).ConfigureAwait(false);}
    if(expired){stopAttempted=true;try{p.Kill();}catch{}if(!p.WaitForExit(1000))throw new EngineStuckException();}
    // Drain to EOF, bounded even if a descendant inherited a pipe handle.
    await Task.WhenAny(Task.WhenAll(stdout.Task,stderr.Task),Task.Delay(250)).ConfigureAwait(false);
    token.ThrowIfCancellationRequested();
    lock(gate)return new ProcessOutcome{TimedOut=expired,Output=output.ToString()};
   }finally{
    bool confirmed=!started;
    if(started)try{if(!p.HasExited){if(!stopAttempted){try{p.Kill();}catch{}confirmed=p.WaitForExit(1000);}}else confirmed=true;}catch{}
    try{p.CancelOutputRead();}catch{}try{p.CancelErrorRead();}catch{}
    if(confirmed)p.Dispose();else execution.Retain(()=>p.HasExited,()=>p.Dispose());
    execution.Leave();
    if(!confirmed)throw new EngineStuckException();
   }
  }
 }
 public static class HttpText {
  public static async Task<string> Read(Uri uri,int limit,CancellationToken token){
   var request=(HttpWebRequest)WebRequest.Create(uri);request.Proxy=null;request.AllowAutoRedirect=false;request.Timeout=3000;request.ReadWriteTimeout=3000;
   using(token.Register(()=>request.Abort())){
    using(var response=(HttpWebResponse)await request.GetResponseAsync().ConfigureAwait(false)){
     if(response.StatusCode!=HttpStatusCode.OK)return "";
     using(var reader=new StreamReader(response.GetResponseStream(),Encoding.UTF8)){
      var b=new char[limit];int count=0;
      while(count<limit){int n=await reader.ReadAsync(b,count,limit-count).ConfigureAwait(false);if(n==0)break;count+=n;if(uri.Host!="portal.nwafu.edu.cn" && Regex.IsMatch(new string(b,0,count),@"(?im)^User-agent\s*:"))break;}
      return count==limit && uri.Host=="portal.nwafu.edu.cn"?"":new string(b,0,count);
     }
    }
   }
  }
 }
 public static class InternetProbe {
  public static async Task<bool> Race(Func<CancellationToken,Task<bool>> first,Func<CancellationToken,Task<bool>> second,CancellationToken token){
   using(var cancel=CancellationTokenSource.CreateLinkedTokenSource(token)){
    var tasks=new List<Task<bool>>{first(cancel.Token),second(cancel.Token)};
    try{while(tasks.Count>0){var done=await Task.WhenAny(tasks).ConfigureAwait(false);tasks.Remove(done);try{if(await done.ConfigureAwait(false))return true;}catch(OperationCanceledException){token.ThrowIfCancellationRequested();}catch{} }return false;}
    finally{cancel.Cancel();foreach(var task in tasks)Deadline.Observe(task);}
   }
  }
 }
 public sealed class ConnectionTransport:IConnectionTransport {
  readonly string engine;readonly Store store;readonly IDiagnostics log;readonly DnsLookup dns=new DnsLookup(()=>Dns.GetHostAddressesAsync("portal.nwafu.edu.cn"));
  public ConnectionTransport(string enginePath,Store store,IDiagnostics log){engine=enginePath;this.store=store;this.log=log;}
  public async Task<PortalState> QueryAsync(TimeSpan budget,CancellationToken token){
   var watch=Stopwatch.StartNew();
   try{return await Deadline.Run(async t=>{
    bool valid=false;foreach(var ip in await dns.Lookup().ConfigureAwait(false))if(Policy.IsCampusAddress(ip.ToString()))valid=true;
    t.ThrowIfCancellationRequested();if(!valid)return PortalState.Invalid;
    return Policy.ParsePortal(await HttpText.Read(new Uri("https://portal.nwafu.edu.cn/cgi-bin/rad_user_info?callback=nwafuProbe"),32768,t).ConfigureAwait(false));
   },budget,token).ConfigureAwait(false);}
   catch(OperationCanceledException){throw;}
   catch{return PortalState.Invalid;}
   finally{log.Record("probe",0,watch.ElapsedMilliseconds,"end");}
  }
  public async Task<AuthResult> AuthenticateAsync(Account account,bool slowPath,CancellationToken token){
   token.ThrowIfCancellationRequested();
   if(!await Task.Run(()=>EngineRunner.Verify(engine),token).ConfigureAwait(false))throw new InvalidDataException();
   token.ThrowIfCancellationRequested();
   string path=store.FilePath("runtime-"+Guid.NewGuid().ToString("N")+".json");
   try{File.WriteAllText(path,EngineConfig.Create(account,slowPath?4:2),new UTF8Encoding(false));
    var result=await ProcessExecutor.RunAsync(new ProcessStartInfo(engine,"--config \""+path+"\""){WorkingDirectory=store.Root},TimeSpan.FromSeconds(slowPath?15:8),token).ConfigureAwait(false);
    return result.TimedOut?AuthResult.NetworkError:Policy.Classify(result.Output);
   }finally{try{File.Delete(path);}catch{log.Record("error",0,0,"io");}}
  }
  public async Task<bool> CheckInternetAsync(CancellationToken token){
   try{return await Deadline.Run(t=>InternetProbe.Race(
    async c=>Regex.IsMatch(await HttpText.Read(new Uri("https://www.baidu.com/robots.txt"),4096,c).ConfigureAwait(false),@"(?im)^User-agent\s*:"),
    async c=>Regex.IsMatch(await HttpText.Read(new Uri("https://www.qq.com/robots.txt"),4096,c).ConfigureAwait(false),@"(?im)^User-agent\s*:"),t),TimeSpan.FromSeconds(1),token).ConfigureAwait(false);}
   catch(OperationCanceledException){throw;}catch{return false;}
  }
 }
}

