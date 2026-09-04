# Character Customization Screen Assets

분리된 에셋은 1097×732 원본 화면을 기준으로 Unity Canvas에 배치할 수 있게 정리했습니다.

## 주요 에셋

- `customization-panel-frame.png`: 문양·로프 장식이 포함된 빈 양피지 패널(실제 알파)
- `character-preview.png`: 해적 캐릭터 + 석재 받침대 투명 컷아웃
- `shoe-01.png` ~ `shoe-10.png`: 신발 선택 아이템 스프라이트
- `shoe-slot-01-reference.png` ~ `shoe-slot-10-reference.png`: 원본 타일 모양/상태 참고용
- `tab-*-reference.png`: 헤어/얼굴/상의/하의/신발/액세서리 탭 참고용
- `arrow-*-reference.png`, `gender-*.png`: 캐릭터 좌우 이동과 성별 아이콘
- `swatch-reference.png`, `swatch-selected-reference.png`: 색상 칩 기본/선택 상태
- `button-complete-reference.png`: `생성 완료` 버튼 참고용
- `customization-screen-reference.png`: 원본 합성 화면, `character-customization-preview.png`: QA 확인용

`*-reference` 파일은 원본의 글자·아이콘이 함께 들어간 시각 참고 에셋입니다. 실제 게임에서는 버튼/탭/아이템 슬롯을 재사용하고, 라벨은 TextMeshPro로 별도 렌더링하세요. 선택 상태는 슬롯 위에 주황색 외곽선과 체크마크 오버레이를 켜는 방식으로 처리하면 아이템별 활성/비활성 이미지를 따로 만들 필요가 없습니다.

## 권장 Canvas 계층

`Background` → `CharacterPreview` → `CustomizationPanel` → `CategoryTabs` → `ItemGrid` → `ColorSwatches` → `CompleteButton` → `NavigationOverlay`

좌표와 입력 동작은 `character-customization-layout.json`을 기준으로 시작한 뒤, Canvas Scaler(Reference Resolution 1097×732)에 맞춰 조정하세요. 한글은 프로젝트에 포함된 `NotoSansKR-Bold SDF.asset`을 사용합니다.

생성된 투명 컷아웃은 ImageGen으로 분리한 후 체크무늬 배경을 실제 알파로 정리했습니다.
