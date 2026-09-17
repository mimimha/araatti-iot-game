using System.Collections.Generic;
using System.Text;
using Fusion;
using UnderTheSea.Character;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 🧑 HUD 프로필 사진을 **그 사람이 실제로 쓰는 캐릭터**로 찍는다. (SHIPCOOP.md 9장)
///
/// 프로필 칸이 있는 이유는 갑판이 3층이라 서로 안 보이기 때문입니다.
/// 그래서 **누가 누군지 한눈에 갈려야** 합니다.
///
/// ⚠ **그림 한 장을 박아두면 안 됩니다.**
///
///    4명이 각자 커스터마이징한 캐릭터로 들어오면 네 칸이 전부 같은 얼굴이
///    됩니다. 사람을 구분하라고 만든 칸인데 구분이 안 됩니다.
///    색 테두리만 남고 칸 자체가 반쯤 무의미해집니다.
///
/// 그래서 화면 밖 **촬영장**에 각자의 캐릭터 복사본을 세워두고 찍습니다.
///
/// ⚠ **한 번만 찍습니다.**
///
///    초상화는 움직일 필요가 없습니다. 카메라 네 대를 매 프레임 돌리면
///    공짜가 아니지만, 한 장 찍고 끄면 사실상 공짜입니다.
///    옷을 갈아입는 기능이 생기면 그때 <see cref="Retake"/> 를 부르면 됩니다.
/// </summary>
public class ShipCoopPortrait : MonoBehaviour
{
    // ------------------------------------------------------------
    // ⚠ **촬영장은 멀리 둡니다.**
    //
    //    바다 밑이나 갑판 옆에 두면 카메라 사거리 안에 들어와서 화면에
    //    비칩니다. 배가 항해 끝에 600m 를 가므로 그보다 훨씬 멀리 둡니다.
    // ------------------------------------------------------------

    [Header("촬영장")]
    [Tooltip("복사본을 세워둘 자리. 화면에 절대 안 들어올 만큼 멀어야 한다.")]
    [SerializeField] private Vector3 studioAt = new Vector3(0f, -5000f, 0f);

    [Tooltip("사람끼리 이만큼 떼어 놓는다. 카메라에 옆 사람이 걸리면 안 된다.")]
    [SerializeField, Min(2f)] private float apart = 20f;

    [Header("사진")]
    [Tooltip("한 변 크기 (픽셀). HUD 칸이 100px 라 256 이면 충분하다.")]
    [SerializeField, Min(64)] private int size = 256;

    [Tooltip("배경색. 알파 0 이면 뒤 판때기가 비친다.")]
    [SerializeField] private Color background = new Color(0f, 0f, 0f, 0f);

    [Header("어디를 찍을지")]
    [Tooltip("머리 꼭대기에서 이만큼 내려온 곳을 본다 (사람 키 대비 비율).\n" +
             "0 이면 정수리, 0.25 면 얼굴 한가운데쯤.")]
    [SerializeField, Range(0f, 0.5f)] private float lookAtDrop = 0.30f;

    [Tooltip("얼굴에서 이만큼 떨어져서 찍는다 (사람 키 대비 비율).\n" +
             "작을수록 얼굴이 꽉 찬다.")]
    [SerializeField, Min(0.1f)] private float distance = 0.55f;

    // 0 이 정면입니다. 정자세도 해봤는데 증명사진 같아서 되돌렸습니다.
    // 살짝 튼 쪽이 사람처럼 보입니다.
    [Tooltip("정면에서 이만큼 돌려서 찍는다 (도). 0 이 정자세.")]
    [SerializeField] private float turn = 20f;

    // ⚠ 올릴수록 멀리서 찍은 것처럼 되고, 내릴수록 얼굴이 꽉 찹니다.
    //    0.22 머리가 칸을 넘쳐 잘림 · 0.30 얼굴만 · 0.42 어깨까지지만 작아 보임
    //    0.36 이 어깨까지 담기면서 칸을 꽉 채우는 자리입니다.
    [Tooltip("화면에 담을 높이 (사람 키 대비 비율). 올릴수록 멀리서 찍은 것처럼 된다.")]
    [SerializeField, Min(0.05f)] private float frame = 0.36f;

