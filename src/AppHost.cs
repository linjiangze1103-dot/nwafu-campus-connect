using System;
using System.IO;
using System.Net;
using System.Diagnostics;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;
namespace NwafuConnect {
 public sealed class StartupStatus {public bool Enabled,Fast,PathVerified;public string ErrorCode;}
 public interface IStartupManager {StartupStatus Read();StartupStatus SetEnabled(bool enabled);}
 public static class Program {
  public const string Version="1.3.0";
  public static string Sid{get{return WindowsIdentity.GetCurrent().User.Value;}}
  public static string MutexName{get{return @"Local\NWAFU_CampusConnect_"+Sid;}}
  public static string ShowName{get{return @"Local\NWAFU_CampusConnect_Show_"+Sid;}}
  public static string ExitName{get{return @"Local\NWAFU_CampusConnect_Exit_"+Sid;}}
  [STAThread] public static void Main(string[] args){
   var clock=new SystemClock();bool auto=Array.IndexOf(args,"--autostart")>=0;
   if(!Environment.Is64BitOperatingSystem)return;
   int setting=Array.IndexOf(args,"--set-autostart");
   if(setting>=0 && setting+1<args.Length){new StartupManager(StartupManager.StableExe).SetEnabled(args[setting+1]=="on");return;}
   if(!String.Equals(Path.GetFullPath(Application.ExecutablePath),Path.GetFullPath(StartupManager.StableExe),StringComparison.OrdinalIgnoreCase)){
    string installer=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"安装或更新.exe");
    if(File.Exists(installer)){Process.Start(new ProcessStartInfo(installer){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory});return;}
    if(!auto)MessageBox.Show("请先全部解压软件包，再运行安装或更新程序。");return;
   }
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   bool first;using(var mutex=new Mutex(true,MutexName,out first)){
    if(!first){if(!auto){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<2000){try{using(var show=EventWaitHandle.OpenExisting(ShowName)){show.Set();return;}}catch(WaitHandleCannotBeOpenedException){Thread.Sleep(20);}}}return;}
    using(var show=new EventWaitHandle(false,EventResetMode.AutoReset,ShowName))
    using(var exit=new EventWaitHandle(false,EventResetMode.AutoReset,ExitName)){
     try{
      var store=new Store(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NWAFU-CampusConnect"));
      var startup=new StartupManager(Application.ExecutablePath);if(auto&&!startup.Read().Enabled)return;
      using(var log=new DiagnosticLog(store,clock,Version,auto?"autostart":"manual")){
       store.RemoveRuntime();
       using(var core=new ConnectionController(store,new ConnectionTransport(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"engine","bitsrun.exe"),store,log),new NetworkObserver(clock),clock,log))
       using(var host=new AppHost(store,core,auto,startup,log)){
        int index=Array.IndexOf(args,"--ready");Guid nonce;
        if(index>=0 && index+1<args.Length && Guid.TryParseExact(args[index+1],"N",out nonce))File.WriteAllText(store.FilePath("ready-"+nonce.ToString("N")+".txt"),Version+"|"+Process.GetCurrentProcess().Id);
        host.AttachSignals(show,exit);if(!auto)host.ShowWindow();Application.Run(host);
       }
      }
     }catch{if(!auto)MessageBox.Show("程序初始化失败，请重新安装后再试。","西农校园网助手");}
    }
   }
  }
 }
 public sealed class AppHost:ApplicationContext {
  readonly Store store;readonly ConnectionController core;readonly IStartupManager startup;readonly DiagnosticLog diagnostics;readonly System.Windows.Forms.Timer commands=new System.Windows.Forms.Timer();EventWaitHandle show,exit;bool closing;long lastNotice=-1;
  public MainWindow Window{get;private set;}
  public AppHost(Store store,ConnectionController controller,bool autoStart,IStartupManager startup=null,DiagnosticLog diagnostics=null){this.store=store;core=controller;this.startup=startup??new StartupManager(Application.ExecutablePath);this.diagnostics=diagnostics;commands.Interval=100;commands.Tick+=Poll;commands.Start();}
  public void AttachSignals(EventWaitHandle show,EventWaitHandle exit){this.show=show;this.exit=exit;}
  void Poll(object sender,EventArgs e){if(exit!=null && exit.WaitOne(0)){RequestShutdown();return;}if(show!=null && show.WaitOne(0))ShowWindow();var state=core.Snapshot;if(state.Blocked && core.Notifications && lastNotice!=state.Generation){lastNotice=state.Generation;MessageBox.Show("自动连接已暂停，请打开软件查看账号或认证状态。","西农校园网助手");}}
  public void ShowWindow(){if(closing)return;if(Window==null || Window.IsDisposed){Application.EnableVisualStyles();Window=new MainWindow(store,core,startup,diagnostics);}Window.Show();Window.WindowState=FormWindowState.Normal;Window.Activate();}
  public void RequestShutdown(){closing=true;commands.Stop();if(Window!=null)Window.Shutdown();core.Dispose();ExitThread();}
  protected override void Dispose(bool disposing){if(disposing){closing=true;commands.Dispose();if(Window!=null)Window.Dispose();}base.Dispose(disposing);}
 }
}

