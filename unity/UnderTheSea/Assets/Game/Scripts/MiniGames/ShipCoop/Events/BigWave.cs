using UnityEngine;

/// <summary>
/// 🌊 거대한 파도. 조타를 붙잡고 정면으로 돌파한다. (SHIPCOOP.md 5장)
///
/// 암초와 반대입니다. 암초는 꺾어서 피하고, 파도는 **꺾지 않고 정면으로 받아야** 합니다.
/// 폭이 배가 움직일 수 있는 폭보다 훨씬 넓어서 비켜갈 방법이 없습니다.
///
/// **파도가 치는 동안 뱃머리가 계속 한쪽으로 밀립니다.**
/// 사람이 조타에 붙어 반대로 꺾고 있어야 정면이 유지됩니다.
/// 이것이 이 사건의 전부입니다. 밀리는 힘이 없으면 파도는 아무 일도 아닙니다 —
/// 자리가 비면 조타륜이 저절로 정면으로 돌아오기 때문에 **아무도 안 가도 넘어갑니다.**
///
/// <code>
/// 돌풍  →  돛이 계속 풀린다      →  돛에 사람을 묶는다
/// 파도  →  뱃머리가 계속 밀린다  →  조타에 사람을 묶는다
/// </code>
///
/// **실패해도 배 HP 는 깎지 않습니다.** 대신 갑판에 물이 쏟아집니다.
/// HP 는 깎이고 끝이지만 물은 누군가 퍼내야 할 일로 남습니다.
/// 실패의 대가는 게이지가 아니라 일거리여야 합니다. (5장)
///
/// **바다 위에 실제로 떠 있는 파도입니다.** 예고 동안 수평선에 나타나 다가오고,
/// 뱃머리가 정면이면 흰색, 벗어나 있으면 붉게 보입니다.
/// 조타를 돌리다 색이 바뀌는 지점을 손으로 찾게 하는 것이 목적입니다. (9장)
/// </summary>
public class BigWave : VoyageEvent
{
    [Header("바다 위 파도")]
    [Tooltip("띄울 파도. 비워두면 판을 만들어 쓴다. (프로토타입용)")]
    [SerializeField] private GameObject wavePrefab;

    [Tooltip("판으로 만들 때 입힐 색. 비워두면 회색이라 바다에서 안 보인다.")]
    [SerializeField] private Material waveMaterial;

    [Tooltip("파도의 폭 (m).\n\n" +
             "배가 좌우로 갈 수 있는 폭은 약 5m 뿐이다. 그보다 조금 넓은 정도로는\n" +
             "수평선에서 좌우 끝이 보여서 '돌아가면 되겠다' 로 읽힌다.\n" +
             "화면 밖으로 나갈 만큼 넓어야 '못 피한다' 가 그림만으로 읽힌다.")]
    [SerializeField, Min(2f)] private float waveWidth = 120f;

    // ⚠ **높이가 1.5m 였습니다. 그중 물 위로 나온 것은 0.75m 뿐이었습니다.**
    //    큐브의 피벗이 가운데라 절반이 물속이었기 때문입니다.
    //    지금은 <see cref="ShipCoopWaveMesh"/> 가 수면(y 0) 기준으로 뽑아 줍니다.
    //
    // ⚠ **키운다고 벽으로 보이지는 않습니다.** 예전에 그랬던 것은 납작한
    //    네모라서였지 큰 것이 문제가 아니었습니다. 지금은 마루가 곡면이고
    //    앞으로 말리므로, 높일수록 오히려 파도로 읽힙니다. (그 주석은 저쪽에 있습니다)
    [Tooltip("수면에서 마루까지의 높이 (m). 판으로 만들 때만 쓴다.")]
    [SerializeField, Min(0.2f)] private float waveHeight = 8f;

