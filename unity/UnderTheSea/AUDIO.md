# 아라아띠 오디오 — 소리를 넣는 법

이 문서는 **어느 씬이든, 어느 미니게임이든** 배경음악과 효과음을 붙이는 방법입니다.
팀원이 직접 읽어도 되고, AI(Claude 등)에게 **이 문서를 그대로 건네고** 시켜도 됩니다. 맨 끝에 그럴 때 쓰는 프롬프트 틀이 있습니다.

관련 문서: `GAME_STRUCTURE.md` 8장(미니게임 규칙 3항) · `CONVENTION.md` 6장(Audio 폴더) · `SHIPCOOP.md` 4장(배 협동 소리 절 — 첫 적용 사례)

---

## 1. 한 줄 요약

```text
소리를 내는 것은 AudioHub 하나뿐이다.  씬은 "무엇을 언제" 만 정해서 허브에 부탁한다.
```

```text
Assets/Game/Scripts/Audio/
├── AudioHub.cs      공용. 씬을 넘어 살아남는 하나. 배경음악 교차 페이드 · 효과음 · 루프 · 볼륨
└── SceneMusic.cs    공용. 씬에 하나 놓고 클립을 꽂으면 그 씬의 배경음악

Assets/Game/Scripts/MiniGames/<내 게임>/
└── <내 게임>Audio.cs   내 것. 게임 상태를 보고 허브에 PlayMusic · PlayOneShot · Loop 를 부른다 (연출가)

Assets/Game/Audio/<내 게임>/     클립 파일 (CONVENTION 6장). 개인 Develop 폴더에 두지 않는다
```

**왜 허브가 하나인가.** 씬마다 AudioSource 를 두면 씬이 바뀔 때 음악이 끊깁니다. 이 게임은 로비 → 미니게임 → 로비로
계속 오가므로 음악이 씬을 따라 이어져야 합니다. 허브는 처음 `AudioHub.Instance` 를 부르는 순간 스스로 생겨
`DontDestroyOnLoad` 로 남습니다. **씬에 미리 놓을 것이 없습니다.**

**왜 연출가는 게임마다 따로인가.** "내 게임에서 언제 무슨 소리" 는 내 게임의 타입(상태 · 사건 · 작업)을 읽어야 합니다.
그걸 공용 허브에 넣으면 공용 코드가 미니게임에 묶이고, 다른 담당자가 공용 파일을 고쳐야 합니다(GAME_STRUCTURE 9장 위반).
게임 씬이 끝나면 감시자도 함께 죽어야 하는데, 허브는 살아남는다는 수명 차이도 있습니다.

---

## 2. 허브가 해 주는 것 — `AudioHub`

```csharp
using UnderTheSea.Audio;

AudioHub hub = AudioHub.Instance;          // 없으면 만들어진다. 앱 종료 중이면 null

hub.PlayMusic(clip, fadeSeconds: 1.5f);    // 배경음악. 같은 곡이면 그대로 이어지고, 다른 곡이면 교차 페이드
hub.StopMusic(1.5f);                       // 페이드 아웃
hub.CurrentMusic                           // 지금 곡 (null 이면 없음)

hub.PlayOneShot(clip, level: 1f);          // 효과음 한 번. 2D. level 에 효과음 볼륨이 곱해진다

var wind = hub.Loop("mygame.wind", windClip, fadeSeconds: 0.8f);   // 루프 손잡이. 같은 이름은 같은 손잡이
wind.Target = 0.7f;                        // 매 프레임 목표(0~1)만 정하면 허브가 페이드해서 켜고 · 키우고 · 끈다
hub.StopAllLoops();                        // 미니게임을 떠날 때

hub.MusicVolume / EffectsVolume / MasterVolume   // 0~1. 바꾸면 PlayerPrefs 에 저장
hub.CanHear                                // 서버(그래픽 장치 없음)면 false. 모든 호출이 조용히 빠진다
```

- **2D 입니다.** 배 위 어디에 서 있어도 같게 들립니다. 위치 음향이 필요하면 그때 따로 정합니다.
- **서버에서는 아무것도 만들지 않습니다.** Dedicated Server 빌드에서 `AudioListener`·`AudioSource` 는 각 미니게임의
  `*ServerCleanup` 이 끄는데, 허브는 그 전에 스스로 소스를 안 만듭니다. 부르는 쪽은 서버인지 신경 쓰지 않습니다.
