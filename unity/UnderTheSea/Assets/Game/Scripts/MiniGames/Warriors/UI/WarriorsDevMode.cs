using System.Text;
using UnderTheSea.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using Warriors.Net;

/// <summary>
/// 🛠 <b>검 게임 QA 를 빨리 돌리기 위한 개발자 모드.</b> 배 게임의 것과 같은 조작이다.
///
/// <code>
///   P 또는 `   켜기 / 끄기
///   [          앞 페이즈로
///   ]          다음 페이즈로
///   -          지금 페이즈의 목표를 채운다 (2·3 페이즈에서는 보스를 때리는 것)
///   0          무적 켜기 / 끄기
/// </code>
///
/// <b>화면에서 값을 직접 바꾸지 않는다.</b> 판의 상태는 전부 서버가 정하므로 부탁만 보내고
/// 결과를 받는다. 직접 바꾸면 그 사람 화면만 바뀌고 서버와 어긋난다.
///
/// ⚠ <b>Release 빌드에도 들어간다. 실행 인자 <c>-devmode</c> 가 있어야 돈다.</b>
///    (<see cref="DevMode"/>, 에디터는 항상) 전에는 파일 전체를 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>
///    로 감싸 두어 Release 빌드에서는 코드째로 빠졌고, 시연을 Release 로 하니 나오지 않았다.
///    서버도 <c>-devmode</c> 없이 떴으면 <c>WarriorsMatchState.Rpc_DevCommand</c> 가 듣지 않는다.
///
/// ⚠ <b>켜는 키를 배 게임과 같은 P 로 맞췄다</b>(<see cref="DevMode.PanelKey"/>). 시연하는 사람이
///    게임마다 다른 키를 외우지 않게. 예전 키 ` 도 그대로 먹는다. 검 게임은 P 를 다른 데 쓰지 않는다.
///    ⚠ <c>toggleKey</c> 칸은 씬(<c>WarriorsNet.unity</c>)에 ` 로 저장돼 있어서 기본값을 바꿔도 소용이
///    없다. 그래서 P 는 칸이 아니라 코드에서 늘 추가로 받는다.
///
/// ⚠ <b>켜 둔 것은 판이 끝나면 저절로 풀린다.</b> 무적은 서버가 들고 있고
///    <c>WarriorsMatchState.ResetToWaiting</c> 이 끈다. 다음 사람이 무적으로 시작하지 않는다.
///    이 화면 자체도 판이 끝나면 스스로 닫는다.
/// </summary>
public sealed class WarriorsDevMode : MonoBehaviour
{
    /// <summary>
    /// 켜고 끄는 키.
    ///
    /// F1 · F2 를 쓰지 않는 이유는 배 게임과 같다 — 에디터가 먼저 먹는다.
    /// </summary>
    [Tooltip("P 말고 추가로 받는 켜고 끄기 키. P 는 늘 먹는다(DevMode.PanelKey).")]
    [SerializeField] private Key toggleKey = Key.Backquote;

    [Header("한 번에 채우는 양")]
    [Tooltip("- 를 한 번 누를 때 목표를 이만큼 채운다. 2·3 페이즈에서는 보스를 그만큼 때린다.")]
    [SerializeField, Min(1)] private int advanceStep = 3;

    [Header("표시")]
    [SerializeField] private int fontSize = 14;

    [Tooltip("오른쪽 모서리에서 띄울 거리.")]
    [SerializeField, Min(0f)] private float marginRight = 10f;

    [Tooltip("위쪽 모서리에서 띄울 거리. 여기서 아래로 자란다.")]
    [SerializeField, Min(0f)] private float marginTop = 10f;

    /// <summary>지금 켜져 있는가.</summary>
    public bool IsOn { get; private set; }

    private GUIStyle style;
    private readonly StringBuilder text = new StringBuilder(512);

