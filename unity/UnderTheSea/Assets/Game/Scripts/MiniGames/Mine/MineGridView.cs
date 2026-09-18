using System.Collections.Generic;
using UnityEngine;

/// <summary>격자 위에 무엇을 겹쳐 보여줄 것인가.</summary>
public enum MineOverlay
{
    /// <summary>아무것도 안 겹친다. 채굴 중.</summary>
    None,

    /// <summary>
    /// 목표 그림. 공개 7초와 힌트에 쓴다.
    ///
    /// **무른 돌 바탕에 검은 돌로 그림만** 보여준다. 돌 종류는 감춘다 —
    /// 그림을 외우는 시간이라 단단한 돌이 섞여 보이면 방해만 된다.
    /// </summary>
    Drawing,

    /// <summary>
    /// 채점 결과. **내가 판 그림**을 보여준다. 도안 보기와 같은 방식이라
    /// 방금 본 목표와 나란히 비교된다.
    ///
    /// 맞은 칸·틀린 칸을 초록·빨강으로 칠해봤으나 되돌렸다. 그림을 보는 자리인데
    /// 색이 얼룩덜룩하면 무엇을 그렸는지가 안 보인다. 맞고 틀림은 점수와
    /// AI 한 줄 평이 말해준다. (MINE.md 7장)
    /// </summary>
    Result,
}

/// <summary>
/// <see cref="MineGrid"/> 를 화면에 그린다. 칸마다 큐브 하나.
///
/// 20×20 이면 400개, 40×40 이어도 1600개라 그냥 생성해도 감당된다.
/// 느려지면 그때 메시를 합친다. 먼저 만들고 나중에 최적화한다.
///
/// **파인 칸은 지우지 않고 살짝 내린다.**
/// 지워버리면 그 자리에 바닥이 없어져 캐릭터가 빠진다.
/// 홈처럼 파인 것으로 보이면서 계속 걸어다닐 수 있어야 한다.
///
/// 겹쳐 보여주는 방식은 <see cref="MineOverlay"/> 가 정하고,
/// <see cref="MineGame"/> 이 단계마다 <see cref="SetOverlay"/> 로 바꾼다.
/// 도안이든 결과든 **바탕은 무른 돌 하나로 통일**한다. 돌 종류가 섞여 보이면
/// 그림을 읽기 어렵다.
/// </summary>

[RequireComponent(typeof(MineGrid))]
public class MineGridView : MonoBehaviour
{
    [Header("칸 모양")]
    [Tooltip("칸 사이 틈. 0 이면 딱 붙는다. 격자가 보이게 하려면 조금 준다.")]
    [SerializeField, Range(0f, 0.2f)] private float gap = 0.04f;

    [Tooltip("칸 블록의 두께(m). 윗면은 바닥 높이에 고정이고 아래로 두꺼워진다.\n" +
             "두꺼울수록 판 가장자리와 파인 구멍이 깊어 보인다.")]
    [SerializeField, Min(0.05f)] private float blockHeight = 1f;

    [Header("파였을 때")]
    [Tooltip("파인 칸이 내려가는 깊이(m). 캐릭터가 넘어다닐 수 있을 만큼만.")]
    [SerializeField, Min(0.02f)] private float digDepth = 0.25f;

    [Tooltip("금 간 칸이 내려가는 깊이(m). 파인 것과 구분되게 얕게.")]
    [SerializeField, Min(0f)] private float crackDepth = 0.07f;

    [Header("머티리얼 (비우면 기본 흰색 그대로)")]
    [Tooltip("무른 돌. 한 번에 깨진다.")]
    [SerializeField] private Material softMaterial;

    [Tooltip("단단한 돌. 두 번 쳐야 깨진다. 무른 돌과 눈에 띄게 달라야 한다.")]
    [SerializeField] private Material hardMaterial;

    [Tooltip("파인 바닥. 비우면 무른 돌 것을 쓴다.")]
    [SerializeField] private Material dugMaterial;

    [Tooltip("도안과 결과를 보여주는 동안 쓸 무늬 없는 돌. 비우면 안 바꾼다.")]
    [SerializeField] private Material flatMaterial;

    [Tooltip("목표 도안에서 **파야 하는 칸**에 쓸 돌. 비워두면 예전처럼 무늬 없는 돌 위에 색으로만 그린다. 넣으면 공개와 힌트에서 바탕은 무른 돌, 도안 칸만 이 돌이 된다.")]
    [SerializeField] private Material drawingMaterial;

    [Tooltip("도안 칸의 색. drawingMaterial 을 넣었을 때만 쓴다. 재질이 그대로 보이게 흰색 근처로 두고, 바탕과 대비가 부족하면 낮춘다.")]
    [SerializeField, ColorUsage(false, true)]
    private Color drawingStoneColor = Color.white;

    [Tooltip("무늬 없는 돌을 쓸 때 칸 색에 곱할 값. 색은 MaterialPropertyBlock 으로 " +
             "가는데 그것이 머티리얼의 밑색을 덮어쓰므로, 머티리얼을 고쳐서는 밝기를 못 바꾼다.")]
    [SerializeField, Range(0.05f, 1f)] private float flatColorScale = 0.4f;

    [Tooltip("무늬 없는 돌을 쓸 때 색조를 얼마나 지울 것인가. 1 이면 완전한 회색.")]
    [SerializeField, Range(0f, 1f)] private float flatDesaturate = 1f;

    [Tooltip("채굴 중에도 무늬 없는 돌을 쓴다. 끄면 도안과 결과에서만 쓴다.")]
    [SerializeField] private bool flatAlways;

    [Tooltip("칸마다 90도씩 무작위로 돌려 텍스처 반복을 깬다.\n" +
             "끄면 400칸이 똑같이 보여 격자무늬가 도드라진다.")]
    [SerializeField] private bool varyRotation = true;

