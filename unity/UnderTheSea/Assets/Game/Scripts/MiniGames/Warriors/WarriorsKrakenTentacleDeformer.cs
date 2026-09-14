using UnityEngine;

namespace Warriors
{
    public sealed class WarriorsKrakenTentacleDeformer : MonoBehaviour
    {
        private MeshFilter filter;
        private Mesh runtimeMesh;
        private Vector3[] baseVertices;
        private Vector3[] deformedVertices;
        private Vector4[] weights;
        private WarriorsTarget[] targets;
        private readonly float[] hitStrength = new float[4];

        public void Configure(MeshFilter source, WarriorsTarget[] tentacleTargets)
        {
            filter = source;
            targets = tentacleTargets;
            InitializeMesh();
        }

        private void Start() => InitializeMesh();

        private void InitializeMesh()
        {
            if (runtimeMesh != null || filter == null || filter.sharedMesh == null) return;
            runtimeMesh = Instantiate(filter.sharedMesh);
            runtimeMesh.name = filter.sharedMesh.name + "_RuntimeTentacleRig";
            filter.sharedMesh = runtimeMesh;
            baseVertices = runtimeMesh.vertices;
            deformedVertices = new Vector3[baseVertices.Length];
            weights = new Vector4[baseVertices.Length];

            Bounds worldBounds = filter.GetComponent<Renderer>().bounds;
            float[] centers =
            {
                worldBounds.center.x - worldBounds.size.x * .34f,
                worldBounds.center.x - worldBounds.size.x * .15f,
                worldBounds.center.x + worldBounds.size.x * .15f,
                worldBounds.center.x + worldBounds.size.x * .34f
            };
            float radius = worldBounds.size.x * .15f;
            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector3 world = filter.transform.TransformPoint(baseVertices[i]);
                float vertical = Mathf.InverseLerp(worldBounds.min.y + worldBounds.size.y * .18f,
                    worldBounds.max.y, world.y);
                weights[i] = new Vector4(
                    Weight(world.x, centers[0], radius) * vertical,
                    Weight(world.x, centers[1], radius) * vertical,
                    Weight(world.x, centers[2], radius) * vertical,
                    Weight(world.x, centers[3], radius) * vertical);
            }
        }

        public void PlayHit(WarriorsTarget target)
        {
            if (targets == null) return;
            for (int i = 0; i < targets.Length && i < hitStrength.Length; i++)
                if (targets[i] == target) hitStrength[i] = 1f;
        }

        private void LateUpdate() => UpdateDeformation(Time.time, Time.deltaTime);

        public void UpdateDeformation(float time, float deltaTime)
        {
            if (runtimeMesh == null) return;
            for (int i = 0; i < hitStrength.Length; i++)
                hitStrength[i] = Mathf.MoveTowards(hitStrength[i], 0f, deltaTime * 4.8f);

            for (int i = 0; i < baseVertices.Length; i++)
            {
                Vector4 w = weights[i];
                Vector3 displacement = Vector3.zero;
                for (int tentacle = 0; tentacle < 4; tentacle++)
                {
                    float weight = w[tentacle];
                    if (weight <= .001f) continue;
                    float side = tentacle < 2 ? -1f : 1f;
                    float idle = Mathf.Sin(time * 2.1f + tentacle * 1.37f) * .055f;
                    float hit = Mathf.Sin((1f - hitStrength[tentacle]) * Mathf.PI * 3f)
                        * hitStrength[tentacle] * .30f;
                    displacement += filter.transform.InverseTransformVector(
                        new Vector3(side * (idle + hit), Mathf.Abs(idle) * .55f + Mathf.Abs(hit) * .18f, 0f)) * weight;
                }
                deformedVertices[i] = baseVertices[i] + displacement;
            }
            runtimeMesh.vertices = deformedVertices;
            runtimeMesh.RecalculateBounds();
        }

        private static float Weight(float value, float center, float radius)
        {
            float normalized = Mathf.Clamp01(1f - Mathf.Abs(value - center) / radius);
            return normalized * normalized * (3f - 2f * normalized);
        }

        private void OnDestroy()
        {
            if (runtimeMesh != null) Destroy(runtimeMesh);
        }
    }
}
