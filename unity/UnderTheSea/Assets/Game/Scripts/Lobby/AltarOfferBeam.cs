using System.Collections;
using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **조각을 바친 순간 제단에서 하늘로 올라가는 얇은 노란 빛줄기.** 봉헌 한 번에 한 줄.
    ///
    /// <code>
    ///   솟기   0 → 목표 높이까지 위로 뻗는다      (끝으로 갈수록 느려진다)
    ///   머물기  다 뻗은 채로 잠깐
    ///   사라지기 투명해지며 꺼진다
    /// </code>
    ///
    /// 선 두 겹으로 그린다 — 가늘고 밝은 심(core)과, 그보다 넓고 옅은 번짐(glow). 위로 갈수록 가늘어진다.
    /// 재질은 일반 투명 섞기다 — 더하기 섞기는 밝은 하늘 위에서 노란색이 흰색으로 바랬다.
    /// 그림 파일 없이 <see cref="LineRenderer"/> 로 그려서 높이 · 굵기 · 색을 숫자로 바꿀 수 있다.
    ///
    /// <b>제단의 평소 빛기둥(<c>AltarBeam</c>)과 별개다.</b> 그것은 늘 켜져 있는 기본 연출이고 여기서 손대지
    /// 않는다. 이 빛줄기는 봉헌 사건마다 새로 만들어 겹쳐 그리고, 끝나면 지운다. 겹쳐 바쳐도 줄마다 따로 돈다.
    ///
    /// <b>네트워크를 모른다.</b> <see cref="AltarOfferingRelay"/> 가 봉헌 성공 알림을 받으면 부른다.
    /// 그래서 로비의 모두가 본다. 카메라 연출(<see cref="AltarOfferCinematic"/>)은 바친 사람에게만 있다.
    ///
    /// 설치: <c>Tools/아라아띠/제단 봉헌 빛줄기 설치</c> — <c>P_HeartAltar</c> 프리팹에 이 부품과 재질을 붙인다.
    /// </summary>
    public sealed class AltarOfferBeam : MonoBehaviour
    {
        [Header("연결 (설치 메뉴가 채운다)")]
        [Tooltip("투명 재질. 설치 메뉴가 만든다(AltarOfferBeam.mat).")]
        [SerializeField] private Material material;

        [Tooltip("빛줄기가 시작할 제단 돌기둥. 이 렌더러의 꼭대기 한가운데에서 솟는다. 비면 이 오브젝트 위치.")]
        [SerializeField] private Renderer origin;

        [Header("모양")]
        [SerializeField] private Color color = new Color(1f, 0.74f, 0.12f, 1f);
        [SerializeField, Min(1f)] private float height = 60f;
        [SerializeField, Min(0.01f)] private float coreWidth = 0.16f;
        [SerializeField, Min(0.01f)] private float glowWidth = 0.8f;
        [SerializeField, Range(0f, 1f)] private float glowAlpha = 0.55f;
        [Tooltip("꼭대기에서의 굵기 배율. 1 이면 위아래가 같다.")]
        [SerializeField, Range(0f, 1f)] private float topWidthScale = 0.35f;

        [Header("시간 (초)")]
        [SerializeField, Min(0.05f)] private float riseSeconds = 0.7f;
        [SerializeField, Min(0f)] private float holdSeconds = 1.2f;
        [SerializeField, Min(0.05f)] private float fadeSeconds = 0.9f;

        /// <summary>한 번 재생에 걸리는 전체 시간(초). 카메라 연출이 이만큼 머문다.</summary>
        public float Duration => riseSeconds + holdSeconds + fadeSeconds;

        /// <summary>지금 로비에 있는 빛줄기. 없으면 null(미니게임 · 서버).</summary>
        public static AltarOfferBeam Current { get; private set; }

        /// <summary>빛줄기가 솟는 자리. 카메라 연출이 이곳을 겨눈다.</summary>
        public Vector3 BasePosition
        {
            get
            {
                if (origin == null) return transform.position;
                Bounds b = origin.bounds;
                return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        /// <summary>
        /// 동시에 살아 있을 수 있는 빛줄기 수. 악성 클라이언트가 봉헌 알림을 도배해도 선이 끝없이 늘지 않게.
        /// (<see cref="AltarVfxController"/> 의 펄스 상한과 같은 이유)
        /// </summary>
        private const int MaxConcurrent = 6;
        private int live;

        private void OnEnable()
        {
            // 화면이 없는 서버에서는 만들 것도 없다. 받는 쪽은 Current == null 을 보고 넘어간다.
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
            {
                return;
            }

            Current = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

        /// <summary>빛줄기를 한 번 쏜다.</summary>
        public void Play()
        {
            if (!isActiveAndEnabled || material == null || live >= MaxConcurrent)
            {
                return;
            }

            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            live++;

            var root = new GameObject("OfferBeam");
            root.transform.SetParent(transform, worldPositionStays: true);

            LineRenderer glow = MakeLine(root.transform, "Glow", glowWidth);
            LineRenderer core = MakeLine(root.transform, "Core", coreWidth);

            try
            {
                Vector3 bottom = BasePosition;
                float t = 0f;

                // 솟기 — 끝으로 갈수록 느려진다(ease-out). 쭉 뻗어 나가는 느낌.
                while (t < riseSeconds)
                {
                    t += Time.deltaTime;
                    float k = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / riseSeconds), 3f);
                    Draw(core, glow, bottom, height * k, 1f);
                    yield return null;
                }

                Draw(core, glow, bottom, height, 1f);
                yield return new WaitForSeconds(holdSeconds);

                // 사라지기 — 길이는 두고 투명해진다.
                t = 0f;
                while (t < fadeSeconds)
                {
                    t += Time.deltaTime;
                    Draw(core, glow, bottom, height, 1f - Mathf.Clamp01(t / fadeSeconds));
                    yield return null;
                }
            }
            finally
            {
                Destroy(root);
                live--;
            }
        }

        /// <summary>
        /// 다 뻗은 빛줄기를 멈춘 채로 만든다. <b>에디터 미리보기용</b>(<c>제단 봉헌 빛줄기 미리보기</c>)이다.
        /// 게임에서는 <see cref="Play"/> 를 쓴다. 돌려받은 오브젝트는 부른 쪽이 지운다.
        /// </summary>
        public GameObject BuildStill()
        {
            var root = new GameObject("OfferBeamStill");
            root.transform.SetParent(transform, worldPositionStays: true);
            LineRenderer glow = MakeLine(root.transform, "Glow", glowWidth);
            LineRenderer core = MakeLine(root.transform, "Core", coreWidth);
            Draw(core, glow, BasePosition, height, 1f);
            return root;
        }

        private LineRenderer MakeLine(Transform parent, string name, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, worldPositionStays: false);

            var line = go.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.numCapVertices = 4;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.widthMultiplier = width;
            line.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, topWidthScale));
            return line;
        }

        /// <param name="alpha">1 이면 원래 밝기, 0 이면 안 보인다.</param>
        private void Draw(LineRenderer core, LineRenderer glow, Vector3 bottom, float length, float alpha)
        {
            Vector3 top = bottom + Vector3.up * Mathf.Max(0.01f, length);

            core.SetPosition(0, bottom);
            core.SetPosition(1, top);
            glow.SetPosition(0, bottom);
            glow.SetPosition(1, top);

            // 심은 가운데가 거의 흰색에 가깝게 밝히고, 위로 갈수록 옅어진다.
            Color coreColor = Color.Lerp(color, Color.white, 0.25f);
            core.colorGradient = Fade(coreColor, alpha, alpha * 0.5f);
            glow.colorGradient = Fade(color, alpha * glowAlpha, 0f);
        }

        private static Gradient Fade(Color c, float bottomAlpha, float topAlpha)
        {
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(c, 0f), new GradientColorKey(c, 1f) },
                new[] { new GradientAlphaKey(bottomAlpha, 0f), new GradientAlphaKey(topAlpha, 1f) });
            return g;
        }
    }
}
