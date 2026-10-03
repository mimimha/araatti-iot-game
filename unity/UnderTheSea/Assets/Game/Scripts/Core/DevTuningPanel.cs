#if DEVELOPMENT_BUILD || UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnderTheSea.Core
{
    /// <summary>
    /// **성능을 한자리에서 재는 창.** 개발 빌드에서만 뜬다.
    ///
    /// <b>왜 만들었나.</b> 로비가 무거운 이유를 찾는데, 설정을 바꾸려면 에셋을 고치고 다시
    /// 구워야 했다. 한 항목에 10분씩 드니 여섯 항목이면 한 시간이다. 그 사이에 <b>서 있는
    /// 자리와 보는 방향이 달라져서</b> 숫자를 비교할 수가 없었다. 실제로 컬링을 껐는데
    /// GPU 가 더 낮게 나온 적이 있다.
    ///
    /// 그래서 <b>한자리에 서서 키만 눌러</b> 바꾸고, 바뀔 때마다 알아서 재도록 했다.
    ///
    /// <b>ms 로 잰다.</b> GPU 사용률로 보면 안 된다. 100% 에 붙어 버리면 더 나빠져도 100%,
    /// 조금 좋아져도 100% 라 개선이 안 보인다. 한 프레임에 몇 ms 걸리는지가 진짜 값이다.
    ///
    /// <code>
    ///   F9    이 창 열기 / 닫기        ⚠ 창이 열려 있어야 숫자키가 먹는다
    ///   F8    프레임 제한 순환          30 → 60 → 90 → 120 → 무제한
    ///   F10   풀 컬링 켜기 / 끄기
    ///   F11   컬링 거리 -5m
    ///   F12   컬링 거리 +5m
    ///
    ///   1     SSAO
    ///   2     그림자 품질               High / Medium / Low
    ///   3     그림자 캐스케이드          4 / 2 / 1
    ///   4     불투명 화면 복사           물이 쓸 수 있다. 끄고 물을 봐라
    ///   5     깊이 텍스처
    ///   6     후처리 전체
    ///   7     RenderScale               1.0 / 0.8 / 0.67 / 0.5
    ///   8     업스케일러                 Auto / Linear / FSR / STP
    ///   9     물 숨기기
    ///   0     기록 지우고 기준값 다시 잡기
    /// </code>
    ///
    /// <b>재는 방법.</b> 무엇이든 바꾸면 <see cref="Warmup"/> 초 쉬었다가
    /// <see cref="Window"/> 초 동안 재서 한 줄로 쌓는다. 첫 줄이 기준값이 되고 그 뒤로는
    /// 기준값과의 차이를 같이 보여 준다. <b>한자리에 가만히 서서</b> 눌러야 한다.
    ///
    /// <b>결과는 파일로도 남는다.</b> 실행 파일 옆 <c>성능측정.txt</c> 에 쌓인다.
    /// 화면을 캡처해 옮겨 적지 않아도 되고, 게임을 껐다 켜도 같은 파일에 이어 붙는다.
    ///
    /// <b>원복했는지 화면이 알려 준다.</b> 2·3·7·8 은 켜고 끄는 것이 아니라 값이 돌기
    /// 때문에 지금이 원래값인지 알기 어렵다. 창 가운데 <b>"바뀐 것"</b> 줄에 원래와 다른
    /// 것만 노랗게 적히고, 전부 원래대로면 초록색으로 "바뀐 것 없음" 이 뜬다.
    /// <b>다음 항목을 재기 전에 이 줄이 초록인지 보라.</b> 두 항목이 겹치면 측정이 망한다.
    ///
    /// ⚠ <b>비교할 땐 F8 로 무제한을 골라라.</b> 60 으로 묶어 두면 어떤 설정이든 16.7ms 가
    ///    나와서 차이가 안 보인다.
    ///
    /// ⚠ <b>에디터에서는 파이프라인 설정을 안 바꾼다.</b> 씬 오브젝트를 고친 것은 플레이를
    ///    멈추면 되돌아가지만, <c>PC_RPAsset.asset</c> 같은 <b>에셋을 고친 것은 그대로
    ///    남는다.</b> 모르고 커밋될 수 있어서 빌드에서만 먹게 막아 두었다.
    ///    물 숨기기와 후처리 끄기는 씬 쪽이라 에디터에서도 된다.
    ///
    /// ⚠ <b>평소에는 꺼져 있다.</b> <c>-perfpanel</c> 을 줘야 뜬다.
    ///    설정이 정해진 뒤에는 화면만 가리기 때문이다. 다시 잴 일이 생기면 그 인자만 붙인다.
    /// <code>
    ///   AraAtti-Flow.exe -perfpanel -api http://43.202.67.137:5080 -appver prod -region kr
    /// </code>
    ///
    /// ⚠ <b>Release 빌드에는 들어가지 않는다.</b> 파일 전체가 <c>DEVELOPMENT_BUILD</c>
    ///    안에 있다. 시연용 Showcase 빌드에서는 인자를 줘도 이 창이 존재하지 않는다.
    ///
    /// <b>여기서 알 수 있는 것과 없는 것.</b> 이 창은 "어느 항목이 몇 ms 인가"까지만
    /// 알려 준다. 그 안에서 어느 드로우콜이 비싼지는 에디터의 Frame Debugger 로 봐야 한다.
    /// 다만 먼저 여기서 범인을 좁히고 들어가는 편이 낫다. 안 그러면 볼 것이 너무 많다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DevTuningPanel : MonoBehaviour
    {
        private const string HostName = "[성능 조절판]";

        /// <summary>바꾼 직후 몇 초를 버리는가. 셰이더 컴파일과 첫 프레임 튐을 뺀다.</summary>
        private const float Warmup = 1.0f;

        /// <summary>몇 초 동안 재는가. 짧으면 흔들리고 길면 답답하다.</summary>
        private const float Window = 3.0f;

        private static readonly int[] FrameRates = { 30, 60, 90, 120, 0 };
        private static readonly float[] RenderScales = { 1.0f, 0.8f, 0.67f, 0.5f };
        private static readonly int[] Cascades = { 4, 2, 1 };

        /// <summary>
        /// 고를 수 있는 업스케일러.
        ///
        /// ⚠ <b>STP 는 뺐다.</b> 이 프로젝트에서 고르면 렌더 그래프가 예외로 죽는다.
        ///    처음에는 3.3 ms 라는 말도 안 되는 값이 나왔고(장면을 안 그린 프레임을 잰 것),
        ///    두 번째에는 클라이언트가 아예 뻗었다. 두 번 다 같은 자리다.
        /// <code>
        ///   NullReferenceException
        ///     UnityEngine.Rendering.STP.Execute (STP.cs:1111)
        ///     UnityEngine.Rendering.Universal.StpUtils.Execute (StpUtils.cs:142)
        /// </code>
        ///    원인은 특정하지 못했다. 모션 벡터 쪽이 의심되지만 예외만으로는 단정할 수 없다.
        ///    고칠 일이 생기면 그때 다시 넣는다. 지금은 재는 데 방해만 된다.
        ///
        /// Point 도 뺐다. 3D 에서는 계단만 커져서 쓸 일이 없다.
        /// </summary>
        private static readonly UpscalingFilterSelection[] Upscalers =
        {
            UpscalingFilterSelection.Auto,
            UpscalingFilterSelection.Linear,
            UpscalingFilterSelection.FSR,
        };

        /// <summary>16.7 ms = 60 fps. 이 값을 넘은 프레임의 비율을 센다.</summary>
        private const float Budget = 1000f / 60f;

        /// <summary>긴 측정은 몇 초 동안 재는가. 후보를 좁힌 뒤 최종 선택에 쓴다.</summary>
        private const float LongWindow = 20f;

        /// <summary>
        /// 한 번 잰 결과 한 줄.
        ///
        /// ⚠ <b>여기의 ms 는 GPU 시간이 아니다.</b> <c>Time.unscaledDeltaTime</c> 로 잰
        ///    <b>프레임 간격 전체</b>다. CPU·GPU·대기가 다 섞여 있다.
        ///    "SSAO 를 끄니 프레임 시간이 5.8 ms 줄었다" 는 말은 되지만,
        ///    "SSAO 가 GPU 에서 5.8 ms 를 쓴다" 는 말은 안 된다. 그건 GPU Profiler 로 봐야 한다.
        ///
        /// <b>평균만 보면 안 된다.</b> 혼자 서서 평균 16.7 ms 를 겨우 맞추면, 움직이거나
        /// 사람이 늘었을 때 버틸 여유가 없다. 그래서 상위 5%(<see cref="P95Ms"/>)와
        /// 예산 초과 비율(<see cref="OverPct"/>)을 같이 남긴다.
        /// </summary>
        private struct Row
        {
            public string Label;
            public float AvgMs;

            /// <summary>느린 쪽 5% 가 시작되는 지점. 평균보다 체감에 가깝다.</summary>
            public float P95Ms;

            public float WorstMs;

            /// <summary>16.7 ms 를 넘은 프레임의 비율(%).</summary>
            public float OverPct;

            /// <summary>몇 초 동안 쟀는가. 3초와 20초를 구분해 읽으려고 남긴다.</summary>
            public float Seconds;
        }

        private bool open = true;
        private int frameIndex;
        private float lastDistance = 35f;

        // ── 재기 ──────────────────────────────────────────────────────────
        private readonly List<Row> rows = new List<Row>();
        private readonly List<float> samples = new List<float>();
        private string pendingLabel;
        private float sampleFrom;
        private float sampleUntil;

        /// <summary>재는 도중에 키를 눌렀을 때 "기다리세요" 를 띄워 둘 시각.</summary>
        private float blockedUntil;

        /// <summary>프레임이 묶여 있어 측정을 거절했을 때 띄워 둘 시각.</summary>
        private float cappedUntil;

        /// <summary>
        /// 프레임이 묶여 있는가.
        ///
        /// ⚠ <b>묶인 채로 재면 전부 같은 값이 나온다.</b> VSync 가 켜져 있으면 모니터
        ///    주사율에, <c>targetFrameRate</c> 가 걸려 있으면 그 값에 묶인다. 실제로
        ///    그림자 측정이 16.7 ms(= 정확히 60fps)로 나와서 차이를 못 본 적이 있다.
        ///    VSync 는 게임 시작 시 켜져 있을 수 있어서 F8 을 누르기 전까지 걸린다.
        /// </summary>
        private static bool Capped =>
            QualitySettings.vSyncCount > 0 || Application.targetFrameRate > 0;

        // 화면에 늘 띄워 두는 값. 재는 것과 별개다.
        private float smoothedFps;
        private float worstMs;
        private float worstResetAt;

        // ── 되돌리기 ──────────────────────────────────────────────────────
        private bool restoreCaptured;
        private bool origSsao;
        private bool origSsaoDownsample;
        private float origRenderScale;
        private int origCascades;
        private bool origOpaque;
        private bool origDepth;
        private SoftShadowQuality origShadowQuality;
        private UpscalingFilterSelection origUpscaler;

        // ── 지금 고른 것 ──────────────────────────────────────────────────
        private int shadowQualityIndex;
        private int cascadeIndex;
        private int renderScaleIndex;
        private int upscalerIndex;
        private bool postOn = true;
        private bool waterOn = true;

        /// <summary>
        /// 잰 값을 적어 두는 파일. 실행 파일 옆에 생긴다.
        ///
        /// 화면을 캡처해 옮겨 적지 않아도 되도록 파일로 남긴다. 게임을 껐다 켜도 이어서
        /// 쌓이므로, 여러 번에 나눠 재도 한 파일에 모인다.
        /// </summary>
        private string logPath;

        /// <summary>
        /// 파이프라인 설정을 건드려도 되는가.
        ///
        /// 에디터에서는 안 된다. 위 클래스 주석의 경고를 보라.
        /// </summary>
        private static bool CanTouchPipeline => !Application.isEditor;

        private static UniversalRenderPipelineAsset Urp =>
            GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;

        /// <summary>
        /// 이 인자를 줘야 창이 뜬다. <c>AraAtti-Flow.exe -perfpanel</c>
        ///
        /// <b>평소에는 꺼 둔다.</b> 설정을 정하고 나면 이 창은 화면만 가린다. 그렇다고
        /// 지우면 다음에 다시 잴 때 처음부터 만들어야 하므로, 켜고 끄는 스위치만 뒀다.
        /// </summary>
        private const string EnableKey = "-perfpanel";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return;
            }

            // 인자가 없으면 아예 만들지 않는다. 키 입력을 가로채지도, OnGUI 를 돌지도 않는다.
            if (!FusionLaunchArguments.HasFlag(EnableKey))
            {
                return;
            }

            var host = new GameObject(HostName);
            DontDestroyOnLoad(host);
            host.AddComponent<DevTuningPanel>();
        }

        private void Start()
        {
            int current = Application.targetFrameRate;
            for (int i = 0; i < FrameRates.Length; i++)
            {
                if (FrameRates[i] == current || (current <= 0 && FrameRates[i] == 0))
                {
                    frameIndex = i;
                    break;
                }
            }

            if (FoliageCulling.Distance > 0f)
            {
                lastDistance = FoliageCulling.Distance;
            }

            Capture();
            OpenLog();

            // ⚠ 여기서 바로 재면 안 된다. 이 시점은 씬 로딩 직후라 프레임 하나가 몇 초씩
            //    걸린다. 실제로 기준값이 2984 ms 로 잡힌 적이 있다. 로비에 들어가 자리를
            //    잡은 뒤 사람이 0 을 눌러야 한다.
        }

        /// <summary>
        /// 기록 파일을 열고 머리말을 적는다.
        ///
        /// ⚠ 파일을 못 쓰는 자리(권한 없는 폴더)일 수 있으므로 실패해도 게임은 계속 돈다.
        ///    그때는 <see cref="logPath"/> 가 비고 화면에 "파일 못 씀" 이라고 뜬다.
        /// </summary>
        private void OpenLog()
        {
            try
            {
                // Application.dataPath 는 <빌드폴더>/AraAtti-Flow_Data 다. 그 위가 빌드 폴더.
                string folder = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(folder))
                {
                    return;
                }

                logPath = Path.Combine(folder, "성능측정.txt");

                var head = new StringBuilder();
                head.AppendLine();
                head.AppendLine("══════════════════════════════════════════════════");
                head.AppendLine("  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                head.AppendLine("  " + StateLine());
                head.AppendLine("  ms = Time.unscaledDeltaTime (프레임 간격 전체, GPU 시간 아님)");
                head.AppendLine("  초과% = 16.7ms(60fps)를 넘긴 프레임 비율");
                head.AppendLine("══════════════════════════════════════════════════");

                File.AppendAllText(logPath, head.ToString(), Encoding.UTF8);
            }
            catch (Exception e)
            {
                logPath = null;
                Debug.LogWarning("[성능 조절판] 기록 파일을 못 엽니다: " + e.Message);
            }
        }

        /// <summary>머리말에 적을 한 줄. 지금 실제로 돌고 있는 설정이다.</summary>
        private string StateLine()
        {
            var urp = Urp;
            string res = $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}";

            if (urp == null)
            {
                return res + " / URP 못 찾음";
            }

            return res +
                   $" / RenderScale {urp.renderScale:0.00} {urp.upscalingFilter}" +
                   $" / SSAO {SsaoText()}" +
                   $" / 그림자 {ReadShadowQuality()} {urp.shadowCascadeCount}단" +
                   $" / MSAA {urp.msaaSampleCount}x HDR {(urp.supportsHDR ? "켬" : "끔")}" +
                   $" / 불투명 {(urp.supportsCameraOpaqueTexture ? "켬" : "끔")}" +
                   $" 깊이 {(urp.supportsCameraDepthTexture ? "켬" : "끔")}" +
                   $" / 카메라 {Camera.allCamerasCount}대";
        }

        /// <summary>바꾸기 전 값을 적어 둔다. 창을 닫거나 게임이 끝나면 되돌린다.</summary>
        private void Capture()
        {
            var urp = Urp;
            if (urp == null || restoreCaptured)
            {
                return;
            }

            origRenderScale = urp.renderScale;
            origCascades = urp.shadowCascadeCount;
            origOpaque = urp.supportsCameraOpaqueTexture;
            origDepth = urp.supportsCameraDepthTexture;
            origUpscaler = urp.upscalingFilter;
            origShadowQuality = ReadShadowQuality();

            ScriptableRendererFeature ssao = FindSsao();
            origSsao = ssao != null && ssao.isActive;
            origSsaoDownsample = ReadSsaoDownsample();

            // 지금 값이 후보 목록의 어디인지 찾아 둔다. 그래야 첫 키 입력이 자연스럽다.
            renderScaleIndex = IndexOf(RenderScales, origRenderScale);
            cascadeIndex = IndexOf(Cascades, origCascades);
            upscalerIndex = IndexOf(Upscalers, origUpscaler);
            shadowQualityIndex = (int)origShadowQuality;

            restoreCaptured = true;
        }

        private void OnDestroy() => Restore();

        private void OnApplicationQuit() => Restore();

        /// <summary>
        /// 건드린 것을 원래대로 되돌린다.
        ///
        /// 빌드에서는 어차피 프로세스가 죽으면 그만이지만, 에디터에서 혹시 파이프라인을
        /// 건드린 경우를 대비해 남겨 둔다.
        /// </summary>
        private void Restore()
        {
            var urp = Urp;
            if (urp == null || !restoreCaptured)
            {
                return;
            }

            urp.renderScale = origRenderScale;
            urp.shadowCascadeCount = origCascades;
            urp.supportsCameraOpaqueTexture = origOpaque;
            urp.supportsCameraDepthTexture = origDepth;
            urp.upscalingFilter = origUpscaler;
            WriteShadowQuality(origShadowQuality);

            ScriptableRendererFeature ssao = FindSsao();
            if (ssao != null)
            {
                ssao.SetActive(origSsao);
            }

            WriteSsaoDownsample(origSsaoDownsample);
            SetWater(true);
            SetPostProcessing(true);
        }

        private void Update()
        {
            Sample();

            if (Input.GetKeyDown(KeyCode.F9)) open = !open;
            if (Input.GetKeyDown(KeyCode.F8)) CycleFrameRate();

            // 긴 측정은 설정을 안 바꾸므로 재는 도중만 아니면 언제든 받는다.
            if (Input.GetKeyDown(KeyCode.F7) && pendingLabel == null) MeasureLong();

            if (Input.GetKeyDown(KeyCode.F10)) ToggleCulling();
            if (Input.GetKeyDown(KeyCode.F11)) Nudge(-5f);
            if (Input.GetKeyDown(KeyCode.F12)) Nudge(+5f);

            // 숫자키는 창이 열려 있을 때만 먹는다. 게임 조작과 겹치지 않게.
            if (!open)
            {
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha0))
            {
                ResetRows();
                return;
            }

            // ⚠ **재는 도중에는 아무것도 못 바꾸게 막는다.**
            //    앞서 1 을 누르고 4초가 지나기 전에 2 를 눌러서, SSAO 측정이 버려진 채
            //    SSAO 와 그림자가 둘 다 바뀐 상태가 된 적이 있다. 그러면 어느 쪽 효과인지
            //    알 수가 없다. 키를 씹고 화면에 남은 시간을 띄우는 편이 낫다.
            if (pendingLabel != null)
            {
                if (AnySettingKey())
                {
                    blockedUntil = Time.unscaledTime + 1.5f;
                }

                return;
            }

            // 묶여 있으면 설정을 바꾸지도 않는다. 바꿔 놓고 못 재면 상태만 어긋난다.
            if (Capped && AnySettingKey())
            {
                cappedUntil = Time.unscaledTime + 3f;
                return;
            }

            if (Input.GetKeyDown(KeyCode.Alpha1)) CycleSsao();
            if (Input.GetKeyDown(KeyCode.Alpha2)) CycleShadowQuality();
            if (Input.GetKeyDown(KeyCode.Alpha3)) CycleCascades();
            if (Input.GetKeyDown(KeyCode.Alpha4)) ToggleOpaque();
            if (Input.GetKeyDown(KeyCode.Alpha5)) ToggleDepth();
            if (Input.GetKeyDown(KeyCode.Alpha6)) TogglePost();
            if (Input.GetKeyDown(KeyCode.Alpha7)) CycleRenderScale();
            if (Input.GetKeyDown(KeyCode.Alpha8)) CycleUpscaler();
            if (Input.GetKeyDown(KeyCode.Alpha9)) ToggleWater();
        }

        private static bool AnySettingKey()
        {
            return Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Alpha2)
                || Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Alpha4)
                || Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Alpha6)
                || Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Alpha8)
                || Input.GetKeyDown(KeyCode.Alpha9);
        }

        // ── 재기 ──────────────────────────────────────────────────────────

        /// <summary>이 이름으로 한 번 잰다. 재는 도중에 또 부르면 앞의 것은 버린다.</summary>
        private void Measure(string label, float window = Window)
        {
            // 묶여 있으면 아예 재지 않는다. 쓸모없는 줄을 쌓는 것보다 거절하는 편이 낫다.
            if (Capped)
            {
                cappedUntil = Time.unscaledTime + 3f;
                return;
            }

            pendingLabel = label;
            samples.Clear();
            sampleFrom = Time.unscaledTime + Warmup;
            sampleUntil = sampleFrom + window;
        }

        /// <summary>
        /// 지금 상태 그대로 <see cref="LongWindow"/> 초 동안 길게 잰다.
        ///
        /// 3초는 큰 차이를 찾는 1차 조사에는 충분하지만 최종 설정을 고르기에는 짧다.
        /// 후보를 둘쯤으로 좁힌 뒤 이걸로 다시 재서 p95 와 예산 초과 비율을 본다.
        /// </summary>
        private void MeasureLong()
        {
            string what = Changed() ?? "원래값";
            Measure("긴측정 " + what, LongWindow);
        }

        private void Sample()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f)
            {
                return;
            }

            // 화면에 늘 띄우는 값
            float fps = 1f / dt;
            smoothedFps = smoothedFps <= 0f ? fps : Mathf.Lerp(smoothedFps, fps, 0.05f);

            float ms = dt * 1000f;
            if (Time.unscaledTime > worstResetAt)
            {
                worstMs = ms;
                worstResetAt = Time.unscaledTime + 3f;
            }
            else if (ms > worstMs)
            {
                worstMs = ms;
            }

            // 기록용
            if (pendingLabel == null || Time.unscaledTime < sampleFrom)
            {
                return;
            }

            if (Time.unscaledTime < sampleUntil)
            {
                samples.Add(ms);
                return;
            }

            Commit();
        }

        private void Commit()
        {
            if (samples.Count == 0)
            {
                pendingLabel = null;
                return;
            }

            float sum = 0f;
            int over = 0;
            foreach (float ms in samples)
            {
                sum += ms;
                if (ms > Budget) over++;
            }

            // 상위 5% 지점. 정렬해서 뽑는다. 샘플이 수백 개라 비용은 무시할 만하다.
            samples.Sort();
            int p95 = Mathf.Clamp(Mathf.FloorToInt(samples.Count * 0.95f), 0, samples.Count - 1);

            var row = new Row
            {
                Label = pendingLabel,
                AvgMs = sum / samples.Count,
                P95Ms = samples[p95],
                WorstMs = samples[samples.Count - 1],
                OverPct = 100f * over / samples.Count,
                Seconds = sampleUntil - sampleFrom,
            };

            rows.Add(row);
            Append(row);

            // 화면에는 최근 것만 남긴다. 파일에는 전부 남으니 잃는 것은 없다.
            if (rows.Count > 12)
            {
                rows.RemoveAt(1); // 기준값(0번)은 남긴다
            }

            pendingLabel = null;
            samples.Clear();
        }

        /// <summary>잰 한 줄을 파일에 덧붙인다.</summary>
        private void Append(Row row)
        {
            if (string.IsNullOrEmpty(logPath))
            {
                return;
            }

            // 기준값과의 차이도 같이 적는다. 나중에 파일만 봐도 순위를 알 수 있게.
            string delta = rows.Count <= 1 || rows[0].AvgMs <= 0f
                ? ""
                : $"  ({(row.AvgMs - rows[0].AvgMs >= 0f ? "+" : "")}{row.AvgMs - rows[0].AvgMs:0.0} ms)";

            try
            {
                File.AppendAllText(
                    logPath,
                    $"{row.Label,-24} 평균 {row.AvgMs,6:0.0}  p95 {row.P95Ms,6:0.0}" +
                    $"  최악 {row.WorstMs,6:0.0}  16.7초과 {row.OverPct,5:0.0}%" +
                    $"  [{row.Seconds:0}초]{delta}\n",
                    Encoding.UTF8);
            }
            catch (Exception e)
            {
                logPath = null;
                Debug.LogWarning("[성능 조절판] 기록을 못 적었습니다: " + e.Message);
            }
        }

        private void ResetRows()
        {
            rows.Clear();
            Measure("기준값");
        }

        // ── 토글 ──────────────────────────────────────────────────────────

        /// <summary>
        /// SSAO 를 <b>켬 → 반해상도 → 끔</b> 세 단계로 돌린다.
        ///
        /// 끄는 것과 켜는 것만 비교하면 "음영을 포기해야 얼마나 빨라지는가" 밖에 모른다.
        /// <b>Downsample 은 음영을 남긴 채</b> AO 계산 해상도를 가로·세로 절반으로 줄인다.
        /// 계산 대상 픽셀이 ¼ 이 된다는 뜻이지, SSAO 비용이나 전체 비용이 ¼ 이 된다는
        /// 뜻은 아니다. 얼마나 줄고 음영이 얼마나 남는지는 재 보고 눈으로 봐야 안다.
        /// </summary>
        private void CycleSsao()
        {
            if (!RequirePipeline()) return;

            ScriptableRendererFeature ssao = FindSsao();
            if (ssao == null)
            {
                rows.Add(new Row { Label = "SSAO 를 못 찾음", AvgMs = -1f });
                return;
            }

            // 켬(전체) → 켬(반해상도) → 끔 → 켬(전체)
            if (ssao.isActive && !ReadSsaoDownsample())
            {
                if (!WriteSsaoDownsample(true))
                {
                    rows.Add(new Row { Label = "SSAO Downsample 을 못 바꿈", AvgMs = -1f });
                    return;
                }

                Measure("SSAO 반해상도");
                return;
            }

            if (ssao.isActive)
            {
                WriteSsaoDownsample(false);
                ssao.SetActive(false);
                Measure("SSAO 끔");
                return;
            }

            ssao.SetActive(true);
            WriteSsaoDownsample(false);
            Measure("SSAO 켬(전체)");
        }

        private void CycleShadowQuality()
        {
            if (!RequirePipeline()) return;

            // UsePipelineSettings(0) 은 건너뛴다. High(3) → Medium(2) → Low(1) 만 돈다.
            shadowQualityIndex = shadowQualityIndex <= 1 ? 3 : shadowQualityIndex - 1;

            var wanted = (SoftShadowQuality)shadowQualityIndex;
            if (!WriteShadowQuality(wanted))
            {
                rows.Add(new Row { Label = "그림자 품질을 못 바꿈", AvgMs = -1f });
                return;
            }

            Measure("그림자 " + wanted);
        }

        private void CycleCascades()
        {
            if (!RequirePipeline()) return;

            cascadeIndex = (cascadeIndex + 1) % Cascades.Length;
            Urp.shadowCascadeCount = Cascades[cascadeIndex];
            Measure("캐스케이드 " + Cascades[cascadeIndex]);
        }

        private void ToggleOpaque()
        {
            if (!RequirePipeline()) return;

            Urp.supportsCameraOpaqueTexture = !Urp.supportsCameraOpaqueTexture;
            Measure("불투명복사 " + (Urp.supportsCameraOpaqueTexture ? "켬" : "끔"));
        }

        private void ToggleDepth()
        {
            if (!RequirePipeline()) return;

            Urp.supportsCameraDepthTexture = !Urp.supportsCameraDepthTexture;
            Measure("깊이텍스처 " + (Urp.supportsCameraDepthTexture ? "켬" : "끔"));
        }

        private void CycleRenderScale()
        {
            if (!RequirePipeline()) return;

            renderScaleIndex = (renderScaleIndex + 1) % RenderScales.Length;
            Urp.renderScale = RenderScales[renderScaleIndex];
            Measure("RenderScale " + RenderScales[renderScaleIndex].ToString("0.00"));
        }

        private void CycleUpscaler()
        {
            if (!RequirePipeline()) return;

            upscalerIndex = (upscalerIndex + 1) % Upscalers.Length;
            Urp.upscalingFilter = Upscalers[upscalerIndex];
            Measure("업스케일러 " + Upscalers[upscalerIndex]);
        }

        /// <summary>후처리는 카메라 쪽 설정이라 에디터에서도 안전하다.</summary>
        private void TogglePost()
        {
            postOn = !postOn;
            SetPostProcessing(postOn);
            Measure("후처리 " + (postOn ? "켬" : "끔"));
        }

        /// <summary>물도 씬 오브젝트라 에디터에서도 안전하다.</summary>
        private void ToggleWater()
        {
            waterOn = !waterOn;
            SetWater(waterOn);
            Measure("물 " + (waterOn ? "켬" : "숨김"));
        }

        private bool RequirePipeline()
        {
            if (CanTouchPipeline && Urp != null)
            {
                return true;
            }

            rows.Add(new Row
            {
                Label = Urp == null ? "URP 에셋을 못 찾음" : "에디터에서는 안 바꿈 (빌드에서)",
                AvgMs = -1f,
            });
            return false;
        }

        // ── 찾기 ──────────────────────────────────────────────────────────

        /// <summary>SSAO 의 지금 상태를 한 단어로. 끔 / 전체해상도 / 반해상도.</summary>
        private static string SsaoText()
        {
            ScriptableRendererFeature ssao = FindSsao();

            if (ssao == null) return "없음";
            if (!ssao.isActive) return "끔";

            return ReadSsaoDownsample() ? "반해상도" : "전체해상도";
        }

        private static ScriptableRendererFeature FindSsao()
        {
            var urp = Urp;
            if (urp == null)
            {
                return null;
            }

            foreach (ScriptableRendererData data in urp.rendererDataList)
            {
                if (data == null)
                {
                    continue;
                }

                foreach (ScriptableRendererFeature feature in data.rendererFeatures)
                {
                    if (feature != null &&
                        feature.GetType().Name.Contains("ScreenSpaceAmbientOcclusion"))
                    {
                        return feature;
                    }
                }
            }

            return null;
        }

        private static void SetPostProcessing(bool on)
        {
            foreach (Camera camera in Camera.allCameras)
            {
                if (camera == null)
                {
                    continue;
                }

                var data = camera.GetComponent<UniversalAdditionalCameraData>();
                if (data != null)
                {
                    data.renderPostProcessing = on;
                }
            }
        }

        /// <summary>이름으로 물을 찾아 렌더러만 끈다. 콜라이더는 그대로 둬야 안 빠진다.</summary>
        private static void SetWater(bool on)
        {
            foreach (Renderer renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Exclude))
            {
                if (renderer == null)
                {
                    continue;
                }

                string n = renderer.gameObject.name;
                if (n.StartsWith("Water") || n == "Water" || n.Contains("WaterPlane"))
                {
                    renderer.enabled = on;
                }
            }
        }

        // ── 리플렉션 ──────────────────────────────────────────────────────
        //
        // softShadowQuality 는 URP 에서 internal 이라 밖에서 못 쓴다.
        // 개발 빌드 전용 도구라 직렬화 필드를 직접 건드린다. 실패하면 조용히 false 를
        // 돌려주고 화면에 "못 바꿈" 으로 적는다. URP 를 올리면 여기가 먼저 깨진다.

        private const string ShadowQualityField = "m_SoftShadowQuality";

        /// <summary>
        /// SSAO 의 Downsample 을 읽고 쓴다.
        ///
        /// <c>ScreenSpaceAmbientOcclusionSettings</c> 가 <c>internal class</c> 고
        /// <c>Downsample</c> 도 <c>internal bool</c> 이라 밖에서 못 쓴다. 개발 빌드 전용
        /// 도구라 직렬화 필드를 직접 건드린다. 패스가 매 프레임
        /// <c>m_CurrentSettings.Downsample</c> 을 읽으므로 다시 만들 필요는 없다.
        /// </summary>
        private static object SsaoSettings()
        {
            ScriptableRendererFeature ssao = FindSsao();
            FieldInfo f = ssao?.GetType().GetField(
                "m_Settings", BindingFlags.Instance | BindingFlags.NonPublic);

            return f?.GetValue(ssao);
        }

        private static FieldInfo DownsampleField(object settings)
        {
            return settings?.GetType().GetField(
                "Downsample", BindingFlags.Instance | BindingFlags.NonPublic);
        }

        private static bool ReadSsaoDownsample()
        {
            object settings = SsaoSettings();
            FieldInfo f = DownsampleField(settings);
            return f != null && (bool)f.GetValue(settings);
        }

        private static bool WriteSsaoDownsample(bool on)
        {
            object settings = SsaoSettings();
            FieldInfo f = DownsampleField(settings);
            if (f == null)
            {
                return false;
            }

            f.SetValue(settings, on);
            return true;
        }

        private static SoftShadowQuality ReadShadowQuality()
        {
            var urp = Urp;
            FieldInfo f = urp?.GetType().GetField(
                ShadowQualityField, BindingFlags.Instance | BindingFlags.NonPublic);

            return f == null ? SoftShadowQuality.Medium : (SoftShadowQuality)f.GetValue(urp);
        }

        private static bool WriteShadowQuality(SoftShadowQuality value)
        {
            var urp = Urp;
            FieldInfo f = urp?.GetType().GetField(
                ShadowQualityField, BindingFlags.Instance | BindingFlags.NonPublic);

            if (f == null)
            {
                return false;
            }

            f.SetValue(urp, value);
            return true;
        }

        // ── 기존 기능 ─────────────────────────────────────────────────────

        private void ToggleCulling()
        {
            if (FoliageCulling.Distance > 0f)
            {
                lastDistance = FoliageCulling.Distance;
                FoliageCulling.SetDistance(0f);
                Measure("풀 컬링 끔");
            }
            else
            {
                FoliageCulling.SetDistance(lastDistance);
                Measure("풀 컬링 " + lastDistance.ToString("0") + "m");
            }
        }

        private void Nudge(float delta)
        {
            if (FoliageCulling.Distance <= 0f)
            {
                return;
            }

            FoliageCulling.SetDistance(Mathf.Clamp(FoliageCulling.Distance + delta, 5f, 400f));
            lastDistance = FoliageCulling.Distance;
            Measure("컬링 " + lastDistance.ToString("0") + "m");
        }

        private void CycleFrameRate()
        {
            frameIndex = (frameIndex + 1) % FrameRates.Length;
            int wanted = FrameRates[frameIndex];

            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = wanted <= 0 ? -1 : wanted;
        }

        private static int IndexOf<T>(IReadOnlyList<T> list, T value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Equals(list[i], value))
                {
                    return i;
                }
            }

            return 0;
        }

        // ── 화면 ──────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!open)
            {
                GUI.Label(new Rect(10f, 10f, 300f, 22f), "F9  성능 조절판");
                return;
            }

            var box = new Rect(10f, 10f, 620f, 540f);
            GUI.Box(box, "성능 조절판");

            GUILayout.BeginArea(new Rect(box.x + 12f, box.y + 24f, box.width - 24f, box.height - 34f));

            DrawState();
            GUILayout.Space(6f);
            DrawKeys();
            GUILayout.Space(6f);
            DrawRows();

            GUILayout.EndArea();
        }

        private void DrawState()
        {
            var urp = Urp;
            int w = Screen.width;
            int h = Screen.height;

            GUILayout.Label($"화면   {w} x {h}   {Screen.fullScreenMode}");

            if (urp == null)
            {
                GUILayout.Label("URP 에셋을 못 찾았습니다.");
                return;
            }

            float s = urp.renderScale;
            GUILayout.Label(
                $"3D     {Mathf.RoundToInt(w * s)} x {Mathf.RoundToInt(h * s)}" +
                $"   (RenderScale {s:0.00}, {urp.upscalingFilter})");

            string ssaoText = SsaoText();

            GUILayout.Label(
                $"SSAO {ssaoText}   그림자 {ReadShadowQuality()}/{urp.shadowCascadeCount}단" +
                $"   MSAA {urp.msaaSampleCount}x   HDR {(urp.supportsHDR ? "켬" : "끔")}");

            GUILayout.Label(
                $"불투명복사 {(urp.supportsCameraOpaqueTexture ? "켬" : "끔")}" +
                $"   깊이 {(urp.supportsCameraDepthTexture ? "켬" : "끔")}" +
                $"   카메라 {Camera.allCamerasCount}대");

            // ⚠ 패널이 고른 값이 아니라 **실제로 묶여 있는지**를 보여 준다.
            //    VSync 는 게임이 시작할 때 켜져 있을 수 있어서, F8 을 누르기 전까지는
            //    패널에 "무제한" 이라 떠 있어도 실제로는 주사율에 묶인다.
            string limit =
                QualitySettings.vSyncCount > 0 ? "VSync 묶임 ⚠"
                : Application.targetFrameRate > 0 ? Application.targetFrameRate + "fps 묶임 ⚠"
                : "무제한";
            string cull = !FoliageCulling.Available ? "레이어없음"
                : FoliageCulling.Distance > 0f ? FoliageCulling.Distance.ToString("0") + "m" : "끔";

            GUILayout.Label($"지금   {smoothedFps:0} fps (최악 {worstMs:0.0} ms)" +
                            $"   제한 {limit}   풀컬링 {cull}");

            // ⚠ 이 줄이 제일 중요하다. 2·3·7·8 은 켜고 끄는 것이 아니라 값이 돌기 때문에
            //    지금이 원래값인지 눈으로 알기 어렵다. 여기에 다른 것만 적어 준다.
            string changed = Changed();
            GUI.color = changed == null ? Color.green : Color.yellow;
            GUILayout.Label(changed == null
                ? "바뀐 것 없음 — 전부 원래값입니다"
                : "바뀐 것:  " + changed);
            GUI.color = Color.white;

            GUILayout.Label(string.IsNullOrEmpty(logPath)
                ? "기록 파일을 못 씁니다 (화면 값만 보세요)"
                : "기록 → " + logPath);

            if (!CanTouchPipeline)
            {
                GUILayout.Label("⚠ 에디터라 파이프라인 설정은 안 바뀝니다. 빌드에서 재세요.");
            }
        }

        /// <summary>
        /// 지금 원래값과 다른 것들. 전부 원래대로면 <c>null</c>.
        ///
        /// <b>왜 필요한가.</b> SSAO·물처럼 켜고 끄는 것은 화면만 봐도 알지만, 그림자 품질과
        /// 캐스케이드·RenderScale·업스케일러는 <b>값이 돌아가는</b> 방식이라 지금이 원래값인지
        /// 알기 어렵다. 원복한 줄 알고 다음 항목을 재면 두 항목이 겹쳐서 측정이 망한다.
        /// </summary>
        private string Changed()
        {
            var urp = Urp;
            if (urp == null || !restoreCaptured)
            {
                return null;
            }

            var parts = new List<string>();

            ScriptableRendererFeature ssao = FindSsao();
            if (ssao != null && ssao.isActive != origSsao)
            {
                parts.Add("SSAO " + (ssao.isActive ? "켬" : "끔"));
            }
            else if (ssao != null && ssao.isActive && ReadSsaoDownsample() != origSsaoDownsample)
            {
                parts.Add("SSAO " + (ReadSsaoDownsample() ? "반해상도" : "전체해상도"));
            }

            if (ReadShadowQuality() != origShadowQuality)
            {
                parts.Add($"그림자 {ReadShadowQuality()}(원래 {origShadowQuality})");
            }

            if (urp.shadowCascadeCount != origCascades)
            {
                parts.Add($"캐스케이드 {urp.shadowCascadeCount}(원래 {origCascades})");
            }

            if (urp.supportsCameraOpaqueTexture != origOpaque)
            {
                parts.Add("불투명복사 " + (urp.supportsCameraOpaqueTexture ? "켬" : "끔"));
            }

            if (urp.supportsCameraDepthTexture != origDepth)
            {
                parts.Add("깊이 " + (urp.supportsCameraDepthTexture ? "켬" : "끔"));
            }

            if (!Mathf.Approximately(urp.renderScale, origRenderScale))
            {
                parts.Add($"스케일 {urp.renderScale:0.00}(원래 {origRenderScale:0.00})");
            }

            if (urp.upscalingFilter != origUpscaler)
            {
                parts.Add($"업스케일러 {urp.upscalingFilter}(원래 {origUpscaler})");
            }

            if (!postOn) parts.Add("후처리 끔");
            if (!waterOn) parts.Add("물 숨김");

            return parts.Count == 0 ? null : string.Join(" · ", parts);
        }

        private void DrawKeys()
        {
            GUILayout.Label("1 SSAO(전체→반해상도→끔)   2 그림자   3 캐스케이드   4 불투명복사   5 깊이");
            GUILayout.Label("6 후처리   7 스케일   8 업스케일러   9 물   0 기록지움");
            GUILayout.Label("F7 긴측정 20초 (지금 상태 그대로)   F8 프레임제한   F10 풀컬링   F11/F12 거리");
            GUILayout.Label("ms 는 프레임 간격 전체입니다 (GPU 시간 아님). 초과% 는 16.7ms 를 넘긴 프레임 비율.");
        }

        private void DrawRows()
        {
            if (Time.unscaledTime < cappedUntil)
            {
                GUI.color = Color.red;
                GUILayout.Label("✋ 프레임이 묶여 있습니다. F8 로 '무제한' 을 먼저 고르세요.");
                GUI.color = Color.white;
            }
            else if (Time.unscaledTime < blockedUntil)
            {
                GUI.color = Color.red;
                GUILayout.Label("✋ 아직 재는 중입니다. 줄이 쌓인 뒤에 누르세요.");
                GUI.color = Color.white;
            }

            if (pendingLabel != null)
            {
                bool warming = Time.unscaledTime < sampleFrom;
                GUI.color = Color.cyan;
                GUILayout.Label(warming
                    ? $"⏳  {pendingLabel} — 안정되기를 기다리는 중… 움직이지 마세요"
                    : $"⏳  {pendingLabel} — 재는 중  {sampleUntil - Time.unscaledTime:0.0}초 남음");
                GUI.color = Color.white;
            }

            if (rows.Count == 0)
            {
                GUI.color = Capped ? Color.red : Color.yellow;
                GUILayout.Label(Capped
                    ? "① F8 을 눌러 '제한 무제한' 으로 맞추세요   ② 0 으로 기준값"
                    : "0 을 눌러 기준값을 잡으세요. (한자리에 서서)");
                GUI.color = Color.white;
                return;
            }

            float baseMs = rows[0].AvgMs;

            for (int i = 0; i < rows.Count; i++)
            {
                Row r = rows[i];

                if (r.AvgMs < 0f)
                {
                    GUILayout.Label($"  {r.Label}");
                    continue;
                }

                string delta = i == 0 || baseMs <= 0f
                    ? ""
                    : $" {(r.AvgMs - baseMs >= 0f ? "+" : "")}{r.AvgMs - baseMs:0.0}";

                // 예산을 넘긴 프레임이 많으면 평균이 좋아도 체감은 나쁘다. 빨갛게 띄운다.
                GUI.color = r.OverPct > 10f ? new Color(1f, 0.6f, 0.6f) : Color.white;
                GUILayout.Label(
                    $"  {r.Label,-20} 평균{r.AvgMs,5:0.0} p95{r.P95Ms,5:0.0}" +
                    $" 최악{r.WorstMs,5:0.0} 초과{r.OverPct,4:0}%{delta}");
                GUI.color = Color.white;
            }
        }
    }
}
#endif
