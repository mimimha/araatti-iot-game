# UnderTheSea Git 컨벤션

본 문서는 팀 전체의 Git 작업 규칙을 정의합니다.

저장소 최상위에 위치하며 `unity/`, `iot/`, `server/` 등 **모든 디렉토리에 동일하게 적용**됩니다.

관련 문서:

- `unity/UnderTheSea/CONVENTION.md` — Unity 파일과 폴더를 어디에 두는가
- `unity/UnderTheSea/GAME_STRUCTURE.md` — 각자 만든 것을 어떻게 이어붙이는가

---

## 1. 브랜치 전략

### Main 브랜치

| 브랜치 | 용도 | 직접 push |
| --- | --- | --- |
| `master` | 제품으로 출시되는 브랜치 (제출, 시연, 발표) | **금지** |
| `develop` | 다음 출시 버전을 개발하는 브랜치 (개발 통합) | **금지** |

### Sub 브랜치

| 브랜치 | 용도 | 어디서 분기 | 어디로 병합 |
| --- | --- | --- | --- |
| `feature` | 기능을 개발하는 브랜치 | `develop` | `develop` |
| `fix` | 개발 중 발견한 버그를 수정하는 브랜치 | `develop` | `develop` |
| `refactor` | 기능 변화 없이 구조를 개선하는 브랜치 | `develop` | `develop` |
| `chore` | 설정, 패키지, 문서 등 기타 작업 브랜치 | `develop` | `develop` |
| `release` | 이번 출시 버전을 준비하는 브랜치 | `develop` | `develop`, `master` |
| `hotfix` | 출시 버전 버그를 수정하는 브랜치 | `master` | `develop`, `master` |

### `fix`와 `hotfix`의 차이

헷갈리기 쉬우니 구분해서 사용합니다.

| | `fix` | `hotfix` |
| --- | --- | --- |
| 언제 | **개발 중** 발견한 버그 | **`master`에 나간 버전**의 버그 |
| 분기 | `develop`에서 | `master`에서 |
| 급한가 | 보통 | 급함 (시연 직전 등) |

대부분의 버그 수정은 `fix`입니다. `hotfix`는 발표나 제출 직전에 터진 문제에만 사용합니다.

### `release` 브랜치

7주 프로젝트에서는 거의 사용하지 않습니다. 보통 `develop`을 바로 `master`로 병합합니다.

발표를 앞두고 `develop`은 계속 개발하면서 발표용 버전만 따로 다듬어야 할 때만 사용합니다.

### 브랜치 흐름

```text
master        제출 / 시연
   ▲
   │ Merge Request  (develop → master 만 허용)
   │
develop       개발 통합
   ▲
   │ Merge Request
   │
feature/xxx   실제 작업
```

### 핵심 규칙

**1) `master`에는 직접 push 하지 않습니다.**

`master`로 들어갈 수 있는 경로는 **`develop`(또는 `release`, `hotfix`)에서 올린 Merge Request** 뿐입니다.

작업 브랜치에서 `master`로 바로 MR을 올리지 않습니다.

```text
❌  feature/player-movement  →  master
✅  feature/player-movement  →  develop  →  master
```

**2) `develop`에도 직접 push 하지 않습니다.**

반드시 작업 브랜치를 만들고 Merge Request를 통해 반영합니다.

**3) `master` 병합은 팀 합의 후에만 합니다.**

`develop`이 안정적이라고 팀이 판단했을 때, 주요 시점(중간 발표, 최종 제출 등)에만 병합합니다.

---

## 2. 브랜치 이름 규칙

### 형식

```text
<타입>/<작업-내용>
```

### 작성 규칙

- **영어 소문자**만 사용합니다.
- 단어는 **하이픈(`-`)**으로 구분합니다. 띄어쓰기와 밑줄(`_`)은 쓰지 않습니다.
- **한글은 사용하지 않습니다.** Windows와 Git 사이에 글자 깨짐 문제가 생길 수 있습니다.
- 이름만 봐도 무슨 작업인지 알 수 있게 짓습니다.

### 예시

```text
✅  feature/main-world-sync
✅  feature/title-ui
✅  feature/fishing-minigame
✅  fix/spawn-position
✅  refactor/input-provider
✅  chore/setup-netcode
✅  hotfix/build-crash
✅  release/v1.0

❌  feature/작업              한글
❌  feature/MainWorldSync     대문자
❌  feature/main_world_sync   밑줄
❌  minhwa                    타입 없음
❌  test                      무슨 작업인지 알 수 없음
```

