using UnityEditor;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.EditorTools
{
    /// <summary>
    /// 자리 연출 두 가지를 프리팹에 붙인다.
    ///
    /// <code>
    ///   ShipCoopStationPose    → ShipCoopPlayer.prefab   자리에 붙으면 잡는 자세
    ///   ShipCoopCannonRecoil   → P_PirateShip.prefab     쏘면 대포가 뒤로 튄다 (+ 포구 연기)
    ///   ShipCoopSailRope       → P_PirateShip.prefab     돛 자리의 밧줄 (활대 → 손 → 갑판)
    /// </code>
    ///
    /// 두 씬의 배 · 플레이어가 모두 이 프리팹 인스턴스라 프리팹에 한 번 붙이면 둘 다 받는다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod UnderTheSea.MiniGames.ShipCoop.EditorTools.ShipCoopStationPoseInstaller.Install
    /// </summary>
    public static class ShipCoopStationPoseInstaller
    {
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/ShipCoopPlayer.prefab";
        private const string ShipPrefabPath = "Assets/Game/Prefabs/PirateShip/P_PirateShip.prefab";
        private const string SmokePath = "Assets/Synty/PolygonGeneric/Prefabs/FX/FX_Smoke_01.prefab";
        private const string RopeMaterialPath = "Assets/Game/Prefabs/PirateShip/M_ShipCoop_SailsRope_Sand_01.mat";
        private const string BallMaterialPath = "Assets/Game/Art/Materials/ShipCoop/AmmoBall.mat";

        [MenuItem("아라아띠/배 협동/자리 자세 · 대포 반동 붙이기")]
        public static void Install()
        {
            // ⚠ 자리 자세는 떼었다 다시 붙인다. [SerializeField] 는 코드 기본값을 바꿔도 프리팹에 저장된
            //    값이 이기므로(cannonGripAlong 0.22 등), 기본값을 고칠 때마다 새로 붙여야 한 벌로 맞는다.
            AddOnce<ShipCoopStationPose>(PlayerPrefabPath, null, replace: true);

            GameObject smokeGo = AssetDatabase.LoadAssetAtPath<GameObject>(SmokePath);
            ParticleSystem smoke = smokeGo != null ? smokeGo.GetComponent<ParticleSystem>() : null;

            if (smoke == null)
            {
                Debug.LogWarning($"[StationPose] 연기 프리팹을 못 찾았습니다: {SmokePath}. 반동만 붙입니다.");
            }

            Material ball = AssetDatabase.LoadAssetAtPath<Material>(BallMaterialPath);

            if (ball == null)
            {
                Debug.LogWarning($"[StationPose] 포탄 재질을 못 찾았습니다: {BallMaterialPath}. 기본 재질로 그립니다.");
            }

            AddOnce<ShipCoopCannonRecoil>(ShipPrefabPath, so =>
            {
                SerializedProperty p = so.FindProperty("smokePrefab");
                if (p != null && smoke != null)
                {
                    p.objectReferenceValue = smoke;
                }

                SerializedProperty b = so.FindProperty("ballMaterial");
                if (b != null && ball != null)
                {
                    b.objectReferenceValue = ball;
                }
            });

            Material rope = AssetDatabase.LoadAssetAtPath<Material>(RopeMaterialPath);

            if (rope == null)
            {
                Debug.LogWarning($"[StationPose] 밧줄 재질을 못 찾았습니다: {RopeMaterialPath}. 기본 재질로 그립니다.");
            }

            AddOnce<ShipCoopSailRope>(ShipPrefabPath, so =>
            {
                SerializedProperty p = so.FindProperty("ropeMaterial");
                if (p != null && rope != null)
                {
                    p.objectReferenceValue = rope;
                }
            });

            AssetDatabase.SaveAssets();
        }

        private static void AddOnce<T>(string prefabPath, System.Action<SerializedObject> fill, bool replace = false) where T : Component
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);

            if (root == null)
            {
                Debug.LogError($"[StationPose] {prefabPath} 를 열지 못했습니다.");
                return;
            }

            try
            {
                T target = root.GetComponent<T>();

                if (target != null && replace)
                {
                    Object.DestroyImmediate(target, allowDestroyingAssets: false);
                    target = null;
                    Debug.Log($"[StationPose] {typeof(T).Name} 을 떼었다 다시 붙입니다. 저장값이 코드 기본값으로 맞춰집니다.");
                }

                if (target != null)
                {
                    Debug.Log($"[StationPose] {typeof(T).Name} 은 {prefabPath} 에 이미 붙어 있습니다. 값만 다시 채웁니다.");
                }
                else
                {
                    target = root.AddComponent<T>();
                    Debug.Log($"[StationPose] {typeof(T).Name} 을 {prefabPath} 에 붙였습니다.");
                }

                if (fill != null && target != null)
                {
                    var so = new SerializedObject(target);
                    fill(so);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }
    }
}
