using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using Microsoft.Win32;
namespace NwafuConnect {
 public sealed class NetworkSnapshot {
  public readonly string Fingerprint;public readonly bool CandidateReady;public readonly long ObservedMs;
  public NetworkSnapshot(string fingerprint,bool ready,long observed){Fingerprint=fingerprint;CandidateReady=ready;ObservedMs=observed;}
 }
 public interface INetworkObserver:IDisposable {event Action<NetworkSnapshot> Changed;NetworkSnapshot Read();void Start();}
 public sealed class NetworkObserver:INetworkObserver {
  readonly IClock clock;public event Action<NetworkSnapshot> Changed;
  public NetworkObserver(IClock clock){this.clock=clock;}
  public NetworkSnapshot Read(){
   var parts=new List<string>();
   try{foreach(var nic in NetworkInterface.GetAllNetworkInterfaces()){
    if(nic.OperationalStatus!=OperationalStatus.Up || nic.NetworkInterfaceType==NetworkInterfaceType.Loopback || nic.NetworkInterfaceType==NetworkInterfaceType.Tunnel)continue;
    var props=nic.GetIPProperties();var addresses=new List<string>();var routes=new List<string>();
    foreach(var a in props.UnicastAddresses)if(a.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork && !a.Address.ToString().StartsWith("169.254."))addresses.Add(a.Address.ToString());
    foreach(var g in props.GatewayAddresses)if(g.Address.AddressFamily==System.Net.Sockets.AddressFamily.InterNetwork && g.Address.ToString()!="0.0.0.0")routes.Add(g.Address.ToString());
    if(addresses.Count==0 || routes.Count==0)continue;addresses.Sort();routes.Sort();parts.Add(nic.Id+":"+String.Join(",",addresses)+":"+String.Join(",",routes));
   }}catch{}
   parts.Sort();return new NetworkSnapshot(String.Join("|",parts),parts.Count>0,clock.Milliseconds);
  }
  public void Start(){NetworkChange.NetworkAddressChanged+=OnChange;NetworkChange.NetworkAvailabilityChanged+=OnAvailability;SystemEvents.PowerModeChanged+=OnPower;}
  void OnChange(object s,EventArgs e){var h=Changed;if(h!=null)h(Read());}
  void OnAvailability(object s,NetworkAvailabilityEventArgs e){OnChange(s,e);}
  void OnPower(object s,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume)OnChange(s,e);}
  public void Dispose(){NetworkChange.NetworkAddressChanged-=OnChange;NetworkChange.NetworkAvailabilityChanged-=OnAvailability;SystemEvents.PowerModeChanged-=OnPower;}
 }
}