    /// <summary>찍어 둔 사진 한 장과, 그것이 <b>어떤 외형이었는지</b>.</summary>
    private sealed class Shot
    {
        public RenderTexture Texture;

        /// <summary>찍을 당시의 외형 서명. 이 값이 달라지면 다시 찍는다.</summary>
        public string Signature;

        /// <summary>촬영장에서 이 사람이 서는 자리. 다시 찍어도 자리를 옮기지 않는다.</summary>
        public int StudioIndex;
    }

    /// <summary>사람 이름 → 그 사람의 사진. HUD 가 여기서 꺼내 쓴다.</summary>
    private readonly Dictionary<string, Shot> _shots = new Dictionary<string, Shot>();

    private Transform _studio;
    private int _layer = -1;

    /// <summary>
    /// 사람을 가리키는 <b>안정된 키.</b> 사진 보관과 HUD 칸 순서가 **둘 다 이것을 씁니다.**
    ///
    /// ⚠ <b>오브젝트 이름을 쓰면 안 됩니다.</b> 네트워크로 스폰된 플레이어는 이름이
    ///    전부 <c>ShipCoopPlayer(Clone)</c> 로 <b>똑같습니다.</b> 실측으로 확인했습니다.
    ///    이름으로 보관하면 네 사람이 사진 한 장을 나눠 쓰게 되고, 이름으로 정렬하면
    ///    <c>Array.Sort</c> 가 같은 키끼리 순서를 보장하지 않아 <b>칸이 매 프레임 뒤바뀝니다.</b>
    ///    카드가 지직거리던 원인이 이 둘이었습니다.
    ///
    /// <c>NetworkObject.Id</c> 는 서버가 정하고 모든 피어에서 같으므로, 사람마다 하나씩이고
    /// 어느 화면에서나 같은 순서가 됩니다. 자리 수를 맞춰 문자열 정렬로도 숫자 순서가 나옵니다.
    ///
    /// 네트워크가 없는 씬(<c>ShipCoopTest</c> · 직접 실행)에는 <c>NetworkObject</c> 가 없습니다.
    /// 그때는 예전처럼 이름을 씁니다. 사람이 하나뿐이라 겹칠 일이 없습니다.
    /// </summary>
    public static string StableKeyOf(TaskWorker who)
    {
        if (who == null)
        {
            return string.Empty;
        }

        NetworkObject net = who.GetComponentInParent<NetworkObject>();

        return net != null ? net.Id.Raw.ToString("D10") : who.name;
    }

    /// <summary>그 사람의 사진. 아직 안 찍었으면 null.</summary>
    public Texture Of(TaskWorker who)
    {
        return _shots.TryGetValue(StableKeyOf(who), out Shot shot) ? shot.Texture : null;
    }

    private void Start()
    {
        _layer = LayerMask.NameToLayer(PortraitLayerName);

        if (_layer < 0)
        {
            Debug.LogWarning(
                $"[{name}] '{PortraitLayerName}' 레이어가 없습니다. 프로필 사진을 못 찍습니다. " +
                "배치 도구(Tools/ShipCoop)를 한 번 돌리면 레이어를 만들어 줍니다.", this);
            return;
        }

        Retake();
    }

    // ------------------------------------------------------------
    // ⚠ **나중에 온 사람도 찍습니다.**
    //
    //    Start 에서 한 번만 찍었는데, 네트워크에서는 플레이어가 HUD 보다 **뒤에** 스폰됩니다.
    //    (씬이 뜬 뒤 Fusion 이 스폰한다) 그래서 클라이언트 화면의 왼쪽 아래 프로필이 비어 있었다.
    //    반 초마다 사람을 훑어서 사진이 없는 사람만 찍는다. 외형이 아직 감춰져 있으면
    //    (NetworkPlayerAppearance 가 준비될 때까지 forceRenderingOff) 다음 차례로 미룬다 —
    //    감춰진 채 찍으면 빈 사진이 나온다.
    // ------------------------------------------------------------

