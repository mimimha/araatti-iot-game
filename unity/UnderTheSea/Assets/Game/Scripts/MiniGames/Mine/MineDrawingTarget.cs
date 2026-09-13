using UnityEngine;

/// <summary>
/// 광산 목표 그림 하나. 격자 위의 참/거짓 배열이다. (MINE.md 5장)
///
/// 도안은 <see cref="rows"/> 에 **글자 그림**으로 적는다.
/// 한 줄이 격자 한 행이고, 한 글자가 한 칸이다.
///
///     '.' 또는 공백  →  안 파는 칸
///     그 외 모든 글자 →  파는 칸  ('#' 를 권장)
///
/// bool 400개를 인스펙터에서 찍는 대신 이렇게 두는 이유
///   - 눈으로 보면서 그릴 수 있다
///   - git diff 에 그림이 그대로 보인다
///   - AI 에게 "20×20 물고기 아스키로" 라고 하면 바로 나온다
/// </summary>
[CreateAssetMenu(fileName = "MineDrawing", menuName = "Mine/Drawing Target")]
public class MineDrawingTarget : ScriptableObject
{
    [Header("표시")]
    [Tooltip("결과 화면에 보일 이름. 예: 물고기")]
    public string displayName = "물고기";

    [Header("격자")]
    [Tooltip("한 변의 칸 수. MineGrid 의 Size 와 같아야 한다.")]
    [Min(1)] public int size = 20;

    [Header("도안")]
    [Tooltip("size 줄 × size 글자. '.' 은 안 파는 칸, '#' 은 파는 칸.\n" +
             "첫 줄이 그림의 맨 위다.")]
    [TextArea(20, 20)]
    public string rows;

    /// <summary>파야 하는 칸의 수.</summary>
    public int TargetCount
    {
        get
        {
            int n = 0;
            foreach (bool b in ToCells())
            {
                if (b) n++;
            }
            return n;
        }
    }

    /// <summary>
    /// 도안을 <see cref="MineGrid"/> 와 같은 형식의 배열로 바꾼다.
    /// 인덱스는 y * size + x, 길이는 size * size.
    ///
    /// ⚠ 행 순서가 뒤집힌다.
    ///   MineGrid 는 y = 0 이 −Z 쪽 구석이지만, 글자 그림은 첫 줄이 맨 위다.
    ///   그래서 rows 의 첫 줄이 y = size − 1 로 들어간다.
    ///   이걸 안 하면 위아래가 뒤집힌 도안이 된다.
    /// </summary>
    public bool[] ToCells()
    {
        var cells = new bool[size * size];
        string[] lines = SplitRows();

        for (int r = 0; r < size; r++)
        {
            int y = size - 1 - r;                                  // 첫 줄 → 맨 위
            string line = r < lines.Length ? lines[r] : string.Empty;

            for (int x = 0; x < size; x++)
            {
                bool dig = x < line.Length && IsDigChar(line[x]);
                cells[y * size + x] = dig;
            }
        }

        return cells;
    }

    /// <summary>'.' 공백 '_' '0' 은 안 파는 칸. 그 외는 전부 파는 칸으로 본다.</summary>
    private static bool IsDigChar(char c)
    {
        return c != '.' && c != ' ' && c != '_' && c != '0' && c != '\t';
    }

    /// <summary>줄바꿈으로 자른다. 끝에 딸려오는 빈 줄은 버린다.</summary>
    private string[] SplitRows()
    {
        if (string.IsNullOrEmpty(rows)) return System.Array.Empty<string>();

        string[] raw = rows.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        int last = raw.Length - 1;
        while (last >= 0 && raw[last].Trim().Length == 0) last--;

        var trimmed = new string[last + 1];
        System.Array.Copy(raw, trimmed, last + 1);
        return trimmed;
    }

#if UNITY_EDITOR
    /// <summary>인스펙터에서 값을 고칠 때마다 도안 형식을 검사해 준다.</summary>
    private void OnValidate()
    {
        if (string.IsNullOrEmpty(rows)) return;

        string[] lines = SplitRows();

        if (lines.Length != size)
        {
            Debug.LogWarning($"[{name}] 줄 수가 {lines.Length} 입니다. {size} 줄이어야 합니다.", this);
        }

        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].Length != size)
            {
                Debug.LogWarning($"[{name}] {i + 1}번째 줄이 {lines[i].Length}글자입니다. " +
                                 $"{size}글자여야 합니다.", this);
                break;   // 첫 줄만 알려준다. 전부 찍으면 시끄럽다
            }
        }
    }
#endif
}