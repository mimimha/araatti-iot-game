const sharp = require('sharp');
const path = require('path');

const root = path.resolve(__dirname, '../../Assets/Art/CharacterCustomization');
const source = path.join(root, 'customization-screen-reference.png');
const rawDir = path.join(root, '_raw');

async function crop(name, left, top, width, height) {
  await sharp(source).extract({ left, top, width, height }).png().toFile(path.join(root, name));
}

async function cropMasked(name, cropRect, shape) {
  const base = await sharp(source).extract(cropRect).ensureAlpha().toBuffer();
  const mask = Buffer.from(`<svg width="${cropRect.width}" height="${cropRect.height}" xmlns="http://www.w3.org/2000/svg">${shape}</svg>`);
  await sharp(base).composite([{ input: mask, blend: 'dest-in' }]).png().toFile(path.join(root, name));
}

async function extractDarkIcon(name, cropRect) {
  const { data, info } = await sharp(source).extract(cropRect).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  for (let i = 0; i < data.length; i += 4) {
    const luma = 0.299 * data[i] + 0.587 * data[i + 1] + 0.114 * data[i + 2];
    const alpha = Math.max(0, Math.min(255, Math.round((205 - luma) * 4.0)));
    data[i + 3] = alpha;
  }
  await sharp(data, { raw: info }).trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(path.join(root, name));
}

async function extractShoe(name, cropRect) {
  const { data, info } = await sharp(source).extract(cropRect).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  // Remove the warm paper/tile background while retaining dark, saturated shoe pixels.
  for (let i = 0; i < data.length; i += 4) {
    const r = data[i], g = data[i + 1], b = data[i + 2];
    const min = Math.min(r, g, b), max = Math.max(r, g, b);
    const saturation = max - min;
    const dark = min < 150;
    const pixel = i / 4;
    const x = pixel % info.width;
    const y = Math.floor(pixel / info.width);
    const likelySelectionCheck = x > info.width * 0.78 && y > info.height * 0.70;
    const alpha = (!likelySelectionCheck && (dark || saturation > 48)) ? 255 : 0;
    data[i + 3] = alpha;
  }
  await sharp(data, { raw: info }).trim({ background: { r: 0, g: 0, b: 0, alpha: 0 } }).png().toFile(path.join(root, name));
}

