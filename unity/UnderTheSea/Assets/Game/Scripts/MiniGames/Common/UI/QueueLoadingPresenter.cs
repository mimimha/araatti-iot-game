using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 서버의 최초 대기열 패킷을 기다리는 동안 보여 주는 전체 화면.
    /// 네트워크를 직접 알지 않고, 표시와 조타 회전만 담당한다.
    /// </summary>
    public sealed class QueueLoadingPresenter : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform wheel;
        [SerializeField] private Image background;
        [SerializeField] private CanvasGroup wheelCanvasGroup;
        [SerializeField] private Shader circularRevealShader;
        [SerializeField, Min(1f)] private float rotationDegreesPerSecond = 72f;
        [SerializeField, Min(0f)] private float circularRevealSeconds = .85f;

        private static readonly int RadiusId = Shader.PropertyToID("_Radius");
        private static readonly int AspectId = Shader.PropertyToID("_Aspect");
        private Material revealMaterial;

        public bool IsShown => root != null && root.activeSelf;
        public float VisibleSeconds { get; private set; }

        /// <summary>
        /// 한 프레임에 인정하는 최대 시간(초). 씬 로드나 컴파일 직후처럼 한 프레임이 몇 초씩 걸리면
        /// 그 시간을 그대로 더하면 "최소 5초" 가 한 번에 건너뛰어지고 조타도 툭 튀어 버린다.
        /// 사람 눈에 보인 시간만 세도록 큼직한 프레임은 이만큼으로 잘라 넣는다.
        /// </summary>
        private const float MaxFrameSeconds = .1f;

        private void Update()
        {
            if (!IsShown) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, MaxFrameSeconds);
            VisibleSeconds += dt;
            if (wheel != null)
                wheel.Rotate(0f, 0f, -rotationDegreesPerSecond * dt);
        }

        public void Show()
        {
            VisibleSeconds = 0f;
            if (wheel != null) wheel.localRotation = Quaternion.identity;
            if (wheelCanvasGroup != null) wheelCanvasGroup.alpha = 1f;
            PrepareRevealMaterial(0f);
            if (root != null)
            {
                root.SetActive(true);
                root.transform.SetAsLastSibling();
            }
        }

        /// <summary>대기 배경의 가운데를 원형으로 넓혀 뒤쪽 매칭 화면을 드러낸다.</summary>
        public IEnumerator RevealFromCentre()
        {
            float aspect = GetAspect();
            float finalRadius = Mathf.Sqrt(aspect * aspect * .25f + .25f) + .05f;

            if (revealMaterial == null || circularRevealSeconds <= 0f)
            {
                Hide();
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < circularRevealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / circularRevealSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                revealMaterial.SetFloat(RadiusId, finalRadius * eased);
                if (wheelCanvasGroup != null) wheelCanvasGroup.alpha = 1f - t;
                yield return null;
            }

            Hide();
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void PrepareRevealMaterial(float radius)
        {
            if (background == null || circularRevealShader == null) return;

            if (revealMaterial == null)
            {
                revealMaterial = new Material(circularRevealShader)
                {
                    name = "Queue Circular Reveal (Runtime)"
                };
            }

            revealMaterial.SetFloat(AspectId, GetAspect());
            revealMaterial.SetFloat(RadiusId, radius);
            background.material = revealMaterial;
        }

        private float GetAspect()
        {
            if (background == null) return 16f / 9f;
            Rect rect = background.rectTransform.rect;
            return rect.height > 0f ? rect.width / rect.height : 16f / 9f;
        }

        private void OnDestroy()
        {
            if (revealMaterial != null) Destroy(revealMaterial);
        }
    }
}
