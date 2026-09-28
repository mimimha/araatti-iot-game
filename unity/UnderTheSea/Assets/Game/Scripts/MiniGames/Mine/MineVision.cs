using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// 광산의 어둠과 랜턴. (MINE.md 6장 — 관전은 시야 제한입니다)
///
/// **어둠은 모두에게 적용된다.** 관전자만 어둡게 하면 같은 광산인데 사람마다
/// 밝기가 다른 셈이 되고, 조명을 두 벌 만들어야 한다. 모두 어두우면
/// 6장의 "바닥 전체는 한눈에 안 들어온다" 가 지금 턴인 사람에게도 그대로 걸리고,
/// 나중에 관전 카메라가 붙어도 저절로 따라온다.
///
/// **시야를 가두는 것은 랜턴이지 안개가 아니다.** 안개는 *카메라*로부터의 거리로 가린다.
/// 우리가 원하는 것은 *플레이어*로부터의 거리다. 그래서 점광원을 쓴다.
/// 안개는 동굴 분위기를 내는 배경 효과로만 쓴다 — 멀리 있는 벽을 어둠에 묻는다.
///
/// ⚠ **안개는 밝을 때 반드시 꺼야 한다.** 탑뷰 카메라는 20m 위에 있어서
///   판 귀퉁이까지가 24m 다. 안개가 켜져 있으면 공개 7초에 바깥 칸이
///   3분의 1쯤 검게 먹혀 그림을 읽을 수 없다.
///
/// ⚠ 랜턴을 캐릭터 프리팹에 붙이지 않는다. 붙이면 로비의 같은 캐릭터도
///   광산 랜턴을 들고 다닌다. 여기서 라이트를 하나 만들어 따라다니게 한다.
///
/// 밝기 전환은 <see cref="MineGame"/> 이 단계마다 불러준다.
/// 공개와 결과는 밝게, 턴 중에는 어둡게.
///
/// ⚠ **밝힌다고 동굴까지 밝히면 안 된다.** 환경광은 전역이라 판만 골라 밝힐 수가 없다.
///   그대로 두면 탑뷰에서 판보다 둘레 바닥이 더 밝아 눈이 그쪽으로 간다.
///
///   **안개만으로는 못 자른다.** 탑뷰에서 판 귀퉁이까지가 24.4m 인데 바로 옆
///   둘레 바닥은 22.8m 다. 둘레가 판보다 카메라에 **더 가깝다.** 거리로 자르면
///   둘레보다 판이 먼저 먹힌다.
///
///   그래서 두 가지를 같이 쓴다.
///     먼 곳 — 안개. 판 귀퉁이 너머부터 덮는다 (벽, 바깥 바닥)
///     가까운 곳 — 동굴 쪽 색을 직접 낮춘다 (둘레 바닥, 소품)
/// </summary>
public class MineVision : MonoBehaviour
{
    [Header("랜턴")]
    [Tooltip("빛이 닿는 거리(m). 칸 크기가 1이면 곧 몇 칸까지 보이는지다.\n" +
             "MINE.md 12장의 조정 항목이다.")]
    [SerializeField, Min(1f)] private float lanternRange = 6f;

    [Tooltip("랜턴 밝기. 어두우면 올린다.")]
    [SerializeField, Min(0f)] private float lanternIntensity = 8f;

    [Tooltip("랜턴이 떠 있는 높이(m). 바닥에 두면 빛이 퍼지지 않는다.")]
    [SerializeField, Min(0f)] private float lanternHeight = 2f;

    [SerializeField] private Color lanternColor = new Color(1f, 0.85f, 0.6f);

    [Header("어두울 때")]
    [Tooltip("어두울 때의 환경광. 완전히 0 이면 랜턴 밖이 새까매서 방향 감각이 사라진다.")]
    [SerializeField] private Color darkAmbient = new Color(0.03f, 0.03f, 0.04f);

    [Tooltip("어두울 때 태양(Directional Light)을 얼마나 남길 것인가. 0 이면 완전히 끈다.")]
    [SerializeField, Range(0f, 1f)] private float darkSunIntensity = 0.03f;

    [Tooltip("어두울 때 안개를 켜서 먼 동굴 벽을 묻는다. 배경 효과이지 난이도 장치가 아니다.")]
    [SerializeField] private bool darkFog = true;

