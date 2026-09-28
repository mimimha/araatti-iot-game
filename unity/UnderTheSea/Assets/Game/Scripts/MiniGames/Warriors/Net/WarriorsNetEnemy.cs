using System.Collections;
using System.Collections.Generic;
using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// 몬스터 하나. **서버가 움직이고 서버가 죽인다.**
    ///
    /// <code>
    ///   서버    가장 가까운 **살아 있는** 사람을 골라 쫓아간다 · 때린다 · 죽으면 치운다
    ///   모두    NetworkTransform 이 준 자리를 그린다 · 쓰러지는 연출을 낸다
    /// </code>
    ///
    /// <b>몬스터에 주인은 없다.</b> 누가 벴는지 기록하지 않는다. 두 사람이 같은 몬스터를
    /// 같이 두들길 수 있고, 마지막 한 대가 누구 것이든 처치 수는 팀 합산으로 하나 오른다.
    ///
    /// ⚠ 클라이언트에서는 이동 · 공격 부품을 꺼 둔다. 켜 두면 각자 자기 화면에서
    ///    몬스터를 다르게 움직이고, <c>WarriorsEnemyAttack</c> 의 정적 웨이브 타이머까지
    ///    따로 돌아 완전히 다른 판이 된다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WarriorsTarget))]
    public sealed class WarriorsNetEnemy : NetworkBehaviour
    {
        [Header("치우기")]
        [Tooltip("쓰러진 뒤 이만큼 있다가 사라진다. 쓰러지는 연출이 끝날 즈음.")]
        [SerializeField, Min(0f)] private float despawnDelay = 0.4f;

        [Header("타겟 고르기")]
        [Tooltip("이 간격(초)마다 쫓을 사람을 다시 고른다. 매 틱 고르면 낭비다.")]
        [SerializeField, Min(0.05f)] private float retargetInterval = 0.25f;

        [Tooltip("이미 그 사람을 노리는 몬스터 한 마리마다 더해지는 가상 거리(m). 클수록 두 사람에게 고르게 나뉜다.")]
        [SerializeField, Min(0f)] private float spreadPerHunter = 3f;

        [Tooltip("지금 대상보다 이만큼(m) 더 나은 후보가 있을 때만 갈아탄다. 두 사람 사이에서 왔다갔다하지 않게.")]
        [SerializeField, Min(0f)] private float switchMargin = 1.5f;

        /// <summary>쓰러졌는가. 서버가 정하고 모두가 같은 순간에 연출을 낸다.</summary>
        [Networked] public NetworkBool Defeated { get; private set; }

        /// <summary>
        /// 맞을 때마다 1씩 오른다. <b>타격 연출을 클라이언트에서 보이게 하는 값이다.</b>
        ///
        /// ⚠ 맞은 순간을 잡는 곳은 <c>FixedUpdateNetwork</c> 인데 그 함수는 맨 위에서
        ///    <c>HasStateAuthority</c> 로 막혀 <b>서버에서만</b> 돈다. 거기서 번쩍임이나
        ///    파티클을 내면 화면 요소가 꺼진 데디케이티드 서버 안에서만 일어나 아무도 못 본다.
        ///    <c>Retreat</c> 가 보였던 것은 그것이 연출이 아니라 <b>위치 변경</b>이라
        ///    NetworkTransform 이 대신 복제해 주기 때문이다. 번쩍임은 그렇지 않다.
        ///
        /// 그래서 <c>WarriorsPhase3Director.HitSerial</c> 과 같은 방식으로 번호만 복제하고
        /// 연출은 각 화면이 <c>Render</c> 에서 재생한다.
        /// </summary>
        [Networked] public int HitSerial { get; private set; }

        /// <summary>서버에서 살아 움직이는 몬스터들. 누가 누구를 노리는지 세는 데 쓴다.</summary>
        private static readonly List<WarriorsNetEnemy> Hunting = new List<WarriorsNetEnemy>();

        private WarriorsTarget target;
        private WarriorsHealth health;
        private WarriorsBeachEnemyApproach approach;

        /// <summary>마지막으로 본 HP. 이 값이 줄어든 틱이 "맞은 순간" 이다.</summary>
        private int lastSeenHealth = int.MinValue;
        private WarriorsEnemyAttack enemyAttack;
        private WarriorsPlayerLife currentTarget;

        private bool shownDefeat;
        private float nextRetargetTime;
        private TickTimer despawnTimer;

        /// <summary>이 화면이 마지막으로 재생한 타격 번호. -1 은 "아직 한 번도 안 봤다".</summary>
        private int shownHitSerial = -1;

        private Coroutine flashRoutine;
        private Renderer[] hitRenderers;
        private Color[] hitBaseColors;

        // URP 는 _BaseColor, 빌트인은 _Color 를 쓴다. 둘 다 본다.
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public override void Spawned()
        {
            target = GetComponent<WarriorsTarget>();
            health = GetComponent<WarriorsHealth>();
            approach = GetComponent<WarriorsBeachEnemyApproach>();
            enemyAttack = GetComponent<WarriorsEnemyAttack>();

            if (!HasStateAuthority)
            {
                // 클라이언트는 그리기만 한다. 움직임은 NetworkTransform 이 준다.
                if (approach != null) approach.enabled = false;
                if (enemyAttack != null) enemyAttack.enabled = false;
                return;
            }

            Hunting.Add(this);
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            Hunting.Remove(this);
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority) return;

            // 일시정지 중에는 대상도 고르지 않는다. 이동 · 공격 자체는 서버의 timeScale 이 세운다.
            if (WarriorsMatchState.PausedNow) return;

            // **맞으면 뒤로 밀린다.** <c>WarriorsBeachEnemyApproach.Retreat</c> 는 만들어져 있었는데
            // 아무도 부르지 않아, 몬스터가 맞아도 제자리에서 계속 걸어왔다. 손맛이 없던 이유다.
            // HP 가 줄어든 틱을 잡아 짧게 물러나게 한다. 죽을 때는 조금 더 크게.
            if (health != null && health.CurrentHealth != lastSeenHealth)
            {
                bool dying = health.IsDead;

                if (health.CurrentHealth < lastSeenHealth)
                {
                    // 일반 타격을 0.12 → 0.16초로 늘렸다. 감속 곡선이 붙으면서 앞머리에
                    // 속도가 몰리므로, 같은 시간이면 밀려나는 거리가 오히려 짧아 보인다.
                    if (approach != null) approach.Retreat(dying ? .25f : .16f);

                    // 번쩍임·파티클은 여기서 내지 않는다(서버 전용이라 아무도 못 본다).
                    // 번호만 올리고 각 화면이 Render 에서 재생한다.
                    HitSerial++;
                }

                lastSeenHealth = health.CurrentHealth;
            }

            if (!Defeated && health != null && health.IsDead)
            {
                Defeated = true;
                despawnTimer = TickTimer.CreateFromSeconds(Runner, despawnDelay);

                WarriorsMatchState match = WarriorsMatchState.Current;
                if (match != null) match.ReportPhase1Kill();
            }

            if (Defeated)
            {
                if (despawnTimer.Expired(Runner)) Runner.Despawn(Object);
                return;
            }

            if (Time.time < nextRetargetTime) return;
            nextRetargetTime = Time.time + retargetInterval;

            Retarget();
        }

        /// <summary>
        /// 쫓을 사람을 고른다. **가깝되, 이미 많이 몰린 사람은 피한다.**
        ///
        /// 예전에는 가장 가까운 사람만 골랐다. 두 사람이 나란히 서 있으면 모든 몬스터가
        /// 몇 cm 더 가까운 한 사람에게 쏠려, 다른 사람은 구경만 했다.
        /// 그래서 거리에 "이미 그 사람을 노리는 몬스터 수 × <see cref="spreadPerHunter"/>" 를 더해
        /// 비교한다. 여섯 마리가 1P 에 붙어 있으면 2P 가 18m 안에만 있어도 2P 쪽이 이긴다.
        ///
        /// 지금 대상은 <see cref="switchMargin"/> 만큼 유리하게 본다 — 매 0.25초 두 사람 사이를
        /// 오가며 제자리걸음하지 않게.
        ///
        /// 쓰러진 사람은 후보에서 빠진다. 아무도 없으면 대상을 비워 몬스터가 제자리에 서 있게 한다.
        /// 시체를 계속 쫓게 두면 남은 사람이 반대편에서 편하게 정리해 버린다.
        /// </summary>
        private void Retarget()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            WarriorsPlayerLife best = null;
            float bestScore = float.MaxValue;
            float currentScore = float.MaxValue;
            Vector3 here = transform.position;

            bool keepable = currentTarget != null && currentTarget.IsLive && !currentTarget.IsDown;

            foreach (WarriorsPlayerLife one in crew)
            {
                if (one == null || !one.IsLive || one.IsDown) continue;

                float distance = Vector3.Distance(one.transform.position, here);
                int hunters = CountHunting(one);
                float score = distance + hunters * spreadPerHunter;

                if (one == currentTarget) currentScore = score;

                if (score >= bestScore) continue;

                bestScore = score;
                best = one;
            }

            // 지금 대상이 아직 괜찮으면 굳이 갈아타지 않는다.
            if (keepable && best != currentTarget && bestScore > currentScore - switchMargin) best = currentTarget;

            currentTarget = best;

            if (approach != null)
            {
                approach.RetargetPlayer(best != null ? best.transform : null);
            }

            if (enemyAttack != null)
            {
                enemyAttack.RetargetPlayer(best != null ? best.GetComponent<WarriorsHealth>() : null);
            }
        }

        /// <summary>나 말고 이 사람을 노리는 몬스터 수.</summary>
        private int CountHunting(WarriorsPlayerLife life)
        {
            int count = 0;

            foreach (WarriorsNetEnemy other in Hunting)
            {
                if (other == null || other == this || other.Defeated) continue;
                if (other.currentTarget == life) count++;
            }

            return count;
        }

        /// <summary>쓰러지는 모습과 맞는 모습은 모든 화면에서 같은 순간에 난다.</summary>
        public override void Render()
        {
            // **맞았다.** 서버가 올린 번호가 바뀌면 이 화면에서 때린 표시를 낸다.
            // 접속 직후 0 -> 값 으로 튀면서 몰아치지 않도록 처음 본 번호는 맞춰만 둔다.
            if (HitSerial != shownHitSerial)
            {
                bool first = shownHitSerial < 0;
                shownHitSerial = HitSerial;

                if (!first) PlayHitFeedback();
            }

            if (shownDefeat || !Defeated || target == null) return;

            shownDefeat = true;
            target.ShowDefeated();

            // **죽는 순간에도 한 번 더 때린 표시를 낸다.** 그냥 사라지면 마지막 일격이
            // 가장 밋밋해진다. 살아 있을 때보다 크게 튀기고 몸을 살짝 오므린다.
            PlayHitFeedback();
            if (isActiveAndEnabled) StartCoroutine(DeathShrinkRoutine());
        }

        /// <summary>
        /// 쓰러질 때 몸이 살짝 오므라든다. 1.0 → 0.9 로 0.18초.
        ///
        /// 오래 남기지 않는다 — <c>despawnDelay</c>(0.4초) 안에 끝나야 시체가 화면에 쌓이지 않는다.
        /// </summary>
        private IEnumerator DeathShrinkRoutine()
        {
            Transform body = transform;
            Vector3 original = body.localScale;

            const float Duration = .18f;

            for (float elapsed = 0f; elapsed < Duration && body != null; elapsed += Time.deltaTime)
            {
                float k = elapsed / Duration;
                body.localScale = original * Mathf.Lerp(1f, .9f, k);
                yield return null;
            }
        }

        /// <summary>
        /// 맞은 표시. **넉백만으로는 "맞았다" 가 아니라 "밀렸다" 로만 읽힌다.**
        ///
        /// 세 가지를 겹친다 — 흰 번쩍임(맞은 순간), 짧은 찌그러짐(무게), 튀는 입자(베였다).
        /// 전부 0.12~0.2초 안에 끝나므로 화면에 남지 않는다.
        /// </summary>
        private void PlayHitFeedback()
        {
            if (!isActiveAndEnabled) return;

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(HitFlashRoutine());

            // **몸이 눌렸다 튀어 오른다.** 번쩍임은 "표시"일 뿐 무게가 없다.
            // 맞은 순간 납작해졌다가 되돌아와야 맞은 쪽에 실린 힘이 읽힌다.
            if (squashRoutine != null) StopCoroutine(squashRoutine);
            squashRoutine = StartCoroutine(HitSquashRoutine());

            SpawnHitSpark();
        }

        /// <summary>
        /// 맞은 순간의 **눌림과 되튐**(squash &amp; stretch).
        ///
        /// 0.06초 만에 세로 0.72배 · 가로 1.22배로 납작해졌다가, 0.14초에 걸쳐 원래대로
        /// 되돌아오면서 살짝 넘어간다. 전체 0.2초라 화면에 남지 않는다.
        ///
        /// ⚠ 원래 배율을 <b>코루틴 밖</b>에서 한 번만 기억한다. 겹쳐 돌면 이미 찌그러진 배율을
        ///   원래 값으로 기억해 몬스터가 영영 납작하게 남는다 — 번쩍임에서 같은 실수를 한 적이 있다.
        /// </summary>
        private IEnumerator HitSquashRoutine()
        {
            Transform body = ResolveVisualRoot();
            if (body == null) yield break;

            if (!squashCaptured)
            {
                squashRest = body.localScale;
                squashCaptured = true;
            }

            Vector3 rest = squashRest;
            Vector3 squashed = new(rest.x * 1.22f, rest.y * .72f, rest.z * 1.22f);

            const float Down = .06f;
            const float Up = .14f;

            for (float elapsed = 0f; elapsed < Down; elapsed += Time.deltaTime)
            {
                body.localScale = Vector3.Lerp(rest, squashed, elapsed / Down);
                yield return null;
            }

            for (float elapsed = 0f; elapsed < Up; elapsed += Time.deltaTime)
            {
                // 되돌아오면서 조금 넘어갔다가 제자리로 — 고무공처럼 읽힌다.
                float k = elapsed / Up;
                float overshoot = Mathf.Sin(k * Mathf.PI) * .12f;

                body.localScale = Vector3.Lerp(squashed, rest, k) * (1f + overshoot);
                yield return null;
            }

            body.localScale = rest;
            squashRoutine = null;
        }

        /// <summary>
        /// 눌림을 적용할 **겉모습 오브젝트**. 루트가 아니다.
        ///
        /// ⚠ 루트의 배율을 건드리면 안 된다. 루트는 <c>NetworkTransform</c> 이 쥐고 있어
        ///   연출이 복제 값과 싸운다. 겉모습은 루트 밑 자식이므로 거기만 눌렀다 편다.
        /// </summary>
        private Transform ResolveVisualRoot()
        {
            if (visualRoot != null) return visualRoot;

            Renderer body = GetComponentInChildren<Renderer>(true);
            if (body == null) return null;

            Transform step = body.transform;

            // 루트의 바로 아래 자식까지 거슬러 올라간다.
            while (step != null && step.parent != null && step.parent != transform)
                step = step.parent;

            visualRoot = step != null && step != transform ? step : null;
            return visualRoot;
        }

        private Transform visualRoot;
        private Coroutine squashRoutine;
        private Vector3 squashRest;
        private bool squashCaptured;

        /// <summary>
        /// 몸을 흰색으로 물들였다가 되돌린다.
        ///
        /// 원래 색을 기억해 두었다가 그대로 복원한다. 겹쳐 돌면 <b>이미 흰색인 상태</b>를
        /// 원래 색으로 기억해 몬스터가 영영 하얗게 남으므로, 앞 코루틴을 반드시 끊는다.
        /// </summary>
        private IEnumerator HitFlashRoutine()
        {
            if (hitRenderers == null)
            {
                hitRenderers = GetComponentsInChildren<Renderer>(true);
                hitBaseColors = new Color[hitRenderers.Length];

                for (int i = 0; i < hitRenderers.Length; i++)
                {
                    Material m = hitRenderers[i] != null ? hitRenderers[i].material : null;
                    hitBaseColors[i] = m != null && m.HasProperty(BaseColorId) ? m.GetColor(BaseColorId)
                        : m != null && m.HasProperty(ColorId) ? m.GetColor(ColorId)
                        : Color.white;
                }
            }

            const float Duration = .14f;

            for (float elapsed = 0f; elapsed < Duration; elapsed += Time.deltaTime)
            {
                float k = 1f - elapsed / Duration;
                ApplyFlash(k);
                yield return null;
            }

            ApplyFlash(0f);
            flashRoutine = null;
        }

        private void ApplyFlash(float amount)
        {
            if (hitRenderers == null) return;

            for (int i = 0; i < hitRenderers.Length; i++)
            {
                Renderer r = hitRenderers[i];
                if (r == null) continue;

                Material m = r.material;
                if (m == null) continue;

                Color tinted = Color.Lerp(hitBaseColors[i], Color.white, amount);

                if (m.HasProperty(BaseColorId)) m.SetColor(BaseColorId, tinted);
                else if (m.HasProperty(ColorId)) m.SetColor(ColorId, tinted);
            }
        }

        /// <summary>베인 자리에서 튀는 작은 입자. 프리팹 없이 코드로 만든다.</summary>
        private void SpawnHitSpark()
        {
            GameObject spark = new GameObject("EnemyHitSpark");
            spark.transform.position = transform.position + Vector3.up * .8f;

            ParticleSystem particles = spark.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // 입자 14개 · 속도 3.4 로는 몸 뒤에 묻혀 거의 안 보였다. 두 배 가까이 키운다.
            ParticleSystem.MainModule main = particles.main;
            main.duration = .12f;
            main.loop = false;
            main.startLifetime = .3f;
            main.startSpeed = 6.2f;
            main.startSize = .15f;
            main.startColor = new Color(1f, .95f, .7f, 1f);
            main.gravityModifier = .8f;      // 튀었다가 떨어져야 무게가 있다
            main.maxParticles = 26;
            main.stopAction = ParticleSystemStopAction.Destroy;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .22f;

            particles.Play();
        }
    }
}
