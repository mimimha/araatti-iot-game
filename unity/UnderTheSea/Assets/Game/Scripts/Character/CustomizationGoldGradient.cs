using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Character
{
    public sealed class CustomizationGoldGradient : BaseMeshEffect
    {
        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive() || vh.currentVertCount == 0) return;
            var rect = ((RectTransform)transform).rect;
            UIVertex v = default;
            for (int i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref v, i);
                float t = Mathf.InverseLerp(rect.yMin, rect.yMax, v.position.y);
                v.color = (Color)v.color * Color.Lerp(new Color(.91f,.72f,.43f), Color.white, t);
                vh.SetUIVertex(v, i);
            }
        }
    }
}
