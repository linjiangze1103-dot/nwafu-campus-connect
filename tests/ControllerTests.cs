using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NwafuConnect;
public static class ControllerTests {
 public static void Run(){string root=FastTests.Temp();try{
  var store=new Store(root);var account=new Account{Username="fixture",Password="fixture"};store.Save(account);
  var net=new FakeNetwork();var wire=new FakeTransport();wire.Query=t=>Task.FromResult(wire.Auth>0?PortalState.Online:PortalState.Offline);
  using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){
   FastTests.Check(FastTests.Until(()=>core.Snapshot.InternetVerified,1200),"AuthenticatesWithoutMessagePump");
   FastTests.Check(wire.Auth==1 && wire.Maximum==1,"single login verified online");int before=wire.Queries;net.Resume();FastTests.Check(FastTests.Until(()=>wire.Queries>before,500),"resume wakes unchanged network immediately");core.Stop();
   net.Set("two",true);Thread.Sleep(250);FastTests.Check(!core.Snapshot.Enabled && store.LoadPreferences().Paused && wire.Auth==1,"LateSuccessCannotUndoStop");
   core.ClearAccount();FastTests.Check(core.CurrentAccount==null && !store.HasAccount,"cleared account cannot be reused from memory");store.Save(account);
  }
  net=new FakeNetwork();wire=new FakeTransport();using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){Thread.Sleep(150);FastTests.Check(wire.Queries==0,"PausedRestartNeverProbes");}
  store.SavePreferences(new Preferences());net=new FakeNetwork();wire=new FakeTransport();wire.Query=t=>Task.FromResult(PortalState.Invalid);
  using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){
   FastTests.Check(FastTests.Until(()=>wire.Queries>0,500),"startup has no fixed timer delay");FastTests.Check(wire.Auth==0,"InvalidTlsOrPortalNeverAuthenticates");
   wire.Query=t=>Task.FromResult(PortalState.Online);net.Set("new",true);FastTests.Check(FastTests.Until(()=>core.Snapshot.InternetVerified,500),"NetworkChangeWakesBackoff");
  }
  store.SavePreferences(new Preferences());net=new FakeNetwork();wire=new FakeTransport();var started=new ManualResetEventSlim();var release=new TaskCompletionSource<AuthResult>();
  wire.Login=t=>{started.Set();return release.Task;};wire.Query=t=>Task.FromResult(wire.Auth>0?PortalState.Online:PortalState.Offline);
  using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){
   FastTests.Check(started.Wait(1000),"login in progress");for(int i=0;i<20;i++)net.Set("one",true);
   core.Stop();release.SetResult(AuthResult.Success);Thread.Sleep(250);FastTests.Check(!core.Snapshot.Enabled && wire.Auth==1 && wire.Maximum==1,"EventStormAndLateResultDoNotUndoStop");
  }
  store.SavePreferences(new Preferences());net=new FakeNetwork();net.Set("absent",false);wire=new FakeTransport();wire.Query=t=>Task.FromResult(PortalState.Online);
  using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){Thread.Sleep(100);net.Set("ready",true,false);FastTests.Check(FastTests.Until(()=>core.Snapshot.InternetVerified,1600),"NoEventStillFindsReadyNetwork");}
  store.SavePreferences(new Preferences());net=new FakeNetwork();wire=new FakeTransport();wire.Query=t=>Task.FromResult(wire.Auth>0?PortalState.Online:PortalState.Offline);wire.Login=t=>Task.FromResult(AuthResult.NetworkError);
  using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){FastTests.Check(FastTests.Until(()=>core.Snapshot.InternetVerified,1000) && wire.Auth==1,"UncertainLoginQueriesBeforeRetry");}
  store.SavePreferences(new Preferences());net=new FakeNetwork();wire=new FakeTransport();wire.Login=t=>Task.FromResult(AuthResult.PasswordError);
  using(var core=new ConnectionController(store,wire,net,new SystemClock(),new NullLog())){FastTests.Check(FastTests.Until(()=>core.Snapshot.Blocked,1000),"explicit credential rejection pauses");net.Set("changed",true);Thread.Sleep(250);FastTests.Check(wire.Auth==1 && store.LoadPreferences().Paused,"network event cannot resume credential rejection");}
 }finally{Directory.Delete(root,true);}}
}
