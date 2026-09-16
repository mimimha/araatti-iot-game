using System.Collections.Generic;
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
///   오른손 스틱(키보드는 마우스 우클릭 드래그) 으로 **배를 중심으로** 돕니다. 사람을 중심으로 돌면
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
/// <summary>카메라가 무엇을 가운데에 둘지. (SHIPCOOP.md 9장)</summary>
public enum CameraMode
{
    /// <summary>
    /// 🪜 층 고정. 사람이 있는 갑판 한 층을 비춘다.
    ///
    /// 같은 층에 있는 사람끼리 **똑같은 화면**을 봅니다. 그래야 "야 저기!" 가 통합니다.
    /// 자리가 늘 같은 곳에 보여서 어디에 뭐가 있는지도 외워집니다.
    /// 돛대와 밧줄에 가리는 각도를 층마다 한 번만 잡아두면 됩니다.
    /// </summary>
    PerDeck,

    /// <summary>
    /// 🏃 사람 따라가기. 내 캐릭터를 따라다닌다.
    ///
    /// 내가 늘 화면 가운데 있어서 위치는 헷갈리지 않습니다. 대신 4명이 각자
    /// 다른 화면을 보게 되고, 돛대에 가리는 순간이 계속 생깁니다.
    /// </summary>
    FollowPlayer,
}

[RequireComponent(typeof(Camera))]
public class ShipCoopCamera : MonoBehaviour
{
    [Header("무엇을 가운데에 둘지")]
    [Tooltip("층 고정 — 사람이 있는 갑판 한 층을 비춘다. 같은 층 사람끼리 같은 화면을 본다.\n" +
             "사람 따라가기 — 내 캐릭터를 따라다닌다. 흔한 3D 게임 방식.\n\n" +
             "어느 쪽이 나은지는 4명이서 해봐야 압니다. 그래서 골라 쓸 수 있게 두었습니다.")]
    [SerializeField] private CameraMode mode = CameraMode.PerDeck;

    [Tooltip("층이 바뀔 때 옮겨가는 시간 (초).\n\n" +
             "0 이면 순간이동이라 어디로 갔는지 모릅니다.\n" +
             "1초쯤 되면 답답합니다. 0.2~0.3 이 적당합니다.")]
    [SerializeField, Range(0f, 1f)] private float moveTime = 0.25f;

    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [Tooltip("층을 못 찾았을 때 가운데에 둘 대상. 보통 배다.")]
    [SerializeField] private Transform target;

    [Tooltip("이 화면의 주인을 알려준다. 입력과 지금 있는 층을 여기서 가져온다.")]
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

    /// <summary>
    /// 이 화면 주인이 지금 있는 갑판. 없으면 null.
    ///
    /// 카메라만 쓰는 것이 아닙니다. **HUD 가 "뒷갑판 침수!" 를 띄울 때도 이걸 봅니다.** (9장)
    /// </summary>
    public ShipDeck CurrentDeck { get; private set; }

    /// <summary>지금 가운데에 두고 있는 자리. 층이 바뀌면 여기로 미끄러져 간다.</summary>
    private Vector3 _pivot;
    private Vector3 _pivotSpeed;
    private bool _pivotReady;

    /// <summary>
    /// 지금 쓰고 있는 구도. 거리 · 높이 · 보는 높이 · 보는 앞쪽.
    ///
    /// **층마다 다릅니다.** 자리(pivot)와 함께 미끄러지듯 바뀌어야
    /// 계단을 오를 때 카메라가 튀지 않습니다.
    /// </summary>
    private Vector4 _view;
    private Vector4 _viewSpeed;

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

