using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Character
{
    public sealed partial class CharacterCustomizationController
    {
        [Header("Scrollable catalogue")]
        [SerializeField] private ScrollRect catalogScroll;
        private readonly Dictionary<Category, int> selectedOptions = new Dictionary<Category, int>();
        private readonly List<int> visiblePartIndices = new List<int>();

        /// <summary>
        /// 이 파츠를 지금 탭에 보여줄지.
        ///
        /// 슬롯 정보는 카탈로그 에셋에서 읽는다. 예전에는 컨트롤러의 catalogParts 배열을
        /// 훑었지만, 그 배열은 PRD 09-1 에서 카탈로그로 옮겼다.
        /// </summary>
        private bool IsVisibleInCurrentCategory(GameObject prefab)
        {
            if (catalog != null && catalog.TryFind(prefab, out CharacterPartCatalog.Entry entry))
            {
                // The face tab is for expressions only. Body and ear meshes belong to skin/model setup.
                if (activeCategory == Category.Face) return entry.slot == WearSlot.Face;
                // Hands/gloves are not accessories in this creator.
                if (activeCategory == Category.Accessory) return entry.slot != WearSlot.Gloves;
                return true;
            }
            return activeCategory != Category.Face;
        }

        private int GetPartIndexForVisibleIndex(int visibleIndex)
        {
            return catalogScroll != null && activeCategory != Category.BodyColor
                && visibleIndex >= 0 && visibleIndex < visiblePartIndices.Count
                ? visiblePartIndices[visibleIndex]
                : currentPage * itemsPerPage + visibleIndex;
        }

        private void RefreshScrollOptions()
        {
            bool skin = activeCategory == Category.BodyColor;
            PartCollection collection = GetActiveCollection();
            visiblePartIndices.Clear();
            if (!skin)
                for (int i = 0; i < collection.partPrefabs.Length; i++)
                    if (IsVisibleInCurrentCategory(collection.partPrefabs[i])) visiblePartIndices.Add(i);
            int count = skin ? skinColors.Length : visiblePartIndices.Count;
            // Reuse the existing cards, growing once for larger catalogues.
            int oldCount = optionButtons.Length;
            if (count > oldCount)
            {
                System.Array.Resize(ref optionButtons, count);
                System.Array.Resize(ref optionImages, count);
                for (int i = oldCount; i < count; i++)
                {
                    int index = i;
                    var button = Instantiate(optionButtons[0], catalogScroll.content);
                    button.name = "Option " + (i + 1);
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => SelectVisibleOption(index));
                    optionButtons[i] = button;
                    optionImages[i] = button.transform.Find("Preview Image").GetComponent<Image>();
                    ConfigureOptionPreview(optionButtons[i], optionImages[i]);
                }
            }
            for (int i = 0; i < optionButtons.Length; i++)
            {
                bool visible = i < count;
                optionButtons[i].gameObject.SetActive(visible);
                if (!visible) continue;
                optionImages[i].gameObject.SetActive(!skin);
                optionImages[i].raycastTarget = false;
                ConfigureOptionPreview(optionButtons[i], optionImages[i]);
                if (!skin)
                {
                    int sourceIndex = visiblePartIndices[i];
                    optionImages[i].sprite = sourceIndex < collection.partThumbnails.Length ? collection.partThumbnails[sourceIndex] : null;
                }
                foreach (var label in optionButtons[i].GetComponentsInChildren<TMPro.TMP_Text>(true))
                    label.gameObject.SetActive(false);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(catalogScroll.content);
            RefreshScrollSelection();
        }

        private void RefreshScrollSelection()
        {
            bool skin = activeCategory == Category.BodyColor;
            Color selectedFill = new Color(1f, .89f, .72f, 1f);
            Color normalFill = new Color(1f, 246f / 255f, 224f / 255f, 1f);
            selectedOptions.TryGetValue(activeCategory, out int selected);
            bool hasSelection = selectedOptions.ContainsKey(activeCategory);
            for (int i = 0; i < optionButtons.Length; i++)
            {
                if (!optionButtons[i].gameObject.activeSelf) continue;
                bool chosen = skin ? skinColors[i] == currentSkinColor
                    : hasSelection && i < visiblePartIndices.Count && selected == visiblePartIndices[i]
                        && IsCatalogPartEquipped(GetActiveCollection().partPrefabs[visiblePartIndices[i]]);
                optionButtons[i].image.color = skin ? skinColors[i] : chosen ? selectedFill : normalFill;
                if (!skin && i < optionImages.Length && optionImages[i] != null)
                    optionImages[i].color = chosen ? new Color(1f, .94f, .86f, 1f) : Color.white;
                var rim = EnsureSelectionRim(optionButtons[i]);
                rim.color = new Color(0f, 0f, 0f, 0f);
            }
        }
    }
}
