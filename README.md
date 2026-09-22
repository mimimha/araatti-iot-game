# 아라아띠

IoT 체감형 해양 게임 · SSAFY 15기 · 팀 C101

> **저장소와 Unity 프로젝트 폴더 이름은 `UnderTheSea` 를 그대로 사용합니다.**
> 폴더 이름을 바꾸면 팀 전원이 경로를 다시 잡아야 하므로,
> **게임 이름(아라아띠)과 폴더 이름(UnderTheSea)은 다릅니다.** 정상입니다.

## 팀 구성 (6명)

| 역할 | 인원 |
| --- | --- |
| 게임 클라이언트 (Unity) | 3명 |
| 게임 서버 | 1명 |
| IoT / 하드웨어 | 2명 |

## 미니게임

| 미니게임 | Scene | 담당 |
| --- | --- | --- |
| 무쌍 게임 | `Warriors.unity` | 서연 |
| 배 협동 게임 | `ShipCoop.unity` | 민화 |
| 광산 게임 | `Mine.unity` | 효진 |

세 게임 모두 **4명 고정**입니다.

로비(허브)를 돌아다니다가 각 미니게임 입구에서 참가하면,
**4명이 모였을 때 자동으로 시작**됩니다.

자세한 규격은 [GAME_STRUCTURE.md](unity/UnderTheSea/GAME_STRUCTURE.md) 7장을 참고하세요.

## 개발 문서

작업을 시작하기 전에 아래 문서를 확인합니다.

| 문서 | 내용 |
| --- | --- |
| [GIT_CONVENTION.md](GIT_CONVENTION.md) | 브랜치 전략, 커밋 메시지, Merge Request 규칙 |
| [unity/UnderTheSea/CONVENTION.md](unity/UnderTheSea/CONVENTION.md) | Unity 파일과 폴더를 어디에 두는가 |
| [unity/UnderTheSea/GAME_STRUCTURE.md](unity/UnderTheSea/GAME_STRUCTURE.md) | 게임 흐름, 네트워크 방식, 담당별 규격 |
| [unity/UnderTheSea/ASSETS.md](unity/UnderTheSea/ASSETS.md) | 사용한 외부 에셋 기록 |

## 음악 크레딧

배 협동 게임 배경음악은 [Pixabay](https://pixabay.com) 에서 받았습니다.
셋 모두 [Pixabay Content License](https://pixabay.com/service/license-summary/) — 무료 · 상업적 이용 가능 · 출처 표기 의무 없음.

| 화면 | 곡 | 작곡 | 출처 |
| --- | --- | --- | --- |
| 대기 | Set Sail | Forgotten-Hero-Records | https://pixabay.com/music/main-title-set-sail-350596/ |
| 출항(항해) | Pirate Tavern (Full Version!) | Magiksolo (Artem Hramushkin) | https://pixabay.com/music/main-title-pirate-tavern-full-version-167990/ |
| 결과 | Pirate Adventure Loop | Ebunny | https://pixabay.com/music/orchestral-pirate-adventure-loop-557984/ |

효과음 출처는 [unity/UnderTheSea/Assets/Game/Audio/ShipCoop/CREDITS.md](unity/UnderTheSea/Assets/Game/Audio/ShipCoop/CREDITS.md) 에 있습니다.

---

> 1주차 기획 회의 기록(후보 아이디어 6종)은 이 문서에서 제거했습니다.
> 필요하면 `git show d11a784:README.md` 로 확인할 수 있습니다.
