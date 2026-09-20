using UnityEngine;

/// <summary>
/// 📍 자리마다 **서는 정위치와 보는 방향**. 붙는 순간 캐릭터가 여기로 미끄러져 간다. (SHIPCOOP.md 4장)
///
/// <code>
///   💣 대포     포미(안쪽) 뒤에 서서 포구 쪽을 본다
///   🪢 돛       돛대 오른쪽(우현, 화면 오른쪽)에 서서 돛대를 본다
///   🛞 조타     바퀴 뒤(선미 쪽)에 서서 뱃머리 쪽을 본다 — 뒷모습
/// </code>
///
/// <b>왜 필요한가.</b> 붙기 범위(2m) 안이면 어디서 스페이스를 눌러도 붙는다. 그러면 대포 옆구리에서
/// 어정쩡하게 팔만 뻗은 모습이 된다. 정위치로 옮기면 **붙었다는 것이 확 보이고**, 잡는 자세
/// (<see cref="ShipCoopStationPose"/>)도 늘 같은 각도에서 나온다.
///
/// ⚠ <b>정위치는 자리 범위 안에 있어야 한다.</b> 범위를 벗어나면 <c>TaskWorker</c> 가 "멀어졌다" 고
///    보고 바로 떨어진다. 그래서 자리에서 <see cref="MaxFromStation"/> 배 이상 멀어지지 않게 당긴다.
///
/// 방향 · 거리는 배 기준(자리의 부모 축)이라 배가 틀어져도 따라간다. 높이는 지금 서 있는 높이를 쓴다 —
/// 중력과 갑판 콜라이더가 알아서 맞춘다.
/// </summary>
public static class ShipCoopStationStand
{
    /// <summary>자리에서 이 비율(붙기 범위 대비, 3차원)보다 멀어지지 않는다. 남는 0.08 × 2m = 16cm 가 여유다.</summary>
    private const float MaxFromStation = 0.92f;

    private const float CannonBack = 2.0f;   // 포미 뒤로 (m). 팔(0.6m)이 포미 끝에 닿아야 해서 더 못 뺀다. 대포 자리 범위는 3m (씬 값)
    private const float SailSide = 1.3f;     // 돛대 오른쪽으로 (m)
    private const float HelmBack = 1.15f;    // 바퀴 뒤로 (m)
    private const float HelmSecondSide = 0.9f; // 두 번째 조타수는 이만큼 오른쪽 옆 (m)

    /// <summary>
    /// 이 자리의 정위치. 없는 자리(수리 · 운반 등)면 false.
    /// </summary>
    /// <param name="task">붙은 자리</param>
    /// <param name="who">붙은 사람. 조타에 둘이 붙었을 때(파도) 두 번째 사람은 옆으로 비켜 선다.</param>
    /// <param name="currentY">지금 서 있는 높이. 정위치의 y 로 그대로 쓴다.</param>
    /// <param name="stand">설 곳 (월드)</param>
    /// <param name="facing">볼 방향 (월드, 수평 단위)</param>
    public static bool TryGet(TaskBase task, TaskWorker who, float currentY, out Vector3 stand, out Vector3 facing)
    {
        stand = default;
        facing = default;

        if (task == null)
        {
            return false;
        }

        // 몇 번째로 붙었나. 평소 정원은 1 이라 0 이고, 파도 중 조타만 1 이 나온다.
        int order = 0;
        for (int i = 0; i < task.Workers.Count; i++)
        {
            if (ReferenceEquals(task.Workers[i], who))
            {
                order = i;
                break;
            }
        }

        Transform station = task.transform;
        Vector3 shipRight = Flat(station.right, Vector3.right);
        Vector3 shipForward = Flat(station.forward, Vector3.forward);
        Vector3 at = station.position;

        switch (task)
        {
            case CannonTask _:
            {
                // 포구는 **뱃전 바깥**이다. 자리가 배 가운데에서 어느 쪽에 있는지(배-로컬 x 부호)로 정한다.
                //
                // ⚠ 반동 연출(ShipCoopCannonRecoil)이 렌더러로 잰 포신 축을 빌려 썼는데, 이 계산은 **서버**에서
                //    도는데 Dedicated Server 는 렌더러가 꺼져 있어 측정이 되지 않고 월드 +x 로 떨어졌다.
                //    배가 조타로 돌아가 있으면 월드 +x 는 배 기준 옆이나 앞이라, 헤딩에 따라 "어떨 땐 뒤, 어떨 땐 앞"
                //    에 섰다. 배 축으로 정하면 측정도 헤딩도 상관없다.
                Vector3 local = station.parent != null ? station.parent.InverseTransformPoint(at) : at;
                Vector3 muzzle = local.x >= 0f ? shipRight : -shipRight;

                stand = at - muzzle * CannonBack;
                facing = muzzle;
                break;
            }

            case SailTask _:
                stand = at + shipRight * SailSide;
                facing = -shipRight;
                break;

            case HelmTask _:
            {
                var wheel = Object.FindAnyObjectByType<ShipCoopHelmWheel>(FindObjectsInactive.Include);

                if (wheel != null)
                {
                    Vector3 centre = wheel.Center;
                    Vector3 bow = Flat(wheel.Axis, shipForward);

                    // 축의 두 방향 중 뱃머리 쪽 — 배 앞과 같은 쪽.
                    if (Vector3.Dot(bow, shipForward) < 0f) bow = -bow;

                    stand = new Vector3(centre.x, at.y, centre.z) - bow * HelmBack;
                    facing = bow;
                }
                else
                {
                    stand = at - shipForward * HelmBack;
                    facing = shipForward;
                }

                // 파도 중 둘이 붙으면(6장) 두 번째 사람은 바퀴 오른쪽 옆에서 같이 잡는다.
                if (order > 0)
                {
                    stand += shipRight * HelmSecondSide;
                }

                break;
            }

            default:
                return false;
        }

        // 자리 범위 안으로 당긴다. 벗어나면 붙자마자 떨어진다.
        //
        // ⚠ 범위 판정(TaskBase.IsInRange)은 **3차원 거리**다. 자리는 손 높이(갑판 위 0.7 ~ 1.6m)에
        //    떠 있고 사람 위치는 발이라, 높이 차가 먼저 범위를 먹는다. 돛 자리는 1.6m 위여서 옆으로
        //    1.3m 만 비켜도 √(1.6² + 1.3²) = 2.06m > 2m 가 되어 붙자마자 떨어졌다.
        //    그래서 높이 차를 뺀 나머지만큼만 수평으로 벌린다.
        Vector3 fromStation = stand - at;
        fromStation.y = 0f;

        float rise = Mathf.Abs(currentY - at.y);
        float limit3D = task.InteractRange * MaxFromStation;
        float limitFlat = Mathf.Sqrt(Mathf.Max(0f, limit3D * limit3D - rise * rise));

        if (fromStation.magnitude > limitFlat)
        {
            fromStation = fromStation.sqrMagnitude > 1e-6f ? fromStation.normalized * limitFlat : Vector3.zero;
            stand = at + fromStation;
        }

        stand.y = currentY;
        return true;
    }

    /// <summary>수평 단위 벡터. 너무 작으면 대신 값.</summary>
    private static Vector3 Flat(Vector3 v, Vector3 fallback)
    {
        v.y = 0f;
        return v.sqrMagnitude > 1e-6f ? v.normalized : fallback;
    }
}
