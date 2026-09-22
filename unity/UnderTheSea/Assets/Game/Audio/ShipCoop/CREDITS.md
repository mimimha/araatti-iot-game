# 배 협동 소리 — 출처

## 효과음 · 루프 (전부 CC0)

| 파일 | 원본 | 출처 |
| --- | --- | --- |
| cannonFire · enemyHit | cannon_fire_1 · cannon_hit_1 | opengameart.org/content/battle-at-sea |
| dumpSplash · waterScoop | bubble_03 | opengameart.org/content/40-cc0-water-splash-slime-sfx |
| windLoop | wind woosh loop | opengameart.org/content/wind-whoosh-loop |
| hammerHit · repairDone · boxLid | bookClose · bookPlace3 · doorClose_2 | kenney.nl/assets/rpg-audio |
| seaLoop | Vistula (강/바다 물결 녹음, 짧은 판) | opengameart.org/content/sea-and-river-wave-sounds |
| seagull1 · seagull2 · seagull3 | Seagull Ambient 1 · 2 · 3 | opengameart.org/content/solo-seagull-sound-effects |
| warnChime | Short Alarm | opengameart.org/content/short-alarm |

## 효과음 (Pixabay, 무료 · Pixabay Content License)

| 파일 | 원본 | 작성자 | 출처 |
| --- | --- | --- | --- |
| ropeLoop | Rope & Leather tension 2 (0:02) | OxidVideos | https://pixabay.com/sound-effects/rope-amp-leather-tension-2-449631/ |

돛을 당기거나 풀 때 도르래 · 밧줄이 끼익 하는 소리. 짧은 클립을 `ropeRepeat`(기본 2 = 클립 길이만큼 쉬고) 간격으로 반복한다.

## 배경음악 (Pixabay, 무료 · Pixabay Content License)

셋 모두 [Pixabay Content License](https://pixabay.com/service/license-summary/) — 무료 · 상업적 이용 가능 · 수정 가능 · 출처 표기 의무 없음.
받은 날: 2026-09-21. 파일은 곡 이름을 버리고 필드 이름으로 저장한다.

| 파일 | 곡 | 작곡 | 출처 | 메모 |
| --- | --- | --- | --- | --- |
| bgmReady | Set Sail | Forgotten-Hero-Records | https://pixabay.com/music/main-title-set-sail-350596/ | AI 생성 표기 |
| bgmSailing | Pirate Tavern (Full Version!) | Magiksolo (Artem Hramushkin) | https://pixabay.com/music/main-title-pirate-tavern-full-version-167990/ | Content ID 등록 |
| bgmResult | Pirate Adventure Loop | Ebunny | https://pixabay.com/music/orchestral-pirate-adventure-loop-557984/ | Content ID 등록 |

> Content ID 가 등록된 곡은 플레이 영상을 YouTube 에 올리면 저작권 알림이 붙을 수 있다. 게임 자체에는 문제 없고,
> 이의 제기할 때는 `Licenses/` 폴더의 Pixabay 라이선스 증명서(곡 ID · 다운로드 날짜 · 라이선서 계정 기재)를 첨부한다.
> Set Sail 은 Pixabay 가 증명서를 발행하지 않아 없다 — Content ID 도 없으니 페이지 링크와 받은 날(2026-09-21)로 충분하다.

배경음악은 3단계 — 대기(`bgmReady`) · 항해(`bgmSailing`) · 결과(`bgmResult`, 성공 · 실패 공통). 곡이 바뀔 때는
`crossfadeSeconds`(기본 2.5초) 동안 교차하고, 씬에 들어올 때 페이드 인 · 나갈 때 페이드 아웃한다.
`bgmResult` 가 비어 있으면 항해 곡을 그대로 이어서 `endLevel`(기본 0.7 = 70%) 만큼만 낮춰 튼다.
긴장 단계나 성공/실패를 나눈 스팅어는 없다.

이전에 쓰던 Exploration Fantasy Free Pack(Unity Asset Store, Eugene Des) 곡은 2026-09-21 에 위 곡으로 바꿨다.

메뉴 `아라아띠/배 협동/소리 놓고 클립 채우기` 를 돌리면 파일 이름대로 연결된다. (AUDIO.md 5장)
