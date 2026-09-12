using System.Collections;
using UnityEngine;

namespace Warriors
{
    [RequireComponent(typeof(WarriorsHealth))]
    public sealed class WarriorsPlayerHitFeedback : MonoBehaviour
    {
        [SerializeField] private Transform visualRoot;
        private WarriorsHealth health;
        private Renderer[] renderers;
        private Coroutine routine;
        private float overlayUntil;
        private Vector3 restPosition;
        private Quaternion restRotation;
        private MaterialPropertyBlock flashBlock;

        private void Awake()
        {
            flashBlock = new MaterialPropertyBlock();
            health = GetComponent<WarriorsHealth>();
            renderers = GetComponentsInChildren<Renderer>(true);
            if (visualRoot != null)
            {
                restPosition = visualRoot.localPosition;
                restRotation = visualRoot.localRotation;
            }
        }

        private void OnEnable()
        {
            if (health != null) health.Damaged += Play;
        }

        private void OnDisable()
        {
            if (health != null) health.Damaged -= Play;
            Restore();
        }

        private void Play(int damage)
        {
            if (routine != null) StopCoroutine(routine);
            routine = StartCoroutine(HitRoutine());
            overlayUntil = Time.time + .18f;
            SpawnBurst();
        }

        private IEnumerator HitRoutine()
        {
            SetFlash(true);
            const float duration = .22f;
            for (float elapsed = 0f; elapsed < duration; elapsed += Time.deltaTime)
            {
                float fade = 1f - elapsed / duration;
                if (visualRoot != null)
                {
                    visualRoot.localPosition = restPosition + Vector3.right * Mathf.Sin(elapsed * 85f) * .10f * fade;
                    visualRoot.localRotation = restRotation * Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 70f) * 4f * fade);
                }
                yield return null;
            }
            Restore();
            routine = null;
        }

        private void Restore()
        {
            if (visualRoot != null)
            {
                visualRoot.localPosition = restPosition;
                visualRoot.localRotation = restRotation;
            }
            SetFlash(false);
        }

        private void SetFlash(bool enabled)
        {
            if (renderers == null || flashBlock == null) return;
            flashBlock.Clear();
            if (enabled)
            {
                flashBlock.SetColor("_BaseColor", new Color(1f, .34f, .28f, 1f));
                flashBlock.SetColor("_Color", new Color(1f, .34f, .28f, 1f));
            }
            foreach (Renderer item in renderers) item.SetPropertyBlock(enabled ? flashBlock : null);
        }

        private void SpawnBurst()
        {
            GameObject burst = new("PlayerHitBurst");
            burst.transform.position = transform.position + Vector3.up * 1.05f;
            ParticleSystem particles = burst.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.duration = .12f; main.loop = false; main.startLifetime = .25f;
            main.startSpeed = 3.2f; main.startSize = .12f;
            main.startColor = new Color(1f, .25f, .16f, 1f);
            main.maxParticles = 14; main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 12) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = .16f;
            particles.Play();
        }

        private void OnGUI()
        {
            if (Time.time >= overlayUntil) return;
            float alpha = Mathf.Clamp01((overlayUntil - Time.time) / .18f) * .20f;
            Color previous = GUI.color;
            GUI.color = new Color(1f, .08f, .04f, alpha);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
