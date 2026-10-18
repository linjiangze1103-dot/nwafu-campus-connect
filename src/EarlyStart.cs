using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Threading;
using Microsoft.Win32;
public static class EarlyStart {
 static void Trace(string text){try{File.AppendAllText(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NWAFU-CampusConnect","early-start.log"),DateTime.Now.ToString("MM-dd HH:mm:ss")+" "+text+Environment.NewLine);}catch{}}
 public static bool Configured(string setting,string exe){return String.Equals(setting,"\""+exe+"\" --autostart",StringComparison.OrdinalIgnoreCase);}
 public static bool KnownLegacy(string setting,string exe){string root=Directory.GetParent(Path.GetDirectoryName(exe)).FullName;foreach(string version in new[]{"1.0.0","1.1.0","1.2.0","1.2.1"})if(Configured(setting,Path.Combine(root,"NWAFU-校园网助手-"+version,"西农校园网助手.exe")))return true;return false;}
 [STAThread] public static int Main(){
  try{
   string exe=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"西农校园网助手.exe");
   string setting;using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){setting=key==null?null:key.GetValue("NWAFU-CampusConnect") as string;}
   if(!Configured(setting,exe) && KnownLegacy(setting,exe)){using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){setting="\""+exe+"\" --autostart";key.SetValue("NWAFU-CampusConnect",setting);}Trace("已迁移登录环境中的旧版自启路径。");}
   if(!Configured(setting,exe)){Trace("自启开关关闭或路径不同："+setting+"；本机目标："+exe);return 0;}
   try{using(var existing=Mutex.OpenExisting("Local\\NWAFU_CampusConnect_"+WindowsIdentity.GetCurrent().User.Value)){Trace("已有实例，跳过重复启动。");return 0;}}catch(WaitHandleCannotBeOpenedException){}
   Trace("启动新版后台。");Process.Start(new ProcessStartInfo(exe,"--autostart"){UseShellExecute=false,WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory,CreateNoWindow=true});
   return 0;
  }catch(Exception e){Trace("启动异常："+e.GetType().Name);return 1;}
 }
}