    [Tooltip("윗면 모서리를 깎는 폭(m). 0 이면 각진 큐브 그대로다. 깎인 띠가 빛을 받아 돌마다 가는 하이라이트 선이 생긴다. 어두운 광산에서 칸의 형태를 살리는 것이 이 값이다.")]
    [SerializeField, Range(0f, 0.15f)] private float bevel = 0.04f;

    [Header("판 테두리")]
    [Tooltip("판 바깥을 둘러싸는 돌 테두리의 두께(칸). 0 이면 안 만든다. 콜라이더가 남아 캐릭터가 판 밖으로 못 나간다.")]
    [SerializeField, Range(0, 3)] private int borderRing = 1;

    [Tooltip("테두리 돌. 비우면 단단한 돌 재질을 쓰고, 그것도 없으면 무른 돌 것을 쓴다.")]
    [SerializeField] private Material borderMaterial;

    [Tooltip("테두리가 판보다 솟은 높이(m). 턱이 지면 판이 '만들어진 자리'로 읽힌다.")]
    [SerializeField, Range(0f, 1f)] private float borderRise = 0.6f;

    [Tooltip("테두리 색. 재질이 그대로 보이게 흰색 근처로 두고, 판과 구분이 필요하면 조절한다.")]
    [SerializeField, ColorUsage(false, true)]
    private Color borderColor = Color.white;

    [Tooltip("판을 둘러싸는 보이지 않는 벽의 높이(m). 0 이면 안 세운다. 캐릭터가 판 밖으로 못 나가게 막는다.")]
    [SerializeField, Range(0f, 10f)] private float borderWallHeight = 3f;

    [Tooltip("단단한 돌이 더 솟은 높이(m). 어두운 곳에서 실루엣으로 구분된다.\n" +
             "걸려 넘어질 정도로 크게 주면 안 된다.")]
    [SerializeField, Min(0f)] private float hardRise = 0.02f;

    [Tooltip("바닥 굴곡의 높이(m).\n" +
             "노멀맵은 표면 무늬만 준다. 울퉁불퉁해 보이려면 실루엣이 흔들려야 한다.\n" +
             "0 이면 반듯한 타일 바닥이 된다.")]
    [SerializeField, Range(0f, 0.25f)] private float heightJitter = 0.08f;

    [Tooltip("굴곡 하나가 몇 칸에 걸치는가.\n" +
             "작으면 칸마다 제각각 튀어 계단처럼 보이고, 크면 완만한 언덕이 된다.")]
    [SerializeField, Range(1f, 10f)] private float jitterSpan = 4f;

    [Header("금 (데칼)")]
    [Tooltip("금 간 칸 위에 얹을 데칼. Synty 의 Generic_Decal_Crack_01 등.\n" +
             "비우면 금을 안 그리고 높이 차이로만 표시한다.")]
    [SerializeField] private Material crackMaterial;

    [Tooltip("금 데칼 크기. 1 이면 칸을 꽉 채운다.")]
    [SerializeField, Range(0.3f, 1f)] private float crackScale = 0.85f;

    [Header("도안 표시 (개발용)")]
    [Tooltip("끄면 도안을 무시하고 안 팜/팜 두 색으로만 그린다. 실제 플레이에서는 꺼야 한다.\n" +
             "도안 자체는 MineGrid 에서 지정한다.")]
    [SerializeField] private bool showTarget = true;

    [Header("색")]
    // ⚠ 아래 두 색은 **HDR 로 연다.**
    //
    // 칸 색은 머티리얼의 `_BaseColor` 를 덮어쓰는 곱셈값이라, 텍스처를 밝히려면
    // 1 을 넘겨야 한다. 씬에는 실제로 1.83 같은 값이 들어 있었다.
    // 그런데 일반 Color 필드의 피커는 0~1 에서 자른다. 색조만 손보려고 피커를
    // 한 번 여는 것만으로 밝기가 함께 잘려 판이 어두워지고, 되돌릴 방법도 없었다.
    // ColorUsage 로 열어 두면 Intensity 슬라이더로 1 을 넘는 값을 다룰 수 있다.
    [Tooltip("건드릴 필요 없는 칸 — 무른 돌. 흙빛.")]
    [SerializeField, ColorUsage(false, true)]
    private Color intactColor = new Color(0.55f, 0.50f, 0.42f);

    [Tooltip("건드릴 필요 없는 칸 — 단단한 돌. 푸른 잿빛.\n" +
             "머티리얼이 무엇이든 색이 다르면 구분된다. 어두운 곳에서는 특히.")]
    [SerializeField, ColorUsage(false, true)]
    private Color hardIntactColor = new Color(0.42f, 0.46f, 0.52f);

    [Tooltip("칸마다 색을 흔드는 폭. 0 이면 모든 칸이 똑같아 플라스틱 한 장처럼 보인다. 칸 사이 폭은 이 값의 2배가 되는데, 그것이 무른 돌과 단단한 돌의 휘도 차이(6.9%)를 넘으면 밝은 단단한 칸이 어두운 무른 칸처럼 보여 구분이 무너진다. 0.034 가 그 한계다. 올릴수록 얼룩덜룩해진다.")]
    [SerializeField, Range(0f, 0.05f)] private float cellMottle = 0.015f;

    [Tooltip("판 칸. 채굴 중에도 결과 화면에도 이 색이다.")]
    [SerializeField] private Color dugColor = new Color(0.15f, 0.13f, 0.12f);

    [Tooltip("금 간 칸에 곱할 색. 1보다 작으면 어두워진다.")]
    [SerializeField] private Color crackTint = new Color(0.75f, 0.72f, 0.7f);

    [Tooltip("도안을 보여줄 때 그림이 되는 칸의 색. 무른 돌 바탕 위의 검은 돌.")]
    [SerializeField] private Color drawingColor = new Color(0.06f, 0.055f, 0.05f);

