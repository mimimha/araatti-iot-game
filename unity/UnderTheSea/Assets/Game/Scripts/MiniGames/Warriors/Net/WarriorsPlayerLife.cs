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

        private WarriorsHealth health;

        public override void Spawned()
        {
            health = GetComponent<WarriorsHealth>();

            if (!HasStateAuthority)
            {
                return;
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
            if (!HasStateAuthority || IsDown || health == null) return;
            if (!health.IsDead) return;

            Lives = Mathf.Max(0, Lives - 1);

            if (Lives > 0)
            {
                // 아직 목숨이 남았다. 다시 세운다.
                health.ResetHealth();
                Debug.Log($"[WarriorsLife] {Object.InputAuthority} 목숨 {Lives}개 남음", this);
                return;
            }

            IsDown = true;
            Debug.Log($"[WarriorsLife] {Object.InputAuthority} 쓰러졌습니다. 이 판에서는 다시 일어나지 않습니다.", this);
        }
    }
}
