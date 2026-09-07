// Remove only border-connected neutral backgrounds; preserve source artwork.
// Usage: node prepare-unified-ui-alpha.cjs <source-directory> <output-directory>
const fs = require('node:fs');
const path = require('node:path');
const sharp = require('sharp');

async function prepare(source, destination) {
  const { data, info } = await sharp(source).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h } = info;
  const n = w * h;
  const original = Buffer.from(data);
  const exterior = new Uint8Array(n);
  const queue = new Int32Array(n);
  let head = 0, tail = 0;
  const visit = (p) => {
    if (exterior[p]) return;
    const i = p * 4;
    const lo = Math.min(data[i], data[i + 1], data[i + 2]);
    const hi = Math.max(data[i], data[i + 1], data[i + 2]);
    if (data[i + 3] === 0 || (lo > 145 && hi - lo < 26)) {
      exterior[p] = 1;
      queue[tail++] = p;
    }
  };
  // Existing RGBA cutouts already have an alpha matte. Do not recolor their edges.
  const hasTransparency = original.some((v, i) => i % 4 === 3 && v < 255);
  if (!hasTransparency) {
    for (let x = 0; x < w; x++) { visit(x); visit((h - 1) * w + x); }
    for (let y = 0; y < h; y++) { visit(y * w); visit(y * w + w - 1); }
    while (head < tail) {
      const p = queue[head++], x = p % w;
      if (x > 0) visit(p - 1);
      if (x < w - 1) visit(p + 1);
      if (p >= w) visit(p - w);
      if (p < n - w) visit(p + w);
    }
    for (let p = 0; p < n; p++) if (exterior[p]) data[p * 4 + 3] = 0;
    // Recover fractional coverage of pale boundary pixels from nearby solid color.
    // This affects only the immediate outer edge, never the enclosed highlights.
    for (let p = 0; p < n; p++) {
      if (exterior[p]) continue;
      const x = p % w, y = Math.floor(p / w), i = p * 4;
      const adjacent = (x > 0 && exterior[p - 1]) || (x < w - 1 && exterior[p + 1]) ||
        (y > 0 && exterior[p - w]) || (y < h - 1 && exterior[p + w]);
      if (!adjacent || Math.min(data[i], data[i + 1], data[i + 2]) < 160) continue;
      let best = null, bestDistance = Infinity;
      for (let dy = -2; dy <= 2; dy++) for (let dx = -2; dx <= 2; dx++) {
        const xx = x + dx, yy = y + dy;
        if (xx < 0 || xx >= w || yy < 0 || yy >= h) continue;
        const q = yy * w + xx, j = q * 4;
        if (exterior[q]) continue;
        const lo = Math.min(original[j], original[j + 1], original[j + 2]);
        const hi = Math.max(original[j], original[j + 1], original[j + 2]);
        const distance = dx * dx + dy * dy;
        if (hi - lo > 65 && lo < 150 && distance < bestDistance) {
          best = [original[j], original[j + 1], original[j + 2]];
          bestDistance = distance;
        }
      }
      if (!best) continue;
      let numerator = 0, denominator = 0;
      for (let c = 0; c < 3; c++) {
        numerator += (original[i + c] - 250) * (best[c] - 250);
        denominator += (best[c] - 250) ** 2;
      }
      const alpha = Math.max(0, Math.min(1, numerator / denominator));
      if (alpha < 0.12 || alpha > 0.97) continue;
      data[i + 3] = Math.round(alpha * 255);
      for (let c = 0; c < 3; c++) data[i + c] = Math.round(Math.max(0, Math.min(255,
        (original[i + c] - 250 * (1 - alpha)) / alpha)));
    }
  }
  let left = w, top = h, right = -1, bottom = -1, transparent = 0, softened = 0;
  let interiorChanged = 0;
  for (let p = 0; p < n; p++) {
    const i = p * 4, a = data[i + 3];
    if (!a) { transparent++; continue; }
    if (a < 255) softened++;
    if (a === 255 && (data[i] !== original[i] || data[i + 1] !== original[i + 1] || data[i + 2] !== original[i + 2])) interiorChanged++;
    const x = p % w, y = Math.floor(p / w);
    left = Math.min(left, x); top = Math.min(top, y);
    right = Math.max(right, x); bottom = Math.max(bottom, y);
  }
  if (right < left || transparent === 0 || interiorChanged > 0) throw new Error(`Invalid matte: ${source}`);
  const crop = { left, top, width: right - left + 1, height: bottom - top + 1 };
  await sharp(data, { raw: { width: w, height: h, channels: 4 } })
    .extract(crop).extend({ top: 8, bottom: 8, left: 8, right: 8,
      background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(destination);
  const out = await sharp(destination).metadata();
  return { name: path.basename(source), sourceWidth: w, sourceHeight: h,
    width: out.width, height: out.height, hasAlpha: out.hasAlpha,
    retainedExistingAlpha: hasTransparency, removedBackgroundPixels: tail,
    partiallyTransparentPixels: softened, unchangedOpaqueInterior: interiorChanged === 0, crop };
}

async function main() {
  if (process.argv.length !== 4) throw new Error('Supply source and output directories.');
  const sourceDir = path.resolve(process.argv[2]), outputDir = path.resolve(process.argv[3]);
  if (sourceDir === outputDir) throw new Error('Output must differ from source.');
  const files = fs.readdirSync(sourceDir).filter(name => name.endsWith('.png')).sort();
  if (!files.length) throw new Error('No PNG inputs.');
  for (const file of files) if (fs.existsSync(path.join(outputDir, file))) throw new Error(`Refusing to overwrite ${file}`);
  fs.mkdirSync(outputDir, { recursive: true });
  const report = [];
  for (const file of files) report.push(await prepare(path.join(sourceDir, file), path.join(outputDir, file)));
  fs.writeFileSync(path.join(outputDir, 'alpha-report.json'), JSON.stringify(report, null, 2) + '\n');
  console.log(JSON.stringify(report, null, 2));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
