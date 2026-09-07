// 채널 목록 줄(channel-row-base.png) 양 끝에 있는 금색 리벳을 지운다.
//
// 배 아이콘이 왼쪽 리벳과 겹쳐 보여서 없애기로 했다.
// 손으로 픽셀을 고치면 extract 스크립트를 다시 돌릴 때 되살아나므로 여기에 남긴다.
//
// 여러 번 실행해도 안전하다. 리벳이 이미 없으면 아무것도 하지 않는다.

const sharp = require('sharp');
const path = require('path');
const fs = require('fs');

const root = path.resolve(__dirname, '../../unity/UnderTheSea/Assets/Game/Art/ChannelSelect');
const TARGET = 'channel-row-base.png';

// 리벳과 테두리는 둘 다 금색이다. 구분은 위치로 한다 —
// 테두리는 이미지 가장자리에 닿아 있고, 리벳은 안쪽에 고립되어 있다.
const isGold = (d, i) => d[i + 3] > 128 && d[i] > 140 && d[i + 1] > 100 && d[i + 2] < d[i] - 40;

// 줄 안쪽의 남색 판재.
const isPlank = (d, i) =>
  d[i + 3] > 200 &&
  d[i + 2] > d[i] + 12 &&
  d[i + 2] >= d[i + 1] &&
  Math.max(d[i], d[i + 1], d[i + 2]) < 160;

/** 금색 덩어리를 찾아 가장자리에 닿지 않는 것(=리벳)만 돌려준다. */
function findRivets(data, w, h) {
  const seen = new Uint8Array(w * h);
  const rivets = [];

  for (let p = 0; p < w * h; p++) {
    if (seen[p] || !isGold(data, p * 4)) continue;

    const stack = [p];
    seen[p] = 1;
    let minX = w, maxX = -1, minY = h, maxY = -1, count = 0, touchesEdge = false;

    while (stack.length) {
      const q = stack.pop();
      const x = q % w, y = (q / w) | 0;
      count++;
      if (x < minX) minX = x;
      if (x > maxX) maxX = x;
      if (y < minY) minY = y;
      if (y > maxY) maxY = y;
      if (x === 0 || y === 0 || x === w - 1 || y === h - 1) touchesEdge = true;

      for (let dy = -1; dy <= 1; dy++) {
        for (let dx = -1; dx <= 1; dx++) {
          const nx = x + dx, ny = y + dy;
          if (nx < 0 || ny < 0 || nx >= w || ny >= h) continue;
          const n = ny * w + nx;
          if (seen[n] || !isGold(data, n * 4)) continue;
          seen[n] = 1;
          stack.push(n);
        }
      }
    }

    if (!touchesEdge && count >= 30) rivets.push({ minX, maxX, minY, maxY, count });
  }
  return rivets;
}

async function run() {
  const file = path.join(root, TARGET);
  if (!fs.existsSync(file)) throw new Error('대상 파일이 없습니다: ' + file);

  const { data, info } = await sharp(file).ensureAlpha().raw().toBuffer({ resolveWithObject: true });
  const { width: w, height: h } = info;

  const rivets = findRivets(data, w, h);
  if (rivets.length === 0) {
    console.log(`${TARGET}: 리벳이 없습니다. 그대로 둡니다.`);
    return;
  }
  console.log(`${TARGET}: 리벳 ${rivets.length}개 발견`);
  for (const r of rivets) {
    console.log(`  x ${r.minX}..${r.maxX}  y ${r.minY}..${r.maxY}  (${r.count}px)`);
  }

  // 금색만 지우면 리벳 둘레의 어두운 그림자 링이 남는다.
  // 그래서 색으로 고르지 않고, 리벳이 걸친 높이 구간에서 양 끝 일정 폭을 통째로 덮는다.
  const MARGIN = 8;   // 리벳 바깥으로 더 지울 여유
  const INSET = 2;    // 판재 가장자리를 침범하지 않도록 남길 간격

  const bandTop = Math.max(0, Math.min(...rivets.map((r) => r.minY)) - MARGIN);
  const bandBottom = Math.min(h - 1, Math.max(...rivets.map((r) => r.maxY)) + MARGIN);
  const leftReach = Math.max(...rivets.filter((r) => r.maxX < w / 2).map((r) => r.maxX), -1);
  const rightReach = Math.min(...rivets.filter((r) => r.minX >= w / 2).map((r) => r.minX), w);

  const centerX = (w / 2) | 0;
  let painted = 0;

  for (let y = bandTop; y <= bandBottom; y++) {
    // 이 줄에서 판재가 시작하고 끝나는 지점을 찾는다. 줄은 양 끝이 둥글어 y 마다 다르다.
    let L = -1, R = -1;
    for (let x = 0; x < w; x++) if (isPlank(data, (y * w + x) * 4)) { L = x; break; }
    for (let x = w - 1; x >= 0; x--) if (isPlank(data, (y * w + x) * 4)) { R = x; break; }
    if (L < 0 || R < 0) continue;

    const s = (y * w + centerX) * 4;   // 같은 높이의 깨끗한 판재 색
    const paint = (x) => {
      const i = (y * w + x) * 4;
      data[i] = data[s];
      data[i + 1] = data[s + 1];
      data[i + 2] = data[s + 2];
      data[i + 3] = data[s + 3];
      painted++;
    };

    if (leftReach >= 0) for (let x = L + INSET; x <= leftReach + MARGIN && x < w; x++) paint(x);
    if (rightReach < w) for (let x = Math.max(0, rightReach - MARGIN); x <= R - INSET; x++) paint(x);
  }

  const tmp = file + '.tmp';
  await sharp(data, { raw: { width: w, height: h, channels: 4 } }).png().toFile(tmp);
  fs.renameSync(tmp, file);

  console.log(`  y ${bandTop}..${bandBottom} 구간에서 ${painted}px 를 판재 색으로 덮었습니다.`);
  console.log('  이미지 크기는 그대로입니다. 레이아웃 좌표에 영향 없습니다.');
}

run().catch((e) => { console.error(e); process.exit(1); });
