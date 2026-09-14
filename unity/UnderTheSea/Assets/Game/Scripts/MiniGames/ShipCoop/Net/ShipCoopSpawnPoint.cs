using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 갑판 위의 스폰 자리. **표시만 하는 부품이다.**
    ///
    /// <b>왜 스포너가 <c>Transform[]</c> 로 직접 들고 있지 않은가.</b>
    /// 스포너는 <c>NetworkRunner</c> 와 같은 오브젝트에 있어야 한다.
    /// Fusion 은 <b>러너와 같은 오브젝트에 붙은</b> <c>SimulationBehaviour</c> 만 자동으로 등록한다.
    /// 다른 오브젝트에 두면 <c>PlayerJoined</c> 가 아예 들어오지 않는다. 오류도 없다 —
    /// 접속은 되는데 아무도 스폰되지 않는다. 실측해서 알아냈다.
    ///
    /// 그런데 러너는 시작 씬(<c>ShipCoopBoot</c>)에, 스폰 자리는 게임 씬에 있다.
    /// <b>유니티는 씬을 넘는 참조를 저장하지 못한다.</b> 그래서 인스펙터로 이을 수 없다.
    ///
    /// 대신 이 표식을 붙여 두고 스포너가 실행 중에 찾는다.
    /// 이름순(<c>Spawn_1</c>, <c>Spawn_2</c>...)으로 줄을 세우므로 순서는 늘 같다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopSpawnPoint : MonoBehaviour
    {
    }
}
