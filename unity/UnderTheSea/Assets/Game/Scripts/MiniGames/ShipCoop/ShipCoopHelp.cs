using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🆘 도움 요청. 플레이어마다 하나씩 붙는다. (SHIPCOOP.md 7장)
///
/// 왜 필요한가
///   갑판이 3층이 되면서 **서로 안 보이는 곳에 흩어집니다.** 한 층에 한 명씩 맡는 것이
///   이 게임의 전략이고, 그러려면 부를 수단이 있어야 합니다.
///
///   그리고 7장이 이 버튼을 넣은 원래 이유가 더 큽니다.
///   시연장은 시끄럽고 헤드셋도 없습니다. **말이 안 들리면 협동이 안 되고,**
///   협동이 안 되면 이 게임은 그냥 어려운 게임이 됩니다.
///
/// **"도와줘" 가 아니라 "뒷갑판으로 와" 여야 합니다.**
/// 어디로 갈지 모르는 도움 요청은 아무 쓸모가 없습니다. 그래서 층 이름을 함께 싣습니다.
///
/// 무엇이 일어나는가
///   부른 사람   화면에 표시가 남는다
///   나머지      기기가 진동한다 + HUD 에 "🆘 뒷갑판!" 이 뜬다
///
/// 진동은 키보드에서 빈 함수라 아무 일도 안 합니다. 부르는 쪽은 신경 쓰지 않습니다.
/// IoT 기기가 꽂히면 그대로 손이 울립니다. (7장 — 출력이 있다는 점이 중요합니다)
/// </summary>
[RequireComponent(typeof(TaskWorker))]
public class ShipCoopHelp : MonoBehaviour
{
    [Header("아무도 안 왔을 때 남는 시간 (초)")]
    [Tooltip("누가 오면 이 시간과 상관없이 바로 꺼진다.\n\n" +
             "진동은 누르는 순간 한 번 울리고 끝이라 그게 진짜 알림이다.\n" +
             "배지는 **어디로 가야 하는지**를 보여주는 뒤따르는 정보라,\n" +
             "다른 층에 있던 사람이 초상화를 볼 틈만큼은 남아 있어야 한다.")]
    [SerializeField, Min(0.5f)] private float showSeconds = 8f;

    [Header("남을 부를 때 울리는 진동")]
    [SerializeField, Range(0f, 1f)] private float vibrateStrength = 0.8f;
    [SerializeField, Min(0f)] private float vibrateSeconds = 0.4f;

    [Header("연타 막기 (초)")]
    [Tooltip("누르고 있는 동안 계속 울리면 시끄럽기만 하다.")]
    [SerializeField, Min(0f)] private float cooldown = 2f;

    /// <summary>지금 도움을 청하고 있는 사람들. HUD 가 이걸 읽는다.</summary>
    public static IReadOnlyList<ShipCoopHelp> Calling => CallingNow;

    private static readonly List<ShipCoopHelp> CallingNow = new List<ShipCoopHelp>();
    private static readonly List<ShipCoopHelp> AllHelpers = new List<ShipCoopHelp>();

    /// <summary>누군가 도움을 청했다. (부른 사람, 어느 갑판인지)</summary>
    public static event Action<ShipCoopHelp, ShipDeck> Called;

    /// <summary>지금 도움을 청하고 있는지</summary>
    public bool IsCalling => _calling;

    /// <summary>
    /// 도움이 도착해서 꺼졌는지. 시간이 다 되어 꺼진 것과 구분한다.
    ///
    /// **배지가 꺼지는 것 자체가 답장입니다.** 부른 사람은 이걸 보고
    /// "아 오는구나" 를 압니다. 시간이 다 되어 꺼진 것이면 아무도 안 온 것입니다.
    /// </summary>
    public bool WasAnswered { get; private set; }

    private bool _calling;

    /// <summary>부를 때 그 갑판에 있던 사람 수. 이보다 늘면 누가 온 것이다.</summary>
    private int _crewWhenCalled;

    /// <summary>부를 때 있던 갑판. 아직 안 불렀으면 null.</summary>
    public ShipDeck CalledFrom { get; private set; }

