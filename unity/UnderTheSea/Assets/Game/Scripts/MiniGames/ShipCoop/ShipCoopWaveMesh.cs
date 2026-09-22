using UnityEngine;

/// <summary>
/// 🌊 거대한 파도 한 장을 **앞으로 말리는 물마루**로 뽑는다. (<see cref="BigWave"/>)
///
/// ⛔ **납작한 네모를 쓰면 안 됩니다. 이미 세 번 실패했습니다.**
///
///    `ShipCoopDeckLayout.UseFoam` 주석("아무리 잘게 줄여도 흰 판때기로 보인다"),
///    <see cref="ShipCoopWake"/> 주석("납작한 사각형을 얹는 방식은 하지 마세요"),
///    그리고 이 파도 자신이 오랫동안 120 × 1.5 × 2m 짜리 큐브였습니다.
///
///    원인은 **크기가 아니라 평면**입니다. 면이 하나뿐이면 빛이 고르게 깔려서
///    아무리 키워도 물이 아니라 판때기입니다. 능선이 곡면이고 위로 갈수록
///    얇아지면, **같은 재질로도** 물처럼 보입니다.
///
/// ⚠ **에셋을 사지 않고 직접 뽑는 이유.**
///
///    에셋 스토어에 "120m 짜리 파도 한 장" 은 없습니다. 물 시스템(바다 표면
///    전체를 그리는 셰이더)이거나 파도 공격 이펙트(스펠 크기)뿐입니다. 사 오면
///    **로비 · 바다 · 파도가 서로 다른 물**이 되는데, 그건 `UseWaterWorks = false`
///    에서 이미 한 번 "바다 색이 로비와 어긋나지 않는 것이 더 중요하다" 고
///    결론 낸 문제입니다. 여기서 뽑으면 바다가 쓰는 재질을 그대로 입힐 수 있습니다.
///
/// ⚠ **에디터 도구가 아니라 실행 중에 만듭니다.**
///
///    <see cref="ShipCoopSeaMesh"/> · <see cref="ShipCoopPuddleMesh"/> 와 다른 점입니다.
///    저쪽은 씬에 한 번 깔고 끝이라 파일로 구워도 되지만, 파도 모양은
///    <see cref="BigWave"/> 의 인스펙터 값(폭 · 높이 · 두께)을 따라야 합니다.
///    구워 두면 그 값들이 아무 일도 안 하게 됩니다.
///
///    대신 <b>한 번 만들고 캐시</b>합니다. 값이 안 변하면 판마다 다시 안 만듭니다.
///
/// <code>
///   단면    Profile 을 한 바퀴 (닫힌 고리 — 뒤에서 봐도 뚫려 있지 않다)
///   훑기    그 단면을 폭만큼 x 축으로 밀면서, 마루 높이와 좌우 위치를 흔든다
///   양끝    TaperMeters 안에서 0 으로 줄어든다. 수직으로 잘려 있으면 벽으로 읽힌다
/// </code>
/// </summary>
public static class ShipCoopWaveMesh
{
    // ------------------------------------------------------------
    // 단면 — 배에서 본 파도의 옆모습
    //
    // ⚠ **−z 가 배 쪽입니다.** 파도는 +z(수평선)에서 배로 옵니다.
    //    그래서 마루가 **−z 로 말려야** 배를 덮치는 것처럼 보입니다.
    //
    //   x  두께의 비율 (−0.5 ~ +0.5). +가 뒤(수평선), −가 앞(배)
    //   y  높이의 비율. 0 이 수면, 1 이 마루.
    //      **음수는 물속 깊이의 비율입니다.** (−1 = waveSink 만큼 잠긴다)
    //
    // ⚠ 순서는 **반시계**여야 합니다. (z 오른쪽 · y 위로 놓고 봤을 때)
    //    뒤집으면 면이 전부 안쪽을 봐서 파도가 통째로 사라집니다.
    //
    // ⚠ 9번(입술 끝)이 13번(앞 수면)보다 **더 앞에 있어야** 합니다.
    //    그래야 마루 밑이 파여서(10~11번) 물이 말리는 것으로 읽힙니다.
    //    다 같은 z 로 두면 앞면이 평면이 되고, 다시 판때기가 됩니다.
    // ------------------------------------------------------------

