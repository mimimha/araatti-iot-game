using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 섬 회복도 HUD. 화면 위쪽 중앙에서 <see cref="AltarState.RecoveryPercent"/> 를 보여 준다.
    ///
    /// <b>값을 계산하지 않는다.</b> 회복도는 서버가 <c>min(totalOffered / targetOffering, 1) * 100</c>
    /// 으로 계산해서 내려준 것이고, 여기서는 그대로 받아 적는다. 클라이언트가 같은 공식을 또 쓰면
    /// 기획이 바뀔 때 한쪽만 고쳐진다. (설계 13.3절 · 결정 #4)
    ///
    /// ⚠ <c>RewardService.RecoveryRatio</c> 는 <b>섬 회복도가 아니다.</b> 그쪽은 "가진 조각 종류 / 3"
    ///    이고 이름만 닮았다. 여기서 쓰지 않는다. (설계 13.3절)
    ///
    /// <b>지속 상태를 듣는다.</b> <see cref="AltarState.Changed"/> 는 내 봉헌뿐 아니라 남의 봉헌으로
    /// 갱신될 때도 오르므로, 같은 HUD 가 그대로 따라간다.
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 13.3 · 13.4절
    /// </summary>
    public sealed class IslandRecoveryView : MonoBehaviour
    {
        [Header("화면")]
        [SerializeField] private Image recoveryFill;
        [SerializeField] private TextMeshProUGUI percentText;

        /// <summary>
        /// 값이 바뀔 때 차오르는 데 걸리는 시간(초).
        ///
        /// 68 → 71 이 한 프레임에 점프하면 "올랐다" 는 느낌이 안 난다. (설계 13.4절)
        ///
        /// ⚠ <b>변화량과 무관하게 이 시간을 쓴다.</b> "1초에 몇 % 씩" 으로 만들면 3% 상승이
        ///    0.015초에 끝나서 보간을 넣은 의미가 없어진다. 3% 가 올라도 0.5초를 쓴다.
        /// </summary>
        [Header("연출")]
        [SerializeField, Range(0.1f, 2f)] private float fillDuration = 0.5f;

        /// <summary>지금 화면에 그려져 있는 값.</summary>
        private float shownPercent;

        /// <summary>서버가 준 목표값. 보간의 도착점이다.</summary>
        private float targetPercent;

        /// <summary>이번 보간의 출발점. 값이 바뀐 순간의 <see cref="shownPercent"/> 다.</summary>
        private float fromPercent;

        /// <summary>이번 보간이 시작된 뒤 지난 시간(초).</summary>
        private float elapsed;

        /// <summary>
        /// 아직 한 번도 그리지 않았는지.
        ///
        /// ⚠ 로비에 처음 들어올 때는 <b>보간하지 않는다.</b> 현재 회복도가 68% 인데 0 에서부터
        ///    올라가면 "방금 68% 를 봉헌했다" 는 거짓말이 된다. 첫 값은 즉시 찍는다. (설계 13.4절)
        /// </summary>
        private bool firstApplyDone;

        private void OnEnable()
        {
            AltarState.Changed += Apply;

            // 캐시에 값이 있으면 먼저 그려 둔다. 조회 응답을 기다리는 동안 0% 로 비어 보이지 않게.
            if (AltarState.HasValue)
            {
                Apply();
            }

            AltarState.RequestRefresh();
        }

        private void OnDisable()
        {
            AltarState.Changed -= Apply;
        }

        /// <summary>
        /// 서버 값이 바뀌었다. 도착점만 옮기고, 실제로 차오르는 것은 <see cref="Update"/> 가 한다.
        ///
        /// ⚠ 조회가 실패하면 <see cref="AltarState.Changed"/> 가 오지 않으므로 마지막 상태가
        ///    그대로 남는다. 화면을 비우지 않는 것이 맞다. (STEP 9)
        /// </summary>
        private void Apply()
        {
            // 서버가 100 을 넘겨 보내는 일은 없어야 하지만, 넘어와도 게이지가 터지지 않게 막는다.
            targetPercent = Mathf.Clamp(AltarState.RecoveryPercent, 0f, 100f);

            if (!firstApplyDone)
            {
                firstApplyDone = true;
                shownPercent = targetPercent;
                fromPercent = targetPercent;
                elapsed = fillDuration;   // 보간할 것이 없다는 표시
                Redraw();
                return;
            }

            // 보간 중에 새 값이 와도 지금 보이는 값에서 이어서 간다. 되돌아가지 않는다.
            fromPercent = shownPercent;
            elapsed = 0f;
        }

        /// <summary>
        /// ⚠ <see cref="Time.unscaledDeltaTime"/> 을 쓴다. 봉헌 창이 열려 있는 동안 시간이
        ///    느려지거나 멈추는 연출이 뒤에 붙어도 게이지는 계속 차올라야 한다.
        /// </summary>
        private void Update()
        {
            if (elapsed >= fillDuration)
            {
                return;
            }

            elapsed += Time.unscaledDeltaTime;

            float t = fillDuration > 0f ? Mathf.Clamp01(elapsed / fillDuration) : 1f;
            shownPercent = Mathf.Lerp(fromPercent, targetPercent, t);

            Redraw();
        }

        /// <summary>게이지와 숫자를 <b>같은 값으로 같이</b> 갱신한다. 둘이 어긋나면 눈에 띈다.</summary>
        private void Redraw()
        {
            if (recoveryFill != null)
            {
                recoveryFill.fillAmount = shownPercent / 100f;
            }

            if (percentText != null)
            {
                percentText.text = $"{shownPercent:0}%";
            }
        }
    }
}
