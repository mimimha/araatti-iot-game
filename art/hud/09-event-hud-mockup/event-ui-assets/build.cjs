const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const root = __dirname;
const uiDir = path.join(root, 'ui');
const sourceDir = path.join(root, 'source');
const iconDir = path.resolve(root, '../event-icon-assets/icons');
fs.mkdirSync(uiDir, { recursive: true });
fs.mkdirSync(sourceDir, { recursive: true });

const assets = [
  {
    name: 'event-card-background', width: 1024, height: 300, border: [64, 64, 64, 64],
    svg: `<svg xmlns="http://www.w3.org/2000/svg" width="1024" height="300"><defs><linearGradient id="g" x2="0" y2="1"><stop stop-color="#123d5e" stop-opacity=".94"/><stop offset="1" stop-color="#071c2e" stop-opacity=".94"/></linearGradient><filter id="s" x="-20%" y="-30%" width="140%" height="170%"><feGaussianBlur stdDeviation="10"/></filter></defs><rect x="16" y="22" width="992" height="264" rx="54" fill="#00101d" opacity=".54" filter="url(#s)"/><rect x="12" y="10" width="1000" height="268" rx="54" fill="url(#g)"/><path d="M66 25H958" stroke="#79b9d7" stroke-opacity=".11" stroke-width="3" stroke-linecap="round"/></svg>`
  },
  {
    name: 'event-state-badge', width: 256, height: 96, border: [42, 42, 42, 42], tintable: true,
    svg: `<svg xmlns="http://www.w3.org/2000/svg" width="256" height="96"><defs><linearGradient id="g" x2="0" y2="1"><stop stop-color="#fff"/><stop offset="1" stop-color="#c8c8c8"/></linearGradient></defs><rect x="4" y="4" width="248" height="88" rx="44" fill="url(#g)"/><path d="M45 14H211" stroke="#fff" stroke-opacity=".45" stroke-width="4" stroke-linecap="round"/><rect x="4" y="4" width="248" height="88" rx="44" fill="none" stroke="#071c2e" stroke-opacity=".7" stroke-width="7"/></svg>`
  },
  {
    name: 'event-timer-track', width: 512, height: 32, border: [16, 16, 16, 16],
    svg: `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="32"><rect x="2" y="2" width="508" height="28" rx="14" fill="#061829" fill-opacity=".86" stroke="#7fa8bd" stroke-opacity=".65" stroke-width="3"/></svg>`
  },
  {
    name: 'event-timer-fill', width: 512, height: 32, border: [16, 16, 16, 16], tintable: true,
    svg: `<svg xmlns="http://www.w3.org/2000/svg" width="512" height="32"><defs><linearGradient id="g" x2="0" y2="1"><stop stop-color="#fff"/><stop offset="1" stop-color="#c8c8c8"/></linearGradient></defs><rect x="2" y="2" width="508" height="28" rx="14" fill="url(#g)"/><path d="M18 8H494" stroke="#fff" stroke-opacity=".55" stroke-width="2" stroke-linecap="round"/></svg>`
  }
];

