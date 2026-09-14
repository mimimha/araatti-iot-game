using UnityEngine;

/// <summary>
/// 🪨 판정이 끝난 바위를 **화면 밖으로 나갈 때까지** 계속 흘려보낸다. (SHIPCOOP.md 5장)
///
/// ⚠ **판정과 바위의 수명은 다릅니다.**
///
///    `Reef` 는 바위 앞면이 **뱃머리(z 22.8)를 넘는 순간** 판정합니다.
///    보이는 것과 판정을 맞추려고 그렇게 했습니다.
///
///    그런데 그 순간 사건이 끝나면서 `OnHide` 가 바위를 지워버렸습니다.
///    그때 바위는 z 30.9, 조타수 카메라(z −24.5)에서 **55m 앞**입니다.
///    저 멀리서 바위가 뿅 사라집니다.
///
///    판정은 뱃머리에서 하되, 바위는 **뒤로 다 지나갈 때까지** 남깁니다.
///    피한 바위가 옆을 스쳐 지나가는 것이 보여야 "피했다" 가 눈에 남습니다.
///
/// ⚠ **부딪힌 바위는 여기 안 옵니다.** 그 자리에서 바로 사라집니다.
///    판정이 **진짜 겹침**이라(`Reef.Judge`) 부딪혔다면 정말로 선체에
///    닿아 있습니다. 배를 뚫고 지나가는 바위를 보여줄 수는 없습니다.
/// </summary>
public class ReefDrift : MonoBehaviour
{
    private float _laneX;
    private float _speed;
    private float _sinkY;
    private float _z;

    /// <summary>카메라보다 이만큼 더 뒤로 가면 지운다 (m).</summary>
    private const float BehindCamera = 25f;

    /// <summary>카메라를 못 찾았을 때 쓸 한계 z.</summary>
    private const float FallbackStopZ = -60f;

    /// <summary>
    /// 흘려보내기 시작한다.
    /// </summary>
    /// <param name="laneX">배 중심에서 좌우로 떨어진 거리. 판정에 쓰던 값 그대로.</param>
    /// <param name="speed">초당 몇 m 로 다가오던 바위인가.</param>
    /// <param name="sinkY">물에 잠긴 깊이. 음수다.</param>
    public void Begin(float laneX, float speed, float sinkY)
    {
        _laneX = laneX;
        _speed = Mathf.Max(speed, 0.1f);
        _sinkY = sinkY;
        _z = transform.position.z - (VoyageSea.Current != null ? VoyageSea.Current.Origin.z : 0f);
    }

    private void Update()
    {
        if (VoyageSea.Current == null)
        {
            Destroy(gameObject);
            return;
        }

        _z -= _speed * Time.deltaTime;

        // ⚠ 옆으로 밀리는 것도 계속 따라가야 한다. 안 그러면 조타할 때
        //    지나간 바위만 세상과 따로 놀며 옆으로 미끄러진다.
        transform.position = VoyageSea.Current.Origin
                             + new Vector3(_laneX - VoyageSea.Current.ShipLateral, _sinkY, _z);

        if (_z < StopZ())
        {
            Destroy(gameObject);
        }
    }

    // 조타수 카메라가 가장 뒤에 있다 (뒷갑판 기준 z −24.5). 거기서 더 뒤로 보낸다.
    private float StopZ()
    {
        Camera eye = Camera.main;

        if (eye == null)
        {
            return FallbackStopZ;
        }

        float originZ = VoyageSea.Current != null ? VoyageSea.Current.Origin.z : 0f;

        return eye.transform.position.z - originZ - BehindCamera;
    }
}
