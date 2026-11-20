import * as THREE from 'three';
import {GLTFLoader} from './vendor/GLTFLoader.js';
import {DRACOLoader} from './vendor/DRACOLoader.js';
import {OrbitControls} from './vendor/OrbitControls.js';
import {HDRLoader} from './vendor/HDRLoader.js';
import {createCuktechModel} from './cuktech-model.js';
import {createConnector} from './connectors.js';
import {calibrateA2345,fitA2345Screen} from './a2345-fit.js';
const stage=document.getElementById('modelStage'),portLegend=document.getElementById('modelPorts');
const palette=[0x347fdf,0x46baa6,0xdbab4b,0xa68ade,0xe58fa3,0x7f9cb9];
let renderer,scene,camera,controls,object,model='A2687',generation=0,dirty=true,latest=null,rigs=[],lastFrame=0,screenTexture=null,screenCanvas=null,screenKey='';
let socketCalibration=null,screenFit=null,cuktechGeometry=null;
const reduced=matchMedia('(prefers-reduced-motion: reduce)');
// Original GLB coordinates, before centering or scaling; A2687 anchors match upstream.
const specs={A2687:[
 {p:[.00556,.01091,.03208],n:[.053,.003,.999],end:[-.013,.032,.066]},
 {p:[.00564,-.00128,.03211],n:[.053,.003,.999],end:[.007,.025,.069]},
 {p:[.00597,-.01396,.03214],n:[.054,.004,.999],end:[.027,.018,.067]}
],A2345:[],AD1204U:[]};
function dispose(root){root?.traverse(x=>{x.geometry?.dispose();if(x.material)for(const m of Array.isArray(x.material)?x.material:[x.material]){for(const v of Object.values(m))if(v?.isTexture)v.dispose();m.dispose();}});}
function cable(spec,index,m){
 const unit=m==='A2345'?.1:1,root=new THREE.Group(),normal=new THREE.Vector3(...spec.n).normalize();
 root.position.fromArray(spec.p);root.quaternion.setFromUnitVectors(new THREE.Vector3(0,0,1),normal);
 const connector=createConnector(spec.usbA?'USB-A':'USB-C',unit,palette[index]);root.add(connector.root);
 const inv=root.quaternion.clone().invert(),end=new THREE.Vector3(...spec.end).sub(root.position).applyQuaternion(inv),down=new THREE.Vector3(...(m==='A2687'?[0,0,1]:[0,-1,0])).applyQuaternion(inv),start=new THREE.Vector3(0,0,connector.cableStart);
 const curve=new THREE.CubicBezierCurve3(start,start.clone().add(new THREE.Vector3(0,0,.013*unit)).addScaledVector(down,.004*unit),end.clone().addScaledVector(down,-.012*unit),end);
 root.add(new THREE.Mesh(new THREE.TubeGeometry(curve,40,connector.wireRadius,10,false),connector.rubber));
 const flow=[];for(let j=0;j<3;j++){const dot=new THREE.Mesh(new THREE.SphereGeometry(connector.wireRadius*1.16,8,6),new THREE.MeshBasicMaterial({color:palette[index]}));root.add(dot);flow.push(dot);}
 root.visible=false;root.scale.setScalar(.001);object.add(root);return {root,ring:connector.ring,glow:connector.glow,flow,curve,amount:0,target:0,charging:false,index,connectorType:connector.type};
}
function installScreen(m){
 screenCanvas=document.createElement('canvas');screenCanvas.width=m==='AD1204U'?400:768;screenCanvas.height=m==='AD1204U'?640:288;screenTexture=new THREE.CanvasTexture(screenCanvas);screenTexture.colorSpace=THREE.SRGBColorSpace;screenTexture.flipY=false;screenKey='';
 const material=new THREE.MeshBasicMaterial({map:screenTexture,side:THREE.DoubleSide,toneMapped:false});
 if(m==='AD1204U'){screenTexture.flipY=true;const artwork=object.getObjectByName('CuktechScreen');artwork.material.dispose();artwork.material=material;}else if(m==='A2687'){const artwork=object.getObjectByName('资源_9@4x')||object.getObjectByName('资源 9@4x');if(artwork){artwork.material.dispose();artwork.material=material;artwork.scale.multiplyScalar(.56);}else{material.dispose();}}
 else{screenFit=fitA2345Screen(object,material);}
}
function paintScreen(){
 if(!screenTexture||!latest)return;const value=latest.device.connected&&!latest.device.stale?latest.readings.total:null,key=value==null?'—':value.toFixed(1);if(model==='AD1204U'){const signature=key+latest.readings.ports.map(p=>p.power).join(',');if(signature===screenKey)return;screenKey=signature;stage.dataset.screenWatts=key;const c=screenCanvas.getContext('2d');c.fillStyle='#050a10';c.fillRect(0,0,400,640);c.textAlign='left';c.fillStyle='#f2f7fa';c.font='500 78px Segoe UI';c.fillText(key,16,94,270);c.font='28px Segoe UI';c.fillStyle='#86a5b5';c.fillText('W',292,92);c.font='600 20px Segoe UI';c.fillText('CUKTECH',16,128);latest.device.ports.forEach((p,i)=>{const x=14+(i%2)*190,y=154+Math.floor(i/2)*225,reading=latest.readings.ports[i],color=['#dc8c36','#409cbd','#409cbd','#afa45a'][i];c.fillStyle=color;c.fillRect(x,y,174,37);c.fillStyle='#061019';c.textAlign='center';c.font='600 26px Segoe UI';c.fillText(p.name,x+87,y+28);c.textAlign='left';c.fillStyle='#ddf1ff';c.font='500 46px Segoe UI';c.fillText(reading?.power==null?'—':reading.power.toFixed(1),x+6,y+105,128);c.fillStyle='#92a5b4';c.font='19px Segoe UI';c.fillText('W',x+143,y+104);c.font='21px Segoe UI';c.fillText(reading?.voltage==null?'— V':reading.voltage.toFixed(1)+' V',x+6,y+150);c.fillText(reading?.current==null?'— A':reading.current.toFixed(2)+' A',x+6,y+181);});screenTexture.needsUpdate=true;return;}if(key===screenKey)return;screenKey=key;stage.dataset.screenWatts=key;
 const c=screenCanvas.getContext('2d');c.fillStyle='#05080c';c.fillRect(0,0,768,288);c.textAlign='center';c.fillStyle='#1ac7ea';c.font='italic 600 63px Segoe UI';c.fillText('ANKER',384,88);c.font='26px Segoe UI';c.fillText('P R I M E',384,128);c.fillStyle='#e8f7ff';c.font='500 89px Segoe UI';c.fillText(key+(value==null?'':' W'),384,237);screenTexture.needsUpdate=true;
}
function update(){
 if(!latest||latest.device.model!==model)return;const {device,readings,language}=latest,en=language==='en',states=device.ports.map(p=>portVisualState(device,p));
 if(portLegend.dataset.model!==model){portLegend.dataset.model=model;portLegend.replaceChildren(...device.ports.map(p=>{const e=document.createElement('span');e.className='model-port';e.dataset.port=p.index;e.append(document.createElement('i'),document.createElement('b'),document.createElement('small'));return e;}));}
 states.forEach((s,i)=>{
  const r=rigs[i];if(r){r.target=s.attached?1:0;r.charging=s.charging;r.ring.visible=s.charging;r.flow.forEach(x=>x.visible=s.charging);if(!s.known){r.amount=0;r.root.visible=false;}r.glow.emissiveIntensity=s.charging?1.5:0;}
  const e=portLegend.children[i],p=device.ports[i],value=readings.ports[i]?.power;e.classList.toggle('attached',s.attached);e.classList.toggle('charging',s.charging);e.style.setProperty('--port-color','#'+palette[i].toString(16).padStart(6,'0'));e.querySelector('b').textContent=p.name;
  const text=s.attached?(value==null?'—':(p.on===false?0:value).toFixed(1))+' W':s.known?(en?'Empty':'未接入'):'—',small=e.querySelector('small');if(small.textContent!==text)small.textContent=text;
  e.title=p.name+' · '+(s.charging?(en?'Supplying power':'正在供电'):s.attached?(en?'Connected / idle':'已接入 / 待机'):s.known?(en?'Not connected':'未接入'):(en?'Waiting for device report':'等待设备上报'));
 });stage.dataset.attached=states.map(s=>s.attached?'1':'0').join('');stage.dataset.charging=states.map(s=>s.charging?'1':'0').join('');paintScreen();dirty=true;
}
window.addEventListener('charger-telemetry',e=>{latest=e.detail;update();});
try{
 renderer=new THREE.WebGLRenderer({alpha:true,antialias:true});renderer.setPixelRatio(Math.min(devicePixelRatio,2));renderer.outputColorSpace=THREE.SRGBColorSpace;renderer.toneMapping=THREE.ACESFilmicToneMapping;renderer.toneMappingExposure=1.12;stage.appendChild(renderer.domElement);
 scene=new THREE.Scene();camera=new THREE.PerspectiveCamera(32,1,.01,100);controls=new OrbitControls(camera,renderer.domElement);controls.enableDamping=true;controls.enablePan=false;controls.minDistance=2;controls.maxDistance=8;controls.addEventListener('change',()=>dirty=true);
 scene.add(new THREE.HemisphereLight(0xe4f1ff,0x67666b,2.4));const key=new THREE.DirectionalLight(0xffffff,3.8);key.position.set(3,5,4);scene.add(key);const fill=new THREE.DirectionalLight(0xc2d9ff,1.5);fill.position.set(-3,2,-3);scene.add(fill);
 const pmrem=new THREE.PMREMGenerator(renderer);new HDRLoader().load('./assets/A2687.hdr',texture=>{scene.environment=pmrem.fromEquirectangular(texture).texture;scene.environmentIntensity=.65;texture.dispose();pmrem.dispose();dirty=true;});
 const draco=new DRACOLoader();draco.setDecoderPath('./vendor/draco/');const loader=new GLTFLoader();loader.setDRACOLoader(draco);
 async function load(m){
  model=m;const id=++generation;stage.dataset.ready='';delete stage.dataset.error;document.getElementById('modelImage').src='assets/'+m+(m==='AD1204U'?'.svg':m==='A2687'?'.webp':'.png');
  if(object){scene.remove(object);dispose(object);object=null;}rigs=[];socketCalibration=null;screenFit=null;cuktechGeometry=null;screenTexture=null;portLegend.replaceChildren();delete portLegend.dataset.model;stage.querySelector('.model-fallback').style.display='flex';
  try{
   const cuk=m==='AD1204U'?createCuktechModel():null;if(cuk){specs.AD1204U=cuk.ports;cuktechGeometry=cuk;}const gltf=cuk?{scene:cuk.object}:await loader.loadAsync('./assets/'+m+'.glb');if(id!==generation){dispose(gltf.scene);return;}object=gltf.scene;
   // Source contains a sample connector unrelated to the live cable population.
   if(m==='A2687')for(const name of ['group2_polySurface41','group3_polySurface121']){const sample=object.getObjectByName(name);if(sample)sample.visible=false;}
   const bounds=new THREE.Box3().setFromObject(object),size=bounds.getSize(new THREE.Vector3()),center=bounds.getCenter(new THREE.Vector3());if(m==='A2345'){socketCalibration=calibrateA2345(object);const ends=[[-.0058,-.0008,.0138],[-.0022,-.0011,.0144],[.0017,-.0011,.0144],[.0054,-.0008,.0138],[.0115,-.0004,.0048],[.0119,-.0005,.0005]];specs.A2345=socketCalibration.ports.map((p,i)=>({...p,end:ends[i]}));}rigs=specs[m].map((spec,i)=>cable(spec,i,m));stage.dataset.connectors=rigs.map(r=>r.connectorType).join(',');installScreen(m);
   const scale=1.8/Math.max(size.x,size.y,size.z);if(m==='A2687')object.rotation.x=Math.PI/2;object.position.copy(center).applyQuaternion(object.quaternion).negate().multiplyScalar(scale);object.scale.setScalar(scale);scene.add(object);
   camera.up.set(0,1,0);camera.position.set(m==='A2687'?2.6:m==='AD1204U'?2.8:2.7,m==='A2687'?-2.1:m==='AD1204U'?-2.0:2.5,m==='A2687'?4.7:m==='AD1204U'?4.4:4.0);controls.target.set(0,m==='A2687'?-.24:m==='AD1204U'?-.52:-.06,.15);controls.update();stage.querySelector('.model-fallback').style.display='none';stage.dataset.ready=m;dirty=true;update();
  }catch(e){stage.dataset.error=e.message;stage.querySelector('.model-fallback').style.display='flex';console.warn('Model load failed:',e.message);}
 }
 function resize(){const r=stage.getBoundingClientRect();if(!r.width||!r.height)return;renderer.setSize(r.width,r.height);camera.aspect=r.width/r.height;camera.updateProjectionMatrix();dirty=true;}new ResizeObserver(resize).observe(stage);resize();load(model);
 window.addEventListener('charger-model',e=>{if(e.detail!==model)load(e.detail);});
 renderer.setAnimationLoop(now=>{
  if(!stage.offsetParent||document.hidden){lastFrame=now;return;}const dt=Math.min(.1,(now-lastFrame)/1000);let moving=false;lastFrame=now;
  for(const r of rigs){if(Math.abs(r.amount-r.target)>.002){r.amount=reduced.matches?r.target:THREE.MathUtils.damp(r.amount,r.target,17,dt);dirty=true;moving=true;}else r.amount=r.target;r.root.visible=r.amount>.002;r.root.scale.setScalar(Math.max(.001,r.amount));if(r.charging&&r.root.visible&&!reduced.matches){r.flow.forEach((dot,j)=>dot.position.copy(r.curve.getPoint((now*.00024+j/3)%1)));moving=true;}}
  const changed=controls.update();if(changed||dirty||moving){renderer.render(scene,camera);dirty=false;}
 });
}catch(e){stage.dataset.error=e.message;console.warn('3D unavailable; displaying product image.');}

