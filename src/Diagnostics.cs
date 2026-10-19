using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
namespace NwafuConnect {
 public interface IClock {long Milliseconds{get;} DateTime UtcNow{get;}}
 public sealed class SystemClock:IClock {readonly Stopwatch watch=Stopwatch.StartNew();public long Milliseconds{get{return watch.ElapsedMilliseconds;}}public DateTime UtcNow{get{return DateTime.UtcNow;}}}
 public interface IDiagnostics {void Record(string stage,long generation,long elapsedMs,string outcome);}
 public sealed class DiagnosticLog:IDiagnostics,IDisposable {
  readonly BlockingCollection<string> queue=new BlockingCollection<string>(256);readonly Queue<string> recent=new Queue<string>();readonly Thread writer;readonly string path;readonly IClock clock;
  volatile bool failed;public bool WriteFailed{get{return failed;}}public readonly string RunId=Guid.NewGuid().ToString("N");
  static readonly HashSet<string> stages=new HashSet<string>{"entry","ready","network","probe","auth","online","internet","stop","start","error","startup","shutdown","settings","install"};
  static readonly HashSet<string> outcomes=new HashSet<string>{"begin","end","ok","failed","timeout","cancelled","missing","paused","changed","ready","unavailable","Online","Offline","Invalid","Success","Unknown","Rejected","PasswordError","DeviceLimit","NetworkError","Cancelled","test","manual","autostart","verification","io"};
  public DiagnosticLog(Store store,IClock clock,string version,string source){this.clock=clock;path=store.FilePath("diagnostic-v2.log");writer=new Thread(WriteLoop){IsBackground=true,Name="NWAFU diagnostics"};writer.Start();Record("entry",0,0,outcomes.Contains(source)?source:"manual");}
  public void Record(string stage,long generation,long elapsedMs,string outcome){if(!stages.Contains(stage))stage="error";if(!outcomes.Contains(outcome))outcome="Unknown";string line=clock.UtcNow.ToString("o")+" run="+RunId+" version=1.3.0 ms="+clock.Milliseconds+" stage="+stage+" gen="+generation+" elapsed="+elapsedMs+" result="+outcome;
   lock(recent){recent.Enqueue(line);while(recent.Count>80)recent.Dequeue();}try{queue.TryAdd(line);}catch(InvalidOperationException){}
  }
  public string[] Snapshot(){lock(recent)return recent.ToArray();}
  void WriteLoop(){foreach(string line in queue.GetConsumingEnumerable())try{
   if(File.Exists(path) && new FileInfo(path).Length>=1048576){if(File.Exists(path+".2"))File.Delete(path+".2");if(File.Exists(path+".1"))File.Move(path+".1",path+".2");File.Move(path,path+".1");}
   File.AppendAllText(path,line+Environment.NewLine,new UTF8Encoding(false));
  }catch{failed=true;}}
  public void Dispose(){queue.CompleteAdding();writer.Join(1000);}
 }
}
