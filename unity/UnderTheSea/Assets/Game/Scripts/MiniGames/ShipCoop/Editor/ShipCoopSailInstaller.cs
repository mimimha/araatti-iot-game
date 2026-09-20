using UnityEditor;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.EditorTools
{
    /// <summary>
    /// <see cref="ShipCoopSail"/> 을 배 프리팹에 붙인다.
    ///
    /// 두 씬(ShipCoop · ShipCoopTest)의 PirateShip 이 모두 <c>P_PirateShip.prefab</c>
    /// 인스턴스라, 프리팹에 한 번 붙이면 둘 다 받는다. 씬을 따로 만지지 않는다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod UnderTheSea.MiniGames.ShipCoop.EditorTools.ShipCoopSailInstaller.Install
    /// </summary>
    public static class ShipCoopSailInstaller
    {
        private const string PrefabPath = "Assets/Game/Prefabs/PirateShip/P_PirateShip.prefab";

        [MenuItem("아라아띠/배 협동/돛 접힘 연출 붙이기")]
        public static void Install()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            if (root == null)
            {
                Debug.LogError($"[Sail] {PrefabPath} 를 열지 못했습니다.");
                return;
            }

            try
            {
                if (root.GetComponent<ShipCoopSail>() != null)
                {
                    Debug.Log("[Sail] 이미 붙어 있습니다. 아무것도 하지 않습니다.");
                    return;
                }

                root.AddComponent<ShipCoopSail>();
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log("[Sail] P_PirateShip 에 붙였습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }
    }
}
