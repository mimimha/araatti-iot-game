using UnityEngine;

/// <summary>
/// 💥 대포가 **쏘면 뒤로 튄다.** 연출 전용이다. 판정 · 수치에 영향이 없다. (SHIPCOOP.md 4장)
///
/// <c>CannonTask.Recoiled</c> 를 듣는다. 서버에서는 쏘는 순간에, 클라이언트에서는 서버가 복제한
/// 발수(<c>FireCount</c>)가 늘어난 순간에 터지므로 **모든 화면에서 같은 순간에** 튄다.
///
/// <b>어떻게 튀나.</b> 포신 축을 따라 안쪽(뱃전 반대편)으로 <see cref="kickDistance"/> 만큼 빠르게
/// 밀렸다가 천천히 제자리로 돈다. 그러면서 포구가 살짝 들린다. 연기는 포구에서 한 번 뿜는다.
///
/// <b>어느 쪽이 뒤인가 — 재서 정한다.</b> 이 배의 대포 모델은 단일 메시라 포신 · 포가가 나뉘지
/// 않고, 축이 어디를 보는지도 모델마다 다르다. 그래서 켤 때 렌더러 경계에서 **가로로 가장 긴
/// 축**을 포신으로 보고, 배 가운데(x 0)에서 멀어지는 쪽을 포구로 잡는다. 대포는 뱃전에 붙어
/// 바깥을 보기 때문이다. 틀리면 <see cref="muzzleLocal"/> 에 직접 적으면 된다.
///
/// ⚠ <b>로컬 기준으로 움직인다.</b> 대포는 배의 자식이라 월드 자리를 덮어쓰면 배가 틀어진 것이
///    지워진다. 로컬로 두면 배를 따라가고 그 위에서 튄다. (ShipCoopSail · ShipCoopHelmWheel 과 같은 이유)
/// </summary>
public class ShipCoopCannonRecoil : MonoBehaviour
{
    [Header("연결 — 비워두면 찾는다")]
    [Tooltip("튈 대포 모델. 비워두면 자식 중 이름이 Cannon 인 것.")]
    [SerializeField] private Transform cannon;

    [Tooltip("발사를 알려주는 자리. 비워두면 씬에서 찾는다.")]
    [SerializeField] private CannonTask task;

    [Header("반동")]
    [Tooltip("뒤로 밀리는 거리 (m).")]
    [SerializeField, Range(0f, 1f)] private float kickDistance = 0.28f;

    [Tooltip("포구가 들리는 각도 (도).")]
    [SerializeField, Range(0f, 20f)] private float kickTilt = 5f;

    [Tooltip("뒤로 밀리는 데 걸리는 시간 (초). 짧아야 '쾅' 이다.")]
    [SerializeField, Range(0.01f, 0.3f)] private float kickSeconds = 0.06f;

    [Tooltip("제자리로 돌아오는 데 걸리는 시간 (초).")]
    [SerializeField, Range(0.05f, 1.5f)] private float returnSeconds = 0.45f;

    [Header("방향 — 비워두면(0,0,0) 켤 때 잰다")]
    [Tooltip("대포 로컬 기준 포구 방향. 0 이면 렌더러 경계에서 재서 정한다.")]
    [SerializeField] private Vector3 muzzleLocal = Vector3.zero;

    [Header("연기 (선택)")]
    [Tooltip("포구에서 한 번 뿜을 파티클 프리팹. 비워두면 연기 없음.")]
    [SerializeField] private ParticleSystem smokePrefab;

    [Tooltip("연기를 뿜는 시간 (초). 프리팹이 루프여도 이만큼만 뿜고 멈춘다.")]
    [SerializeField, Range(0.05f, 2f)] private float smokeSeconds = 0.35f;

    [Header("포탄 (선택)")]
    [Tooltip("날아가는 포탄에 입힐 재질. 배치 도구가 AmmoBall 재질을 넣는다. 비워두면 기본 재질.")]
    [SerializeField] private Material ballMaterial;

    [Tooltip("포탄 지름 (m). 들고 다니는 포탄(0.6m)보다 살짝 작게 — 멀어지며 작아 보이는 게 자연스럽다.")]
    [SerializeField, Range(0.1f, 1f)] private float ballDiameter = 0.5f;

    [Tooltip("포구 속도 (m/s).")]
    [SerializeField, Range(5f, 80f)] private float ballSpeed = 30f;

    [Tooltip("포구에서 위로 살짝 띄우는 속도 (m/s). 0 이면 곧게, 크면 포물선이 커진다.")]
    [SerializeField, Range(0f, 15f)] private float ballLoft = 3f;

    [Tooltip("포탄에 걸리는 중력 (m/s²). 크면 빨리 떨어진다.")]
    [SerializeField, Range(0f, 40f)] private float ballGravity = 14f;

    [Tooltip("이 시간이 지나거나 수면 아래로 가면 사라진다 (초).")]
    [SerializeField, Range(0.5f, 6f)] private float ballLife = 2.5f;

