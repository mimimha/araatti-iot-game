# IoT 로비 튜토리얼 이미지

## 사용 권장 파일

`unity/UnderTheSea/Assets/Game/Art/UI/Tutorial/`의 `icon-iot-*-fit-v2.png`가 최종 배치 조정본입니다.

| 조작 | 파일 | 픽셀 크기 |
| --- | --- | --- |
| 이동 | icon-iot-move-fit-v2.png | 1367×821 |
| 점프 | icon-iot-jump-fit-v2.png | 1254×1254 |
| 카메라 | icon-iot-look-fit-v2.png | 1175×733 |

이동·카메라는 기존 이미지 조작부를 확대하고 손잡이 하단을 페이드 처리했습니다. 점프는 내장 image_gen 편집으로 연결선·외부 A 버튼·R 표기를 제거하고 기기 자체의 A 버튼만 파랗게 강조했습니다. 투명 정사각형 캔버스로 교체하여 기존 158×110 이미지 영역에서 크게 표시됩니다. 컨테이너와 Sprite 메타 GUID는 유지했습니다.

`fit-iot-tutorial.ps1`은 이동·카메라 배치만 재현하며 점프 편집본을 덮어쓰지 않습니다. `iot-tutorial-layout-comparison-v2.png` 및 아래 인게임 캡처의 점프 이미지는 정사각형 교체 전 버전입니다.

실제 로비에서 최종본을 임시 적용한 1920×1080 캡처는 `art/screenshots/iot-tutorial/`에 있습니다. 순서는 이동 → 점프 → 카메라 → 마지막 안내입니다. 프리팹과 실행 코드는 변경하지 않았고, IoT 연결 감지 및 튜토리얼 자동 전환 기능은 아직 적용하지 않았습니다.

## 원본 생성 기록

생성: 내장 image_gen 도구. 최종 PNG는 비율을 유지하여 기존 에셋과 동일한 캔버스 크기에 배치했습니다. 투명 여백 포함.

- icon-iot-move.png: 1367×821, 왼손 스틱 강조
- icon-iot-jump.png: 1809×525, 오른손 위쪽 A 버튼(버튼 1로 해석) 강조
- icon-iot-look.png: 1175×733, 오른손 스틱 강조

참조: 사용자 제공 KEY_MAPPING.md의 로비 매핑 및 10_정투상_정면.png. 양손 2대 기준. 기존 panel-empty-clean.png와 button-skip.png 재사용. 코드/프리팹 연결은 수정하지 않았습니다.

문구 제안: 이동 — 왼손 스틱 움직이기 / 점프 — 오른손 A 버튼 누르기 / 둘러보기 — 오른손 스틱 움직이기.
마지막 문구: 다양한 사람들을 만나,\n바다의 심장 조각을 함께 모아보세요.
현재 코드 순서는 이동→둘러보기→점프→마지막 문구입니다. 이미지 파일은 순서에 독립적입니다.

## 생성 프롬프트

### 점프 단일 기기 편집 (현재 버전)
내장 image_gen 사용. 기존 점프 이미지 편집: 왼쪽 기기 상단의 정면 형태와 아이보리/금색 스타일을 유지한다. 금색 연결선, 오른쪽의 확대 A 버튼, 외부 R 문자를 제거한다. 실제 기기의 A 버튼만 청록색으로 강조하고 조이스틱과 B 버튼은 어두운 중립색으로 유지한다. 기기 상단과 짧은 손잡이만 정사각형 투명 캔버스 중앙에 크게 배치한다. 손잡이 끝은 투명하게 페이드 처리한다. 외부 선, 화살표, 문구, 배경은 넣지 않는다.

### 이동
Use case: style-transfer. Create a production transparent PNG Unity tutorial icon, replacing the keyboard subject with the referenced physical IoT wand. Image 1 is exact device geometry reference: broad oval head, one large circular joystick at top, A then B round buttons vertically below, straight ribbed handle with elongated oval grip. Image 2 and 3 are visual style references only: polished low-poly faceted cream ivory objects, warm amber beveled edges and cyan active control glow. Preserve recognizable device geometry. MOVEMENT asset: show two upright matching wands side by side, LEFT wand larger foreground and right wand muted smaller behind, visibly highlight ONLY LEFT joystick with cyan light and four directional cyan arrows around that joystick. Both A/B buttons neutral. Small crisp ivory 'L' below left wand and 'R' below right wand, no other text except device A/B. Broad landscape composition target 1367x821; use width efficiently, comfortable 4% transparent margin, complete objects uncropped. Real transparent alpha background, no rectangle, no panel, no keyboard keys, no hands, no environment. Same fantasy game HUD quality as references; dark subtle outline to remain legible on brown panel.

### 점프
Use case: style-transfer. Create a transparent Unity tutorial JUMP icon replacing a very wide Space key asset. Reference 1 physical IoT wand exact shape (oval joystick head, A above B, ribbed elongated oval grip). Reference 2 movement icon establishes MATCHING DESIGN and low-poly faceted cream ivory material, amber bevel outlines, dark brown controls, cyan activated control. Produce a VERY WIDE horizontal composition aspect about 3.45:1, target 1809x525. On left third show a complete small upright RIGHT wand (joystick neutral dark, upper A button glowing bright cyan, lower B neutral); small ivory R beside its handle. Center has a short subtle golden connector leading from A button towards the right. On the right half show a large circular faceted cyan button with ivory 'A', as a magnified callout of the upper button to press; surround with two short press indication rays, not directional arrows. A button is the largest most legible element. All objects entirely within frame with clear transparent margin, fill horizontal width, no huge blank areas, keep complete wand. Exactly one device and one enlarged A button. Only text A/B on device, R beside device, A on enlarged button. No other text. Real transparent alpha background, no panel, no keyboard, no hands, no landscape. Polished consistent game HUD illustration.

### 카메라
Use case: style-transfer. Generate one transparent PNG Unity tutorial CAMERA icon. Reference 1 is exact physical wand geometry, reference 2 is the already approved movement icon STYLE AND MATCHING DEVICE design. Same cream ivory faceted body, amber edge highlights, dark brown A and B buttons, oval joystick head and ribbed straight grip. Show two wands side by side; LEFT wand smaller muted in background and RIGHT wand larger foreground, upright. Highlight ONLY RIGHT joystick cyan with cyan left and right arrows around its head to teach camera rotation. The left joystick and all A/B buttons stay dark neutral. Put a small ivory L beside lower left wand and R beside lower right wand. No other text except A/B on buttons. Complete objects with 5% clear transparent margins, landscape 1175x733 aspect ratio. Real alpha transparency, no panel or background, no hands. Maintain exact same illustration family as reference 2.
