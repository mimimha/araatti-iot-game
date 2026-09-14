using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 매칭 화면을 그린다.
    ///
    /// 규칙은 <see cref="MatchFlow"/> 가 안다. 이 파일은 그 판단을 글자와 색으로 옮기기만 한다.
    ///
    /// 매칭 중과 카운트다운은 <b>같은 판, 같은 자리</b> 를 쓴다.
    ///
    /// 화면은 세 칸으로 고정돼 있다 — 머리말(Header) · 파티원 줄(SlotArea) · 버튼(ActionArea).
    /// 상태가 바뀔 때 움직이는 것은 머리말 안의 <b>StatusArea 내용뿐</b>이다.
    ///
    ///     매칭 중     StatusArea = "1 / 4명    5초 후 자동 시작"
    ///     카운트다운   StatusArea = 큰 숫자 "5" + "게임이 시작됩니다"
    ///
    /// 파티원 줄은 두 상태에서 <b>같은 Y</b> 에 있는다. 예전에는 큰 숫자 자리를 만들려고 줄을
    /// 아래로 내렸는데, 그러면 다 모이는 순간 카드가 아래로 툭 떨어져서 같은 화면의 상태
    /// 변화가 아니라 다른 화면으로 갈아탄 것처럼 보였다.
    ///
    /// 슬롯 개수는 미니게임 설정의 최대 인원을 따르므로, 검이면 2칸 · 광산과 배면 4칸이
    /// 같은 화면에서 나온다.
    /// </summary>
    public sealed class MatchPanelPresenter : MonoBehaviour
    {
        [SerializeField] private MatchFlow flow;

        [Header("머리말 (두 상태가 같이 쓴다)")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private MatchSlotView[] slots;

        [Header("매칭 중일 때만")]
        [Tooltip("부제 + 인원/자동시작 줄. StatusArea 안에 있다.")]
        [SerializeField] private GameObject matchStatus;

        [Tooltip("버튼 줄. 숨겨져도 파티원 줄이 움직이지 않도록 따로 둔다.")]
        [SerializeField] private GameObject actionArea;

        [SerializeField] private TMP_Text subtitleText;
        [Tooltip("인원만 크게. 예: 3 / 4")]
        [SerializeField] private TMP_Text countText;

        [Tooltip("언제 시작하는지 짧게.")]
        [SerializeField] private TMP_Text hintText;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private Button cancelButton;

        [Header("카운트다운일 때만")]
        [Tooltip("큰 숫자 + 설명. 매칭 상태의 StatusArea 와 같은 자리를 쓴다.")]
        [SerializeField] private GameObject countdownStatus;
        [SerializeField] private TMP_Text countdownNumber;
        [SerializeField] private TMP_Text countdownFooter;

        [Tooltip("숫자가 바뀔 때 살짝 커졌다 돌아오는 시간(초). 0 이면 연출 없음.")]
        [SerializeField, Min(0f)] private float countdownPopSeconds = .18f;
        [SerializeField, Min(1f)] private float countdownPopScale = 1.22f;

        [Tooltip("자리 사이 간격. 쓰는 자리 수가 달라지면 이 값으로 다시 가운데 정렬한다.")]
        [SerializeField] private float slotGap = 34f;

        [Header("색")]
        [SerializeField] private Color titleMatching = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color titleCountdown = new(1f, .82f, .35f, 1f);

        private void OnEnable()
        {
            if (flow == null) return;

            flow.Refreshed += Redraw;
            flow.StateChanged += OnStateChanged;
            flow.CountdownChanged += OnCountdownChanged;

            if (startButton != null) startButton.onClick.AddListener(flow.RequestStart);
            if (cancelButton != null) cancelButton.onClick.AddListener(flow.CancelMatch);

            // 가짜 게임이 끝나고 다시 켜졌을 때를 위해 한 번 그려 둔다. Start 는 한 번만 돈다.
            OnStateChanged(flow.State);
            Redraw();
        }

        private void OnDisable()
        {
            if (flow == null) return;

            flow.Refreshed -= Redraw;
            flow.StateChanged -= OnStateChanged;
            flow.CountdownChanged -= OnCountdownChanged;

            if (startButton != null) startButton.onClick.RemoveListener(flow.RequestStart);
            if (cancelButton != null) cancelButton.onClick.RemoveListener(flow.CancelMatch);
        }

        private void Start()
        {
            OnStateChanged(flow != null ? flow.State : MatchState.Matching);
            Redraw();
        }

        private void Update()
        {
            TickCountdownPop();

            // 자동 시작까지 남은 시간은 매 프레임 줄어들므로 아래 줄만 따로 고쳐 준다.
            if (flow == null || flow.State != MatchState.Matching) return;

            WriteStatus();
        }

        private void OnStateChanged(MatchState state)
        {
            bool counting = state == MatchState.Countdown;

            // 바뀌는 것은 머리말 안쪽뿐. 파티원 줄은 건드리지 않는다.
            if (matchStatus != null) matchStatus.SetActive(!counting);
            if (countdownStatus != null) countdownStatus.SetActive(counting);
            if (actionArea != null) actionArea.SetActive(!counting);

            if (titleText != null)
            {
                titleText.text = counting ? "모든 파티원이 준비되었습니다!" : "지금 매칭 중입니다";
                titleText.color = counting ? titleCountdown : titleMatching;
            }

            if (counting && countdownFooter != null)
                countdownFooter.text = "게임이 시작됩니다";
        }

        private void OnCountdownChanged(int remaining)
        {
            if (countdownNumber == null) return;

            countdownNumber.text = remaining.ToString();
            popRemaining = countdownPopSeconds;
        }

        private float popRemaining;

        /// <summary>숫자가 바뀔 때만 살짝 커졌다 제자리로. 그 이상은 하지 않는다.</summary>
        private void TickCountdownPop()
        {
            if (countdownNumber == null || countdownPopSeconds <= 0f) return;
            if (popRemaining <= 0f)
            {
                if (countdownNumber.transform.localScale != Vector3.one)
                    countdownNumber.transform.localScale = Vector3.one;
                return;
            }

            popRemaining -= Time.deltaTime;
            float t = Mathf.Clamp01(popRemaining / countdownPopSeconds);
            countdownNumber.transform.localScale = Vector3.one * Mathf.Lerp(1f, countdownPopScale, t);
        }

        private void Redraw()
        {
            if (flow == null) return;
            MiniGameConfig config = flow.Config;
            if (config == null) return;

            if (subtitleText != null)
                subtitleText.text = $"{config.DisplayName} · 다른 플레이어를 찾고 있어요...";

            // 슬롯은 이 게임의 정원만큼만 쓴다. 남는 칸은 아예 감춘다.
            for (int i = 0; i < slots.Length; i++)
            {
                MatchSlotView slot = slots[i];
                if (slot == null) continue;

                bool used = i < config.MaxPlayers;
                slot.gameObject.SetActive(used);
                if (!used) continue;

                PlayerEntry member = PlayerRoster.AtSlot(i);
                if (member != null) slot.ShowMember(member, i + 1);
                else slot.ShowEmpty();
            }

            CentreSlots(config.MaxPlayers);

            bool showStart = config.ShowsStartButton;
            if (startButton != null)
            {
                startButton.gameObject.SetActive(showStart);
                startButton.interactable = flow.CanStartMatch();
            }

            if (startButtonLabel != null) startButtonLabel.text = "게임 시작";
            WriteStatus();
        }

        /// <summary>
        /// 인원은 크게, 언제 시작하는지는 작게. 한 줄에 다 붙이면 길어져서 둘 다 안 읽힌다.
        /// 문장 자체는 설정이 만든다 — 게임마다 다른 말을 여기서 if 로 고르지 않는다.
        /// </summary>
        private void WriteStatus()
        {
            MiniGameConfig config = flow != null ? flow.Config : null;
            if (config == null) return;

            int count = PlayerRoster.ActivePlayerCount;
            if (countText != null) countText.text = config.CountText(count);
            if (hintText != null)
                hintText.text = config.ShortHint(count, flow.AutoStartRemaining, flow.CanStartMatch());
        }

        /// <summary>
        /// 쓰는 자리만 가운데로 모은다. 네 자리 기준으로 박아 둔 좌표를 그대로 두면
        /// 검(두 자리)에서 왼쪽으로 쏠려 보인다.
        /// </summary>
        private void CentreSlots(int used)
        {
            if (slots == null || slots.Length == 0) return;
            used = Mathf.Clamp(used, 1, slots.Length);

            var first = slots[0] != null ? (RectTransform)slots[0].transform : null;
            if (first == null) return;

            float width = first.sizeDelta.x;
            float row = used * width + (used - 1) * slotGap;

            for (int i = 0; i < used; i++)
            {
                if (slots[i] == null) continue;
                var rt = (RectTransform)slots[i].transform;
                float x = -row * .5f + width * .5f + i * (width + slotGap);
                rt.anchoredPosition = new Vector2(x, rt.anchoredPosition.y);
            }
        }

    }
}
