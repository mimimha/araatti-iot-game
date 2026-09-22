using UnityEngine;

namespace ithappy.Cute_Characters.Controller
{
    public abstract class PlayerCamera : MonoBehaviour
    {
        private const float MIN_DISTANCE = 1f;
        private const float MAX_DISTANCE = 10f;

        private const float TARGET_DISTANCE = MAX_DISTANCE * 2f;

        [SerializeField]
        protected Transform m_Player;

        [SerializeField, Range(0f, 1f)]
        private float m_SensitivityX = 0.1f;
        [SerializeField, Range(0f, 1f)]
        private float m_SensitivityY = 0.1f;

        [SerializeField, Range(0f, 1f)]
        private float m_Zoom = 0.5f;
        [SerializeField, Range(0f, 1f)]
        private float m_SensetivityZoom = 0.1f;

        // ⚠ 아라아띠 수정 — 하한을 음수까지 열었다. 이 카메라는 항상 주시점을 LookAt 하므로
        // 하늘을 보려면 카메라가 주시점보다 아래로 내려가야 한다. 원본의 Range(0, 90) 은
        // 인스펙터에서 0 미만을 넣을 수 없어 그 자체로 막혀 있었다.
        //
        // 이 값이 그대로 실제 하한이다. 지면을 뚫는 문제는 각도를 깎아서가 아니라
        // ThirdPersonCamera 가 리그를 통째로 들어올려서 해결한다. (MIN_HEIGHT)
        [SerializeField, Range(-90f, 90f)]
        private float m_MinAngle = 0f;
        [SerializeField, Range(0, 90f)]
        private float m_MaxAngle = 50f;

        protected Transform m_Target;
        protected Transform m_Transform;

        protected Vector2 m_Angles;
        protected float m_Distance;

        public Transform Player => m_Player;
        public Vector3 Target => m_Target.position;
        public float TargetDistance => TARGET_DISTANCE;

        protected virtual void Awake()
        {
            m_Transform = transform;

            m_Target = new GameObject($"Target_{gameObject.name}").transform;
            if(m_Transform.parent != null)
            {
                m_Target.transform.parent = m_Transform.parent;
            }
            
            if (m_Player == null)
            {
                Debug.Log($"Please set the player transform to the camera. GameObject name ({gameObject.name})");
            }
        }

        public virtual void SetInput(in Vector2 delta, float scroll)
        {
            m_Angles += new Vector2(delta.y * m_SensitivityY, delta.x * m_SensitivityX) * 360f;
            m_Angles.x = Mathf.Clamp(m_Angles.x, m_MinAngle, m_MaxAngle);

            m_Zoom += scroll * m_SensetivityZoom;
            m_Zoom = Mathf.Clamp01(m_Zoom);

            m_Distance = (1f - m_Zoom) * (MAX_DISTANCE - MIN_DISTANCE) + MIN_DISTANCE;
        }

        public void BindPlayer(Transform player)
        {
            m_Player = player;
        }
    }
}