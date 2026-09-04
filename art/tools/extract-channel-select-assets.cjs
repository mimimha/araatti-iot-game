const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const root = path.resolve(__dirname, '../../Assets/Art/ChannelSelect');
const source = path.join(root, 'channel-select-screen-reference.png');

async function cropMasked(name, crop, shape) {
  const base = await sharp(source).extract(crop).ensureAlpha().toBuffer();
  const mask = Buffer.from(`<svg width="${crop.width}" height="${crop.height}" xmlns="http://www.w3.org/2000/svg">${shape}</svg>`);
  await sharp(base).composite([{ input: mask, blend: 'dest-in' }]).png().toFile(path.join(root, name));
}

async function extractGoldIcon(name, crop) {
  const { data, info } = await sharp(source).extract(crop).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    const max = Math.max(r, g, b), min = Math.min(r, g, b);
    const gold = r > 105 && g > 70 && r > b * 1.25 && g > b * 1.05;
    data[i + 3] = gold ? Math.min(255, Math.round((max - 45) * 1.5)) : 0;
  }
  await sharp(data, { raw: info }).trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(path.join(root, name));
}

async function main() {
  // Visual references, cropped from the supplied screen.
  await cropMasked('channel-row-01-reference.png', { left: 587, top: 190, width: 628, height: 103 }, '<rect x="0" y="0" width="628" height="103" rx="48" fill="white"/>');
  await cropMasked('channel-row-02-reference.png', { left: 587, top: 308, width: 628, height: 103 }, '<rect x="0" y="0" width="628" height="103" rx="48" fill="white"/>');
  await cropMasked('channel-row-03-reference.png', { left: 587, top: 416, width: 628, height: 103 }, '<rect x="0" y="0" width="628" height="103" rx="48" fill="white"/>');
  await cropMasked('refresh-reference.png', { left: 1063, top: 108, width: 170, height: 55 }, '<rect x="0" y="0" width="170" height="55" rx="25" fill="white"/>');
  await cropMasked('button-join-reference.png', { left: 721, top: 520, width: 364, height: 97 }, '<rect x="0" y="0" width="364" height="97" rx="45" fill="white"/>');
  await cropMasked('button-back-reference.png', { left: 35, top: 620, width: 218, height: 78 }, '<rect x="0" y="0" width="218" height="78" rx="35" fill="white"/>');
  await cropMasked('status-smooth-reference.png', { left: 900, top: 223, width: 116, height: 42 }, '<rect x="0" y="0" width="116" height="42" rx="12" fill="white"/>');
  await cropMasked('status-crowded-reference.png', { left: 900, top: 339, width: 116, height: 42 }, '<rect x="0" y="0" width="116" height="42" rx="12" fill="white"/>');
  await extractGoldIcon('ship-icon-01.png', { left: 628, top: 205, width: 75, height: 71 });
  await extractGoldIcon('ship-icon-reference.png', { left: 628, top: 205, width: 75, height: 71 });

  // Runtime-friendly vector-like PNG primitives (no baked labels).
  const selected = `<svg width="628" height="103" xmlns="http://www.w3.org/2000/svg"><rect x="2" y="2" width="624" height="99" rx="48" fill="none" stroke="#ffd34e" stroke-width="5"/><rect x="8" y="8" width="612" height="87" rx="42" fill="none" stroke="#4de5ff" stroke-opacity=".8" stroke-width="3"/></svg>`;
  await sharp(Buffer.from(selected)).png().toFile(path.join(root, 'channel-row-selected-overlay.png'));
  await sharp(Buffer.from('<svg width="116" height="42" xmlns="http://www.w3.org/2000/svg"><rect x="1" y="1" width="114" height="40" rx="12" fill="#087f61" stroke="#07513f" stroke-width="2"/><rect x="3" y="3" width="110" height="15" rx="8" fill="#35c69c" opacity=".35"/></svg>')).png().toFile(path.join(root, 'status-smooth-base.png'));
  await sharp(Buffer.from('<svg width="116" height="42" xmlns="http://www.w3.org/2000/svg"><rect x="1" y="1" width="114" height="40" rx="12" fill="#c86700" stroke="#713300" stroke-width="2"/><rect x="3" y="3" width="110" height="15" rx="8" fill="#ffb23b" opacity=".35"/></svg>')).png().toFile(path.join(root, 'status-crowded-base.png'));
  await sharp(Buffer.from('<svg width="64" height="64" xmlns="http://www.w3.org/2000/svg"><circle cx="32" cy="32" r="22" fill="#164f6d" stroke="#d5a33b" stroke-width="4"/><path d="M45 27a16 16 0 0 0-26-5M19 37a16 16 0 0 0 26 5" fill="none" stroke="white" stroke-width="4" stroke-linecap="round"/><path d="M18 18v10h10M46 46V36H36" fill="none" stroke="white" stroke-width="4" stroke-linecap="round"/></svg>')).png().toFile(path.join(root, 'refresh.png'));
  await sharp(Buffer.from('<svg width="360" height="92" xmlns="http://www.w3.org/2000/svg"><rect x="2" y="2" width="356" height="88" rx="42" fill="#0d4b91" stroke="#e1a73e" stroke-width="5"/><rect x="10" y="10" width="340" height="72" rx="35" fill="none" stroke="#42a7ff" stroke-opacity=".75" stroke-width="3"/></svg>')).png().toFile(path.join(root, 'button-join-base.png'));
  await sharp(Buffer.from('<svg width="116" height="103" xmlns="http://www.w3.org/2000/svg"><rect x="2" y="2" width="112" height="99" rx="48" fill="none" stroke="#d3a23f" stroke-width="4"/></svg>')).png().toFile(path.join(root, 'channel-row-base-fallback.png'));

  // Preserve exact reference for later layout calibration.
  if (!fs.existsSync(source)) throw new Error('Missing source image: ' + source);
}

main().catch((e) => { console.error(e); process.exit(1); });
