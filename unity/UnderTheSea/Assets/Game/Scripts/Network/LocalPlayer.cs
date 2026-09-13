using System;
using Fusion;
using UnityEngine;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 이 프로세스에서 <b>내가 조종하는 캐릭터</b> 하나를 알려 주는 등록소.
    ///
    /// <b>왜 필요한가.</b>
    /// Lobby 는 원래 씬에 <c>JaeYoung</c> 이라는 캐릭터가 박혀 있다는 전제로 만들어졌다.
    /// 포털 같은 로컬 연출이 <c>GameObject.Find("JaeYoung")</c> 으로 그 캐릭터를 집어 왔다.
    /// Fusion 으로 바뀌면서 캐릭터는 <b>접속 후 서버가 스폰</b>하므로 그 방식이 통하지 않는다.
    ///   · 씬 로드 시점(Start)에는 아직 캐릭터가 없다
    ///   · 이름으로 찾으면 남의 캐릭터를 집을 수 있다
    ///   · 서버에는 "내 캐릭터" 라는 것이 아예 없다
    ///
    /// <b>책임.</b> 딱 하나다 — "내 캐릭터가 지금 있는가, 있다면 누구인가".
    /// 카메라 연출은 <see cref="LocalPlayerView"/>, 이동은 <see cref="NetworkPlayerMover"/>,
    /// 포털 판정은 각 포털이 알아서 한다. 여기에 그런 기능을 더 얹지 않는다.
    ///
    /// <b>수명.</b>
    ///   등록  <see cref="LocalPlayerView.Spawned"/> — InputAuthority 를 가진 피어에서만
    ///   해제  <see cref="LocalPlayerView.Despawned"/> — 퇴장 · 세션 종료 · 씬 재로드
    ///   초기화 플레이 모드 진입마다 (도메인 리로드를 꺼 둔 에디터 대비)
    ///
    /// <b>쓰는 쪽.</b> 두 가지 방법이 다 된다. 늦게 붙는 쪽도 놓치지 않는다.
    ///   · 지금 값을 묻는다   <see cref="Transform"/> 이 null 이면 아직 없는 것
    ///   · 알림을 받는다      <see cref="Registered"/> · <see cref="Unregistered"/>
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
    /// </summary>
    public static class LocalPlayer
    {
        private static NetworkObject current;

        /// <summary>내 캐릭터의 NetworkObject. 없으면 null.</summary>
        public static NetworkObject Object => current != null ? current : null;

        /// <summary>내 캐릭터의 Transform. 없으면 null. 거리 계산 · 카메라 추적에 쓴다.</summary>
        public static Transform Transform => current != null ? current.transform : null;

        /// <summary>내 캐릭터가 지금 존재하는가.</summary>
        public static bool Exists => current != null;

        /// <summary>내 캐릭터가 생겼을 때. 구독 시점이 늦었으면 <see cref="Transform"/> 을 직접 읽으면 된다.</summary>
        public static event Action<NetworkObject> Registered;

        /// <summary>내 캐릭터가 사라졌을 때. 들고 있던 참조를 반드시 버린다.</summary>
        public static event Action Unregistered;

        /// <summary>
        /// 내 캐릭터로 등록한다. <b>InputAuthority 를 가진 피어에서만</b> 부른다.
        /// 서버나 남의 캐릭터가 들어오면 막고 경고를 남긴다.
        /// </summary>
        internal static void Register(NetworkObject player)
        {
            if (player == null)
            {
                Debug.LogWarning("[LocalPlayer] null 을 등록하려 했습니다. 무시합니다.");
                return;
            }

            if (!player.HasInputAuthority)
            {
                // 서버(InputAuthority 없음)와 원격 플레이어를 걸러 낸다.
                Debug.LogWarning(
                    $"[LocalPlayer] InputAuthority 가 없는 오브젝트({player.Id})를 등록하려 했습니다. " +
                    "내 캐릭터가 아니므로 무시합니다.");
                return;
            }

            if (current != null && current != player)
            {
                // 이전 것이 아직 안 치워졌다는 뜻이다. 새 것으로 갈아끼우되 흔적을 남긴다.
                Debug.LogWarning(
                    $"[LocalPlayer] 이미 {current.Id} 가 등록돼 있는데 {player.Id} 가 들어왔습니다. " +
                    "새 쪽으로 바꿉니다.");
                Unregistered?.Invoke();
            }

            current = player;
            Debug.Log($"[LocalPlayer] 내 캐릭터 등록 — {player.Id} ({player.InputAuthority})");

            Registered?.Invoke(player);
        }

        /// <summary>
        /// 등록을 푼다. 지금 등록된 것과 다른 오브젝트가 해제를 요청하면 무시한다.
        /// (먼저 나간 캐릭터의 뒤늦은 Despawned 가 새 캐릭터를 지우지 않게)
        /// </summary>
        internal static void Unregister(NetworkObject player)
        {
            if (current == null)
            {
                return;
            }

            if (player != null && current != player)
            {
                return;
            }

            current = null;
            Debug.Log("[LocalPlayer] 내 캐릭터 등록 해제");

            Unregistered?.Invoke();
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때마다 비운다.
        ///
        /// 빌드에서는 프로세스마다 새로 시작하므로 의미가 없지만,
        /// 에디터에서 "Reload Domain" 을 꺼 두면 static 이 이전 플레이의 값을 그대로 들고 있다.
        /// 그러면 이미 파괴된 캐릭터를 가리키는 참조가 남는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            current = null;
            Registered = null;
            Unregistered = null;
        }
    }
}
