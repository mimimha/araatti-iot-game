using UnityEngine;

/// <summary>
/// 물결 무늬를 **배가 간 거리만큼 뒤로 밀어준다.** (SHIPCOOP.md 9장)
///
/// ⚠ 왜 필요한가 — 물 판을 옮기는 것으로는 안 됩니다.
///
///    쓰는 물 셰이더(Synty)는 무늬를 **월드 좌표로** 그리고 **시간으로 스스로
///    일렁입니다.** 그래서 판을 아무리 뒤로 밀어도 무늬는 제자리에 있습니다.
///    실제로는 잘 밀리고 있는데 화면에서는 배가 멈춰 보입니다.
///
///    무늬 자체를 밀어야 합니다. 셰이더에 그 칸(`_Animation_Offset`)이 있습니다.
///
/// 재질은 **우리 것만** 건드립니다. 로비가 쓰는 원본을 복사해 두었습니다.
/// 원본을 밀면 로비 물까지 같이 흘러갑니다.
/// </summary>
/// <remarks>
/// 이 값이 무늬를 어느 방향으로 얼마나 미는지는 셰이더가 정합니다.
/// 눈으로 보고 `metersPerTurn` 을 맞추세요. 방향이 반대면 음수로 두면 됩니다.
///
/// 셰이더에 그 칸이 없으면 **한 번만 경고하고 조용히 손을 뗍니다.**
/// 매 프레임 콘솔을 도배하면 진짜 문제를 못 봅니다.
/// </remarks>
public class ShipCoopSeaFlow : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private ShipVoyage voyage;

    [Header("어느 칸을 미는가")]
    [Tooltip("물 셰이더에서 무늬를 밀어주는 값의 이름.\n" +
             "셰이더를 바꾸면 이 이름도 바뀔 수 있다.")]
    [SerializeField] private string offsetProperty = "_Animation_Offset";

    [Header("얼마나 밀지")]
    [Tooltip("배가 이만큼(m) 가면 무늬가 1 만큼 밀린다.\n\n" +
             "작게 두면 빠르게 흐르고, 크게 두면 느리게 흐른다.\n" +
             "눈으로 보고 맞추는 값이다. 반대로 흐르면 음수로 둔다.")]
    [SerializeField] private float metersPerTurn = 8f;

    private Renderer[] _skin;
    private int _propertyId;
    private bool _usable;
    private bool _warned;

    private void Awake()
    {
        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }

        _propertyId = Shader.PropertyToID(offsetProperty);

        // 자식 중 물 판만. 이 오브젝트 밑에 물만 있으므로 전부 가져온다.
        _skin = GetComponentsInChildren<Renderer>(true);

        _usable = voyage != null && _skin.Length > 0
                  && _skin[0].sharedMaterial != null
                  && _skin[0].sharedMaterial.HasProperty(_propertyId);

        if (!_usable && !_warned)
        {
            _warned = true;

            Debug.LogWarning(
                $"[{name}] 물결을 밀 수 없습니다. " +
                $"'{offsetProperty}' 칸이 셰이더에 없거나 항해를 못 찾았습니다. " +
                "바다는 제자리에서 일렁이기만 합니다.", this);
        }
    }

    private void Update()
    {
        if (!_usable || Mathf.Approximately(metersPerTurn, 0f))
        {
            return;
        }

        float pushed = voyage.Distance / metersPerTurn;

        for (int i = 0; i < _skin.Length; i++)
        {
            if (_skin[i] == null)
            {
                continue;
            }

            // material 을 쓰면 판마다 복제본이 생긴다. 10장뿐이라 괜찮고,
            // sharedMaterial 을 건드리면 **에셋 파일이 바뀌어 저장된다.**
            _skin[i].material.SetFloat(_propertyId, pushed);
        }
    }
}
