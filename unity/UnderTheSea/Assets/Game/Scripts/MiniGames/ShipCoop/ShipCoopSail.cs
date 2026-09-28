using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 🌬️ 돛을 **돛 힘만큼 펼친다.** 당기면 내려와 펴지고, 놓으면 활대 쪽으로 걷어 올라간다.
///
/// 돛 힘은 <see cref="ShipVoyage.SailPower01"/> 하나로 정해집니다. 그 값을 그대로 씁니다.
/// 서버의 SailTask 가 바꾸고 네트워크로 모두에게 오는 값이라, **누구 화면에서든
/// 같은 돛이 같은 만큼** 펴져 있습니다. (ShipCoopStateSync 가 실어 나릅니다)
///
/// <b>왜 필요한가.</b> 돛이 풀려 있는지 당겨져 있는지를 알 길이 HUD 게이지밖에
/// 없었습니다. 돛 자리에 붙은 사람은 게이지로 알지만, 나머지 셋은 배가 왜 느린지
/// 모릅니다. 돛이 걷혀 올라가 있으면 **한 번 쳐다보는 것만으로** "돛 비었네" 가 됩니다.
///
/// <b>어떻게 접는가.</b> 이 배의 돛 메시(StylShip_Sail*)는 블렌드셰이프도, 접힌
/// 모양의 다른 메시도 없습니다. 그래서 **위쪽 활대 끝을 붙잡고 세로로 줄입니다.**
/// 돛을 활대로 걷어 올리는 것처럼 보입니다.
///
/// ⚠ <b>돛 모델의 원점(pivot)이 한가운데입니다.</b> 그냥 세로 배율만 줄이면 위아래가
///    함께 가운데로 모여 활대에서 떨어진 채 공중에 뜹니다. 그래서 켤 때 위쪽 끝
///    자리를 재어 두고, 줄이는 만큼 돛을 그쪽으로 끌어올립니다.
///    (조타륜이 바퀴 한가운데를 축으로 삼는 것과 같은 이유 — <c>ShipCoopHelmWheel</c>)
///
/// ⚠ <b>로컬 기준으로 움직입니다.</b> 돛은 배의 자식이라 월드 자리를 덮어쓰면
///    배가 틀어진 것이 지워집니다. 로컬로 두면 배를 따라가고 그 위에서 접힙니다.
///
/// <b>돛은 이름이 아니라 메시로 찾습니다.</b> 씬의 돛 오브젝트 이름은 프리팹 인스턴스에서
/// 바꿔 놓은 것이라(SailFore · SailMid_01 …) 또 바뀔 수 있습니다. 메시 이름
/// <c>StylShip_Sail*</c> 은 모델 파일에 박혀 있어 안 바뀝니다.
/// </summary>
public class ShipCoopSail : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private ShipVoyage voyage;

    [Header("접힌 모양")]
    [Tooltip("돛 힘이 0 일 때 세로가 원래의 몇 배로 남는가.\n" +
             "0 이면 선으로 사라진다. 조금 남겨야 활대에 걷어 올린 천으로 보인다.")]
    [SerializeField, Range(0.02f, 1f)] private float furledHeight = 0.12f;

    [Tooltip("돛 힘이 0 일 때 가로가 원래의 몇 배가 되는가.\n" +
             "살짝 좁히면 걷어 모은 천처럼 보인다. 1 이면 가로는 그대로다.")]
    [SerializeField, Range(0.3f, 1f)] private float furledWidth = 0.9f;

    [Header("움직임")]
    [Tooltip("돛 힘 변화를 얼마나 빨리 따라가는가 (초당). 클수록 즉각적이다.\n" +
             "SailTask 가 0 → 1 을 약 2초에 올리므로 그보다 빠르면 거의 그대로 따라간다.\n" +
             "네트워크 틱마다 값이 계단처럼 오는 것을 부드럽게 잇는 용도다.")]
    [SerializeField, Min(0.1f)] private float followPerSecond = 3f;

    /// <summary>돛 하나. 켤 때 잰 원래 자리와 위쪽 끝.</summary>
    private struct Sail
    {
        public Transform Cloth;
        public Vector3 RestScale;
        public Vector3 LocalTop;      // 부모 기준, 돛 위쪽 끝 한가운데
        public Vector3 FromTop;       // 위쪽 끝에서 돛 원점까지
    }

    private readonly List<Sail> _sails = new List<Sail>();

    /// <summary>지금 화면에 보이는 펼침 정도. 0 접힘 ~ 1 펼침. 돛 힘을 부드럽게 따라간다.</summary>
    private float _shown = 1f;

    private bool _ready;

    private void Awake()
    {
        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }

        if (voyage == null)
        {
            Debug.LogWarning($"[{name}] ShipVoyage 를 찾지 못했습니다. 돛이 접히지 않습니다.", this);
            return;
        }

        foreach (MeshFilter filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null || !filter.sharedMesh.name.StartsWith("StylShip_Sail"))
            {
                continue;
            }

            Renderer draw = filter.GetComponent<Renderer>();
            Transform cloth = filter.transform;
            Transform parent = cloth.parent;

            // 위쪽 끝 한가운데. 원점이 어디에 있든 이 점을 붙잡고 줄인다.
            Bounds bounds = draw != null ? draw.bounds : new Bounds(cloth.position, Vector3.zero);
            Vector3 worldTop = new Vector3(bounds.center.x, bounds.max.y, bounds.center.z);
            Vector3 localTop = parent != null ? parent.InverseTransformPoint(worldTop) : worldTop;

            _sails.Add(new Sail
            {
                Cloth = cloth,
                RestScale = cloth.localScale,
                LocalTop = localTop,
                FromTop = cloth.localPosition - localTop,
            });
        }

        if (_sails.Count == 0)
        {
            Debug.LogWarning($"[{name}] 돛 메시(StylShip_Sail*)를 찾지 못했습니다. 돛이 접히지 않습니다.", this);
            return;
        }

        _shown = Mathf.Clamp01(voyage.SailPower01);
        _ready = true;
        Apply();
    }

    private void LateUpdate()
    {
        if (!_ready)
        {
            return;
        }

        float target = Mathf.Clamp01(voyage.SailPower01);
        _shown = Mathf.MoveTowards(_shown, target, followPerSecond * Time.deltaTime);

        Apply();
    }

    private void Apply()
    {
        // 접힌 정도. 0 이면 다 펴짐, 1 이면 다 걷힘.
        float furl = 1f - _shown;

        float heightScale = Mathf.Lerp(1f, furledHeight, furl);
        float widthScale = Mathf.Lerp(1f, furledWidth, furl);

        for (int i = 0; i < _sails.Count; i++)
        {
            Sail sail = _sails[i];

            Vector3 scale = sail.RestScale;
            scale.x *= widthScale;
            scale.y *= heightScale;
            sail.Cloth.localScale = scale;

            // 위쪽 끝은 그대로 두고, 원점을 그쪽으로 끌어올린다.
            // 원점에서 위쪽 끝까지의 거리가 세로 배율만큼 줄어든 셈이다.
            Vector3 fromTop = sail.FromTop;
            fromTop.y *= heightScale;
            sail.Cloth.localPosition = sail.LocalTop + fromTop;
        }
    }
}
