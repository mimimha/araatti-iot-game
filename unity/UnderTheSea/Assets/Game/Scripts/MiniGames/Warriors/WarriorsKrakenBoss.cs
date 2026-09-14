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
            if (!reactingTentacles.Add(tentacle)) return;

            tentacleDeformer?.PlayHit(tentacle);
            StartCoroutine(TentacleHitReaction(tentacle, direction));
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
            if (tentaclePhaseHead != null) tentaclePhaseHead.SetActive(false);
            foreach (WarriorsTarget tentacle in tentacles)
                if (tentacle != null) tentacle.gameObject.SetActive(false);
            FinalFormHealth = finalFormMaxHealth;
            FinalWeakness = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);
            teamGauge?.ResetGauge();
            if (finalFormRoot != null) finalFormRoot.SetActive(true);
            else if (body != null) body.gameObject.SetActive(true);
            if (weakPoint != null) weakPoint.SetActive(true);
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
            if (body == null || !isActiveAndEnabled || !body.gameObject.activeInHierarchy) return;
            StartCoroutine(RhythmImpactRoutine(strong));
            SpawnRhythmImpact(strong);
        }

        public void ApplyRhythmHit(bool strong)
        {
            if (FinalFormHealth <= 0) return;
            FinalFormHealth = Mathf.Max(1, FinalFormHealth - (strong ? rhythmHitDamage + 2 : rhythmHitDamage));
            PlayRhythmHit(strong);
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
            float duration = strong ? .38f : .16f;
            float jolt = strong ? .26f : .12f;
            float twist = strong ? 11f : 5f;
            float squash = strong ? .16f : .07f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / duration;
                body.localPosition = originalPosition + UnityEngine.Random.insideUnitSphere * (jolt * fade)
                    + Vector3.up * (strong ? -.12f * fade : 0f);
                body.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 75f) * twist * fade);
                body.localScale = originalScale * (1f - squash * fade);
                yield return null;
            }
            if (body != null)
            {
                body.localPosition = originalPosition;
                body.localRotation = originalRotation;
                body.localScale = originalScale;
            }
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
