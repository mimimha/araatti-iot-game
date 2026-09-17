using System.Collections.Generic;
using System.IO;
using MiniGames.Common;
using TMPro;
using UnityEditor;
using UnityEngine;

namespace UnderTheSea.UI.Editor
{
    /// <summary>
    /// 🎡 <b>로딩 화면을 서연님의 타륜으로 바꾸는 프리팹을 만든다.</b>
    ///
    /// <see cref="TransitionOverlay"/> 는 <c>Resources/TransitionOverlay.prefab</c> 이 있으면
    /// 검은 화면 대신 그것을 띄우도록 <b>처음부터 설계돼 있다.</b> 그래서 코드는 한 줄도 고치지
    /// 않고 프리팹만 만들어 두면 된다.
    ///
    /// <b>왜 손으로 만들지 않는가.</b> 원본(<c>CommonMatchCanvas</c>)이 바뀌면 다시 만들어야
    /// 하는데, 손으로 만든 프리팹은 어디를 어떻게 지웠는지 아무도 모른다. 결과 오버레이
    /// (<c>MiniGameResultOverlaySetup</c>)와 같은 이유, 같은 방식이다.
    ///
    /// <b>무엇을 남기는가.</b> <c>QueueLoadingPanel</c>(배경 + 타륜)만 남기고 매칭·결과 쪽은
    /// 전부 지운다. 통째로 들고 오면 로딩 중에 매칭 화면이 뜨는 사고가 난다.
    ///
    /// <b>문구를 하나 더한다.</b> 원본에는 글자가 없다. 그런데 이 화면은 실패 사유도 보여 줘야
    /// 한다 — 예를 들어 "지금은 이 미니게임에 입장할 수 없습니다". 글자가 없으면 사람은 타륜만
    /// 도는 화면을 보며 갇혔다고 생각한다. <see cref="TransitionOverlay"/> 는 안에서 처음 찾은
    /// <see cref="TMP_Text"/> 에 문구를 넣으므로 하나만 두면 된다.
    ///
    /// ⚠ <c>QueueLoadingPresenter</c> 는 그대로 둔다. 타륜을 돌리는 것이 그 부품이고,
    ///    켜져 있기만 하면 도는 구조라 부르는 이가 없어도 된다.
    /// </summary>
    public static class TransitionOverlaySetup
    {
        private const string Source = "Assets/Game/Prefabs/MiniGames/Common/CommonMatchCanvas.prefab";
        private const string Folder = "Assets/Resources";
        private const string Output = Folder + "/TransitionOverlay.prefab";

        /// <summary>남길 것. 이 이름 말고는 캔버스 아래에서 전부 지운다.</summary>
        private const string Keep = "QueueLoadingPanel";

        [MenuItem("Tools/아라아띠/로딩 화면 타륜 프리팹 만들기")]
        public static void Build()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Source);
            if (source == null)
            {
                Debug.LogError($"[로딩 화면] 원본을 찾지 못했습니다: {Source}");
                return;
            }

            GameObject copy = Object.Instantiate(source);
            copy.name = "TransitionOverlay";

            // ⚠ 글꼴을 먼저 챙긴다. 아래에서 매칭·결과 판을 지우면 글꼴을 가진 글자도 같이 사라진다.
            TMP_FontAsset font = null;
            foreach (TMP_Text text in copy.GetComponentsInChildren<TMP_Text>(true))
            {
                if (text.font != null) { font = text.font; break; }
            }

            Transform panel = FindDeep(copy.transform, Keep);
            Canvas canvas = copy.GetComponentInChildren<Canvas>(true);

            if (panel == null || canvas == null)
            {
                Debug.LogError($"[로딩 화면] 원본에 '{Keep}' 이나 Canvas 가 없습니다. 원본이 바뀐 것 같습니다.");
                Object.DestroyImmediate(copy);
                return;
            }

            // 1) 타륜 판 말고 캔버스 아래의 것을 전부 지운다.
            List<GameObject> doomed = new List<GameObject>();

            foreach (Transform child in canvas.transform)
            {
                if (child != panel) doomed.Add(child.gameObject);
            }

            // 캔버스 밖에 있는 것들(Systems, MatchFlow 등)도 지운다.
            foreach (Transform child in copy.transform)
            {
                if (child.GetComponentInChildren<Canvas>(true) == null) doomed.Add(child.gameObject);
            }

            foreach (GameObject dead in doomed)
            {
                if (dead != null) Object.DestroyImmediate(dead);
            }

            // 2) 매칭을 모는 부품을 뗀다. 남으면 로딩 중에 매칭 화면이 뜰 수 있다.
            StripAll<CommonMatchingUI>(copy);
            StripAll<MatchingQueueCoordinator>(copy);
            StripAll<MatchFlowController>(copy);

            // 3) 무조건 맨 위에 그린다. TransitionOverlay 가 만드는 임시 화면과 같은 값이다.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = false;
            canvas.sortingOrder = short.MaxValue;

            // 4) 보이는 상태로 굳힌다. 원본은 Show() 가 알파를 올려 주지만 여기서는 부르는 이가 없다.
            panel.gameObject.SetActive(true);

            foreach (CanvasGroup group in copy.GetComponentsInChildren<CanvasGroup>(true))
            {
                group.alpha = 1f;
                group.blocksRaycasts = true;
            }

            // 5) 문구 자리를 만든다. (위 주석 — 실패 사유를 보여 줘야 한다)
            AddMessage(panel, font);

            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(copy, Output);
            Object.DestroyImmediate(copy);

            AssetDatabase.Refresh();
            Debug.Log($"[로딩 화면] 만들었습니다 — {Output}. 이제 모든 로딩 화면이 타륜으로 바뀝니다.");
        }

        private static void AddMessage(Transform parent, TMP_FontAsset font)
        {
            GameObject host = new GameObject("Message", typeof(RectTransform));
            host.transform.SetParent(parent, worldPositionStays: false);

            RectTransform rect = host.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 120f);
            rect.sizeDelta = new Vector2(900f, 80f);

            TextMeshProUGUI text = host.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;

            text.text = "불러오는 중...";
            text.fontSize = 32f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
        }

        private static void StripAll<T>(GameObject root) where T : Component
        {
            foreach (T found in root.GetComponentsInChildren<T>(true))
            {
                Object.DestroyImmediate(found);
            }
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;

            for (int i = 0; i < root.childCount; i++)
            {
                Transform hit = FindDeep(root.GetChild(i), name);
                if (hit != null) return hit;
            }

            return null;
        }
    }
}
