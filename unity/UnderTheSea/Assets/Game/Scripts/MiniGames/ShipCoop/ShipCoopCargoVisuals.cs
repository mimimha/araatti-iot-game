using UnityEngine;

/// <summary>
/// 🎒 들고 다니는 물건 · 갑판에 놓인 물건의 **모양**을 한 곳에 모은 설정. (SHIPCOOP.md 4장)
///
/// <c>Resources/ShipCoopCargoVisuals.asset</c> 에 하나 있고, 배치 도구(<c>ShipCoopDeckLayout</c>)가 만들고 채운다.
/// <c>CarryTask</c>(들고 있을 때)와 <c>DroppedCargo</c>(놓았을 때)가 같은 설정을 읽어서
/// **든 것과 놓인 것이 같은 물건으로 보인다.**
///
/// <code>
///   🪣 물     양동이 모델 (Quaternius Pirate Kit) + 안에 물 원판
///   🪵 자재   판자 2장 묶음 (Synty PolygonGeneric Plank_02). 자기 재질 그대로
///   ⚫ 포탄   아직 모델 없음 → 색 칠한 캡슐 / 회색 구 (예전 그대로)
/// </code>
///
/// 포탄 모델이 오면 아래 슬롯만 채우고 CarryTask · DroppedCargo 의 분기를 한 줄씩 늘리면 된다.
/// </summary>
[CreateAssetMenu(menuName = "ShipCoop/Cargo Visuals", fileName = "ShipCoopCargoVisuals")]
public class ShipCoopCargoVisuals : ScriptableObject
{
    /// <summary>Resources 안의 이름. 배치 도구와 런타임이 같은 이름을 쓴다.</summary>
    public const string ResourceName = "ShipCoopCargoVisuals";

    [Header("🪣 물 — 양동이")]
    [Tooltip("양동이 모델. Prop_Bucket.fbx — ⚠ FBX 루트에 회전 -90° x · 배율 100 이 들어 있다(cm 단위 모델의 축·단위 변환). 루트를 identity 로 덮어쓰면 0.8cm 짜리가 옆으로 눕는다.")]
    public GameObject bucketModel;

    [Tooltip("양동이에 입힐 URP 재질. 낚시 게임이 같은 FBX 에 쓰는 것.")]
    public Material bucketMaterial;

    [Tooltip("양동이 실제 높이(m). 0 보다 크면 렌더러 bounds 를 재서 이 높이로 맞춘다. 원본은 0.76m 라 조금 줄이는 셈. 0 이면 bucketScale 만 쓴다.")]
    [Min(0f)] public float bucketHeight = 0.55f;

    [Tooltip("양동이 배율. bucketHeight 로 맞춘 높이에 곱한다. 작아 보이면 1.3 까지.")]
    [Min(0.1f)] public float bucketScale = 1f;

    [Tooltip("양동이 안 물 원판의 색. CarryTask.waterColor · DroppedCargo 와 같다.")]
    public Color waterColor = new Color(0.25f, 0.60f, 0.85f);

    // ⚠ **물 원판이 양동이에 안 맞았습니다.** 지름을 `min(가로, 세로) × 0.75` 로 잡았는데,
    //    이 양동이는 아래가 좁고 **위로 벌어지는** 모양이다. (재서 확인 — ShipCoopBucketMeasure)
    //
    //      바닥 반지름 0.31m  →  테두리 반지름 0.38m,  전체 0.760 × 0.679 × 0.715m
    //      테두리가 경계의 **맨 위**다 (손잡이가 그 위로 안 솟는다)
    //
    //    넓은 테두리 바로 아래에 지름 0.54m 원판을 놓으니 물이 덜 찬 것처럼 보였다.
    //    그래서 테두리 기준으로 다시 잡고, 두 값 다 인스펙터에서 맞출 수 있게 연다.
    [Tooltip("물 원판 지름. 양동이 테두리 폭 대비 비율. 1 이면 테두리에 딱 붙는다 — 벽 두께만큼 줄인다.")]
    [Range(0.3f, 1f)] public float waterWidth = 0.88f;

    [Tooltip("물 높이. 테두리에서 이만큼 아래로 내린다 (양동이 높이 대비 비율). 0 이면 테두리와 같은 높이.")]
    [Range(0f, 0.5f)] public float waterSink = 0.08f;

