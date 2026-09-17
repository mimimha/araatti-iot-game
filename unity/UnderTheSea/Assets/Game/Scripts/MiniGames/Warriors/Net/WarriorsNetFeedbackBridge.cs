using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// **서버에서 일어난 일을 이 사람의 검 진동으로 옮긴다.**
    ///
    /// 왜 다리가 필요한가. 판정은 전부 서버가 하는데 진동은 <b>그 사람 손에 있는 장치</b>에서
    /// 나야 한다. 서버에서 <c>WarriorsIoTFeedbackHub.Request</c> 를 불러 봐야 서버 프로세스
    /// 안에서 끝난다 — 크라켄 리액션이 안 보였던 것과 같은 함정이다.
    ///
    /// 그래서 서버는 <b>번호만 올리고</b>, 이 부품이 각 화면에서 그 번호가 바뀌는 것을 보고
    /// 자기 허브에 넣는다. 번호로 비교하므로 <b>같은 사건이 두 번 울리지 않는다.</b>
    ///
    /// <code>
    ///   정답 공격        CorrectAttack     짧고 선명하게
    ///   오답 공격        WrongAttack       짧게 두 번
    ///   피격             PlayerDamaged     피해량에 따라 세게
    ///   동료 Down        MateDown          구조 요청
    ///   동시 공격 성공   ComboMilestone    양쪽 동시에 강하게
    ///   최종 결정타      FinalSwingReady   가장 센 단발
    /// </code>
    ///
    /// ⚠ <b>장치가 없어도 게임은 그대로 돌아간다.</b> 허브가 없으면 아무 일도 하지 않는다.
    ///    가짜 장치를 만들지 않는다 — 실제 전송 계층이 붙는 날 허브만 연결하면 된다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsNetFeedbackBridge : NetworkBehaviour
    {
        private WarriorsIoTFeedbackHub hub;
        private WarriorsPlayerLife life;

        // 마지막으로 울린 번호들. -1 은 "아직 한 번도 안 봤다" 라 첫 스냅숏에 몰아 울리지 않는다.
        private int shownHit = -1;
        private int shownFinish = -1;
        private int lastHp = -1;
        private bool shownMateDown;

        public override void Spawned()
        {
            life = GetComponent<WarriorsPlayerLife>();
            hub = FindFirstObjectByType<WarriorsIoTFeedbackHub>(FindObjectsInactive.Include);
        }

        /// <summary>
        /// 이 화면의 주인에게만 울린다. 남의 캐릭터 복사본이 내 검을 흔들면 안 된다.
        /// </summary>
        public override void Render()
        {
            if (hub == null || life == null || !HasInputAuthority) return;

            int id = life.PlayerIndex;

            WatchDamage(id);
            WatchMatch(id);
            WatchRhythm(id);
            WatchMate(id);
        }

        /// <summary>내 HP 가 줄었으면 피해량에 비례해 세게 울린다.</summary>
        private void WatchDamage(int id)
        {
            int hp = life.Hp;

            if (lastHp < 0) { lastHp = hp; return; }
            if (hp >= lastHp) { lastHp = hp; return; }

            int lost = lastHp - hp;
            lastHp = hp;

            float intensity = life.MaxHp > 0 ? Mathf.Clamp01(lost / (float)life.MaxHp * 3f) : .6f;
            hub.Request(id, WarriorsIoTFeedbackType.PlayerDamaged, Mathf.Max(.35f, intensity));
        }

        /// <summary>협동 게이지가 찼을 때 — 두 검이 같은 순간에 울린다.</summary>
        private void WatchMatch(int id)
        {
            WarriorsMatchState match = WarriorsMatchState.Current;
            if (match == null || match.Object == null || !match.Object.IsValid) return;

        }

        /// <summary>3라운드 — 정타와 묶음 피니시.</summary>
        private void WatchRhythm(int id)
        {
            WarriorsPhase3Director rhythm = WarriorsPhase3Director.Current;
            if (rhythm == null || rhythm.Object == null || !rhythm.Object.IsValid) return;

            if (shownHit < 0) { shownHit = rhythm.HitSerial; shownFinish = rhythm.FinishSerial; return; }

            if (rhythm.HitSerial != shownHit)
            {
                shownHit = rhythm.HitSerial;

                // 내 레인의 정타일 때만 내 검이 울린다.
                if (rhythm.HitLane == id)
                    hub.Request(id, WarriorsIoTFeedbackType.CorrectAttack, rhythm.HitStrength == 2 ? .85f : .5f);
            }

            if (rhythm.FinishSerial != shownFinish)
            {
                shownFinish = rhythm.FinishSerial;
                hub.Request(id, WarriorsIoTFeedbackType.ComboMilestone, rhythm.FinishKind == 2 ? 1f : .7f);
            }

        }

        /// <summary>동료가 쓰러졌다 — 구조하러 오라는 신호.</summary>
        private void WatchMate(int id)
        {
            bool mateDown = false;

            foreach (WarriorsPlayerLife mate in FindObjectsByType<WarriorsPlayerLife>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mate == null || mate == life || !mate.IsLive) continue;
                if (mate.IsDown) mateDown = true;
            }

            if (mateDown && !shownMateDown)
            {
                shownMateDown = true;
                hub.Request(id, WarriorsIoTFeedbackType.MateDown, .9f);
            }
            else if (!mateDown)
            {
                shownMateDown = false;
            }
        }
    }
}
