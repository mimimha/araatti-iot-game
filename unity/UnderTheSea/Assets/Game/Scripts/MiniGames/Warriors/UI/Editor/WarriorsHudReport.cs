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

            // 문제가 난 가지는 통째로 펼쳐 본다. 어떤 자식이 무엇을 그리는지 봐야 원인이 잡힌다.
            foreach (string want in new[] { "Objective", "Players", "SpecialFeedback" })
            {
                Transform found = root.GetComponentsInChildren<Transform>(true)
                    .FirstOrDefault(t => t.name == want);

                if (found == null) continue;

                Debug.Log($"[HUD 조사] ───── '{want}' 가지 ─────");
                Dump(found, root.transform, 0);
            }

            Debug.Log("[HUD 조사] ───── 안내 칸 부품 ─────");

            foreach (string want in new[] { "Objective", "Text" })
            {
                foreach (Transform found in root.GetComponentsInChildren<Transform>(true))
                {
                    if (found.name != want) continue;

                    string parts = string.Join(" + ", found.GetComponents<Component>()
                        .Select(c => c == null ? "(깨짐)" : c.GetType().Name));

                    Debug.Log($"[HUD 조사]   {Path(found, root.transform)}  →  {parts}");
                }
            }

            // 동그란 장식 찾기 — 가로세로가 비슷하고 작은 Image 를 전부 훑는다.
            Debug.Log("[HUD 조사] ───── 동그라미 후보 ─────");

            foreach (Image image in root.GetComponentsInChildren<Image>(true))
            {
                RectTransform rt = image.rectTransform;
                float w = rt.sizeDelta.x, h = rt.sizeDelta.y;

                if (w < 16f || w > 120f) continue;
                if (Mathf.Abs(w - h) > 6f) continue;

                Debug.Log(
                    $"[HUD 조사]   {Path(image.transform, root.transform)}  " +
                    $"{w:F0}×{h:F0} @{rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0} · " +
                    $"스프라이트 {(image.sprite == null ? "없음" : image.sprite.name)} · " +
                    $"색 #{ColorUtility.ToHtmlStringRGBA(image.color)} · " +
                    $"{(image.gameObject.activeInHierarchy ? "켜짐" : "꺼짐")}");
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

        /// <summary>가지 하나를 들여쓰기로 펼친다. 그림 · 크기 · 글자를 한 줄에 같이 본다.</summary>
        private static void Dump(Transform t, Transform stop, int depth)
        {
            RectTransform rt = t as RectTransform;
            Image image = t.GetComponent<Image>();
            RawImage raw = t.GetComponent<RawImage>();
            TMP_Text text = t.GetComponent<TMP_Text>();

            string what = image != null
                ? $"Image {(image.sprite == null ? "단색" : image.sprite.name)} {image.type} #{ColorUtility.ToHtmlStringRGBA(image.color)}"
                : raw != null ? "RawImage"
                : text != null ? $"글자 \"{(text.text ?? string.Empty).Replace("\n", "↵")}\" {text.fontSize:F0}pt"
                : "-";

            // ⚠ 앵커를 같이 본다. 앵커가 벌어져 있으면(stretch) sizeDelta 는 **절대 크기가 아니라
            //    부모 대비 여백**이다. 그것을 모르고 크기를 넣으면 판이 화면만큼 커진다.
            string anchor = rt == null ? "" :
                (rt.anchorMin == rt.anchorMax
                    ? $"고정({rt.anchorMin.x:F1},{rt.anchorMin.y:F1})"
                    : $"⚠늘림({rt.anchorMin.x:F1},{rt.anchorMin.y:F1})~({rt.anchorMax.x:F1},{rt.anchorMax.y:F1})");

            Debug.Log(
                $"[HUD 조사] {new string(' ', depth * 3)}{t.name}  " +
                (rt != null ? $"[{rt.sizeDelta.x:F0}×{rt.sizeDelta.y:F0} @{rt.anchoredPosition.x:F0},{rt.anchoredPosition.y:F0} {anchor}]  " : "") +
                what + (t.gameObject.activeSelf ? "" : "  (꺼짐)"));

            foreach (Transform child in t) Dump(child, stop, depth + 1);
        }

        private static string Path(Transform t, Transform stop)
        {
            string path = t.name;

            for (Transform p = t.parent; p != null && p != stop; p = p.parent) path = p.name + "/" + path;

            return path;
        }
    }
}
