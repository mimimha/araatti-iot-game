const sharp = require('sharp');
const path = require('path');

const root = path.resolve(__dirname, '../../Assets/Art/CharacterCustomization');

function isNeutral(r, g, b) {
  const min = Math.min(r, g, b), max = Math.max(r, g, b);
  return min > 145 && max - min < 16;
}

async function clean(file) {
  const full = path.join(root, file);
  const { data, info } = await sharp(full).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const count = info.width * info.height;
  const seen = new Uint8Array(count);
  const queue = new Int32Array(count);
  let head = 0, tail = 0;
  const add = (p) => {
    if (seen[p]) return;
    const i = p * 4;
    if (isNeutral(data[i], data[i + 1], data[i + 2])) { seen[p] = 1; queue[tail++] = p; }
  };
  for (let x = 0; x < info.width; x++) { add(x); add((info.height - 1) * info.width + x); }
  for (let y = 0; y < info.height; y++) { add(y * info.width); add(y * info.width + info.width - 1); }
  while (head < tail) {
    const p = queue[head++], x = p % info.width, y = Math.floor(p / info.width);
    if (x > 0) add(p - 1); if (x + 1 < info.width) add(p + 1);
    if (y > 0) add(p - info.width); if (y + 1 < info.height) add(p + info.width);
  }
  for (let p = 0; p < count; p++) if (seen[p]) data[p * 4 + 3] = 0;
  await sharp(data, { raw: info }).trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(full + '.tmp');
  require('fs').renameSync(full + '.tmp', full);
}

Promise.all(['character-preview.png', 'customization-panel-frame.png'].map(clean)).catch((e) => { console.error(e); process.exit(1); });
