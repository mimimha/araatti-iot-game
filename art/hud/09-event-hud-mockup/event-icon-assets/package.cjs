const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const root = __dirname;
const sourceDir = path.join(root, 'source');
const iconDir = path.join(root, 'icons');
fs.mkdirSync(sourceDir, { recursive: true });
fs.mkdirSync(iconDir, { recursive: true });

const assets = [
  ['reef', '암초', 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-361fb1d7-a77c-4cad-bf33-61463369d88b.png'],
  ['enemy-ship', '적선', 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-3fbb779e-3005-481a-944b-f4c97d1a6e43.png'],
  ['hull-damage', '선체 파손·침수', 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-bd7cca7d-6e36-4866-8920-4f51e09b6c42.png'],
  ['big-wave', '거대한 파도', 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-c6ff67f8-727a-41ff-b850-db272dc70441.png'],
  ['squall', '돌풍', 'C:/Users/SSAFY/.codex/generated_images/01a0a8a8-b978-78e3-9c8a-0d9aaf37bfef/exec-c0dfe7a2-2a3d-4922-bfe1-c1ed9f2a4984.png']
];

async function main() {
  const records = [];
  const previewParts = [];
  for (let i = 0; i < assets.length; i++) {
    const [name, label, source] = assets[i];
    const sourceCopy = path.join(sourceDir, `${name}.png`);
    fs.copyFileSync(source, sourceCopy);
    const meta = await sharp(sourceCopy).metadata();
    if (!meta.hasAlpha) throw new Error(`Missing alpha: ${name}`);

    const trimmed = await sharp(sourceCopy).trim({ background: '#00000000', threshold: 3 }).png().toBuffer();
    const fitted = await sharp(trimmed).resize(432, 432, { fit: 'inside' }).png().toBuffer();
    const fittedMeta = await sharp(fitted).metadata();
    const output = path.join(iconDir, `event-${name}.png`);
    await sharp({ create: { width: 512, height: 512, channels: 4, background: '#00000000' } })
      .composite([{ input: fitted, left: Math.floor((512 - fittedMeta.width) / 2), top: Math.floor((512 - fittedMeta.height) / 2) }])
      .png().toFile(output);

    const { data, info } = await sharp(output).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let clear = 0, solid = 0, partial = 0;
    for (let p = 3; p < data.length; p += info.channels) {
      if (data[p] === 0) clear++;
      else if (data[p] === 255) solid++;
      else partial++;
    }
    if (!clear || !solid) throw new Error(`Invalid alpha: ${name}`);
    records.push({ name, label, file: `icons/event-${name}.png`, width: 512, height: 512, clearPixels: clear, partialAlphaPixels: partial });

    const x = 18 + i * 300;
    const large = await sharp(output).resize(220, 220).png().toBuffer();
    const small = await sharp(output).resize(56, 56).png().toBuffer();
    previewParts.push({ input: large, left: x + 40, top: 120 });
    previewParts.push({ input: small, left: x + 122, top: 390 });
    previewParts.push({ input: Buffer.from(`<svg width="296" height="80"><text x="148" y="34" text-anchor="middle" font-family="Malgun Gothic,sans-serif" font-size="24" fill="#fff1d0">${label}</text><text x="148" y="65" text-anchor="middle" font-family="Arial,sans-serif" font-size="14" fill="#8faec1">512 PNG / 56 px</text></svg>`), left: x, top: 490 });
  }

  const bg = Buffer.from('<svg width="1536" height="620"><defs><pattern id="p" width="32" height="32" patternUnits="userSpaceOnUse"><rect width="32" height="32" fill="#183247"/><path d="M0 0h16v16H0zM16 16h16v16H16z" fill="#203d52"/></pattern></defs><rect width="1536" height="620" fill="url(#p)"/><text x="64" y="58" font-family="Arial,sans-serif" font-size="28" fill="#fff1d0">ALL 5 VOYAGE EVENTS — TRANSPARENCY &amp; SIZE CHECK</text></svg>');
  await sharp(bg).composite(previewParts).png().toFile(path.join(root, 'preview.png'));
  fs.writeFileSync(path.join(root, 'manifest.json'), JSON.stringify({
    reference: '../event-icon-style-sheet-v1.png',
    importSettings: { textureType: 'Sprite (2D and UI)', spriteMode: 'Single', alphaIsTransparency: true, mipmaps: false, wrapMode: 'Clamp', filterMode: 'Bilinear', compression: 'None' },
    assets: records
  }, null, 2));
  console.log(JSON.stringify(records, null, 2));
}

main().catch(error => { console.error(error); process.exit(1); });
