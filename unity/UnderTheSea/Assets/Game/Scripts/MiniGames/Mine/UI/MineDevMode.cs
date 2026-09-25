using System.Text;
using Mine.Net;
using UnderTheSea.Core;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 🛠 <b>광산 시연을 빨리 넘기기 위한 개발자 모드.</b> 배 · 검 게임과 같은 방식이다.
///
/// <code>
///   P   켜기 / 끄기
///   ]   지금 턴인 사람 건너뛰기 (마지막 사람이면 판이 끝나고 평소대로 채점)
///   -   판을 지금 끝내고 무조건 성공
/// </code>
///
/// <b>화면에서 값을 직접 바꾸지 않는다.</b> 판의 상태는 전부 서버가 정하므로 부탁만 보내고
/// 결과를 받는다. 직접 바꾸면 그 사람 화면만 바뀌고 서버와 어긋난다.
///
/// ⚠ <b>Release 빌드에도 들어간다. 실행 인자 <c>-devmode</c> 가 있어야 돈다.</b>
///    (<see cref="DevMode"/>, 에디터는 항상) 서버도 <c>-devmode</c> 없이 떴으면
///    <c>MineMatchState.Rpc_DevCommand</c> 가 듣지 않는다.
/// </summary>
public sealed class MineDevMode : MonoBehaviour
{
    [Header("표시")]
    [SerializeField] private int fontSize = 14;

    [Tooltip("오른쪽 모서리에서 띄울 거리.")]
    [SerializeField, Min(0f)] private float marginRight = 10f;

    [Tooltip("위쪽 모서리에서 띄울 거리. 여기서 아래로 자란다.")]
    [SerializeField, Min(0f)] private float marginTop = 10f;

    /// <summary>지금 켜져 있는가.</summary>
    public bool IsOn { get; private set; }

    private GUIStyle style;
    private readonly StringBuilder text = new StringBuilder(256);

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

        if (keyboard[DevMode.PanelKey].wasPressedThisFrame)
        {
            IsOn = !IsOn;
            Debug.Log($"[Mine 개발자] {(IsOn ? "켜짐" : "꺼짐")}", this);
            return;
        }

        if (!IsOn) return;

        MineMatchState match = MineMatchState.Current;
        if (match == null) return;

        // 끝난 판에는 손댈 것이 없다. 화면도 닫는다 — 결과 판을 가리면 안 된다.
        if (match.IsOver)
        {
            IsOn = false;
            return;
        }

        if (keyboard[Key.RightBracket].wasPressedThisFrame)
        {
            Ask(match, MineMatchState.DevCommand.SkipTurn);
        }
        else if (keyboard[Key.Minus].wasPressedThisFrame)
        {
            Ask(match, MineMatchState.DevCommand.ForceSuccess);
        }
    }

    /// <summary>서버에 부탁한다. 클라이언트에서도 RPC 로 전달된다.</summary>
    private static void Ask(MineMatchState match, MineMatchState.DevCommand command)
    {
        if (match.Object == null || !match.Object.IsValid) return;

        match.Rpc_DevCommand(command);
    }

    private void OnGUI()
    {
        if (!IsOn) return;

        MineMatchState match = MineMatchState.Current;
        if (match == null || match.Object == null || !match.Object.IsValid) return;

        style ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = fontSize,
            richText = false,
            padding = new RectOffset(10, 10, 8, 8),
        };

        text.Clear();
        text.AppendLine($"── 광산 개발자 모드 ({DevMode.PanelKey} 로 끈다) ──");
        text.AppendLine($"상태   {match.Phase}");
        text.AppendLine(match.CurrentSlot >= 0
            ? $"턴     P{match.CurrentSlot + 1} / {match.RosterSize}   남은 {match.TurnTimeLeft:0}초"
            : "턴     —");

        text.AppendLine();
        text.AppendLine("  ]   지금 턴 건너뛰기");
        text.AppendLine("  -   무조건 성공으로 끝내기");

        // 높이를 0 으로 넘기면 상자가 잘린다. 내용에 맞는 크기를 재서 오른쪽 위에서 아래로 자라게 한다.
        GUIContent content = new GUIContent(text.ToString());
        Vector2 size = style.CalcSize(content);

        Rect where = new Rect(
            Screen.width - size.x - marginRight,
            marginTop,
            size.x,
            size.y);

        GUI.Box(where, content, style);
    }
}
