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
        [SerializeField, Min(4)] private int tentacleSuccessesRequired = 4;
        [SerializeField] private bool singleTentacleSet = true;
        [SerializeField, Min(.1f)] private float tentacleRespawnDelay = 1.1f;
        [SerializeField, Min(0f)] private float introProtectionSeconds = 1.25f;
        [SerializeField, Min(0f)] private float tentacleGaugeGain = 12.5f;
        [SerializeField, Min(0f)] private float finalHitGaugeGain = 20f;
        [SerializeField] private WarriorsTeamGauge teamGauge;
        public int RemainingTentacles { get; private set; }
        public int TentacleSuccesses { get; private set; }
        public int TentacleSuccessesRequired => singleTentacleSet ? tentacles.Length : tentacleSuccessesRequired;
        public int FinalFormHealth { get; private set; }
        public int FinalFormHealthPercent => Mathf.RoundToInt(FinalFormHealth / (float)finalFormMaxHealth * 100f);
        public WarriorsAttackDirection FinalWeakness { get; private set; }
        public float TeamGaugeNormalized => teamGauge != null ? teamGauge.Normalized : 0f;
        public bool IsTeamGaugeReady => teamGauge != null && teamGauge.IsReady;
        public event Action AllTentaclesDefeated;
        public event Action FinalFormDefeated;
        private bool tentaclePhaseComplete;
        private readonly HashSet<WarriorsTarget> reactingTentacles = new();
        private WarriorsKrakenTentacleDeformer tentacleDeformer;

        private void Awake()
        {
            if (teamGauge == null) teamGauge = GetComponent<WarriorsTeamGauge>();
            if (teamGauge == null) teamGauge = gameObject.AddComponent<WarriorsTeamGauge>();
            tentacleDeformer = GetComponent<WarriorsKrakenTentacleDeformer>();
            if (tentacleDeformer == null) tentacleDeformer = gameObject.AddComponent<WarriorsKrakenTentacleDeformer>();
            tentacleDeformer.Configure(tentaclePhaseHead != null ? tentaclePhaseHead.GetComponentInChildren<MeshFilter>(true) : null, tentacles);
        }

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
            RemainingTentacles = tentacles.Length;
            foreach (WarriorsTarget tentacle in tentacles)
            {
                if (tentacle == null) continue;
                tentacle.ConfigureRequiredDirection((WarriorsAttackDirection)WarriorsRun.Range(0, 3));
                tentacle.ConfigureAsBossPart();
                tentacle.SetAttackEnabled(false);
                tentacle.Defeated -= HandleTentacleDefeated;
                tentacle.Defeated += HandleTentacleDefeated;
                tentacle.gameObject.SetActive(true);
            }
            StartCoroutine(EnableTentaclesAfterIntro());
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

        private IEnumerator EnableTentaclesAfterIntro()
        {
            yield return new WaitForSeconds(introProtectionSeconds);
            foreach (WarriorsTarget tentacle in tentacles)
                if (tentacle != null && tentacle.gameObject.activeInHierarchy)
                    tentacle.SetAttackEnabled(true);
        }

        private void HandleTentacleDefeated(WarriorsTarget tentacle)
        {
            tentacle.Defeated -= HandleTentacleDefeated;
            if (tentacle != null && reactingTentacles.Add(tentacle))
            {
                tentacleDeformer?.PlayHit(tentacle);
                StartCoroutine(TentacleHitReaction(tentacle));
            }
            RemainingTentacles = Mathf.Max(0, RemainingTentacles - 1);
            TentacleSuccesses++;
            teamGauge?.Add(tentacleGaugeGain);
            if (TentacleSuccesses >= TentacleSuccessesRequired)
            {
                tentaclePhaseComplete = true;
                foreach (WarriorsTarget part in tentacles)
                    if (part != null)
                    {
                        part.SetAttackEnabled(false);
                        part.gameObject.SetActive(false);
                    }
                AllTentaclesDefeated?.Invoke();
                return;
            }
            StartCoroutine(RespawnTentacle(tentacle));
        }

        private IEnumerator TentacleHitReaction(WarriorsTarget tentacle)
        {
            Transform hitRoot = tentacle.transform;
            Vector3 restPosition = hitRoot.localPosition;
            Quaternion restRotation = hitRoot.localRotation;
            const float duration = .20f;
            for (float elapsed = 0f; elapsed < duration && hitRoot != null; elapsed += Time.deltaTime)
            {
                float normalized = elapsed / duration;
                float spring = Mathf.Sin(normalized * Mathf.PI * 3f) * (1f - normalized);
                float recoil = Mathf.Sin(Mathf.Clamp01(normalized * 2f) * Mathf.PI);
                hitRoot.localRotation = restRotation * Quaternion.Euler(-14f * recoil, 0f, 9f * spring);
                hitRoot.localPosition = restPosition + new Vector3(.16f * spring, 0f, -.32f * recoil);
                yield return null;
            }
            if (hitRoot != null)
            {
                hitRoot.localPosition = restPosition;
                hitRoot.localRotation = restRotation;
            }
            reactingTentacles.Remove(tentacle);
        }

        private IEnumerator RespawnTentacle(WarriorsTarget tentacle)
        {
            yield return new WaitForSeconds(.3f);
            if (tentacle != null) tentacle.gameObject.SetActive(false);
            yield return new WaitForSeconds(tentacleRespawnDelay);
            if (tentaclePhaseComplete || tentacle == null) yield break;
            WarriorsAttackDirection previous = tentacle.RequiredDirection;
            WarriorsAttackDirection next = (WarriorsAttackDirection)(((int)previous + WarriorsRun.Range(1, 3)) % 3);
            tentacle.ReviveBossPart(next);
            tentacle.Defeated -= HandleTentacleDefeated;
            tentacle.Defeated += HandleTentacleDefeated;
            RemainingTentacles++;
            yield return new WaitForSeconds(.25f);
            if (!tentaclePhaseComplete) tentacle.SetAttackEnabled(true);
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
            float duration = strong ? .24f : .16f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / duration;
                body.localPosition = originalPosition + UnityEngine.Random.insideUnitSphere * (.12f * fade);
                body.localRotation = originalRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 75f) * 5f * fade);
                body.localScale = originalScale * (1f - .07f * fade);
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
