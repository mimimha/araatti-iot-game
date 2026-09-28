using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Lobby.Dance
{
    /// <summary>
    /// **도넛 한 조각을 그리는 UI 그래픽.** 춤 휠(<see cref="DanceWheelView"/>)의 칸 하나다.
    ///
    /// 그림 파일 없이 메시로 그린다. 칸 수 · 반지름 · 틈을 숫자로 바꾸면 모양이 그대로 따라온다.
    ///
    /// <b>틈이 평행하다.</b> 칸을 각도로만 나누면 틈이 바깥으로 갈수록 벌어진다(부채꼴 틈).
    /// 여기서는 칸의 양쪽 변을 반지름선에서 <see cref="gap"/> 의 절반만큼 <b>평행하게</b> 민다.
    /// 그래서 안쪽이든 바깥쪽이든 틈 폭이 같다.
    ///
    /// <code>
    ///   반지름 r 에서 변이 반지름선과 벌어지는 각도 = asin((gap / 2) / r)
    ///   안쪽 점과 바깥 점을 각각 그 각도로 잡고 잇는다 → 변은 곧은 선, 반지름선과 평행
    /// </code>
    ///
    /// 각도는 수학 기준이다 — 0° 가 오른쪽, 반시계 방향으로 커진다. 조각의 중심은 이 RectTransform 의
    /// 기준점(pivot)이다. 휠의 칸들은 모두 같은 중심에 겹쳐 놓고 각도만 다르게 준다.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class RingSegmentGraphic : MaskableGraphic
    {
        [SerializeField, Min(0f)] private float innerRadius = 90f;
        [SerializeField, Min(0f)] private float outerRadius = 220f;

        [Tooltip("조각이 시작하는 각도(도). 0 = 오른쪽, 반시계 방향.")]
        [SerializeField] private float startAngle;

        [Tooltip("조각이 차지하는 각도(도). 5칸이면 72.")]
        [SerializeField, Range(0f, 360f)] private float sweep = 72f;

        [Tooltip("옆 칸과의 틈(px). 안쪽 · 바깥쪽 모두 같은 폭이다.")]
        [SerializeField, Min(0f)] private float gap = 12f;

        [Tooltip("곡선을 몇 도마다 한 번 꺾을지. 작을수록 매끄럽다.")]
        [SerializeField, Range(0.5f, 10f)] private float degreesPerStep = 2f;

        /// <summary>조각의 가운데 각도(도). 칸 이름을 놓을 자리를 구할 때 쓴다.</summary>
        public float CenterAngle => startAngle + sweep * 0.5f;

        public float InnerRadius => innerRadius;
        public float OuterRadius => outerRadius;

        /// <summary>모양을 한 번에 정한다. 휠 프리팹을 만드는 에디터 스크립트가 쓴다.</summary>
        public void Configure(float inner, float outer, float start, float sweepDegrees, float gapPixels)
        {
            innerRadius = inner;
            outerRadius = outer;
            startAngle = start;
            sweep = sweepDegrees;
            gap = gapPixels;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            float r0 = Mathf.Max(innerRadius, 0.01f);
            float r1 = outerRadius;
            if (r1 <= r0 || sweep <= 0f)
            {
                return;
            }

            // 반지름마다 변이 안으로 들어오는 각도. 안쪽일수록 크다 — 그래야 틈 폭이 같다.
            float halfGap = gap * 0.5f;
            float innerTrim = Mathf.Asin(Mathf.Clamp01(halfGap / r0)) * Mathf.Rad2Deg;
            float outerTrim = Mathf.Asin(Mathf.Clamp01(halfGap / r1)) * Mathf.Rad2Deg;

            float innerFrom = startAngle + innerTrim;
            float innerTo = startAngle + sweep - innerTrim;
            float outerFrom = startAngle + outerTrim;
            float outerTo = startAngle + sweep - outerTrim;

            // 틈이 조각보다 넓으면 그릴 것이 없다.
            if (innerTo <= innerFrom || outerTo <= outerFrom)
            {
                return;
            }

            int steps = Mathf.Max(2, Mathf.CeilToInt(sweep / degreesPerStep));
            Color32 tint = color;

            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                vh.AddVert(Polar(r0, Mathf.Lerp(innerFrom, innerTo, t)), tint, new Vector2(t, 0f));
                vh.AddVert(Polar(r1, Mathf.Lerp(outerFrom, outerTo, t)), tint, new Vector2(t, 1f));
            }

            for (int i = 0; i < steps; i++)
            {
                int a = i * 2;
                vh.AddTriangle(a, a + 1, a + 3);
                vh.AddTriangle(a, a + 3, a + 2);
            }
        }

        private static Vector2 Polar(float radius, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            outerRadius = Mathf.Max(outerRadius, innerRadius + 1f);
            SetVerticesDirty();
        }
#endif
    }
}
