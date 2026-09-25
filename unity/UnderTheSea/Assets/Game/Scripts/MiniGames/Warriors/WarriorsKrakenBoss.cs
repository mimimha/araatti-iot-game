using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsKrakenBoss : MonoBehaviour
    {
        [SerializeField] private WarriorsTarget[] tentacles;
        [SerializeField] private Transform body;
        [SerializeField] private GameObject weakPoint;
        [SerializeField] private GameObject finalFormRoot;
        [SerializeField] private GameObject tentaclePhaseHead;
        [SerializeField, Min(1)] private int finalFormMaxHealth = 100;
        [SerializeField, Min(1)] private int finalAttackDamage = 25;
        [SerializeField, Min(1)] private int rhythmHitDamage = 6;
        [SerializeField, Min(0f)] private float introProtectionSeconds = 1.25f;
        [SerializeField, Min(0f)] private float tentacleGaugeGain = 12.5f;
        [SerializeField, Min(0f)] private float finalHitGaugeGain = 20f;
        [SerializeField] private WarriorsTeamGauge teamGauge;
        public int RemainingTentacles { get; private set; }
        public int TentacleSuccesses { get; private set; }
        public int TentacleSuccessesRequired => PatternTentacleTotal;
        public int FinalFormHealth { get; private set; }
        public int FinalFormHealthPercent => Mathf.RoundToInt(FinalFormHealth / (float)finalFormMaxHealth * 100f);
        public WarriorsAttackDirection FinalWeakness { get; private set; }
        public float TeamGaugeNormalized => teamGauge != null ? teamGauge.Normalized : 0f;
        public bool IsTeamGaugeReady => teamGauge != null && teamGauge.IsReady;
        public event Action AllTentaclesDefeated;
        /// <summary>Raised when a tentacle pattern is cleared. True when a two tentacle
        /// pattern went down almost together, which is the co-op moment worth rewarding.</summary>
        public event Action<bool> PatternCleared;
        public event Action FinalFormDefeated;
        private bool tentaclePhaseComplete;
        private readonly HashSet<WarriorsTarget> reactingTentacles = new();
        private WarriorsKrakenTentacleDeformer tentacleDeformer;

        /// <summary>최종 형태(3라운드)의 다리 뼈를 돌리는 부품. 겉모습에 뼈가 없으면 null 이다.</summary>
        private WarriorsKrakenLegs legs;

        /// <summary>촉수 단계(2라운드) 머리의 다리 뼈.</summary>
        private WarriorsKrakenLegs headLegs;

        /// <summary>
        /// 지금 돌고 있는 리듬 타격 연출. 새 타격이 오면 이것을 끊는다.
        ///
        /// 겹쳐 돌면 뒤엣것이 <b>이미 밀린 자리</b>를 원래 자리로 기억했다가 거기로 되돌려 놓아,
        /// 연타할수록 크라켄이 조금씩 밀려난다.
        /// </summary>
        private Coroutine impactRoutine;
        private readonly List<WarriorsTarget> activePattern = new();
        private int lastSoloSlot = -1;
        private float firstTentacleDefeatTime = -1f;
        private float lastTentacleDefeatTime;
        [SerializeField, Min(.2f)] private float coopWindowSeconds = 1.4f;
        [SerializeField, Min(1.5f)] private float tentacleWindowSeconds = 4.5f;
        [SerializeField, Min(0f)] private float tentacleWindowPerExtra = 1.6f;
        [SerializeField, Min(1)] private int tentacleStrikeDamage = 9;

        /// <summary>
        /// ROUND 2 is a sequence of short patterns, not four tentacles standing up at once.
        /// Each entry says how many tentacles surface together and how many correct hits
        /// each one takes, so the round teaches, then complicates, then closes.
        ///
        /// Two tentacles is the co-op moment: a pair takes one each, a solo player takes
        /// them left to right.  Nothing here requires a second player.
        /// </summary>
        private readonly struct TentaclePattern
        {
            public readonly int Count;
            public readonly int HitsEach;
            public readonly float LeadIn;

            public TentaclePattern(int count, int hitsEach, float leadIn)
            {
                Count = count;
                HitsEach = hitsEach;
                LeadIn = leadIn;
            }
        }

        private static readonly TentaclePattern[] Patterns =
        {
            new TentaclePattern(1, 1, 0f),      // teach: read the weakness, answer it
            new TentaclePattern(1, 1, .6f),     // again, so the read becomes a habit
            new TentaclePattern(2, 1, .6f),     // two at once: one each for a pair, left then
                                                // right for a solo player
            new TentaclePattern(1, 1, .6f),     // back to one, as a breather
            new TentaclePattern(2, 1, .4f),     // finale: two again, and quicker
        };

        private static int PatternTentacleTotal
        {
            get
            {
                int total = 0;
                foreach (TentaclePattern pattern in Patterns) total += pattern.Count;
                return total;
            }
        }

        private void Awake()
        {
            if (teamGauge == null) teamGauge = GetComponent<WarriorsTeamGauge>();
            if (teamGauge == null) teamGauge = gameObject.AddComponent<WarriorsTeamGauge>();
            tentacleDeformer = GetComponent<WarriorsKrakenTentacleDeformer>();
            if (tentacleDeformer == null) tentacleDeformer = gameObject.AddComponent<WarriorsKrakenTentacleDeformer>();
            tentacleDeformer.Configure(tentaclePhaseHead != null ? tentaclePhaseHead.GetComponentInChildren<MeshFilter>(true) : null, tentacles);

            // 다리 뼈. 라운드마다 겉모습이 다르므로 **각각** 잡는다.
            // ⚠ 통째로 GetComponentInChildren 하면 둘 중 먼저 찾힌 하나만 잡혀 한쪽이 안 움직인다.
            legs = finalFormRoot != null ? finalFormRoot.GetComponentInChildren<WarriorsKrakenLegs>(true) : null;
            headLegs = tentaclePhaseHead != null ? tentaclePhaseHead.GetComponentInChildren<WarriorsKrakenLegs>(true) : null;

            // Tentacles are ordinary targets until a pattern claims them, so leaving them
            // switched on through ROUND 1 let a stray beach swing kill one before the
            // kraken fight had even started.
            foreach (WarriorsTarget tentacle in tentacles)
            {
                if (tentacle == null) continue;
                tentacle.SetAttackEnabled(false);
                tentacle.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// The boss is a prefab of its own, so the health it swings at has to be handed
        /// over by the flow rather than wired in the inspector.
        /// </summary>
        public void ConfigurePlayer(WarriorsHealth health) => playerHealth = health;

        // ------------------------------------------------------------
        // 네트워크 전환용 덧붙임 (Warriors 서버화 4단계)
        // 아래 셋은 **읽기와 연출만** 한다. 위의 규칙 코드는 하나도 건드리지 않았다.
        // ------------------------------------------------------------

        /// <summary>
        /// 촉수 목록. 왼쪽부터 오른쪽 순서다.
        ///
        /// 서버가 "P1 은 왼쪽, P2 는 오른쪽" 을 정하려면 어느 팔이 어느 쪽인지 알아야 한다.
        /// 자식에서 <c>WarriorsTarget</c> 을 긁어 모으면 약점 오브젝트까지 딸려 오고
        /// 순서도 보장되지 않아, 인스펙터에 꽂힌 이 배열을 그대로 내보낸다.
        /// </summary>
        public IReadOnlyList<WarriorsTarget> Tentacles => tentacles;

        /// <summary>
        /// 촉수 무대만 세운다. **패턴 코루틴은 돌리지 않는다.**
        ///
        /// <c>BeginBattle</c> 은 무대를 세우고 곧바로 정해진 5패턴을 순서대로 돌린다.
        /// 네트워크에서는 <b>서버가</b> 사람마다 따로 촉수를 올리고 목표 횟수로 끝내므로
        /// 그 코루틴이 돌면 판이 둘로 갈린다. 그래서 앞부분만 따로 뺐다.
        ///
        /// <c>tentaclePhaseHead</c> · <c>finalFormRoot</c> · <c>weakPoint</c> 가 모두
        /// private 참조라 밖에서는 켜고 끌 수 없다. 그래서 이 함수가 여기에 있다.
        /// </summary>
        public void PrepareNetworkTentacleStage()
        {
            gameObject.SetActive(true);
            if (tentaclePhaseHead != null) tentaclePhaseHead.SetActive(true);
            if (finalFormRoot != null) finalFormRoot.SetActive(false);
            else if (body != null) body.gameObject.SetActive(false);
            if (weakPoint != null) weakPoint.SetActive(false);

            TentacleSuccesses = 0;
            teamGauge?.ResetGauge();
            tentaclePhaseComplete = false;
            RemainingTentacles = 0;
            lastSoloSlot = -1;
            activePattern.Clear();

            for (int i = 0; i < tentacles.Length; i++)
            {
                if (tentacles[i] == null) continue;
                tentacles[i].SetAttackEnabled(false);
                tentacles[i].gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 촉수가 맞고 휘청이는 **모습만** 낸다. 판정도 약점 재추첨도 하지 않는다.
        ///
        /// 서버가 맞았다고 정하면 모든 화면이 이 함수로 같은 반응을 낸다.
        /// 같은 촉수가 아직 흔들리는 중이면 겹쳐 재생하지 않는다.
        /// </summary>
        public void ShowTentacleHit(WarriorsTarget tentacle, WarriorsAttackDirection direction)
        {
            if (tentacle == null || !isActiveAndEnabled) return;

            // ⚠ **뼈는 아래 중복 방지보다 먼저 휘게 한다.** 그 줄은 팔 전체를 기울이는 코루틴이
            //    겹쳐 도는 것을 막는 장치라, 연타를 먹은 촉수는 그 뒤로 아무 반응도 못 낸다.
            //    뼈 쪽은 겹쳐도 세기만 다시 1로 올릴 뿐이라 안전하다.
            tentacle.GetComponentInChildren<WarriorsTentacleArmLink>(true)?.PlayHit(direction);

            // ⚠ 2라운드 머리는 맞아도 **움직이지 않는다.** 팔 넷이 한꺼번에 흔들려 무엇을 맞혔는지
            //    읽히지 않았다. 맞은 표시는 표식이 터지는 것으로 충분하다.

            if (!reactingTentacles.Add(tentacle)) return;

            tentacleDeformer?.PlayHit(tentacle);
            StartCoroutine(TentacleHitReaction(tentacle, direction));

            // **물보라.** 촉수는 이미 휘청이는데(TentacleHitReaction) 맞은 자리에서 아무것도
            // 튀지 않아 "닿았다" 가 아니라 "흔들렸다" 로 읽혔다. 바다 보스이므로 물이 튄다.
            SpawnTentacleSplash(TentacleImpactPoint(tentacle.transform));
        }

        /// <summary>
        /// 촉수에서 **타격 연출이 터져야 하는 자리**. 방향 표식(↔ ↕ ⊙)이 붙은 지점이다.
        ///
        /// ⚠ <c>tentacle.transform.position</c> 을 그대로 쓰면 안 된다. 그 자리는 촉수의 <b>뿌리</b>다.
        ///    프리팹 실측으로 표식(<c>IndicatorAnchor</c>)은 거기서 로컬 y <b>+2.65</b>, z <b>-0.82</b> 위에 있다.
        ///    플레이어는 표식을 보고 그 방향으로 휘두르는데 물보라만 2m 아래 물가에서 터져
        ///    <b>맞은 곳과 터지는 곳이 따로 놀았다.</b>
        ///
        /// 표식이 없는 촉수면 뿌리에서 조금 띄운 예전 자리로 되돌아간다.
        /// </summary>
        private static Vector3 TentacleImpactPoint(Transform arm)
        {
            if (arm == null) return Vector3.zero;

            SpriteRenderer mark = arm.GetComponentInChildren<SpriteRenderer>(true);
            return mark != null ? mark.transform.position : arm.position + Vector3.up * .5f;
        }

        /// <summary>
        /// **촉수가 잘리는 순간.** 뒤로 크게 젖혀지며 아래로 빠진다.
        ///
        /// 그냥 <c>ShowDefeated</c> 로 사라지면 잘린 순간이 화면에 남지 않는다.
        /// 짧게라도 물리적으로 반응해야 "내가 잘랐다" 가 읽힌다.
        /// </summary>
        public void PlayTentacleCut(WarriorsTarget tentacle)
        {
            if (tentacle == null || !isActiveAndEnabled) return;

            // ⚠ **크게 젖히지 않는다.** 한때 여기서 촉수를 58도 젖히고 0.9m 아래로 내렸는데,
            //    화면에서 지나치게 요란해 "잘랐다" 가 아니라 "뭔가 크게 움직였다" 로 보였다.
            //    잘린 표시는 <b>방향 표식이 터지는 것</b>으로 충분하다.
            StartCoroutine(TentacleIndicatorBurst(tentacle.transform));

            // 뼈가 든 촉수는 힘이 빠지듯 늘어진다. 팔 자체를 젖히는 것이 아니라 길이가 휘는 것이라
            // 위에서 뺀 "요란함" 과는 다르다.
            tentacle.GetComponentInChildren<WarriorsTentacleArmLink>(true)?.PlayCut();
        }

        /// <summary>
        /// 잘린 촉수의 **방향 표식(↔ ↕ ⊙)만** 짧게 터뜨린다.
        ///
        /// 팔 자체는 <c>ShowDefeated</c> 가 처리한다. 여기서는 표식을 1.6배로 키우며
        /// 지우기만 한다 — 0.18초 안에 끝나고 화면에 남지 않는다.
        /// </summary>
        private IEnumerator TentacleIndicatorBurst(Transform arm)
        {
            if (arm == null) yield break;

            // ⚠ 크기를 여기서 직접 키우지 않는다. 표식(WarriorsTentacleIndicator)이 매 프레임
            //    자기 크기를 다시 정하므로 여기서 키워 봐야 한 프레임도 안 보인다 — 실제로 그랬다.
            arm.GetComponentInChildren<WarriorsTentacleIndicator>(true)?.PlayBurst();
            yield break;
        }

        /// <summary>촉수가 맞은 자리에서 튀는 물보라. 프리팹 없이 코드로 만든다.</summary>
        private static void SpawnTentacleSplash(Vector3 position)
        {
            // ⚠ 여기서 다시 올리지 않는다. 부르는 쪽이 이미 "터져야 하는 자리"를 넘긴다
            //    (TentacleImpactPoint). 예전에는 촉수 뿌리를 받아 여기서 0.5m 띄웠는데,
            //    그 보정이 남아 있으면 표식보다 위에서 터진다.
            GameObject splash = new GameObject("TentacleSplash");
            splash.transform.position = position;

            ParticleSystem particles = splash.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = .16f;
            main.loop = false;
            main.startLifetime = .38f;
            main.startSpeed = 4.2f;
            main.startSize = .16f;
            main.startColor = new Color(.62f, .88f, 1f, .95f);
            main.gravityModifier = 1.1f;          // 물이라 떨어진다
            main.maxParticles = 22;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 16) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = .2f;

            particles.Play();
        }

        private WarriorsHealth playerHealth;

        public void BeginBattle()
        {
            gameObject.SetActive(true);
            if (tentaclePhaseHead != null) tentaclePhaseHead.SetActive(true);
            if (finalFormRoot != null) finalFormRoot.SetActive(false);
            else if (body != null) body.gameObject.SetActive(false);
            if (weakPoint != null) weakPoint.SetActive(false);
            TentacleSuccesses = 0;
            teamGauge?.ResetGauge();
            tentaclePhaseComplete = false;
            RemainingTentacles = 0;
            lastSoloSlot = -1;
            activePattern.Clear();

            // Every arm starts untargetable; a pattern turns on just the ones it wants.
            for (int i = 0; i < tentacles.Length; i++)
            {
                if (tentacles[i] == null) continue;
                tentacles[i].SetAttackEnabled(false);
                tentacles[i].gameObject.SetActive(false);
            }
            StartCoroutine(TentaclePatternRoutine());
        }

        private IEnumerator TentaclePatternRoutine()
        {
            yield return new WaitForSeconds(introProtectionSeconds);
            foreach (TentaclePattern pattern in Patterns)
            {
                if (tentaclePhaseComplete) yield break;
                if (pattern.LeadIn > 0f) yield return new WaitForSeconds(pattern.LeadIn);
                yield return RunPattern(pattern);
            }
            tentaclePhaseComplete = true;
            AllTentaclesDefeated?.Invoke();
        }

        private IEnumerator RunPattern(TentaclePattern pattern)
        {
            activePattern.Clear();
            List<int> slots = SelectSlots(pattern.Count);
            List<WarriorsAttackDirection> weaknesses = CreatePatternWeaknesses(slots.Count);

            for (int i = 0; i < slots.Count; i++)
            {
                WarriorsTarget tentacle = tentacles[slots[i]];
                if (tentacle == null) continue;
                tentacle.gameObject.SetActive(true);
                tentacle.ConfigureAsBossPart(pattern.HitsEach);
                tentacle.ConfigureRequiredDirection(weaknesses[i]);
                tentacle.SetAttackEnabled(false);
                tentacle.Defeated -= HandleTentacleDefeated;
                tentacle.Defeated += HandleTentacleDefeated;
                tentacle.HitAccepted -= HandleTentacleHit;
                tentacle.HitAccepted += HandleTentacleHit;
                activePattern.Add(tentacle);
            }

            RemainingTentacles = activePattern.Count;
            firstTentacleDefeatTime = -1f;
            if (RemainingTentacles == 0) yield break;

            // A tentacle becomes hittable the moment its weakness icon appears. There used to
            // be a delay here from when tentacles physically rose out of the water, but the
            // arms belong to the body mesh and no longer move, so that delay only produced a
            // window where the icon was up and swings silently did nothing.
            foreach (WarriorsTarget tentacle in activePattern)
                if (tentacle != null && !tentaclePhaseComplete) tentacle.SetAttackEnabled(true);

            // A tentacle used to wait indefinitely to be cut, so ROUND 2 could only be lost
            // to the round clock - standing still was survivable. Each pattern now has a
            // window, and an arm still standing when it closes takes its swing.
            float window = tentacleWindowSeconds + tentacleWindowPerExtra * (activePattern.Count - 1);
            float deadline = Time.time + window;
            foreach (WarriorsTarget tentacle in activePattern)
                if (tentacle != null) tentacle.BeginStrikeWindow(window);
            while (RemainingTentacles > 0 && !tentaclePhaseComplete && Time.time < deadline)
                yield return null;

            bool struck = RemainingTentacles > 0 && !tentaclePhaseComplete;
            if (struck) StrikeWithUnansweredTentacles();
            foreach (WarriorsTarget tentacle in activePattern)
                if (tentacle != null) tentacle.ClearStrikeWindow();

            // Two tentacles taken down within a beat of each other is what a pair playing
            // together looks like - including in the same frame, which is why the span is
            // measured from the first kill rather than inferred from the count dropping.
            // A solo player can still earn it by being quick.
            bool coop = slots.Count > 1 && firstTentacleDefeatTime >= 0f
                && lastTentacleDefeatTime - firstTentacleDefeatTime <= coopWindowSeconds;
            // A pattern only pays out when the whole pattern was answered. Arms that were
            // cut still count towards the round total and the team gauge either way.
            if (!struck) PatternCleared?.Invoke(coop);
            yield return new WaitForSeconds(.25f);
        }

        /// <summary>
        /// Every arm left standing lands, so ignoring a two tentacle pattern costs twice
        /// what ignoring a single one does - the pattern size is the threat.
        /// </summary>
        private void StrikeWithUnansweredTentacles()
        {
            int swings = 0;
            foreach (WarriorsTarget tentacle in activePattern)
            {
                if (tentacle == null || tentacle.IsDefeated) continue;
                swings++;
                tentacle.Defeated -= HandleTentacleDefeated;
                tentacle.HitAccepted -= HandleTentacleHit;
                tentacle.SetAttackEnabled(false);
                tentacle.gameObject.SetActive(false);
            }

            RemainingTentacles = 0;
            if (swings > 0) playerHealth?.TryApplyDamage(tentacleStrikeDamage * swings);
        }

        /// <summary>
        /// Which tentacle positions surface. A solo tentacle uses an inner slot so it reads
        /// as centred; a pair always takes one from each side, which is what lets two players
        /// split it and a single player sweep left to right.
        /// </summary>
        private List<int> SelectSlots(int count)
        {
            List<int> slots = new(count);
            int half = Mathf.Max(1, tentacles.Length / 2);
            if (count <= 1)
            {
                int inner = tentacles.Length > 2 ? half - 1 + WarriorsRun.Range(0, 2) : WarriorsRun.Range(0, tentacles.Length);
                if (inner == lastSoloSlot) inner = (inner + 1) % tentacles.Length;
                lastSoloSlot = inner;
                slots.Add(Mathf.Clamp(inner, 0, tentacles.Length - 1));
                return slots;
            }
            slots.Add(WarriorsRun.Range(0, half));
            slots.Add(Mathf.Clamp(half + WarriorsRun.Range(0, tentacles.Length - half), half, tentacles.Length - 1));
            return slots;
        }

        /// <summary>
        /// Weaknesses for one pattern. Two tentacles never share a weakness: the round is
        /// about reading each one, and identical icons would turn it into a single choice.
        /// </summary>
        private List<WarriorsAttackDirection> CreatePatternWeaknesses(int count)
        {
            List<WarriorsAttackDirection> weaknesses = new(count);
            if (count <= 0) return weaknesses;
            WarriorsAttackDirection first = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);
            weaknesses.Add(first);
            for (int i = 1; i < count; i++)
                weaknesses.Add((WarriorsAttackDirection)(((int)first + WarriorsRun.Range(1, 3)) % 3));
            return weaknesses;
        }


        public void ShowFinalForm()
        {
            // 지난 판에서 바다로 가라앉혔으면 제자리로 되돌린다. 안 그러면 다음 판에 물속에서 시작한다.
            RestoreAfterSink();

            if (tentaclePhaseHead != null) tentaclePhaseHead.SetActive(false);
            foreach (WarriorsTarget tentacle in tentacles)
                if (tentacle != null) tentacle.gameObject.SetActive(false);
            FinalFormHealth = finalFormMaxHealth;
            FinalWeakness = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);
            teamGauge?.ResetGauge();
            if (finalFormRoot != null) finalFormRoot.SetActive(true);
            else if (body != null) body.gameObject.SetActive(true);
            if (weakPoint != null) weakPoint.SetActive(true);

            // 일어서는 순간 한 번 벌린다. 최종 형태는 이때 처음 화면에 나온다.
            legs?.PlayRoar(true);
        }

        public bool TryDamageFinalForm(WarriorsAttackDirection direction, float strength = 1f)
        {
            if (FinalFormHealth <= 0 || finalFormRoot == null || !finalFormRoot.activeInHierarchy) return false;
            if (direction != FinalWeakness) return false;
            int damage = Mathf.Max(1, Mathf.RoundToInt(finalAttackDamage * Mathf.Clamp(strength, .5f, 1.5f)));
            int nextHealth = Mathf.Max(0, FinalFormHealth - damage);
            if (FinalFormHealth > 10 && nextHealth < 10) nextHealth = 10;
            FinalFormHealth = nextHealth;
            teamGauge?.Add(finalHitGaugeGain);
            StartCoroutine(HitPulse());
            if (FinalFormHealth == 0) FinalFormDefeated?.Invoke();
            else FinalWeakness = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);
            return true;
        }

        public void ResetTeamGauge() => teamGauge?.ResetGauge();

        public void ApplyCooperativeDamage(float multiplier, bool finish)
        {
            if (FinalFormHealth <= 0) return;
            int damage = finish ? FinalFormHealth : Mathf.RoundToInt(finalAttackDamage * 2f * Mathf.Max(1f, multiplier));
            FinalFormHealth = Mathf.Max(0, FinalFormHealth - damage);
            teamGauge?.ResetGauge();
            StartCoroutine(HitPulse());
            if (FinalFormHealth == 0) FinalFormDefeated?.Invoke();
        }

        private void HandleTentacleDefeated(WarriorsTarget tentacle)
        {
            tentacle.Defeated -= HandleTentacleDefeated;
            tentacle.HitAccepted -= HandleTentacleHit;
            tentacle.SetAttackEnabled(false);
            activePattern.Remove(tentacle);
            if (firstTentacleDefeatTime < 0f) firstTentacleDefeatTime = Time.time;
            lastTentacleDefeatTime = Time.time;
            RemainingTentacles = Mathf.Max(0, RemainingTentacles - 1);
            TentacleSuccesses++;
            teamGauge?.Add(tentacleGaugeGain);
            tentacle.gameObject.SetActive(false);
        }

        private void HandleTentacleHit(WarriorsTarget tentacle, WarriorsAttackDirection hitDirection)
        {
            if (tentacle == null) return;
            if (reactingTentacles.Add(tentacle))
            {
                tentacleDeformer?.PlayHit(tentacle);
                StartCoroutine(TentacleHitReaction(tentacle, hitDirection));
            }

            // A surviving tentacle immediately advertises a new, different weakness.
            // The indicator reads RequiredDirection from this same target every frame.
            if (tentacle.BossHitsRemaining > 0)
            {
                WarriorsAttackDirection next = (WarriorsAttackDirection)
                    (((int)hitDirection + WarriorsRun.Range(1, 3)) % 3);
                // Two tentacles must never advertise the same weakness: one swing only ever
                // takes the nearest match, so a shared icon would make the far one look broken.
                foreach (WarriorsTarget other in activePattern)
                {
                    if (other == null || other == tentacle || other.RequiredDirection != next) continue;
                    next = (WarriorsAttackDirection)(((int)next + 1) % 3);
                    if (next == hitDirection) next = (WarriorsAttackDirection)(((int)next + 1) % 3);
                    break;
                }
                tentacle.ConfigureRequiredDirection(next);
            }
        }

        private IEnumerator TentacleHitReaction(WarriorsTarget tentacle, WarriorsAttackDirection direction)
        {
            Transform hitRoot = tentacle.transform;
            Vector3 restPosition = hitRoot.localPosition;
            Quaternion restRotation = hitRoot.localRotation;
            float sideSign = restPosition.x < 0f ? -1f : 1f;
            const float duration = .28f;
            for (float elapsed = 0f; elapsed < duration && hitRoot != null; elapsed += Time.deltaTime)
            {
                float normalized = elapsed / duration;
                float spring = Mathf.Sin(normalized * Mathf.PI * 3f) * (1f - normalized);
                float recoil = Mathf.Sin(Mathf.Clamp01(normalized * 2f) * Mathf.PI);
                Vector3 recoilEuler = direction switch
                {
                    WarriorsAttackDirection.HorizontalSlash => new Vector3(-4f * recoil, 0f, sideSign * 17f * recoil),
                    WarriorsAttackDirection.VerticalSlash => new Vector3(-17f * recoil, 0f, 5f * spring),
                    _ => new Vector3(-10f * recoil, 0f, 7f * spring)
                };
                Vector3 recoilPosition = direction switch
                {
                    WarriorsAttackDirection.HorizontalSlash => new Vector3(sideSign * .24f * recoil, 0f, .12f * recoil),
                    WarriorsAttackDirection.VerticalSlash => new Vector3(.08f * spring, -.12f * recoil, .20f * recoil),
                    _ => new Vector3(.08f * spring, 0f, .42f * recoil)
                };
                hitRoot.localRotation = restRotation * Quaternion.Euler(recoilEuler);
                hitRoot.localPosition = restPosition + recoilPosition;
                yield return null;
            }
            if (hitRoot != null)
            {
                hitRoot.localPosition = restPosition;
                hitRoot.localRotation = restRotation;
            }
            reactingTentacles.Remove(tentacle);
        }


        private IEnumerator HitPulse()
        {
            if (body == null) yield break;
            Vector3 original = body.localScale;
            body.localScale = original * .92f;
            yield return new WaitForSeconds(.12f);
            if (body != null) body.localScale = original;
        }

        public void PlayRhythmHit(bool strong)
        {
            // ⚠ **다리 뼈는 아래 검사보다 먼저 부른다.** 프리팹의 <c>body</c> 칸은 비어 있어서
            //    (실측 — WarriorsKrakenBoss.prefab 의 body 는 fileID: 0) 아래 줄에서 늘 되돌아간다.
            //    그 뒤에 두면 다리가 영영 움직이지 않는다. 몸통을 밀어 내는 코루틴만 body 가 필요하다.
            legs?.PlayHit(strong);

            if (body == null || !isActiveAndEnabled || !body.gameObject.activeInHierarchy) return;

            // ⚠ 겹쳐 돌면 안 된다. 앞 코루틴이 몸을 옮겨 놓은 상태에서 다음 코루틴이 시작하면
            //    그 <b>옮겨진 자리</b>를 원래 자리로 기억한다. 그러면 끝날 때 거기로 되돌려 놓아
            //    맞을수록 크라켄이 조금씩 밀려 나간다. 빠른 연타에서 실제로 그렇게 된다.
            if (impactRoutine != null) StopCoroutine(impactRoutine);
            impactRoutine = StartCoroutine(RhythmImpactRoutine(strong));
            SpawnRhythmImpact(strong);
        }

        /// <summary>
        /// **묶음을 다 받아 냈을 때의 큰 리액션.** 한 대 맞은 것과 눈에 띄게 달라야 한다.
        /// <paramref name="team"/> 이면 두 사람이 동시에 해낸 것이라 한 단계 더 크게 친다.
        /// </summary>
        public void PlayRhythmFinish(bool team)
        {
            legs?.PlayRoar(team);   // 위와 같은 이유로 body 검사보다 먼저 (PlayRhythmHit 주석 참고)

            if (body == null || !isActiveAndEnabled || !body.gameObject.activeInHierarchy) return;

            if (impactRoutine != null) StopCoroutine(impactRoutine);
            impactRoutine = StartCoroutine(RhythmFinishRoutine(team));
            SpawnRhythmImpact(true);
        }

        public void ApplyRhythmHit(bool strong)
        {
            ApplyRhythmHitSilently(strong);
            PlayRhythmHit(strong);
        }

        /// <summary>
        /// 체력만 깎고 연출은 하지 않는다. <b>서버가 쓰는 쪽이다.</b>
        ///
        /// 서버에서 연출을 재생해 봐야 서버 프로세스 안에서만 일어나고 아무도 보지 못한다.
        /// 연출은 <c>WarriorsPhase3Director</c> 가 복제한 번호를 보고 각 화면이 따로 재생한다.
        /// </summary>
        public void ApplyRhythmHitSilently(bool strong)
        {
            if (FinalFormHealth <= 0) return;
            FinalFormHealth = Mathf.Max(1, FinalFormHealth - (strong ? rhythmHitDamage + 2 : rhythmHitDamage));
        }

        /// <summary>
        /// Damage from one finished ROUND 3 pattern.  ROUND 3 used to end simply because the
        /// notes ran out - the kraken died whether or not the player hit anything - so the
        /// boss now only goes down through this, which means the attacks have to land.
        /// </summary>
        public void ApplyPatternDamage(int damage, bool finisher)
        {
            if (FinalFormHealth <= 0 || damage <= 0) return;
            FinalFormHealth = Mathf.Max(0, FinalFormHealth - damage);
            PlayRhythmHit(finisher);
            if (FinalFormHealth == 0) FinalFormDefeated?.Invoke();
        }

        public void CompleteRhythmBattle()
        {
            FinalFormHealth = 0;
            PlayRhythmHit(true);
        }

        /// <summary>
        /// 묶음을 다 받아 냈을 때. 한 대와 다른 점은 <b>세 가지</b>다.
        ///   1. 잠깐 멈춘다(hit stop) — 때린 순간이 눈에 박히게.
        ///   2. 뒤로 밀린다 — 흔들기만 하면 "맞았다" 가 아니라 "떨었다" 로 보인다.
        ///   3. 밀린 자리에서 천천히 돌아온다.
        /// 끝나면 반드시 원래 자리로 복원하므로 여러 번 나도 자리가 밀리지 않는다.
        /// </summary>
        private IEnumerator RhythmFinishRoutine(bool team)
        {
            if (body == null) yield break;

            Vector3 originalPosition = body.localPosition;
            Quaternion originalRotation = body.localRotation;
            Vector3 originalScale = body.localScale;

            // 1. 히트 스톱. 실시간으로 재므로 Time.timeScale 을 건드리지 않는다 —
            //    네트워크 게임에서 시간을 늦추면 서버와 어긋난다.
            body.localScale = originalScale * (team ? .88f : .91f);
            float stop = team ? .09f : .06f;
            for (float t = 0f; t < stop; t += Time.unscaledDeltaTime) yield return null;

            // 2. 뒤로(크라켄 기준 뒤 = +Z) 밀린다.
            float shove = team ? .85f : .55f;
            float duration = team ? .5f : .4f;
            float twist = team ? 16f : 11f;

            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float k = elapsed / duration;
                // 앞의 1/4 동안 확 밀리고 나머지는 되돌아온다.
                float push = k < .25f ? k / .25f : 1f - (k - .25f) / .75f;
                push = Mathf.SmoothStep(0f, 1f, push);
                float fade = 1f - k;

                body.localPosition = originalPosition
                    + Vector3.forward * (shove * push)
                    + UnityEngine.Random.insideUnitSphere * (.1f * fade);
                body.localRotation = originalRotation
                    * Quaternion.Euler(-twist * push, 0f, Mathf.Sin(elapsed * 60f) * twist * .4f * fade);
                body.localScale = originalScale * (1f - .12f * fade);
                yield return null;
            }

            if (body != null)
            {
                body.localPosition = originalPosition;
                body.localRotation = originalRotation;
                body.localScale = originalScale;
            }

            impactRoutine = null;
        }

        private IEnumerator RhythmImpactRoutine(bool strong)
        {
            if (body == null) yield break;
            Vector3 originalPosition = body.localPosition;
            Quaternion originalRotation = body.localRotation;
            Vector3 originalScale = body.localScale;
            // A finisher has to land differently from a single hit or three clean swings feel
            // the same as one. Every value here is the same shake, just louder, and the whole
            // thing still restores to the captured pose below - so repeated finishers cannot
            // walk the kraken out of position.
            // **한 대도 눈에 보여야 한다.** 예전 값(jolt .12 · twist 5°)은 화면에서 거의
            // 정지한 그림처럼 보였다. 흔들림만으로는 "맞았다" 가 아니라 "떨었다" 로 읽히므로
            // 뒤로 밀리는 성분(아래 pushBack)을 같이 준다.
            float duration = strong ? .42f : .24f;
            float jolt = strong ? .30f : .18f;
            float twist = strong ? 14f : 9f;
            float squash = strong ? .18f : .10f;
            float pushBack = strong ? .34f : .18f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / duration;

                // 앞의 1/3 동안 뒤로 밀렸다가 돌아온다. 흔들림(jolt)에만 기대면
                // 자리가 바뀌지 않아 "맞았다" 로 읽히지 않는다.
                float k = elapsed / duration;
                float push = k < .33f ? k / .33f : 1f - (k - .33f) / .67f;

                body.localPosition = originalPosition
                    + Vector3.forward * (pushBack * Mathf.SmoothStep(0f, 1f, push))
                    + UnityEngine.Random.insideUnitSphere * (jolt * fade)
                    + Vector3.up * (strong ? -.12f * fade : 0f);

                // 머리가 젖혀지는 성분(X)을 더한다. Z 흔들림만 있으면 갸우뚱하는 것처럼 보인다.
                body.localRotation = originalRotation
                    * Quaternion.Euler(-twist * .8f * Mathf.SmoothStep(0f, 1f, push),
                                       0f,
                                       Mathf.Sin(elapsed * 75f) * twist * fade);

                body.localScale = originalScale * (1f - squash * fade);
                yield return null;
            }
            if (body != null)
            {
                body.localPosition = originalPosition;
                body.localRotation = originalRotation;
                body.localScale = originalScale;
            }

            impactRoutine = null;
        }

        private void SpawnRhythmImpact(bool strong)
        {
            GameObject burst = new("RhythmHitBurst");
            burst.transform.position = body.position + Vector3.up * .4f;
            ParticleSystem particles = burst.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.duration = .18f;
            main.loop = false;
            main.startLifetime = strong ? .42f : .3f;
            main.startSpeed = strong ? 5.2f : 3.6f;
            main.startSize = strong ? .22f : .14f;
            main.startColor = strong ? new Color(1f, .72f, .16f, 1f) : new Color(.35f, .85f, 1f, 1f);
            main.maxParticles = 24;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)(strong ? 20 : 12)) });
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .22f;
            particles.Play();
        }

        // ------------------------------------------------------------
        // 3라운드 승리 — 바다 밑으로 가라앉는다
        // ------------------------------------------------------------

        private Coroutine sinkRoutine;
        private bool sinkRestSaved;
        private Vector3 sinkRestPosition;
        private Quaternion sinkRestRotation;

        private Transform SinkRoot => finalFormRoot != null ? finalFormRoot.transform : transform;

        /// <summary>
        /// **쓰러진 크라켄이 바다 밑으로 가라앉는다.** 3라운드를 깬 순간 각 화면이 부른다.
        ///
        /// 예전에는 이긴 뒤에도 크라켄이 그대로 서 있다가 화면이 어두워지며 결과 판이 떠서
        /// "갑자기 사라진" 것처럼 보였다. 이제 <b>천천히 물속으로 잠겨 사라진다.</b>
        /// <code>
        ///   시작   수면에 잔잔한 물결 한 번
        ///   내내   같은 빠르기로 부드럽게(처음과 끝만 살짝 느리게) 내려간다
        ///   끝     머리 끝까지 잠기면 끈다 (다음 판은 ShowFinalForm 이 되돌린다)
        /// </code>
        /// ⚠ 떨림 · 포효 · 큰 물보라를 넣지 않는다. 처음에 넣었더니 가라앉는 게 아니라
        ///    "눈앞에서 터지는" 것처럼 보였다.
        /// ⚠ 서버에서 부르지 않는다. 서버 화면은 아무도 보지 않는다 (PlayRhythmHit 과 같은 이유).
        /// </summary>
        public void PlayDefeatSink(float seconds)
        {
            Transform root = SinkRoot;
            if (!isActiveAndEnabled || root == null || !root.gameObject.activeInHierarchy) return;

            if (!sinkRestSaved)
            {
                sinkRestPosition = root.localPosition;
                sinkRestRotation = root.localRotation;
                sinkRestSaved = true;
            }

            SpawnDefeatSplash(root.position);

            if (sinkRoutine != null) StopCoroutine(sinkRoutine);
            sinkRoutine = StartCoroutine(DefeatSinkRoutine(root, Mathf.Max(1f, seconds)));
        }

        private IEnumerator DefeatSinkRoutine(Transform root, float seconds)
        {
            Vector3 start = root.position;

            // 모델 키보다 조금 더 내려가야 머리 끝까지 수면 아래로 잠긴다.
            // ⚠ 키는 메시로만 잰다. 파티클까지 넣어 쟀더니 수십 m 로 나와 한순간에 화면 밖으로 떨어졌다.
            float depth = Mathf.Clamp(MeasureMeshHeight(root) * 1.1f, 3f, 20f);
            Debug.Log($"[WarriorsKraken] 바다 밑으로 가라앉습니다 — 깊이 {depth:F1}m, {seconds:F1}초 ({root.name})", this);

            for (float t = 0f; t < seconds; t += Time.deltaTime)
            {
                float u = t / seconds;
                root.position = start + Vector3.down * depth * Mathf.SmoothStep(0f, 1f, u);
                yield return null;
            }

            root.position = start + Vector3.down * depth;
            root.gameObject.SetActive(false);
            sinkRoutine = null;
        }

        /// <summary>가라앉힌 것을 되돌린다. 새 판을 세울 때 부른다.</summary>
        private void RestoreAfterSink()
        {
            if (!sinkRestSaved) return;

            if (sinkRoutine != null)
            {
                StopCoroutine(sinkRoutine);
                sinkRoutine = null;
            }

            Transform root = SinkRoot;
            if (root != null)
            {
                root.localPosition = sinkRestPosition;
                root.localRotation = sinkRestRotation;
            }
        }

        /// <summary>메시(겉모습)만으로 잰 키. 파티클 · 잔상은 범위가 커서 넣지 않는다.</summary>
        private static float MeasureMeshHeight(Transform root)
        {
            bool found = false;
            Bounds bounds = default;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
            {
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }

            return found ? bounds.size.y : 0f;
        }

        /// <summary>잔잔한 물결. 잠기기 시작할 때 수면에 한 번만 인다. 크게 튀지 않는다.</summary>
        private static void SpawnDefeatSplash(Vector3 at)
        {
            GameObject burst = new("KrakenDefeatSplash");
            burst.transform.position = at + Vector3.up * .2f;
            ParticleSystem particles = burst.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.duration = .4f;
            main.loop = false;
            main.startLifetime = .7f;
            main.startSpeed = 1.8f;
            main.startSize = .18f;
            main.gravityModifier = .8f;
            main.startColor = new Color(.85f, .96f, 1f, .55f);
            main.maxParticles = 24;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)16) });

            // 낮게 퍼지는 원뿔. 솟구치지 않고 수면 가까이서 번진다.
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 1.4f;
            shape.rotation = new Vector3(-90f, 0f, 0f);

            particles.Play();
        }

        public void Defeat() => StartCoroutine(DefeatRoutine());
        private IEnumerator DefeatRoutine()
        {
            Vector3 start = body != null ? body.localScale : Vector3.one;
            for (float t = 0f; t < .8f; t += Time.deltaTime)
            {
                if (body != null) body.localScale = Vector3.Lerp(start, Vector3.zero, t / .8f);
                yield return null;
            }
            gameObject.SetActive(false);
        }
    }
}
