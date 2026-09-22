using System;
using UnderTheSea.Account;
using UnderTheSea.Lobby;

namespace UnderTheSea.Inventory
{
    /// <summary>
    /// 내 보유 수량의 <b>클라이언트 캐시</b>.
    ///
    /// ⚠ 원본은 언제나 서버(AraAtti.Api + MySQL)다. 이 클래스는 서버가 준 숫자를 들고만 있는다.
    ///
    /// <b>그래서 더하거나 빼는 메서드가 없다.</b> Add · Remove · Spend · Gain 이 없고
    /// "봉헌 성공했으니 현재 수량 - amount" 같은 로컬 계산도 하지 않는다.
    /// 로컬 계산이 서버와 1이라도 어긋나면 그 어긋남이 다음 봉헌까지 따라간다.
    /// 수량이 바뀌는 길은 <b>서버 응답을 그대로 대입하는 것</b> 하나뿐이다.
    ///
    /// 수량을 싣고 오는 응답은 셋이다. 셋 다 같은 신선도 순번을 쓴다 (AltarState 참고).
    ///
    /// <code>
    ///   GET  /api/inventory      items[sea_heart_fragment].quantity
    ///   GET  /api/altar/state    myFragments
    ///   POST /api/altar/offer    remainingFragments   (성공 · 409 둘 다)
    /// </code>
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.2 · 12.7절
    /// </summary>
    public static class PlayerInventory
    {
        /// <summary>
        /// 지금 들고 있는 바다의 심장 조각 수. 서버에서 한 번도 받지 못했으면 0.
        ///
        /// 0 인 것과 아직 못 받은 것을 구분하려면 <see cref="HasValue"/> 를 함께 본다.
        /// </summary>
        public static long SeaHeartFragment { get; private set; }

        /// <summary>서버 값을 한 번이라도 받았는지.</summary>
        public static bool HasValue { get; private set; }

        /// <summary>수량이 실제로 바뀌었을 때만 오른다. 같은 값을 다시 받으면 올리지 않는다.</summary>
        public static event Action Changed;

        /// <summary>조회가 진행 중인지. 같은 요청을 두 번 내지 않기 위한 값이다.</summary>
        private static bool inFlight;

        /// <summary>
        /// 서버에 지금 값을 물어본다. (GET /api/inventory)
        ///
        /// 화면은 이것만 부르면 된다. 아래 세 가지를 여기서 처리한다.
        ///
        /// <code>
        ///   봉헌(POST)이 진행 중이면   → 지금 묻지 않고 "끝나면 한 번" 으로 미룬다
        ///   이미 묻고 있으면           → 합친다 (같은 답을 두 번 받을 이유가 없다)
        ///   그 외                      → 서비스에 요청한다
        /// </code>
        ///
        /// ⚠ 봉헌 중에 미루는 이유: 봉헌 응답이 더 새로운 권위 값을 싣고 오는데,
        ///    그 직전에 출발한 조회가 늦게 도착해 옛 수량으로 되돌릴 수 있다.
        /// </summary>
        public static void RequestRefresh()
        {
            if (AltarState.DeferIfMutating(AltarState.PendingRefresh.Inventory))
            {
                return;
            }

            if (inFlight)
            {
                return;
            }

            IInventoryService service = AccountServiceLocator.Inventory;
            if (service == null)
            {
                return;
            }

            inFlight = true;
            service.RequestInventory();
        }

        /// <summary>성공이든 실패든 요청이 끝나면 반드시 불린다. 빼먹으면 이후 조회가 영영 막힌다.</summary>
        internal static void RequestFinished()
        {
            inFlight = false;
        }

        /// <summary>
        /// GET /api/inventory 의 결과를 적용한다.
        ///
        /// 목록에 조각이 없으면 0 이다. 서버가 "안 가지고 있다" 고 말한 것이므로 그대로 받는다.
        /// </summary>
        internal static void ApplyItems(int sequence, InventoryItemDto[] items)
        {
            long fragments = 0;

            if (items != null)
            {
                foreach (InventoryItemDto item in items)
                {
                    if (item != null && item.itemId == ItemIds.SeaHeartFragment)
                    {
                        fragments = item.quantity;
                        break;
                    }
                }
            }

            // 늦게 도착한 옛 응답이면 버린다. 순번은 조각 수를 싣고 오는 세 응답이 공유한다.
            if (!AltarState.TryAcceptSequence(sequence))
            {
                return;
            }

            SetFragments(fragments);
        }

        /// <summary>
        /// 서버가 준 조각 수를 그대로 대입한다.
        ///
        /// ⚠ 순번 확인은 <b>부르는 쪽이 이미 끝냈다.</b> 제단 응답(myFragments · remainingFragments)도
        ///    같은 숫자를 싣고 오는데, 그쪽은 제단 값과 함께 한 번에 판정하기 때문이다.
        ///    한 응답에 순번을 두 번 소비하지 않게 여기서는 다시 보지 않는다.
        /// </summary>
        internal static void SetFragments(long fragments)
        {
            bool changed = !HasValue || SeaHeartFragment != fragments;

            SeaHeartFragment = fragments;
            HasValue = true;

            if (changed)
            {
                Changed?.Invoke();
            }
        }

        /// <summary>
        /// 로그아웃할 때 메모리 캐시만 비운다.
        ///
        /// ⚠ 서버 데이터는 건드리지 않는다. 다음 사람이 로그인하기 전까지 이전 계정의
        ///    수량이 남아 있으면 안 되기 때문에 비운다.
        /// </summary>
        public static void Clear()
        {
            SeaHeartFragment = 0;
            HasValue = false;
            inFlight = false;
            Changed?.Invoke();
        }
    }
}