    // ⚠ **높이를 바꾸면 두께도 같이 바꿔야 합니다.**
    //
    //    뒤 비탈의 기울기는 둘의 **비율**로 정해집니다. 높이만 4 → 8 로 올리고
    //    두께를 6 으로 두면 비탈이 40° 에서 60° 가 되어, 파도가 아니라 다시
    //    벽으로 섭니다. 1 : 1.5 를 지키면 키워도 비탈이 그대로입니다.
    [Tooltip("파도의 앞뒤 두께 (m).\n\n" +
             "**높이의 1.5배**로 둔다. 비율이 기울기를 정하므로 높이를 바꾸면 여기도 같이 바꾼다.\n" +
             "얇으면 종잇장처럼 서 있고, 두꺼우면 마루가 뭉툭해져서 물마루가 안 보인다.")]
    [SerializeField, Min(0.2f)] private float waveThickness = 12f;

    [Tooltip("수면 아래로 이만큼 잠긴다 (m). 0 이면 밑선이 드러나서 바다에 얹힌 것처럼 보인다.")]
    [SerializeField, Min(0f)] private float waveSink = 0.8f;

    [Tooltip("뱃머리가 정면일 때의 색")]
    [SerializeField] private Color straightColor = new Color(0.70f, 0.85f, 1f);

    [Tooltip("정면에서 벗어났을 때의 색. 조타를 돌리면 이 색이 풀린다.")]
    [SerializeField] private Color crookedColor = new Color(0.90f, 0.25f, 0.20f);

    [Header("밀리는 힘")]
    [Tooltip("파도가 치는 동안 뱃머리가 초당 이만큼 밀린다 (도/초).\n\n" +
             "🤝 **조타 회전 속도(기본 35)보다 크게** 잡는다. 그래야 혼자서는 지고\n" +
             "둘이 붙어야(70) 이긴다. 이것이 6장의 협력 작업이다.\n\n" +
             "  아무도 없음   45°/초        → 3.9초 뒤 실패\n" +
             "  혼자          45−35 = 10    → 5.5초 뒤 실패. 시간은 벌지만 진다\n" +
             "  둘이서        70−45 = +25   → 버틴다\n\n" +
             "35 보다 작게 두면 혼자서도 이기고, 협력이 사라진다.\n" +
             "0 으로 두면 아무도 조타에 안 가도 파도가 저절로 넘어간다.")]
    [SerializeField, Min(0f)] private float pushPerSecond = 45f;

    [Tooltip("켜면 밀리는 방향을 매번 무작위로 정한다.")]
    [SerializeField] private bool randomSide = true;

    [Tooltip("randomSide 를 끌 때 쓸 방향. 양수면 우현으로 밀린다.")]
    [SerializeField] private float fixedSide = 1f;

    [Header("정면 판정")]
    [Tooltip("뱃머리가 이 각도 안에 있으면 정면으로 받는 것으로 본다.")]
    [SerializeField, Min(1f)] private float straightTolerance = 12f;

    [Tooltip("정면을 벗어나도 되는 시간 (초). 이만큼 넘게 벗어나면 실패한다.\n" +
             "0 으로 두면 한 순간도 벗어날 수 없어 너무 가혹하다.")]
    [SerializeField, Min(0f)] private float allowedOffTime = 1.5f;

    [Header("갑판에 넘어오는 물")]
    [Tooltip("옆으로 맞으면 갑판에 물이 이만큼 쏟아진다. (0 ~ 1)\n\n" +
             "배 HP 대신 이걸 씁니다. HP 는 깎이고 끝이지만 물은 퍼내야 할 일로 남습니다.\n" +
             "그래서 사건 하나가 다른 자리를 비우는 연쇄가 만들어집니다. (5장)")]
    [SerializeField, Range(0f, 1f)] private float floodOnFail = 0.35f;

