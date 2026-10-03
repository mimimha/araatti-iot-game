using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// "늦었다" 팻말 글씨에 씌우는 **검정 그림자 머티리얼**을 만들어 둔다.
///
/// 왜 머티리얼인가
///   TMP 는 UI 의 Shadow · Outline 컴포넌트를 **무시합니다.** 제 메시를 직접 굽기
///   때문에 <c>BaseMeshEffect</c> 가 끼어들 자리가 없습니다. 어두운 글씨를 한 장 더
///   까는 방법도 있지만, 그러면 글자가 바뀔 때마다 두 장을 같이 고쳐야 합니다.
///   TMP 가 원래 들고 있는 **Underlay(그림자)** 를 켜면 글자는 하나로 두고도
///   그림자가 알아서 따라옵니다.
///
///   <c>fontMaterial</c>(인스턴스) 이 아니라 **에셋으로 구워서
///   <c>fontSharedMaterial</c>** 에 꽂습니다. 인스턴스 머티리얼은 프리팹 안에 숨은 채로
///   저장돼서 나중에 어디서 왔는지 찾기가 어렵습니다.
///
/// ⚠ **글꼴의 기본 머티리얼을 복사해서** 만듭니다. 그래야 <c>_MainTex</c> 가 그 글꼴의
///    아틀라스를 가리킵니다. 빈 머티리얼에 셰이더만 꽂으면 글자가 안 나옵니다.
///    글꼴이 바뀌면 아틀라스도 달라지므로, 이미 있는 것이 지금 글꼴과 안 맞으면 다시 굽습니다.
/// </summary>
public static class ShipCoopHurryTextShadow
{
    public const string Path = "Assets/Game/Art/UI/ShipCoopHudV3/hurry-text-shadow.mat";

    /// <summary>그림자 색. 갑판이 밝아서 꽤 진해야 주황 글씨가 뜬다.</summary>
    private static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.85f);

    // ── TMP Distance Field 셰이더의 Underlay 값들 ──
    //
    // 이름을 문자열로 적는다. `ShaderUtilities.ID_...` 상수도 있지만, 셰이더 프로퍼티
    // 이름은 TMP 가 바꾼 적이 없고 이렇게 적으면 무엇을 건드리는지 바로 읽힌다.
    //
    // 밀어내는 양은 **글자 크기에 대한 비율**(-1 ~ 1)이다. px 가 아니라서 글씨를
    // 키우고 줄여도 그림자 비율이 그대로 따라온다.
    private const string KeywordUnderlay = "UNDERLAY_ON";
    private const float OffsetX = 0.12f;   // 오른쪽으로
    private const float OffsetY = -0.12f;  // 아래로

    /// <summary>번지는 정도. 0 이면 칼같이 떨어지고, 올리면 부드러워진다.</summary>
    private const float Softness = 0.1f;

    /// <summary>그림자를 글자보다 얼마나 살찌울 것인가. 올리면 두꺼워진다.</summary>
    private const float Dilate = 0.1f;

    /// <summary>없거나 글꼴이 안 맞으면 굽고, 맞으면 그대로 쓴다. HUD 빌더가 부른다.</summary>
    public static Material Ensure(TMP_FontAsset font)
    {
        if (font == null || font.material == null)
        {
            Debug.LogWarning("[서두르기 팻말] 글꼴이 없어 그림자를 못 만든다. 그림자 없이 간다.");
            return null;
        }

        var found = AssetDatabase.LoadAssetAtPath<Material>(Path);

        // 같은 아틀라스를 보고 있으면 그대로 쓴다. 글꼴이 바뀌었으면 글자가 깨지므로 다시 굽는다.
        if (found != null && found.mainTexture == font.material.mainTexture)
        {
            return found;
        }

        var material = new Material(font.material) { name = "hurry-text-shadow" };

        material.EnableKeyword(KeywordUnderlay);
        material.SetColor("_UnderlayColor", ShadowColor);
        material.SetFloat("_UnderlayOffsetX", OffsetX);
        material.SetFloat("_UnderlayOffsetY", OffsetY);
        material.SetFloat("_UnderlaySoftness", Softness);
        material.SetFloat("_UnderlayDilate", Dilate);

        if (found != null)
        {
            AssetDatabase.DeleteAsset(Path);
        }

        AssetDatabase.CreateAsset(material, Path);
        AssetDatabase.SaveAssets();

        Debug.Log($"[서두르기 팻말] 그림자 머티리얼을 구웠다 ({font.name}) → {Path}");
        return material;
    }
}