    [Tooltip("도안을 보여주는 동안 **이미 판 칸**의 색.\n" +
             "검은 도안과 하얀 바탕 사이의 회색이어야 한다.\n" +
             "dugColor 를 그대로 쓰면 도안과 같은 검정이라 힌트 때 둘이 안 갈린다.")]
    [SerializeField] private Color hintDugColor = new Color(0.6f, 0.6f, 0.6f);

    private MineGrid _grid;
    private Transform[] _cells;

    /// <summary>칸마다 하나씩. 금이 갔을 때만 켠다. crackMaterial 이 없으면 안 만든다.</summary>
    private Transform[] _cracks;

    /// <summary>부스러기. 같은 오브젝트에 붙어 있으면 쓰고, 없으면 안 쓴다.</summary>
    private MineDebris _debris;

    /// <summary>지금 무엇을 겹쳐 보여주는가. MineGame 이 단계마다 정한다.</summary>
    private MineOverlay _overlay = MineOverlay.None;

    private MaterialPropertyBlock _props;

    /// <summary>깎인 상자. 400칸이 한 장을 나눠 쓴다.</summary>
    private Mesh _blockMesh;

    /// <summary>켜지면 단단한 돌도 무른 돌처럼 그린다. <see cref="SetUniformStone"/></summary>
    private bool _uniformStone;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private Vector2Int _targetOffset;

    /// <summary>
    /// 도안을 겹쳐 보일 때 이만큼 밀어서 본다.
    ///
    /// 판정이 위치를 맞춰 채점하므로(MINE.md 7장), 결과 화면도 같은 기준으로
    /// 칠해야 색과 점수가 맞는다. 안 맞추면 100점인데 화면이 빨갛다.
    /// </summary>
    public void SetTargetOffset(Vector2Int offset)
    {
        _targetOffset = offset;
        RefreshAll();
    }

    private void Awake()
    {
        _grid = GetComponent<MineGrid>();
        _debris = GetComponent<MineDebris>();
        _props = new MaterialPropertyBlock();
        Build();
    }

    private void OnEnable()
    {
        if (_grid == null) return;

        _grid.OnCellChanged += HandleCellChanged;
        _grid.OnCellHit += HandleCellHit;
        _grid.OnTargetChanged += RefreshAll;
    }

    private void OnDisable()
    {
        if (_grid == null) return;

        _grid.OnCellChanged -= HandleCellChanged;
        _grid.OnCellHit -= HandleCellHit;
        _grid.OnTargetChanged -= RefreshAll;
    }

    private void OnDestroy()
    {
        // 코드로 만든 메시는 씬이 바뀌어도 안 없어진다. 직접 지운다.
        if (_blockMesh == null) return;

        if (Application.isPlaying) Destroy(_blockMesh);
        else DestroyImmediate(_blockMesh);

        _blockMesh = null;
    }

