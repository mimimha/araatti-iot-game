const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const hud = path.resolve(root, '../..');
const generated = 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef';
const panels = [
 ['phase-panel','출항 / 단계','e68131af-e24e-4e97-a4c2-884f44f6418f',512],
 ['hp-panel','선박 HP','b5ddebf3-ac4e-4f7f-9ffb-bd4974e3e8b2',1024],
 ['timer-panel','남은 시간','6d8893e3-76d0-4deb-b135-4eafbf2a5100',384],
 ['voyage-panel','항해 진행도','37e13788-6794-49c2-a09d-3d6780d263ca',1024],
 ['warning-panel','지연 경고','e16df8e0-0a06-4f6d-9567-bbea25109c10',768],
 ['crew-panel','4인 프로필 바탕','d74b4814-7aad-4642-8133-89fb71e65612',1024],
 ['interaction-panel','상호작용 진행도','9f8b87d1-2501-4d33-9ad4-5ef1935c0f17',768]
];
const records = [];
// Remove isolated alpha specks while retaining antialiased edges around the main sprite.
async function cleanAndTrim(input) {
 const m = await sharp(input).metadata();
 if (!m.hasAlpha) throw Error('Missing alpha: '+input);
 const {data,info} = await sharp(input).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 const w=info.width,h=info.height,n=w*h,ids=new Int32Array(n),queue=new Int32Array(n);
 let id=0,best=0,bestSize=0;
 for(let p=0;p<n;p++) {
  if(ids[p] || data[p*4+3]<16) continue;
  id++; let head=0,tail=1;queue[0]=p;ids[p]=id;
  while(head<tail) {
   const q=queue[head++],x=q%w;
   const neighbors=[x>0?q-1:-1,x<w-1?q+1:-1,q>=w?q-w:-1,q<n-w?q+w:-1];
   for(const s of neighbors) if(s>=0&&!ids[s]&&data[s*4+3]>=16){ids[s]=id;queue[tail++]=s;}
  }
  if(tail>bestSize){best=id;bestSize=tail;}
 }
 if(bestSize<n*.03) throw Error('Sprite too small: '+input);
 let x0=w,y0=h,x1=0,y1=0;
 for(let p=0;p<n;p++) {
  if(!data[p*4+3]) continue;
  let keep=ids[p]===best;
  const x=p%w,y=Math.floor(p/w);
  if(!keep) for(let dy=-2;dy<=2&&!keep;dy++)for(let dx=-2;dx<=2&&!keep;dx++){
   const xx=x+dx,yy=y+dy;
   if(xx>=0&&xx<w&&yy>=0&&yy<h&&ids[yy*w+xx]===best)keep=true;
  }
  if(!keep){data[p*4+3]=0;continue;}
  x0=Math.min(x0,x);y0=Math.min(y0,y);x1=Math.max(x1,x);y1=Math.max(y1,y);
 }
 const result=await sharp(data,{raw:{width:w,height:h,channels:4}}).extract({left:x0,top:y0,width:x1-x0+1,height:y1-y0+1}).png().toBuffer();
 return result;
}
async function record(file,label,provenance) {
 const full=path.join(root,file),m=await sharp(full).metadata();
 const {data,info}=await sharp(full).ensureAlpha().raw().toBuffer({resolveWithObject:true});
 let clear=0,solid=0,partial=0;
 for(let i=3;i<data.length;i+=4){if(data[i]===0)clear++;else if(data[i]===255)solid++;else partial++;}
 if(!m.hasAlpha||!clear||!solid)throw Error('Invalid transparency: '+file);
 records.push({file,label,width:m.width,height:m.height,hasAlpha:true,transparentPixels:clear,partialAlphaPixels:partial,provenance});
}
async function main() {
 for(const d of ['panels','crew','fills','source'])fs.mkdirSync(path.join(root,d),{recursive:true});
 for(const [name,label,uuid,width] of panels) {
  const src=path.join(root,'source',name+'.png');
  if(!fs.existsSync(src))fs.copyFileSync(path.join(generated,'exec-'+uuid+'.png'),src);
  const clean=await cleanAndTrim(src);
  await sharp(clean).resize({width:width-8}).extend({top:4,bottom:4,left:4,right:4,background:'#00000000'}).png().toFile(path.join(root,'panels',name+'.png'));
  await record('panels/'+name+'.png',label,'image_gen: reference-based recreation, text removed; icon and track remain baked into panel');
 }
 const crew=[['portrait-wig','흰 가발'],['portrait-captain','선장'],['portrait-jester','광대'],['portrait-diver','잠수부'],['frame-red','빨강 프레임'],['frame-yellow','노랑 프레임'],['frame-green','초록 프레임'],['frame-purple','보라 프레임'],['portrait-backplate','프로필 배경']];
 for(const [name,label] of crew){
  const source=path.join(hud,'01-crew',name+'.png'),file='crew/'+name+'.png';
  if(name.startsWith('portrait-')&&name!=='portrait-backplate'){
   await sharp(await cleanAndTrim(source)).resize(456,456,{fit:'contain',background:'#00000000'}).extend({top:28,bottom:28,left:28,right:28,background:'#00000000'}).png().toFile(path.join(root,file));
  }else fs.copyFileSync(source,path.join(root,file));
  await record(file,label,'Reused existing art/hud/01-crew asset; character details differ from mockup');
 }
 // Derived from existing repo-native hp-fill-green.svg and bar-fill-white.svg.
 const fills=[['hp-fill-green','HP 채움','#79ff93','#09d74e','#d4ffe0'],['voyage-fill-cyan','항해 채움','#98ecff','#24ade4','#ddfaff'],['interaction-fill-ivory','상호작용 채움','#fff5d8','#e9c479','#ffffff']];
 for(const [name,label,a,b,c] of fills){
  const svg=`<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="48"><defs><linearGradient id="f" x2="0" y2="1"><stop stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient></defs><rect x="2" y="2" width="1020" height="44" rx="22" fill="url(#f)"/><path d="M24 8H1000" stroke="${c}" stroke-opacity=".7" stroke-width="2" stroke-linecap="round"/></svg>`;
  fs.writeFileSync(path.join(root,'source',name+'.svg'),svg);
  await sharp(Buffer.from(svg)).png().toFile(path.join(root,'fills',name+'.png'));
  await record('fills/'+name+'.png',label,'Derivative of existing repo-native SVG gauge fill');
 }
 fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify({reference:'../minigame-hud-heart-only-v6.png',import:{textureType:'Sprite (2D and UI)',spriteMode:'Single',alphaIsTransparency:true,mipmaps:false,wrapMode:'Clamp',filterMode:'Bilinear',compression:'None',maxTextureSize:2048},notes:['Text and numeric data must be rendered separately.','Panels contain their static icons/tracks. Do not duplicate those icons.','No Unity scene or prefab has been changed.'],assets:records},null,2));
 const parts=[];
 const label=(s,x,y,size=19)=>({input:Buffer.from(`<svg width="1500" height="50"><text x="0" y="30" font-family="Malgun Gothic,sans-serif" font-size="${size}" fill="#f4e6c8">${s}</text></svg>`),left:x,top:y});
 parts.push(label('GADOGADO  /  TRANSPARENT HUD ASSETS',44,20,28));
 parts.push(label('7 PANELS  ·  4 PORTRAITS  ·  4 FRAMES  ·  BACKPLATE  ·  3 FILLS',44,66,17));
 for(let i=0;i<panels.length;i++){
  const [name,title]=panels[i],col=i%2,row=Math.floor(i/2),x=44+col*770,y=138+row*152;
  parts.push(label(title,x,y,18));
  const img=await sharp(path.join(root,'panels',name+'.png')).resize(700,99,{fit:'inside'}).png().toBuffer();
  parts.push({input:img,left:x,top:y+46});
 }
 for(let i=0;i<4;i++){
  const x=44+i*180,y=798;
  for(const name of ['portrait-backplate',crew[i][0],crew[4+i][0]])parts.push({input:await sharp(path.join(root,'crew',name+'.png')).resize(144,144).png().toBuffer(),left:x,top:y});
  parts.push(label(crew[i][1],x,y+147,17));
 }
 for(let i=0;i<fills.length;i++){
  parts.push(label(fills[i][1],814,798+i*64,17));
  parts.push({input:await sharp(path.join(root,'fills',fills[i][0]+'.png')).resize(620,28).png().toBuffer(),left:814,top:836+i*64});
 }
 // Checkerboard is only in preview, never in deliverable sprites.
 const checker=Buffer.from('<svg width="1584" height="1024"><defs><pattern id="p" width="32" height="32" patternUnits="userSpaceOnUse"><rect width="32" height="32" fill="#293844"/><path d="M0 0h16v16H0zM16 16h16v16H16z" fill="#30404c"/></pattern></defs><rect width="1584" height="1024" fill="url(#p)"/></svg>');
 await sharp(checker).composite(parts).png().toFile(path.join(root,'preview.png'));
 console.log(JSON.stringify(records.map(({file,width,height,transparentPixels})=>({file,width,height,transparentPixels})),null,2));
}
main().catch(e=>{console.error(e);process.exit(1)});
