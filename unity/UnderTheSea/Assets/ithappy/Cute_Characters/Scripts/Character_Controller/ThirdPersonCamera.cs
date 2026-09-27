using UnityEngine;

namespace ithappy.Cute_Characters.Controller
{
    public class ThirdPersonCamera : PlayerCamera
    {
        /// <summary>
        /// ⚠ 아라아띠 추가 — 카메라가 <b>플레이어 발 높이 기준</b>으로 유지해야 하는 최소 높이(m).
        /// 계산된 자리가 이보다 낮으면 리그를 그만큼 위로 올린다. <see cref="SetInput"/> 참고.
        /// </summary>
        private const float MIN_HEIGHT = 0.3f;

        [SerializeField, Range(0f, 2f)]
        private float m_Offset = 1.5f;
        [SerializeField, Range(0f, 360f)]
        private float m_CameraSpeed = 90f;

        private Vector3 m_LookPoint;
        private Vector3 m_TargetPos;

        /// <summary>
        /// ⚠ 아라아띠 추가 — 주시점 높이(플레이어 발 기준, m). 원래 값은 인스펙터의 m_Offset 이다.
        ///
        /// 로비 물속 연출(<c>LobbyUnderwaterView</c>)이 잠수 중에 낮춘다. 발 위 1.5m 를 보고 있으면
        /// 얕은 바다에서는 카메라가 늘 수면 위에 남아 물속이 보이지 않는다.
        /// </summary>
        public float LookHeight
        {
            get => m_Offset;
            set => m_Offset = value;
        }

        /// <summary>
        /// ⚠ 아라아띠 추가 — 카메라가 올라갈 수 있는 가장 높은 곳(월드 y). null 이면 제한이 없다.
        ///
        /// 잠수 중에 수면 바로 아래로 둔다. 넘으면 <b>거리는 두고 각도를 눕혀</b> 이 높이에 맞춘다.
        /// 기본 거리(5.5m)로 내려다보면 얕은 바다에서도 카메라가 물 밖에 있어 물속이 안 보인다.
        ///
        /// ⚠ 예전에는 각도를 두고 주시점 쪽으로 당겼다. 그러면 주시점이 천장에 가까울수록 거리가 0 에
        ///    가까워져, 헤엄치며 Space 로 떠오르면 카메라가 캐릭터 얼굴까지 확대돼 들어왔다.
        /// </summary>
        public float? CameraCeiling { get; set; }

        /// <summary>천장 아래에서 뒤쪽 바닥 · 바위에 막혀 당길 때 주시점에서 이보다 가까이 오지 않는다. 캐릭터 머리 속으로 들어가지 않게.</summary>
        private const float CEILING_MIN_DISTANCE = 0.8f;

        /// <summary>천장 아래에서 뒤쪽이 막혔을 때 막힌 면에서 이만큼 떨어뜨린다. 딱 붙이면 바닥이 화면을 가른다.</summary>
        private const float CEILING_WALL_MARGIN = 0.2f;

        /// <summary><see cref="ClearDistance"/> 가 쓰는 버퍼. 매 프레임 배열을 만들지 않으려고 둔다.</summary>
        private readonly RaycastHit[] m_CeilingHits = new RaycastHit[8];

        private void LateUpdate()
        {
            // ⚠ 아라아띠 수정 — 따라갈 대상이 없으면 아예 움직이지 않는다.
            //
            // m_TargetPos 는 SetInput() 에서만 계산된다. 대상이 붙기 전에는 초기값 (0,0,0) 이라
            // 이 LateUpdate 가 매 프레임 카메라를 **월드 원점으로** 끌고 간다.
            // Fusion 에서는 캐릭터가 접속 후 서버 스폰으로 생기므로 그 사이가 몇 초씩 된다.
            // 그동안 카메라가 원점(수면 y=0)에 가 있어 접속 직후 물속 시점이 보였다.
            // 실제 로그: 바인딩 시점 카메라 (0.00, 0.00, 0.00), 캐릭터까지 거리 51.49
            if (m_Player == null)
            {
                return;
            }

            Move(Time.deltaTime);
        }

        /// <summary>
        /// ⚠ 아라아띠 추가 — 대상 뒤 제자리로 <b>보간 없이</b> 즉시 이동한다.
        ///
        /// <see cref="Move"/> 는 m_CameraSpeed(기본 90) 로 서서히 다가간다.
        /// 멀리서 시작하면 그 과정이 화면에 "스윽 날아가는" 연출로 보인다.
        /// 로컬 캐릭터가 준비된 첫 순간에는 연출 없이 바로 자리를 잡아야 하므로
        /// 목표를 계산한 뒤 좌표를 직접 넣는다.
        ///
        /// 첫 Snap 이후의 평상시 추적은 그대로 <see cref="Move"/> 가 맡는다.
        /// </summary>
        /// <summary>
        /// ⚠ 아라아띠 추가 — 카메라를 이 좌우 각도(도)에서 보게 한다. 위아래 각도는 그대로 둔다.
        ///
        /// 접속할 때마다 카메라는 늘 북쪽(+Z)을 보고 시작한다. 미니게임에서 돌아와 포탈을 등지고
        /// 선 캐릭터를 엉뚱한 쪽에서 보면 "포탈에서 나왔다" 는 느낌이 안 난다. 캐릭터가 보는
        /// 방향을 넘겨 그 뒤에 서게 한다. 곧바로 <see cref="SnapToPlayer"/> 를 부르면 보간 없이 자리를 잡는다.
        /// </summary>
        public void FaceYaw(float yawDegrees)
        {
            m_Angles.y = yawDegrees;
        }

