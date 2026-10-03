using System;
using System.Collections.Generic;
using UnityEngine;
using UnderTheSea.Account;

namespace UnderTheSea.Character
{
    /// <summary>
    /// 캐릭터 생성 화면의 "저장 / 복원" 담당 조각.
    ///
    /// CharacterCustomizationController 본체(UI · 파츠 적용)는 건드리지 않고
    /// 아래 두 곳에서만 불려 온다.
    ///
    ///     Awake()                  →  TryApplySavedAppearance()   저장된 외형으로 시작
    ///     CompleteCustomization()  →  SubmitCharacter()           서비스에 만들고 저장
    ///
    /// 파츠를 실제로 입히는 일은 하지 않는다. 기존 ApplyPart / ApplySkinColor 를 그대로 부른다.
    /// 뼈 매핑과 피부 재질 처리를 두 벌로 만들지 않기 위함이다.
    ///
    /// 문서: docs/prd/auth-character-roadmap.md (PRD 01 저장 · 복원 / PRD 02 서비스 연동)
    /// </summary>
    public sealed partial class CharacterCustomizationController
    {
        /// <summary>
        /// 저장하고 복원하는 카테고리와 그 순서.
        ///
        /// ⚠ 순서가 중요하다. ApplyCuteDefaultCharacter() 와 같게 맞춰 두었다.
        ///    신발이 하의보다 먼저여야 일체형 부츠(Costume_14_03)가 신발을 가릴 수 있다.
        ///    BodyColor 는 프리팹이 아니라 색이므로 여기 없다. bodyColorHex 로 따로 저장한다.
        /// </summary>
        private static readonly Category[] PersistedCategories =
        {
            Category.Face,
            Category.Hair,
            Category.Shoes,
            Category.Top,
            Category.Bottom,
            Category.Accessory
        };

        /// <summary>카테고리별 "프리팹 이름 → 프리팹" 색인. 처음 찾을 때 한 번만 만든다.</summary>
        private Dictionary<Category, Dictionary<string, GameObject>> prefabsByName;

        // ------------------------------------------------------------
        // 생성 완료 — 서비스에 만들고, 성공했을 때만 저장한다
        // ------------------------------------------------------------

        /// <summary>결과를 기다리는 동안 들고 있는 외형. 성공하면 이대로 저장한다.</summary>
        private CharacterAppearanceSnapshot pendingSnapshot;

        /// <summary>지금 생성 요청을 보내고 결과를 기다리는 중인지. 중복 클릭을 막는다.</summary>
        private bool isSubmitting;

        /// <summary>
        /// [생성 완료] 를 눌러 이름 검증을 통과한 뒤에 불린다.
        ///
        /// 서비스에 캐릭터를 만들고 **성공했을 때만** 로컬 저장과 Completed 알림을 한다.
        /// 실패하면 씬을 넘기지 않고 이유를 보여준 뒤 다시 누를 수 있게 한다.
        /// </summary>
        private void SubmitCharacter(string nickname)
        {
            if (isSubmitting)
            {
                return;
            }

            // 지금 화면의 상태를 이 시점에 한 번만 읽는다.
            // 보낸 내용과 저장한 내용이 어긋나지 않게 하기 위함이다.
            pendingSnapshot = BuildAppearanceSnapshot(nickname);

            if (!CanCreateThroughService())
            {
                // 로그인한 계정이 없다. 이 씬만 단독 실행한 경우다.
                // 예전(PRD 01)처럼 이 PC 에만 저장하고 넘어간다.
                // ⚠ 여기서 막으면 커마 UI 를 단독으로 확인할 수 없게 된다.
                Debug.LogWarning(
                    "[CharacterCustomization] 로그인한 계정이 없어 이 PC 에만 저장합니다. " +
                    "서버에 저장하려면 Boot 씬부터 실행해 로그인해 주세요.", this);
                CompleteWithLocalSaveOnly();
                return;
            }

            isSubmitting = true;
            SetCompleteButtonInteractable(false);
            ShowNicknameGuide(false, "캐릭터를 만드는 중...");

            AccountServiceLocator.Characters.OnCreateResult += HandleCreateResult;
            AccountServiceLocator.Characters.CreateCharacter(
                CharacterAppearanceMapping.ToCreateRequest(pendingSnapshot));
        }

