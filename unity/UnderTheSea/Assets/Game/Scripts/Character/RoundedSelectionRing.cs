using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Character
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RoundedSelectionRing : MaskableGraphic
    {
        [SerializeField, Min(1f)] private float thickness = 5f;
        [SerializeField, Min(1f)] private float cornerRadius = 24f;
        private const int SegmentsPerCorner = 8;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            Rect r = GetPixelAdjustedRect();
            float radius = Mathf.Min(cornerRadius, Mathf.Min(r.width, r.height) * .5f);
            float innerRadius = Mathf.Max(0f, radius - thickness);
            Rect inner = new Rect(r.xMin + thickness, r.yMin + thickness,
                Mathf.Max(0f, r.width - thickness * 2f), Mathf.Max(0f, r.height - thickness * 2f));

            int count = SegmentsPerCorner * 4;
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                Vector2 outerCenter = GetCornerCenter(r, radius, direction);
                Vector2 innerCenter = GetCornerCenter(inner, innerRadius, direction);
                AddVertex(vh, outerCenter + direction * radius);
                AddVertex(vh, innerCenter + direction * innerRadius);
            }

            for (int i = 0; i < count; i++)
            {
                int next = (i + 1) % count;
                int outer = i * 2;
                int inside = outer + 1;
                int nextOuter = next * 2;
                int nextInside = nextOuter + 1;
                vh.AddTriangle(outer, nextOuter, nextInside);
                vh.AddTriangle(outer, nextInside, inside);
            }
        }

        private static Vector2 GetCornerCenter(Rect rect, float radius, Vector2 direction)
        {
            return new Vector2(direction.x >= 0f ? rect.xMax - radius : rect.xMin + radius,
                direction.y >= 0f ? rect.yMax - radius : rect.yMin + radius);
        }

        private void AddVertex(VertexHelper vh, Vector2 position)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            vertex.position = position;
            vh.AddVert(vertex);
        }
    }
}
