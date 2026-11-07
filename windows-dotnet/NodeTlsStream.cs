using System.Diagnostics;
namespace Charker;
/// <summary>Uses the bundled Node/OpenSSL TLS implementation, avoiding Schannel's ephemeral-PEM limitation.</summary>
public sealed class NodeTlsStream : Stream {
 readonly Process child;readonly Stream input,output;int disposed;string? failure;
 NodeTlsStream(Process child){this.child=child;input=child.StandardOutput.BaseStream;output=child.StandardInput.BaseStream;}
 public static async Task<NodeTlsStream> Connect(string host,string cert,string key,CancellationToken ct,int port=8883,string? testCa=null,string? testName=null){
  var start=StartInfo();var p=new Process{StartInfo=start,EnableRaisingEvents=true};var ready=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);NodeTlsStream? stream=null;
  p.ErrorDataReceived+=(_,e)=>{if(e.Data==null)return;try{var m=JsonNode.Parse(e.Data);if(m?["event"]?.ToString()=="ready")ready.TrySetResult();else if(m?["event"]?.ToString()=="error"){string code=m["code"]?.ToString()??"TLS_ERROR";if(stream!=null)stream.failure=code;ready.TrySetException(new IOException(Describe(code)));}}catch(JsonException){}};
  p.Exited+=(_,_)=>ready.TrySetException(new IOException("TLS 连接进程已退出。"));
  try{p.Start();stream=new(p);p.BeginErrorReadLine();var config=JsonSerializer.SerializeToUtf8Bytes(new{host,port,cert,key,ca=testCa,servername=testName});try{await stream.output.WriteAsync(config,ct);await stream.output.WriteAsync(new byte[]{10},ct);await stream.output.FlushAsync(ct);}finally{CryptographicOperations.ZeroMemory(config);}await ready.Task.WaitAsync(TimeSpan.FromSeconds(25),ct);return stream;}
  catch{if(stream!=null)stream.Dispose();else p.Dispose();throw;}
 }
 internal static ProcessStartInfo StartInfo(){string dir=Path.Combine(AppContext.BaseDirectory,"Bridge"),node=Path.Combine(dir,"node.exe"),helper=Path.Combine(dir,"anker-tls.mjs");if(!File.Exists(node)||!File.Exists(helper))throw new IOException("云端 TLS 运行库缺失，请使用完整程序文件夹。");var start=new ProcessStartInfo(node){WorkingDirectory=dir,UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardInputEncoding=new UTF8Encoding(false)};start.ArgumentList.Add(helper);return start;}
 public static string Describe(string code)=>code switch{
  "CERT_HAS_EXPIRED"=>"云端 TLS 证书已过期，请重新登录后再试（CERT_HAS_EXPIRED）。",
  "ERR_TLS_CERT_ALTNAME_INVALID"=>"云端服务器证书名称不匹配，连接已拒绝（ERR_TLS_CERT_ALTNAME_INVALID）。",
  "SELF_SIGNED_CERT_IN_CHAIN" or "UNABLE_TO_VERIFY_LEAF_SIGNATURE" or "UNABLE_TO_GET_ISSUER_CERT_LOCALLY"=>"无法验证云端服务器证书，请检查网络代理或证书链（"+code+"）。",
  "ECONNREFUSED" or "ETIMEDOUT" or "TLS_HANDSHAKE_TIMEOUT"=>"无法连接云端 MQTT 服务，请检查网络对 8883 端口的访问（"+code+"）。",
  _=>"云端 TLS 认证失败（"+code+"）。"
 };
 public override bool CanRead=>true;public override bool CanSeek=>false;public override bool CanWrite=>true;public override long Length=>throw new NotSupportedException();public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
 public override int Read(byte[] b,int o,int n)=>input.Read(b,o,n);
 public override async ValueTask<int> ReadAsync(Memory<byte> buffer,CancellationToken ct=default){int n=await input.ReadAsync(buffer,ct);if(n==0&&failure!=null)throw new IOException(Describe(failure));return n;}
 public override Task<int> ReadAsync(byte[] b,int o,int n,CancellationToken ct)=>ReadAsync(b.AsMemory(o,n),ct).AsTask();
 public override void Write(byte[] b,int o,int n)=>output.Write(b,o,n);
 public override ValueTask WriteAsync(ReadOnlyMemory<byte> b,CancellationToken ct=default)=>output.WriteAsync(b,ct);
 public override Task WriteAsync(byte[] b,int o,int n,CancellationToken ct)=>WriteAsync(b.AsMemory(o,n),ct).AsTask();
 public override void Flush()=>output.Flush();public override Task FlushAsync(CancellationToken ct)=>output.FlushAsync(ct);
 public override long Seek(long o,SeekOrigin s)=>throw new NotSupportedException();public override void SetLength(long n)=>throw new NotSupportedException();
 protected override void Dispose(bool disposing){if(Interlocked.Exchange(ref disposed,1)!=0)return;try{output.Dispose();input.Dispose();if(!child.HasExited)child.Kill(true);}catch(InvalidOperationException){}finally{child.Dispose();}base.Dispose(disposing);}
}
