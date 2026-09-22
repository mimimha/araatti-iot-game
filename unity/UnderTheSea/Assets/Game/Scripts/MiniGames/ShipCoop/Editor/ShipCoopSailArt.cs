using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ⛵ 우리 배의 **미색 돛 텍스처 · 재질**을 만든다. (SHIPCOOP.md 5장)
///
/// <b>왜.</b> 적선(<c>P_EnemyShip</c>)이 우리 배와 같은 모델이다. 둘을 가르는 유일한 표식이 돛 색이다 —
/// 우리는 미색 · 문양 없음, 적선은 검은 해골 돛 그대로.
///
/// <b>원본은 절대 고치지 않는다.</b> <c>T_Ship_SailsRope_01_BC.png</c> · <c>M_Ship_SailsRope_01.mat</c> 은 적선과 로비 배가 쓴다.
/// 여기서는 <b>사본</b>만 만든다.
///
/// <code>
///   텍스처   Art/Textures/ShipCoop/T_Ship_SailsRope_White_01_BC.png
///   재질     Art/Materials/ShipCoop/M_Ship_SailsRope_White_01.mat   (원본 mat 복제, 텍스처만 교체)
/// </code>
///
/// <b>어디를 칠하나.</b> 돛 · 로프 · 깃발이 한 아틀라스다. 로프는 건드리면 안 된다. 그래서 배 모델에서
/// 이름에 Sail · Flag 가 든 메시의 <b>UV 삼각형을 그대로 래스터라이즈</b>해 마스크를 만들고 그 안만 칠한다.
/// (UV 바운딩 박스로 하면 아틀라스에서 옆에 붙은 로프 조각이 같이 칠해진다) 메시 정점은 FBX 의 Read/Write 가
/// 켜져 있어야 읽힌다 — 꺼져 있으면 잠깐 켜고 <b>반드시 되돌린다.</b>
///
/// <b>무엇으로 칠하나.</b> 미색 (0.92, 0.90, 0.85). 순백은 하늘돔 앞에서 타 버린다. 천의 음영은 <b>원본 BC 의 명도</b>를
/// 0.6 ~ 1.0 으로 눌러 곱한다 (천 판 줄무늬 · 아랫단 어두운 띠가 살아남는다). 단색으로 채우면 종이처럼 보인다.
/// 해골 · 칼 · 뼈 문양은 밝은 선이라 명도로 걸러 <b>주변 천의 명도로 메운다</b> — 문양이 음영으로 남지 않는다.
/// (AO 텍스처로 하려 했는데 이 배의 AO 는 거의 흰 한 장이라 음영이 없었다)
///
/// UV 를 못 읽으면 대안: 마스크 없이 검은 픽셀(RGB 전부 0.25 미만)과 해골의 흰 픽셀(전부 0.85 초과)을 칠한다.
/// 로프는 갈색이라 안 걸린다. 어느 길로 갔는지 로그에 남긴다.
/// </summary>
public static class ShipCoopSailArt
{
    private const string SourceTexturePath = "Assets/Game/Prefabs/PirateShip/T_Ship_SailsRope_01_BC.png";
    private const string SourceMaterialPath = "Assets/Game/Prefabs/PirateShip/M_Ship_SailsRope_01.mat";
    private const string ShipModelPath = "Assets/Stylized_Pirate_Ship/StylShip_Unity.prefab";

    public const string TexturePath = "Assets/Game/Art/Textures/ShipCoop/T_Ship_SailsRope_White_01_BC.png";
    public const string MaterialPath = "Assets/Game/Art/Materials/ShipCoop/M_Ship_SailsRope_White_01.mat";

    /// <summary>미색. 순백(1,1,1)은 하늘돔 앞에서 타 버린다.</summary>
    private static readonly Color Cream = new Color(0.92f, 0.90f, 0.85f, 1f);

    /// <summary>천의 음영 범위. 원본 천의 가장 어두운 곳이 0.6, 가장 밝은 곳이 1.0.</summary>
    private const float ShadeMin = 0.6f;
    private const float ShadeMax = 1.0f;

