using System.Collections.Generic;
using System.Text;
using UnderTheSea.Character;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using Warriors.Net;

namespace Warriors
{
    /// <summary>
    /// 🧑 <b>HUD 의 프로필 사진을 그 사람이 실제로 쓰는 캐릭터로 찍는다.</b>
    ///
    /// 두 사람이 각자 커스터마이즈한 모습으로 들어오는데, 그림 한 장을 박아 두면 두 칸이
    /// 같은 얼굴이 된다. 사람을 가르라고 만든 칸인데 갈라지지 않는다.
    ///
    /// 그래서 화면 밖 <b>촬영장</b>에 각자의 캐릭터 복사본을 세워 두고 한 번 찍는다.
    ///
    /// <code>
    ///   사람이 들어옴 → 외형이 정해짐 → 복사본을 멀리 세움 → 한 장 찍음 → 카메라 끔
    /// </code>
    ///
    /// ⚠ <b>한 번만 찍는다.</b> 초상화는 움직일 필요가 없다. 카메라를 매 프레임 돌리면 공짜가
    ///    아니지만 한 장 찍고 끄면 사실상 공짜다. 외형이 바뀌면 그 사람만 다시 찍는다.
    ///
    /// <b>왜 배 게임 것을 그대로 안 쓰는가.</b> <c>ShipCoopPortrait</c> 가 같은 일을 하지만
    /// 배의 <c>TaskWorker</c> 타입에 묶여 있고, 몸을 <c>"Body"</c> 라는 자식에서 찾는다.
    /// 무쌍의 캐릭터는 그 래퍼 없이 메시가 바로 달려 있어 그 경로가 맞지 않는다.
    /// 지금은 두 벌이지만, <b>같은 기법이므로 나중에 공통으로 묶는 것이 맞다.</b>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsPortrait : MonoBehaviour
    {
        [Header("촬영장")]
        [Tooltip("복사본을 세워 둘 자리. 화면에 절대 안 들어올 만큼 멀어야 한다.")]
        [SerializeField] private Vector3 studioAt = new Vector3(0f, -5000f, 0f);

        [Tooltip("사람끼리 이만큼 떼어 놓는다. 카메라에 옆 사람이 걸리면 안 된다.")]
        [SerializeField, Min(2f)] private float apart = 20f;

        [Header("사진")]
        [Tooltip("한 변 크기(픽셀). HUD 칸이 100px 라 256 이면 넉넉하다.")]
        [SerializeField, Min(64)] private int size = 256;

        [Tooltip("배경색. 알파 0 이면 뒤 접시가 비친다.")]
        [SerializeField] private Color background = new Color(0f, 0f, 0f, 0f);

        [Header("어디를 찍을지")]
        [Tooltip("머리 꼭대기에서 이만큼 내려온 곳을 본다(키 대비). 0 정수리 · 0.25 얼굴 가운데.")]
        [SerializeField, Range(0f, .5f)] private float lookAtDrop = .30f;

        [Tooltip("얼굴에서 이만큼 떨어져 찍는다(키 대비). 작을수록 얼굴이 꽉 찬다.")]
        [SerializeField, Min(.1f)] private float distance = .55f;

        [Tooltip("정면에서 이만큼 돌려 찍는다(도). 0 이 정자세. 살짝 틀어야 사람처럼 보인다.")]
        [SerializeField] private float turn = 20f;

        [Tooltip("화면에 담을 높이(키 대비). 올릴수록 멀리서 찍은 것처럼 된다.")]
        [SerializeField, Min(.05f)] private float frame = .36f;

        /// <summary>찍어 둔 사진 한 장과, 그것이 <b>어떤 외형이었는지</b>.</summary>
        private sealed class Shot
        {
            public RenderTexture Texture;
            public string Signature;
            public int StudioIndex;
            public Camera Camera;
        }

        private readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();

        private readonly Dictionary<int, RawImage> faces = new Dictionary<int, RawImage>();

        private Transform studio;
        private int layer = -1;

        /// <summary>촬영용 레이어 이름. 이 레이어만 사진기가 본다.</summary>
        public const string PortraitLayerName = "Portrait";

        /// <summary>
        /// ⚠ <b>데디케이티드 서버에서는 절대 돌면 안 된다.</b>
        ///
        /// 서버 빌드는 "Dedicated Server Optimizations" 로 셰이더를 통째로 뺀다. 그런데 이
        /// 컴포넌트는 화면이 있든 없든 씬에 그대로 있어 매 프레임 새 카메라를 만들고
        /// <c>cam.Render()</c> 를 부르려 했다. 셰이더가 없는 채로 URP 파이프라인을 새로
        /// 만들려다 <c>NullReferenceException</c> 이 나고, 이게 <b>매 프레임 반복</b>되면서
        /// 서버 틱이 밀려 실제로 두 사람이 게임 도중 튕겼다 (로그에 32번 연속 스택트레이스).
        ///
        /// 서버에는 볼 사람이 없으니 애초에 찍을 이유도 없다.
        /// </summary>
        private void Awake()
        {
            if (FusionLaunchArguments.IsDedicatedServerProcess())
            {
                enabled = false;
            }
        }

        private void Update()
        {
            if (!EnsureStudio()) return;

            TakeMissingShots();
            ShowShots();
        }

        // ------------------------------------------------------------
        // 찍기
        // ------------------------------------------------------------

        /// <summary>
        /// 아직 못 찍었거나 외형이 바뀐 사람만 찍는다.
        ///
        /// ⚠ <b>외형이 준비되기 전에 찍으면 기본 얼굴이 박힌다.</b> 서버가 그 사람의 외형을
        ///    정하기 전에는 <see cref="SignatureOf"/> 가 <c>null</c> 을 돌려주고, 그때는 미룬다.
        /// </summary>
        private void TakeMissingShots()
        {
            WarriorsPlayerLife[] crew = FindObjectsByType<WarriorsPlayerLife>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);

            for (int i = 0; i < crew.Length; i++)
            {
                WarriorsPlayerLife who = crew[i];
                if (who == null) continue;

                string signature = SignatureOf(who);
                if (signature == null) continue;   // 아직 외형이 안 왔다. 다음 차례에.

                int id = who.PlayerIndex;

                if (shots.TryGetValue(id, out Shot already) && already.Signature == signature) continue;

                Shoot(who, id, signature);
            }
        }