        /// <summary>
        /// 서비스에 캐릭터를 만들 수 있는 상태인지.
        ///
        /// ⚠ 서비스 객체가 있는 것만으로는 부족하다.
        ///    AccountServiceBootstrap 이 **어떤 씬을 실행하든** 시작 전에 Fake 서비스를
        ///    만들어 두기 때문에, 이 씬만 단독 실행해도 IsReady 는 true 다.
        ///    그래서 **로그인한 계정이 있는지**까지 봐야 한다.
        ///    이것을 보지 않으면 단독 실행에서 "로그인이 필요합니다" 로 생성이 막혀
        ///    커마 UI 를 확인할 수 없게 된다.
        /// </summary>
        private static bool CanCreateThroughService()
        {
            if (!AccountServiceLocator.IsReady)
            {
                return false;
            }

            IAuthService auth = AccountServiceLocator.Auth;
            return auth != null && auth.IsAuthenticated && auth.CurrentUser != null;
        }

        private void HandleCreateResult(bool success, CharacterDto created, string failureReason)
        {
            // 먼저 구독을 뗀다. 결과가 두 번 들어와도 두 번 처리되지 않는다.
            if (AccountServiceLocator.Characters != null)
            {
                AccountServiceLocator.Characters.OnCreateResult -= HandleCreateResult;
            }

            isSubmitting = false;

            if (!success)
            {
                SetCompleteButtonInteractable(true);
                ShowNicknameGuide(true, string.IsNullOrWhiteSpace(failureReason)
                    ? "캐릭터를 만들지 못했습니다. 다시 시도해 주세요."
                    : failureReason);

                if (nicknameInput != null)
                {
                    nicknameInput.ActivateInputField();
                }

                Debug.LogWarning($"[CharacterCustomization] 캐릭터 생성 실패: {failureReason}", this);
                return;
            }

            // 서버가 돌려준 값이 원본이다. 서버가 닉네임 공백을 다듬거나 피부색 표기를
            // 바꿨을 수 있으므로, 화면이 만든 pendingSnapshot 이 아니라 이쪽을 쓴다.
            //
            // ⚠ 로컬 캐시는 여기서 저장하지 않는다. 캐릭터 서비스가 이미 서버 응답으로
            //    갱신했다. (CharacterSessionCache.CacheAsCurrent)
            //    여기서 또 저장하면 화면이 만든 값으로 덮어써 버린다.
            string savedNickname = created != null && !string.IsNullOrWhiteSpace(created.nickname)
                ? created.nickname
                : pendingSnapshot.nickname;

            Debug.Log(
                $"[CharacterCustomization] 캐릭터를 만들었습니다. " +
                $"id={(created != null ? created.id : 0)}, 이름 \"{savedNickname}\"", this);

            // 막 만든 캐릭터에게만 로비 튜토리얼을 띄운다.
            //
            // ⚠ **여기서 표시를 세우는 것이 중요하다.** 튜토리얼 쪽에서 "본 적 없으면 띄운다"
            //    로 하면, 이 기능이 생기기 전에 만들어진 캐릭터나 남의 컴퓨터에서 처음
            //    로그인한 캐릭터까지 전부 튜토리얼을 보게 된다.
            if (created != null)
            {
                LobbyTutorial.MarkPending(created.id);

                // 오프닝 영상도 같은 까닭으로 여기서만 세운다. 로비에 처음 들어가면 튜토리얼보다 먼저 나온다.
                UnderTheSea.Lobby.OpeningVideo.MarkPending(created.id);
            }

            FinishCreation(savedNickname);
        }

