Unity의 가도가도 안내 팝업을 아래 에셋으로 적용해줘.

에셋:
C:/project/S15P21C101/art/hud/08-tutorial-popup/gadogado-popup.png
1536×1024 투명 PNG. 제목·설명·아이콘·테두리·오른쪽 Enter 닫기 안내가 한 장에 포함되어 있고, 하단 왼쪽 카운트다운 영역만 비어 있다.

수정 대상:
- unity/UnderTheSea/Assets/Game/Scripts/MiniGames/ShipCoop/UI/ShipCoopTutorialView.cs
- unity/UnderTheSea/Assets/Game/Prefabs/MiniGames/ShipCoop/ShipCoopTutorial.prefab
관련 씬의 프리팹 오버라이드도 확인해줘.

요구사항:
1. PNG를 Assets/Game/Art/UI/ShipCoopHud/Tutorial/로 복사하고 Sprite (2D and UI), Single, Full Rect, Alpha Is Transparency, Mip Map Off, Max Size 2048 이상으로 설정해줘.
2. 팝업은 하나의 Unity UI Image로 표시해줘. Image Type은 Simple, Preserve Aspect On. 9-slice나 비율을 바꾸는 늘리기는 사용하지 말고, 기존 본문 텍스트는 겹쳐 나오지 않게 비활성화해줘.
3. 기존 Canvas 구조를 확인하고 화면 중앙에 배치해줘. 1920×1080 기준 표시 크기는 약 1350×900, 3:2 비율을 유지하며 작은 화면에서도 화면 안에 들어오게 축소해줘. 팝업 뒤에는 별도 검정 반투명 딤 배경을 둬.
4. 하단 빈 공간에는 TextMeshProUGUI를 별도로 추가하고 footerLabel로 연결해줘. 이미지 RectTransform을 부모로 삼아 anchorMin=(0.13, 0.074), anchorMax=(0.70, 0.133), offsetMin/Max=(0,0)을 시작값으로 배치하고 실제 화면에서 미세 조정해줘. 이는 투명 여백을 포함한 전체 PNG 기준이다. 왼쪽 정렬·수직 중앙·한 줄, 따뜻한 연금색 #D4BB8C, 원본 크기 기준 약 26px 글꼴로 맞춰줘.
5. 문구는 처음에 "10초 후 자동으로 닫힙니다."로 표시하고 9초, 8초…로 감소시켜줘. Mathf.CeilToInt로 남은 초를 계산하고, 0초가 되면 팝업과 딤 배경을 닫아줘. 실제 표시 시점부터 Time.unscaledTime으로 10초를 측정하고 Show()마다 타이머를 초기화해줘.
6. 현재 autoHideSeconds 기본값 18초를 10초로 바꾸고 프리팹 및 씬 직렬화 값도 함께 맞춰줘. 기존 Awake()의 footer 할당과 Update()의 자동 닫힘 로직을 정리해 이전 문구나 타이머가 중복으로 동작하지 않도록 해줘. 기존 Enter·숫자패드 Enter·Esc 닫기와 게임 종료 시 닫기는 유지해줘. 이 팝업은 개인 안내이므로 게임이나 네트워크 진행을 일시정지하지 마.
7. 1920×1080과 1280×720에서 비율·글자 겹침·카운트다운 위치를 확인하고, 표시 직후 10초 표기, 10초 후 자동 닫힘, Enter 닫힘, 재표시 시 타이머 초기화를 검증해줘.

완료 후 변경 파일과 검증 결과를 알려줘.
