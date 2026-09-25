# IoT 로비 튜토리얼 이미지

## 사용 권장 파일

`unity/UnderTheSea/Assets/Game/Art/UI/Tutorial/`의 `icon-iot-*-fit-v2.png`가 최종 배치 조정본입니다.

| 조작 | 파일 | 픽셀 크기 |
| --- | --- | --- |
| 이동 | icon-iot-move-fit-v2.png | 1367×821 |
| 점프 | icon-iot-jump-fit-v2.png | 1809×525 |
| 카메라 | icon-iot-look-fit-v2.png | 1175×733 |

기존 이미지의 조작부를 확대하고 손잡이 하단을 페이드 처리했습니다. 점프 그림은 A 버튼을 분리하여 재배치했습니다. 새 이미지 생성 없이 원본 픽셀을 가공했으며, 캔버스 크기와 투명 배경을 유지했습니다. 최종본의 Unity 메타 파일은 Sprite 설정입니다.

`fit-iot-tutorial.ps1`은 이 배치를 재현하는 스크립트이고, `iot-tutorial-layout-comparison-v2.png`는 전후 비교 이미지입니다.

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

### 이동
Use case: style-transfer. Create a production transparent PNG Unity tutorial icon, replacing the keyboard subject with the referenced physical IoT wand. Image 1 is exact device geometry reference: broad oval head, one large circular joystick at top, A then B round buttons vertically below, straight ribbed handle with elongated oval grip. Image 2 and 3 are visual style references only: polished low-poly faceted cream ivory objects, warm amber beveled edges and cyan active control glow. Preserve recognizable device geometry. MOVEMENT asset: show two upright matching wands side by side, LEFT wand larger foreground and right wand muted smaller behind, visibly highlight ONLY LEFT joystick with cyan light and four directional cyan arrows around that joystick. Both A/B buttons neutral. Small crisp ivory 'L' below left wand and 'R' below right wand, no other text except device A/B. Broad landscape composition target 1367x821; use width efficiently, comfortable 4% transparent margin, complete objects uncropped. Real transparent alpha background, no rectangle, no panel, no keyboard keys, no hands, no environment. Same fantasy game HUD quality as references; dark subtle outline to remain legible on brown panel.

### 점프
Use case: style-transfer. Create a transparent Unity tutorial JUMP icon replacing a very wide Space key asset. Reference 1 physical IoT wand exact shape (oval joystick head, A above B, ribbed elongated oval grip). Reference 2 movement icon establishes MATCHING DESIGN and low-poly faceted cream ivory material, amber bevel outlines, dark brown controls, cyan activated control. Produce a VERY WIDE horizontal composition aspect about 3.45:1, target 1809x525. On left third show a complete small upright RIGHT wand (joystick neutral dark, upper A button glowing bright cyan, lower B neutral); small ivory R beside its handle. Center has a short subtle golden connector leading from A button towards the right. On the right half show a large circular faceted cyan button with ivory 'A', as a magnified callout of the upper button to press; surround with two short press indication rays, not directional arrows. A button is the largest most legible element. All objects entirely within frame with clear transparent margin, fill horizontal width, no huge blank areas, keep complete wand. Exactly one device and one enlarged A button. Only text A/B on device, R beside device, A on enlarged button. No other text. Real transparent alpha background, no panel, no keyboard, no hands, no landscape. Polished consistent game HUD illustration.

### 카메라
Use case: style-transfer. Generate one transparent PNG Unity tutorial CAMERA icon. Reference 1 is exact physical wand geometry, reference 2 is the already approved movement icon STYLE AND MATCHING DEVICE design. Same cream ivory faceted body, amber edge highlights, dark brown A and B buttons, oval joystick head and ribbed straight grip. Show two wands side by side; LEFT wand smaller muted in background and RIGHT wand larger foreground, upright. Highlight ONLY RIGHT joystick cyan with cyan left and right arrows around its head to teach camera rotation. The left joystick and all A/B buttons stay dark neutral. Put a small ivory L beside lower left wand and R beside lower right wand. No other text except A/B on buttons. Complete objects with 5% clear transparent margins, landscape 1175x733 aspect ratio. Real alpha transparency, no panel or background, no hands. Maintain exact same illustration family as reference 2.
