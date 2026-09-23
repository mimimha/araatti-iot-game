# 광산 소리 — 출처

모두 [Pixabay Content License](https://pixabay.com/service/license-summary/) — 무료 · 상업적 이용 가능 · 수정 가능 · 출처 표기 의무 없음.
받은 날: 2026-09-23. 파일은 원본 이름을 버리고 필드 이름으로 저장한다. 필드 이름 목록은 `MINE.md` 10장 "소리" 절.

## 배경음악

| 파일 | 원본 | 작성자 | 출처 | 메모 |
| --- | --- | --- | --- | --- |
| bgmWaiting | Mystery Secret | leberch | https://pixabay.com/music/mystery-mystery-secret-255437/ | Content ID 등록 |
| bgmPlaying | Old Mine Ambience (10:03) | JoelFazhari | https://pixabay.com/sound-effects/film-special-effects-old-mine-ambience-200677/ | 곡이 아니라 동굴 울림 녹음 |

> Content ID 가 등록된 곡은 플레이 영상을 YouTube 에 올리면 저작권 알림이 붙을 수 있다. 게임 자체에는 문제 없고,
> 이의 제기할 때는 `Licenses/` 폴더의 Pixabay 라이선스 증명서(곡 ID · 다운로드 날짜 · 라이선서 계정 기재)를 첨부한다.

본편은 곡 대신 동굴 울림을 배경음악 자리에 둔다. 처음에는 같은 작곡가의 Mystery Tension 을 본편 곡으로,
Old Mine Ambience 를 `caveLoop` 로 같이 깔았는데, 동굴 울림만 두는 편이 광산 분위기에 맞아 바꿨다.
배경음악 자리라 설정의 음악 볼륨을 따르고, 대기 곡과 카운트다운 "3" 에서 1.5초 교차한다.

`bgmPlaying` 은 10분짜리라 Load Type 을 `Streaming` 으로 둔다. 통째로 풀면 메모리를 100MB 가까이 쓴다.

## 스팅어 · 효과음

| 파일 | 원본 | 작성자 | 출처 |
| --- | --- | --- | --- |
| stingerClear | Great Success | freesound_gamestudio | https://pixabay.com/sound-effects/film-special-effects-great-success-384935/ |
| stingerFail | Error Fail | freesound_gamestudio | https://pixabay.com/sound-effects/film-special-effects-error-fail-408419/ |
| countTick | Race Start Beeps | transcendedlifting | https://pixabay.com/sound-effects/film-special-effects-race-start-beeps-125125/ |
| countGo | Sacred Rune Lock 02 – Arcane Seal Activation | Coghezzi | https://pixabay.com/sound-effects/film-special-effects-sacred-rune-lock-02-arcane-seal-activation-536458/ |
| turnStart | New Notification 040 | Universfield | https://pixabay.com/sound-effects/technology-new-notification-040-493469/ |
| timeWarn | Clock Ticking | Universfield | https://pixabay.com/sound-effects/film-special-effects-clock-ticking-149907/ |
| stoneCrack | Hit Rock 01 | u_xjrmmgxfru | https://pixabay.com/sound-effects/film-special-effects-hit-rock-01-266301/ |
| stoneBreak | Hit Rock 03 | u_xjrmmgxfru | https://pixabay.com/sound-effects/film-special-effects-hit-rock-03-266305/ |
| restorePlace | UI Sound 01 | juniorsoundays | https://pixabay.com/sound-effects/film-special-effects-ui-sound-01-527815/ |
| hintOpen | UI Sound 70 | juniorsoundays | https://pixabay.com/sound-effects/film-special-effects-ui-sound-70-527837/ |
| swingMiss | Item swing SFX 2 | OxidVideos | https://pixabay.com/sound-effects/film-special-effects-item-swing-sfx-2-409076/ |

`countGo` 는 처음에 힌트 소리로 받은 룬 봉인 소리다. 도안이 뜨는 순간이 더 어울려 자리를 옮겼다.

`caveLoop` 는 비워 두었다. 동굴 울림이 `bgmPlaying` 으로 옮겨 갔다.
