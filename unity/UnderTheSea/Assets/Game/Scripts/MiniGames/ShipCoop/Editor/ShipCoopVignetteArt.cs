using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 화면 가장자리에 까는 **비네트** 한 장을 구워서 저장한다.
///
/// 왜 코드로 굽는가
///   가장자리 그라데이션은 그림이라기보다 **수식**입니다. 그림 파일로 들고 있으면
///   두께를 조금 바꾸고 싶을 때마다 다시 그려 받아야 합니다. 여기서는 숫자 두 개입니다.
///
/// 색은 **흰색**으로 굽습니다. 쓰는 쪽에서 <c>Image.color</c> 로 물들입니다.
/// 그래야 경고(빨강) 말고 다른 곳에도 같은 장을 돌려쓸 수 있습니다.
///
/// 가장자리까지의 거리를 <c>max(|x|, |y|)</c> 로 재기 때문에 **네모난 비네트**가 됩니다.
/// 원형으로 재면 16:9 로 늘였을 때 위아래만 두꺼워져서 화면이 눌린 것처럼 보입니다.
///
/// Tools > 아라아띠 > 배 협동 경고 비네트 다시 굽기
/// </summary>
public static class ShipCoopVignetteArt
{
    public const string Path = "Assets/Game/Art/UI/ShipCoopHudV3/vignette-edge.png";

    private const int Size = 1024;

    /// <summary>안쪽 몇 할까지는 완전히 투명한가. 올리면 테두리가 얇아진다.</summary>
    private const float ClearUntil = 0.88f;

    /// <summary>가장자리로 갈수록 몰리는 정도. 올리면 끝에서만 진해진다.</summary>
    private const float Falloff = 2.0f;

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

    [MenuItem("Tools/아라아띠/배 협동 경고 비네트 다시 굽기")]
    public static void Bake()
    {
        var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var pixels = new Color32[Size * Size];

        for (int y = 0; y < Size; y++)
        {
            float ny = y / (Size - 1f) * 2f - 1f;

            for (int x = 0; x < Size; x++)
            {
                float nx = x / (Size - 1f) * 2f - 1f;

                // 화면 가장자리까지의 거리. 네모로 재야 늘였을 때 두께가 고르다.
                float edge = Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny));

                float t = Mathf.InverseLerp(ClearUntil, 1f, edge);
                float a = Mathf.Pow(Mathf.SmoothStep(0f, 1f, t), Falloff);

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

        Debug.Log($"[비네트] 구웠다 → {Path}");
    }
}
