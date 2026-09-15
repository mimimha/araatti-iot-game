using UnityEngine;

/// <summary>
/// 🔧 수리 지점(파손 지점)의 **모양**. 빨간 큐브를 대신한다. (SHIPCOOP.md 5장)
///
/// 갑판 위에 세 겹이 얹힌다. 전부 <c>HullDamagePoint.prefab</c> 의 자식이고 배치 도구가 만들어 둔다.
///
/// <code>
///   🪵 판자 덮기    Plank_02 ×5 를 구멍 위에 눕혀 쌓음. 망치질 1번마다 한 장씩 (+0.3m 에서 내려앉음)   ← 수리 진행
///   🪵 조각         Plank_01 ×5, 0.42배(≈0.7m). 구멍 가장자리에서 바깥으로 15~35° 들려 솟아 있음.
///                   망치질 1번마다 하나씩 0° 로 **눕는다** (숨기지 않는다 — 부서진 걸 도로 붙였다)          ← 부서짐 · 수리 진행
///   🕳 구멍         Grunge_04 데칼을 검게 틴트. 지름 1.5m. 처음부터 끝까지
///                                          갑판
/// </code>
///
/// ⚠ **붉은 표식(원)은 뺐다.** 붉게 칠한 원이 갑판 위에 떠 있어 게임 그림과 겉돌았다.
///    멀리서 "여기 부서졌다" 는 솟은 조각이 말한다 — 위에서 내려보는 카메라에 조각의 그림자와 높이가 읽힌다.
///
/// ⚠ **균열 데칼(Decal_Crack_01)은 뺐다.** 바닥에 붙은 데칼은 위에서 내려보는 카메라에 "얼룩" 으로 보였다.
///    조각이 위로 솟아야 "부서졌다" 가 보인다. 그래서 구멍(검은 데칼) + 조각 다섯 개로 바꿨다.
///
/// ⚠ **물 자국 겹은 뺐다.** 웅덩이(퍼는 곳)와 같이 보여서 퍼야 할 곳과 막아야 할 곳이 헷갈렸다. 물은 웅덩이 한 군데에만.
///
/// ⚠ **조각의 흔들림은 Random 이 아니라 조각 번호로 정한다.** 네 명이 같은 모양을 봐야 한다. (11장)
///    <see cref="ShardYaw"/> · <see cref="ShardLift"/> 를 배치 도구와 런타임이 같이 쓴다.
///
/// ⚠ **RepairTask 의 값을 읽어서 그린다. 이벤트를 구독하지 않는다.**
///    <c>Repaired</c> 같은 event Action 은 서버에서만 터진다 — 클라이언트의 RepairTask 는 판정을 안 하고
///    <c>ShowRepair</c> 로 값만 받는다. (11장) 값(<c>Hits</c> · <c>IsRepaired</c>)은 양쪽에 똑같이 있으니,
///    매 프레임 그 값을 보고 그리면 서버 화면과 클라이언트 화면이 같아진다.
///    망치질 피드백(조각이 눕고 판자가 내려앉는 0.2초)은 **값이 바뀐 프레임**을 스스로 알아채서 낸다.
/// </summary>
[DisallowMultipleComponent]
public class HullDamageVisual : MonoBehaviour
{
    // ------------------------------------------------------------
    // 🪵 조각 — 값의 근거
    //
    //   개수 5      hitsToRepair 와 같다. 망치질마다 하나씩 눕는다
    //   반지름 0.55 구멍 지름 1.5(반지름 0.75) 의 가장자리 안쪽. 조각 안쪽 끝이 구멍 테두리에 걸쳐 "여기서 튀어나왔다"
    //   배율 0.42   Plank_01 1.70m → 0.71m. 3m 사람 배에서 발목 높이쯤 솟는다. 1배면 사람 키만 한 판이 서 있다
    //   들림 15~35° 15° 아래면 바닥에 붙어 보이고, 35° 위면 세워 놓은 것 같다
    //   흔들림      각도 ±15°, 들림은 sin 으로 번호마다 다르게. Random 금지 — 네 명이 같은 모양
    // ------------------------------------------------------------

