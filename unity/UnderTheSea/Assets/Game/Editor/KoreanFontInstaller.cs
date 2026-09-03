using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace UnderTheSea.Editor
{
    /// <summary>
    /// Creates a dynamic Korean TMP font asset and registers it as a global fallback.
    /// The source OTF stays in the build so characters can be added to the atlas at runtime.
    /// </summary>
    [InitializeOnLoad]
    public static class KoreanFontInstaller
    {
        private const string SourceFontPath = "Assets/Game/Fonts/NotoSansKR-Bold.otf";
        private const string FontAssetPath = "Assets/Game/Fonts/NotoSansKR-Bold SDF.asset";
        private const string TmpSettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";
        private const string KoreanSmokeTest = "게임 시작 설정 종료";

        static KoreanFontInstaller()
        {
            EditorApplication.delayCall += InstallIfNeeded;
        }

        [MenuItem("Tools/Under The Sea/Install Korean TMP Font")]
        public static void Install()
        {
            InstallInternal(logSuccess: true);
        }

        private static void InstallIfNeeded()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.IsAssetImportWorkerProcess())
            {
                return;
            }

            InstallInternal(logSuccess: false);
        }

        private static void InstallInternal(bool logSuccess)
        {
            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                Debug.LogWarning($"Korean font source was not found at {SourceFontPath}.");
                return;
            }

            EnsureFontDataIsIncluded();

            TMP_FontAsset fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontAssetPath);
            bool created = false;

            if (fontAsset == null)
            {
                FontEngine.InitializeFontEngine();
                fontAsset = TMP_FontAsset.CreateFontAsset(
                    sourceFont,
                    samplingPointSize: 90,
                    atlasPadding: 9,
                    renderMode: GlyphRenderMode.SDFAA,
                    atlasWidth: 2048,
                    atlasHeight: 2048,
                    atlasPopulationMode: AtlasPopulationMode.Dynamic,
                    enableMultiAtlasSupport: true);

                if (fontAsset == null)
                {
                    Debug.LogError("Failed to create the Noto Sans KR TextMeshPro font asset.");
                    return;
                }

                fontAsset.name = "NotoSansKR-Bold SDF";
                fontAsset.atlasPopulationMode = AtlasPopulationMode.Dynamic;
                fontAsset.isMultiAtlasTexturesEnabled = true;

                Material material = fontAsset.material;
                Texture2D atlasTexture = fontAsset.atlasTextures[0];

                AssetDatabase.CreateAsset(fontAsset, FontAssetPath);
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
                AssetDatabase.AddObjectToAsset(material, fontAsset);
                EditorUtility.SetDirty(fontAsset);
                created = true;
            }

            int characterCountBefore = fontAsset.characterTable.Count;
            if (!fontAsset.TryAddCharacters(KoreanSmokeTest, out string missingCharacters))
            {
                Debug.LogError($"The Korean TMP font is missing required glyphs: {missingCharacters}");
                return;
            }

            bool glyphsAdded = fontAsset.characterTable.Count != characterCountBefore;
            if (glyphsAdded)
            {
                EditorUtility.SetDirty(fontAsset);
                EditorUtility.SetDirty(fontAsset.material);
                foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
                {
                    EditorUtility.SetDirty(atlasTexture);
                }
            }

            bool fallbackAdded = RegisterAsGlobalFallback(fontAsset);
            if (created || glyphsAdded || fallbackAdded)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log($"Korean TMP font is ready: {FontAssetPath}");
            }
            else if (logSuccess)
            {
                Debug.Log($"Korean TMP font is already installed: {FontAssetPath}");
            }
        }

        private static void EnsureFontDataIsIncluded()
        {
            if (AssetImporter.GetAtPath(SourceFontPath) is not TrueTypeFontImporter importer || importer.includeFontData)
            {
                return;
            }

            importer.includeFontData = true;
            importer.SaveAndReimport();
        }

        private static bool RegisterAsGlobalFallback(TMP_FontAsset fontAsset)
        {
            TMP_Settings settings = AssetDatabase.LoadAssetAtPath<TMP_Settings>(TmpSettingsPath);
            if (settings == null)
            {
                Debug.LogWarning($"TMP settings were not found at {TmpSettingsPath}.");
                return false;
            }

            SerializedObject serializedSettings = new SerializedObject(settings);
            SerializedProperty fallbacks = serializedSettings.FindProperty("m_fallbackFontAssets");
            if (fallbacks == null)
            {
                Debug.LogError("TMP fallback font list could not be found.");
                return false;
            }

            for (int i = 0; i < fallbacks.arraySize; i++)
            {
                if (fallbacks.GetArrayElementAtIndex(i).objectReferenceValue == fontAsset)
                {
                    return false;
                }
            }

            int index = fallbacks.arraySize;
            fallbacks.InsertArrayElementAtIndex(index);
            fallbacks.GetArrayElementAtIndex(index).objectReferenceValue = fontAsset;
            serializedSettings.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            return true;
        }
    }
}
