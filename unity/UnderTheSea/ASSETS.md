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
| Stylized Pirate Ship | Unity Asset Store | 유료 (구매) | `Assets/Stylized_Pirate_Ship/` | Lobby 해적선 | 효진 | 2026-09-08 |
| Quaternius Pirate Kit | https://quaternius.com/packs/piratekit.html | 무료 (CC0-1.0) | `Assets/QuaterniusPirateKit/` | 낚시 캐릭터·부두·소품 | 용주 | 2026-09-08 |
| Quaternius Animated Fish | https://quaternius.com/packs/animatedfish.html | 무료 (CC0-1.0) | `Assets/QuaterniusAnimatedFish/` | 낚시 물고기 모델·애니메이션 | 용주 | 2026-09-08 |
| WaterWorks | Unity Asset Store | 유료 (구매) | `Assets/WaterWorks/` | 배 협동 게임 바다 (표면 셰이더만) | 민화 | 2026-09-13 |

> ⚠ 유료 에셋 4종 합계 **약 561MB** (Synty 431MB / ithappy 85MB / ARPG Effects 19MB / Stylized Pirate Ship 26MB) 입니다.
> 이미지·모델·오디오는 `.gitattributes` 규칙에 따라 Git LFS 로 보관됩니다.

> ⚠ WaterWorks 는 **일부만** 넣었습니다. 같이 들어 있는 `Water_Volume`(수중 볼륨)
> 은 URP 17 에서 없어진 API 를 써서 **프로젝트 전체가 컴파일되지 않았습니다.**
> 표면 셰이더(`SSR_Water`)와 서브그래프만 남기고 `Scripts/` · `Demo/` ·
> `Volumetric_Water.shader` 는 지웠습니다. 업데이트할 때 다시 지워야 합니다.

> ⛔ `Shaders/WSUV_Water.shadersubgraph` 의 Position 노드를 **월드 → 오브젝트**로
> 바꿨습니다. (`"m_Space": 0`) 물 무늬가 판을 따라 흐르게 하려고 그런 것으로,
> 배 협동 게임에서 **배가 나아가 보이는 것이 여기에 달려 있습니다.**
> 업데이트하면 되돌아가니 다시 바꿔야 합니다. (`SHIPCOOP.md` 5장)

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
