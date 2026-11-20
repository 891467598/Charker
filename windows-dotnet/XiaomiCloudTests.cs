using System.Net;
namespace Charker;
internal static class XiaomiCloudTests {
 static JsonNode Data(object value)=>JsonSerializer.SerializeToNode(value)!;
 public static void Run(){var jar=new CookieContainer();using var cloud=new XiaomiCloud(jar,new Server(jar));var qr=Data(cloud.Start().GetAwaiter().GetResult());if(!qr["image"]!.ToString().StartsWith("data:image/png;base64,"))throw new Exception("QR missing");var pending=Data(cloud.Poll("cn").GetAwaiter().GetResult());if((bool?)pending["pending"]!=true)throw new Exception("Scan must wait for approval");var result=Data(cloud.Poll("cn").GetAwaiter().GetResult());if((bool?)result["pending"]!=false||result["devices"]?.AsArray().Count!=1||result.ToJsonString().Contains("token",StringComparison.OrdinalIgnoreCase)||cloud.Select("AA:BB:CC:DD:EE:01").Token!=new string('a',24))throw new Exception("Token import contract failed");}
 sealed class Server(CookieContainer jar) : HttpMessageHandler {
  int polls;readonly string secret=Convert.ToBase64String(Enumerable.Range(0,16).Select(i=>(byte)i).ToArray());
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct){string path=req.RequestUri!.AbsolutePath;object body;
   if(path=="/longPolling/loginUrl")body=new{qr="https://account.xiaomi.com/qr.png",lp="https://account.xiaomi.com/longPolling/test",timeout=300};
   else if(path=="/qr.png")return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent([1,2,3])});
   else if(path=="/longPolling/test")body=++polls==1?new{code=401}:new{code=0,ssecurity=secret,userId="synthetic-user",location="https://sts.api.io.mi.com/sts"} as object;
   else if(path=="/sts"){jar.Add(new Uri("https://api.io.mi.com"),new Cookie("serviceToken","synthetic-service","/",".io.mi.com"));body=new{code=0};}
   else if(path=="/app/home/device_list"){var query=req.RequestUri.Query.TrimStart('?').Split('&').Select(p=>p.Split('=',2)).ToDictionary(p=>Uri.UnescapeDataString(p[0]),p=>Uri.UnescapeDataString(p[1]));var signed=SHA256.HashData(Convert.FromBase64String(secret).Concat(Convert.FromBase64String(query["_nonce"])).ToArray());string plain=Encoding.UTF8.GetString(XiaomiCloud.Rc4(signed,Convert.FromBase64String(query["data"])));if(!plain.Contains("getVirtualModel")||!req.Headers.GetValues("Cookie").Single().Contains("synthetic-service"))throw new Exception("Bad Xiaomi API request");var list=new{code=0,result=new{list=new[]{new{model="njcuk.fitting.ad1204",mac="AA:BB:CC:DD:EE:01",name="Test Ultra",token=new string('a',24)},new{model="unrelated.device",mac="AA:BB:CC:DD:EE:02",name="Other",token=new string('b',24)}}}};return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(Convert.ToBase64String(XiaomiCloud.Rc4(signed,JsonSerializer.SerializeToUtf8Bytes(list))))});}
   else throw new Exception("Unexpected network request");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(body))});
  }
 }
}
