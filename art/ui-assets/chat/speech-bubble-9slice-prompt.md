# 머리 위 말풍선 — 9-slice 에셋

제작: 이미지 생성 + 후처리. 결과물은
`unity/UnderTheSea/Assets/Game/Art/UI/Chat/speech-bubble-body-v2.png` · `speech-bubble-tail-v2.png`.
원본은 이 폴더의 `speech-bubble-sheet-v2-source.png`.

붙이는 코드는 `Assets/Game/Scripts/Network/PlayerSpeechBubble.cs`.

---

## 왜 규격이 까다로운가

말풍선은 글자 길이에 맞춰 늘어난다. 9-slice 로 늘리므로 그림이 세 구역으로 나뉜다.

```
 ┌─────┬───────────────┬─────┐
 │  A  │       B       │  A  │   A · 모서리 — 안 늘어남. 자유
 ├─────┼───────────────┼─────┤
 │  C  │       D       │  C  │   B · 위아래 띠 — 가로로 늘어남
 │     │               │     │   C · 좌우 띠 — 세로로 늘어남
 ├─────┼───────────────┼─────┤
 │  A  │       B       │  A  │   D · 가운데 — 양쪽으로 늘어남
 └─────┴───────────────┴─────┘
```

그래서 **직선 구간에서 외곽선 굵기가 흔들리면 안 된다.** 늘리면 그 흔들림도 같이
늘어나 물결이 된다. 그림자 · 가로 그라디언트 · 모서리 장식도 같은 이유로 못 쓴다.

손맛은 **모서리(A)와 테두리의 두께 방향**에 넣으면 된다. 거기는 안 늘어난다.

---

## 몸통 + 꼬리 합본 프롬프트

```
Two separate UI shapes on ONE transparent canvas, placed side by side with a clear gap
between them. They must never touch, overlap or connect. Transparent background, PNG.

STYLE — applies to both shapes:
Hand-inked and hand-painted, like a children's picture book or a cozy cartoon adventure
game. Warm, soft, friendly, slightly imperfect and organic — not a clean vector shape, not
corporate flat design, not clip art.
Fill: warm ivory #FDF6E9 with a gentle top-to-bottom shading, a touch brighter at the top
and a touch warmer and creamier at the bottom. The shading runs PURELY vertically —
identical at every horizontal position, no side lighting, no corner vignette, no diagonal
light, no spot highlight, no sheen blob.
Outline: deep navy #1E2B5E, a hand-inked stroke with a soft brushy quality.
Both shapes share the exact same ivory fill and the exact same stroke weight and brush
character, as if drawn with one pen in one sitting.

LEFT SHAPE — the bubble body:
One horizontally-wide rounded rectangle, filling about half the canvas width and a third of
its height. Symmetric left-to-right and top-to-bottom. All four corners share the same
generous corner radius. NO tail, NO pointer, NO spike — a plain closed rounded rectangle.
Its outline thickness stays constant all the way around; it may breathe a little at the
rounded corners, but along the four straight runs it must never taper, break or fade.

RIGHT SHAPE — the tail, on its own:
A classic comic speech-balloon tail: a slim, gently curved horn. It is noticeably TALLER
than it is wide — about 1.3 times as tall as its base is wide — and it is small, roughly
one third the height of the bubble body beside it.
Its two sides are gently CONCAVE, curving inward: the tail flares out a little where it
meets the base, narrows quickly, then runs slim and tapering down to a FINE, SHARP, cleanly
pointed tip. Each side is a single smooth concave arc bowing inward by roughly 15% of the
base width.
The tip sits a short way to the RIGHT of the base's center, about a quarter of the base
width.
Keep it restrained: one single smooth concave arc per side, no S-curve, no reverse curve,
no long sweeping arc, no flame, no comma, no calligraphic flourish.
Outline on the TWO concave sides only, converging at the tip and tapering noticeably
thinner as it reaches it. The flat TOP edge carries NO outline whatsoever — the ivory fill
runs clean to the top edge so the tail can be tucked under the bubble with no visible seam.

GLOBAL:
No drop shadow, no cast shadow, no glow, no bevel, no 3D extrusion, no second shape stacked
behind, no frame or border around the image, no grid, no checkerboard.
Dead-on front view, no perspective, no tilt, no rotation.
No text, no letters, no icons, no sparkles, no decorations, no background scene, no
annotations, no labels. Just the two shapes on transparency.
```