        /// <summary>
        /// 이 PC 에만 저장하고 끝낸다. **로그인한 계정이 없을 때만** 부른다.
        ///
        /// CharacterCreate 씬을 단독 실행해 커마 UI 만 확인하는 경우다. (PRD 01 경로)
        /// 정상 흐름에서는 서버가 저장하고, 로컬 캐시는 캐릭터 서비스가 갱신한다.
        /// </summary>
        private void CompleteWithLocalSaveOnly()
        {
            // ⚠ 키 이름을 바꾸지 않는다. SceneFlow.HasCharacter 가 이 값을 본다.
            PlayerPrefs.SetString(SceneFlow.NicknameKey, pendingSnapshot.nickname);
            PlayerPrefs.Save();

            CharacterAppearanceStore.Save(pendingSnapshot);

            Debug.Log(
                $"[CharacterCustomization] 이 PC 에만 외형을 저장했습니다. " +
                $"파츠 {pendingSnapshot.parts.Length}개, 피부색 {pendingSnapshot.bodyColorHex}", this);

            FinishCreation(pendingSnapshot.nickname);
        }

        /// <summary>
        /// 화면을 마무리하고 "끝났다" 고 알린다. **저장은 하지 않는다.**
        ///
        /// 씬 전환은 이 알림을 듣는 쪽(CharacterCreateFlow)이 한다.
        /// (GAME_STRUCTURE.md 3장 — 씬 전환 코드는 이 파일에 넣지 않는다)
        /// </summary>
        private void FinishCreation(string nickname)
        {
            if (sectionTitle != null)
            {
                sectionTitle.text = nickname + " 캐릭터 설정 완료!";
            }

            Completed?.Invoke(nickname);
        }

        /// <summary>결과를 기다리는 중에 화면이 사라지면 구독을 남기지 않는다.</summary>
        private void OnDisable()
        {
            if (!isSubmitting)
            {
                return;
            }

            if (AccountServiceLocator.Characters != null)
            {
                AccountServiceLocator.Characters.OnCreateResult -= HandleCreateResult;
            }

            isSubmitting = false;
        }

        private void SetCompleteButtonInteractable(bool value)
        {
            if (completeButton != null)
            {
                completeButton.interactable = value;
            }
        }

        /// <summary>지금 착용 중인 것들을 모아 스냅샷을 만든다.</summary>
        private CharacterAppearanceSnapshot BuildAppearanceSnapshot(string nickname)
        {
            List<CharacterPartSnapshot> parts = new List<CharacterPartSnapshot>();

            foreach (Category category in PersistedCategories)
                CollectEquippedParts(category, parts);

            return new CharacterAppearanceSnapshot
            {
                nickname = nickname,
                bodyColorHex = "#" + ColorUtility.ToHtmlStringRGB(currentSkinColor),
                parts = parts.ToArray()
            };
        }

        /// <summary>
        /// 한 카테고리에서 지금 착용 중인 파츠를 모아 담는다.
        ///
        /// 착용 상태의 기준은 카탈로그(equippedParts)다. selectedOptions 를 쓰지 않는 이유는,
        /// 서로 가리는 파츠를 고르면 가려진 쪽이 지워지는데도 selectedOptions 에는
        /// 지워진 파츠의 인덱스가 그대로 남기 때문이다. 그것을 저장하면 다시 열었을 때
        /// 화면과 다른 조합이 나온다.
        ///
        /// 한 카테고리에 둘 이상이 함께 착용될 수 있다. (예: 모자와 안경은 서로를 가리지 않는다)
        /// 그래서 partPrefabs 순서대로 훑어 전부 담는다. Dictionary 순회 순서에 의존하지 않으므로
        /// 같은 캐릭터는 항상 같은 JSON 이 된다.
        /// </summary>
        private void CollectEquippedParts(Category category, List<CharacterPartSnapshot> parts)
        {
            PartCollection collection = GetCollection(category);
            if (collection?.partPrefabs == null)
                return;

            string slot = category.ToString();
            int found = 0;

            foreach (GameObject prefab in collection.partPrefabs)
            {
                if (prefab == null || !IsCatalogPartEquipped(prefab))
                    continue;

                // 모자 + 안경처럼 한 카테고리에 둘 이상이면 카테고리 이름으로는 서버가 받지 않는다.
                // 그래서 자리 이름으로 나눠 저장한다. (CharacterPartCatalog.SnapshotSlotOf)
                string partSlot = catalog != null && catalog.TryFind(prefab, out CharacterPartCatalog.Entry entry)
                    ? CharacterPartCatalog.SnapshotSlotOf(entry)
                    : slot;
                parts.Add(new CharacterPartSnapshot(partSlot, prefab.name));
                found++;
            }

            if (found > 0)
                return;

            // 카탈로그에 등록되지 않은 파츠는 targetRenderer 경로로 적용된다.
            // 이때는 activePart 가 있고 selectedOptions 가 유일한 단서다.
            if (collection.activePart == null)
                return;

            if (!selectedOptions.TryGetValue(category, out int index))
                return;

            if (index < 0 || index >= collection.partPrefabs.Length)
                return;

            GameObject selected = collection.partPrefabs[index];
            if (selected != null)
                parts.Add(new CharacterPartSnapshot(slot, selected.name));
        }

