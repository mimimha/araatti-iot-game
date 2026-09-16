const sharp = require('sharp');
const path = require('path');
async function main() {
 const src=process.argv[2];
 const m=await sharp(src).metadata();
 const mask=Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="${m.width}" height="${m.height}" viewBox="0 0 1536 1024"><rect x="76" y="77" width="1384" height="897" rx="84" fill="white"/><path d="M383 113 Q387 99 397 95 L397 75 Q403 65 403 54 Q409 35 424 32 Q430 24 443 29 L452 30 Q460 34 478 33 L1058 33 Q1075 33 1085 29 Q1097 23 1109 32 Q1125 34 1132 50 L1138 74 L1137 91 Q1150 97 1151 111 Q1143 127 1132 135 L1126 147 L1107 159 L421 158 L403 147 L396 132 Q386 127 383 113Z" fill="white"/></svg>`);
 await sharp(src).ensureAlpha().composite([{input:mask,blend:'dest-in'}]).png().toFile(path.join(__dirname,'gadogado-popup.png'));
 const {data,info}=await sharp(path.join(__dirname,'gadogado-popup.png')).raw().toBuffer({resolveWithObject:true});
 let clear=0;for(let i=3;i<data.length;i+=4)if(data[i]===0)clear++;
 console.log(JSON.stringify({width:info.width,height:info.height,channels:info.channels,transparentPixels:clear}));
}
main().catch(e=>{console.error(e);process.exit(1)});
