using System;
using System.IO;
namespace NwafuConnect {
 public sealed class DirectorySwap {
  readonly string target,parent,backup;bool moved,activated;
  public DirectorySwap(string target){this.target=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);parent=Path.GetDirectoryName(this.target);if(Path.GetFileName(this.target)!="NWAFU-CampusConnect")throw new InvalidOperationException();backup=this.target+"-backup-"+Guid.NewGuid().ToString("N");}
  public bool Activate(string stage,Func<bool> quiesce){
   if(!Installer.IsWithin(parent,stage) || !Path.GetFileName(stage).StartsWith("NWAFU-CampusConnect-stage-",StringComparison.Ordinal))throw new InvalidOperationException();
   if((File.GetAttributes(stage)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException();
   if(Directory.Exists(target) && (File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException();
   if(!quiesce())return false;
   if(Directory.Exists(target)){Directory.Move(target,backup);moved=true;}
   Directory.Move(stage,target);activated=true;return true;
  }
  public void Rollback(){
   if(activated){if(!Installer.IsWithin(parent,target) || (File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException();foreach(var d in Directory.GetDirectories(target,"*",SearchOption.AllDirectories))if((File.GetAttributes(d)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException();Directory.Delete(target,true);activated=false;}
   if(moved){Directory.Move(backup,target);moved=false;}
  }
 }
}

