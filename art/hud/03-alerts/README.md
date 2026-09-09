# HUD 03 — 사건 알림 카드와 아이콘

투명 PNG 10개와 동일 도형 SVG 10개. 이전 묶음의 검정 외곽선과 색 체계에 맞춰 코드로 제작했습니다. imagegen을 사용하거나 시안에서 픽셀을 잘라낸 결과는 아닙니다. `preview.png`의 문자/카드 조합은 사용 예시이며 개별 에셋에는 문자가 없습니다.

| 파일 | 크기 | 용도 |
|---|---|---|
| alert-background | 960 × 240 | 어두운 반투명 카드 배경 |
| alert-frame | 960 × 240 | 중심이 투명한 얇은 외곽선 |
| alert-accent-red | 24 × 184 | 왼쪽 붉은 경고선 |
| alert-new-highlight | 960 × 240 | 독립된 주황색 강조 외곽선, 중앙 투명 |
| alert-icon-slot | 192 × 192 | 반투명 아이콘 슬롯 |
| alert-countdown-track | 680 × 16 | 타이머 빈 트랙 |
| alert-countdown-fill | 680 × 16 | 주황색 채움, 전체 길이 상태 |
| icon-hull-breach | 256 × 256 | 부서진 판자와 물방울 |
| icon-sail-torn | 256 × 256 | 찢어진 돛 |
| icon-cannon-jam | 256 × 256 | 대포와 고장 표시 |

## 조합

좌상단 기준, 기본 카드 크기 960 × 240:

1. 배경과 frame은 `(0,0)`, 크기 960 × 240.
2. accent는 `(24,28)`, 크기 24 × 184.
3. icon-slot은 `(60,24)`, 크기 192 × 192. 사건 아이콘은 `(76,40)`, 크기 160 × 160.
4. 제목은 `(280,36)` 부근, 설명은 `(280,103)` 부근. 오른쪽 여백 40 이상. 실제 언어/폰트에 맞춰 텍스트 영역을 조절합니다.
5. countdown track/fill은 `(280,188)`, 표시 크기 640 × 16. 원본 680 × 16을 같은 크기로 표시합니다.
6. highlight는 `(0,0)`, 크기 960 × 240, 최상단. 새 알림일 때만 활성화하고 opacity를 바꾸어 점멸/페이드합니다.

카드 전체 부모를 균일하게 축소합니다. preview는 약 640 × 160으로 표시합니다. 라벨/시간/아이콘은 서로 독립적으로 바꿀 수 있습니다. 아이콘 자체에 카드나 배경은 없습니다.

## 실시간 값

`remainingRatio = totalDuration > 0 ? clamp(remainingSeconds / totalDuration, 0, 1) : 0`

타이머 fill 이미지는 고정 크기 상태로 왼쪽부터 보이는 양만 바꿉니다. Unity Image Type=Filled, Fill Method=Horizontal, Origin=Left, Fill Amount=remainingRatio. 이미지의 가로 크기를 직접 줄이면 둥근 끝이 찌그러질 수 있습니다. 값 0에서는 fill을 숨기며 track은 남깁니다. 만료 이후 사건 삭제/실패 처리 규칙은 게임 로직에서 결정합니다.

사건 종류를 바꿀 때 icon sprite와 제목·설명 텍스트만 교체합니다. 같은 카드 프리팹을 리스트로 사용하고 실제 사건 우선순위에 따라 순서를 정합니다. preview.html에서는 첫 카드의 종류/비율/강조를 바꾸고 카운트다운을 재생할 수 있습니다. preview의 제목과 설명은 예시입니다.

## Unity 가져오기

- Sprite (2D and UI), Single, Pivot Center, Alpha Is Transparency On.
- Filter Bilinear, Wrap Clamp, Mip Maps Off. 첫 확인은 Compression None 권장.
- 배경/프레임/highlight: 폭만 늘릴 경우 Border L/R=48, B/T=48, Image Type=Sliced. 텍스트와 타이머의 우측 앵커도 함께 늘립니다.
- icon-slot: Border 28 all sides, Sliced 가능. 사건 아이콘은 Simple, Preserve Aspect On.
- 경고선은 고정 비율로 사용합니다. 타이머 track은 Simple 또는 L/R=8, B/T=0의 Sliced; fill은 Filled.
- 역할/사건명은 TextMeshPro 등 동적 텍스트로 구현합니다.
- 별도 glow shader 없이도 highlight sprite의 CanvasGroup alpha로 강조를 조절할 수 있습니다.

## 범위 / 재생성

Unity 프로젝트의 `Assets/Game/Art/UI/ShipCoopHud/Alerts/` 로 들어가 `ShipCoopHud.prefab` 의 사건 알림 4줄에 붙어 있습니다. `build-assets.ps1`은 함께 포함된 render-core.ps1을 사용하며 이전 묶음 파일을 수정하지 않습니다. `build-preview.ps1`은 조합 예시를 생성합니다.

다음 묶음: 현재 작업 원형 게이지와 월드 상호작용 아이콘.