    /// <summary>
    /// 이 명도 이상이면 천이 아니라 문양(해골 · 칼 · 뼈 — 흰 선)이다. 검은 천은 0.04 ~ 0.13 (2~98% 백분위).
    /// 0.45 로 했을 때 선의 안티에일리어싱 가장자리(0.2~0.4)가 천으로 남아 문양이 흐릿하게 비쳤다.
    /// </summary>
    private const float SkullLuminance = 0.2f;

    /// <summary>문양 마스크를 이만큼 넓힌다(px). 선 가장자리의 반투명 픽셀까지 같이 메운다.</summary>
    private const int PatternGrow = 2;

    /// <summary>문양을 메울 때 볼 주변 반지름(px). 문양 선이 5px 안팎 + 넓힌 2px 이라 이만큼이면 양옆 천이 잡힌다. (같은 열에서 못 찾았을 때)</summary>
    private const int InpaintRadius = 9;

    /// <summary>같은 열에서 위아래로 이만큼(px) 천을 찾는다. 해골 · 칼 문양은 세로로 200px 이 넘지 않는다.</summary>
    private const int ColumnReach = 160;

    [MenuItem("Tools/ShipCoop/흰 돛 텍스처 만들기")]
    public static void MakeFromMenu()
    {
        StringBuilder log = new StringBuilder();
        log.AppendLine("[흰 돛 텍스처]");
        Material made = CreateOrOverwrite(log);
        Debug.Log(log.ToString() + (made != null ? $"→ {MaterialPath}" : "→ 실패"));
    }

    /// <summary>텍스처와 재질을 만들어(있으면 덮어써) 재질을 돌려준다. 실패하면 null.</summary>
    public static Material CreateOrOverwrite(StringBuilder log)
    {
        Texture2D source = LoadPng(SourceTexturePath);

        if (source == null)
        {
            log.AppendLine($"  ⚠ 원본 돛 텍스처를 못 읽음: {SourceTexturePath}");
            return null;
        }

        int w = source.width;
        int h = source.height;

        bool[] mask = new bool[w * h];
        int meshes = RasterizeSailUvs(mask, w, h, log);
        bool byUv = meshes > 0;

        Color[] pixels = source.GetPixels();

        if (!byUv)
        {
            // 대안 — 색으로 고른다. 검은 천과 해골의 흰 부분. 로프(갈색)는 둘 다 아니다.
            int picked = 0;

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = pixels[i];
                bool dark = c.r < 0.25f && c.g < 0.25f && c.b < 0.25f;
                bool skull = c.r > 0.85f && c.g > 0.85f && c.b > 0.85f;

                if (c.a > 0.01f && (dark || skull))
                {
                    mask[i] = true;
                    picked++;
                }
            }

            log.AppendLine($"  UV 를 못 읽어 색으로 골랐습니다 — 검은 · 흰 픽셀 {picked}개");
        }

        // ------------------------------------------------------------
        // 음영 — 원본 BC 의 명도. 천 판 줄무늬 · 아랫단 어두운 띠가 여기 있다.
        //
        // ⚠ AO 로 하려 했는데(페인트가 없어 해골이 안 남으니) 이 AO 는 거의 흰 한 장이라 음영이 없었다.
        //    그래서 BC 명도를 쓰고, 해골 문양(밝은 선)은 **주변 천의 명도로 메운다.** 선이 5px 안팎이라
        //    반지름 InpaintRadius 창에서 어두운(천) 픽셀만 평균하면 감쪽같다.
        //    천의 명도 범위는 2% ~ 98% 백분위로 잡아 0.6 ~ 1.0 으로 펼친다 — 튀는 픽셀 하나에 범위가 안 흔들린다.
        // ------------------------------------------------------------
        float[] lum = new float[pixels.Length];
        var cloth = new List<float>();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            lum[i] = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

