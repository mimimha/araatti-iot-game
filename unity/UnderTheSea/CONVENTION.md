# UnderTheSea Unity Convention

본 문서는 UnderTheSea Unity 클라이언트 개발을 위한 협업 규칙을 정의합니다.

팀원과 AI 개발 도구는 아래 규칙을 기준으로 프로젝트를 수정합니다.

---

## 1. 프로젝트 구조

Unity 프로젝트 루트는 다음과 같습니다.

```text
unity/UnderTheSea/
```

팀에서 직접 제작하는 게임 관련 Asset은 `Assets/Game` 아래에서 관리합니다.

```text
Assets/
├── Game/
│   ├── Art/
│   ├── Audio/
│   ├── Prefabs/
│   ├── Scenes/
│   │   ├── Develop/
│   │   └── Main/
│   │       ├── CoreGames/
│   │       └── MiniGames/
│   ├── ScriptableObjects/
│   └── Scripts/
│
├── Settings/
└── <External Asset Folders>/
```

### 기본 원칙

* `Assets/Game` : 우리 팀이 직접 제작하고 관리하는 영역
* `Assets/Settings` : Unity 및 Render Pipeline 설정 영역
* 그 외 Import된 Asset 폴더 : 외부 Asset 영역

외부 Asset을 Import했다고 해서 해당 파일들을 모두 `Assets/Game`으로 이동하지 않습니다.

---

## 2. Develop Scene 관리

`Scenes/Develop`은 **각 팀원이 기능을 독립적으로 개발하고 테스트하기 위한 Scene 전용 작업 공간**입니다.

```text
Assets/Game/Scenes/Develop/<이름>/
```

예:

```text
Scenes/
└── Develop/
    ├── GeonHee/
    │   └── PlayerTest.unity
    └── HyoJin/
        └── FishingTest.unity
```

### 중요

`Develop/<이름>` 폴더는 개인 Asset 보관소가 아닙니다.

다음과 같은 실제 프로젝트 결과물은 Develop 폴더에 저장하지 않습니다.

* Prefab
* Script
* Material
* Texture
* Model
* Animation
* ScriptableObject
* Audio

이러한 결과물은 처음부터 `Assets/Game` 아래의 적절한 공용 폴더에 생성합니다.

예를 들어 낚시 기능을 개발한다면:

```text
Scenes/
└── Develop/
    └── HyoJin/
        └── FishingTest.unity

Prefabs/
└── MiniGames/
    └── Fishing/
        └── FishingRod.prefab

Scripts/
└── MiniGames/
    └── Fishing/
        └── FishingRod.cs
```

즉,

```text
Develop Scene
     ↓
기능 테스트
     ↓
Prefab / Script 등의 실제 결과물은
처음부터 공용 폴더에서 관리
```

하는 방식을 사용합니다.

### Develop Scene 규칙

* 자신의 Develop 폴더를 사용합니다.
* 다른 팀원의 Develop Scene은 수정하지 않습니다.
* Develop Scene 자체를 최종 게임 콘텐츠로 사용하지 않습니다.
* Develop Scene은 기능을 테스트하고 조립하기 위한 공간으로 사용합니다.
* 실제 결과물은 `Prefabs`, `Scripts`, `Art` 등의 적절한 공용 폴더에서 관리합니다.

---

## 3. Main Scene 관리

실제 게임에서 사용하는 Scene은 다음 위치에서 관리합니다.

```text
Assets/Game/Scenes/Main/
```

기본 구조:

```text
Main/
├── CoreGames/
└── MiniGames/
```

* `CoreGames` : Boot, Title, Lobby 등 핵심 게임 Scene
* `MiniGames` : 개별 미니게임 Scene

> 확정된 Scene 목록과 각 Scene의 규격은 `GAME_STRUCTURE.md`를 따릅니다.

### Main Scene 규칙

