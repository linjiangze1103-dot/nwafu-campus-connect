using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using NwafuConnect;
public static class DiagnosticsTests {
 public static void Run(){string root=FastTests.Temp();try{
  var store=new Store(root);var clock=new SystemClock();
  using(var log=new DiagnosticLog(store,clock,"1.3.0","test")){
   log.Record("probe",1,5,"Online");
   log.Record("fixture-secret-password",1,0,"fixture-account-token");
   FastTests.Check(FastTests.Until(()=>File.Exists(store.FilePath("diagnostic-v2.log")),1000),"log is asynchronously persisted");
   FastTests.Check(!String.Join("",log.Snapshot()).Contains("fixture-"),"LogsNeverContainCredentialFixture");
   FastTests.Check(String.Join("",log.Snapshot()).Contains(log.RunId),"current run identity retained");
  }
  string logfile=store.FilePath("diagnostic-v2.log");File.WriteAllText(logfile,new string('x',1048576));
  using(var log=new DiagnosticLog(store,clock,"1.3.0","test")){log.Record("probe",1,1,"Online");FastTests.Check(FastTests.Until(()=>File.Exists(logfile+".1"),1000),"RotationRetainsCurrentRun");}
  using(var locked=new FileStream(logfile,FileMode.Open,FileAccess.ReadWrite,FileShare.None))using(var log=new DiagnosticLog(store,clock,"1.3.0","test")){
   var w=Stopwatch.StartNew();for(int i=0;i<300;i++)log.Record("probe",1,1,"Online");
   FastTests.Check(w.ElapsedMilliseconds<300,"SlowDiskDoesNotBlockCaller");
   FastTests.Check(FastTests.Until(()=>log.WriteFailed,1000) && log.Snapshot().Length>0,"write failure retains in-memory diagnostics");
  }
  store.SavePreferences(new Preferences{Paused=true});
  using(var locked=new FileStream(store.FilePath("settings.json"),FileMode.Open,FileAccess.Read,FileShare.Read)){
   bool failed=false;try{store.SavePreferences(new Preferences{Paused=false});}catch{failed=true;}
   FastTests.Check(failed && store.LoadPreferences().Paused,"SettingsInterruptedWriteRetainsOldValue");
  }
 }finally{Directory.Delete(root,true);}}
}