    private void Awake()
    {
        // -devmode 없이 실행했으면 돌지 않는다. 키도 화면도 없다.
        if (!DevMode.Enabled)
        {
            enabled = false;
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return;

        bool toggled = keyboard[DevMode.PanelKey].wasPressedThisFrame
                    || (toggleKey != Key.None && keyboard[toggleKey].wasPressedThisFrame);

        if (toggled)
        {
            IsOn = !IsOn;
            Debug.Log($"[Warriors 개발자] {(IsOn ? "켜짐" : "꺼짐")}", this);
            return;
        }

        if (!IsOn) return;

        WarriorsMatchState match = WarriorsMatchState.Current;
        if (match == null) return;

        // 끝난 판에는 손댈 것이 없다. 화면도 닫는다 — 결과 판을 가리면 안 된다.
        if (match.IsOver)
        {
            IsOn = false;
            return;
        }

        if (keyboard[Key.LeftBracket].wasPressedThisFrame)
        {
            Ask(match, WarriorsMatchState.DevCommand.PreviousPhase, 0);
        }
        else if (keyboard[Key.RightBracket].wasPressedThisFrame)
        {
            Ask(match, WarriorsMatchState.DevCommand.NextPhase, 0);
        }
        else if (keyboard[Key.Minus].wasPressedThisFrame)
        {
            Ask(match, WarriorsMatchState.DevCommand.Advance, advanceStep);
        }
        else if (keyboard[Key.Digit0].wasPressedThisFrame)
        {
            Ask(match, WarriorsMatchState.DevCommand.ToggleInvincible, 0);
        }
    }

    /// <summary>서버에 부탁한다. 클라이언트에서도 RPC 로 전달된다.</summary>
    private static void Ask(WarriorsMatchState match, WarriorsMatchState.DevCommand command, int amount)
    {
        if (match.Object == null || !match.Object.IsValid) return;

        match.Rpc_DevCommand(command, amount);
    }

    private void OnGUI()
    {
        if (!IsOn) return;

        WarriorsMatchState match = WarriorsMatchState.Current;
        if (match == null || match.Object == null || !match.Object.IsValid) return;

        style ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = fontSize,
            richText = false,
            padding = new RectOffset(10, 10, 8, 8),
        };

        text.Clear();
        text.AppendLine($"── 검 개발자 모드 ({DevMode.PanelKey} 로 끈다) ──");
        text.AppendLine($"페이즈 {WarriorsMatchState.RoundOf(match.Phase)}  ·  {match.Phase}");
        text.AppendLine($"목표   {Progress(match)}");
        text.AppendLine($"점수   {match.Score}   경과 {match.Elapsed:0}초");

        // 무적은 서버만 아는 값이라 여기서 켜짐/꺼짐을 보여 주지 못한다.
        // (네트워크 값으로 두었다가 오브젝트 칸 수가 어긋나 판이 통째로 깨졌다)
        // 켰는지 껐는지는 서버 로그에 남는다.

        text.AppendLine();
        text.AppendLine($"  [ ]   페이즈 앞뒤로");
        text.AppendLine($"  -     목표 +{advanceStep} (보스 때리기)");
        text.AppendLine($"  0     무적 켜기/끄기 (서버 로그에 남음)");

        // ⚠ **높이를 0 으로 넘기면 안 된다.** 상자가 그 높이에 맞춰 잘려 글자 한 줄만 보인다.
        //    처음에 0 을 넘겼더니 오른쪽 위 모서리에 한 조각만 나왔다.
        //    내용에 맞는 크기를 재서 넘긴다 — 오른쪽 위에서 아래로 자란다.
        GUIContent content = new GUIContent(text.ToString());
        Vector2 size = style.CalcSize(content);

        Rect where = new Rect(
            Screen.width - size.x - marginRight,
            marginTop,
            size.x,
            size.y);

        GUI.Box(where, content, style);
    }

    private static string Progress(WarriorsMatchState match) => match.Phase switch
    {
        WarriorsMatchPhase.Phase1 => $"{match.Phase1Kills} / {match.Phase1Target}",
        WarriorsMatchPhase.Phase2 => $"{match.Phase2Hits} / {match.Phase2Target}",
        WarriorsMatchPhase.Phase3 => $"{match.Phase3Hits} / {match.Phase3Target}",
        _ => "—",
    };
}
