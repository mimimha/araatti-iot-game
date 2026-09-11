const fs=require('fs'),path=require('path'),assert=require('assert'),sharp=require('sharp');
(async()=>{
 const {assets}=JSON.parse(fs.readFileSync(path.join(__dirname,'manifest.json')));
 const guids=new Set();
 for(const a of assets){
  const p=path.join(__dirname,a.file),m=await sharp(p).metadata();
  assert.equal(m.width,a.width,a.name);assert.equal(m.height,a.height,a.name);assert(m.hasAlpha,a.name+' alpha');
  const {data,info}=await sharp(p).ensureAlpha().raw().toBuffer({resolveWithObject:true});
  if(a.name.includes('frame')||a.name==='ring-fill-white'||a.name==='ring-track')
   assert.equal(data[(Math.floor(info.height/2)*info.width+Math.floor(info.width/2))*4+3],0,a.name+' transparent center');
  const meta=fs.readFileSync(p+'.meta','utf8');const guid=meta.match(/^guid: (\w+)/m)[1];assert(!guids.has(guid),'duplicate GUID');guids.add(guid);
  assert(meta.includes('enableMipMap: 0'));assert(meta.includes('textureType: 8'));
 }
 const html=fs.readFileSync(path.join(__dirname,'preview.html'),'utf8');
 for(const match of html.matchAll(/(?:src="|url\()(sprites\/[a-z0-9-]+\.png)/g))assert(fs.existsSync(path.join(__dirname,match[1])),match[1]);
 console.log(`PASS: ${assets.length} PNG sizes, alpha channels, hollow centers, unique Sprite GUIDs, and static preview references.`);
})().catch(e=>{console.error(e);process.exit(1)});
