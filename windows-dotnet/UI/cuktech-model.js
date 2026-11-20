import * as THREE from 'three';
import {RoundedBoxGeometry} from './vendor/RoundedBoxGeometry.js';

// Original reconstruction, authored in millimetres. x=thickness, y=screen
// length, z=USB-face length. Screen (+z) and sockets (-y) are ADJACENT.
const mm=.001,W=33.13,H=66.89,D=75.59;
function outline(w,h,r){const s=new THREE.Shape(),x=w*mm/2,y=h*mm/2;r=Math.min(r,w/2,h/2)*mm;s.moveTo(-x+r,-y);s.lineTo(x-r,-y);s.quadraticCurveTo(x,-y,x,-y+r);s.lineTo(x,y-r);s.quadraticCurveTo(x,y,x-r,y);s.lineTo(-x+r,y);s.quadraticCurveTo(-x,y,-x,y-r);s.lineTo(-x,-y+r);s.quadraticCurveTo(-x,-y,-x+r,-y);return s;}
function hole(w,h,r,x=0,y=0){return new THREE.Path(outline(w,h,r).getPoints(32).reverse().map(p=>p.add(new THREE.Vector2(x*mm,y*mm))));}
export function createCuktechModel(){
 const object=new THREE.Group();object.name='CUKTECH 10 Ultra AD1204U';
 const mat=(color,metalness=0,roughness=.5)=>new THREE.MeshStandardMaterial({color,metalness,roughness});
 const silver=mat(0x979b9f,.78,.32),edge=mat(0xb6bdc2,.88,.25),dark=mat(0x202329,.12,.53),black=mat(0x090c10,.14,.23),well=mat(0x020305,0,.9),cyan=mat(0x78cbd9,.42,.35);
 function add(geo,material,name,parent=object){const m=new THREE.Mesh(geo,material);m.name=name;parent.add(m);return m;}
 function box(w,h,d,r,material,name,parent=object){return add(new RoundedBoxGeometry(w*mm,h*mm,d*mm,4,r*mm),material,name,parent);}
 function plate(shape,depth,material,name,parent=object){return add(new THREE.ExtrudeGeometry(shape,{depth:depth*mm,bevelEnabled:false,curveSegments:16}),material,name,parent);}
 function label(text,w,h,color,name,parent,x,y,z,size=64){const canvas=document.createElement('canvas');canvas.width=512;canvas.height=Math.round(512*h/w);const c=canvas.getContext('2d');c.fillStyle=color;c.textAlign='center';c.textBaseline='middle';c.font=`600 ${size}px Segoe UI, sans-serif`;c.fillText(text,256,canvas.height/2,480);const texture=new THREE.CanvasTexture(canvas);texture.colorSpace=THREE.SRGBColorSpace;const m=add(new THREE.PlaneGeometry(w*mm,h*mm),new THREE.MeshBasicMaterial({map:texture,transparent:true,depthWrite:false,polygonOffset:true,polygonOffsetFactor:-1}),name,parent);m.position.set(x*mm,y*mm,z*mm);return m;}
 // Stop the core behind the perforated lower cover; holes remain physically open.
 const core=box(W-1.4,H-6,D,4.1,dark,'rounded dark chassis');core.position.y=3*mm;
 for(const sign of [1,-1]){
  const panel=add(new THREE.ExtrudeGeometry(outline(D-10.4,H-3.5,4.8),{depth:.3*mm,bevelEnabled:true,bevelThickness:.35*mm,bevelSize:.5*mm,bevelSegments:4,curveSegments:16}),silver,'satin silver side '+sign);panel.rotation.y=sign*Math.PI/2;panel.position.set(sign*(W/2-.65)*mm,0,4*mm);
  const ring=outline(D-9.9,H-3,5.1);ring.holes.push(hole(D-11.4,H-4.5,4.5));const trim=plate(ring,.25,edge,'polished side rim '+sign);trim.rotation.y=panel.rotation.y;trim.position.set(sign*(W/2-.25)*mm,0,4*mm);
  const side=new THREE.Group();side.rotation.y=panel.rotation.y;side.position.set(sign*(W/2+.05)*mm,0,0);object.add(side);
  label('CUKTECH',18,3.6,'#8ac5cf','side brand '+sign,side,sign*-20,24,0,74);label('10',12,9,'#454a50','10 Ultra emblem '+sign,side,sign*19,-20,0,210);label('ULTRA',11,2.4,'#444a50','Ultra wordmark '+sign,side,sign*19,-26,0,72);
 }
 for(let z=-36.5;z<-28;z+=.8){const rib=box(W+.05,H-.3,.30,.12,mat(0x30333a,.15,.65),'rear cap rib');rib.position.z=z*mm;}
 const seam=box(W+.12,H-.1,.40,.18,cyan,'cyan rear seam');seam.position.z=-27.8*mm;
 // TFT and glass share a plane; the separate touch strip sits below the TFT.
 const bezel=plate(outline(30.4,61.6,4.0),.55,edge,'display chrome bezel');bezel.position.set(0,.3*mm,(D/2-.5)*mm);
 const glass=plate(outline(28.9,60.2,3.5),.2,black,'front glass');glass.position.set(0,.3*mm,(D/2+.06)*mm);
 const screen=add(new THREE.PlaneGeometry(22.3*mm,35.68*mm),new THREE.MeshBasicMaterial({color:0x06090d}),'CuktechScreen');screen.position.set(0,9.4*mm,(D/2+.275)*mm);
 const button=plate(outline(4.2,11.2,2),.16,mat(0x0b0e13,.24,.32),'recessed touch control');button.position.set(0,-20*mm,(D/2+.27)*mm);
 for(const x of [-.65,.65]){const bar=box(.38,7,.10,.04,cyan,'cyan touch bar');bar.position.set(x*mm,-20*mm,(D/2+.45)*mm);}
 const face=new THREE.Group();face.name='USB output face';face.position.y=-H/2*mm;face.rotation.x=Math.PI/2;object.add(face);
 const zPositions=[23.8,12.8,1.8,-10.2],cover=outline(31.6,D-1.0,4.0);zPositions.forEach((z,i)=>cover.holes.push(hole(i===3?12.7:9.05,i===3?5.2:3.18,i===3?.35:1.59,0,z)));
 const coverMesh=plate(cover,5.8,dark,'perforated USB cover',face);coverMesh.position.z=-5.8*mm;
 for(let z=-24;z<30;z+=.75){const occupied=zPositions.findIndex((p,i)=>Math.abs(z-p)<(i===3?3.5:2.6));for(const sign of [-1,1]){const width=occupied<0?14.4:occupied===3?6.1:8.0,center=occupied<0?sign*7.2:sign*(14.4-width/2);const rib=add(new THREE.BoxGeometry(width*mm,.13*mm,.065*mm),mat(0x35383e,.12,.66),'USB cover micro rib',face);rib.position.set(center*mm,z*mm,.035*mm);}}
 const mouths=[];zPositions.forEach((z,i)=>{
  const isA=i===3,outer=outline(isA?13.3:9.8,isA?5.85:3.95,isA?.45:1.96);outer.holes.push(hole(isA?12.7:9.05,isA?5.2:3.18,isA?.35:1.59));const rim=plate(outer,.26,mat(0x555c64,.73,.3),`socket ${i} metal rim`,face);rim.position.set(0,z*mm,-.12*mm);
  const socket=new THREE.Group();socket.name='socket '+i+' cavity';socket.position.set(0,z*mm,0);face.add(socket);
  const rear=add(new THREE.ShapeGeometry(outline(isA?12.7:9.05,isA?5.2:3.18,isA?.35:1.59)),well,'socket rear wall',socket);rear.position.z=-4.7*mm;
  const tongue=box(isA?10.5:6.5,isA?1.2:.65,2.1,.15,i<2?mat(0x458d9e,.35,.5):mat(0x444a54,.1,.62),'socket tongue',socket);tongue.position.set(0,(isA?1.2:0)*mm,-2.5*mm);
  if(isA)for(const x of [-4.1,-1.35,1.35,4.1]){const contact=add(new THREE.BoxGeometry(.65*mm,.10*mm,1.4*mm),mat(0xb9a16f,.8,.35),'USB-A contact',socket);contact.position.set(x*mm,.5*mm,-2.3*mm);}
  label(isA?'A':'C'+(i+1),4.4,2.1,'#777f88','port label '+i,face,-10.9,z,.12,100);mouths.push(socket);
 });
 const badge=plate(outline(27,7.5,2.6),.30,silver,'CUKTECH lower badge',face);badge.position.set(0,-32.3*mm,.02*mm);label('CUKTECH',20,3.4,'#343a40','port face brand',face,0,-32.2,.35,72);
 // The opposite narrow face carries folded mains blades, not the silver side.
 const input=new THREE.Group();input.name='folding mains input';input.position.y=H/2*mm;input.rotation.x=-Math.PI/2;object.add(input);
 const back=plate(outline(30.6,D-2.2,4),.15,dark,'mains cover',input);back.position.z=.03*mm;
 for(const x of [-6.35,6.35]){const slot=plate(outline(2.3,25,1.1),.1,well,'folding pin recess',input);slot.position.set(x*mm,-19*mm,.20*mm);const blade=box(1.55,22,.5,.12,edge,'folded AC blade',input);blade.position.set(x*mm,-20*mm,.40*mm);}
 label('CUKTECH 10 Ultra   AD1204U',25,4,'#70757c','mains product label',input,0,23,.20,40);label('100–240V ~ 50/60Hz',24,3,'#70757c','mains rating',input,0,16,.20,40);label('USB-C1 / C2   120W MAX',25,3,'#70757c','output rating',input,0,10,.20,38);label('Made in China',20,3,'#70757c','mains origin',input,0,4,.20,42);
 object.updateMatrixWorld(true);
 const ports=mouths.map((socket,i)=>({p:socket.getWorldPosition(new THREE.Vector3()).toArray(),n:[0,-1,0],end:[(-28+i*18)*mm,-.080,(55-i*15)*mm],usbA:i===3}));
 return {object,ports,dimensions:[W,H,D],mouths,screen,glass,portFace:face};
}
