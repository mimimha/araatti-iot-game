using UnityEngine;

/// <summary>
/// 🌊 물 무늬를 **배가 간 거리만큼** 뒤로 민다. (SHIPCOOP.md 5장)
///
/// 배는 제자리에 있고 세상이 흘러오는 구조라, 물도 같이 흘러야 배가
/// 나아가 보입니다. 그런데 **물 판을 옮기는 것만으로는 안 됩니다.**
/// 무늬가 판이 아니라 시간과 월드 좌표로 그려지기 때문에, 판을 아무리 뒤로
/// 밀어도 무늬는 제자리에 있습니다. 실제로는 잘 밀리고 있는데 화면에서는
/// 배가 멈춰 보입니다.
///
/// 그래서 무늬 자체를 미는 값(`_Animation_Offset`)을 거리로 굴립니다.
///
/// ⚠ **이 값은 셰이더마다 있기도 하고 없기도 합니다.**
///
///    한동안 WaterWorks(`SSR_Water`)를 썼는데 **거기에는 이 값이 없습니다.**
///    그래서 셰이더 UV 를 오브젝트 좌표로 바꾸는 우회로를 탔고, 바다를 한 장으로
///    만들고 되돌리지 않게 하는 것까지 줄줄이 딸려왔습니다.
///    로비와 같은 Synty 물로 돌아오면서 그 우회로가 전부 필요 없어졌습니다.
///
///    이름이 없으면 한 번 경고하고 스스로 꺼집니다. 조용히 아무 일도 안 하면
///    "고쳤는데 왜 안 되지" 를 또 반복하게 됩니다.
///
/// ⚠ **공용 재질이 아니라 사본에 씁니다.** (`renderer.material`)
///    `sharedMaterial` 에 쓰면 에디터에서 재질 파일이 실제로 바뀌어서,
///    플레이를 멈춰도 물이 흘러간 자리에 그대로 남습니다.
/// </summary>
public class ShipCoopSeaFlow : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private ShipVoyage voyage;

    [Tooltip("무늬를 밀 물 판들. 비워두면 이 오브젝트와 자식에서 찾는다.")]
    [SerializeField] private Renderer[] water;

    [Header("얼마나 흐를지")]
    [Tooltip("배가 이만큼 갈 때 무늬가 한 바퀴 돈다 (m). 작을수록 빨리 흐른다.\n\n" +
             "⚠ 눈에 보이려면 초당 0.3 쯤은 밀려야 합니다.\n" +
             "배 속도가 1.25m/s 니까 3 이면 초당 0.42 입니다.\n" +
             "18 로 뒀을 때는 초당 0.07 이라 흐르는 것이 하나도 안 느껴졌습니다.\n\n" +
             "흐름이 부족하면 물결 무늬를 잘게 쪼개지 말고 이 값을 낮추세요.\n" +
             "무늬를 쪼개면 멀리서 지글거립니다.")]
    [SerializeField, Min(0.5f)] private float metersPerLoop = 3f;

    [Tooltip("흐르는 방향. 배가 앞으로 가면 물은 뒤로 가야 한다.\n" +
             "반대로 흐르면 부호를 바꾼다.")]
    [SerializeField] private float direction = -1f;

    // ------------------------------------------------------------
    // ⚠ **배가 서 있어도 조금은 흘러야 합니다.**
    //
    //    거리에만 비례시키면 돛이 비었을 때 물이 **완전히 얼어붙습니다.**
    //    진짜 바다는 배가 멈춰도 물이 움직입니다. 그래서 최소한의 흐름을
    //    시간으로 더해 둡니다.
    // ------------------------------------------------------------

    [Tooltip("배가 멈춰 있을 때도 흐르는 양 (초당). 0 이면 완전히 언다.")]
    [SerializeField, Min(0f)] private float idleFlowPerSecond = 0.08f;

    /// <summary>재질을 밀 때 쓰는 이름. 셰이더에 없으면 꺼진다.</summary>
    private const string OffsetName = "_Animation_Offset";

    private Material[] _paints;
    private float _idle;
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

        if (!_paints[0].HasProperty(OffsetName))
        {
            Debug.LogWarning(
                $"[{name}] 이 물 셰이더에는 {OffsetName} 이 없습니다. " +
                $"({_paints[0].shader.name}) 물이 안 흐릅니다. " +
                "무늬를 미는 값이 있는 셰이더로 바꾸거나 이 스크립트를 떼세요.", this);
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

        _idle += idleFlowPerSecond * Time.deltaTime;

        float offset = (voyage.Distance / metersPerLoop + _idle) * direction;

        for (int i = 0; i < _paints.Length; i++)
        {
            _paints[i].SetFloat(OffsetName, offset);
        }
    }
}