    private static readonly Vector2[] Profile =
    {
        new Vector2(-0.50f, -1.00f),   //  0  앞 물속
        new Vector2( 0.50f, -1.00f),   //  1  뒤 물속
        new Vector2( 0.48f,  0.10f),   //  2  뒤 수면
        new Vector2( 0.38f,  0.33f),   //  3  뒤 비탈 — 완만하다. 여기가 가파르면 벽이 된다
        new Vector2( 0.25f,  0.56f),   //  4
        new Vector2( 0.10f,  0.77f),   //  5
        new Vector2(-0.06f,  0.92f),   //  6
        new Vector2(-0.22f,  1.00f),   //  7  마루
        new Vector2(-0.38f,  0.98f),   //  8  말리기 시작
        new Vector2(-0.46f,  0.88f),   //  9  입술 끝 — 가장 앞으로 나온 자리
        new Vector2(-0.42f,  0.72f),   // 10  그 아래는 파여 있다 (역경사)
        new Vector2(-0.35f,  0.52f),   // 11
        new Vector2(-0.34f,  0.31f),   // 12
        new Vector2(-0.40f,  0.11f),   // 13  앞 수면
    };

    /// <summary>x 축으로 이만큼마다 단면을 하나 놓는다 (m). 마루의 잔물결이 뭉개지지 않을 만큼.</summary>
    private const float SliceStep = 1.2f;

    private const int LeastSlices = 8;
    private const int MostSlices = 220;

    // ⚠ **양 끝을 0 으로 줄입니다.**
    //
    //    안 줄이면 폭 끝에서 파도가 수직으로 뚝 잘립니다. 그 단면이 화면
    //    가장자리에 걸치면 "여기가 끝이구나 → 돌아가면 되겠다" 로 읽혀서
    //    이 사건의 전제(못 피한다)가 무너집니다. 줄이면 바다로 잦아듭니다.
    //
    //    ⚠ 너무 길게 잡으면 **보이는 자리에서 낮아집니다.** 화면에 걸리는
    //       폭보다 한참 바깥에서만 줄어야 합니다. 파도 폭을 줄일 거면
    //       이 값도 같이 줄이세요.
    private const float TaperMeters = 8f;

    // ------------------------------------------------------------
    // 마루의 높낮이와 사행(蛇行)
    //
    // 주기가 서로 **나누어떨어지지 않아야** 합니다. 37 과 13.3 처럼 어긋난
    // 주기를 겹치면 되풀이되는 자리가 안 보입니다. 40 과 20 으로 두면
    // 20m 마다 같은 마루가 서서 무늬가 눈에 띕니다.
    // ------------------------------------------------------------

    private const float CrestAmountA = 0.10f;
    private const float CrestPeriodA = 37f;
    private const float CrestPhaseA = 0.7f;

    private const float CrestAmountB = 0.06f;
    private const float CrestPeriodB = 13.3f;
    private const float CrestPhaseB = 2.1f;

    /// <summary>마루가 앞뒤로 흔들리는 폭 (m). 일직선이면 자로 그은 것처럼 보인다.</summary>
    private const float MeanderA = 1.2f;
    private const float MeanderPeriodA = 53f;
    private const float MeanderPhaseA = 1.3f;

    private const float MeanderB = 0.5f;
    private const float MeanderPeriodB = 17f;
    private const float MeanderPhaseB = 0.4f;

    /// <summary>UV 한 칸이 몇 m 인지. 물 무늬가 늘어나거나 잘아지지 않게 실제 크기로 깐다.</summary>
    private const float UvMetres = 10f;

    private static Mesh _cached;
    private static Vector4 _cachedShape;

    private static Mesh _overlay;
    private static Vector4 _overlayShape;
    private static float _overlaySwell;

