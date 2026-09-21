'use strict';

/**
 * 항해 진행도 HUD — 공통 벡터 마스터.
 *
 * 이 파일이 유일한 좌표 출처다. voyage-track-empty.svg · voyage-progress-fill.svg ·
 * voyage-node-active.svg · voyage-ship-marker.svg 는 전부 여기 정의한 상수에서
 * 잘라 쓴다. 좌표를 각 SVG에 따로 적지 않는다 — 다시 돌리면 늘 같은 결과가 나온다.
 */

const fs = require('fs');
const path = require('path');

const OUT_DIR = __dirname;

// ------------------------------------------------------------------
// 공통 캔버스
// ------------------------------------------------------------------

const VIEW_W = 2152;
const VIEW_H = 731;

/** 트랙 중심선의 Y. 모든 레이어(트랙 · 진행선 · 노드 · 배 마커)가 이 값을 공유한다. */
const TRACK_Y = 365.5;

/** 트랙 색이 채워지는 구간의 시작 · 끝 X. 진행선(0~100%)이 정확히 이 구간을 채운다. */
const TRACK_START_X = 332;
const TRACK_END_X = 1820;
const TRACK_LENGTH = TRACK_END_X - TRACK_START_X;

/**
 * 트랙 안쪽 색(아이보리 · 청록)의 두께. **트랙 파일과 진행선 파일이 반드시 같은
 * 값을 쓴다** — 그래야 겹쳤을 때 픽셀 단위로 일치한다.
 *
 * ⚠ 여기는 **각진 사각형**이다(rx 없음). 둥근 끝을 쓰면 fillAmount 로 자를 때
 *    잘리는 경계에서 둥근 모양이 뭉개져 보인다. 둥근 느낌은 바깥 황동 테두리가
 *    대신 낸다 — 테두리는 진행선 파일에는 없으므로 잘려도 문제가 없다.
 */
const TRACK_INNER_H = 42;

/** 바깥 황동 테두리(테두리만 있는 파일 = 빈 트랙에만 그린다). */
const TRACK_FRAME_H = 72;
const TRACK_FRAME_PAD = 36; // 안쪽 색 구간보다 이만큼 더 좌우로 뻗는다.
const TRACK_FRAME_X0 = TRACK_START_X - TRACK_FRAME_PAD;
const TRACK_FRAME_X1 = TRACK_END_X + TRACK_FRAME_PAD;

// ------------------------------------------------------------------
// 체크포인트 노드 5개 — 트랙 구간을 6칸으로 나눠 등간격으로 놓는다.
// ------------------------------------------------------------------

const NODE_COUNT = 5;
const NODE_GAP = TRACK_LENGTH / (NODE_COUNT + 1);
const NODE_X = Array.from({ length: NODE_COUNT }, (_, i) => TRACK_START_X + NODE_GAP * (i + 1));

const NODE_OUTER_R = 46; // 황동 테두리 바깥 반지름
const NODE_RING_W = 9;
const NODE_FACE_R = 30; // 아이보리(비활성) · 청록(활성) 안쪽 원

// ------------------------------------------------------------------
// 항구 · 섬 배지 (트랙 양 끝)
// ------------------------------------------------------------------

const HARBOR_CX = 176;
const ISLAND_CX = 1976;
const END_BADGE_OUTER_R = 118;
const END_BADGE_RING_W = 11;
const END_BADGE_FACE_R = 96;

// ------------------------------------------------------------------
// 배 마커 — 트랙과 같은 캔버스에서, **0% 위치(TRACK_START_X)** 를 기준점으로 그린다.
// Unity 는 이 기준점에서 (progress01 * TRACK_LENGTH) 만큼 X 로 옮겨서 쓴다.
// ------------------------------------------------------------------

const SHIP_MARKER_CX = TRACK_START_X;
const SHIP_MARKER_OUTER_R = 96;
const SHIP_MARKER_RING_W = 11;
const SHIP_MARKER_FACE_R = 78;