    [Tooltip("안개가 시작하는 거리(m). 판 반대편 끝이 20m 쯤이므로 그보다 멀어야 판이 안 흐려진다.")]
    [SerializeField, Min(0f)] private float darkFogStart = 20f;

    [Tooltip("안개가 완전히 덮는 거리(m). 동굴 벽이 20m 에 있으므로 그 너머를 덮는다.")]
    [SerializeField, Min(0f)] private float darkFogEnd = 70f;

    [SerializeField] private Color darkFogColor = new Color(0.025f, 0.025f, 0.035f);

    [Header("밝을 때 (공개 · 힌트 · 결과)")]
    [Tooltip("밝을 때의 환경광.\n" +
             "⚠ 광산은 하늘이 없어서(카메라 배경이 단색) 스카이박스 환경광을 쓸 수 없다.\n" +
             "  그래서 밝기를 씬에 맡기지 않고 여기서 직접 정한다.")]
    [SerializeField] private Color litAmbient = new Color(0.34f, 0.34f, 0.36f);

    [Tooltip("밝을 때 태양(Directional Light)을 얼마나 남길 것인가. 1 이면 씬에 적힌 세기 그대로다. 낮추면 탑뷰가 3인칭 시점처럼 어두워진다.")]
    [SerializeField, Range(0f, 1f)] private float litSunIntensity = 0.4f;

    [Tooltip("밝을 때도 판 바깥은 안개로 덮는다. 끄면 동굴 전체가 환하게 보인다.")]
    [SerializeField] private bool litFog = true;

    [Tooltip("밝을 때 동굴에 곱할 색. 어두울수록 판만 도드라진다. 흰색이면 안 낮춘다.")]
    [SerializeField] private Color litCaveTint = new Color(0.3f, 0.3f, 0.34f);

    [Tooltip("밝을 때 광물 밝기. 1 이면 제 색 그대로다. 암반과 따로 정한다 — 동굴을 어둡게 하면서 광물은 도드라지게 하려면 이 둘이 묶여 있으면 안 된다.")]
    [SerializeField, Range(0f, 2f)] private float litCrystalDim = 1f;

    [Tooltip("판 귀퉁이에서 이 배율만큼 떨어진 곳부터 안개가 시작한다. 1 보다 커야 판이 안 흐려진다.")]
    [SerializeField, Min(1f)] private float litFogNear = 1.02f;

    [Tooltip("안개가 완전히 덮는 지점. 위 값의 몇 배인가.")]
    [SerializeField, Min(1.05f)] private float litFogFar = 1.35f;

    [Header("연결")]
    [Tooltip("비워두면 씬의 Directional Light 를 찾는다.")]
    [SerializeField] private Light sun;

    [Tooltip("탑뷰 높이를 읽는다. 비워두면 씬에서 찾는다. 없으면 안개 거리를 못 재서 밝을 때 안개를 끈다.")]
    [SerializeField] private MineCamera boardCamera;

    [Tooltip("판 크기를 읽는다. 비워두면 씬에서 찾는다.")]
    [SerializeField] private MineGrid grid;

    [Tooltip("동굴 배경. 밝을 때 이 아래 것들의 색을 낮춘다. 비워두면 MineCave 를 찾는다.")]
    [SerializeField] private Transform caveRoot;

    /// <summary>빛이 닿는 거리. 밸런싱할 때 화면에 띄운다.</summary>
    public float LanternRange => lanternRange;

    /// <summary>지금 밝은 상태인가.</summary>
    public bool Lit { get; private set; } = true;

    private Light _lantern;
    private Transform _follow;

    // 동굴 색을 낮출 때 쓴다. 매번 모으면 낭비라 한 번만 모은다.
    private Renderer[] _caveRenderers;
    private MaterialPropertyBlock _caveBlock;
    private bool _caveDimmed;

    // 광물은 제 색을 들고 있다. 덮어쓰면 안 되고 곱해야 한다.
    private MineCrystalTint[] _caveTints;

    // 원래 값. 어둡게 했다가 반드시 되돌려야 한다.
    private Color _savedAmbient;
    private AmbientMode _savedAmbientMode;
    private float _savedSunIntensity;
    private bool _savedFog;
    private Color _savedFogColor;
    private FogMode _savedFogMode;
    private float _savedFogStart;
    private float _savedFogEnd;
    private bool _saved;

