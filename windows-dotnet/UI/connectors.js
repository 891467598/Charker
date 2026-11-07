import * as THREE from 'three';

// Model-space millimetres, scaled to match each source GLB by the caller.
// These are two distinct silhouettes, not the same rounded box at two scales.
const profiles={
 'USB-C':{metal:[8.3,2.5],body:[10.4,4.4],length:10.4,start:1.8,radius:2.2,wire:1.2,sleeveEnd:17.2},
 'USB-A':{metal:[12.0,4.5],body:[14.8,7.0],length:15.8,start:1.5,radius:.55,wire:1.65,sleeveEnd:24.2}
};
function contour(w,h,r){
 const s=new THREE.Shape(),x=w/2,y=h/2;r=Math.min(r,y,x);
 s.moveTo(-x+r,-y);s.lineTo(x-r,-y);s.quadraticCurveTo(x,-y,x,-y+r);
 s.lineTo(x,y-r);s.quadraticCurveTo(x,y,x-r,y);s.lineTo(-x+r,y);s.quadraticCurveTo(-x,y,-x,y-r);
 s.lineTo(-x,-y+r);s.quadraticCurveTo(-x,-y,-x+r,-y);return s;
}
function extrusion(shape,length,material,z,name){
 const mesh=new THREE.Mesh(new THREE.ExtrudeGeometry(shape,{depth:length,bevelEnabled:false,curveSegments:12,steps:1}),material);
 mesh.position.z=z;mesh.name=name;return mesh;
}
function cylinder(parent,near,far,length,z,material,name){
 const mesh=new THREE.Mesh(new THREE.CylinderGeometry(far,near,length,20),material);mesh.rotation.x=Math.PI/2;mesh.position.z=z;mesh.name=name;parent.add(mesh);return mesh;
}
export function createConnector(type,scale,color){
 const p=profiles[type];if(!p)throw Error('Unknown connector type');
 const u=.001*scale,root=new THREE.Group();root.name=type+' connector';root.userData.connectorType=type;
 const housing=new THREE.MeshStandardMaterial({color:type==='USB-C'?0xb9c0c7:0x515a65,metalness:.12,roughness:.45});
 const metal=new THREE.MeshStandardMaterial({color:0xaab4bf,metalness:.85,roughness:.25});
 const rubber=new THREE.MeshStandardMaterial({color:type==='USB-C'?0xb3bbc3:0x89929c,roughness:.78});
 const glow=new THREE.MeshStandardMaterial({color,emissive:color,emissiveIntensity:1,roughness:.45});
 const [mw,mh]=p.metal,[bw,bh]=p.body;
 const shell=contour(mw*u,mh*u,(type==='USB-C'?mh/2:.25)*u);
 const inside=contour((mw-.65)*u,(mh-.65)*u,(type==='USB-C'?(mh-.65)/2:.12)*u);
 shell.holes.push(new THREE.Path(inside.getPoints(32).reverse()));
 root.add(extrusion(shell,5.0*u,metal,-3.0*u,'hollow metal shell'));
 root.add(extrusion(contour(bw*u,bh*u,p.radius*u),p.length*u,housing,p.start*u,'moulded housing'));
 const ring=extrusion(contour((bw+.06)*u,(bh+.06)*u,p.radius*u),.45*u,glow,(p.start+p.length-1.1)*u,'power indicator');root.add(ring);
 const bodyEnd=p.start+p.length;
 if(type==='USB-C'){
  cylinder(root,1.65*u,p.wire*u,(p.sleeveEnd-bodyEnd)*u,(bodyEnd+p.sleeveEnd)/2*u,rubber,'USB-C tapered strain relief');
 }else{
  cylinder(root,2.15*u,p.wire*u,(p.sleeveEnd-bodyEnd)*u,(bodyEnd+p.sleeveEnd)/2*u,rubber,'USB-A strain relief');
  for(let i=0;i<3;i++)cylinder(root,(2.13-i*.13)*u,(2.13-i*.13)*u,.6*u,(bodyEnd+1.2+i*1.5)*u,housing,'USB-A relief rib');
  const recess=new THREE.MeshStandardMaterial({color:0x38414b,roughness:.8});
  for(const x of [-3,3]){const hole=new THREE.Mesh(new THREE.BoxGeometry(1.4*u,.08*u,1.25*u),recess);hole.position.set(x*u,mh/2*u,.25*u);hole.name='USB-A retention recess';root.add(hole);}
 }
 return {root,ring,glow,rubber,wireRadius:p.wire*u,cableStart:p.sleeveEnd*u,type};
}