    /// <summary>
    /// 파도 메시를 돌려준다. 같은 모양이면 **다시 만들지 않는다.**
    /// </summary>
    /// <param name="width">좌우 폭 (m). 화면 밖까지 나가야 "못 피한다" 가 읽힌다.</param>
    /// <param name="height">수면에서 마루까지 (m).</param>
    /// <param name="thickness">앞뒤 두께 (m). 높이의 1.5배쯤이면 비탈이 완만해진다.</param>
    /// <param name="sink">수면 아래로 이만큼 잠긴다 (m). 0 이면 밑선이 드러난다.</param>
    public static Mesh GetOrBuild(float width, float height, float thickness, float sink)
    {
        Vector4 shape = new Vector4(width, height, thickness, sink);

        if (_cached != null && _cachedShape == shape)
        {
            return _cached;
        }

        // 값이 바뀌었다. 예전 것은 버린다. 안 버리면 판마다 메시가 쌓인다.
        if (_cached != null)
        {
            Object.Destroy(_cached);
        }

        _cached = Build(width, height, thickness, sink);
        _cachedShape = shape;

        return _cached;
    }

    /// <summary>
    /// 같은 파도를 **살짝 부풀린** 것. 경고 막(<see cref="BigWave"/>)이 파도 겉에 뜨게 하는 데 쓴다.
    /// </summary>
    /// <param name="swell">겉으로 이만큼 부푼다 (m).</param>
    public static Mesh GetOrBuildOverlay(float width, float height, float thickness, float sink, float swell)
    {
        Vector4 shape = new Vector4(width, height, thickness, sink);

        if (_overlay != null && _overlayShape == shape && Mathf.Approximately(_overlaySwell, swell))
        {
            return _overlay;
        }

        if (_overlay != null)
        {
            Object.Destroy(_overlay);
        }

        _overlay = Swell(Build(width, height, thickness, sink), swell);
        _overlayShape = shape;
        _overlaySwell = swell;

        return _overlay;
    }

    /// <summary>
    /// 꼭짓점을 법선 방향으로 밀어 껍질을 만든다.
    ///
    /// ⚠ **같은 자리에 겹쳐 놓으면 두 면이 서로 이기려고 깜빡입니다.** (z-파이팅)
    ///    경고 막은 파도와 완전히 같은 모양이라 이 문제를 그대로 맞습니다.
    ///
    /// ⚠ **크기를 키우는 것으로는 안 됩니다.** 폭 120m · 높이 8m 이라 비율이
    ///    15배 차이 납니다. 0.3% 를 키우면 양 끝은 0.18m 씩 밀려 나가는데
    ///    마루는 1.2cm 밖에 안 뜹니다. 뜨는 곳과 안 뜨는 곳이 생겨서
    ///    **마루에서만 깜빡입니다.** 법선으로 밀면 어디서나 똑같이 뜹니다.
    /// </summary>
    private static Mesh Swell(Mesh source, float swell)
    {
        Vector3[] points = source.vertices;
        Vector3[] normals = source.normals;

        for (int i = 0; i < points.Length; i++)
        {
            points[i] += normals[i] * swell;
        }

        source.vertices = points;
        source.name += "_경고막";

        // 꼭짓점이 움직였으니 접선도 다시. 법선은 모양이 같으므로 그대로 둔다.
        source.RecalculateTangents();

        // ⚠ 꼭짓점을 바꿔도 경계상자가 저절로 따라오지 않습니다. 재면 몸통 것 그대로입니다.
        //    그대로 두면 화면 가장자리에서 막만 먼저 잘려 나갑니다.
        source.RecalculateBounds();

        return source;
    }