            if (mask[i] && lum[i] < SkullLuminance)
            {
                cloth.Add(lum[i]);
            }
        }

        cloth.Sort();
        float clothMin = cloth.Count > 0 ? cloth[(int)(cloth.Count * 0.02f)] : 0f;
        float clothMax = cloth.Count > 0 ? cloth[Mathf.Min(cloth.Count - 1, (int)(cloth.Count * 0.98f))] : 1f;
        float clothMid = cloth.Count > 0 ? cloth[cloth.Count / 2] : 0.5f;

        if (clothMax - clothMin < 0.01f)
        {
            clothMax = clothMin + 0.01f;
        }

        // 문양 마스크 — 밝은 픽셀과 그 주변 PatternGrow 픽셀. 가장자리 반투명 픽셀까지 메워야 선이 안 비친다.
        bool[] pattern = new bool[pixels.Length];

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;

                if (!mask[i] || lum[i] < SkullLuminance)
                {
                    continue;
                }

                for (int dy = -PatternGrow; dy <= PatternGrow; dy++)
                {
                    for (int dx = -PatternGrow; dx <= PatternGrow; dx++)
                    {
                        int nx = x + dx, ny = y + dy;

                        if (nx >= 0 && nx < w && ny >= 0 && ny < h && mask[ny * w + nx])
                        {
                            pattern[ny * w + nx] = true;
                        }
                    }
                }
            }
        }

        int painted = 0;
        int inpainted = 0;

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * w + x;

                if (!mask[i])
                {
                    continue;
                }

                float l = lum[i];

                if (pattern[i])
                {
                    // 해골 · 칼 · 뼈 문양. 주변 천으로 메운다.
                    l = NeighbourCloth(lum, mask, pattern, w, h, x, y, clothMid);
                    inpainted++;
                }

                float shade = Mathf.Lerp(ShadeMin, ShadeMax, Mathf.InverseLerp(clothMin, clothMax, l));

                Color c = Cream * shade;
                c.a = pixels[i].a;
                pixels[i] = c;
                painted++;
            }
        }

        string shadeNote = $"BC 명도 {clothMin:F2}~{clothMax:F2} → {ShadeMin}~{ShadeMax}, 문양 {inpainted}픽셀을 주변 천으로 메움";

        // 남은 검은 조각 — 마스크 밖에 있는 아주 어두운 픽셀 수. 돛인데 마스크가 못 덮은 곳이 있으면 여기 잡힌다.
        int darkLeft = 0;

        for (int i = 0; i < pixels.Length; i++)
        {
            Color c = pixels[i];
            if (c.a > 0.5f && c.r < 0.12f && c.g < 0.12f && c.b < 0.12f) darkLeft++;
        }

        Texture2D result = new Texture2D(w, h, TextureFormat.RGBA32, false);
        result.SetPixels(pixels);
        result.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(TexturePath)!);
        File.WriteAllBytes(TexturePath, result.EncodeToPNG());
        Object.DestroyImmediate(result);
        Object.DestroyImmediate(source);

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        CopyImportSettings(SourceTexturePath, TexturePath);

        Material material = CreateOrUpdateMaterial();

        log.AppendLine($"  돛 · 깃발 {(byUv ? $"메시 {meshes}개의 UV 영역" : "색으로 고른 영역")} {painted}픽셀을 미색으로 칠했습니다 " +
                       $"({shadeNote}). " +
                       $"칠한 뒤 남은 아주 어두운 픽셀 {darkLeft}개 (돛 UV 밖 — 돛단 테두리 · 로프 그림자) → {TexturePath}");

        return material;
    }

    // ------------------------------------------------------------
    // UV 마스크
    // ------------------------------------------------------------

    /// <summary>배 모델에서 Sail · Flag 메시의 UV 삼각형을 마스크에 채운다. 처리한 메시 수를 돌려준다.</summary>
    private static int RasterizeSailUvs(bool[] mask, int w, int h, StringBuilder log)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ShipModelPath);

        if (model == null)
        {
            log.AppendLine($"  ⚠ 배 모델을 못 찾음: {ShipModelPath}");
            return 0;
        }

        // Read/Write 가 꺼진 FBX 는 정점을 안 준다. 잠깐 켜고 되돌린다.
        var toggled = new List<ModelImporter>();
        int done = 0;

        try
        {
            foreach (MeshFilter part in model.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = part.name.ToLowerInvariant();

                if (!n.Contains("sail") && !n.Contains("flag"))
                {
                    continue;
                }

                Renderer draw = part.GetComponent<Renderer>();

                // 돛 아틀라스를 쓰는 메시만. 다른 아틀라스의 좌표를 여기 칠하면 엉뚱한 곳이 하얘진다.
                if (draw == null || draw.sharedMaterial == null || !draw.sharedMaterial.name.Contains("SailsRope"))
                {
                    continue;
                }

                Mesh mesh = part.sharedMesh;

                if (mesh == null)
                {
                    continue;
                }

                if (!mesh.isReadable)
                {
                    string meshPath = AssetDatabase.GetAssetPath(mesh);

                    if (AssetImporter.GetAtPath(meshPath) is ModelImporter importer && !importer.isReadable)
                    {
                        importer.isReadable = true;
                        importer.SaveAndReimport();
                        toggled.Add(importer);
                        mesh = part.sharedMesh;
                    }
                }

                if (!mesh.isReadable)
                {
                    log.AppendLine($"  ⚠ {part.name} 의 정점을 읽지 못했습니다");
                    continue;
                }

                Vector2[] uv = mesh.uv;
                int[] tris = mesh.triangles;

                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    FillTriangle(mask, w, h, uv[tris[t]], uv[tris[t + 1]], uv[tris[t + 2]]);
                }

                done++;
            }
        }
        finally
        {
            // ⚠ 반드시 되돌린다. 켜 둔 채 두면 메시가 메모리에 두 벌 남는다.
            foreach (ModelImporter importer in toggled)
            {
                importer.isReadable = false;
                importer.SaveAndReimport();
            }

            if (toggled.Count > 0)
            {
                log.AppendLine($"  FBX Read/Write 를 잠깐 켰다가 되돌렸습니다 ({toggled.Count}개)");
            }
        }

        return done;
    }

    /// <summary>UV 삼각형 하나를 픽셀로. 가장자리에 1픽셀 여유를 둬 이음새가 남지 않게 한다.</summary>
    private static void FillTriangle(bool[] mask, int w, int h, Vector2 a, Vector2 b, Vector2 c)
    {
        Vector2 pa = new Vector2(Frac(a.x) * w, Frac(a.y) * h);
        Vector2 pb = new Vector2(Frac(b.x) * w, Frac(b.y) * h);
        Vector2 pc = new Vector2(Frac(c.x) * w, Frac(c.y) * h);

        int minX = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.x, pb.x, pc.x)) - 1, 0, w - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.x, pb.x, pc.x)) + 1, 0, w - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Mathf.Min(pa.y, pb.y, pc.y)) - 1, 0, h - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Mathf.Max(pa.y, pb.y, pc.y)) + 1, 0, h - 1);

        float area = Edge(pa, pb, pc);

        if (Mathf.Abs(area) < 1e-6f)
        {
            return;
        }

        // 1픽셀 여유 — 삼각형 밖이라도 가장자리에서 1픽셀 안이면 채운다.
        float pad = 1.5f;

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);

                float w0 = Edge(pb, pc, p) / area;
                float w1 = Edge(pc, pa, p) / area;
                float w2 = Edge(pa, pb, p) / area;

                if (w0 >= 0f && w1 >= 0f && w2 >= 0f)
                {
                    mask[y * w + x] = true;
                    continue;
                }

                if (DistanceToSegment(p, pa, pb) <= pad || DistanceToSegment(p, pb, pc) <= pad || DistanceToSegment(p, pc, pa) <= pad)
                {
                    mask[y * w + x] = true;
                }
            }
        }
    }

    private static float Frac(float v)
    {
        v -= Mathf.Floor(v);
        return v;
    }

    private static float Edge(Vector2 a, Vector2 b, Vector2 p)
    {
        return (b.x - a.x) * (p.y - a.y) - (b.y - a.y) * (p.x - a.x);
    }

    private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float len = ab.sqrMagnitude;
        float t = len > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / len) : 0f;
        return Vector2.Distance(p, a + ab * t);
    }

    // ------------------------------------------------------------
    // 파일
    // ------------------------------------------------------------

    /// <summary>임포트 설정과 무관하게 PNG 를 그대로 읽는다.</summary>
    private static Texture2D LoadPng(string assetPath)
    {
        string full = Path.Combine(Directory.GetCurrentDirectory(), assetPath);

        if (!File.Exists(full))
        {
            return null;
        }

        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(File.ReadAllBytes(full)))
        {
            Object.DestroyImmediate(texture);
            return null;
        }

        return texture;
    }

    /// <summary>문양 픽셀 주변의 천 명도 평균. 주변에 천이 없으면(문양이 두꺼우면) 천의 중간값.</summary>
    private static float NeighbourCloth(float[] lum, bool[] mask, bool[] pattern, int w, int h, int cx, int cy, float fallback)
    {
        float sum = 0f;
        float sumWeight = 0f;
        int count = 0;

        // 천 무늬가 **세로 줄무늬**다. 같은 열(x ±1)에서 위아래로 먼저 찾으면 줄무늬가 이어져 문양 자리가 티 안 난다.
        // 네모 창으로 평균하면 옆 줄무늬 값이 섞여 문양 모양의 흐릿한 얼룩이 남았다.
        for (int dy = -ColumnReach; dy <= ColumnReach; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h) continue;

            for (int dx = -1; dx <= 1; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= w) continue;

                int i = y * w + x;

                if (mask[i] && !pattern[i])
                {
                    // 가까운 것에 더 무게. 멀리서 온 값이 가까운 값을 덮지 않게.
                    float weight = 1f / (1f + Mathf.Abs(dy) * 0.1f);
                    sum += lum[i] * weight;
                    count++;
                    sumWeight += weight;
                }
            }
        }

        if (count >= 4)
        {
            return sum / sumWeight;
        }

        sum = 0f;
        count = 0;

        for (int dy = -InpaintRadius; dy <= InpaintRadius; dy++)
        {
            int y = cy + dy;
            if (y < 0 || y >= h) continue;

            for (int dx = -InpaintRadius; dx <= InpaintRadius; dx++)
            {
                int x = cx + dx;
                if (x < 0 || x >= w) continue;

                int i = y * w + x;

                if (mask[i] && !pattern[i])
                {
                    sum += lum[i];
                    count++;
                }
            }
        }

        return count > 0 ? sum / count : fallback;
    }

    /// <summary>원본과 같은 임포트 설정. sRGB 켬.</summary>
    private static void CopyImportSettings(string fromPath, string toPath)
    {
        if (!(AssetImporter.GetAtPath(fromPath) is TextureImporter from) ||
            !(AssetImporter.GetAtPath(toPath) is TextureImporter to))
        {
            return;
        }

        TextureImporterSettings settings = new TextureImporterSettings();
        from.ReadTextureSettings(settings);
        to.SetTextureSettings(settings);

        to.textureType = from.textureType;
        to.sRGBTexture = true;
        to.alphaIsTransparency = from.alphaIsTransparency;
        to.mipmapEnabled = from.mipmapEnabled;
        to.maxTextureSize = from.maxTextureSize;
        to.textureCompression = from.textureCompression;
        to.filterMode = from.filterMode;
        to.wrapMode = from.wrapMode;
        to.isReadable = false;
        to.SaveAndReimport();
    }

    /// <summary>원본 재질을 복제해 텍스처만 바꾼다. 있으면 텍스처 참조만 다시 맞춘다.</summary>
    private static Material CreateOrUpdateMaterial()
    {
        Material made = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (made == null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath)!);

            if (!AssetDatabase.CopyAsset(SourceMaterialPath, MaterialPath))
            {
                return null;
            }

            made = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        }

        Texture2D white = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        if (made != null && white != null)
        {
            if (made.HasProperty("_BaseMap")) made.SetTexture("_BaseMap", white);
            if (made.HasProperty("_MainTex")) made.SetTexture("_MainTex", white);
            EditorUtility.SetDirty(made);
            AssetDatabase.SaveAssets();
        }

        return made;
    }
}
