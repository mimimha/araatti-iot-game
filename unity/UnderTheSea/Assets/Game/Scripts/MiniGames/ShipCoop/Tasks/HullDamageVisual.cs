using UnityEngine;

/// <summary>
/// 🔧 수리 지점(파손 지점)의 **모양**. 빨간 큐브를 대신한다. (SHIPCOOP.md 5장)
///
/// 갑판 위에 네 겹이 얹힌다. 전부 <c>HullDamagePoint.prefab</c> 의 자식이고 배치 도구가 만들어 둔다.
///
/// <code>
///   🪵 판자 덮기    Plank_02 를 균열 위에 눕혀 쌓음. 망치질 1번마다 한 장씩     ← 수리 진행
///   💧 물 자국      파란 데칼. 새는 물. 수리되면 0.5초에 사라짐                ← 새는 중
///   🕳 균열         구멍. 처음부터 끝까지
///   🔴 표식         붉은 원. 알파 깜빡임. 누가 붙거나 수리되면 꺼짐             ← 멀리서 찾는 용도
///                                          갑판
/// </code>
///
/// ⚠ **RepairTask 의 값을 읽어서 그린다. 이벤트를 구독하지 않는다.**
///    <c>Repaired</c> 같은 event Action 은 서버에서만 터진다 — 클라이언트의 RepairTask 는 판정을 안 하고
///    <c>ShowRepair</c> 로 값만 받는다. (11장) 값(<c>Hits</c> · <c>IsRepaired</c> · <c>Workers</c>)은 양쪽에
///    똑같이 있으니, 매 프레임 그 값을 보고 그리면 서버 화면과 클라이언트 화면이 같아진다.
///
/// 표식이 있는 이유 — 예전 빨간 큐브가 하던 일이 "멀리서 여기 구멍" 이었다. 데칼은 바닥에 붙어 있어
/// 멀리서 안 읽히니, 그 일을 붉은 원이 대신한다. 누가 붙으면 이미 찾은 것이라 끈다.
/// </summary>
[DisallowMultipleComponent]
public class HullDamageVisual : MonoBehaviour
{
    [Header("연결 — 배치 도구가 채운다")]
    [Tooltip("비워두면 같은 오브젝트에서 찾는다.")]
    [SerializeField] private RepairTask repair;

    [Tooltip("네 겹을 담은 자식. 루트 배율을 상쇄하고 갑판 윗면에 맞춘다.")]
    [SerializeField] private Transform visual;

    [SerializeField] private Renderer marker;
    [SerializeField] private Renderer crack;
    [SerializeField] private Renderer leak;

    [Tooltip("켜는 순서대로. 망치질 n번이면 앞에서 n장이 보인다.")]
    [SerializeField] private GameObject[] planks;

    [Header("표식")]
    [Tooltip("HUD 의 crookedColor 와 같은 계열")]
    [SerializeField] private Color markerColor = new Color(0.90f, 0.25f, 0.20f);
    [SerializeField, Range(0f, 1f)] private float markerAlphaMin = 0.35f;
    [SerializeField, Range(0f, 1f)] private float markerAlphaMax = 0.80f;
    [SerializeField, Min(0.1f)] private float markerPeriod = 1f;

    [Header("물 자국")]
    [Tooltip("CarryTask.waterColor · 양동이 물과 같다")]
    [SerializeField] private Color leakColor = new Color(0.25f, 0.60f, 0.85f);
    [SerializeField, Min(0.05f)] private float leakFadeSeconds = 0.5f;

