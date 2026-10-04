using System;
using System.Collections.Generic;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.InputSystem;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **이정표. 가까이 가서 누르면 다른 이정표로 갈 수 있는 목록이 뜬다.**
    ///
    /// <code>
    ///   가까이 가면   안내가 뜬다
    ///   F 를 누르면   이동할 곳을 고르는 창이 열린다
    ///   고르면        그 이정표 앞으로 옮겨진다
    /// </code>
    ///
    /// <b>미니게임 입구와 무엇이 다른가.</b> <c>MiniGamePortal</c> 은 <b>들어가면 게임이
    /// 시작되는</b> 곳이고, 이정표는 <b>로비 안에서 자리를 옮기는</b> 곳이다. 로비가 넓어져
    /// 미니게임들이 양 끝에 있다 보니 걸어 다니는 시간이 길어서 둔 장치다.
    ///
    /// <b>구조는 <c>MiniGamePortal</c> 을 그대로 따른다.</b> 거리 판정도, 입력 키도,
    /// 채팅 중 잠금도 같다. 플레이어가 규칙을 두 번 배우지 않아도 되게 하려는 것이다.
    /// 연출만 다르다 — 소용돌이 대신 <see cref="SignpostBeacon"/> 의 화살표를 쓴다.
    ///
    /// ⚠ <b>이동은 이 부품이 하지 않는다.</b> 어디로 갈지 고르는 것은 창이 하고, 실제로
    ///    옮기는 것은 <see cref="Registry"/> 에 등록된 다른 이정표의 <see cref="ArrivalPoint"/>
    ///    를 받아 캐릭터를 옮기는 쪽이 한다. 여기는 <b>창을 여는 것까지</b>만 한다.
    ///
    /// ⚠ <b>네트워크 이동이 아니다.</b> 지금은 내 캐릭터의 위치를 바꾸는 것뿐이다.
    ///    Fusion 이 상태 권한을 서버에 두고 있으므로, 서버가 인정하지 않으면 원래 자리로
    ///    되돌아간다. 제대로 하려면 서버에 "여기로 옮겨 달라" 를 요청해야 한다.
    ///    그 부분은 창을 붙일 때 같이 정한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SignpostTeleport : MonoBehaviour
    {
        /// <summary>지금 로비에 놓여 있는 이정표들. 창이 목록을 만들 때 쓴다.</summary>
        private static readonly List<SignpostTeleport> All = new List<SignpostTeleport>();

        /// <summary>놓여 있는 이정표 전부. 순서는 씬에 놓인 순서다.</summary>
        public static IReadOnlyList<SignpostTeleport> Registry => All;

        /// <summary>누군가 이정표를 눌렀다. 창을 띄우는 쪽이 받는다.</summary>
        public static event Action<SignpostTeleport> Requested;

        [Header("이 이정표")]
        [Tooltip("창의 목록에 보일 이름. 예: 광산 앞, 검투장 앞")]
        [SerializeField] private string displayName = "이정표";

        [Tooltip("여기로 이동해 왔을 때 설 자리. 비워 두면 이정표 자신의 위치를 쓴다.")]
        [SerializeField] private Transform arrivalPoint;

        [Tooltip("켜면 여기 도착했을 때 캐릭터가 아래 각도를 바라보고, 카메라는 그 등 뒤에 선다. " +
                 "끄면 예전처럼 원래 보던 방향 그대로 도착한다.")]
        [SerializeField] private bool faceOnArrival;

        [Tooltip("도착했을 때 바라볼 좌우 각도(도). 0 = 북쪽(+Z), 90 = 동쪽(+X). " +
                 "씬에서 이정표를 고르면 노란 화살표로 보인다.")]
        [SerializeField, Range(0f, 360f)] private float arrivalYaw;

        [Header("상호작용")]
        [Tooltip("이 거리 안에 들어와야 누를 수 있다(m).")]
        [SerializeField, Min(0.5f)] private float interactDistance = 4f;

        [Tooltip("높이 차이를 무시할지. 언덕 위아래로 겹칠 일이 없으면 켜 둔다.")]
        [SerializeField] private bool ignoreHeight = true;

        [Tooltip("누를 키. 미니게임 입구와 같은 F 로 맞춰 두었다.")]
        [SerializeField] private Key interactKey = Key.F;

        [Header("안내")]
        [Tooltip("가까이 갔을 때 켤 것. 비워 둬도 동작한다.")]
        [SerializeField] private GameObject promptRoot;

        private bool shownNear;

        /// <summary>창의 목록에 보일 이름.</summary>
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;

        /// <summary>여기로 이동해 왔을 때 설 자리.</summary>
        public Vector3 ArrivalPoint =>
            arrivalPoint != null ? arrivalPoint.position : transform.position;

        /// <summary>
        /// 도착했을 때 바라볼 각도. 정해 두지 않은 이정표면 false — 그때는 방향을 건드리지 않는다.
        ///
        /// 서버는 캐릭터를 이 각도로 돌리고(<c>NetworkPlayerMover.RpcRequestTeleport</c>),
        /// 클라이언트는 카메라를 그 등 뒤에 세운다(<see cref="SignpostTeleportUI"/>). 둘 다 같은 씬 값을 읽는다.
        /// </summary>
        public bool TryGetArrivalYaw(out float yaw)
        {
            yaw = arrivalYaw;
            return faceOnArrival;
        }

        /// <summary>지금 누를 수 있는가.</summary>
        public bool PlayerIsNear { get; private set; }

        /// <summary>
        /// **이 좌표가 실제로 어느 이정표 앞인가.** 서버가 이동 요청을 검증할 때 쓴다.
        ///
        /// 클라이언트가 보낸 좌표를 그대로 믿으면 어디로든 갈 수 있다. 서버도 같은 Lobby
        /// 씬을 들고 있으므로 놓여 있는 이정표를 안다. <b>그 중 하나의 도착 지점과
        /// 맞아떨어질 때만</b> 보내 준다.
        ///
        /// <paramref name="slack"/> 만큼은 봐준다. 부동소수 오차와, 도착 지점을 살짝
        /// 옮겨 놓았을 수 있는 여지다.
        /// </summary>
        public static bool IsKnownArrival(Vector3 point, float slack, out SignpostTeleport which)
        {
            foreach (SignpostTeleport post in All)
            {
                if (post == null) continue;

                if ((post.ArrivalPoint - point).sqrMagnitude <= slack * slack)
                {
                    which = post;
                    return true;
                }
            }

            which = null;
            return false;
        }

        private void OnEnable()
        {
            if (!All.Contains(this))
            {
                All.Add(this);
            }
        }

        private void OnDisable()
        {
            All.Remove(this);
            ShowPrompt(false);
        }

        /// <summary>
        /// 서버 프로세스인가. 서버는 창을 열 일이 없다 — 캐릭터도 키보드도 없다.
        ///
        /// ⚠ <b>그렇다고 <c>enabled = false</c> 로 끄면 안 된다.</b> 끄면 <c>OnEnable</c> 이
        ///    돌지 않아 <see cref="Registry"/> 가 서버에서 비어 버린다. 그러면 서버가
        ///    "이 좌표가 진짜 이정표 앞인가" 를 확인할 수 없어, 클라이언트가 보낸 좌표를
        ///    그대로 믿고 아무 데나 보내 주게 된다.
        /// </summary>
        private static bool IsServer => FusionLaunchArguments.IsDedicatedServerProcess();

        private void Awake()
        {
            ShowPrompt(false);
        }

        private void Update()
        {
            if (IsServer)
            {
                return;
            }

            PlayerIsNear = ResolveDistance(out float distance) && distance <= interactDistance;

            if (PlayerIsNear != shownNear)
            {
                shownNear = PlayerIsNear;
                ShowPrompt(PlayerIsNear);
            }

            if (!PlayerIsNear)
            {
                return;
            }

            // 채팅을 치는 중이면 F 가 글자다. 미니게임 입구와 같은 잠금을 본다.
            if (ChatFocus.Typing)
            {
                return;
            }

            Keyboard keys = Keyboard.current;
            if (keys == null || !keys[interactKey].wasPressedThisFrame)
            {
                return;
            }

            // 창을 띄우는 쪽이 아직 없으면 아무 일도 일어나지 않는다. 그게 맞다 —
            // 여기서 직접 옮겨 버리면 고를 기회가 없다.
            if (Requested == null)
            {
                Debug.LogWarning(
                    $"[이정표] '{DisplayName}' 을 눌렀지만 이동 창이 없습니다. " +
                    "SignpostTeleport.Requested 를 받는 화면을 붙여 주세요.", this);
                return;
            }

            Requested.Invoke(this);
        }

        /// <summary>
        /// 키가 아니라 <b>밖에서</b> 누르라고 할 때. 완드가 이 길로 들어온다.
        ///
        /// 위 <c>Update</c> 의 F 와 <b>같은 조건을 그대로 본다.</b> 가까이 있어야 하고 채팅을 치는
        /// 중이면 안 된다. <c>MiniGamePortal.TryEnterFromDevice</c> 와 같은 모양이다.
        ///
        /// <c>Requested</c> 는 이벤트라 밖에서 <c>Invoke</c> 할 수 없어서 대신 불러 주는 자리를 연다.
        /// 완드는 <c>IPlayerController</c> 로만 들어오고 Input System 을 거치지 않아 키를 흉내낼 수 없다.
        /// (IOT_INPUT.md 7장 "키 이벤트를 만들어 보내기" 금지)
        /// </summary>
        /// <returns>실제로 창을 열라고 알렸으면 true.</returns>
        public bool TryInteractFromDevice()
        {
            if (IsServer || !PlayerIsNear || ChatFocus.Typing || Requested == null)
            {
                return false;
            }

            Requested.Invoke(this);
            return true;
        }

        /// <summary>
        /// 내 캐릭터까지의 거리.
        ///
        /// ⚠ <b>미니게임 입구와 같은 등록소를 쓴다.</b> 로비의 내 캐릭터는 네트워크가
        ///    스폰하므로 씬에 미리 꽂아 둘 수 없다. 한쪽만 다른 방법으로 찾으면 두 부품이
        ///    서로 다른 것을 "나" 로 여기게 된다.
        /// </summary>
        private bool ResolveDistance(out float distance)
        {
            distance = float.MaxValue;

            Transform me = LocalPlayer.Transform;
            if (me == null)
            {
                return false;
            }

            Vector3 delta = me.position - transform.position;
            if (ignoreHeight)
            {
                delta.y = 0f;
            }

            distance = delta.magnitude;
            return true;
        }

        private void ShowPrompt(bool on)
        {
            if (promptRoot != null && promptRoot.activeSelf != on)
            {
                promptRoot.SetActive(on);
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, interactDistance);

            if (arrivalPoint != null)
            {
                Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.9f);
                Gizmos.DrawWireSphere(arrivalPoint.position, 0.4f);
                Gizmos.DrawLine(transform.position, arrivalPoint.position);
            }

            // 도착해서 바라볼 쪽. 캐릭터는 이 화살표 쪽을 보고, 카메라는 화살표 꼬리 뒤에 선다.
            if (faceOnArrival)
            {
                Vector3 from = ArrivalPoint + Vector3.up * 1.5f;
                Vector3 dir = Quaternion.Euler(0f, arrivalYaw, 0f) * Vector3.forward;
                Vector3 tip = from + dir * 4f;
                Vector3 side = Vector3.Cross(Vector3.up, dir) * 0.6f;

                Gizmos.color = Color.yellow;
                Gizmos.DrawLine(from, tip);
                Gizmos.DrawLine(tip, tip - dir * 1f + side);
                Gizmos.DrawLine(tip, tip - dir * 1f - side);
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// 씬 뷰가 지금 보고 있는 좌우 방향을 도착 방향으로 넣는다. 카메라 고르기와 같은 요령이다 —
        /// 씬 뷰를 도착했을 때 보이고 싶은 쪽으로 돌려 놓고 이 메뉴를 누른다.
        /// </summary>
        [ContextMenu("도착 방향을 지금 씬 뷰가 보는 쪽으로")]
        private void FaceSceneView()
        {
            UnityEditor.SceneView view = UnityEditor.SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                Debug.LogWarning("[이정표] 열려 있는 씬 뷰가 없습니다.", this);
                return;
            }

            Vector3 forward = view.camera.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                Debug.LogWarning("[이정표] 씬 뷰가 바로 위나 아래를 보고 있어 좌우 방향을 알 수 없습니다. 비스듬히 돌려 주세요.", this);
                return;
            }

            UnityEditor.Undo.RecordObject(this, "이정표 도착 방향");
            faceOnArrival = true;
            arrivalYaw = Mathf.Repeat(Quaternion.LookRotation(forward).eulerAngles.y, 360f);
            UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            UnityEditor.EditorUtility.SetDirty(this);
            Debug.Log($"[이정표] '{DisplayName}' 도착 방향 {arrivalYaw:0}° 로 정했습니다. 씬을 저장해 주세요.", this);
        }
#endif
    }
}
