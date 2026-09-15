using UnityEngine;

/// <summary>
/// 🧰 보급 궤짝의 뚜껑 표현. **화면 쪽**이다. 규칙은 <see cref="AmmoBox.IsOpen"/> 이 정한다. (SHIPCOOP.md 11장)
///
/// 매 프레임 <c>AmmoBox.IsOpen</c> 을 읽어 뚜껑의 로컬 X 회전을 닫힘(0°) ↔ 열림(-100°) 사이로 보간한다.
/// 열기 0.25초 · 닫기 0.35초, ease-out. 회전 하나에 Animator 는 과해서 코드로 돈다.
///
/// 뚜껑이 있는 상자(포탄 궤짝)에만 붙는다. 통 · 팰릿은 해당 없다.
/// <c>lid</c> 는 배치 도구(<c>ShipCoopDeckLayout.DressBoxes</c>)가 채운다. 비어 있으면 아무 것도 하지 않고 경고 한 줄.
///
/// ⚠ 열리는 방향 — Synty 궤짝의 뚜껑 원점은 힌지에 있고, 로컬 X 로 **음수**로 돌려야 밖으로 열린다.
///    양수로 돌리면 상자 안으로 들어간다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AmmoBox))]
public class SupplyChestLid : MonoBehaviour
{
    [Header("연결 — 배치 도구가 채운다")]
    [Tooltip("SM_Gen_Prop_Chest_01_Lid_01. 원점이 힌지에 있다.")]
    [SerializeField] private Transform lid;

    [Header("회전")]
    [Tooltip("열렸을 때 뚜껑의 로컬 X 각도. 음수가 밖으로 열리는 방향이다.")]
    [SerializeField] private float openAngle = -100f;

    [Tooltip("여는 데 걸리는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float openSeconds = 0.25f;

    [Tooltip("닫는 데 걸리는 시간 (초)")]
    [SerializeField, Min(0.01f)] private float closeSeconds = 0.35f;

    private AmmoBox _box;
    private Quaternion _closed;

    /// <summary>0 = 닫힘, 1 = 열림. 여기서 각도로 바꾼다.</summary>
    private float _open01;

    private bool _warned;

    private void Awake()
    {
        _box = GetComponent<AmmoBox>();

        if (lid != null)
        {
            _closed = lid.localRotation;
        }
    }

    private void Update()
    {
        if (lid == null)
        {
            if (!_warned)
            {
                Debug.LogWarning($"[{name}] 뚜껑(lid)이 연결되어 있지 않아 열고 닫지 못합니다. 배치 도구를 다시 돌려 주세요.", this);
                _warned = true;
            }

            return;
        }

        bool open = _box != null && _box.IsOpen;
        float target = open ? 1f : 0f;
        float seconds = open ? openSeconds : closeSeconds;

        // 진행도는 시간에 비례해 움직이고, 각도로 바꿀 때 ease-out 을 건다.
        // 열 때는 열림 쪽으로, 닫을 때는 닫힘 쪽으로 — 어느 방향이든 처음 빠르고 끝에 느리다.
        _open01 = Mathf.MoveTowards(_open01, target, Time.deltaTime / seconds);

        float eased = open ? EaseOut(_open01) : 1f - EaseOut(1f - _open01);
        lid.localRotation = _closed * Quaternion.Euler(openAngle * eased, 0f, 0f);
    }

    private static float EaseOut(float t)
    {
        return 1f - (1f - t) * (1f - t);
    }

    /// <summary>배치 도구가 뚜껑을 연결할 때 쓴다. 닫힌 각도를 다시 잰다.</summary>
    public void SetLid(Transform hinge)
    {
        lid = hinge;

        if (lid != null)
        {
            _closed = lid.localRotation;
        }
    }
}
