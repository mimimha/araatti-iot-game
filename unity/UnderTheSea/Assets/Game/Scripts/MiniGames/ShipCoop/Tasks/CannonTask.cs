using System;
using UnityEngine;

/// <summary>
/// 💣 대포. 접근하는 적선과 위협을 제거한다. (SHIPCOOP.md 4장)
///
/// **포탄이 없으면 쏠 수 없습니다.** 포탄은 운반하는 사람이 채워줍니다.
/// 이것이 "포탄 좀 가져와!" 가 나오는 지점이고, 운반이 이 게임의 접착제인 이유입니다.
///
/// 입력
///   발사 → ShipCoopInput.ConsumeFire. 오른손 면버튼 2. 키보드는 K.
///   조준 → 오른손 스틱(마우스 우클릭 드래그). 조준용 입력을 따로 두지 않습니다. (7장)
///
/// 비웠을 때 적선이 계속 포격해 배가 깎이는 것은 적선 쪽(순서 4번 EnemyShip)이 합니다.
/// 대포가 스스로 피해를 주지는 않습니다.
/// </summary>
public class CannonTask : TaskBase
{
    [Header("포탄")]
    [Tooltip("한 번에 실을 수 있는 포탄 수")]
    [SerializeField, Min(1)] private int maxAmmo = 3;

    [Tooltip("게임을 시작할 때 실려 있는 포탄 수")]
    [SerializeField, Min(0)] private int startingAmmo = 1;

    [Header("발사")]
    [Tooltip("연속 발사를 막는 최소 간격 (초)")]
    [SerializeField, Min(0f)] private float fireInterval = 0.8f;

    [Header("조준 (선택)")]
    [Tooltip("연결하면 오른손 스틱으로 이 오브젝트를 좌우로 돌린다. 포신 연출용.")]
    [SerializeField] private Transform barrel;

    [Tooltip("포신이 좌우로 돌아가는 최대 각도")]
    [SerializeField] private float maxTurretYaw = 70f;

    [Tooltip("포신이 초당 몇 도 돌아가는지")]
    [SerializeField] private float turretSpeed = 90f;

    /// <summary>지금 실려 있는 포탄 수</summary>
    public int Ammo { get; private set; }

    /// <summary>실을 수 있는 최대 포탄 수</summary>
    public int MaxAmmo => maxAmmo;

    /// <summary>포탄을 더 실을 자리가 있는지. 운반하는 사람이 이걸 본다.</summary>
    public bool HasRoomForAmmo => Ammo < maxAmmo;

    /// <summary>지금 쏠 수 있는지. 포탄이 있고 간격도 지났는지.</summary>
    public bool CanFire => Ammo > 0 && Time.time >= _nextFireTime;

    /// <summary>포신 각도. 0 이 정면.</summary>
    public float TurretYaw { get; private set; }

    /// <summary>한 발 쐈다. 인자는 쏜 뒤 남은 포탄 수.</summary>
    public event Action<int> Fired;

    /// <summary>포탄 수가 바뀌었다. (지금, 최대) — HUD 가 듣는다.</summary>
    public event Action<int, int> AmmoChanged;

    /// <summary>포탄이 없는데 쏘려고 했다. HUD 가 "포탄 없음" 을 띄운다.</summary>
    public event Action FiredEmpty;

    private float _nextFireTime;
    private float _baseYaw;

    private void Awake()
    {
        Ammo = Mathf.Clamp(startingAmmo, 0, maxAmmo);

        if (barrel != null)
        {
            _baseYaw = barrel.localEulerAngles.y;
        }
    }

    /// <summary>
    /// 포탄을 싣는다. 운반하는 사람이 부른다.
    /// 실제로 실린 개수를 돌려준다. 자리가 없으면 0.
    /// </summary>
    public int LoadAmmo(int amount)
    {
        if (amount <= 0)
        {
            return 0;
        }

        int loaded = Mathf.Min(amount, maxAmmo - Ammo);
        if (loaded <= 0)
        {
            return 0;
        }

        Ammo += loaded;
        AmmoChanged?.Invoke(Ammo, maxAmmo);
        return loaded;
    }

    /// <summary>
    /// **포탄 수를 밖에서 정해 준다.** 네트워크에서 서버가 정한 결과를 화면에 옮길 때 쓴다.
    ///
    /// 싣고 쏘는 판정은 서버에서만 돈다. 클라이언트는 이 값을 복제받아 여기로 넣는다.
    /// 그래야 "포탄 1/3" 안내와 게이지가 나른 사람 화면에도 같이 바뀐다.
    /// 값이 같으면 아무 일도 하지 않으므로 매 프레임 불러도 된다.
    /// </summary>
    public void ShowAmmo(int ammo)
    {
        int clamped = Mathf.Clamp(ammo, 0, maxAmmo);

        if (clamped == Ammo)
        {
            return;
        }

        Ammo = clamped;
        AmmoChanged?.Invoke(Ammo, maxAmmo);
    }

    protected override void Work(float deltaTime)
    {
        for (int i = 0; i < Workers.Count; i++)
        {
            IPlayerController input = Workers[i].Input;
            if (input == null)
            {
                continue;
            }

            Aim(input, deltaTime);

            if (ShipCoopInput.ConsumeFire(input))
            {
                TryFire(input);
            }
        }
    }

    /// <summary>오른손 스틱으로 포신을 돌린다.</summary>
    private void Aim(IPlayerController input, float deltaTime)
    {
        if (barrel == null)
        {
            return;
        }

        float turn = input.Look.x * turretSpeed * deltaTime;
        if (Mathf.Approximately(turn, 0f))
        {
            return;
        }

        TurretYaw = Mathf.Clamp(TurretYaw + turn, -maxTurretYaw, maxTurretYaw);
        barrel.localRotation = Quaternion.Euler(0f, _baseYaw + TurretYaw, 0f);
    }

    private void TryFire(IPlayerController input)
    {
        if (Ammo <= 0)
        {
            // 포탄이 없다는 것을 손으로 알려준다. 화면을 못 보고 있을 수 있다.
            input.VibrateBoth(0.3f, 0.1f);
            FiredEmpty?.Invoke();
            return;
        }

        if (Time.time < _nextFireTime)
        {
            return;
        }

        Ammo--;
        _nextFireTime = Time.time + fireInterval;

        AmmoChanged?.Invoke(Ammo, maxAmmo);
        Fired?.Invoke(Ammo);

        input.VibrateBoth(0.8f, 0.15f);

        // 맞출 대상은 순서 4번의 적선이 붙으면서 생긴다.
        // 격파 판정과 ShipCoopGame.ReportEnemyDestroyed 는 그때 적선 쪽에서 한다.
        Debug.Log($"[{name}] 발사. 남은 포탄 {Ammo}/{maxAmmo}", this);
    }
}
