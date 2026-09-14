using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 광산 바닥 격자. **어느 칸이 파였는지**와 **이번 판의 목표 도안**을 들고 있다.
///
/// 화면은 이 클래스의 일이 아니다. <see cref="MineGridView"/> 가
/// <see cref="OnCellChanged"/> 를 듣고 그린다.
///
/// 도안을 여기 두는 이유
///   - 화면(MineGridView)과 채점(MineDigger)이 **같은 도안**을 봐야 한다.
///     각자 참조를 들면 한쪽만 바꿔놓고 헤매게 된다.
///   - 크기 검사와 캐시를 한 곳에서 한다.
///   - 나중에 MineGame 이 인원에 맞는 도안을 골라 <see cref="SetTarget"/> 로 갈아끼운다.
///     (MINE.md 2장 — 인원이 곧 난이도)
///
/// 격자는 이 오브젝트를 **중심**으로 XZ 평면에 놓인다.
/// 칸 (0,0) 은 -X, -Z 쪽 구석이다.
/// </summary>
public class MineGrid : MonoBehaviour
{
    [Header("격자")]
    [Tooltip("한 변의 칸 수. MINE.md 기준 시작값은 20 이고, 실측 후 조정한다.")]
    [SerializeField, Min(1)] private int size = 20;

    [Tooltip("한 칸의 한 변 길이(m).")]
    [SerializeField, Min(0.05f)] private float cellSize = 1f;

    [Header("목표 도안")]
    [Tooltip("이번 판의 목표 그림. 화면 표시와 채점이 모두 이것을 쓴다.\n" +
             "비워두면 도안 없이 파기만 된다.")]
    [SerializeField] private MineDrawingTarget target;

    private bool[] _dug;

    private bool[] _targetCells;
    private bool _targetCached;

    /// <summary>한 변의 칸 수.</summary>
    public int Size => size;

    /// <summary>한 칸의 한 변 길이(m).</summary>
    public float CellSize => cellSize;

    /// <summary>전체 칸 수.</summary>
    public int CellCount => size * size;

    /// <summary>지금까지 파인 칸 수.</summary>
    public int DugCount { get; private set; }

    /// <summary>이번 판의 목표 도안. 없을 수 있다.</summary>
    public MineDrawingTarget Target => target;

    /// <summary>
    /// 칸 상태가 바뀔 때. (x, y, 파였는가)
    /// 실제로 바뀐 경우에만 발생한다. 이미 파인 칸을 또 파면 발생하지 않는다.
    /// </summary>
    public event Action<int, int, bool> OnCellChanged;

    /// <summary>도안이 교체될 때. 화면이 이걸 듣고 다시 칠한다.</summary>
    public event Action OnTargetChanged;

    /// <summary>
    /// 판 상태. 판정이 그대로 읽는다. 길이는 <see cref="CellCount"/>.
    /// 인덱스는 y * Size + x.
    /// </summary>
    public IReadOnlyList<bool> Cells
    {
        get
        {
            EnsureAllocated();
            return _dug;
        }
    }

    /// <summary>
    /// 목표 상태. <see cref="Cells"/> 와 같은 형식이다.
    /// **도안이 없거나 크기가 안 맞으면 null** 이므로 쓰는 쪽에서 확인해야 한다.
    /// </summary>
    public IReadOnlyList<bool> TargetCells
    {
        get
        {
            EnsureTargetCached();
            return _targetCells;
        }
    }

    private void Awake()
    {
        EnsureAllocated();
        EnsureTargetCached();
    }

    private void EnsureAllocated()
    {
        if (_dug != null && _dug.Length == CellCount) return;

        _dug = new bool[CellCount];
        DugCount = 0;
    }

