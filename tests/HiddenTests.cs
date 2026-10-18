using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using NwafuConnect;
class HiddenTests {
 [STAThread] static int Main(){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"hidden-test-"+Guid.NewGuid().ToString("N"));
  MainWindow f=null;int queries=0,auth=0;double second=0,third=0;var watch=Stopwatch.StartNew();
  try {
   var store=new Store(dir);store.Save(new Account{Username="fixture",Password="fixture"});store.SavePreferences(new Preferences{Notifications=false});
   f=new MainWindow(store,true,t=>{
    int n=Interlocked.Increment(ref queries);
    if(n==1)return PortalState.Invalid;
    if(n==2){second=watch.Elapsed.TotalSeconds;f.BeginInvoke((Action)(()=>typeof(MainWindow).GetMethod("ScheduleNetworkCheck",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,null)));Thread.Sleep(300);return PortalState.Invalid;}
    if(n==3){third=watch.Elapsed.TotalSeconds;return PortalState.Offline;}
    return PortalState.Online;
   },(a,t)=>{Interlocked.Increment(ref auth);return AuthResult.Success;});
   f.Show();
   while(watch.Elapsed.TotalSeconds<15 && queries<4){Application.DoEvents();Thread.Sleep(20);}
   Application.DoEvents();
   if(f.Visible || queries<4 || auth!=1 || second>5 || third-second>2)throw new Exception("hidden/fast-retry/pending-network-event failed");
   Console.WriteLine("PASS hidden startup detects offline, authenticates once and verifies online without opening UI");
   Console.WriteLine("First retry at "+second.ToString("F2")+"s; network-ready event retry after "+(third-second).ToString("F2")+"s");
   typeof(MainWindow).GetMethod("StopMonitoring",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(f,null);
   var monitor=(MonitorState)typeof(MainWindow).GetField("state",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(f);
   if(monitor.CanAttempt || !store.LoadPreferences().Paused)throw new Exception("stop not retained");
   Console.WriteLine("PASS explicit stop remains effective");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}finally{if(f!=null)f.Dispose();if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
}