    [Tooltip("정면으로 잘 받아냈어도 이만큼은 넘어온다. (0 ~ 1)\n\n" +
             "**파도가 지나가면 무조건 물이 찹니다.** 정면으로 받는 것은 피하는 것이 아니라\n" +
             "덜 맞는 것입니다. 넘겨도 뒷정리가 남아야 배수가 상시 작업이 됩니다. (4장)\n\n" +
             "0 으로 두면 예전처럼 실패할 때만 물이 찹니다.")]
    [SerializeField, Range(0f, 1f)] private float floodOnSucceed = 0.15f;

    /// <summary>정면을 벗어나 있던 시간 (초)</summary>
    public float OffTime { get; private set; }

    /// <summary>지금 정면으로 받고 있는지</summary>
    public bool IsStraight { get; private set; } = true;

    /// <summary>밀리는 쪽. +1 이면 우현으로 밀린다. 꺾어야 할 방향은 그 반대다.</summary>
    public float PushSide { get; private set; } = 1f;

    /// <summary>벗어난 시간이 허용치의 몇 %인지. HUD 게이지가 이걸 본다.</summary>
    public float OffTime01 =>
        allowedOffTime <= 0f ? (IsStraight ? 0f : 1f) : Mathf.Clamp01(OffTime / allowedOffTime);

    private HelmTask _helm;
    private ShipFlooding _flooding;
    private GameObject _wave;
    private Material _waveInstance;

    protected override void Awake()
    {
        base.Awake();
        _flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
    }

    /// <summary>
    /// 예고 시작. **여기서 파도를 띄우고 밀릴 방향을 정한다.**
    ///
    /// 조타도 여기서 찾습니다. 터질 때 찾으면 예고 동안 색을 칠할 수가 없는데,
    /// 정작 뱃머리를 되돌려야 하는 시간이 예고 구간입니다.
    ///
    /// 암초와 달리 좌우로 치우쳐 놓지 않습니다. 파도는 뱃길을 가로질러 오므로
    /// 피할 수 없고, **정면으로 받는 것 말고는 방법이 없다**는 것이 보여야 합니다. (5장)
    /// </summary>
    protected override void OnWarn()
    {
        _helm = FindHelm();

        PushSide = randomSide
            ? (Dice.NextDouble() < 0.5 ? -1f : 1f)   // 서버 씨앗으로. 기계마다 다른 쪽이 나오면 안 된다 (11장)
            : Mathf.Sign(fixedSide == 0f ? 1f : fixedSide);

        if (_helm == null)
        {
            Debug.LogWarning($"[{name}] 조타를 찾지 못했습니다. 정면 판정을 할 수 없습니다.", this);
        }

        if (VoyageSea.Current == null)
        {
            return;
        }

        if (wavePrefab != null)
        {
            _wave = Instantiate(wavePrefab);
        }
        else
        {
            // ⚠ **빈 것을 껍데기로 두고 마루를 자식으로 넣습니다.**
            //    `VoyageSea.Place` 가 매 프레임 이 오브젝트의 위치를 통째로
            //    덮어씁니다. 마루를 그대로 쓰면 자리를 잡아 줄 데가 없습니다.
            _wave = new GameObject("BigWave");

            GameObject crest = new GameObject("파도 마루");
            crest.transform.SetParent(_wave.transform, false);

            // 메시가 수면(y 0) 기준으로 나오므로 따로 올리거나 내리지 않는다.
            crest.AddComponent<MeshFilter>().sharedMesh =
                ShipCoopWaveMesh.GetOrBuild(waveWidth, waveHeight, waveThickness, waveSink);

            MeshRenderer skin = crest.AddComponent<MeshRenderer>();

            // ⚠ CreatePrimitive 은 기본 재질을 끼워 줬지만 직접 만든 것은 비어 있습니다.
            //    그대로 두면 120m 짜리 자홍색 벽이 섭니다.
            skin.sharedMaterial = waveMaterial != null ? waveMaterial : FallbackPaint();

            // 부딪히는 것은 안 붙인다. 판정은 조타각으로 한다.
        }

        _wave.name = $"BigWave_{Time.frameCount}";

        // 색을 바꾸려면 이 파도만의 재질이 필요하다. 공용 재질을 칠하면
        // 에셋 파일이 바뀌어서 다음 판까지 붉은 채로 남는다.
        Renderer renderer = _wave.GetComponentInChildren<Renderer>();
        _waveInstance = renderer != null ? renderer.material : null;

        VoyageSea.Current.Place(_wave.transform, 0f, Approach01);
    }

