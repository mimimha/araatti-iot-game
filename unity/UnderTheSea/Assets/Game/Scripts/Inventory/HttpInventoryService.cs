using System;
using System.Collections;
using UnityEngine;
using UnderTheSea.Account;
using UnderTheSea.Lobby;

namespace UnderTheSea.Inventory
{
    /// <summary>
    /// 실제 서버와 통신하는 인벤토리 서비스.
    ///
    /// 서버 규격
    ///   GET /api/inventory   Bearer  → 200, { items: [...] }   (없으면 빈 배열, 404 아님)
    ///
    /// <see cref="HttpCharacterService"/> 와 같은 모양이다. 토큰은 직접 들고 있지 않고
    /// <see cref="AccountServiceLocator.Auth"/> 에서 그때그때 꺼내 쓴다.
    ///
    /// ⚠ UnityWebRequest 를 직접 쓰지 않는다. <see cref="HttpJson"/> 과 <see cref="HttpApiConfig"/> 를
    ///    그대로 재사용한다.
    /// </summary>
    public class HttpInventoryService : MonoBehaviour, IInventoryService
    {
        [Header("서버")]
        [Tooltip("비워 두면 실행 인자 -api 를 쓰고, 그것도 없으면 http://localhost:5080 을 쓴다.")]
        [SerializeField] private string baseUrl = string.Empty;

        [Header("로그")]
        [Tooltip("켜면 요청한 메서드 · 경로 · 상태 코드를 Console 에 남긴다. 토큰은 찍지 않는다.")]
        [SerializeField] private bool verboseLogging = false;

        public event Action<bool, InventoryItemDto[], string> OnInventoryResult;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log($"[HttpInventoryService] 실제 서버 인벤토리를 사용합니다. ({ResolvedBaseUrl})", this);
        }

        private void OnDestroy()
        {
            AccountServiceLocator.Unregister(this);
        }

        private string ResolvedBaseUrl =>
            string.IsNullOrWhiteSpace(baseUrl) ? HttpApiConfig.EffectiveBaseUrl : baseUrl.Trim().TrimEnd('/');

        /// <summary>지금 쓸 수 있는 토큰. 로그인 전이면 빈 문자열.</summary>
        private static string AccessToken
        {
            get
            {
                IAuthService auth = AccountServiceLocator.Auth;
                return auth != null ? auth.AccessToken : string.Empty;
            }
        }

        public void RequestInventory()
        {
            StartCoroutine(RequestInventoryRoutine());
        }

        private IEnumerator RequestInventoryRoutine()
        {
            string token = AccessToken;
            if (string.IsNullOrEmpty(token))
            {
                Finish(false, Array.Empty<InventoryItemDto>(), "로그인이 필요합니다.");
                yield break;
            }

            // 응답이 몇 번째인지 표를 먼저 받아 둔다. 늦게 도착하면 캐시에 반영되지 않는다.
            int sequence = AltarState.IssueSequence();

            string url = HttpApiConfig.Combine(ResolvedBaseUrl, HttpApiConfig.InventoryPath);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "GET", null, token, r => result = r);

            Log("GET", HttpApiConfig.InventoryPath, result);

            if (!result.IsSuccess)
            {
                // ⚠ 조회 실패 때문에 기존 수량을 0 으로 덮지 않는다.
                //    잠깐 서버가 끊긴 것 때문에 멀쩡한 값을 잃는다.
                Finish(false, Array.Empty<InventoryItemDto>(), result.FailureMessage);
                yield break;
            }

            if (!HttpJson.TryParse(result.Body, out InventoryListBody parsed, out string parseFailure))
            {
                Finish(false, Array.Empty<InventoryItemDto>(), parseFailure);
                yield break;
            }

            InventoryItemDto[] items = parsed.items ?? Array.Empty<InventoryItemDto>();

            PlayerInventory.ApplyItems(sequence, items);

            // 가진 것이 0개인 것은 실패가 아니다. 성공 + 빈 배열로 알린다.
            Finish(true, items, string.Empty);
        }

        /// <summary>
        /// 요청 하나를 마무리한다.
        ///
        /// ⚠ <see cref="PlayerInventory.RequestFinished"/> 를 성공·실패 양쪽에서 반드시 부른다.
        ///    빼먹으면 이후 조회가 "이미 묻고 있다" 로 영영 막힌다.
        /// </summary>
        private void Finish(bool success, InventoryItemDto[] items, string failureMessage)
        {
            PlayerInventory.RequestFinished();
            OnInventoryResult?.Invoke(success, items, failureMessage);
        }

        // ------------------------------------------------------------
        // 손으로 확인하는 길 (UI 가 아직 없다)
        //
        // 플레이 모드에서 "AccountService (Http)" 오브젝트를 고르고, 이 컴포넌트의
        // 톱니바퀴 메뉴에서 실행한다.
        // ------------------------------------------------------------

        /// <summary>읽기만 한다. DB 를 바꾸지 않는다.</summary>
        [ContextMenu("디버그 — 인벤토리 조회")]
        private void DebugRequestInventory()
        {
            PlayerInventory.RequestRefresh();
            Debug.Log("[HttpInventoryService] 인벤토리 조회를 요청했습니다. " +
                      "결과는 PlayerInventory.SeaHeartFragment 에 반영됩니다.", this);
        }

        /// <summary>지금 캐시에 들어 있는 값만 찍는다. 요청하지 않는다.</summary>
        [ContextMenu("디버그 — 캐시 값 보기")]
        private void DebugDumpCache()
        {
            Debug.Log(
                $"[HttpInventoryService] 캐시 — 조각 {PlayerInventory.SeaHeartFragment}개, " +
                $"서버 값 받음={PlayerInventory.HasValue}", this);
        }

        /// <summary>GET /api/inventory 의 응답 껍데기. JsonUtility 는 최상위 배열을 못 읽는다.</summary>
#pragma warning disable 0649
        [Serializable]
        private class InventoryListBody
        {
            public InventoryItemDto[] items;
        }
#pragma warning restore 0649

        /// <summary>⚠ 토큰은 절대 찍지 않는다. 메서드 · 경로 · 상태 코드만 남긴다.</summary>
        private void Log(string method, string path, HttpJsonResult result)
        {
            if (!verboseLogging)
            {
                return;
            }

            Debug.Log(
                $"[HttpInventoryService] {method} {path} → " +
                $"{(result.IsSuccess ? "성공" : "실패")} (HTTP {result.StatusCode})", this);
        }
    }
}
