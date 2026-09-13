# ShipCoop HUD — 분리형 에셋 v1

두 번째 HUD 목업의 배치와 색을 기준으로 만든 Unity 조립용 패키지입니다.
목업 이미지를 잘라낸 파일이 아니라, 배경과 문자가 섞이지 않도록 패널·바·아이콘을 벡터로 다시 제작했습니다. 목업과 픽셀 단위로 같지는 않습니다.
초상화 4장은 `01-crew`의 기존 캐릭터 원본을 재사용했습니다. 목업의 빨강·노랑·초록·보라 해적 얼굴과는 다릅니다.

## 파일

- `sprites/`: 투명 PNG 48장 + 각각의 Unity `.meta`. 이 폴더만 Unity로 복사합니다.
- `svg/`: 패널·아이콘 등 편집 가능한 SVG 원본 44개. 초상화는 래스터 원본입니다.
- `manifest.json`: 크기, 용도, 9-slice 경계, Image 타입.
- `contact-sheet.png`: 전체 에셋 목록. 게임에 넣는 파일이 아닙니다.
- `preview.html`: 에셋을 실제로 조합한 배치 예시. HP·항해·침수·수리 진행률 조절 가능.
- `build.cjs`: 재생성 도구. Node.js + sharp 필요. 기존 01-crew 초상화와 프로젝트의 hp-mask.png.meta 템플릿을 읽습니다. 기존 생성 GUID는 유지합니다.

## Unity로 가져오기

`sprites` 폴더를 PNG와 `.meta`를 함께 선택해 `Assets/Game/Art/UI/ShipCoopHudV2/` 같은 새 경로에 복사합니다. 기존 HUD 폴더 위에 덮어쓰지 마세요.
메타에는 Sprite / Single / Full Rect / Pivot Center / 100 PPU / Bilinear / Clamp / 압축 없음 / Mipmap Off와 9-slice 경계를 넣었습니다. 현재 프로젝트 형식의 메타를 바탕으로 작성했으며 Unity 에디터에서의 실제 임포트·플레이 검증은 아직 하지 않았습니다.

Canvas Scaler: Scale With Screen Size, 1920×1080, Match 0.5. 모든 장식 Image의 Raycast Target은 Off.
9-slice 지정 에셋은 Image Type=Sliced, fill은 아래 규칙대로 Filled로 별도 설정합니다. 이 설정은 Sprite 메타가 아니라 UI Image 컴포넌트의 설정입니다.
배경 투명도를 조절할 때 배경 Image의 alpha만 낮추세요. 부모 전체를 투명하게 하면 텍스트까지 흐려집니다.

## 권장 배치

아래는 1920×1080 기준, 화면 좌상단에서의 x/y입니다. Unity에서는 각 그룹의 지정 anchor와 pivot을 사용해 해상도 변화에 대응하세요.

| 그룹 | x,y | 폭×높이 | Anchor / Pivot |
|---|---|---|---|
| 페이즈 | 28,24 | 180×56 | 좌상 / 좌상 |
| 배 HP | 600,24 | 520×64 | 상단 중앙 / 상단 중앙 |
| 시간 | 1136,24 | 184×64 | 상단 중앙 / 상단 중앙 |
| 항해 | 600,102 | 720×68 | 상단 중앙 / 상단 중앙 |
| 사건 목록 | 28,116 | 410×104 각 행, 간격 12 | 좌상 / 좌상 |
| 팀원 4명 | 28,876 | 570×176 | 좌하 / 좌하 |
| 침수 | 680,958 | 560×94 | 하단 중앙 / 하단 중앙 |
| 현재 작업 | 1510,872 | 382×180 | 우하 / 우하 |

상단 HP·시간·항해는 폭 720의 하나의 부모 그룹 아래에 둡니다. 그룹 x=600, y=24이고 내부 HP x=0, 시간 x=536, 항해 x=0/y=78입니다. 하단 y 값은 1080 기준이므로 Unity에서는 아래쪽 여백으로 환산합니다.

## 컴포넌트 조립

### 공통 패널

아래에서 위로 `*-background → *-frame → 아이콘 → TMP`.
배경과 프레임은 같은 RectTransform 크기. 프레임 중심은 투명합니다.
`panel-capsule`은 HP·시간·항해·페이즈에 재사용하고 폭만 다르게 설정합니다.

### HP / 항해 / 침수 / 사건 카운트다운

