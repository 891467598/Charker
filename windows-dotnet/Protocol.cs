// Protocol port of qzz0518/Charker v0.5.1, MIT. All byte layouts follow the supplied source.
namespace Charker;
public record Field(byte Id, byte[] Value);
public record Frame(byte Slave, byte Group, ushort Command, byte[] Payload) {
 public ushort Opcode => (ushort)(Command & ~0x4800);
 public bool Encrypted => (Command & 0x4000)!=0;
}
public record Reply(byte? Status, List<Field> Fields) {
 public byte[]? Get(int id) => Fields.FirstOrDefault(f=>f.Id==id)?.Value;
 public byte[]? Body(int id) => Get(id) is {Length:>1} v ? v[1..] : null;
 public int? Byte(int id) {var v=Get(id);return v is {Length:1}?v[0]:v is {Length:2} && v[0]==1?v[1]:null;}
}
public static class Wire {
 public static Field F(int id, params byte[] value)=>new((byte)id,value);
 public static Field U8(int id,int v)=>F(id,1,checked((byte)v));
 public static Field Block(int id,byte[] v)=>F(id,new byte[]{4}.Concat(v).ToArray());
 public static byte[] Epoch(DateTimeOffset? at=null)=>BitConverter.GetBytes((uint)(at??DateTimeOffset.UtcNow).ToUnixTimeSeconds());
 public static byte[] Tlv(IEnumerable<Field> fs) {
  using var s=new MemoryStream();foreach(var f in fs){if(f.Value.Length>255)throw new InvalidDataException("TLV too long");s.WriteByte(f.Id);s.WriteByte((byte)f.Value.Length);s.Write(f.Value);}return s.ToArray();
 }
 public static List<Field> Fields(byte[] b,int start=0) {
  var f=new List<Field>();for(int i=start;i<b.Length;){if(i+2>b.Length || i+2+b[i+1]>b.Length)throw new InvalidDataException("Truncated TLV");int n=b[i+1];f.Add(F(b[i],b[(i+2)..(i+2+n)]));i+=2+n;}return f;
 }
 public static Reply Parse(byte[] b) {
  if(b.Length==0)return new(null,[]);
  if(b[0]==0){try{return new(0,Fields(b,1));}catch(InvalidDataException){}}
  try{return new(null,Fields(b));}catch(InvalidDataException){return new(b[0],Fields(b,1));}
 }
 public static byte[] Encode(ushort cmd,byte[] payload,byte group=15,byte slave=0) {
  int len=10+payload.Length;if(len>2048)throw new InvalidDataException("Frame too large");var b=new byte[len];b[0]=255;b[1]=9;b[2]=(byte)len;b[3]=(byte)(len>>8);b[4]=3;b[5]=slave;b[6]=group;b[7]=(byte)(cmd>>8);b[8]=(byte)cmd;payload.CopyTo(b,9);for(int i=0;i<len-1;i++)b[^1]^=b[i];return b;
 }
 public static Frame Decode(byte[] b) {
  if(b.Length<10 || b.Length>2048 || b[0]!=255 || b[1]!=9 || (b[2]|b[3]<<8)!=b.Length || b[4]!=3 || b.Aggregate((byte)0,(a,x)=>(byte)(a^x))!=0)throw new InvalidDataException("Invalid FF09 frame");
  return new(b[5],b[6],(ushort)(b[7]<<8|b[8]),b[9..^1]);
 }
 public static byte[] Session(IEnumerable<Field>? f=null,bool typedTimestamp=false) => Tlv(new[]{F(0xa1,0x21)}.Concat(f??[]).Append(F(0xfe,typedTimestamp?new byte[]{3}.Concat(Epoch()).ToArray():Epoch())));
 public static byte[] Cloud(ushort opcode,IEnumerable<Field>? values=null,DateTimeOffset? at=null,bool longFields=false) {
  using var s=new MemoryStream();s.Write(Tlv([F(0xa1,0x22)]));foreach(var f in (values??[]).OrderBy(x=>x.Id)){
   int len=f.Value.Length+1;if(len>255&&!longFields)throw new InvalidDataException("Long cloud field");s.WriteByte(f.Id);s.WriteByte((byte)len);if(longFields&&len>0x78)s.WriteByte((byte)(len>>8));s.WriteByte(f.Value.Length switch{1=>1,2=>2,4=>3,_=>4});s.Write(f.Value);
  }s.Write(Tlv([F(0xfe,new byte[]{3}.Concat(Epoch(at)).ToArray())]));return Encode(opcode,s.ToArray());
 }
 public static Reply CloudFields(Frame f) => new(null,Fields(f.Payload,f.Payload.Length>0&&f.Payload[0]<0xa0?1:0));
 public static uint Crc32(byte[] data) {uint c=0xffffffff;foreach(byte b in data){c^=b;for(int i=0;i<8;i++)c=(c&1)!=0?0xedb88320^(c>>1):c>>1;}return c^0xffffffff;}
 public static byte[] ExtractCloud(byte[] payload) {
  if(payload.Length>262144)throw new InvalidDataException("MQTT too large");
  if(payload.Length>=2&&payload[0]==255&&payload[1]==9){Decode(payload);return payload;}
  var found=new Dictionary<string,byte[]>();int nodes=0;
  void Walk(JsonNode? n,int depth,bool inspect=false){if(depth>8||++nodes>512)throw new InvalidDataException("MQTT nesting");
   if(n is JsonObject o){foreach(var kv in o)Walk(kv.Value,depth+1,kv.Key.Equals("data",StringComparison.OrdinalIgnoreCase)||kv.Key.Equals("payload",StringComparison.OrdinalIgnoreCase));}
   else if(n is JsonArray a){foreach(var x in a)Walk(x,depth+1,inspect);}
   else if(inspect&&n is JsonValue v&&v.TryGetValue<string>(out var text)){
    if(text.TrimStart().StartsWith('{')||text.TrimStart().StartsWith('[')){try{Walk(JsonNode.Parse(text),depth+1);}catch(JsonException){}}else if(text.Length<=2732){try{var b=Convert.FromBase64String(text);Decode(b);found[Convert.ToHexString(b)]=b;}catch(FormatException){}catch(InvalidDataException){}}
   }
  }Walk(JsonNode.Parse(payload),0);if(found.Count!=1)throw new InvalidDataException("Missing or ambiguous cloud frame");return found.Values.Single();
 }
}
public class Reassembler {
 readonly List<byte> buffer=[];
 public List<Frame> Append(byte[] chunk){buffer.AddRange(chunk);var frames=new List<Frame>();while(buffer.Count>=4){if(buffer[0]!=255||buffer[1]!=9){buffer.RemoveAt(0);continue;}int n=buffer[2]|buffer[3]<<8;if(n<10||n>2048){buffer.RemoveAt(0);continue;}if(buffer.Count<n)break;var b=buffer.GetRange(0,n).ToArray();buffer.RemoveRange(0,n);try{frames.Add(Wire.Decode(b));}catch(InvalidDataException){}}if(buffer.Count>4096)buffer.Clear();return frames;}
}
public sealed class SessionCrypto : IDisposable {
 public static readonly byte[] Aad=Convert.FromHexString("3322110077665544BBAA9988FFEEDDCC");
 byte[] key=Convert.FromHexString("B8FF7422955D4EB6D554A2C470280559");
 byte[] nonce=Convert.FromHexString("6BA3E3F2F3A60F2971CE5D1F");
 public void Derive(ECDiffieHellman own,byte[] point){if(point.Length!=64)throw new CryptographicException("Invalid P256 key");using var peer=ECDiffieHellman.Create(new ECParameters{Curve=ECCurve.NamedCurves.nistP256,Q=new ECPoint{X=point[..32],Y=point[32..]}});var s=own.DeriveRawSecretAgreement(peer.PublicKey);CryptographicOperations.ZeroMemory(key);key=s[..16];nonce=s[16..28];CryptographicOperations.ZeroMemory(s);}
 public byte[] Seal(byte[] p){byte[] c=new byte[p.Length],t=new byte[16];using var g=new AesGcm(key,16);g.Encrypt(nonce,p,c,t,Aad);return c.Concat(t).ToArray();}
 public byte[] Open(byte[] b){if(b.Length<=16)throw new CryptographicException("Short GCM");var p=new byte[b.Length-16];using var g=new AesGcm(key,16);g.Decrypt(nonce,b[..^16],b[^16..],p,Aad);return p;}
 public void Dispose(){CryptographicOperations.ZeroMemory(key);CryptographicOperations.ZeroMemory(nonce);}
}