async function main() {
  // Navigation and gender controls.
  await cropMasked('arrow-left-reference.png', { left: 58, top: 466, width: 66, height: 70 }, '<circle cx="33" cy="35" r="31" fill="white"/>');
  await cropMasked('arrow-right-reference.png', { left: 286, top: 466, width: 66, height: 70 }, '<circle cx="33" cy="35" r="31" fill="white"/>');
  await sharp(Buffer.from('<svg width="72" height="72" xmlns="http://www.w3.org/2000/svg"><circle cx="36" cy="36" r="30" fill="#1b5f78" fill-opacity=".35" stroke="white" stroke-opacity=".75" stroke-width="2"/><path d="M42 22L28 36l14 14" fill="none" stroke="white" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/></svg>')).png().toFile(path.join(root, 'arrow-left.png'));
  await sharp(Buffer.from('<svg width="72" height="72" xmlns="http://www.w3.org/2000/svg"><circle cx="36" cy="36" r="30" fill="#1b5f78" fill-opacity=".35" stroke="white" stroke-opacity=".75" stroke-width="2"/><path d="M30 22l14 14-14 14" fill="none" stroke="white" stroke-width="7" stroke-linecap="round" stroke-linejoin="round"/></svg>')).png().toFile(path.join(root, 'arrow-right.png'));
  await extractDarkIcon('gender-male.png', { left: 198, top: 603, width: 42, height: 59 });
  await extractDarkIcon('gender-female.png', { left: 247, top: 603, width: 43, height: 59 });
  // Clean runtime gender glyphs; the screenshot's pedestal texture is intentionally omitted.
  await sharp(Buffer.from('<svg width="64" height="64" xmlns="http://www.w3.org/2000/svg"><g fill="none" stroke="#655c4d" stroke-width="5" stroke-linecap="round"><circle cx="27" cy="37" r="14"/><path d="M37 27L54 10M44 10h10v10"/></g></svg>')).png().toFile(path.join(root, 'gender-male.png'));
  await sharp(Buffer.from('<svg width="64" height="64" xmlns="http://www.w3.org/2000/svg"><g fill="none" stroke="#655c4d" stroke-width="5" stroke-linecap="round"><circle cx="32" cy="25" r="14"/><path d="M32 39v17M23 49h18"/></g></svg>')).png().toFile(path.join(root, 'gender-female.png'));

  // Category tabs (six static references; use TMP labels/icons in the live UI).
  const tabs = [
    ['tab-hair-reference.png', 389], ['tab-face-reference.png', 495],
    ['tab-top-reference.png', 600], ['tab-bottom-reference.png', 704],
    ['tab-shoes-active-reference.png', 808], ['tab-accessory-reference.png', 912]
  ];
  for (const [name, left] of tabs) await cropMasked(name, { left, top: 158, width: 101, height: 94 }, '<rect x="0" y="0" width="101" height="94" rx="14" fill="white"/>');

  // Reusable item tiles and ten shoe item sprites from the screenshot.
  const cols = [397, 527, 656, 785, 914];
  const rows = [288, 394];
  let index = 1;
  for (const top of rows) {
    for (const left of cols) {
      await cropMasked(`shoe-slot-${String(index).padStart(2, '0')}-reference.png`, { left, top, width: 118, height: 94 }, '<rect x="0" y="0" width="118" height="94" rx="12" fill="white"/>');
      await extractShoe(`shoe-${String(index).padStart(2, '0')}.png`, { left: left + 10, top: top + 7, width: 98, height: 78 });
      index++;
    }
  }

  // Runtime-reusable empty tile and selected-state overlay (code-native UI primitives).
  const slotSvg = Buffer.from(`<svg width="118" height="94" xmlns="http://www.w3.org/2000/svg"><rect x="1" y="1" width="116" height="92" rx="12" fill="#f5ead8" stroke="#d1b183" stroke-width="2"/><rect x="4" y="4" width="110" height="86" rx="10" fill="none" stroke="#fff8ea" stroke-width="2" opacity=".7"/></svg>`);
  await sharp(slotSvg).png().toFile(path.join(root, 'item-slot-base.png'));
  const selectedSvg = Buffer.from(`<svg width="118" height="94" xmlns="http://www.w3.org/2000/svg"><rect x="2" y="2" width="114" height="90" rx="12" fill="none" stroke="#ffad16" stroke-width="4"/><circle cx="101" cy="77" r="12" fill="#f59b13" stroke="#fff5d5" stroke-width="2"/><path d="M95 77l4 4 8-9" fill="none" stroke="white" stroke-width="3" stroke-linecap="round" stroke-linejoin="round"/></svg>`);
  await sharp(selectedSvg).png().toFile(path.join(root, 'item-selection-overlay.png'));

  // Color picker references and completion button.
  await cropMasked('swatch-selected-reference.png', { left: 402, top: 529, width: 52, height: 52 }, '<circle cx="26" cy="26" r="25" fill="white"/>');
  await cropMasked('swatch-reference.png', { left: 465, top: 529, width: 52, height: 52 }, '<circle cx="26" cy="26" r="25" fill="white"/>');
  await cropMasked('button-complete-reference.png', { left: 773, top: 588, width: 235, height: 58 }, '<rect x="0" y="0" width="235" height="58" rx="14" fill="white"/>');

  // A compact implementation preview using the original screenshot as a visual QA reference.
  await sharp(source).resize({ width: 1097, height: 732 }).png().toFile(path.join(root, 'character-customization-preview.png'));
}

main().catch((error) => { console.error(error); process.exit(1); });
