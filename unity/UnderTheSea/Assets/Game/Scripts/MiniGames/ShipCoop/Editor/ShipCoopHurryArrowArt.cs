using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 항해 바의 붉은 구간을 가리키는 **아래쪽 삼각형** 한 장을 구워서 저장한다.
///
/// 왜 코드로 굽는가
///   손글씨 화살표 그림이 나오기 전까지 쓰는 **임시 표식**입니다. 자리 · 크기 ·
///   타이밍을 먼저 눈으로 보려고 만든 것이라, 그림 파일을 주고받을 일이 아닙니다.
///   삼각형은 부등식 세 줄이면 되고, 뾰족함도 숫자 하나입니다.
///
/// 색은 <see cref="ShipCoopVignetteArt"/> 와 같은 이유로 **흰색**으로 굽습니다.
/// 쓰는 쪽에서 <c>Image.color</c> 로 물들입니다.
///
/// 가장자리는 픽셀 거리로 한 칸만 부드럽게 폅니다. 안 펴면 비스듬한 변이
/// 계단처럼 보이는데, 22px 로 줄여 놓고 보면 그 계단이 꽤 눈에 띕니다.
///
/// Tools > 아라아띠 > 배 협동 서두르기 화살표 다시 굽기
/// </summary>
public static class ShipCoopHurryArrowArt
{
    public const string Path = "Assets/Game/Art/UI/ShipCoopHudV3/hurry-arrow.png";

    private const int Size = 128;

    /// <summary>가장자리를 펴는 폭(px). 0 이면 계단이 보인다.</summary>
    private const float Feather = 1.5f;

    /// <summary>없으면 굽고, 있으면 그대로 쓴다. HUD 빌더가 부른다.</summary>
    public static Sprite Ensure()
    {
        var found = AssetDatabase.LoadAssetAtPath<Sprite>(Path);
        if (found != null)
        {
            return found;
        }

        Bake();
        return AssetDatabase.LoadAssetAtPath<Sprite>(Path);
    }

    [MenuItem("Tools/아라아띠/배 협동 서두르기 화살표 다시 굽기")]
    public static void Bake()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color32[Size * Size];

        // 꼭짓점 세 개. 텍스처 좌표는 아래가 0 이므로 **아래 가운데가 뾰족한** 삼각형이다.
        var apex = new Vector2(Size * 0.5f, 0f);
        var left = new Vector2(0f, Size);
        var right = new Vector2(Size, Size);

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);

                // 세 변 모두의 안쪽까지 얼마나 남았는지. 가장 빠듯한 변이 가장자리를 정한다.
                float inside = Mathf.Min(
                    EdgeDistance(p, apex, right),
                    Mathf.Min(EdgeDistance(p, right, left), EdgeDistance(p, left, apex)));

                float a = Mathf.Clamp01(inside / Feather + 0.5f);
                pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();

        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path));
        File.WriteAllBytes(Path, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(Path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(Path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;

        TextureImporterPlatformSettings settings = importer.GetDefaultPlatformTextureSettings();
        settings.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SetPlatformTextureSettings(settings);

        importer.SaveAndReimport();

        Debug.Log($"[서두르기 화살표] 구웠다 → {Path}");
    }

    /// <summary>
    /// 변 <c>a→b</c> 의 **왼쪽**까지 남은 거리(px). 음수면 밖이다.
    ///
    /// 세 꼭짓점을 반시계로 넣었으므로 삼각형 안쪽이 모든 변의 왼쪽이 된다.
    /// </summary>
    private static float EdgeDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 edge = b - a;
        float length = edge.magnitude;

        if (length <= Mathf.Epsilon)
        {
            return 0f;
        }

        // 2차원 외적. 변 길이로 나누면 부호 있는 점–직선 거리가 된다.
        return (edge.x * (p.y - a.y) - edge.y * (p.x - a.x)) / length;
    }
}
