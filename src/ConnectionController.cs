using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
namespace NwafuConnect {
 public sealed class ConnectionSnapshot {
  public readonly long Generation,NextDueMs;public readonly string Stage;public readonly bool Enabled,Blocked,Busy,InternetVerified,SettingsFailed;public readonly AuthResult Result;
  public ConnectionSnapshot(long generation,string stage,bool enabled,bool blocked,bool busy,bool internet,long due,AuthResult result,bool settingsFailed){Generation=generation;Stage=stage;Enabled=enabled;Blocked=blocked;Busy=busy;InternetVerified=internet;NextDueMs=due;Result=result;SettingsFailed=settingsFailed;}
 }
 public sealed class ConnectionController:IDisposable {
  readonly object gate=new object();readonly AutoResetEvent wake=new AutoResetEvent(false);readonly Store store;readonly IConnectionTransport transport;readonly INetworkObserver network;readonly IClock clock;readonly IDiagnostics log;readonly Thread worker;
  Account account;Preferences prefs;NetworkSnapshot net;CancellationTokenSource attempt;bool enabled,blocked,busy,internet,disposed,authenticating,settingsFailed,needInternet=true,slowUsed;int failures,unknown,externalFailures;long generation,due,windowStart,lastAuth=-10000,lastSnapshot,lastPortalOnline;string stage="paused";AuthResult result=AuthResult.Unknown;
  int notifying;public event Action<ConnectionSnapshot> Changed;
  public ConnectionController(Store store,IConnectionTransport transport,INetworkObserver network,IClock clock,IDiagnostics log){
   this.store=store;this.transport=transport;this.network=network;this.clock=clock;this.log=log;prefs=store.LoadPreferences();
   try{account=store.Load();}catch{stage="credentials";}enabled=account!=null && !prefs.Paused;if(enabled)stage="waiting";
   windowStart=clock.Milliseconds;network.Changed+=NetworkChanged;network.Start();NetworkChanged(network.Read());due=clock.Milliseconds;
   log.Record("ready",generation,0,enabled?"ready":"paused");
   worker=new Thread(Loop){IsBackground=true,Name="NWAFU connection"};worker.Start();
  }
  public ConnectionSnapshot Snapshot{get{lock(gate)return new ConnectionSnapshot(generation,stage,enabled,blocked,busy,internet,due,result,settingsFailed);}}
  public Account CurrentAccount{get{lock(gate)return account==null?null:new Account{Username=account.Username,Password=account.Password};}}
  public void SetNotifications(bool value){lock(gate){prefs.Notifications=value;Persist();}}
  public bool Notifications{get{lock(gate)return prefs.Notifications;}}
  public void Start(Account value){if(value==null)throw new ArgumentNullException("value");lock(gate){account=new Account{Username=value.Username,Password=value.Password};generation++;enabled=true;blocked=false;failures=unknown=externalFailures=0;internet=false;needInternet=true;slowUsed=false;windowStart=due=clock.Milliseconds;prefs.Paused=false;Persist();if(attempt!=null)attempt.Cancel();stage="waiting";}log.Record("start",generation,0,"begin");Signal();}
  public void Stop(){lock(gate){enabled=false;generation++;prefs.Paused=true;Persist();stage="paused";if(attempt!=null)attempt.Cancel();}log.Record("stop",generation,0,"paused");Signal();}
  public void CheckNow(){Account saved=CurrentAccount;if(saved!=null)Start(saved);}
  public void ClearAccount(){Stop();lock(gate){account=null;store.ClearAccount();}}
  public void Resume(){lock(gate){if(!enabled || blocked)return;windowStart=due=clock.Milliseconds;needInternet=true;}Signal();}
  void Persist(){try{store.SavePreferences(prefs);settingsFailed=false;}catch{settingsFailed=true;log.Record("settings",generation,0,"io");}}
  void Signal(){wake.Set();Notify();}
  void Notify(){if(Interlocked.Exchange(ref notifying,1)!=0)return;ThreadPool.QueueUserWorkItem(_=>{try{var h=Changed;if(h!=null)h(Snapshot);}catch{}finally{Interlocked.Exchange(ref notifying,0);}});}
  void NetworkChanged(NetworkSnapshot value){
   if(value.Resumed){Resume();return;}
   lock(gate){if(disposed)return;if(net!=null && net.Fingerprint==value.Fingerprint && net.CandidateReady==value.CandidateReady)return;
    net=value;generation++;internet=false;needInternet=true;externalFailures=0;
    if(enabled && !blocked){failures=0;windowStart=clock.Milliseconds;due=clock.Milliseconds+150;if(attempt!=null && (!authenticating || !value.CandidateReady))attempt.Cancel();}
    log.Record("network",generation,0,value.CandidateReady?"ready":"unavailable");
   }Signal();
  }
  void Loop(){while(true){
   lock(gate){if(disposed)return;}
   if(clock.Milliseconds-lastSnapshot>=1000){lastSnapshot=clock.Milliseconds;NetworkChanged(network.Read());}
   bool run;lock(gate){run=enabled && !blocked && !busy && clock.Milliseconds>=due;if(run){busy=true;attempt=new CancellationTokenSource();}}
   if(run){try{Cycle().GetAwaiter().GetResult();}catch{lock(gate){if(enabled){result=AuthResult.NetworkError;stage="waiting";due=clock.Milliseconds+2000;}}}
    finally{lock(gate){busy=false;authenticating=false;if(attempt!=null){attempt.Dispose();attempt=null;}}Notify();}
   }else wake.WaitOne(100);
  }}
  bool Live(long epoch){return !disposed && enabled && !blocked && epoch==generation && attempt!=null && !attempt.IsCancellationRequested;}
  TimeSpan ProbeBudget(){return TimeSpan.FromMilliseconds(clock.Milliseconds-windowStart<30000?1500:3000);}
  async Task Cycle(){
   long epoch;CancellationToken token;Account saved;bool onlyInternet;
   lock(gate){epoch=generation;token=attempt.Token;saved=account;onlyInternet=result==AuthResult.Online && needInternet && stage=="internet" && clock.Milliseconds-lastPortalOnline<5000;stage=onlyInternet?"internet":"probe";}
   Notify();
   try{
    if(onlyInternet){await VerifyInternet(epoch,token).ConfigureAwait(false);return;}
    log.Record("probe",epoch,0,"begin");
    var observed=await transport.QueryAsync(ProbeBudget(),token).ConfigureAwait(false);
    lock(gate){if(!Live(epoch))return;}
    if(observed==PortalState.Online){await Online(epoch,token).ConfigureAwait(false);return;}
    if(observed==PortalState.Invalid){Apply(epoch,AuthResult.NetworkError);return;}
    bool slow;
    lock(gate){
     if(!Live(epoch))return;
     if(clock.Milliseconds-lastAuth<2000){due=lastAuth+2000;stage="waiting";return;}
     lastAuth=clock.Milliseconds;authenticating=true;internet=false;needInternet=true;stage="auth";slow=failures>0 && !slowUsed;if(slow)slowUsed=true;
    }
    Notify();log.Record("auth",epoch,0,"begin");var watch=clock.Milliseconds;
    var auth=await transport.AuthenticateAsync(saved,slow,token).ConfigureAwait(false);
    log.Record("auth",epoch,clock.Milliseconds-watch,auth.ToString());lock(gate){authenticating=false;if(!Live(epoch))return;}
    if(auth==AuthResult.Success || auth==AuthResult.Online || auth==AuthResult.NetworkError || auth==AuthResult.Unknown){
     // A submitted request may succeed even if its response is lost.
     observed=await transport.QueryAsync(ProbeBudget(),token).ConfigureAwait(false);
     lock(gate){if(!Live(epoch))return;}
     if(observed==PortalState.Online){await Online(epoch,token).ConfigureAwait(false);return;}
     if(auth==AuthResult.Success || auth==AuthResult.Online)auth=AuthResult.NetworkError;
    }
    Apply(epoch,auth);
   }catch(OperationCanceledException){}
   catch(InvalidDataException){Block(epoch,"engine");}
   catch(EngineStuckException){Block(epoch,"engine");}
   catch{Apply(epoch,AuthResult.NetworkError);}
  }
  async Task Online(long epoch,CancellationToken token){
   bool verify;lock(gate){if(!Live(epoch))return;result=AuthResult.Online;lastPortalOnline=clock.Milliseconds;failures=unknown=0;stage="online";due=clock.Milliseconds+60000;verify=needInternet;}
   log.Record("online",epoch,0,"Online");if(verify)await VerifyInternet(epoch,token).ConfigureAwait(false);
  }
  async Task VerifyInternet(long epoch,CancellationToken token){
   bool ok=await transport.CheckInternetAsync(token).ConfigureAwait(false);
   lock(gate){if(!Live(epoch))return;internet=ok;needInternet=!ok;stage=ok?"online":"internet";result=AuthResult.Online;
    if(ok){externalFailures=0;due=clock.Milliseconds+60000;}else{externalFailures++;due=clock.Milliseconds+(externalFailures==1?1000:externalFailures==2?2000:5000);}
   }log.Record("internet",epoch,0,ok?"ok":"failed");
  }
  void Apply(long epoch,AuthResult value){lock(gate){if(!Live(epoch))return;result=value;internet=false;needInternet=true;
   if(value==AuthResult.PasswordError || value==AuthResult.DeviceLimit || value==AuthResult.Rejected){blocked=true;prefs.Paused=true;Persist();stage="blocked";return;}
   if(value==AuthResult.Unknown && ++unknown>=3){blocked=true;prefs.Paused=true;Persist();stage="unknown";return;}
   failures++;long delay;if(clock.Milliseconds-windowStart<30000)delay=failures==1?500:failures==2?1000:2000;else{long[] backoff={5000,10000,30000,60000};delay=backoff[Math.Min(backoff.Length-1,Math.Max(0,failures-1))];}
   due=clock.Milliseconds+delay;stage="waiting";
  }}
  void Block(long epoch,string reason){lock(gate){if(!Live(epoch))return;blocked=true;prefs.Paused=true;Persist();stage=reason;log.Record("error",epoch,0,"failed");}}
  public void Dispose(){lock(gate){if(disposed)return;disposed=true;enabled=false;if(attempt!=null)attempt.Cancel();}network.Changed-=NetworkChanged;network.Dispose();wake.Set();worker.Join(2000);}
 }
}

