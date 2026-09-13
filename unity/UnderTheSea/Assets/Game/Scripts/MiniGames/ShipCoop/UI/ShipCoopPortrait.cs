using System.Collections.Generic;
using UnityEngine;

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

    /// <summary>사람 이름 → 그 사람의 사진. HUD 가 여기서 꺼내 쓴다.</summary>
    private readonly Dictionary<string, RenderTexture> _shots = new Dictionary<string, RenderTexture>();

    private Transform _studio;
    private int _layer = -1;

    /// <summary>
    /// 그 사람의 사진. 아직 안 찍었으면 null.
    ///
    /// 이름으로 찾습니다. HUD 도 이름순으로 칸을 정하므로 (`UpdatePortraits`)
    /// 둘이 어긋나지 않습니다.
    /// </summary>
    public Texture Of(string crewName)
    {
        return _shots.TryGetValue(crewName, out RenderTexture shot) ? shot : null;
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

        Bounds box = Measure(stand);

        if (box.size.y <= 0.01f)
        {
            Destroy(stand);
            return;
        }

        float tall = box.size.y;
        Vector3 lookAt = new Vector3(box.center.x, box.max.y - tall * lookAtDrop, box.center.z);

        RenderTexture shot = new RenderTexture(size, size, 16, RenderTextureFormat.ARGB32)
        {
            name = who.name + " 프로필",
        };

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
        cam.Render();
        cam.enabled = false;

        _shots[who.name] = shot;
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
        foreach (RenderTexture shot in _shots.Values)
        {
            if (shot != null)
            {
                shot.Release();
            }
        }

        _shots.Clear();
    }
}