        // 층(ShipDeck)이 있으면 그쪽을 먼저 쓰므로 target 이 없어도 된다.
        // 둘 다 없을 때만 문제다.
        if (target == null && ShipDeck.All.Count == 0)
        {
            Debug.LogWarning(
                $"[{name}] 갑판(ShipDeck)도 가운데에 둘 대상도 찾지 못했습니다. " +
                "Tools / ShipCoop / 갑판 3층으로 배치 를 한 번 돌리세요.", this);
        }
    }

    private void LateUpdate()
    {
        UpdateYaw();
        UpdatePivot();

        Quaternion spin = Quaternion.Euler(0f, Yaw, 0f);

        transform.position = _pivot + spin * new Vector3(0f, _view.y, -_view.x);

        Vector3 lookAt = _pivot + Vector3.up * _view.z + spin * (Vector3.forward * _view.w);
        transform.rotation = Quaternion.LookRotation(lookAt - transform.position, Vector3.up);

        HideWhatBlocksNow();
    }

    // ------------------------------------------------------------
    // 지금 눈앞을 가리는 것만 잠깐 감춘다
    //
    // 층마다 미리 정해둔 목록(`ShipDeck.hideWhenHere`)은 **그 층에서 늘 가리는 것**을
    // 맡습니다. 뒷갑판 구조물처럼 어느 각도에서든 막는 것들입니다.
    //
    // 그런데 난간이나 돛대처럼 **카메라를 돌리면 비켜나는 것**이 있습니다.
    // 이런 것까지 목록에 넣으면 그 층 내내 사라져 있습니다. 실제로 뒷갑판에서
    // 난간을 목록에 넣었더니 **사방 난간이 통째로 없어졌습니다.**
    // (난간 한 층이 메시 하나라 앞쪽만 감출 수가 없습니다)
    //
    // 그래서 **매 프레임 카메라에서 선을 그어보고**, 거기 걸리는 것만 감춥니다.
    // 카메라를 돌려 비켜나면 바로 돌아옵니다.
    //
    // 선은 **둘**입니다. 사람에게 하나, 카메라가 담는 갑판 한가운데에 하나.
    // 사람 하나만 보면 "사람은 안 가리는데 화면은 다 가리는" 것을 놓칩니다.
    // ------------------------------------------------------------

    [Header("눈앞을 가리는 것 (카메라를 돌리면 돌아온다)")]
    [Tooltip("끄면 미리 정해둔 목록만 쓴다. 난간·돛대가 사람을 가려도 그대로 둔다.")]
    [SerializeField] private bool hideWhatBlocks = true;

    [Tooltip("사람 주변 이만큼(m) 안에 걸리는 것을 가린 것으로 본다.\n" +
             "가늘게 두면 난간 살 사이로 빠져나가 깜빡인다.")]
    [SerializeField, Min(0.1f)] private float blockRadius = 0.8f;

    [Tooltip("사람의 발밑이 아니라 이만큼(m) 위를 본다. 몸통 높이.")]
    [SerializeField, Min(0f)] private float blockAimHeight = 1.4f;

    /// <summary>
    /// 가려도 **감추면 안 되는 것.** 이름이 이걸로 시작하면 그냥 둔다.
    ///
    /// ⚠ 가린다고 다 감추면 **사람이 봐야 하는 것까지 사라집니다.**
    ///
    ///   Deck      걷는 바닥. 감추면 갑판에 구멍이 뚫린 것처럼 보인다
    ///   Stairs    걸어다니는 경사로가 안 보이므로, **올라갈 길을 알려주는 유일한 단서**
    ///   Wheel     🛞 조타 자리. 지금 쓰는 물건이 사라지면 안 된다
    ///   Cannon    💣 대포 자리. 같은 이유
    ///   Mast      ⛵ 돛 자리이자 배의 뼈대. 통째로 사라지면 배가 무너져 보인다
    ///   Hull      선체. 감추면 갑판만 바다 위에 뜬다
    ///
    /// 여기 없는 것 — 난간 · 밧줄 · 돛천 · 선실 벽 — 은 가리면 감춥니다.
    /// 카메라를 돌려 비켜나면 바로 돌아옵니다.
    /// </summary>
    private static readonly string[] NeverHideLive =
    {
        "Deck", "Stairs", "Wheel", "Cannon", "Mast", "Hull",
    };

    /// <summary>지금 감춰둔 것들. 안 가리게 되면 도로 켠다.</summary>
    private readonly List<Renderer> _blocking = new List<Renderer>();

    private readonly HashSet<Renderer> _stillBlocking = new HashSet<Renderer>();

    private void OnDisable()
    {
        ShowBlockersAgain();
    }

    private void HideWhatBlocksNow()
    {
        if (!hideWhatBlocks)
        {
            ShowBlockersAgain();
            return;
        }

        TaskWorker worker = FindObjectOfTypeCached();

        if (worker == null)
        {
            ShowBlockersAgain();
            return;
        }

        _stillBlocking.Clear();

        // ① 사람을 가리는 것.
        CollectBlockers(worker.transform.position + Vector3.up * blockAimHeight, worker);

        // ② 카메라가 담고 있는 **갑판 한가운데**(_pivot)를 가리는 것.
        //
        // ⚠ 사람만 보면 놓치는 것이 있다. 뒷갑판 앞면(Wall_AftBulkhead) 처럼
        //    카메라 바로 앞에 선 큰 벽은, 사람이 앞으로 걸어가면 카메라→사람 선이
        //    벽 위로 올라가면서 "안 가린다" 로 판정된다. 그 순간 벽이 도로 켜지고
        //    화면 아래를 통째로 먹는다. 걸어다니는 내내 벽이 깜빡이게 된다.
        //
        //    카메라가 벽 뒤에 서 있는 동안 그 벽은 갑판 한가운데를 계속 가리므로,
        //    여기까지 보면 숨은 채로 유지된다.
        //
        // ⚠ 겨냥점은 _pivot 이다. LateUpdate 가 쓰는 lookAt 이 아니다.
        //    lookAt 은 lookAhead(기본 6m)만큼 앞을 보는 점이라 선이 너무 높게 지나가
        //    벽 위를 스쳐 간다. 실제로 그렇게 해봤더니 위쪽 슬래브를 놓쳤다.
        CollectBlockers(_pivot, worker);

        // 이제 안 가리는 것은 도로 켠다.
        for (int i = _blocking.Count - 1; i >= 0; i--)
        {
            if (_blocking[i] == null || !_stillBlocking.Contains(_blocking[i]))
            {
                if (_blocking[i] != null)
                {
                    _blocking[i].enabled = true;
                }

                _blocking.RemoveAt(i);
            }
        }

        // 새로 가리는 것은 감춘다.
        foreach (Renderer draw in _stillBlocking)
        {
            if (!draw.enabled || _blocking.Contains(draw))
            {
                continue;
            }

            draw.enabled = false;
            _blocking.Add(draw);
        }
    }

    /// <summary>
    /// 카메라에서 <paramref name="target"/> 까지 굵은 선을 긋고, 거기 걸리는 것을
    /// <see cref="_stillBlocking"/> 에 모은다. 켜고 끄는 것은 부르는 쪽이 한다.
    ///
    /// 여러 번 불러 **합집합**을 만드는 것을 전제로 한다. 그래서 여기서는
    /// <see cref="_stillBlocking"/> 를 비우지 않는다.
    /// </summary>
    private void CollectBlockers(Vector3 target, TaskWorker worker)
    {
        Vector3 toTarget = target - transform.position;
        float far = toTarget.magnitude;

        if (far < 0.5f)
        {
            return;
        }

        // 목표 바로 앞에서 멈춘다. 사람 자신이나 사람이 든 물건은 안 감춘다.
        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position, blockRadius, toTarget / far, far - blockRadius,
            ~0, QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hits.Length; i++)
        {
            Transform what = hits[i].collider.transform;

            if (what == worker.transform || what.IsChildOf(worker.transform))
            {
                continue;
            }

            if (MustStayVisible(what.name))
            {
                continue;
            }

            Renderer draw = hits[i].collider.GetComponent<Renderer>();

            if (draw != null)
            {
                _stillBlocking.Add(draw);
            }
        }
    }

    private static bool MustStayVisible(string name)
    {
        for (int i = 0; i < NeverHideLive.Length; i++)
        {
            if (name.StartsWith(NeverHideLive[i]))
            {
                return true;
            }
        }

        return false;
    }

    private void ShowBlockersAgain()
    {
        for (int i = 0; i < _blocking.Count; i++)
        {
            if (_blocking[i] != null)
            {
                _blocking[i].enabled = true;
            }
        }

        _blocking.Clear();
    }

    private TaskWorker _worker;

    private TaskWorker FindObjectOfTypeCached()
    {
        if (_worker == null || !_worker.gameObject.activeInHierarchy)
        {
            _worker = FindAnyObjectByType<TaskWorker>(FindObjectsInactive.Exclude);
        }

        return _worker;
    }

    /// <summary>
    /// 이 층을 어디서 볼지. 층이 따로 정해두지 않았으면 인스펙터 기본값을 쓴다.
    ///
    /// **층마다 생김새가 다릅니다.** 중간갑판은 뒤에 선미루(4.9m 높이)가 서 있어서,
    /// 뒷갑판과 같은 높이로 보면 **카메라가 그 구조물 안으로 들어갑니다.**
    /// 그러면 조타륜만 코앞에 보이고 갑판은 하나도 안 보입니다.
    /// </summary>
    private Vector4 WantedView()
    {
        if (CurrentDeck != null && CurrentDeck.OverridesCamera)
        {
            return CurrentDeck.CameraView;
        }

        return new Vector4(distance, height, lookHeight, lookAhead);
    }

    /// <summary>
    /// 가운데에 둘 자리를 정한다.
    ///
    /// **층 고정** 이면 사람이 있는 갑판의 한가운데, **따라가기** 면 사람 자리다.
    /// 둘의 차이는 이 함수 하나뿐이고, 구도(거리 · 높이 · 회전)는 똑같이 쓴다.
    /// </summary>
    private void UpdatePivot()
    {
        TaskWorker worker = hud != null ? hud.LocalWorker : null;

        ShipDeck before = CurrentDeck;
        CurrentDeck = worker != null ? ShipDeck.At(worker.transform.position) : null;

        // 층이 바뀌면 가로막던 것을 도로 보여주고, 새 층의 가로막는 것을 감춘다.
        //
        // 중간갑판에 서면 뒷갑판 바닥과 난간이 눈앞을 가립니다. 카메라를 높여도
        // 그 사이에 있으니 소용이 없어서, 그 층에 있는 동안에는 아예 감춥니다.
        if (!ReferenceEquals(before, CurrentDeck))
        {
            if (before != null)
            {
                before.HideBlockers(false);
            }

            if (CurrentDeck != null)
            {
                CurrentDeck.HideBlockers(true);
            }
        }

        Vector3 want = FindPivot(worker);
        Vector4 wantView = WantedView();

        if (!_pivotReady)
        {
            // 첫 프레임에 멀리서 날아오지 않게 바로 자리를 잡는다.
            _pivot = want;
            _view = wantView;
            _pivotSpeed = Vector3.zero;
            _viewSpeed = Vector4.zero;
            _pivotReady = true;
            return;
        }

        if (moveTime <= 0f)
        {
            _pivot = want;
            _view = wantView;
            return;
        }

        _pivot = Vector3.SmoothDamp(_pivot, want, ref _pivotSpeed, moveTime);

        // 구도도 함께 미끄러진다. 자리만 옮기고 구도가 튀면 계단에서 멀미가 난다.
        _view = Vector4.MoveTowards(_view, wantView,
                                    (wantView - _view).magnitude * Time.deltaTime / Mathf.Max(0.01f, moveTime));
    }

    private Vector3 FindPivot(TaskWorker worker)
    {
        if (mode == CameraMode.FollowPlayer && worker != null)
        {
            return worker.transform.position;
        }

        if (CurrentDeck != null)
        {
            return CurrentDeck.Center;
        }

        // 층이 없는 씬(예전 평면 갑판)에서도 돌아가야 한다.
        return target != null ? target.position : _pivot;
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