    /// <summary>
    /// <see cref="waveMaterial"/> 을 안 꽂았을 때 쓸 임시 재질.
    ///
    /// 자홍색 벽보다는 낫다는 것뿐입니다. 제대로 하려면 바다가 쓰는
    /// SeaWater.mat 을 인스펙터에서 꽂으세요. 그래야 바다와 색이 맞습니다.
    /// </summary>
    private static Material FallbackPaint()
    {
        Shader lit = Shader.Find("Universal Render Pipeline/Lit");

        Debug.LogWarning("[BigWave] waveMaterial 이 비어 있습니다. 임시 재질로 그립니다. " +
                         "바다와 색을 맞추려면 SeaWater.mat 을 꽂으세요.");

        return new Material(lit != null ? lit : Shader.Find("Sprites/Default"));
    }

    /// <summary>
    /// 예고 때부터 계속 다가온다. 판정은 안 하고, **색만 바꾼다.**
    ///
    /// 예고 중에 칠하는 것이 요점입니다. 아직 아무것도 안 깎이는 동안
    /// 조타를 돌려보며 색이 바뀌는 지점을 찾게 합니다. 규칙을 글자로 읽을 필요가 없습니다.
    /// </summary>
    protected override void OnShow(float deltaTime)
    {
        IsStraight = _helm == null || Mathf.Abs(_helm.Heading) <= straightTolerance;

        if (_waveInstance != null)
        {
            _waveInstance.color = IsStraight ? straightColor : crookedColor;
        }

        if (_wave != null && VoyageSea.Current != null)
        {
            VoyageSea.Current.Place(_wave.transform, 0f, Approach01);
        }
    }

    /// <summary>
    /// 끝났으면 파도를 치우고 **미는 힘을 끈다.**
    ///
    /// 성공·실패·취소가 전부 여기를 지납니다. OnSucceed / OnFail 에 나눠 두면
    /// 게임이 끝나거나 예고 중에 끊겼을 때 **뱃머리가 계속 밀린 채로 남습니다.**
    /// </summary>
    protected override void OnHide()
    {
        if (_helm != null)
        {
            _helm.ExternalPushPerSecond = 0f;
        }

        if (_wave != null)
        {
            Destroy(_wave);
            _wave = null;
        }

        _waveInstance = null;
    }

    /// <summary>파도가 닥쳤다. 여기서부터 뱃머리가 밀린다.</summary>
    protected override void OnBegin()
    {
        OffTime = 0f;

        if (_helm == null || pushPerSecond <= 0f)
        {
            return;
        }

        _helm.ExternalPushPerSecond = pushPerSecond * PushSide;

        string side = PushSide < 0f ? "좌현" : "우현";
        Debug.Log($"[{name}] 뱃머리가 {side}으로 밀린다. 조타를 잡고 버텨라. ({pushPerSecond:F0}°/초)", this);
    }

    protected override void OnTick(float deltaTime)
    {
        // IsStraight 는 OnShow 가 매 프레임 먼저 채운다. (예고 중에도 채운다)
        if (_helm == null || IsStraight)
        {
            return;
        }

        OffTime += deltaTime;

        if (OffTime > allowedOffTime)
        {
            Fail();
        }
    }

    /// <summary>버틴 시간이 다 됐다. 정면을 지켰으면 넘긴 것이다.</summary>
    protected override void OnTimeout()
    {
        Succeed();
    }

