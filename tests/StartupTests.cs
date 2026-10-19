using System;
using System.IO;
using NwafuConnect;
public sealed class FakeStartupBackend:IStartupBackend {
 public bool? Marker;public string RunValue,TaskValue;public bool FailTask;public bool TaskEnabled=true;
 public bool? ReadMarker(){return Marker;}public void WriteMarker(bool value){Marker=value;}
 public string ReadRun(){return RunValue;}public void WriteRun(string value){RunValue=value;}
 public string ReadTaskPath(){return TaskEnabled?TaskValue:null;}public string ExportTask(){return TaskValue;}
 public void RegisterTask(string path){if(FailTask)throw new Exception();TaskValue=path;}
 public void RestoreTask(string xml){TaskValue=xml;}public void DeleteTask(){if(FailTask)throw new Exception();TaskValue=null;}
 public void RestoreMarker(bool? value){Marker=value;}
}
public static class StartupTests {
 public static void Run(){
  string exe=Path.Combine(FastTests.Temp(),"西农校园网助手.exe");var b=new FakeStartupBackend();var s=new StartupManager(exe,b);
  b.FailTask=true;var status=s.SetEnabled(true);FastTests.Check(status.Enabled && !status.Fast && status.ErrorCode!=null,"PartialRegistrationNeverReportsFast");
  b.TaskValue=exe;status=s.SetEnabled(false);FastTests.Check(!status.Enabled && b.Marker==false,"DisabledMarkerBlocksStaleTask");
  b.FailTask=false;status=s.SetEnabled(true);FastTests.Check(status.Fast && status.PathVerified,"registered task and Run read back");
  var backup=s.Capture();s.SetEnabled(false);s.Restore(backup);FastTests.Check(s.Read().Fast,"RollbackRestoresExactStartupState");
  b.TaskValue="C:\\other\\app.exe";FastTests.Check(!s.Read().Fast,"wrong task path not reported fast");
  FastTests.Check(StartupManager.ParseRun(StartupManager.RunCommand(exe))==exe,"legacy quoted startup path preserves Chinese and spaces");Directory.Delete(Path.GetDirectoryName(exe),true);
 }
}
public static class InstallerTests {
 public static void Run(){
  string root=FastTests.Temp();try{string source=Path.Combine(root,"source"),target=Path.Combine(root,"NWAFU-CampusConnect");Directory.CreateDirectory(source);Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"sentinel"),"keep");
   var result=Installer.ValidatePackage(source);FastTests.Check(!result.Success && File.ReadAllText(Path.Combine(target,"sentinel"))=="keep","IncompleteArchiveDoesNotTouchInstallation");
   FastTests.Check(!Installer.IsWithin(target,Path.Combine(root,"other")) && !Installer.IsWithin(target,target+"-outside/file"),"MovedOldPathIsNotBroadlyDeleted");
   string stage=target+"-stage-test";Directory.CreateDirectory(stage);File.WriteAllText(Path.Combine(stage,"new-file"),"new");var swap=new DirectorySwap(target);
   FastTests.Check(!swap.Activate(stage,()=>false) && File.Exists(Path.Combine(target,"sentinel")),"BusyOldProcessRequiresConfirmedExit");
   FastTests.Check(swap.Activate(stage,()=>true) && File.Exists(Path.Combine(target,"new-file")),"verified installation switches directories");
   swap.Rollback();FastTests.Check(File.ReadAllText(Path.Combine(target,"sentinel"))=="keep" && !File.Exists(Path.Combine(target,"new-file")),"failed upgrade restores exact old files");
  }finally{Directory.Delete(root,true);}
 }
}

