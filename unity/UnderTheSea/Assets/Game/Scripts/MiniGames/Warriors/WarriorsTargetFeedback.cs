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
        private bool defeating;
        private bool bossPart;
        private Renderer[] renderers;
        private MaterialPropertyBlock flashBlock;
        private bool initialized;

        private void Awake() => EnsureInitialized();

        private void EnsureInitialized()
        {
            if (initialized) return;
            initialized = true;
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
            EnsureInitialized();
            if (defeating || bossPart) return;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(Shake());
        }

        public void PlayDefeat()
        {
            EnsureInitialized();
            if (bossPart) return;
            defeating = true;
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(DefeatAnimation());
        }

        public void ConfigureAsBossPart()
        {
            EnsureInitialized();
            bossPart = true;
            if (routine != null) StopCoroutine(routine);
            routine = null;
            defeating = false;
            visualRoot.localPosition = restPosition;
            visualRoot.localScale = restScale;
            visualRoot.localRotation = restRotation;
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
            // Short on purpose: the death has to read as part of the swing, not as an
            // animation the player waits through.
            const float duration = .35f;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float normalized = t / duration;
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
            // The corpse used to sit at 8% scale until the delayed Destroy caught up, which
            // read as the monster hanging around after it was killed. Hide it the moment the
            // animation is over so the kill lands exactly when it looks like it does.
            if (renderers != null)
                foreach (Renderer renderer in renderers)
                    if (renderer != null) renderer.enabled = false;
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
            if (!initialized) return;
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