    [Header("🪵 자재 — 판자 묶음")]
    [Tooltip("판자 2장이 붙은 메시 하나. SM_Gen_Prop_Plank_02 (1.77 × 0.11 × 0.28m). 긴 축이 x 라 그대로 들면 어깨 방향으로 눕는다.")]
    public GameObject plankModel;

    [Tooltip("판자 배율. 1 로 시작. 얇아서(0.11m) 안 보이면 1.3 까지 — 두께만이 아니라 통째로.")]
    [Min(0.1f)] public float plankScale = 1f;

    [Header("⚫ 포탄 — 아직 캡슐 (비워 두면 예전 표시 그대로)")]
    public GameObject ammoModel;

    private static ShipCoopCargoVisuals _cached;
    private static bool _searched;

    /// <summary>Resources 에서 찾는다. 없으면 null — 그러면 예전 프리미티브 표시로 돈다.</summary>
    public static ShipCoopCargoVisuals Load()
    {
        if (!_searched)
        {
            _cached = Resources.Load<ShipCoopCargoVisuals>(ResourceName);
            _searched = true;
        }

        return _cached;
    }

    /// <summary>
    /// 양동이 하나를 만든다. 콜라이더는 떼고 (걸려 넘어지면 안 된다 — DroppedCargo 와 같은 이유),
    /// 재질을 입히고, 안에 **물 원판**을 넣는다. 들고 있는 양동이는 늘 물이 든 것이라 원판은 항상 켜져 있다.
    /// 모델이 없으면 null.
    /// </summary>
    public GameObject BuildBucket(Transform parent, string name)
    {
        GameObject made = BuildModel(bucketModel, bucketScale, bucketMaterial, parent, name);

        if (made != null)
        {
            FitHeight(made, bucketHeight, bucketScale);
            AddWaterDisc(made);
        }

        return made;
    }