    private void Awake()
    {
        // 랜턴을 만들기 *전에* 태양을 찾는다. 안 그러면 랜턴을 태양으로 착각한다.
        if (sun == null) sun = FindSun();

        if (boardCamera == null) boardCamera = FindAnyObjectByType<MineCamera>(FindObjectsInactive.Include);
        if (grid == null) grid = FindAnyObjectByType<MineGrid>(FindObjectsInactive.Include);

        if (caveRoot == null)
        {
            var cave = GameObject.Find("MineCave");
            if (cave != null) caveRoot = cave.transform;
        }

        if (caveRoot != null)
        {
            _caveRenderers = caveRoot.GetComponentsInChildren<Renderer>(true);
            _caveTints = caveRoot.GetComponentsInChildren<MineCrystalTint>(true);
        }

        _caveBlock = new MaterialPropertyBlock();

        SaveOriginal();
        CreateLantern();

        SetLit(true);
    }

    private void OnDisable()
    {
        // 씬의 조명 설정은 전역이다. 꺼질 때 반드시 원래대로 돌려놓는다.
        RestoreOriginal();
    }

    private static Light FindSun()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
        {
            if (light.type == LightType.Directional) return light;
        }

        return null;
    }

    private void SaveOriginal()
    {
        if (_saved) return;

        _savedAmbient = RenderSettings.ambientLight;
        _savedAmbientMode = RenderSettings.ambientMode;
        _savedSunIntensity = sun != null ? sun.intensity : 0f;

        _savedFog = RenderSettings.fog;
        _savedFogColor = RenderSettings.fogColor;
        _savedFogMode = RenderSettings.fogMode;
        _savedFogStart = RenderSettings.fogStartDistance;
        _savedFogEnd = RenderSettings.fogEndDistance;

        _saved = true;
    }

    private void RestoreOriginal()
    {
        if (!_saved) return;

        RenderSettings.ambientMode = _savedAmbientMode;
        RenderSettings.ambientLight = _savedAmbient;

        RenderSettings.fog = _savedFog;
        RenderSettings.fogColor = _savedFogColor;
        RenderSettings.fogMode = _savedFogMode;
        RenderSettings.fogStartDistance = _savedFogStart;
        RenderSettings.fogEndDistance = _savedFogEnd;

        if (sun != null) sun.intensity = _savedSunIntensity;
    }

    private void CreateLantern()
    {
        var go = new GameObject("MineLantern");
        go.transform.SetParent(transform, false);

        _lantern = go.AddComponent<Light>();
        _lantern.type = LightType.Point;
        _lantern.color = lanternColor;
        _lantern.range = lanternRange;
        _lantern.intensity = lanternIntensity;

        // 그림자는 끈다. 칸이 400개라 켜면 무거운데, 시야를 가리는 데는 필요 없다.
        _lantern.shadows = LightShadows.None;

        _lantern.enabled = false;
    }

    /// <summary>
    /// 랜턴이 따라다닐 대상. **null 이면 랜턴을 끈다.**
    /// 턴이 시작될 때 MineGame 이 지금 턴인 사람을 넘겨준다.
    /// </summary>
    public void Follow(Transform target)
    {
        _follow = target;
        if (_lantern != null) _lantern.enabled = target != null && !Lit;
    }

    /// <summary>
    /// 밝게 / 어둡게. 공개와 결과는 밝게, 턴 중에는 어둡게.
    /// 밝을 때는 랜턴이 필요 없으므로 끈다.
    /// </summary>
    public void SetLit(bool lit)
    {
        Lit = lit;
        SaveOriginal();

        // 밝을 때도 어두울 때도 Flat 으로 간다. 색만 바꾼다.
        //
        // ⚠ 예전에는 밝힐 때 씬의 원래 설정(Skybox)으로 되돌렸는데, 광산은 하늘이 없어서
        //   (카메라 배경이 단색) 스카이박스 환경광이 0 에 가깝습니다. 그러면 공개 7초와
        //   결과 화면이 캄캄해집니다. 밝기를 씬에 맡기지 않고 여기서 정합니다.
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = lit ? litAmbient : darkAmbient;

        // 안개. 밝을 때와 어두울 때 거리가 다르다.
        //
        // 밝을 때는 **판이 안 먹히는 선까지** 당겨서 판 바깥만 덮는다.
        // 어두울 때는 멀리 밀어서 먼 동굴 벽만 묻는다.
        float near = darkFogStart;
        float far = darkFogEnd;
        bool wantFog = darkFog;

        if (lit)
        {
            // ⚠ 거리 재기를 && 뒤에 두면 안 된다. 앞이 false 면 아예 안 불리는데,
            //   그러면 corner 에 값이 안 들어가는 길이 생겨 컴파일이 막힌다.
            float corner;
            bool measured = TryBoardCornerDistance(out corner);

            wantFog = litFog && measured;
            if (wantFog)
            {
                near = corner * litFogNear;
                far = near * litFogFar;
            }
        }

        RenderSettings.fog = wantFog;
        if (wantFog)
        {
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = darkFogColor;
            RenderSettings.fogStartDistance = near;
            RenderSettings.fogEndDistance = far;
        }

        // 가까운 둘레는 안개가 못 잡는다. 동굴 쪽 색을 직접 낮춘다.
        DimCave(lit);

        if (sun != null)
            sun.intensity = _savedSunIntensity * (lit ? litSunIntensity : darkSunIntensity);

        if (_lantern != null) _lantern.enabled = !lit && _follow != null;
    }

    // 동굴 쪽만 색을 낮춘다. 판은 MineGridView 가 따로 칠하므로 건드리지 않는다.
    //
    // 머티리얼을 고치지 않고 MaterialPropertyBlock 으로 덮는다. 머티리얼은 여러
    // 오브젝트가 같이 쓰는 에셋이라, 고치면 재생을 멈춰도 그 색이 남는다.
    private void DimCave(bool lit)
    {
        if (_caveRenderers == null || _caveBlock == null) return;
        if (_caveDimmed == lit) return;

        Color tint = lit ? litCaveTint : Color.white;

        for (int i = 0; i < _caveRenderers.Length; i++)
        {
            Renderer r = _caveRenderers[i];
            if (r == null) continue;

            // ⚠ 광물은 건너뛴다. 제 색을 들고 있어서 덮어쓰면 흰 돌이 된다.
            //   재생 전에는 멀쩡하고 재생하면 색이 사라지는 증상이 이것이었다.
            if (r.GetComponent<MineCrystalTint>() != null) continue;

            r.GetPropertyBlock(_caveBlock);
            _caveBlock.SetColor("_BaseColor", tint);
            r.SetPropertyBlock(_caveBlock);
        }

        // 광물은 덮어쓰는 대신 제 색에 곱하게 한다.
        if (_caveTints != null)
        {
            float dim = lit ? litCrystalDim : 1f;
            for (int i = 0; i < _caveTints.Length; i++)
            {
                if (_caveTints[i] != null) _caveTints[i].SetDim(dim);
            }
        }

        _caveDimmed = lit;
    }

    // 탑뷰 카메라에서 판 귀퉁이까지의 거리.
    //
    // 카메라는 판 한가운데 위 h 에 있고, 귀퉁이는 수평으로 half*sqrt(2) 떨어져 있다.
    // **숫자를 박아두지 않는 이유** — 판 크기를 바꾸면 카메라 높이가 따라 바뀌고,
    // 그러면 이 거리도 바뀐다. 박아두면 판을 키운 날 판 바깥 칸이 안개에 먹힌다.
    private bool TryBoardCornerDistance(out float distance)
    {
        distance = 0f;
        if (boardCamera == null || grid == null) return false;

        float half = grid.Size * grid.CellSize * 0.5f;
        float h = boardCamera.BoardHeight;
        if (half <= 0f || h <= 0f) return false;

        distance = Mathf.Sqrt(2f * half * half + h * h);
        return true;
    }

    private void LateUpdate()
    {
        if (_lantern == null || _follow == null) return;

        // 캐릭터는 Update 에서 움직인다. LateUpdate 에서 따라가야 한 프레임 안 밀린다.
        _lantern.transform.position = _follow.position + Vector3.up * lanternHeight;

        // 인스펙터에서 값을 바꿔가며 맞출 수 있게 매 프레임 반영한다. (밸런싱용)
        _lantern.range = lanternRange;
        _lantern.intensity = lanternIntensity;
        _lantern.color = lanternColor;
    }
}