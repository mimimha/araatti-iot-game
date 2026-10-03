using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 등록이 성공한 <b>순간</b>에 제단 돌기둥에서 파란 펄스를 한 번 터뜨린다.
    ///
    /// <code>
    ///   성공한 논리적 등록 1건  →  펄스 정확히 1회
    /// </code>
    ///
    /// 등록한 조각 수와 펄스 횟수는 <b>무관하다.</b> 1개를 등록해도 1회, 50개를 등록해도 1회다.
    /// 그래서 <see cref="PlayOnce"/> 는 수량을 받지 않는다. (결정 #9, 설계 12.2절)
    ///
    /// <b>이 부품은 네트워크를 모른다.</b> <c>NetworkBehaviour</c> 가 아니고 RPC 도 없다.
    /// <see cref="AltarOfferingRelay"/> 가 받은 사건을 넘겨 주고, 여기서는 그것을 화면으로만 바꾼다.
    ///
    /// ⚠ <b><see cref="AltarState"/> 를 읽지 않는다.</b> <c>Changed</c> 도 <c>AltarActivated</c> 도
    ///    보지 않는다. 상태는 지속되는 값이고 펄스는 순간적인 사건이라, 상태를 트리거로 쓰면
    ///    로비에 들어갈 때마다 · 30초 폴링마다 · Late Join 때마다 제단이 번쩍인다. (설계 12.3 · 12.5절)
    ///
    /// ⚠ <b>기존 평소 연출을 껐다 켜지 않는다.</b> <c>AltarBeam</c> · <c>Light_Monolith</c> ·
    ///    <c>Light_Crystal</c> 은 평소에도 켜져 있는 제단의 기본 연출이고 이 부품이 손대지 않는다.
    ///    펄스는 그 <b>위에 겹쳐서</b> 얹힌다. 그래서 로비 진입 직후 "번쩍했다 사라지는"
    ///    한 프레임이 생기지 않는다. (설계 12.1절)
    ///
    /// 문서: docs/prd/lobby_altar_inventory_system_design.md 12.2 · 12.3 · 12.6절
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AltarVfxController : MonoBehaviour
    {
        [Header("펄스 이펙트")]
        [Tooltip("한 번 재생할 파란 세로 이펙트. P_HeartAltar 안에 " +
                 "**꺼진 채로** 넣어 둔 자식 오브젝트를 연결한다. " +
                 "여기 있는 것을 직접 켜지 않고, 사건마다 복제해서 재생한다.")]
        [SerializeField] private GameObject offeringPulseEffect;

        [Tooltip("한 번 재생하는 길이(초). 이 시간이 지나면 복제본을 지운다. " +
                 "연결한 이펙트가 끝까지 나오는 길이로 Scene 에서 맞춘다.")]
        [SerializeField, Min(0.1f)] private float pulseDuration = 2f;

        [Header("돌기둥 문양 발광")]
        [Tooltip("문양이 새겨진 돌기둥의 MeshRenderer. P_HeartAltar/Monolith 를 연결한다. " +
                 "그 Renderer 의 Material 슬롯은 두 개여야 한다 — " +
                 "[0] 돌(M_Altar_Monolith_Glow), [1] 문양 발광(M_Altar_Monolith_RuneGlow).")]
        [SerializeField] private Renderer monolithRenderer;

        [Tooltip("문양이 켜질 색. 목표는 청록색이다. 세기는 아래 항목이 따로 정한다.")]
        [SerializeField, ColorUsage(false, false)]
        private Color monolithGlowColor = new Color(0.10f, 0.95f, 1f, 1f);

        [Tooltip("최대 세기. Bloom 이 얼마나 번지는지 보면서 Game View 에서 맞춘다.")]
        [SerializeField, Min(0f)] private float monolithGlowIntensity = 4f;

        [Tooltip("문양 맨 아래에서 꼭대기까지 차오르는 시간(초). 이 구간이 이 연출의 주인공이다.")]
        [FormerlySerializedAs("glowFadeInSeconds")]
        [SerializeField, Min(0f)] private float glowSweepUpSeconds = 0.8f;

        [Tooltip("전체가 켜진 채로 머무는 시간(초).")]
        [SerializeField, Min(0f)] private float glowHoldSeconds = 0.25f;

        [Tooltip("전체 점등 상태에서 0 으로 사그라드는 시간(초). " +
                 "이 동안 Progress 는 1 로 유지된다 — 위에서 아래로 되감기지 않는다.")]
        [SerializeField, Min(0f)] private float glowFadeOutSeconds = 0.55f;

        /// <summary>
        /// 지금 로비에 있는 제단 펄스. 없으면 null.
        ///
        /// <see cref="AltarOfferingRelay"/> 가 이것을 찾아 넘긴다.
        /// <c>LobbyChatView.Current</c> 와 같은 방식이다 — 펄스가 없는 화면(미니게임 ·
        /// Dedicated Server)에서는 null 이고, 받는 쪽이 조용히 넘어간다.
        /// </summary>
        public static AltarVfxController Current { get; private set; }

        /// <summary>
        /// 동시에 살아 있을 수 있는 복제본의 상한.
        ///
        /// ⚠ <b>정상적인 사건을 버리기 위한 값이 아니다.</b> 사람이 누르는 등록은 아무리 겹쳐도
        ///    몇 건이다. 이 상한은 악성 클라이언트가 서로 다른 requestId 로 RPC 를 도배할 때
        ///    (설계 9.3절이 남겨 둔 한계) 복제본이 끝없이 늘어나는 것만 막는다.
        /// </summary>
        private const int MaxConcurrentPulses = 8;

        private int livePulses;

        /// <summary>
        /// <c>Altar/MonolithRuneGlow</c> 의 실제 property 이름. 셰이더에서 확인한 값이다.
        ///
        /// ⚠ 돌 Material 의 <c>_Emission_Color</c> 와 이름이 겹치지 않게 일부러 다르게 두었다.
        ///    MaterialPropertyBlock 은 Renderer 의 <b>모든</b> 슬롯에 적용되므로, 이름이 같으면
        ///    돌 쪽 emission 까지 같이 올라가 예전의 "전체 동시 점등" 이 되살아난다.
        /// </summary>
        private static readonly int RuneGlowColorId = Shader.PropertyToID("_RuneGlowColor");

        /// <summary>0 이면 문양이 전부 꺼진 상태, 1 이면 전부 켜진 상태.</summary>
        private static readonly int RuneGlowProgressId = Shader.PropertyToID("_RuneGlowProgress");

        /// <summary>
        /// 문양 색만 바꾸는 데 쓰는 블록. <b>한 번 만들어 계속 쓴다.</b>
        ///
        /// ⚠ <c>renderer.material</c> 을 건드리면 등록마다 Material 인스턴스가 하나씩 새로 생긴다.
        ///    <c>MaterialPropertyBlock</c> 은 Material asset 을 복제하지 않는다.
        /// </summary>
        private MaterialPropertyBlock glowBlock;

        /// <summary>아직 재생하지 못한 문양 발광 사건의 수. <b>0 으로 깎아 내리지 않는다.</b></summary>
        private int pendingGlowPulses;

        private Coroutine glowRoutine;

        private void Awake()
        {
            if (offeringPulseEffect == null)
            {
                Debug.LogWarning(
                    $"[AltarVfx] {nameof(offeringPulseEffect)} 이 비어 있습니다. " +
                    "등록이 성공해도 펄스가 나오지 않습니다. Inspector 에서 연결하세요.", this);
                return;
            }

            // ⚠ 원본은 언제나 꺼져 있어야 한다. 켜진 채로 저장되어 있으면 로비에 들어간 순간
            //    평소에도 펄스가 보인다. 여기서 한 번 내려 주되, 프리팹을 고쳐 달라고 알린다.
            if (offeringPulseEffect.activeSelf)
            {
                offeringPulseEffect.SetActive(false);
                Debug.LogWarning(
                    $"[AltarVfx] {offeringPulseEffect.name} 이 켜진 채로 저장되어 있습니다. " +
                    "프리팹에서 꺼 두세요 — 로비 진입 직후 한 프레임 보일 수 있습니다.", this);
            }
        }

        /// <summary>
        /// 문양을 확실히 꺼 둔 상태에서 시작한다.
        ///
        /// Material asset 의 <c>_Emission_Color</c> 는 이미 검정이지만, 누가 에디터에서
        /// 올려 둔 채로 저장했더라도 로비에서는 꺼진 모습으로 시작해야 한다.
        /// </summary>
        /// <param name="intensity">전체 세기. 0 이면 아무것도 더하지 않는다.</param>
        /// <param name="progress">
        /// 아래에서 위로 어디까지 점등됐는지. 0 = 전부 꺼짐, 1 = 전부 켜짐.
        ///
        /// ⚠ 세기와 진행도는 **다른 값**이다. Fade Out 동안 진행도는 1 로 두고 세기만 내린다 —
        ///    그러지 않으면 불이 위에서 아래로 되감기는 것처럼 보인다.
        /// </param>
        private void ApplyGlow(float intensity, float progress)
        {
            if (monolithRenderer == null)
            {
                return;
            }

            glowBlock ??= new MaterialPropertyBlock();

            // ⚠ 기존 블록을 먼저 읽어 온다. 다른 코드가 같은 Renderer 에 무언가를 올려 두었다면
            //    통째로 덮어써서 지우지 않는다.
            monolithRenderer.GetPropertyBlock(glowBlock);

            // 발광은 선형 공간 값이다. 인스펙터의 색은 sRGB 라 한 번 변환한다.
            Color linear = monolithGlowColor.linear * intensity;
            linear.a = 1f;

            glowBlock.SetColor(RuneGlowColorId, linear);
            glowBlock.SetFloat(RuneGlowProgressId, Mathf.Clamp01(progress));
            monolithRenderer.SetPropertyBlock(glowBlock);
        }

        private void OnEnable()
        {
            // 화면이 없는 프로세스(Dedicated Server)에서는 등록하지 않는다. 아무도 보지 않는
            // 파티클을 굴리지 않고, 받는 쪽은 Current == null 을 보고 조용히 넘어간다.
            // (DedicatedServerParticles 가 방출을 잠그기도 하지만, 애초에 만들지 않는 편이 낫다)
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                return;
            }

            Current = this;

            // 평소 모습으로 시작한다. 이 한 줄이 "로비에 들어갔더니 이미 빛나고 있다" 를 막는다.
            ApplyGlow(0f, 0f);
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }

            // 꺼지면 코루틴도 함께 죽는다. 세던 수를 되돌려 주지 않으면 다시 켤 때
            // "이미 8개가 돌고 있다" 로 남아 펄스가 영영 안 나온다.
            livePulses = 0;

            // 발광 코루틴도 함께 죽는다. 켜진 채로 멈추면 그대로 남으므로 손으로 되돌린다.
            pendingGlowPulses = 0;
            glowRoutine = null;
            ApplyGlow(0f, 0f);
        }

        /// <summary>
        /// 펄스를 한 번 재생한다. <b>성공한 등록 사건 하나가 여기로 온다.</b>
        ///
        /// ⚠ <b>수량을 받지 않는다.</b> 이펙트의 세기도 횟수도 등록량과 연결하지 않는다.
        ///
        /// ⚠ <b>사건을 합치거나 버리지 않는다.</b> 0.3초 간격으로 두 건이 들어오면 두 번 재생한다.
        ///    시간이 가깝다는 이유로 두 번째를 버리면 사건이 사라진다. (설계 12.6절)
        ///
        /// <b>왜 원본을 켜는 대신 복제하는가.</b> 오브젝트가 하나뿐이면 두 번째 사건에서
        /// 그것을 다시 켜야 하고, 그러면 <b>앞 사건의 재생이 중간에 끊긴다.</b>
        /// 연결할 자산(<c>ARPG Effects</c> 의 파란 포탈 계열)은 짧은 one-shot 버스트라
        /// 겹쳐 재생해도 자연스럽다. 복제본을 따로 두면 사건 수와 펄스 수가 정확히 같아진다.
        ///
        /// 등록은 사람이 버튼을 눌러야 일어나는 드문 사건이라 복제 비용이 문제가 되지 않는다.
        /// </summary>
        public void PlayOnce()
        {
            if (!isActiveAndEnabled)
            {
                return;
            }

            // ① 주연 — 돌기둥 문양이 켜진다.
            EnqueueMonolithGlow();

            // ② 보조 — 기둥 아래에서 파티클이 퍼진다.
            PlayOfferingPulseParticles();
        }

        /// <summary>
        /// 문양 발광 사건을 하나 쌓는다.
        ///
        /// ⚠ <b>지금 재생 중이면 다시 시작하지 않고 뒤에 세운다.</b> Renderer 가 하나뿐이라
        ///    새 사건이 앞 사건을 덮어쓰면 첫 번째 flash 가 중간에 사라진다.
        ///    서로 다른 requestId 의 성공 2건은 2번 보여야 한다. (설계 12.6절)
        ///
        /// ⚠ <b>시간으로 합치거나 버리지 않는다.</b> 0.3초 간격으로 들어온 두 사건도 둘 다 남는다.
        ///    쌓인 수만큼 순서대로 재생한다 — 성공 event 수 = flash 수.
        /// </summary>
        private void EnqueueMonolithGlow()
        {
            if (monolithRenderer == null)
            {
                return;
            }

            pendingGlowPulses++;

            if (glowRoutine == null)
            {
                glowRoutine = StartCoroutine(GlowQueueRoutine());
            }
        }

        private IEnumerator GlowQueueRoutine()
        {
            while (pendingGlowPulses > 0)
            {
                pendingGlowPulses--;
                yield return GlowOnceRoutine();
            }

            // 큐를 다 비우고 나서 평소 모습으로 정확히 되돌린다.
            ApplyGlow(0f, 0f);
            glowRoutine = null;
        }

        /// <summary>
        /// 한 번의 점등 — <b>아래에서 위로 차오르고</b>, 전체가 잠깐 머물고, 부드럽게 사그라든다.
        ///
        /// <code>
        ///   1. 세기를 곧바로 최대로 올린다        (밝기는 처음부터 최대)
        ///   2. Progress 0 → 1                     (아래 문양부터 차례로 켜진다)
        ///   3. Progress 1 로 Hold                 (전체 점등 유지)
        ///   4. Progress 1 고정, 세기 최대 → 0     (전체가 함께 사그라든다)
        ///   5. 세기 0, Progress 0 으로 복귀
        /// </code>
        ///
        /// ⚠ <b>4단계에서 Progress 를 내리지 않는다.</b> 내리면 불이 위에서 아래로 되감기는
        ///    것처럼 보인다. 꺼지는 것은 세기 쪽이다.
        ///
        /// ⚠ 지나간 아래쪽 문양은 계속 켜져 있다. 밝은 띠 하나가 지나가는 방식이 아니라
        ///    아래에서부터 누적되는 방식이다 — 셰이더의 <c>reveal</c> 이 그것을 보장한다.
        /// </summary>
        private IEnumerator GlowOnceRoutine()
        {
            // 1~2. 세기는 최대로 두고 진행도만 올린다.
            for (float t = 0f; t < glowSweepUpSeconds; t += Time.deltaTime)
            {
                ApplyGlow(monolithGlowIntensity, t / glowSweepUpSeconds);
                yield return null;
            }

            // 3. 전체 점등 유지.
            ApplyGlow(monolithGlowIntensity, 1f);

            if (glowHoldSeconds > 0f)
            {
                yield return new WaitForSeconds(glowHoldSeconds);
            }

            // 4. 진행도는 1 그대로, 세기만 내린다.
            for (float t = 0f; t < glowFadeOutSeconds; t += Time.deltaTime)
            {
                ApplyGlow(Mathf.SmoothStep(1f, 0f, t / glowFadeOutSeconds) * monolithGlowIntensity, 1f);
                yield return null;
            }

            // 5. 평소 상태로 정확히 복귀.
            ApplyGlow(0f, 0f);
        }

        /// <summary>
        /// 기둥 아래 파티클을 한 번 재생한다. 기존 동작 그대로다.
        /// </summary>
        private void PlayOfferingPulseParticles()
        {
            if (offeringPulseEffect == null)
            {
                return;
            }

            if (livePulses >= MaxConcurrentPulses)
            {
                Debug.LogWarning(
                    $"[AltarVfx] 펄스가 동시에 {MaxConcurrentPulses}개를 넘어 이번 것은 건너뜁니다. " +
                    "정상적인 등록으로는 일어나지 않습니다.", this);
                return;
            }

            Transform parent = offeringPulseEffect.transform.parent != null
                ? offeringPulseEffect.transform.parent
                : transform;

            // 원본과 같은 부모 · 같은 자리에 둔다. 그래서 위치 · 회전 · 크기를 코드가 정하지 않고
            // Scene 뷰에서 잡은 원본의 Transform 이 그대로 결정한다. 월드 좌표를 박지 않는다.
            GameObject clone = Instantiate(offeringPulseEffect, parent);
            clone.name = offeringPulseEffect.name + " (pulse)";
            clone.SetActive(true);

            // playOnAwake 가 꺼진 자산도 있어 한 번 직접 켠다. 켜져 있으면 Play 는 무해하다.
            foreach (ParticleSystem system in clone.GetComponentsInChildren<ParticleSystem>(true))
            {
                system.Play(withChildren: false);
            }

            // ⚠ 지우는 일을 코루틴에 맡기지 않는다. 코루틴은 오브젝트가 꺼지면 함께 죽는데
            //    복제본은 제단 쪽에 매달려 있어 그대로 남는다. Destroy 의 지연 인자는
            //    엔진이 지키므로 코루틴이 죽어도 제 시간에 사라진다.
            Destroy(clone, pulseDuration);

            livePulses++;
            StartCoroutine(ReleaseSlotLater());
        }

        /// <summary>복제본이 사라질 때쯤 자리를 하나 돌려준다. 세는 것 말고는 하지 않는다.</summary>
        private IEnumerator ReleaseSlotLater()
        {
            yield return new WaitForSeconds(pulseDuration);

            livePulses--;
        }

        /// <summary>
        /// 연출만 확인한다. <b>서버도 Fusion 도 거치지 않는다.</b>
        ///
        /// 실제 등록 경로(<c>AltarOfferingRelay</c> → <see cref="PlayOnce"/>)와 같은 입구를 쓰므로
        /// 보이는 것이 실전과 같다. 다만 이것으로는 DB · 조각 수 · 다른 클라이언트가 바뀌지 않는다.
        ///
        /// ⚠ 코루틴이 필요해서 Play Mode 에서만 동작한다.
        /// </summary>
        [ContextMenu("디버그 — 등록 성공 펄스 1회")]
        private void DebugPlayOnce()
        {
            if (!Application.isPlaying)
            {
                Debug.LogWarning(
                    "[AltarVfx] 이 메뉴는 Play Mode 에서만 동작합니다. " +
                    "재생 중에 같은 메뉴를 다시 누르세요.", this);
                return;
            }

            PlayOnce();
        }

        // ------------------------------------------------------------
        // 점등 방향 확인용. Play Mode 가 아니어도 동작한다 —
        // 코루틴 없이 MaterialPropertyBlock 만 한 번 쓰기 때문이다.
        // Scene View 에서 아래→위 방향을 눈으로 확인할 때 쓴다.
        //
        // ⚠ 실제 gameplay 경로(PlayOnce)와 무관하다. 여기서 바꾼 값은 직렬화되지 않으므로
        //    씬을 다시 열면 사라진다. 확인이 끝나면 "문양 끄기" 로 되돌린다.
        // ------------------------------------------------------------

        [ContextMenu("디버그 — Glow Progress 0%")]
        private void DebugProgress000() => ApplyGlow(monolithGlowIntensity, 0f);

        [ContextMenu("디버그 — Glow Progress 25%")]
        private void DebugProgress025() => ApplyGlow(monolithGlowIntensity, 0.25f);

        [ContextMenu("디버그 — Glow Progress 50%")]
        private void DebugProgress050() => ApplyGlow(monolithGlowIntensity, 0.5f);

        [ContextMenu("디버그 — Glow Progress 75%")]
        private void DebugProgress075() => ApplyGlow(monolithGlowIntensity, 0.75f);

        [ContextMenu("디버그 — Glow Progress 100%")]
        private void DebugProgress100() => ApplyGlow(monolithGlowIntensity, 1f);

        [ContextMenu("디버그 — 문양 끄기 (평소 상태)")]
        private void DebugGlowOff() => ApplyGlow(0f, 0f);
    }
}
