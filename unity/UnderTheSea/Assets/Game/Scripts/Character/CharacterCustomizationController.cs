using System;
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
            if (sectionTitle != null)
                sectionTitle.text = "캐릭터 설정 완료!";

            Debug.Log("Character customization completed. Saving will be implemented in a later step.");
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
            Color selected = new Color(0.80f, 0.89f, 0.97f, 1f);
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
            Color selected = new Color(1f, 0.88f, 0.58f, 1f);
            Color normal = new Color(1f, 0.965f, 0.88f, 1f);
            int firstIndex = currentPage * itemsPerPage;

            for (int i = 0; i < optionButtons.Length; i++)
                optionButtons[i].image.color = firstIndex + i == selectedPartIndex ? selected : normal;
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
