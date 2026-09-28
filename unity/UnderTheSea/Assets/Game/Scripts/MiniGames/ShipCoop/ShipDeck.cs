using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 갑판 한 층. (SHIPCOOP.md 4장)
///
/// 두 가지에 쓰입니다.
///   📷 카메라   지금 사람이 있는 층을 비춘다 (ShipCoopCamera)
///   ⚠ 알림     "뒷갑판 침수!" 처럼 **어느 층인지** 말해준다 (9장)
///
/// 두 번째가 중요합니다. 갑판이 3층이 되면서 **화면 밖에서 벌어지는 일**이 생겼습니다.
/// 앞갑판에 있는데 뒷갑판에 구멍이 나면 눈에 안 보입니다. 어느 층인지 말해주지 않으면
/// 알림을 보고도 어디로 뛸지 모릅니다.
///
/// 층은 앞뒤(z)로만 나뉘고 겹치지 않습니다. 계단은 아래층 범위 안에 놓이므로,
/// 계단을 다 올라가 위층에 발을 디뎌야 층이 바뀝니다. 경계에서 왔다 갔다 하지 않습니다.
/// </summary>
/// <remarks>
/// ExecuteAlways 인 이유 — 플레이를 누르지 않아도 목록에 올라야 합니다.
/// 배치 도구와 Scene view 가 "이 자리는 어느 층인가" 를 물어보기 때문입니다.
/// 하는 일이 등록뿐이라 에디트 모드에서 돌아도 안전합니다.
/// </remarks>
[ExecuteAlways]
public class ShipDeck : MonoBehaviour
{
    [Header("이 층의 이름 — 알림에 그대로 나온다")]
    [SerializeField] private string deckName = "갑판";

    [Header("이 층을 어디서 볼지 (9장)")]
    [Tooltip("끄면 카메라의 기본 구도를 쓴다.\n\n" +
             "층마다 생김새가 다른데 같은 구도를 쓰면 가려진다.\n" +
             "중간갑판은 뒤에 선미루가 서 있어서, 같은 높이로 보면 **카메라가 그 안에 들어간다.**")]
    [SerializeField] private bool overrideCamera = false;

    [Tooltip("배에서 뒤로 얼마나 물러날지 (m)")]
    [SerializeField, Min(1f)] private float cameraDistance = 13f;

    [Tooltip("이 갑판 면보다 얼마나 위에 있을지 (m).\n" +
             "앞을 가리는 구조물보다 높아야 한다.")]
    [SerializeField, Min(0f)] private float cameraHeight = 7f;

    [Tooltip("갑판 면에서 이만큼 위를 본다 (m)")]
    [SerializeField] private float cameraLookHeight = 1.5f;

    [Tooltip("갑판 한가운데보다 이만큼 앞을 본다 (m)")]
    [SerializeField] private float cameraLookAhead = 6f;

    /// <summary>이 층만의 구도를 쓰는지</summary>
    public bool OverridesCamera => overrideCamera;

    /// <summary>거리 · 높이 · 보는 높이 · 보는 앞쪽. 순서대로.</summary>
    public Vector4 CameraView =>
        new Vector4(cameraDistance, cameraHeight, cameraLookHeight, cameraLookAhead);

    [Header("이 층에 있을 때 감출 것")]
    [Tooltip("카메라와 이 층 사이를 가로막는 배 부분들.\n\n" +
             "중간갑판에 서면 **뒷갑판 바닥과 난간이 눈앞을 가립니다.**\n" +
             "카메라를 아무리 높여도 그 사이에 있으니 소용이 없습니다. 아예 감춰야 합니다.\n\n" +
             "층을 벗어나면 도로 보입니다. 돛대나 밧줄처럼 얇은 것은 넣지 않습니다.")]
    [SerializeField] private Renderer[] hideWhenHere;

    [Header("범위 (비워두면 콜라이더·렌더러에서 가져온다)")]
    [Tooltip("켜면 아래 크기를 쓴다. 끄면 이 오브젝트의 실제 크기를 쓴다.")]
    [SerializeField] private bool overrideSize = false;

    [SerializeField] private Vector3 size = new Vector3(10f, 1f, 14f);

    /// <summary>씬에 있는 모든 층. 뱃머리 쪽이 뒤에 온다.</summary>
    public static IReadOnlyList<ShipDeck> All => AllDecks;

