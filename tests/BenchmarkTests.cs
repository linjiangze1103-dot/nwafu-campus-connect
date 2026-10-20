using System;
using System.IO;
using System.Diagnostics;
using System.Threading.Tasks;
using NwafuConnect;
public static class BenchmarkTests {
 public static void Run(){var boot=new DateTime(2026,10,19,0,0,0,DateTimeKind.Utc);var p=boot.AddSeconds(20);var i=p.AddSeconds(1);
  FastTests.Check(!EvidenceEvaluator.Evaluate(boot.AddSeconds(19),null,p,i,boot).Passed,"MissingNetworkTimestampIsNotPass");
  FastTests.Check(!EvidenceEvaluator.Evaluate(boot.AddSeconds(1),boot.AddSeconds(2),p,i,boot).Passed,"late process start counts against five seconds");
  FastTests.Check(!EvidenceEvaluator.Evaluate(boot.AddSeconds(-10),boot.AddSeconds(-9),boot.AddSeconds(-8),boot.AddSeconds(-7),boot).Passed,"OldRunCannotMatchCurrentBoot");
  FastTests.Check(EvidenceEvaluator.Evaluate(boot.AddSeconds(19),boot.AddSeconds(20),p,i,boot).Passed,"complete valid timing sample can pass");
  string root=FastTests.Temp();try{
   var store=new Store(root);store.Save(new Account{Username="fixture",Password="fixture"});
   var net=new FakeNetwork();var wire=new FakeTransport();wire.Query=t=>Task.FromResult(PortalState.Online);
   using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){
    FastTests.Check(FastTests.Until(()=>core.Snapshot.InternetVerified && !core.Snapshot.Busy,1000),"benchmark reaches idle online state");
    int before=wire.Queries;var watch=Stopwatch.StartNew();net.Set("changed-route",true);
    FastTests.Check(FastTests.Until(()=>wire.Queries>before,500),"simulated idle network event starts probe within 500ms");
    Console.WriteLine("MEASURE simulated event-to-probe ms="+watch.ElapsedMilliseconds+" (fake network, excludes Windows launch and server latency)");
   }
  }finally{Directory.Delete(root,true);}
 }
}
