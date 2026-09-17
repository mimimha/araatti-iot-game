using UnityEngine;

namespace UnderTheSea.Core
{
    /// <summary>
    /// 클라이언트가 초당 몇 프레임까지 그릴 것인가. **기본값을 두고 실행 인자로 덮는다.**
    ///
    /// <code>
    ///   AraAtti-Flow.exe              60fps (기본값)
    ///   AraAtti-Flow.exe -fps 120     120fps
    ///   AraAtti-Flow.exe -fps 0       제한 없음 (예전 동작)
    /// </code>
    ///
    /// <b>왜 제한하는가.</b> 제한이 없으면 낼 수 있는 만큼 낸다. 실측으로 150~210fps 였고
    /// 그때 클라이언트 하나가 <b>CPU 코어 5개와 GPU 34%</b> 를 썼다. Dedicated Server 둘을
    /// 합친 것의 네 배다. 한 PC 에서 서버 둘과 클라이언트 둘을 같이 띄우는 QA 구성에서는
    /// 그 낭비가 그대로 서로의 경합이 된다.
    ///
    /// <b>왜 60인가.</b> Fusion 틱이 64Hz 라 서버는 초당 64번만 상태를 정한다. 그보다 훨씬
    /// 많이 그려도 <b>같은 상태를 여러 번 보간해 그리는 것</b>일 뿐, 입력이 서버에 닿는 빈도는
    /// 달라지지 않는다. 60 은 틱과 거의 맞아 지연 손해가 작고 절감 효과는 크다.
    ///
    /// ⚠ 모니터 주사율보다 높게 잡아도 화면에는 그만큼 안 나온다. 더 부드럽게 하고 싶으면
    ///    주사율에 맞춰 <c>-fps 120</c> 처럼 올리면 된다.
    ///
    /// ⚠ <b>Dedicated Server 에는 적용하지 않는다.</b> 서버는 각자 자기 정리 코드에서
    ///    120 으로 맞춘다. (<c>DedicatedServerSceneCleanup</c> · <c>ShipCoopServerCleanup</c>)
    /// </summary>
    public static class FrameRateSettings
    {
        /// <summary>실행 인자 이름. 값은 뒤에 띄어 쓴다 — <c>-fps 120</c></summary>
        public const string Key = "-fps";

        /// <summary>
        /// 인자가 없을 때 쓸 값.
        ///
        /// 120 인 이유는 <b>이 게임을 혼자 할 때가 기본</b>이기 때문이다. 요즘 노트북 내장
        /// 화면이 120Hz 라 60 으로 묶으면 눈에 띄게 덜 부드럽다. 120 이어도 무제한(1,200fps,
        /// 코어 3개) 대비 <b>코어 0.5개</b> 수준이라 절감 효과는 거의 그대로 가져간다.
        ///
        /// 한 PC 에 클라이언트를 여럿 띄우는 QA 에서는 그때만 <c>-fps 60</c> 으로 낮춘다.
        /// </summary>
        public const int Default = 120;

        /// <summary>이 값 아래로는 내리지 않는다. 너무 낮으면 조작이 눈에 띄게 늦어진다.</summary>
        private const int Lowest = 30;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Apply()
        {
            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                // 서버는 자기 정리 코드가 정한다. 여기서 끼어들면 두 곳이 같은 값을 다툰다.
                return;
            }

            int wanted = FusionLaunchArguments.GetInt(Key, Default);

            // 0 은 "제한 없음" 이다. Unity 에서는 -1 이 그 뜻이다.
            if (wanted <= 0)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                Debug.Log($"[프레임] 제한 없음. ({Key} 0)");
                return;
            }

            if (wanted < Lowest)
            {
                Debug.LogWarning($"[프레임] {wanted}는 너무 낮습니다. {Lowest}로 올립니다.");
                wanted = Lowest;
            }

            // ⚠ vSync 가 켜져 있으면 targetFrameRate 를 무시한다. 우리가 정한 값이 이기게 한다.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = wanted;

            Debug.Log($"[프레임] 초당 {wanted}장으로 맞췄습니다. " +
                      $"(기본 {Default}, 바꾸려면 {Key} <숫자>, 제한 없이는 {Key} 0)");
        }
    }
}