        private void Shoot(WarriorsPlayerLife who, int id, string signature)
        {
            Transform body = FindBody(who.transform);
            if (body == null) return;

            // ⚠ **찍기 직전에 죽었던 카메라를 지운다.** 예전에는 다시 찍을 때마다 카메라
            //    오브젝트를 새로 만들고 옛 것은 그대로 두었다. 외형이 바뀔 때마다(장비 갱신,
            //    늦은 접속 등) 다시 찍으므로 죽은 카메라가 계속 쌓인다.
            // ⚠ 아직 아무 렌더러도 안 보이면 찍지 않는다. 진단 로그로 "렌더러 보임 0 · 숨김 1" 인
            //    채 찍힌 빈 사진이 확인됐다 — 외형 준비 신호(AppearanceReady)와 실제
            //    forceRenderingOff 해제 사이에 한 프레임 이상 틈이 있다. 다음 Update 에 다시 온다.
            if (!AnyVisible(who.transform)) return;

            if (shots.TryGetValue(id, out Shot stale) && stale.Camera != null)
            {
                // ⚠ targetTexture 를 문 채 카메라를 지우면 RT 가 같이 풀린다(진단: "텍스처가 없습니다").
                //    먼저 떼고 지운다.
                stale.Camera.targetTexture = null;
                Destroy(stale.Camera.gameObject);
            }

            // **몸 하나가 아니라 렌더러가 있는 자식 트리 전부**를 복제한다.
            //
            // 진단 로그에서 복제본 렌더러가 늘 1개였다 — 모자·머리 같은 장비 파츠가 몸(SkinnedMesh)
            // 과 다른 자식 밑에 붙어 있어서 예전 FindBody(몸 하나)로는 사진에 안 들어왔다.
            // 그래서 뜬 얼굴도 회색 민머리였다. 네트워크 부품이 달린 루트는 복제하지 않고,
            // 루트의 직계 자식 중 렌더러를 가진 것만 같은 자리에 복제한다.
            GameObject stand = new GameObject($"{id + 1}P 사진용");
            stand.transform.SetParent(studio, false);
            stand.transform.localPosition = new Vector3(id * apart, 0f, 0f);
            stand.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            stand.transform.localScale = who.transform.lossyScale;

            // ⚠ **자식을 전부 복제하고 뼈를 다시 잇는다.**
            //
            //    캐릭터는 뼈대(Armature)와 메시가 서로 다른 직계 자식이다. 메시 자식만 복제하면
            //    복제된 SkinnedMeshRenderer 의 bones · rootBone 이 **원본 뼈대(y≈0)** 를 그대로
            //    가리켜, 메시가 촬영장이 아니라 원래 자리에 그려진다. 진단 로그의 "키 5002m ·
            //    카메라 (940, -1497, 2582)" 가 정확히 그 증상이다(두 번 반복). 배 게임은 둘을
            //    함께 담은 "Body" 래퍼 하나를 복제해서 이 문제가 없었다.
            Dictionary<Transform, Transform> twin = new Dictionary<Transform, Transform>();

            foreach (Transform child in who.transform)
            {
                GameObject copy = Instantiate(child.gameObject, stand.transform);
                copy.transform.localPosition = child.localPosition;
                copy.transform.localRotation = child.localRotation;
                copy.transform.localScale = child.localScale;

                PairUp(child, copy.transform, twin);
                CopyVisibility(child.gameObject, copy);
            }

            RebindBones(who.transform, stand.transform, twin);
            StripAndLayer(stand);

            Bounds box = Measure(stand);

            if (box.size.y <= .01f)
            {
                Destroy(stand);
                return;
            }

            float tall = box.size.y;
            Vector3 lookAt = new Vector3(box.center.x, box.max.y - tall * lookAtDrop, box.center.z);

            RenderTexture shot = PrepareTexture(id);

            Camera cam = new GameObject($"{id + 1}P 사진기").AddComponent<Camera>();
            cam.transform.SetParent(studio, false);

            // ⚠ **얼굴 쪽에 세운다.** 뒤에 두면 뒤통수를 찍는다.
            cam.transform.position = lookAt + stand.transform.forward * (tall * distance);
            cam.transform.LookAt(lookAt);

            cam.cullingMask = 1 << layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.orthographic = true;
            cam.orthographicSize = tall * frame;
            cam.nearClipPlane = .01f;
            cam.farClipPlane = tall * 4f;
            cam.targetTexture = shot;

            // 한 번만 찍고 끈다. URP 는 Camera.Render 대신 렌더 요청을 쓴다.
            cam.enabled = false;

            UniversalRenderPipeline.SingleCameraRequest request =
                new UniversalRenderPipeline.SingleCameraRequest { destination = shot };

            if (RenderPipeline.SupportsRenderRequest(cam, request))
            {
                RenderPipeline.SubmitRenderRequest(cam, request);
            }
            else
            {
                cam.Render();
            }

            if (shots.TryGetValue(id, out Shot had))
            {
                had.Texture = shot;
                had.Signature = signature;
                had.StudioIndex = id;
                had.Camera = cam;
            }
            else
            {
                shots[id] = new Shot { Texture = shot, Signature = signature, StudioIndex = id, Camera = cam };
            }

            // 진단: 무엇을 찍었고 실제로 무엇이 찍혔는가. 프로필이 비어 보일 때 로그만으로 갈라낸다.
            int drawn = 0, hidden = 0;
            foreach (Renderer r in stand.GetComponentsInChildren<Renderer>(true))
            {
                if (r.forceRenderingOff || !r.enabled || !r.gameObject.activeInHierarchy) hidden++; else drawn++;
            }

            Debug.Log(
                $"[무쌍 초상] {id + 1}P 를 찍었습니다. 렌더러 보임 {drawn} · 숨김 {hidden} · " +
                $"키 {tall:F2}m · 카메라 {cam.transform.position} → {lookAt} · 렌더요청 {RenderPipeline.SupportsRenderRequest(cam, request)}",
                this);

            // 픽셀 읽기(ReadPixels)는 조사용이다. 개발 빌드에서만 돈다.
            if (Debug.isDebugBuild) StartCoroutine(InspectShot(id, shot));
        }

