using UnityEngine;

namespace Warriors
{
    /// <summary>
    /// Visual-only secondary motion for the lightweight monster prefabs.
    /// It deliberately animates ModelFacing below the gameplay visual root so
    /// hit/defeat feedback, colliders and movement remain independent.
    /// </summary>
    public sealed class WarriorsMonsterVisualAnimator : MonoBehaviour
    {
        private enum MonsterKind { Crab, Fish, Jellyfish }

        [SerializeField] private MonsterKind kind;
        [SerializeField, Min(0.1f)] private float motionSpeed = 4f;
        [SerializeField, Range(0f, 0.2f)] private float bobHeight = 0.06f;

        private Transform model;
        private Transform[] legs = System.Array.Empty<Transform>();
        private Transform[] claws = System.Array.Empty<Transform>();
        private Transform[] fins = System.Array.Empty<Transform>();
        private Transform[] tentacles = System.Array.Empty<Transform>();
        private Vector3 modelPosition;
        private Vector3 modelScale;
        private Quaternion modelRotation;
        private Quaternion[] legRotations = System.Array.Empty<Quaternion>();
        private Quaternion[] clawRotations = System.Array.Empty<Quaternion>();
        private Quaternion[] finRotations = System.Array.Empty<Quaternion>();
        private Quaternion[] tentacleRotations = System.Array.Empty<Quaternion>();
        private float phase;

        private void Awake()
        {
            model = transform.Find("ModelFacing");
            if (model == null) model = transform;

            modelPosition = model.localPosition;
            modelScale = model.localScale;
            modelRotation = model.localRotation;
            phase = Mathf.Abs(GetEntityId().GetHashCode() * 0.173f) % 6.283f;

            legs = FindChildren("Leg");
            claws = FindChildren("Claw");
            fins = FindChildren("Fin", "Tail");
            tentacles = FindChildren("Tentacle");
            legRotations = CaptureRotations(legs);
            clawRotations = CaptureRotations(claws);
            finRotations = CaptureRotations(fins);
            tentacleRotations = CaptureRotations(tentacles);
        }

        private void LateUpdate()
        {
            float t = Time.time * motionSpeed + phase;
            switch (kind)
            {
                case MonsterKind.Crab:
                    AnimateCrab(t);
                    break;
                case MonsterKind.Fish:
                    AnimateFish(t);
                    break;
                case MonsterKind.Jellyfish:
                    AnimateJellyfish(t);
                    break;
            }
        }

        private void AnimateCrab(float t)
        {
            model.localPosition = modelPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(t * 0.5f)) * bobHeight);
            model.localRotation = modelRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.25f) * 2f, Mathf.Sin(t * 0.5f) * 1.5f);
            for (int i = 0; i < legs.Length; i++)
            {
                float step = Mathf.Sin(t + i * Mathf.PI * 0.72f) * 15f;
                legs[i].localRotation = legRotations[i] * Quaternion.Euler(0f, 0f, step);
            }
            for (int i = 0; i < claws.Length; i++)
            {
                float lift = 5f + Mathf.Sin(t * 0.65f + i * Mathf.PI) * 7f;
                claws[i].localRotation = clawRotations[i] * Quaternion.Euler(0f, 0f, i == 0 ? -lift : lift);
            }
        }

        private void AnimateFish(float t)
        {
            model.localPosition = modelPosition + Vector3.up * (Mathf.Sin(t * 0.65f) * bobHeight * 1.4f);
            model.localRotation = modelRotation * Quaternion.Euler(Mathf.Sin(t * 0.5f) * 2f, Mathf.Sin(t * 0.35f) * 4f, Mathf.Sin(t * 0.7f) * 3f);
            for (int i = 0; i < fins.Length; i++)
            {
                float flap = Mathf.Sin(t * 1.35f + i * 1.4f) * (fins[i].name.StartsWith("Tail") ? 18f : 11f);
                fins[i].localRotation = finRotations[i] * Quaternion.Euler(0f, flap, 0f);
            }
        }

        private void AnimateJellyfish(float t)
        {
            float pulse = Mathf.Sin(t * 0.7f);
            model.localPosition = modelPosition + Vector3.up * (pulse * bobHeight * 1.8f);
            model.localScale = Vector3.Scale(modelScale, new Vector3(1f - pulse * 0.025f, 1f + pulse * 0.055f, 1f - pulse * 0.025f));
            model.localRotation = modelRotation * Quaternion.Euler(0f, Mathf.Sin(t * 0.22f) * 3f, Mathf.Sin(t * 0.42f) * 2f);
            for (int i = 0; i < tentacles.Length; i++)
            {
                float sway = Mathf.Sin(t + i * 0.85f) * 12f;
                tentacles[i].localRotation = tentacleRotations[i] * Quaternion.Euler(sway * 0.25f, 0f, sway);
            }
        }

        private Transform[] FindChildren(params string[] prefixes)
        {
            var all = model.GetComponentsInChildren<Transform>(true);
            return System.Array.FindAll(all, candidate =>
            {
                foreach (string prefix in prefixes)
                    if (candidate.name.StartsWith(prefix, System.StringComparison.Ordinal)) return true;
                return false;
            });
        }

        private static Quaternion[] CaptureRotations(Transform[] targets)
        {
            var rotations = new Quaternion[targets.Length];
            for (int i = 0; i < targets.Length; i++) rotations[i] = targets[i].localRotation;
            return rotations;
        }

        private void OnDisable()
        {
            if (model == null) return;
            model.localPosition = modelPosition;
            model.localScale = modelScale;
            model.localRotation = modelRotation;
            RestoreRotations(legs, legRotations);
            RestoreRotations(claws, clawRotations);
            RestoreRotations(fins, finRotations);
            RestoreRotations(tentacles, tentacleRotations);
        }

        private static void RestoreRotations(Transform[] targets, Quaternion[] rotations)
        {
            for (int i = 0; i < targets.Length && i < rotations.Length; i++)
                if (targets[i] != null) targets[i].localRotation = rotations[i];
        }
    }
}
