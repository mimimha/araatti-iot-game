// Final deterministic repair for the generated panel's top crest.
// Closes the accidental dark opening and adds transparent top safe-area.
const sharp = require('sharp');
const path = require('node:path');

async function main() {
  const source = path.resolve(process.argv[2]);
  const destination = path.resolve(process.argv[3]);
  const meta = await sharp(source).metadata();
  if (meta.width !== 1166 || meta.height !== 1221 || !meta.hasAlpha) {
    throw new Error(`Unexpected source geometry: ${meta.width}x${meta.height}, alpha=${meta.hasAlpha}`);
  }

  // Coordinates are limited to the small hollow in the upper gold cap.
  // The gradient follows the existing upper-left lighting and retains the dark
  // bevel at the bottom, so the cap reads as solid metal rather than a flat patch.
  const patch = Buffer.from(`<svg xmlns="http://www.w3.org/2000/svg" width="1166" height="1221">
    <defs>
      <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">
        <stop offset="0" stop-color="#fff1a5"/>
        <stop offset="0.28" stop-color="#ffd257"/>
        <stop offset="0.68" stop-color="#e89b16"/>
        <stop offset="1" stop-color="#9a5609"/>
      </linearGradient>
    </defs>
    <path d="M563 52 C566 37 576 29 583 29 C590 29 600 37 603 52 C598 49 592 47 583 47 C574 47 568 49 563 52 Z"
          fill="url(#gold)" stroke="#9a5708" stroke-width="3" stroke-linejoin="round"/>
    <path d="M568 43 C573 34 578 32 583 32 C589 32 594 35 598 43"
          fill="none" stroke="#fff0a0" stroke-width="3" stroke-linecap="round" opacity="0.9"/>
  </svg>`);

  await sharp(source)
    .composite([{ input: patch, blend: 'over' }])
    .extend({ top: 48, bottom: 0, left: 0, right: 0, background: { r: 0, g: 0, b: 0, alpha: 0 } })
    .png()
    .toFile(destination);

  const out = await sharp(destination).metadata();
  if (!out.hasAlpha || out.width !== 1166 || out.height !== 1269) throw new Error('Output validation failed.');
  console.log(JSON.stringify({ source, destination, width: out.width, height: out.height, hasAlpha: out.hasAlpha, topSafeArea: 56 }));
}
main().catch(error => { console.error(error); process.exitCode = 1; });
