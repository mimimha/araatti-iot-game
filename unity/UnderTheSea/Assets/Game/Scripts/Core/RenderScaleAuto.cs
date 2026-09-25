using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnderTheSea.Core
{
    /// <summary>
    /// **화면이 크면 3D 를 조금 작게 그린다.** 글씨는 안 건드린다.
    ///
    /// <b>왜 필요한가.</b> 로비가 무거운 이유는 모델이 복잡해서가 아니라 <b>픽셀 수</b> 때문이다.
    /// 실측으로 확인했다. (RTX 4050 노트북, 2880×1800 전체화면, 같은 자리에서 잰 값)
    ///
    /// <code>
    ///   RenderScale 1.00   25.8 ms   16.7ms 초과 100%     ← 모든 프레임이 60fps 예산 초과
    ///               0.80   14.1 ms   초과   0%
    ///               0.67   11.9 ms   초과   0%
    ///               0.50    9.0 ms   초과   0%
    /// </code>
    ///
    /// 다른 설정은 이만큼 못 준다. 같은 조건에서 SSAO 를 아예 꺼도 3.8 ms, 그림자를
    /// High→Low 로 내려도 2.1 ms 였다. <b>해상도 하나가 나머지 전부를 합친 것보다 크다.</b>
    ///
    /// <b>왜 고정 비율로 하지 않는가.</b> RenderScale 은 곱하기라서, 화면이 작아도 똑같이
    /// 깎는다. 그런데 화면이 작으면 애초에 깎을 이유가 없다.
    ///
    /// <code>
    ///   1920×1080  2.07M 픽셀   그냥도 100fps 쯤 난다   →  깎으면 화질만 손해
    ///   2880×1800  5.18M 픽셀   깎아야 60fps 가 나온다
    /// </code>
    ///
    /// 그래서 비율이 아니라 <b>내부 해상도 상한</b>(<see cref="TargetPixels"/>)을 둔다.
    /// 상한을 넘는 화면만 그 상한에 맞춰 줄이고, 넘지 않으면 아무것도 안 한다.
    ///
    /// <b>UI 는 안 줄어든다.</b> RenderScale 은 3D 를 그리는 그림판 크기만 바꾼다.
    /// Screen Space Overlay 캔버스는 그 위에 화면 해상도 그대로 합성되므로,
    /// 채팅 글씨와 숫자는 선명함을 유지한다. 화질 저하를 사람이 가장 먼저 알아채는 것이
    /// 글씨라, 이 점이 중요하다.
    ///
    /// ⚠ <b>화면 크기는 도중에 바뀐다.</b> Alt+Enter 로 창↔전체화면을 오가면 Unity 가
    ///    전체화면을 화면 기본 해상도로 다시 잡는다. 실행 인자로 준 해상도는 이걸 못 버틴다.
    ///    그래서 주기적으로 다시 본다.
    ///
    /// ⚠ <b>에디터에서는 URP 에셋을 실제로 고친다.</b> 씬 오브젝트와 달리 에셋 변경은
    ///    플레이를 멈춰도 되돌아가지 않는다. 그래서 <see cref="OnApplicationQuit"/> 와
    ///    <see cref="OnDisable"/> 에서 원래 값으로 돌려놓는다. 보통은 에디터 Game 뷰가
    ///    상한보다 작아 1.0 이 나오므로 애초에 바뀌지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RenderScaleAuto : MonoBehaviour
    {
        /// <summary>
        /// 3D 를 그릴 픽셀 수의 상한.
        ///
        /// 2,300,000 은 눈으로 정했다. 2880×1800 에서 이 값이면 스케일 0.67 이 나오는데,
        /// 그 화면을 보고 "이 정도면 됐다" 고 판단했다. 1920×1080(2.07M)은 상한 아래라
        /// 손대지 않는다 — 흔한 해상도를 지키는 것이 이 값을 고른 이유다.
        /// </summary>
        private const int TargetPixels = 2_300_000;

        /// <summary>너무 깎으면 못 봐준다. 0.5 면 픽셀이 ¼ 이다.</summary>
        private const float MinScale = 0.5f;

        /// <summary>자동 계산을 무시하고 직접 정한다. <c>-renderscale 80</c> = 0.80</summary>
        private const string ScaleKey = "-renderscale";

        /// <summary>상한을 바꿔 본다. 천 단위. <c>-renderpixels 3000</c> = 3,000,000</summary>
        private const string TargetKey = "-renderpixels";

        /// <summary>몇 초마다 화면 크기를 다시 보는가.</summary>
        private const float Interval = 1f;

        private const string HostName = "[렌더 스케일]";

        private static UniversalRenderPipelineAsset Urp =>
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        private int targetPixels = TargetPixels;
        private float forced;
        private float next;
        private int lastWidth;
        private int lastHeight;

        private bool captured;
        private float origScale;
        private UpscalingFilterSelection origFilter;

        /// <summary>지금 적용된 배율. 1 이면 줄이지 않고 있다는 뜻.</summary>
        public static float Scale { get; private set; } = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // 서버는 아무것도 그리지 않는다.
            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return;
            }

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            host.AddComponent<RenderScaleAuto>();
        }

        private void Awake()
        {
            // 퍼센트로 받는다. FusionLaunchArguments 에 실수를 읽는 함수가 없다.
            int percent = FusionLaunchArguments.GetInt(ScaleKey, 0, 0, 100);
            forced = percent > 0 ? percent / 100f : 0f;

            int thousands = FusionLaunchArguments.GetInt(TargetKey, 0, 0, 50_000);
            if (thousands > 0)
            {
                targetPixels = thousands * 1000;
            }

            if (Urp == null)
            {
                Debug.LogWarning("[렌더 스케일] URP 에셋을 못 찾아 아무것도 하지 않습니다.");
                enabled = false;
                return;
            }

            Apply(force: true);
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < next)
            {
                return;
            }

            next = Time.unscaledTime + Interval;
            Apply(force: false);
        }

        private void Apply(bool force)
        {
            var urp = Urp;
            if (urp == null)
            {
                return;
            }

            int w = Screen.width;
            int h = Screen.height;

            if (!force && w == lastWidth && h == lastHeight)
            {
                return;
            }

            lastWidth = w;
            lastHeight = h;

            if (!captured)
            {
                origScale = urp.renderScale;
                origFilter = urp.upscalingFilter;
                captured = true;
            }

            float wanted = forced > 0f ? forced : Wanted(w, h);

            // 0.01 단위로 끊는다. 창 크기를 잘게 흔들 때마다 값이 미세하게 바뀌면
            // 파이프라인이 렌더 타깃을 계속 다시 만든다.
            wanted = Mathf.Round(Mathf.Clamp(wanted, MinScale, 1f) * 100f) / 100f;

            urp.renderScale = wanted;
            Scale = wanted;

            // 줄일 때만 FSR 을 쓴다. 1.0 에서는 늘릴 것이 없어 비용만 든다.
            // 실측으로 스케일 1.0 에서 FSR 이 +0.8 ms, 0.80 에서 +0.7 ms 였다.
            // 기본값 Auto 는 이름과 달리 정수 배율이 아니면 그냥 Bilinear 를 쓴다.
            urp.upscalingFilter = wanted < 1f
                ? UpscalingFilterSelection.FSR
                : origFilter;

            Debug.Log(
                $"[렌더 스케일] 화면 {w}x{h} ({w * h / 1_000_000f:0.00}M 픽셀) → " +
                $"배율 {wanted:0.00}, 3D {Mathf.RoundToInt(w * wanted)}x{Mathf.RoundToInt(h * wanted)}" +
                $", 업스케일러 {urp.upscalingFilter}" +
                (forced > 0f ? $"  ({ScaleKey} 로 직접 지정)" : string.Empty));
        }

        /// <summary>상한 안이면 1.0, 넘으면 상한에 맞춘 배율.</summary>
        private float Wanted(int w, int h)
        {
            long pixels = (long)w * h;

            return pixels <= targetPixels
                ? 1f
                : Mathf.Sqrt(targetPixels / (float)pixels);
        }

        private void OnDisable() => Restore();

        private void OnApplicationQuit() => Restore();

        /// <summary>
        /// 에셋을 원래대로 돌려놓는다.
        ///
        /// 빌드에서는 프로세스가 죽으면 그만이라 의미가 없지만, 에디터에서는 플레이를
        /// 멈춰도 에셋 변경이 남기 때문에 필요하다. 모르고 커밋되면 팀 전체 설정이 바뀐다.
        /// </summary>
        private void Restore()
        {
            var urp = Urp;
            if (urp == null || !captured)
            {
                return;
            }

            urp.renderScale = origScale;
            urp.upscalingFilter = origFilter;
            Scale = origScale;
            captured = false;
        }
    }
}
