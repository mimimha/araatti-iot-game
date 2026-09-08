const sharp = require('sharp');
const path = require('path');

const root = path.resolve(__dirname, '../../unity/UnderTheSea/Assets/Game/Art/UI/Login,SignUp');
const source = path.join(root, 'start-login-reference.png');

async function cropMasked(name, crop, shape) {
  const base = await sharp(source).extract(crop).ensureAlpha().toBuffer();
  const mask = Buffer.from(`<svg width="${crop.width}" height="${crop.height}" xmlns="http://www.w3.org/2000/svg">${shape}</svg>`);
  await sharp(base).composite([{ input: mask, blend: 'dest-in' }]).png().toFile(path.join(root, name));
}

async function removeNeutralBackdrop(inputName, outputName, width, height) {
  const input = path.join(root, '_raw', inputName);
  const { data, info } = await sharp(input).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const pixels = info.width * info.height;
  const clear = new Uint8Array(pixels);
  const queue = new Int32Array(pixels);
  let head = 0;
  let tail = 0;

  const isBackdrop = (pixel) => {
    const i = pixel * 4;
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    return Math.min(r, g, b) >= 160 && Math.max(r, g, b) - Math.min(r, g, b) <= 60;
  };
  const enqueue = (pixel) => {
    if (!clear[pixel] && isBackdrop(pixel)) {
      clear[pixel] = 1;
      queue[tail++] = pixel;
    }
  };

  for (let x = 0; x < info.width; x++) {
    enqueue(x);
    enqueue((info.height - 1) * info.width + x);
  }
  for (let y = 0; y < info.height; y++) {
    enqueue(y * info.width);
    enqueue(y * info.width + info.width - 1);
  }
  while (head < tail) {
    const pixel = queue[head++];
    const x = pixel % info.width;
    const y = Math.floor(pixel / info.width);
    if (x > 0) enqueue(pixel - 1);
    if (x + 1 < info.width) enqueue(pixel + 1);
    if (y > 0) enqueue(pixel - info.width);
    if (y + 1 < info.height) enqueue(pixel + info.width);
  }
  for (let pixel = 0; pixel < pixels; pixel++) {
    const i = pixel * 4;
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    const strict = Math.min(r, g, b) >= 235 && Math.max(r, g, b) - Math.min(r, g, b) <= 8;
    if (clear[pixel] || strict) data[i + 3] = 0;
  }

  await sharp(data, { raw: info })
    .trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .resize({ width, height, fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toFile(path.join(root, outputName));
}

async function extractIcon(name, crop) {
  const { data, info } = await sharp(source).extract(crop).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const luma = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    const alpha = Math.max(0, Math.min(255, Math.round((luma - 70) * 3.5)));
    data[i + 3] = alpha;
  }
  await sharp(data, { raw: info }).trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(path.join(root, name));
}

async function main() {
  await cropMasked('tab-login-active.png', { left: 677, top: 143, width: 222, height: 72 }, '<rect x="0" y="0" width="222" height="72" rx="18" fill="white"/>');
  await cropMasked('tab-register-inactive.png', { left: 896, top: 143, width: 222, height: 72 }, '<rect x="0" y="0" width="222" height="72" rx="18" fill="white"/>');
  await cropMasked('input-email-reference.png', { left: 684, top: 248, width: 430, height: 76 }, '<rect x="0" y="0" width="430" height="76" rx="18" fill="white"/>');
  await cropMasked('input-password-reference.png', { left: 684, top: 334, width: 430, height: 76 }, '<rect x="0" y="0" width="430" height="76" rx="18" fill="white"/>');
  await cropMasked('button-login-reference.png', { left: 678, top: 423, width: 445, height: 101 }, '<rect x="0" y="0" width="445" height="101" rx="40" fill="white"/>');
  await cropMasked('button-back-reference.png', { left: 45, top: 618, width: 230, height: 75 }, '<rect x="0" y="0" width="230" height="75" rx="30" fill="white"/>');
  await extractIcon('icon-email.png', { left: 701, top: 262, width: 55, height: 50 });
  await extractIcon('icon-lock.png', { left: 704, top: 345, width: 50, height: 52 });

  await removeNeutralBackdrop('crew-checker.png', 'crew.png', 640, 450);
  await removeNeutralBackdrop('input-field-generated.png', 'input-field-base.png', 430, 76);
  await removeNeutralBackdrop('login-button-generated.png', 'button-login-base.png', 438, 94);

  // The generated panel already has true transparency; trim only its safety margin.
  await sharp(path.join(root, '_raw', 'login-panel-frame.png'))
    .trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .resize({ width: 570, height: 545, fit: 'contain', background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toFile(path.join(root, 'login-panel-frame.png'));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