async function main() {
  const manifest = [];
  for (const asset of assets) {
    const svgPath = path.join(sourceDir, `${asset.name}.svg`);
    const pngPath = path.join(uiDir, `${asset.name}.png`);
    fs.writeFileSync(svgPath, asset.svg);
    await sharp(Buffer.from(asset.svg)).png().toFile(pngPath);
    const { data, info } = await sharp(pngPath).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
    let clear = 0, solid = 0, partial = 0;
    for (let p = 3; p < data.length; p += info.channels) {
      if (data[p] === 0) clear++; else if (data[p] === 255) solid++; else partial++;
    }
    if (!clear || (!solid && !partial)) throw new Error(`Alpha validation failed: ${asset.name}`);
    manifest.push({ file: `ui/${asset.name}.png`, width: asset.width, height: asset.height, borderLTRB: asset.border, tintable: !!asset.tintable, clearPixels: clear });
  }

  const background = Buffer.from('<svg width="1280" height="860"><defs><pattern id="p" width="32" height="32" patternUnits="userSpaceOnUse"><rect width="32" height="32" fill="#25465a"/><path d="M0 0h16v16H0zM16 16h16v16H16z" fill="#2c5065"/></pattern></defs><rect width="1280" height="860" fill="url(#p)"/><text x="48" y="58" font-family="Arial,sans-serif" font-size="28" fill="#fff0d0">BORDERLESS EVENT CARD — GAME ASSET ASSEMBLY</text><text x="48" y="92" font-family="Arial,sans-serif" font-size="16" fill="#a7bfcc">Icons are exact transparent sprites · Text remains runtime TMP</text></svg>');
  const cards = [
    { icon: 'event-big-wave.png', y: 130, title: '거대한 파도', sub: '조타를 붙잡고 정면 돌파', time: '5초', badge: '예고', color: '#ffc64d', fill: .70 },
    { icon: 'event-squall.png', y: 365, title: '돌풍', sub: '돛을 30% 이상 유지', time: '12초', badge: '발생', color: '#ff665f', fill: .45 },
    { icon: 'event-hull-damage.png', y: 600, title: '선체 파손·침수', sub: '구멍을 막고 물을 퍼내세요  ·  하단갑판', time: '', badge: '발생', color: '#ff665f', fill: null }
  ];
  const parts = [];
  const cardBuffer = await sharp(path.join(uiDir, 'event-card-background.png')).resize(1160, 210).png().toBuffer();
  for (const card of cards) {
    parts.push({ input: cardBuffer, left: 48, top: card.y });
    parts.push({ input: await sharp(path.join(iconDir, card.icon)).resize(138, 138).png().toBuffer(), left: 78, top: card.y + 34 });
    const badgeSvg = `<svg width="130" height="54"><rect width="130" height="54" rx="27" fill="${card.color}"/><text x="65" y="37" text-anchor="middle" font-family="Malgun Gothic,sans-serif" font-size="25" font-weight="700" fill="#071c2e">${card.badge}</text></svg>`;
    parts.push({ input: Buffer.from(badgeSvg), left: 1028, top: card.y + 28 });
    const textSvg = `<svg width="780" height="150"><text x="0" y="40" font-family="Malgun Gothic,sans-serif" font-size="37" font-weight="700" fill="#fff0d0">${card.title}</text><text x="0" y="82" font-family="Malgun Gothic,sans-serif" font-size="24" fill="#b8d8e8">${card.sub}</text>${card.time ? `<text x="750" y="42" text-anchor="end" font-family="Malgun Gothic,sans-serif" font-size="30" font-weight="700" fill="#fff0d0">${card.time}</text>` : ''}</svg>`;
    parts.push({ input: Buffer.from(textSvg), left: 250, top: card.y + 30 });
    if (card.fill !== null) {
      parts.push({ input: await sharp(path.join(uiDir, 'event-timer-track.png')).resize(745, 22).png().toBuffer(), left: 250, top: card.y + 155 });
      parts.push({ input: await sharp(Buffer.from(`<svg width="${Math.round(735 * card.fill)}" height="18"><rect width="100%" height="18" rx="9" fill="${card.color}"/></svg>`)).png().toBuffer(), left: 255, top: card.y + 157 });
    }
  }
  await sharp(background).composite(parts).png().toFile(path.join(root, 'preview.png'));
  fs.writeFileSync(path.join(root, 'manifest.json'), JSON.stringify({
    importSettings: { textureType: 'Sprite (2D and UI)', spriteMode: 'Single', alphaIsTransparency: true, mipmaps: false, wrapMode: 'Clamp', filterMode: 'Bilinear', compression: 'None' },
    colors: { warning: '#FFC64D', running: '#FF665F', title: '#FFF0D0', secondary: '#B8D8E8' },
    assets: manifest,
    runtimeText: ['event title', 'live hint', 'deck/location', 'countdown seconds', 'state badge label'],
    notes: ['No icon medallion is used.', 'Hide timer track and fill when HasCountdown is false.', 'Use card background as Sliced with the listed border.']
  }, null, 2));
  console.log(JSON.stringify(manifest, null, 2));
}

main().catch(error => { console.error(error); process.exit(1); });