    [Header("나중에 온 사람")]
    [Tooltip("이 간격(초)으로 새 사람이 있는지 본다. 매 프레임 훑을 일은 아니다.")]
    [SerializeField, Min(0.1f)] private float pollSeconds = 0.5f;

    private float _nextPoll;

    private void Update()
    {
        if (_layer < 0 || _studio == null || Time.unscaledTime < _nextPoll)
        {
            return;
        }

        _nextPoll = Time.unscaledTime + pollSeconds;

        TaskWorker[] crew = FindObjectsByType<TaskWorker>(FindObjectsInactive.Exclude,
                                                          FindObjectsSortMode.None);

        for (int i = 0; i < crew.Length; i++)
        {
            string signature = SignatureOf(crew[i]);

            // null 이면 아직 외형이 정해지지 않았다. 지금 찍으면 기본 얼굴이 박힌다.
            if (signature == null)
            {
                continue;
            }

            if (_shots.TryGetValue(StableKeyOf(crew[i]), out Shot already))
            {
                if (already.Signature == signature)
                {
                    continue;
                }

                // 외형이 바뀌었다. **이 사람만** 자기 자리에서 다시 찍는다.
                Shoot(crew[i], already.StudioIndex);
                continue;
            }

            // 촬영장 자리는 지금까지 찍은 수로 — 이미 선 사람과 겹치지 않는다.
            Shoot(crew[i], _shots.Count);
        }
    }

    /// <summary>전원의 사진을 다시 찍는다. 옷을 갈아입었을 때 부르면 된다.</summary>
    public void Retake()
    {
        if (_layer < 0)
        {
            return;
        }

        if (_studio != null)
        {
            Destroy(_studio.gameObject);
        }

        _studio = new GameObject("__프로필 촬영장").transform;
        _studio.position = studioAt;

        // 촬영장 전용 빛. 배의 해를 쓰면 시간대에 따라 얼굴이 어두워진다.
        Light lamp = new GameObject("빛").AddComponent<Light>();
        lamp.transform.SetParent(_studio, false);
        lamp.transform.rotation = Quaternion.Euler(15f, 160f, 0f);
        lamp.type = LightType.Directional;
        lamp.intensity = 1.6f;
        lamp.cullingMask = 1 << _layer;
        lamp.shadows = LightShadows.None;

        TaskWorker[] crew = FindObjectsByType<TaskWorker>(FindObjectsInactive.Exclude,
                                                          FindObjectsSortMode.None);

        for (int i = 0; i < crew.Length; i++)
        {
            // 외형이 아직 안 정해진 사람은 건너뛴다. Update 가 준비되는 대로 찍는다.
            if (SignatureOf(crew[i]) == null)
            {
                continue;
            }

            Shoot(crew[i], i);
        }
    }

