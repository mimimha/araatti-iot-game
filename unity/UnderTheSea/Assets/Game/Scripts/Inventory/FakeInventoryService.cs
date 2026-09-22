using System;
using System.Collections;
using UnityEngine;
using UnderTheSea.Account;
using UnderTheSea.Lobby;

namespace UnderTheSea.Inventory
{
    /// <summary>
    /// 서버 없이 인벤토리를 흉내내는 가짜 서비스.
    ///
    /// ⚠ HTTP · UnityWebRequest 를 쓰지 않습니다. 전부 이 PC 안에서 끝납니다.
    ///
    /// 조각 수의 "가짜 원본" 은 <see cref="FakeAltarService"/> 가 들고 있습니다.
    /// 두 가짜 서비스가 각자 숫자를 세면 봉헌한 뒤 인벤토리만 옛 값으로 남습니다.
    /// 그래서 여기서는 세지 않고 <b>가져와서 돌려주기만</b> 합니다.
    ///
    /// 가짜 제단이 아직 없으면(제단 없이 이 서비스만 띄운 경우) 인스펙터의 대체값을 씁니다.
    /// </summary>
    public class FakeInventoryService : MonoBehaviour, IInventoryService
    {
        [Header("가짜 제단이 없을 때 쓸 값")]
        [Tooltip("FakeAltarService 가 없을 때만 쓰인다. 있으면 그쪽 값을 따른다.")]
        [SerializeField] private long fallbackFragments = 100;

        [Header("응답 지연 (초)")]
        [SerializeField, Range(0f, 3f)] private float responseDelay = 0.3f;

        public event Action<bool, InventoryItemDto[], string> OnInventoryResult;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            AccountServiceLocator.Register(this);
            Debug.Log("[FakeInventoryService] 가짜 인벤토리를 사용합니다. 서버에 붙지 않습니다.", this);
        }

        private void OnDestroy()
        {
            AccountServiceLocator.Unregister(this);
        }

        public void RequestInventory()
        {
            StartCoroutine(RequestInventoryRoutine());
        }

        private IEnumerator RequestInventoryRoutine()
        {
            // 진짜와 같은 자리에서 순번을 받는다. 그래야 늦게 온 응답이 새 값을 덮지 않는다.
            int sequence = AltarState.IssueSequence();

            if (responseDelay > 0f)
            {
                yield return new WaitForSeconds(responseDelay);
            }

            FakeAltarService altar = FakeAltarService.Current;
            long fragments = altar != null ? altar.Fragments : Math.Max(0, fallbackFragments);

            // 서버와 같은 모양으로 돌려준다. 0개면 빈 배열이다 — 404 가 아니다.
            InventoryItemDto[] items = fragments > 0
                ? new[]
                {
                    new InventoryItemDto
                    {
                        itemId = ItemIds.SeaHeartFragment,
                        displayName = "바다의 심장 조각",
                        quantity = fragments
                    }
                }
                : Array.Empty<InventoryItemDto>();

            // ⚠ 진짜 서비스와 **같은 적용 경로**를 쓴다. 캐시에 직접 대입하지 않는다.
            PlayerInventory.ApplyItems(sequence, items);

            PlayerInventory.RequestFinished();
            OnInventoryResult?.Invoke(true, items, string.Empty);
        }

        // ------------------------------------------------------------
        // 손으로 확인하는 길 (UI 가 아직 없다)
        //
        // 플레이 모드에서 "AccountService (Fake)" 오브젝트를 고르고,
        // 이 컴포넌트의 ⋮ 메뉴에서 실행한다.
        //
        // ⚠ 여기서 수량을 직접 건드리지 않는다. 언제나
        //      ContextMenu → 서비스 public API → 기존 응답·적용 경로 → PlayerInventory
        //    순서로만 값이 움직인다. 그래야 이 메뉴로 확인한 것이 실제 동작과 같다.
        // ------------------------------------------------------------

        /// <summary>읽기만 한다. 가짜 서버의 수량을 바꾸지 않는다.</summary>
        [ContextMenu("디버그 — 인벤토리 조회")]
        private void DebugRequestInventory()
        {
            // 진짜와 같은 입구를 쓴다. 중복 요청 합치기와 봉헌 중 차단까지 그대로 탄다.
            OnInventoryResult += LogInventoryOnce;
            PlayerInventory.RequestRefresh();
        }

        /// <summary>응답이 한 번 오면 찍고 스스로 구독을 뗀다.</summary>
        private void LogInventoryOnce(bool success, InventoryItemDto[] items, string failureMessage)
        {
            OnInventoryResult -= LogInventoryOnce;

            if (!success)
            {
                Debug.LogWarning($"[FakeInventoryService] 조회 실패 — {failureMessage}", this);
                return;
            }

            long quantity = 0;
            foreach (InventoryItemDto item in items)
            {
                if (item != null && item.itemId == ItemIds.SeaHeartFragment)
                {
                    quantity = item.quantity;
                    break;
                }
            }

            Debug.Log(
                $"[FakeInventoryService] 조회 성공 — 응답 items {items.Length}개, " +
                $"{ItemIds.SeaHeartFragment} {quantity}개 / " +
                $"PlayerInventory 캐시 {PlayerInventory.SeaHeartFragment}개 " +
                $"(서버 값 받음={PlayerInventory.HasValue})", this);
        }
    }
}