    /// <summary>날아가는 포탄 하나.</summary>
    private struct Ball
    {
        public Transform Show;
        public Vector3 Velocity;
        public float DieAt;
        public float FloorY;   // 이 아래로 떨어지면 물에 빠진 것으로 친다
    }

    [Tooltip("포구보다 이만큼 아래로 떨어지면 물에 빠진 것으로 보고 지운다 (m).\n" +
             "⚠ 월드 y 0 을 수면으로 쓰면 안 된다 — 앞갑판이 y ≈ -1.8 이라 태어난 프레임에 사라진다.")]
    [SerializeField, Range(1f, 30f)] private float ballDropBelowMuzzle = 6f;

    private readonly System.Collections.Generic.List<Ball> _balls = new System.Collections.Generic.List<Ball>();
    private readonly System.Collections.Generic.Stack<Transform> _ballPool = new System.Collections.Generic.Stack<Transform>();

    private Vector3 _restLocalPosition;
    private Quaternion _restLocalRotation;

    /// <summary>대포 로컬 기준 포구 방향(단위). 반동은 이 반대로 간다.</summary>
    private Vector3 _muzzle = Vector3.right;

    /// <summary>포구 자리 (대포 로컬). 연기가 여기서 난다.</summary>
    private Vector3 _muzzleLocalPoint;

    private ParticleSystem _smoke;
    private float _smokeUntil = -1f;

    /// <summary>튀기 시작한 시각. 음수면 쉬는 중.</summary>
    private float _kickStart = -1f;

    private bool _ready;

    /// <summary>포신 축 (월드). 자리 자세가 손을 놓을 곳을 정할 때 본다.</summary>
    public Vector3 MuzzleWorldDirection => _ready ? cannon.TransformDirection(_muzzle).normalized : Vector3.right;

    /// <summary>튈 대포 모델.</summary>
    public Transform Cannon => cannon;

    private void Awake()
    {
        if (cannon == null)
        {
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cannon")
                {
                    cannon = t;
                    break;
                }
            }
        }

        if (cannon == null)
        {
            Debug.LogWarning($"[{name}] 대포(Cannon)를 찾지 못했습니다. 반동이 없습니다.", this);
            return;
        }

        _restLocalPosition = cannon.localPosition;
        _restLocalRotation = cannon.localRotation;
        MeasureMuzzle();

        if (smokePrefab != null)
        {
            _smoke = Instantiate(smokePrefab, cannon);
            _smoke.transform.localPosition = _muzzleLocalPoint;
            _smoke.transform.localRotation = Quaternion.LookRotation(_muzzle);

            // 프리팹이 루프 · 자동 재생이어도 여기서는 한 번만 뿜는다.
            ParticleSystem.MainModule main = _smoke.main;
            main.loop = true;
            main.playOnAwake = false;
            main.prewarm = false;
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        _ready = true;
    }

    private void OnEnable()
    {
        if (task == null)
        {
            task = FindAnyObjectByType<CannonTask>(FindObjectsInactive.Include);
        }

        if (task != null)
        {
            task.Recoiled += Kick;
        }
        else
        {
            Debug.LogWarning($"[{name}] CannonTask 를 찾지 못했습니다. 반동이 없습니다.", this);
        }
    }

    private void OnDisable()
    {
        if (task != null)
        {
            task.Recoiled -= Kick;
        }
    }

    /// <summary>
    /// 포신이 어디를 보는지 잰다. 가로로 가장 긴 축이 포신, 배 가운데에서 먼 쪽이 포구.
    /// 배가 아직 안 돌아간 켜는 순간에 재므로 월드 경계로 봐도 배 축과 같다.
    /// </summary>
    private void MeasureMuzzle()
    {
        if (muzzleLocal.sqrMagnitude > 1e-6f)
        {
            _muzzle = muzzleLocal.normalized;
        }
        else
        {
            Renderer draw = cannon.GetComponentInChildren<Renderer>();
            Bounds box = draw != null ? draw.bounds : new Bounds(cannon.position, Vector3.one);

            // 배 기준으로 본다. 배 루트가 이 컴포넌트가 붙은 곳이다.
            Vector3 size = transform.InverseTransformVector(box.size);
            Vector3 axisShip = Mathf.Abs(size.x) >= Mathf.Abs(size.z) ? Vector3.right : Vector3.forward;

            Vector3 centreShip = transform.InverseTransformPoint(box.center);
            float side = Vector3.Dot(centreShip, axisShip);
            Vector3 muzzleShip = axisShip * (side >= 0f ? 1f : -1f);

            _muzzle = cannon.InverseTransformDirection(transform.TransformDirection(muzzleShip)).normalized;
        }

        // 포구 자리 — 경계에서 포신 방향으로 가장 먼 점.
        Renderer r = cannon.GetComponentInChildren<Renderer>();
        if (r != null)
        {
            Vector3 muzzleWorld = cannon.TransformDirection(_muzzle).normalized;
            Vector3 half = r.bounds.extents;
            float reach = Mathf.Abs(muzzleWorld.x) * half.x + Mathf.Abs(muzzleWorld.y) * half.y + Mathf.Abs(muzzleWorld.z) * half.z;
            _muzzleLocalPoint = cannon.InverseTransformPoint(r.bounds.center + muzzleWorld * reach);
        }
        else
        {
            _muzzleLocalPoint = _muzzle;
        }
    }

