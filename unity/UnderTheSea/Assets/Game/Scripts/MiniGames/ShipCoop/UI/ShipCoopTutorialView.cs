using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 게임에 들어오면 한 번 뜨는 설명 팝업.
///
/// **키는 적지 않습니다.** 자리에 가까이 가면 오른쪽 아래에 키캡과 할 일이 뜨므로
/// (<see cref="ShipCoopHud"/>) 여기서 또 적으면 중복이고, 읽을 때쯤엔 어차피 잊습니다.
/// 여기는 **무엇을 해야 하는 게임인지**만 말합니다.
///
/// 사용법
///   1. ShipCoopTutorial 프리팹을 씬에 올린다.
///   2. 끝. 씬에 ShipCoopGame 이 하나뿐이면 자동으로 찾는다.
///
/// ⚠ 이 팝업은 **각자의 화면**입니다. 서버 상태가 아닙니다.
///    4명이 각자 닫으므로, 한 명이 읽는 동안 배가 기다리지는 않습니다.
///    기다리게 하려면 "넷 다 확인했는가" 를 서버가 세야 하는데, 그건 이 컴포넌트의 일이
///    아닙니다. 초반 몇 초는 여유가 있으니 그대로 둬도 됩니다.
/// </summary>
public class ShipCoopTutorialView : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다. 씬에 두 개 이상 있을 때만 직접 지정한다.")]
    [SerializeField] private ShipCoopGame game;

    [Header("화면")]
    [Tooltip("설명 패널. 닫으면 꺼진다.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private TextMeshProUGUI bodyLabel;

    [Tooltip("닫는 방법을 알려주는 작은 글씨. 없어도 동작한다.")]
    [SerializeField] private TextMeshProUGUI footerLabel;

    [Header("문구")]
    [TextArea(8, 20)]
    [SerializeField]
    private string body =
        "넷이서 배 한 척을 몹니다. 3분 안에 목적지까지.\n" +
        "\n" +
        "체력은 넷이 나눠 쓰는 하나뿐입니다.\n" +
        "가라앉아도 끝이고, 늦어도 끝입니다.\n" +
        "\n" +
        "   돛을 펴야 배가 나아가고\n" +
        "   조타를 잡아야 암초를 피하고\n" +
        "   대포로 다가오는 적선을 막고\n" +
        "   구멍은 판자로 막고, 차오른 물은 퍼냅니다\n" +
        "\n" +
        "수리와 대포, 배수는 상자에서 필요한 물건을 날라 와야 합니다.\n" +
        "무언가를 들고 있는 동안에는 다른 일을 못 합니다.\n" +
        "\n" +
        "맡은 자리는 없습니다. 비어 있는 자리를 서로 메우세요.";

    [SerializeField] private string footer = "잠시 뒤 저절로 닫힙니다   ·   Enter 로 바로 닫기";

    // ------------------------------------------------------------
    // 읽을 시간을 준 뒤 저절로 닫는다.
    //
    // 본문이 220자쯤 된다. 처음 보는 설명을 훑는 속도를 분당 400자로 잡으면 33초,
    // 빠르게 넘기면 20초쯤이다. 그런데 한 판이 **3분뿐**이라 오래 띄워두면 그만큼
    // 갑판이 비어 있다. 그래서 훑기에 맞춘 18초로 두고, 다 읽은 사람은 Enter 로
    // 먼저 닫게 했다.
    //
    // 길고 짧은 것은 사람마다 다르니 인스펙터에서 맞춘다. 0 이면 저절로 닫히지 않는다.
    // ------------------------------------------------------------

    [Header("닫는 방법")]
    [Tooltip("이 시간이 지나면 저절로 닫힌다. 0 이면 사람이 닫을 때까지 떠 있는다.")]
    [SerializeField, Min(0f)] private float autoHideSeconds = 18f;

    /// <summary>
    /// ⚠ 닫는 키에 Space 를 넣지 않습니다. Space 는 상호작용이라, 팝업을 닫은 그 누름이
    ///    그대로 집기나 붙기까지 해버립니다. 게임이 안 쓰는 키만 씁니다.
    /// </summary>
    private float _shownAt;

    /// <summary>설명이 지금 떠 있는지</summary>
    public bool IsShowing => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        }

        if (bodyLabel != null)
        {
            bodyLabel.text = body;
        }

        if (footerLabel != null)
        {
            footerLabel.text = footer;
        }

        Show();
    }

    private void OnEnable()
    {
        if (game != null)
        {
            // 결과 화면과 겹치지 않게 한다. 끝났는데 설명이 떠 있으면 점수를 가린다.
            game.Finished += HandleFinished;
        }
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.Finished -= HandleFinished;
        }
    }

    private void Update()
    {
        if (!IsShowing)
        {
            return;
        }

        if (autoHideSeconds > 0f && Time.time - _shownAt >= autoHideSeconds)
        {
            Hide();
            return;
        }

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null)
        {
            return;
        }

        if (keyboard.enterKey.wasPressedThisFrame
            || keyboard.numpadEnterKey.wasPressedThisFrame
            || keyboard.escapeKey.wasPressedThisFrame)
        {
            Hide();
        }
    }

    /// <summary>설명을 띄운다.</summary>
    public void Show()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }

        _shownAt = Time.time;
    }

    /// <summary>설명을 닫는다. 버튼에 연결해도 된다.</summary>
    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    private void HandleFinished(bool cleared, int score)
    {
        Hide();
    }
}