- **볼륨은 타이틀 설정과 같은 키입니다.** `AraAtti.Audio.MasterVolume` · `MusicVolume` · `EffectsVolume` · `Muted`
  (`StartMenuController` · `SettingsPanelView`). 허브가 켤 때 복원하고, 설정이 바뀌면 `StartMenuController` 의
  static 이벤트로 받습니다. 마스터는 `AudioListener.volume` 그대로 — **믹서(AudioMixer)는 없습니다.** 새로 들이려면
  `SetMasterVolume` 의 AudioListener 방식과 이중 관리가 되니 팀에서 먼저 정합니다.
- **로비에서 F1 은 음소거 토글입니다.** (`LobbyMuteHotkey` → `AudioHub.ToggleMute`) 음소거 ↔ 설정 슬라이더에 맞춰 둔
  마스터 크기를 오갑니다. 슬라이더 값은 건드리지 않고 `Muted` 깃발만 바꿉니다. 광산 · 배 협동은 F1 이 디버그 화면이라
  로비에서만 받습니다.

---

## 3. 씬에 배경음악만 넣기 — `SceneMusic`

로비 · 타이틀 · 채널 선택처럼 **한 곡만 흐르는 씬**은 코드가 필요 없습니다.

1. 씬에 빈 오브젝트를 만들고(이름 `Music`) `SceneMusic` 을 붙인다.
2. `music` 에 클립을 꽂는다. `fadeSeconds` 는 앞 곡과 겹치는 시간.
3. 끝.

```text
앞 씬과 같은 곡           끊기지 않고 이어진다
다른 곡                   교차 페이드
클립을 비워 두면          앞 씬 음악을 멈춘다
다음 씬에 SceneMusic 없음  음악이 그대로 이어진다 (로비 → 채널 선택처럼 같은 분위기를 이어 갈 때)
                          끊고 싶으면 stopWhenLeaving 을 켠다
```

> ⚠ 상태에 따라 곡이 바뀌는 미니게임 씬에는 `SceneMusic` 을 **두지 않습니다.** 연출가(4장)와 서로 곡을 바꿔 댑니다.

---

## 4. 미니게임 소리 넣기 — 연출가 스크립트

상태에 따라 곡이 바뀌고 효과음이 여러 개면 **내 게임 폴더에 `<내 게임>Audio.cs`** 를 만듭니다.
배 협동의 `ShipCoopAudio.cs` 가 첫 사례입니다. 그대로 베껴서 타입 이름만 바꾸면 됩니다.

### 4.1 뼈대

```csharp
using UnderTheSea.Audio;
using UnityEngine;

[DisallowMultipleComponent]
public class MineAudio : MonoBehaviour          // 씬에 하나. 오브젝트 이름은 "Audio"
{
    [Header("🎵 배경음악")]
    [SerializeField] private AudioClip bgmReady;
    [SerializeField] private AudioClip bgmPlaying;
    [SerializeField] private AudioClip stingerClear;
    [SerializeField] private AudioClip stingerFail;

    [Header("🌊 루프")]
    [SerializeField] private AudioClip caveLoop;

    [Header("💥 효과음")]
    [SerializeField] private AudioClip pickHit;
    // … 게임에 맞게. 클립이 비어 있으면 그 소리만 안 난다 (허브가 null 을 무시한다)

    [SerializeField, Range(0f, 1f)] private float loopLevel = 0.5f;
    [SerializeField, Range(0f, 1f)] private float sfxLevel = 0.9f;

    private AudioHub _hub;
    private AudioHub.LoopHandle _cave;
    private MineGame _game;                     // 내 게임의 상태를 들고 있는 것
    private MineState _stateSeen;

    private void Awake()
    {
        _hub = AudioHub.Instance;
        if (_hub == null || !_hub.CanHear) { enabled = false; return; }   // 서버

        _cave = _hub.Loop("mine.cave", caveLoop, 1.2f);   // 이름은 "게임.용도" 로. 다른 게임과 안 겹치게
    }

    private void Start()
    {
        _game = FindAnyObjectByType<MineGame>(FindObjectsInactive.Include);
    }

    private void OnDisable()
    {
        // 씬을 떠난다. 루프는 끄고 음악은 페이드 아웃 — 다음 씬의 SceneMusic 이 새 곡을 올린다.
        if (_hub != null) { _hub.StopAllLoops(); _hub.StopMusic(1.5f); }
    }

    private void Update()
    {
        if (_game == null) return;

        // 1) 바뀐 순간을 잡아 효과음
        if (_game.State != _stateSeen)
        {
            _stateSeen = _game.State;
            if (_stateSeen == MineState.Cleared) _hub.PlayOneShot(stingerClear, sfxLevel);
            if (_stateSeen == MineState.Failed)  _hub.PlayOneShot(stingerFail, sfxLevel);
        }

        // 2) 상태에 맞는 곡 — 매 프레임 불러도 된다. 같은 곡이면 허브가 무시한다
        switch (_game.State)
        {
            case MineState.Ready:   _hub.PlayMusic(bgmReady);   break;
            case MineState.Playing: _hub.PlayMusic(bgmPlaying); break;
            default:                _hub.StopMusic();           break;   // 결과 화면은 스팅어만
        }

        // 3) 루프는 목표만
        _cave.Target = (_game.State == MineState.Playing ? 1f : 0f) * loopLevel;
    }
}
```

