// TLS byte relay. Client key/certificate arrive only through stdin and stay in memory.
// stdout contains binary MQTT only; stderr contains bounded, credential-free status JSON.
import tls from 'node:tls';
const status=v=>process.stderr.write(JSON.stringify(v)+'\n');
let input=Buffer.alloc(0),initialized=false,socket,server;
const fail=e=>{status({event:'error',code:/^[A-Z0-9_]+$/.test(e.code||'')?e.code:'TLS_ERROR'});socket?.destroy();server?.close();process.exitCode=1;process.stdin.destroy();};
process.stdin.on('data',function initialize(chunk){
 if(initialized)return;
 input=Buffer.concat([input,chunk]);const end=input.indexOf(10);
 if(input.length>131072){fail({code:'INVALID_TLS_CONFIG'});return;}
 if(end<0)return;initialized=true;process.stdin.pause();process.stdin.removeListener('data',initialize);
 try{
  const cfg=JSON.parse(input.subarray(0,end).toString('utf8')),rest=input.subarray(end+1);input=Buffer.alloc(0);
  if(process.argv.includes('--test-server')){
   // Regression tests bind loopback only and use generated test credentials.
   server=tls.createServer({key:cfg.key,cert:cfg.cert,ca:cfg.ca,requestCert:true,rejectUnauthorized:true,minVersion:'TLSv1.2'},peer=>{status({event:'peer',authorized:peer.authorized});peer.on('error',()=>{});peer.pipe(peer);});
   server.on('tlsClientError',()=>{});server.on('error',fail);server.listen(0,'127.0.0.1',()=>status({event:'listening',port:server.address().port}));process.stdin.on('end',()=>server.close());process.stdin.resume();return;
  }
  if(!/^[A-Za-z0-9.-]+$/.test(cfg.host)||!Number.isInteger(cfg.port)||cfg.port<1||cfg.port>65535)throw Error();
  socket=tls.connect({host:cfg.host,port:cfg.port,servername:cfg.servername||cfg.host,key:cfg.key,cert:cfg.cert,ca:cfg.ca||undefined,minVersion:'TLSv1.2',rejectUnauthorized:true});
  socket.setTimeout(20000,()=>fail({code:'TLS_HANDSHAKE_TIMEOUT'}));socket.once('secureConnect',()=>{socket.setTimeout(0);socket.setNoDelay(true);status({event:'ready',protocol:socket.getProtocol()});if(rest.length)socket.write(rest);process.stdin.pipe(socket);socket.pipe(process.stdout);process.stdin.resume();});
  socket.on('error',fail);socket.on('close',()=>{process.stdin.destroy();});process.stdin.on('end',()=>socket.end());
 }catch{fail({code:'INVALID_TLS_CONFIG'});}
});
process.stdin.on('error',()=>socket?.destroy());process.stdout.on('error',()=>socket?.destroy());
