const fs = require('fs');
const path = require('path');

const out = __dirname;
const W = 2152;
const H = 731;
const y = 365.5;
const start = 332;
const end = 1820;
const nodes = [555, 815, 1076, 1337, 1597];

const defs = `
  <defs>
    <linearGradient id="gold" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#fff0a8"/><stop offset="0.2" stop-color="#f9c84c"/>
      <stop offset="0.55" stop-color="#b86b16"/><stop offset="0.82" stop-color="#f2b83f"/>
      <stop offset="1" stop-color="#6f350e"/>
    </linearGradient>
    <linearGradient id="ivory" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#fffef1"/><stop offset="0.52" stop-color="#f5e5c4"/>
      <stop offset="1" stop-color="#c9a97c"/>
    </linearGradient>
    <linearGradient id="cyan" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="#baffff"/><stop offset="0.28" stop-color="#28f2ff"/>
      <stop offset="0.7" stop-color="#00b8e7"/><stop offset="1" stop-color="#087daf"/>
    </linearGradient>
    <radialGradient id="navy"><stop offset="0" stop-color="#173c68"/><stop offset="1" stop-color="#06182b"/></radialGradient>
    <filter id="shadow" x="-30%" y="-30%" width="160%" height="160%">
      <feDropShadow dx="0" dy="9" stdDeviation="8" flood-color="#130a03" flood-opacity=".65"/>
    </filter>
    <filter id="cyanGlow" x="-30%" y="-100%" width="160%" height="300%">
      <feGaussianBlur stdDeviation="8" result="b"/><feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
    </filter>
  </defs>`;

function svg(body, width = W, height = H) {
  return `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}">${defs}${body}</svg>`;
}

function medallion(cx, icon) {
  return `<g filter="url(#shadow)">
    <circle cx="${cx}" cy="${y}" r="142" fill="#3a1e0b"/>
    <circle cx="${cx}" cy="${y}" r="136" fill="url(#gold)"/>
    <circle cx="${cx}" cy="${y}" r="112" fill="url(#navy)" stroke="#51290c" stroke-width="8"/>
    ${icon}
  </g>`;
}

const harbor = `<g fill="url(#ivory)" stroke="#5b3a20" stroke-width="3" stroke-linejoin="round">
  <path d="M112 ${y+62}h136v18H112zM126 ${y+40}h108v18H126z"/>
  <path d="M140 ${y+38}v-73h28v73zm52 0v-73h28v73zM166 ${y+38}v-112h28v112z"/>
  <path d="M158 ${y-74}l22-22 22 22zM132 ${y-35}l22-20 22 20zm52 0l22-20 22 20z"/>
  <path d="M180 ${y-98}v-28h42l-16 12 16 12h-42z"/>
 </g>`;

const island = `<g stroke="#174b24" stroke-width="3" stroke-linejoin="round">
  <path d="M1900 ${y+58}q72-44 144 0q-18 34-72 34t-72-34" fill="#f5c63f"/>
  <path d="M1968 ${y+48}q-12-78 8-132" fill="none" stroke="#a95b1b" stroke-width="14"/>
  <path d="M1974 ${y-79}q-58-38-78 10q45-11 76 17M1978 ${y-80}q58-38 78 10q-45-11-76 17M1976 ${y-73}q-8-58-49-61q25 29 37 67M1978 ${y-73}q8-58 49-61q-25 29-37 67" fill="#35d83f"/>
  <ellipse cx="1944" cy="${y+45}" rx="32" ry="18" fill="#4fd148"/><ellipse cx="2006" cy="${y+48}" rx="38" ry="20" fill="#52d646"/>
 </g>`;

const base = svg(`
  <g filter="url(#shadow)">
    <rect x="${start}" y="${y-22}" width="${end-start}" height="44" rx="20" fill="#694018"/>
    <rect x="${start}" y="${y-15}" width="${end-start}" height="30" rx="15" fill="url(#ivory)"/>
  </g>
  ${nodes.map(x => `<g filter="url(#shadow)"><circle cx="${x}" cy="${y}" r="53" fill="#3a1d09"/><circle cx="${x}" cy="${y}" r="47" fill="url(#gold)"/><circle cx="${x}" cy="${y}" r="31" fill="url(#ivory)" stroke="#6b3b13" stroke-width="5"/></g>`).join('')}
  ${medallion(180, harbor)}${medallion(1972, island)}
`);

const progress = svg(`
  <rect x="${start}" y="${y-15}" width="${end-start}" height="30" rx="15" fill="url(#cyan)" filter="url(#cyanGlow)"/>
`);

const activeNode = svg(`
  <circle cx="627" cy="627" r="270" fill="url(#cyan)" stroke="#087ca7" stroke-width="35" filter="url(#cyanGlow)"/>
  <ellipse cx="555" cy="535" rx="75" ry="42" fill="#d7ffff" opacity=".8" transform="rotate(-35 555 535)"/>
`, 1254, 1254);

function write(name, data) {
  fs.writeFileSync(path.join(out, name), data, 'utf8');
}

write('voyage-track-empty-v3.svg', base);
write('voyage-progress-fill-v3.svg', progress);
write('voyage-node-active-v3.svg', activeNode);
