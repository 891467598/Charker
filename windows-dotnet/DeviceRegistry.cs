namespace Charker;
internal static class DeviceRegistry {
 public static string Display(SavedDevice d)=>string.IsNullOrWhiteSpace(d.Alias)?d.Name:d.Alias;
 public static bool Same(SavedDevice a,SavedDevice b)=>a.Model==b.Model&&(a.Id==b.Id||Addresses(a).Intersect(Addresses(b)).Any()||(!string.IsNullOrEmpty(a.SerialNumber)&&a.SerialNumber==b.SerialNumber)||(!string.IsNullOrEmpty(a.HardwareAddress)&&a.HardwareAddress==b.HardwareAddress));
 public static IEnumerable<string> Addresses(SavedDevice d)=>new[]{d.Id}.Concat(d.Addresses??[]).Distinct();
 public static SavedDevice FromState(DeviceState s)=>new(s.Id,s.Model,s.Name,null,s.SerialNumber,s.HardwareAddress);
 public static SavedDevice Remember(List<SavedDevice> devices,SavedDevice fresh){
  var matches=devices.Where(d=>Same(d,fresh)).ToArray();var addresses=matches.SelectMany(Addresses).Concat(Addresses(fresh)).Distinct().ToList();
  var result=fresh with{Alias=matches.LastOrDefault(d=>!string.IsNullOrWhiteSpace(d.Alias))?.Alias??fresh.Alias,SerialNumber=fresh.SerialNumber??matches.LastOrDefault(d=>d.SerialNumber!=null)?.SerialNumber,HardwareAddress=fresh.HardwareAddress??matches.LastOrDefault(d=>d.HardwareAddress!=null)?.HardwareAddress,Addresses=addresses};
  devices.RemoveAll(d=>matches.Contains(d));devices.Add(result);return result;
 }
 public static void Normalize(List<SavedDevice> devices){var old=devices.ToArray();devices.Clear();foreach(var d in old)Remember(devices,d);}
 public static (string? Serial,string? Mac) Identity(Reply reply){
  var sn=reply.Get(0xa4);string? serial=sn is {Length:>=8}&&sn.All(b=>b>=0x20&&b<0x7f)?Encoding.ASCII.GetString(sn).Trim():null;
  var bytes=reply.Get(0xa5);string? mac=bytes is {Length:>=6}&&!bytes.Take(6).All(b=>b>=0x20&&b<0x7f)?string.Join(":",bytes.Take(6).Select(b=>b.ToString("X2"))):null;
  return(serial,mac);
 }
}