동일한 Main Scene을 여러 사람이 동시에 수정하지 않습니다.

Main Scene 수정이 필요한 경우 해당 Scene의 작업 담당자를 확인합니다.

가능하면 기능을 Main Scene에 직접 구현하지 않고 다음과 같이 분리합니다.

```text
Script / Prefab 등으로 기능 구현
            ↓
      Develop Scene 테스트
            ↓
       Main Scene에 배치
```

Main Scene은 여러 기능을 조립하여 실제 게임을 구성하는 공간으로 사용합니다.

---

## 4. Prefab 관리

Prefab은 Develop/Main으로 구분하지 않습니다.

개발 중인 Prefab이라도 처음부터 적절한 공용 위치에 생성합니다.

예:

```text
Assets/Game/Prefabs/
├── Characters/
├── Environment/
├── Interactables/
├── UI/
└── MiniGames/
    ├── Warriors/
    ├── ShipCoop/
    └── Mine/
```

예를 들어 `FishingTest.unity`에서 낚싯대 GameObject를 만들고 이를 Prefab으로 만들 경우:

```text
Hierarchy
└── FishingRod
       │
       │ Prefab 생성
       ▼
Assets/Game/Prefabs/MiniGames/Fishing/
└── FishingRod.prefab
```

처럼 처음부터 최종 위치에 생성합니다.

### Prefab 이동

Prefab을 잘못된 위치에 생성했다면 **Unity Editor의 Project 창에서 이동합니다.**

예:

```text
잘못된 위치

Scenes/Develop/HyoJin/FishingRod.prefab

        ↓ Unity Project 창에서 이동

Prefabs/MiniGames/Fishing/FishingRod.prefab
```

Unity Editor에서 이동하면 `.meta` 파일과 GUID가 함께 관리되어 기존 Asset 참조를 유지할 수 있습니다.

Windows 탐색기에서 `.prefab` 파일만 따로 이동하지 않습니다.

동일한 Prefab을 여러 사람이 동시에 수정하는 것은 가급적 피합니다.

---

## 5. Script 관리

Script는 다음 위치에서 관리합니다.

```text
Assets/Game/Scripts/
```

기능 단위로 폴더를 분리합니다.

예:

```text
Scripts/
├── Core/
├── Character/
├── Network/
├── UI/
└── MiniGames/
    ├── Warriors/
    ├── ShipCoop/
    └── Mine/
```

개인 Develop 폴더에 Script를 생성하지 않습니다.

개발 중인 Script라도 처음부터 해당 기능의 공용 폴더에 생성합니다.

파일이 많아지기 전부터 지나치게 세분화하지 않고 실제 기능이 추가될 때 필요한 폴더를 생성합니다.

---

## 6. Art / Audio 관리

우리 팀에서 직접 제작하거나 프로젝트 전용으로 관리하는 Art Asset은 다음 위치에 저장합니다.

```text
Assets/Game/Art/
```

필요에 따라 다음과 같이 분류합니다.

```text
Art/
├── Models/
├── Materials/
├── Textures/
├── Animations/
└── VFX/
```

Audio Asset은 다음 위치에서 관리합니다.

```text
Assets/Game/Audio/
```

Art와 Audio 역시 개인 Develop 폴더에 저장하지 않습니다.

---

## 7. 외부 Asset 관리

### 본 프로젝트에서 바로 Import 하지 않습니다

Asset Store 에셋에는 데모 씬, 예제 텍스처, 문서 등 실제로 쓰지 않는 파일이 많이 들어 있습니다.

본 프로젝트에 바로 Import 하면 이런 파일까지 전부 저장소에 올라가고,
**한 번 올라가면 나중에 지워도 Git 기록에 영원히 남습니다.**

따라서 **별도의 테스트 프로젝트에서 먼저 Import 하고, 필요한 것만 옮깁니다.**

### 테스트 프로젝트 만들기