        /// <summary>
        /// 다음 프레임에 <b>실제로 그려진 픽셀</b>을 읽어 로그로 남긴다. 조사용.
        /// 불투명 픽셀 비율이 0 이면 카메라가 아무것도 못 봤고, 밝기가 0 에 가까우면 빛이 없다.
        /// </summary>
        private System.Collections.IEnumerator InspectShot(int id, RenderTexture shot)
        {
            yield return null;
            yield return new WaitForEndOfFrame();

            if (shot == null || !shot.IsCreated()) { Debug.LogWarning($"[무쌍 초상] {id + 1}P 텍스처가 없습니다."); yield break; }

            RenderTexture was = RenderTexture.active;
            Texture2D probe = new Texture2D(shot.width, shot.height, TextureFormat.RGBA32, false);
            RenderTexture.active = shot;
            probe.ReadPixels(new Rect(0, 0, shot.width, shot.height), 0, 0);
            probe.Apply(false);
            RenderTexture.active = was;

            Color32[] px = probe.GetPixels32();
            int opaque = 0; float sum = 0f;
            foreach (Color32 c in px)
            {
                if (c.a > 128) { opaque++; sum += (c.r + c.g + c.b) / (3f * 255f); }
            }

            float coverage = opaque / (float)px.Length;
            float brightness = opaque > 0 ? sum / opaque : 0f;

            Debug.Log(
                $"[무쌍 초상 진단] {id + 1}P — 불투명 {coverage:P1} · 평균 밝기 {brightness:F2} · " +
                $"HUD 칸 {(FaceOf(id) != null ? "찾음" : "없음")}", this);

            Destroy(probe);
        }

