# 무쌍(Warriors) 소리 — 출처

이 폴더에 넣은 클립의 출처를 **한 줄씩** 적습니다. 저작권은 CC0 · Pixabay Content License(배 · 광산과 같음) 또는 팀이 권리를 가진 것만 씁니다.
에셋 팩 단위로는 저장소 뿌리의 `ASSETS.md` 목록에도 한 줄 추가합니다. (AUDIO.md 5장)

파일 이름은 **연출가(`WarriorsAudio`)의 필드 이름과 같아야** 합니다. 그래야
메뉴 `Tools/아라아띠/Warriors 소리 놓고 클립 채우기` 가 이름으로 자동 연결합니다.
확장자는 `.ogg` 권장, `.wav` · `.mp3` 도 됩니다.

## 지금 들어 있는 것 — 효과음 18개 (CC0) · 배경음악 2곡 · 파도 루프 (Pixabay)

길이는 실측값입니다. 빈 줄은 아직 안 넣은 것이고, **그 소리만 안 납니다.**

| 파일 (= 필드 이름) | 언제 나는가 | 길이 | 원본 | 출처 |
| --- | --- | --- | --- | --- |
| `bgmWaiting` | 대기 · 시작 카운트다운 | 1:03 | Pirate Jolly Roger Loop (Ebunny) | Pixabay |
| `bgmRound1` | 1라운드 해변 방어 | — | 파일 없음 — `bgmWaiting` 과 **같은 클립**을 설치 도구가 이어 꽂는다(대기에서 끊기지 않고 이어짐) | Pixabay |
| `bgmRound2` | 2라운드 촉수 절단 | — | 파일 없음 — `bgmWaiting` 과 **같은 클립**(대기부터 2라운드까지 한 곡이 이어짐) | Pixabay |
| `bgmRound3` | 3라운드 리듬 전투 (긴장) | 1:42 | Pirates Battle (Ebunny) — 너무 웅장하지 않은 쪽으로 골랐다 | Pixabay |
| `stingerClear` | 성공. 한 번 나고 음악은 멈춤 | 1.51초 | 도-미-솔-도 팡파르 — **직접 만듦** | 팀 제작 |
| `stingerFail` | 실패. 한 번 나고 음악은 멈춤 | 1.55초 | `jingles_STEEL07` | Kenney Jingles |
| `waveLoop` | 해변 파도 루프 (대기부터 결과까지 같은 크기) | 1:01 | Gentle Ocean Shore Waves (DRAGON-STUDIO) | Pixabay |
| `krakenLoop` | 크라켄 숨소리 루프 (3라운드) | | | |
| `swingHorizontal` | 가로베기(J) — 내 검 | 0.13초 | `swish-5` | Swishes |
| `swingVertical` | 세로베기(K) — 내 검 | 0.20초 | `swish-9` (가장 무겁게) | Swishes |
| `swingThrust` | 찌르기(L) — 내 검 | 0.07초 | `swish-13` (가장 날카롭게) | Swishes |
| `monsterHit` | 내 칼이 몬스터에 맞음 | 0.14초 | `impactSoft_medium_002` | Kenney Impact |
| `comboUp` | 내 콤보가 3 · 6 · 9 … 에 닿음 | | ⛔ 일부러 비움 — 1R 에서 너무 자주 남 | |
| `monsterKill` | 몬스터 처치 (팀 합산) | 0.37초 | `squishpop` | Squish |
| `tentacleHit` | 내 촉수에 정타 (아직 안 잘림) | 0.14초 | `impactSoft_medium_002` — `monsterHit` 과 **같은 클립** | Kenney Impact |
| `tentacleCut` | 내 촉수가 잘림 | 0.34초 | 베는 소리 — **직접 만듦** (CC0 에 없었다) | 팀 제작 |
| `finishWindow` | 나에게 협동 마무리 창이 열림 | 0.33초 | `question_002` | Kenney |
| `comboSet` | 2R 협동 세트 완성 · **3R 콤보 피니시** | 0.49초 | `confirmation_004` | Kenney |
| `noteHitHorizontal` | 내 노트를 **가로베기**로 받음 | 0.28초 | 도 C5 523.25Hz — **직접 만듦** | 팀 제작 |
| `noteHitVertical` | 내 노트를 **세로베기**로 받음 | 0.28초 | 미 E5 659.25Hz — **직접 만듦** | 팀 제작 |
| `noteHitThrust` | 내 노트를 **찌르기**로 받음 | 0.28초 | 솔 G5 783.99Hz — **직접 만듦** | 팀 제작 |
| `noteMiss` | 내 레인 노트 놓침 | 0.19초 | `error_007` | Kenney |
| `krakenHurt` | 크라켄이 맞음 (팀 공통) | 0.14초 | `impactSoft_medium_002` — `monsterHit` 과 **같은 클립** (1R 과 타격감을 맞춤. 크기는 `krakenLevel` 로 더 크게) | Kenney Impact |
| `krakenRoar` | **3라운드가 열릴 때 한 번만** | 2.80초 | `monster_roar` — **잘라서 씀** (아래) | Deep Monster Roar |
| `playerHurt` | 내 HP 감소 | 0.46초 | `impactPunch_heavy_002` | Kenney Impact |
| `playerDown` | 내가 쓰러짐 | 0.96초 | `scream_01` | 80 creature |
| `roundChange` | 라운드 전환 | 0.53초 | `maximize_005` | Kenney |
| `countdownTick` | 카운트다운 3 · 2 · 1 | 0.22초 | 880Hz(A5) 톤 — **직접 만듦** | 팀 제작 |
| `countdownGo` | 카운트다운이 끝나고 1라운드 시작 | 0.70초 | 1760Hz(A6, 한 옥타브 위) 톤 — **직접 만듦** | 팀 제작 |