// Inspection hooks used by the isolated native --ui-test driver.
export function inspectA2345(view){
 if(model!=='A2345'||!socketCalibration||!screenFit)throw Error('A2345 is not ready');
 const poses={front:[0,.6,3.2],right:[3.4,.45,.2],oblique:[2.4,1.4,2.45]};
 if(view){if(!poses[view])throw Error('Unknown view');camera.position.fromArray(poses[view]);controls.target.set(0,-.06,.15);controls.update();dirty=true;}
 object.updateMatrixWorld(true);const inverse=object.matrixWorld.clone().invert();
 const fitted=screenFit.artwork.geometry.attributes.position,vertices=[];
 for(let i=0;i<fitted.count;i++)vertices.push(new THREE.Vector3().fromBufferAttribute(fitted,i).applyMatrix4(screenFit.artwork.matrixWorld).applyMatrix4(inverse).toArray());
 const ray=new THREE.Raycaster(),gaps=[],interiorGaps=[];
 function gap(p){ray.set(p.clone().add(new THREE.Vector3(0,0,.002)).applyMatrix4(object.matrixWorld),new THREE.Vector3(0,0,-1).transformDirection(object.matrixWorld));const hit=ray.intersectObjects(screenFit.glass,false)[0];if(!hit)return null;const normal=hit.face.normal.clone().transformDirection(hit.object.matrixWorld).transformDirection(inverse);return p.clone().sub(hit.point.clone().applyMatrix4(inverse)).dot(normal);}
 for(const vertex of vertices)gaps.push(gap(new THREE.Vector3(...vertex)));
 const index=screenFit.artwork.geometry.index;for(let i=0;i<index.count;i+=3){const center=new THREE.Vector3();for(let j=0;j<3;j++)center.add(new THREE.Vector3(...vertices[index.getX(i+j)]));interiorGaps.push(gap(center.multiplyScalar(1/3)));}
 return {ports:rigs.map((r,i)=>({actual:r.root.position.toArray(),mouth:socketCalibration.ports[i].p,type:r.connectorType})),vertices,gaps,interiorGaps,columns:screenFit.columns,clearance:screenFit.clearance};
}

