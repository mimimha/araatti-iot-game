const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = path.resolve(__dirname, '..');
const source = path.join(root, 'source', 'transparent-atlas.png');
const entries = [
 ['helm','조타',[20,20,393,425]],
 ['sails','돛 조절',[416,25,402,428]],
 ['cannon','대포',[835,70,415,364]],
 ['repair','선체 수리',[20,458,386,384]],
 ['ammo','포탄',[458,492,338,341]],
 ['plank','수리 자재',[802,465,450,377]],
 ['water','물 양동이',[20,855,391,345]],
 ['action-panel','상호작용 패널',[412,933,445,191]],
 ['keycap','키캡',[868,933,366,184]]
];
async function main() {
 const meta = await sharp(source).metadata();
 if (!meta.hasAlpha) throw Error('Transparent source required');
 const records = [], composites = [];
 const scaleX = meta.width / 1280, scaleY = meta.height / 1280;
 for (let i=0;i<entries.length;i++) {
  const [name,label,box] = entries[i];
  const [x,y,w,h] = box;
  const crop = await sharp(source).extract({left:Math.round(x*scaleX),top:Math.round(y*scaleY),width:Math.round(w*scaleX),height:Math.round(h*scaleY)}).png().toBuffer();
  const sprite = await sharp(crop).trim({background:'#00000000',threshold:4}).png().toBuffer();
  const dir = i<7?'icons':'ui';
  fs.mkdirSync(path.join(root,dir),{recursive:true});
  const file = `${dir}/${i<7?'icon-':''}${name}.png`;
  if(i<7) {
   const fit = await sharp(sprite).resize(232,232,{fit:'inside'}).png().toBuffer();
   const fm = await sharp(fit).metadata();
   await sharp({create:{width:256,height:256,channels:4,background:'#00000000'}}).composite([{input:fit,left:Math.floor((256-fm.width)/2),top:Math.floor((256-fm.height)/2)}]).png().toFile(path.join(root,file));
  } else {
   await sharp(sprite).png().toFile(path.join(root,file));
  }
  const m = await sharp(path.join(root,file)).metadata();
  const {data,info} = await sharp(path.join(root,file)).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  let clear=0,solid=0;
  for(let k=3;k<data.length;k+=info.channels){if(data[k]===0)clear++;if(data[k]===255)solid++;}
  if(!clear || !solid) throw Error('Invalid alpha '+name);
  records.push({name,label,file,width:m.width,height:m.height,alpha:true,transparentPixels:clear});
  const left=32+(i%3)*320,top=30+Math.floor(i/3)*286;
  const thumb=await sharp(path.join(root,file)).resize(200,200,{fit:'inside'}).png().toBuffer();
  const tm=await sharp(thumb).metadata();
  composites.push({input:Buffer.from(`<svg width="296" height="266"><rect width="296" height="266" rx="20" fill="#102b40"/><text x="148" y="247" text-anchor="middle" font-family="Malgun Gothic, sans-serif" font-size="20" fill="#fff2d9">${label}</text></svg>`),left,top});
  composites.push({input:thumb,left:left+Math.round((296-tm.width)/2),top:top+14+Math.round((200-tm.height)/2)});
 }
 await sharp({create:{width:1008,height:902,channels:4,background:'#263d50'}}).composite(composites).png().toFile(path.join(root,'preview.png'));
 fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify({importSettings:{textureType:'Sprite (2D and UI)',spriteMode:'Single',alphaIsTransparency:true,mipmaps:false,wrapMode:'Clamp',filterMode:'Bilinear'},assets:records},null,2));
 console.log(JSON.stringify(records));
}
main().catch(e=>{console.error(e);process.exit(1)});