### 이슈 번호를 함께 쓰는 경우

GitLab 이슈를 사용한다면 번호를 붙여도 됩니다. 팀에서 하나로 통일합니다.

```text
feature/12-main-world-sync
```

---

## 3. 커밋 메시지

### 형식

```text
[type] subject
```

### type

| type | 용도 |
| --- | --- |
| `feat` | 새로운 기능 추가 |
| `fix` | 버그 수정 |
| `refactor` | 기능 변화 없이 구조 개선 |
| `style` | 들여쓰기, 포맷팅 등 코드 스타일 수정 |
| `docs` | README, API 명세 등 문서 수정 |
| `test` | 테스트 코드 추가/수정 |
| `chore` | 설정 변경, 패키지 관리, 파일 정리 등 기타 작업 |

### subject

- **50자 이하**로 간결하게
- 변경 내용을 **명사형**으로
- **마침표 없이**

### 예시

```text
[feat] 로그인 기능 추가
[fix] 알림 중복 전송 오류 수정
[refactor] 인증 로직 분리
[docs] 실행 방법 추가
```

### 이 프로젝트 예시

```text
[feat] MainWorld 캐릭터 이동 동기화 구현
[feat] Title 화면 닉네임 입력 기능 추가
[fix] 캐릭터가 바닥을 통과하는 문제 수정
[refactor] 입력 처리와 이동 로직 분리
[chore] 해양 환경 에셋 추가
[docs] 게임 구조 규격 문서 추가
```

### 좋은 예 / 나쁜 예

| ❌ | ✅ |
| --- | --- |
| `수정` | `[fix] 캐릭터가 바닥을 통과하는 문제 수정` |
| `ㅇㅇ` | `[feat] 방 만들기 버튼 UI 구현` |
| `작업중` | `[feat] Lobby 접속 처리 추가` |
| `[feat] 이것저것 많이 함` | `[feat] MainWorld 캐릭터 이동 동기화 구현` |
| `Update Player.cs` | `[refactor] 입력 처리와 이동 로직 분리` |
| `에셋 추가.` | `[chore] 해양 환경 에셋 추가` |

### 추가 규칙

- 하나의 커밋에는 **하나의 작업**만 담습니다.
- 작업이 길어지면 **중간중간 커밋**합니다. 하루치를 한 번에 커밋하지 않습니다.

---

## 4. Merge Request

### 규칙

| 항목 | 내용 |
| --- | --- |
| 대상 브랜치 | 반드시 `develop` (`master` 아님) |
| 제목 | 커밋 메시지와 같은 형식. 예: `[feat] MainWorld 캐릭터 이동 동기화` |
| 리뷰어 | 최소 **1명** 지정 |
| 승인 | 최소 1명의 승인 후 병합 |
| 옵션 | `Delete source branch` 체크 |

리뷰 없이 혼자 병합하지 않습니다.

### 템플릿

MR을 만들면 아래 양식이 자동으로 채워집니다. (`.gitlab/merge_request_templates/`)

```markdown
## 📌 요약 (Summary)
어떤 변경 사항을 적용하는 MR인가요?

## 🛠 작업 내용 (Changes)
- [ ] 기능 개발 (Feature)
    - [ ]  Client (Unity)
    - [ ]  IoT
    - [ ]  Server
- [ ] 버그 수정 (Bug fix)
- [ ] 리팩토링 (Refactoring)
- [ ] 문서 수정 (Documentation)

## 🎯 관련 이슈 (Related Issues)
Close #

## 📸 스크린샷 (선택)
```

### 올리기 전 확인

- [ ] Unity가 정상 실행되고 Console에 새 Error가 없는가
- [ ] `git status`로 의도하지 않은 파일이 포함되지 않았는가
- [ ] `Library/`, `Temp/`, `Logs/`가 포함되지 않았는가
- [ ] 다른 팀원의 Develop Scene을 건드리지 않았는가
- [ ] `.meta` 파일이 빠지지 않았는가

---

## 5. 이슈

이슈를 만들면 아래 양식이 자동으로 채워집니다. (`.gitlab/issue_templates/`)

```markdown
## 목적

## 작업 내용

- [ ]

## 참고사항
```

이슈 번호는 MR의 `Close #` 에 적어 연결합니다. 병합되면 이슈가 자동으로 닫힙니다.

---

## 6. 작업 흐름

### 작업 시작

```bash
git switch develop
```

```bash
git pull origin develop
```

```bash
git switch -c feature/작업명
```

**항상 최신 `develop`에서 브랜치를 만듭니다.** 오래된 상태에서 시작하면 나중에 충돌이 커집니다.