    /// <summary>
    /// 도안을 배열로 뽑아 캐시한다. 처음 읽을 때 만든다.
    ///
    /// Awake 에서만 만들면 안 된다. 같은 오브젝트의 컴포넌트끼리는 Awake 순서가 정해져
    /// 있지 않아서, MineGridView 가 먼저 깨어나면 캐시가 아직 없다.
    /// </summary>
    private void EnsureTargetCached()
    {
        if (_targetCached) return;

        _targetCached = true;
        _targetCells = null;

        if (target == null) return;

        // 크기가 다르면 인덱스가 어긋나 엉뚱한 칸을 비교하고 칠한다.
        // 격자 Size 를 바꾸면서 도안을 안 맞췄을 때 생기는 사고다. (MINE.md 12장)
        if (target.size != size)
        {
            Debug.LogWarning($"{nameof(MineGrid)}: 도안 크기가 다릅니다. " +
                             $"격자 {size} vs 도안 '{target.name}' {target.size}. " +
                             $"도안을 무시합니다.", this);
            return;
        }

        _targetCells = target.ToCells();
    }

    /// <summary>
    /// 도안을 갈아끼운다. 판을 시작할 때 MineGame 이 부른다.
    /// 판 상태는 건드리지 않으므로, 새 판이면 <see cref="ResetAll"/> 도 함께 부른다.
    /// </summary>
    public void SetTarget(MineDrawingTarget next)
    {
        target = next;
        _targetCached = false;
        OnTargetChanged?.Invoke();
    }

    private int Index(int x, int y) => y * size + x;

    public bool InBounds(int x, int y) => x >= 0 && x < size && y >= 0 && y < size;

    public bool IsDug(int x, int y)
    {
        EnsureAllocated();
        return InBounds(x, y) && _dug[Index(x, y)];
    }

    /// <summary>목표가 파라고 한 칸인지. 도안이 없으면 항상 false.</summary>
    public bool IsTarget(int x, int y)
    {
        EnsureTargetCached();
        return _targetCells != null && InBounds(x, y) && _targetCells[Index(x, y)];
    }

    /// <summary>판다. 이미 파여 있었으면 false.</summary>
    public bool Dig(int x, int y) => SetCell(x, y, true);

    /// <summary>되메운다. 안 파여 있었으면 false.</summary>
    public bool Restore(int x, int y) => SetCell(x, y, false);

    private bool SetCell(int x, int y, bool dug)
    {
        EnsureAllocated();

        if (!InBounds(x, y)) return false;

        int i = Index(x, y);
        if (_dug[i] == dug) return false;

        _dug[i] = dug;
        DugCount += dug ? 1 : -1;
        OnCellChanged?.Invoke(x, y, dug);
        return true;
    }

    /// <summary>전부 안 판 상태로 되돌린다. 판을 새로 시작할 때.</summary>
    public void ResetAll()
    {
        EnsureAllocated();

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                int i = Index(x, y);
                if (!_dug[i]) continue;

                _dug[i] = false;
                OnCellChanged?.Invoke(x, y, false);
            }
        }

        DugCount = 0;
    }

    /// <summary>
    /// 월드 좌표가 어느 칸인지. 격자 밖이면 false 를 돌려주고 x, y 는 믿을 수 없다.
    /// 높이(Y)는 보지 않는다.
    /// </summary>
    public bool WorldToCell(Vector3 world, out int x, out int y)
    {
        Vector3 local = transform.InverseTransformPoint(world);
        float half = size * cellSize * 0.5f;

        x = Mathf.FloorToInt((local.x + half) / cellSize);
        y = Mathf.FloorToInt((local.z + half) / cellSize);

        return InBounds(x, y);
    }

    /// <summary>칸의 중심 월드 좌표. 높이는 이 오브젝트의 평면 위.</summary>
    public Vector3 CellToWorld(int x, int y)
    {
        float half = size * cellSize * 0.5f;

        Vector3 local = new Vector3(
            (x + 0.5f) * cellSize - half,
            0f,
            (y + 0.5f) * cellSize - half);

        return transform.TransformPoint(local);
    }

    private void OnDrawGizmosSelected()
    {
        float half = size * cellSize * 0.5f;
        Gizmos.color = Color.yellow;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(half * 2f, 0f, half * 2f));
    }
}