        /// <summary>
        /// 찍을 도화지. <b>있으면 그대로 다시 쓴다.</b>
        ///
        /// ⚠ 새로 만들고 옛것을 <c>Release()</c> 하면, HUD 의 <c>RawImage</c> 가 아직 옛 텍스처를
        ///    들고 있는 한 프레임 동안 버려진 텍스처를 그린다. 그것이 지직거림으로 보인다.
        /// </summary>
        private RenderTexture PrepareTexture(int id)
        {
            if (shots.TryGetValue(id, out Shot already) && already.Texture != null && already.Texture.width == size)
            {
                return already.Texture;
            }

            RenderTexture made = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
            {
                name = $"무쌍 초상 {id + 1}P",
                antiAliasing = 2,
            };

            made.Create();
            return made;
        }

        // ------------------------------------------------------------
        // 보여주기
        // ------------------------------------------------------------

        /// <summary>찍은 사진을 HUD 칸에 끼운다. 칸은 이름으로 찾는다.</summary>
        private void ShowShots()
        {
            foreach (KeyValuePair<int, Shot> pair in shots)
            {
                RawImage face = FaceOf(pair.Key);
                if (face == null || pair.Value.Texture == null) continue;

                if (face.texture == pair.Value.Texture) continue;

                face.texture = pair.Value.Texture;
                face.color = Color.white;
            }
        }