        // ------------------------------------------------------------
        // 복원
        // ------------------------------------------------------------

        /// <summary>
        /// 저장된 외형으로 캐릭터를 세운다. 세우지 못했으면 false — 부르는 쪽이 기본 캐릭터로 시작한다.
        ///
        /// "캐릭터가 있는지" 는 SceneFlow.HasCharacter (PlayerPrefs "PlayerNickname") 로 판단한다.
        /// 저장 근거를 한 곳으로 모아두면 Tools > 아라아띠 > 캐릭터 이름 지우기 를 눌렀을 때
        /// 이름과 외형이 함께 초기화된다. 이름은 없는데 외형만 남아 되살아나는 일이 없다.
        /// 서버 연동 단계에서는 이 판단만 API 결과로 갈아끼운다.
        /// </summary>
        /// <summary>
        /// 저장된 외형으로 캐릭터를 세운다. 세우지 못했으면 false.
        ///
        /// 읽는 값은 **서버 응답의 로컬 캐시**다. (CharacterSessionCache 가 채운다)
        /// 로그인 직후 서버 값으로 갱신되므로, 다른 PC 에서 로그인해도 같은 외형이 나온다.
        /// 로그인 없이 이 씬만 단독 실행한 경우에는 예전에 로컬로 저장한 값이 쓰인다.
        /// </summary>
        private bool TryApplySavedAppearance()
        {
            if (!SceneFlow.HasCharacter)
                return false;

            if (!CharacterAppearanceStore.TryLoad(out CharacterAppearanceSnapshot snapshot))
                return false;

            int applied = 0;

            // 저장 순서가 아니라 PersistedCategories 순서로 입힌다.
            // 서로 가리는 파츠의 결과가 항상 같아야 한다.
            foreach (Category category in PersistedCategories)
            {
                string slot = category.ToString();

                foreach (CharacterPartSnapshot part in snapshot.parts)
                {
                    // 모자 · 안경 · 얼굴장식은 자리 이름으로 저장돼 있다. (CharacterPartCatalog.SnapshotSlotOf)
                    if (!string.Equals(CharacterPartCatalog.CategoryOfSnapshotSlot(part.slot), slot, StringComparison.Ordinal))
                        continue;

                    if (string.IsNullOrEmpty(part.prefabName))
                        continue;

                    if (!TryFindPrefab(category, part.prefabName, out GameObject prefab))
                    {
                        // 에셋에서 사라진 파츠다. 이 칸만 기본 상태로 두고 계속 진행한다.
                        Debug.LogWarning(
                            $"[CharacterCustomization] 저장된 파츠 \"{part.prefabName}\" 을 " +
                            $"{slot} 에서 찾지 못했습니다. 이 칸은 기본값을 유지합니다.", this);
                        continue;
                    }

                    ApplyPart(GetCollection(category), prefab);
                    applied++;
                }
            }

            if (applied == 0 && snapshot.parts.Length > 0)
            {
                // 저장값은 있는데 하나도 못 살렸다. 반쯤 벗은 캐릭터를 보여주는 대신 기본 캐릭터로 간다.
                Debug.LogWarning(
                    "[CharacterCustomization] 저장된 외형을 하나도 복원하지 못했습니다. " +
                    "기본 캐릭터로 시작합니다.", this);
                return false;
            }

            // 피부색은 파츠 뒤에 적용한다. ApplySkinColor 가 이미 입혀진 파츠까지 다시 칠해준다.
            ApplySavedBodyColor(snapshot.bodyColorHex);
            PrefillSavedNickname();

            Debug.Log($"[CharacterCustomization] 저장된 외형을 복원했습니다. 파츠 {applied}개", this);
            return true;
        }

