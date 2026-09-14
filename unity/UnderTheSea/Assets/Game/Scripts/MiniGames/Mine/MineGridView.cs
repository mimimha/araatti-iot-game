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

    [Tooltip("칸마다 90도씩 무작위로 돌려 텍스처 반복을 깬다.\n" +
             "끄면 400칸이 똑같이 보여 격자무늬가 도드라진다.")]
    [SerializeField] private bool varyRotation = true;

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
    [Tooltip("건드릴 필요 없는 칸 — 무른 돌. 흙빛.")]
    [SerializeField] private Color intactColor = new Color(0.55f, 0.50f, 0.42f);

    [Tooltip("건드릴 필요 없는 칸 — 단단한 돌. 푸른 잿빛.\n" +
             "머티리얼이 무엇이든 색이 다르면 구분된다. 어두운 곳에서는 특히.")]
    [SerializeField] private Color hardIntactColor = new Color(0.42f, 0.46f, 0.52f);

    [Tooltip("판 칸. 채굴 중에도 결과 화면에도 이 색이다.")]
    [SerializeField] private Color dugColor = new Color(0.15f, 0.13f, 0.12f);

    [Tooltip("금 간 칸에 곱할 색. 1보다 작으면 어두워진다.")]
    [SerializeField] private Color crackTint = new Color(0.75f, 0.72f, 0.7f);

    [Tooltip("도안을 보여줄 때 그림이 되는 칸의 색. 무른 돌 바탕 위의 검은 돌.")]
    [SerializeField] private Color drawingColor = new Color(0.06f, 0.055f, 0.05f);

    private MineGrid _grid;
    private Transform[] _cells;

    /// <summary>칸마다 하나씩. 금이 갔을 때만 켠다. crackMaterial 이 없으면 안 만든다.</summary>
    private Transform[] _cracks;

    /// <summary>부스러기. 같은 오브젝트에 붙어 있으면 쓰고, 없으면 안 쓴다.</summary>
    private MineDebris _debris;

    /// <summary>지금 무엇을 겹쳐 보여주는가. MineGame 이 단계마다 정한다.</summary>
    private MineOverlay _overlay = MineOverlay.None;

    private MaterialPropertyBlock _props;
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

    private void Build()
    {
        int size = _grid.Size;
        _cells = new Transform[size * size];
        _cracks = crackMaterial != null ? new Transform[size * size] : null;

        float side = Mathf.Max(0.01f, _grid.CellSize - gap);

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

                SetColor(block, ColorFor(x, y, false));
                _cells[y * size + x] = block.transform;

                if (_cracks != null) _cracks[y * size + x] = CreateCrack(x, y);
            }
        }
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
        SetColor(block.gameObject, c);
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

        // 그림을 보여주는 동안에는 바탕을 무른 돌 하나로 통일한다.
        // 돌 종류가 섞여 보이면 그림을 읽기 어렵다. 도안이든 결과든 마찬가지다.
        bool uniform = _overlay != MineOverlay.None;

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
        Color intact = _grid.IsHard(x, y) ? hardIntactColor : intactColor;

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

        if (dug) return dugColor;
        return isTarget ? drawingColor : intactColor;
    }

    /// <summary>
    /// 무엇을 겹쳐 보여줄지 정한다. MineGame 이 단계마다 부른다.
    ///
    /// 도안 보기와 결과는 목적이 다르다. 도안은 그림을 외우는 것이고,
    /// 결과는 맞고 틀림을 따지는 것이다. 그래서 칠하는 방식이 다르다.
    /// </summary>
    public void SetOverlay(MineOverlay overlay)
    {
        _overlay = overlay;
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

    private void SetColor(GameObject go, Color color)
    {
        if (!go.TryGetComponent(out Renderer r)) return;

        r.GetPropertyBlock(_props);
        _props.SetColor(BaseColorId, color);
        r.SetPropertyBlock(_props);
    }
}