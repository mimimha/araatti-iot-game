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
///   1. ShipCoopHud 프리팹을 씬에 올린다. (팝업은 그 안 Tutorial 에 들어 있다)
///   2. 끝. 씬에 ShipCoopGame 이 하나뿐이면 자동으로 찾는다.
///
/// ⚠ 이 팝업은 **각자의 화면**입니다. 서버 상태가 아닙니다.
///    4명이 각자 닫으므로, 한 명이 읽는 동안 배가 기다리지는 않습니다.
///    기다리게 하려면 "넷 다 확인했는가" 를 서버가 세야 하는데, 그건 이 컴포넌트의 일이
///    아닙니다. 초반 몇 초는 여유가 있으니 그대로 둬도 됩니다.
///
/// ⚠ 게임도 네트워크도 멈추지 않습니다. <c>Time.timeScale</c> 을 건드리지 마세요.
///    각자 닫는 팝업이라 한 사람이 멈추면 그 사람만 뒤처집니다. 남은 시간을
///    <c>Time.unscaledTime</c> 으로 재는 것도 같은 이유입니다 — 다른 무언가가
///    timeScale 을 건드려도 이 팝업의 10초는 벽시계 10초여야 합니다.
/// </summary>
public class ShipCoopTutorialView : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다. 씬에 두 개 이상 있을 때만 직접 지정한다.")]
    [SerializeField] private ShipCoopGame game;

    [Header("화면")]
    [Tooltip("설명 패널. 딤 배경까지 여기 들어 있다. 닫으면 통째로 꺼진다.")]
    [SerializeField] private GameObject panel;

    [Tooltip("옛 글자 본문. 지금은 그림 한 장(gadogado-popup)에 본문이 들어가 있어서 꺼 둔다. "
             + "그림을 안 쓸 때만 다시 켜면 된다.")]
    [SerializeField] private TextMeshProUGUI bodyLabel;

    [Tooltip("그림 아래 왼쪽 빈자리에 남은 시간을 센다. 없어도 동작한다.")]
    [SerializeField] private TextMeshProUGUI footerLabel;

    [Header("문구")]
    [Tooltip("bodyLabel 을 다시 켤 때만 쓰인다. 지금 화면에 보이는 본문은 그림에 그려져 있다.")]
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

    /// <summary>{0} 자리에 남은 초가 들어간다.</summary>
    [Tooltip("{0} 에 남은 초가 들어간다. Enter 로 닫는 방법은 그림에 그려져 있어서 여기 적지 않는다.")]
    [SerializeField] private string footerFormat = "{0}초 후 자동으로 닫힙니다.";

    // ------------------------------------------------------------
    // 읽을 시간을 준 뒤 저절로 닫는다.
    //
    // 본문이 그림이 되면서 훑는 속도가 빨라졌다. 아이콘 다섯 칸이라 글줄을 따라
    // 읽지 않고 한눈에 본다. 그래서 옛 18초를 10초로 줄였다. 한 판이 **3분뿐**이라
    // 팝업이 떠 있는 만큼 갑판이 비어 있다는 사정도 그대로다.
    //
    // 인스펙터에서 맞춘다. 0 이면 저절로 닫히지 않고 카운트다운도 표시하지 않는다.
    // ------------------------------------------------------------

    [Header("닫는 방법")]
    [Tooltip("이 시간이 지나면 저절로 닫힌다. 0 이면 사람이 닫을 때까지 떠 있는다.")]
    [SerializeField, Min(0f)] private float autoHideSeconds = 10f;

    /// <summary>
    /// ⚠ 닫는 키에 Space 를 넣지 않습니다. Space 는 상호작용이라, 팝업을 닫은 그 누름이
    ///    그대로 집기나 붙기까지 해버립니다. 게임이 안 쓰는 키만 씁니다.
    /// </summary>
    private float _shownAt;

    /// <summary>지금 글자로 찍혀 있는 초. 같은 숫자를 매 프레임 다시 만들지 않으려고 둔다.</summary>
    private int _shownSecond = -1;

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

        if (autoHideSeconds > 0f)
        {
            float remaining = autoHideSeconds - (Time.unscaledTime - _shownAt);

            if (remaining <= 0f)
            {
                Hide();
                return;
            }

            DrawRemaining(Mathf.CeilToInt(remaining));
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

    /// <summary>설명을 띄운다. 남은 시간은 여기서 다시 센다.</summary>
    public void Show()
    {
        if (panel != null)
        {
            panel.SetActive(true);
        }

        _shownAt = Time.unscaledTime;
        _shownSecond = -1;

        // 뜨자마자 한 프레임이라도 빈 줄이나 지난 판의 숫자가 보이지 않게 여기서 먼저 찍는다.
        if (autoHideSeconds > 0f)
        {
            DrawRemaining(Mathf.CeilToInt(autoHideSeconds));
        }
        else if (footerLabel != null)
        {
            // 저절로 닫히지 않는다. 닫는 방법은 그림에 그려져 있으니 빈 줄로 둔다.
            footerLabel.text = string.Empty;
        }
    }

    /// <summary>설명을 닫는다. 버튼에 연결해도 된다.</summary>
    public void Hide()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }
    }

    /// <summary>남은 초가 바뀐 프레임에만 글자를 다시 만든다.</summary>
    private void DrawRemaining(int seconds)
    {
        if (seconds == _shownSecond || footerLabel == null)
        {
            return;
        }

        _shownSecond = seconds;
        footerLabel.text = string.Format(footerFormat, seconds);
    }

    private void HandleFinished(bool cleared, int score)
    {
        Hide();
    }
}
