const fs = require('fs');
const path = require('path');

const out = __dirname;
const W = 2048;
const H = 512;
const panel = { x: 72, y: 82, w: 1904, h: 340, r: 150 };
const track = { x: 640, y: 190, w: 1000, h: 132, r: 66 };

const defs = `
<defs>
  <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#fff1a2"/>
    <stop offset="0.18" stop-color="#f6bd38"/>
    <stop offset="0.48" stop-color="#8a470f"/>
    <stop offset="0.7" stop-color="#d88b20"/>
    <stop offset="1" stop-color="#522507"/>
  </linearGradient>
  <linearGradient id="navy" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#173e70"/>
    <stop offset="0.5" stop-color="#08284f"/>
    <stop offset="1" stop-color="#04182f"/>
  </linearGradient>
  <linearGradient id="track" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#35465a"/>
    <stop offset="1" stop-color="#1d2a38"/>
  </linearGradient>
  <linearGradient id="green" x1="0" y1="0" x2="0" y2="1">
    <stop offset="0" stop-color="#8ff0ab"/>
    <stop offset="0.45" stop-color="#4ad38f"/>
    <stop offset="1" stop-color="#20a86e"/>
  </linearGradient>
  <filter id="shadow" x="-20%" y="-35%" width="140%" height="180%">
    <feDropShadow dx="0" dy="15" stdDeviation="12" flood-color="#06101e" flood-opacity=".72"/>
  </filter>
  <filter id="softGlow" x="-20%" y="-60%" width="140%" height="220%">
    <feGaussianBlur stdDeviation="8" result="g"/><feMerge><feMergeNode in="g"/><feMergeNode in="SourceGraphic"/></feMerge>
  </filter>
  <clipPath id="panelClip"><rect x="${panel.x}" y="${panel.y}" width="${panel.w}" height="${panel.h}" rx="${panel.r}"/></clipPath>
</defs>`;

const anchor = `
<g transform="translate(1024 69)" filter="url(#shadow)">
  <circle r="84" fill="#082a54" stroke="url(#gold)" stroke-width="22"/>
  <circle r="55" fill="none" stroke="#d79526" stroke-width="7"/>
  <path d="M0-54v111M-24-31h48M-48 25c5 50 91 50 96 0M-48 25l-22 19M48 25l22 19"
        fill="none" stroke="#f2bd46" stroke-width="14" stroke-linecap="round" stroke-linejoin="round"/>
</g>`;

const leaves = `
<g transform="translate(210 256)" fill="#8ecb48" stroke="#315d25" stroke-width="7" stroke-linejoin="round">
  <path d="M0 64V-42" fill="none" stroke="#a4d65d" stroke-width="14" stroke-linecap="round"/>
  <path d="M-4-14C-88-20-95-91-91-119C-41-111-6-78-4-14Z"/>
  <path d="M7-31C23-105 87-112 115-106C103-54 65-25 7-31Z"/>
</g>`;

const rivets = [
  [panel.x+28, panel.y+panel.h/2], [panel.x+panel.w-28, panel.y+panel.h/2]
].map(([cx,cy]) => `<circle cx="${cx}" cy="${cy}" r="24" fill="#5b2c0a" stroke="url(#gold)" stroke-width="15"/>`).join('');

const baseBody = `
<g filter="url(#shadow)">
  <rect x="${panel.x}" y="${panel.y}" width="${panel.w}" height="${panel.h}" rx="${panel.r}" fill="#311604"/>
  <rect x="${panel.x+10}" y="${panel.y+10}" width="${panel.w-20}" height="${panel.h-20}" rx="${panel.r-10}" fill="url(#gold)"/>
  <rect x="${panel.x+34}" y="${panel.y+34}" width="${panel.w-68}" height="${panel.h-68}" rx="${panel.r-34}" fill="url(#navy)" stroke="#06162b" stroke-width="10"/>
  <path d="M150 121H1898" stroke="#ffe693" stroke-opacity=".55" stroke-width="8" stroke-linecap="round"/>
  ${rivets}
</g>
${anchor}
${leaves}
<rect x="${track.x}" y="${track.y}" width="${track.w}" height="${track.h}" rx="${track.r}" fill="url(#track)" stroke="#061321" stroke-width="12"/>
<path d="M${track.x+38} ${track.y+24}H${track.x+track.w-38}" stroke="#708399" stroke-opacity=".42" stroke-width="10" stroke-linecap="round"/>
`;

function svg(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${W}" height="${H}" viewBox="0 0 ${W} ${H}">${defs}${body}</svg>`;
}

const base = svg(baseBody);
const fill = svg(`<rect x="${track.x}" y="${track.y}" width="${track.w}" height="${track.h}" rx="${track.r}" fill="url(#green)" filter="url(#softGlow)"/>
<path d="M${track.x+40} ${track.y+25}H${track.x+track.w-40}" stroke="#c9ffdb" stroke-opacity=".48" stroke-width="11" stroke-linecap="round"/>`);

const previewFillWidth = Math.round(track.w * 0.68);
const preview = svg(`${baseBody}
<clipPath id="previewFill"><rect x="${track.x}" y="${track.y}" width="${previewFillWidth}" height="${track.h}"/></clipPath>
<g clip-path="url(#previewFill)"><rect x="${track.x}" y="${track.y}" width="${track.w}" height="${track.h}" rx="${track.r}" fill="url(#green)"/>
<path d="M${track.x+40} ${track.y+25}H${track.x+track.w-40}" stroke="#c9ffdb" stroke-opacity=".48" stroke-width="11" stroke-linecap="round"/></g>
<text x="330" y="282" fill="#fff9e9" font-family="Arial, sans-serif" font-size="82" font-weight="700">섬 회복도</text>
<text x="1830" y="282" text-anchor="end" fill="#fff9e9" font-family="Arial, sans-serif" font-size="88" font-weight="800">68%</text>`);

fs.mkdirSync(out, { recursive: true });
fs.writeFileSync(path.join(out, 'island-restoration-panel.svg'), base, 'utf8');
fs.writeFileSync(path.join(out, 'island-restoration-fill.svg'), fill, 'utf8');
fs.writeFileSync(path.join(out, 'island-restoration-preview-68.svg'), preview, 'utf8');