    private Material _markerPaint;
    private Material _leakPaint;
    private float _leakAlpha = 1f;
    private Vector3 _snappedAt = new Vector3(float.NaN, 0f, 0f);

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    private void Awake()
    {
        if (repair == null)
        {
            repair = GetComponent<RepairTask>();
        }

        // ⚠ 사본. sharedMaterial 에 칠하면 다른 구멍(두 지점이 동시에 뜬다)까지 같이 깜빡인다.
        _markerPaint = Instanced(marker);
        _leakPaint = Instanced(leak);

        Tint(_leakPaint, leakColor, 1f);
        Tint(_markerPaint, markerColor, markerAlphaMax);

        if (planks != null)
        {
            for (int i = 0; i < planks.Length; i++)
            {
                if (planks[i] != null)
                {
                    planks[i].SetActive(false);
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (_markerPaint != null) Destroy(_markerPaint);
        if (_leakPaint != null) Destroy(_leakPaint);
    }

    private void Update()
    {
        SnapToDeck();

        int hits = repair != null ? repair.Hits : 0;
        bool repaired = repair != null && repair.IsRepaired;

        // "수리 시작" — 누가 붙었거나(Workers 는 클라이언트에도 복제된다) 이미 망치질을 했다.
        bool started = repaired || hits > 0 || (repair != null && repair.Workers.Count > 0);

        ShowPlanks(repaired ? int.MaxValue : hits);
        FadeLeak(repaired);
        BlinkMarker(!started);
    }

    /// <summary>
    /// 네 겹을 **갑판 윗면**에 맞춘다. 루트는 갑판에서 조금 떠 있다(배치 도구의 DamageLift).
    /// 프리팹에는 그 값으로 미리 내려 두지만, 스폰 뒤 실제 갑판 높이를 한 번 더 맞춘다 —
    /// 층마다 바닥이 다르고, 네트워크에서는 위치가 조금 뒤에 도착한다. 루트가 움직이면 다시 맞춘다.
    /// </summary>
    private void SnapToDeck()
    {
        if (visual == null)
        {
            return;
        }

        Vector3 at = transform.position;

        if (!float.IsNaN(_snappedAt.x) && (at - _snappedAt).sqrMagnitude < 0.0001f)
        {
            return;
        }

        _snappedAt = at;

        ShipDeck deck = ShipDeck.At(at);

        if (deck == null)
        {
            return;
        }

        Vector3 where = visual.position;
        where.y = deck.SurfaceY;
        visual.position = where;
    }

    private void ShowPlanks(int count)
    {
        if (planks == null)
        {
            return;
        }

        for (int i = 0; i < planks.Length; i++)
        {
            if (planks[i] == null)
            {
                continue;
            }

            bool on = i < count;

            if (planks[i].activeSelf != on)
            {
                planks[i].SetActive(on);
            }
        }
    }

    private void FadeLeak(bool repaired)
    {
        if (leak == null)
        {
            return;
        }

        float target = repaired ? 0f : 1f;
        _leakAlpha = Mathf.MoveTowards(_leakAlpha, target, Time.deltaTime / leakFadeSeconds);

        bool visible = _leakAlpha > 0.001f;

        if (leak.enabled != visible)
        {
            leak.enabled = visible;
        }

        if (visible)
        {
            Tint(_leakPaint, leakColor, _leakAlpha);
        }
    }

    private void BlinkMarker(bool on)
    {
        if (marker == null)
        {
            return;
        }

        if (marker.enabled != on)
        {
            marker.enabled = on;
        }

        if (!on)
        {
            return;
        }

        float wave = 0.5f + 0.5f * Mathf.Sin(Time.time * (2f * Mathf.PI / markerPeriod));
        Tint(_markerPaint, markerColor, Mathf.Lerp(markerAlphaMin, markerAlphaMax, wave));
    }

    private static Material Instanced(Renderer draw)
    {
        if (draw == null)
        {
            return null;
        }

        // renderer.material 은 첫 호출에 사본을 만들어 준다. 그 사본을 붙들고 색만 바꾼다.
        return draw.material;
    }

    /// <summary>URP 는 _BaseColor, 구형 셰이더는 _Color. 있는 쪽에 칠한다.</summary>
    private static void Tint(Material paint, Color color, float alpha)
    {
        if (paint == null)
        {
            return;
        }

        color.a = alpha;

        if (paint.HasProperty(BaseColorId))
        {
            paint.SetColor(BaseColorId, color);
        }

        if (paint.HasProperty(ColorId))
        {
            paint.SetColor(ColorId, color);
        }
    }
}
