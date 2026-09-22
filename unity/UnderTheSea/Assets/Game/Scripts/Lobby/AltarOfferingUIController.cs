using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnderTheSea.Account;
using UnderTheSea.Inventory;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 정식 제단 봉헌 UI.
    ///
    /// <code>
    ///   바다의 심장 봉헌
    ///
    ///   누적 봉헌량      999 / 1000
    ///   보유 조각                  5
    ///
    ///   봉헌 수량
    ///   [-] [1] [+] [MAX]
    ///
    ///   [봉헌] [닫기]
    ///
    ///   상태 메시지
    /// </code>
    ///
    /// <b>이 클래스가 하는 일은 넷뿐이다.</b> 화면에 값을 그리고, 버튼 입력을 받고,
    /// 고른 수량을 들고 있고, 서비스에 봉헌을 부탁한다.
    ///
    /// <b>하지 않는 일</b> — HTTP 호출 · JSON 파싱 · DB · Fusion RPC · Blue VFX ·
    /// 서버 상태 직접 가감 · 보상. 전부 다른 계층의 몫이다.
    ///
    /// ⚠ <b>서버가 준 숫자를 로컬에서 더하거나 빼지 않는다.</b> 봉헌이 성공하면
    ///    서비스가 응답값을 <see cref="AltarState"/> · <see cref="PlayerInventory"/> 에 넣고,
    ///    이 화면은 그 결과를 다시 읽어 그린다.
    ///
    /// <code>
    ///   서버 → 서비스 → AltarState / PlayerInventory → UI
    /// </code>
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 6장 · 9.2절
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltarOfferingUIController : MonoBehaviour
    {
        [Header("패널")]
        [Tooltip("열고 닫을 대상. 비워 두면 이 오브젝트를 쓴다.")]
        [SerializeField] private GameObject panelRoot;

        [Header("동적 숫자")]
        [SerializeField] private TMP_Text totalOfferedValueText;
        [SerializeField] private TMP_Text ownedFragmentsValueText;
        [SerializeField] private TMP_Text selectedAmountText;
        [SerializeField] private TMP_Text messageText;

        [Header("버튼")]
        [SerializeField] private Button minusButton;
        [SerializeField] private Button plusButton;
        [SerializeField] private Button maxButton;
        [SerializeField] private Button offerButton;
        [SerializeField] private Button closeButton;

        [Header("상태 메시지")]
        [SerializeField, Min(0.5f)]
        [Tooltip("봉헌 결과 문구를 몇 초 동안 띄울지. 읽기 어려우면 늘린다.")]
        private float messageDisplaySeconds = 3f;

        /// <summary>지금 고른 수량. <b>UI 로컬 상태</b>이고 서버 권위값이 아니다.</summary>
        private long selectedAmount;

        /// <summary>봉헌 요청이 도는 중인가. 도는 동안 모든 버튼을 잠근다.</summary>
        private bool offerInFlight;

        /// <summary>떠 있는 문구를 지우려고 걸어 둔 예약. 없으면 <c>null</c>.</summary>
        private Coroutine messageClearRoutine;

        /// <summary>
        /// 창이 닫혔다. 어떤 길로 닫혔든(버튼 · Esc · 범위 이탈) 한 번 오른다.
        ///
        /// 설치기가 이것을 듣고 "[E] 조각 봉헌" 안내를 다시 띄울지 정한다.
        /// </summary>
        public event Action Closed;

        /// <summary>패널이 열려 있는가.</summary>
        public bool IsOpen => Target != null && Target.activeSelf;

        private GameObject Target => panelRoot != null ? panelRoot : gameObject;

        // ------------------------------------------------------------
        // 생명주기
        // ------------------------------------------------------------

        private void Awake()
        {
            if (minusButton != null) minusButton.onClick.AddListener(OnMinus);
            if (plusButton != null) plusButton.onClick.AddListener(OnPlus);
            if (maxButton != null) maxButton.onClick.AddListener(OnMax);
            if (offerButton != null) offerButton.onClick.AddListener(OnOffer);
            if (closeButton != null) closeButton.onClick.AddListener(Close);

            Target.SetActive(false);
        }

        private void OnEnable()
        {
            // ⚠ 같은 구독이 쌓이지 않게 떼고 붙인다.
            AltarState.Changed -= Render;
            AltarState.Changed += Render;

            PlayerInventory.Changed -= Render;
            PlayerInventory.Changed += Render;

            Render();
        }

        private void OnDisable()
        {
            AltarState.Changed -= Render;
            PlayerInventory.Changed -= Render;

            // 오브젝트가 꺼지면 코루틴도 함께 죽는다. 남은 손잡이만 버린다.
            CancelMessageClear();

            // 화면이 꺼지면서 포커스를 쥔 채 사라지면 영영 못 움직인다.
            ReleaseFocus();
        }

        private void OnDestroy()
        {
            ReleaseFocus();
        }

        // ------------------------------------------------------------
        // 열기 · 닫기
        // ------------------------------------------------------------

        /// <summary>
        /// 봉헌 창을 연다.
        ///
        /// 열면서 서버에 현재 값을 한 번 물어본다. 다른 사람이 그사이 봉헌했을 수 있다.
        /// ⚠ 서비스를 직접 부르지 않고 캐시의 입구를 쓴다. 그래야 중복 요청 합치기와
        ///    봉헌 중 차단이 그대로 걸린다. (설계 12.7절)
        ///
        /// ⚠ <b>인벤토리는 따로 묻지 않는다.</b> 제단 응답에 myFragments 가 함께 오고,
        ///    <see cref="AltarState.ApplyStateSnapshot"/> 이 <see cref="PlayerInventory"/> 까지
        ///    같이 갱신한다. 둘을 동시에 내면 공유 순번을 나눠 갖는데(제단이 먼저, 인벤토리가
        ///    나중), 인벤토리 응답이 먼저 도착하면 제단 응답이 옛것으로 판정돼 통째로 버려진다.
        ///    그러면 MaxOfferAmount 가 0 으로 남아 수량을 하나도 고를 수 없다.
        /// </summary>
        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            Target.SetActive(true);

            SetMessage(string.Empty);
            ResetSelectedAmount();

            AltarState.RequestRefresh();

            AcquireFocus();
            Render();
        }

        /// <summary>
        /// 봉헌 창을 닫는다.
        ///
        /// <b>닫는 길은 이 메서드 하나뿐이다.</b> [닫기] 버튼 · Esc · 범위 이탈이 모두
        /// 여기로 들어온다. 길이 갈리면 어느 한쪽에서 포커스를 안 돌려주는 사고가 난다.
        /// </summary>
        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            ReleaseFocus();

            SetMessage(string.Empty);
            Target.SetActive(false);

            Closed?.Invoke();
        }

        /// <summary>
        /// Esc 로 닫는다.
        ///
        /// ⚠ <b>나 말고 다른 화면이 입력을 잠그고 있으면 닫지 않는다.</b> 채팅칸이 켜져 있으면
        ///    그 Esc 는 채팅을 끄는 것이 먼저다. (설계 6.4절)
        ///
        /// ⚠ 여기서 <c>ChatFocus.Typing</c> 을 보면 안 된다. 창이 열려 있는 동안 나 자신이
        ///    보유자라 그 값은 언제나 참이고, 그러면 <b>Esc 로 영영 닫히지 않는다.</b>
        ///    그래서 <see cref="ChatFocus.HeldByOther"/> 를 쓴다. (설계 6.4.1절)
        /// </summary>
        public void CloseFromEscape()
        {
            if (!IsOpen || ChatFocus.HeldByOther(this))
            {
                return;
            }

            Close();
        }

        // ------------------------------------------------------------
        // 수량
        // ------------------------------------------------------------

        private void OnMinus()
        {
            if (offerInFlight || selectedAmount <= 1)
            {
                return;
            }

            selectedAmount--;
            Render();
        }

        private void OnPlus()
        {
            if (offerInFlight || selectedAmount >= AltarState.MaxOfferAmount)
            {
                return;
            }

            selectedAmount++;
            Render();
        }

        /// <summary>
        /// 고를 수 있는 최대치로 올린다.
        ///
        /// ⚠ <c>min(보유량, 남은 칸)</c> 을 여기서 다시 계산하지 않는다. 그러면 같은 공식이
        ///    서버와 UI 두 군데에 생긴다. 서버가 준 값을 그대로 쓴다. (설계 11.1절)
        /// </summary>
        private void OnMax()
        {
            if (offerInFlight)
            {
                return;
            }

            selectedAmount = AltarState.MaxOfferAmount;
            Render();
        }

        private void ResetSelectedAmount()
        {
            selectedAmount = AltarState.MaxOfferAmount > 0 ? 1 : 0;
        }

        // ------------------------------------------------------------
        // 봉헌
        // ------------------------------------------------------------

        /// <summary>
        /// 봉헌을 요청한다.
        ///
        /// ⚠ <b>requestId 는 여기서 만든다.</b> 버튼을 누른 이 순간이 논리적 봉헌 하나의
        ///    시작이기 때문이다. 서비스가 스스로 만들면 재시도 때 값이 바뀌어 서버의
        ///    중복 방지가 무력해진다. (설계 10.4 · 10.5절)
        /// </summary>
        private void OnOffer()
        {
            if (!CanOffer())
            {
                return;
            }

            IAltarService service = AccountServiceLocator.Altar;
            if (service == null)
            {
                SetMessage("제단 서비스를 사용할 수 없습니다.");
                return;
            }

            string requestId = Guid.NewGuid().ToString();

            offerInFlight = true;
            SetMessage("봉헌하는 중...", false);
            Render();

            service.OnOfferResult -= OnOfferResult;
            service.OnOfferResult += OnOfferResult;

            service.Offer(selectedAmount, requestId);
        }

        /// <summary>
        /// 봉헌 결과를 받는다.
        ///
        /// ⚠ 여기서 JSON 을 다시 파싱하지 않고, 실패 뒤에 상태를 다시 조회하지도 않는다.
        ///    서비스가 이미 응답 본문의 최신 상태를 캐시에 넣어 두었다. (설계 9.2절)
        ///
        /// ⚠ <c>duplicate == true</c> 는 오류가 아니다. 같은 요청이 이미 처리됐다는 뜻이고
        ///    성공으로 다룬다. 이번 STEP 에서는 이 값으로 연출을 판단하지 않는다.
        /// </summary>
        private void OnOfferResult(AltarOfferOutcome outcome)
        {
            IAltarService service = AccountServiceLocator.Altar;
            if (service != null)
            {
                service.OnOfferResult -= OnOfferResult;
            }

            offerInFlight = false;

            if (outcome.Success)
            {
                SetMessage(outcome.Duplicate
                    ? "이미 반영된 봉헌입니다."
                    : "봉헌이 완료되었습니다.");
            }
            else
            {
                // 서버가 준 한국어 문구를 그대로 띄운다. 없으면 코드라도 보여 준다.
                SetMessage(string.IsNullOrEmpty(outcome.Message)
                    ? $"봉헌하지 못했습니다. ({outcome.Code})"
                    : outcome.Message);
            }

            // 캐시는 서비스가 이미 갱신했다. 그 값을 다시 읽어 그린다.
            ResetSelectedAmount();
            Render();
        }

        private bool CanOffer()
        {
            return !offerInFlight
                && selectedAmount > 0
                && AltarState.MaxOfferAmount > 0
                && !AltarState.AltarActivated;
        }

        // ------------------------------------------------------------
        // 그리기
        // ------------------------------------------------------------

        /// <summary>
        /// 캐시의 현재 값으로 화면을 다시 그린다.
        ///
        /// <see cref="AltarState.Changed"/> · <see cref="PlayerInventory.Changed"/> 로도 불린다.
        /// 다른 사람이 봉헌해 남은 칸이 줄면 내가 고른 수량도 함께 내려간다.
        /// </summary>
        private void Render()
        {
            long max = AltarState.MaxOfferAmount;

            // 내가 창을 보고 있는 사이에 남은 칸이 줄 수 있다. 고른 값을 그 안으로 끌어내린다.
            if (selectedAmount > max)
            {
                selectedAmount = max;
            }
            if (selectedAmount < 1 && max > 0)
            {
                selectedAmount = 1;
            }
            if (max <= 0)
            {
                selectedAmount = 0;
            }

            if (totalOfferedValueText != null)
            {
                totalOfferedValueText.text = $"{AltarState.TotalOffered} / {AltarState.TargetOffering}";
            }

            if (ownedFragmentsValueText != null)
            {
                ownedFragmentsValueText.text = PlayerInventory.SeaHeartFragment.ToString();
            }

            if (selectedAmountText != null)
            {
                selectedAmountText.text = selectedAmount.ToString();
            }

            SetInteractable(minusButton, !offerInFlight && selectedAmount > 1);
            SetInteractable(plusButton, !offerInFlight && selectedAmount < max);
            SetInteractable(maxButton, !offerInFlight && max > 0 && selectedAmount != max);
            SetInteractable(offerButton, CanOffer());
            SetInteractable(closeButton, !offerInFlight);
        }

        private static void SetInteractable(Button button, bool on)
        {
            if (button != null && button.interactable != on)
            {
                button.interactable = on;
            }
        }

        /// <summary>
        /// 상태 문구를 띄운다. <b>메시지가 지워지는 길은 여기 하나뿐이다.</b>
        ///
        /// <code>
        ///   SetMessage("봉헌이 완료되었습니다.")        3초 뒤 저절로 사라진다
        ///   SetMessage("봉헌하는 중...", false)         결과가 덮어쓸 때까지 남는다
        ///   SetMessage(string.Empty)                    지금 지운다 (열기 · 닫기)
        /// </code>
        ///
        /// ⚠ <b>먼저 앞선 예약을 취소한다.</b> 안 그러면 두 번째 문구가 첫 번째 문구의
        ///    타이머에 끌려 일찍 사라진다. 연달아 봉헌하면 바로 걸린다.
        ///
        /// ⚠ 서버 상태가 갱신돼도(<see cref="Render"/>) 문구는 건드리지 않는다.
        ///    상태 표시와 일시적인 알림은 수명이 다르다.
        /// </summary>
        /// <param name="autoClear">
        /// 스스로 사라질지. 응답을 기다리는 동안 띄우는 문구는 <c>false</c> 로 남긴다.
        /// 요청이 <see cref="messageDisplaySeconds"/> 보다 오래 걸릴 때 "봉헌하는 중..."
        /// 이 먼저 사라지면, 버튼은 잠겨 있는데 이유가 화면에 없게 된다.
        /// </param>
        private void SetMessage(string text, bool autoClear = true)
        {
            if (messageText == null)
            {
                return;
            }

            CancelMessageClear();

            messageText.text = text ?? string.Empty;

            if (!autoClear || string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // 꺼진 오브젝트에서는 코루틴이 돌지 않는다. 창이 닫힌 뒤 늦게 도착한 응답이
            // 여기로 들어올 수 있어 막아 둔다. 그 문구는 다음 Open 이 지운다.
            if (isActiveAndEnabled)
            {
                messageClearRoutine = StartCoroutine(ClearMessageAfterDelay());
            }
        }

        private IEnumerator ClearMessageAfterDelay()
        {
            yield return new WaitForSeconds(messageDisplaySeconds);

            if (messageText != null)
            {
                messageText.text = string.Empty;
            }

            messageClearRoutine = null;
        }

        private void CancelMessageClear()
        {
            if (messageClearRoutine == null)
            {
                return;
            }

            StopCoroutine(messageClearRoutine);
            messageClearRoutine = null;
        }

        // ------------------------------------------------------------
        // 이동 잠금
        //
        // 채팅 입력칸이 쓰는 것과 같은 자물쇠를 쓴다. 열려 있는 동안 걸어 나갈 수 없다.
        // ⚠ 돌려주는 것을 빼먹으면 영영 못 움직인다. 닫기 · Esc · 범위 이탈 · Disable ·
        //    Destroy 가 전부 같은 ReleaseFocus 를 지난다.
        // ------------------------------------------------------------

        private bool holdsFocus;

        private void AcquireFocus()
        {
            if (holdsFocus)
            {
                return;
            }

            ChatFocus.Begin(this);
            holdsFocus = true;
        }

        private void ReleaseFocus()
        {
            if (!holdsFocus)
            {
                return;
            }

            ChatFocus.End(this);
            holdsFocus = false;
        }
    }
}