    /// <summary>정면으로 받아냈다. 그래도 물은 넘어온다.</summary>
    protected override void OnSucceed()
    {
        Game?.ReportObstacleAvoided();
        Flood(floodOnSucceed);
    }

    /// <summary>옆으로 맞았다. 배를 깎는 대신 갑판에 물이 쏟아진다.</summary>
    protected override void OnFail()
    {
        Flood(floodOnFail);
    }

    /// <summary>
    /// 갑판에 물을 붓는다.
    ///
    /// 성공과 실패가 같은 자리를 쓰는 이유는 **차이가 양뿐**이기 때문입니다.
    /// 정면으로 받는 것은 피하는 것이 아니라 덜 맞는 것입니다.
    /// </summary>
    private void Flood(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        if (_flooding == null)
        {
            Debug.LogWarning($"[{name}] ShipFlooding 을 찾지 못했습니다. 물이 쏟아지지 않습니다.", this);
            return;
        }

        _flooding.Add(amount);
    }

    /// <summary>
    /// 혼자 버티는 중이면 발생 중에도 안내를 띄운다.
    ///
    /// 이건 가르치는 말이 아니라 **"지금 한 명 더 안 오면 진다"** 는 신호입니다.
    /// 예고 때만 띄우면 혼자 붙은 사람은 왜 밀리는지 모른 채로 집니다.
    /// </summary>
    public override bool HintIsUrgent => _helm != null && _helm.NeedsHelp;

    /// <summary>
    /// HUD 문구. 사건 알림 아래에 작은 글씨로 붙는다.
    ///
    /// **어느 키를 누르는지까지만 말한다.** 각도와 버틴 시간은 뺐습니다 — 이 게임은
    /// 뱃머리가 파도를 향했는지를 **눈으로 보고** 판단하는 게임이지, 34° 를 읽고
    /// 판단하는 게임이 아닙니다. 숫자는 디버그(F1, <see cref="StraightHint"/>)에 있습니다.
    /// </summary>
    public override string LiveHint()
    {
        // 🤝 혼자서는 못 이기는 사건이다. 그 사실을 제일 먼저 말해준다. (6장)
        //
        // 이 줄이 없으면 혼자 붙은 사람은 자기가 왜 밀리는지 모릅니다.
        // "조타가 고장났나?" 로 읽히고, 도움을 부를 생각을 못 합니다.
        if (_helm != null && _helm.NeedsHelp)
        {
            return IsRunning
                ? "혼자서는 못 버틴다 — 🆘 한 명 더!"
                : "혼자서는 못 버틴다 — 둘이 조타륜을 잡아라";
        }

        if (IsStraight)
        {
            return IsRunning ? "정면 유지 중 — 버텨라" : "정면으로 맞춰라";
        }

        return (_helm != null ? _helm.Heading : 0f) > 0f ? "J 로 정면으로!" : "L 로 정면으로!";
    }

    /// <summary>
    /// 디버그 오버레이가 띄우는 한 줄. (F1)
    ///
    /// 각도와 버틴 시간이 여기 붙습니다. 카드 쪽(<see cref="LiveHint"/>)에서는 뺐습니다 —
    /// 조타륜을 붙잡고 화면이 흔들리는 중에 읽을 수 있는 건 "어느 키" 까지입니다. (9장)
    /// 숫자가 맞는지 눈으로 맞춰볼 곳은 남겨 둡니다.
    /// </summary>
    public string StraightHint()
    {
        float heading = _helm != null ? _helm.Heading : 0f;

        return $"{LiveHint()}  (지금 {heading:F0}°, 벗어남 {OffTime:F1}/{allowedOffTime:F1}초)";
    }

    private static HelmTask FindHelm()
    {
        for (int i = 0; i < TaskBase.All.Count; i++)
        {
            if (TaskBase.All[i] is HelmTask helm)
            {
                return helm;
            }
        }

        return null;
    }
}
