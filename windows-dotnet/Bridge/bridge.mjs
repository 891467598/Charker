// Local Matter adapter. All protocol traffic remains on this PC / local network.
import {config} from '@matter/nodejs/config';
import path from 'node:path';
import fs from 'node:fs';
import readline from 'node:readline';
config.defaultStoragePath=path.resolve(process.argv[2]);config.loadConfigFile=false;config.loadProcessArgv=false;
const {ServerNode,Environment,Logger}=await import('@matter/main');
const {OnOffPlugInUnitDevice,OnOffPlugInUnitRequirements}=await import('@matter/main/devices/on-off-plug-in-unit');
const {ElectricalPowerMeasurementServer,ElectricalEnergyMeasurementServer,PowerTopologyServer}=await import('@matter/main/behaviors');
const QRCode=(await import('qrcode')).default;
Logger.level='error';
const send=v=>process.stdout.write(JSON.stringify(v)+'\n');
let latest=null,allow=false,requestId=0,processing=Promise.resolve(),last=Date.now(),closing=false;const waiters=new Map(),nodes=[],endpoints=[],energies=[];
const model=['A2345','AD1204U'].includes(process.argv[3])?process.argv[3]:'A2687',count=model==='A2345'?6:model==='AD1204U'?4:3;
const acc=(measurementType,max)=>({measurementType,measured:false,minMeasuredValue:0,maxMeasuredValue:max,accuracyRanges:[{rangeMin:0,rangeMax:max,percentMax:500}]});
const energyFile=path.join(config.defaultStoragePath,'energy-'+model+'.json');let saved={};try{saved=JSON.parse(fs.readFileSync(energyFile,'utf8'));}catch{}
function control(port,on){if(!allow||!latest?.connected||latest?.stale||latest.model!==model)throw Error('Device control unavailable');if(port<0)throw Error('Total endpoint is read only');let id=String(++requestId);send({type:'control',id,port,on});return new Promise((resolve,reject)=>{let timer=setTimeout(()=>{waiters.delete(id);reject(Error('Charger did not confirm'));},15000);waiters.set(id,{resolve,reject,timer});});}
readline.createInterface({input:process.stdin}).on('line',line=>{try{const m=JSON.parse(line);if(m.type==='result'){let w=waiters.get(m.id);if(w){clearTimeout(w.timer);waiters.delete(m.id);m.ok?w.resolve():w.reject(Error(m.error||'Device rejected command'));}}else if(m.type==='update'){latest=m.device;allow=m.allow;processing=processing.then(update).catch(e=>send({type:'error',error:e.message}));}else if(m.type==='stop')shutdown();}catch{}}).on('close',()=>shutdown());
const power=ElectricalPowerMeasurementServer.with('DirectCurrent').enable({attributes:{voltage:true,activeCurrent:true}});
const energy=ElectricalEnergyMeasurementServer.with('ImportedEnergy','CumulativeEnergy');
const topology=PowerTopologyServer.with('NodeTopology');
const pairings=[];
try{
 for(let i=-1;i<count;i++){
  const index=i;class ChargerOnOff extends OnOffPlugInUnitRequirements.OnOffServer {
   async on(){await control(index,true);this.state.onOff=true;}
   async off(){await control(index,false);this.state.onOff=false;}
   async toggle(){await control(index,!this.state.onOff);this.state.onOff=!this.state.onOff;}
  }
  let name=(model==='AD1204U'?'CUKTECH ':'ANKER ')+model+' '+(i<0?'Total':model==='AD1204U'&&i===3?'A':i<4?'C'+(i+1):'A'+(i-3));
  const node=await ServerNode.create({id:'charker-'+model+'-'+(i+1),network:{port:5540+i+1},productDescription:{name},basicInformation:{vendorName:'ANKER',productName:name,nodeLabel:name,serialNumber:'ANKERWin-'+model+'-'+(i+1),softwareVersion:50131,softwareVersionString:'0.5.1-win.31'}});nodes.push(node);
  const endpoint=await node.add(OnOffPlugInUnitDevice.with(ChargerOnOff,power,energy,topology),{id:'meter',descriptor:{deviceTypeList:[{deviceType:0x10a,revision:3},{deviceType:0x510,revision:1}]},onOff:{onOff:true},electricalPowerMeasurement:{powerMode:2,numberOfMeasurementTypes:3,accuracy:[acc(1,60000),acc(2,10000),acc(5,300000)],voltage:0,activeCurrent:0,activePower:0},electricalEnergyMeasurement:{accuracy:acc(14,1e15),cumulativeEnergyImported:{energy:0}}});endpoints.push(endpoint);energies.push(saved[i+1]||0);
  await node.start();const codes=node.state.commissioning.pairingCodes;pairings.push({name,code:codes.manualPairingCode,qrImage:await QRCode.toDataURL(codes.qrPairingCode,{width:220,margin:2})});
 }
 send({type:'ready',status:'running',model,pairings});
 if(process.argv.includes('--test')){latest={connected:true,stale:false,model,ports:Array.from({length:count},(_,i)=>({power:i+1,voltage:20,current:(i+1)/20,on:true}))};await update();send({type:'test',ok:true,count:endpoints.length,totalPower:endpoints[0].state.electricalPowerMeasurement.activePower});await shutdown();}
}catch(e){send({type:'error',error:e.stack||e.message});await shutdown(1);}
async function update(){if(!latest||endpoints.length!==count+1)return;const now=Date.now(),dt=typeof last==='undefined'?0:Math.min(15,(now-last)/1000);last=now;const good=latest.connected&&!latest.stale&&latest.model===model;
 for(let j=0;j<endpoints.length;j++){const index=j-1,p=index<0?null:latest.ports[index];const w=good?(index<0?latest.ports.reduce((s,p)=>s+(p.on===false?0:p.power),0):p.on===false?0:p.power):0;energies[j]+=w*dt/3.6;await endpoints[j].set({onOff:{onOff:good&&(index<0||p.on!==false)},electricalPowerMeasurement:{activePower:Math.round(w*1000),voltage:good&&p?Math.round(p.voltage*1000):0,activeCurrent:good&&p?Math.round(p.current*1000):0},electricalEnergyMeasurement:{cumulativeEnergyImported:{energy:Math.round(energies[j])}}});}
 if(!good)return;fs.mkdirSync(config.defaultStoragePath,{recursive:true});fs.writeFileSync(energyFile,JSON.stringify(Object.fromEntries(energies.map((e,i)=>[i,e]))));
}
async function shutdown(code=0){if(closing)return;closing=true;for(const n of nodes.reverse()){try{await n.close();}catch{}}process.exit(code);}
process.on('SIGTERM',()=>shutdown());process.on('uncaughtException',e=>{send({type:'error',error:e.message});shutdown(1);});