    public const int ShardCount = 5;
    public const float ShardRadius = 0.55f;
    public const float ShardScale = 0.42f;
    public const float ShardLiftMin = 15f;
    public const float ShardLiftMax = 35f;

    /// <summary>조각 i 의 바깥 방향(도). 72° 균등 + 번호로 정한 ±15° 흔들림.</summary>
    public static float ShardYaw(int i) => 72f * i + 15f * Mathf.Sin(i * 2.7f);

    /// <summary>조각 i 의 들림(도). 15~35° 사이를 번호로 정한다.</summary>
    public static float ShardLift(int i) => Mathf.Lerp(ShardLiftMin, ShardLiftMax, 0.5f + 0.5f * Mathf.Sin(i * 1.9f + 0.4f));

    /// <summary>조각이 눕는 시간(초). ease-out. 망치질 한 번의 피드백이라 짧아야 한다.</summary>
    private const float ShardLaySeconds = 0.2f;

    /// <summary>덮는 판자가 내려앉는 높이(m)와 시간(초). 위에서 툭 놓는 느낌.</summary>
    private const float PlankDropHeight = 0.3f;
    private const float PlankDropSeconds = 0.15f;

    [Header("연결 — 배치 도구가 채운다")]
    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private RepairTask repair;

    [Tooltip("세 겹을 담은 자식. 루트 배율을 상쇄하고 갑판 윗면에 맞춘다.")]
    [SerializeField] private Transform visual;

    [SerializeField] private Renderer hole;

    [Tooltip("켜는 순서대로. 망치질 n번이면 앞에서 n장이 보인다.")]
    [SerializeField] private GameObject[] planks;

    [Tooltip("조각의 회전 축(빈 부모). 안쪽 끝에 있어 들 때 갑판에 묻히지 않는다. 눕는 순서대로.")]
    [SerializeField] private Transform[] shards;

    private Vector3 _snappedAt = new Vector3(float.NaN, 0f, 0f);

    /// <summary>판자 i 의 제자리(로컬). 내려앉는 연출은 여기서 +0.3m 위에서 시작한다.</summary>
    private Vector3[] _plankRest;

    /// <summary>판자 i 가 켜진 시각. -1 이면 아직, -2 면 처음부터 놓여 있던 것(연출 없음).</summary>
    private float[] _plankShownAt;

    /// <summary>조각 i 가 눕기 시작한 시각. -1 이면 아직 들려 있다, -2 면 처음부터 누워 있던 것.</summary>
    private float[] _shardLayAt;

    private int _shownCount = -1;

    private void Awake()
    {
        if (repair == null)
        {
            repair = GetComponent<RepairTask>();
        }

        int plankCount = planks != null ? planks.Length : 0;
        _plankRest = new Vector3[plankCount];
        _plankShownAt = new float[plankCount];

        for (int i = 0; i < plankCount; i++)
        {
            _plankShownAt[i] = -1f;

            if (planks[i] != null)
            {
                _plankRest[i] = planks[i].transform.localPosition;
                planks[i].SetActive(false);
            }
        }

        int shardCount = shards != null ? shards.Length : 0;
        _shardLayAt = new float[shardCount];

        for (int i = 0; i < shardCount; i++)
        {
            _shardLayAt[i] = -1f;
            SetShard(i, 1f);
        }
    }

    private void Update()
    {
        SnapToDeck();

        int hits = repair != null ? repair.Hits : 0;
        bool repaired = repair != null && repair.IsRepaired;
        int count = repaired ? int.MaxValue : hits;

        // 값이 바뀐 프레임 — 새로 덮이는 판자 · 새로 눕는 조각의 연출 시각을 적어 둔다.
        if (count != _shownCount)
        {
            MarkChanges(count);
            _shownCount = count;
        }

        ShowPlanks(count);
        LayShards(count);
    }

