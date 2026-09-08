using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 만드는 동안 Game 화면에 상태를 글자로 띄우는 개발용 표시.
///
/// Scene view 라벨은 Scene 탭을 눌러야 보이는데, 그러면 포커스가 옮겨가서
/// 키가 게임에 들어가지 않습니다. 그래서 Game 화면에도 같은 정보가 필요합니다.
///
/// ⚠ 임시입니다. 문서 9장의 진짜 HUD 를 만들면 이 스크립트를 지웁니다.
///    IMGUI(OnGUI) 로 그리므로 출시용으로 쓸 것이 아닙니다.
///
/// 사용법
///   테스트 씬에 빈 오브젝트를 하나 만들고 붙인다. 나머지는 자동으로 찾는다.
/// </summary>
public class ShipCoopDebugHud : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private bool showKeyGuide = true;
    [SerializeField] private int fontSize = 14;

    [Header("켜고 끄기")]
    [Tooltip("이 키를 누르면 표시가 켜지고 꺼진다.")]
    [SerializeField] private Key toggleKey = Key.F1;

    private ShipCoopGame _game;
    private ShipHealth _health;
    private ShipVoyage _voyage;

    private GUIStyle _style;
    private bool _visible = true;
    private readonly StringBuilder _sb = new StringBuilder(1024);

    private void Awake()
    {
        _game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        _health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        _voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (toggleKey != Key.None && keyboard != null && keyboard[toggleKey].wasPressedThisFrame)
        {
            _visible = !_visible;
        }
    }

    private void OnGUI()
    {
        if (!_visible)
        {
            return;
        }

        EnsureStyle();

        _sb.Clear();
        AppendGame();
        AppendTasks();
        AppendPlayers();

        if (showKeyGuide)
        {
            AppendKeyGuide();
        }

        var area = new Rect(12f, 12f, 460f, Screen.height - 24f);
        GUI.Box(area, GUIContent.none);
        GUI.Label(new Rect(area.x + 10f, area.y + 8f, area.width - 20f, area.height - 16f), _sb.ToString(), _style);
    }

    private void AppendGame()
    {
        if (_game == null)
        {
            _sb.AppendLine("ShipCoopGame 이 없습니다.");
            return;
        }

        _sb.AppendLine($"[{_game.State}]   {_game.Elapsed:F0}s / {_game.TimeLimit:F0}s");

        if (_health != null)
        {
            _sb.AppendLine($"배 HP   {_health.CurrentHp:F0} / {_health.MaxHp:F0}");
        }

        string late = _game.IsBehindSchedule ? "   ⚠ 이 속도면 늦는다" : string.Empty;
        _sb.AppendLine($"진행도  {_game.Progress01:P1}   (있어야 할 위치 {_game.ExpectedProgress01:P1}){late}");

        if (_voyage != null)
        {
            _sb.AppendLine($"속도    {_voyage.Speed:F2} m/s   (돛 {_voyage.SailPower01:P0})");
        }

        _sb.AppendLine();
    }

    private void AppendTasks()
    {
        _sb.AppendLine("── 자리 ──");

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            TaskBase task = TaskBase.All[i];
            _sb.AppendLine($"{task.DisplayName,-4} ({task.Workers.Count}/{task.Capacity})  {StateOf(task)}");
        }

        foreach (AmmoBox box in FindObjectsByType<AmmoBox>(FindObjectsInactive.Exclude))
        {
            _sb.AppendLine($"포탄 상자     {(box.HasStock ? "포탄 있음" : "빔")}");
        }

        _sb.AppendLine();
    }

    private static string StateOf(TaskBase task)
    {
        switch (task)
        {
            case HelmTask helm:
                return $"{helm.Heading:F0}°  {(helm.IsHeadingStraight() ? "정면" : "꺾임")}";
            case SailTask sail:
                return sail.IsSlack ? "⚠ 돛이 풀렸다" : $"돛 {sail.SailPower01:P0}";
            case CannonTask cannon:
                return cannon.Ammo <= 0 ? "⚠ 포탄 없음" : $"포탄 {cannon.Ammo}/{cannon.MaxAmmo}  포신 {cannon.TurretYaw:F0}°";
            case RepairTask repair:
                return repair.IsRepaired ? "수리 완료"
                    : repair.IsEmpty ? $"⚠ 침수 중  ({repair.Progress01:P0})"
                    : $"수리 중  {repair.Progress01:P0} ({repair.Hits}회)";
            default:
                return string.Empty;
        }
    }

    private void AppendPlayers()
    {
        _sb.AppendLine("── 사람 ──");

        foreach (TaskWorker worker in FindObjectsByType<TaskWorker>(FindObjectsInactive.Exclude))
        {
            var carry = worker.GetComponent<CarryTask>();
            bool carrying = carry != null && carry.IsCarrying;

            string state = carrying
                ? CarryHint(carry)
                : worker.Current != null
                    ? $"→ {worker.Current.DisplayName}"
                    : worker.Nearby != null
                        ? $"{worker.Nearby.DisplayName} 근처 — Space 로 붙기"
                        : NearestHint(worker);

            _sb.AppendLine($"{worker.name}   {state}");
        }

        _sb.AppendLine();
    }

    /// <summary>
    /// 포탄을 든 사람에게 지금 무엇을 해야 하는지 알려준다.
    ///
    /// 게임이 사거리 안이라고 보는지를 화면에 그대로 띄운다.
    /// "대포 앞 — Space 로 싣기" 가 뜨는데도 안 실린다면 사거리 문제가 아니라
    /// 키 입력이 게임까지 오지 않는 것이다.
    /// </summary>
    private static string CarryHint(CarryTask carry)
    {
        if (carry.FindLoadableCannon() != null)
        {
            return "⚫ 운반 중 · 대포 앞 — Shift 를 놓으면 들어간다 ★";
        }

        (CannonTask cannon, float distance) = carry.NearestCannon();

        if (cannon == null)
        {
            return "⚫ 운반 중 — 대포가 씬에 없다";
        }

        if (!cannon.HasRoomForAmmo)
        {
            return $"⚫ 운반 중 — 대포가 꽉 찼다 ({cannon.Ammo}/{cannon.MaxAmmo})";
        }

        return $"⚫ 운반 중 — 대포까지 {distance:F1}m  (안으로 {carry.LoadRange:F1}m 들어가야 함)";
    }

    /// <summary>붙을 수 있는 것이 없을 때, 가장 가까운 것과 거리를 알려준다.</summary>
    private static string NearestHint(TaskWorker worker)
    {
        string bestName = null;
        float bestDistance = float.MaxValue;
        Vector3 position = worker.transform.position;

        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            float d = Vector3.Distance(position, TaskBase.All[i].transform.position);
            if (d < bestDistance)
            {
                bestDistance = d;
                bestName = TaskBase.All[i].DisplayName;
            }
        }

        foreach (AmmoBox box in FindObjectsByType<AmmoBox>(FindObjectsInactive.Exclude))
        {
            float d = Vector3.Distance(position, box.transform.position);
            if (d >= bestDistance)
            {
                continue;
            }

            bestDistance = d;
            bestName = "포탄 상자";

            if (box.IsInReach(position))
            {
                return "포탄 상자 앞 — Shift 누른 채 Space 로 집기";
            }
        }

        return bestName == null ? "빈손" : $"빈손 — 가까운 것: {bestName} ({bestDistance:F1}m)";
    }

    private void AppendKeyGuide()
    {
        _sb.AppendLine("── 조작 ──");
        _sb.AppendLine("방향키      이동");
        _sb.AppendLine("Space       자리에 붙기 · 포탄 집기");
        _sb.AppendLine("A  D        조타 꺾기 · 돛 당기기(D) / 풀기(A)");
        _sb.AppendLine("Shift       포탄을 든 채로 유지");
        _sb.AppendLine("            대포 앞에서 놓으면 → 싣기");
        _sb.AppendLine("            그 밖에서 놓으면 → 떨어뜨림");
        _sb.AppendLine("X           대포 발사");
        _sb.AppendLine("F           망치질 (수리)");
        _sb.AppendLine("Q  E        포신 조준");
        _sb.AppendLine("C           도움 요청");
        _sb.AppendLine();
        _sb.AppendLine($"{toggleKey}          이 표시 켜기 / 끄기");
    }

    private void EnsureStyle()
    {
        if (_style != null && _style.fontSize == fontSize)
        {
            return;
        }

        _style = new GUIStyle(GUI.skin.label)
        {
            fontSize = fontSize,
            alignment = TextAnchor.UpperLeft,
            richText = false,
            wordWrap = false,
        };
        _style.normal.textColor = Color.white;
    }
}
