# 무쌍(Warriors) 소리 — 출처

이 폴더에 넣은 클립의 출처를 **한 줄씩** 적습니다. 저작권은 CC0 또는 팀이 권리를 가진 것만 씁니다.
에셋 팩 단위로는 저장소 뿌리의 `ASSETS.md` 목록에도 한 줄 추가합니다. (AUDIO.md 5장)

파일 이름은 **연출가(`WarriorsAudio`)의 필드 이름과 같아야** 합니다. 그래야
메뉴 `Tools/아라아띠/Warriors 소리 놓고 클립 채우기` 가 이름으로 자동 연결합니다.
확장자는 `.ogg` 권장, `.wav` · `.mp3` 도 됩니다.

## 넣을 자리 — 아직 비어 있음

| 파일 (= 필드 이름) | 언제 나는가 | 원본 | 출처 · 라이선스 |
| --- | --- | --- | --- |
| `bgmWaiting` | 대기 · 시작 카운트다운 | | |
| `bgmRound1` | 1라운드 해변 방어 | | |
| `bgmRound2` | 2라운드 촉수 절단 | | |
| `bgmRound3` | 3라운드 리듬 전투 (긴장) | | |
| `stingerClear` | 성공. 한 번 나고 음악은 멈춤 | | |
| `stingerFail` | 실패. 한 번 나고 음악은 멈춤 | | |
| `waveLoop` | 해변 파도 루프 (1 · 2라운드) | | |
| `krakenLoop` | 크라켄 숨소리 루프 (3라운드) | | |
| `swingHorizontal` | 가로베기(J) — 내 검 | | |
| `swingVertical` | 세로베기(K) — 내 검 | | |
| `swingThrust` | 찌르기(L) — 내 검 | | |
| `monsterHit` | 내 칼이 몬스터에 맞음 | | |
| `comboUp` | 내 콤보가 3 · 6 · 9 … 에 닿음 | | |
| `monsterKill` | 몬스터 처치 (팀 합산) | | |
| `tentacleHit` | 내 촉수에 정타 (아직 안 잘림) | | |
| `tentacleCut` | 내 촉수가 잘림 | | |
| `finishWindow` | 나에게 협동 마무리 창이 열림 | | |
| `comboSet` | 협동 세트 완성 | | |
| `noteHit` | 내 레인 노트 정타 | | |
| `noteMiss` | 내 레인 노트 놓침 | | |
| `krakenHurt` | 크라켄이 맞음 (팀 공통) | | |
| `krakenRoar` | 3라운드 시작 · 콤보 피니시 | | |
| `playerHurt` | 내 HP 감소 | | |
| `playerDown` | 내가 쓰러짐 | | |
| `roundChange` | 라운드 전환 | | |
| `countdownTick` | 카운트다운 3 · 2 · 1 | | |
| `countdownGo` | 카운트다운이 끝나고 1라운드 시작 | | |

> 클립을 넣을 때마다 그 줄의 **원본**과 **출처 · 라이선스** 칸을 채웁니다.
> 안 넣은 줄은 비워 두면 됩니다 — 그 소리만 안 납니다.

## Import 설정

```text
배경음악 (bgm* · stinger*)   Load Type: Streaming
효과음 · 루프                 Load Type: Decompress On Load
루프 클립                     이음새가 끊기면 Compressed In Memory 로
```