    /// <summary>한 번 튄다. 튀는 중이면 처음부터 다시.</summary>
    public void Kick()
    {
        if (!_ready)
        {
            return;
        }

        _kickStart = Time.time;

        if (_smoke != null)
        {
            _smoke.Play(true);
            _smokeUntil = Time.time + smokeSeconds;
        }

        LaunchBall();
    }

    /// <summary>
    /// 💣 포구에서 포탄이 날아간다. 연출 전용 — 맞는지는 적선 쪽(EnemyShip)이 따로 판정한다.
    ///
    /// 콜라이더 없는 구를 포구에서 포신 방향으로 던지고, 중력을 걸어 포물선으로 떨어뜨린다.
    /// 배 자식이 아니라 월드에 둔다 — 배를 따라가면 날아가는 동안 배가 돌 때 포탄이 휘어진다.
    /// 구는 되쓴다. 연사 간격 0.8초 · 수명 2.5초라 동시에 많아야 서넛이다.
    /// </summary>
    private void LaunchBall()
    {
        Transform show = _ballPool.Count > 0 ? _ballPool.Pop() : MakeBall();

        if (show == null)
        {
            return;
        }

        Vector3 muzzleWorld = cannon.TransformDirection(_muzzle).normalized;

        show.position = cannon.TransformPoint(_muzzleLocalPoint);
        show.gameObject.SetActive(true);

        _balls.Add(new Ball
        {
            Show = show,
            Velocity = muzzleWorld * ballSpeed + Vector3.up * ballLoft,
            DieAt = Time.time + ballLife,
            FloorY = show.position.y - ballDropBelowMuzzle,
        });
    }

    private Transform MakeBall()
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "CannonBall (연출)";

        Collider bump = go.GetComponent<Collider>();
        if (bump != null)
        {
            Destroy(bump);
        }

        go.transform.localScale = Vector3.one * ballDiameter;

        Renderer draw = go.GetComponent<Renderer>();
        if (draw != null)
        {
            draw.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            if (ballMaterial != null)
            {
                draw.sharedMaterial = ballMaterial;
            }
        }

        return go.transform;
    }

    private void FlyBalls()
    {
        for (int i = _balls.Count - 1; i >= 0; i--)
        {
            Ball ball = _balls[i];

            ball.Velocity += Vector3.down * (ballGravity * Time.deltaTime);
            ball.Show.position += ball.Velocity * Time.deltaTime;

            // 포구보다 한참 아래로 떨어졌거나(물에 빠짐) 수명이 끝났다.
            if (Time.time >= ball.DieAt || ball.Show.position.y < ball.FloorY)
            {
                ball.Show.gameObject.SetActive(false);
                _ballPool.Push(ball.Show);
                _balls.RemoveAt(i);
                continue;
            }

            _balls[i] = ball;
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _balls.Count; i++)
        {
            if (_balls[i].Show != null) Destroy(_balls[i].Show.gameObject);
        }

        while (_ballPool.Count > 0)
        {
            Transform t = _ballPool.Pop();
            if (t != null) Destroy(t.gameObject);
        }
    }

    private void LateUpdate()
    {
        if (!_ready)
        {
            return;
        }

        FlyBalls();

        if (_smoke != null && _smokeUntil >= 0f && Time.time >= _smokeUntil)
        {
            _smoke.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _smokeUntil = -1f;
        }

        if (_kickStart < 0f)
        {
            return;
        }

        float t = Time.time - _kickStart;
        float amount;

        if (t < kickSeconds)
        {
            // 확 밀린다.
            amount = t / kickSeconds;
        }
        else if (t < kickSeconds + returnSeconds)
        {
            // 천천히 돈다. 끝에서 부드럽게 멈춘다.
            float back = (t - kickSeconds) / returnSeconds;
            amount = 1f - Mathf.SmoothStep(0f, 1f, back);
        }
        else
        {
            amount = 0f;
            _kickStart = -1f;
        }

        // 뒤로 = 포구 반대. 로컬 위치라 배가 돌아도 같이 간다.
        Vector3 backLocal = -_muzzle * (kickDistance * amount);

        // 포구가 들린다 — 포신 축과 위 축에 수직인 축으로 돈다.
        Vector3 upLocal = cannon.InverseTransformDirection(Vector3.up);
        Vector3 tiltAxis = Vector3.Cross(_muzzle, upLocal);
        Quaternion tilt = tiltAxis.sqrMagnitude > 1e-6f
            ? Quaternion.AngleAxis(-kickTilt * amount, tiltAxis.normalized)
            : Quaternion.identity;

        cannon.localPosition = _restLocalPosition + _restLocalRotation * backLocal;
        cannon.localRotation = _restLocalRotation * tilt;
    }
}