// ------------------------------------------------------------------
// 색 — 짙은 남색 에나멜 · 황동 · 아이보리 · 청록. 과한 그라디언트 없이 2단만 쓴다.
// ------------------------------------------------------------------

const COLOR = {
  navyTop: '#1c3a63',
  navyBottom: '#0a1c33',
  brassLight: '#e8b654',
  brassDark: '#8a5a1e',
  brassEdge: '#4a2f10',
  ivoryLight: '#fdf6e3',
  ivoryDark: '#d8c69a',
  cyanLight: '#7ff0ee',
  cyanDark: '#0f9fb0',
};

function defs() {
  return `<defs>
    <linearGradient id="navy" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${COLOR.navyTop}"/>
      <stop offset="1" stop-color="${COLOR.navyBottom}"/>
    </linearGradient>
    <linearGradient id="brass" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${COLOR.brassLight}"/>
      <stop offset="1" stop-color="${COLOR.brassDark}"/>
    </linearGradient>
    <linearGradient id="ivory" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${COLOR.ivoryLight}"/>
      <stop offset="1" stop-color="${COLOR.ivoryDark}"/>
    </linearGradient>
    <linearGradient id="cyan" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0" stop-color="${COLOR.cyanLight}"/>
      <stop offset="1" stop-color="${COLOR.cyanDark}"/>
    </linearGradient>
  </defs>`;
}

