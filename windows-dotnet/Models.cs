namespace Charker;
public class PortReading {
 public int Index {get;set;} public string Name {get;set;}="";public int Status {get;set;}
 public double Voltage {get;set;} public double Current {get;set;} public double Power {get;set;}
 public bool? On {get;set;} public bool? CableAttached {get;set;} public string? Cable {get;set;} public string? Protocol {get;set;} public string? Usb {get;set;}
 public uint? Countdown {get;set;} public byte[]? ScheduleOn {get;set;} public byte[]? ScheduleOff {get;set;} public int? Priority {get;set;}
}
public class DeviceState {
 [System.Text.Json.Serialization.JsonIgnore] public object Sync {get;}=new();
 public string Model {get;set;}="A2687";public string Id {get;set;}="";public string Name {get;set;}="Anker Prime";
 public string? SerialNumber {get;set;}public string? HardwareAddress {get;set;}public string? Alias {get;set;}
 public bool Connected {get;set;} public bool Demo {get;set;} public string Status {get;set;}="未连接";
 public DateTimeOffset? Updated {get;set;}public string? Firmware {get;set;} public List<PortReading> Ports {get;set;}=[];
 public Dictionary<string,object?> Settings {get;set;}=[];
 public double Total=>Ports.Where(p=>p.On!=false).Sum(p=>p.Power);
 public bool Stale=>Updated is null || DateTimeOffset.UtcNow-Updated>TimeSpan.FromSeconds(12);
 public static DeviceState Empty(string model,string id="",bool demo=false)=> new(){Model=model,Id=id,Demo=demo,Name="Anker Prime "+(model=="A2687"?"160W":"250W"),Ports=Enumerable.Range(0,model=="A2687"?3:6).Select(i=>new PortReading{Index=i,Name=i<4?"C"+(i+1):"A"+(i-3)}).ToList()};
}
public static class Telemetry {
 static ushort U16(byte[] b,int i)=>BitConverter.ToUInt16(b,i);
 static string Cable(int c)=>c switch{0=>"3A · 60W",1=>"5A · 100W",2=>"EPR · 240W",_=>""};
 static string Profile(int c)=>c switch{1=>"Apple PD",2=>"Samsung Fast",3=>"Samsung Super Fast",5=>"Huawei SCP",6=>"Xiaomi HyperCharge",_=>""};
 public static bool Apply(DeviceState state,ushort opcode,Reply r) {lock(state.Sync)return ApplyCore(state,opcode,r);}
 static bool ApplyCore(DeviceState state,ushort opcode,Reply r) {
  bool cloud=state.Model=="A2345",ports=false;
  if(cloud&&(opcode==0x303||opcode==0xa00)){int first=opcode==0x303?0xa2:0xa4;for(int i=0;i<6;i++){var matches=r.Fields.Where(f=>f.Id==first+i).ToList();if(matches.Count!=1||matches[0].Value.Length<8||matches[0].Value[0]!=4)throw new InvalidDataException("Incomplete A2345 telemetry snapshot");}}
  if(cloud && opcode!=0x303 && opcode!=0xa00){UpdateCloud(state,opcode,r);return false;}
  if(!cloud && opcode!=0x200&&opcode!=0x300&&opcode!=0x20a&&opcode!=0x20b)return false;
  foreach(var p in state.Ports){int tag=cloud?(opcode==0x303?0xa2:0xa4)+p.Index:0xa5+p.Index;var raw=r.Get(tag);if(raw is not {Length:>=8}||raw[0]!=4)continue;
   byte[] b=raw[1..];p.Status=b[0];p.Voltage=U16(b,1)/1000.0;p.Current=U16(b,3)/1000.0;p.Power=U16(b,5)/100.0;ports=true;if(cloud?b[0]!=1:b[0]==0)p.Power=0;
   if(cloud){if(b.Length>8){p.Cable=b[0]==1&&p.Index<4?b[7] switch{3=>"Huawei",4=>"Xiaomi",_=>Cable(b[7])}:null;p.Protocol=b[0]==1?b[8] switch{1=>"Huawei SCP",2=>"Xiaomi HyperCharge",_=>""}:null;}}
   else if(r.Body(0xac+p.Index) is {Length:>=12} c){p.On=c[0]==1;p.Countdown=c[1]==1?BitConverter.ToUInt32(c,6):null;p.Cable=Cable(c[10]);p.CableAttached=c[10]<=2?true:c[10]==3?false:null;p.Protocol=Profile(c[11]);}
  }
  if(cloud&&opcode==0xa00){
   for(int i=0;i<5;i++){var b=r.Body(0xaa+i);if(b is not {Length:>=19})continue;foreach(var p in state.Ports.Where(p=>Math.Min(p.Index,4)==i)){p.On=b[0]==1;p.ScheduleOn=b[1..5];p.ScheduleOff=b[5..9];p.Countdown=b[9]==1?BitConverter.ToUInt32(b,14):null;p.Priority=b[18];}}
   Map(state,r,0xb0,"timeout");Map(state,r,0xb1,"mode");Map(state,r,0xb3,"brightness");Map(state,r,0xb4,"knob");Map(state,r,0xb5,"hour");Map(state,r,0xbb,"compatibility",true);Map(state,r,0xbc,"ufcs");Map(state,r,0xbf,"identify");
   if(r.Body(0xb7) is {Length:>=2} cap)state.Settings["capabilities"]=U16(cap,0);
   if(r.Body(0xb8) is {} cp)state.Settings["custom"]=cp.Select(x=>(int)x).ToArray();
   if(r.Body(0xb9) is {} cs)state.Settings["clockSchedule"]=cs.Select(x=>(int)x).ToArray();
   if(r.Body(0xa2) is {} fw && fw.Length<=4){uint n=0;for(int i=0;i<fw.Length;i++)n|=(uint)fw[i]<<(8*i);state.Firmware=string.Join('.',n.ToString().ToCharArray());}
   if(r.Body(0xa3) is {Length:>=1} err)state.Settings["error"]=err.Length>=2?U16(err,0):err[0];
  } else if(!cloud){Map(state,r,0xa8,"timeout");Map(state,r,0xa9,"brightness");Map(state,r,0xaa,"mode");Map(state,r,0xaf,"orientation");Map(state,r,0xb2,"gyro");}
  var ids=r.Get(cloud?0xbd:0xb4);if(ids!=null){if(cloud||ids.Length==13&&ids[0]==4)ids=ids[1..];for(int i=0;i<(cloud?4:3)&&i*4+4<=ids.Length;i++){var a=U16(ids,i*4);var b=U16(ids,i*4+2);state.Ports[i].Usb=a==0xfffa&&b==0xfffb?null:$"{a:X4}:{b:X4}";}}
  if(ports){state.Updated=DateTimeOffset.UtcNow;state.Status="已连接";}return ports;
 }
 static void Map(DeviceState s,Reply r,int id,string key,bool inverse=false){var v=r.Get(id);if(v is {Length:1})s.Settings[key]=inverse?1-v[0]:v[0];else if(v is {Length:2})s.Settings[key]=inverse?1-v[1]:v[1];}
 static void UpdateCloud(DeviceState s,ushort op,Reply r){int? n=r.Body(0xa2)?.FirstOrDefault();switch(op){case 0x302:if(n is >=0 and <=4&&r.Body(0xa3) is {Length:>0} v)foreach(var p in s.Ports.Where(p=>Math.Min(p.Index,4)==n))p.On=v[0]==1;break;case 0x309:Map(s,r,0xa2,"timeout");break;case 0x30a:Map(s,r,0xa2,"brightness");break;case 0x30d:Map(s,r,0xa2,"mode");break;case 0x310:Map(s,r,0xa2,"knob");break;case 0x30b:case 0xa02:if(n!=null)s.Settings["clockFlags"]=n;break;}}
}
public class Preferences {
 public bool StartWithWindows {get;set;}
 public List<SavedDevice> ExcludedBluetoothDevices {get;set;}=[];
 public bool DesktopWidgetEnabled {get;set;} public bool DesktopWidgetLocked {get;set;} public bool DesktopWidgetTopmost {get;set;} public double? DesktopWidgetLeft {get;set;} public double? DesktopWidgetTop {get;set;}
 public int PowerRefreshSeconds {get;set;}
 public bool AllowPortControl {get;set;}public bool AllowExperimental {get;set;} public bool CloseToTray {get;set;}=true;
 public string Language {get;set;}="zh";public string OwnerId {get;set;}="";public string Country {get;set;}="CN";
 public double Tariff {get;set;}=0.6; public string Currency {get;set;}="CNY"; public string TrayTemplate {get;set;}="{total} W";
 public List<SavedDevice> Devices {get;set;}=[];
}
public record SavedDevice(string Id,string Model,string Name,string? Alias=null,string? SerialNumber=null,string? HardwareAddress=null,List<string>? Addresses=null);
public record EnergyPoint(DateTimeOffset At,string Device,string Model,double[] Watts);
public static class Store {
 public static string Root {get;}=Environment.GetEnvironmentVariable("CHARKER_DATA_DIRECTORY")??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Charker.Windows");
 public static JsonSerializerOptions Json {get;}=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=false};
 public static T? Read<T>(string name){try{return JsonSerializer.Deserialize<T>(File.ReadAllText(Path.Combine(Root,name)),Json);}catch{return default;}}
 public static void Write(string name,object value){Directory.CreateDirectory(Root);string p=Path.Combine(Root,name);File.WriteAllText(p+".tmp",JsonSerializer.Serialize(value,Json));File.Move(p+".tmp",p,true);}
}
public class EnergyHistory {
 readonly object gate=new();readonly Dictionary<string,DateTimeOffset> savedAt=[];
 public void Reset(){lock(gate)savedAt.Clear();}
 public void Record(DeviceState s){lock(gate){if(s.Demo||!s.Connected||s.Stale||s.Updated==savedAt.GetValueOrDefault(s.Id))return;var now=new EnergyPoint(s.Updated!.Value,s.Id,s.Model,s.Ports.Select(p=>p.On==false?0:p.Power).ToArray());Directory.CreateDirectory(Path.Combine(Store.Root,"history"));File.AppendAllText(Path.Combine(Store.Root,"history",now.At.LocalDateTime.ToString("yyyy-MM-dd")+".jsonl"),JsonSerializer.Serialize(now,Store.Json)+Environment.NewLine);savedAt[s.Id]=now.At;}}
 public List<EnergyPoint> Read(DateTimeOffset start,DateTimeOffset end,string? device=null){var result=new List<EnergyPoint>();string dir=Path.Combine(Store.Root,"history");if(!Directory.Exists(dir))return result;lock(gate){foreach(var file in Directory.EnumerateFiles(dir,"*.jsonl").Order()){if(DateTime.TryParse(Path.GetFileNameWithoutExtension(file),out var d)&& (d.Date>end.LocalDateTime.Date||d.Date<start.LocalDateTime.Date.AddDays(-1)))continue;foreach(var line in File.ReadLines(file)){try{var p=JsonSerializer.Deserialize<EnergyPoint>(line,Store.Json);if(p!=null&&p.At>=start&&p.At<=end&&(device==null||p.Device==device))result.Add(p);}catch(JsonException){}}}}return result.OrderBy(p=>p.At).ToList();}
 public static double Kwh(IEnumerable<EnergyPoint> points){double wh=0;foreach(var group in points.GroupBy(p=>p.Device)){EnergyPoint? prev=null;foreach(var p in group.OrderBy(p=>p.At)){if(prev!=null){double dt=(p.At-prev.At).TotalSeconds;if(dt>0&&dt<=15)wh+=(p.Watts.Sum()+prev.Watts.Sum())/2*dt/3600;}prev=p;}}return wh/1000;}
}
