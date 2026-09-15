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
/// <summary>한 번 휘둘렀을 때 그 칸에 일어난 일. (MINE.md 4장)</summary>
public enum MineHitResult
{
    /// <summary>아무 일도 없었다. 격자 밖이거나 이미 파인 칸.</summary>
    None,

    /// <summary>금이 갔다. 아직 안 파였다. 단단한 돌을 처음 쳤을 때.</summary>
    Cracked,

    /// <summary>파였다.</summary>
    Broke,
}

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

    [Header("돌 종류 (MINE.md 4장)")]
    [Tooltip("단단한 돌의 비율. 0.25 면 네 칸 중 하나가 두 번 쳐야 깨진다.\n" +
             "높이면 한 판에 팔 수 있는 칸이 줄어든다. 7번 밸런싱에서 조정한다.")]
    [SerializeField, Range(0f, 1f)] private float hardRatio = 0.25f;

    private bool[] _dug;

    /// <summary>칸마다 원래 필요한 타격 수. 1 = 무른 돌, 2 = 단단한 돌.</summary>
    private byte[] _hardness;

    /// <summary>칸마다 남은 타격 수. 0 이면 파인 것이다.</summary>
    private byte[] _remaining;

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

    /// <summary>
    /// 이번 판의 돌 배치를 만든 시드.
    ///
    /// ⚠ 배치는 무작위지만 **시드로 만든다.** 네트워크가 붙으면 4명이 각자 무작위로
    ///   깔게 되어 사람마다 다른 판을 보게 된다. 시드를 나눠 가지면 같은 판이 된다.
    /// </summary>
    public int Seed { get; private set; }

    /// <summary>이번 판의 목표 도안. 없을 수 있다.</summary>
    public MineDrawingTarget Target => target;

    /// <summary>
    /// 칸 상태가 바뀔 때. (x, y)
    ///
    /// 파였을 때만이 아니라 **금이 갔을 때도** 발생한다. 상태가 셋이라 인자로 넘기지
    /// 않고, 듣는 쪽이 <see cref="IsDug"/> · <see cref="IsCracked"/> 로 물어본다.
    /// </summary>
    public event Action<int, int> OnCellChanged;

    /// <summary>
    /// 돌을 쳤을 때. (x, y, 깨졌는가)
    ///
    /// OnCellChanged 와 따로 두는 이유는 **이펙트 때문**이다. 되메우기나 판 초기화로
    /// 칸이 바뀔 때는 부스러기가 튀면 안 된다. 이 이벤트는 실제로 친 순간에만 난다.
    /// </summary>
    public event Action<int, int, bool> OnCellHit;

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
        _hardness = new byte[CellCount];
        _remaining = new byte[CellCount];

        Scatter(Environment.TickCount);
    }

    /// <summary>
    /// 단단한 돌을 뿌리고 전부 안 판 상태로 되돌린다.
    /// 같은 시드면 같은 배치가 나온다.
    /// </summary>
    private void Scatter(int seed)
    {
        Seed = seed;
        var rng = new System.Random(seed);

        for (int i = 0; i < CellCount; i++)
        {
            _hardness[i] = rng.NextDouble() < hardRatio ? (byte)2 : (byte)1;
            _remaining[i] = _hardness[i];
            _dug[i] = false;
        }

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

    /// <summary>단단한 돌인가. 두 번 쳐야 깨진다.</summary>
    public bool IsHard(int x, int y)
    {
        EnsureAllocated();
        return InBounds(x, y) && _hardness[Index(x, y)] > 1;
    }

    /// <summary>금이 간 상태인가. 한 번 맞았지만 아직 안 파였다.</summary>
    public bool IsCracked(int x, int y)
    {
        EnsureAllocated();
        if (!InBounds(x, y)) return false;

        int i = Index(x, y);
        return !_dug[i] && _remaining[i] < _hardness[i];
    }

    /// <summary>
    /// 한 번 친다. 무른 돌은 바로 파이고, 단단한 돌은 처음에 금만 간다.
    ///
    /// ⚠ 금 간 상태는 **턴이 넘어가도 남는다.** 앞사람이 한 번 쳐둔 돌을
    ///   뒷사람이 한 번만 쳐도 깨진다. 릴레이 게임이므로 흔적이 남는 것이 맞다.
    /// </summary>
    public MineHitResult Hit(int x, int y)
    {
        EnsureAllocated();

        if (!InBounds(x, y)) return MineHitResult.None;

        int i = Index(x, y);
        if (_remaining[i] == 0) return MineHitResult.None;   // 이미 파인 칸

        _remaining[i]--;

        if (_remaining[i] > 0)
        {
            OnCellChanged?.Invoke(x, y);
            OnCellHit?.Invoke(x, y, false);
            return MineHitResult.Cracked;
        }

        _dug[i] = true;
        DugCount++;
        OnCellChanged?.Invoke(x, y);
        OnCellHit?.Invoke(x, y, true);
        return MineHitResult.Broke;
    }

    /// <summary>
    /// 되메운다. 안 파여 있었으면 false.
    ///
    /// ⚠ **단단한 돌은 다시 단단한 돌로 돌아간다.** 되메운 자리를 다시 파려면
    ///   또 두 번 쳐야 한다. 금 간 상태로 돌려주지 않는다.
    /// </summary>
    public bool Restore(int x, int y)
    {
        EnsureAllocated();

        if (!InBounds(x, y)) return false;

        int i = Index(x, y);
        if (!_dug[i]) return false;

        _dug[i] = false;
        _remaining[i] = _hardness[i];
        DugCount--;
        OnCellChanged?.Invoke(x, y);
        return true;
    }

    /// <summary>
    /// 판을 새로 시작한다. 돌 배치를 다시 뿌리고 전부 안 판 상태로 되돌린다.
    /// 같은 시드를 주면 같은 배치가 나온다. 네트워크에서 이 값을 나눠 갖는다.
    /// </summary>
    public void ResetAll(int seed)
    {
        EnsureAllocated();
        Scatter(seed);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                OnCellChanged?.Invoke(x, y);
            }
        }
    }

    /// <summary>시드를 아무거나 골라 새로 시작한다. 혼자 테스트할 때.</summary>
    public void ResetAll() => ResetAll(Environment.TickCount);

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