// Numeric inspection of actual scene nodes, used only by the isolated UI tests.
export function inspectCuktech(view){
 if(model!=='AD1204U'||!cuktechGeometry)throw Error('10 Ultra is not ready');
 const poses={front:[0,0,3.6],ports:[0,-3.6,0],back:[0,3.6,0],right:[3.6,-.2,.2],oblique:[2.8,-2.0,4.4]};
 if(view){if(!poses[view])throw Error('Unknown view');camera.position.fromArray(poses[view]);camera.up.set(0,view==='ports'||view==='back'?0:1,view==='ports'?1:view==='back'?-1:0);controls.target.set(0,view==='oblique'?-.52:0,view==='oblique'?.15:0);controls.update();dirty=true;}
 object.updateMatrixWorld(true);const inverse=object.matrixWorld.clone().invert(),local=node=>node.getWorldPosition(new THREE.Vector3()).applyMatrix4(inverse),ray=new THREE.Raycaster(),cover=object.getObjectByName('perforated USB cover');
 const ports=rigs.map((r,i)=>{const mouth=local(cuktechGeometry.mouths[i]),n=new THREE.Vector3(0,0,1).applyQuaternion(r.root.quaternion),socketNormal=new THREE.Vector3(0,0,1).transformDirection(cuktechGeometry.mouths[i].matrixWorld).transformDirection(inverse);ray.set(mouth.clone().addScaledVector(n,.002).applyMatrix4(object.matrixWorld),n.clone().negate().transformDirection(object.matrixWorld));const hits=ray.intersectObject(cover,false);return {actual:r.root.position.toArray(),mouth:mouth.toArray(),normal:n.toArray(),socketNormal:socketNormal.toArray(),type:r.connectorType,coverBlocksMouth:hits.length>0,visible:r.root.visible,curve:Array.from({length:21},(_,j)=>r.curve.getPoint(j/20).applyQuaternion(r.root.quaternion).add(r.root.position).toArray())};});
 const {screen,glass}=cuktechGeometry,vertices=Array.from({length:screen.geometry.attributes.position.count},(_,i)=>new THREE.Vector3().fromBufferAttribute(screen.geometry.attributes.position,i).applyMatrix4(screen.matrixWorld).applyMatrix4(inverse).toArray()),normal=new THREE.Vector3(0,0,1).transformDirection(screen.matrixWorld).transformDirection(inverse),glassFront=local(glass).z+glass.geometry.parameters.options.depth;
 ray.set(new THREE.Vector3(.06,0,0).applyMatrix4(object.matrixWorld),new THREE.Vector3(-1,0,0).transformDirection(object.matrixWorld));const silverSurface=ray.intersectObjects([object.getObjectByName('rounded dark chassis'),object.getObjectByName('satin silver side 1')],false)[0]?.object.name;
 return {dimensions:cuktechGeometry.dimensions,silverSurface,ports,screenNormal:normal.toArray(),screenGaps:vertices.map(v=>v[2]-glassFront),screenVertices:vertices,foldedBlades:object.getObjectsByProperty('name','folded AC blade').length};
}
