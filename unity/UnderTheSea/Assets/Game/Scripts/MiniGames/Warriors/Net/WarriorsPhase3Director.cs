using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>떨어지는 노트 하나. <c>Serial</c> 이 0 이면 빈 칸이다.</summary>
    public struct WarriorsNoteSlot : INetworkStruct
    {
        /// <summary>올릴 때마다 1씩 오른다. 0 은 빈 칸이라는 뜻이다.</summary>
        public int Serial;

        /// <summary>어느 레인인가. 플레이어 번호와 같다.</summary>
        public int Lane;

        /// <summary>어떤 공격으로 받아야 하는가. <see cref="WarriorsAttackDirection"/> 의 숫자값.</summary>
        public int Type;

        /// <summary>판정선에 닿는 틱.</summary>
        public int DueTick;

        /// <summary>0 떨어지는 중 · 1 성공 · 2 실패.</summary>
        public int State;
    }

    /// <summary>
    /// 3페이즈 — 최후의 리듬. **서버가 노트를 내고 서버가 판정한다.**
    ///
    /// <code>
    ///   자리 고정   2페이즈와 같은 자리에 선 채로 끝까지 간다
    ///   레인 분리   1P 노트는 1P 만, 2P 노트는 2P 만 받는다
    ///   합산 목표   성공한 노트를 팀 합계로 센다. 채우면 전체 클리어
    ///   쓰러진 사람 그 레인에는 노트가 나오지 않는다
    ///   놓치면      그 사람만 맞는다
    /// </code>
    ///
    /// <b>왜 <c>WarriorsRhythmBattle</c> 을 쓰지 않는가.</b>
    /// 그쪽은 <c>Time.time</c> 과 코루틴으로 도는 <b>혼자 플레이용</b> 루프다.
    /// 레인 수를 <c>WarriorsGameFlow</c> 에서 받아 오는데 그 부품이 꺼져 있어 늘 1이 되고,
    /// 반격 피해도 <b>플레이어 한 명</b>에게만 들어간다. 무엇보다 <c>Time.time</c> 은
    /// PC 마다 다르게 흐른다 — 같은 노트가 서로 다른 순간에 판정된다.
    ///
    /// <b>그래서 시간을 틱으로 센다.</b> <c>DueTick</c> 은 모든 PC 에서 같은 숫자이고,
    /// 입력도 틱에 맞춰 들어오므로 "언제 눌렀는가" 가 서버에서 정확히 하나로 정해진다.
    /// 화면은 각자 자기 틱으로 노트를 내려 그리기만 한다.
    ///
    /// ⚠ <b>혼자 플레이는 그대로 둔다.</b> <c>WarriorsRhythmBattle</c> 은 한 줄도 고치지 않았다.
    ///    <c>WarriorsTest.unity</c> 의 리듬 라운드는 예전 규칙(크라켄 HP 0 으로 종료) 그대로다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsPhase3Director : NetworkBehaviour
    {
        /// <summary>한 번에 하늘에 떠 있을 수 있는 노트 수. 레인당 절반씩 쓴다.</summary>
        public const int NoteCapacity = 12;

        [Header("무대")]
        [Tooltip("최종 형태를 보여 줄 크라켄. 생성 도구가 꽂는다.")]
        [SerializeField] private WarriorsKrakenBoss kraken;

        [Tooltip("3페이즈에 두 사람이 설 자리. 비워 두면 2페이즈 자리를 그대로 쓴다.")]
        [SerializeField] private Transform[] stands = new Transform[WarriorsPlayers.Max];

        [Header("리듬 규칙 — 내일 같이 조정한다")]
        [Tooltip("노트가 판정선까지 내려오는 데 걸리는 시간(초).")]
        [SerializeField, Min(.5f)] private float noteTravelSeconds = 2.2f;

        [Tooltip("같은 레인에서 노트와 노트 사이 간격(초).")]
        [SerializeField, Min(.3f)] private float noteInterval = .9f;

        [Tooltip("정타로 인정되는 시간 폭(초). 이 밖이면 빗나간 것으로 본다.")]
        [SerializeField, Range(.1f, .6f)] private float goodWindow = .2f;

        [Tooltip("노트를 놓쳤을 때 그 사람이 받는 피해.")]
        [SerializeField, Min(1)] private int missDamage = 6;

        [Tooltip("판정이 끝난 노트를 화면에서 치우기까지의 여유(초).")]
        [SerializeField, Min(0f)] private float clearSeconds = .35f;

        // ------------------------------------------------------------
        // 복제되는 것
        // ------------------------------------------------------------

        /// <summary>하늘에 떠 있는 노트들. 모든 화면이 같은 값을 본다.</summary>
        [Networked, Capacity(NoteCapacity)]
        public NetworkArray<WarriorsNoteSlot> Notes { get; }

        /// <summary>지금 3페이즈가 돌고 있는가. HUD 가 이 값으로 리듬 화면을 켠다.</summary>
        [Networked] public NetworkBool Running { get; private set; }

        /// <summary>
        /// 지금 판의 3페이즈 담당. 공격 입력이 여기로 들어온다.
        ///
        /// <c>WarriorsNetPlayerCombat</c> 이 사람마다 <c>GetInput</c> 으로 버튼을 읽는데,
        /// 이 부품은 씬 오브젝트라 입력 권한이 없어 스스로 읽을 수 없다. 그래서 받는다.
        /// </summary>
        public static WarriorsPhase3Director Current { get; private set; }

        private WarriorsMatchState match;
        private WarriorsPhase2Director phase2;
        private WarriorsHudPresenter hud;

        private readonly int[] nextSpawnTick = new int[WarriorsPlayers.Max];
        private bool stageOpen;
        private int nextSerial = 1;

        private readonly List<WarriorsRhythmNoteView> shownNotes = new();
        private bool shownStage;

        public override void Spawned()
        {
            Current = this;

            match = WarriorsMatchState.Current;
            if (match == null) match = FindFirstObjectByType<WarriorsMatchState>(FindObjectsInactive.Include);

            phase2 = GetComponent<WarriorsPhase2Director>();
            hud = FindFirstObjectByType<WarriorsHudPresenter>(FindObjectsInactive.Include);

            if (kraken == null) kraken = FindFirstObjectByType<WarriorsKrakenBoss>(FindObjectsInactive.Include);

            if (!HasStateAuthority) return;

            if (kraken == null)
            {
                Debug.LogError("[WarriorsPhase3] 크라켄을 찾지 못했습니다. 최종 형태가 보이지 않습니다.", this);
            }
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this) Current = null;
            if (hud != null) hud.NetworkRhythmActive = false;
        }

        // ------------------------------------------------------------
        // 규칙 — 서버만 돈다
        // ------------------------------------------------------------

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || match == null) return;

            bool wantStage = match.Phase == WarriorsMatchPhase.Phase3;

            if (wantStage != stageOpen)
            {
                stageOpen = wantStage;

                if (wantStage) OpenStage();
                else CloseStage();
            }

            if (!stageOpen) return;

            RetireFinishedNotes();
            MissOverdueNotes();
            SpawnDueNotes();
        }

        private void OpenStage()
        {
            // 촉수 무대를 걷고 최종 형태를 세운다. 이 함수는 원래 공개돼 있어 그대로 쓴다.
            if (kraken != null)
            {
                kraken.gameObject.SetActive(true);
                kraken.ShowFinalForm();
            }

            for (int i = 0; i < NoteCapacity; i++) Notes.Set(i, default);

            int openAt = Runner.Tick + TicksFor(noteTravelSeconds);

            for (int lane = 0; lane < nextSpawnTick.Length; lane++)
            {
                // 레인마다 반 박자씩 어긋나게 연다. 두 사람 노트가 겹쳐 떨어지면
                // 서로의 레인을 자기 것으로 착각한다.
                nextSpawnTick[lane] = openAt + lane * TicksFor(noteInterval * .5f);
            }

            Running = true;
            PlaceEveryone();

            Debug.Log("[WarriorsPhase3] 최후의 리듬을 시작합니다. 레인은 사람마다 따로입니다.");
        }

        private void CloseStage()
        {
            for (int i = 0; i < NoteCapacity; i++) Notes.Set(i, default);

            Running = false;
            Debug.Log("[WarriorsPhase3] 리듬 단계를 닫았습니다.");
        }

        /// <summary>판정이 끝나고 잠깐 지난 노트를 빈 칸으로 되돌린다.</summary>
        private void RetireFinishedNotes()
        {
            int clearTicks = TicksFor(clearSeconds);

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0 || note.State == 0) continue;
                if (Runner.Tick <= note.DueTick + clearTicks) continue;

                Notes.Set(i, default);
            }
        }

        /// <summary>판정선을 지나쳐 버린 노트. **그 레인의 사람만** 맞는다.</summary>
        private void MissOverdueNotes()
        {
            int goodTicks = TicksFor(goodWindow);

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0 || note.State != 0) continue;
                if (Runner.Tick <= note.DueTick + goodTicks) continue;

                note.State = 2;
                Notes.Set(i, note);

                Punish(note.Lane);
            }
        }

        /// <summary>살아 있는 레인에 차례가 되면 노트를 하나 띄운다.</summary>
        private void SpawnDueNotes()
        {
            int travelTicks = TicksFor(noteTravelSeconds);
            int intervalTicks = TicksFor(noteInterval);

            for (int lane = 0; lane < nextSpawnTick.Length; lane++)
            {
                WarriorsPlayerLife life = FindLife(lane);

                // 쓰러진 사람 레인에는 노트가 나오지 않는다. 남은 사람이 목표를 채운다.
                if (life == null || life.IsDown)
                {
                    nextSpawnTick[lane] = Runner.Tick + intervalTicks;
                    continue;
                }

                if (Runner.Tick < nextSpawnTick[lane]) continue;

                int slot = FreeSlot();

                if (slot < 0)
                {
                    // 하늘이 꽉 찼다. 한 박자 쉬었다 다시 본다.
                    nextSpawnTick[lane] = Runner.Tick + intervalTicks;
                    continue;
                }

                Notes.Set(slot, new WarriorsNoteSlot
                {
                    Serial = nextSerial++,
                    Lane = lane,
                    Type = WarriorsRun.Range(0, 3),
                    DueTick = Runner.Tick + travelTicks,
                    State = 0,
                });

                nextSpawnTick[lane] = Runner.Tick + intervalTicks;
            }
        }

        /// <summary>
        /// **이 사람이 지금 칼을 휘둘렀다.** <c>WarriorsNetPlayerCombat</c> 이 서버에서 부른다.
        ///
        /// 자기 레인에서 판정선에 가장 가까운 노트 하나만 본다. 남의 레인은 건드리지 않는다 —
        /// 이것이 "각자 레인" 규칙이 실제로 지켜지는 지점이다.
        ///
        /// <code>
        ///   가까운 노트가 없다   아무 일도 없다. 헛스윙에 벌을 주지 않는다
        ///   모양이 맞다          성공. 팀 합계가 하나 오른다
        ///   모양이 틀리다        실패. 그 사람만 맞는다
        /// </code>
        /// </summary>
        public void ReportSwing(int lane, WarriorsAttackDirection direction)
        {
            if (!HasStateAuthority || !stageOpen) return;

            int goodTicks = TicksFor(goodWindow);
            int best = -1;
            int bestDelta = int.MaxValue;

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);

                if (note.Serial == 0 || note.State != 0 || note.Lane != lane) continue;

                int delta = Mathf.Abs(Runner.Tick - note.DueTick);
                if (delta >= bestDelta) continue;

                bestDelta = delta;
                best = i;
            }

            if (best < 0 || bestDelta > goodTicks) return;

            WarriorsNoteSlot hit = Notes.Get(best);
            bool correct = hit.Type == (int)direction;

            hit.State = correct ? 1 : 2;
            Notes.Set(best, hit);

            if (!correct)
            {
                Punish(lane);
                return;
            }

            if (match != null) match.ReportPhase3Hit();
        }

        /// <summary>노트를 놓친 사람에게만 피해를 준다.</summary>
        private void Punish(int lane)
        {
            WarriorsPlayerLife life = FindLife(lane);
            if (life == null || life.IsDown) return;

            WarriorsHealth health = life.GetComponent<WarriorsHealth>();
            if (health != null) health.TryApplyDamage(missDamage);
        }

        /// <summary>두 사람을 자리에 세운다. 3페이즈 자리가 비어 있으면 2페이즈 자리를 쓴다.</summary>
        private void PlaceEveryone()
        {
            foreach (WarriorsPlayerLife life in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (life == null) continue;

                Transform stand = StandFor(life.PlayerIndex);
                if (stand == null) continue;

                WarriorsNetPlayerMover mover = life.GetComponent<WarriorsNetPlayerMover>();
                if (mover != null) mover.PlaceAt(stand.position, stand.rotation);
            }
        }

        private Transform StandFor(int playerIndex)
        {
            if (stands != null && playerIndex >= 0 && playerIndex < stands.Length && stands[playerIndex] != null)
            {
                return stands[playerIndex];
            }

            return phase2 != null ? phase2.StandFor(playerIndex) : null;
        }

        private int FreeSlot()
        {
            for (int i = 0; i < NoteCapacity; i++)
                if (Notes.Get(i).Serial == 0) return i;

            return -1;
        }

        private int TicksFor(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds / Runner.DeltaTime));
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
        // 표시 — 모든 화면이 같은 노트를 같은 자리에 그린다
        // ------------------------------------------------------------

        /// <summary>
        /// 복제된 노트를 HUD 가 그릴 모양으로 옮긴다.
        ///
        /// <c>Travel</c> 은 0 에서 1 로 가는 값이고 1 이 판정선이다. 남은 틱을 전체 낙하
        /// 틱으로 나누면 그대로 나온다. 클라이언트의 틱은 서버보다 조금 앞서 있지만
        /// 그 차이는 노트 하나 크기보다 훨씬 작다.
        /// </summary>
        public override void Render()
        {
            if (hud == null) return;

            if (!Running)
            {
                shownStage = false;

                if (hud.NetworkRhythmActive)
                {
                    hud.NetworkRhythmActive = false;
                    hud.NetworkRhythmNotes.Clear();
                }

                return;
            }

            // 최종 형태를 딱 한 번 세운다. 서버에서만 세우면 서버에서만 보인다 —
            // 크라켄 프리팹의 루트가 기본 비활성이라 켜 주는 쪽이 있어야 한다.
            if (!shownStage && !HasStateAuthority && kraken != null)
            {
                shownStage = true;
                kraken.gameObject.SetActive(true);
                kraken.ShowFinalForm();
            }

            int travelTicks = TicksFor(noteTravelSeconds);
            shownNotes.Clear();

            for (int i = 0; i < NoteCapacity; i++)
            {
                WarriorsNoteSlot note = Notes.Get(i);
                if (note.Serial == 0) continue;

                float travel = 1f - (note.DueTick - Runner.Tick) / (float)travelTicks;

                shownNotes.Add(new WarriorsRhythmNoteView(
                    (WarriorsAttackDirection)note.Type, note.Lane, travel, note.State == 1));
            }

            hud.NetworkRhythmActive = true;
            hud.NetworkRhythmNotes.Clear();
            hud.NetworkRhythmNotes.AddRange(shownNotes);

            if (match != null)
            {
                hud.NetworkRhythmProgress = match.Phase3Target <= 0
                    ? 0f
                    : match.Phase3Hits / (float)match.Phase3Target;
                hud.NetworkRhythmDetail = $"최후의 일격   {match.Phase3Hits} / {match.Phase3Target}";
                hud.NetworkRhythmLives = match.LivesLine();
            }
        }

#if UNITY_EDITOR
        /// <summary>생성 도구가 무대 참조를 넣는다. **에디터 전용이다.**</summary>
        public void EditorSetStage(WarriorsKrakenBoss boss, Transform[] standPoints)
        {
            kraken = boss;
            stands = standPoints;
        }
#endif
    }
}
