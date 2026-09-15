using UnityEngine;

/// <summary>
/// 🏴‍☠️ 적선의 **모양**. 우현 바다에 우리 배와 같은 모델을 띄운다. (SHIPCOOP.md 5장)
///
/// <b>왜 같은 모델인가.</b> 조준 · 거리감의 기준이 같아진다. 다른 배를 쓰면 "저건 얼마나 먼가" 를
/// 새로 배워야 한다. 구분은 <b>돛 색</b> 하나로 한다 — 우리 배는 미색 돛, 적선은 검은 해골 돛 그대로.
///
/// <b>어디에 있나.</b> 우리 배는 서 있고 바다가 흘러오는 구조(5장)라 적선은 <b>우리 옆에 나란히</b> 떠 있으면 된다.
/// 바다 흐름(암초처럼 흘러오는 목록)에는 넣지 않는다. 우현(+x) — 대포가 우현(x 3.21)에 있다.
///
/// <b>움직임은 x 축 하나다.</b> 옆에서 들어와서, 끝나면 옆으로 돌아간다.
/// <code>
///   예고 (IsWarning)   먼 우현 (x +220) 에서 나란한 자리 (x +56) 로 옆으로 미끄러져 들어온다 (Remaining01). z 는 +40 고정
///   발생 (IsRunning)   그 자리에 머물며 살짝 흔들린다 (롤 ±2° · 피치 ±1° · 주기 4초)
///   Hits 가 늘면       0.3초 롤 6° 휘청 + 맞은 쪽(우리 쪽, -x) 선체 중간에 연기 한 번
///   끝 (IsActive false — 격파든 시간 초과든 취소든)   3초 동안 들어온 길로 돌아나가(x +220) 꺼진다
/// </code>
/// 가라앉는 연출은 없다. 격파해도 물러나는 것으로 읽힌다 — "쫓아냈다".
///
/// ⚠ <b>값으로 그린다. 이벤트를 구독하지 않는다.</b> (11장) <c>EnemyShip</c> 의 <c>IsWarning</c> · <c>IsRunning</c> ·
///    <c>IsActive</c> · <c>Hits</c> 를 매 프레임 읽는다. <c>Hits</c> 는 <c>ShipCoopEventSync</c> 가 단계와 함께 복제한다.
///
/// ⚠ <b>조준 판정은 여기 없다.</b> 이 컴포넌트는 적선에 "위치" 를 준 것뿐이다. 조준이 판정에 들어가는 것은 다음 작업.
///
/// 높이 — 우리 배 루트와 바다의 차가 8.0m (<c>ShipCoopDeckLayout.SeaLevelY</c> = −8.0). 같은 모델이니
/// 적선 루트를 바다 원점 y + 8.0 에 두면 흘수가 같다.
/// </summary>
[DisallowMultipleComponent]
public class EnemyShipVisual : MonoBehaviour
{
    [Header("연결 — 배치 도구가 채운다")]
    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private EnemyShip enemy;

    [Tooltip("띄울 배. P_EnemyShip (StylShip_Unity + 우리 재질, 검은 해골 돛 그대로)")]
    [SerializeField] private GameObject shipPrefab;

    [Tooltip("대포에 맞았을 때 한 번 피우는 연기. FX_Smoke_01")]
    [SerializeField] private GameObject smokePrefab;

    // ------------------------------------------------------------
    // 자리 (바다 원점 기준, m). 우현이 +x, 앞이 +z. **x 만 움직인다.**
    //
    //   먼 곳    x +220   수평선(horizonDistance 100) 너머 옆. 예고 3초 동안 여기서 들어온다
    //   나란히   x  +56   대포(우현 x 3.21) 에서 봤을 때 화면 오른쪽. 배 폭(27m) 의 두 배 떨어져 겹치지 않는다
    //   z        +40     우리 배 옆 조금 앞. 가운데(0)에 두면 우리 돛대와 겹쳐 보인다
    // ------------------------------------------------------------
    public const float FarX = 220f;

    /// <summary>
    /// 나란히 설 자리 (m). 80 에서는 화면에서 너무 작아 "저기 배가 있다" 가 잘 안 읽혀서 **30% 당겼다.**
    /// 배 폭이 27m 라 56m 면 아직 두 배 넘게 떨어져 있어 겹치지 않는다.
    /// </summary>
    public const float NearX = 56f;

    public const float LaneZ = 40f;

    /// <summary>우리 배 루트(y 0)와 바다(y −8)의 차. 적선 루트를 바다보다 이만큼 위에 두면 흘수가 같다.</summary>
    public const float RootAboveSea = 8f;

    // 흔들림 · 연출 시간 (초 · 도)
    private const float SwayPeriod = 4f;
    private const float RollSway = 2f;
    private const float PitchSway = 1f;

    private const float FlinchSeconds = 0.3f;
    private const float FlinchRoll = 6f;

    /// <summary>끝났을 때 들어온 길로 돌아나가는 시간(초). 예고(3초)와 비슷해야 같은 속도로 보인다.</summary>
    private const float ReturnSeconds = 3f;

