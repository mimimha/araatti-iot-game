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

            Current = this;

            Debug.Log($"[ShipCoopEventSync] 사건 {ordered.Length}개를 복제 대상으로 잡았습니다.");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this)
            {
                Current = null;
            }
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
                        Seed = step.Seed,
                        Extra = step.SyncExtra,
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
                    step.ShowStage((VoyageEvent.Stage)slot.Stage, slot.Elapsed, slot.Seed, slot.Extra);
                }
            }
        }

        // ------------------------------------------------------------
        // 🛠 개발자 모드 — **클라이언트에서 누른 키를 호스트가 대신 실행한다**
        //
        // ⚠ 규칙(사건 · HP · 침수 · 진행도)은 호스트만 계산한다. 그래서 클라이언트에서 개발자 모드 키를
        //    누르고 그 자리에서 값을 바꾸면, 그 프레임에만 잠깐 바뀌었다가 호스트가 보내는 진짜 값이
        //    도착해 **곧바로 덮어써 없던 일이 된다.** 눌러도 아무 일도 안 일어나는 것처럼 보였던 이유다.
        //
        //    그렇다고 "호스트에서만 눌러라" 로 막으면 쓸 수가 없다. 진짜 Dedicated Server 는 창도
        //    입력도 없어서 사람이 키를 누를 데가 아예 없다. 그래서 **클라이언트가 호스트에게 부탁**한다.
        //
        // ⚠ 이 RPC 는 `#if DEVELOPMENT_BUILD` 로 감싸지 않는다. Fusion 은 RPC 를 컴파일 시점에 번호로
        //    엮는데, 서버(Release)와 클라이언트(Development)의 RPC 목록이 다르면 번호가 어긋나
        //    **이 게임의 모든 RPC 가 엉뚱한 곳으로 간다.** 양쪽에 똑같이 있어야 한다.
        //    개발자 모드 UI(ShipCoopDevMode)도 이제 Release 빌드에 들어간다.
        // ------------------------------------------------------------

        /// <summary>개발자 모드가 호스트에 부탁하는 일. 숫자는 <c>ShipCoopDevMode</c> 와 짝이다.</summary>
        public enum DevCommand
        {
            FireEvent = 0,
            ToggleInvincible = 1,
            MoveProgress = 2,
            AddFlood = 3,
            ResetFlood = 4,
            Damage = 5,
            RepairFull = 6,
            TogglePause = 7,
            Restart = 8,
            ForceArrive = 9,
        }

        /// <summary>이 화면에서 개발자 모드가 부탁을 보낼 수 있는 곳. 없으면 네트워크 세션이 아니다.</summary>
        public static ShipCoopEventSync Current { get; private set; }

        /// <summary>사건 개수. 개발자 모드가 숫자키 범위를 정할 때 쓴다.</summary>
        public int EventCount => ordered.Length;

        /// <summary>사건 이름 (경로순). 개발자 모드 화면에 그대로 뜬다.</summary>
        public VoyageEvent EventAt(int index)
        {
            return index >= 0 && index < ordered.Length ? ordered[index] : null;
        }

        /// <summary>
        /// 클라이언트 → 호스트. 개발자 모드 키 하나를 호스트에서 실행한다.
        ///
        /// 호스트가 직접 누른 경우에는 RPC 를 거치지 않고 자기 자리에서 바로 한다. (ShipCoopDevMode)
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void Rpc_DevCommand(int command, int index, float value)
        {
            // ⚠ 서버가 -devmode 없이 떴으면 듣지 않는다. 클라이언트만 막으면 누구든 실행 인자
            //    한 줄로 열 수 있다. 이 RPC 자체는 #if 로 감싸지 않는다 — 위 주석 참고.
            if (!UnderTheSea.Core.DevMode.Enabled)
            {
                Debug.LogWarning(
                    $"[개발자 모드] 개발자 명령 {(DevCommand)command} 이 왔지만 이 서버는 " +
                    $"{UnderTheSea.Core.DevMode.Key} 없이 떴습니다. 실행하지 않습니다.");
                return;
            }

            RunDevCommand((DevCommand)command, index, value);
        }

        /// <summary>부탁받은 일을 실제로 한다. **호스트에서만 불린다.**</summary>
        public static void RunDevCommand(DevCommand command, int index, float value)
        {
            switch (command)
            {
                case DevCommand.FireEvent:
                {
                    VoyageEvent step = Current != null ? Current.EventAt(index) : null;

                    if (step == null)
                    {
                        return;
                    }

                    if (step.IsActive)
                    {
                        Debug.Log($"[개발자 모드] {step.WarningText} 는 이미 떠 있다.", step);
                        return;
                    }

                    step.Begin();
                    break;
                }

                case DevCommand.ToggleInvincible:
                {
                    ShipHealth health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
                    if (health == null) return;
                    health.Invincible = !health.Invincible;
                    Debug.Log($"[개발자 모드] 무적 {(health.Invincible ? "켜짐" : "꺼짐")}");
                    break;
                }

                case DevCommand.MoveProgress:
                {
                    ShipVoyage voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
                    if (voyage == null) return;
                    voyage.SetProgress01(voyage.Progress01 + value);
                    Debug.Log($"[개발자 모드] 진행도 {voyage.Progress01:P0}");
                    break;
                }

                case DevCommand.AddFlood:
                {
                    ShipFlooding flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
                    if (flooding == null) return;
                    flooding.Add(value);
                    break;
                }

                case DevCommand.ResetFlood:
                {
                    ShipFlooding flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
                    if (flooding == null) return;
                    flooding.ResetFlooding();
                    Debug.Log("[개발자 모드] 물을 전부 비웠다.");
                    break;
                }

                case DevCommand.Damage:
                {
                    ShipHealth health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
                    if (health == null) return;
                    health.TakeDamage(value, "개발자 모드");
                    break;
                }

                case DevCommand.RepairFull:
                {
                    ShipHealth health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
                    if (health == null) return;
                    health.Repair(health.MaxHp);
                    Debug.Log($"[개발자 모드] HP 를 채웠다. {health.CurrentHp:F0}");
                    break;
                }

                case DevCommand.TogglePause:
                {
                    EventScheduler scheduler = FindAnyObjectByType<EventScheduler>(FindObjectsInactive.Include);
                    if (scheduler == null) return;
                    scheduler.Paused = !scheduler.Paused;
                    Debug.Log($"[개발자 모드] 사건 뿌리기 {(scheduler.Paused ? "정지" : "재개")}");
                    break;
                }

                case DevCommand.Restart:
                {
                    ShipCoopGame game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
                    if (game == null) return;
                    game.RestartVoyage();
                    Debug.Log("[개발자 모드] 처음부터 다시 시작했다.");
                    break;
                }

                case DevCommand.ForceArrive:
                {
                    // 결과창을 직접 띄우지 않고 **진짜로 도착시킨다.** 다음 프레임 ShipCoopGame.CheckEnd 가
                    // 도착을 보고 성공으로 끝낸다. 점수 · 결과 보고 · 클라이언트 동기화가 실제 성공과 같게 돈다.
                    ShipCoopGame game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
                    ShipVoyage voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
                    if (game == null || voyage == null) return;

                    if (game.State != ShipCoopState.Sailing)
                    {
                        Debug.Log($"[개발자 모드] 항해 중이 아니라 도착시킬 수 없다. ({game.State})");
                        return;
                    }

                    voyage.SetProgress01(1f);
                    Debug.Log("[개발자 모드] 목적지로 옮겼다. 성공으로 끝난다.");
                    break;
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

        /// <summary>사건 한 칸. 단계 · 그 단계에서 지난 시간 · 이번 발생의 무작위 씨앗.</summary>
        private struct EventSlot : INetworkStruct
        {
            public int Stage;
            public float Elapsed;

            /// <summary>서버가 뽑은 주사위 씨앗. 같은 씨앗이면 암초의 쪽 · 바위 종류가 모든 화면에서 같다.</summary>
            public int Seed;

            /// <summary>사건이 하나 더 내주는 값(VoyageEvent.SyncExtra). 적선은 맞힌 발수.</summary>
            public int Extra;
        }
    }
}
