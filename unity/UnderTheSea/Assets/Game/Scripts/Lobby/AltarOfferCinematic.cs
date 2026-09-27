using System;
using System.Collections;
using Fusion;
using UnderTheSea.Network;
using UnderTheSea.UI;
using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **조각을 바친 사람에게만 보여 주는 짧은 카메라 연출.** 공중에서 제단을 비춰 빛줄기가 하늘로 올라가는 것을 보인다.
    ///
    /// <code>
    ///   어두워짐 → 공중 시점으로 옮김 → 밝아짐
    ///   → 제단 연출 재생(문양 · 파티클 · 노란 빛줄기) → 빛줄기가 끝날 때까지 머묾
    ///   → 어두워짐 → 캐릭터 뒤로 되돌림 → 밝아짐
    /// </code>
    ///
    /// <b>제단 연출은 밝아진 뒤에 재생한다.</b> 바친 사람이 공중에서 처음부터 보게 하려는 것이다.
    /// 다른 사람은 자기 시점 그대로 곧바로 본다(<see cref="AltarOfferingRelay"/>).
    ///
    /// <b>연출 중에는 움직이지 못한다.</b> <see cref="ChatFocus"/> 를 잡아 이동 · 상호작용 입력을 막는다
    /// (키보드와 완드 모두 그것을 본다). 끝나면 캐릭터는 제단 앞 그대로다.
    ///
    /// <b>카메라 자리는 사람이 고른다.</b> 제단 프리팹 안의 <see cref="AltarOfferCameraPoint"/> 위치 · 방향 그대로 놓는다.
    /// 씬 뷰에서 <c>Tools/아라아띠/제단 봉헌 카메라 고르기</c> 로 바꾼다. 그 자리가 없을 때만 아래처럼 계산한다 —
    /// 제단에서 내 캐릭터 쪽, 높은 곳에서 제단 조금 위를 겨눈다. 바로 위에서 보면 솟는 빛줄기가 점으로만 보여서
    /// 비스듬히 본다.
    ///
    /// ⚠ 페이드 막(<see cref="ScreenFade"/>)이 다른 일로 쓰이는 중이면 카메라 연출을 건너뛰고 제단 연출만 튼다.
    /// </summary>
    public static class AltarOfferCinematic
    {
        /// <summary>한쪽 페이드 시간(초).</summary>
        private const float FadeSeconds = 0.3f;

        /// <summary>제단에서 카메라까지 수평 거리(m).</summary>
        private const float Distance = 22f;

        /// <summary>제단 꼭대기보다 카메라가 높은 정도(m).</summary>
        private const float Height = 13f;

        /// <summary>제단 꼭대기보다 이만큼 위를 겨눈다(m). 빛줄기가 화면 가운데에서 위로 뻗게.</summary>
        private const float AimAbove = 4f;

        /// <summary>빛줄기 길이를 모를 때 머무는 시간(초).</summary>
        private const float FallbackHoldSeconds = 2.8f;

        /// <summary>지금 연출 중인가. 겹쳐 부르면 두 번째는 제단 연출만 튼다.</summary>
        public static bool Playing { get; private set; }

        private static readonly object InputLock = new object();

        /// <summary>연출 전 게임 카메라 화각. 되돌릴 때 쓴다. 바꾸지 않았으면 null.</summary>
        private static float? savedFieldOfView;

        /// <summary>
        /// 연출을 시작한다. <paramref name="playEffects"/> 는 공중 시점이 밝아진 뒤 한 번 불린다.
        /// 연출을 못 하는 상황이면 곧바로 불린다 — 제단 연출이 빠지는 일은 없다.
        /// </summary>
        public static void Play(Action playEffects)
        {
            NetworkObject me = LocalPlayer.Object;
            LocalPlayerView view = me != null ? me.GetComponent<LocalPlayerView>() : null;
            AltarOfferBeam beam = AltarOfferBeam.Current;

            if (Playing || view == null || view.CameraTransform == null || beam == null || ScreenFade.Busy)
            {
                playEffects?.Invoke();
                return;
            }

            CoroutineHost.Run(Run(view, beam, playEffects));
        }

        private static IEnumerator Run(LocalPlayerView view, AltarOfferBeam beam, Action playEffects)
        {
            Playing = true;
            ChatFocus.Begin(InputLock);
            CloseOfferingPanel();

            try
            {
                // ① 공중으로 — 어두운 동안 카메라를 얼리고 옮긴다.
                view.FreezeCamera(true);
                if (!ScreenFade.Cover(() => MoveToAerial(view, beam), FadeSeconds))
                {
                    // 다른 연출이 막을 쓰는 중이었다. 카메라를 풀고 제단 연출만 튼다.
                    view.FreezeCamera(false);
                    playEffects?.Invoke();
                    yield break;
                }

                yield return WaitFade();

                // ② 밝아졌다 — 제단 연출을 틀고, 빛줄기가 끝날 때까지 본다.
                playEffects?.Invoke();
                float hold = beam != null ? beam.Duration : FallbackHoldSeconds;
                yield return new WaitForSeconds(hold);

                // ③ 캐릭터 뒤로 — 어두운 동안 제자리에 놓고 카메라를 푼다.
                ScreenFade.Cover(() => BackToPlayer(view), FadeSeconds);
                yield return WaitFade();
            }
            finally
            {
                // 무슨 일이 있어도 조작과 카메라는 돌려준다. 안 그러면 제단 앞에 영영 묶인다.
                if (view != null)
                {
                    RestoreFieldOfView(view);
                    view.FreezeCamera(false);
                }

                ChatFocus.End(InputLock);
                Playing = false;
            }
        }

        private static IEnumerator MoveToAerial(LocalPlayerView view, AltarOfferBeam beam)
        {
            Transform cam = view.CameraTransform;

            // 사람이 씬 뷰에서 골라 둔 자리가 있으면 그대로 쓴다(AltarOfferCameraPoint).
            AltarOfferCameraPoint point = AltarOfferCameraPoint.Current;
            if (point != null)
            {
                cam.SetPositionAndRotation(point.transform.position, point.transform.rotation);

                // 화각도 고른 그대로. 로비 카메라(34°)와 씬 뷰(60°)가 달라서, 안 바꾸면 확대된 것처럼 보인다.
                Camera lens = cam.GetComponent<Camera>();
                if (lens != null && !savedFieldOfView.HasValue)
                {
                    savedFieldOfView = lens.fieldOfView;
                    lens.fieldOfView = point.FieldOfView;
                }
                yield break;
            }

            // 없으면 계산한다 — 제단에서 내 캐릭터 쪽, 높은 곳에서 제단 조금 위를 겨눈다.
            Vector3 altar = beam.BasePosition;

            // 제단에서 내 캐릭터 쪽을 수평으로. 캐릭터가 제단에 딱 붙어 있으면 지금 카메라 쪽을 쓴다.
            Vector3 toMe = view.transform.position - altar;
            toMe.y = 0f;
            if (toMe.sqrMagnitude < 0.25f)
            {
                toMe = cam.position - altar;
                toMe.y = 0f;
            }
            Vector3 side = toMe.sqrMagnitude > 0.0001f ? toMe.normalized : Vector3.back;

            Vector3 aim = altar + Vector3.up * AimAbove;
            cam.position = altar + side * Distance + Vector3.up * Height;
            cam.rotation = Quaternion.LookRotation(aim - cam.position, Vector3.up);
            yield break;
        }

        private static IEnumerator BackToPlayer(LocalPlayerView view)
        {
            RestoreFieldOfView(view);
            view.SnapCameraToMe();
            view.FreezeCamera(false);
            yield break;
        }

        private static void RestoreFieldOfView(LocalPlayerView view)
        {
            if (!savedFieldOfView.HasValue)
            {
                return;
            }

            Camera lens = view.CameraTransform != null ? view.CameraTransform.GetComponent<Camera>() : null;
            if (lens != null)
            {
                lens.fieldOfView = savedFieldOfView.Value;
            }
            savedFieldOfView = null;
        }

        private static IEnumerator WaitFade()
        {
            // 막이 한 번 켜진 뒤 꺼질 때까지. Cover 가 받아들였으면 이 프레임에 이미 Busy 다.
            while (ScreenFade.Busy)
            {
                yield return null;
            }
        }

        /// <summary>봉헌 창이 떠 있으면 공중 시점을 가린다. 연출 전에 닫는다.</summary>
        private static void CloseOfferingPanel()
        {
            var panel = UnityEngine.Object.FindFirstObjectByType<AltarOfferingUIController>();
            if (panel != null && panel.IsOpen)
            {
                panel.Close();
            }
        }
    }
}