`bar-track` 위에 `bar-fill-white`를 좌우 6px, 위아래 6px 안쪽에 둡니다. 원본 512×32 기준입니다.
HP/침수/카운트다운: Type=Filled, Method=Horizontal, Origin=Left, FillAmount=0~1.
색: HP `#48D889`, 항해 `#64C9F1`, 침수 `#53BAED`에서 고수위 `#FF5864`, 예고 `#FFBC55`, 발생 `#FF5864`.
채움의 폭 자체를 줄이지 말고 fillAmount를 바꾸세요. 채움 PNG는 전체 길이이며 72% 등이 구워져 있지 않습니다.

항해 진행 아이콘은 `icon-ship`, 예정 위치는 `voyage-reference`. 두 마커와 지연 구간을 같은 내부 트랙 부모 아래 둡니다.
부모 폭 W에서 shipX=W×Clamp01(progress), expectedX=W×Clamp01(elapsed/timeLimit).
`bar-delay`는 Image Simple, pivot=(0,0.5), x=shipX, width=Max(0,expectedX-shipX). 0이면 숨깁니다. 진행도 채움 다음, 마커 아래에 배치합니다.
기준선은 트랙 밖까지 나오므로 그룹 전체에 RectMask2D를 걸지 않습니다.
출발지·목적지 아이콘은 포함하지 않았습니다.

### 사건 카드

`panel-event-background` + `event-frame-warning` 또는 `event-frame-danger` + 같은 색 accent + 사건 아이콘 + 제목/대응문구/시간 TMP.
기본 `panel-event-frame`은 중립 카드에만 사용합니다. 상태 프레임 두 개를 동시에 켜지 않습니다.
카드 원본 640×156 기준: 아이콘 (20,36,80,80), 제목 (118,20,382,42), 설명 (118,70,490,40), 시간 (520,20,96,42), 카운트다운 (118,128,490,8).
암초=reef, 적선=enemy-ship, 파손=hull-breach, 돌풍=sails, 파도=water.
예고→발생에서 같은 행의 색·문구·남은 시간을 갱신합니다. 선체 파손은 제한시간과 타이머 트랙/채움을 모두 숨깁니다.

### 초상화

`portrait-backplate → (Mask 아래 portrait-*) → portrait-frame-{색} → 번호/현재 행동 TMP`.
마스크는 backplate 스프라이트 Image + Mask(showMaskGraphic=false)로 만듭니다. 프레임은 Mask 밖 형제로 둡니다.
빨강/노랑/초록/보라는 플레이어 구분이며 직업이 아닙니다. 행동 문구는 계속 바뀝니다. 개인 HP나 하트는 없습니다.

### 현재 작업

`panel-action-background/frame` 옆에 원형 그룹을 겹칩니다.
원형 그룹: `ring-background → ring-track → ring-fill-white → ring-frame → icon-repair 등 작업 아이콘`.
ring-fill-white: Type=Filled, Method=Radial360, Origin=Top, Clockwise=true, Color=`#FFD05D`.
원형 스프라이트 4개는 동일한 크기와 위치. 진행률은 0~1. UI 예시의 3/5는 0.6입니다.
키 배경은 keycap, F/X/Space 등의 글자는 TMP로 따로 둡니다.
양동이·포탄·판자를 들고 있을 때는 대응 아이콘과 운반 안내를 보여줍니다. 장치가 IoT면 키보드 문자 대신 장치용 안내를 넣으세요.

### 침수와 도움 요청

침수는 water>0일 때만 보입니다. 최신 SHIPCOOP.md에 따라 침수량과 초당 HP 피해를 표시합니다. 구멍이 남아 있으면 '구멍부터 수리!'를 함께 표시합니다.
`badge-player` 위에 1~4 TMP, `badge-help` 위에 `icon-warning` 등을 넣습니다. 월드 위치를 화면에 투영하는 동작은 별도 코드가 필요합니다.

## 확인 범위

PNG 크기·알파 채널·프레임 중심 투명·진행 링 중심 투명·파일 수를 자동 확인합니다. 전체 목록은 contact-sheet.png로 시각 확인했습니다.
이 패키지는 UI 스프라이트이며 C# MonoBehaviour나 완성 HUD 프리팹은 아닙니다. 실제 게임 상태 연결은 기존 ShipCoopHud에서 각 Image/TMP를 연결해야 합니다.
기존 씬/프리팹 변경, 커밋, 푸쉬는 하지 않았습니다.