저장소 **바깥**에 만듭니다. `.gitignore` 로 막는 것보다 확실합니다.

```text
C:\project\
├── S15P21C101\              ← 실제 프로젝트 (Git 관리)
│   └── unity\UnderTheSea\
│
└── UnderTheSea-Sandbox\     ← 에셋 테스트용 (Git 관리 안 함)
```

각자 자기 컴퓨터에만 있으면 되고, 팀원끼리 공유하지 않습니다.

만들 때 아래 두 가지를 **실제 프로젝트와 똑같이** 맞춥니다.

| 항목 | 값 |
| --- | --- |
| Unity 버전 | `6000.5.9f1` |
| 템플릿 | Universal 3D (URP) |

버전이 다르면 옮길 때 에셋이 깨지고,
Built-in RP 로 만들면 머티리얼이 **전부 분홍색으로** 넘어옵니다.

### 옮기는 순서

```text
1. 테스트 프로젝트에서 에셋을 Import 한다
        ↓
2. 데모 씬을 실행해보고 실제로 쓸 것을 파악한다
        ↓
3. 필요 없는 것을 제외한다
   (Demo / Example 씬, 문서, 안 쓰는 프리팹과 텍스처)
        ↓
4. 필요한 폴더를 우클릭 → Export Package...
   ⚠ "Include dependencies" 체크
        ↓
5. .unitypackage 파일로 저장한다
        ↓
6. 본 프로젝트에서
   Assets > Import Package > Custom Package
```

**Windows 탐색기에서 폴더를 복사하지 않습니다.**

`Export Package` 를 사용해야 `.meta` 와 GUID 가 유지되어 참조가 깨지지 않고,
딸린 머티리얼·셰이더·텍스처가 함께 따라옵니다.

### Import 전에 팀에 공유합니다

어떤 에셋인지, 용량이 얼마인지 먼저 알립니다.

여러 명이 같은 에셋을 각자 Import 하면 저장소 용량이 금방 초과됩니다.

Import 후에는 `ASSETS.md` 에 기록합니다.

### 폴더 구조는 원본을 유지합니다

Asset Store 또는 외부에서 가져온 Asset은 가능한 한 제작자가 제공한 **원본 폴더 구조를 유지합니다.**


예:

```text
Assets/
├── Game/
├── Settings/
├── StylizedWater/
├── CharacterPack/
└── EnvironmentPack/
```

외부 Asset의 Prefab, Material, Texture, Shader 등을 종류별로 분해하여 `Assets/Game`으로 옮기지 않습니다.

### 외부 Asset 원본 수정

외부 Asset 원본은 가능하면 직접 수정하지 않습니다.

외부 Asset을 기반으로 우리 게임에서 사용할 결과물이 필요하다면 `Assets/Game` 아래에 프로젝트용 Asset을 별도로 생성합니다.

예:

```text
Assets/CharacterPack/
└── Prefabs/
    └── Character.prefab
              │
              │ 기반으로 사용
              ▼
Assets/Game/
└── Prefabs/
    └── Characters/
        └── Player.prefab
```

즉,

```text
외부 Asset 원본
      ↓
가능하면 그대로 유지
      ↓
우리 게임용 결과물
      ↓
Assets/Game/
```

으로 관리합니다.

---

## 8. Unity Asset 이동 및 삭제

Unity Asset을 이동하거나 삭제할 때는 가능한 한 **Unity Editor의 Project 창에서 처리합니다.**

다음과 같은 Asset이 대상입니다.

* Scene
* Prefab
* Material
* Texture
* Model
* Animation
* ScriptableObject
* Script

Windows 탐색기에서 Asset과 `.meta` 파일을 개별적으로 이동하거나 삭제하지 않습니다.

---

## 9. `.meta` 파일 관리

Unity가 생성하는 `.meta` 파일은 반드시 Git으로 관리합니다.

예:

```text
Player.prefab
Player.prefab.meta
```