    private void Shoot(TaskWorker who, int index)
    {
        Renderer[] draws = who.GetComponentsInChildren<Renderer>(false);

        if (draws.Length == 0)
        {
            return;
        }

        // 사람 몸만 고른다. 머리 위에 든 물건까지 찍으면 얼굴이 안 보인다.
        Transform body = FindBody(who.transform);

        if (body == null)
        {
            return;
        }

        GameObject stand = Instantiate(body.gameObject);
        stand.name = who.name + " 사진용";
        stand.transform.SetParent(_studio, false);
        stand.transform.localPosition = new Vector3(index * apart, 0f, 0f);
        stand.transform.localRotation = Quaternion.Euler(0f, turn, 0f);
        stand.transform.localScale = body.lossyScale;

        StripAndLayer(stand);
        CopyVisibility(body.gameObject, stand);

        Bounds box = Measure(stand);

        if (box.size.y <= 0.01f)
        {
            Destroy(stand);
            return;
        }

        float tall = box.size.y;
        Vector3 lookAt = new Vector3(box.center.x, box.max.y - tall * lookAtDrop, box.center.z);

        RenderTexture shot = PrepareTexture(who);

        Camera cam = new GameObject(who.name + " 사진기").AddComponent<Camera>();
        cam.transform.SetParent(_studio, false);
        // ⚠ **얼굴 쪽에 세웁니다.** 처음에 `-forward` 로 뒀다가 뒤통수를 찍었습니다.
        //    거기에 몸을 180도 돌려놓기까지 해서 두 번 뒤집혔습니다.
        cam.transform.position = lookAt + stand.transform.forward * (tall * distance);
        cam.transform.LookAt(lookAt);

        cam.cullingMask = 1 << _layer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = background;
        cam.orthographic = true;
        cam.orthographicSize = tall * frame;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = tall * 4f;
        cam.targetTexture = shot;

        // ⚠ 한 번만 찍고 끕니다. 매 프레임 도는 카메라 네 대는 공짜가 아닙니다.
        //    URP 에서는 Camera.Render() 대신 렌더 요청을 낸다. 지원하지 않는 환경이면 예전 방식.
        cam.enabled = false;

        UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest
        {
            destination = shot,
        };

        if (RenderPipeline.SupportsRenderRequest(cam, request))
        {
            RenderPipeline.SubmitRenderRequest(cam, request);
        }
        else
        {
            cam.Render();
        }

        // 다시 찍은 경우에도 **텍스처 객체를 바꾸지 않았다.** (아래 PrepareTexture 참고)
        // 그래서 HUD 가 든 참조가 그대로라 교체 순간에도 깜빡이지 않는다.
        string key = StableKeyOf(who);

        if (_shots.TryGetValue(key, out Shot already))
        {
            already.Texture = shot;
            already.Signature = SignatureOf(who);
            already.StudioIndex = index;
        }
        else
        {
            _shots[key] = new Shot
            {
                Texture = shot,
                Signature = SignatureOf(who),
                StudioIndex = index,
            };
        }
    }

    /// <summary>
    /// 찍을 도화지를 마련한다. <b>이미 있으면 그것을 그대로 다시 쓴다.</b>
    ///
    /// ⚠ 새로 만들고 옛것을 <c>Release()</c> 하면, HUD 의 <c>RawImage</c> 가 아직 옛 텍스처를
    ///    들고 있는 한 프레임 동안 <b>버려진 텍스처를 그린다.</b> 그것이 지직거림으로 보인다.
    ///    같은 객체에 덮어 그리면 참조가 바뀌지 않아 그 틈이 아예 생기지 않는다.
    /// </summary>
    private RenderTexture PrepareTexture(TaskWorker who)
    {
        if (_shots.TryGetValue(StableKeyOf(who), out Shot already)
            && already.Texture != null
            && already.Texture.width == size)
        {
            return already.Texture;
        }

        return new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
        {
            name = who.name + " 프로필",
        };
    }

    /// <summary>
    /// 지금 이 사람의 외형을 한 줄로 요약한다. <b>사진을 다시 찍을지 판단하는 유일한 기준이다.</b>
    ///
    /// <c>null</c> 을 돌려주면 <b>아직 찍으면 안 된다</b>는 뜻이다.
    ///
    /// <b>왜 필요한가.</b> 예전에는 "렌더러가 보이면" 찍었다. 그런데 외형이 늦게 오는 사람은
    /// <c>ShipCoopDefaultAppearance</c> 가 <b>기본 외형으로 확정하는 순간에도 모델이 보이게</b> 되고,
    /// 그 틈에 찍혀서 <b>기본 얼굴이 초상화에 박제</b>됐다. 진짜 외형은 그 뒤에 도착하는데
    /// 사진은 이름으로 캐시돼 다시 찍히지 않았다. 실제로 그렇게 되어 있었다.
    ///
    /// 그래서 <b>무엇을 입고 있는지</b>를 서명으로 만들어 두고, 달라지면 그 사람만 다시 찍는다.
    ///
    /// ⚠ 네트워크가 없는 씬(<c>ShipCoopTest</c> · 직접 실행)에는 <c>NetworkPlayerAppearance</c> 가
    ///    없다. 그때는 예전처럼 <b>한 번 찍고 끝</b>이다.
    /// </summary>
    private static string SignatureOf(TaskWorker who)
    {
        CharacterAppearanceApplier applier = who.GetComponent<CharacterAppearanceApplier>();
        NetworkPlayerAppearance networked = who.GetComponent<NetworkPlayerAppearance>();

        if (networked == null)
        {
            // 혼자 하는 씬. 기다릴 외형이 없다.
            return applier != null ? "단독:" + Worn(applier) : "단독";
        }

        if (!networked.AppearanceReady)
        {
            // 서버가 아직 이 사람의 외형을 정하지 않았다. 지금 찍으면 기본 얼굴이 박힌다.
            return null;
        }

        return Worn(applier);
    }

