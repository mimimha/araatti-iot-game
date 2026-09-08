using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Character
{
    [ExecuteAlways]
    [RequireComponent(typeof(GridLayoutGroup))]
    public sealed class CustomizationGridLayout : MonoBehaviour
    {
        private void OnEnable() { Resize(); }
        private void OnRectTransformDimensionsChange() { Resize(); }
        private void Resize()
        {
            var grid = GetComponent<GridLayoutGroup>();
            if (grid == null) return;
            float width = ((RectTransform)transform).rect.width;
            float cell = Mathf.Max(1, (width - grid.padding.horizontal - grid.spacing.x * 4) / 5);
            grid.cellSize = new Vector2(cell, cell * .90f);
        }
    }
}
