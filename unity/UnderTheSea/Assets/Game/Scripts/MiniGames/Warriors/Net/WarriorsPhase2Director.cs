using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>촉수 한 자리의 지금 상태. 네 자리가 모두 이 모양으로 복제된다.</summary>
    public struct WarriorsTentacleSlot : INetworkStruct
    {
        /// <summary>
        /// 올릴 때마다 1씩 오른다.
        ///
        /// 같은 자리를 다시 쓰는 일이 잦아서, 값만 봐서는 "아직 그 촉수" 인지
        /// "같은 자리에 새로 올라온 촉수" 인지 구별할 수 없다. 이 번호가 바뀌면 새 촉수다.
        /// </summary>
        public int Serial;

        /// <summary>1 이면 올라와 있다.</summary>
        public int Up;

        /// <summary>약점. <see cref="WarriorsAttackDirection"/> 의 숫자값.</summary>
        public int Weakness;

        /// <summary>담당 플레이어 번호. 없으면 -1.</summary>
        public int Owner;

        /// <summary>남은 타격 수. 줄어들면 클라이언트가 휘청이는 연출을 낸다.</summary>
        public int HitsLeft;

        /// <summary>0 진행 중 · 1 잘렸다 · 2 시간을 놓쳐 반격당했다.</summary>
        public int Result;
    }

    /// <summary>
    /// 2페이즈 — 크라켄 촉수. **서버가 올리고 서버가 판정한다.**
    ///
    /// <code>
    ///   자리 고정   1페이즈가 끝나면 두 사람을 정해진 자리에 세우고 이동을 잠근다
    ///   좌우 분담   1P 는 왼쪽 두 팔(0·1), 2P 는 오른쪽 두 팔(2·3)
    ///   한 번에 하나 담당 팔이 하나 서 있고, 끝나면 잠깐 쉬었다 다음 팔이 선다
    ///   합산 목표   누가 잘랐든 팀 합계로 센다. 목표를 채우면 3페이즈로
    ///   쓰러진 사람 그쪽 팔은 아예 올라오지 않는다
    /// </code>
    ///
    /// <b>왜 <c>WarriorsKrakenBoss.BeginBattle()</c> 을 쓰지 않는가.</b>
    /// 그 안의 코루틴은 <b>정해진 5패턴</b>을 순서대로 돌리고, 놓친 팔의 반격을
    /// <b>플레이어 한 명</b>에게 몰아 넣는다. 2인 서버 권위와는 규칙이 다르다.
    /// 그래서 그 코루틴은 시작하지 않고, 촉수를 세우는 공개 함수들만 직접 쓴다.
    /// 약점 · 타격 수 · 반격 시간은 전부 <c>WarriorsTarget</c> 의 원래 규칙 그대로다.
    ///
    /// ⚠ <b>이벤트를 듣지 않고 틱마다 읽는다.</b> <c>Defeated</c> · <c>HitAccepted</c> 는
    ///    칼 히트박스(유니티 <c>Update</c>)에서 터진다. 거기서 <c>[Networked]</c> 값을 쓰면
    ///    틱 경계 밖이라 어느 틱에 들어갈지가 실행 순서에 달린다.
    ///    <c>WarriorsPlayerLife</c> 와 같은 이유로 읽어서 판단한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsPhase2Director : NetworkBehaviour
    {
        /// <summary>자리 수. 크라켄 프리팹의 촉수 개수와 같다.</summary>
        public const int SlotCount = 4;

        [Header("무대")]
        [Tooltip("촉수를 가진 크라켄. 씬에 놓인 것을 생성 도구가 꽂는다.")]
        [SerializeField] private WarriorsKrakenBoss kraken;

        [Tooltip("2페이즈에 두 사람이 설 자리. 0번이 1P, 1번이 2P.")]
        [SerializeField] private Transform[] stands = new Transform[WarriorsPlayers.Max];

        [Header("촉수 규칙 — 내일 같이 조정한다")]
        [Tooltip("촉수 하나를 베는 데 필요한 정타 수.")]
        [SerializeField, Min(1)] private int hitsPerTentacle = 1;

        [Tooltip("올라온 촉수에 답할 수 있는 시간(초). 넘기면 반격당한다.")]
        [SerializeField, Min(.5f)] private float answerSeconds = 4.5f;

        [Tooltip("촉수가 내려간 뒤 다음 촉수가 설 때까지의 틈(초).")]
        [SerializeField, Min(0f)] private float gapSeconds = .6f;

        [Tooltip("잘린 촉수가 쓰러지는 연출을 보여 주는 시간(초).")]
        [SerializeField, Min(0f)] private float defeatShowSeconds = .35f;

        [Tooltip("시간을 놓쳤을 때 담당 플레이어가 받는 피해.")]
        [SerializeField, Min(1)] private int missDamage = 9;

        // ------------------------------------------------------------
        // 복제되는 것
        // ------------------------------------------------------------

        /// <summary>네 자리의 상태. 모든 화면이 같은 값을 본다.</summary>
        [Networked, Capacity(SlotCount)]
        public NetworkArray<WarriorsTentacleSlot> Slots { get; }

        /// <summary>
        /// 촉수 무대가 서 있는가.
        ///
        /// ⚠ <b>이 값이 없으면 클라이언트에는 크라켄이 아예 안 보인다.</b>
        ///    크라켄 프리팹의 루트는 기본이 <c>비활성</c>이라 무대를 세우는 쪽에서
        ///    켜 줘야 한다. 그 일을 서버에서만 하면 서버에서만 보인다. 실측으로 확인했다.
        /// </summary>
        [Networked] public NetworkBool StageOpen { get; private set; }

        // ------------------------------------------------------------
        // 서버만 쓰는 것
        // ------------------------------------------------------------

        /// <summary>담당자 한 명의 진행 상황. 복제하지 않는다 — 결과만 Slots 에 담긴다.</summary>
        private struct Duty
        {
            public int Slot;            // 지금 맡고 있는 자리. 없으면 -1
            public TickTimer Answer;    // 답할 시간
            public TickTimer Retire;    // 잘린 뒤 내려가기까지
            public TickTimer Gap;       // 다음 촉수까지의 틈
            public int LastSlot;        // 직전에 쓴 자리. 같은 팔만 계속 나오지 않게
        }

        private readonly Duty[] duties = new Duty[WarriorsPlayers.Max];

        private WarriorsMatchState match;
        private IReadOnlyList<WarriorsTarget> tentacles;
        private bool stageOpen;
        private int nextSerial = 1;

        // 클라이언트가 "달라졌는가" 를 재는 자리. 복제되지 않는 각자의 기억이다.
        private readonly int[] shownSerial = new int[SlotCount];
        private readonly int[] shownHits = new int[SlotCount];
        private readonly bool[] shownUp = new bool[SlotCount];
        private bool shownStage;

        public override void Spawned()
        {
            match = WarriorsMatchState.Current;
            if (match == null) match = FindFirstObjectByType<WarriorsMatchState>(FindObjectsInactive.Include);

            if (kraken == null) kraken = FindFirstObjectByType<WarriorsKrakenBoss>(FindObjectsInactive.Include);
            if (kraken != null) tentacles = kraken.Tentacles;

            for (int i = 0; i < duties.Length; i++)
            {
                duties[i].Slot = -1;
                duties[i].LastSlot = -1;
            }

            // 담당이 아닌 촉수는 아예 맞지 않게 한다. **모든 PC 에서 단다** —
            // 서버는 타격을 확정할 때, 클라이언트는 어느 팔을 겨눌지 고를 때 쓴다.
            WarriorsNet.AttackOwnerFilter = AllowHit;

            if (!HasStateAuthority) return;

            if (kraken == null)
            {
                Debug.LogError("[WarriorsPhase2] 크라켄을 찾지 못했습니다. 2페이즈에 촉수가 나오지 않습니다.", this);
                return;
            }

            if (tentacles == null || tentacles.Count < SlotCount)
            {
                Debug.LogError(
                    $"[WarriorsPhase2] 촉수가 {tentacles?.Count ?? 0}개뿐입니다. {SlotCount}개가 필요합니다.", this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // 세션이 끝나면 걸개를 뗀다. 남겨 두면 같은 에디터에서 WarriorsTest 를
            // 열었을 때 죽은 상태를 읽고 공격이 통째로 막힌다.
            if (WarriorsNet.AttackOwnerFilter == AllowHit) WarriorsNet.AttackOwnerFilter = null;
        }

        /// <summary>
        /// **이 사람이 이 대상을 칠 자격이 있는가.**
        ///
        /// <code>
        ///   촉수가 아니다        참 — 1페이즈 공유 몬스터는 둘 다 벤다
        ///   올라와 있지 않다     참 — 막을 이유가 없다
        ///   담당이 없다          참
        ///   담당자가 휘둘렀다    참
        ///   남의 팔이다          거짓
        /// </code>
        ///
        /// ⚠ 이 규칙은 <b>2페이즈 촉수에만</b> 걸린다. 3페이즈는 각자 레인으로 따로 가른다.
        ///    누가 휘둘렀는지 알 수 없는 출처(플레이어가 아닌 피해)는 막지 않는다 —
        ///    모르는 것을 거짓으로 처리하면 기존 경로가 조용히 죽는다.
        /// </summary>
        private bool AllowHit(WarriorsTarget target, GameObject attacker)
        {
            int slot = SlotOf(target);
            if (slot < 0) return true;

            WarriorsTentacleSlot state = Slots.Get(slot);
            if (state.Up != 1 || state.Owner < 0) return true;

            if (attacker == null) return true;

            WarriorsPlayerLife life = attacker.GetComponentInParent<WarriorsPlayerLife>();
            if (life == null) return true;

            return life.PlayerIndex == state.Owner;
        }

        /// <summary>이 대상이 몇 번 촉수인가. 촉수가 아니면 -1.</summary>
        private int SlotOf(WarriorsTarget target)
        {
            if (target == null || tentacles == null) return -1;

            for (int i = 0; i < SlotCount && i < tentacles.Count; i++)
            {
                if (ReferenceEquals(tentacles[i], target)) return i;
            }

            return -1;
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || match == null) return;

            bool wantStage = match.Phase == WarriorsMatchPhase.Phase2;

            if (wantStage != stageOpen)
            {
                stageOpen = wantStage;

                if (wantStage) OpenStage();
                else CloseStage();
            }

            if (!stageOpen) return;

            for (int index = 0; index < duties.Length; index++)
            {
                DriveDuty(index);
            }
        }

        // ------------------------------------------------------------
        // 무대 열고 닫기
        // ------------------------------------------------------------

        /// <summary>2페이즈가 시작됐다. 자리를 잡아 세우고 촉수 무대를 연다.</summary>
        private void OpenStage()
        {
            if (kraken != null) kraken.PrepareNetworkTentacleStage();

            for (int i = 0; i < SlotCount; i++)
            {
                Slots.Set(i, new WarriorsTentacleSlot { Owner = -1 });
            }

            for (int i = 0; i < duties.Length; i++)
            {
                duties[i].Slot = -1;
                duties[i].LastSlot = -1;
                duties[i].Gap = TickTimer.None;
                duties[i].Retire = TickTimer.None;
                duties[i].Answer = TickTimer.None;
            }

            StageOpen = true;
            PlaceEveryone();

            Debug.Log("[WarriorsPhase2] 크라켄 촉수 단계를 열었습니다. 1P 는 왼쪽, 2P 는 오른쪽입니다.");
        }

        /// <summary>3페이즈로 넘어갔거나 판이 끝났다. 서 있는 촉수를 모두 내린다.</summary>
        private void CloseStage()
        {
            for (int i = 0; i < SlotCount; i++)
            {
                LowerSlot(i, 0);
            }

            for (int i = 0; i < duties.Length; i++)
            {
                duties[i].Slot = -1;
            }

            StageOpen = false;
            Debug.Log("[WarriorsPhase2] 촉수 단계를 닫았습니다.");
        }

        /// <summary>
        /// 두 사람을 정해진 자리에 세운다.
        ///
        /// 자리를 고정하는 이유는 담당이 좌우로 갈리기 때문이다. 걸어 다닐 수 있으면
        /// 한 사람이 반대편까지 가서 둘 다 처리해 버린다. 이동은
        /// <c>WarriorsMatchState.MovementLocked</c> 로 잠근다.
        /// </summary>
        private void PlaceEveryone()
        {
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life == null) continue;

                Transform stand = StandFor(life.PlayerIndex);
                if (stand == null) continue;

                WarriorsNetPlayerMover mover = life.GetComponent<WarriorsNetPlayerMover>();
                if (mover == null) continue;

                mover.PlaceAt(stand.position, stand.rotation);
                Debug.Log($"[WarriorsPhase2] {life.PlayerIndex + 1}P 를 촉수 자리에 세웠습니다.");
            }
        }

        /// <summary>이 사람이 설 자리. 3페이즈가 같은 자리를 그대로 쓴다.</summary>
        public Transform StandFor(int playerIndex)
        {
            if (stands == null || playerIndex < 0 || playerIndex >= stands.Length) return null;
            return stands[playerIndex];
        }

        // ------------------------------------------------------------
        // 한 사람의 촉수 돌리기
        // ------------------------------------------------------------

        /// <summary>
        /// 담당자 한 명분을 한 틱 진행한다.
        ///
        /// <code>
        ///   쓰러졌다        서 있던 팔을 내리고 더 올리지 않는다
        ///   팔이 없다       틈이 지났으면 새로 하나 올린다
        ///   팔이 잘렸다     팀 합계를 올리고, 쓰러지는 모습을 보여 준 뒤 내린다
        ///   시간이 지났다   담당자가 맞는다. 팔은 그냥 내려간다
        ///   맞는 중이다     남은 타격 수를 옮기고 약점을 다시 뽑는다
        /// </code>
        /// </summary>
        private void DriveDuty(int index)
        {
            WarriorsPlayerLife life = FindLife(index);
            bool away = life == null || life.IsDown;

            if (away)
            {
                // 쓰러진 사람의 촉수는 올리지 않는다. 서 있던 것은 조용히 내린다.
                if (duties[index].Slot >= 0)
                {
                    LowerSlot(duties[index].Slot, 0);
                    duties[index].Slot = -1;
                    Debug.Log($"[WarriorsPhase2] {index + 1}P 가 쓰러져 그쪽 촉수를 내렸습니다.");
                }

                return;
            }

            int slot = duties[index].Slot;

            if (slot < 0)
            {
                if (duties[index].Gap.IsRunning && !duties[index].Gap.Expired(Runner)) return;

                RaiseFor(index);
                return;
            }

            WarriorsTentacleSlot state = Slots.Get(slot);

            // 잘린 촉수가 쓰러지는 모습을 다 보여 줬다. 이제 내린다.
            if (state.Result != 0)
            {
                if (duties[index].Retire.IsRunning && !duties[index].Retire.Expired(Runner)) return;

                LowerSlot(slot, state.Result);
                duties[index].Slot = -1;
                duties[index].Gap = TickTimer.CreateFromSeconds(Runner, gapSeconds);
                return;
            }

            WarriorsTarget tentacle = TentacleAt(slot);

            if (tentacle == null)
            {
                duties[index].Slot = -1;
                return;
            }

            // 잘렸는가. 이벤트가 아니라 결과를 읽는다.
            if (tentacle.IsDefeated)
            {
                state.Result = 1;
                state.HitsLeft = 0;
                Slots.Set(slot, state);

                tentacle.SetAttackEnabled(false);
                tentacle.ClearStrikeWindow();

                duties[index].Retire = TickTimer.CreateFromSeconds(Runner, defeatShowSeconds);
                duties[index].Answer = TickTimer.None;

                if (match != null) match.ReportPhase2Hit();

                Debug.Log($"[WarriorsPhase2] {index + 1}P 가 {slot}번 촉수를 잘랐습니다.");
                return;
            }

            // 시간을 놓쳤는가. 담당자만 맞는다 — 원본처럼 한 사람에게 몰지 않는다.
            if (duties[index].Answer.Expired(Runner))
            {
                state.Result = 2;
                Slots.Set(slot, state);

                tentacle.SetAttackEnabled(false);
                tentacle.ClearStrikeWindow();

                WarriorsHealth health = life.GetComponent<WarriorsHealth>();
                if (health != null) health.TryApplyDamage(missDamage);

                duties[index].Retire = TickTimer.None;
                duties[index].Answer = TickTimer.None;

                LowerSlot(slot, 2);
                duties[index].Slot = -1;
                duties[index].Gap = TickTimer.CreateFromSeconds(Runner, gapSeconds);

                Debug.Log($"[WarriorsPhase2] {index + 1}P 가 {slot}번 촉수를 놓쳐 {missDamage} 피해를 입었습니다.");
                return;
            }

            // 정타가 들어갔다면 남은 타격 수가 줄어 있다. 그 값을 복제하고 약점을 다시 뽑는다.
            int left = tentacle.BossHitsRemaining;

            if (left >= state.HitsLeft) return;

            state.HitsLeft = left;

            if (left > 0)
            {
                // 살아남은 팔은 곧바로 **다른** 약점을 내건다. 원본과 같은 규칙이다.
                WarriorsAttackDirection next = (WarriorsAttackDirection)
                    (((int)tentacle.RequiredDirection + WarriorsRun.Range(1, 3)) % 3);

                tentacle.ConfigureRequiredDirection(next);
                state.Weakness = (int)next;
            }

            Slots.Set(slot, state);
        }

        /// <summary>담당자 몫으로 촉수 하나를 세운다.</summary>
        private void RaiseFor(int index)
        {
            int slot = PickSlot(index);
            WarriorsTarget tentacle = TentacleAt(slot);

            if (tentacle == null) return;

            WarriorsAttackDirection weakness = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);

            tentacle.gameObject.SetActive(true);
            tentacle.ConfigureAsBossPart(hitsPerTentacle);
            tentacle.ConfigureRequiredDirection(weakness);
            tentacle.SetAttackEnabled(true);
            tentacle.BeginStrikeWindow(answerSeconds);

            Slots.Set(slot, new WarriorsTentacleSlot
            {
                Serial = nextSerial++,
                Up = 1,
                Weakness = (int)weakness,
                Owner = index,
                HitsLeft = hitsPerTentacle,
                Result = 0,
            });

            duties[index].Slot = slot;
            duties[index].LastSlot = slot;
            duties[index].Answer = TickTimer.CreateFromSeconds(Runner, answerSeconds);
            duties[index].Retire = TickTimer.None;
            duties[index].Gap = TickTimer.None;
        }

        /// <summary>
        /// 담당 구역에서 자리 하나를 고른다.
        ///
        /// 1P 는 0·1번(왼쪽), 2P 는 2·3번(오른쪽)이다. 크라켄 프리팹의 촉수 배열이
        /// 이미 왼쪽부터 오른쪽 순서라 절반으로 자르면 그대로 좌우가 된다.
        /// 직전에 쓴 팔은 피해서 같은 자리만 계속 나오지 않게 한다.
        /// </summary>
        private int PickSlot(int index)
        {
            int half = SlotCount / 2;
            int first = Mathf.Clamp(index, 0, 1) * half;
            int pick = first + WarriorsRun.Range(0, half);

            if (pick == duties[index].LastSlot) pick = first + (pick - first + 1) % half;

            return pick;
        }

        /// <summary>촉수 하나를 내린다. <paramref name="result"/> 는 클라이언트가 낼 연출이다.</summary>
        private void LowerSlot(int slot, int result)
        {
            WarriorsTarget tentacle = TentacleAt(slot);

            if (tentacle != null)
            {
                tentacle.SetAttackEnabled(false);
                tentacle.ClearStrikeWindow();
                tentacle.gameObject.SetActive(false);
            }

            WarriorsTentacleSlot state = Slots.Get(slot);
            state.Up = 0;
            state.Owner = -1;
            state.HitsLeft = 0;
            state.Result = result;
            Slots.Set(slot, state);
        }

        private WarriorsTarget TentacleAt(int slot)
        {
            if (tentacles == null || slot < 0 || slot >= tentacles.Count) return null;
            return tentacles[slot];
        }

        private WarriorsPlayerLife FindLife(int index)
        {
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life != null && life.PlayerIndex == index) return life;
            }

            return null;
        }

        // ------------------------------------------------------------
        // 표시 — 클라이언트는 복제된 값만 보고 같은 무대를 그린다
        // ------------------------------------------------------------

        public override void Render()
        {
            // 서버는 위에서 진짜 촉수를 이미 움직였다. 여기서 또 건드리면 서로 싸운다.
            if (HasStateAuthority || tentacles == null) return;

            // 무대를 딱 한 번 세운다. 이 호출이 크라켄 루트를 켜고 머리를 올린다 —
            // 이게 없으면 촉수를 켜도 부모가 꺼져 있어 아무것도 그려지지 않는다.
            if (StageOpen && !shownStage && kraken != null)
            {
                shownStage = true;
                kraken.PrepareNetworkTentacleStage();
            }
            else if (!StageOpen)
            {
                shownStage = false;
            }

            for (int slot = 0; slot < SlotCount && slot < tentacles.Count; slot++)
            {
                ShowSlot(slot, Slots.Get(slot));
            }
        }

        private void ShowSlot(int slot, WarriorsTentacleSlot state)
        {
            WarriorsTarget tentacle = TentacleAt(slot);
            if (tentacle == null) return;

            bool up = state.Up == 1;

            // 번호가 바뀌었으면 같은 자리라도 새 촉수다. 무대를 다시 세운다.
            if (state.Serial != shownSerial[slot])
            {
                shownSerial[slot] = state.Serial;
                shownHits[slot] = state.HitsLeft;

                if (up)
                {
                    // 서버가 한 것과 같은 준비를 한다. 판정은 어차피 서버만 하지만,
                    // 약점 표시기 · 히트박스 · 보스용 연출이 이 호출에서 붙는다.
                    tentacle.gameObject.SetActive(true);
                    tentacle.ConfigureAsBossPart(Mathf.Max(1, state.HitsLeft));
                    tentacle.ConfigureRequiredDirection((WarriorsAttackDirection)state.Weakness);
                    tentacle.BeginStrikeWindow(answerSeconds);
                }
            }

            if (up)
            {
                shownUp[slot] = true;

                // 약점이 바뀌면 곧바로 반영한다. 표시기가 이 값을 매 프레임 읽는다.
                tentacle.ConfigureRequiredDirection((WarriorsAttackDirection)state.Weakness);

                if (state.HitsLeft < shownHits[slot])
                {
                    shownHits[slot] = state.HitsLeft;
                    if (kraken != null)
                    {
                        kraken.ShowTentacleHit(tentacle, (WarriorsAttackDirection)state.Weakness);
                    }
                }

                if (state.Result == 1) tentacle.ShowDefeated();

                return;
            }

            if (!shownUp[slot]) return;

            shownUp[slot] = false;
            tentacle.ClearStrikeWindow();
            tentacle.gameObject.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>
        /// 생성 도구가 무대 참조를 넣는다. **에디터 전용이다.**
        ///
        /// ⚠ <c>SerializedProperty</c> 로 넣지 않는다. 오브젝트 참조가 조용히
        ///    <c>fileID: 0</c> 으로 남는 일을 <c>WarriorsEnemyDirector</c> 에서 실측했다.
        /// </summary>
        public void EditorSetStage(WarriorsKrakenBoss boss, Transform[] standPoints)
        {
            kraken = boss;
            stands = standPoints;
        }
#endif
    }
}
