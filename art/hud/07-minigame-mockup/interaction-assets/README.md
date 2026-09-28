# 항해 상호작용 에셋

승인된 목재·황동·천 스타일을 기준으로 정리한 최종 패키지입니다.

- `icons/`: 조타, 돛 조절, 대포, 선체 수리, 포탄, 수리 자재, 물 양동이. 모두 256×256 RGBA 투명 PNG.
- `ui/`: 글자와 아이콘이 없는 상호작용 패널, 빈 키캡. 크기는 `manifest.json` 참고.
- `preview.png`: 개별 결과 미리보기. 미리보기의 카드와 글자는 아이콘 파일에 포함되지 않습니다.
- `source/approved-concept.png`: 승인 시안 보관.
- `source/transparent-atlas.png`: 이미지 생성 도구로 승인 시안에서 배경과 글자를 제거하고 재배치한 투명 원본. 생성 과정에서 세부 표현은 조금 달라질 수 있습니다.
- `source/*prompt.md`: 제작 프롬프트.
- `tools/package.cjs`: 투명 원본을 영역별로 자르고 크기를 통일하는 재생성 도구. `sharp` 필요.

Unity에서는 Sprite (2D and UI), Single, Alpha Is Transparency, Clamp, Bilinear로 가져오세요. 아이콘은 Preserve Aspect를 켜고 64~96px에서 사용하면 좋습니다. 패널/키캡은 비율을 유지해 사용하거나 테두리를 확인해 9-slice를 설정하세요. 글자, 조작키, 진행 게이지는 별도 UI로 배치합니다.

게임 코드와 프리팹 연결은 변경하지 않았습니다. 기존 코드의 taskIconHelm/Sails/Cannon/Repair에는 대응하는 작업 아이콘을 연결할 수 있습니다. 운반물은 기존 코드가 아이콘을 재사용하므로, 3종을 구분해 연결하려면 별도의 매핑 수정이 필요합니다.
