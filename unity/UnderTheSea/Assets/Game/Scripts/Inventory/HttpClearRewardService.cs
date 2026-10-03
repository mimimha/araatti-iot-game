using System;
using System.Collections;
using MiniGames.Common;
using UnderTheSea.Account;
using UnderTheSea.Lobby;
using UnityEngine;

namespace UnderTheSea.Inventory
{
    /// <summary>
    /// **미니게임을 이기면 서버에 바다의 심장 조각을 달라고 한다.** <see cref="ClearReward.Service"/> 에 꽂힌다.
    ///
    /// <code>
    ///   결과 화면(MiniGameResultOverlay)이 이긴 판에서 ClearReward.Claim 을 부른다
    ///     → 여기서 POST /api/inventory/clear-reward { gameId, matchKey }
    ///     → 서버가 조각 +1 하고 지금 수량을 돌려준다
    ///     → PlayerInventory 에 넣는다 → 로비의 조각 개수가 따라 바뀐다
    /// </code>
    ///
    /// <b>결과 화면은 이 응답을 기다리지 않는다</b>(서연 — ClearReward.cs). 그래서 여기서 실패해도
    /// 화면은 그대로다. 실패는 로그로만 남긴다.
    ///
    /// ⚠ <b>판 번호(matchKey)는 청구할 때 한 번 만들고, 다시 보낼 때도 같은 값을 쓴다.</b> 응답이 끊겨
    ///    다시 보내도 서버가 같은 판을 두 번 주지 않는다. 설계는 미니게임 서버가 판 번호를 만들어
    ///    나눠 주는 것이었지만(STEP 10.5), 그러려면 세 게임의 네트워크 코드를 고쳐야 해서 시연용으로
    ///    클라이언트에서 만든다.
    ///
    /// ⚠ <b>실제 계정 서버에 붙었을 때만 청구한다.</b> 가짜 서비스(오프라인 개발)면 아무것도 안 한다.
    ///    서버에는 설치하지 않는다 — 로그인한 사람이 없다.
    /// </summary>
    public sealed class HttpClearRewardService : MonoBehaviour, IClearRewardService
    {
        /// <summary>네트워크 오류로 실패했을 때 다시 보내는 횟수. 같은 판 번호로 보낸다.</summary>
        private const int MaxAttempts = 3;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (FusionLaunchArguments.IsDedicatedServerProcess() || ClearReward.Service != null)
            {
                return;
            }

            var host = new GameObject("[클리어 보상]");
            DontDestroyOnLoad(host);
            ClearReward.Service = host.AddComponent<HttpClearRewardService>();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ClearReward.Service, this))
            {
                ClearReward.Service = null;
            }
        }

        public void Claim(string gameId, MiniGameResult result)
        {
            // 가짜 인벤토리(오프라인 개발)면 붙을 서버가 없다.
            if (AccountServiceLocator.Inventory is not HttpInventoryService)
            {
                Debug.Log($"[클리어 보상] 실제 계정 서버가 아니라 '{gameId}' 보상을 청구하지 않습니다.");
                return;
            }

            string matchKey = $"{gameId}:{Guid.NewGuid():N}";
            StartCoroutine(ClaimRoutine(gameId, matchKey));
        }

        private IEnumerator ClaimRoutine(string gameId, string matchKey)
        {
            IAuthService auth = AccountServiceLocator.Auth;
            string token = auth != null ? auth.AccessToken : string.Empty;
            if (string.IsNullOrEmpty(token))
            {
                Debug.LogWarning($"[클리어 보상] 로그인하지 않아 '{gameId}' 보상을 청구하지 못했습니다.");
                yield break;
            }

            string url = HttpApiConfig.Combine(null, HttpApiConfig.ClearRewardPath);
            string body = JsonUtility.ToJson(new ClaimRequestBody { gameId = gameId, matchKey = matchKey });

            for (int attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                // 응답이 몇 번째인지 표를 먼저 받아 둔다. 늦게 온 응답이 새 값을 덮지 않는다.
                int sequence = AltarState.IssueSequence();

                HttpJsonResult result = default;
                yield return HttpJson.Send(url, "POST", body, token, r => result = r);

                if (result.IsSuccess)
                {
                    Apply(gameId, sequence, result.Body);
                    yield break;
                }

                // 서버가 답은 했는데 거절했다(쿨다운 · 로그인 만료 · 잘못된 요청). 다시 보내도 같다.
                if (result.StatusCode >= 400 && result.StatusCode < 500)
                {
                    Debug.LogWarning(
                        $"[클리어 보상] '{gameId}' 보상을 서버가 거절했습니다 (HTTP {result.StatusCode}) — {result.FailureMessage}");
                    yield break;
                }

                Debug.LogWarning(
                    $"[클리어 보상] '{gameId}' 보상 청구 실패 ({attempt}/{MaxAttempts}) — {result.FailureMessage}");

                if (attempt < MaxAttempts)
                {
                    yield return new WaitForSecondsRealtime(attempt);
                }
            }
        }

        private static void Apply(string gameId, int sequence, string responseBody)
        {
            if (!HttpJson.TryParse(responseBody, out ClaimResponseBody parsed, out string parseFailure))
            {
                Debug.LogWarning($"[클리어 보상] '{gameId}' 응답을 읽지 못했습니다 — {parseFailure}");
                return;
            }

            // 조회와 같은 길로 넣는다. 캐시에 직접 대입하지 않는다(순번 확인을 건너뛰게 된다).
            PlayerInventory.ApplyItems(sequence, new[]
            {
                new InventoryItemDto
                {
                    itemId = ItemIds.SeaHeartFragment,
                    displayName = "바다의 심장 조각",
                    quantity = parsed.quantity
                }
            });

            if (parsed.granted)
            {
                Debug.Log(
                    $"[클리어 보상] '{gameId}' 바다의 심장 조각 {(parsed.duplicate ? "(이미 받은 판)" : "+1")} " +
                    $"→ 보유 {parsed.quantity}개");
            }
            else
            {
                // 섬 회복이 끝나 더 주지 않는다. 오류가 아니다 — 게임은 이긴 것이다.
                Debug.Log($"[클리어 보상] '{gameId}' 조각을 주지 않았습니다 ({parsed.code}). 보유 {parsed.quantity}개");
            }
        }

#pragma warning disable 0649
        [Serializable]
        private class ClaimRequestBody
        {
            public string gameId;
            public string matchKey;
        }

        [Serializable]
        private class ClaimResponseBody
        {
            public bool success;
            public bool granted;
            public long quantity;
            public bool duplicate;
            public string code;
        }
#pragma warning restore 0649
    }
}
