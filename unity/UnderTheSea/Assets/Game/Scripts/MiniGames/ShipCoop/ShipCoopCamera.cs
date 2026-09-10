using UnityEngine;

/// <summary>
/// 배 협동 게임의 카메라. 배 뒤 · 위에서 **갑판 전체**를 내려다본다. (SHIPCOOP.md 1장)
///
/// 왜 각자 캐릭터를 따라가지 않는가
///   "돛 비었어!" 를 말하려면 돛이 비었다는 것이 보여야 합니다.
///   카메라가 내 캐릭터 뒤에 붙어 앞만 보면 뒤쪽 자리가 비었는지 알 수 없고,
///   그러면 이 게임의 목적인 **서로 소리치는 것**이 안 됩니다.
///   갑판은 12 × 8m 라 한 화면에 다 들어갑니다.
///
/// 화면은 4대에 각각 하나씩입니다. 각자 자기 컴퓨터에서 접속하므로
/// (GAME_STRUCTURE.md 3장) 분할 화면이 아닙니다. 구도만 모두 같습니다.
///
/// 회전
///   Q / E (오른손 스틱) 로 **배를 중심으로** 돕니다. 사람을 중심으로 돌면
///   뱃머리로 갔을 때 고물이 화면 밖으로 나가 갑판이 안 보입니다.
///   대포에 붙어 있는 동안에는 같은 스틱이 포신을 돌리므로 카메라는 멈춥니다. (7장)
///
/// ⚠ 좌우 회전에 제한이 있습니다.
///    <see cref="VoyageSea"/> 는 배를 움직이지 않고 **바다를 반대로 밀어서**
///    배가 꺾이는 것처럼 보이게 합니다. 갑판 위 캐릭터가 배의 자식이 아니라
///    배를 실제로 옮기면 사람들이 갑판에서 흘러내리기 때문입니다.
///    그래서 옆이나 뒤에서 보면 배가 꺾이는 대신 바위가 미끄러지는 것이 보입니다.
///    앞쪽 부채꼴 안에서는 티가 나지 않으므로 그 범위로 묶어 둡니다.
/// </summary>
[RequireComponent(typeof(Camera))]
public class ShipCoopCamera : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [Tooltip("가운데에 둘 대상. 보통 배(갑판)다.")]
    [SerializeField] private Transform target;

    [Tooltip("이 화면의 주인을 알려준다. 입력을 여기서 가져온다.")]
    [SerializeField] private ShipCoopHud hud;

    [Header("구도")]
    [Tooltip("배에서 뒤로 얼마나 물러날지 (m)")]
    [SerializeField, Min(1f)] private float distance = 13f;

    [Tooltip("배보다 얼마나 위에 있을지 (m)")]
    [SerializeField, Min(0f)] private float height = 7f;

    [Tooltip("배 중심에서 이만큼 위를 본다 (m). 올리면 앞바다가 더 보인다.")]
    [SerializeField] private float lookHeight = 1.5f;

    [Tooltip("배 중심보다 이만큼 앞을 본다 (m). 올리면 수평선이 화면 가운데로 온다.")]
    [SerializeField] private float lookAhead = 6f;

    [Header("회전")]
    [Tooltip("초당 몇 도 돌지")]
    [SerializeField, Min(0f)] private float rotateSpeed = 90f;

    [Tooltip("좌우로 이 각도까지만 돈다. 0 이면 회전하지 않는다.\n" +
             "너무 키우면 바다를 미는 방식이 들통난다. 위 주석 참고.")]
    [SerializeField, Range(0f, 180f)] private float maxYaw = 100f;

    [Tooltip("입력이 없을 때 정면으로 되돌아오는 속도 (초당 도). 0 이면 그대로 있는다.")]
    [SerializeField, Min(0f)] private float recenterSpeed = 25f;

    /// <summary>지금 돌아간 각도. 0 이 정면.</summary>
    public float Yaw { get; private set; }

    private void Awake()
    {
        if (hud == null)
        {
            hud = FindAnyObjectByType<ShipCoopHud>(FindObjectsInactive.Include);
        }

        if (target == null)
        {
            // 배를 못 찾으면 항해 쪽이 붙어 있는 오브젝트라도 쓴다.
            GameObject ship = GameObject.Find("Ship");
            if (ship != null)
            {
                target = ship.transform;
            }
            else
            {
                var voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
                target = voyage != null ? voyage.transform : null;
            }
        }

        if (target == null)
        {
            Debug.LogWarning($"[{name}] 가운데에 둘 대상을 찾지 못했습니다. 카메라가 움직이지 않습니다.", this);
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        UpdateYaw();

        Quaternion spin = Quaternion.Euler(0f, Yaw, 0f);
        Vector3 pivot = target.position;

        transform.position = pivot + spin * new Vector3(0f, height, -distance);

        Vector3 lookAt = pivot + Vector3.up * lookHeight + spin * (Vector3.forward * lookAhead);
        transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);
    }

    private void UpdateYaw()
    {
        if (maxYaw <= 0f)
        {
            Yaw = 0f;
            return;
        }

        float turn = Input();

        if (Mathf.Approximately(turn, 0f))
        {
            // 손을 떼면 슬슬 정면으로 돌아온다. 앞을 안 보고 있다가 바위를 맞으면 억울하다.
            Yaw = Mathf.MoveTowards(Yaw, 0f, recenterSpeed * Time.deltaTime);
            return;
        }

        Yaw = Mathf.Clamp(Yaw + turn * rotateSpeed * Time.deltaTime, -maxYaw, maxYaw);
    }

    /// <summary>이 화면 주인의 좌우 스틱. 대포에 붙어 있으면 포신이 가져가므로 0.</summary>
    private float Input()
    {
        TaskWorker worker = hud != null ? hud.LocalWorker : null;
        if (worker == null || worker.Input == null)
        {
            return 0f;
        }

        // 같은 스틱을 대포 조준과 나눠 쓴다. 붙어 있는 동안에는 포신이 먼저다. (7장)
        if (worker.Current is CannonTask)
        {
            return 0f;
        }

        return worker.Input.Look.x;
    }
}
