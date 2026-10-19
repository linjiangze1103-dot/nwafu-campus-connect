using System;
using System.Threading;
using System.Threading.Tasks;
using NwafuConnect;
public sealed class FakeNetwork:INetworkObserver {
 public event Action<NetworkSnapshot> Changed;public NetworkSnapshot Value=new NetworkSnapshot("one",true,0);
 public NetworkSnapshot Read(){return Value;}public void Start(){}public void Dispose(){}
 public void Set(string id,bool ready,bool signal=true){Value=new NetworkSnapshot(id,ready,0);if(signal && Changed!=null)Changed(Value);}
}
public sealed class FakeTransport:IConnectionTransport {
 public int Queries,Auth,Internet,Concurrent,Maximum;public Func<CancellationToken,Task<PortalState>> Query=t=>Task.FromResult(PortalState.Offline);
 public Func<CancellationToken,Task<AuthResult>> Login=t=>Task.FromResult(AuthResult.Success);
 public Func<CancellationToken,Task<bool>> External=t=>Task.FromResult(true);
 public Task<PortalState> QueryAsync(TimeSpan b,CancellationToken t){Interlocked.Increment(ref Queries);return Query(t);}
 public async Task<AuthResult> AuthenticateAsync(Account a,bool slow,CancellationToken t){Interlocked.Increment(ref Auth);int n=Interlocked.Increment(ref Concurrent);Maximum=Math.Max(Maximum,n);try{return await Login(t);}finally{Interlocked.Decrement(ref Concurrent);}}
 public Task<bool> CheckInternetAsync(CancellationToken t){Interlocked.Increment(ref Internet);return External(t);}
}
public sealed class NullLog:IDiagnostics {public void Record(string s,long g,long e,string o){}}