        public void SnapToPlayer()
        {
            if (m_Player == null)
            {
                return;
            }

            // m_LookPoint 와 m_TargetPos 를 지금 값으로 계산한다. (입력 변화량은 0)
            SetInput(Vector2.zero, 0f);

            m_Transform.position = m_TargetPos;
            m_Transform.LookAt(m_LookPoint);

            if (m_Target != null)
            {
                m_Target.position = m_Transform.position + m_Transform.forward * TargetDistance;
            }
        }

        public override void SetInput(in Vector2 delta, float scroll)
        {
            base.SetInput(delta, scroll);

            var dir = new Vector3(0, 0, -m_Distance);
            var rot = Quaternion.Euler(m_Angles.x, m_Angles.y, 0f);

            var playerPos = (m_Player == null) ? Vector3.zero : m_Player.position;
            m_LookPoint = playerPos + m_Offset * Vector3.up;
            m_TargetPos = m_LookPoint + rot * dir;

            // ⚠ 아라아띠 수정 — 올려다볼 때 카메라가 지면·수면 아래로 내려가는 것을 막는다.
            //
            // <b>각도는 깎지 않는다.</b> 카메라와 주시점을 같은 높이만큼 함께 올린다.
            // 둘의 차(= 시선 벡터)가 그대로라 m_Angles 가 만든 시선 방향이 보존되고,
            // 리그 전체가 평행이동할 뿐이다. 하늘은 원하는 만큼 보이면서 카메라만 발밑에 남는다.
            //
            // 각을 깎는 방식과의 차이: 깎으면 올려다보는 것 자체가 막히지만,
            // 올리면 올려다보되 보는 지점이 플레이어 머리 위로 옮겨간다.
            // 그래서 플레이어는 화면 아래쪽으로 내려간다 — 그것이 올려다보기의 대가다.
            float lift = (playerPos.y + MIN_HEIGHT) - m_TargetPos.y;
            if (lift > 0f)
            {
                m_TargetPos.y += lift;
                m_LookPoint.y += lift;
            }

            // ⚠ 아라아띠 추가 — 천장(CameraCeiling)을 넘으면 거리는 그대로 두고 각도만 눕힌다. 위 설명 참고.
            //
            // 끝이 천장 높이에 오도록 팔을 뒤쪽으로 눕힌다. 높이 차(rise)는 팔 높이(arm.y)보다 작으므로
            // (카메라는 천장 위, 주시점은 아래) 제곱근 안이 음수가 되지 않는다.
            // 뒤쪽은 좌우 각도만으로 정한다. 위아래 각도를 90도까지 올려도 방향이 사라지지 않는다.
            if (CameraCeiling is float ceiling && m_TargetPos.y > ceiling && m_LookPoint.y < ceiling)
            {
                float length = (m_TargetPos - m_LookPoint).magnitude;
                float rise = ceiling - m_LookPoint.y;
                float run = Mathf.Sqrt(length * length - rise * rise);

                Vector3 back = Quaternion.Euler(0f, m_Angles.y, 0f) * Vector3.back;
                Vector3 slope = (back * run + Vector3.up * rise) / length;

                // 눕힌 팔은 바닥을 스치듯 지난다. 뒤쪽 해저가 얕으면 카메라가 땅속으로 들어가므로 거기서 멈춘다.
                m_TargetPos = m_LookPoint + slope * ClearDistance(m_LookPoint, slope, length);
            }
        }

        /// <summary>
        /// ⚠ 아라아띠 추가 — 주시점에서 <paramref name="dir"/> 쪽으로 막힘 없이 갈 수 있는 거리.
        /// 캐릭터(자기 · 남의 캡슐)에는 막히지 않는다. 옆을 헤엄쳐 지나갈 때마다 확대되면 안 된다.
        /// </summary>
        private float ClearDistance(Vector3 from, Vector3 dir, float length)
        {
            int count = Physics.RaycastNonAlloc(from, dir, m_CeilingHits, length, ~0, QueryTriggerInteraction.Ignore);
            float clear = length;

            for (int i = 0; i < count; i++)
            {
                if (m_CeilingHits[i].collider is CharacterController)
                {
                    continue;
                }

                clear = Mathf.Min(clear, m_CeilingHits[i].distance - CEILING_WALL_MARGIN);
            }

            return Mathf.Max(clear, CEILING_MIN_DISTANCE);
        }

        private void Move(float deltaTime)
        {
            camera();
            target();

            void camera()
            {
                var direction = m_TargetPos - m_Transform.position;
                var delta = m_CameraSpeed * deltaTime;

                if(delta * delta > direction.sqrMagnitude)
                {
                    m_Transform.position = m_TargetPos;
                }
                else
                {
                    m_Transform.position += delta * direction.normalized;
                }

                m_Transform.LookAt(m_LookPoint);
            }

            void target()
            {
                if(m_Target == null)
                {
                    return;
                }

                m_Target.position = m_Transform.position + m_Transform.forward * TargetDistance;
            }
        }
    }
}