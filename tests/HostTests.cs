using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NwafuConnect;
public static class HostTests {
 public static void Run(){string root=FastTests.Temp();try{var store=new Store(root);store.Save(new Account{Username="fixture",Password="fixture"});var wire=new FakeTransport();wire.Query=t=>System.Threading.Tasks.Task.FromResult(PortalState.Online);
  string installed=Path.Combine(root,"installed.exe");File.Copy(typeof(FastTests).Assembly.Location,installed);
  FastTests.Check(PackageLauncher.CanForward(installed),"same-version portable entry forwards to installed host");
  FastTests.Check(!Installer.LaunchArguments("fixture",true,true,true).Contains("--autostart"),"manual upgrade opens window with saved account and enabled startup");
  FastTests.Check(Installer.LaunchArguments("fixture",true,true,false).Contains("--autostart"),"quiet upgrade retains hidden launch");
  using(var core=new ConnectionController(store,wire,new FakeNetwork(),new SystemClock(),new NullLog()))using(var host=new AppHost(store,core,true,new FakeStartup())){
   FastTests.Check(host.Window==null,"AutostartCreatesNoForm");Thread.Sleep(200);FastTests.Check(core.Snapshot.InternetVerified,"BlockedUiDoesNotBlockController");
   host.ShowWindow();var form=host.Window;FastTests.Check(form!=null && form.Visible,"manual request creates window");host.ShowWindow();FastTests.Check(Object.ReferenceEquals(form,host.Window),"second request reuses window");
   form.Close();FastTests.Check(!form.Visible && !form.IsDisposed && core.Snapshot.Enabled,"CloseKeepsMonitoring");
   host.ShowWindow();var stop=(Button)typeof(MainWindow).GetField("stop",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);stop.PerformClick();FastTests.Check(!core.Snapshot.Enabled && store.LoadPreferences().Paused,"StopPersistsAfterHostRestart");
  }
 }finally{Directory.Delete(root,true);}}
}
public sealed class FakeStartup:IStartupManager {public bool Enabled;public StartupStatus Read(){return new StartupStatus{Enabled=Enabled,Fast=Enabled,PathVerified=true};}public StartupStatus SetEnabled(bool value){Enabled=value;return Read();}}
