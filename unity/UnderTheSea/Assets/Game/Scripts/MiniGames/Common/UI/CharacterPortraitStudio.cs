using System.Collections.Generic;
using System.Text;
using UnderTheSea.Character;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 🧑 <b>그 사람이 실제로 쓰는 캐릭터로 프로필 사진을 찍는다.</b> 미니게임 공통.
    ///
    /// 화면 밖 <b>촬영장</b>에 캐릭터 복사본을 세워 두고 한 장 찍는다.
    ///
    /// <code>
    ///   Take(자리, 캐릭터 루트) → 외형이 정해졌나? → 복사본을 멀리 세움 → 한 장 찍음 → 사진 돌려줌
    /// </code>
    ///
    /// 게임별 타입을 모른다. 누가 몇 번 자리인지는 부르는 쪽이 정하고, 여기는 <b>루트
    /// Transform 만</b> 받는다. 같은 자리 · 같은 사람 · 같은 외형이면 이미 찍은 사진을 그대로 준다.
    ///
    /// <b>기법은 <c>WarriorsPortrait</c> 와 같다.</b> 그쪽 주석에 적힌 함정(뼈 다시 잇기 ·
    /// forceRenderingOff 옮기기 · 촬영장 조명 · 서버에서 끄기)을 전부 그대로 따른다.
    /// 무쌍 · 배 게임은 아직 각자 것을 쓴다. 광산이 먼저 이것을 쓴다.
    ///
    /// ⚠ <b>원본이 보이는 동안에만 찍는다.</b> 대기 중이라 감춰진 사람(forceRenderingOff)을
    ///    복제하면 빈 사진이 나온다. 보일 때까지 미루고, 그 사이에는 <c>null</c> 을 준다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterPortraitStudio : MonoBehaviour
    {
        [Header("촬영장")]
        [Tooltip("복사본을 세워 둘 자리. 화면에 절대 안 들어올 만큼 멀어야 한다.")]
        [SerializeField] private Vector3 studioAt = new Vector3(0f, -5000f, 0f);

        [Tooltip("사람끼리 이만큼 떼어 놓는다. 카메라에 옆 사람이 걸리면 안 된다.")]
        [SerializeField, Min(2f)] private float apart = 20f;

        [Header("사진")]
        [Tooltip("배경색. 알파 0 이면 뒤 틀이 비친다.")]
        [SerializeField] private Color background = new Color(0f, 0f, 0f, 0f);

        [Tooltip("자리마다 둔 조명의 세기. 얼굴에서 약 3.4m. 0.35 는 광산 실측에서 얼굴이 거의 안 보일 만큼 어두웠다.")]
        [SerializeField, Min(0f)] private float lampIntensity = 2f;

        // ⚠ 구도의 잣대는 **기본 자세에서 발부터 머리 뼈까지의 높이**다.
        //   - 키(바운드)로 재면 모자를 쓴 사람은 꼭대기가 모자 끝이 되어, 카메라가 모자를 보고
        //     얼굴은 작게 아래로 밀렸다(광산 실측 — "흐릿하다"). 머리 뼈는 무엇을 쓰든 그 자리다.
        //   - **지금 자세**의 머리 뼈 높이로 재면 점프 중에 찍힌 사람이 멀리서 찍혔다
        //     (광산 실측 — 1P 머리 뼈 1.08m, 평소 0.62m). 그래서 크기는 자세와 상관없는
        //     Animator.humanScale 로 재고, 얼굴 자리만 지금 머리 뼈를 따라간다.
        //   아래 값은 모자 없는 광산 캐릭터(머리 뼈 0.62m)에서 예전 구도와 똑같이 맞춘 것이다.
        [Header("어디를 찍을지 — 머리 뼈 높이 대비")]
        [Tooltip("발에서 이만큼 올라간 높이가 얼굴 가운데다. 1 이 머리 뼈(목 위), 1.4 쯤이 얼굴 가운데.")]
        [SerializeField, Min(0f)] private float faceAt = 1.42f;

        [Tooltip("얼굴에서 이만큼 떨어져 찍는다. 직교 카메라라 크기에는 상관없고, 몸을 가리지 않을 만큼만 떨어지면 된다.")]
        [SerializeField, Min(.1f)] private float distance = 1.1f;

        [Tooltip("정면에서 이만큼 돌려 찍는다(도). 0 이 정자세.")]
        [SerializeField] private float turn = 20f;

        [Tooltip("화면에 담을 높이의 절반. 올릴수록 멀리서 찍은 것처럼 된다.")]
        [SerializeField, Min(.05f)] private float frame = .73f;

        /// <summary>머리 뼈를 못 찾을 때 머리 뼈 높이를 키의 이만큼으로 친다. 광산 캐릭터 0.62 / 1.26.</summary>
        private const float HeadShareOfHeight = .49f;

        /// <summary>
        /// 기본 자세의 머리 뼈 높이 ÷ <see cref="Animator.humanScale"/>. 광산 0.622 / 0.3914.
        /// 무쌍 캐릭터(크기 2.14 배)도 같은 뼈대라 1.329 / (0.3914 × 2.14) 로 같다.
        /// </summary>
        private const float HeadPerHumanScale = 1.59f;

        /// <summary>
        /// 사진은 HUD 칸의 **정확히 두 배** 로 찍는다. 두 배를 줄이면 2×2 픽셀 평균이라
        /// 선명하면서 계단도 없다. 256 을 76px 칸에 넣었을 때(3.4 배)는 밉맵 두 단계가
        /// 섞여 뿌옇게 보였다(광산 실측).
        /// </summary>
        private const int Supersample = 2;

        /// <summary>촬영용 레이어 이름. 이 레이어만 사진기가 본다.</summary>
        public const string PortraitLayerName = "Portrait";

        private sealed class Shot
        {
            public RenderTexture Texture;
            public Transform Owner;
            public string Signature;
            public GameObject Stand;
            public Camera Camera;
        }

        private readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();
        private readonly HashSet<int> lamps = new HashSet<int>();

        private Transform studio;
        private int layer = -1;
        private bool offline;

        /// <summary>
        /// ⚠ 데디케이티드 서버에서는 찍지 않는다. 셰이더가 빠진 서버 빌드에서 렌더를 부르면
        ///    매 프레임 예외가 나 틱이 밀린다(무쌍에서 실측). 볼 사람도 없다.
        /// </summary>
        private void Awake()
        {
            offline = FusionLaunchArguments.IsDedicatedServerProcess();
        }

        private void OnDestroy()
        {
            foreach (Shot shot in shots.Values)
            {
                if (shot.Camera != null) shot.Camera.targetTexture = null;
                if (shot.Texture != null) shot.Texture.Release();
            }

            shots.Clear();

            if (studio != null) Destroy(studio.gameObject);
        }

        /// <summary>
        /// <paramref name="key"/> 자리에 선 <paramref name="character"/> 의 사진.
        /// <paramref name="pixels"/> 는 그 사진을 끼울 칸의 <b>화면 픽셀</b> 크기(한 변)다.
        /// 아직 찍을 수 없으면(외형 미정 · 감춰짐) 지난 사진이나 <c>null</c>.
        /// 매 프레임 불러도 된다 — 사람 · 외형 · 칸 크기가 그대로면 찍지 않는다.
        /// </summary>
        public Texture Take(int key, Transform character, int pixels)
        {
            if (offline || character == null || !EnsureStudio()) return null;

            shots.TryGetValue(key, out Shot had);

            string signature = SignatureOf(character);
            if (signature == null) return had?.Texture;

            int size = Mathf.Clamp(pixels * Supersample, 64, 1024);

            if (had != null && had.Owner == character && had.Signature == signature &&
                had.Texture != null && had.Texture.width == size)
            {
                return had.Texture;
            }

            if (!EnsureLamp(key)) return had?.Texture;

            Shoot(key, character, signature, size);

            return shots.TryGetValue(key, out Shot now) ? now.Texture : null;
        }

        // ------------------------------------------------------------
        // 찍기
        // ------------------------------------------------------------

        private void Shoot(int key, Transform who, string signature, int size)
        {
            // ⚠ 아직 아무 렌더러도 안 보이면 찍지 않는다. 빈 사진이 박힌다. 다음 프레임에 다시 온다.
            if (!AnyVisible(who)) return;

            shots.TryGetValue(key, out Shot stale);

            if (stale != null)
            {
                // ⚠ targetTexture 를 문 채 카메라를 지우면 RT 가 같이 풀린다. 먼저 뗀다.
                if (stale.Camera != null)
                {
                    stale.Camera.targetTexture = null;
                    Destroy(stale.Camera.gameObject);
                }

                // 옛 복사본이 같은 자리에 남아 있으면 새 사진에 겹쳐 찍힌다.
                if (stale.Stand != null) Destroy(stale.Stand);
            }

            // ⚠ 루트(네트워크 부품)는 복제하지 않는다. 직계 자식을 전부 복제하고 뼈를 다시 잇는다.
            //    메시만 복제하면 뼈 참조가 원본 뼈대를 가리켜 메시가 원래 자리에 그려진다.
            //    뼈 Transform 은 지금 자세 그대로 복제되므로 Animator 없이도 그 자세로 찍힌다.
            GameObject stand = new GameObject($"{key + 1}P 사진용");
            stand.transform.SetParent(studio, false);
            stand.transform.localPosition = new Vector3(key * apart, 0f, 0f);
            stand.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            stand.transform.localScale = who.lossyScale;

            Dictionary<Transform, Transform> twin = new Dictionary<Transform, Transform>();

            foreach (Transform child in who)
            {
                GameObject copy = Instantiate(child.gameObject, stand.transform);
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale = child.localScale;

                PairUp(child, copy.transform, twin);
                CopyVisibility(child.gameObject, copy);
            }

            RebindBones(who, twin);
            StripAndLayer(stand);

            Bounds box = Measure(stand);

            if (box.size.y <= .01f)
            {
                Destroy(stand);
                return;
            }

            float feet = stand.transform.position.y;
            Transform head = HeadOf(who, twin);
            float neck = RestHeadHeight(who, head, feet, box);

            Vector3 lookAt = head != null
                ? head.position + Vector3.up * (neck * (faceAt - 1f))
                : new Vector3(box.center.x, feet + neck * faceAt, box.center.z);

            RenderTexture texture = stale != null && stale.Texture != null && stale.Texture.width == size
                ? stale.Texture   // ⚠ 다시 쓴다. 새로 만들고 옛것을 풀면 HUD 가 한 프레임 버려진 텍스처를 그린다.
                : NewTexture(key, size);

            // 칸 크기가 바뀌어(창 크기 변경) 새로 만들었으면 옛것은 이제 아무도 안 쓴다.
            if (stale != null && stale.Texture != null && stale.Texture != texture) stale.Texture.Release();

            Camera cam = new GameObject($"{key + 1}P 사진기").AddComponent<Camera>();
            cam.transform.SetParent(studio, false);

            // ⚠ 얼굴 쪽에 세운다. 뒤에 두면 뒤통수를 찍는다.
            cam.transform.position = lookAt + stand.transform.forward * (neck * distance);
            cam.transform.LookAt(lookAt);

            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.orthographic = true;
            cam.orthographicSize = neck * frame;
            cam.nearClipPlane = .01f;
            cam.farClipPlane = neck * 6f;
            cam.targetTexture = texture;

            // 한 번만 찍고 끈다. URP 는 Camera.Render 대신 렌더 요청을 쓴다.
            cam.enabled = false;

            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest { destination = texture };

            if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
            else cam.Render();

            shots[key] = new Shot
            {
                Texture = texture,
                Owner = who,
                Signature = signature,
                Stand = stand,
                Camera = cam,
            };

            Debug.Log(
                $"[초상] {key + 1}P 를 찍었습니다 — {who.name} · 머리 뼈 {neck:F2}m · {size}px" +
                (head != null ? string.Empty : " (머리 뼈를 못 찾아 키로 어림함)"), this);
        }

        private RenderTexture NewTexture(int key, int size)
        {
            // ⚠ **밉맵을 켠다.** 칸의 두 배로 찍으므로(Supersample) 그릴 때는 밉맵 한 단계
            //   아래, 곧 2×2 평균을 쓴다. 밉맵이 없으면 픽셀을 듬성듬성 집어 와 뭉개진다.
            //   밉맵과 MSAA 는 같이 못 쓴다. 가장자리 계단은 그 평균이 지운다.
            RenderTexture made = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
            {
                name = $"초상 {key + 1}P",
                useMipMap = true,
                autoGenerateMips = true,
                filterMode = FilterMode.Trilinear,
            };

            made.Create();
            return made;
        }

        // ------------------------------------------------------------
        // 촬영장
        // ------------------------------------------------------------

        private bool EnsureStudio()
        {
            if (layer < 0)
            {
                layer = LayerMask.NameToLayer(PortraitLayerName);

                if (layer < 0)
                {
                    Debug.LogError(
                        $"[초상] '{PortraitLayerName}' 레이어가 없습니다. " +
                        "Project Settings > Tags and Layers 에 추가해 주세요.", this);

                    offline = true;
                    return false;
                }
            }

            if (studio != null) return true;

            studio = new GameObject("초상 촬영장").transform;

            // 이 부품이 있는 씬에 둔다. 판이 끝나 씬이 내려가면 촬영장도 같이 사라진다.
            SceneManager.MoveGameObjectToScene(studio.gameObject, gameObject.scene);
            studio.position = studioAt;
            return true;
        }

        /// <summary>
        /// 자리마다 조명 하나. 없으면 지금 만들고 <c>false</c> — 그 자리는 다음 프레임에 찍는다.
        ///
        /// ⚠ 멀리 떨어진 촬영장에는 라이트 프로브가 없어 주변광이 검다. 전용 조명을 둔다.
        /// ⚠ Directional 은 씬 전체를 밝힌다(URP 는 라이트 cullingMask 를 안 본다). Point 로 둔다.
        /// ⚠ 사진기와 같은 프레임에 만들면 그 렌더의 컬링에 안 들어가 검게 찍힌다. 한 프레임 먼저 만든다.
        /// ⚠ 자리마다 두는 이유 — Point 는 거리 제곱으로 어두워져, 조명 하나로는 20m 씩 떨어진
        ///    네 자리를 고르게 못 비춘다.
        /// </summary>
        private bool EnsureLamp(int key)
        {
            if (lamps.Contains(key)) return true;

            Light lamp = new GameObject($"{key + 1}P 촬영 조명").AddComponent<Light>();
            lamp.transform.SetParent(studio, false);
            lamp.transform.localPosition = new Vector3(key * apart, 3f, 3f);
            lamp.type = LightType.Point;
            lamp.range = apart * .5f;
            lamp.intensity = lampIntensity;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;

            lamps.Add(key);
            return false;
        }

        // ------------------------------------------------------------
        // 복제
        // ------------------------------------------------------------

        /// <summary>
        /// 복제본의 머리 뼈. 원본 루트의 휴머노이드 <see cref="Animator"/> 에서 찾아 짝으로 옮긴다.
        /// 휴머노이드가 아니거나 짝이 없으면 null — 그때는 키로 어림한다.
        /// </summary>
        private static Transform HeadOf(Transform who, Dictionary<Transform, Transform> twin)
        {
            Animator animator = who.GetComponent<Animator>();
            if (animator == null || !animator.isHuman) return null;

            Transform head = animator.GetBoneTransform(HumanBodyBones.Head);
            return head != null && twin.TryGetValue(head, out Transform copy) ? copy : null;
        }

        /// <summary>
        /// 기본 자세에서 발부터 머리 뼈까지의 높이. 구도의 크기를 여기에 맞춘다.
        ///
        /// 휴머노이드면 <see cref="Animator.humanScale"/> 로 잰다 — 자세와 상관없는 값이다.
        /// 아니면 지금 머리 뼈 높이, 그것도 없으면 키로 어림한다.
        /// </summary>
        private static float RestHeadHeight(Transform who, Transform head, float feet, Bounds box)
        {
            Animator animator = who.GetComponent<Animator>();

            if (animator != null && animator.isHuman && animator.humanScale > .01f)
            {
                return animator.humanScale * who.lossyScale.y * HeadPerHumanScale;
            }

            if (head != null && head.position.y > feet + .01f) return head.position.y - feet;

            return (box.max.y - feet) * HeadShareOfHeight;
        }

        /// <summary>원본 트리와 복제 트리를 같은 순서로 걸어가며 짝을 기록한다.</summary>
        private static void PairUp(Transform origin, Transform copy, Dictionary<Transform, Transform> twin)
        {
            twin[origin] = copy;

            int n = Mathf.Min(origin.childCount, copy.childCount);
            for (int i = 0; i < n; i++) PairUp(origin.GetChild(i), copy.GetChild(i), twin);
        }

        /// <summary>복제된 스킨 메시의 뼈 참조를 복제된 뼈대로 옮긴다.</summary>
        private static void RebindBones(Transform origin, Dictionary<Transform, Transform> twin)
        {
            foreach (SkinnedMeshRenderer from in origin.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!twin.TryGetValue(from.transform, out Transform mirror)) continue;

                SkinnedMeshRenderer to = mirror.GetComponent<SkinnedMeshRenderer>();
                if (to == null) continue;

                Transform[] bones = from.bones;
                Transform[] moved = new Transform[bones.Length];

                for (int b = 0; b < bones.Length; b++)
                {
                    moved[b] = bones[b] != null && twin.TryGetValue(bones[b], out Transform t) ? t : bones[b];
                }

                to.bones = moved;

                if (from.rootBone != null && twin.TryGetValue(from.rootBone, out Transform root)) to.rootBone = root;
            }
        }

        /// <summary>
        /// 원본이 무엇을 보이고 감췄는지 복제본에 옮긴다.
        ///
        /// ⚠ <c>forceRenderingOff</c> 는 실행 중에만 있는 값이라 <c>Instantiate</c> 가 복사하지
        ///    않는다. 빠뜨리면 기본 얼굴 + 모든 슬롯 파츠가 전부 켜진 채 찍힌다.
        /// </summary>
        private static void CopyVisibility(GameObject origin, GameObject copy)
        {
            Renderer[] from = origin.GetComponentsInChildren<Renderer>(true);
            Renderer[] to = copy.GetComponentsInChildren<Renderer>(true);

            if (from.Length != to.Length)
            {
                Debug.LogWarning($"[초상] 원본({from.Length})과 복제본({to.Length})의 렌더러 수가 다릅니다.");
                return;
            }

            for (int i = 0; i < from.Length; i++)
            {
                if (from[i] == null || to[i] == null) continue;

                to[i].forceRenderingOff = from[i].forceRenderingOff;
                to[i].enabled = from[i].enabled;
            }
        }

        /// <summary>
        /// 복사본에서 움직이는 것을 끄고 촬영용 레이어로 옮긴다. 지우지 않고 끈다 —
        /// 컴포넌트끼리 의존이 있어 지우는 순서를 맞추기 어렵다.
        /// </summary>
        private void StripAndLayer(GameObject stand)
        {
            foreach (MonoBehaviour script in stand.GetComponentsInChildren<MonoBehaviour>(true)) script.enabled = false;
            foreach (Collider bump in stand.GetComponentsInChildren<Collider>(true)) bump.enabled = false;

            // 머리 위 이름표 같은 월드 캔버스는 사진에 들어오면 안 된다.
            foreach (Canvas canvas in stand.GetComponentsInChildren<Canvas>(true)) canvas.enabled = false;

            foreach (ParticleSystem ps in stand.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (Renderer r in stand.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsBodyRenderer(r)) r.enabled = false;
            }

            foreach (Transform t in stand.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        /// <summary>몸을 그리는 렌더러인가. 파티클 · 트레일 · 라인은 바운드를 오염시킨다.</summary>
        private static bool IsBodyRenderer(Renderer r) => r is SkinnedMeshRenderer || r is MeshRenderer;

        /// <summary>지금 이 사람에게 실제로 그려지는 몸 렌더러가 하나라도 있는가.</summary>
        private static bool AnyVisible(Transform who)
        {
            foreach (Renderer r in who.GetComponentsInChildren<Renderer>(false))
            {
                if (r != null && IsBodyRenderer(r) && r.enabled && !r.forceRenderingOff) return true;
            }

            return false;
        }

        private static Bounds Measure(GameObject stand)
        {
            Bounds box = default;
            bool any = false;

            foreach (Renderer r in stand.GetComponentsInChildren<Renderer>(false))
            {
                if (!IsBodyRenderer(r) || !r.enabled || r.forceRenderingOff) continue;

                if (!any) { box = r.bounds; any = true; }
                else box.Encapsulate(r.bounds);
            }

            return any ? box : new Bounds(stand.transform.position, Vector3.zero);
        }

        // ------------------------------------------------------------
        // 외형 서명
        // ------------------------------------------------------------

        /// <summary>
        /// 무엇을 입고 있는가를 한 줄로. 달라지면 다시 찍는다.
        /// 아직 외형이 안 정해졌거나 사라지는 중이면 <c>null</c> — 그때 찍으면 기본 얼굴이 박힌다.
        /// </summary>
        private static string SignatureOf(Transform who)
        {
            CharacterAppearanceApplier applier = who.GetComponent<CharacterAppearanceApplier>();
            NetworkPlayerAppearance networked = who.GetComponent<NetworkPlayerAppearance>();

            if (networked != null)
            {
                if (networked.Object == null || !networked.Object.IsValid) return null;
                if (!networked.AppearanceReady) return null;
            }

            if (applier == null) return "(외형 없음)";

            List<string> keys = new List<string>(applier.EquippedKeys);
            keys.Sort(System.StringComparer.Ordinal);

            StringBuilder text = new StringBuilder();
            foreach (string key in keys) text.Append(key).Append(',');

            return text.Length > 0 ? text.ToString() : "(맨몸)";
        }
    }
}
