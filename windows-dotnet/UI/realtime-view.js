'use strict';
// Display cadence is independent of transport, recording and port-control state.
class PowerRefreshGate {
 constructor(){this.reset();}
 reset(){this.key='';this.at=-Infinity;this.value=null;}
 read(device,seconds,now=performance.now()){
  const fresh=device.connected&&!device.stale;
  const interval=Number.isInteger(seconds)&&seconds>=1&&seconds<=10?seconds:0;
  const key=[device.model,device.id,device.demo,fresh,interval,device.ports.map(p=>[p.on,p.status,p.cableAttached].join('/')).join(',')].join(':');
  if(key!==this.key||!this.value||interval===0||now-this.at>=interval*1000){
   this.key=key;this.at=now;
   this.value={total:fresh?device.total:null,updated:device.updated,ports:device.ports.map(p=>({power:fresh?p.power:null,voltage:fresh?p.voltage:null,current:fresh?p.current:null}))};
  }
  return this.value;
 }
}
function portVisualState(device,port){
 if(!device.connected||device.stale)return {attached:false,charging:false,known:false};
 if(device.model==='A2687'&&!(port.on!==false&&port.power>.2&&(port.power>=.7||device.confirmedPorts?.[port.index]===true)))return {attached:false,charging:false,known:true};
 if(port.power>0&&port.power<=.2)return {attached:false,charging:false,known:true};
 const drawing=port.on!==false&&port.power>0.1;
 // A2687 cable capability can identify an idle cable; USB identity alone cannot.
 const attached=device.model==='A2345'?port.status===1:(port.cableAttached===true||drawing||(port.cableAttached==null&&port.status!==0&&port.voltage>3));
 return {attached,charging:attached&&drawing,known:device.model==='A2345'||port.cableAttached!=null||attached};
}