    /// <summary>입고 있는 파츠를 이름순으로 이어 붙인 것. 순서가 흔들리지 않게 정렬한다.</summary>
    private static string Worn(CharacterAppearanceApplier applier)
    {
        if (applier == null)
        {
            return "(외형 없음)";
        }

        List<string> keys = new List<string>(applier.EquippedKeys);
        keys.Sort(System.StringComparer.Ordinal);

        StringBuilder text = new StringBuilder();

        foreach (string key in keys)
        {
            text.Append(key).Append(',');
        }

        return text.Length > 0 ? text.ToString() : "(맨몸)";
    }

    // 캐릭터 모델. 없으면 null. (`ShipCoopDeckLayout` 이 "Body" 라는 이름으로 붙인다)
    private static Transform FindBody(Transform who)
    {
        Transform named = who.Find(BodyName);

        if (named != null)
        {
            return named;
        }

        // 이름이 다르면 SkinnedMeshRenderer 를 가진 첫 자식을 쓴다.
        SkinnedMeshRenderer skin = who.GetComponentInChildren<SkinnedMeshRenderer>(false);

        return skin != null ? skin.transform.root == who ? skin.transform : skin.transform.parent : null;
    }

    // 복사본에서 움직이는 것들을 꺼두고 촬영용 레이어로 옮긴다.
    //
    // ⚠ **지우지 말고 끕니다.**
    //
    //    `Destroy` 로 지우려 했더니 "MovePlayerInput 이 CharacterMover 에
    //    기대고 있어서 못 지운다" 로 줄줄이 막혔습니다. 의존 순서를 맞춰가며
    //    지우는 것은 컴포넌트가 하나 늘 때마다 깨집니다.
    //    끄기만 해도 안 돌아가므로 그걸로 충분합니다.
    //
    // ⚠ Animator 는 **켜 둡니다.** 끄면 뼈가 풀려서 **T 자세**로 굳습니다.
    //    켜두면 기본 대기 동작이 돌아서 사람처럼 서 있습니다.
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
            t.gameObject.layer = _layer;
        }
    }

    /// <summary>
    /// 원본이 무엇을 보이고 무엇을 감췄는지 <b>복제본에 그대로 옮긴다.</b>
    ///
    /// <see cref="Renderer.forceRenderingOff"/> 는 씬에 저장되는 값이 아니라 실행 중에만 있는
    /// 값이라, <c>Instantiate</c> 가 따라온다고 믿을 수 없다. 안 따라오면 <b>감춰 둔 프리팹
    /// 기본 얼굴이 사진에서만 다시 나타난다.</b> 그래서 한 번 더 맞춘다.
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
                $"[ShipCoopPortrait] 원본({from.Length})과 사진용 복제본({to.Length})의 렌더러 수가 " +
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
        Renderer[] draws = stand.GetComponentsInChildren<Renderer>(false);

        if (draws.Length == 0)
        {
            return new Bounds(stand.transform.position, Vector3.zero);
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        return box;
    }

    /// <summary>촬영용 레이어 이름. 배치 도구가 이 이름으로 레이어를 만든다.</summary>
    public const string PortraitLayerName = "Portrait";

    /// <summary>캐릭터 모델의 이름. `ShipCoopDeckLayout` 이 이 이름으로 붙인다.</summary>
    private const string BodyName = "Body";

    private void OnDestroy()
    {
        foreach (Shot shot in _shots.Values)
        {
            if (shot?.Texture != null)
            {
                shot.Texture.Release();
            }
        }

        _shots.Clear();
    }
}
