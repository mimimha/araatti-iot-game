using System;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnderTheSea.Character
{
    public sealed partial class CharacterCustomizationController : MonoBehaviour
    {
        private enum Category
        {
            Face,
            BodyColor,
            Hair,
            Top,
            Bottom,
            Shoes,
            Accessory
        }

        [Serializable]
        private sealed class PartCollection
        {
            public string displayName;
            public SkinnedMeshRenderer targetRenderer;
            public GameObject[] partPrefabs;
            public Sprite[] partThumbnails;

            [NonSerialized] public GameObject activePart;
        }

        /// <summary>
        /// [생성 완료] 를 눌러 이름 검증을 통과하고 저장까지 끝났을 때 알린다. 인자는 저장된 이름.
        ///
        /// 검증에 걸려서 되돌아간 경우에는 호출되지 않는다.
        /// 다음 화면으로 넘기는 것은 이 알림을 듣는 쪽(CharacterCreateFlow)이 한다.
        /// </summary>
        public event Action<string> Completed;

        [Header("Character")]
        [SerializeField] private Transform characterPreview;
        [SerializeField] private PartCollection face;
        [SerializeField] private PartCollection hair;
        [SerializeField] private PartCollection top;
        [SerializeField] private PartCollection bottom;
        [SerializeField] private PartCollection shoes;
        [SerializeField] private PartCollection accessory;
        [Tooltip("화면에 보여줄 피부색 팔레트. 실제로 칠하는 일은 Applier 가 한다.")]
        [SerializeField] private Color[] skinColors;
        [SerializeField] private int defaultSkinColorIndex = 1;

        [Header("UI")]
        [SerializeField] private Button[] categoryButtons;
        [SerializeField] private Button[] optionButtons;
        [SerializeField] private Image[] optionImages;
        [SerializeField] private TMP_Text sectionTitle;
        [SerializeField] private TMP_Text pageText;
        [SerializeField] private Button previousPageButton;
        [SerializeField] private Button nextPageButton;
        [SerializeField] private Button rotateLeftButton;
        [SerializeField] private Button rotateRightButton;
        [SerializeField] private Button completeButton;
        [SerializeField] private TMP_InputField nicknameInput;
        [SerializeField] private TMP_Text nicknameGuideText;

        [Header("Paging")]
        [SerializeField, Min(1)] private int itemsPerPage = 8;
        [SerializeField] private float rotationStep = 30f;

        private Category activeCategory;
        private int currentPage;
        private Color currentSkinColor = Color.white;

        private void Awake()
        {
            EnsureReusableUiLayout();

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                int categoryIndex = i;
                categoryButtons[i].onClick.AddListener(() => OpenCategory((Category)categoryIndex));
            }

            for (int i = 0; i < optionButtons.Length; i++)
            {
                int optionIndex = i;
                optionButtons[i].onClick.AddListener(() => SelectVisibleOption(optionIndex));
            }

            previousPageButton.onClick.AddListener(PreviousPage);
            nextPageButton.onClick.AddListener(NextPage);
            completeButton.onClick.AddListener(CompleteCustomization);
            nicknameInput.onValueChanged.AddListener(_ => ShowNicknameGuide(false));
            nicknameInput.onSelect.AddListener(_ => SetNicknamePlaceholderVisible(false));
            nicknameInput.onDeselect.AddListener(_ => SetNicknamePlaceholderVisible(string.IsNullOrEmpty(nicknameInput.text)));

            if (skinColors.Length > 0)
                ApplySkinColor(Mathf.Clamp(defaultSkinColorIndex, 0, skinColors.Length - 1));

            // 저장된 외형이 있으면 그것으로 시작한다. (CharacterCustomizationPersistence.cs)
            if (!TryApplySavedAppearance())
                ApplyCuteDefaultCharacter();

            OpenCategory(Category.Face);
        }

        private void ApplyCuteDefaultCharacter()
        {
            // Start from a friendly, game-ready character instead of the source prefab's
            // angry red-face test combination.
            ApplyNamedDefaultPart(face, "Male_Emotion_Usual_01");
            ApplyDefaultPart(hair, 12);     // neat short hair
            ApplyDefaultPart(shoes, 7);
            ApplyNamedDefaultPart(top, "Costume_14_01");
            // Apply after shoes so the costume's integrated boots hide regular shoes.
            ApplyNamedDefaultPart(bottom, "Costume_14_03");
            ApplyNamedDefaultPart(accessory, "Costume_14_02");
        }

        private void ApplyNamedDefaultPart(PartCollection collection, string prefabName)
        {
            foreach (GameObject prefab in collection.partPrefabs)
            {
                if (prefab != null && prefab.name == prefabName)
                {
                    ApplyPart(collection, prefab);
                    return;
                }
            }
            Debug.LogError("Default character part missing: " + prefabName, this);
        }

        private void ApplyDefaultPart(PartCollection collection, int index)
        {
            if (collection.partPrefabs == null || index < 0 || index >= collection.partPrefabs.Length)
                return;

            ApplyPart(collection, collection.partPrefabs[index]);
        }

        private void OpenCategory(Category category)
        {
            activeCategory = category;
            currentPage = 0;
            RefreshOptions();
            RefreshCategoryColors();
            if (catalogScroll != null) catalogScroll.verticalNormalizedPosition = 1f;
        }

        private void SelectVisibleOption(int visibleIndex)
        {
            if (activeCategory == Category.BodyColor)
            {
                if (visibleIndex >= 0 && visibleIndex < skinColors.Length)
                    ApplySkinColor(visibleIndex);
                return;
            }

            PartCollection collection = GetActiveCollection();
            int partIndex = GetPartIndexForVisibleIndex(visibleIndex);
            if (partIndex < 0 || partIndex >= collection.partPrefabs.Length)
                return;

            GameObject selectedPrefab = collection.partPrefabs[partIndex];
            // 입고 있으면 벗긴다. "마지막에 고른 것" 이 아니라 "지금 입고 있는가" 로 판단한다.
            // 한 카테고리에 여럿을 함께 입을 수 있어서다. (Accessory 의 모자 · 안경 · 얼굴장식)
            if (IsOptionWorn(activeCategory, partIndex) && TryUnequipPart(collection, selectedPrefab))
            {
                if (selectedOptions.TryGetValue(activeCategory, out int lastIndex) && lastIndex == partIndex)
                    selectedOptions.Remove(activeCategory);
                RefreshOptionColors(-1);
                return;
            }

            ApplyPart(collection, selectedPrefab);
            selectedOptions[activeCategory] = partIndex;
            RefreshOptionColors(partIndex);
        }

        /// <summary>
        /// 이 파츠를 지금 입고 있는가.
        ///
        /// 카탈로그 파츠는 Applier 가 기준이다. 카탈로그에 없는 파츠(targetRenderer 경로)는
        /// 카테고리에 하나뿐이라 마지막에 고른 번호(selectedOptions)로 판단한다.
        /// </summary>
        private bool IsOptionWorn(Category category, int partIndex)
        {
            PartCollection collection = GetCollection(category);
            if (collection?.partPrefabs == null || partIndex < 0 || partIndex >= collection.partPrefabs.Length)
                return false;

            if (IsCatalogPartEquipped(collection.partPrefabs[partIndex]))
                return true;

            return collection.activePart != null
                && selectedOptions.TryGetValue(category, out int lastIndex)
                && lastIndex == partIndex;
        }

        private bool TryUnequipPart(PartCollection collection, GameObject partPrefab)
        {
            if (TryUnequipCatalogPart(partPrefab))
                return true;

            if (collection.activePart == null)
                return false;

            collection.activePart.SetActive(false);
            Destroy(collection.activePart);
            collection.activePart = null;
            if (collection.targetRenderer != null)
                collection.targetRenderer.enabled = true;
            return true;
        }

        private void ApplyPart(PartCollection collection, GameObject partPrefab)
        {
            Category category = ReferenceEquals(collection, face) ? Category.Face
                : ReferenceEquals(collection, hair) ? Category.Hair
                : ReferenceEquals(collection, top) ? Category.Top
                : ReferenceEquals(collection, bottom) ? Category.Bottom
                : ReferenceEquals(collection, shoes) ? Category.Shoes : Category.Accessory;
            selectedOptions[category] = Array.IndexOf(collection.partPrefabs, partPrefab);
            if (TryApplyCatalogPart(partPrefab))
                return;
            if (collection.targetRenderer == null || partPrefab == null)
                return;

            SkinnedMeshRenderer source = partPrefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (source == null)
                return;

            if (collection.activePart != null)
            {
                collection.activePart.SetActive(false);
                Destroy(collection.activePart);
            }

            Transform original = collection.targetRenderer.transform;
            collection.activePart = new GameObject(original.name + " Runtime Part");
            Transform partTransform = collection.activePart.transform;
            partTransform.SetParent(original.parent, false);
            partTransform.localPosition = original.localPosition;
            partTransform.localRotation = original.localRotation;
            partTransform.localScale = original.localScale;

            SkinnedMeshRenderer runtimeRenderer = collection.activePart.AddComponent<SkinnedMeshRenderer>();
            runtimeRenderer.sharedMesh = source.sharedMesh;
            runtimeRenderer.sharedMaterials = source.sharedMaterials;
            runtimeRenderer.bones = collection.targetRenderer.bones;
            runtimeRenderer.rootBone = collection.targetRenderer.rootBone;
            runtimeRenderer.localBounds = source.localBounds;
            runtimeRenderer.updateWhenOffscreen = collection.targetRenderer.updateWhenOffscreen;

            // 표정에는 피부 재질을 입히지 않는다. 입술 · 볼터치 같은 빨강 계열이 피부색으로 지워진다.
            // (CharacterAppearanceApplier.NeedsSkinMaterial 참고)

            collection.targetRenderer.enabled = false;

            if (ReferenceEquals(collection, accessory))
            {
                bool coversHair = partPrefab.name.StartsWith("Hat_", StringComparison.Ordinal)
                    || partPrefab.name == "Costume_14_02";
                if (hair.activePart != null)
                    hair.activePart.SetActive(!coversHair);
                else if (hair.targetRenderer != null)
                    hair.targetRenderer.enabled = !coversHair;
            }

            if (ReferenceEquals(collection, bottom))
            {
                bool includesBoots = partPrefab.name == "Costume_14_03";
                if (shoes.activePart != null)
                    shoes.activePart.SetActive(!includesBoots);
                else if (shoes.targetRenderer != null)
                    shoes.targetRenderer.enabled = !includesBoots;
            }
        }

        private void PreviousPage()
        {
            if (currentPage <= 0)
                return;

            currentPage--;
            RefreshOptions();
        }

        private void NextPage()
        {
            int pageCount = GetPageCount(GetActiveCollection());
            if (currentPage >= pageCount - 1)
                return;

            currentPage++;
            RefreshOptions();
        }

        /// <summary>
        /// 팔레트에서 고른 색을 입힌다.
        ///
        /// ⚠ <b>두 가지 일을 명확히 나눠 둔다.</b> (PRD 09-1)
        ///    · 색을 칠하는 일   → Applier. UI 가 없어도 된다
        ///    · 팔레트를 갱신하는 일 → 이 화면. UI 가 있어야 한다
        ///    나누지 않으면 로비의 캐릭터에 색을 칠할 때 팔레트 버튼을 찾다가 죽는다.
        /// </summary>
        private void ApplySkinColor(int colorIndex)
        {
            currentSkinColor = skinColors[colorIndex];

            // 피부 렌더러와 이미 입은 파츠까지 Applier 가 한 번에 다시 칠한다.
            // 표정은 칠하지 않는다. (CharacterAppearanceApplier.NeedsSkinMaterial 참고)
            if (appearance != null)
                appearance.ApplySkinColor(currentSkinColor);

            RefreshSkinColorOptions();
        }







        private void CompleteCustomization()
        {
            string nickname = nicknameInput != null ? nicknameInput.text.Trim() : string.Empty;
            string validationMessage = GetNicknameValidationMessage(nickname);
            if (validationMessage != null)
            {
                ShowNicknameGuide(true, validationMessage);
                if (nicknameInput != null)
                    nicknameInput.ActivateInputField();
                return;
            }

            // 캐릭터를 서비스에 만든다. **성공했을 때만** 저장하고 Completed 를 알린다.
            // 저장 · 알림 · 실패 표시는 모두 CharacterCustomizationPersistence.cs 에 있다.
            SubmitCharacter(nickname);
        }

        private static string GetNicknameValidationMessage(string nickname)
        {
            if (string.IsNullOrWhiteSpace(nickname))
                return "닉네임을 입력해주세요.";
            if (nickname.Length < 2 || nickname.Length > 10)
                return "닉네임은 2~10자로 입력해 주세요.";
            // Include standalone Korean consonants/vowels such as ㅇㅇ as valid Hangul.
            if (!Regex.IsMatch(nickname, "^[가-힣ㄱ-ㅎㅏ-ㅣA-Za-z0-9]+$"))
                return "공백과 특수문자는 사용할 수 없습니다.";
            return null;
        }

        private void ShowNicknameGuide(bool warning, string message = null)
        {
            if (nicknameGuideText == null) return;
            nicknameGuideText.gameObject.SetActive(true);
            nicknameGuideText.transform.SetAsLastSibling();
            nicknameGuideText.text = message ?? "한글·영문·숫자 2~10자 (공백·특수문자 불가)";
            nicknameGuideText.color = warning
                ? new Color(.78f, .12f, .08f, 1f)
                : new Color(.28f, .20f, .13f, .82f);
        }

        private void SetNicknamePlaceholderVisible(bool visible)
        {
            if (nicknameInput != null && nicknameInput.placeholder != null)
                nicknameInput.placeholder.gameObject.SetActive(visible);
        }

        private bool ConfigureCategoryTabs()
        {
            bool changed = false;

            if (categoryButtons.Length > 0 && categoryButtons[0] != null)
            {
                RectTransform tabsRect = categoryButtons[0].transform.parent as RectTransform;
                if (tabsRect != null)
                {
                    tabsRect.anchorMin = new Vector2(tabsRect.anchorMin.x, .705f);
                    tabsRect.anchorMax = new Vector2(tabsRect.anchorMax.x, .815f);
                    tabsRect.offsetMin = Vector2.zero;
                    tabsRect.offsetMax = Vector2.zero;
                }
            }

            for (int i = 0; i < categoryButtons.Length; i++)
            {
                Button button = categoryButtons[i];
                if (button == null) continue;
                foreach (Image image in button.GetComponentsInChildren<Image>(true))
                {
                    if (image == button.image || !image.gameObject.activeSelf) continue;
                    image.gameObject.SetActive(false);
                    changed = true;
                }

                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    if (!label.gameObject.activeSelf)
                    {
                        label.gameObject.SetActive(true);
                        changed = true;
                    }

                    label.fontSize = 29f;
                    label.fontStyle = FontStyles.Bold;
                    label.alignment = TextAlignmentOptions.Center;
                    RectTransform labelRect = label.rectTransform;
                    labelRect.anchorMin = Vector2.zero;
                    labelRect.anchorMax = Vector2.one;
                    labelRect.offsetMin = Vector2.zero;
                    labelRect.offsetMax = Vector2.zero;
                }

                float center = (i + .5f) / categoryButtons.Length;
                RectTransform buttonRect = button.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(center - .0695f, .06f);
                buttonRect.anchorMax = new Vector2(center + .0695f, .94f);
                buttonRect.offsetMin = Vector2.zero;
                buttonRect.offsetMax = Vector2.zero;

                if (CopyOptionCardDepth(button)) changed = true;
            }

            return changed;
        }

        private bool CopyOptionCardDepth(Button target)
        {
            bool changed = false;
            if (optionButtons.Length == 0 || optionButtons[0] == null) return changed;
            Button source = optionButtons[0];

            Shadow sourceShadow = null;
            foreach (Shadow effect in source.GetComponents<Shadow>())
                if (effect.GetType() == typeof(Shadow)) { sourceShadow = effect; break; }
            Shadow targetShadow = null;
            foreach (Shadow effect in target.GetComponents<Shadow>())
                if (effect.GetType() == typeof(Shadow)) { targetShadow = effect; break; }
            if (sourceShadow != null)
            {
                if (targetShadow == null)
                {
                    targetShadow = target.gameObject.AddComponent<Shadow>();
                    changed = true;
                }

                targetShadow.effectColor = sourceShadow.effectColor;
                targetShadow.effectDistance = sourceShadow.effectDistance;
                targetShadow.useGraphicAlpha = sourceShadow.useGraphicAlpha;
            }

            Outline sourceOutline = source.GetComponent<Outline>();
            Outline targetOutline = target.GetComponent<Outline>();
            if (sourceOutline != null && targetOutline != null)
            {
                targetOutline.effectColor = sourceOutline.effectColor;
                targetOutline.effectDistance = sourceOutline.effectDistance;
                targetOutline.useGraphicAlpha = sourceOutline.useGraphicAlpha;
            }
            if (target.GetComponent<CustomizationButtonFeedback>() == null)
            {
                target.gameObject.AddComponent<CustomizationButtonFeedback>();
                changed = true;
            }

            return changed;
        }

        private bool ConfigureOptionPreviews()
        {
            bool changed = false;

            for (int i = 0; i < optionButtons.Length && i < optionImages.Length; i++)
            {
                if (ConfigureOptionPreview(optionButtons[i], optionImages[i])) changed = true;
            }

            return changed;
        }

        private static bool ConfigureOptionPreview(Button button, Image preview)
        {
            bool changed = false;
            if (button == null || preview == null) return changed;

            if (button.GetComponent<RectMask2D>() == null)
            {
                button.gameObject.AddComponent<RectMask2D>();
                changed = true;
            }

            // RectMask2D only clips to a rectangle, leaving the thumbnail's square
            // corners visible. Mask uses the card Image's rounded sprite alpha so the
            // preview can never render outside the rounded card.
            Mask roundedMask = button.GetComponent<Mask>();
            if (roundedMask == null)
            {
                roundedMask = button.gameObject.AddComponent<Mask>();
                changed = true;
            }

            roundedMask.showMaskGraphic = true;
            RectTransform rect = preview.rectTransform;
            // Slightly fill the rounded card while the alpha Mask keeps every preview
            // strictly inside its curved boundary.
            rect.anchorMin = new Vector2(-.06f, -.06f);
            rect.anchorMax = new Vector2(1.06f, 1.06f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            preview.preserveAspect = true;

            EnsureSelectionRim(button, out bool rimChanged);
            return changed || rimChanged;
        }

        /// <summary>
        /// 선택 테두리를 보장한다. <b>바뀐 것이 있었는지는 알려 주지 않는다.</b>
        ///
        /// 화면을 새로 그릴 때(<c>RefreshOptions</c> 등) 쓰는 길이다. 거기서는 프리팹을
        /// 저장하지 않으므로 변경 여부가 필요 없다.
        /// </summary>
        private static RoundedSelectionRing EnsureSelectionRim(Button button)
            => EnsureSelectionRim(button, out _);

        /// <summary>
        /// 선택 테두리를 보장하고, <paramref name="changed"/> 로 <b>구조가 달라졌는지</b> 알린다.
        ///
        /// 에디터 도구가 프리팹을 저장할지 정할 때 쓴다. 자세한 이유는
        /// <see cref="EnsureReusableUiLayout"/> 주석 참고.
        /// </summary>
        private static RoundedSelectionRing EnsureSelectionRim(Button button, out bool changed)
        {
            changed = false;

            Transform obsolete = button.transform.Find("Selection Rim");
            if (obsolete != null && obsolete.gameObject.activeSelf)
            {
                obsolete.gameObject.SetActive(false);
                changed = true;
            }


            Transform existing = button.transform.Find("Selection Border");
            RoundedSelectionRing ring;
            if (existing == null)
            {
                GameObject rimObject = new GameObject("Selection Border", typeof(RectTransform), typeof(RoundedSelectionRing));
                RectTransform rimRect = rimObject.GetComponent<RectTransform>();
                rimRect.SetParent(button.transform, false);
                rimRect.anchorMin = new Vector2(.025f, .025f);
                rimRect.anchorMax = new Vector2(.975f, .975f);
                rimRect.offsetMin = Vector2.zero;
                rimRect.offsetMax = Vector2.zero;
                ring = rimObject.GetComponent<RoundedSelectionRing>();
                changed = true;
            }
            else
            {
                ring = existing.GetComponent<RoundedSelectionRing>();
            }

            ring.raycastTarget = false;
            ring.transform.SetAsLastSibling();
            return ring;
        }

        /// <summary>
        /// 이 화면이 갖춰야 할 UI 를 보장한다. **실제로 무언가 만들거나 껐으면 참**을 준다.
        ///
        /// <b>왜 반환값이 필요한가.</b> 에디터 도구가 이 함수를 부른 뒤 프리팹을 저장하는데,
        /// 바뀐 것이 없어도 저장하면 파일이 매번 다시 쓰인다. 그 쓰기가 Multiplayer Play Mode
        /// 의 가상 플레이어에게 에셋 갱신을 일으키고, 거기 AssetDatabase 는 읽기 전용이라
        /// <c>"Asset Database is set to Read Only, but it has found out-of-date assets"</c> 가
        /// 쏟아진다. 심하면 클론이 Fusion 설정을 못 읽어 접속까지 실패한다.
        ///
        /// ⚠ <b>세는 것은 "구조가 바뀌었나" 뿐이다.</b> 오브젝트를 새로 만들었거나, 켜고 끈
        ///    상태가 달라졌을 때만 참이다. 좌표·글자 크기 같은 속성 대입은 매번 <b>같은 값</b>을
        ///    넣으므로 직렬화 결과가 달라지지 않아 세지 않는다.
        ///
        /// ⚠ 나중에 <b>실행할 때마다 달라지는 값</b>을 여기서 쓰게 되면 그 전제가 깨진다.
        ///    그때는 그 자리에서도 changed 를 올려 줘야 한다.
        /// </summary>
        public bool EnsureReusableUiLayout()
        {
            // ⚠ 네 개를 모두 부른 뒤에 합친다. || 로 이으면 앞이 참일 때 뒤가 실행되지 않는다.
            bool nickname = EnsureNicknameInput();
            bool tabs = ConfigureCategoryTabs();
            bool previews = ConfigureOptionPreviews();
            bool heading = RemoveMainHeadingDepth();

            return nickname || tabs || previews || heading;
        }

        private bool RemoveMainHeadingDepth()
        {
            bool changed = false;

            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || !text.text.Contains("나만의 캐릭터를 꾸며보세요"))
                    continue;

                foreach (Shadow shadow in text.GetComponents<Shadow>())
                {
                    if (!shadow.enabled) continue;
                    shadow.enabled = false;
                    changed = true;
                }

                Outline outline = text.GetComponent<Outline>();
                if (outline != null && outline.enabled)
                {
                    outline.enabled = false;
                    changed = true;
                }
            }

            return changed;
        }

        public bool EnsureNicknameInput()
        {
            bool changed = false;

            if (nicknameInput == null)
            {
                Transform existing = transform.Find("Nickname Input");
                if (existing != null)
                    nicknameInput = existing.GetComponent<TMP_InputField>();
            }
            TMP_Text referenceLabel = completeButton != null ? completeButton.GetComponentInChildren<TMP_Text>(true) : null;
            if (nicknameInput == null)
            {
                GameObject inputObject = new GameObject("Nickname Input", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
                RectTransform inputRect = inputObject.GetComponent<RectTransform>();
                inputRect.SetParent(transform, false);
                inputRect.anchorMin = new Vector2(.43f, .12f);
                inputRect.anchorMax = new Vector2(.68f, .19f);
                inputRect.offsetMin = Vector2.zero;
                inputRect.offsetMax = Vector2.zero;

                Image background = inputObject.GetComponent<Image>();
                Image completeBackground = completeButton != null ? completeButton.GetComponent<Image>() : null;
                if (completeBackground != null)
                {
                    background.sprite = completeBackground.sprite;
                    background.type = completeBackground.type;
                }
                background.color = new Color(1f, .95f, .78f, 1f);

                GameObject viewportObject = new GameObject("Text Area", typeof(RectTransform), typeof(RectMask2D));
                RectTransform viewport = viewportObject.GetComponent<RectTransform>();
                viewport.SetParent(inputRect, false);
                viewport.anchorMin = Vector2.zero;
                viewport.anchorMax = Vector2.one;
                viewport.offsetMin = new Vector2(28f, 8f);
                viewport.offsetMax = new Vector2(-28f, -8f);

                TMP_Text text = CreateInputText("Text", viewport, referenceLabel, new Color(.23f, .12f, .055f, 1f));
                TMP_Text placeholder = CreateInputText("Placeholder", viewport, referenceLabel, new Color(.35f, .25f, .16f, .55f));
                placeholder.text = "닉네임을 입력해 주세요";
                placeholder.fontStyle = FontStyles.Italic;

                nicknameInput = inputObject.GetComponent<TMP_InputField>();
                nicknameInput.textViewport = viewport;
                nicknameInput.textComponent = text;
                nicknameInput.placeholder = placeholder;
                nicknameInput.characterLimit = 10;
                nicknameInput.lineType = TMP_InputField.LineType.SingleLine;
                nicknameInput.contentType = TMP_InputField.ContentType.Standard;
                nicknameInput.text = PlayerPrefs.GetString("PlayerNickname", string.Empty);
                changed = true;
            }

            if (nicknameInput.placeholder is TMP_Text inputPlaceholder)
                inputPlaceholder.text = "닉네임을 입력해주세요";

            RectTransform nicknameInputRect = nicknameInput.GetComponent<RectTransform>();
            nicknameInputRect.anchorMin = new Vector2(.43f, .14f);
            nicknameInputRect.anchorMax = new Vector2(.68f, .20f);
            nicknameInputRect.offsetMin = Vector2.zero;
            nicknameInputRect.offsetMax = Vector2.zero;
            if (completeButton != null)
            {
                RectTransform completeRect = completeButton.GetComponent<RectTransform>();
                completeRect.anchorMin = new Vector2(completeRect.anchorMin.x, .14f);
                completeRect.anchorMax = new Vector2(completeRect.anchorMax.x, .20f);
                completeRect.offsetMin = Vector2.zero;
                completeRect.offsetMax = Vector2.zero;
            }

            if (nicknameGuideText == null)
            {
                Transform existingGuide = transform.Find("Nickname Guide");
                if (existingGuide != null)
                    nicknameGuideText = existingGuide.GetComponent<TMP_Text>();
            }
            if (nicknameGuideText == null)
            {
                GameObject guideObject = new GameObject("Nickname Guide", typeof(RectTransform), typeof(TextMeshProUGUI));
                RectTransform guideRect = guideObject.GetComponent<RectTransform>();
                guideRect.SetParent(transform, false);
                nicknameGuideText = guideObject.GetComponent<TextMeshProUGUI>();
                if (referenceLabel != null)
                {
                    nicknameGuideText.font = referenceLabel.font;
                    nicknameGuideText.fontSharedMaterial = referenceLabel.fontSharedMaterial;
                }
                nicknameGuideText.fontSize = 19f;
                nicknameGuideText.alignment = TextAlignmentOptions.Center;
                nicknameGuideText.enableWordWrapping = false;
                changed = true;
            }
            RectTransform nicknameGuideRect = nicknameGuideText.rectTransform;
            nicknameGuideText.fontSize = 19f;
            nicknameGuideRect.anchorMin = new Vector2(.41f, .105f);
            nicknameGuideRect.anchorMax = new Vector2(.70f, .135f);
            nicknameGuideRect.offsetMin = Vector2.zero;
            nicknameGuideRect.offsetMax = Vector2.zero;
            ShowNicknameGuide(false);

            return changed;
        }

        private static TMP_Text CreateInputText(string objectName, RectTransform parent, TMP_Text reference, Color color)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform rect = textObject.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
            if (reference != null)
            {
                text.font = reference.font;
                text.fontSharedMaterial = reference.fontSharedMaterial;
            }
            text.fontSize = 31f;
            text.color = color;
            text.alignment = TextAlignmentOptions.Center;
            text.enableWordWrapping = false;
            return text;
        }

        private void RefreshOptions()
        {
            if (catalogScroll != null)
            {
                RefreshScrollOptions();
                return;
            }
            if (activeCategory == Category.BodyColor)
            {
                RefreshSkinColorOptions();
                return;
            }

            PartCollection collection = GetActiveCollection();
            int pageCount = GetPageCount(collection);
            currentPage = Mathf.Clamp(currentPage, 0, pageCount - 1);

            if (sectionTitle != null)
                sectionTitle.text = collection.displayName + " 선택";
            if (pageText != null)
                pageText.text = $"{currentPage + 1} / {pageCount}";

            int firstIndex = currentPage * itemsPerPage;
            for (int i = 0; i < optionButtons.Length; i++)
            {
                int partIndex = firstIndex + i;
                bool hasPart = partIndex < collection.partPrefabs.Length;
                optionButtons[i].gameObject.SetActive(hasPart);
                if (!hasPart)
                    continue;

                TMP_Text label = optionButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.gameObject.SetActive(false);

                if (i < optionImages.Length && optionImages[i] != null)
                {
                    optionImages[i].gameObject.SetActive(true);
                    optionImages[i].sprite = partIndex < collection.partThumbnails.Length
                        ? collection.partThumbnails[partIndex]
                        : null;
                }
            }

            previousPageButton.interactable = currentPage > 0;
            nextPageButton.interactable = currentPage < pageCount - 1;
            RefreshOptionColors(-1);
        }

        private void RefreshSkinColorOptions()
        {
            if (catalogScroll != null)
            {
                if (activeCategory == Category.BodyColor) RefreshScrollOptions();
                return;
            }
            if (sectionTitle != null)
                sectionTitle.text = "피부";
            if (pageText != null)
                pageText.text = "1 / 1";

            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool hasColor = i < skinColors.Length;
                optionButtons[i].gameObject.SetActive(hasColor);
                if (!hasColor)
                    continue;

                TMP_Text label = optionButtons[i].GetComponentInChildren<TMP_Text>(true);
                if (label != null)
                    label.gameObject.SetActive(false);
                if (i < optionImages.Length && optionImages[i] != null)
                    optionImages[i].gameObject.SetActive(false);

                optionButtons[i].image.color = skinColors[i];
            }

            previousPageButton.interactable = false;
            nextPageButton.interactable = false;
        }

        private void RefreshCategoryColors()
        {
            Color selected = new Color(1f, .89f, .72f, 1f);
            Color normal = new Color(1f, 0.97f, 0.88f, 1f);

            for (int i = 0; i < categoryButtons.Length; i++)
                categoryButtons[i].image.color = i == (int)activeCategory ? selected : normal;
        }

        private void RefreshOptionColors(int selectedPartIndex)
        {
            if (catalogScroll != null)
            {
                RefreshScrollSelection();
                return;
            }
            Color selected = new Color(1f, .89f, .72f, 1f);
            // Thumbnail backgrounds are authored as RGB(255,246,224). Matching the
            // card removes the visible square without enlarging the character art.
            Color normal = new Color(1f, 246f / 255f, 224f / 255f, 1f);
            int firstIndex = currentPage * itemsPerPage;

            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool chosen = firstIndex + i == selectedPartIndex;
                optionButtons[i].image.color = chosen ? selected : normal;
                if (i < optionImages.Length && optionImages[i] != null)
                    optionImages[i].color = chosen ? new Color(1f, .94f, .86f, 1f) : Color.white;
                RoundedSelectionRing rim = EnsureSelectionRim(optionButtons[i]);
                rim.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private PartCollection GetActiveCollection()
        {
            return activeCategory switch
            {
                Category.Face => face,
                Category.Hair => hair,
                Category.Top => top,
                Category.Bottom => bottom,
                Category.Shoes => shoes,
                Category.Accessory => accessory,
                _ => face
            };
        }

        private int GetPageCount(PartCollection collection)
        {
            return Mathf.Max(1, Mathf.CeilToInt(collection.partPrefabs.Length / (float)itemsPerPage));
        }
    }

}
