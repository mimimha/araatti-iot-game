using UnityEditor;
using UnityEngine;

/// <summary>
/// Scene view 에 배 협동 작업 자리의 상태를 글자로 띄운다.
///
/// 큐브만 놓고 만드는 동안 어느 큐브가 무슨 자리인지, 지금 누가 붙어 있는지,
/// 조타가 얼마나 꺾였는지를 눈으로 보기 위한 개발 도구다.
///
/// 에디터 폴더에 있으므로 빌드에는 들어가지 않는다.
/// 에셋을 씌우면 필요 없어진다. 그때 지우면 된다.
///
/// 끄고 싶으면 Tools > 아라아띠 > 배 협동 자리 라벨 보기
/// </summary>
[InitializeOnLoad]
public static class ShipCoopSceneLabels
{
    private const string MenuPath = "Tools/아라아띠/배 협동 자리 라벨 보기";
    private const string PrefKey = "ShipCoop.SceneLabels.Enabled";

    private static readonly Color OccupiedColor = new Color(0.45f, 0.95f, 0.55f);
    private static readonly Color EmptyColor = new Color(0.80f, 0.82f, 0.88f);
    private static readonly Color LeakingColor = new Color(1.00f, 0.45f, 0.40f);
    private static readonly Color WorkerColor = new Color(0.55f, 0.80f, 1.00f);
    private static readonly Color CarryColor = new Color(1.00f, 0.85f, 0.40f);

    private static GUIStyle _style;

    static ShipCoopSceneLabels()
    {
        SceneView.duringSceneGui += OnSceneGui;
    }

    private static bool Enabled
    {
        get => EditorPrefs.GetBool(PrefKey, true);
        set => EditorPrefs.SetBool(PrefKey, value);
    }

    [MenuItem(MenuPath)]
    private static void Toggle()
    {
        Enabled = !Enabled;
        SceneView.RepaintAll();
    }

    [MenuItem(MenuPath, validate = true)]
    private static bool ToggleValidate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return true;
    }

    private static void OnSceneGui(SceneView view)
    {
        // 그리는 순간에만 일한다. duringSceneGui 는 입력 처리에도 불린다.
        if (Event.current.type != EventType.Repaint || !Enabled)
        {
            return;
        }

        var tasks = Object.FindObjectsByType<TaskBase>(FindObjectsInactive.Include);
        if (tasks.Length == 0)
        {
            // 배 협동 씬이 아니다.
            return;
        }

        EnsureStyle();

        foreach (TaskBase task in tasks)
        {
            DrawTask(task);
        }

        DrawAmmoBoxes();

        var workers = Object.FindObjectsByType<TaskWorker>(FindObjectsInactive.Include);
        foreach (TaskWorker worker in workers)
        {
            DrawWorker(worker);
        }
    }

    private static void DrawTask(TaskBase task)
    {
        Color color = ColorFor(task);
        Vector3 position = task.transform.position;

        // 상호작용 범위
        Handles.color = new Color(color.r, color.g, color.b, 0.55f);
        Handles.DrawWireDisc(position, Vector3.up, task.InteractRange);

        // 라벨
        _style.normal.textColor = color;
        Handles.Label(position + Vector3.up * 1.4f, LabelFor(task), _style);
    }

    private static string LabelFor(TaskBase task)
    {
        string head = $"{task.DisplayName}  ({task.Workers.Count}/{task.Capacity})";

        switch (task)
        {
            case HelmTask helm:
                string straight = helm.IsHeadingStraight() ? "정면" : "꺾임";
                return $"{head}\n{helm.Heading:F0}°  {straight}\nSteer {helm.Steer:+0.00;-0.00; 0.00}";

            case SailTask sail:
                string slack = sail.IsSlack ? "⚠ 돛이 풀렸다" : $"돛 {sail.SailPower01:P0}";
                return $"{head}\n{slack}\nPull {sail.Pull:+0.00;-0.00; 0.00}";

            case CannonTask cannon:
                string ammo = cannon.Ammo <= 0 ? "⚠ 포탄 없음" : $"포탄 {cannon.Ammo}/{cannon.MaxAmmo}";
                return $"{head}\n{ammo}\n포신 {cannon.TurretYaw:F0}°";

            case RepairTask repair:
                if (repair.IsRepaired)
                {
                    return $"{head}\n수리 완료";
                }
                string leak = repair.IsEmpty ? "\n⚠ 침수 중" : string.Empty;
                return $"{head}\n{repair.Progress01:P0}  ({repair.Hits}회){leak}";

            case DummyTask dummy:
                return $"{head}\n{dummy.Progress01:P0}  (임시 자리)";

            default:
                return head;
        }
    }

    private static Color ColorFor(TaskBase task)
    {
        if (task is RepairTask repair && !repair.IsRepaired && repair.IsEmpty)
        {
            return LeakingColor;
        }

        return task.IsEmpty ? EmptyColor : OccupiedColor;
    }

    private static void DrawWorker(TaskWorker worker)
    {
        var carry = worker.GetComponent<CarryTask>();
        bool carrying = carry != null && carry.IsCarrying;

        string state = carrying
            ? "⚫ 포탄 운반 중 (양손 묶임)"
            : worker.Current != null
                ? $"→ {worker.Current.DisplayName}"
                : worker.Nearby != null
                    ? $"({worker.Nearby.DisplayName} 근처)"
                    : "빈손";

        _style.normal.textColor = carrying ? CarryColor : WorkerColor;
        Handles.Label(worker.transform.position + Vector3.up * 1.1f, $"{worker.name}\n{state}", _style);
    }

    /// <summary>포탄 상자도 표시한다. 자리가 아니라 TaskBase 목록에 없다.</summary>
    private static void DrawAmmoBoxes()
    {
        foreach (AmmoBox box in Object.FindObjectsByType<AmmoBox>(FindObjectsInactive.Include))
        {
            Color color = box.HasStock ? CarryColor : EmptyColor;

            Handles.color = new Color(color.r, color.g, color.b, 0.55f);
            Handles.DrawWireDisc(box.transform.position, Vector3.up, box.ReachRange);

            _style.normal.textColor = color;
            Handles.Label(box.transform.position + Vector3.up * 1.1f,
                box.HasStock ? "⚫ 포탄 상자" : "⚫ 포탄 상자 (빔)", _style);
        }
    }

    private static void EnsureStyle()
    {
        if (_style != null)
        {
            return;
        }

        _style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.UpperCenter,
            fontSize = 11,
            richText = false,
        };
    }
}
