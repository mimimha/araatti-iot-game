using UnityEngine;

/// <summary>
/// 항해. 속도와 진행도를 관리한다.
///
/// 목적지까지의 거리는 고정이고, 배가 얼마나 나아갔는지가 진행도다.
/// **속도가 곧 진행도이므로 돛이 진행에 직결된다.**
///
/// 스스로 Update 하지 않는다. ShipCoopGame 이 항해 중일 때만 Tick 을 불러준다.
/// (시작 전이나 끝난 뒤에 배가 계속 나아가면 안 되기 때문)
/// </summary>
public class ShipVoyage : MonoBehaviour
{
    // ------------------------------------------------------------
    // 기본값 계산 (제한시간 180초 기준)
    //
    //   최저 속도로만 가면   600 / 1.25 = 480초  →  시간 초과. 돛을 버릴 수 없다.
    //   최대 속도로만 가면   600 / 5.00 = 120초  →  60초를 남기고 도착
    //   도착하려면          평균 3.33 m/s 이상  →  돛 힘 평균 0.55 정도
    //
    // 즉 "돛에 상시 한 명이면 여유, 가끔 봐주면 아슬아슬, 방치하면 실패"가 된다.
    // 밸런싱은 이 네 숫자(거리 · 최대속도 · 최저비율 · 제한시간)만 만지면 된다.
    //
    // 5분(1000m)에서 3분으로 줄일 때 거리도 600m 로 같이 줄였다.
    // 180/300 = 0.6 이라 거리에 같은 0.6 을 걸면 위 세 줄의 **비율이 그대로 보존된다.**
    // 필요 평균 속도도 돛 힘 0.55 도 5분짜리와 똑같다. 최대 속도와 최저 비율은
    // 건드리지 않았다 — 그 둘은 바다가 흐르는 속도라 화면의 체감이 바뀐다.
    // ------------------------------------------------------------

    [Header("목적지까지의 거리 (m)")]
    [SerializeField] private float totalDistance = 600f;

    [Header("최대 속도 (m/s)")]
    [Tooltip("돛을 완벽하게 다뤘을 때의 속도")]
    [SerializeField] private float maxSpeed = 5f;

    [Header("최저 속도 비율")]
    [Tooltip("아무도 돛을 잡지 않아도 해류에 밀려 이만큼은 나아간다.\n" +
             "0 으로 두면 배가 멈춰 서서 화면이 정지 화면이 된다.\n" +
             "이 속도로는 제한시간 안에 도착하지 못하게 잡는다. (권장 0.25)")]
    [SerializeField, Range(0f, 1f)] private float minSpeedRatio = 0.25f;

    /// <summary>
    /// 돛이 정하는 값. 0 ~ 1.
    /// SailTask 가 매 프레임 넣어준다. 아무도 돛에 없으면 0 으로 떨어진다.
    /// </summary>
    public float SailPower01 { get; set; }

    /// <summary>
    /// 조타가 정하는 값. 0 ~ 1. **목적지 쪽을 향한 정도**다.
    ///
    /// VoyageSea 가 매 프레임 cos(조타각) 을 넣어준다.
    /// 뱃머리가 목적지 섬을 향하고 있으면 1, 옆을 보고 있으면 줄어든다.
    ///
    /// "항로에서 벗어나면 느려진다" 는 규칙을 따로 가르치지 않는다.
    /// **수평선에 섬이 보이고, 거기를 향하면 빨라진다.** 그게 전부다. (5장)
    /// </summary>
    public float CourseFactor { get; set; } = 1f;

    // 침수는 여기에 손대지 않는다. 물이 찬 배를 느려지게 해봤지만 아무도 못 느꼈고,
    // 지금은 ShipFlooding 이 배 HP 를 직접 깎는다. (SHIPCOOP.md 2장)

    /// <summary>
    /// 해류가 뒤집혔는지. **돌풍이 부는 동안 참**이다. (Squall 이 켜고 끈다)
    ///
    /// 평소에는 아무도 돛을 안 잡아도 해류가 최저 속도만큼 밀어줍니다.
    /// 돌풍 중에는 그 해류가 반대로 붑니다. 돛을 잡고 있으면 느리게라도 앞으로 가지만,
    /// **손을 놓는 순간 뒤로 밀립니다.**
    ///
    /// 진행도를 숫자로 깎지 않는 이유는 그것이 화면에 안 보이기 때문입니다.
    /// 뒤로 밀리면 섬이 멀어지고 항로선이 거꾸로 흐릅니다. 그건 보입니다.
    /// </summary>
    public bool CurrentReversed { get; set; }

    /// <summary>최저 속도 (m/s). 아무도 아무것도 안 해도 이만큼은 간다.</summary>
    public float MinSpeed => maxSpeed * minSpeedRatio;

    /// <summary>
    /// 지금 속도 (m/s)
    ///
    /// 조타는 **최저 속도 위쪽에만** 곱한다. 그래야 아무리 틀어져 있어도
    /// 배가 멈춰 서서 화면이 정지 화면이 되는 일이 없다.
    /// (2장 — 최저 속도를 두는 이유)
    /// </summary>
    public float Speed
    {
        get
        {
            float sailed = Mathf.Lerp(MinSpeed, maxSpeed, Mathf.Clamp01(SailPower01));
            float kept = Mathf.Clamp01(CourseFactor);

            // 해류. 돌풍 중에는 반대로 분다.
            float drift = CurrentReversed ? -MinSpeed : MinSpeed;

            return drift + (sailed - MinSpeed) * kept;
        }
    }

    /// <summary>지금까지 나아간 거리 (m)</summary>
    public float Distance { get; private set; }

    /// <summary>목적지까지의 총 거리 (m)</summary>
    public float TotalDistance => totalDistance;

    /// <summary>0 ~ 1. HUD 진행도 바에 그대로 쓴다.</summary>
    public float Progress01 => totalDistance <= 0f ? 1f : Mathf.Clamp01(Distance / totalDistance);

    /// <summary>목적지에 도착했는지</summary>
    public bool HasArrived => Distance >= totalDistance;

    /// <summary>ShipCoopGame 이 항해 중일 때만 불러준다.</summary>
    public void Tick(float deltaTime)
    {
        if (HasArrived)
        {
            return;
        }

        // 뒤로도 간다. 다만 출항한 자리보다 뒤로는 못 간다.
        // 음수가 되면 진행도 계산이 이상해지고 페이즈도 어긋난다.
        Distance = Mathf.Clamp(Distance + Speed * deltaTime, 0f, totalDistance);
    }

    /// <summary>
    /// 진행도를 바로 옮긴다. **개발용입니다.** (ShipCoopDevMode 가 부릅니다)
    ///
    /// 페이즈가 진행도로 갈리기 때문에, 뒷 구간을 보려면 여기까지 배를 몰고 와야 합니다.
    /// 최저 속도로는 목적지까지 480초가 걸립니다. 사건 하나 확인하려고 그걸 기다릴 수 없습니다.
    /// </summary>
    public void SetProgress01(float progress01)
    {
        Distance = Mathf.Clamp01(progress01) * totalDistance;
    }

    /// <summary>처음부터 다시 시작한다.</summary>
    public void ResetVoyage()
    {
        Distance = 0f;
        SailPower01 = 0f;
        CourseFactor = 1f;
        CurrentReversed = false;
    }
}