    private void Build()
    {
        int size = _grid.Size;
        _cells = new Transform[size * size];
        _cracks = crackMaterial != null ? new Transform[size * size] : null;

        float side = Mathf.Max(0.01f, _grid.CellSize - gap);

        // 깎인 상자를 한 장만 만들어 400칸이 나눠 쓴다.
        _blockMesh = bevel > 0f ? BuildBeveledCube(side, blockHeight, bevel) : null;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Cell_{x:00}_{y:00}";
                block.transform.SetParent(transform, false);

                // 텍스처 반복을 깬다. 시드가 아니라 좌표로 정해서 누가 봐도 같게 만든다.
                if (varyRotation)
                {
                    block.transform.localRotation =
                        Quaternion.Euler(0f, 90f * ((x * 7 + y * 13) % 4), 0f);
                }

                // ⚠ 메시만 바꾸고 BoxCollider 는 그대로 둔다. 캐릭터가 밟고 다니는
                //   판정이라 깎인 모양까지 따라갈 이유가 없다.
                if (_blockMesh != null && block.TryGetComponent(out MeshFilter filter))
                {
                    filter.sharedMesh = _blockMesh;
                }

                ApplyMaterial(block, x, y);

                // CellToWorld 는 평면 위 중심. 블록은 그 아래로 두께만큼 잠기게 놓는다.
                //
                // ⚠ 여기서는 SinkOf 를 안 쓴다. 처음에는 아무 칸도 안 파여 있고
                //   heightJitter 와 hardRise 가 둘 다 0 이라 결과가 같기 때문이다.
                //   **그 둘 중 하나라도 켜면 여기도 SinkOf 를 써야 한다.**
                //   안 그러면 시작하자마자 발밑 표시(MineCursor)가 그만큼 어긋난다.
                //   발밑 표시는 SinkOf 를 물어보기 때문이다.
                Vector3 top = _grid.CellToWorld(x, y);
                block.transform.position = top + Vector3.down * (blockHeight * 0.5f);
                block.transform.localScale = new Vector3(side, blockHeight, side);

                SetColor(block, Mottle(ColorFor(x, y, false), x, y));
                _cells[y * size + x] = block.transform;

                if (_cracks != null) _cracks[y * size + x] = CreateCrack(x, y);
            }
        }

        BuildBorder(size, side);
        BuildBorderWall(size);
    }

    /// <summary>
    /// 판 둘레에 <b>보이지 않는 벽</b>을 세운다. 캐릭터가 판 밖으로 못 나가게 막는다.
    ///
    /// ⚠ <b>테두리 블록의 콜라이더로는 못 막는다.</b> 캐릭터의 <c>CharacterController</c> 는
    ///   <c>stepOffset</c> 이 1.2 — 키(<c>height</c>)와 같은 값이라, 그보다 낮은 턱은
    ///   전부 걸어 올라간다. 실측으로 확인했다. 테두리를 1.2m 넘게 올리면 막히긴 하지만
    ///   그러면 판을 가리는 벽이 되어 보기 위해 둔 테두리의 뜻이 사라진다.
    ///
    /// 그래서 <b>보이는 것과 막는 것을 나눈다.</b> 테두리는 보기만 맡고, 막는 것은
    /// 렌더러 없는 콜라이더 넷이 맡는다. 덕분에 <see cref="borderRise"/> 는 순수하게
    /// 보기 좋은 값으로 정할 수 있다.
    ///
    /// 벽은 <b>판 가장자리</b>에 세운다. 테두리 턱에 부딪혀 멈추는 것으로 읽힌다.
    /// 아래로 1m 더 내려 파인 칸에 서 있어도 빠져나가지 못하게 한다.
    /// </summary>
    private void BuildBorderWall(int size)
    {
        if (borderWallHeight <= 0f) return;

        float half = size * _grid.CellSize * 0.5f;
        const float thick = 0.5f;

        float h = borderWallHeight;
        float centerY = (h - 1f) * 0.5f;   // -1 ~ h 를 덮는다
        float sizeY = h + 1f;
        float span = 2f * half + 2f * thick;

        var root = new GameObject("BorderWall");
        root.transform.SetParent(transform, false);

        Add(root, "Wall_X+", new Vector3(half + thick * 0.5f, centerY, 0f), new Vector3(thick, sizeY, span));
        Add(root, "Wall_X-", new Vector3(-half - thick * 0.5f, centerY, 0f), new Vector3(thick, sizeY, span));
        Add(root, "Wall_Z+", new Vector3(0f, centerY, half + thick * 0.5f), new Vector3(span, sizeY, thick));
        Add(root, "Wall_Z-", new Vector3(0f, centerY, -half - thick * 0.5f), new Vector3(span, sizeY, thick));

        // 테두리 블록의 콜라이더는 이제 필요 없다. 벽 바깥이라 닿을 일이 없고,
        // 84개가 물리 계산에 남을 이유도 없다.
        foreach (Transform child in transform)
        {
            if (!child.name.StartsWith("Border_")) continue;
            if (child.TryGetComponent(out Collider col)) Destroy(col);
        }

        static void Add(GameObject parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);

            var box = go.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
            go.transform.localPosition = center;
        }
    }

    /// <summary>
    /// 판 바깥을 돌로 둘러싼다.
    ///
    /// <b>판이 그냥 끊겨 있으면 떠 있는 것처럼 보인다.</b> 한 바퀴 두르고 살짝 턱을 주면
    /// 같은 판이 '만들어진 자리' 로 읽힌다.
    ///
    /// ⚠ <b>콜라이더를 남긴다.</b> 테두리가 곧 벽이라 캐릭터가 판 밖으로 못 나간다.
    ///
    /// 테두리는 <see cref="_cells"/> 에 넣지 않는다. 파이지도, 색이 바뀌지도, 발밑 표시에
    /// 잡히지도 않아야 한다. <c>MineGrid</c> 가 판 안쪽만 아는 것으로 이미 갈리지만,
    /// 배열에 섞으면 언젠가 한쪽만 고쳐서 따로 논다.
    ///
    /// 좌표만으로 정해지므로 네트워크에서 복제할 것이 없다. 모든 화면이 같게 그린다.
    /// </summary>
    private void BuildBorder(int size, float side)
    {
        if (borderRing <= 0) return;

        Material mat = borderMaterial != null ? borderMaterial
                     : hardMaterial != null ? hardMaterial
                     : softMaterial;

        for (int y = -borderRing; y < size + borderRing; y++)
        {
            for (int x = -borderRing; x < size + borderRing; x++)
            {
                // 판 안쪽은 건너뛴다. 테두리는 바깥 링뿐이다.
                if (x >= 0 && x < size && y >= 0 && y < size) continue;

                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Border_{x}_{y}";
                block.transform.SetParent(transform, false);

                if (varyRotation)
                {
                    block.transform.localRotation =
                        Quaternion.Euler(0f, 90f * ((x * 7 + y * 13) % 4), 0f);
                }

                if (_blockMesh != null && block.TryGetComponent(out MeshFilter filter))
                {
                    filter.sharedMesh = _blockMesh;
                }

                if (mat != null && block.TryGetComponent(out Renderer r)) r.sharedMaterial = mat;

                // 판과 같은 높이에서 borderRise 만큼 올린다.
                Vector3 top = _grid.CellToWorld(x, y) + Vector3.up * borderRise;
                block.transform.position = top + Vector3.down * (blockHeight * 0.5f);
                block.transform.localScale = new Vector3(side, blockHeight, side);

                SetColor(block, Mottle(borderColor, x, y));
            }
        }
    }

    /// <summary>
    /// 윗면 네 모서리를 깎은 상자를 만든다.
    ///
    /// <b>왜 메시를 코드로 만드는가.</b> 블록은 <c>localScale = (side, blockHeight, side)</c> 로
    /// **비균일 스케일**이 걸린다. 모델링 툴에서 만든 메시를 그대로 넣으면 깎인 폭이
    /// 축마다 다르게 늘어난다 — blockHeight 가 2 면 세로 쪽만 두 배로 두꺼워진다.
    /// 여기서는 실제 치수를 알고 만들므로 스케일을 미리 나눠 보정한다.
    ///
    /// <b>UV 가 이 작업의 절반이다.</b> 깎인 띠에 UV 를 안 주면 그 좁은 면에 텍스처가
    /// 늘어나 번진다. 윗면과 띠를 <b>하나의 평면 매핑</b>으로 이어 붙여, 돌 무늬가
    /// 윗면에서 모서리까지 끊기지 않고 넘어가게 한다.
    ///
    /// 옆면과 아랫면은 기본 큐브와 같게 둔다. 파인 구멍에서만 잠깐 보이는 면이다.
    /// </summary>
    private static Mesh BuildBeveledCube(float side, float height, float bevel)
    {
        // 월드에서 같은 폭으로 깎이도록 축별 스케일로 나눈다.
        float ix = Mathf.Clamp(bevel / Mathf.Max(0.0001f, side), 0f, 0.45f);
        float iy = Mathf.Clamp(bevel / Mathf.Max(0.0001f, height), 0f, 0.45f);

        float a = 0.5f - ix;    // 윗면이 줄어든 반폭
        float hd = 0.5f - iy;   // 깎인 띠가 끝나고 옆면이 시작하는 높이

        // 사각형을 한 바퀴 도는 네 귀퉁이. (x, z) 의 부호다.
        var corner = new[] { new Vector2(1, 1), new Vector2(1, -1), new Vector2(-1, -1), new Vector2(-1, 1) };

        var verts = new Vector3[40];
        var uvs = new Vector2[40];
        var tris = new int[60];
        int v = 0, t = 0;

        void Quad(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3,
                  Vector2 u0, Vector2 u1, Vector2 u2, Vector2 u3)
        {
            int b = v;
            verts[v] = p0; uvs[v++] = u0;
            verts[v] = p1; uvs[v++] = u1;
            verts[v] = p2; uvs[v++] = u2;
            verts[v] = p3; uvs[v++] = u3;

            tris[t++] = b; tris[t++] = b + 1; tris[t++] = b + 2;
            tris[t++] = b; tris[t++] = b + 2; tris[t++] = b + 3;
        }

        // 윗면과 띠는 같은 평면 매핑을 쓴다. 이래야 무늬가 모서리를 넘어간다.
        Vector2 Plane(Vector3 p) => new Vector2(p.x + 0.5f, p.z + 0.5f);

        // 윗면
        Vector3 t0 = new Vector3(-a, 0.5f, -a), t1 = new Vector3(-a, 0.5f, a);
        Vector3 t2 = new Vector3(a, 0.5f, a), t3 = new Vector3(a, 0.5f, -a);
        Quad(t0, t1, t2, t3, Plane(t0), Plane(t1), Plane(t2), Plane(t3));

        for (int k = 0; k < 4; k++)
        {
            Vector2 c = corner[k];
            Vector2 n = corner[(k + 1) % 4];

            Vector3 inA = new Vector3(c.x * a, 0.5f, c.y * a);
            Vector3 inB = new Vector3(n.x * a, 0.5f, n.y * a);
            Vector3 outA = new Vector3(c.x * 0.5f, hd, c.y * 0.5f);
            Vector3 outB = new Vector3(n.x * 0.5f, hd, n.y * 0.5f);

            // 깎인 띠
            Quad(inA, outA, outB, inB, Plane(inA), Plane(outA), Plane(outB), Plane(inB));

            // 옆면 — 띠 아래부터 바닥까지. 가로는 변을 따라, 세로는 높이를 따라 편다.
            Vector3 lowA = new Vector3(c.x * 0.5f, -0.5f, c.y * 0.5f);
            Vector3 lowB = new Vector3(n.x * 0.5f, -0.5f, n.y * 0.5f);

            bool alongZ = Mathf.Abs(c.x - n.x) < 0.001f;   // x 가 같으면 ±X 면이라 z 로 편다
            Vector2 Wall(Vector3 p) => new Vector2((alongZ ? p.z : p.x) + 0.5f, p.y + 0.5f);

            Quad(outA, lowA, lowB, outB, Wall(outA), Wall(lowA), Wall(lowB), Wall(outB));
        }

        // 아랫면 — 칸이 붙어 있어 거의 안 보이지만 뚫려 있으면 안 된다.
        Vector3 b0 = new Vector3(-0.5f, -0.5f, -0.5f), b1 = new Vector3(0.5f, -0.5f, -0.5f);
        Vector3 b2 = new Vector3(0.5f, -0.5f, 0.5f), b3 = new Vector3(-0.5f, -0.5f, 0.5f);
        Quad(b0, b1, b2, b3, Plane(b0), Plane(b1), Plane(b2), Plane(b3));

        var mesh = new Mesh { name = "MineBlock_Beveled" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;

        // 꼭짓점을 면마다 따로 두었으므로 여기서 각진 면이 나온다. 띠만 따로 빛을 받는다.
        mesh.RecalculateNormals();

        // ⚠ 탄젠트가 없으면 노멀맵이 먹지 않는다. 돌 재질이 노멀맵을 쓴다.
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// 칸 윗면의 월드 좌표. **파이면 내려가고 금 가면 살짝 내려간다.**
    ///
    /// 발밑 표시처럼 칸 위에 얹는 것들이 이걸 물어본다.
    /// 각자 계산하면 깊이를 바꿀 때 한쪽만 고쳐서 따로 논다 —
    /// 실제로 발밑 표시가 파인 칸 위에 붕 떠 있었다.
    /// </summary>
    public Vector3 CellSurface(int x, int y)
    {
        if (_grid == null) return Vector3.zero;
        return _grid.CellToWorld(x, y) + Vector3.down * SinkOf(x, y);
    }

    /// <summary>
    /// 칸이 평면에서 얼마나 내려가 있는가. 양수면 아래로.
    ///
    /// 단단한 돌은 살짝 솟아 있어 어두운 곳에서도 실루엣으로 구분된다.
    /// 안 판 칸은 칸마다 조금씩 높낮이가 달라 바닥이 울퉁불퉁해 보인다.
    /// </summary>
    private float SinkOf(int x, int y)
    {
        if (_grid == null) return 0f;

        bool dug = _grid.IsDug(x, y);
        bool cracked = !dug && _grid.IsCracked(x, y);
        bool hard = _grid.IsHard(x, y);

        return dug ? digDepth
             : cracked ? crackDepth - Jitter(x, y)
             : hard ? -hardRise - Jitter(x, y)
             : -Jitter(x, y);
    }

    /// <summary>
    /// 칸 하나를 지금 상태에 맞춰 다시 그린다.
    ///
    /// 상태가 셋이다 — 안 팜 / 금 감 / 팜. 금 간 칸은 살짝만 내려가고 조금 어두워진다.
    /// 금이 간 것을 눈으로 알아야 "한 번 더 치면 깨진다" 를 판단할 수 있다.
    /// </summary>
    private void HandleCellChanged(int x, int y)
    {
        int i = y * _grid.Size + x;
        if (_cells == null || i < 0 || i >= _cells.Length) return;

        Transform block = _cells[i];
        if (block == null) return;

        bool dug = _grid.IsDug(x, y);
        bool cracked = !dug && _grid.IsCracked(x, y);
        bool hard = _grid.IsHard(x, y);

        Vector3 top = _grid.CellToWorld(x, y);
        float sink = SinkOf(x, y);
        block.position = top + Vector3.down * (blockHeight * 0.5f + sink);

        // 금은 돌 윗면에 얹는다. 돌이 내려가면 같이 내려간다.
        if (_cracks != null && _cracks[i] != null)
        {
            _cracks[i].gameObject.SetActive(cracked);
            if (cracked) _cracks[i].position = top + Vector3.down * sink + Vector3.up * 0.003f;
        }

        ApplyMaterial(block.gameObject, x, y);

        Color c = ColorFor(x, y, dug);
        if (cracked) c *= crackTint;

        // ⚠ 무늬 없는 돌을 쓸 때는 색을 낮춰야 한다.
        //
        // 칸 색은 MaterialPropertyBlock 으로 가는데 그것은 머티리얼의 `_BaseColor` 를
        // **덮어쓴다.** 텍스처가 있을 때는 `텍스처 × 칸 색` 이라 칸 색이 1 을 넘어도
        // 괜찮았지만(intactColor 는 1.09, 1.27, 1.83 이다), 텍스처를 떼면 칸 색만
        // 남아서 그대로 1 을 넘고 **판이 하얗게 잘린다.**
        //
        // 머티리얼의 밑색을 낮춰도 소용없다 — 블록이 그 칸을 이긴다.
        // 낮출 곳은 보내는 색이다.
        if (UseFlat && flatMaterial != null) c = Flatten(c);

        SetColor(block.gameObject, Mottle(c, x, y));
    }

    /// <summary>
    /// 돌을 친 순간 부스러기를 뿜는다.
    ///
    /// 되메우기나 판 초기화로 칸이 바뀔 때는 부르지 않는다. 실제로 친 순간만이다.
    /// </summary>
    private void HandleCellHit(int x, int y, bool broke)
    {
        if (_debris == null) return;

        // 칸 윗면에서 튀어오르게 한다.
        _debris.Burst(_grid.CellToWorld(x, y), broke);
    }

    /// <summary>
    /// 그 칸의 바닥 굴곡 높이(m).
    ///
    /// ⚠ 칸마다 따로 주사위를 굴리면 옆칸과 뚝뚝 끊겨 **계단처럼 어긋난 타일**이 된다.
    ///   울퉁불퉁한 바닥은 옆칸과 이어져야 하므로 이어지는 잡음을 쓴다.
    ///   <see cref="jitterSpan"/> 칸에 걸쳐 천천히 오르내린다.
    ///
    /// 시드로 위치를 밀어 판마다 지형이 달라진다. 같은 시드면 늘 같은 지형이다 —
    /// 네트워크가 붙었을 때 사람마다 다른 바닥이 나오면 안 된다.
    /// </summary>
    private float Jitter(int x, int y)
    {
        if (heightJitter <= 0f) return 0f;

        float span = Mathf.Max(1f, jitterSpan);

        // 시드로 잡음을 어디서부터 읽을지 정한다. 음수가 안 되게 큰 양수 쪽에서 읽는다.
        float ox = 1000f + (_grid.Seed & 0x3FF);
        float oy = 2000f + ((_grid.Seed >> 10) & 0x3FF);

        float n = Mathf.PerlinNoise(ox + x / span, oy + y / span);

        return (n - 0.5f) * 2f * heightJitter;
    }

    /// <summary>
    /// 금 조각 하나를 만든다. 평소엔 꺼두고 금이 갔을 때만 켠다.
    ///
    /// ⚠ 블록의 자식으로 두지 않는다. 블록은 가로세로와 두께가 달라(비균등 스케일)
    ///   자식이 찌그러진다. 격자 밑에 따로 두고 위치만 맞춘다.
    ///
    /// ⚠ Quad 에 딸려오는 콜라이더는 지운다. 안 지우면 캐릭터가 금에 걸린다.
    /// </summary>
    private Transform CreateCrack(int x, int y)
    {
        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = $"Crack_{x:00}_{y:00}";
        quad.transform.SetParent(transform, false);

        if (quad.TryGetComponent(out Collider col)) Destroy(col);

        // 90도 눕혀 위를 보게 하고, 칸마다 다른 각도로 돌려 같은 금이 반복되지 않게 한다.
        float spin = 90f * ((x * 5 + y * 11) % 4) + ((x * 3 + y * 7) % 3) * 7f;
        quad.transform.rotation = Quaternion.Euler(90f, 0f, spin);

        float side = _grid.CellSize * crackScale;
        quad.transform.localScale = new Vector3(side, side, 1f);

        if (quad.TryGetComponent(out Renderer r)) r.sharedMaterial = crackMaterial;

        quad.SetActive(false);
        return quad.transform;
    }

    /// <summary>
    /// 돌 종류에 맞는 머티리얼을 붙인다. 비워두면 기본 흰색 그대로 둔다.
    /// 파인 칸은 따로 지정할 수 있고, 없으면 무른 돌 것을 쓴다.
    /// </summary>
    private void ApplyMaterial(GameObject go, int x, int y)
    {
        if (!go.TryGetComponent(out Renderer r)) return;

        // 그림을 보여주는 동안에는 바탕을 하나로 통일한다.
        // 돌 종류가 섞여 보이면 그림을 읽기 어렵다. 도안이든 결과든 마찬가지다.
        bool uniform = _overlay != MineOverlay.None || _uniformStone;

        // 그때는 **무늬도 지운다.** 탑뷰에서 내려다보면 돌결이 도안 위에
        // 겹쳐 보여서, 어느 칸이 파였는지 읽는 데 방해가 된다.
        // 칸 색은 아래 SetColor 가 블록마다 따로 칠하므로 그대로 나온다.
        if (UseFlat && flatMaterial != null)
        {
            if (r.sharedMaterial != flatMaterial) r.sharedMaterial = flatMaterial;
            return;
        }

        // 도안을 돌로 그리는 동안에는 파야 하는 칸만 따로 칠한다.
        // 바탕은 아래 uniform 이 참이라 전부 무른 돌로 간다.
        // ⚠ 결과 화면은 제외한다. 거기서 목표 칸을 따로 칠하면 정답을 알려주는 꼴이다.
        //   결과가 보여야 하는 것은 목표가 아니라 **내가 판 그림**이다.
        if (StoneBoard && _overlay == MineOverlay.Drawing && !_grid.IsDug(x, y)
            && _grid.IsTarget(x + _targetOffset.x, y + _targetOffset.y))
        {
            if (r.sharedMaterial != drawingMaterial) r.sharedMaterial = drawingMaterial;
            return;
        }

        Material m = _grid.IsDug(x, y)
            ? (dugMaterial != null ? dugMaterial : softMaterial)
            : (!uniform && _grid.IsHard(x, y) ? hardMaterial : softMaterial);

        if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
    }

    /// <summary>
    /// 칸의 색을 정한다.
    ///
    ///   도안이 없거나 showTarget 이 꺼짐  →  안 팜 / 팜 두 색
    ///   그 외 네 가지:
    ///     목표임   + 팜     →  맞게 팠다
    ///     목표임   + 안 팜  →  여기를 파야 한다
    ///     목표아님 + 팜     →  잘못 팠다
    ///     목표아님 + 안 팜  →  건드릴 필요 없다
    /// </summary>
    private Color ColorFor(int x, int y, bool dug)
    {
        // 안 판 칸은 돌 종류에 따라 색이 다르다. 이것이 무른 돌과 단단한 돌을
        // 구분하는 주된 수단이다. 머티리얼만으로는 어두운 곳에서 잘 안 갈린다.
        Color intact = !_uniformStone && _grid.IsHard(x, y) ? hardIntactColor : intactColor;

        if (!showTarget || _overlay == MineOverlay.None || _grid.TargetCells == null)
            return dug ? dugColor : intact;

        // 결과 — **내가 판 그림만** 보여준다. 도안 보기와 같은 방식이다.
        //
        // 맞은 칸·틀린 칸을 초록·빨강으로 칠해봤으나 되돌렸다. 그림을 보는 자리인데
        // 색이 얼룩덜룩하면 무엇을 그렸는지가 안 보인다. 맞고 틀림은 점수와
        // AI 한 줄 평이 말해준다. (MINE.md 7장)
        if (_overlay == MineOverlay.Result)
        {
            return dug ? dugColor : intactColor;
        }

        // 도안 보기 — 무른 돌 바탕에 검은 돌로 그림만. 돌 종류는 감춘다.
        bool isTarget = _grid.IsTarget(x + _targetOffset.x, y + _targetOffset.y);

        // ⚠ 판 칸을 dugColor 로 칠하면 도안과 **똑같은 검정**이 된다.
        //   (dugColor 0.07 · drawingColor 0.06 — 눈으로는 둘 다 그냥 검정이다.)
        //   공개 7초에는 아직 아무것도 안 파여 문제가 없었지만, 힌트는 턴 중간에 뜨므로
        //   이미 판 칸이 있다. 그러면 어디가 도안이고 어디가 내가 판 구멍인지 갈리지 않는다.
        //
        //   그래서 도안을 보는 동안만 판 칸을 중간 회색으로 뺀다. 세 단계로 읽힌다 —
        //     흰색 = 바탕 · 회색 = 내가 판 곳 · 검정 = 아직 남은 도안
        //   도안 안의 회색은 제대로 판 곳이고, 밖의 회색은 잘못 판 곳이다.
        //
        // 결과 화면은 그대로 둔다. 거기서는 판 칸 자체가 그림이므로 검정이 맞다.
        if (dug) return hintDugColor;

        // 돌로 그릴 때는 검정으로 덮으면 재질이 안 보인다. 색을 따로 둔다.
        if (StoneBoard) return isTarget ? drawingStoneColor : intactColor;

        return isTarget ? drawingColor : intactColor;
    }

    /// <summary>
    /// 무엇을 겹쳐 보여줄지 정한다. MineGame 이 단계마다 부른다.
    ///
    /// 도안 보기와 결과는 목적이 다르다. 도안은 그림을 외우는 것이고,
    /// 결과는 맞고 틀림을 따지는 것이다. 그래서 칠하는 방식이 다르다.
    /// </summary>
    /// <summary>
    /// 판의 반폭(m). <b>테두리까지 포함한다.</b>
    ///
    /// 탑뷰 카메라가 이 값으로 높이를 잡는다. 판 크기만 보면 테두리가 화면 밖으로
    /// 밀려나고, 그만큼 판이 꽉 차 보여 HUD 를 가린다.
    ///
    /// ⚠ <c>Awake</c> 를 기다리지 않는다. 카메라가 다른 컴포넌트의 Awake 에서 물어보는
    ///   길이 있어 그때는 <c>_grid</c> 가 아직 비어 있다. 여기서 직접 찾는다.
    /// </summary>
    public float BoardHalfExtent
    {
        get
        {
            MineGrid g = _grid != null ? _grid : GetComponent<MineGrid>();
            if (g == null) return 0f;

            return g.Size * g.CellSize * 0.5f + borderRing * g.CellSize;
        }
    }

    public void SetOverlay(MineOverlay overlay)
    {
        _overlay = overlay;
        RefreshAll();
    }

    /// <summary>
    /// 돌 종류를 감추고 판을 <b>한 가지 밝은 돌</b>로 보여줄 것인가.
    ///
    /// 시작 카운트다운에 쓴다. 아직 아무도 못 파는 시간인데 단단한 돌이 어두운
    /// 얼룩으로 먼저 드러나면, 판이 지저분해 보이고 어디가 단단한지도 미리 알려준다.
    ///
    /// 도안·결과 화면도 같은 이유로 바탕을 하나로 통일하는데, 그쪽은
    /// <see cref="_overlay"/> 로 이미 갈린다. 카운트다운은 overlay 가 None 이라
    /// 따로 알려줄 길이 필요했다.
    /// </summary>
    public void SetUniformStone(bool on)
    {
        if (_uniformStone == on) return;

        _uniformStone = on;
        RefreshAll();
    }

    /// <summary>모든 칸의 색과 높이를 현재 상태에 맞춰 다시 적용한다.</summary>
    public void RefreshAll()
    {
        if (_cells == null || _grid == null) return;

        int size = _grid.Size;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                HandleCellChanged(x, y);
            }
        }
    }

    // 지금 무늬 없는 돌을 쓰는가.
    //
    // 원래는 도안과 결과를 보여주는 동안만 썼다. 탑뷰로 그림을 읽는 시간이라
    // 돌결이 방해가 되기 때문이다. flatAlways 를 켜면 채굴 중에도 쓴다.
    /// <summary>
    /// 목표 도안을 <b>돌 재질로</b> 그리는가. <see cref="drawingMaterial"/> 을 넣었을 때만이다.
    ///
    /// 원래 공개·결과 화면은 무늬 없는 돌 하나로 덮고 색으로만 그림을 그렸다.
    /// 돌결이 도안 위에 겹쳐 읽기 어렵다는 이유였다. 이 길은 그 대신
    /// <b>바탕은 무른 돌, 파야 하는 칸은 다른 돌</b>로 갈라 보여 준다.
    ///
    /// 결과 화면은 건드리지 않는다. 거기서는 판 칸 자체가 그림이다.
    /// </summary>
    /// <summary>
    /// 판을 <b>돌 재질로</b> 보여주는가. 목표 공개(힌트 포함)와 결과 화면 둘 다다.
    ///
    /// 원래 두 화면은 무늬 없는 돌 하나로 덮고 색으로만 그림을 그렸다. 돌결이 그림 위에
    /// 겹쳐 읽기 어렵다는 이유였다. 지금은 바탕을 실제 돌로 두고 그림만 갈라 보여 준다.
    ///
    /// 켜지는 조건을 <see cref="drawingMaterial"/> 하나로 묶어 두었다. 비우면 두 화면
    /// 모두 예전 방식으로 돌아간다.
    /// </summary>
    private bool StoneBoard => _overlay != MineOverlay.None && drawingMaterial != null;

    private bool UseFlat => !StoneBoard && (flatAlways || _overlay != MineOverlay.None);

    // 무늬 없는 돌을 쓸 때 칸 색을 손본다. 밝기를 낮추고 색조를 지운다.
    //
    // ⚠ 색조를 지우는 이유 — intactColor 는 따뜻한 텍스처와 주황 랜턴을 상쇄하려고
    //   파랗게 만든 값이다 (1.09, 1.27, 1.83). 그런데 탑뷰에서는 텍스처를 떼고
    //   랜턴도 끈다. 상쇄할 대상이 없으니 파란 보정만 남아 판이 파래진다.
    //
    // 밝기는 사람이 느끼는 대로 뽑는다. 초록이 가장 밝게 보이고 파랑이 가장 어둡다.
    private Color Flatten(Color c)
    {
        float gray = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;

        return new Color(Mathf.Lerp(c.r, gray, flatDesaturate) * flatColorScale,
                         Mathf.Lerp(c.g, gray, flatDesaturate) * flatColorScale,
                         Mathf.Lerp(c.b, gray, flatDesaturate) * flatColorScale,
                         c.a);
    }

    /// <summary>
    /// 칸마다 고정된 얼룩. 실제 돌은 장마다 톤이 다르다.
    ///
    /// ⚠ <b>좌표 해시로 뽑는다.</b> <c>Random</c> 을 쓰면 다시 그릴 때마다 값이 바뀌어
    ///   칸을 팔 때마다 판 전체가 깜박인다. 같은 칸은 언제 불러도 같은 값이어야 한다.
    ///
    /// 밝기와 색조를 <b>서로 무관하게</b> 흔든다(salt 가 다르다). 둘이 같이 움직이면
    /// 밝은 칸이 늘 따뜻해서 얼룩이 규칙적으로 보인다.
    ///
    /// ⚠ 폭을 키우면 금방 얼룩덜룩해진다. 한 번 ±3% 로 해봤더니 너무 시끄러웠다.
    ///   실제 돌은 이웃끼리 비슷하고 가끔만 튀는데, 여기 얼룩은 칸마다 무관한
    ///   잡음이라 폭이 커지면 TV 노이즈처럼 보인다.
    /// </summary>
    private Color Mottle(Color c, int x, int y)
    {
        if (cellMottle <= 0f) return c;

        float bright = 1f + (Hash01(x, y, 1) * 2f - 1f) * cellMottle;
        float warm = 1f + (Hash01(x, y, 2) * 2f - 1f) * cellMottle;

        return new Color(c.r * bright * warm,
                         c.g * bright,
                         c.b * bright / warm,
                         c.a);
    }

    /// <summary>좌표에서 0~1 을 뽑는다. salt 를 바꾸면 서로 무관한 값이 나온다.</summary>
    private static float Hash01(int x, int y, int salt)
    {
        unchecked
        {
            int h = x * 73856093 ^ y * 19349663 ^ salt * 83492791;
            h ^= h >> 13;
            h *= 1274126177;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }
    }

    private void SetColor(GameObject go, Color color)
    {
        if (!go.TryGetComponent(out Renderer r)) return;

        r.GetPropertyBlock(_props);
        _props.SetColor(BaseColorId, color);
        r.SetPropertyBlock(_props);
    }
}