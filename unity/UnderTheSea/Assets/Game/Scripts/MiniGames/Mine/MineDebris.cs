using UnityEngine;

/// <summary>
/// 돌이 깨질 때 튀는 부스러기. (MINE.md 4장)
///
/// 파티클 시스템 **하나**로 400칸을 전부 처리한다. 칸마다 만들면 오브젝트가
/// 400개 늘어난다. 깨진 자리에서 <see cref="Burst"/> 로 몇 조각씩 뿜는다.
///
/// 조각은 작은 육면체다. 광산이 어두워 랜턴 불빛에만 반짝이므로 화려하지 않아도
/// "깨졌다" 는 느낌이 난다.
///
/// 비어 있어도 게임은 돈다. <see cref="MineGridView"/> 가 없으면 그냥 안 부른다.
/// </summary>
public class MineDebris : MonoBehaviour
{
    [Header("조각 수")]
    [Tooltip("돌이 깨질 때 튀는 조각 수.")]
    [SerializeField, Range(0, 40)] private int piecesPerBreak = 10;

    [Tooltip("금만 갔을 때 튀는 조각 수. 깨질 때보다 적어야 구분된다.")]
    [SerializeField, Range(0, 20)] private int piecesPerCrack = 4;

    [Header("모양과 움직임")]
    [Tooltip("조각 한 변의 길이(m).")]
    [SerializeField, Min(0.01f)] private float pieceSize = 0.11f;

    [Tooltip("튀어나가는 속도(m/s).")]
    [SerializeField, Min(0f)] private float speed = 2.6f;

    [Tooltip("조각이 남아 있는 시간(초).")]
    [SerializeField, Min(0.1f)] private float lifetime = 0.9f;

    [Tooltip("중력을 얼마나 받는가. 1 이면 보통 중력.")]
    [SerializeField, Min(0f)] private float gravity = 1.4f;

    [Header("색")]
    [Tooltip("조각 색. 돌 색과 비슷하게 두면 자연스럽다.")]
    [SerializeField] private Color pieceColor = new Color(0.62f, 0.58f, 0.5f);

    [Tooltip("비우면 URP Lit 으로 하나 만들어 쓴다.")]
    [SerializeField] private Material pieceMaterial;

    private ParticleSystem _ps;

    private void Awake()
    {
        Build();
    }

    /// <summary>
    /// 그 자리에서 조각을 뿜는다.
    /// </summary>
    /// <param name="broke">깨진 것인가. 아니면 금만 간 것이다.</param>
    public void Burst(Vector3 position, bool broke)
    {
        if (_ps == null) return;

        int count = broke ? piecesPerBreak : piecesPerCrack;
        if (count <= 0) return;

        var p = new ParticleSystem.EmitParams
        {
            position = position,
            applyShapeToPosition = true,
        };

        _ps.Emit(p, count);
    }

    private void Build()
    {
        var go = new GameObject("Debris");
        go.transform.SetParent(transform, false);

        _ps = go.AddComponent<ParticleSystem>();

        // 저절로 뿜지 않게 한다. 돌이 깨질 때만 Emit 으로 부른다.
        ParticleSystem.EmissionModule emission = _ps.emission;
        emission.enabled = false;

        ParticleSystem.MainModule main = _ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(pieceSize * 0.6f, pieceSize);
        main.startColor = pieceColor;
        main.gravityModifier = gravity;
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 600;

        // 위쪽 반구로 흩어진다. 바닥에서 튀어오르는 모양.
        ParticleSystem.ShapeModule shape = _ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Hemisphere;
        shape.radius = 0.25f;

        // 날아가면서 조금씩 돈다.
        ParticleSystem.RotationOverLifetimeModule spin = _ps.rotationOverLifetime;
        spin.enabled = true;
        spin.separateAxes = true;
        spin.x = new ParticleSystem.MinMaxCurve(-3f, 3f);
        spin.y = new ParticleSystem.MinMaxCurve(-3f, 3f);
        spin.z = new ParticleSystem.MinMaxCurve(-3f, 3f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        renderer.mesh = CubeMesh();
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.sharedMaterial = pieceMaterial != null ? pieceMaterial : MakeMaterial();
    }

    /// <summary>기본 육면체 메시. 임시 오브젝트에서 빌려온다.</summary>
    private static Mesh CubeMesh()
    {
        var temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Mesh mesh = temp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(temp);
        return mesh;
    }

    /// <summary>
    /// 조각에 쓸 머티리얼. **못 만들면 null 을 돌려준다.**
    ///
    /// ⚠ Dedicated Server 빌드에는 셰이더가 들어 있지 않다 (Dedicated Server Optimizations).
    ///   그대로 두면 <c>Shader.Find</c> 가 null 을 돌려주고 <c>new Material(null)</c> 이
    ///   예외를 던져 <c>Awake</c> 가 중간에 끊긴다. 부스러기는 연출이라 서버에는 없어도 된다.
    ///   (광산 서버화 1단계 — Warriors 에서 같은 함정을 겪었다)
    /// </summary>
    private Material MakeMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader == null) return null;

        return new Material(shader) { color = pieceColor };
    }
}
