# HUD 02 — 배 HP / 항해

검정 외곽선, 둥근 캡, 흰색 실루엣 아이콘을 사용하는 독립 UI 에셋입니다. 첫 묶음과 같은 색/외곽선 체계로 코드 도형을 제작했습니다. 시안의 픽셀 추출물은 아니며, imagegen은 사용하지 않았습니다.

PNG 15개와 동일한 도형의 SVG 15개를 제공합니다. 숫자와 문자는 이미지에 포함하지 않았습니다. `preview.html`은 PNG를 조합하여 HP, 배 위치, 기준선, 속도, 시간을 조절하는 미리보기입니다. `build-assets.ps1`로 원본을 재생성할 수 있습니다.

| 파일 | 크기 | 용도 |
|---|---|---|
| hp-frame | 1024 × 96 | 중앙 투명, 검정/회색 캡슐 외곽선 |
| hp-track | 1024 × 96 | HP 빈 공간의 어두운 배경 |
| hp-fill-green | 1000 × 72 | 전체 체력 채움; 좌측부터 마스킹 |
| hp-mask | 1000 × 72 | 추가 클리핑이 필요할 때 사용하는 흰 캡슐 마스크 |
| voyage-frame | 1024 × 64 | 중앙 투명한 항해 트랙 테두리 |
| voyage-track | 1024 × 64 | 빈 항해 트랙 배경 |
| voyage-delay-fill | 32 × 32 | 배–기준선 사이로 가로 확장하는 빨간 채움 |
| voyage-reference-marker | 32 × 112 | 독립적인 금색 점선 기준선 |
| icon-ship | 256 × 256 | HP 대표 아이콘 / 항해 위치 아이콘에 재사용 |
| icon-harbour | 256 × 256 | 출발지 |
| icon-island | 256 × 256 | 목적지 |
| icon-speed | 256 × 256 | 선택형 속도 표시 |
| timer-label | 320 × 96 | 시간 텍스트용 빈 배경 |
| speed-label | 320 × 96 | 속도 텍스트용 빈 배경 |
| heading-label | 320 × 96 | SHIP HP 등 제목용 빈 배경 |

## HP 배치

원본 크기 기준 `(0,0)`에 track, `(12,12)`에 fill, `(0,0)`에 frame을 이 순서대로 놓습니다. 좌표는 화면 좌상단 기준입니다. 세 요소를 하나의 부모에서 동일 비율로 축소합니다. fill은 고정 크기 1000 × 72 상태로 좌측에서 보이는 범위만 바꿉니다. 이미지 자체를 HP에 따라 찌그러뜨리지 않습니다.

`fillAmount = clamp(currentHp / maxHp, 0, 1)`이며 maxHp가 0 이하일 때는 0으로 처리합니다. Unity Image Type=Filled, Fill Method=Horizontal, Origin=Left를 사용할 수 있습니다. 이미 채움 PNG가 캡슐 모양이므로 기본 구현에는 hp-mask가 필요하지 않습니다. 별도 머티리얼/직사각 채움으로 바꾸는 경우에만 hp-mask를 사용합니다.

## 항해 배치 및 계산

트랙 내부의 유효 x 범위는 `24..1000`, 길이는 `976`입니다. `progress`와 `reference`는 0..1로 제한합니다.

```text
shipX = 24 + 976 * progress
referenceX = 24 + 976 * reference
delayLeft = shipX
delayWidth = max(0, referenceX - shipX)
```

delay-fill을 `(delayLeft,16)`에 크기 `(delayWidth,32)`로 배치합니다. 순서는 track → delay-fill → frame → marker → ship입니다. 배가 기준선보다 앞서면 빨간 지연 구간은 숨깁니다. 기준선과 배가 같은 위치이면 너비가 0입니다. 이것은 속도 막대가 아니라 항해 진행도와 예정 진행도의 차이입니다.

marker는 `(referenceX-16,-24)`에 32 × 112로, 배 아이콘은 `(shipX-52,-48)`에 104 × 104로 배치하면 preview와 일치합니다. 배 아이콘의 pivot은 중앙입니다. 부모의 스케일을 모든 수치에 동일하게 적용합니다.

Unity에서는 delay-fill의 Image Type=Simple, Preserve Aspect=Off로 가로 폭만 바꿉니다. 기준선의 x 좌표는 Image 이동으로 갱신합니다. 마커는 트랙 밖으로 돌출되므로 전체 그룹에 RectMask2D를 걸지 마세요.

## 텍스트 / 선택형 속도

시간, 역할과 마찬가지로 HP 제목, 남은 시간, 속도는 TextMeshPro 등 런타임 텍스트로 배치합니다. `3:42`나 `8.5`가 들어 있는 이미지가 아닙니다. 속도 표시와 화살표는 기존 시안에 없던 선택형 요소입니다. preview의 `kn`은 표시 예시이며 게임의 단위나 속도 계산 방식을 지정하지 않습니다.

## 가져오기

- Texture Type=Sprite (2D and UI), Single, Pivot=Center.
- Alpha Is Transparency=On, Mip Maps=Off, Wrap=Clamp, Filter=Bilinear.
- 첫 시각 확인은 Compression=None 권장.
- 비율을 유지하며 전체 HUD를 축소할 때 바/프레임은 Simple로 사용합니다.
- 라벨 길이만 늘릴 때 Sprite Border L/R=52, B/T=0, Image Type=Sliced, 높이 고정.
- 바 폭만 늘리는 별도 레이아웃에서는 hp-frame/track L/R=60, B/T=0, voyage-frame/track L/R=40, B/T=0로 Sliced를 설정하고 내부 fill 영역과 위 좌표를 새 폭에 맞춰 계산합니다. 기본 패키지/preview는 원본 비율 축소 방식입니다.

Unity 프로젝트의 `Assets/Game/Art/UI/ShipCoopHud/Voyage/` 로 들어가 `ShipCoopHud.prefab` 에 붙어 있습니다.

붙이지 않은 것: `icon-harbour`, `icon-island` (바 끝에서 34px 라 뭉개지고, 바에 이미 끝이 있어 알려주는 것이 없습니다), `icon-speed`/`speed-label` (선택형), `hp-fill-green` (스크립트가 남은 양에 따라 초록·주황·빨강으로 다시 칠하므로 흰 `hp-mask` 를 씁니다).
