# HUD 04 — 현재 작업 원형 게이지 / 월드 상호작용

PNG 12개와 동일 도형의 SVG 12개를 제공합니다. 앞 묶음과 같은 검정 외곽선/흰 아이콘 체계로 코드 도형을 제작했습니다. imagegen 생성이나 시안 픽셀 추출물이 아닙니다. 제목·작업명과 현재 진행률은 개별 이미지에 포함되어 있지 않습니다.

| 파일 | 크기 | 용도 |
|---|---|---|
| task-background | 512 × 512 | 반투명 어두운 작업 배경 |
| task-frame | 512 × 512 | 외곽 테두리, 중앙 투명 |
| task-progress-track | 512 × 512 | 비어 있는 회색 진행 링 |
| task-progress-fill | 512 × 512 | 100% 노란 진행 링, 중앙 투명 |
| task-label | 512 × 128 | 작업명용 빈 라벨 |
| world-background | 512 × 512 | 선택형 반투명 빌보드 배경 |
| world-ring | 512 × 512 | 흰 원형 빌보드 테두리 |
| world-focus-ring | 512 × 512 | 독립된 노란 선택 강조 |
| icon-repair | 256 × 256 | 망치 / 수리 |
| icon-sails | 256 × 256 | 정상 상태의 돛 / 돛 조작 |
| icon-helm | 256 × 256 | 조타륜 / 조타 |
| icon-cannon | 256 × 256 | 정상 상태의 대포 / 장전·대포 조작 |

## 작업 게이지

공통 캔버스 크기 512 × 512, Pivot Center. 아래에서 위 순서: background → progress-track → progress-fill → frame → 작업 아이콘. 각 링과 배경은 같은 크기/위치로 정렬합니다. 아이콘은 중앙에 256 × 256으로 배치합니다. 부모 그룹을 균일하게 축소합니다. 예: 전체 표시 230 × 230, 내부 아이콘 115 × 115.

진행률은 `duration > 0 ? clamp(elapsed / duration, 0, 1) : 0` 또는 게임에서 계산한 정규화 값을 사용합니다. duration 0인 즉시 완료 작업의 완료 처리는 게임 로직에서 별도로 결정합니다. fill 이미지는 완전한 원이며, 65%가 구워진 이미지가 아닙니다.

Unity Image의 Type=Filled, Fill Method=Radial 360, Fill Origin=Top, Clockwise=On, Fill Amount=진행률로 설정합니다. 12시에서 시작하여 시계 방향으로 채웁니다. 0에서는 노란 링이 보이지 않고 1에서는 완전한 원이 됩니다. 원형 링 자체의 중앙이 투명하므로 pie 모양 노란 원판이 되지 않습니다. track과 frame은 Simple입니다.

작업명은 task-label 위에 TextMeshPro로 별도 배치합니다. preview의 REPAIRING, TRIMMING SAILS, STEERING, LOADING은 표시 예시입니다. 실제 작업 상태가 바뀌면 아이콘과 텍스트만 교체합니다. 기본 배치는 우하단에 두되 실제 화면 여백과 Canvas 배율은 게임 레이아웃에서 결정합니다.

## 월드 빌보드

아래에서 위: 선택형 world-background → world-ring → world-focus-ring → 재사용 작업 아이콘. 세 원형 에셋은 동일 512 × 512 위치. 아이콘은 중앙에 약 297 × 297 (58%)로 배치합니다. 선택 강조는 대상 선택/상호작용 가능 상태에 맞춰 활성화합니다. 배경이 충분히 어두운 장면에서는 world-background를 숨길 수 있습니다.

빌보드는 이미지 에셋만으로 월드 위치를 추적하지 않습니다. 게임 구현에서 월드 작업 지점 위의 앵커와 연결해야 합니다. World Space Canvas를 카메라와 평행하게 회전시키거나, Screen Space Canvas에서 월드 좌표를 화면 좌표로 변환하는 방식을 선택합니다. 카메라 뒤의 대상, 가림, 거리, 입력 가능 여부에 따른 표시/숨김도 게임 로직이 담당합니다. 이 미리보기는 평면 에셋 조합이며 실제 3D 추적 구현은 포함하지 않습니다.

## 가져오기

- Texture Type Sprite (2D and UI), Single, Pivot Center (0.5,0.5).
- Alpha Is Transparency On, Wrap Clamp, Filter Bilinear, Mip Maps Off.
- 첫 확인은 Compression None 권장.
- 원형 배경·링·아이콘은 Simple/Preserve Aspect; progress-fill만 Filled.
- task-label은 폭만 늘릴 경우 Border L/R=70, B/T=0, Sliced; 높이 고정.
- SpriteRenderer로 빌보드를 구성할 때는 Unity UI Image의 Radial Fill을 직접 사용할 수 없으므로 작업 게이지는 UI Canvas에서 구성하거나 별도 shader/mask를 사용합니다.

## 파일 및 범위

preview.html에서 작업 아이콘, 원형 진행률, 선택 강조, 월드 배경을 바꾸고 진행 애니메이션을 재생할 수 있습니다. preview.png는 조합 예시입니다. build-assets.ps1과 render-core.ps1은 도형 원본을, build-preview.ps1은 정적 예시를 재생성합니다.

이 단계까지 요청한 네 에셋 묶음이 완성되었습니다. 작업 게이지는 Unity 프로젝트의 `Assets/Game/Art/UI/ShipCoopHud/Interactions/` 로 들어가 `ShipCoopHud.prefab` 아래쪽 상호작용 안내에 붙어 있습니다. 월드 빌보드(world-*)는 아직 붙이지 않았습니다.
