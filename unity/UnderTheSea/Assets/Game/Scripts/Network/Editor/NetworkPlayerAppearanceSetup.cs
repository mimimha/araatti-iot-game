#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnderTheSea.Character;
using UnderTheSea.Network;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// 캐릭터 프리팹에 외형 복제 부품을 붙이고 배선한다. (PRD 09-2)
    ///
    /// 로비·배·검이 같은 모델을 쓰므로 같은 표로 배선된다. 미니게임마다 이 부품이 빠지면
    /// <b>그 게임에서만 기본 외형이 나온다.</b> 검 게임이 실제로 그랬다 — 커스터마이즈한
    /// 얼굴과 옷이 로비에서는 보이는데 검 게임에 들어가면 샘플 캐릭터로 바뀌었다.
    ///
    /// 슬롯 13개를 손으로 끌어다 놓으면 하나쯤 빠뜨리고, 빠뜨린 자리는 **조용히 안 보인다.**
    /// 이름으로 찾아 붙이면 빠짐없이 연결되고, 못 찾은 것은 오류로 드러난다.
    ///
    /// 모델을 바꿔 메시 오브젝트 이름이 달라지면 아래 표를 고치고 다시 실행한다.
    ///
    /// 메뉴: Tools > 아라아띠 > 캐릭터 외형 > NetworkPlayer 외형 배선
    /// </summary>
    internal static class NetworkPlayerAppearanceSetup
    {
        private const string PrefabPath = "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";
        private const string CatalogPath = "Assets/Game/ScriptableObjects/Character/CharacterPartCatalog.asset";
        private const string SkinMaterialPath = "Assets/ithappy/Cute_Characters/Materials/Color.mat";

        /// <summary>
        /// 몸의 자리 → 그 자리를 그리는 메시 오브젝트 이름.
        ///
        /// ithappy Cute_Characters 모델의 이름을 그대로 쓴다.
        /// <c>Costumes</c> · <c>Shorts</c> 는 메시가 비어 있어(<c>m_Mesh: 0</c>) 아무것도 그리지 않으므로
        /// 어느 자리에도 묶지 않는다.
        /// </summary>
        private static readonly (WearSlot slot, string rendererName)[] Bindings =
        {
            (WearSlot.Body,          "Body"),
            (WearSlot.Face,          "Faces"),
            (WearSlot.Hair,          "Hairstyle"),
            (WearSlot.Top,           "Outwear"),
            (WearSlot.Bottom,        "Pants"),
            (WearSlot.Shoes,         "Shoes"),
            (WearSlot.Glasses,       "Glasses"),
            (WearSlot.Ears,          "Ears"),
            (WearSlot.Gloves,        "Gloves"),
            (WearSlot.Socks,         "Socks"),
            (WearSlot.Hat,           "Hat"),
            (WearSlot.FaceAccessory, "Face_Accessories"),
            (WearSlot.Outfit,        "Outfit")
        };

        /// <summary>피부색을 칠할 메시. 커마 화면과 같은 셋이다.</summary>
        private static readonly string[] SkinRendererNames = { "Body", "Faces", "Ears" };

        private const string ShipCoopPrefabPath = "Assets/Game/Prefabs/Characters/ShipCoopPlayer.prefab";
        private const string WarriorsPrefabPath = "Assets/Game/Prefabs/MiniGames/Warriors/Player/WarriorsNetPlayer.prefab";

        [MenuItem("Tools/아라아띠/캐릭터 외형/NetworkPlayer 외형 배선")]
        public static void Wire() => WirePrefab(PrefabPath);

        [MenuItem("Tools/아라아띠/캐릭터 외형/ShipCoopPlayer 외형 배선")]
        public static void WireShipCoop() => WirePrefab(ShipCoopPrefabPath);

        [MenuItem("Tools/아라아띠/캐릭터 외형/WarriorsNetPlayer 외형 배선")]
        public static void WireWarriors() => WirePrefab(WarriorsPrefabPath);

        public static void WireWarriorsFromCommandLine() => WireWarriors();

        private static void WirePrefab(string prefabPath)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogError($"[NetworkPlayerAppearanceSetup] 프리팹을 열지 못했습니다: {prefabPath}");
                return;
            }

            try
            {
                CharacterPartCatalog catalog = AssetDatabase.LoadAssetAtPath<CharacterPartCatalog>(CatalogPath);
                if (catalog == null)
                {
                    Debug.LogError(
                        $"[NetworkPlayerAppearanceSetup] 카탈로그 에셋이 없습니다: {CatalogPath}\n" +
                        "먼저 Tools > 아라아띠 > 캐릭터 외형 > 파츠 카탈로그 만들기 를 실행해 주세요.");
                    return;
                }

                Dictionary<string, SkinnedMeshRenderer> byName = new Dictionary<string, SkinnedMeshRenderer>();
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    byName[renderer.name] = renderer;
                }

                if (!TryBuildBindings(byName, out CharacterAppearanceApplier.SlotBinding[] bindings))
                {
                    return;
                }

                if (!TryBuildSkinRenderers(byName, out SkinnedMeshRenderer[] skins))
                {
                    return;
                }

                Material skinMaterial = AssetDatabase.LoadAssetAtPath<Material>(SkinMaterialPath);
                if (skinMaterial == null)
                {
                    Debug.LogWarning(
                        $"[NetworkPlayerAppearanceSetup] 피부 재질을 찾지 못했습니다: {SkinMaterialPath}. " +
                        "Applier 가 첫 피부 렌더러의 재질로 대신합니다.");
                }

                CharacterAppearanceApplier applier = root.GetComponent<CharacterAppearanceApplier>();
                if (applier == null)
                {
                    applier = root.AddComponent<CharacterAppearanceApplier>();
                }

                applier.EditorWire(catalog, bindings, skins, skinMaterial);

                NetworkPlayerAppearance appearance = root.GetComponent<NetworkPlayerAppearance>();
                if (appearance == null)
                {
                    appearance = root.AddComponent<NetworkPlayerAppearance>();
                }

                SerializedObject so = new SerializedObject(appearance);
                so.FindProperty("catalog").objectReferenceValue = catalog;
                so.FindProperty("applier").objectReferenceValue = applier;

                // 감출 렌더러는 비워 둔다. 런타임에 자식에서 모두 찾는다.
                so.FindProperty("hiddenUntilReady").arraySize = 0;
                so.ApplyModifiedPropertiesWithoutUndo();

                // ⚠ **로그인을 거치지 않고 바로 들어오는 미니게임에는 안전망이 필요하다.**
                //
                //    그런 경로에는 제출할 외형이 아예 없을 수 있다. 그때 AppearanceReady 가
                //    false 로 남으면 그 사람은 **모든 화면에서 투명한 채로** 돌아다닌다.
                //    Lobby 프리팹에는 붙이지 않는다 — 거기서는 진짜 외형을 끝까지 기다리는
                //    것이 맞고, 포기 정책이 섞이면 로비 캐릭터가 한 번 기본 외형으로
                //    바뀌었다가 되돌아온다. 실제로 그렇게 됐던 적이 있다.
                //
                // ⚠ global:: 을 붙인다. 이 파일은 UnderTheSea.Network.Editor 안이라
                //    MiniGames 가 UnderTheSea.MiniGames 로 먼저 해석된다.
                if (prefabPath != PrefabPath
                    && root.GetComponent<global::MiniGames.Common.MiniGameDefaultAppearance>() == null)
                {
                    root.AddComponent<global::MiniGames.Common.MiniGameDefaultAppearance>();
                }

                EditorUtility.SetDirty(applier);
                EditorUtility.SetDirty(appearance);
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                AssetDatabase.SaveAssets();

                Debug.Log(
                    $"[NetworkPlayerAppearanceSetup] {System.IO.Path.GetFileNameWithoutExtension(prefabPath)} " +
                    $"배선 완료 — 슬롯 {bindings.Length}개, " +
                    $"피부 렌더러 {skins.Length}개, 카탈로그 {catalog.Count}개 항목");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool TryBuildBindings(
            Dictionary<string, SkinnedMeshRenderer> byName,
            out CharacterAppearanceApplier.SlotBinding[] bindings)
        {
            List<CharacterAppearanceApplier.SlotBinding> list =
                new List<CharacterAppearanceApplier.SlotBinding>();
            List<string> missing = new List<string>();

            foreach ((WearSlot slot, string rendererName) in Bindings)
            {
                if (!byName.TryGetValue(rendererName, out SkinnedMeshRenderer renderer))
                {
                    missing.Add($"{slot} → \"{rendererName}\"");
                    continue;
                }

                list.Add(new CharacterAppearanceApplier.SlotBinding
                {
                    slot = slot,
                    renderer = renderer
                });
            }

            if (missing.Count > 0)
            {
                Debug.LogError(
                    "[NetworkPlayerAppearanceSetup] 다음 자리의 메시 오브젝트를 찾지 못했습니다. " +
                    "배선하지 않았습니다:\n  " + string.Join("\n  ", missing));
                bindings = null;
                return false;
            }

            bindings = list.ToArray();
            return true;
        }

        private static bool TryBuildSkinRenderers(
            Dictionary<string, SkinnedMeshRenderer> byName,
            out SkinnedMeshRenderer[] skins)
        {
            List<SkinnedMeshRenderer> list = new List<SkinnedMeshRenderer>();
            List<string> missing = new List<string>();

            foreach (string name in SkinRendererNames)
            {
                if (byName.TryGetValue(name, out SkinnedMeshRenderer renderer))
                {
                    list.Add(renderer);
                }
                else
                {
                    missing.Add(name);
                }
            }

            if (missing.Count > 0)
            {
                Debug.LogError(
                    "[NetworkPlayerAppearanceSetup] 피부 렌더러를 찾지 못했습니다: " +
                    string.Join(", ", missing));
                skins = null;
                return false;
            }

            skins = list.ToArray();
            return true;
        }
    }
}
#endif
