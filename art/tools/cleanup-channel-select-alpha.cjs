const sharp = require('sharp');
const path = require('path');
const fs = require('fs');
const root = path.resolve(__dirname, '../../Assets/Art/ChannelSelect');

function neutral(r, g, b) {
  const min = Math.min(r, g, b), max = Math.max(r, g, b);
  return min > 145 && max - min < 16;
}
async function clean(name) {
  const file = path.join(root, name);
  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const n = info.width * info.height, seen = new Uint8Array(n), q = new Int32Array(n);
  let h = 0, t = 0;
  const add = (p) => { if (seen[p]) return; const i = p * 4; if (neutral(data[i], data[i+1], data[i+2])) { seen[p] = 1; q[t++] = p; } };
  for (let x = 0; x < info.width; x++) { add(x); add((info.height - 1) * info.width + x); }
  for (let y = 0; y < info.height; y++) { add(y * info.width); add(y * info.width + info.width - 1); }
  while (h < t) { const p = q[h++], x = p % info.width, y = Math.floor(p / info.width); if (x) add(p-1); if (x+1 < info.width) add(p+1); if (y) add(p-info.width); if (y+1 < info.height) add(p+info.width); }
  for (let p = 0; p < n; p++) if (seen[p]) data[p * 4 + 3] = 0;
  const tmp = file + '.tmp';
  await sharp(data, { raw: info }).trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(tmp);
  fs.renameSync(tmp, file);
}
Promise.all(['channel-panel-frame.png', 'channel-row-base.png'].map(clean)).catch((e) => { console.error(e); process.exit(1); });
