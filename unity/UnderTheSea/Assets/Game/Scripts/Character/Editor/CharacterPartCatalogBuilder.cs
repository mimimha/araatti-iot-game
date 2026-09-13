#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Character.Editor
{
    /// <summary>
    /// <see cref="CharacterPartCatalog"/> 에셋을 **기존 커마 프리팹에서 뽑아 만든다.**
    ///
    /// 파츠 378개를 손으로 옮길 수는 없다. 이미 <c>CharacterCustomization.prefab</c> 안
    /// <c>catalogParts</c> 배열에 정답이 들어 있으므로, 그것을 읽어 에셋으로 옮긴다.
    /// 카테고리는 여섯 개 <c>PartCollection</c> 의 <c>partPrefabs</c> 중 어디에 들어 있는지로 정한다.
    ///
    /// 같은 메뉴에서 <see cref="CharacterAppearanceApplier"/> 의 배선(슬롯 · 피부 렌더러)도
    /// 프리팹에서 복사한다. 두 작업을 나누면 한쪽만 한 상태로 남기 쉽다.
    ///
    /// ⚠ <b>"만들기" 는 한 번만 쓰는 이사 도구다.</b>
    ///    옮기고 나면 프리팹에서 <c>catalogParts</c> 필드가 사라지므로 다시 눌러도 읽을 것이 없다.
    ///    그때는 "이미 마이그레이션한 프리팹" 이라는 오류가 나고 카탈로그를 덮어쓰지 않는다.
    ///
    ///    <b>파츠를 새로 추가할 때는</b> 카탈로그 에셋에 항목을 직접 추가한다.
    ///    (프리팹의 <c>partPrefabs</c> 에도 넣어야 커마 화면 탭에 보인다)
    ///    추가한 뒤에는 아래 <b>"파츠 카탈로그 검사"</b> 를 눌러 이름 중복과 키 길이를 확인한다.
    ///    이 검사는 언제든 다시 쓸 수 있다.
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 09-1)
    /// </summary>
    internal static class CharacterPartCatalogBuilder
    {
        private const string PrefabPath = "Assets/Game/Prefabs/Characters/CharacterCustomization.prefab";
        private const string CatalogFolder = "Assets/Game/ScriptableObjects/Character";
        private const string CatalogPath = CatalogFolder + "/CharacterPartCatalog.asset";

        private const string MenuRoot = "Tools/아라아띠/캐릭터 외형/";

        /// <summary>커마 컨트롤러의 PartCollection 필드 이름 → 스냅샷 카테고리 이름.</summary>
        private static readonly (string field, string category)[] Collections =
        {
            ("face", "Face"),
            ("hair", "Hair"),
            ("top", "Top"),
            ("bottom", "Bottom"),
            ("shoes", "Shoes"),
            ("accessory", "Accessory")
        };

        [MenuItem(MenuRoot + "파츠 카탈로그 만들기 / 갱신")]
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError($"[CharacterPartCatalogBuilder] 프리팹을 열지 못했습니다: {PrefabPath}");
                return;
            }

            try
            {
                CharacterCustomizationController controller =
                    root.GetComponentInChildren<CharacterCustomizationController>(true);

                if (controller == null)
                {
                    Debug.LogError("[CharacterPartCatalogBuilder] 프리팹에서 커마 컨트롤러를 찾지 못했습니다.");
                    return;
                }

                SerializedObject source = new SerializedObject(controller);

                CharacterPartCatalog catalog = LoadOrCreateCatalog();
                int count = FillCatalog(catalog, source);

                if (count == 0)
                {
                    Debug.LogError(
                        "[CharacterPartCatalogBuilder] 프리팹의 catalogParts 가 비어 있습니다. " +
                        "이미 마이그레이션한 프리팹일 수 있습니다. 카탈로그를 덮어쓰지 않았습니다.");
                    return;
                }

                EditorUtility.SetDirty(catalog);
                AssetDatabase.SaveAssets();

                WireApplier(controller, catalog, source);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[CharacterPartCatalogBuilder] 카탈로그 {count}개 항목을 만들고 " +
                    $"Applier 배선을 마쳤습니다. → {CatalogPath}");

                Verify();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem(MenuRoot + "파츠 카탈로그 검사")]
        public static void Verify()
        {
            CharacterPartCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterPartCatalog>(CatalogPath);
            if (catalog == null)
            {
                Debug.LogError($"[CharacterPartCatalogBuilder] 카탈로그 에셋이 없습니다: {CatalogPath}");
                return;
            }

            List<string> problems = catalog.Validate();

            if (problems.Count == 0)
            {
                Debug.Log(
                    $"[CharacterPartCatalogBuilder] 카탈로그 검사 통과. 항목 {catalog.Count}개, " +
                    $"이름 중복 없음, 모든 키가 {CharacterPartCatalog.MaxNetworkKeyLength}자 이하입니다.");
                return;
            }

            Debug.LogWarning(
                $"[CharacterPartCatalogBuilder] 카탈로그에 문제가 {problems.Count}건 있습니다:\n  " +
                string.Join("\n  ", problems));
        }

        private static CharacterPartCatalog LoadOrCreateCatalog()
        {
            CharacterPartCatalog existing = AssetDatabase.LoadAssetAtPath<CharacterPartCatalog>(CatalogPath);
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(CatalogFolder);
            AssetDatabase.Refresh();

            CharacterPartCatalog created = ScriptableObject.CreateInstance<CharacterPartCatalog>();
            AssetDatabase.CreateAsset(created, CatalogPath);
            return created;
        }

        /// <summary>프리팹의 <c>catalogParts</c> 를 읽어 카탈로그 항목으로 옮긴다.</summary>
        private static int FillCatalog(CharacterPartCatalog catalog, SerializedObject source)
        {
            Dictionary<Object, string> categoryByPrefab = BuildCategoryIndex(source);

            SerializedProperty parts = source.FindProperty("catalogParts");
            if (parts == null || !parts.isArray)
            {
                Debug.LogError("[CharacterPartCatalogBuilder] 프리팹에서 catalogParts 를 찾지 못했습니다.");
                return 0;
            }

            List<CharacterPartCatalog.Entry> entries = new List<CharacterPartCatalog.Entry>(parts.arraySize);

            for (int i = 0; i < parts.arraySize; i++)
            {
                SerializedProperty element = parts.GetArrayElementAtIndex(i);
                Object prefab = element.FindPropertyRelative("prefab").objectReferenceValue;

                if (prefab == null)
                {
                    continue;
                }

                categoryByPrefab.TryGetValue(prefab, out string category);

                entries.Add(new CharacterPartCatalog.Entry
                {
                    category = category ?? string.Empty,
                    prefab = prefab as GameObject,
                    slot = (WearSlot)element.FindPropertyRelative("slot").intValue,
                    covers = (WearSlot)element.FindPropertyRelative("covers").intValue,
                    skin = element.FindPropertyRelative("skin").boolValue
                });
            }

            catalog.EditorSetEntries(entries.ToArray());
            return entries.Count;
        }

        /// <summary>여섯 개 컬렉션의 partPrefabs 를 훑어 "프리팹 → 카테고리" 를 만든다.</summary>
        private static Dictionary<Object, string> BuildCategoryIndex(SerializedObject source)
        {
            Dictionary<Object, string> index = new Dictionary<Object, string>();

            foreach ((string field, string category) in Collections)
            {
                SerializedProperty prefabs = source.FindProperty(field + ".partPrefabs");
                if (prefabs == null || !prefabs.isArray)
                {
                    Debug.LogWarning($"[CharacterPartCatalogBuilder] {field}.partPrefabs 를 찾지 못했습니다.");
                    continue;
                }

                for (int i = 0; i < prefabs.arraySize; i++)
                {
                    Object prefab = prefabs.GetArrayElementAtIndex(i).objectReferenceValue;
                    if (prefab != null && !index.ContainsKey(prefab))
                    {
                        index[prefab] = category;
                    }
                }
            }

            return index;
        }

        /// <summary>
        /// 커마 컨트롤러와 같은 오브젝트에 <see cref="CharacterAppearanceApplier"/> 를 두고,
        /// 슬롯 배선 · 피부 렌더러 · 피부 재질을 프리팹에서 복사한다.
        /// </summary>
        private static void WireApplier(
            CharacterCustomizationController controller,
            CharacterPartCatalog catalog,
            SerializedObject source)
        {
            // Applier 는 **모델 쪽**에 둔다. 다루는 것이 그 모델의 렌더러이기 때문이다.
            // UI 오브젝트에 두면 UI 를 지웠을 때 외형 적용까지 함께 사라진다.
            Transform preview = source.FindProperty("characterPreview")?.objectReferenceValue as Transform;
            GameObject host = preview != null ? preview.gameObject : controller.gameObject;

            CharacterAppearanceApplier applier = host.GetComponent<CharacterAppearanceApplier>();
            if (applier == null)
            {
                applier = host.AddComponent<CharacterAppearanceApplier>();
            }

            List<CharacterAppearanceApplier.SlotBinding> bindings =
                new List<CharacterAppearanceApplier.SlotBinding>();

            SerializedProperty slotBindings = source.FindProperty("slotBindings");
            if (slotBindings != null && slotBindings.isArray)
            {
                for (int i = 0; i < slotBindings.arraySize; i++)
                {
                    SerializedProperty element = slotBindings.GetArrayElementAtIndex(i);
                    bindings.Add(new CharacterAppearanceApplier.SlotBinding
                    {
                        slot = (WearSlot)element.FindPropertyRelative("slot").intValue,
                        renderer = element.FindPropertyRelative("renderer").objectReferenceValue
                            as SkinnedMeshRenderer
                    });
                }
            }

            List<SkinnedMeshRenderer> skins = new List<SkinnedMeshRenderer>();
            SerializedProperty skinRenderers = source.FindProperty("skinRenderers");
            if (skinRenderers != null && skinRenderers.isArray)
            {
                for (int i = 0; i < skinRenderers.arraySize; i++)
                {
                    skins.Add(skinRenderers.GetArrayElementAtIndex(i).objectReferenceValue as SkinnedMeshRenderer);
                }
            }

            Material skinMaterial = source.FindProperty("skinSourceMaterial")?.objectReferenceValue as Material;

            applier.EditorWire(catalog, bindings.ToArray(), skins.ToArray(), skinMaterial);

            // 컨트롤러가 이 Applier 와 카탈로그를 쓰도록 연결한다.
            SerializedProperty applierField = source.FindProperty("appearance");
            if (applierField != null)
            {
                applierField.objectReferenceValue = applier;
            }

            SerializedProperty catalogField = source.FindProperty("catalog");
            if (catalogField != null)
            {
                catalogField.objectReferenceValue = catalog;
            }

            source.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(applier);
            EditorUtility.SetDirty(controller);

            Debug.Log(
                $"[CharacterPartCatalogBuilder] Applier 배선: 슬롯 {bindings.Count}개, " +
                $"피부 렌더러 {skins.Count}개, 피부 재질 {(skinMaterial != null ? skinMaterial.name : "없음")}");
        }
    }
}
#endif