    /// <summary>HUD 문구. "🆘 뒷갑판!" · 층을 모르면 "🆘 도움!"</summary>
    public string CallText =>
        CalledFrom != null ? $"🆘 {CalledFrom.DeckName}!" : "🆘 도움!";

    private TaskWorker _worker;
    private float _showUntil;
    private float _nextAllowed;

    private void Awake()
    {
        _worker = GetComponent<TaskWorker>();
    }

    private void OnEnable()
    {
        if (!AllHelpers.Contains(this))
        {
            AllHelpers.Add(this);
        }
    }

    private void OnDisable()
    {
        AllHelpers.Remove(this);
        CallingNow.Remove(this);
        _calling = false;
    }

    private void Update()
    {
        if (_calling)
        {
            CheckAnswered();
        }

        if (_worker == null || _worker.Input == null)
        {
            return;
        }

        if (!ShipCoopInput.ConsumeHelpCall(_worker.Input))
        {
            return;
        }

        if (Time.time < _nextAllowed)
        {
            return;
        }

        Call();
    }

    /// <summary>도움을 청한다. 개발자 모드도 이걸 부를 수 있다.</summary>
    public void Call()
    {
        _nextAllowed = Time.time + cooldown;
        _showUntil = Time.time + showSeconds;

        CalledFrom = ShipDeck.At(transform.position);

        // 지금 이 갑판에 몇 명인지 기억해 둔다. 이보다 늘면 누가 온 것이다.
        //
        // 사람 수로 재는 이유는, **이미 같이 있던 사람 때문에 바로 꺼지는 것**을
        // 막기 위해서다. 중간갑판에 셋이 있는데 🆘 를 눌러도 꺼지지 않는다.
        _crewWhenCalled = CountCrewOn(CalledFrom);

        _calling = true;
        WasAnswered = false;

        if (!CallingNow.Contains(this))
        {
            CallingNow.Add(this);
        }

        RingEveryoneElse();

        Debug.Log($"[🆘] {name} 이 도움을 청했다 — {CallText} (지금 그 갑판에 {_crewWhenCalled}명)", this);
        Called?.Invoke(this, CalledFrom);
    }

    /// <summary>
    /// 도움이 왔는지, 아니면 시간이 다 됐는지 본다.
    ///
    /// <code>
    /// 사람이 늘었다  →  즉시 꺼진다      도움이 왔다
    /// 시간이 다 됐다 →  꺼진다           아무도 안 왔다
    /// </code>
    /// </summary>
    private void CheckAnswered()
    {
        if (CountCrewOn(CalledFrom) > _crewWhenCalled)
        {
            Stop(answered: true);
            return;
        }

        if (Time.time >= _showUntil)
        {
            Stop(answered: false);
        }
    }

    private void Stop(bool answered)
    {
        _calling = false;
        WasAnswered = answered;
        CallingNow.Remove(this);

        Debug.Log($"[🆘] {name} 의 요청이 {(answered ? "닿았다 — 누가 왔다" : "시간이 다 됐다 — 아무도 안 왔다")}", this);
    }

    /// <summary>그 갑판에 지금 몇 명 있는지. 갑판을 모르면 0.</summary>
    private static int CountCrewOn(ShipDeck deck)
    {
        if (deck == null)
        {
            return 0;
        }

        int count = 0;

        for (int i = 0; i < AllHelpers.Count; i++)
        {
            ShipCoopHelp helper = AllHelpers[i];

            if (helper != null && ShipDeck.At(helper.transform.position) == deck)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// 부른 사람 빼고 전부 울린다.
    ///
    /// **자기 자신은 울리지 않습니다.** 부른 사람은 자기가 불렀다는 것을 알고,
    /// 손이 울리면 오히려 놀랍니다.
    /// </summary>
    private void RingEveryoneElse()
    {
        for (int i = 0; i < AllHelpers.Count; i++)
        {
            ShipCoopHelp other = AllHelpers[i];

            if (other == null || other == this)
            {
                continue;
            }

            TaskWorker worker = other._worker;

            if (worker != null && worker.Input != null)
            {
                worker.Input.VibrateBoth(vibrateStrength, vibrateSeconds);
            }
        }
    }
}
