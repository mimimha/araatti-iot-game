using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 이 사람의 **목숨과 쓰러짐**. 서버가 정하고 모두가 본다.
    ///
    /// <b>왜 <c>WarriorsHealth</c> 를 고치지 않고 따로 두는가.</b>
    /// 그쪽은 HP 하나를 재는 부품이고 몬스터 · 촉수 · 플레이어가 함께 쓴다.
    /// "목숨 4개" 와 "쓰러지면 끝" 은 이 게임의 <b>플레이어 규칙</b>이라 성격이 다르다.
    /// 섞으면 몬스터도 목숨 4개를 갖게 된다.
    ///
    /// <code>
    ///   HP 가 0 이 되면        목숨을 하나 잃고 HP 를 되살린다
    ///   목숨이 0 이 되면       Down. 이 판에서 다시 일어나지 않는다
    ///   둘 다 Down 이면        전체 실패 (WarriorsMatchState 가 판정)
    /// </code>
    ///
    /// ⚠ 부활 · 재합류는 없다. 기획이 그렇게 정해져 있다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WarriorsHealth))]
    public sealed class WarriorsPlayerLife : NetworkBehaviour
    {
        [Header("목숨")]
        [Tooltip("이 판에서 쓸 수 있는 목숨. 다 쓰면 Down 이고 부활은 없다.")]
        [SerializeField, Min(1)] private int startingLives = 4;

        /// <summary>남은 목숨. 모든 화면에 같은 값이 보인다.</summary>
        [Networked]
        public int Lives { get; private set; }

        /// <summary>쓰러졌는가. 이동 · 공격 · 입력이 모두 막힌다.</summary>
        [Networked]
        public NetworkBool IsDown { get; private set; }

        /// <summary>몇 번 플레이어인가. 담당 촉수와 리듬 레인이 이 번호로 갈린다.</summary>
        [Networked]
        public int PlayerIndex { get; private set; }

        /// <summary>
        /// 지금 HP. **서버가 깎고 모두가 본다.**
        ///
        /// <c>WarriorsHealth</c> 는 복제되지 않는 일반 컴포넌트라 클라이언트에서는 늘 100 이었다.
        /// 그래서 HUD 가 "맞아도 안 닳는" 것처럼 보였다. 서버 값을 여기로 실어 보내고,
        /// 클라이언트는 <see cref="MirrorHealth"/> 로 자기 <c>WarriorsHealth</c> 에 같은 변화를 낸다 —
        /// 그러면 HUD · 피격 플래시 · 콤보 초기화가 원본 이벤트 경로 그대로 돈다.
        /// </summary>
        [Networked]
        public int Hp { get; private set; }

        /// <summary>최대 HP. 0 이면 서버가 아직 채우지 않은 것이라 읽지 않는다.</summary>
        [Networked]
        public int MaxHp { get; private set; }

        /// <summary>
        /// 이 목숨을 **읽어도 되는가.**
        ///
        /// ⚠ 세션에서 빠진 캐릭터는 스폰이 풀린 뒤에도 잠깐 씬에 남는다.
        ///    그동안 <c>FindObjectsByType</c> 에 잡히는데, 그 상태에서 <c>[Networked]</c> 값을
        ///    읽으면 <c>"Networked properties can only be accessed when Spawned() has been
        ///    called"</c> 예외가 난다. 실측으로 확인했다 — 두 사람이 나가는 순간
        ///    <c>EveryoneDown()</c> 이 매 틱 예외를 던졌다.
        /// </summary>
        public bool IsLive => Object != null && Object.IsValid;

        private WarriorsHealth health;
        private WarriorsPlayerCombat combat;

        public override void Spawned()
        {
            health = GetComponent<WarriorsHealth>();
            combat = GetComponent<WarriorsPlayerCombat>();

            SyncPlayerId();

            // 내 캐릭터의 HP 를 HUD 에 붙인다. HUD 는 원래 "처음 찾은 캐릭터" 를 읽어서
            // 두 사람이 있으면 상대의 HP 를 보여 주는 일이 있었다.
            if (HasInputAuthority)
            {
                WarriorsHudPresenter hud = FindFirstObjectByType<WarriorsHudPresenter>(FindObjectsInactive.Include);
                if (hud != null) hud.BindLocalHealth(health);
            }

            if (!HasStateAuthority)
            {
                return;
            }

            Lives = startingLives;
            IsDown = false;

            if (health != null)
            {
                Hp = health.CurrentHealth;
                MaxHp = health.MaxHealth;
            }
        }

        /// <summary>
        /// 복제된 번호를 <c>WarriorsPlayerCombat</c> 에도 넣는다. 모든 PC 에서.
        ///
        /// 프리팹에는 0 이 박혀 있어 두 사람이 모두 1P 로 등록됐고, HUD 의 "2P 적중" 줄이
        /// 늘 비어 있었다. 번호는 서버가 스폰 직후 정하므로 첫 스냅숏에 같이 온다.
        /// </summary>
        private void SyncPlayerId()
        {
            if (combat == null) combat = GetComponent<WarriorsPlayerCombat>();
            if (combat != null && combat.PlayerId != PlayerIndex) combat.ConfigurePlayerId(PlayerIndex);
        }

        /// <summary>
        /// **클라이언트가 서버 HP 를 자기 <c>WarriorsHealth</c> 에 그대로 옮긴다.**
        ///
        /// 깎였으면 그만큼 <c>TryApplyDamage</c> — 피격 플래시와 콤보 초기화가 원본 경로로 난다.
        /// 목숨을 잃고 다시 찼으면 <c>ResetHealth</c>. 회복이면 <c>Heal</c>.
        /// 판정은 전부 서버 것이고 이것은 화면용 사본이다.
        /// </summary>
        private void MirrorHealth()
        {
            int current = health.CurrentHealth;
            if (Hp == current) return;

            if (Hp < current)
            {
                // 무적 창을 무시한다 — 이미 서버가 그 판정을 거친 결과를 그대로 그리는 것이다.
                health.TryApplyDamage(current - Hp, ignoreCooldown: true);
                return;
            }

            if (current <= 0)
            {
                // 새 목숨. 가득 채운 뒤 서버 값까지 내린다(같은 틱에 이미 맞았을 수 있다).
                health.ResetHealth();
                if (Hp < health.MaxHealth) health.TryApplyDamage(health.MaxHealth - Hp, ignoreCooldown: true);
                return;
            }

            health.Heal(Hp - current);
        }

        public override void Render()
        {
            SyncPlayerId();

            // 쓰러진 모습은 **모든 화면**이 그린다. 서버도 지나가지만 화면이 없어 아무 일이 없다.
            SyncDownPose();

            if (HasStateAuthority || health == null || MaxHp <= 0) return;

            MirrorHealth();
        }

        /// <summary>
        /// **쓰러졌으면 실제로 바닥에 눕는다.**
        ///
        /// 예전에는 <see cref="IsDown"/> 이 규칙에만 쓰이고 화면에는 아무 표시가 없었다.
        /// HP 가 0 이고 조작도 안 되는데 <b>선 채로 가만히 있어</b> 멈춘 것처럼 보였다.
        ///
        /// <b>복제된 값을 보고 각 화면이 그린다.</b> 서버에서 애니메이션을 재생해 봐야
        /// 데디케이티드 서버에는 보여 줄 화면이 없다 — 이 프로젝트가 여러 번 겪은 함정이다.
        ///
        /// 되돌아가는 길도 같은 값이 연다. [다시 하기] 로 <see cref="IsDown"/> 이 풀리면
        /// 애니메이터가 이동 상태로 나간다. 그래서 트리거가 아니라 bool 이다.
        /// </summary>
        private void SyncDownPose()
        {
            if (downAnimator == null)
            {
                downAnimator = GetComponent<Animator>();
                if (downAnimator == null) return;
            }

            bool down = IsDown;
            if (down == shownDown) return;

            shownDown = down;
            downAnimator.SetBool(DownHash, down);
        }

        private Animator downAnimator;

        private bool shownDown;

        private static readonly int DownHash = Animator.StringToHash("IsDown");

        /// <summary>
        /// **새 판을 위해 되살린다.** 결과 화면의 [다시 하기] 를 받은 서버가 부른다.
        ///
        /// 판 안에서의 부활이 아니다 — 끝난 판을 처음부터 다시 할 때만 쓴다.
        /// 이걸 빼면 새 판이 시작하자마자 둘 다 <see cref="IsDown"/> 이라 그 자리에서 다시 실패한다.
        /// </summary>
        public void ResetForNewMatch()
        {
            if (!HasStateAuthority) return;

            if (health == null) health = GetComponent<WarriorsHealth>();

            if (health != null)
            {
                health.ResetHealth();
                Hp = health.CurrentHealth;
                MaxHp = health.MaxHealth;
            }

            Lives = startingLives;
            IsDown = false;
        }

        /// <summary>서버가 이 사람의 번호를 정한다. 스포너가 부른다.</summary>
        public void AssignIndex(int index)
        {
            if (!HasStateAuthority) return;
            PlayerIndex = index;
        }

        /// <summary>
        /// HP 가 0 이 됐는지 **틱마다 본다.** 서버에서만 돈다.
        ///
        /// ⚠ <c>WarriorsHealth.Died</c> 이벤트를 듣지 않는다.
        ///    그 이벤트는 몬스터의 <c>Update</c> 에서 터지는데, 거기서 <c>[Networked]</c> 값을
        ///    쓰면 <b>틱 경계 밖</b>에서 상태가 바뀐다. Fusion 의 상태는 틱 단위 스냅샷이라
        ///    그런 쓰기는 어느 틱에 들어갈지가 실행 순서에 달린다.
        ///    읽어서 판단하면 그 문제가 통째로 사라진다.
        ///
        /// <c>TryApplyDamage</c> 는 이미 죽은 상태에서 다시 들어오지 않으므로,
        /// 목숨은 <b>한 번 죽을 때 정확히 하나</b>만 줄어든다.
        /// </summary>
        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || health == null) return;

            // 서버 HP 를 매 틱 실어 보낸다. Down 이어도 0 이 보여야 한다.
            int before = Hp;

            Hp = health.CurrentHealth;
            MaxHp = health.MaxHealth;

            // **받은 피해를 남긴다.** 1라운드에는 피해 로그가 아예 없어서, 맞고 있는지
            // 안 맞고 있는지 로그만 봐서는 구분할 수 없었다("무적 같다" 의 원인).
            // 목숨이 줄며 HP 가 다시 찬 경우는 늘어난 것이므로 줄었을 때만 센다.
            if (before > 0 && Hp < before) WarriorsTelemetry.DamageTaken(PlayerIndex, before - Hp);

            // 한 번 쓰러지면 그 판에서는 끝이다. 더 볼 것이 없다.
            if (IsDown) return;

            if (!health.IsDead) return;

            // ⚠ **개발자 모드의 무적.** 죽을 때마다 다시 세우고 목숨은 건드리지 않는다.
            //    판이 되돌아갈 때 서버가 이 값을 끄므로 다음 판까지 따라가지 않는다.
            if (WarriorsMatchState.Current != null && WarriorsMatchState.Current.Invincible)
            {
                health.ResetHealth();
                Hp = health.CurrentHealth;
                return;
            }

            Lives = Mathf.Max(0, Lives - 1);

            if (Lives > 0)
            {
                // 아직 목숨이 남았다. 다시 세운다.
                health.ResetHealth();
                Hp = health.CurrentHealth;
                Debug.Log($"[WarriorsLife] {Object.InputAuthority} 목숨 {Lives}개 남음", this);
                return;
            }

            IsDown = true;

            WarriorsTelemetry.Down(PlayerIndex);
            Debug.Log($"[WarriorsLife] {Object.InputAuthority} 쓰러졌습니다. 이 판에서는 다시 일어나지 않습니다.", this);
        }

        // ------------------------------------------------------------
        // 쓰러짐 — **되살아나지 않는다**
        // ------------------------------------------------------------
        //
        // ⚠ 이 게임에는 부활도 구조도 없다. HP 가 0 이 되면 그 사람의 판은 거기서 끝이다.
        //   한때 "동료가 12초 안에 표시 공격 3회로 되살린다" 는 구조 시스템이 있었지만
        //   규칙에서 빠졌다. 되살리는 코드도, "1P 를 살리세요" 같은 안내도 다시 넣지 마라.
        //   남은 사람은 그대로 계속하고, 둘 다 쓰러지면 실패다.
    }
}