    private static readonly List<ShipDeck> AllDecks = new List<ShipDeck>();

    /// <summary>알림에 쓰는 이름</summary>
    public string DeckName => deckName;

    /// <summary>걸어다니는 면의 높이</summary>
    public float SurfaceY => Area.max.y;

    /// <summary>카메라가 가운데에 둘 자리. 갑판 면 높이로 맞춘다.</summary>
    public Vector3 Center
    {
        get
        {
            Bounds area = Area;
            return new Vector3(area.center.x, area.max.y, area.center.z);
        }
    }

    /// <summary>이 층이 차지하는 공간</summary>
    public Bounds Area
    {
        get
        {
            if (overrideSize)
            {
                return new Bounds(transform.position, size);
            }

            Collider box = GetComponent<Collider>();
            if (box != null)
            {
                return box.bounds;
            }

            Renderer draw = GetComponent<Renderer>();
            if (draw != null)
            {
                return draw.bounds;
            }

            return new Bounds(transform.position, size);
        }
    }

    private void OnEnable()
    {
        if (!AllDecks.Contains(this))
        {
            AllDecks.Add(this);
            AllDecks.Sort((a, b) => a.Area.center.z.CompareTo(b.Area.center.z));
        }
    }

    private void OnDisable()
    {
        AllDecks.Remove(this);
    }

    /// <summary>씬을 훑어 목록을 다시 만든다. 비어 있을 때만 부른다.</summary>
    private static void Rebuild()
    {
        AllDecks.Clear();
        AllDecks.AddRange(FindObjectsByType<ShipDeck>(FindObjectsInactive.Exclude, FindObjectsSortMode.None));
        AllDecks.Sort((a, b) => a.Area.center.z.CompareTo(b.Area.center.z));
    }

    /// <summary>
    /// 이 층에 있는 동안 가로막는 것들을 감춘다. 층을 벗어나면 도로 보인다.
    ///
    /// 지우는 것이 아니라 **렌더러만 끕니다.** 콜라이더는 그대로라 걸어다니는 데는
    /// 아무 영향이 없고, 위층에 올라가면 다시 나타납니다.
    /// </summary>
    public void HideBlockers(bool hidden)
    {
        if (hideWhenHere == null)
        {
            return;
        }

        for (int i = 0; i < hideWhenHere.Length; i++)
        {
            if (hideWhenHere[i] != null)
            {
                hideWhenHere[i].enabled = !hidden;
            }
        }
    }

    /// <summary>이 위치가 이 층의 앞뒤·좌우 범위 안인지. 높이는 보지 않는다.</summary>
    public bool CoversHorizontally(Vector3 worldPosition)
    {
        Bounds area = Area;

        return worldPosition.x >= area.min.x && worldPosition.x <= area.max.x
            && worldPosition.z >= area.min.z && worldPosition.z <= area.max.z;
    }

    /// <summary>
    /// 이 위치가 어느 층인지. 없으면 가장 가까운 층.
    ///
    /// 앞뒤(z)로만 나뉘므로 좌우·앞뒤 범위만 봅니다. 계단에 서 있어도
    /// **계단이 놓인 아래층**으로 칩니다. 위층에 발을 디뎌야 바뀝니다.
    /// </summary>
    public static ShipDeck At(Vector3 worldPosition)
    {
        // 목록이 비어 있으면 한 번 훑어서 채운다.
        // 도메인 리로드 직후나 스크립트가 켜지기 전에 물어보는 경우가 있다.
        if (AllDecks.Count == 0)
        {
            Rebuild();
        }

        ShipDeck nearest = null;
        float nearestSqr = float.MaxValue;

        for (int i = 0; i < AllDecks.Count; i++)
        {
            ShipDeck deck = AllDecks[i];

            if (deck == null)
            {
                continue;
            }

            if (deck.CoversHorizontally(worldPosition))
            {
                return deck;
            }

            float sqr = (deck.Area.center - worldPosition).sqrMagnitude;
            if (sqr < nearestSqr)
            {
                nearestSqr = sqr;
                nearest = deck;
            }
        }

        return nearest;
    }

    /// <summary>Scene view 에서 층 범위를 보여준다.</summary>
    private void OnDrawGizmosSelected()
    {
        Bounds area = Area;
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.35f);
        Gizmos.DrawWireCube(area.center, area.size);
    }
}