function svg(body) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${VIEW_W} ${VIEW_H}">
${defs()}
${body}
</svg>
`;
}

// ------------------------------------------------------------------
// 조립 조각들 — 여러 파일이 같은 모양을 다시 그리지 않고 이 함수들을 나눠 쓴다.
// ------------------------------------------------------------------

/** 원형 배지(항구 · 섬 · 배 마커 · 노드가 전부 이 구조를 쓴다): 황동 테두리 + 안쪽 원. */
function badge(cx, cy, outerR, ringW, faceR, faceFill) {
  return `<circle cx="${cx}" cy="${cy}" r="${outerR}" fill="url(#brass)" stroke="${COLOR.brassEdge}" stroke-width="2"/>
  <circle cx="${cx}" cy="${cy}" r="${outerR - ringW}" fill="${faceFill}"/>`;
}

function harborIcon(cx, cy) {
  const s = END_BADGE_FACE_R / 96; // 96 기준으로 그려서 다른 반지름에도 비례한다.
  const g = (dx, dy) => `${cx + dx * s},${cy + dy * s}`;
  return `<g fill="${COLOR.ivoryLight}" stroke="${COLOR.brassEdge}" stroke-width="${3 * s}" stroke-linejoin="round">
    <path d="M${g(-48, 30)} L${g(48, 30)} L${g(48, 44)} L${g(-48, 44)} Z"/>
    <path d="M${g(-26, -8)} L${g(-26, 30)} L${g(-10, 30)} L${g(-10, -8)} Z"/>
    <path d="M${g(10, -8)} L${g(10, 30)} L${g(26, 30)} L${g(26, -8)} Z"/>
    <path d="M${g(-30, -8)} L${g(-18, -22)} L${g(-6, -8)} Z"/>
    <path d="M${g(6, -8)} L${g(18, -22)} L${g(30, -8)} Z"/>
    <path d="M${g(-4, -30)} L${g(-4, -54)} L${g(26, -54)} L${g(10, -42)} L${g(26, -30)} Z"/>
  </g>`;
}

function islandIcon(cx, cy) {
  const s = END_BADGE_FACE_R / 96;
  const g = (dx, dy) => `${cx + dx * s},${cy + dy * s}`;
  return `<g stroke="${COLOR.brassEdge}" stroke-width="${2.5 * s}" stroke-linejoin="round">
    <path d="M${g(-52, 24)} Q${cx},${cy + 44 * s} ${g(52, 24)} Q${cx},${cy + 6 * s} ${g(-52, 24)} Z" fill="${COLOR.ivoryLight}"/>
    <path d="M${cx},${cy - 4 * s} L${cx - 6 * s},${cy + 20 * s} L${cx + 6 * s},${cy + 20 * s} Z" fill="${COLOR.brassDark}"/>
    <path d="M${cx},${cy - 46 * s} L${cx - 26 * s},${cy - 8 * s} L${cx},${cy - 20 * s} L${cx + 26 * s},${cy - 8 * s} Z" fill="${COLOR.cyanDark}"/>
  </g>`;
}

/** 배 마커 안에 들어가는 작은 범선 실루엣. 아이보리 한 색, 장식 없음. */
function shipIcon(cx, cy, r) {
  const s = r / 78;
  const g = (dx, dy) => `${cx + dx * s},${cy + dy * s}`;
  return `<g fill="${COLOR.ivoryLight}">
    <path d="M${g(-40, 18)} L${g(40, 18)} L${g(28, 34)} L${g(-28, 34)} Z"/>
    <rect x="${cx - 3 * s}" y="${cy - 40 * s}" width="${6 * s}" height="${58 * s}"/>
    <path d="M${g(2, -38)} L${g(2, 10)} L${g(38, 10)} Z"/>
    <path d="M${g(-2, -30)} L${g(-2, 10)} L${g(-30, 6)} Z"/>
  </g>`;
}

// ------------------------------------------------------------------
// 파일 1 — voyage-track-empty.svg
// ------------------------------------------------------------------

function buildTrackEmpty() {
  const frameY0 = TRACK_Y - TRACK_FRAME_H / 2;
  const innerY0 = TRACK_Y - TRACK_INNER_H / 2;

  const body = `
  <!-- 바깥 황동 테두리: 둥근 캡슐. 안쪽 색 구간보다 좌우로 더 뻗어서, 안쪽의
       각진 모서리가 이 둥근 캡 안에 묻히게 한다. -->
  <rect x="${TRACK_FRAME_X0}" y="${frameY0}" width="${TRACK_FRAME_X1 - TRACK_FRAME_X0}" height="${TRACK_FRAME_H}"
        rx="${TRACK_FRAME_H / 2}" fill="url(#brass)" stroke="${COLOR.brassEdge}" stroke-width="3"/>

  <!-- 안쪽 아이보리 트랙. 진행선(voyage-progress-fill.svg)과 좌표 · 두께가 완전히 같다. -->
  <rect x="${TRACK_START_X}" y="${innerY0}" width="${TRACK_LENGTH}" height="${TRACK_INNER_H}" fill="url(#ivory)"/>

  ${NODE_X.map(x => `<g>${badge(x, TRACK_Y, NODE_OUTER_R, NODE_RING_W, NODE_FACE_R, 'url(#ivory)')}</g>`).join('\n  ')}

  <g>${badge(HARBOR_CX, TRACK_Y, END_BADGE_OUTER_R, END_BADGE_RING_W, END_BADGE_FACE_R, 'url(#navy)')}${harborIcon(HARBOR_CX, TRACK_Y)}</g>
  <g>${badge(ISLAND_CX, TRACK_Y, END_BADGE_OUTER_R, END_BADGE_RING_W, END_BADGE_FACE_R, 'url(#navy)')}${islandIcon(ISLAND_CX, TRACK_Y)}</g>
`;

  return svg(body);
}

// ------------------------------------------------------------------
// 파일 2 — voyage-progress-fill.svg
// ------------------------------------------------------------------

function buildProgressFill() {
  const innerY0 = TRACK_Y - TRACK_INNER_H / 2;

  // ⚠ 각진 사각형 하나뿐이다. 둥근 캡 · 그림자 · 광택 없음 — Unity Image(Filled,
  //    Horizontal, Origin=Left) 가 왼쪽부터 자를 때 경계가 항상 곧은 수직선이 된다.
  const body = `
  <rect x="${TRACK_START_X}" y="${innerY0}" width="${TRACK_LENGTH}" height="${TRACK_INNER_H}" fill="url(#cyan)"/>