    /// <summary>연기 자리 — 우리 쪽(-x) 선체 중간. 배 반폭 13.5m 안쪽으로 조금, 흘수선(−8)과 갑판(−3.5) 사이.</summary>
    private static readonly Vector3 SmokeOffset = new Vector3(-12.5f, -5.5f, 0f);
    private const float SmokeSeconds = 4f;

    private enum Phase { Hidden, Approaching, Holding, Returning }

    private Transform _ship;
    private Phase _phase = Phase.Hidden;
    private float _phaseTime;
    private float _flinchLeft;
    private int _lastHits;
    private bool _wasActive;
    private float _lastX = FarX;

    private void Awake()
    {
        if (enemy == null)
        {
            enemy = GetComponent<EnemyShip>();
        }
    }

    private void Update()
    {
        VoyageSea sea = VoyageSea.Current;

        if (enemy == null || sea == null || !EnsureShip(sea))
        {
            return;
        }

        bool active = enemy.IsActive;

        if (active)
        {
            if (!_wasActive)
            {
                // 새 발생. 먼 곳에서 다시 시작한다.
                _lastHits = 0;
                _flinchLeft = 0f;
                _phase = Phase.Approaching;
                _ship.gameObject.SetActive(true);
            }

            if (enemy.Hits > _lastHits)
            {
                _flinchLeft = FlinchSeconds;
                Puff();
            }

            _lastHits = enemy.Hits;
            _phase = enemy.IsWarning ? Phase.Approaching : Phase.Holding;

            // 예고 동안 먼 곳 → 나란한 자리. Remaining01 은 예고 중 1 → 0. 터진 뒤에는 나란한 자리에 머문다.
            float t = enemy.IsWarning ? Mathf.SmoothStep(0f, 1f, 1f - enemy.Remaining01) : 1f;
            _lastX = Mathf.Lerp(FarX, NearX, t);

            _flinchLeft = Mathf.Max(0f, _flinchLeft - Time.deltaTime);
            float flinch = FlinchRoll * (_flinchLeft / FlinchSeconds);

            Place(sea, _lastX, Sway() + new Vector3(0f, 0f, flinch));
        }
        else
        {
            if (_wasActive)
            {
                // 방금 끝났다. 격파든 시간 초과든 취소든 **들어온 길로 돌아나간다.** 가라앉는 연출은 없다.
                _phase = Phase.Returning;
                _phaseTime = 0f;
            }

            if (_phase == Phase.Returning)
            {
                _phaseTime += Time.deltaTime;
                float u = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_phaseTime / ReturnSeconds));

                Place(sea, Mathf.Lerp(_lastX, FarX, u), Sway());

                if (_phaseTime >= ReturnSeconds)
                {
                    Hide();
                }
            }
        }

        _wasActive = active;
    }

    private void Hide()
    {
        _phase = Phase.Hidden;

        if (_ship != null)
        {
            _ship.gameObject.SetActive(false);
        }
    }

    /// <summary>바다 위에 떠 있는 배의 잔잔한 흔들림. (피치, 요, 롤) 도.</summary>
    private Vector3 Sway()
    {
        float w = 2f * Mathf.PI / SwayPeriod;
        float roll = RollSway * Mathf.Sin(Time.time * w);
        float pitch = PitchSway * Mathf.Sin(Time.time * w * 0.73f + 1.3f);
        return new Vector3(pitch, 0f, roll);
    }

    /// <summary>
    /// 바다 원점 기준 x 에 놓는다. 배가 꺾으면 세상이 옆으로 밀리므로(<c>ShipLateral</c>) 섬 · 암초와 같이 밀린다.
    /// 뱃머리는 우리와 같은 +z.
    /// </summary>
    private void Place(VoyageSea sea, float x, Vector3 euler)
    {
        _ship.position = sea.Origin + new Vector3(x - sea.ShipLateral, RootAboveSea, LaneZ);
        _ship.rotation = Quaternion.Euler(euler);
    }

    /// <summary>맞은 쪽(우리 쪽) 선체 중간에 연기 한 번. 연출 전용 — 판정과 무관하다.</summary>
    private void Puff()
    {
        if (smokePrefab == null || _ship == null)
        {
            return;
        }

        // 앞뒤로 조금 흩어 놓는다. 매번 같은 자리면 스탬프처럼 보인다. 연출이라 기계마다 달라도 된다.
        Vector3 at = _ship.TransformPoint(SmokeOffset + new Vector3(0f, 0f, Random.Range(-10f, 10f)));
        GameObject puff = Instantiate(smokePrefab, at, Quaternion.identity, _ship);
        puff.name = "적선 연기";
        Destroy(puff, SmokeSeconds);
    }

    /// <summary>배를 한 번만 만들어 꺼둔다. 필요할 때 켠다.</summary>
    private bool EnsureShip(VoyageSea sea)
    {
        if (_ship != null)
        {
            return true;
        }

        if (shipPrefab == null)
        {
            return false;
        }

        GameObject made = Instantiate(shipPrefab, sea.transform);
        made.name = "적선";

        // 부딪히거나 레이에 걸릴 것이 아니다. 카메라 가림 레이 · 발 높이 레이가 저 멀리 이 배를 맞히면 안 된다.
        foreach (Collider bump in made.GetComponentsInChildren<Collider>(true))
        {
            Destroy(bump);
        }

        made.SetActive(false);
        _ship = made.transform;
        return true;
    }
}
