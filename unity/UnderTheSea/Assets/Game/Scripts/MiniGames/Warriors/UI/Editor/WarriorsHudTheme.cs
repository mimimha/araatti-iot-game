using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🎨 <b>무쌍 HUD 를 배 게임과 같은 테마로 입힌다.</b>
    ///
    /// 배 협동이 쓰는 분리형 에셋(<c>Art/UI/ShipCoopHudV2</c>)을 그대로 빌려 온다.
    /// 어두운 남색 판에 <b>금색 밧줄 테두리</b>가 둘린 그 모양이다. 두 게임이 같은 부품을
    /// 쓰면 플레이어가 미니게임을 오갈 때 화면이 같은 세계로 읽힌다.
    ///
    /// <b>새로 짓지 않고 껍데기만 간다.</b> 배 쪽 도구(<c>ShipCoopHudV2Art</c>)는 자식을 전부
    /// 다시 만드는데, 무쌍은 <c>WarriorsHudPresenter</c> 가 수십 개의 칸을 인스펙터로 물고 있어
    /// 그렇게 하면 <b>참조가 통째로 끊긴다.</b> 그래서 있는 오브젝트의 그림과 색만 바꾸고,
    /// 테두리는 <b>자식으로 덧댄다.</b> 자식이 늘어나는 것은 참조를 깨지 않는다.
    ///
    /// <code>
    ///   큰 판    panel-capsule-background + panel-capsule-frame
    ///   안내판   panel-event-background   + panel-event-frame
    ///   공격 카드 panel-action-background  + panel-action-frame
    ///   막대     bar-track + bar-fill-white
    ///   키 배지  keycap
    /// </code>
    ///
    /// ⚠ <b>3라운드 노트와 판정선은 건드리지 않는다.</b> 링 두께 7px · 가운데 알파 0 ·
    ///    원판/반투명 금지 같은 규격이 따로 잡혀 있다. 여기서 테마를 입히면 그 규격이 깨진다.
    ///    <c>RhythmHUD/NoteTrack</c> 아래는 통째로 건너뛴다.
    ///
    /// ⚠ <b>글자는 손대지 않는다.</b> 확정된 문구가 있고, 색과 크기는 이미 읽히는 값이다.
    /// </summary>
    public static class WarriorsHudTheme
    {
        private const string HudPrefabPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/UI/WarriorsHUD.prefab";

        private const string ArtRoot = "Assets/Game/Art/UI/ShipCoopHudV2";

        /// <summary>덧대는 테두리의 이름. 두 번 돌려도 하나만 있게 하는 표식이기도 하다.</summary>
        private const string FrameName = "ThemeFrame";

        /// <summary>손대지 않을 가지. 여기 아래는 별도 규격이 있다.</summary>
        private static readonly string[] Untouchable = { "NoteTrack" };

        // ── 어디에 무엇을 입힐지 ──────────────────────────────────────
        // 이름으로 고른다. 경로가 아니라 이름인 이유는 같은 판이 StandardHUD 와 RhythmHUD 에
        // 한 벌씩 있기 때문이다. 둘 다 같은 테마여야 한다.

        private static readonly string[] BigPanels =
        {
            "RoundCard", "HPCard", "ScoreCard", "BossCard", "PhaseProgress",
            "ResultCard", "StatPanel", "RewardPanel", "Card",
            "MCard_1", "MCard_2", "MCard_3",
            "Player_1", "Player_2", "Player_3", "Player_4",
        };

        private static readonly string[] NoticePanels = { "Announcement", "Objective", "JudgementChip" };

        private static readonly string[] ActionPanels = { "Card_1", "Card_2", "Card_3" };

        private static readonly string[] Bars =
        {
            "HPBar", "ProgressBar", "Health", "BossHP",
        };

        private static readonly string[] KeyBadges = { "KeyBadge" };

        [MenuItem("Tools/아라아띠/Warriors HUD 를 배 게임 테마로")]
        public static void Wire()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);

            if (root == null)
            {
                Debug.LogError($"[HUD 테마] 프리팹을 열지 못했습니다 — {HudPrefabPath}");
                return;
            }

            int touched = 0;

            try
            {
                GrowForFrame(root);

                foreach (Image image in root.GetComponentsInChildren<Image>(true))
                {
                    if (image == null || UnderUntouchable(image.transform)) continue;
                    if (image.gameObject.name == FrameName) continue;

                    string name = image.gameObject.name;

                    if (BigPanels.Contains(name)) touched += Panel(image, "panel-capsule-background", "panel-capsule-frame", 44f);
                    else if (NoticePanels.Contains(name)) touched += Panel(image, "panel-event-background", "panel-event-frame", 30f);
                    else if (ActionPanels.Contains(name)) touched += Panel(image, "panel-action-background", "panel-action-frame", 34f);
                    else if (Bars.Contains(name)) touched += Track(image);
                    else if (KeyBadges.Contains(name)) touched += Badge(image);
                    else if (name == "Fill") touched += Fill(image);
                }

                if (touched == 0)
                {
                    Debug.Log("[HUD 테마] 바꾼 것이 없습니다.");
                    return;
                }

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Verify(touched);
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// **금테가 들어갈 만큼 판을 키운다.**
        ///
        /// 9-슬라이스 테두리는 모서리를 줄이지 않는다. 판이 테두리 두 겹보다 얇으면 좌우 모서리가
        /// 겹쳐 뭉개지므로, 그런 판은 배경만 갈고 넘어가게 해 두었다. 그런데 <b>화면 아래 공격
        /// 카드</b>는 이 테마에서 가장 눈에 띄는 자리라 금테 없이 두면 배 게임과 결이 안 맞는다.
        ///
        /// 그래서 그 판들만 테두리가 들어갈 높이로 키운다. 참조를 깨지 않는 <b>크기 변경</b>이고,
        /// 글자와 아이콘은 가운데 정렬이라 같이 따라온다.
        ///
        /// ⚠ 플레이어 줄(<c>Player_1~4</c>)은 키우지 않는다. 네 줄이 320×160 안에 들어가야 해서
        ///    한 줄을 키우면 넘친다. 그쪽은 배경만 간다.
        /// </summary>
        private static void GrowForFrame(GameObject root)
        {
            // 공격 카드 — 34px 테두리라 68 이상이어야 한다.
            foreach (string name in ActionPanels) Grow(root, name, 76f);

            // 카드를 키웠으니 담는 줄도 같이 키운다. 안 그러면 카드가 줄 밖으로 삐져나온다.
            Grow(root, "AttackGuide", 84f);

            // 안내판 — 30px 테두리라 60 이상.
            Grow(root, "Objective", 64f);
            Grow(root, "JudgementChip", 64f);
        }

        private static void Grow(GameObject root, string name, float height)
        {
            foreach (RectTransform rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (rt.name != name || UnderUntouchable(rt)) continue;
                if (rt.sizeDelta.y >= height) continue;

                float was = rt.sizeDelta.y;
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);

                Debug.Log($"[HUD 테마] {rt.name}: 높이 {was:F0} → {height:F0} (금테가 들어가게)");
            }
        }

        /// <summary>
        /// 판 하나. 배경 그림을 갈고 금색 테두리를 덧댄다.
        ///
        /// ⚠ <b>배경색은 흰색으로 되돌린다.</b> 지금 판들은 남색(<c>#0B172E</c>)으로 <b>칠해진</b>
        ///    흰 그림을 쓰고 있다. 새 그림은 남색이 이미 구워져 있어, 그 칠을 남겨 두면
        ///    두 번 어두워져 거의 검게 된다. 투명도는 원래 값을 지킨다 —
        ///    판마다 얼마나 비쳐야 하는지가 다르게 잡혀 있다.
        /// </summary>
        private static int Panel(Image image, string background, string frame, float border)
        {
            float alpha = image.color.a;

            image.sprite = Load(background);
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, alpha);

            RectTransform rt = image.rectTransform;

            // ⚠ **테두리가 판보다 두꺼우면 덧대지 않는다.** 9-슬라이스는 모서리를 줄이지 않아,
            //    얇은 판에 두꺼운 테두리를 씌우면 좌우 모서리가 겹쳐 뭉개진다.
            //    배경만 갈아도 결이 맞으므로 그쪽이 낫다.
            bool tooThin = rt.sizeDelta.y > 0f && rt.sizeDelta.y < border * 2f;

            if (tooThin)
            {
                Debug.Log($"[HUD 테마] {image.name}: 배경만 (높이 {rt.sizeDelta.y:F0} < 테두리 {border * 2:F0})");
                return 1;
            }

            AddFrame(rt, frame);
            Debug.Log($"[HUD 테마] {image.name}: 배경 + 테두리");
            return 1;
        }

        /// <summary>막대 바탕. 채움은 자식 <c>Fill</c> 이 따로 맡는다.</summary>
        private static int Track(Image image)
        {
            image.sprite = Load("bar-track");
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Debug.Log($"[HUD 테마] {image.name}: 막대 바탕");
            return 1;
        }

        /// <summary>
        /// 막대 채움. <b>색은 그대로 둔다.</b> HP 는 하늘색, 진행도는 주황처럼
        /// 무엇을 뜻하는지가 색에 실려 있다. 새 그림이 흰색이라 그 색이 그대로 살아난다.
        /// </summary>
        private static int Fill(Image image)
        {
            if (image.transform.parent == null || !Bars.Contains(image.transform.parent.name)) return 0;

            image.sprite = Load("bar-fill-white");
            image.type = Image.Type.Filled;

            Debug.Log($"[HUD 테마] {Path(image.transform)}: 막대 채움 (색 #{ColorUtility.ToHtmlStringRGB(image.color)} 유지)");
            return 1;
        }

        /// <summary>키 배지. 배 게임의 Space 키캡과 같은 그림을 쓴다.</summary>
        private static int Badge(Image image)
        {
            image.sprite = Load("keycap");
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            Debug.Log($"[HUD 테마] {Path(image.transform)}: 키캡");
            return 1;
        }

        /// <summary>
        /// 테두리를 자식으로 덧댄다. <b>맨 마지막 자식</b>이라야 글자 위에 금테가 얹힌다.
        /// 이미 있으면 그림만 맞추고 새로 만들지 않는다.
        /// </summary>
        private static void AddFrame(RectTransform parent, string frame)
        {
            Transform found = parent.Find(FrameName);

            RectTransform rt;

            if (found != null)
            {
                rt = (RectTransform)found;
            }
            else
            {
                GameObject go = new GameObject(FrameName, typeof(RectTransform));
                rt = (RectTransform)go.transform;
                rt.SetParent(parent, false);
            }

            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.localScale = Vector3.one;
            rt.SetAsLastSibling();

            Image image = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            image.sprite = Load(frame);
            image.type = Image.Type.Sliced;
            image.color = Color.white;

            // 테두리가 클릭을 먹으면 아래 버튼이 안 눌린다.
            image.raycastTarget = false;
        }

        private static bool UnderUntouchable(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (Untouchable.Contains(p.name)) return true;
            }

            return false;
        }

        private static Sprite Load(string name)
        {
            foreach (string guid in AssetDatabase.FindAssets($"{name} t:Sprite", new[] { ArtRoot }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                {
                    return AssetDatabase.LoadAssetAtPath<Sprite>(path);
                }
            }

            throw new System.IO.FileNotFoundException($"{ArtRoot} 아래에 {name} 이 없습니다.");
        }

        /// <summary>⚠ 저장을 믿지 않고 디스크에서 다시 읽는다. 다른 셋업 도구와 같은 이유다.</summary>
        private static void Verify(int touched)
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            List<string> stillOld = saved
                .GetComponentsInChildren<Image>(true)
                .Where(i => !UnderUntouchable(i.transform))
                .Where(i => BigPanels.Contains(i.name) || NoticePanels.Contains(i.name) || ActionPanels.Contains(i.name))
                .Where(i => i.sprite == null || !i.sprite.name.StartsWith("panel-"))
                .Select(i => $"{Path(i.transform)}({(i.sprite == null ? "없음" : i.sprite.name)})")
                .ToList();

            int frames = saved.GetComponentsInChildren<Transform>(true).Count(t => t.name == FrameName);

            // 노트가 그대로인지 반드시 확인한다. 규격이 걸린 곳이다.
            int notes = saved.GetComponentsInChildren<Image>(true)
                .Count(i => UnderUntouchable(i.transform) && i.sprite != null && i.sprite.name.StartsWith("panel-"));

            if (stillOld.Count > 0)
            {
                Debug.LogError($"[HUD 테마] 안 바뀐 판이 남았습니다 — {string.Join(", ", stillOld)}");
                return;
            }

            if (notes > 0)
            {
                Debug.LogError($"[HUD 테마] ⚠ 3라운드 노트 쪽에 테마가 들어갔습니다. 규격이 깨졌습니다.");
                return;
            }

            Debug.Log($"[HUD 테마] ✅ {touched}곳을 입혔습니다. 금테 {frames}개. 노트는 손대지 않았습니다.");
        }

        private static string Path(Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
            return path;
        }
    }
}