        /// <summary>
        /// 저장된 피부색을 팔레트에서 찾아 적용한다.
        ///
        /// 색을 직접 칠하지 않고 팔레트 인덱스를 찾아 기존 ApplySkinColor 를 부른다.
        /// 피부 텍스처를 다시 칠하는 처리(CreateRecoloredSkinTexture)를 그대로 태우기 위함이다.
        /// </summary>
        private void ApplySavedBodyColor(string bodyColorHex)
        {
            if (string.IsNullOrEmpty(bodyColorHex) || skinColors == null || skinColors.Length == 0)
                return;

            for (int i = 0; i < skinColors.Length; i++)
            {
                string paletteHex = "#" + ColorUtility.ToHtmlStringRGB(skinColors[i]);
                if (!string.Equals(paletteHex, bodyColorHex, StringComparison.OrdinalIgnoreCase))
                    continue;

                ApplySkinColor(i);
                return;
            }

            Debug.LogWarning(
                $"[CharacterCustomization] 저장된 피부색 {bodyColorHex} 이 팔레트에 없습니다. " +
                "기본 피부색을 유지합니다.", this);
        }

        /// <summary>
        /// 저장된 이름을 입력칸에 미리 채운다.
        ///
        /// SetTextWithoutNotify 를 쓰는 이유: onValueChanged 를 건드리지 않아
        /// 화면을 열자마자 안내 문구가 뜨는 것을 막는다.
        /// </summary>
        private void PrefillSavedNickname()
        {
            if (nicknameInput == null)
                return;

            string saved = SceneFlow.Nickname;
            if (string.IsNullOrWhiteSpace(saved))
                return;

            nicknameInput.SetTextWithoutNotify(saved);
            SetNicknamePlaceholderVisible(false);
        }

        // ------------------------------------------------------------
        // 찾기
        // ------------------------------------------------------------

        private PartCollection GetCollection(Category category)
        {
            return category switch
            {
                Category.Face => face,
                Category.Hair => hair,
                Category.Top => top,
                Category.Bottom => bottom,
                Category.Shoes => shoes,
                Category.Accessory => accessory,
                _ => null
            };
        }

        private bool TryFindPrefab(Category category, string prefabName, out GameObject prefab)
        {
            if (prefabsByName == null)
                prefabsByName = new Dictionary<Category, Dictionary<string, GameObject>>();

            if (!prefabsByName.TryGetValue(category, out Dictionary<string, GameObject> index))
            {
                index = BuildPrefabNameIndex(category);
                prefabsByName[category] = index;
            }

            return index.TryGetValue(prefabName, out prefab) && prefab != null;
        }

        private Dictionary<string, GameObject> BuildPrefabNameIndex(Category category)
        {
            Dictionary<string, GameObject> index = new Dictionary<string, GameObject>(StringComparer.Ordinal);

            PartCollection collection = GetCollection(category);
            if (collection?.partPrefabs == null)
                return index;

            foreach (GameObject prefab in collection.partPrefabs)
            {
                if (prefab == null)
                    continue;

                if (index.ContainsKey(prefab.name))
                {
                    // 이름으로 저장하므로 같은 이름이 둘 있으면 어느 쪽인지 가릴 수 없다.
                    Debug.LogWarning(
                        $"[CharacterCustomization] {category} 에 이름이 같은 프리팹이 둘 이상 있습니다: " +
                        $"\"{prefab.name}\". 앞의 것을 사용합니다.", this);
                    continue;
                }

                index.Add(prefab.name, prefab);
            }

            return index;
        }
    }
}
