using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 발밑에 깔 **둥근 그림자** 그림과 재질을 만든다.
///
/// 왜 그려서 만드는가
///   납작한 네모를 깔면 바닥에 네모난 자국이 남습니다. 물거품에서 겪은 그대로입니다.
///   가장자리로 갈수록 흐려지는 **동그란 얼룩**이어야 그림자로 보입니다.
///   그림 파일 하나를 받아 올 수도 있지만, 픽셀 몇 줄로 만들 수 있는 것이라
///   여기서 만들어 둡니다. 에셋 목록에 짐이 하나 줄어듭니다.
///
/// 이미 있으면 다시 만들지 않습니다. 색이나 흐림 정도를 바꾸고 싶으면
/// 파일을 지우고 다시 돌리면 됩니다.
/// </summary>
public static class ShipCoopBlobShadow
{
    private const string Folder = "Assets/Game/Art/Materials/ShipCoop";
    private const string TexturePath = Folder + "/BlobShadow.png";
    private const string MaterialPath = Folder + "/BlobShadow.mat";

    private const int Size = 128;

    /// <summary>가운데의 짙기. 1 이면 새까맣다.</summary>
    private const float Darkest = 0.45f;

    /// <summary>이 비율 안쪽은 고르게 짙다. 바깥으로 갈수록 흐려진다.</summary>
    private const float SolidPart = 0.35f;

    /// <summary>둥근 그림자 재질. 없으면 만들어서 돌려준다.</summary>
    public static Material GetOrCreate()
    {
        Material found = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (found != null)
        {
            return found;
        }

        Texture2D texture = GetOrCreateTexture();

        if (texture == null)
        {
            return null;
        }

        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");

        if (unlit == null)
        {
            Debug.LogError("[ShipCoopBlobShadow] URP Unlit 셰이더를 찾지 못했습니다.");
            return null;
        }

        Material made = new Material(unlit);
        made.name = "BlobShadow";

        // ⚠ **반투명은 칸 하나로 안 됩니다.**
        //
        //    `_Surface` 만 1 로 바꿔도 셰이더는 여전히 불투명하게 그립니다.
        //    섞는 방식(SrcBlend · DstBlend)과 키워드까지 같이 줘야 합니다.
        //    처음에 그걸 빼먹어서 **검은 딱지**가 되거나 아예 안 보였습니다.
        made.SetFloat("_Surface", 1f);                                    // 0 불투명, 1 반투명
        made.SetFloat("_Blend", 0f);                                      // Alpha
        made.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        made.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        made.SetFloat("_ZWrite", 0f);
        made.SetFloat("_AlphaClip", 0f);

        // 0 = 양면. 눕힌 판이 뒤집혀도 보이게. 한 면만 그리면 위에서 안 보인다.
        made.SetFloat("_Cull", 0f);

        made.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        made.DisableKeyword("_ALPHATEST_ON");
        made.SetOverrideTag("RenderType", "Transparent");
        made.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        made.SetTexture("_BaseMap", texture);
        made.SetColor("_BaseColor", Color.white);

        AssetDatabase.CreateAsset(made, MaterialPath);
        AssetDatabase.SaveAssets();

        return made;
    }

    private static Texture2D GetOrCreateTexture()
    {
        Texture2D found = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);

        if (found != null)
        {
            return found;
        }

        if (!Directory.Exists(Folder))
        {
            Directory.CreateDirectory(Folder);
        }

        Texture2D made = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        float half = Size * 0.5f;

        for (int y = 0; y < Size; y++)
        {
            for (int x = 0; x < Size; x++)
            {
                // 가운데에서 얼마나 떨어졌는지 (0 가운데 ~ 1 가장자리)
                float dx = (x + 0.5f - half) / half;
                float dy = (y + 0.5f - half) / half;
                float away = Mathf.Sqrt(dx * dx + dy * dy);

                // 안쪽은 고르게 짙고, 바깥으로 갈수록 부드럽게 사라진다.
                float alpha = 1f - Mathf.InverseLerp(SolidPart, 1f, away);

                // 제곱해서 가장자리를 더 흐리게. 각진 테두리가 안 보인다.
                alpha = alpha * alpha * Darkest;

                made.SetPixel(x, y, new Color(0f, 0f, 0f, alpha));
            }
        }

        made.Apply();

        File.WriteAllBytes(TexturePath, made.EncodeToPNG());
        Object.DestroyImmediate(made);

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);

        TextureImporter importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;

        if (importer != null)
        {
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;   // 가장자리가 반대편으로 번지지 않게
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }

        return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
    }
}
