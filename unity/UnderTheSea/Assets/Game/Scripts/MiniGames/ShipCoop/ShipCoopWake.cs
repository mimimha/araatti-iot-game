using UnityEngine;

/// <summary>
/// 🌊 뱃머리가 가르는 **물살.** 배가 나아가는 것을 눈에 보이게 한다. (SHIPCOOP.md 5장)
///
/// ⚠ 왜 필요한가 — 바다 판만으로는 나아가는 것이 안 보입니다.
///
///    쓰는 물 셰이더는 무늬를 **월드 좌표로** 그리고 시간으로 스스로 일렁입니다.
///    판을 밀어도 무늬가 안 따라오고, 무늬를 밀어도 **속도가 변한 것이 잘 안 읽힙니다.**
///    돛을 당겨 빨라졌는데 화면이 그대로면 돛을 당길 이유가 없습니다.
///
///    물살은 다릅니다. **빠르면 많이, 느리면 적게** 생깁니다.
///    속도가 그림의 양으로 바뀌므로 한눈에 읽힙니다.
///
/// ⚠ 납작한 사각형을 얹는 방식은 **하지 마세요.** 이미 해봤습니다.
///    아무리 잘게 줄여도 바다 위에 흰 판때기가 둥둥 뜬 것으로 보입니다.
///    가장자리가 흐린 **둥근 알갱이**여야 물거품으로 보입니다. (파티클)
/// </summary>
public class ShipCoopWake : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private ShipVoyage voyage;

    [Tooltip("물살을 뿌리는 파티클. 비워두면 이 오브젝트에서 찾는다.")]
    [SerializeField] private ParticleSystem spray;

    [Header("얼마나 뿌릴지")]
    [Tooltip("이 속도(m/s)에서 아래 개수만큼 나온다. 돛을 다 당겼을 때의 속도로 둔다.")]
    [SerializeField, Min(0.1f)] private float fullSpeed = 5f;

    [Tooltip("가장 빠를 때 초당 알갱이 수.\n\n" +
             "**잘고 많아야 거품으로 보입니다.** 적으면 띄엄띄엄한 흰 얼룩이 되어 인위적이다.")]
    [SerializeField, Min(0f)] private float mostPerSecond = 420f;

    [Tooltip("멈춰 있어도 이만큼은 나온다. 0 이면 서 있을 때 바다가 죽은 것처럼 보인다.")]
    [SerializeField, Min(0f)] private float leastPerSecond = 90f;

    [Header("얼마나 빨리 흘려보낼지")]
    [Tooltip("배 속도의 몇 배로 뒤로 흘려보낼지.\n\n" +
             "1 이면 물이 제자리에 있는 것처럼 보인다. 넘겨야 **가르고 지나가는** 느낌이 난다.")]
    [SerializeField, Min(0.1f)] private float flowRatio = 2.2f;

    [Tooltip("멈춰 있어도 이만큼(m/s)은 흐른다.\n\n" +
             "배가 느릴 때 물살이 거의 안 움직여서 **바다가 멈춰 보입니다.**\n" +
             "바다에는 늘 해류가 있으니 밑바닥을 깔아둔다.")]
    [SerializeField, Min(0f)] private float leastFlow = 2f;

    private ParticleSystem.EmissionModule _emission;
    private ParticleSystem.MainModule _main;
    private bool _ready;

    private void Awake()
    {
        if (voyage == null)
        {
            voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        }

        if (spray == null)
        {
            spray = GetComponentInChildren<ParticleSystem>(true);
        }

        if (spray == null || voyage == null)
        {
            Debug.LogWarning(
                $"[{name}] 물살을 뿌릴 수 없습니다. 파티클이나 항해를 찾지 못했습니다.", this);
            return;
        }

        _emission = spray.emission;
        _main = spray.main;
        _ready = true;
    }

    private void Update()
    {
        if (!_ready)
        {
            return;
        }

        float speed = Mathf.Max(0f, voyage.Speed);
        float how = Mathf.Clamp01(speed / fullSpeed);

        _emission.rateOverTime = Mathf.Lerp(leastPerSecond, mostPerSecond, how);

        // 뒤로 흘러가는 속도. 배보다 빨라야 가르고 지나가는 것처럼 보이고,
        // 멈춰 있어도 해류만큼은 흐른다.
        _main.startSpeed = Mathf.Max(leastFlow, speed * flowRatio);
    }
}
