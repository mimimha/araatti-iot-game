using System.Collections.Generic;
using FishingMiniGame.Core;
using UnityEngine;

namespace FishingMiniGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FishVisualAdapter : MonoBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private Transform tail;
        [SerializeField] private Transform hookAnchor;

        private readonly HashSet<int> _parameters = new HashSet<int>();
        private MaterialPropertyBlock _propertyBlock;
        private Renderer[] _renderers;
        private Quaternion _tailBaseRotation;
        private bool _bound;

        public bool HasHookAnchor => hookAnchor != null;
        public Vector3 HookAnchorPosition => hookAnchor != null ? hookAnchor.position : transform.position;

        public void Configure(Animator value, Transform tailTransform, Transform catchAnchor)
        {
            animator = value;
            tail = tailTransform;
            hookAnchor = catchAnchor;
            _bound = false;
            _parameters.Clear();
        }

        public void AutoBind()
        {
            if (_bound) return;
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (animator != null)
            {
                AnimatorControllerParameter[] animatorParameters = animator.parameters;
                for (int i = 0; i < animatorParameters.Length; i++)
                {
                    _parameters.Add(animatorParameters[i].nameHash);
                }
            }

            if (tail == null) tail = FindTail(transform);
            if (hookAnchor == null) hookAnchor = FindNamedTransform(transform, "HookAnchor");
            if (tail != null) _tailBaseRotation = tail.localRotation;
            _renderers = GetComponentsInChildren<Renderer>(true);
            _propertyBlock = new MaterialPropertyBlock();
            _bound = true;
        }

        public void ApplyTint(Color tint)
        {
            AutoBind();
            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                Material[] materials = renderer.sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++)
                {
                    Material source = materials[materialIndex];
                    Color sourceColor = Color.white;
                    if (source != null && source.HasProperty("_BaseColor")) sourceColor = source.GetColor("_BaseColor");
                    else if (source != null && source.HasProperty("_Color")) sourceColor = source.GetColor("_Color");
                    Color finalColor = new Color(
                        sourceColor.r * tint.r,
                        sourceColor.g * tint.g,
                        sourceColor.b * tint.b,
                        sourceColor.a * tint.a);
                    _propertyBlock.Clear();
                    _propertyBlock.SetColor("_BaseColor", finalColor);
                    _propertyBlock.SetColor("_Color", finalColor);
                    renderer.SetPropertyBlock(_propertyBlock, materialIndex);
                }
            }
        }

        public void Apply(
            FishingSnapshot snapshot,
            float feedbackIntensity,
            float animationPhaseSeconds,
            bool paused,
            float headShakeAccent)
        {
            if (snapshot == null) return;
            AutoBind();
            float swimSpeed = snapshot.State == FishingPlayerState.Fighting
                ? 1.3f + feedbackIntensity * 1.8f
                : 0.8f;
            SetFloat("SwimSpeed", swimSpeed);
            SetFloat("FightIntensity", feedbackIntensity);

            if (animator != null) animator.speed = paused ? 0f : swimSpeed;
            if (tail != null && animator == null)
            {
                float swing = Mathf.Sin(animationPhaseSeconds * (8f + swimSpeed * 2f)) *
                    (18f + feedbackIntensity * 12f + headShakeAccent * 18f);
                tail.localRotation = _tailBaseRotation * Quaternion.Euler(0f, swing, 0f);
            }
        }

        private void SetFloat(string parameter, float value)
        {
            int hash = Animator.StringToHash(parameter);
            if (animator != null && _parameters.Contains(hash)) animator.SetFloat(hash, value);
        }

        private static Transform FindTail(Transform root)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                string lower = transforms[i].name.ToLowerInvariant();
                if (lower.Contains("tail")) return transforms[i];
            }
            return null;
        }

        private static Transform FindNamedTransform(Transform root, string targetName)
        {
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                if (string.Equals(transforms[i].name, targetName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return transforms[i];
                }
            }
            return null;
        }
    }
}
