import * as THREE from 'three';

// The source GLB combines the case, knob and socket assemblies in one mesh.
// Recover its disconnected assemblies in model space; do not guess socket spacing.
export function calibrateA2345(root){
 root.updateMatrixWorld(true);let source;
 root.traverse(mesh=>{if(mesh.isMesh&&mesh.material?.name==='new')source=mesh;});
 if(!source)throw Error('A2345 socket mesh is missing');
 const position=source.geometry.attributes.position,index=source.geometry.index;
 const parent=Int32Array.from({length:position.count},(_,i)=>i),points=[],weld=new Map();
 function find(i){while(parent[i]!==i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
 function union(a,b){parent[find(a)]=find(b);}
 for(let i=0;i<position.count;i++){
  const p=new THREE.Vector3().fromBufferAttribute(position,i).applyMatrix4(source.matrixWorld);points.push(p);
  const key=p.toArray().map(v=>Math.round(v*1e8)).join(',');
  if(weld.has(key))union(i,weld.get(key));else weld.set(key,i);
 }
 for(let i=0;i<index.count;i+=3){union(index.getX(i),index.getX(i+1));union(index.getX(i),index.getX(i+2));}
 const groups=new Map();
 points.forEach((p,i)=>{const key=find(i);if(!groups.has(key))groups.set(key,{box:new THREE.Box3(),count:0});const g=groups.get(key);g.box.expandByPoint(p);g.count++;});
 const assemblies=[...groups.values()].map(g=>({...g,size:g.box.getSize(new THREE.Vector3()),center:g.box.getCenter(new THREE.Vector3())}));
 const cs=assemblies.filter(g=>g.count>500&&g.size.x>.0007&&g.size.x<.001&&g.size.y>.0002&&g.size.y<.00035&&g.size.z>.0004&&g.size.z<.00055&&g.center.z>.008).sort((a,b)=>a.center.x-b.center.x);
 const as=assemblies.filter(g=>g.count>2000&&g.size.x>.0009&&g.size.x<.0011&&g.size.y>.0005&&g.size.y<.0007&&g.size.z>.0012&&g.size.z<.0014&&g.center.x>.004).sort((a,b)=>b.center.z-a.center.z);
 if(cs.length!==4||as.length!==2)throw Error('A2345 socket geometry did not match four USB-C / two USB-A assemblies');
 const inverse=root.matrixWorld.clone().invert();
 const ports=[...cs.map(g=>({p:new THREE.Vector3(g.center.x,g.center.y,g.box.max.z).applyMatrix4(inverse).toArray(),n:[0,0,1],bounds:g.box})),...as.map(g=>({p:new THREE.Vector3(g.box.max.x,g.center.y,g.center.z).applyMatrix4(inverse).toArray(),n:[1,0,0],usbA:true,bounds:g.box}))];
 return {ports};
}

export function fitA2345Screen(root,material){
 root.updateMatrixWorld(true);
 const artwork=root.getObjectByName('1280X1280_(2)')||root.getObjectByName('1280X1280 (2)'),glass=[];
 root.traverse(mesh=>{if(mesh.isMesh&&mesh.material?.name?.includes('_Ping.'))glass.push(mesh);});
 if(!artwork||!glass.length)throw Error('A2345 original screen or front glass is missing');
 const original=artwork.geometry,uv=original.attributes.uv,sourcePositions=original.attributes.position;
 const corners=[[0,0],[1,0],[0,1],[1,1]].map(([u,v])=>{let best=0,distance=Infinity;for(let i=0;i<uv.count;i++){const d=(uv.getX(i)-u)**2+(uv.getY(i)-v)**2;if(d<distance){distance=d;best=i;}}return new THREE.Vector3().fromBufferAttribute(sourcePositions,best).applyMatrix4(artwork.matrixWorld);});
 const columns=32,rows=16,geometry=new THREE.BufferGeometry(),positions=new THREE.Float32BufferAttribute((columns+1)*(rows+1)*3,3),texcoords=[],indices=[];
 for(let y=0;y<=rows;y++)for(let x=0;x<=columns;x++){const u=x/columns,v=y/rows,point=corners[0].clone().lerp(corners[1],u).lerp(corners[2].clone().lerp(corners[3],u),v);positions.setXYZ(y*(columns+1)+x,...point.toArray());texcoords.push(u,v);if(x<columns&&y<rows){const a=y*(columns+1)+x,b=a+1,c=a+columns+1,d=c+1;indices.push(a,c,b,b,c,d);}}
 geometry.setAttribute('position',positions);geometry.setAttribute('uv',new THREE.Float32BufferAttribute(texcoords,2));geometry.setIndex(indices);
 const normal=new THREE.Vector3().fromBufferAttribute(original.attributes.normal,0).transformDirection(artwork.matrixWorld);
 const ray=new THREE.Raycaster(),inverse=artwork.matrixWorld.clone().invert(),samples=[],clearance=.000002;
 for(let i=0;i<positions.count;i++){
  const point=new THREE.Vector3().fromBufferAttribute(positions,i);
  ray.set(point.clone().addScaledVector(normal,.002),normal.clone().negate());
  const hit=ray.intersectObjects(glass,false)[0];if(!hit)throw Error('A2345 screen vertex does not project onto the original front glass');
  const faceNormal=hit.face.normal.clone().transformDirection(hit.object.matrixWorld);
  if(faceNormal.dot(normal)<0)faceNormal.negate();
  const fitted=hit.point.clone().addScaledVector(faceNormal,clearance);
  samples.push({point:fitted.toArray(),surface:hit.point.toArray(),normal:faceNormal.toArray(),gap:clearance});
  positions.setXYZ(i,...fitted.clone().applyMatrix4(inverse).toArray());
 }
 geometry.computeVertexNormals();geometry.computeBoundingBox();geometry.computeBoundingSphere();
 artwork.geometry.dispose();artwork.material.dispose();artwork.geometry=geometry;artwork.material=material;artwork.visible=true;
 material.side=THREE.FrontSide;material.depthTest=true;material.depthWrite=false;material.polygonOffset=true;material.polygonOffsetFactor=-1;material.polygonOffsetUnits=-1;
 artwork.name='A2345 fitted LCD';
 return {artwork,glass,samples,clearance,columns,rows};
}