    /// <summary>
    /// 세 겹을 **갑판 윗면**에 맞춘다. 루트는 갑판에서 조금 떠 있다(배치 도구의 DamageLift).
    /// 프리팹에는 그 값으로 미리 내려 두지만, 스폰 뒤 실제 갑판 높이를 한 번 더 맞춘다 —
    /// 층마다 바닥이 다르고, 네트워크에서는 위치가 조금 뒤에 도착한다. 루트가 움직이면 다시 맞춘다.
    /// </summary>
    private void SnapToDeck()
    {
        if (visual == null)
        {
            return;
        }

        Vector3 at = transform.position;

        if (!float.IsNaN(_snappedAt.x) && (at - _snappedAt).sqrMagnitude < 0.0001f)
        {
            return;
        }

        _snappedAt = at;

        ShipDeck deck = ShipDeck.At(at);

        if (deck == null)
        {
            return;
        }

        Vector3 where = visual.position;
        where.y = deck.SurfaceY;
        visual.position = where;
    }

    /// <summary>망치질 수가 바뀌었다. 이번에 새로 켜지는 판자 · 새로 눕는 조각에 시각을 찍는다.</summary>
    private void MarkChanges(int count)
    {
        float now = Time.time;

        // 첫 프레임(스폰 직후)에 이미 맞아 있던 것은 연출 없이 제자리. 늦게 들어온 사람 화면에서 다섯 장이 한꺼번에 떨어지면 이상하다.
        bool first = _shownCount < 0;

        for (int i = 0; i < _plankShownAt.Length; i++)
        {
            if (i < count && _plankShownAt[i] < 0f)
            {
                _plankShownAt[i] = first ? -2f : now;
            }
            else if (i >= count)
            {
                _plankShownAt[i] = -1f;
            }
        }

        for (int i = 0; i < _shardLayAt.Length; i++)
        {
            if (i < count && _shardLayAt[i] < 0f)
            {
                _shardLayAt[i] = first ? -2f : now;
            }
            else if (i >= count)
            {
                _shardLayAt[i] = -1f;
            }
        }
    }

    private void ShowPlanks(int count)
    {
        if (planks == null)
        {
            return;
        }

        for (int i = 0; i < planks.Length; i++)
        {
            if (planks[i] == null)
            {
                continue;
            }

            bool on = i < count;

            if (planks[i].activeSelf != on)
            {
                planks[i].SetActive(on);
            }

            if (!on)
            {
                continue;
            }

            // +0.3m 에서 0.15초에 내려앉는다. -2 는 "이미 놓여 있던 것" — 연출 없이 제자리.
            float lift = 0f;

            if (_plankShownAt[i] >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - _plankShownAt[i]) / PlankDropSeconds);
                lift = PlankDropHeight * (1f - EaseOut(t));
            }

            planks[i].transform.localPosition = _plankRest[i] + Vector3.up * lift;
        }
    }

    /// <summary>조각 i &lt; count 는 눕고(0°), 나머지는 원래 들림. 눕는 데 0.2초 ease-out.</summary>
    private void LayShards(int count)
    {
        if (shards == null)
        {
            return;
        }

        for (int i = 0; i < shards.Length; i++)
        {
            if (i >= count)
            {
                SetShard(i, 1f);
                continue;
            }

            float up = 0f;

            if (_shardLayAt[i] >= 0f)
            {
                float t = Mathf.Clamp01((Time.time - _shardLayAt[i]) / ShardLaySeconds);
                up = 1f - EaseOut(t);
            }

            SetShard(i, up);
        }
    }

    /// <summary>조각 i 의 축을 돌린다. <paramref name="up"/> 1 이면 원래 들림, 0 이면 갑판에 평평.</summary>
    private void SetShard(int i, float up)
    {
        Transform pivot = shards[i];

        if (pivot == null)
        {
            return;
        }

        // Euler(0, yaw, lift) = Ry(yaw) · Rz(lift) — 자기 옆축으로 먼저 들고, 그 다음 바깥 방향으로 돌린다.
        // 축이 안쪽 끝에 있어(배치 도구) 들어도 안쪽 끝은 갑판 위에 남는다.
        pivot.localRotation = Quaternion.Euler(0f, ShardYaw(i), ShardLift(i) * up);
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }
}
