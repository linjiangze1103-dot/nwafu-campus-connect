using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NwafuConnect;
public static class TransportTests {
 public static void Run(){
  FastTests.Check(Policy.Classify("unrecognized output")==AuthResult.Unknown,"UnknownOutputIsNotCredentialRejection");
  var lease=new EngineProcessGate();bool exited=false,released=false;lease.Enter();lease.Retain(()=>exited,()=>released=true);lease.Leave();
  bool refused=false;try{lease.Enter();}catch(EngineStuckException){refused=true;}
  FastTests.Check(refused && !released,"unconfirmed engine exit retains handle and rejects new execution");
  exited=true;lease.Enter();lease.Leave();FastTests.Check(released,"confirmed engine exit releases retained handle");
  var w=Stopwatch.StartNew();bool expired=false;try{Deadline.Run(t=>new TaskCompletionSource<int>().Task,TimeSpan.FromMilliseconds(80),CancellationToken.None).GetAwaiter().GetResult();}catch(TimeoutException){expired=true;}
  FastTests.Check(expired && w.ElapsedMilliseconds<500,"QueryBudgetIncludesDnsAndBody");
  int calls=0;var pending=new TaskCompletionSource<System.Net.IPAddress[]>();var dns=new DnsLookup(()=>{calls++;return pending.Task;});
  for(int i=0;i<3;i++)try{Deadline.Run(t=>dns.Lookup(),TimeSpan.FromMilliseconds(20),CancellationToken.None).GetAwaiter().GetResult();}catch(TimeoutException){}
  FastTests.Check(calls==1,"CancelledDnsDoesNotMultiplyOutstandingLookups");pending.SetResult(new[]{System.Net.IPAddress.Loopback});
  var info=new ProcessStartInfo("fixture");info.EnvironmentVariables["HTTPS_PROXY"]="secret-proxy";ProcessExecutor.RemoveProxy(info);
  FastTests.Check(!info.EnvironmentVariables.ContainsKey("HTTPS_PROXY"),"ProxyEnvironmentRemovedOnlyForChild");
  var proc=new ProcessStartInfo(typeof(FastTests).Assembly.Location,"--hang"){UseShellExecute=false,CreateNoWindow=true};
  w.Restart();var result=ProcessExecutor.RunAsync(proc,TimeSpan.FromMilliseconds(120),CancellationToken.None).GetAwaiter().GetResult();
  FastTests.Check(result.TimedOut && w.ElapsedMilliseconds<1600,"HungEngineReapedWithinBudget");
  proc=new ProcessStartInfo(typeof(FastTests).Assembly.Location,"--output"){UseShellExecute=false,CreateNoWindow=true};
  result=ProcessExecutor.RunAsync(proc,TimeSpan.FromSeconds(2),CancellationToken.None).GetAwaiter().GetResult();
  FastTests.Check(!result.TimedOut && result.Output.Length<=66000,"OutputReaderCannotHangAfterExit");
  proc=new ProcessStartInfo(typeof(FastTests).Assembly.Location,"--reject");
  result=ProcessExecutor.RunAsync(proc,TimeSpan.FromSeconds(2),CancellationToken.None).GetAwaiter().GetResult();
  FastTests.Check(result.Output.Contains("E2553") && Policy.Classify(result.Output)==AuthResult.PasswordError,"short-lived engine rejection is drained before classification");
  bool loserCancelled=false;
  var race=InternetProbe.Race(t=>Task.FromResult(false),async t=>{await Task.Delay(50,t);return true;},CancellationToken.None);
  FastTests.Check(race.GetAwaiter().GetResult(),"first failed probe does not suppress second success");
  race=InternetProbe.Race(t=>Task.FromResult(true),async t=>{try{await Task.Delay(2000,t);return false;}catch(OperationCanceledException){loserCancelled=true;throw;}},CancellationToken.None);
  FastTests.Check(race.GetAwaiter().GetResult() && FastTests.Until(()=>loserCancelled,500),"InternetProbeRaceCancelsLoser");
 }
}
