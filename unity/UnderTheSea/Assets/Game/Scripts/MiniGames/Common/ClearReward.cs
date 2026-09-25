using System;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 미니게임 클리어 보상을 서버에 알리는 창구. 서버 담당자가 구현해 <see cref="ClearReward.Service"/> 에 꽂는다.
    ///
    /// 구현할 것 (설계 문서 STEP 11 · 9.2절 clear-reward)
    /// <code>
    ///   POST /api/inventory/clear-reward   { gameId, matchKey }
    ///   서버가 인벤토리 sea_heart_fragment 를 +1 하고, 받은 수량을 PlayerInventory 에 대입한다
    ///   (로비 우측 상단 ×N 이 따라 바뀐다)
    /// </code>
    ///
    /// <b>결과 화면은 이 응답을 기다리지도, 읽지도 않는다.</b> 클리어하면 보상은 무조건 받는 것으로
    /// 보여 준다(기획 결정). 재시도 · 중복 방지(matchKey)는 이 서비스 안에서 처리한다.
    /// </summary>
    public interface IClearRewardService
    {
        /// <param name="gameId">어느 게임의 클리어인가. <see cref="MiniGameConfig.FragmentId"/> ("sword" / "mining" / "ship")</param>
        /// <param name="result">서버가 확정한 결과. 판 식별값(matchKey)은 STEP 10.5 이후 여기에 더한다.</param>
        void Claim(string gameId, MiniGameResult result);
    }

    /// <summary>
    /// **클리어 보상 청구의 유일한 입구.** 결과 화면(<c>MiniGameResultOverlay</c>)이 클리어한 판에서만 부른다.
    ///
    /// 규칙: 클리어한 판마다 바다의 심장 조각 1개. 게임 오버면 부르지 않는다.
    ///
    /// <b>서버 연동 전에는 아무것도 하지 않는다</b>(로그만 남긴다). 결과 화면의 보상 칸은 이것과
    /// 상관없이 뜬다. 실제 수량은 서버가 붙어야 늘어난다 — 수량의 원본은 서버이고
    /// <c>PlayerInventory</c> 에는 더하는 길이 없다.
    /// </summary>
    public static class ClearReward
    {
        /// <summary>서버 담당자가 꽂는 자리. 비어 있으면 로그만 남긴다.</summary>
        public static IClearRewardService Service { get; set; }

        public static void Claim(string gameId, in MiniGameResult result)
        {
            if (Service == null)
            {
                Debug.Log($"[ClearReward] 서버 연동 전 — '{gameId}' 클리어 보상을 서버에 알리지 않았습니다. (화면에는 표시됨)");
                return;
            }

            try
            {
                Service.Claim(gameId, result);
            }
            catch (Exception e)
            {
                // 청구가 터져도 결과 화면과 로비 복귀는 막지 않는다.
                Debug.LogError($"[ClearReward] 보상 청구 중 오류가 났습니다.\n{e}");
            }
        }
    }
}
