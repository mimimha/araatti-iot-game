using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

/// <summary>
/// 상호작용 링 안 **키캡 글꼴**(Fredoka)의 TMP 폰트 에셋을 굽는다.
///
/// 키캡에 찍히는 것은 <c>Space</c> · <c>K</c> · <c>J · L</c> 뿐이라 한글이 필요 없습니다.
/// 키캡은 읽는 "글" 이 아니라 **버튼에 새긴 각인**이라, 본문(NotoSansKR)과 같은 글꼴일
/// 이유가 없습니다. 둥근 링 그림에 맞춰 둥근 글꼴을 따로 씁니다.
///
/// ⚠ **ttf 만으로는 TMP 가 못 씁니다.** TMP 는 글자를 미리 구운 아틀라스(SDF)로 그리므로
///    폰트 에셋(.asset)을 한 번 만들어 둬야 합니다. 그 굽는 일을 여기서 합니다.
///    저장소에는 ttf 만 올리고 에셋은 각자 한 번 구워도 되지만, 지금은 구운 것도
///    같이 올려 뒀습니다 — 안 그러면 받는 사람마다 이 메뉴를 눌러야 합니다.
///
/// ⚠ **아틀라스는 Dynamic 입니다.** 미리 구워 둔 글자에 없는 문자(가운뎃점 `·` 등)가
///    나와도 그때 원본 ttf 에서 채웁니다. Static 으로 바꾸면 그 글자가 네모로 나옵니다.
///
/// 라이선스: SIL Open Font License 1.1 (Copyright 2016 The Fredoka Project Authors).
/// 전문은 <c>Assets/Game/Fonts/Fredoka-OFL.txt</c>, 출처 기록은 <c>ASSETS.md</c> 에 있습니다.
/// 게임에 끼워 파는 것은 허용됩니다 — 글꼴 자체를 따로 파는 것만 안 됩니다.
///
/// Tools > 아라아띠 > 배 협동 키캡 글꼴 굽기
/// </summary>
public static class ShipCoopKeyFontInstaller
{
    // ⚠ **가변 글꼴(VariableFont)이 아니라 굵기가 박힌 파일이다.**
    //
    //    가변 글꼴로 구우면 TMP 가 기본 굵기(Regular 400)만 굽는다. 실제로 그렇게
    //    했다가 키캡이 너무 얇아서 Bold(usWeightClass 700) 한 벌로 다시 받았다.
    private const string SourcePath = "Assets/Game/Fonts/Fredoka-Bold.ttf";

    /// <summary>구워진 폰트 에셋 자리. <see cref="ShipCoopHudV2Art"/> 가 이 경로를 본다.</summary>
    public const string OutputPath = "Assets/Game/Fonts/Fredoka SDF.asset";

    // 아틀라스를 굽는 값. 라틴 글자 한 벌뿐이라 512 로 충분하다.
    private const int SamplingPointSize = 90;
    private const int Padding = 9;
    private const int AtlasSize = 512;

    [MenuItem("Tools/아라아띠/배 협동 키캡 글꼴 굽기")]
    public static void Apply()
    {
        var source = AssetDatabase.LoadAssetAtPath<Font>(SourcePath);

        if (source == null)
        {
            Debug.LogError($"[키캡 글꼴] 원본 ttf 를 못 찾았습니다 — {SourcePath}");
            return;
        }

        TMP_FontAsset font = TMP_FontAsset.CreateFontAsset(
            source, SamplingPointSize, Padding, GlyphRenderMode.SDFAA,
            AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic);

        if (font == null)
        {
            Debug.LogError($"[키캡 글꼴] 폰트 에셋을 못 만들었습니다 — {SourcePath}");
            return;
        }

        font.name = System.IO.Path.GetFileNameWithoutExtension(OutputPath);

        // 이미 있으면 덮어쓴다. 지웠다 새로 만들면 이 에셋을 가리키던 곳이 전부 끊긴다.
        AssetDatabase.DeleteAsset(OutputPath);
        AssetDatabase.CreateAsset(font, OutputPath);

        // ⚠ **아틀라스 텍스처와 재질을 자식으로 넣는다.** 안 넣으면 에셋만 저장되고
        //    그림과 재질은 메모리에만 남아, 유니티를 다시 켜면 글자가 안 보인다.
        if (font.atlasTextures != null && font.atlasTextures.Length > 0)
        {
            font.atlasTextures[0].name = font.name + " Atlas";
            AssetDatabase.AddObjectToAsset(font.atlasTextures[0], font);
        }

        if (font.material != null)
        {
            font.material.name = font.name + " Material";
            AssetDatabase.AddObjectToAsset(font.material, font);
        }

        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[키캡 글꼴] 구웠습니다 → {OutputPath}\n" +
                  "이제 Tools > 아라아띠 > 배 협동 HUD 를 V2 로 다시 짓기 를 돌리면 키캡에 들어갑니다.", font);
    }
}
