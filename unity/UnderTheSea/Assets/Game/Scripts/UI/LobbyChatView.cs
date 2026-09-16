using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 로비 채팅창. **화면만** 담당합니다. 주고받는 것은 아직 없습니다.
///
/// 지금 되는 것
///   · 입력칸을 **클릭하면** 글자를 칠 수 있습니다
///   · 치는 동안 캐릭터가 걸어가지 않습니다 (<see cref="ChatFocus"/>)
///   · 전송하면 목록에 한 줄 쌓입니다 — **아직 나에게만 보입니다**
///   · 튜토리얼이 도는 동안에는 숨어 있다가 끝나면 나옵니다
///
/// <b>왜 클릭으로만 켜는가.</b>
/// Enter 로 켜면 손이 키보드를 안 떠나 편하지만, 켜진 줄 모르고 걸어가려다
/// 글자만 쌓이는 일이 생깁니다. 클릭은 본인이 분명히 의도한 행동입니다.
/// (IOT_INPUT.md 8장 — 끄는 방법은 채팅 담당이 정한다)
///
/// 끄는 것은 셋 다 됩니다. **전송 · Esc · 바깥 클릭.**
///
/// <b>자리는 왼쪽 아래입니다.</b> 튜토리얼 안내와 같은 자리라 겹치는데,
/// 튜토리얼이 도는 동안 숨는 것으로 피합니다. 둘을 나란히 두면 화면 구석이 복잡해지고,
/// 튜토리얼은 몇십 초면 끝나므로 잠깐 양보하는 편이 낫습니다.
///
/// <b>메시지를 보내는 것은 다음 단계입니다.</b> <see cref="Submitted"/> 를 구독하면
/// 네트워크 쪽이 그 문자열을 가져다 쓰면 됩니다. 이 컴포넌트는 Fusion 을 모릅니다.
/// </summary>
public class LobbyChatView : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private GameObject panel;
    [SerializeField] private TMP_InputField input;
    [SerializeField] private Button sendButton;

    [Tooltip("메시지 한 줄들이 쌓이는 곳. 세로 Layout Group 이 붙어 있어야 한다.")]
    [SerializeField] private RectTransform messageRoot;

    [Tooltip("한 줄의 본보기. 꺼진 채로 두고 복제해서 쓴다.")]
    [SerializeField] private TMP_Text messageTemplate;

    [Header("한도")]
    [Tooltip("이보다 많아지면 오래된 줄부터 지운다. 끝없이 쌓으면 느려진다.")]
    [SerializeField, Min(10)] private int maxMessages = 60;

    [Tooltip("한 번에 보낼 수 있는 글자 수.")]
    [SerializeField, Min(10)] private int maxLength = 100;

    /// <summary>
    /// 보내기를 눌렀다. 인자는 다듬은 메시지다.
    ///
    /// 네트워크 쪽이 이것을 듣고 실제 전송을 맡는다. 이 컴포넌트는 화면만 안다.
    /// </summary>
    public event Action<string> Submitted;

    private readonly List<TMP_Text> lines = new List<TMP_Text>();

    private void Awake()
    {
        if (messageTemplate != null)
        {
            messageTemplate.gameObject.SetActive(false);
        }

        if (input != null)
        {
            input.characterLimit = maxLength;

            // 클릭해서 켜졌을 때와 꺼졌을 때를 그대로 ChatFocus 로 옮긴다.
            input.onSelect.AddListener(_ => ChatFocus.Begin());
            input.onDeselect.AddListener(_ => ChatFocus.End());

            // Enter 로 보낸다. 켜는 것은 클릭만, 보내는 것은 Enter 도 된다.
            input.onSubmit.AddListener(_ => Send());
        }

        if (sendButton != null)
        {
            sendButton.onClick.AddListener(Send);
        }
    }

    private void OnEnable()
    {
        // 튜토리얼이 도는 동안에는 비켜 준다. 자리가 겹친다.
        LobbyTutorial.Started += Hide;
        LobbyTutorial.Finished += Show;

        // 이미 돌고 있는 중에 켜졌을 수 있다. 그때는 바로 숨는다.
        if (LobbyTutorial.IsRunning)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private void OnDisable()
    {
        LobbyTutorial.Started -= Hide;
        LobbyTutorial.Finished -= Show;

        // ⚠ 켜 둔 채로 사라지면 영영 못 움직인다. 반드시 내린다.
        ChatFocus.End();
    }

    private void Update()
    {
        // Esc 로 입력을 끈다. 바깥을 클릭하면 onDeselect 가 알아서 꺼 준다.
        if (ChatFocus.Typing && Input.GetKeyDown(KeyCode.Escape))
        {
            Unfocus();
        }
    }

    /// <summary>목록에 한 줄 더한다. 네트워크로 받은 남의 말도 이리로 들어온다.</summary>
    public void Append(string speaker, string message)
    {
        if (messageTemplate == null || messageRoot == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        TMP_Text line = Instantiate(messageTemplate, messageRoot);
        line.gameObject.SetActive(true);

        // 시각을 붙인다. 시안처럼 "누가 · 무엇을 · 언제" 가 한 줄에 들어간다.
        line.text = string.IsNullOrWhiteSpace(speaker)
            ? message
            : $"<b>{speaker}</b>  {message}   <size=80%><alpha=#99>{DateTime.Now:HH:mm}</size>";

        lines.Add(line);

        // 오래된 줄부터 지운다.
        while (lines.Count > maxMessages)
        {
            if (lines[0] != null)
            {
                Destroy(lines[0].gameObject);
            }

            lines.RemoveAt(0);
        }
    }

    /// <summary>입력칸을 비우고 포커스를 푼다.</summary>
    public void Unfocus()
    {
        if (input != null)
        {
            input.DeactivateInputField();
        }

        ChatFocus.End();
    }

    public void Show()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }
    }

    public void Hide()
    {
        // 숨기기 전에 입력을 반드시 끈다. 켜진 채로 숨으면 못 움직인다.
        Unfocus();

        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void Send()
    {
        if (input == null)
        {
            return;
        }

        string text = input.text != null ? input.text.Trim() : string.Empty;

        input.text = string.Empty;

        if (text.Length == 0)
        {
            // 빈 줄은 보내지 않는다. 다만 포커스는 그대로 둔다 — 이어서 치던 중일 수 있다.
            return;
        }

        // ⚠ 아직 나에게만 보인다. 네트워크는 Submitted 를 듣는 쪽이 맡는다.
        Append("나", text);

        Submitted?.Invoke(text);
    }
}