### 4.2 ⚠ 네트워크 미니게임에서 반드시 지킬 것 — **클라이언트가 아는 값만 본다**

판정은 서버에서만 돕니다. 그래서 **서버 판정이 쏘는 `event Action` 에 소리를 걸면 호스트에서만 납니다.**
(`SHIPCOOP.md` 11장 · `WaterDumpSplash.cs` 주석) 클라이언트 화면에서 소리가 나려면 두 길 중 하나입니다.

```text
① 복제된 값을 매 프레임 읽어 "바뀐 순간" 을 잡는다   ← 기본. 예: Hits 가 늘었다 → 망치 소리
② 서버가 횟수를 복제하고, 클라이언트가 늘어난 만큼 낸다  ← 한 번 나는 사건. 예: 발사 수(FireCount) · 물 버린 수(DumpCount)
```

배 협동의 실제 훅 (베낄 때 참고):

| 소리 | 보는 값 | 왜 이것 |
| --- | --- | --- |
| 사건 예고 · 시작 | `VoyageEvent.CurrentStage` 가 바뀜 | 단계는 `ShipCoopEventSync` 가 복제 |
| 망치 | `RepairTask.Hits` 가 늘어남 | `ShowRepair` 로 복제 |
| 물 버림 | `WaterDumpPoint.DumpCount` 가 늘어남 | `StateSync.DumpCounts` 로 복제 |
| 대포 발사 | `CannonTask.Recoiled` 이벤트 | 예외 — 클라에서도 터지도록 `FireCount` 복제로 만들어 둔 것 |
| 암초 충돌 / 스침 | `Reef.WasHit` · `WasDodged` | 판정(`SyncExtra`)이 복제 |
| HP 감소 | `ShipHealth.CurrentHp` 가 줄어듦 | `ShowHp` 로 복제 |

새 미니게임에서 "이 순간에 소리" 가 필요한데 복제되는 값이 없으면, **횟수 하나를 `[Networked]` 로 늘려** 복제하는 것이
관례입니다 (`ShipCoopStateSync.DumpCounts` · `TaskGauge.Extra` 참고).

### 4.3 배경음악 규칙 (통일)

```text
대기 곡         시작 전 로비 · 준비 화면
본편 곡         플레이 중
긴장 곡 (선택)  위기 상태가 1초 넘게 이어질 때. 짧은 사건마다 곡이 널뛰지 않게 지연을 둔다
결과 스팅어     성공 1개 · 실패 1개. 한 번 나고 배경음악은 멈춘다 (결과 화면은 조용히)
```

교차 페이드 1.5초, 루프 페이드 0.3~1.2초가 배 협동 기본값입니다. 다른 게임도 비슷하게 두면 씬을 넘어가도 톤이 맞습니다.

---

## 5. 클립 파일 — 어디에, 어떤 이름으로

```text
Assets/Game/Audio/
├── Common/       여러 씬이 같이 쓰는 것 (UI 클릭 · 로비 음악 …)
├── ShipCoop/     bgmReady.ogg  bgmSailing.ogg  …  (필드 이름 = 파일 이름)
├── Mine/
└── Warriors/
```

- **파일 이름을 연출가의 필드 이름과 같게** 둡니다. 그러면 설치 도구가 이름으로 자동 연결합니다
  (`ShipCoopAudioInstaller` — 메뉴 `아라아띠/배 협동/소리 놓고 클립 채우기`. 다른 게임도 같은 도구를 베껴 씁니다).
- 확장자는 `.ogg` 권장(작고 루프 이음새가 깨끗함). `.wav` · `.mp3` 도 됩니다. `.gitattributes` 가 셋 다 LFS 로 잡습니다.
- 루프 클립은 **이음새가 없는지** 먼저 들어 봅니다. 끊기면 Import 설정에서 Load Type 을 `Compressed In Memory` 로.
- 배경음악 Import 설정: `Streaming`, 효과음: `Decompress On Load`. (Unity 기본값이면 대체로 무방)
- 저작권: CC0 또는 팀이 권리를 가진 것만. 출처는 `ASSETS.md` 에 한 줄 적습니다.