    private static Mesh Build(float width, float height, float thickness, float sink)
    {
        int slices = Mathf.Clamp(Mathf.RoundToInt(width / SliceStep), LeastSlices, MostSlices);

        // ⚠ 고리의 첫 점을 **끝에 한 번 더** 놓습니다. 나머지 연산(%)으로 이으면
        //    마지막 띠의 UV 가 끝값이 아니라 0 으로 돌아가서 무늬가 거기서만 늘어납니다.
        int ring = Profile.Length + 1;

        Vector3[] points = new Vector3[(slices + 1) * ring];
        Vector2[] uvs = new Vector2[points.Length];

        float[] alongCut = CutLengths(height, thickness, sink, ring);
        float half = width * 0.5f;

        for (int s = 0; s <= slices; s++)
        {
            float x = (s / (float)slices - 0.5f) * width;

            // 끝에서 0 으로. 높이 · 두께 · 잠김이 한꺼번에 줄어야 한 점으로 닫힌다.
            float taper = Mathf.SmoothStep(0f, 1f, (half - Mathf.Abs(x)) / TaperMeters);

            float lift = height * taper * CrestScale(x);
            float depth = thickness * taper;
            float dip = sink * taper;
            float shift = Meander(x) * taper;

            for (int p = 0; p < ring; p++)
            {
                Vector2 cut = Profile[p % Profile.Length];

                // 음수 y 는 물속 깊이의 비율이다. (위 Profile 주석)
                float y = cut.y >= 0f ? cut.y * lift : cut.y * dip;

                int i = s * ring + p;

                points[i] = new Vector3(x, y, cut.x * depth + shift);
                uvs[i] = new Vector2(x / UvMetres, alongCut[p] / UvMetres);
            }
        }

        int[] tris = new int[slices * (ring - 1) * 6];
        int t = 0;

        for (int s = 0; s < slices; s++)
        {
            for (int p = 0; p < ring - 1; p++)
            {
                int near = s * ring + p;
                int far = (s + 1) * ring + p;

                tris[t++] = near;
                tris[t++] = far;
                tris[t++] = far + 1;

                tris[t++] = near;
                tris[t++] = far + 1;
                tris[t++] = near + 1;
            }
        }

        Mesh mesh = new Mesh { name = $"BigWave_{width:F0}x{height:F1}" };

        mesh.vertices = points;
        mesh.uv = uvs;
        mesh.triangles = tris;

        // 단면이 곡면이라 면마다 법선이 다르다. 이게 파도를 판때기와 갈라놓는 전부다.
        mesh.RecalculateNormals();

        // 물 셰이더가 잔결(노멀맵)을 얹으려면 접선이 있어야 한다. (ShipCoopSeaMesh 와 같은 이유)
        mesh.RecalculateTangents();

        return mesh;
    }

    /// <summary>
    /// 단면을 따라 잰 거리 (m). UV 의 v 로 쓴다.
    ///
    /// 점 번호를 그대로 쓰면 촘촘한 구간(마루)에서 무늬가 뭉치고 성긴 구간
    /// (물속 바닥)에서 늘어납니다. 실제 길이로 깔아야 고르게 보입니다.
    /// 훑는 도중에 taper 로 줄어들긴 하지만, UV 는 기준 크기로 한 번만 잽니다.
    /// </summary>
    private static float[] CutLengths(float height, float thickness, float sink, int ring)
    {
        float[] lengths = new float[ring];
        float run = 0f;

        for (int p = 1; p < ring; p++)
        {
            Vector2 was = Metres(Profile[(p - 1) % Profile.Length], height, thickness, sink);
            Vector2 now = Metres(Profile[p % Profile.Length], height, thickness, sink);

            run += Vector2.Distance(was, now);
            lengths[p] = run;
        }

        return lengths;
    }

    private static Vector2 Metres(Vector2 cut, float height, float thickness, float sink)
    {
        return new Vector2(cut.x * thickness, cut.y >= 0f ? cut.y * height : cut.y * sink);
    }

    /// <summary>마루 높이 배율. 1 을 중심으로 오르내린다.</summary>
    private static float CrestScale(float x)
    {
        return 1f
               + CrestAmountA * Mathf.Sin(x * Mathf.PI * 2f / CrestPeriodA + CrestPhaseA)
               + CrestAmountB * Mathf.Sin(x * Mathf.PI * 2f / CrestPeriodB + CrestPhaseB);
    }

    /// <summary>마루가 앞뒤로 비껴 있는 정도 (m).</summary>
    private static float Meander(float x)
    {
        return MeanderA * Mathf.Sin(x * Mathf.PI * 2f / MeanderPeriodA + MeanderPhaseA)
               + MeanderB * Mathf.Sin(x * Mathf.PI * 2f / MeanderPeriodB + MeanderPhaseB);
    }
}
