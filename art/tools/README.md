# 아트 도구

목업 이미지에서 UI 조각을 잘라내고, 배경으로 남은 흰색을 투명으로 바꾸는 Node 스크립트 모음입니다.

## 준비

```bash
cd art/tools
npm install
```

`sharp` 하나만 씁니다. 예전에는 로컬 PC의 절대 경로를 직접 `require` 하고 있어서 다른 사람 환경에서는 실행되지 않았는데, 지금은 일반 의존성으로 바뀌었습니다.

## 사용

| 명령 | 하는 일 |
|---|---|
| `npm run extract:start` | 시작 화면 목업에서 로고·버튼·아이콘 추출 |
| `npm run extract:login` | 로그인 화면 목업에서 패널·입력칸·탭 추출 |
| `npm run extract:channel` | 채널 선택 목업에서 패널·행·배지·아이콘 추출 |
| `npm run extract:character` | 캐릭터 커스터마이징 목업에서 슬롯·탭·화살표 추출 |
| `npm run clean:channel` | 채널 선택 에셋의 흰 배경을 투명으로 |
| `npm run clean:character` | 캐릭터 커스터마이징 에셋의 흰 배경을 투명으로 |

`cleanup-*` 스크립트는 이미지 가장자리에서 시작해 밝은 무채색 픽셀을 flood fill 로 훑어 알파를 0 으로 만듭니다. 그림 안쪽의 밝은 부분은 가장자리와 이어져 있지 않으면 건드리지 않습니다.

## ⚠ 입출력 경로 주의

모든 스크립트가 저장소 루트의 `Assets/Art/<화면>/` 를 읽고 씁니다.

```js
const root = path.resolve(__dirname, '../../Assets/Art/ChannelSelect');
```

**이 폴더는 `.gitignore` 로 제외되어 있어 clone 직후에는 존재하지 않습니다.** 최종 에셋은 Unity 프로젝트 안에 들어 있습니다.

| 루트 (작업용, git 제외) | Unity (실제 사용, git 포함) |
|---|---|
| `Assets/Art/ChannelSelect/` | `unity/UnderTheSea/Assets/Game/Art/ChannelSelect/` |
| `Assets/Art/CharacterCustomization/` | `unity/UnderTheSea/Assets/Game/Art/CharacterCustomization/` |
| `Assets/Art/LoginScreen/` | `unity/UnderTheSea/Assets/Game/Art/UI/Login,SignUp/` |
| `Assets/Art/StartScreen/` | `unity/UnderTheSea/Assets/Game/Art/UI/Title/` |

스크립트를 돌리려면 둘 중 하나를 하세요.

1. Unity 쪽 에셋을 위 표에 맞춰 `Assets/Art/<화면>/` 으로 복사한 뒤 실행하고, 결과를 다시 Unity 로 옮긴다.
2. 각 스크립트 4~5행의 `root` 경로를 Unity 폴더로 직접 고친다. 다만 이 경우 스크립트가 Unity 에셋을 덮어쓰므로, 손으로 수정한 에셋이 있다면 날아갈 수 있다.

작업용 폴더를 계속 둘 것인지, 아니면 Unity 폴더 하나만 두고 스크립트를 그쪽으로 맞출 것인지는 팀에서 정하는 게 좋겠습니다.
