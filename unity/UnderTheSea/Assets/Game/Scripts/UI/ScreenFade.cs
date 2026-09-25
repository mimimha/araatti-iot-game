using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.UI
{
    /// <summary>
    /// **화면을 잠깐 덮어 가리는 검은 막.** 그 사이에 벌어진 일을 안 보이게 한다.
    ///
    /// <code>
    ///   Cover(...)   어두워짐 → 가운데서 할 일 → 밝아짐
    /// </code>
    ///
    /// <b>왜 필요한가.</b> 이정표로 순간이동할 때 캐릭터는 한 틱 만에 옮겨지지만
    /// <b>카메라는 초당 90 유닛으로 뒤따라간다</b>(<c>ThirdPersonCamera.m_CameraSpeed</c>).
    /// 로비 끝에서 끝이 118m 라 1.3초 동안 화면이 로비를 가로질러 날아간다.
    /// 그 1.3초를 가려 두고 그 안에서 카메라를 제자리에 놓으면 순간이동으로 보인다.
    ///
    /// <b>스스로 설치한다.</b> 씬에 미리 얹어 두지 않는다. 로비 씬은 Fusion 이 러너 전용
    /// 씬으로 인수해 가므로 씬에 놓아 둔 것은 어느 씬에 있게 될지가 실행 경로마다 다르다.
    /// <see cref="SignpostTeleportUI"/> 와 같은 방식이다.
    ///
    /// ⚠ <b>가리는 동안 클릭을 먹는다.</b> <c>raycastTarget</c> 을 켜 두었다. 화면이 까만
    ///    동안 그 아래 단추가 눌리면 안 된다. 다 끝나면 오브젝트를 꺼서 통과시킨다.
    ///
    /// ⚠ <b>서버에는 설치하지 않는다.</b> 화면이 없다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ScreenFade : MonoBehaviour
    {
        /// <summary>다른 UI 보다 확실히 위에 오게 한다. 이정표 창이 32000 이다.</summary>
        private const int SortingOrder = 32500;

        private static ScreenFade instance;

        private Canvas canvas;
        private Image sheet;

        /// <summary>지금 가리는 중인가. 겹쳐 부르는 것을 막는 데 쓴다.</summary>
        public static bool Busy { get; private set; }

        /// <summary>
        /// 어두워졌다가, 가운데서 <paramref name="whileDark"/> 를 돌리고, 다시 밝아진다.
        ///
        /// <paramref name="whileDark"/> 는 <b>완전히 까매진 뒤</b> 시작해서 끝날 때까지
        /// 기다린다. 그동안 화면은 계속 까맣다. 그래서 그 안에서 캐릭터를 옮기든
        /// 카메라를 옮기든 보이지 않는다.
        ///
        /// 이미 가리는 중이면 아무것도 하지 않는다. 두 번 눌러 겹치면 막이 반쯤 걷힌
        /// 상태에서 다시 덮여 깜빡인다.
        /// </summary>
        /// <param name="whileDark">까만 동안 할 일. null 이면 그냥 깜빡인다.</param>
        /// <param name="seconds">한쪽 방향에 걸리는 시간(초). 왕복은 그 두 배다.</param>
        /// <returns>
        /// <paramref name="whileDark"/> 를 돌리기로 했으면 <c>true</c>.
        /// 이미 가리는 중이라 아무것도 하지 않았으면 <c>false</c>.
        ///
        /// <b>부르는 쪽이 이 값을 봐야 한다.</b> <c>false</c> 인데 미리 무언가를 해 두었다면
        /// (예: 카메라를 얼려 두었다면) 그것을 되돌려야 한다. 안 그러면 영영 얼어 있다.
        /// </returns>
        public static bool Cover(Func<IEnumerator> whileDark, float seconds = 0.25f)
        {
            if (Busy)
            {
                return false;
            }

            ScreenFade fade = Ensure();
            if (fade == null)
            {
                // 막을 못 만들었다고 해서 할 일까지 막지는 않는다. 연출만 빠진다.
                if (whileDark != null)
                {
                    CoroutineHost.Run(whileDark());
                }

                return true;
            }

            fade.StartCoroutine(fade.Play(whileDark, seconds));
            return true;
        }

        private IEnumerator Play(Func<IEnumerator> whileDark, float seconds)
        {
            Busy = true;

            try
            {
                Show(true);

                yield return Ramp(0f, 1f, seconds);

                if (whileDark != null)
                {
                    yield return whileDark();
                }

                yield return Ramp(1f, 0f, seconds);
            }
            finally
            {
                SetAlpha(0f);
                Show(false);
                Busy = false;
            }
        }

        /// <summary>
        /// 투명도를 <paramref name="from"/> 에서 <paramref name="to"/> 로 옮긴다.
        ///
        /// ⚠ <b><see cref="Time.unscaledDeltaTime"/> 을 쓴다.</b> 어딘가에서
        ///    <c>Time.timeScale</c> 을 0 으로 멈춰 두면 <c>deltaTime</c> 은 0 이라
        ///    막이 영영 안 걷힌다. 화면 연출은 게임 시간과 무관해야 한다.
        /// </summary>
        private IEnumerator Ramp(float from, float to, float seconds)
        {
            if (seconds <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / seconds)));
                yield return null;
            }

            SetAlpha(to);
        }

        /// <summary>
        /// 막을 보이거나 감춘다.
        ///
        /// ⚠ <b>GameObject 를 끄면 안 된다.</b> Unity 는 꺼진 GameObject 에서
        ///    <c>StartCoroutine</c> 을 시작하지 못한다. 막을 GameObject 째로 꺼 두었더니
        ///    다음번 <see cref="Cover"/> 가 코루틴을 못 돌려서, <b>화면도 안 어두워지고
        ///    캐릭터도 안 옮겨지고 카메라만 얼어붙은 채로 남았다.</b> 실제로 그렇게 됐다.
        ///
        ///    <see cref="Canvas"/> 만 끈다. 그러면 그리지도 않고 클릭도 안 먹으면서
        ///    오브젝트는 살아 있어 코루틴을 얹을 수 있다.
        /// </summary>
        private void Show(bool on)
        {
            if (canvas != null)
            {
                canvas.enabled = on;
            }
        }

        private void SetAlpha(float a)
        {
            if (sheet == null)
            {
                return;
            }

            Color c = sheet.color;
            c.a = a;
            sheet.color = c;
        }

        // ───────────────────────────── 설치 ─────────────────────────────

        private static ScreenFade Ensure()
        {
            if (instance != null)
            {
                return instance;
            }

            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return null;
            }

            var host = new GameObject("ScreenFade");
            DontDestroyOnLoad(host);

            instance = host.AddComponent<ScreenFade>();
            instance.Build();

            // GameObject 는 켜 둔 채 Canvas 만 끈다. 이유는 Show() 에 적었다.
            instance.Show(false);

            return instance;
        }

        private void Build()
        {
            var canvasObject = new GameObject(
                "Canvas", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, worldPositionStays: false);

            canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var sheetObject = new GameObject("Sheet", typeof(Image));
            sheetObject.transform.SetParent(canvasObject.transform, worldPositionStays: false);

            sheet = sheetObject.GetComponent<Image>();
            sheet.color = new Color(0f, 0f, 0f, 0f);

            // 까만 동안 그 아래 단추가 눌리면 안 된다.
            sheet.raycastTarget = true;

            // 화면 전체를 덮는다. 해상도가 바뀌어도 따라간다.
            var rect = sheetObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            // 에디터에서 Reload Domain 을 꺼 두면 static 이 이전 플레이의 값을 들고 있다.
            instance = null;
            Busy = false;
        }
    }

    /// <summary>
    /// **주인 없는 코루틴을 대신 돌려 주는 자리.**
    ///
    /// <see cref="ScreenFade"/> 가 막을 못 만들었을 때(서버이거나 설치 실패), 연출은
    /// 빠지더라도 <b>할 일 자체는 돌아가야 한다.</b> 그때 코루틴을 얹을 곳이 필요하다.
    /// </summary>
    internal sealed class CoroutineHost : MonoBehaviour
    {
        private static CoroutineHost instance;

        public static void Run(IEnumerator routine)
        {
            if (routine == null)
            {
                return;
            }

            if (instance == null)
            {
                var host = new GameObject("CoroutineHost");
                DontDestroyOnLoad(host);
                instance = host.AddComponent<CoroutineHost>();
            }

            instance.StartCoroutine(routine);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            instance = null;
        }
    }
}
