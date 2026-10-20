using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace NwafuConnect {
 public interface IStartupBackend {
  bool? ReadMarker();void WriteMarker(bool value);void RestoreMarker(bool? value);
  string ReadRun();void WriteRun(string value);
  string ReadTaskPath();string ExportTask();void RegisterTask(string path);void RestoreTask(string xml);void DeleteTask();
 }
 public sealed class StartupBackup {public bool? Marker;public string Run,TaskXml;}
 public sealed class StartupManager:IStartupManager {
  readonly string exe;readonly IStartupBackend backend;
  public StartupManager(string path):this(path,new WindowsStartupBackend()){}
  public StartupManager(string path,IStartupBackend backend){exe=Path.GetFullPath(path);this.backend=backend;}
  public static string StableDirectory{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","NWAFU-CampusConnect");}}
  public static string StableExe{get{return Path.Combine(StableDirectory,"西农校园网助手.exe");}}
  public static string RunCommand(string path){return "\""+path+"\" --autostart";}
  public static string ParseRun(string command){if(String.IsNullOrEmpty(command)||!command.EndsWith("\" --autostart",StringComparison.OrdinalIgnoreCase)||command[0]!='"')return null;string path=command.Substring(1,command.Length-14);return Path.GetFileName(path)=="西农校园网助手.exe"?path:null;}
  public bool ShouldAutoStart(){try{return backend.ReadMarker()??(ParseRun(backend.ReadRun())!=null);}catch{return false;}}
  public StartupStatus Read(){
   try{
    var marker=backend.ReadMarker();string run=backend.ReadRun();bool enabled=marker??(ParseRun(run)!=null);
    string task=backend.ReadTaskPath();bool taskOk=String.Equals(task,exe,StringComparison.OrdinalIgnoreCase),runOk=String.Equals(run,RunCommand(exe),StringComparison.OrdinalIgnoreCase);
    return new StartupStatus{Enabled=enabled,Fast=enabled&&taskOk,Operational=enabled&&(taskOk||runOk),PathVerified=taskOk&&runOk,ErrorCode=enabled&&!taskOk&&!runOk?"启动路径需要修复":null};
   }catch{return new StartupStatus{ErrorCode="无法读取自启配置"};}
  }
  public StartupStatus SetEnabled(bool enabled){
   string error=null;
   try{backend.WriteMarker(enabled);}catch{return new StartupStatus{ErrorCode="无法保存自启开关"};}
   if(enabled){
    try{backend.WriteRun(RunCommand(exe));}catch{error="普通自启注册失败";}
    try{backend.RegisterTask(exe);}catch{error="快速任务注册失败，已尝试普通自启";}
   }else{
    try{backend.WriteRun(null);}catch{error="残留普通入口已由关闭标志拦截";}
    try{backend.DeleteTask();}catch{error="残留快速任务已由关闭标志拦截";}
   }
   var status=Read();if(error!=null)status.ErrorCode=error;return status;
  }
  public StartupBackup Capture(){return new StartupBackup{Marker=backend.ReadMarker(),Run=backend.ReadRun(),TaskXml=backend.ExportTask()};}
  public void Restore(StartupBackup value){backend.RestoreMarker(value.Marker);backend.WriteRun(value.Run);backend.RestoreTask(value.TaskXml);}
 }
 public sealed class WindowsStartupBackend:IStartupBackend {
  const string AppKey=@"Software\NWAFU-CampusConnect",RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run",RunName="NWAFU-CampusConnect";
  public const string TaskName="NWAFU-CampusConnect-EarlyStart";
  public bool? ReadMarker(){using(var k=Registry.CurrentUser.OpenSubKey(AppKey)){object v=k==null?null:k.GetValue("AutoStartEnabled");return v==null?(bool?)null:Convert.ToInt32(v)==1;}}
  public void WriteMarker(bool value){using(var k=Registry.CurrentUser.CreateSubKey(AppKey))k.SetValue("AutoStartEnabled",value?1:0,RegistryValueKind.DWord);}
  public void RestoreMarker(bool? value){if(value.HasValue)WriteMarker(value.Value);else using(var k=Registry.CurrentUser.CreateSubKey(AppKey))k.DeleteValue("AutoStartEnabled",false);}
  public string ReadRun(){using(var k=Registry.CurrentUser.OpenSubKey(RunKey))return k==null?null:k.GetValue(RunName) as string;}
  public void WriteRun(string value){using(var k=Registry.CurrentUser.CreateSubKey(RunKey)){if(value==null)k.DeleteValue(RunName,false);else k.SetValue(RunName,value);}}
  static dynamic Connect(){dynamic service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service"));service.Connect();return service;}
  public string ReadTaskPath(){dynamic service=null;try{service=Connect();dynamic task=service.GetFolder("\\").GetTask(TaskName);if(!task.Enabled)return null;dynamic action=task.Definition.Actions.Item(1);return (string)action.Arguments=="--autostart"?(string)action.Path:null;}catch(COMException){return null;}finally{if(service!=null)Marshal.FinalReleaseComObject(service);}}
  public string ExportTask(){dynamic service=null;try{service=Connect();return (string)service.GetFolder("\\").GetTask(TaskName).Xml;}catch(COMException e){if((uint)e.ErrorCode==0x80070002)return null;throw;}finally{if(service!=null)Marshal.FinalReleaseComObject(service);}}
  public void RegisterTask(string path){dynamic service=Connect();try{dynamic def=service.NewTask(0);def.RegistrationInfo.Description="西农校园网助手：当前用户登录后立即启动";def.Principal.UserId=Program.Sid;def.Principal.LogonType=3;def.Principal.RunLevel=0;
   def.Settings.Enabled=true;def.Settings.StartWhenAvailable=true;def.Settings.DisallowStartIfOnBatteries=false;def.Settings.StopIfGoingOnBatteries=false;def.Settings.RunOnlyIfNetworkAvailable=false;def.Settings.ExecutionTimeLimit="PT0S";def.Settings.MultipleInstances=2;
   dynamic trigger=def.Triggers.Create(9);trigger.UserId=Program.Sid;trigger.Enabled=true;
   dynamic action=def.Actions.Create(0);action.Path=path;action.Arguments="--autostart";action.WorkingDirectory=Path.GetDirectoryName(path);
   service.GetFolder("\\").RegisterTaskDefinition(TaskName,def,6,Program.Sid,null,3,null);
  }finally{Marshal.FinalReleaseComObject(service);}}
  public void RestoreTask(string xml){if(xml==null){DeleteTask();return;}dynamic service=Connect();try{service.GetFolder("\\").RegisterTask(TaskName,xml,6,Program.Sid,null,3,null);}finally{Marshal.FinalReleaseComObject(service);}}
  public void DeleteTask(){dynamic service=Connect();try{try{service.GetFolder("\\").DeleteTask(TaskName,0);}catch(COMException e){if((uint)e.ErrorCode!=0x80070002)throw;}}finally{Marshal.FinalReleaseComObject(service);}}
 }
}