        /// <summary>
        /// <c>Players/Player_N/Backplate/Face</c> 를 찾는다. 한 번 찾으면 들고 있는다.
        ///
        /// ⚠ 프레젠터의 인스펙터 칸을 늘리지 않는다. 늘리면 프리팹을 다시 이어야 하고,
        ///    그 과정에서 기존 참조가 끊길 수 있다. 이름으로 찾는 편이 안전하다.
        /// </summary>
        private RawImage FaceOf(int id)
        {
            if (faces.TryGetValue(id, out RawImage had) && had != null) return had;

            foreach (RawImage candidate in FindObjectsByType<RawImage>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate == null || candidate.name != "Face") continue;

                Transform slot = candidate.transform.parent != null ? candidate.transform.parent.parent : null;
                if (slot == null || slot.name != $"Player_{id + 1}") continue;

                faces[id] = candidate;
                return candidate;
            }

            return null;
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
                        $"[무쌍 초상] '{PortraitLayerName}' 레이어가 없습니다. " +
                        "Project Settings > Tags and Layers 에 추가해 주세요.", this);

                    enabled = false;
                    return false;
                }
            }

            if (studio != null) return true;

            studio = new GameObject("무쌍 초상 촬영장").transform;
            studio.position = studioAt;

            // ⚠ **여기는 본 씬의 빛이 안 닿는다.** 메인 디렉셔널 라이트는 위치 없이
            //    방향만 있어 이론상 어디든 비추지만, 라이트 프로브(주변광)는 <c>studioAt</c>
            //    처럼 아무 프로브도 없는 먼 곳에서는 완전히 검은 값을 준다. 실측 스크린샷에서
            //    사진이 실루엣처럼 어둡게 나온 것이 이 때문이다 — 직사광만 겨우 받고
            //    주변광이 하나도 없었다. 촬영장 전용 라이트를 하나 심어 항상 같은 밝기로
            //    찍히게 한다. Portrait 레이어만 비추므로 본 게임 화면에는 영향이 없다.
            // ⚠ 여기 Directional 라이트를 두었다가 **게임 화면이 밝아졌다.** URP 는 라이트의
            //    cullingMask 를 보지 않고, 방향광은 위치가 없어 씬 전체를 비춘다.
            //    → Point 라이트. 사거리 40m 는 y=-5000 촬영장에서 게임 무대까지 절대 닿지 않는다.
            //
            // ⚠ **촬영 순간에 만들면 안 된다.** 사진기와 같은 프레임에 붙인 램프는 그 렌더 요청의
            //    컬링에 아직 안 들어가 사진이 **검게** 찍혔다(실측). ShipCoop 처럼 촬영장을 세울 때
            //    미리 만들어 둔다. 두 자리(0 · apart) 사이 위쪽 앞에서 비춰 둘 다 얼굴이 밝다.
            Light lamp = new GameObject("촬영 조명").AddComponent<Light>();
            lamp.transform.SetParent(studio, false);
            lamp.transform.localPosition = new Vector3(apart * 0.5f, 3f, 3f);
            lamp.type = LightType.Point;
            lamp.range = apart * 2f;
            lamp.intensity = 3.5f;
            lamp.color = Color.white;
            lamp.shadows = LightShadows.None;

            return true;
        }

        /// <summary>
        /// 캐릭터 모델. 무쌍은 <c>"Body"</c> 래퍼가 없어 <c>SkinnedMeshRenderer</c> 로 찾는다.
        /// 여럿이면 <b>가장 위쪽 공통 부모</b>를 쓴다 — 하나만 복사하면 몸이 조각난다.
        /// </summary>
        /// <summary>원본 트리와 복제 트리를 같은 순서로 걸어가며 짝을 기록한다.</summary>
        private static void PairUp(Transform origin, Transform copy, Dictionary<Transform, Transform> twin)
        {
            twin[origin] = copy;

            int n = Mathf.Min(origin.childCount, copy.childCount);
            for (int i = 0; i < n; i++) PairUp(origin.GetChild(i), copy.GetChild(i), twin);
        }

        /// <summary>복제된 스킨 메시의 뼈 참조를 복제된 뼈대로 옮긴다. 짝이 없는 뼈는 그대로 둔다.</summary>
        private static void RebindBones(Transform origin, Transform copy, Dictionary<Transform, Transform> twin)
        {
            SkinnedMeshRenderer[] from = origin.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            SkinnedMeshRenderer[] to = copy.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            if (from.Length != to.Length)
            {
                Debug.LogWarning($"[무쌍 초상] 스킨 메시 수가 다릅니다. 원본 {from.Length} · 복제 {to.Length}");
                return;
            }

            for (int i = 0; i < from.Length; i++)
            {
                Transform[] bones = from[i].bones;
                Transform[] moved = new Transform[bones.Length];

                for (int b = 0; b < bones.Length; b++)
                {
                    moved[b] = bones[b] != null && twin.TryGetValue(bones[b], out Transform t) ? t : bones[b];
                }

                to[i].bones = moved;

                if (from[i].rootBone != null && twin.TryGetValue(from[i].rootBone, out Transform root))
                {
                    to[i].rootBone = root;
                }
            }
        }

        /// <summary>몸을 그리는 렌더러인가. 파티클 · 트레일 · 라인은 사진 바운드를 오염시키므로 제외.</summary>
        private static bool IsBodyRenderer(Renderer r) =>
            r is SkinnedMeshRenderer || r is MeshRenderer;

        private static bool HasBodyRenderer(Transform t)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>(true))
            {
                if (IsBodyRenderer(r)) return true;
            }

            return false;
        }

        /// <summary>지금 이 사람에게 실제로 그려지는 렌더러가 하나라도 있는가.</summary>
        private static bool AnyVisible(Transform who)
        {
            foreach (Renderer r in who.GetComponentsInChildren<Renderer>(false))
            {
                if (r != null && r.enabled && !r.forceRenderingOff) return true;
            }

            return false;
        }

        private static Transform FindBody(Transform who)
        {
            SkinnedMeshRenderer skin = who.GetComponentInChildren<SkinnedMeshRenderer>(false);
            if (skin == null) return null;

            // 루트 바로 아래까지 거슬러 올라간다. 거기가 모델 한 벌의 꼭대기다.
            Transform t = skin.transform;

            while (t.parent != null && t.parent != who) t = t.parent;

            return t;
        }

        /// <summary>
        /// 복사본에서 움직이는 것을 끄고 촬영용 레이어로 옮긴다.
        ///
        /// ⚠ <b>지우지 말고 끈다.</b> 컴포넌트끼리 의존이 걸려 있어 지우는 순서를 맞추기 어렵고,
        ///    부품이 하나 늘 때마다 깨진다. 끄기만 해도 안 돈다.
        ///
        /// ⚠ <c>Animator</c> 는 <b>켜 둔다.</b> 끄면 뼈가 풀려 T 자세로 굳는다.
        /// </summary>
        private void StripAndLayer(GameObject stand)
        {
            foreach (MonoBehaviour script in stand.GetComponentsInChildren<MonoBehaviour>(true))
            {
                script.enabled = false;
            }

            foreach (Collider bump in stand.GetComponentsInChildren<Collider>(true))
            {
                bump.enabled = false;
            }

            foreach (CharacterController body in stand.GetComponentsInChildren<CharacterController>(true))
            {
                body.enabled = false;
            }

            // 머리 위 이름표 같은 월드 캔버스도 사진에 들어오면 안 된다.
            foreach (Canvas canvas in stand.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
            }

            // 파티클 · 트레일 · 라인은 사진에 들어오면 안 된다 (바운드는 Measure 가 이미 뺀다).
            foreach (ParticleSystem ps in stand.GetComponentsInChildren<ParticleSystem>(true))
            {
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            foreach (Renderer r in stand.GetComponentsInChildren<Renderer>(true))
            {
                if (!IsBodyRenderer(r)) r.enabled = false;
            }

            foreach (Transform t in stand.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        /// <summary>
        /// 원본이 무엇을 보이고 무엇을 감췼는지 <b>복제본에 그대로 옮긴다.</b>
        ///
        /// ⚠ <b>이것이 빠져서 프로필이 기본 얼굴 덩어리로 찍혔다.</b>
        ///    <c>CharacterAppearanceApplier.RefreshVisibility</c> 는 프리팹 기본 얼굴과
        ///    안 입은 슬롯 파츠를 <see cref="Renderer.forceRenderingOff"/> 로 감춘다
        ///    (<c>enabled</c> 는 Fusion 의 RunnerVisibilityLink 가 되돌려 놓아서 못 쓴다).
        ///    <c>forceRenderingOff</c> 는 실행 중에만 있는 값이라 <c>Instantiate</c> 가 복사하지
        ///    않는다. 그래서 복제본은 기본 얼굴 + 모든 슬롯 파츠 + 입은 파츠가 <b>전부 켜진
        ///    채</b> 찍혔다. 배 게임(<c>ShipCoopPortrait.CopyVisibility</c>)과 같은 방식으로 맞춘다.
        ///
        /// 두 트리는 같은 프리팹에서 나왔으므로 훑는 순서가 같다. 그래도 길이는 확인한다.
        /// </summary>
        private static void CopyVisibility(GameObject origin, GameObject copy)
        {
            Renderer[] from = origin.GetComponentsInChildren<Renderer>(true);
            Renderer[] to = copy.GetComponentsInChildren<Renderer>(true);

            if (from.Length != to.Length)
            {
                Debug.LogWarning(
                    $"[무쌍 초상] 원본({from.Length})과 사진용 복제본({to.Length})의 렌더러 수가 " +
                    "다릅니다. 가시성을 옮기지 못했습니다.");
                return;
            }

            for (int i = 0; i < from.Length; i++)
            {
                if (from[i] != null && to[i] != null)
                {
                    to[i].forceRenderingOff = from[i].forceRenderingOff;
                    to[i].enabled = from[i].enabled;
                }
            }
        }

        private static Bounds Measure(GameObject stand)
        {
            // 몸 렌더러만, 그리고 실제로 그려지는 것만 잰다. 감춰진 기본 얼굴 등은 바운드에서도 뺀다.
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
        /// <b>무엇을 입고 있는가</b>를 한 줄로. 달라지면 그 사람만 다시 찍는다.
        ///
        /// ⚠ 아직 외형이 안 정해졌으면 <c>null</c>. 그때 찍으면 기본 얼굴이 박힌다.
        ///
        /// ⚠ <b>사라지는 중인 캐릭터의 복제 값을 읽으면 예외가 난다.</b> 판이 끝나고 로비로
        ///    돌아가는 프레임에 그 일이 생긴다. 사라지는 중이면 찍을 것도 없으므로 물러난다.
        /// </summary>
        private static string SignatureOf(WarriorsPlayerLife who)
        {
            CharacterAppearanceApplier applier = who.GetComponent<CharacterAppearanceApplier>();
            NetworkPlayerAppearance networked = who.GetComponent<NetworkPlayerAppearance>();

            if (networked == null)
            {
                return applier != null ? "단독:" + Worn(applier) : "단독";
            }

            if (networked.Object == null || !networked.Object.IsValid) return null;
            if (!networked.AppearanceReady) return null;

            return Worn(applier);
        }

        /// <summary>입고 있는 파츠를 이름순으로 이어 붙인 것. 순서가 흔들리지 않게 정렬한다.</summary>
        private static string Worn(CharacterAppearanceApplier applier)
        {
            if (applier == null) return "(외형 없음)";

            List<string> keys = new List<string>(applier.EquippedKeys);
            keys.Sort(System.StringComparer.Ordinal);

            StringBuilder text = new StringBuilder();

            foreach (string key in keys) text.Append(key).Append(',');

            return text.Length > 0 ? text.ToString() : "(맨몸)";
        }
    }
}
