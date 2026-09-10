# HUD 01 — 프로필 및 역할 라벨

첫 번째 검토용 묶음입니다. 원본 HUD에서 픽셀을 잘라낸 결과가 아니라, 승인된 시안을 참고하여 초상화를 투명 이미지로 재생성하고 공용 UI 도형을 새로 제작했습니다. 초상화의 세부 얼굴/장비 표현은 시안과 차이가 있습니다.

## 파일

| 이름 | 용도 | 크기 |
|---|---|---|
| portrait-diver.png | 잠수부 초상화 | 1254 × 1254 |
| portrait-wig.png | 흰 가발 초상화 | 1254 × 1254 |
| portrait-captain.png | 선장 초상화 | 1254 × 1254 |
| portrait-jester.png | 광대 초상화 | 생성 원본 정사각형 |
| frame-green/red/yellow/purple.png | 중앙이 투명한 색 테두리 4종 | 512 × 512 |
| portrait-backplate.png | 공용 어두운 프로필 배경 | 512 × 512 |
| role-label.png | 텍스트 없는 역할 라벨 | 512 × 144 |

총 PNG 10개. 도형 6종은 SVG 원본도 제공합니다. 프레임과 얼굴에 역할이 고정되어 있지 않습니다. preview.html에서 실제 표시 크기로 조합하고 역할/테두리 색을 변경할 수 있습니다.

## 레이어 및 크기

아래에서 위 순서: 공용 프로필 배경 → 초상화 → 색 테두리. 역할 라벨과 동적 텍스트는 프로필 아래 별도 오브젝트로 둡니다.

기본 프로필 표시 크기 144 × 144, 중앙 정렬. 초상화는 프로필의 82%인 약 118 × 118로 표시하고 Preserve Aspect를 켭니다. 역할 라벨은 144 × 40, 텍스트는 중앙 정렬합니다. 생성된 얼굴마다 내부 여백은 조금 다르므로 최종 게임 화면에서 필요에 따라 초상화 크기를 개별 조정하세요.

## Unity 적용 권장값

- PNG Texture Type: Sprite (2D and UI), Sprite Mode: Single.
- Alpha Source: Input Texture Alpha, Alpha Is Transparency: On.
- Pivot: Center (0.5, 0.5), Wrap: Clamp, Filter: Bilinear, Mip Maps: Off.
- 작은 HUD에서 확인하는 동안 Compression: None 권장.
- 얼굴과 정사각 프레임은 Image Type: Simple, Preserve Aspect 사용.
- 역할 라벨은 가로 확장 시 Sprite Editor Border Left/Right=76, Bottom/Top=0, Image Type: Sliced. 높이는 고정하여 둥근 양 끝을 유지합니다.
- 역할명은 TextMeshPro 등 런타임 텍스트로 별도 배치합니다. 역할 변경 시 텍스트만 교체합니다.
- 프레임 중심은 완전히 투명합니다. 뒤에 공용 backplate를 배치해야 어두운 내부 배경이 나타납니다.

현재 프로젝트의 씬이나 프리팹에는 아직 연결하지 않았습니다. 디자인 확인 후 게임의 실제 Canvas 배율과 표시 크기에 맞춰 적용하세요.

## 제작

초상화: built-in image_gen, 승인된 HUD 시안을 스타일/캐릭터 참고 이미지로 사용. prompts.md에 생성 프롬프트를 기록했습니다.

프레임/라벨/배경: 코드로 제작한 UI 도형. build-ui-shapes.ps1로 PNG와 SVG를 재생성할 수 있습니다.
