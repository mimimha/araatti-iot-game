using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 구멍을 <b>서버가</b> 만들고 치우도록 경로를 갈아끼운다.
    ///
    /// <c>HullDamage</c> 는 원래 <c>Instantiate</c> 로 파손 지점을 만든다. 혼자 하는 씬에서는
    /// 그게 맞지만, 네트워크에서는 각자 만들게 되어 <b>자리가 제각각</b>이 되고
    /// 늦게 들어온 사람에게는 아무것도 안 생긴다.
    ///
    /// 그래서 만드는 방법과 치우는 방법만 바꿔 끼운다.
    /// <code>
    ///   만들기  Runner.Spawn   → 늦게 들어온 사람에게도 그대로 생긴다
    ///   치우기  Runner.Despawn → 모두의 화면에서 함께 사라진다
    /// </code>
    ///
    /// <b>일회성 RPC 를 쓰지 않는 이유.</b> "여기 구멍이 났다" 를 한 번 쏘면 그때 있던
    /// 사람만 받는다. 1분 뒤에 들어온 사람 화면에는 구멍이 없는데 배는 계속 깎인다.
    /// 스폰된 <c>NetworkObject</c> 는 <b>지금 있는 것</b>이라 접속하는 순간 그대로 온다.
    ///
    /// ⚠ 민화님의 <c>HullDamagePoint.prefab</c> 을 그대로 쓰지 않고 네트워크 전용 사본을 쓴다.
    ///    원본에 <c>NetworkObject</c> 를 달면 <c>ShipCoopTest</c> 혼자 플레이에서
    ///    스폰되지 않은 네트워크 오브젝트가 생긴다. 그 길을 건드리지 않기로 했다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopHoleSpawner : NetworkBehaviour
    {
        [Header("네트워크 전용 파손 지점")]
        [Tooltip("ShipCoopSceneSetup 이 HullDamagePoint 에서 만들어 넣는다.")]
        [SerializeField] private NetworkObject holePrefab;

        public override void Spawned()
        {
            if (Runner == null || !Runner.IsServer)
            {
                return;
            }

            if (holePrefab == null)
            {
                Debug.LogError(
                    "[ShipCoopHoleSpawner] 네트워크용 파손 지점 프리팹이 없습니다. " +
                    "구멍이 생기지 않습니다.", this);
                return;
            }

            HullDamage.Factory = SpawnHole;
            RepairTask.Remover = RemoveHole;

            Debug.Log("[ShipCoopHoleSpawner] 구멍 생성·제거를 서버 권위로 바꿨습니다.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // 세션이 끝나면 원래대로 돌려놓는다. 남겨 두면 다음에 혼자 하는 씬을 열었을 때
            // 죽은 Runner 로 스폰하려 든다.
            HullDamage.Factory = null;
            RepairTask.Remover = null;
        }

        /// <summary>
        /// 구멍 하나를 서버에서 만든다.
        ///
        /// <c>parent</c> 는 쓰지 않는다. Fusion 이 스폰한 오브젝트를 다른 것의 자식으로 넣으면
        /// 자리 복제가 부모 기준으로 꼬인다. 대신 <c>ShipCoopRider</c> 가 배에 태워
        /// 배가 트는 만큼 같이 돌게 한다.
        /// </summary>
        private RepairTask SpawnHole(RepairTask prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            NetworkObject spawned = Runner.Spawn(holePrefab, position, rotation);

            if (spawned == null)
            {
                Debug.LogError("[ShipCoopHoleSpawner] 구멍을 만들지 못했습니다.", this);
                return null;
            }

            return spawned.GetComponent<RepairTask>();
        }

        /// <summary>다 고쳐진 구멍을 치운다. 모두의 화면에서 함께 사라진다.</summary>
        private void RemoveHole(RepairTask hole)
        {
            if (hole == null)
            {
                return;
            }

            NetworkObject spawned = hole.GetComponent<NetworkObject>();

            if (spawned != null && Runner != null)
            {
                Runner.Despawn(spawned);
                return;
            }

            // 네트워크 오브젝트가 아니면 예전처럼 스스로 꺼진다.
            hole.gameObject.SetActive(false);
        }
    }
}
