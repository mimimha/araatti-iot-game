using UnityEngine;

/// <summary>
/// 📦 보급 상자. 운반하는 사람이 여기서 물건을 집는다. (SHIPCOOP.md 4장)
///
/// 포탄만이 아니라 **수리 자재와 양동이도 같은 상자에서** 나옵니다.
/// 무엇이 나오는지는 kind 로 정합니다. 집고 · 들고 가고 · 놓는 문법이 셋 다 같아서
/// 상자를 따로 만들 이유가 없습니다.
///
/// 자리가 아닙니다. TaskBase 가 아니므로 여기 서 있어도 자리를 차지하지 않습니다.
/// 집는 것은 CarryTask 가 합니다.
/// </summary>
public class AmmoBox : MonoBehaviour
{
    [Header("여기서 무엇이 나오는지")]
    [Tooltip("포탄은 대포로, 자재는 파손 지점으로, 양동이는 뱃전으로 나른다.")]
    [SerializeField] private Cargo kind = Cargo.Ammo;

    [Header("집을 수 있는 거리 (m)")]
    [SerializeField] private float reachRange = 2f;

    [Header("남은 포탄")]
    [Tooltip("-1 이면 무한. 상자가 바닥나는 규칙은 아직 없다.")]
    [SerializeField] private int stock = -1;

    [Header("양동이 상자만")]
    [Tooltip("켜면 배에 물이 찼을 때만 나타난다. (kind 가 물일 때만 쓴다)\n\n" +
             "물은 0 에서 시작하는데 양동이 상자가 처음부터 갑판에 놓여 있으면\n" +
             "'물이 처음부터 차 있다' 로 읽힌다. 도구는 일이 생긴 뒤에 나타나야 한다.")]
    [SerializeField] private bool showOnlyWhenFlooded = true;

    private ShipFlooding _flooding;
    private Renderer[] _renderers;

    /// <summary>여기서 나오는 물건</summary>
    public Cargo Kind => kind;

    /// <summary>
    /// 🧰 뚜껑이 열려 있는가. **이 값이 진실이다.** 뚜껑 표현(<c>SupplyChestLid</c>)은 이것을 읽기만 한다.
    ///
    /// <code>
    ///   열림   TryTake 가 true 를 돌려준 순간 — 상호작용으로 실제로 집은 순간이다.
    ///          키를 누른 것이 기준이 아니다. 재고가 없어 못 집었으면 안 열린다.
    ///   닫힘   열린 상태에서 어느 TaskWorker 도 사거리(reachRange) 안에 없을 때. 지연 없음.
    ///          한 사람이 집고 다른 사람이 다가오면 열린 채 유지된다. 마지막 사람이 나가면 닫힌다.
    /// </code>
    ///
    /// 왜 다가가면 열리는 것이 아니라 집을 때 열리나 — 지나가기만 해도 열리면 앞계단 사이를 오가는
    /// 동안 계속 덜컹거린다. 집기 성공에 묶으면 "여기서 뭔가 가져갔다" 가 화면에 남고,
    /// 나갈 때 닫히는 것은 "자리에서 걸어나가면 떨어진다" 와 같은 문법이다. (SHIPCOOP.md 4장)
    ///
    /// ⚠ event Action 으로 알리지 않는다. 클라이언트에서 안 터진다. (11장) 값 하나만 복제하면 된다.
    /// </summary>
    public bool IsOpen { get; private set; }

    /// <summary>
    /// **열림 상태를 밖에서 정해 준다.** 네트워크에서 서버가 정한 값을 클라이언트 화면에 옮길 때 쓴다.
    /// 클라이언트는 집기 판정이 없어 스스로 열 수 없다. (<c>ShipCoopStateSync</c>)
    /// </summary>
    public void ShowOpen(bool open)
    {
        IsOpen = open;
    }

    // 사거리 안에 사람이 있는지 볼 때 쓰는 목록. 매 프레임 찾으면 아까우니 잠깐 들고 있는다.
    private TaskWorker[] _workers;
    private float _workersRefreshedAt = -1f;
    private const float WorkersRefreshSeconds = 1f;

    /// <summary>집을 수 있는 거리</summary>
    public float ReachRange => reachRange;

    /// <summary>지금 쓸 수 있는 상자인지. 양동이는 퍼낼 물이 있어야 쓸 수 있다.</summary>
    public bool IsUsable =>
        kind != Cargo.Water || !showOnlyWhenFlooded || (_flooding != null && _flooding.HasWater);

    /// <summary>아직 집을 것이 남았는지. HUD 안내와 집기 판정이 함께 이걸 본다.</summary>
    public bool HasStock => stock != 0 && IsUsable;

    private void Awake()
    {
        if (kind != Cargo.Water || !showOnlyWhenFlooded)
        {
            return;
        }

        _flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        _renderers = VisibleRenderers();
        ShowBox(false);
    }

    /// <summary>
    /// 켜고 끌 렌더러. **씬에서 이미 꺼진 것은 넣지 않는다.**
    ///
    /// 배치 도구가 부모 큐브의 겉을 감추고 자식으로 웅덩이(물 Quad)를 얹는다. 큐브 렌더러까지 목록에
    /// 넣으면 물이 찰 때 회색 큐브가 웅덩이와 함께 다시 나타난다.
    /// </summary>
    private Renderer[] VisibleRenderers()
    {
        Renderer[] all = GetComponentsInChildren<Renderer>(includeInactive: true);
        System.Collections.Generic.List<Renderer> kept = new System.Collections.Generic.List<Renderer>(all.Length);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].enabled)
            {
                kept.Add(all[i]);
            }
        }

        return kept.ToArray();
    }

    private void Update()
    {
        CloseWhenEveryoneLeft();

        if (_renderers == null)
        {
            return;
        }

        ShowBox(IsUsable);
    }

    /// <summary>열려 있는데 사거리 안에 아무도 없으면 닫는다. 판정하는 쪽(서버 · 혼자 하는 씬)에서만 돈다.</summary>
    private void CloseWhenEveryoneLeft()
    {
        if (!IsOpen || !UnderTheSea.MiniGames.ShipCoop.Net.ShipCoopNet.IsAuthorityHere)
        {
            return;
        }

        if (_workers == null || Time.time - _workersRefreshedAt > WorkersRefreshSeconds)
        {
            _workers = FindObjectsByType<TaskWorker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            _workersRefreshedAt = Time.time;
        }

        for (int i = 0; i < _workers.Length; i++)
        {
            if (_workers[i] != null && IsInReach(_workers[i].transform.position))
            {
                return;
            }
        }

        IsOpen = false;
    }

    private void ShowBox(bool visible)
    {
        for (int i = 0; i < _renderers.Length; i++)
        {
            if (_renderers[i] != null)
            {
                _renderers[i].enabled = visible;
            }
        }
    }

    /// <summary>주어진 위치에서 손이 닿는지</summary>
    public bool IsInReach(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= reachRange * reachRange;
    }

    /// <summary>포탄 하나를 꺼낸다. 성공하면 true.</summary>
    public bool TryTake()
    {
        if (stock == 0 || !IsUsable)
        {
            return false;
        }

        if (stock > 0)
        {
            stock--;
        }

        // 실제로 집었다. 뚜껑을 연다. (키를 누른 것이 아니라 집기 성공이 기준)
        IsOpen = true;
        _workersRefreshedAt = -1f;

        return true;
    }
}
