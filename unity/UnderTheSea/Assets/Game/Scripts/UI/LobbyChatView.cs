using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 로비 채팅창. **화면만** 담당합니다. 주고받는 것은 아직 없습니다.
///
/// 지금 되는 것
///   · **Enter 를 치면** 입력칸이 열립니다 (클릭해도 됩니다)
///   · 치는 동안 캐릭터가 걸어가지 않습니다 (<see cref="ChatFocus"/>)
///   · 전송하면 목록에 한 줄 쌓입니다 — **아직 나에게만 보입니다**
///   · 튜토리얼이 도는 동안에는 숨어 있다가 끝나면 나옵니다
///
/// <b>왜 Enter 로 여는가.</b>
/// WASD 로 걷는 게임의 관례입니다(WoW · 롤 · 마인크래프트 모두 Enter 로 치고
/// Enter 로 보냅니다). 손이 키보드를 안 떠납니다.
///
/// 처음에는 클릭으로만 열었습니다. "켜진 줄 모르고 걸어가려다 글자만 쌓인다" 를
/// 걱정해서였는데, 실제로 난 문제는 그 반대였습니다. EventSystem 의 Navigate 가
/// WASD 에 묶여 있어서 **걷다가 저절로 입력칸이 켜졌습니다.** 그쪽은
/// Navigation.Mode.None 으로 막았고(Awake 참고), 여는 방법은 관례대로 되돌렸습니다.
/// (IOT_INPUT.md 9장 — 끄는 방법은 채팅 담당이 정한다)
///
/// 끄는 것은 넷 다 됩니다. **Enter(전송) · Esc(취소) · 보내기 버튼 · 바깥 클릭.**
/// 보내고 나면 반드시 닫습니다. 그래야 다음 Enter 가 다시 엽니다.
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

    [Tooltip("확성기. 켜면 보낸 말이 화면 맨 위 띠로 모두에게 뜬다.")]
    [SerializeField] private Button megaphoneButton;

    [Tooltip("확성기가 켜졌을 때 도드라지게 할 그림. 없어도 된다.")]
    [SerializeField] private Image megaphoneGlow;

    [Tooltip("메시지 한 줄들이 쌓이는 곳. 세로 Layout Group 이 붙어 있어야 한다.")]
    [SerializeField] private RectTransform messageRoot;

    [Tooltip("한 줄의 본보기. 꺼진 채로 두고 복제해서 쓴다.")]
    [SerializeField] private LobbyChatLine messageTemplate;

    [Header("한도")]
    [Tooltip("이보다 많아지면 오래된 줄부터 지운다. 끝없이 쌓으면 느려진다.")]
    [SerializeField, Min(10)] private int maxMessages = 60;

    [Tooltip("한 번에 보낼 수 있는 글자 수.")]
    [SerializeField, Min(10)] private int maxLength = 100;

    [Header("되짚어 보기")]
    [Tooltip("휠 한 칸에 몇 칸 올릴까. 한 줄이 대략 24 칸이다.")]
    [SerializeField, Min(8f)] private float scrollStep = 48f;

    /// <summary>
    /// 보내기를 눌렀다. 인자는 다듬은 메시지다.
    ///
    /// 네트워크 쪽이 이것을 듣고 실제 전송을 맡는다. 이 컴포넌트는 화면만 안다.
    /// </summary>
    public event Action<string, bool> Submitted;

    /// <summary>
    /// 지금 화면에 있는 채팅창. 없으면 null.
    ///
    /// 네트워크 쪽(<c>LobbyChatRelay</c>)이 받은 줄을 넣을 곳을 찾을 때 쓴다.
    /// 캐릭터마다 하나씩 있는 컴포넌트가 화면을 이름으로 뒤지지 않게 하기 위함이다.
    /// </summary>
    public static LobbyChatView Current { get; private set; }

    /// <summary>
    /// 엔터로 입력칸을 연 · 닫은 프레임. 같은 프레임에 다시 열리는 것을 막는다.
    ///
    /// 엔터를 치면 두 곳이 반응한다. TMP 입력칸의 <c>onSubmit</c>(보내고 닫는다)과
    /// 아래 <see cref="Update"/>(닫혀 있으면 연다)다. 둘의 실행 순서는 스크립트
    /// 실행 순서에 달려 있어 보장되지 않는다. 표시가 없으면 **보내고 닫은 그 프레임에
    /// 곧바로 다시 열려** 엔터로 닫을 수가 없다.
    /// </summary>
    private int handledEnterFrame = -1;

    private readonly List<LobbyChatLine> lines = new List<LobbyChatLine>();

    /// <summary>평소 입력칸 안내 문구.</summary>
    private const string NormalHint = "메시지를 입력하세요";

    /// <summary>확성기를 켰을 때의 안내 문구. 평소와 확실히 달라야 한다.</summary>
    private const string MegaphoneHint = "전체 공지로 보냅니다";

    /// <summary>확성기가 켜져 있는가. 한 번 보내면 저절로 꺼진다.</summary>
    private bool megaphoneOn;

    private void Awake()
    {
        if (messageTemplate != null)
        {
            messageTemplate.gameObject.SetActive(false);
        }

        if (input != null)
        {
            input.characterLimit = maxLength;

            // 켜졌을 때와 꺼졌을 때를 그대로 ChatFocus 로 옮긴다.
            input.onSelect.AddListener(_ => ChatFocus.Begin(this));
            input.onDeselect.AddListener(_ => ChatFocus.End(this));

            // Enter 로 보낸다. 보낸 뒤에는 닫아서, 다음 Enter 가 다시 연다.
            input.onSubmit.AddListener(_ => Send());
        }

        // ⚠ **WASD 로 걷다가 채팅칸이 저절로 켜지던 것을 막는다.**
        //    EventSystem 이 InputSystemUIInputModule + 유니티 기본 액션을 쓰는데,
        //    거기서 Navigate 가 WASD 와 화살표에 묶여 있다. 그래서 걸어가는 동안
        //    UI 선택이 옆으로 옮겨 다니다 채팅칸에 닿으면 onSelect 가 떠서
        //    ChatFocus 가 걸리고, 그 순간부터 캐릭터가 멈춘다.
        //
        //    Navigation.Mode.None 인 Selectable 은 방향 이동의 **후보에서 빠진다.**
        //    그래서 채팅 부품 셋을 모두 빼 둔다. 마우스 클릭과 아래의 Enter 는
        //    그대로 동작한다 — 선택 자체를 막는 것이 아니라 방향키로 닿는 길만 끊는다.
        foreach (Selectable selectable in new Selectable[] { input, sendButton, megaphoneButton })
        {
            if (selectable == null)
            {
                continue;
            }

            Navigation none = selectable.navigation;
            none.mode = Navigation.Mode.None;
            selectable.navigation = none;
        }

        if (sendButton != null)
        {
            sendButton.onClick.AddListener(Send);
        }

        if (megaphoneButton != null)
        {
            megaphoneButton.onClick.AddListener(ToggleMegaphone);
        }

        SetMegaphone(false);
    }

    private void OnEnable()
    {
        Current = this;

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
        if (Current == this)
        {
            Current = null;
        }

        LobbyTutorial.Started -= Hide;
        LobbyTutorial.Finished -= Show;

        // ⚠ 켜 둔 채로 사라지면 영영 못 움직인다. 반드시 내린다.
        //    Begin 을 부른 적이 없어도 안전하다 — 집합에 없는 것을 빼는 것뿐이다.
        ChatFocus.End(this);

        // 휠도 같은 이유로 내린다. 쥔 채로 사라지면 카메라 줌이 영영 안 먹는다.
        ChatFocus.EndWheel(this);
    }

    private void Update()
    {
        UpdateWheel();

        // Esc 로 입력을 끈다. 바깥을 클릭하면 onDeselect 가 알아서 꺼 준다.
        if (ChatFocus.Typing && Input.GetKeyDown(KeyCode.Escape))
        {
            Unfocus();
            return;
        }

        if (!EnterPressed())
        {
            return;
        }

        // 이 프레임에 이미 엔터를 처리했다(보내고 닫았다). 곧바로 다시 열지 않는다.
        if (handledEnterFrame == Time.frameCount)
        {
            return;
        }

        // 무언가 입력을 잡고 있으면 열지 않는다.
        //   · 채팅을 치는 중이면 입력칸의 onSubmit 이 보내기를 맡는다
        //   · 제단 UI 처럼 남이 잡고 있으면 끼어들지 않는다
        if (ChatFocus.Typing)
        {
            return;
        }

        Focus();
    }

    /// <summary>엔터를 눌렀는가. 숫자판 엔터도 같이 본다.</summary>
    private static bool EnterPressed()
    {
        return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
    }

    /// <summary>
    /// 입력칸을 열고 커서를 둔다.
    ///
    /// 숨어 있을 때는 열지 않는다. 튜토리얼이 도는 동안 채팅은 비켜 있는데,
    /// 그때 엔터로 열리면 보이지 않는 칸에 글자가 들어가고 캐릭터만 멈춘다.
    /// </summary>
    public void Focus()
    {
        if (input == null || !input.gameObject.activeInHierarchy)
        {
            return;
        }

        handledEnterFrame = Time.frameCount;

        // ⚠ **잠금은 여기서 직접 건다.** onSelect 에 맡기면 안 된다.
        //
        //    onSelect 는 EventSystem 의 선택이 **바뀔 때만** 뜬다. 그런데 아래
        //    DeactivateInputField 는 편집만 끝내고 선택은 입력칸에 그대로 남겨 둔다.
        //    그래서 한 번 보낸 뒤 엔터로 다시 열면 Select() 가 "이미 이게 선택돼 있다"
        //    로 일찍 빠져나가고, onSelect 가 안 떠서 Begin 이 안 불린다.
        //    글자는 쳐지는데 **캐릭터가 같이 걸어간다.** 실제로 그렇게 됐다.
        //
        //    여는 쪽이 잠그는 것이 맞다. 같은 주인이 여러 번 걸어도 결과가 같아서
        //    뒤이어 onSelect 가 떠도 문제없다.
        ChatFocus.Begin(this);

        input.Select();
        input.ActivateInputField();
    }

    /// <summary>목록에 한 줄 더한다. 네트워크로 받은 남의 말도 이리로 들어온다.</summary>
    public void Append(string speaker, string message)
    {
        if (messageTemplate == null || messageRoot == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        // ⚠ 새 줄을 넣기 **전에** 재 둔다. 넣고 나면 넘침이 늘어서, 맨 아래에 있던
        //    사람도 "위로 올라가 읽는 중" 으로 잘못 읽힌다.
        bool follow = IsAtBottom();

        LobbyChatLine line = Instantiate(messageTemplate, messageRoot);
        line.gameObject.SetActive(true);

        // 시안처럼 칸을 나눠 넣는다. 줄 맞춤은 줄 쪽이 한다.
        line.Set(
            string.IsNullOrWhiteSpace(speaker) ? "?" : speaker,
            message,
            DateTime.Now.ToString("HH:mm"));

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

        ScrollToNewest(follow);
    }

    /// <summary>줄이 쌓이는 칸. 이보다 내용이 길어지면 넘친 만큼 위로 밀린다.</summary>
    private RectTransform MessageArea =>
        messageRoot != null ? messageRoot.parent as RectTransform : null;

    /// <summary>
    /// 넘친 높이. 0 이면 다 보이는 것이고, 이 값이 <c>anchoredPosition.y</c> 의 위쪽 끝이다.
    ///
    /// <c>y = 0</c> 이 가장 오래된 줄, <c>y = overflow</c> 가 가장 새 줄이다.
    /// 목록이 **위에서부터** 쌓이기 때문에 이렇게 뒤집혀 있다.
    /// </summary>
    private float Overflow
    {
        get
        {
            RectTransform area = MessageArea;

            return area == null ? 0f : Mathf.Max(0f, messageRoot.rect.height - area.rect.height);
        }
    }

    /// <summary>맨 아래(가장 새 줄)를 보고 있는가. 1 칸은 반올림 오차를 봐준다.</summary>
    private bool IsAtBottom()
    {
        return messageRoot == null || messageRoot.anchoredPosition.y >= Overflow - 1f;
    }

    /// <summary>
    /// 휠로 지난 대화를 되짚는다.
    ///
    /// <b>마우스가 기록 칸 위에 있을 때만 먹는다.</b> 휠은 원래 카메라 줌이 쓰는데
    /// (<c>LocalPlayerView</c>), 둘이 같이 반응하면 대화를 올리는 동안 화면까지 줌된다.
    /// 그래서 올라가 있는 동안 <see cref="ChatFocus.BeginWheel"/> 로 휠을 가져가고,
    /// 카메라 쪽은 <see cref="ChatFocus.WheelHeld"/> 를 보고 비켜 준다.
    ///
    /// ⚠ 이동까지 막지는 않는다. 마우스만 올려 둔 것으로 발이 묶이면 안 된다.
    ///    그래서 <see cref="ChatFocus.Typing"/> 과 다른 집합을 쓴다.
    ///
    /// 되짚을 수 있는 깊이는 <see cref="maxMessages"/> 가 정한다. 그보다 오래된 줄은
    /// 이미 지워져 있어서, 스크롤을 끝까지 올려도 그 위로는 없다.
    /// </summary>
    private void UpdateWheel()
    {
        bool over = false;
        RectTransform area = MessageArea;

        if (area != null && area.gameObject.activeInHierarchy)
        {
            Canvas canvas = area.GetComponentInParent<Canvas>();

            // Overlay 는 카메라를 넘기면 안 된다. 넘기면 좌표가 어긋나 늘 빗나간다.
            Camera eye = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            over = RectTransformUtility.RectangleContainsScreenPoint(area, Input.mousePosition, eye);
        }

        if (over)
        {
            ChatFocus.BeginWheel(this);
        }
        else
        {
            ChatFocus.EndWheel(this);
            return;
        }

        float wheel = Input.GetAxis("Mouse ScrollWheel");

        if (Mathf.Approximately(wheel, 0f))
        {
            return;
        }

        // 휠 값의 크기는 장치마다 다르다(휠 한 칸이 0.1 이기도 하고 1 이기도 하다).
        // 방향만 보고 한 칸씩 움직여야 어디서나 같은 만큼 올라간다.
        Vector2 at = messageRoot.anchoredPosition;
        at.y = Mathf.Clamp(at.y - Mathf.Sign(wheel) * scrollStep, 0f, Overflow);
        messageRoot.anchoredPosition = at;
    }

    /// <summary>
    /// 새 줄이 보이게 목록을 끌어올린다.
    ///
    /// 목록은 **위에서부터** 쌓인다. 몇 줄 없을 때 아래에 붙여 두면 위쪽이 텅 비고,
    /// 입력줄 바로 위에 글자가 몰려 읽기 나쁘다.
    ///
    /// 대신 줄이 칸을 넘치면 넘친 만큼 위로 밀어 마지막 줄이 늘 아래에 보이게 한다.
    /// 스크롤 막대는 붙이지 않았다. 좁은 칸을 또 나눠 써야 하기 때문이다.
    /// 되짚어 볼 일은 휠이 맡는다 (<see cref="UpdateWheel"/>).
    /// </summary>
    /// <param name="follow">
    /// 맨 아래로 따라 내려갈까. <b>지난 대화를 올려 읽는 중이면 false 로 넘긴다.</b>
    /// 늘 끌어내리면 읽던 자리가 새 줄이 올 때마다 튕겨 나가 되짚기가 불가능해진다.
    /// </param>
    private void ScrollToNewest(bool follow)
    {
        if (MessageArea == null)
        {
            return;
        }

        // 방금 넣은 줄까지 넣고 재야 한다. 한 프레임 늦으면 한 줄만큼 어긋난다.
        LayoutRebuilder.ForceRebuildLayoutImmediate(messageRoot);

        float overflow = Overflow;

        Vector2 at = messageRoot.anchoredPosition;

        // 올려 읽는 중이면 자리를 지킨다. 다만 오래된 줄이 지워져 내용이 짧아졌을 수
        // 있으므로, 범위 밖으로 나간 자리는 끌어다 넣는다.
        at.y = follow ? overflow : Mathf.Clamp(at.y, 0f, overflow);
        messageRoot.anchoredPosition = at;
    }

    /// <summary>확성기를 켜고 끈다. 버튼이 부른다.</summary>
    public void ToggleMegaphone()
    {
        SetMegaphone(!megaphoneOn);

        // 켠 직후에는 바로 칠 수 있게 입력칸으로 보낸다. 확성기를 누른 사람은
        // 곧바로 무언가 칠 참이다. 한 번 더 클릭하게 만들 이유가 없다.
        if (megaphoneOn)
        {
            Focus();
        }
    }

    /// <summary>
    /// 확성기 상태를 정하고 화면에 표시한다.
    ///
    /// ⚠ <b>켜져 있다는 것이 눈에 보여야 한다.</b> 안 보이면 잡담을 치고 전송을 눌러
    ///    모두의 화면 한가운데 띄우게 된다. 되돌릴 수 없는 실수다.
    ///    그래서 버튼에 불을 켜고 입력칸 안내 문구까지 바꾼다.
    /// </summary>
    private void SetMegaphone(bool on)
    {
        megaphoneOn = on;

        if (megaphoneGlow != null)
        {
            megaphoneGlow.enabled = on;
        }

        if (input != null && input.placeholder is TMP_Text hint)
        {
            hint.text = on ? MegaphoneHint : NormalHint;
        }
    }

    /// <summary>입력칸을 비우고 포커스를 푼다.</summary>
    public void Unfocus()
    {
        // 닫은 프레임을 남긴다. 엔터로 닫은 그 프레임에 Update 가 다시 열면
        // 엔터로는 영영 닫을 수 없다.
        handledEnterFrame = Time.frameCount;

        if (input != null)
        {
            input.DeactivateInputField();

            // ⚠ EventSystem 의 **선택까지** 푼다. DeactivateInputField 는 편집만 끝내고
            //    선택은 입력칸에 남겨 둔다. 남겨 두면 다음 Select() 가 일찍 빠져나가
            //    onSelect · onDeselect 가 짝이 어긋난 채로 계속 간다. (Focus 참고)
            //
            //    입력칸이 선택돼 있을 때만 푼다. 남이 잡고 있는 선택을 뺏지 않는다.
            EventSystem events = EventSystem.current;

            if (events != null && events.currentSelectedGameObject == input.gameObject)
            {
                events.SetSelectedGameObject(null);
            }
        }

        // 위에서 onDeselect 가 떠 End 가 이미 불렸을 수 있다. 두 번 불려도 결과가 같다.
        ChatFocus.End(this);
    }

    /// <summary>
    /// 배치를 맞출 때 쓴다. 부품 ⋮ 메뉴에서 고른다.
    ///
    /// <b>Play 를 안 눌러도 된다.</b> 편집 중에는 글자를 칠 수 없어 목록이 늘 비어 있고,
    /// 빈 창을 보면서 칸 너비를 맞추면 반드시 어긋난다. 그래서 그럴듯한 줄을 몇 개 채워 준다.
    ///
    /// ⚠ 채운 줄에는 <see cref="HideFlags.DontSave"/> 를 걸어 둔다. 안 걸면 프리팹을
    ///    저장할 때 이 예시 줄들이 통째로 박혀서, 게임에서도 늘 떠 있게 된다.
    /// </summary>
    [ContextMenu("채팅 미리 채우기")]
    public void PreviewFill()
    {
        PreviewClear();
        Show();

        if (messageTemplate != null)
        {
            messageTemplate.gameObject.SetActive(false);
        }

        Append("지훈", "광장에 모여요!");
        Append("서연", "심장 제단 퀘스트 같이 할 분?");
        Append("민수", "곧 갈게요");
        Append("하은", "안녕하세요!");
        Append("지훈", "보물섬 탐험대 모집합니다. 관심 있으신 분 귓말 주세요");

        if (!Application.isPlaying)
        {
            foreach (LobbyChatLine line in lines)
            {
                if (line != null)
                {
                    line.gameObject.hideFlags = HideFlags.DontSave;
                }
            }
        }

        // ⚠ 이것이 없으면 칸 크기가 이번 프레임에 안 다시 계산돼서,
        //    숫자를 바꿔도 화면은 옛 모습 그대로 보인다.
        Canvas.ForceUpdateCanvases();
    }

    [ContextMenu("채팅 미리보기 지우기")]
    public void PreviewClear()
    {
        foreach (LobbyChatLine line in lines)
        {
            if (line == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(line.gameObject);
            }
            else
            {
                DestroyImmediate(line.gameObject);
            }
        }

        lines.Clear();

        if (messageRoot != null)
        {
            messageRoot.anchoredPosition = Vector2.zero;
        }
    }

    public void Show()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }

        // 숨어 있는 동안 쌓인 줄이 있다. 다시 나올 때는 맨 아래를 보여 준다.
        ScrollToNewest(true);
    }

    public void Hide()
    {
        // 숨기기 전에 입력을 반드시 끈다. 켜진 채로 숨으면 못 움직인다.
        Unfocus();

        // ⚠ 휠도 돌려준다. 마우스를 기록 칸 위에 둔 채로 숨으면 Update 가 안 돌아
        //    EndWheel 을 부를 기회가 없다. 그대로 두면 **카메라 줌이 영영 안 먹는다.**
        ChatFocus.EndWheel(this);

        // 확성기도 같이 내린다. 켜 둔 채로 숨었다가 다시 나오면
        // 본인은 잊었는데 다음 한 마디가 전체 공지로 나간다.
        SetMegaphone(false);

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
            // 빈 줄은 보내지 않는다. 그래도 **닫는다.** 엔터가 열고 닫는 한 짝이라,
            // 빈 채로 엔터를 치면 그만두겠다는 뜻으로 보는 편이 자연스럽다.
            Unfocus();
            return;
        }

        // ⚠ 여기서 내 화면에 먼저 찍지 않는다. 서버를 한 바퀴 돌아 Rpc_Receive 로 돌아온다.
        //    먼저 찍으면 내 말만 두 번 보인다. 조금 늦더라도 모두가 같은 것을 보는 편이 낫다.
        bool global = megaphoneOn;

        // ⚠ 보내고 나면 **반드시 끈다.** 켜 둔 채로 두면 다음에 무심코 친 잡담이
        //    모두의 화면 한가운데 뜬다. 확성기는 한 번에 한 마디다.
        SetMegaphone(false);

        // 보냈으면 닫는다. 다음 엔터가 다시 연다. WASD 로 걷던 손이 그대로
        // 이어지도록, 보낸 뒤에도 켜 두지 않는다.
        Unfocus();

        Submitted?.Invoke(text, global);
    }
}
