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
        [SerializeField] private SkinnedMeshRenderer[] skinRenderers;
        [SerializeField] private Color[] skinColors;
        [SerializeField] private Material skinSourceMaterial;
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
        private Material originalSkinMaterial;
        private Texture2D originalSkinTexture;
        private Material runtimeSkinMaterial;
        private Texture2D runtimeSkinTexture;

        private void Awake()
        {
            EnsureReusableUiLayout();
            CacheOriginalSkinAssets();

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
            if (selectedOptions.TryGetValue(activeCategory, out int equippedIndex)
                && equippedIndex == partIndex
                && TryUnequipPart(collection, selectedPrefab))
            {
                selectedOptions.Remove(activeCategory);
                RefreshOptionColors(-1);
                return;
            }

            ApplyPart(collection, selectedPrefab);
            selectedOptions[activeCategory] = partIndex;
            RefreshOptionColors(partIndex);
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

            if (ReferenceEquals(collection, face))
                ApplySkinMaterial(runtimeRenderer);

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

        private void ApplySkinColor(int colorIndex)
        {
            currentSkinColor = skinColors[colorIndex];
            RebuildSkinMaterial();

            foreach (SkinnedMeshRenderer renderer in skinRenderers)
                ApplySkinMaterial(renderer);

            if (face.activePart != null)
                ApplySkinMaterial(face.activePart.GetComponent<SkinnedMeshRenderer>());

            RefreshCatalogSkin();

            RefreshSkinColorOptions();
        }

        private void RebuildSkinMaterial()
        {
            if (originalSkinMaterial == null || originalSkinTexture == null)
            {
                Debug.LogError("피부색 변경에 사용할 원본 재질 또는 텍스처를 찾지 못했습니다.", this);
                return;
            }

            DestroyRuntimeSkinAssets();

            // 항상 변경되지 않은 원본에서 다시 생성해야 밝은 색으로 되돌릴 수 있다.
            runtimeSkinTexture = CreateRecoloredSkinTexture(originalSkinTexture, currentSkinColor);
            runtimeSkinMaterial = new Material(originalSkinMaterial)
            {
                name = originalSkinMaterial.name + " (Runtime Skin)"
            };
            runtimeSkinMaterial.SetTexture("_BaseMap", runtimeSkinTexture);
            runtimeSkinMaterial.SetColor("_BaseColor", Color.white);
        }

        private void CacheOriginalSkinAssets()
        {
            if (skinSourceMaterial != null)
            {
                originalSkinMaterial = skinSourceMaterial;
                originalSkinTexture = skinSourceMaterial.GetTexture("_BaseMap") as Texture2D;
                return;
            }
            foreach (SkinnedMeshRenderer renderer in skinRenderers)
            {
                if (renderer == null || renderer.sharedMaterial == null)
                    continue;

                originalSkinMaterial = renderer.sharedMaterial;
                originalSkinTexture = originalSkinMaterial.GetTexture("_BaseMap") as Texture2D;
                return;
            }
        }

        private void ApplySkinMaterial(Renderer renderer)
        {
            if (renderer == null || runtimeSkinMaterial == null)
                return;

            renderer.sharedMaterial = runtimeSkinMaterial;

            // 이전 구현에서 남은 전체 렌더러 색상 틴트를 제거한다.
            renderer.SetPropertyBlock(null);
        }

        public static Texture2D CreateRecoloredSkinTexture(Texture2D source, Color skinColor)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;

            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount > 1);
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            result.Apply(false, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            Color[] pixels = result.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];

                // Cute Characters 팔레트에서 피부 메시가 참조하는 빨강/주황 계열만 교체한다.
                bool isSkinPalette = pixel.r > 0.22f
                    && pixel.r > pixel.g * 1.12f
                    && pixel.r > pixel.b * 1.12f;
                if (!isSkinPalette)
                    continue;

                float brightness = Mathf.Clamp01(Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)));
                Color recolored = skinColor * brightness;
                recolored.a = pixel.a;
                pixels[i] = recolored;
            }

            result.SetPixels(pixels);
            result.wrapMode = source.wrapMode;
            result.filterMode = source.filterMode;
            result.anisoLevel = source.anisoLevel;
            result.name = source.name + " (Runtime Skin)";
            result.Apply(source.mipmapCount > 1, false);
            return result;
        }

        private void OnDestroy()
        {
            DestroyRuntimeSkinAssets();
        }

        private void DestroyRuntimeSkinAssets()
        {
            if (runtimeSkinMaterial != null)
                Destroy(runtimeSkinMaterial);
            if (runtimeSkinTexture != null)
                Destroy(runtimeSkinTexture);

            runtimeSkinMaterial = null;
            runtimeSkinTexture = null;
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

            PlayerPrefs.SetString("PlayerNickname", nickname);
            PlayerPrefs.Save();

            if (sectionTitle != null)
                sectionTitle.text = nickname + " 캐릭터 설정 완료!";

            Debug.Log("Character customization completed for " + nickname + ".");

            // 저장까지 끝났다고 알린다. 씬 전환은 이 알림을 듣는 쪽에서 한다.
            // (GAME_STRUCTURE.md 3장 — 씬 전환 코드는 이 파일에 넣지 않는다)
            Completed?.Invoke(nickname);
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

        private void ConfigureCategoryTabs()
        {
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
                    if (image != button.image) image.gameObject.SetActive(false);
                foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
                {
                    label.gameObject.SetActive(true);
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

                CopyOptionCardDepth(button);
            }
        }

        private void CopyOptionCardDepth(Button target)
        {
            if (optionButtons.Length == 0 || optionButtons[0] == null) return;
            Button source = optionButtons[0];

            Shadow sourceShadow = null;
            foreach (Shadow effect in source.GetComponents<Shadow>())
                if (effect.GetType() == typeof(Shadow)) { sourceShadow = effect; break; }
            Shadow targetShadow = null;
            foreach (Shadow effect in target.GetComponents<Shadow>())
                if (effect.GetType() == typeof(Shadow)) { targetShadow = effect; break; }
            if (sourceShadow != null)
            {
                if (targetShadow == null) targetShadow = target.gameObject.AddComponent<Shadow>();
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
                target.gameObject.AddComponent<CustomizationButtonFeedback>();
        }

        private void ConfigureOptionPreviews()
        {
            for (int i = 0; i < optionButtons.Length && i < optionImages.Length; i++)
                ConfigureOptionPreview(optionButtons[i], optionImages[i]);
        }

        private static void ConfigureOptionPreview(Button button, Image preview)
        {
            if (button == null || preview == null) return;
            if (button.GetComponent<RectMask2D>() == null)
                button.gameObject.AddComponent<RectMask2D>();
            // RectMask2D only clips to a rectangle, leaving the thumbnail's square
            // corners visible. Mask uses the card Image's rounded sprite alpha so the
            // preview can never render outside the rounded card.
            Mask roundedMask = button.GetComponent<Mask>();
            if (roundedMask == null)
                roundedMask = button.gameObject.AddComponent<Mask>();
            roundedMask.showMaskGraphic = true;
            RectTransform rect = preview.rectTransform;
            // Slightly fill the rounded card while the alpha Mask keeps every preview
            // strictly inside its curved boundary.
            rect.anchorMin = new Vector2(-.06f, -.06f);
            rect.anchorMax = new Vector2(1.06f, 1.06f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            preview.preserveAspect = true;
            EnsureSelectionRim(button);
        }

        private static RoundedSelectionRing EnsureSelectionRim(Button button)
        {
            Transform obsolete = button.transform.Find("Selection Rim");
            if (obsolete != null)
                obsolete.gameObject.SetActive(false);

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
            }
            else
            {
                ring = existing.GetComponent<RoundedSelectionRing>();
            }

            ring.raycastTarget = false;
            ring.transform.SetAsLastSibling();
            return ring;
        }

        public void EnsureReusableUiLayout()
        {
            EnsureNicknameInput();
            ConfigureCategoryTabs();
            ConfigureOptionPreviews();
            RemoveMainHeadingDepth();
        }

        private void RemoveMainHeadingDepth()
        {
            foreach (TMP_Text text in GetComponentsInChildren<TMP_Text>(true))
            {
                if (text == null || !text.text.Contains("나만의 캐릭터를 꾸며보세요"))
                    continue;

                foreach (Shadow shadow in text.GetComponents<Shadow>())
                    shadow.enabled = false;
                Outline outline = text.GetComponent<Outline>();
                if (outline != null)
                    outline.enabled = false;
            }
        }

        public void EnsureNicknameInput()
        {
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
            }
            RectTransform nicknameGuideRect = nicknameGuideText.rectTransform;
            nicknameGuideText.fontSize = 19f;
            nicknameGuideRect.anchorMin = new Vector2(.41f, .105f);
            nicknameGuideRect.anchorMax = new Vector2(.70f, .135f);
            nicknameGuideRect.offsetMin = Vector2.zero;
            nicknameGuideRect.offsetMax = Vector2.zero;
            ShowNicknameGuide(false);
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
