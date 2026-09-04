# Channel Select Screen Assets

채널 선택 화면을 Unity에서 조립할 수 있도록 공통 에셋과 채널 전용 에셋을 분리했습니다. 기준 해상도는 1300×733이며 배치 좌표는 `channel-select-layout.json`에 있습니다.

## 로그인 화면에서 복사한 공통 에셋

- `background-clean.png`: `UI/Login,SignUp/background-clean.png` 복사본
- `button-back-clean.png`: `UI/Login,SignUp/button-back-clean.png` 복사본
- `background-channel-reference.png`: 기존 채널 전용 항구 배경 보관본

## 에셋 구성

- `crew.png`: 시안의 서 있는 네 캐릭터를 한 그룹으로 분리한 610×450 투명 스프라이트
- `crew-transparent.png`: 로그인 화면의 지도 테이블 캐릭터 그룹 보관본
- `channel-panel-frame.png`: 제목·행·버튼이 제거된 700×590 빈 남색/금색 패널
- `channel-row-base.png`: 비선택 채널용 628×103 빈 행
- `channel-row-selected-base.png`: 선택 채널용 628×103 청록색 빈 행
- `channel-row-selected-overlay.png`: 필요할 때 선택 행 위에 추가할 금색/청록 외곽선
- `ship-icon-01.png`: 채널 행에 배치할 해적선 아이콘
- `status-smooth-base.png`, `status-crowded-base.png`: 원활/혼잡 상태 배지 바탕
- `refresh.png`: 새로고침 아이콘
- `button-join-base.png`: 364×97 입장 버튼 바탕
- `selected-channel-compass.png`: 선택된 행 우측에 겹치는 64×64 나침반
- `title-spark.png`: 제목 양옆에 사용하는 32×32 금색 장식
- `button-back-reference.png`: 돌아가기 버튼 참고용
- `channel-row-01-reference.png` ~ `channel-row-03-reference.png`: 원본 행 비교용
- `refresh-reference.png`, `button-join-reference.png`, `status-*-reference.png`: 원본 UI 참고용

`*-reference` 파일은 원본의 글자와 상태 표시가 포함된 시각 참고 이미지입니다. 실제 구현에서는 행 하나를 프리팹으로 만들고 채널명, 상태, 인원수는 TextMeshPro로 별도 렌더링하세요.

## 권장 런타임 구조

`Background` → `Crew` → `ChannelPanel/Frame` → `ChannelPanel/Title + TitleSpark × 2` → `ChannelPanel/Refresh` → `ChannelList/ChannelRowPrefab` × N → `SelectedCompass` → `JoinButton` → `BackButton`

각 `ChannelRowPrefab`에는 `RowBackground`, `SelectedOverlay`, `ShipIcon`, `ChannelName`, `StatusBadge`, `PlayerCount`를 두세요. 선택 상태에서는 `channel-row-selected-base.png`로 배경을 교체하고 첫 번째 선택 행에만 `selected-channel-compass.png`를 겹칩니다. 상태 배지는 `status-smooth-base.png` 또는 `status-crowded-base.png`를 사용하고 라벨은 TMP로 표시합니다.

한글은 `Assets/Game/Fonts/NotoSansKR-Bold SDF.asset`을 지정하세요. 배경과 프레임은 `Image`의 Preserve Aspect를 켜고, 반복 행/버튼은 9-slice를 사용하면 다양한 해상도에서도 자연스럽게 늘어납니다.

패널, 행, 캐릭터, 장식은 투명 PNG로 저장되어 있습니다. 제목, 채널명, 상태, 인원수, 입장 문구는 이미지에 포함하지 않고 TextMeshPro로 렌더링하세요.
