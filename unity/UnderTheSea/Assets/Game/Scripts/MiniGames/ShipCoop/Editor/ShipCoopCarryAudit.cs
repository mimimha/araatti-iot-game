using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.MiniGames.ShipCoop.EditorTools
{
    /// <summary>
    /// 배가 틀 때 **같이 안 도는 것**을 찾는다.
    ///
    /// <see cref="ShipCoopShipTurn"/> 은 <c>carried</c> 에 담긴 것만 돌린다. 갑판에
    /// 새 소품을 놓고 그 목록에 안 넣으면, 배가 꺾일 때 그것만 제자리에 남아
    /// 갑판을 뚫고 지나간다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod UnderTheSea.MiniGames.ShipCoop.EditorTools.ShipCoopCarryAudit.Audit
    /// </summary>
    public static class ShipCoopCarryAudit
    {
        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/ShipCoop.unity",
            "Assets/Game/Scenes/Develop/MinHwa/ShipCoopTest.unity",
        };

        /// <summary>배 가운데에서 이 반지름 안에 있으면 "배 위" 로 본다 (m).</summary>
        private const float ShipRadius = 14f;

        [MenuItem("아라아띠/배 협동/배와 같이 도는지 점검")]
        public static void Audit()
        {
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                Report(scene);
            }
        }

        private static void Report(Scene scene)
        {
            ShipCoopShipTurn turn = null;

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                turn = go.GetComponentInChildren<ShipCoopShipTurn>(true);
                if (turn != null) break;
            }

            if (turn == null)
            {
                Debug.LogError($"[Carry] {scene.name} — ShipCoopShipTurn 이 없습니다.");
                return;
            }

            var so = new SerializedObject(turn);
            SerializedProperty list = so.FindProperty("carried");

            var carried = new List<Transform>();
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue is Transform t)
                {
                    carried.Add(t);
                }
            }

            var sb = new StringBuilder();
            sb.Append($"[Carry] {scene.name} — 목록에 {carried.Count}개\n");

            foreach (Transform t in carried)
            {
                sb.Append("  담김: ").Append(t == null ? "(빈 칸)" : t.name).Append('\n');
            }

            Vector3 center = turn.transform.position;

            sb.Append("\n  배 근처인데 목록에 없는 것:\n");
            int missing = 0;

            foreach (GameObject go in scene.GetRootGameObjects())
            {
                Transform t = go.transform;

                if (IsCoveredBy(t, carried))
                {
                    continue;
                }

                // 배 주위에 있는 것만 본다. 하늘 · 카메라 · 매니저는 볼 것이 없다.
                Vector3 flat = t.position - center;
                flat.y = 0f;

                if (flat.magnitude > ShipRadius)
                {
                    continue;
                }

                sb.Append("    ").Append(t.name)
                  .Append("   거리 ").Append(flat.magnitude.ToString("0.0"))
                  .Append("m, 렌더러 ").Append(t.GetComponentsInChildren<Renderer>(true).Length)
                  .Append("개\n");

                foreach (Transform child in t)
                {
                    sb.Append("        - ").Append(child.name)
                      .Append("  렌더러 ").Append(child.GetComponentsInChildren<Renderer>(true).Length)
                      .Append("개\n");
                }

                missing++;
            }

            if (missing == 0)
            {
                sb.Append("    (없음)\n");
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// 점검에서 나온 <c>Ship/Deck</c> 을 <c>carried</c> 에 넣는다.
        ///
        /// <c>Ship</c> 통째로 넣으면 안 된다. 그 밑의 Station_* · AmmoBox ·
        /// DamageSpawnPoints 는 **이미 개별로** 담겨 있어서 두 번 돌아간다.
        /// 빠진 것은 <c>Deck</c> 하나뿐이다.
        /// </summary>
        [MenuItem("아라아띠/배 협동/안 도는 갑판 태우기")]
        public static void CarryShipDeck()
        {
            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                ShipCoopShipTurn turn = null;
                Transform deck = null;

                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    turn ??= go.GetComponentInChildren<ShipCoopShipTurn>(true);

                    if (go.name == "Ship")
                    {
                        deck = go.transform.Find("Deck");
                    }
                }

                if (turn == null || deck == null)
                {
                    Debug.LogWarning($"[Carry] {scene.name} — 배나 Ship/Deck 을 못 찾았습니다.");
                    continue;
                }

                var so = new SerializedObject(turn);
                SerializedProperty list = so.FindProperty("carried");

                for (int i = 0; i < list.arraySize; i++)
                {
                    if (ReferenceEquals(list.GetArrayElementAtIndex(i).objectReferenceValue, deck))
                    {
                        Debug.Log($"[Carry] {scene.name} — 이미 담겨 있습니다.");
                        goto next;
                    }
                }

                list.arraySize++;
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = deck;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[Carry] {scene.name} — Ship/Deck 을 태웠습니다. 이제 {list.arraySize}개.");

                next: ;
            }
        }

        /// <summary>그 자신이거나 담긴 것의 자손이면 이미 같이 돈다.</summary>
        private static bool IsCoveredBy(Transform t, List<Transform> carried)
        {
            foreach (Transform c in carried)
            {
                if (c == null) continue;
                if (t == c || t.IsChildOf(c)) return true;
            }

            return false;
        }
    }
}