### 작업 중

```bash
git status
```

```bash
git add .
```

```bash
git commit -m "[feat] 작업 내용"
```

### 작업 완료

```bash
git push -u origin feature/작업명
```

그다음 GitLab에서 Merge Request를 만듭니다.

### 작업이 며칠 이어질 때

`develop`이 계속 바뀌므로 주기적으로 최신 내용을 받아옵니다.

```bash
git switch develop && git pull origin develop && git switch - && git merge develop
```

---

## 7. 금지 사항

| 금지 | 이유 |
| --- | --- |
| `master`에 직접 push | 시연용 브랜치가 깨질 수 있음 |
| `develop`에 직접 push | 리뷰 없이 코드가 들어감 |
| 작업 브랜치에서 `master`로 바로 MR | 반드시 `develop`을 거쳐야 함 |
| 공유 브랜치에 `git push --force` | **다른 사람 작업이 사라짐** |
| 남의 브랜치를 임의로 삭제 | 작업 유실 |
| 같은 Scene / Prefab을 동시에 수정 | 병합이 거의 불가능함 |
| `Library/` 등 무시 대상 커밋 | 저장소 용량 폭증 |

---

## 8. 자주 쓰는 명령어

| 하고 싶은 것 | 명령어 |
| --- | --- |
| 현재 상태 확인 | `git status` |
| 브랜치 목록 보기 | `git branch` |
| 브랜치 이동 | `git switch 브랜치명` |
| 새 브랜치 만들며 이동 | `git switch -c 브랜치명` |
| 최신 내용 받기 | `git pull origin develop` |
| 변경 내용 확인 | `git diff` |
| 커밋 기록 보기 | `git log --oneline -10` |
| 특정 파일 변경 취소 | `git restore 파일명` |
| 커밋 안 한 변경 전부 취소 | `git restore .` |
| 방금 커밋 메시지 고치기 | `git commit --amend -m "새 메시지"` |

> `git commit --amend`는 **아직 push 하지 않은 커밋**에만 사용합니다.

---

## 9. 충돌이 났을 때

### 코드 파일(`.cs`) 충돌

직접 열어서 고칠 수 있습니다. `<<<<<<<`, `=======`, `>>>>>>>` 표시를 찾아 필요한 내용만 남기고 지웁니다.

### Scene / Prefab 충돌

**직접 고치려 하지 마세요.** Unity의 Scene과 Prefab 파일은 사람이 읽고 합치기 매우 어렵습니다.

작업한 사람끼리 이야기해서 **한쪽을 선택**하고, 다른 쪽은 Unity에서 다시 작업하는 편이 빠릅니다.

```bash
git checkout --theirs 파일경로   # 상대방 것을 선택
```

```bash
git checkout --ours 파일경로     # 내 것을 선택
```

### 애초에 충돌을 막는 법

- 같은 Scene을 동시에 수정하지 않습니다. (담당자를 정합니다)
- 기능은 Scene이 아니라 **Script와 Prefab으로** 만듭니다.
- 작업 브랜치를 오래 방치하지 않습니다. **짧게 작업하고 자주 병합**합니다.

---

## 10. GitLab 설정 (한 번만, Maintainer)

문서에 적어두는 것만으로는 직접 push를 막을 수 없습니다. **GitLab에서 설정해야 실제로 차단됩니다.**

`Settings > Repository > Protected branches`에서:

| 브랜치 | Allowed to push | Allowed to merge |
| --- | --- | --- |
| `master` | **No one** | Maintainers |
| `develop` | **No one** | Developers + Maintainers |

---

# 핵심 규칙 요약

1. `master`와 `develop`에는 **직접 push 하지 않습니다.**
2. `master`로 가는 경로는 **`develop`에서 올린 MR**입니다.
3. 브랜치는 `feature` `fix` `refactor` `chore` `release` `hotfix`, 이름은 **영어 소문자 + 하이픈**.
4. 커밋 메시지는 `[type] subject`, **50자 이하 · 명사형 · 마침표 없이**.
5. type은 `feat` `fix` `refactor` `style` `docs` `test` `chore` 7가지입니다.
6. 하나의 커밋에는 **하나의 작업**만 담습니다.
7. Merge Request는 **최소 1명의 승인**을 받고 병합합니다.
8. 공유 브랜치에 **force push 하지 않습니다.**
9. 같은 Scene과 Prefab을 **동시에 수정하지 않습니다.**
10. GitLab **Protected branches 설정**으로 실제 차단을 걸어둡니다.
