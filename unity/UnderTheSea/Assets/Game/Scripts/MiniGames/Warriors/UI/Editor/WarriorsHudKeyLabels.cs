using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// ⌨ <b>화면의 키 안내를 실제 키에 맞춘다.</b> <c>1 2 3</c> → <c>J K L</c>.
    ///
    /// 조작을 <c>IOT_INPUT.md</c> 1장에 맞춰 옮겼는데, HUD 하단 카드의 동그란 번호는
    /// 프리팹에 글자로 박혀 있어 따라오지 못했다. <b>안내가 틀린 키를 가리키고 있었다.</b>
    ///
    /// <code>
    ///   1 → J   가로베기
    ///   2 → K   세로베기
    ///   3 → L   찌르기
    /// </code>
    ///
    /// 순서가 그대로라 번호만 글자로 바꾸면 된다. 어떤 카드가 어느 공격인지는 건드리지 않는다.
    ///
    /// ⚠ <b>정확히 그 한 글자인 것만 바꾼다.</b> "1P READY" 나 "HP 100" 처럼 숫자가 들어간
    ///    다른 문구는 지나간다. 바꾼 자리를 전부 로그로 남겨 무엇을 건드렸는지 확인한다.
    /// </summary>
    public static class WarriorsHudKeyLabels
    {
        private const string HudPrefabPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/UI/WarriorsHUD.prefab";

        private static readonly Dictionary<string, string> Rename = new Dictionary<string, string>
        {
            { "1", "J" },
            { "2", "K" },
            { "3", "L" },
        };

        [MenuItem("Tools/아라아띠/Warriors HUD 키 안내 J K L 로 바꾸기")]
        public static void Wire()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);

            if (root == null)
            {
                Debug.LogError($"[HUD 키] 프리팹을 열지 못했습니다 — {HudPrefabPath}");
                return;
            }

            int changed = 0;

            try
            {
                foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
                {
                    string now = label.text?.Trim();

                    if (string.IsNullOrEmpty(now) || !Rename.TryGetValue(now, out string next)) continue;

                    label.text = next;
                    changed++;

                    Debug.Log($"[HUD 키] '{now}' → '{next}'   ({Path(label.transform)})", label);
                }

                if (changed == 0)
                {
                    Debug.Log("[HUD 키] 바꿀 것이 없습니다. 이미 J K L 이거나 라벨을 찾지 못했습니다.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // ⚠ 저장을 믿지 않고 디스크에서 다시 읽는다. 같은 배치 안에서는 캐시가 옛 값을 준다.
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            List<string> leftovers = saved
                .GetComponentsInChildren<TMP_Text>(true)
                .Where(t => Rename.ContainsKey(t.text?.Trim() ?? string.Empty))
                .Select(t => Path(t.transform))
                .ToList();

            if (leftovers.Count > 0)
            {
                Debug.LogError(
                    $"[HUD 키] 저장 뒤에도 옛 번호가 {leftovers.Count}개 남아 있습니다 — " +
                    string.Join(", ", leftovers));
                return;
            }

            Debug.Log($"[HUD 키] {changed}곳을 바꿨습니다. 남은 숫자 라벨 없음.");
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }

        /// <summary>어느 자리인지 사람이 읽게. 무엇을 건드렸는지 로그로 남기기 위한 것이다.</summary>
        private static string Path(Transform t)
        {
            string path = t.name;

            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;

            return path;
        }
    }
}
