# 이벤트 HUD 공용 UI 에셋

좌측 상단 이벤트 발생 HUD를 게임에서 조립하기 위한 공용 부품입니다.

## 폴더 구성

- `ui/event-card-background.png`: 테두리 없는 이벤트 카드 배경
- `ui/event-state-badge.png`: 예고/발생 상태 배지용 흰색 틴트 에셋
- `ui/event-timer-track.png`: 타이머 배경
- `ui/event-timer-fill.png`: 타이머 채움용 흰색 틴트 에셋
- `preview.png`: 실제 아이콘을 조합한 사용 예시
- `manifest.json`: 권장 색상, 9-slice 값, 임포트 설정
- `source/`: 원본 SVG

이벤트 아이콘 원본은 인접 폴더 `../event-icon-assets/icons/`의 아래 다섯 파일을 사용합니다.

- `event-reef.png`
- `event-big-wave.png`
- `event-squall.png`
- `event-enemy-ship.png`
- `event-hull-damage.png`

## Unity 권장 설정

- Texture Type: Sprite (2D and UI)
- Sprite Mode: Single
- Alpha Is Transparency: On
- Wrap Mode: Clamp
- Filter Mode: Bilinear
- Generate Mip Maps: Off
- Compression: None

`event-card-background.png`은 Image Type을 Sliced로 설정하고 Border를
`Left 64 / Bottom 64 / Right 64 / Top 64`로 사용합니다.

배지와 타이머 채움은 흰색 기반이므로 런타임에서 색상을 입힙니다.

- 예고: `#FFC64D`
- 발생: `#FF665F`
- 제목: `#FFF0D0`
- 보조 문구: `#B8D8E8`

카드 안의 이벤트명, 설명, 위치, 남은 초, 상태 문구는 이미지에 포함하지 않고
TextMeshPro 텍스트로 배치합니다. 제한 시간이 없는 `HullDamage`는 타이머를 숨기고
위치 문구를 보여주는 구성이 권장됩니다.
