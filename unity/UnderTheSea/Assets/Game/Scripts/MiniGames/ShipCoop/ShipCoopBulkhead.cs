using UnityEngine;

/// <summary>
/// 🧱 뒷계단 사이 격벽의 **보이기 규칙.** (SHIPCOOP.md 4장 "배 밖으로는 못 나갑니다")
///
/// 중간갑판 카메라는 사람 뒤(고물 쪽) 9.6m · 위 6.25m 에서 뱃머리를 본다. 그 자리는 이 격벽
/// **뒤**라서, 카메라가 뱃머리를 볼 때 격벽이 화면 아래쪽을 통째로 덮는다.
///
/// <c>ShipCoopCamera</c> 의 가림 처리는 카메라에서 사람 **가슴 높이**로 선을 그어 걸리는 것만
/// 감춘다. 카메라가 높아서 그 선은 격벽 위를 넘어가고, 격벽은 걸리지 않은 채 화면 아래를 막는다.
/// 그래서 선이 아니라 **어느 쪽에 있는가**로 정한다.
///
/// <code>
///   카메라가 격벽 뒤(고물 쪽), 사람은 격벽 앞(뱃머리 쪽)   →  감춘다
///   그 밖 (카메라를 돌려 고물 쪽을 본다, 둘 다 같은 쪽)     →  보인다
/// </code>
///
/// 카메라가 고물 쪽을 보려고 돌면 카메라가 격벽 앞으로 넘어오므로 격벽이 다시 나타난다.
/// 벽이 있다는 것은 보이고, 뱃머리를 볼 때는 방해하지 않는다.
///
/// 배치 도구(<c>ShipCoopDeckLayout.BuildAftBulkhead</c>)가 격벽 그룹에 붙인다. 자식 렌더러 전체를 다룬다.
/// </summary>
[DisallowMultipleComponent]
public class ShipCoopBulkhead : MonoBehaviour
{
    [Header("판정 여유")]
    [Tooltip("카메라가 격벽 z 에서 이만큼(m) 이상 고물 쪽에 있어야 '뒤' 로 본다. 딱 경계에서 깜빡이는 것을 막는다.")]
    [SerializeField, Min(0f)] private float margin = 0.3f;

    private Renderer[] _draws;
    private float _wallZ;
    private ShipCoopHud _hud;
    private bool _hiddenByMe;

    private void Awake()
    {
        _draws = GetComponentsInChildren<Renderer>(true);

        // 격벽 z 는 자식들의 가운데. 배가 틀면 함께 돌지만 z 축 기준 판정에는 충분하다.
        Bounds box = new Bounds(transform.position, Vector3.zero);
        bool any = false;

        for (int i = 0; i < _draws.Length; i++)
        {
            if (!any)
            {
                box = _draws[i].bounds;
                any = true;
            }
            else
            {
                box.Encapsulate(_draws[i].bounds);
            }
        }

        _wallZ = any ? box.center.z : transform.position.z;
        _hud = FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);
    }

    private void LateUpdate()
    {
        Camera eye = Camera.main;
        Transform who = LocalPlayer();

        if (eye == null || who == null)
        {
            Show();
            return;
        }

        bool cameraBehind = eye.transform.position.z < _wallZ - margin;
        bool playerAhead = who.position.z > _wallZ;

        if (cameraBehind && playerAhead)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    /// <summary>내 캐릭터. 네트워크 씬은 HUD 가 알고 있고, 혼자 하는 씬은 아무 사람이나 그 사람이다.</summary>
    private Transform LocalPlayer()
    {
        if (_hud != null && _hud.LocalWorker != null)
        {
            return _hud.LocalWorker.transform;
        }

        TaskWorker any = FindAnyObjectByType<TaskWorker>(FindObjectsInactive.Exclude);
        return any != null ? any.transform : null;
    }

    private void Hide()
    {
        if (_hiddenByMe)
        {
            return;
        }

        for (int i = 0; i < _draws.Length; i++)
        {
            if (_draws[i] != null)
            {
                _draws[i].enabled = false;
            }
        }

        _hiddenByMe = true;
    }

    /// <summary>내가 감춘 것만 도로 켠다. 카메라의 가림 처리가 감춘 것은 그쪽이 되돌린다.</summary>
    private void Show()
    {
        if (!_hiddenByMe)
        {
            return;
        }

        for (int i = 0; i < _draws.Length; i++)
        {
            if (_draws[i] != null)
            {
                _draws[i].enabled = true;
            }
        }

        _hiddenByMe = false;
    }
}