`.meta` 파일에는 Asset의 GUID가 저장되어 있으며 Scene, Prefab, Material 등의 참조에 사용됩니다.

따라서 다음 행동을 하지 않습니다.

* `.meta` 파일을 임의로 삭제
* `.meta` 파일을 `.gitignore`에 추가
* Asset과 `.meta`를 서로 다른 위치로 이동

---

## 10. Git에 포함하지 않는 파일

Unity에서 자동으로 생성되는 다음 파일 및 폴더는 Git에 포함하지 않습니다.

```text
Library/
Temp/
Obj/
Logs/
UserSettings/
Build/
Builds/
```

이러한 파일은 `.gitignore`를 통해 제외합니다.

Commit 전에 의도하지 않은 파일이 포함되지 않았는지 확인합니다.

```bash
git status
```

---

## 11. Git 작업 흐름

> 브랜치 이름, 커밋 메시지, Merge Request 등 자세한 Git 규칙은
> 저장소 최상위의 `GIT_CONVENTION.md`를 따릅니다. 아래는 요약입니다.

작업을 시작하기 전에 `develop` 브랜치를 최신 상태로 업데이트합니다.

```bash
git switch develop
git pull origin develop
```

그 후 작업 브랜치를 생성합니다.

```bash
git switch -c <타입>/<이니셜>-<작업명>
```

예:

```text
feature/mh-title-ui
feature/sy-player-prefab
feature/hj-lobby-map

fix/mh-spawn-position
chore/gh-setup-netcode
```

작업 완료 후:

```bash
git add .
git commit -m "[feat] 작업 내용"
git push -u origin <브랜치명>
```

GitLab에서 해당 작업 브랜치로 Merge Request를 생성하고 `develop`으로 Merge합니다.

```text
작업 브랜치
     ↓
Merge Request
     ↓
develop
```

`develop`에 직접 push하지 않습니다.

---

## 12. 작업 완료 전 확인

작업을 완료하기 전에 다음 사항을 확인합니다.

* Unity 프로젝트가 정상적으로 실행되는지 확인
* Unity Console에 새 Error가 발생하지 않는지 확인
* 다른 팀원의 Develop Scene을 수정하지 않았는지 확인
* 담당하지 않은 Main Scene이 변경되지 않았는지 확인
* Prefab이나 Script를 Develop 폴더에 저장하지 않았는지 확인
* 외부 Asset 원본을 불필요하게 수정하지 않았는지 확인
* `.meta` 파일이 누락되지 않았는지 확인
* `Library`, `Temp`, `Logs` 등의 파일이 Git에 포함되지 않았는지 확인
* `git status`로 최종 변경 파일 확인

---

# 핵심 규칙

1. 우리 팀이 제작하는 게임 Asset은 `Assets/Game`에서 관리합니다.
2. `Scenes/Develop/<이름>`은 **개인 테스트 Scene을 위한 공간**이며 개인 Asset 보관소로 사용하지 않습니다.
3. Prefab, Script, Material 등의 결과물은 개발 중이라도 처음부터 적절한 공용 폴더에 생성합니다.
4. 외부 Asset은 원본 폴더 구조를 최대한 유지합니다.
5. 외부 Asset 원본은 가능하면 직접 수정하지 않습니다.
6. 동일한 Main Scene을 여러 사람이 동시에 수정하지 않습니다.
7. 기능은 가능한 한 Script / Prefab으로 분리하고 Develop Scene에서 테스트한 후 Main Scene에 적용합니다.
8. 동일한 Prefab을 여러 사람이 동시에 수정하는 것은 가급적 피합니다.
9. `.meta` 파일은 반드시 Git으로 관리합니다.
10. Unity Asset의 이동과 삭제는 가능한 한 Unity Editor에서 수행합니다.
11. 작업 브랜치에서 개발하고 Merge Request를 통해 `develop`에 반영합니다.
