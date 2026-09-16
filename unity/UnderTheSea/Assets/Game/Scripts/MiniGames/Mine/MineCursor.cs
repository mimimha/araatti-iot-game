using UnityEngine;

/// <summary>
/// 발밑 칸을 네모로 표시한다.
///
/// **왜 필요한가** — 조준이 발밑이라(MINE.md 6장) 어느 칸에 서 있는지가 곧 조준선이다.
/// 그런데 탑뷰는 20m 위에서 내려다보므로 캐릭터가 몇 픽셀밖에 안 된다.
/// 공개 7초에 움직여 시작 칸을 정하려면 **내가 지금 어느 칸인지**가 보여야 한다.
///
/// **속을 채우지 않고 테두리만 그린다.** 채우면 그 칸의 도안이 가려진다.
/// 하필 제일 중요한 칸이 가려진다 — 지금 고르고 있는 칸이다.
///
/// 칸 크기는 <see cref="MineGrid"/> 에서 읽는다. 격자를 바꾸면 따라간다.
///
/// ⚠ **높이는 <see cref="MineGridView"/> 에 물어본다.** 파인 칸은 내려가 있어서
///   평면 높이에 그리면 구멍 위에 붕 뜬다. 깊이를 여기서 따로 계산하면
///   나중에 깊이를 바꿀 때 한쪽만 고쳐서 또 따로 논다.
/// </summary>
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class MineCursor : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 찾는다.")]
    [SerializeField] private MineGrid grid;

    [Tooltip("칸 윗면 높이를 물어본다. 비워두면 격자에서 찾는다. 없으면 평면 높이로 그린다.")]
    [SerializeField] private MineGridView view;

    [Tooltip("따라다닐 사람. 비워두면 씬의 MineDigger 를 찾는다.")]
    [SerializeField] private Transform follow;

    [Header("모양")]
    [Tooltip("칸 크기 대비 테두리 두께. 0.1 이면 한 변의 10%.")]
    [SerializeField, Range(0.02f, 0.3f)] private float thickness = 0.09f;

    [Tooltip("바닥에서 띄우는 높이(m). 0 이면 블록과 겹쳐 지글거린다.")]
    [SerializeField, Min(0f)] private float lift = 0.03f;

    [Tooltip("판 밖으로 나가면 숨긴다.")]
    [SerializeField] private bool hideOutside = true;

    private MeshFilter _filter;
    private MeshRenderer _renderer;
    private Mesh _mesh;

    /// <summary>지금 가리키는 칸. 판 밖이면 (-1, -1).</summary>
    public Vector2Int Cell { get; private set; } = new Vector2Int(-1, -1);

    private void Awake()
    {
        _filter = GetComponent<MeshFilter>();
        _renderer = GetComponent<MeshRenderer>();

        if (grid == null) grid = FindAnyObjectByType<MineGrid>(FindObjectsInactive.Include);
        if (view == null && grid != null) view = grid.GetComponent<MineGridView>();

        if (follow == null)
        {
            var digger = FindAnyObjectByType<MineDigger>(FindObjectsInactive.Include);
            if (digger != null) follow = digger.transform;
        }

        Build();
    }

    /// <summary>다른 사람을 따라가게 한다. 10단계에서 턴 주인이 바뀔 때 쓴다.</summary>
    public void Follow(Transform target)
    {
        follow = target;
    }

    private void Build()
    {
        if (grid == null) return;

        float cell = grid.CellSize;
        float outer = cell * 0.5f;
        float inner = outer - cell * thickness;
        if (inner <= 0f) inner = outer * 0.5f;

        // 가운데가 뚫린 네모. 사각형 네 장이면 된다.
        float[][] rects =
        {
            new[] { -outer,  inner,  outer,  outer },   // 위
            new[] { -outer, -outer,  outer, -inner },   // 아래
            new[] {  inner, -inner,  outer,  inner },   // 오른쪽
            new[] { -outer, -inner, -inner,  inner },   // 왼쪽
        };

        var verts = new Vector3[rects.Length * 4];
        var tris = new int[rects.Length * 6];

        for (int i = 0; i < rects.Length; i++)
        {
            float x0 = rects[i][0], z0 = rects[i][1], x1 = rects[i][2], z1 = rects[i][3];
            int v = i * 4;
            int t = i * 6;

            verts[v + 0] = new Vector3(x0, 0f, z0);
            verts[v + 1] = new Vector3(x1, 0f, z0);
            verts[v + 2] = new Vector3(x1, 0f, z1);
            verts[v + 3] = new Vector3(x0, 0f, z1);

            tris[t + 0] = v; tris[t + 1] = v + 3; tris[t + 2] = v + 2;
            tris[t + 3] = v; tris[t + 4] = v + 2; tris[t + 5] = v + 1;
        }

        _mesh = new Mesh { name = "MineCursor" };
        _mesh.vertices = verts;
        _mesh.triangles = tris;
        _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();

        _filter.sharedMesh = _mesh;
    }

    // ------------------------------------------------------------
    // 네트워크 전환용 덧붙임 (광산 서버화 3단계)
    // ------------------------------------------------------------

    /// <summary>
    /// 밖에서 칸을 정해 주는 중인가. 켜지면 <see cref="follow"/> 를 보지 않는다.
    ///
    /// 네트워크에서는 **서버가 고른 칸**을 그대로 가리켜야 한다. 각자 자기 화면의
    /// 캐릭터 자리로 계산하면, 보간된 자리 때문에 칸 경계에서 한 칸씩 어긋난다.
    /// 그러면 표시된 칸과 실제로 파이는 칸이 달라진다.
    /// </summary>
    public bool DrivenExternally { get; private set; }

    /// <summary>
    /// 서버가 정한 칸을 가리킨다. (-1, -1) 이면 판 밖이라 숨긴다.
    ///
    /// 혼자 하는 씬에서는 아무도 부르지 않으므로 예전 그대로 발밑을 따라간다.
    /// </summary>
    public void ShowCell(int x, int y)
    {
        DrivenExternally = true;

        if (grid == null || _renderer == null) return;

        bool inside = grid.InBounds(x, y);
        Cell = inside ? new Vector2Int(x, y) : new Vector2Int(-1, -1);

        if (!inside)
        {
            if (hideOutside) _renderer.enabled = false;
            return;
        }

        _renderer.enabled = true;

        Vector3 surface = view != null ? view.CellSurface(x, y) : grid.CellToWorld(x, y);
        transform.position = surface + Vector3.up * lift;
    }

    /// <summary>다시 발밑을 따라가게 한다.</summary>
    public void ReleaseExternalDrive()
    {
        DrivenExternally = false;
    }

    private void LateUpdate()
    {
        // 밖에서 칸을 정해 주는 중이면 스스로 계산하지 않는다.
        if (DrivenExternally) return;

        if (grid == null || follow == null || _renderer == null) return;

        // 캐릭터는 Update 에서 움직인다. LateUpdate 라야 한 프레임 안 밀린다.
        bool inside = grid.WorldToCell(follow.position, out int x, out int y);

        Cell = inside ? new Vector2Int(x, y) : new Vector2Int(-1, -1);

        if (!inside)
        {
            if (hideOutside) _renderer.enabled = false;
            return;
        }

        _renderer.enabled = true;

        // 파인 칸은 내려가 있다. 그 높이는 MineGridView 가 안다.
        Vector3 surface = view != null ? view.CellSurface(x, y) : grid.CellToWorld(x, y);

        // 살짝 띄워야 블록 윗면과 겹쳐 지글거리지 않는다.
        transform.position = surface + Vector3.up * lift;
    }

    private void OnDestroy()
    {
        // 런타임에 만든 메시는 스스로 치운다. 안 그러면 재생할 때마다 쌓인다.
        if (_mesh != null) Destroy(_mesh);
    }
}
