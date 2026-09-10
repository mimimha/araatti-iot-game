using UnityEngine;

/// <summary>
/// 🌊 거대한 파도. 조타로 방향을 맞춰 정면으로 돌파한다. (SHIPCOOP.md 5장)
///
/// 암초와 반대입니다. 암초는 꺾어서 피하고, 파도는 **꺾지 않고 정면으로 받아야** 합니다.
/// 그래서 암초 직후에 파도가 오면 방금 꺾은 것을 되돌려야 하고,
/// 그 사이에 조타를 비우면 둘 다 놓칩니다.
///
/// 버티는 시간 동안 정면을 벗어난 시간이 허용치를 넘으면 옆으로 맞아 배가 깎입니다.
///
/// **바다 위에 실제로 떠 있는 파도입니다.** 예고 동안 수평선에 나타나 다가옵니다.
/// 암초와 달리 좌우로 치우쳐 있지 않고 뱃길을 가로질러 오므로 피할 수가 없습니다.
///
/// 판정은 바다가 있든 없든 같습니다. **"정면을 지켰나" 는 각도로 재든 좌우 위치로 재든
/// 같은 값**이기 때문입니다. (조타각 × 계수 = 좌우 위치) 그래서 암초와 달리
/// 판정을 바꾸지 않았고, 보여주는 것만 더했습니다.
/// </summary>
public class BigWave : VoyageEvent
{
    [Header("바다 위 파도")]
    [Tooltip("띄울 파도. 비워두면 회색 판을 만들어 쓴다. (프로토타입용)")]
    [SerializeField] private GameObject wavePrefab;

    [Tooltip("파도의 폭 (m). 배가 옆으로 갈 수 있는 폭(약 5m)보다 넉넉해야\n" +
             "'피할 수 없다, 정면으로 받아야 한다' 가 읽힌다.")]
    [SerializeField, Min(2f)] private float waveWidth = 16f;

    [Header("정면 판정")]
    [Tooltip("뱃머리가 이 각도 안에 있으면 정면으로 받는 것으로 본다.")]
    [SerializeField, Min(1f)] private float straightTolerance = 12f;

    [Tooltip("정면을 벗어나도 되는 시간 (초). 이만큼 넘게 벗어나면 실패한다.\n" +
             "0 으로 두면 한 순간도 벗어날 수 없어 너무 가혹하다.")]
    [SerializeField, Min(0f)] private float allowedOffTime = 1.5f;

    /// <summary>정면을 벗어나 있던 시간 (초)</summary>
    public float OffTime { get; private set; }

    /// <summary>지금 정면으로 받고 있는지</summary>
    public bool IsStraight { get; private set; }

    /// <summary>벗어난 시간이 허용치의 몇 %인지. HUD 게이지가 이걸 본다.</summary>
    public float OffTime01 =>
        allowedOffTime <= 0f ? (IsStraight ? 0f : 1f) : Mathf.Clamp01(OffTime / allowedOffTime);

    private HelmTask _helm;
    private GameObject _wave;

    /// <summary>
    /// 예고 시작. **여기서 파도를 띄운다.**
    ///
    /// 암초와 달리 좌우로 치우쳐 놓지 않습니다. 파도는 뱃길을 가로질러 오므로
    /// 피할 수 없고, **정면으로 받는 것 말고는 방법이 없다**는 것이 보여야 합니다. (5장)
    /// 배가 틀어져 있으면 파도가 한쪽으로 밀려 보입니다. 그것이 "안 맞았다" 는 신호입니다.
    /// </summary>
    protected override void OnWarn()
    {
        if (VoyageSea.Current == null)
        {
            return;
        }

        if (wavePrefab != null)
        {
            _wave = Instantiate(wavePrefab);
        }
        else
        {
            // 프로토타입. 에셋이 오면 wavePrefab 을 채우면 된다.
            _wave = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wave.transform.localScale = new Vector3(waveWidth, 1.5f, 2f);

            // 부딪히면 안 된다. 판정은 조타각으로 한다.
            Collider collider = _wave.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }
        }

        _wave.name = $"BigWave_{Time.frameCount}";
        VoyageSea.Current.Place(_wave.transform, 0f, Approach01);
    }

    /// <summary>예고 때부터 계속 다가온다. 판정은 안 한다.</summary>
    protected override void OnShow(float deltaTime)
    {
        if (_wave != null && VoyageSea.Current != null)
        {
            VoyageSea.Current.Place(_wave.transform, 0f, Approach01);
        }
    }

    /// <summary>끝났으면 파도를 치운다. 성공·실패·취소 모두 여기를 지난다.</summary>
    protected override void OnHide()
    {
        if (_wave == null)
        {
            return;
        }

        Destroy(_wave);
        _wave = null;
    }

    protected override void OnBegin()
    {
        OffTime = 0f;
        IsStraight = true;
        _helm = FindHelm();

        if (_helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 정면 판정을 할 수 없습니다.", this);
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (_helm == null)
        {
            return;
        }

        IsStraight = Mathf.Abs(_helm.Heading) <= straightTolerance;

        if (IsStraight)
        {
            return;
        }

        OffTime += deltaTime;

        if (OffTime > allowedOffTime)
        {
            Fail();
        }
    }

    /// <summary>버틴 시간이 다 됐다. 정면을 지켰으면 넘긴 것이다.</summary>
    protected override void OnTimeout()
    {
        Succeed();
    }

    protected override void OnSucceed()
    {
        Game?.ReportObstacleAvoided();
    }

    /// <summary>HUD 문구</summary>
    public string StraightHint()
    {
        float heading = _helm != null ? _helm.Heading : 0f;
        return IsStraight
            ? $"정면 유지 중  ({heading:F0}°)"
            : $"뱃머리를 정면으로!  (지금 {heading:F0}°, 벗어난 시간 {OffTime:F1}/{allowedOffTime:F1}초)";
    }

    private static HelmTask FindHelm()
    {
        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is HelmTask helm)
            {
                return helm;
            }
        }

        return null;
    }
}
