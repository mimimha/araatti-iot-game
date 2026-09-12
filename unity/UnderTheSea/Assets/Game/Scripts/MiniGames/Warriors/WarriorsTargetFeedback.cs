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

        private void OnGUI()
        {
            if (Time.time >= rejectedUntil || Camera.main == null) return;
            Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
            if (screen.z <= 0f) return;
            GUIStyle style = new(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            style.normal.textColor = new Color(1f, .35f, .35f);
            GUI.Label(new Rect(screen.x - 45f, Screen.height - screen.y - 14f, 90f, 28f), "WRONG", style);
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