---

## 6. 점검표 — 소리를 넣고 나서

```text
□ 씬을 두 번 오가도 음악이 끊기지 않는가 (로비 → 미니게임 → 로비)
□ 결과 화면에서 배경음악이 멈추고 스팅어만 나는가
□ 남의 화면(클라이언트)에서도 효과음이 나는가 — 서버 판정 이벤트에 걸었으면 안 난다 (4.2)
□ 타이틀 설정에서 음악 · 효과음 볼륨을 내리면 바로 줄어드는가
□ Dedicated Server 로그에 AudioSource 관련 오류가 없는가 (허브는 소스를 안 만든다)
□ 클립 파일이 Assets/Game/Audio/<게임>/ 에 있고 개인 Develop 폴더에 없는가
```

---

## 7. AI 에게 시킬 때 — 이 프롬프트를 쓰세요

아래를 복사해 `<>` 만 채우면 됩니다. 문서 경로를 함께 주는 것이 핵심입니다.

```text
Unity 프로젝트 unity/UnderTheSea 의 <광산(Mine)> 미니게임에 배경음악과 효과음을 넣어 줘.

먼저 unity/UnderTheSea/AUDIO.md 를 읽고 그 구조를 그대로 따라. 요점:
- 소리는 공용 AudioHub(Assets/Game/Scripts/Audio/AudioHub.cs) 하나로 낸다. 씬에 AudioSource 를 새로 만들지 마.
- 내 게임 폴더(Assets/Game/Scripts/MiniGames/<Mine>/)에 <Mine>Audio.cs 연출가를 만들어
  게임 상태를 보고 hub.PlayMusic · PlayOneShot · Loop 를 부르게 해. 배 협동의 ShipCoopAudio.cs 를 본보기로.
- 네트워크 판정은 서버에서만 돌아. event Action 에 소리를 걸지 말고, 클라이언트에 복제되는 값이 바뀐 순간을 잡아.
  복제되는 값이 없으면 횟수를 [Networked] 로 하나 늘려 복제해.
- 곡은 대기 · 본편 · (선택) 긴장 · 성공 스팅어 · 실패 스팅어. 결과 화면에서는 배경음악을 멈춰.
- 클립은 Assets/Game/Audio/<Mine>/ 에 필드 이름과 같은 파일 이름으로 두고, 이름으로 자동 연결하는
  에디터 설치 도구를 ShipCoopAudioInstaller.cs 를 본보기로 만들어 (메뉴 "아라아띠/<광산>/소리 놓고 클립 채우기").
- 공용 파일(Scripts/Audio/*)과 다른 담당자 폴더는 고치지 마. 필요하면 나에게 말해.
- 소리를 낼 순간 목록: <곡괭이 타격, 광석 획득, 낙석 경고, 낙석 충돌, 카트 이동 루프, 시간 10초 전 경고 …>
- 끝나면 AUDIO.md 6장 점검표대로 확인하고, <MINE>.md 에 소리 절을 추가해.
```

---

## 8. 자주 하는 실수

| 증상 | 원인 | 고치기 |
| --- | --- | --- |
| 씬 바뀌면 음악이 뚝 끊김 | 씬 안 AudioSource 로 틀었다 | `SceneMusic` 이나 허브 `PlayMusic` 으로 |
| 내 화면엔 나는데 남의 화면엔 안 남 | 서버 판정 `event Action` 에 걸었다 | 복제된 값의 변화를 보거나 횟수를 복제 (4.2) |
| 서버 로그에 AudioSource 오류 | 씬 AudioSource 를 서버가 켠 채로 둠 | 허브를 쓰면 안 생긴다. 남은 소스는 `*ServerCleanup` 이 끈다 |
| 설정에서 볼륨을 내려도 안 줄어듦 | 자기 AudioSource 볼륨을 직접 정했다 | 허브의 `level` 만 주고 볼륨 곱은 허브에 맡긴다 |
| 두 곡이 번갈아 계속 바뀜 | 미니게임 씬에 `SceneMusic` 과 연출가가 같이 있다 | `SceneMusic` 을 뺀다 |
| 클립을 넣었는데 안 남 | 필드 이름과 파일 이름이 다르다 / 설치 도구를 안 돌렸다 | 이름 맞추고 메뉴 다시 실행 |
