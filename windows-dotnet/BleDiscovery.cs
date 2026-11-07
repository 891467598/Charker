namespace Charker;
internal sealed class BleDiscovery {
 readonly object gate=new();readonly Dictionary<ulong,(BleCandidate Candidate,DateTimeOffset At)> seen=[];Dictionary<string,string> identities=[];HashSet<string> approved=[],blocked=[];
 public void Known(IEnumerable<SavedDevice> devices,IEnumerable<SavedDevice>? excluded=null){lock(gate){identities=[];approved=[];blocked=[];foreach(var d in devices.Concat(excluded??[]).Where(d=>d.Model=="A2687"))foreach(var address in DeviceRegistry.Addresses(d)){if(A2687Model.OtherSerial(d.SerialNumber))blocked.Add(address);else{identities[address]=d.SerialNumber??d.Id;if(A2687Model.ChargerSerial(d.SerialNumber))approved.Add(address);}}foreach(var key in seen.Keys.Where(k=>blocked.Contains(k.ToString())).ToArray())seen.Remove(key);}}
 public bool Approved(ulong address){lock(gate)return approved.Contains(address.ToString());}
 public void Clear(){lock(gate)seen.Clear();}
 public bool Contains(ulong address){lock(gate)return seen.ContainsKey(address);}
 public bool Observe(ulong address,string name,int rssi,bool connectable,bool scanResponse=false,DateTimeOffset? clock=null,bool modelMatch=true){lock(gate){
  if(address==0||blocked.Contains(address.ToString())||(!modelMatch&&!approved.Contains(address.ToString())&&!seen.ContainsKey(address))||(!connectable&&(!scanResponse||!seen.ContainsKey(address))))return false;
  var previous=seen.GetValueOrDefault(address).Candidate;var label=string.IsNullOrWhiteSpace(name)?previous?.Name??"Anker Prime 160W":name;
  seen[address]=(new(address,label,rssi),clock??DateTimeOffset.UtcNow);return true;
 }}
 public BleCandidate[] Current(DateTimeOffset? clock=null){lock(gate){var now=clock??DateTimeOffset.UtcNow;foreach(var key in seen.Where(p=>now-p.Value.At>TimeSpan.FromSeconds(15)).Select(p=>p.Key).ToArray())seen.Remove(key);
  return seen.Values.GroupBy(p=>identities.GetValueOrDefault(p.Candidate.Address.ToString(),p.Candidate.Address.ToString())).Select(g=>g.OrderByDescending(p=>p.At).First().Candidate).OrderByDescending(c=>c.Rssi).ToArray();
 }}
}
