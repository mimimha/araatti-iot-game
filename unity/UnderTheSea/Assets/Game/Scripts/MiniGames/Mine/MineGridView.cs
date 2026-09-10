using System.Collections.Generic;
using UnityEngine;

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
/// 도안은 <see cref="MineGrid"/> 가 들고 있다. 여기서는 읽어서 색으로만 쓴다.
/// <see cref="showTarget"/> 이 켜져 있으면 **도안을 겹쳐 보여준다.** 개발 중에 좌표가
/// 맞는지 눈으로 확인하기 위한 것이고, 실제 플레이에서는 꺼야 한다.
/// (MINE.md 2장 — 목표 그림은 시작에 7초만 보여준다)
///
/// 다만 이 4색 구분은 **결과 화면에서 그대로 재사용된다.**
/// MINE.md 7장이 "맞은 칸과 틀린 칸을 색으로 구분해 보여준다"고 요구한다.
/// </summary>
[RequireComponent(typeof(MineGrid))]
public class MineGridView : MonoBehaviour
{
    [Header("칸 모양")]
    [Tooltip("칸 사이 틈. 0 이면 딱 붙는다. 격자가 보이게 하려면 조금 준다.")]
    [SerializeField, Range(0f, 0.2f)] private float gap = 0.04f;

    [Tooltip("칸 블록의 두께(m).")]
    [SerializeField, Min(0.05f)] private float blockHeight = 0.5f;

    [Header("파였을 때")]
    [Tooltip("파인 칸이 내려가는 깊이(m). 캐릭터가 넘어다닐 수 있을 만큼만.")]
    [SerializeField, Min(0.02f)] private float digDepth = 0.25f;

    [Header("도안 표시 (개발용)")]
    [Tooltip("끄면 도안을 무시하고 안 팜/팜 두 색으로만 그린다. 실제 플레이에서는 꺼야 한다.\n" +
             "도안 자체는 MineGrid 에서 지정한다.")]
    [SerializeField] private bool showTarget = true;

    [Header("색")]
    [Tooltip("건드릴 필요 없는 칸")]
    [SerializeField] private Color intactColor = new Color(0.55f, 0.52f, 0.48f);

    [Tooltip("도안 없이 그냥 판 칸 (showTarget 이 꺼졌을 때)")]
    [SerializeField] private Color dugColor = new Color(0.15f, 0.13f, 0.12f);

    [Tooltip("파야 하는데 아직 안 판 칸")]
    [SerializeField] private Color targetColor = new Color(0.34f, 0.46f, 0.62f);

    [Tooltip("맞게 판 칸")]
    [SerializeField] private Color correctColor = new Color(0.11f, 0.34f, 0.17f);

    [Tooltip("잘못 판 칸")]
    [SerializeField] private Color wrongColor = new Color(0.42f, 0.12f, 0.12f);

    private MineGrid _grid;
    private Transform[] _cells;

    private MaterialPropertyBlock _props;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    private void Awake()
    {
        _grid = GetComponent<MineGrid>();
        _props = new MaterialPropertyBlock();
        Build();
    }

    private void OnEnable()
    {
        if (_grid == null) return;

        _grid.OnCellChanged += HandleCellChanged;
        _grid.OnTargetChanged += RefreshAll;
    }

    private void OnDisable()
    {
        if (_grid == null) return;

        _grid.OnCellChanged -= HandleCellChanged;
        _grid.OnTargetChanged -= RefreshAll;
    }

    private void Build()
    {
        int size = _grid.Size;
        _cells = new Transform[size * size];

        float side = Mathf.Max(0.01f, _grid.CellSize - gap);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
                block.name = $"Cell_{x:00}_{y:00}";
                block.transform.SetParent(transform, false);

                // CellToWorld 는 평면 위 중심. 블록은 그 아래로 두께만큼 잠기게 놓는다.
                Vector3 top = _grid.CellToWorld(x, y);
                block.transform.position = top + Vector3.down * (blockHeight * 0.5f);
                block.transform.localScale = new Vector3(side, blockHeight, side);

                SetColor(block, ColorFor(x, y, false));
                _cells[y * size + x] = block.transform;
            }
        }
    }

    private void HandleCellChanged(int x, int y, bool dug)
    {
        int i = y * _grid.Size + x;
        if (_cells == null || i < 0 || i >= _cells.Length) return;

        Transform block = _cells[i];
        if (block == null) return;

        Vector3 top = _grid.CellToWorld(x, y);
        float sink = dug ? digDepth : 0f;
        block.position = top + Vector3.down * (blockHeight * 0.5f + sink);

        SetColor(block.gameObject, ColorFor(x, y, dug));
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
        if (!showTarget || _grid.TargetCells == null)
            return dug ? dugColor : intactColor;

        bool isTarget = _grid.IsTarget(x, y);

        if (isTarget) return dug ? correctColor : targetColor;
        return dug ? wrongColor : intactColor;
    }

    /// <summary>
    /// 도안 표시를 켜고 끈다. 켜거나 끄면 전체 색을 다시 칠한다.
    /// 결과 화면에서 켜는 데 쓸 수 있다. (MINE.md 7장)
    /// </summary>
    public void SetShowTarget(bool show)
    {
        showTarget = show;
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
                HandleCellChanged(x, y, _grid.IsDug(x, y));
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