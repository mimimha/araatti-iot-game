using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Warriors
{
    public sealed class WarriorsTargetFeedback : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        [SerializeField] private Text feedbackText;
        private Vector3 restPosition;
        private Vector3 restScale;
        private Quaternion restRotation;
        private Coroutine routine;
        private float rejectedUntil;
        private bool defeating;
        private Renderer[] renderers;
        private MaterialPropertyBlock flashBlock;

        private void Awake()
        {
            flashBlock = new MaterialPropertyBlock();
            if (visualRoot == null) visualRoot = transform;
            restPosition = visualRoot.localPosition;
            restScale = visualRoot.localScale;
            restRotation = visualRoot.localRotation;
            renderers = visualRoot.GetComponentsInChildren<Renderer>(true);
            if (feedbackText == null) feedbackText = GetComponentInChildren<Text>(true);
            foreach (Text label in GetComponentsInChildren<Text>(true)) label.gameObject.SetActive(false);
        }

        public void PlayHit()
        {
            if (defeating) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Shake());
        }

        public void PlayDefeat()
        {
            defeating = true;
            rejectedUntil = 0f;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(DefeatAnimation());
        }

        /// <summary>
        /// A swing that does not match this monster's weakness.
        /// Every monster caught in the arc used to shout "WRONG" over its head, so a clean
        /// kill on one type spammed the screen with warnings from its neighbours. The
        /// rejection still registers for the IoT feedback hub; it just no longer draws text.
        /// </summary>
        public void PlayRejected()
        {
            rejectedUntil = Time.time + .35f;
        }

        private IEnumerator Shake()
        {
            for (float t = 0f; t < .28f; t += Time.deltaTime)
            {
                visualRoot.localPosition = restPosition + Vector3.right * Mathf.Sin(t * 55f) * .18f;
                yield return null;
            }
            visualRoot.localPosition = restPosition;
            routine = null;
        }

        private IEnumerator DefeatAnimation()
        {
            SetFlash(true);
            for (float t = 0f; t < .55f; t += Time.deltaTime)
            {
                float normalized = t / .55f;
                float impact = 1f - Mathf.Clamp01(normalized / .18f);
                float punch = 1f + Mathf.Sin(Mathf.Min(normalized * 2f, 1f) * Mathf.PI) * .28f;
                visualRoot.localScale = Vector3.Lerp(restScale * punch, restScale * .08f, Mathf.InverseLerp(.28f, 1f, normalized));
                visualRoot.localRotation = restRotation * Quaternion.Euler(0f, normalized * 120f, Mathf.Sin(normalized * 18f) * 8f);
                visualRoot.localPosition = restPosition + Vector3.back * (.28f * impact)
                    + Vector3.right * Mathf.Sin(t * 75f) * (.14f * impact)
                    + Vector3.up * normalized * .45f;
                if (normalized > .16f) SetFlash(false);
                yield return null;
            }
            SetFlash(false);
            routine = null;
        }

        private void SetFlash(bool enabled)
        {
            if (renderers == null || flashBlock == null) return;
            flashBlock.Clear();
            if (enabled)
            {
                flashBlock.SetColor("_BaseColor", new Color(1f, .62f, .28f, 1f));
                flashBlock.SetColor("_Color", new Color(1f, .62f, .28f, 1f));
            }
            foreach (Renderer item in renderers) item.SetPropertyBlock(enabled ? flashBlock : null);
        }

        private void OnDisable()
        {
            rejectedUntil = 0f;
            defeating = false;
            SetFlash(false);
            if (visualRoot != null)
            {
                visualRoot.localPosition = restPosition;
                visualRoot.localScale = restScale;
                visualRoot.localRotation = restRotation;
            }
        }
    }
}
