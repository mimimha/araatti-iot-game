using System;
using System.Linq;
using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 사건(암초 · 적선 · 스콜 · 큰 파도 · 선체 파손)이 **지금 어느 단계인지**를 복제한다.
    ///
    /// <b>왜 RPC 한 번으로 켜지 않는가.</b> "지금 암초가 시작됐다" 를 한 번 쏘면
    /// 그때 접속해 있던 사람만 받는다. 1분 뒤에 들어온 사람 화면에는 바위가 없는데
    /// 배는 깎인다. 그래서 <b>상태를 계속 들고 있다가</b> 늦게 온 사람에게도 보낸다.
    /// <c>[Networked]</c> 는 접속한 순간 현재 값을 그대로 준다.
    ///
    /// <b>순간적인 연출은 어떻게 하는가.</b> 여기서는 단계(<c>Stage</c>)만 보낸다.
    /// "예고 → 진행" 처럼 <b>단계가 바뀐 것을 보고</b> 각 사건의 <c>OnWarn</c> · <c>OnBegin</c> ·
    /// <c>OnCancel</c> 이 알아서 소리와 효과를 낸다. 이미 그렇게 나뉘어 있다.
    ///
    /// <b>자리는 어떻게 맞추는가.</b> 사건은 씬에 미리 놓여 있고 <c>NetworkObject</c> 가 아니다.
    /// 그래서 <b>계층 경로로 줄을 세워</b> 그 순서를 칸 번호로 쓴다. 모두 같은 씬 파일을
    /// 열기 때문에 순서가 같다. (<c>VoyageEvent.Active</c> 의 순서는 켜진 순서라 못 쓴다)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipCoopEventSync : NetworkBehaviour
    {
        /// <summary>씬에 둘 수 있는 사건의 최대 개수. 넘으면 넘은 것은 복제되지 않는다.</summary>
        public const int MaxEvents = 32;

        [Networked, Capacity(MaxEvents)]
        private NetworkArray<EventSlot> Slots { get; }

        /// <summary>경로순으로 줄 세운 사건들. 모든 컴퓨터에서 같은 순서다.</summary>
        private VoyageEvent[] ordered = Array.Empty<VoyageEvent>();

        public override void Spawned()
        {
            ordered = FindObjectsByType<VoyageEvent>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(PathOf, StringComparer.Ordinal)
                .ToArray();

            if (ordered.Length > MaxEvents)
            {
                Debug.LogError(
                    $"[ShipCoopEventSync] 사건이 {ordered.Length}개로 상한({MaxEvents})을 넘었습니다. " +
                    "넘은 것은 복제되지 않습니다. MaxEvents 를 늘려 주세요.", this);
            }

            Debug.Log($"[ShipCoopEventSync] 사건 {ordered.Length}개를 복제 대상으로 잡았습니다.");
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority)
            {
                return;
            }

            int count = Mathf.Min(ordered.Length, MaxEvents);

            for (int i = 0; i < count; i++)
            {
                VoyageEvent step = ordered[i];

                Slots.Set(i, step == null
                    ? default
                    : new EventSlot
                    {
                        Stage = (int)step.CurrentStage,
                        Elapsed = step.Elapsed,
                    });
            }
        }

        public override void Render()
        {
            // 서버는 자기가 적은 값을 도로 읽을 필요가 없다.
            if (HasStateAuthority)
            {
                return;
            }

            int count = Mathf.Min(ordered.Length, MaxEvents);

            for (int i = 0; i < count; i++)
            {
                VoyageEvent step = ordered[i];

                if (step != null)
                {
                    EventSlot slot = Slots.Get(i);
                    step.ShowStage((VoyageEvent.Stage)slot.Stage, slot.Elapsed);
                }
            }
        }

        /// <summary>루트부터의 이름 경로. 씬 이름은 넣지 않는다 — Fusion 이 씬 이름을 바꾼다.</summary>
        private static string PathOf(VoyageEvent step)
        {
            string path = step.name;

            for (Transform parent = step.transform.parent; parent != null; parent = parent.parent)
            {
                path = parent.name + "/" + path;
            }

            return path;
        }

        /// <summary>사건 한 칸. 단계와 그 단계에서 지난 시간.</summary>
        private struct EventSlot : INetworkStruct
        {
            public int Stage;
            public float Elapsed;
        }
    }
}
