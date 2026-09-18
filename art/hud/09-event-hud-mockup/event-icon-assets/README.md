# 이벤트 픽토그램 5종

승인된 `event-icon-style-sheet-v1.png`을 기준으로 built-in image_gen에서 각각 별도로 생성했습니다.

- `icons/event-reef.png`: 암초
- `icons/event-enemy-ship.png`: 적선
- `icons/event-hull-damage.png`: 선체 파손·침수 최종본 — 돛과 파손 구멍, 물 유입
- `icons/event-big-wave.png`: 거대한 파도
- `icons/event-squall.png`: 돌풍

모두 512×512 RGBA 투명 PNG입니다. 아이콘 본체 크기와 중앙 여백을 통일했으며, 완전 투명 픽셀과 반투명 가장자리를 검증했습니다. `preview.png`의 체크무늬는 투명도 확인용이며 실제 아이콘에 포함되지 않습니다.

Unity 권장값: Sprite (2D and UI), Single, Alpha Is Transparency On, Mip Maps Off, Wrap Mode Clamp, Filter Mode Bilinear, Compression None. 이벤트 HUD에서는 48~64px 표시를 기준으로 사용하세요.

`source/`에는 이미지 생성 원본, `prompts.json`에는 실제 생성 프롬프트가 있습니다. 아직 씬이나 프리팹에는 연결하지 않았습니다.