    /// <summary>
    /// 모델을 <paramref name="height"/>(m) 높이로 맞추고 그 위에 <paramref name="scale"/> 를 곱한다.
    ///
    /// ⚠ Prop_Bucket.fbx 는 cm 단위 모델이라 FBX 루트에 **배율 100 · 회전 -90° x** 가 들어 있다. 예전 코드가 루트를
    ///    identity 로 덮어써서 0.8cm 짜리가 옆으로 누웠고, 물을 집어도 손 사이에 아무것도 안 보였다 — **있었지만 너무 작았다.**
    ///    루트는 이제 그대로 두지만(BuildModel), FBX 마다 크기가 달라 숫자를 외우는 대신 렌더러 bounds 를 재서
    ///    높이를 맞춘다. 만든 직후, 물 원판을 얹기 전에 부른다.
    /// </summary>
    private static void FitHeight(GameObject made, float height, float scale)
    {
        if (height <= 0f)
        {
            return;
        }

        Renderer[] draws = made.GetComponentsInChildren<Renderer>(true);

        if (draws.Length == 0)
        {
            Debug.LogWarning($"[ShipCoopCargoVisuals] {made.name} 에 렌더러가 없어 크기를 맞출 수 없다.", made);
            return;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        float now = box.size.y;

        if (now <= 0.0001f)
        {
            return;
        }

        // BuildModel 이 localScale 에 이미 scale 을 넣었으니 bounds 에도 들어 있다. 목표는 height × scale.
        made.transform.localScale *= height * scale / now;
    }

    /// <summary>
    /// 🪵 판자 묶음 하나를 만든다. 자기 재질(Synty Generic_01_A)을 그대로 쓴다 — URP 에서 정상이다.
    /// 콜라이더는 뗀다. 모델이 없으면 null 이라 예전 큐브 표시로 돈다.
    /// </summary>
    public GameObject BuildPlank(Transform parent, string name)
    {
        return BuildModel(plankModel, plankScale, null, parent, name);
    }

    /// <summary>
    /// 모델 하나를 자식으로 만든다. 콜라이더는 떼고(걸려 넘어지면 안 된다 — DroppedCargo 와 같은 이유),
    /// 재질이 주어졌으면 덮어쓴다. 모델이 없으면 null.
    ///
    /// ⚠ **빈 부모(손잡이) 아래에 모델을 넣는다.** 밖(CarryPose · Seat · Drop)이 만지는 것은 이 부모다.
    ///    예전엔 모델 루트를 바로 만들고 회전을 identity 로 덮어썼는데, FBX 루트에는 임포터가 넣은
    ///    **축 변환 회전(-90° x 등)**이 들어 있어 그걸 지우면 양동이가 옆으로 눕는다 — 입구가 앞을 보고
    ///    바닥만 보여서 "양동이가 잘 안 보였다". 모델 루트의 회전·배율은 프리팹 것을 그대로 둔다.
    /// </summary>
    private static GameObject BuildModel(GameObject model, float scale, Material paint, Transform parent, string name)
    {
        if (model == null)
        {
            return null;
        }

        GameObject made = new GameObject(name);
        made.transform.SetParent(parent, false);
        made.transform.localPosition = Vector3.zero;
        made.transform.localRotation = Quaternion.identity;
        made.transform.localScale = Vector3.one * scale;

        GameObject body = Instantiate(model, made.transform);
        body.name = "Model";
        body.transform.localPosition = Vector3.zero;
        // 회전 · 배율은 프리팹(FBX 루트) 값 그대로 — 위 주석.

        foreach (Collider bump in made.GetComponentsInChildren<Collider>(true))
        {
            Destroy(bump);
        }

        if (paint != null)
        {
            foreach (Renderer draw in made.GetComponentsInChildren<Renderer>(true))
            {
                Material[] coats = draw.sharedMaterials;

                for (int i = 0; i < coats.Length; i++)
                {
                    coats[i] = paint;
                }

                draw.sharedMaterials = coats;
            }
        }

        return made;
    }

    /// <summary>
    /// 테두리 살짝 아래에 납작한 원판. 두께 2cm.
    ///
    /// ⚠ 지름은 **테두리 폭**에서 낸다. 예전에는 전체 경계의 좁은 쪽 × 0.75 로 잡았는데, 이 양동이는
    ///    아래가 좁고 위로 벌어져서 그 값이 테두리보다 한참 작았다 — 물이 덜 찬 것처럼 보였다.
    ///    테두리가 가장 넓은 곳이고 경계의 맨 위라, 경계 가로폭이 곧 테두리 폭이다. (ShipCoopBucketMeasure)
    /// </summary>
    private void AddWaterDisc(GameObject bucket)
    {
        Renderer[] draws = bucket.GetComponentsInChildren<Renderer>(true);

        if (draws.Length == 0)
        {
            return;
        }

        Bounds box = draws[0].bounds;

        for (int i = 1; i < draws.Length; i++)
        {
            box.Encapsulate(draws[i].bounds);
        }

        GameObject disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Water";

        Collider bump = disc.GetComponent<Collider>();
        if (bump != null)
        {
            Destroy(bump);
        }

        disc.transform.SetParent(bucket.transform, false);

        float diameter = Mathf.Min(box.size.x, box.size.z) * waterWidth;
        Vector3 lossy = bucket.transform.lossyScale;

        // 유니티 실린더는 높이 2 라 y 배율이 곧 반높이다. 1cm → 두께 2cm.
        disc.transform.localScale = new Vector3(
            diameter / Mathf.Max(lossy.x, 0.0001f),
            0.01f / Mathf.Max(lossy.y, 0.0001f),
            diameter / Mathf.Max(lossy.z, 0.0001f));

        disc.transform.position = new Vector3(box.center.x, box.max.y - box.size.y * waterSink, box.center.z);
        disc.transform.rotation = bucket.transform.rotation;

        Debug.Log(
            $"[양동이] 물 원판 — 양동이 {box.size.x:F2}×{box.size.y:F2}×{box.size.z:F2}m, " +
            $"원판 지름 {diameter:F2}m (테두리 폭의 {waterWidth:P0}), " +
            $"테두리에서 {box.size.y * waterSink:F2}m 아래", bucket);

        Renderer surface = disc.GetComponent<Renderer>();
        if (surface != null)
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            Material water = lit != null ? new Material(lit) : new Material(surface.sharedMaterial);
            water.color = waterColor;
            surface.material = water;
        }
    }
}
