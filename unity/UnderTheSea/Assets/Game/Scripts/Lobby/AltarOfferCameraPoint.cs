using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **제단 봉헌 연출에서 카메라가 설 자리.** 이 오브젝트의 위치 · 방향 그대로 카메라를 놓는다.
    ///
    /// <c>P_HeartAltar</c> 프리팹 안에 있다. 사람이 씬 뷰에서 각도를 골라 저장한다.
    ///
    /// <code>
    ///   Tools/아라아띠/제단 봉헌 카메라 고르기    지금 자리로 씬 뷰를 옮긴다
    ///   (씬 뷰를 돌려 각도를 잡는다)
    ///   Inspector [지금 씬 뷰 시점을 저장]       이 자리를 씬 뷰 카메라에 맞추고 프리팹에 저장한다
    /// </code>
    ///
    /// 선택하면 씬 뷰에 카메라가 보는 범위(사각뿔)가 그려진다.
    ///
    /// <b>화각도 이 자리의 일부다.</b> 연출하는 동안 게임 카메라 화각을 <see cref="fieldOfView"/> 로 바꿨다가 끝나면 되돌린다.
    /// 로비 게임 카메라는 34° 라 씬 뷰(보통 60°)와 크게 달라서, 위치만 맞추면 게임에서는 확대된 것처럼 보였다
    /// (첫 QA 에서 겪음). 씬 뷰에서 본 화면이 그대로 나오게 화각을 같이 저장한다.
    ///
    /// 없으면 연출이 예전처럼 계산한 자리를 쓴다(<see cref="AltarOfferCinematic"/>).
    /// </summary>
    public sealed class AltarOfferCameraPoint : MonoBehaviour
    {
        [Tooltip("연출 중 게임 카메라의 세로 화각(°). 씬 뷰에서 저장하면 그때의 씬 뷰 화각이 들어간다. 로비 기본 카메라는 34°.")]
        [SerializeField, Range(20f, 100f)] private float fieldOfView = 60f;

        /// <summary>지금 로비에 있는 카메라 자리. 없으면 null.</summary>
        public static AltarOfferCameraPoint Current { get; private set; }

        public float FieldOfView => fieldOfView;

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (ReferenceEquals(Current, this))
            {
                Current = null;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.74f, 0.12f, 0.9f);
            Gizmos.DrawWireSphere(transform.position, 0.6f);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.74f, 0.12f, 1f);
            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.DrawFrustum(Vector3.zero, fieldOfView, 40f, 0.5f, 16f / 9f);
            Gizmos.matrix = previous;
        }
#endif
    }
}