### 꼬리가 과하거나 밋밋하면

네 번 돌려 보니 어긋나는 곳은 늘 이 셋이었다. 하나씩 말고 **셋을 같이** 올리고 내린다.

| 손잡이 | 문구 | 더 날렵하게 | 더 뭉툭하게 |
| --- | --- | --- | --- |
| 길이 | `about 1.3 times as tall as its base is wide` | 1.6 | 1.0 |
| 파임 | `bowing inward by roughly 15% of the base width` | 25% | 8% |
| 기울기 | `about a quarter of the base width` | a third | 빼고 `points straight down` |

---

## 받은 그림에 한 것

**몸통.** 크롭 → 알파 정리 → 984×269 에서 492×134 로 축소.
받은 그림은 직선 구간 흔들림이 984px 길이에 **1px** 뿐이라 가장자리 정리는 하지 않았다.
손으로 그린 선 맛을 지우지 않는 편이 낫다.

**그라디언트는 다시 깔았다.** 받은 그림은 위에서 아래로 쭉 이어지는 그라디언트였는데,
그대로 쓰면 짧은 말풍선에서 **가로 줄무늬가 생긴다.** 9-slice 가 가운데(D)를 건너뛰면
위 띠의 끝 색과 아래 띠의 시작 색이 안 맞기 때문이다. 그래서

```
   위 띠(B)   밝은 크림 → 중간색
   가운데(D)  중간색 고정      ← 여기가 평평해야 경계에서 색이 이어진다
   아래 띠(B) 중간색 → 진한 크림
```

로 바꿨다. 이러면 어떤 크기로 늘리든 · 아예 건너뛰든 이음매가 안 보인다.

**꼬리는 직접 그렸다.** 생성된 꼬리는 폭의 12%가 외곽선이었는데 몸통은 1.2%였다.
실제 크기로 붙이면 꼬리만 굵어져 이은 자리가 티 난다. 그래서 몸통에서 색(`#FDECD0`
계열)과 선 굵기를 그대로 떠서 같은 실루엣으로 다시 그렸다. 화면에서 50px 남짓으로
뜨는 물건이라 붓질 차이는 보이지 않는다. 그린 스크립트는 남겨 두지 않았다 —
다시 필요하면 위 프롬프트로 뽑고 선 굵기만 맞추는 편이 빠르다.

---

## Unity 설정

| 항목 | 값 | 왜 |
| --- | --- | --- |
| `speech-bubble-body-v2.png` | 492×134 | |
| ↳ `spriteBorder` | `{x:54, y:45, z:54, w:45}` | 측정한 모서리 곡선 구간(가로 49 · 세로 41)을 덮는다 |
| ↳ Image Type | Sliced | |
| `bodyBorderShrink` | 1.35 | 최소 크기를 80×66 칸으로 맞춘다. 짧은 말이 딱 붙는 크기 |
| `speech-bubble-tail-v2.png` | 224×152 | 최종 50×38 칸의 4배 |
| ↳ `spriteBorder` | 0 | 꼬리는 늘어나지 않는다. Simple 로 그린다 |
| `tailSize` | `{x:50, y:38}` | |
| `tailOverlap` | 8 | 몸통 아래 외곽선(4.4칸)을 덮고도 남는다 |

`bodyBorderShrink` 는 **최소 크기를 정하는 값이다.** 9-slice 는 테두리를 안 늘리므로
사방 테두리의 합이 최소 크기가 된다. 그림을 바꾸면 `.meta` 의 `spriteBorder` 를 보고
이 값을 다시 잡는다.

꼬리가 몸통 위에 덮여 그려지는 것은 `BuildTail` 이 꼬리를 몸통의 **자식**으로 매달기
때문이다. 그래서 꼬리의 크림색이 몸통 아래 외곽선을 가려, 참고 그림처럼 선이 끊기지
않고 이어져 보인다. 순서를 바꾸면 꼬리 위에 몸통 선이 가로질러 그어진다.
