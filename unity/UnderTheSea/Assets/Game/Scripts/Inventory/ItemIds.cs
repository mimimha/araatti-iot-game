namespace UnderTheSea.Inventory
{
    /// <summary>
    /// 아이템 id 상수.
    ///
    /// ⚠ 값은 <b>서버 DB 의 player_inventories.item_id 와 같은 문자열</b>이어야 한다.
    ///    (server/AraAtti.Api/Endpoints/InventoryEndpoints.cs 의 SeaHeartFragmentItemId)
    ///
    /// 지금 아이템은 한 종류뿐이라 ScriptableObject 아이템 DB 를 만들지 않는다.
    /// 3종 이상으로 늘어나는 것이 확정되면 그때 구조를 올린다.
    /// (docs/prd/lobby_altar_inventory_system_design.md 4장)
    /// </summary>
    public static class ItemIds
    {
        /// <summary>바다의 심장 조각. 제단에 봉헌하는 재화.</summary>
        public const string SeaHeartFragment = "sea_heart_fragment";
    }
}
