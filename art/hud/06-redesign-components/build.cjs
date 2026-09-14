// Usage: NODE_PATH=<directory containing sharp> node build.cjs
const fs = require('fs');
const path = require('path');
const sharp = require('sharp');
const root = __dirname;
const assets = [];
const white = '#FFF2D9', gold = '#D2AB64', navy = '#091F31';
const svg = (w,h,s) => `<svg xmlns="http://www.w3.org/2000/svg" width="${w}" height="${h}" viewBox="0 0 ${w} ${h}">${s}</svg>`;
const rect = (x,y,w,h,r,fill,stroke='none',sw=0) => `<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const circle = (x,y,r,fill,stroke='none',sw=0) => `<circle cx="${x}" cy="${y}" r="${r}" fill="${fill}" stroke="${stroke}" stroke-width="${sw}"/>`;
const line = (x,y,xx,yy,c=white,w=9) => `<path d="M${x} ${y}L${xx} ${yy}" stroke="${c}" stroke-width="${w}" stroke-linecap="round"/>`;
const poly = (p,c=white) => `<polygon points="${p}" fill="${c}" stroke-linejoin="round"/>`;
async function asset(name,w,h,body,usage,border=[0,0,0,0],type='Simple') {
  const file = path.join(root,'sprites',name+'.png');
  const source = svg(w,h,body);
  fs.writeFileSync(path.join(root,'svg',name+'.svg'),source);
  await sharp(Buffer.from(source)).resize(w*2,h*2).resize(w,h).png().toFile(file);
  assets.push({name,file:'sprites/'+name+'.png',width:w,height:h,borderLTRB:border,imageType:type,usage});
}
async function panel(name,w,h,r,accent) {
 await asset(name+'-background',w,h,rect(3,3,w-6,h-6,r,navy), '배경. 프레임 아래, 텍스트와 분리.',[r+4,r+4,r+4,r+4],'Sliced');
 await asset(name+'-frame',w,h,rect(3,3,w-6,h-6,r,'none','#020C17',6)+rect(4,4,w-8,h-8,r,'none',accent,2), '중앙 투명 외곽선. 같은 크기 배경 위.',[r+4,r+4,r+4,r+4],'Sliced');
}
async function main(){
 fs.mkdirSync(path.join(root,'sprites'),{recursive:true});fs.mkdirSync(path.join(root,'svg'),{recursive:true});
 await panel('panel-capsule',640,88,40,gold);
 await panel('panel-event',640,156,26,gold);
 await panel('panel-team',720,196,28,'#657C91');
 await panel('panel-action',460,196,30,gold);
 await panel('panel-flood',660,104,28,'#66CAEF');
 for(const [state,c] of [['warning','#FFBC55'],['danger','#FF5864']]) {
  await asset('event-frame-'+state,640,156,rect(4,4,632,148,26,'none',c,4),'사건 상태별 프레임. 배경과 겹침.',[30,30,30,30],'Sliced');
  await asset('event-accent-'+state,8,96,rect(0,0,8,96,4,c),'카드 왼쪽 상태 표시');
 }
 await asset('bar-track',512,32,rect(2,2,508,28,14,'#122839','#A3B3BD',2),'공통 HP/항해/침수 트랙',[18,0,18,0],'Sliced');
 await asset('bar-fill-white',500,20,rect(0,0,500,20,10,'#FFFFFF'),'전체 채움. Image.color로 색 지정. Horizontal Filled.',[0,0,0,0],'Filled Horizontal');
 await asset('bar-delay',32,16,rect(0,0,32,16,0,'#EF5966'),'실제 배 위치부터 기준선까지 폭만 늘림');
 await asset('voyage-reference',16,64,[0,16,32,48].map(y=>rect(5,y+1,6,11,3,'#FFD05D')).join(''),'예정 진행도 점선. 중앙 pivot');
 await asset('ring-background',256,256,circle(128,128,124,navy),'원형 작업 배경');
 await asset('ring-track',256,256,circle(128,128,106,'none','#344456',18),'진행률과 무관한 빈 링');
 await asset('ring-fill-white',256,256,circle(128,128,106,'none','#FFFFFF',18),'Image.color=gold. Radial360, Top, Clockwise. 중심 투명.',[0,0,0,0],'Filled Radial360');
 await asset('ring-frame',256,256,circle(128,128,124,'none',gold,2),'진행 링 바깥 테두리');
 await asset('keycap',72,72,rect(4,7,64,61,12,'#8695A2')+rect(4,3,64,60,12,white),'키 문자는 TMP로 가운데. 키마다 같은 에셋 재사용.',[16,16,16,16],'Sliced');
 await asset('badge-player',80,80,circle(40,40,35,'#FFFFFF','#FFFFFF',4),'Image.color를 플레이어 색으로. 숫자는 별도 TMP');
 await asset('badge-help',96,112,rect(7,4,82,82,24,'#FFD05D','#162333',4)+poly('33,85 49,108 62,85','#FFD05D'),'도움 요청 배경. 느낌표/아이콘 별도');
 for(const [name,c] of [['red','#F35C65'],['yellow','#FFD05D'],['green','#48D889'],['purple','#BB82EE']])
  await asset('portrait-frame-'+name,160,160,rect(4,4,152,152,24,'none','#061323',8)+rect(5,5,150,150,23,'none',c,5),'초상화 위 프레임. 역할이 아닌 플레이어 색');
 await asset('portrait-backplate',160,160,rect(5,5,150,150,23,'#132C3D'),'초상화 뒤 배경. 초상화 자체 클리핑은 Unity Mask 필요');
 // Original vector silhouettes, not raster crops of the generated mockup.
 const icons={
  ship:poly('15,86 116,86 99,111 34,111')+line(62,17,62,86)+poly('69,22 105,73 69,73')+poly('54,36 27,75 54,75')+line(25,119,106,119,white,4),
  clock:circle(64,64,48,'none',white,10)+line(64,34,64,65)+line(64,65,87,76),
  repair:poly('30,104 43,115 87,64 74,52')+poly('55,32 76,14 112,48 94,69 76,51 67,46'),
  bucket:poly('27,45 102,45 94,107 35,107')+`<path d="M35 43V32C35 7 94 7 94 32V43" fill="none" stroke="${white}" stroke-width="8"/>`+line(44,60,84,60,navy,5),
  reef:poly('10,100 34,63 46,70 60,24 81,19 99,65 118,100')+line(57,49,49,79,navy,5),
  cannon:poly('20,69 94,30 111,62 39,93')+circle(40,100,16,white)+circle(90,92,16,white)+line(38,86,88,80,white,8),
  'enemy-ship':poly('11,85 117,85 98,111 32,111')+line(62,17,62,85)+poly('68,23 104,27 100,62 68,59')+circle(84,42,7,navy),
  'hull-breach':poly('9,77 53,43 55,61 72,57 65,80 30,108')+poly('60,33 100,9 119,40 80,71 83,48 63,52'),
  sails:line(64,15,64,113)+poly('55,26 14,96 55,89')+poly('74,32 112,96 74,89')+line(19,111,113,111,white,5),
  water:`<path d="M64 12C51 36 27 60 27 80a37 37 0 0 0 74 0C101 60 77 36 64 12Z" fill="${white}"/>`,
  'cannonball':circle(64,69,43,white)+circle(50,55,10,navy),
  plank:poly('12,83 90,14 116,43 38,112')+line(41,80,87,40,navy,5),
  warning:poly('64,12 121,111 7,111')+line(64,48,64,77,navy,9)+circle(64,96,5,navy),
  helm:circle(64,64,34,'none',white,7)+circle(64,64,10,white)+Array.from({length:8},(_,i)=>{const a=i*Math.PI/4;return line(64+12*Math.cos(a),64+12*Math.sin(a),64+53*Math.cos(a),64+53*Math.sin(a),white,6)}).join('')
 };
 for(const [name,s] of Object.entries(icons))await asset('icon-'+name,128,128,s,'독립 크림색 실루엣. Preserve Aspect On. 색 변경 시 밝은 tint 권장');
 for(const name of ['captain','diver','jester','wig']) {
  const src=path.join(root,'..','01-crew','portrait-'+name+'.png');
  if(!fs.existsSync(src)) throw new Error('Missing portrait '+src);
  await sharp(src).resize(256,256,{fit:'contain',background:{r:0,g:0,b:0,alpha:0}}).png().toFile(path.join(root,'sprites','portrait-'+name+'.png'));
  assets.push({name:'portrait-'+name,file:'sprites/portrait-'+name+'.png',width:256,height:256,borderLTRB:[0,0,0,0],imageType:'Simple',usage:'기존 01-crew 원본 재사용. 새 목업의 해적 얼굴과는 다름. Unity Mask 아래 배치.'});
 }
 fs.writeFileSync(path.join(root,'manifest.json'),JSON.stringify({referenceResolution:[1920,1080],pixelsPerUnit:100,texture:'Sprite Single / FullRect / alpha / no mipmap / Clamp / Bilinear / Uncompressed',assets},null,2));
 const crypto=require('crypto');
 const template=fs.readFileSync(path.join(root,'../../../unity/UnderTheSea/Assets/Game/Art/UI/ShipCoopHud/Voyage/hp-mask.png.meta'),'utf8');
 for(const a of assets){
  const metaPath=path.join(root,a.file+'.meta');
  const prior=fs.existsSync(metaPath)?fs.readFileSync(metaPath,'utf8'):'';
  const guid=prior.match(/^guid: (\w+)/m)?.[1]||crypto.randomUUID().replaceAll('-','');
  const [l,t,r,b]=a.borderLTRB;
  fs.writeFileSync(metaPath,template.replace(/^guid: .*/m,'guid: '+guid)
    .replace(/spriteBorder: .*/,`spriteBorder: {x: ${l}, y: ${b}, z: ${r}, w: ${t}}`)
    .replace(/spriteID: .*/,'spriteID: '+crypto.createHash('md5').update(guid).digest('hex'))
    .replace(/textureCompression: 1/g,'textureCompression: 0')
    .replace(/spriteGenerateFallbackPhysicsShape: 1/,'spriteGenerateFallbackPhysicsShape: 0'));
 }
 const cols=6,cw=260,ch=190,rows=Math.ceil(assets.length/cols);
 const cells=[];let labels='';
 for(let i=0;i<assets.length;i++){
  const a=assets[i],x=(i%cols)*cw,y=Math.floor(i/cols)*ch;
  const b=await sharp(path.join(root,a.file)).resize(228,120,{fit:'inside'}).png().toBuffer();const m=await sharp(b).metadata();
  cells.push({input:b,left:x+Math.round((cw-m.width)/2),top:y+20+Math.round((120-m.height)/2)});
  labels+=`<text x="${x+12}" y="${y+161}" fill="#EDF2F6" font-size="13" font-family="Arial">${a.name}</text><text x="${x+12}" y="${y+180}" fill="#A6BCCB" font-size="12" font-family="Arial">${a.width} x ${a.height}</text>`;
 }
 cells.push({input:Buffer.from(svg(cols*cw,rows*ch,labels)),left:0,top:0});
 await sharp({create:{width:cols*cw,height:rows*ch,channels:4,background:'#304253'}}).composite(cells).png().toFile(path.join(root,'contact-sheet.png'));
 console.log(`Created ${assets.length} independent PNGs + vector sources and manifest.`);
}
main().catch(e=>{console.error(e);process.exit(1)});
