using UnityEngine;

/// <summary>
/// 🌊 물 무늬를 **배 속도에 맞춰 뒤로 흘린다.** (SHIPCOOP.md 5장)
///
/// 배는 제자리에 있고 세상이 흘러오는 구조라, 물도 같이 흘러야 배가
/// 나아가 보입니다. 그런데 **물 판을 옮기는 것만으로는 안 됩니다.**
/// 무늬가 판이 아니라 시간으로 그려지기 때문에, 판을 아무리 뒤로 밀어도
/// 무늬는 제자리에 있습니다.
///
/// ⛔ **`_Animation_Offset` 은 이 일을 하는 값이 아닙니다.**
///
///    그 이름에 속아서 한참 헤맸습니다. 값을 바꾸면 무늬가 달라지긴 해서
///    되는 줄 알았는데, 화면에서는 아무것도 안 흘렀습니다.
///    셰이더 그래프에서 찾아보니 **"Shore Properties" 묶음**이었습니다.
///    해안가 파도 애니메이션용이고, 우리 물은 `_Enable_Shore_Animation` 이
///    꺼져 있어서 애초에 상관이 없었습니다.
///
///    먼바다의 무늬를 흐르게 하는 것은 **`_Normal_Pan_Speed`** 입니다.
///
/// ⚠ **속도를 그냥 바꾸면 무늬가 순간이동합니다.**
///
///    셰이더는 `시간 × 속도` 로 무늬 위치를 냅니다. 그래서 달리는 도중에
///    속도만 바꾸면 **위치가 통째로 튑니다.** 튀는 양은 흐른 시간에
///    비례해서, 3분쯤 지나면 물이 순간이동하는 것처럼 보입니다.
///
///    그래서 속도가 아니라 **위치를 맞춥니다.**
///
///    <code>
///    원하는 무늬 위치 = 배가 간 거리
///    무늬 위치 = 시간 × 속도
///    → 속도 = 거리 / 시간
///    </code>
///
///    거리를 시간으로 나눈 값, 즉 **평균 속도**를 넣으면 곱한 결과가 늘
///    거리와 같아집니다. 튀지 않고, 배가 빠르면 물도 빨라집니다.
///
/// ⚠ **공용 재질이 아니라 사본에 씁니다.** (`renderer.material`)
///    `sharedMaterial` 에 쓰면 에디터에서 재질 파일이 실제로 바뀌어서,
///    플레이를 멈춰도 물이 흘러간 자리에 그대로 남습니다.
/// </summary>
public class ShipCoopSeaFlow : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private ShipVoyage voyage;

    [Tooltip("무늬를 흘릴 물 판들. 비워두면 이 오브젝트와 자식에서 찾는다.")]
    [SerializeField] private Renderer[] water;

    [Header("얼마나 흐를지")]
    [Tooltip("배가 초당 1m 갈 때 무늬를 이만큼 흘린다.\n\n" +
             "배 속도가 1.25m/s 이므로 4 면 무늬 속도가 5 가 됩니다.\n" +
             "1 로 두면 거의 안 움직입니다. 받아온 기본값이 1 이었고,\n" +
             "그래서 흐르는 것이 하나도 안 느껴졌습니다.")]
    [SerializeField, Min(0f)] private float flowPerShipSpeed = 4f;

    [Tooltip("배가 아주 느릴 때도 이만큼은 흐른다. 0 이면 물이 얼어붙는다.")]
    [SerializeField, Min(0f)] private float leastFlow = 1.5f;

    /// <summary>무늬를 흘리는 값. 셰이더에 없으면 꺼진다.</summary>
    private const string PanName = "_Normal_Pan_Speed";

    private Material[] _paints;
    private bool _ready;

    private void Awake()
    {
        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }

        if (water == null || water.Length == 0)
        {
            water = GetComponentsInChildren<Renderer>();
        }

        if (voyage == null || water.Length == 0)
        {
            Debug.LogWarning($"[{name}] 항해나 물 판을 찾지 못했습니다. 물이 안 흐릅니다.", this);
            return;
        }

        // ⚠ 사본을 만든다. 위 주석 참고.
        _paints = new Material[water.Length];

        for (int i = 0; i < water.Length; i++)
        {
            _paints[i] = water[i].material;
        }

        if (!_paints[0].HasProperty(PanName))
        {
            Debug.LogWarning(
                $"[{name}] 이 물 셰이더에는 {PanName} 이 없습니다. " +
                $"({_paints[0].shader.name}) 물이 안 흐릅니다. " +
                "무늬를 흘리는 값이 있는 셰이더로 바꾸거나 이 스크립트를 떼세요.", this);
            return;
        }

        _ready = true;
    }

    private void LateUpdate()
    {
        if (!_ready)
        {
            return;
        }

        // 시작 직후에는 시간이 0 에 가까워서 나눌 수 없다.
        float since = Mathf.Max(Time.timeSinceLevelLoad, 0.5f);

        // 평균 속도. 곱한 결과(무늬 위치)가 늘 간 거리와 같아진다. (위 주석)
        float pan = voyage.Distance / since * flowPerShipSpeed;

        for (int i = 0; i < _paints.Length; i++)
        {
            _paints[i].SetFloat(PanName, Mathf.Max(pan, leastFlow));
        }
    }
}