### 출처 — 6곳, 전부 CC0

| 이름 | 지은이 | 주소 | 받은 날 |
| --- | --- | --- | --- |
| Swishes Sound Pack | artisticdude | https://opengameart.org/content/swishes-sound-pack | 2026-09-22 |
| Kenney Impact Sounds | Kenney | https://kenney.nl/assets/impact-sounds | 2026-09-22 |
| Kenney Music Jingles | Kenney | https://kenney.nl/assets/music-jingles | 2026-09-22 |
| Squish Sounds Effects | EZduzziteh | https://opengameart.org/content/squish-sounds-effects | 2026-09-22 |
| 80 CC0 creature SFX | rubberduck | https://opengameart.org/content/80-cc0-creature-sfx | 2026-09-22 |
| Kenney Interface Sounds | Kenney | https://kenney.nl/assets/interface-sounds | 2026-09-22 |
| CC0 Deep Monster Roar | (OGA 게시자) | https://opengameart.org/content/cc0-deep-monster-roar | 2026-09-22 |

CC0 는 **출처 표기 의무가 없고 수정도 자유**입니다. 그래도 어디서 왔는지 남겨 둡니다.

### 출처 — 배경음악 · 파도 (Pixabay)

모두 [Pixabay Content License](https://pixabay.com/service/license-summary/) — 무료 · 상업적 이용 가능 · 수정 가능 · 출처 표기 의무 없음. 배 · 광산과 같은 라이선스입니다.

| 필드 | 곡 | 지은이 | 주소 | 받은 날 |
| --- | --- | --- | --- | --- |
| `bgmWaiting` · `bgmRound1` · `bgmRound2` | Pirate Jolly Roger Loop | Ebunny | https://pixabay.com/music/main-title-pirate-jolly-roger-loop-369969/ | 2026-09-27 |
| `bgmRound3` | Pirates Battle | Ebunny | https://pixabay.com/music/main-title-pirates-battle-361336/ | 2026-09-27 |
| `waveLoop` | Gentle Ocean Shore Waves | DRAGON-STUDIO | https://pixabay.com/sound-effects/nature-gentle-ocean-shore-waves-499665/ | 2026-09-27 |

> 배경음악 2곡은 가져오기 설정을 **스트리밍**으로 두었다(광산 배경음악과 같음). 곡 전체를 메모리에 풀지 않는다.
> Ebunny 는 배 게임 결과 곡(`Pirate Adventure Loop`)과 같은 작곡가다.

### 직접 만든 것 — 7개

CC0 팩에는 **"칼로 살을 베는 소리"** 와 **리듬게임 타격음**, **클리어 팡파르**가 없었습니다.
임팩트 · 스퀴시 · 휘두르는 바람 소리는 있어도 정작 베는 소리가 없습니다. 그래서 합성했습니다.

```text
tentacleCut    0.34초   잡음을 9000 → 400Hz 로 쓸어내려 "슥-" + 90Hz 저음으로 살 맞는 느낌
stingerClear   1.51초   도-미-솔-도(C5 E5 G5 C6) 올라간 뒤 네 음 화음으로 마무리
noteHit* ×3    0.28초   방향마다 음정 — 가로 도(C5) · 세로 미(E5) · 찌르기 솔(G5)
```

**3라운드 노트는 방향이 곧 음입니다.** 화살표가 도 · 미 · 솔 로 읽혀 두들기는 동안 박자감이 생깁니다.
**장3화음**이라 아무 순서로 쳐도 세 음이 한 화음 안에 있어 어울리고, 사이가 넓어 무엇을 쳤는지
귀로 바로 갈립니다. (처음엔 도-레-미였는데 온음 간격이라 붙어 들려서 벌렸습니다.)
치는 순간 음이 1.4배 위에서 제자리로 20ms 만에 미끄러져 들어와, 음정이 있으면서도 때리는 맛이 남습니다.

> 방향과 음의 짝은 `WarriorsAudio.NoteClipFor` 가 정합니다. 바꾸려면 거기와 이 표를 같이 고칩니다.
> 어떤 공격으로 받아야 하는 노트인지는 `WarriorsNoteSlot.Type` 으로 **이미 복제되고 있어서**
> 서버를 고치지 않고 클라이언트에서 읽습니다. (AUDIO.md 4.2)

만든 스크립트는 `art/tools/warriors_sfx_synth.py` 입니다.

```bash
python art/tools/warriors_sfx_synth.py
```

> `stingerFail` 은 원래 클리어용으로 넣었던 `jingles_STEEL07` 입니다 — 들어 보니
> 클리어보다 **게임 오버에 가깝다**는 판단이라 자리를 옮겼습니다.

### 직접 만든 것 — 카운트다운 톤 2개

레이싱 게임 카운트다운은 실제로 **순음**이라 찾기보다 만드는 것이 정확합니다. 외부 라이선스가 없습니다.

```text
countdownTick   880Hz (A5)   0.22초   3 · 2 · 1 에 세 번
countdownGo    1760Hz (A6)   0.70초   한 옥타브 위로 길게 — "출발"
```

같은 음으로 세 번 뒤 한 옥타브 위에서 길게 끄는 관계가 "삐 · 삐 · 삐 · 삐이----" 를 만듭니다.
순음만으로는 얇아서 2 · 3 배음을 각각 0.25 · 0.08 섞었습니다.

만든 스크립트는 `art/tools/warriors_countdown_tones.py` 입니다. 음 높이나 길이를 바꾸려면
그 파일의 값을 고쳐 다시 돌리면 두 파일이 덮어써집니다 (파이썬 표준 라이브러리만 씁니다).

```bash
python art/tools/warriors_countdown_tones.py
```

> ⚠ `krakenRoar` 는 원본이 **7.22초**입니다. 콤보 피니시마다 나는 소리로는 너무 길어
> **앞 2.8초만 남기고 끝 0.5초를 페이드 아웃**했습니다. 파형을 재 보니 0.2~2.8초가 포효의 본체이고
> 4.6초 뒤는 완전한 무음이었습니다. 원본이 필요하면 위 주소에서 다시 받으면 됩니다.

> ⚠ **BGM 4곡 · 스팅어 2개 · 루프 2개는 아직 비어 있습니다.**
> 루프(`waveLoop` · `krakenLoop`)는 CC0 에서 쓸 만한 것을 못 찾았습니다 — 이음새가 깨끗한 바다 루프는
> Freesound 계정이 있어야 받을 수 있고, 계정 없이 받히는 것은 FLAC 이라 Unity 가 임포트하지 못합니다.
> Pixabay 에서 BGM 고르실 때 같이 받으시면 됩니다.

## Import 설정

```text
배경음악 (bgm* · stinger*)   Load Type: Streaming
효과음 · 루프                 Load Type: Decompress On Load
루프 클립                     이음새가 끊기면 Compressed In Memory 로
```
