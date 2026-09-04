# 아트 도구

목업 이미지에서 UI 조각을 잘라내고, 배경으로 남은 흰색을 투명으로 바꾸는 Node 스크립트 모음입니다.

## 준비

```bash
cd art/tools
npm install
```

`sharp` 하나만 씁니다.

## 사용

```bash
npm run extract:channel     # 채널 선택 화면 에셋 추출
npm run clean:channel       # 흰 배경을 투명으로
npm run fix:rivets          # 채널 줄 양 끝 금색 리벳 제거
```

| 명령 | 대상 화면 |
|---|---|
| `extract:start` | 시작 화면 — 로고·버튼·아이콘 |
| `extract:login` | 로그인 화면 — 패널·입력칸·탭 |
| `extract:channel` | 채널 선택 — 패널·행·배지·아이콘 |
| `extract:character` | 캐릭터 커스터마이징 — 슬롯·탭·화살표 |
| `clean:channel` | 채널 선택 에셋 알파 정리 |
| `clean:character` | 캐릭터 커스터마이징 에셋 알파 정리 |
| `fix:rivets` | `channel-row-base.png` 리벳 제거 |

## 입출력 경로

각 스크립트는 **Unity 프로젝트의 아트 폴더를 직접** 읽고 씁니다. 소스 목업(`*-reference.png`)도 같은 폴더에 함께 있습니다.

| 스크립트 | 대상 폴더 (`unity/UnderTheSea/Assets/Game/Art/` 기준) |
|---|---|
| `*-start-screen-*` | `UI/Title/` |
| `*-login-screen-*` | `UI/Login,SignUp/` |
| `*-channel-select-*` | `ChannelSelect/` |
| `*-character-customization-*` | `CharacterCustomization/` |

예전에는 저장소 루트의 `Assets/Art/` 라는 별도 작업 폴더를 거쳤는데, 최종 에셋과 내용이 같은 사본이라 어느 쪽이 최신인지 헷갈렸습니다. 그 폴더는 `.gitignore` 로 빠졌고 스크립트는 Unity 폴더 하나만 봅니다.

## ⚠ 생성된 에셋은 손으로 고치지 마세요

스크립트가 만드는 파일을 이미지 편집기로 직접 수정하면, **다음에 누가 스크립트를 돌리는 순간 조용히 되돌아갑니다.**

고칠 게 생기면 둘 중 하나로 하세요.

1. 소스 목업(`*-reference.png`)을 고치고 다시 추출한다
2. 보정 로직을 스크립트에 추가한다 — `fix-channel-row-rivets.cjs` 가 그 예입니다

`fix:rivets` 는 배 아이콘과 겹치던 금색 리벳을 지웁니다. 리벳을 색으로만 찾아 지우면 둘레의 어두운 그림자 링이 남기 때문에, 리벳 위치를 탐지한 뒤 그 높이 구간의 양 끝을 판재 색으로 덮습니다. 이미 지워진 파일에 다시 실행해도 안전합니다.

## 손으로 그린 에셋 (스크립트가 건드리지 않음)

아래는 처음엔 스크립트가 단순 도형으로 만들었지만 지금은 손으로 다시 그린 것들입니다. 덮어쓰지 않도록 생성 대상에서 제외해 두었습니다.

| 파일 | 조치 |
|---|---|
| `ChannelSelect/refresh.png` | `extract:channel` 생성 목록에서 제외 |
| `ChannelSelect/button-join-base.png` | `extract:channel` 생성 목록에서 제외 |
| `ChannelSelect/channel-panel-frame.png` | `clean:channel` 처리 대상에서 제외 |

새로 손으로 그린 에셋이 생기면 해당 스크립트에서도 빼주고 이 표에 추가해 주세요.
