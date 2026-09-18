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
        }

        private readonly Dictionary<int, Shot> shots = new Dictionary<int, Shot>();

        private readonly Dictionary<int, RawImage> faces = new Dictionary<int, RawImage>();

        private Transform studio;
        private int layer = -1;

        /// <summary>촬영용 레이어 이름. 이 레이어만 사진기가 본다.</summary>
        public const string PortraitLayerName = "Portrait";

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

            GameObject stand = Instantiate(body.gameObject);
            stand.name = $"{id + 1}P 사진용";
            stand.transform.SetParent(studio, false);
            stand.transform.localPosition = new Vector3(id * apart, 0f, 0f);
            stand.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
            stand.transform.localScale = body.lossyScale;

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
            }
            else
            {
                shots[id] = new Shot { Texture = shot, Signature = signature, StudioIndex = id };
            }

            Debug.Log($"[무쌍 초상] {id + 1}P 를 찍었습니다.", this);
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
            return true;
        }

        /// <summary>
        /// 캐릭터 모델. 무쌍은 <c>"Body"</c> 래퍼가 없어 <c>SkinnedMeshRenderer</c> 로 찾는다.
        /// 여럿이면 <b>가장 위쪽 공통 부모</b>를 쓴다 — 하나만 복사하면 몸이 조각난다.
        /// </summary>
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

            foreach (Transform t in stand.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        private static Bounds Measure(GameObject stand)
        {
            Renderer[] draws = stand.GetComponentsInChildren<Renderer>(false);

            if (draws.Length == 0) return new Bounds(stand.transform.position, Vector3.zero);

            Bounds box = draws[0].bounds;

            for (int i = 1; i < draws.Length; i++) box.Encapsulate(draws[i].bounds);

            return box;
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