`;

  return svg(body);
}

// ------------------------------------------------------------------
// 파일 3 — voyage-node-active.svg
// ------------------------------------------------------------------

function buildNodeActive() {
  // 노드 위에 겹쳐 쓰는 청록 원 하나. 황동 테두리는 넣지 않는다 — 빈 트랙에
  // 이미 있는 테두리 위에 겹쳐지기 때문이다.
  //
  // ⚠ **캔버스 정가운데(VIEW_W/2)에 그린다.** 5개 노드 중 하나(예: NODE_X[0])를
  //    기준으로 잡으면 이 그림은 그 노드에만 맞고 나머지 4곳에는 안 맞는다.
  //    정가운데에 그려 두면, 어느 노드 위에 올리든 "그 노드의 중심 = 이 그림의
  //    중심"이 되도록 Unity 쪽에서 매번 같은 오프셋(캔버스 중심 → 노드 중심)만큼
  //    옮기면 되고, 그 오프셋 계산이 다섯 노드 모두 같은 식으로 통일된다.
  const cx = VIEW_W / 2;
  const body = `
  <circle cx="${cx}" cy="${TRACK_Y}" r="${NODE_FACE_R}" fill="url(#cyan)"/>
`;

  return { svg: svg(body), refX: cx };
}

// ------------------------------------------------------------------
// 파일 4 — voyage-ship-marker.svg
// ------------------------------------------------------------------

function buildShipMarker() {
  const body = `
  <g>${badge(SHIP_MARKER_CX, TRACK_Y, SHIP_MARKER_OUTER_R, SHIP_MARKER_RING_W, SHIP_MARKER_FACE_R, 'url(#navy)')}${shipIcon(SHIP_MARKER_CX, TRACK_Y, SHIP_MARKER_FACE_R)}</g>
`;

  return svg(body);
}

// ------------------------------------------------------------------
// 쓰기 + 검증용 상수 내보내기 (미리보기 스크립트가 재사용한다)
// ------------------------------------------------------------------

function write(name, content) {
  const target = path.join(OUT_DIR, name);
  fs.writeFileSync(target, content, 'utf8');
  console.log('wrote', target);
}

function main() {
  write('voyage-track-empty.svg', buildTrackEmpty());
  write('voyage-progress-fill.svg', buildProgressFill());

  const node = buildNodeActive();
  write('voyage-node-active.svg', node.svg);

  write('voyage-ship-marker.svg', buildShipMarker());

  const manifest = {
    viewBox: { width: VIEW_W, height: VIEW_H },
    trackY: TRACK_Y,
    trackStartX: TRACK_START_X,
    trackEndX: TRACK_END_X,
    trackLength: TRACK_LENGTH,
    nodeX: NODE_X,
    activeNodeRefX: node.refX,
    shipMarkerRestX: SHIP_MARKER_CX,
    fillAmountFormula:
      'fillAmount = (trackStartX + p * trackLength) / viewBox.width  — p 는 0~1 진행률. ' +
      'EmptyTrack 과 ProgressFill 이 둘 다 캔버스 전체(0~2152)를 덮는 RectTransform이라서, ' +
      'Image.fillAmount 는 p 가 아니라 이 값을 써야 한다. p 를 그대로 넣으면 트랙 앞부분 ' +
      '(0~332) 만큼 진행이 일찍 끝난 것처럼 보인다.',
    shipMarkerXFormula:
      'anchoredPosition.x = trackStartX + p * trackLength  — ProgressFill 의 잘린 끝과 ' +
      '항상 같은 X. 두 식이 같은 trackStartX/trackLength 를 쓰므로 자동으로 맞는다.',
    activeNodePlacement:
      'voyage-node-active.svg 를 노드 i 위에 겹칠 때는 (nodeX[i] - activeNodeRefX) 만큼 ' +
      'X 로 옮긴다. i 번째 노드는 p >= (i+1)/(nodeCount+1) 일 때 활성화한다.',
  };
  write('voyage-hud-manifest.json', JSON.stringify(manifest, null, 2));
}

main();

module.exports = {
  VIEW_W,
  VIEW_H,
  TRACK_Y,
  TRACK_START_X,
  TRACK_END_X,
  TRACK_LENGTH,
  NODE_X,
  SHIP_MARKER_CX,
};
