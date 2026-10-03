using System;

namespace UnderTheSea.Inventory
{
    /// <summary>
    /// 인벤토리 한 칸. 서버 응답의 필드 이름과 같아서 JsonUtility 가 그대로 채운다.
    ///
    /// (server/AraAtti.Api/Contracts/InventoryContracts.cs 의 InventoryItemResponse)
    ///
    /// ⚠ quantity 는 서버에서 int unsigned 다. Unity 쪽은 long 으로 받는다 —
    ///    JsonUtility 가 확실히 지원하는 정수 타입이고, uint 의 최댓값도 담을 수 있다.
    ///    수량을 float 로 담지 않는다. 큰 값에서 정밀도를 잃는다.
    /// </summary>
    // JsonUtility 가 응답을 읽어 채우는 필드들이다. 코드에서 대입하는 곳이 없으므로
    // "값이 대입되지 않았다"(CS0649) 경고가 나는데, 여기서는 정상이라 끈다.
#pragma warning disable 0649
    [Serializable]
    public class InventoryItemDto
    {
        public string itemId;
        public string displayName;
        public long quantity;
    }
#pragma warning restore 0649

    /// <summary>
    /// 인벤토리 저장소의 경계.
    ///
    /// ⚠ 이 인터페이스는 <b>요청을 보내는 일만</b> 한다. 받은 값을 캐시에 넣는 것은
    ///    <see cref="PlayerInventory"/> 이고, 화면에 그리는 것은 UI 다.
    ///
    /// ⚠ 화면 코드는 보통 이것을 직접 부르지 않고 <see cref="PlayerInventory.RequestRefresh"/> 를
    ///    부른다. 그쪽이 중복 요청 합치기와 봉헌 중 요청 차단(mutation barrier)을 함께 처리한다.
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 9.2절
    /// </summary>
    public interface IInventoryService
    {
        /// <summary>
        /// GET /api/inventory. 결과는 <see cref="OnInventoryResult"/> 로 온다.
        ///
        /// 받은 값은 이 서비스가 <see cref="PlayerInventory"/> 에 적용한 뒤 이벤트를 올린다.
        /// </summary>
        void RequestInventory();

        /// <summary>
        /// 조회 결과. (성공 여부, 받은 아이템 목록, 실패했다면 그 이유)
        ///
        /// ⚠ 가진 것이 0개인 것은 <b>실패가 아니다.</b> 성공 + 빈 배열로 온다.
        ///    서버가 404 가 아니라 200 + <c>items: []</c> 를 돌려준다.
        /// </summary>
        event Action<bool, InventoryItemDto[], string> OnInventoryResult;
    }
}
