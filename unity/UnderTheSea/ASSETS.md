# 외부 에셋 목록

프로젝트에 사용한 외부 에셋을 기록합니다.

에셋을 Import 한 사람이 아래 표에 한 줄 추가합니다.
**에셋을 추가하는 Merge Request 에서 이 파일도 함께 수정합니다.**

기록하는 이유:

- 발표 자료에 "사용한 외부 에셋" 정리가 필요합니다
- 라이선스 조건을 나중에 확인해야 할 수 있습니다
- 저장소에 있는 폴더가 어디서 왔는지 알 수 있습니다

---

## 목록

| 에셋 이름 | 출처 | 라이선스 | 폴더 | 용도 | 추가한 사람 | 날짜 |
| --- | --- | --- | --- | --- | --- | --- |
| POLYGON Nature Biomes | Unity Asset Store | 유료 (구매) | `Assets/Synty/` | Lobby 정글 맵 | 효진 | 2026-09-07 |
| Cute Characters | Unity Asset Store | 유료 (구매) | `Assets/ithappy/` | 캐릭터 | 효진 | 2026-09-07 |
| ARPG Effects | Unity Asset Store | 유료 (구매) | `Assets/ARPG Effects/` | 포탈 이펙트 | 효진 | 2026-09-07 |
| Meshy Warriors 몬스터/보스 5종 | Meshy에서 프로젝트 담당자가 직접 생성 | 생성 계정의 이용 조건 확인 필요 | `Assets/Game/Art/MiniGames/Warriors/Models/` | Warriors의 Crab, Fish, Jellyfish, Kraken Phase 2/Final 모델 | 서연 | 2026-09-12 |
| Toony Tiny People RTS 검 리소스 | 출처 확인 필요 | 라이선스 확인 전 업로드 보류 | `Assets/ToonyTinyPeople/` | Warriors 테스트용 검 메시와 재질 | 서연 | 2026-09-12 |

> ⚠ 위 3종 합계 **약 535MB** (Synty 431MB / ithappy 85MB / ARPG Effects 19MB) 입니다.
> 이미지·모델·오디오는 `.gitattributes` 규칙에 따라 Git LFS 로 보관됩니다.

> ⚠ Warriors 업로드 전 확인: Meshy 항목은 생성에 사용한 계정의 이용 조건을 확인하고,
> Toony Tiny People 항목은 정확한 배포 페이지와 라이선스를 확인해야 합니다.
> 확인 전에는 `Assets/ToonyTinyPeople/` 전체를 Git에 추가하지 않습니다.

### 작성 예시

| 에셋 이름 | 출처 | 라이선스 | 폴더 | 용도 | 추가한 사람 | 날짜 |
| --- | --- | --- | --- | --- | --- | --- |
| Stylized Water 2 | Unity Asset Store | 유료 (구매) | `Assets/StylizedWater2/` | Lobby 바다 표현 | 효진 | 2026-09-10 |
| Low Poly Fish Pack | Unity Asset Store | 무료 | `Assets/LowPolyFish/` | Lobby 배경 물고기 | 효진 | 2026-09-12 |

---

## 규칙

- **무료 에셋도 반드시 기록합니다.** 무료라도 라이선스 조건이 있습니다.
- **상업적 사용이 제한된 라이선스**는 Import 전에 팀에 공유합니다.
- 에셋을 프로젝트에서 제거하면 이 표에서도 지웁니다.
- Import 방법은 `CONVENTION.md` 7장을 따릅니다. **본 프로젝트에서 바로 Import 하지 않습니다.**
