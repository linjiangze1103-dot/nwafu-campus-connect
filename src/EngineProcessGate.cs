using System;
namespace NwafuConnect {
 // Lifetime ownership is separate from controller generations and user commands.
 public sealed class EngineProcessGate {
  readonly object gate=new object();
  bool busy;
  Func<bool> retainedExited;
  Action releaseRetained;
  public void Enter(){lock(gate){
   if(busy)throw new EngineStuckException();
   if(retainedExited!=null){
    bool exited=false;try{exited=retainedExited();}catch{}
    if(!exited)throw new EngineStuckException();
    releaseRetained();retainedExited=null;releaseRetained=null;
   }
   busy=true;
  }}
  public void Retain(Func<bool> exited,Action release){lock(gate){retainedExited=exited;releaseRetained=release;}}
  public void Leave(){lock(gate){busy=false;}}
 }
}
