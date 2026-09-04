const sharp = require('sharp');
const path = require('path');

const root = path.resolve(__dirname, '../../unity/UnderTheSea/Assets/Game/Art/UI/Title');
const source = path.join(root, 'start-screen-reference.png');

async function extractWithMask(name, crop, svgShapes) {
  const base = await sharp(source)
    .extract(crop)
    .ensureAlpha()
    .toBuffer();

  const mask = Buffer.from(
    `<svg width="${crop.width}" height="${crop.height}" xmlns="http://www.w3.org/2000/svg">
      ${svgShapes}
    </svg>`
  );

  await sharp(base)
    .composite([{ input: mask, blend: 'dest-in' }])
    .png()
    .toFile(path.join(root, name));
}

async function removeCheckerboard(inputName, outputName, targetWidth) {
  const input = path.join(root, '_raw', inputName);
  const { data, info } = await sharp(input)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });

  const pixelCount = info.width * info.height;
  const clear = new Uint8Array(pixelCount);
  const queue = new Int32Array(pixelCount);
  let head = 0;
  let tail = 0;

  function broadBackground(pixel) {
    const i = pixel * 4;
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    const hi = Math.max(r, g, b);
    const lo = Math.min(r, g, b);
    return lo >= 160 && hi - lo <= 60;
  }

  function enqueue(pixel) {
    if (!clear[pixel] && broadBackground(pixel)) {
      clear[pixel] = 1;
      queue[tail++] = pixel;
    }
  }

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

  for (let pixel = 0; pixel < pixelCount; pixel++) {
    const i = pixel * 4;
    const r = data[i];
    const g = data[i + 1];
    const b = data[i + 2];
    const hi = Math.max(r, g, b);
    const lo = Math.min(r, g, b);
    const strictChecker = lo >= 238 && hi - lo <= 8;
    if (clear[pixel] || strictChecker) data[i + 3] = 0;
  }

  await sharp(data, { raw: info })
    .trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .resize({ width: targetWidth, withoutEnlargement: true })
    .png()
    .toFile(path.join(root, outputName));
}

async function createReusableButtonStates() {
  const width = 475;
  const height = 176;
  const transparentCanvas = {
    create: {
      width,
      height,
      channels: 4,
      background: { r: 0, g: 0, b: 0, alpha: 0 }
    }
  };

  const frame = await sharp(path.join(root, 'button-frame.png'))
    .resize({ width: 430, height: 128, fit: 'fill' })
    .png()
    .toBuffer();

  const base = await sharp(transparentCanvas)
    .composite([{ input: frame, left: 22, top: 28 }])
    .png()
    .toBuffer();
  await sharp(base).toFile(path.join(root, 'button-base.png'));

  const decoration = await sharp(path.join(root, '_raw', 'button-selected-decoration-generated.png'))
    .trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .resize({
      width,
      height,
      fit: 'contain',
      background: { r: 0, g: 0, b: 0, alpha: 0 }
    })
    .png()
    .toBuffer();
  await sharp(decoration).toFile(path.join(root, 'button-selected-decoration.png'));

  await sharp(base)
    .composite([{ input: decoration, left: 0, top: 0 }])
    .png()
    .toFile(path.join(root, 'button-selected.png'));
}

async function main() {
  // Pixel-faithful clickable controls from the supplied start-screen artwork.
  await extractWithMask(
    'button-start.png',
    { left: 597, top: 497, width: 475, height: 176 },
    [
      '<rect x="43" y="31" width="389" height="115" rx="56" fill="white"/>',
      '<ellipse cx="238" cy="34" rx="32" ry="31" fill="white"/>',
      '<path d="M58 103 C52 119 34 129 23 145" fill="none" stroke="white" stroke-width="14" stroke-linecap="round"/>',
      '<path d="M18 141 L32 151 L18 171 L5 159 Z" fill="white"/>',
      '<path d="M417 103 C423 119 441 129 452 145" fill="none" stroke="white" stroke-width="14" stroke-linecap="round"/>',
      '<path d="M457 141 L443 151 L457 171 L470 159 Z" fill="white"/>'
    ].join('\n')
  );

  await extractWithMask(
    'button-settings.png',
    { left: 640, top: 651, width: 391, height: 116 },
    '<rect x="7" y="8" width="377" height="100" rx="50" fill="white"/>'
  );

  await extractWithMask(
    'button-exit.png',
    { left: 641, top: 758, width: 390, height: 116 },
    '<rect x="7" y="8" width="376" height="100" rx="50" fill="white"/>'
  );

  await extractWithMask(
    'icon-settings.png',
    { left: 1463, top: 36, width: 77, height: 76 },
    '<ellipse cx="38.5" cy="38" rx="37" ry="37" fill="white"/>'
  );

  await extractWithMask(
    'icon-sound-on.png',
    { left: 1564, top: 36, width: 78, height: 76 },
    '<ellipse cx="39" cy="38" rx="37" ry="37" fill="white"/>'
  );

  // Image-generated isolates arrive with a checkerboard preview baked in;
  // convert that neutral preview field into a real alpha channel and trim it.
  await removeCheckerboard('logo-checker.png', 'logo.png', 930);
  await removeCheckerboard('button-frame-checker.png', 'button-frame.png', 430);
  await createReusableButtonStates();

  const layers = [
    ['logo.png', 371, 22],
    ['button-start.png', 597, 497],
    ['button-settings.png', 640, 651],
    ['button-exit.png', 641, 758],
    ['icon-settings.png', 1463, 36],
    ['icon-sound-on.png', 1564, 36]
  ];
  await sharp(path.join(root, 'background-clean.png'))
    .composite(layers.map(([file, left, top]) => ({
      input: path.join(root, file),
      left,
      top
    })))
    .png()
    .toFile(path.join(root, 'start-screen-preview.png'));
}

main().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
