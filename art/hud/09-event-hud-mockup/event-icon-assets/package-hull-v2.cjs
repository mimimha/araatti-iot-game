const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const root = __dirname;
const version = process.argv[2] === 'v3' ? 'v3' : 'v2';
const sources = {
  v2: 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-c148cc2f-072b-4c60-bc19-1222211c736d.png',
  v3: 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-bd7cca7d-6e36-4866-8920-4f51e09b6c42.png'
};
const source = sources[version];
const sourceCopy = path.join(root, 'source', `hull-damage-${version}.png`);
const output = path.join(root, 'icons', `event-hull-damage-${version}.png`);

async function main() {
  if (!fs.existsSync(sourceCopy)) fs.copyFileSync(source, sourceCopy);
  const meta = await sharp(sourceCopy).metadata();
  if (!meta.hasAlpha) throw new Error('Transparent source required');
  const trimmed = await sharp(sourceCopy).trim({ background: '#00000000', threshold: 3 }).png().toBuffer();
  const fitted = await sharp(trimmed).resize(432, 432, { fit: 'inside' }).png().toBuffer();
  const fm = await sharp(fitted).metadata();
  await sharp({ create: { width: 512, height: 512, channels: 4, background: '#00000000' } })
    .composite([{ input: fitted, left: Math.floor((512 - fm.width) / 2), top: Math.floor((512 - fm.height) / 2) }])
    .png().toFile(output);

  const { data, info } = await sharp(output).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  let clear = 0, solid = 0, partial = 0;
  for (let p = 3; p < data.length; p += info.channels) {
    if (data[p] === 0) clear++; else if (data[p] === 255) solid++; else partial++;
  }
  if (!clear || !solid) throw new Error('Invalid alpha');

  const oldIcon = path.join(root, 'icons', version === 'v3' ? 'event-hull-damage-v2.png' : 'event-hull-damage.png');
  const previousLabel = version === 'v3' ? 'v2 · 파손 구멍 + 물 유입' : '기존';
  const currentLabel = version === 'v3' ? 'v3 · 돛 + 배 실루엣 + 침수' : 'v2 · 파손 구멍 + 물 유입';
  const checker = Buffer.from(`<svg width="1100" height="650"><defs><pattern id="p" width="32" height="32" patternUnits="userSpaceOnUse"><rect width="32" height="32" fill="#183247"/><path d="M0 0h16v16H0zM16 16h16v16H16z" fill="#203d52"/></pattern></defs><rect width="1100" height="650" fill="url(#p)"/><text x="48" y="54" font-family="Arial,sans-serif" font-size="26" fill="#fff1d0">HULL DAMAGE + FLOODING — READABILITY REVISION</text><text x="275" y="600" text-anchor="middle" font-family="Malgun Gothic,sans-serif" font-size="24" fill="#9bb3c4">${previousLabel}</text><text x="825" y="600" text-anchor="middle" font-family="Malgun Gothic,sans-serif" font-size="24" fill="#fff1d0">${currentLabel}</text></svg>`);
  const oldLarge = await sharp(oldIcon).resize(340, 340).png().toBuffer();
  const newLarge = await sharp(output).resize(340, 340).png().toBuffer();
  const oldSmall = await sharp(oldIcon).resize(56, 56).png().toBuffer();
  const newSmall = await sharp(output).resize(56, 56).png().toBuffer();
  await sharp(checker).composite([
    { input: oldLarge, left: 105, top: 105 }, { input: newLarge, left: 655, top: 105 },
    { input: oldSmall, left: 247, top: 480 }, { input: newSmall, left: 797, top: 480 }
  ]).png().toFile(path.join(root, `hull-damage-${version}-comparison.png`));
  console.log(JSON.stringify({ file: `icons/event-hull-damage-${version}.png`, width: 512, height: 512, clearPixels: clear, partialAlphaPixels: partial }, null, 2));
}

main().catch(error => { console.error(error); process.exit(1); });
