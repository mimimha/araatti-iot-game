using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 갑판에 내려놓은 물건. 다시 주울 수 있다. (SHIPCOOP.md 4장)
///
/// 왜 있는가 — **운반을 중간에 멈출 수 있어야 하기 때문입니다.**
///
/// 예전에는 나르던 물건을 놓으면 그대로 사라졌습니다. 그래서 포탄을 나르는 중에
/// 침수가 터지면 선택지가 둘뿐이었습니다.
///
/// <code>
/// 끝까지 나르고 간다   →  급한 일을 무시한다
/// 버리고 간다          →  포탄이 증발한다. 상자로 다시 뛰어야 한다
/// </code>
///
/// 둘 다 손해라서 **사람은 하던 일을 마칩니다.** 그런데 이 게임의 재미는
/// *"지금 누가 무엇을 해야 하는가를 빠르게 판단하는 것"* 입니다. (1장)
/// 판단할 수가 없게 되어 있었습니다.
///
/// 아무 데나 내려놓을 수 있으면 이렇게 됩니다.
///
/// <code>
/// 포탄을 던져두고  →  수리하고  →  돌아와서 줍고  →  마저 나른다
/// </code>
///
/// 그리고 **갑판에 물건이 쌓입니다.** 급할 때 던져둔 포탄 · 자재 · 양동이가
/// 굴러다니는 것이 이 게임이 원하는 그림입니다. 오버쿡에서 접시를 아무 카운터에나
/// 두는 것과 같고, 치우는 일이 남는 것도 같습니다.
///
/// 4장의 문법도 이걸로 완성됩니다. **"놓는다" 가 목적지에서만 되던 것**이 구멍이었습니다.
///
/// <code>
/// 집는다 → 옮긴다 → 짧게 작업한다 → 놓는다
/// </code>
/// </summary>
public class DroppedCargo : MonoBehaviour
{
    [Header("주울 수 있는 거리 (m)")]
    [SerializeField, Min(0.3f)] private float reachRange = 1.5f;

    /// <summary>갑판에 놓여 있는 모든 물건. 줍는 쪽이 여기서 찾는다.</summary>
    public static IReadOnlyList<DroppedCargo> All => AllDropped;

    private static readonly List<DroppedCargo> AllDropped = new List<DroppedCargo>();

    /// <summary>무엇이 놓여 있는지</summary>
    public Cargo Kind { get; private set; }

    /// <summary>주울 수 있는 거리</summary>
    public float ReachRange => reachRange;

    private void OnEnable()
    {
        if (!AllDropped.Contains(this))
        {
            AllDropped.Add(this);
        }
    }

    private void OnDisable()
    {
        AllDropped.Remove(this);
    }

    /// <summary>주어진 위치에서 손이 닿는지</summary>
    public bool IsInReach(Vector3 worldPosition)
    {
        return (worldPosition - transform.position).sqrMagnitude <= reachRange * reachRange;
    }

    /// <summary>집어간다. 물건은 사라진다.</summary>
    public Cargo Take()
    {
        Cargo kind = Kind;
        Destroy(gameObject);
        return kind;
    }

    /// <summary>
    /// 물건을 갑판에 내려놓는다.
    ///
    /// 발밑이 아니라 **한 발 앞**에 둡니다. 발밑에 두면 몸에 가려 안 보이고,
    /// 다시 주울 때 자기 몸과 겹쳐서 어디 있는지 헷갈립니다.
    ///
    /// 높이는 지금 서 있는 갑판의 바닥에 맞춥니다. 3층이라 그냥 두면
    /// 아래층 허공에 떠 있거나 위층 바닥에 묻힙니다.
    /// </summary>
    public static DroppedCargo Drop(Cargo kind, Transform who)
    {
        if (kind == Cargo.None || who == null)
        {
            return null;
        }

        Vector3 where = who.position + who.forward * 0.6f;

        ShipDeck deck = ShipDeck.At(where);
        if (deck != null)
        {
            where.y = deck.SurfaceY;
        }

        GameObject made = BuildVisual(kind);

        // ⚠ 갑판 **위에 얹습니다.** 그냥 놓으면 원점이 가운데라 절반이 묻혀서,
        //    가뜩이나 작은 것이 더 작아 보이고 난간 뒤로 사라집니다.
        where.y += LiftOf(made, kind);

        made.transform.SetPositionAndRotation(where, Quaternion.identity);

        DroppedCargo dropped = made.AddComponent<DroppedCargo>();
        dropped.Kind = kind;

        return dropped;
    }

    /// <summary>
    /// 회색 큐브로 먼저 만든다. 진짜 에셋은 나중이다. (5장)
    /// 종류마다 모양과 색을 다르게 둬야 **갑판에 뭐가 굴러다니는지 한눈에 읽힙니다.**
    ///
    /// ⚠ **크기는 사람 키에 맞춰야 합니다.**
    ///
    ///    포탄이 0.32m 였습니다. 키 1m 회색 큐브 플레이어 시절 값인데,
    ///    2.7m 캐릭터를 씌우면서 안 고쳐서 **자갈만 해졌습니다.**
    ///    난간(높이 1.26m) 뒤로 넘어가면 아예 안 보입니다.
    /// </summary>
    private static GameObject BuildVisual(Cargo kind)
    {
        PrimitiveType shape;
        Color color;
        Vector3 size;

        switch (kind)
        {
            case Cargo.Ammo:
                shape = PrimitiveType.Sphere;
                color = new Color(0.15f, 0.15f, 0.18f);
                size = Vector3.one * 0.7f;
                break;

            case Cargo.Plank:
                shape = PrimitiveType.Cube;
                color = new Color(0.55f, 0.38f, 0.22f);
                size = new Vector3(1.7f, 0.26f, 0.6f);
                break;

            default:
                shape = PrimitiveType.Cylinder;
                color = new Color(0.35f, 0.62f, 0.90f);
                size = new Vector3(0.65f, 0.4f, 0.65f);
                break;
        }

        GameObject made = GameObject.CreatePrimitive(shape);
        made.name = $"Dropped_{kind}";

        // 콜라이더는 끈다. 켜두면 갑판에 던져둔 것에 걸려 넘어진다.
        // 줍는 것은 거리로 판정하므로 부딪힐 필요가 없다.
        Collider hit = made.GetComponent<Collider>();
        if (hit != null)
        {
            Object.Destroy(hit);
        }

        made.transform.localScale = size;

        Renderer draw = made.GetComponent<Renderer>();
        if (draw != null)
        {
            draw.material.color = color;
        }

        return made;
    }

    /// <summary>
    /// 갑판 위에 **얹히는** 높이. 원점이 가운데라 그냥 놓으면 절반이 묻힙니다.
    ///
    /// ⚠ 실린더는 유니티 기본 높이가 2 라서 `localScale.y` 가 곧 반높이입니다.
    ///    구와 큐브는 높이가 1 이라 반이 `localScale.y * 0.5` 입니다.
    /// </summary>
    private static float LiftOf(GameObject made, Cargo kind)
    {
        Vector3 size = made.transform.localScale;

        return kind == Cargo.Water ? size.y : size.y * 0.5f;
    }
}
