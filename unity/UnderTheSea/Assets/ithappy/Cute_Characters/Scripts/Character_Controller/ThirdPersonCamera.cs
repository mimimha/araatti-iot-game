using UnityEngine;

namespace ithappy.Cute_Characters.Controller
{
    public class ThirdPersonCamera : PlayerCamera
    {
        [SerializeField, Range(0f, 2f)]
        private float m_Offset = 1.5f;
        [SerializeField, Range(0f, 360f)]
        private float m_CameraSpeed = 90f;

        private Vector3 m_LookPoint;
        private Vector3 m_TargetPos;

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