using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 매칭 화면을 그린다.
    ///
    /// 규칙은 <see cref="MatchFlow"/> 가 안다. 이 파일은 그 판단을 글자와 색으로 옮기기만 한다.
    /// 어느 게임인지 if 로 가르지 않는다 — 문구·슬롯 수·시작 조건은 전부 <see cref="MiniGameConfig"/>
    /// 가 만들어 준 값을 그대로 쓴다.
    ///
    /// 화면에 적는 글은 최소한이다. 인원 숫자·부제는 적지 않는다 — 몇 명이 왔는지는 카드가
    /// 이미 보여 주고, 같은 뜻을 두 번 쓰면 판이 답답해진다.
    ///
    ///     HeaderArea      작게 "광산 미니게임"  +  크게 "플레이어를 매칭 중입니다" + 튀는 점 셋
    ///     StatusArea      매칭 중 "30초 후 자동 시작" 또는 (배, 인원 부족) "4명이 모두 모여야…" 한 줄
    ///                     →  카운트다운이면 큰 숫자로 교체
    ///     PlayerSlotArea  파티원 카드 — 두 상태에서 같은 Y
    ///     ActionArea      [게임 시작] [매칭 취소]
    ///
    /// 카운트다운은 사람이 [게임 시작] 을 눌러야만 시작한다. 그때 바뀌는 것은 큰 제목의 문구와
    /// StatusArea 의 숫자, 버튼 줄의 표시 여부뿐이고, 파티원 줄은 크기가 고정된 별도 컨테이너라
    /// 자리를 지킨다. 상태 → 화면 On/Off 는 <see cref="OnStateChanged"/> 한 곳에서만 정한다.
    /// </summary>
    public sealed class MatchPanelPresenter : MonoBehaviour
    {
        [SerializeField] private MatchFlow flow;

        [Header("HeaderArea")]
        [Tooltip("작은 게임 이름. 예: 광산 미니게임")]
        [SerializeField] private TMP_Text gameTitleText;

        [Tooltip("큰 제목. 매칭 중 / 카운트다운에 따라 문구가 바뀐다. 글자 자체는 움직이지 않는다.")]
        [SerializeField] private TMP_Text titleText;

        [Tooltip("제목 뒤에서 튀는 점 세 개. 제목과 별도 오브젝트라 제목 글자는 흔들리지 않는다.")]
        [SerializeField] private TMP_Text[] dotTexts;

        [Header("StatusArea — 매칭 중")]
        [Tooltip("자동 시작 안내를 담는 컨테이너. 카운트다운이 시작되면 꺼진다.")]
        [SerializeField] private GameObject matchStatus;
        [Tooltip("제목 아래 상태 한 줄. 시계가 돌면 \"30초 후 자동 시작\", 인원이 모자라 못 시작하면(배) 그 이유.")]
        [SerializeField] private TMP_Text autoStartText;

        [Header("StatusArea — 카운트다운")]
        [Tooltip("큰 숫자를 담는 컨테이너. 매칭 중에는 꺼진다.")]
        [SerializeField] private GameObject countdownStatus;
        [SerializeField] private TMP_Text countdownNumber;

        [Tooltip("숫자가 바뀔 때 살짝 커지며 나타나는 시간(초). 0 이면 연출 없음.")]
        [SerializeField, Min(0f)] private float countdownPopSeconds = .22f;
        [SerializeField, Min(1f)] private float countdownPopScale = 1.25f;

        [Header("PlayerSlotArea")]
        [SerializeField] private MatchSlotView[] slots;

        [Tooltip("자리 사이 간격. 쓰는 자리 수가 달라지면 이 값으로 다시 가운데 정렬한다.")]
        [SerializeField] private float slotGap = 34f;

        [Header("ActionArea")]
        [Tooltip("버튼 줄. 숨겨져도 파티원 줄이 움직이지 않도록 따로 둔다.")]
        [SerializeField] private GameObject actionArea;

        [Tooltip("(예전 자리) 버튼 위 안내 글. 이제 이유는 제목 아래 statusText 에 적으므로 비워 둔다.")]
        [SerializeField] private TMP_Text startHintText;
        [SerializeField] private Button startButton;
        [SerializeField] private TMP_Text startButtonLabel;
        [SerializeField] private Button cancelButton;

        [Header("문구")]
        [SerializeField] private string matchingTitle = "플레이어를 매칭 중입니다";
        [SerializeField] private string countdownTitle = "게임이 곧 시작됩니다";
        [SerializeField] private string startingNumber = "시작!";

        [Header("기다리는 점 (매칭 중 제목 뒤 ...)")]
        [Tooltip("점이 한 번 튀어 오르는 빠르기(rad/s).")]
        [SerializeField, Min(0f)] private float dotBounceSpeed = 6.5f;
        [Tooltip("점이 튀는 높이(px). 0 이면 점이 멈춘다.")]
        [SerializeField, Min(0f)] private float dotBounceHeight = 14f;
        [Tooltip("점 사이 위상 차(rad). 차례로 튀게 한다.")]
        [SerializeField] private float dotPhaseGap = .9f;
        [Tooltip("제목 끝에서 첫 점까지의 간격(px).")]
        [SerializeField] private float dotGapFromTitle = 6f;
        [Tooltip("점과 점 사이 간격(px, 중심 기준).")]
        [SerializeField] private float dotSpacing = 20f;

        [Header("색")]
        [SerializeField] private Color titleMatching = new(1f, 1f, 1f, 1f);
        [SerializeField] private Color titleCountdown = new(1f, .82f, .35f, 1f);
        [SerializeField] private Color startLabelOn = new(1f, .82f, .35f, 1f);
        [SerializeField] private Color startLabelOff = new(.62f, .68f, .78f, .8f);

        private float popRemaining;
        private float dotBaseX;
        private float dotBaseY;
        private bool dotBaseYCaptured;
        private bool dotBaseValid;

        private void OnEnable()
        {
            if (flow == null) return;

            flow.Refreshed += Redraw;
            flow.StateChanged += OnStateChanged;
            flow.CountdownChanged += OnCountdownChanged;

            if (startButton != null) startButton.onClick.AddListener(flow.RequestStart);
            if (cancelButton != null) cancelButton.onClick.AddListener(flow.CancelMatch);

            // 껐다 다시 켜졌을 때를 위해 한 번 그려 둔다. Start 는 한 번만 돈다.
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
            TickWaitingDots();
            WriteAutoStart();
        }

        // ------------------------------------------------------------
        // 상태 → 화면. 여기 한 곳에서만 켜고 끈다.
        // ------------------------------------------------------------

        private void OnStateChanged(MatchState state)
        {
            bool counting = state == MatchState.Countdown;
            bool starting = state == MatchState.Starting || state == MatchState.InGame;
            bool matching = !counting && !starting;

            // 바뀌는 것은 StatusArea 안쪽과 버튼 줄의 표시 여부뿐. 파티원 줄은 건드리지 않는다.
            if (matchStatus != null) matchStatus.SetActive(matching);
            if (countdownStatus != null) countdownStatus.SetActive(!matching);
            if (actionArea != null) actionArea.SetActive(matching);

            if (titleText != null)
            {
                titleText.text = matching ? matchingTitle : countdownTitle;
                titleText.color = matching ? titleMatching : titleCountdown;
                dotBaseValid = false; // 제목 폭이 바뀌었으니 점 자리를 다시 잰다
            }

            SetDotsVisible(matching);

            if (counting && countdownNumber != null && flow != null)
                countdownNumber.text = Mathf.Max(1, flow.CountdownRemaining).ToString();

            if (starting && countdownNumber != null)
            {
                countdownNumber.text = startingNumber;
                popRemaining = countdownPopSeconds;
            }

            // 카운트다운에서는 빈 자리를 한 단 더 어둡게 — 이번 판에 없는 자리라는 뜻.
            Redraw();
        }

        private void OnCountdownChanged(int remaining)
        {
            if (countdownNumber == null) return;

            // 0 은 화면에 안 보여 준다. 1 다음은 "시작!" 이다 (Starting 상태가 쓴다).
            if (remaining <= 0) return;

            countdownNumber.text = remaining.ToString();
            popRemaining = countdownPopSeconds;
        }

        /// <summary>
        /// 매칭 중 제목 뒤의 점 세 개가 차례로 톡톡 튄다 — "기다리는 중" 이라는 느낌만 준다.
        /// 점은 제목과 별개의 오브젝트라 제목 글자는 한 픽셀도 움직이지 않는다. 처음 한 번
        /// 제목의 실제 폭을 재서 그 오른쪽에 점을 세우고, 그 뒤로는 y 만 바꾼다.
        /// </summary>
        private void TickWaitingDots()
        {
            if (dotTexts == null || dotTexts.Length == 0 || titleText == null || flow == null) return;
            if (flow.State != MatchState.Matching && flow.State != MatchState.Idle) return;

            if (!dotBaseValid)
            {
                RectTransform titleRt = titleText.rectTransform;
                float width = titleText.GetPreferredValues(titleText.text).x;
                // 제목은 가운데 정렬이므로 글자의 오른쪽 끝은 제목 중심 + 폭/2 다.
                dotBaseX = titleRt.anchoredPosition.x + width * .5f + dotGapFromTitle;
                if (!dotBaseYCaptured && dotTexts[0] != null)
                {
                    dotBaseY = ((RectTransform)dotTexts[0].transform).anchoredPosition.y;
                    dotBaseYCaptured = true;
                }
                dotBaseValid = true;
            }

            float t = Time.unscaledTime * dotBounceSpeed;
            for (int i = 0; i < dotTexts.Length; i++)
            {
                if (dotTexts[i] == null) continue;

                // 0 아래로는 내려가지 않게 — 점이 밑줄처럼 처지면 대기가 아니라 고장처럼 보인다.
                float lift = Mathf.Max(0f, Mathf.Sin(t - i * dotPhaseGap)) * dotBounceHeight;
                var rt = (RectTransform)dotTexts[i].transform;
                rt.anchoredPosition = new Vector2(dotBaseX + dotSpacing * (i + .5f), dotBaseY + lift);
            }
        }

        private void SetDotsVisible(bool visible)
        {
            if (dotTexts == null) return;
            foreach (TMP_Text dot in dotTexts)
                if (dot != null && dot.gameObject.activeSelf != visible) dot.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 제목 아래 상태 한 줄. 시계가 돌면 "30초 후 자동 시작", 시계가 돌지 않는 이유가 있으면(배에서
        /// 4명이 안 찼을 때) 그 이유를 같은 자리에 적는다. 카운트다운이 시작되면 이 자리에 큰 숫자가
        /// 들어오면서 사라진다. 매 프레임 줄어드는 값이라 여기서 쓴다.
        /// </summary>
        private void WriteAutoStart()
        {
            if (autoStartText == null || flow == null) return;
            if (flow.State != MatchState.Matching) return;

            float remaining = flow.AutoStartRemaining;
            string next = remaining > 0f
                ? $"{Mathf.CeilToInt(remaining)}초 후 자동 시작"
                : flow.Config != null ? flow.Config.StartBlockedHint(PlayerRoster.ActivePlayerCount) : string.Empty;
            if (autoStartText.text != next) autoStartText.text = next;
        }

        /// <summary>숫자가 바뀔 때만 살짝 커진 채 흐리게 나타나 제자리로. 그 이상은 하지 않는다.</summary>
        private void TickCountdownPop()
        {
            if (countdownNumber == null || countdownPopSeconds <= 0f) return;

            if (popRemaining <= 0f)
            {
                if (countdownNumber.transform.localScale != Vector3.one)
                {
                    countdownNumber.transform.localScale = Vector3.one;
                    countdownNumber.alpha = 1f;
                }
                return;
            }

            popRemaining -= Time.deltaTime;
            float t = Mathf.Clamp01(popRemaining / countdownPopSeconds); // 1 → 0
            countdownNumber.transform.localScale = Vector3.one * Mathf.Lerp(1f, countdownPopScale, t);
            countdownNumber.alpha = Mathf.Lerp(1f, .25f, t);
        }

        // ------------------------------------------------------------
        // 내용
        // ------------------------------------------------------------

        private void Redraw()
        {
            if (flow == null) return;
            MiniGameConfig config = flow.Config;
            if (config == null) return;

            if (gameTitleText != null) gameTitleText.text = config.DisplayName;

            bool matching = flow.State == MatchState.Matching || flow.State == MatchState.Idle;
            WriteAutoStart();

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
                else slot.ShowEmpty(dimmed: !matching);
            }

            CentreSlots(config.MaxPlayers);

            int count = PlayerRoster.ActivePlayerCount;

            if (startButton != null)
            {
                startButton.gameObject.SetActive(config.ShowsStartButton);
                startButton.interactable = flow.CanStartMatch();
            }

            if (startButtonLabel != null)
            {
                startButtonLabel.text = "게임 시작";
                // 글자도 같이 죽여야 잠긴 버튼으로 보인다. 판 그림만 어두워지면 글자가 떠 보인다.
                startButtonLabel.color = flow.CanStartMatch() ? startLabelOn : startLabelOff;
            }

            // 못 누르는 이유는 제목 아래 한 줄(WriteAutoStart)이 맡는다. 버튼 위 자리는 비워 둔다.
            if (startHintText != null && startHintText.text.Length > 0) startHintText.text = string.Empty;
        }

        /// <summary>
        /// 쓰는 자리만 가운데로 모은다. 네 자리 기준으로 박아 둔 좌표를 그대로 두면
        /// 검(두 자리)에서 왼쪽으로 쏠려 보인다. Y 는 건드리지 않는다.
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
