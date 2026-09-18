using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🔍 <b>무쌍 HUD 실태 조사.</b> 고치지 않고 <b>보기만</b> 한다.
    ///
    /// 배 게임 테마로 갈아끼우기 전에, 지금 어떤 판이 어떤 그림을 쓰고 있는지 알아야 한다.
    /// 프리팹 YAML 로는 계층과 스프라이트를 같이 보기 어려워 유니티에게 직접 묻는다.
    /// </summary>
    public static class WarriorsHudReport
    {
        private const string HudPrefabPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/UI/WarriorsHUD.prefab";

        [MenuItem("Tools/아라아띠/Warriors HUD 실태 보기")]
        public static void Report()
        {
            GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            if (root == null)
            {
                Debug.LogError($"[HUD 조사] 프리팹을 찾지 못했습니다 — {HudPrefabPath}");
                return;
            }

            Debug.Log("[HUD 조사] ───── 최상위 판 ─────");

            foreach (Transform child in root.transform)
            {
                RectTransform rt = child as RectTransform;

                Debug.Log(
                    $"[HUD 조사] {child.name}  " +
                    (rt != null ? $"크기 {rt.sizeDelta.x:F0}×{rt.sizeDelta.y:F0} · 자리 {rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0}" : "") +
                    $"  자식 {child.childCount}개");
            }

            Debug.Log("[HUD 조사] ───── 배경으로 쓰인 Image ─────");

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                // 아이콘까지 다 찍으면 읽을 수 없다. 판 배경으로 보이는 큰 것만 본다.
                RectTransform rt = image.rectTransform;
                bool big = rt.sizeDelta.x > 90f || rt.sizeDelta.y > 60f ||
                           (rt.anchorMin != rt.anchorMax);

                if (!big) continue;

                Debug.Log(
                    $"[HUD 조사]   {Path(image.transform, root.transform)}\n" +
                    $"      스프라이트 {(image.sprite == null ? "없음(단색)" : image.sprite.name)} · " +
                    $"색 #{ColorUtility.ToHtmlStringRGBA(image.color)} · " +
                    $"타입 {image.type} · 크기 {rt.sizeDelta.x:F0}×{rt.sizeDelta.y:F0}");
            }

            Debug.Log("[HUD 조사] ───── 글자 ─────");

            foreach (TMP_Text text in root.GetComponentsInChildren<TMP_Text>(true).Take(40))
            {
                string shown = (text.text ?? string.Empty).Replace("\n", "↵");
                if (shown.Length > 18) shown = shown.Substring(0, 18) + "…";

                Debug.Log(
                    $"[HUD 조사]   {Path(text.transform, root.transform)}  " +
                    $"\"{shown}\" · {text.fontSize:F0}pt · #{ColorUtility.ToHtmlStringRGB(text.color)}");
            }
        }

        public static void ReportFromCommandLine()
        {
            Report();
            EditorApplication.Exit(0);
        }

        private static string Path(Transform t, Transform stop)
        {
            string path = t.name;

            for (Transform p = t.parent; p != null && p != stop; p = p.parent) path = p.name + "/" + path;

            return path;
        }
    }
}
