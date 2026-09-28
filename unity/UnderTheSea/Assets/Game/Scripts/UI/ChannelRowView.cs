using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채널 목록의 한 줄.
///
/// 스스로 판단하지 않고, ChannelSelectController 가 넘겨준 값을 그리기만 한다.
/// 클릭되면 Clicked 로 알리고, 실제 선택 처리는 컨트롤러가 한다.
/// </summary>
public class ChannelRowView : MonoBehaviour
{
    [Header("배경")]
    [SerializeField] private Image background;

    [Tooltip("선택되지 않았을 때의 줄 이미지")]
    [SerializeField] private Sprite normalSprite;

    [Tooltip("선택되었을 때의 줄 이미지")]
    [SerializeField] private Sprite selectedSprite;

    [Tooltip("선택된 줄의 색. 보통 흰색 = 원본 그대로.")]
    [SerializeField] private Color selectedColor = Color.white;

    [Tooltip("선택되지 않은 줄의 색. 어둡게 깔아 구분한다.")]
    [SerializeField] private Color unselectedColor = new Color(0.70f, 0.70f, 0.72f, 1f);

    [Tooltip("선택되었을 때만 켜지는 빛 효과. 없어도 동작한다.")]
    [SerializeField] private Image selectedOverlay;

    [Tooltip("선택되었을 때만 켜지는 오른쪽 나침반")]
    [SerializeField] private Image selectedCompass;

    [Header("내용")]
    [SerializeField] private TextMeshProUGUI nameLabel;
    [SerializeField] private TextMeshProUGUI countLabel;

    [Header("상태 배지")]
    [SerializeField] private Image statusBadge;
    [SerializeField] private TextMeshProUGUI statusLabel;

    [Tooltip("여유로울 때 쓸 배지 이미지")]
    [SerializeField] private Sprite smoothSprite;

    [Tooltip("붐빌 때 쓸 배지 이미지")]
    [SerializeField] private Sprite crowdedSprite;

    [Header("배지 문구")]
    [SerializeField] private string smoothText = "원활";
    [SerializeField] private string crowdedText = "혼잡";
    [SerializeField] private string fullText = "만원";

    /// <summary>세션 목록이 아직 안 왔을 때. 모르면서 "원활" 이라고 하면 안 된다.</summary>
    [SerializeField] private string unknownText = "확인 중";

    [Header("클릭")]
    [SerializeField] private Button button;

    /// <summary>이 줄이 클릭되었다. 인자는 채널 목록에서의 순번.</summary>
    public event System.Action<int> Clicked;

    /// <summary>이 줄이 보여주고 있는 채널의 서버 ID. 비어 있으면 빈 줄이다.</summary>
    public string ServerId { get; private set; }

    /// <summary>이 줄에 표시할 채널이 있는지</summary>
    public bool HasChannel => !string.IsNullOrEmpty(ServerId);

    /// <summary>이 줄이 채널 목록에서 몇 번째인지</summary>
    public int Index { get; private set; } = -1;

    private void OnEnable()
    {
        if (button != null) button.onClick.AddListener(RaiseClicked);
    }

    private void OnDisable()
    {
        if (button != null) button.onClick.RemoveListener(RaiseClicked);
    }

    private void RaiseClicked()
    {
        if (HasChannel) Clicked?.Invoke(Index);
    }

    /// <summary>채널 하나를 이 줄에 표시한다.</summary>
    /// <param name="crowdedRatio">이 비율 이상 차면 "혼잡" 으로 본다.</param>
    public void Bind(int index, ServerInfo info, float crowdedRatio)
    {
        Index = index;
        ServerId = info.Id;
        gameObject.SetActive(true);

        if (nameLabel != null) nameLabel.text = info.DisplayName;

        // ⚠ **인원을 아직 모르는 상태와 0명을 구별한다.** 세션 목록은 붙고 나서 1~2초 뒤에
        //    온다. 그동안 "0 / 100" 으로 두면 서버가 죽은 것처럼 보이고, 잠시 뒤 숫자가
        //    튀어 오른다. 모르는 동안은 "- / 100" 으로 두는 편이 정직하다.
        bool known = info.CurrentPlayers >= 0;

        if (countLabel != null)
        {
            countLabel.text = known
                ? $"{info.CurrentPlayers} / {info.MaxPlayers}"
                : $"- / {info.MaxPlayers}";
        }

        // 모르는 동안은 혼잡하지 않은 것으로 둔다. 모른다고 "혼잡" 을 띄우면 안 된다.
        float ratio = known && info.MaxPlayers > 0
            ? (float)info.CurrentPlayers / info.MaxPlayers
            : 0f;

        bool crowded = known && (info.IsFull || ratio >= crowdedRatio);

        if (statusBadge != null && smoothSprite != null && crowdedSprite != null)
        {
            statusBadge.sprite = crowded ? crowdedSprite : smoothSprite;
        }

        if (statusLabel != null)
        {
            statusLabel.text = !known ? unknownText
                : info.IsFull ? fullText
                : crowded ? crowdedText
                : smoothText;
        }

        // 가득 찬 채널은 고를 수 없다.
        if (button != null) button.interactable = !info.IsFull;
    }

    /// <summary>표시할 채널이 없을 때. 줄 자체를 숨긴다.</summary>
    public void Clear()
    {
        Index = -1;
        ServerId = null;
        gameObject.SetActive(false);
    }

    /// <summary>선택 상태에 맞게 겉모습을 바꾼다.</summary>
    public void SetSelected(bool selected)
    {
        if (background != null)
        {
            if (normalSprite != null && selectedSprite != null)
            {
                background.sprite = selected ? selectedSprite : normalSprite;
            }

            // Both states share one sprite, so the difference is carried by tint.
            background.color = selected ? selectedColor : unselectedColor;
        }

        if (selectedOverlay != null) selectedOverlay.enabled = selected;
        if (selectedCompass != null) selectedCompass.enabled = selected;
    }
}
