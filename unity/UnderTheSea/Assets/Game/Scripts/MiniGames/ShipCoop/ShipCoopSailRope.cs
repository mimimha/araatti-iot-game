using UnityEngine;

/// <summary>
/// 🪢 돛 자리의 **밧줄.** 활대에서 내려와 돛 담당의 손을 지나 갑판에 닿는다. 연출 전용이다. (SHIPCOOP.md 4장)
///
/// <code>
///   아무도 없을 때   활대 → 돛대 밑동(자리) 으로 곧게 매달려 있다
///   누가 붙었을 때   활대 → 위쪽 손 → 아래쪽 손 → 그 사람 발밑
/// </code>
///
/// <b>왜 필요한가.</b> "돛을 당긴다" 는데 화면에는 당길 것이 없었다. 캐릭터가 돛대 옆에서 팔만
/// 움직이면 무엇을 하는지 모른다. 밧줄이 손에 걸려 있으면 한 번 보고 안다.
///
/// <b>어떻게 그리나.</b> 밧줄 모델을 쓰지 않고 <see cref="LineRenderer"/> 로 그린다. 손이 매 프레임
/// 움직이는데 메시로는 그걸 따라 늘이고 줄일 수 없다. 재질은 배의 밧줄 재질(M_Ship_SailsRope_01)을
/// 그대로 써서 배의 삭구와 같은 색이다.
///
/// <b>활대 자리는 잰다.</b> 이 배의 돛대 · 활대는 이름이 프리팹 인스턴스에서 바뀐 것이라(MastMid …)
/// 또 바뀔 수 있다. 그래서 돛 메시(StylShip_Sail*)들 중 자리에서 가장 가까운 것의 **위쪽 끝**을
/// 활대로 본다. 돛 접힘 연출(<see cref="ShipCoopSail"/>)이 같은 점을 붙잡고 접는다.
///
/// 손 자리는 <see cref="ShipCoopStationPose"/> 가 정한다. 이 스크립트는 그 손을 **읽어서** 그린다.
/// </summary>
public class ShipCoopSailRope : MonoBehaviour
{
    [Header("연결 — 비워두면 찾는다")]
    [Tooltip("돛 자리. 비워두면 씬에서 찾는다.")]
    [SerializeField] private SailTask sail;

    [Tooltip("밧줄 재질. 배치 도구가 배의 밧줄 재질을 넣는다. 비워두면 기본 재질.")]
    [SerializeField] private Material ropeMaterial;

    [Header("모양")]
    [Tooltip("밧줄 굵기 (m).")]
    [SerializeField, Range(0.01f, 0.15f)] private float thickness = 0.045f;

    [Tooltip("활대에서 자리 쪽으로 이만큼 비켜 매단다 (m). 0 이면 밧줄이 돛대 속에 묻힌다.")]
    [SerializeField, Range(0f, 1.5f)] private float anchorOut = 0.45f;

    [Tooltip("아무도 없을 때 밧줄 아래 끝을 갑판에서 이만큼 위에 둔다 (m).")]
    [SerializeField, Range(0f, 2f)] private float idleTailLift = 0.9f;

    private LineRenderer _line;
    private Transform _anchorParent;
    private Vector3 _anchorLocal;
    private Vector3 _tailLocal;
    private bool _ready;

    private readonly Vector3[] _points = new Vector3[4];

    /// <summary>활대의 밧줄 매는 점 (월드). 손 자세가 밧줄 방향을 잴 때 본다.</summary>
    public Vector3 Anchor => _ready ? _anchorParent.TransformPoint(_anchorLocal) : transform.position + Vector3.up * 8f;

    /// <summary>밧줄 아래 끝 — 자리 갑판 (월드).</summary>
    public Vector3 Tail => _ready ? _anchorParent.TransformPoint(_tailLocal) : transform.position;

    private void Awake()
    {
        if (sail == null)
        {
            sail = FindAnyObjectByType<SailTask>(FindObjectsInactive.Include);
        }

        if (sail == null)
        {
            Debug.LogWarning($"[{name}] SailTask 를 찾지 못했습니다. 밧줄을 안 그립니다.", this);
            return;
        }

        // 자리에서 가장 가까운 돛의 위쪽 끝 = 활대.
        Transform bestCloth = null;
        Bounds best = default;
        float bestDist = float.MaxValue;

        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("StylShip_Sail"))
            {
                continue;
            }

            Renderer draw = filter.GetComponent<Renderer>();
            if (draw == null) continue;

            Vector3 flat = draw.bounds.center - sail.transform.position;
            flat.y = 0f;
            float d = flat.sqrMagnitude;

            if (d < bestDist)
            {
                bestDist = d;
                best = draw.bounds;
                bestCloth = filter.transform;
            }
        }

        _anchorParent = transform;

        Vector3 anchorWorld;
        if (bestCloth != null)
        {
            anchorWorld = new Vector3(best.center.x, best.max.y, best.center.z);
        }
        else
        {
            Debug.LogWarning($"[{name}] 돛 메시를 못 찾아 자리 위 8m 를 활대로 씁니다.", this);
            anchorWorld = sail.transform.position + Vector3.up * 8f;
        }

        // 자리 쪽으로 비켜서 돛대 밖으로 나오게 한다.
        Vector3 toward = sail.transform.position - anchorWorld;
        toward.y = 0f;
        if (toward.sqrMagnitude > 1e-4f)
        {
            anchorWorld += toward.normalized * anchorOut;
        }

        _anchorLocal = _anchorParent.InverseTransformPoint(anchorWorld);
        _tailLocal = _anchorParent.InverseTransformPoint(sail.transform.position);

        _line = GetComponent<LineRenderer>();
        if (_line == null)
        {
            _line = gameObject.AddComponent<LineRenderer>();
        }

        _line.useWorldSpace = true;
        _line.startWidth = thickness;
        _line.endWidth = thickness;
        _line.numCapVertices = 3;
        _line.numCornerVertices = 3;
        _line.textureMode = LineTextureMode.Tile;
        _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        _line.receiveShadows = false;

        if (ropeMaterial != null)
        {
            _line.sharedMaterial = ropeMaterial;
        }

        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready)
        {
            return;
        }

        Vector3 anchor = Anchor;

        // 붙은 사람의 손을 찾는다. 첫 사람만 — 밧줄은 하나다.
        ShipCoopStationPose puller = null;

        for (int i = 0; i < sail.Workers.Count; i++)
        {
            TaskWorker w = sail.Workers[i];
            if (w == null) continue;

            var pose = w.GetComponent<ShipCoopStationPose>();
            if (pose != null && pose.IsPosing)
            {
                puller = pose;
                break;
            }
        }

        if (puller == null)
        {
            // 곧게 매달린 밧줄.
            _line.positionCount = 2;
            _line.SetPosition(0, anchor);
            _line.SetPosition(1, Tail + Vector3.up * idleTailLift);
            return;
        }

        Vector3 upper = puller.UpperHand;
        Vector3 lower = puller.LowerHand;
        Vector3 feet = puller.transform.position;
        feet.y += 0.05f;

        _points[0] = anchor;
        _points[1] = upper;
        _points[2] = lower;
        _points[3] = feet;

        _line.positionCount = 4;
        _line.SetPositions(_points);
    }
}
