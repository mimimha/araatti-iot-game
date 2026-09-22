using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 플레이어가 설 자리. **표시만 하는 부품이다.**
    ///
    /// 스포너는 <c>NetworkRunner</c> 와 같은 오브젝트(시작 씬)에 있어야 하고
    /// 자리는 게임 씬에 있다. 유니티는 씬을 넘는 참조를 저장하지 못하므로
    /// 인스펙터로 이을 수 없다. 대신 이 표식을 실행 중에 찾는다.
    ///
    /// 이름순(<c>MineSpawn_01</c>, <c>MineSpawn_02</c>)으로 줄을 세우므로
    /// 어느 컴퓨터에서나 같은 번호가 같은 자리에 선다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineSpawnPoint : MonoBehaviour
    {
    }
}
