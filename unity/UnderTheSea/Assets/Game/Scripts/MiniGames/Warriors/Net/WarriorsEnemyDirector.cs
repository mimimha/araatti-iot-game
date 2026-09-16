using System;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 몬스터를 **서버가** 만들도록 경로를 갈아끼우고, 1페이즈에만 스포너를 켠다.
    ///
    /// <b>일회성 RPC 를 쓰지 않는 이유.</b> "여기 몬스터가 났다" 를 한 번 쏘면 그때 있던
    /// 사람만 받는다. 스폰된 <c>NetworkObject</c> 는 <b>지금 있는 것</b>이라
    /// 늦게 들어온 사람에게도 그대로 온다.
    ///
    /// ⚠ 서연님의 원본 적 프리팹을 그대로 쓰지 않고 네트워크 전용 사본을 쓴다.
    ///    원본에 <c>NetworkObject</c> 를 달면 <c>WarriorsTest</c> 혼자 플레이에서
    ///    스폰되지 않은 네트워크 오브젝트가 생긴다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsEnemyDirector : NetworkBehaviour
    {
        /// <summary>원본 프리팹 이름과 네트워크 사본의 짝.</summary>
        [Serializable]
        public struct EnemyPair
        {
            [Tooltip("스포너의 표에 들어 있는 원본 프리팹 이름. 예: CrabEnemy")]
            public string sourceName;

            [Tooltip("그 자리에 대신 스폰할 네트워크 사본.")]
            public NetworkObject networked;
        }

        [Header("네트워크 전용 몬스터")]
        [Tooltip("WarriorsSceneSetup 이 원본 프리팹에서 만들어 넣는다.")]
        [SerializeField] private EnemyPair[] enemies = Array.Empty<EnemyPair>();

#if UNITY_EDITOR
        /// <summary>
        /// 생성 도구가 몬스터 사본 목록을 넣는다. **에디터 전용이다.**
        ///
        /// ⚠ <c>SerializedProperty.objectReferenceValue</c> 로는 저장되지 않는다.
        ///    중첩 구조체 배열의 오브젝트 참조가 조용히 <c>fileID: 0</c> 으로 남는다.
        ///    이름 필드는 저장되는데 참조만 빠져서 "사본 3종" 이라고 찍으면서도
        ///    "사본이 없다" 는 상태가 된다. 실측으로 확인했다.
        /// </summary>
        public void EditorSetEnemies(EnemyPair[] pairs) => enemies = pairs ?? Array.Empty<EnemyPair>();
#endif

        private WarriorsEnemySpawner spawner;
        private WarriorsMatchState match;
        private bool spawningOpened;

        /// <summary>서버가 만들어 둔 몬스터들. 라운드가 끝나면 이 목록대로 치운다.</summary>
        private readonly List<NetworkObject> spawned = new List<NetworkObject>();

        public override void Spawned()
        {
            spawner = FindFirstObjectByType<WarriorsEnemySpawner>(FindObjectsInactive.Include);
            match = FindFirstObjectByType<WarriorsMatchState>(FindObjectsInactive.Include);

            if (!HasStateAuthority) return;

            if (spawner == null)
            {
                Debug.LogError("[WarriorsEnemyDirector] 스포너를 찾지 못했습니다. 몬스터가 나오지 않습니다.", this);
                return;
            }

            WarriorsEnemySpawner.Factory = SpawnEnemy;
            Debug.Log($"[WarriorsEnemyDirector] 몬스터 생성을 서버 권위로 바꿨습니다. (사본 {enemies.Length}종)");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // 세션이 끝나면 원래대로 돌려놓는다. 남겨 두면 다음에 혼자 하는 씬을 열었을 때
            // 죽은 Runner 로 스폰하려 든다.
            WarriorsEnemySpawner.Factory = null;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || spawner == null || match == null) return;

            // 1페이즈에만 몬스터가 나온다. 대기 · 카운트다운 · 라운드 소개 중에는 조용해야 한다.
            bool wantSpawning = match.Phase == WarriorsMatchPhase.Phase1 && !match.InIntro;

            if (wantSpawning == spawningOpened) return;

            spawningOpened = wantSpawning;
            spawner.enabled = wantSpawning;

            Debug.Log($"[WarriorsEnemyDirector] 몬스터 스폰을 {(wantSpawning ? "켰습니다" : "껐습니다")}.");

            // 1페이즈가 끝났다. 해변에 남은 몬스터를 치운다.
            if (!wantSpawning) ClearBeach();
        }

        /// <summary>
        /// **해변을 비운다. 서버만 한다.**
        ///
        /// <c>Runner.Despawn</c> 이라야 두 화면에서 같은 순간에 사라진다.
        /// 클라이언트가 <c>Destroy</c> 하거나 꺼 버리면 그 컴퓨터에서만 없어지고,
        /// 서버에는 그대로 살아 있어 계속 때리고 맞는다.
        ///
        /// 치운 몬스터는 <c>Defeated</c> 를 내지 않으므로 처치 수도 점수도 오르지 않는다.
        /// </summary>
        private void ClearBeach()
        {
            int cleared = 0;

            foreach (NetworkObject one in spawned)
            {
                if (one == null || !one.IsValid) continue;

                Runner.Despawn(one);
                cleared++;
            }

            spawned.Clear();

            // 서연님 스포너의 추적 목록에도 파괴된 참조가 남아 있다. 같이 비운다.
            if (spawner != null) spawner.ForgetAliveEnemies();

            Debug.Log($"[WarriorsEnemyDirector] 해변에 남은 몬스터 {cleared}마리를 치웠습니다.");
        }

        /// <summary>몬스터 하나를 서버에서 만든다. 이름으로 네트워크 사본을 찾는다.</summary>
        private WarriorsTarget SpawnEnemy(
            WarriorsTarget source, Vector3 position, Quaternion rotation, Transform parent)
        {
            if (source == null) return null;

            // 1페이즈가 아니면 만들지 않는다. 스포너 코루틴이 기다리다 깨어나
            // 라운드가 넘어간 뒤 한 마리를 더 떨구는 일이 있다.
            if (match != null && match.Phase != WarriorsMatchPhase.Phase1) return null;

            NetworkObject prefab = FindNetworked(source.name);

            if (prefab == null)
            {
                Debug.LogError(
                    $"[WarriorsEnemyDirector] '{source.name}' 의 네트워크 사본이 없습니다. " +
                    "WarriorsSceneSetup 을 다시 돌려 주세요.", this);
                return null;
            }

            // parent 는 쓰지 않는다. Fusion 이 스폰한 것을 다른 것의 자식으로 넣으면
            // 자리 복제가 부모 기준으로 꼬인다.
            NetworkObject made = Runner.Spawn(prefab, position, rotation);

            if (made == null) return null;

            // 죽어서 스스로 사라진 것들을 먼저 걷어낸다. 목록이 무한히 자라지 않게.
            spawned.RemoveAll(one => one == null || !one.IsValid);
            spawned.Add(made);

            return made.GetComponent<WarriorsTarget>();
        }

        private NetworkObject FindNetworked(string sourceName)
        {
            foreach (EnemyPair pair in enemies)
            {
                if (pair.networked == null || string.IsNullOrEmpty(pair.sourceName)) continue;

                // Instantiate 한 것은 이름 뒤에 "(Clone)" 이 붙는다. 앞부분만 본다.
                if (sourceName.StartsWith(pair.sourceName, StringComparison.Ordinal)) return pair.networked;
            }

            return null;
        }
    }
}
