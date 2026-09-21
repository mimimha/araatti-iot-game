using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 👥 <b>플레이어 목록을 배 게임의 팀 패널 모양으로 바꾼다.</b> <b>두 칸</b>으로.
    ///
    /// 배는 네 명이라 칸이 넷이지만 무쌍은 <b>1~2명</b>이다(WARRIORS.md 1장).
    /// 남는 칸을 비워 두면 오지 않을 사람을 기다리는 것처럼 보이므로 두 칸만 만든다.
    ///
    /// <code>
    ///   Players        panel-team-background + panel-team-frame
    ///   Player_1 · 2   portrait-backplate + portrait-frame-red / -yellow
    ///   Player_3 · 4   그대로 두되 화면 밖에서 꺼둔다
    /// </code>
    ///
    /// <b>오브젝트를 지우지 않는다.</b> <c>WarriorsHudPresenter</c> 가 네 칸을 배열로 물고 있어
    /// (<c>playerStateTexts</c> · <c>playerStateFills</c>) 지우면 참조가 끊긴다. 3·4번은 남겨 두고
    /// 끄기만 한다. 사람이 둘뿐이면 프레젠터가 어차피 런타임에도 꺼 준다.
    ///
    /// ⚠ <b>얼굴 사진은 넣지 않는다.</b> 배는 <c>ShipCoopPortrait</c> 가 각자의 캐릭터를 찍어
    ///    <c>RenderTexture</c> 로 넣는데, 그 부품은 배의 <c>TaskWorker</c> 에 묶여 있어 그대로
    ///    쓸 수 없다. 여기서는 <b>자리와 테두리까지</b> 만들어 두고, 사진은 별도 작업으로 남긴다.
    ///    빈 자리는 초상 배경만 보이며 그 위에 번호와 적중 수가 얹힌다.
    /// </summary>
    public static class WarriorsHudTeamPanel
    {
        private const string HudPrefabPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/UI/WarriorsHUD.prefab";

        private const string ArtRoot = "Assets/Game/Art/UI/ShipCoopHudV2";

        /// <summary>한 칸의 크기. 배의 값(128×168)을 그대로 쓴다.</summary>
        private static readonly Vector2 SlotSize = new Vector2(128f, 168f);

        /// <summary>칸 사이 간격. 배와 같다.</summary>
        private const float SlotGap = 136f;

        /// <summary>무쌍은 두 칸이다.</summary>
        private const int Slots = 2;

        [MenuItem("Tools/아라아띠/Warriors 플레이어 패널을 배 게임 모양으로")]
        public static void Wire()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(HudPrefabPath);

            if (root == null)
            {
                Debug.LogError($"[팀 패널] 프리팹을 열지 못했습니다 — {HudPrefabPath}");
                return;
            }

            EnsurePortraitLayer();

            try
            {
                // 사진을 찍는 부품. HUD 에 붙여 두면 HUD 가 있는 곳에서만 돈다.
                if (root.GetComponent<WarriorsPortrait>() == null)
                {
                    root.AddComponent<WarriorsPortrait>();
                    Debug.Log("[팀 패널] WarriorsPortrait 을 HUD 에 붙였습니다.");
                }

                RectTransform players = Find(root, "Players");

                if (players == null)
                {
                    Debug.LogError("[팀 패널] 'Players' 판을 찾지 못했습니다.");
                    return;
                }

                // 두 칸이 들어갈 만큼. 왼쪽 여백 13 + 칸 하나 + 간격 + 칸 하나 + 오른쪽 여백.
                float width = 13f + SlotSize.x + SlotGap + 13f;
                players.sizeDelta = new Vector2(width, 176f);

                Skin(players, "panel-team-background", 0.85f);
                Frame(players, "panel-team-frame");

                Debug.Log($"[팀 패널] 판을 {width:F0}×176 두 칸 크기로 맞췄습니다.");

                string[] frames = { "portrait-frame-red", "portrait-frame-yellow" };

                for (int i = 0; i < 4; i++)
                {
                    RectTransform slot = Find(root, $"Player_{i + 1}");
                    if (slot == null) continue;

                    if (i >= Slots)
                    {
                        // ⚠ 지우지 않는다. 프레젠터의 배열이 이 오브젝트를 물고 있다.
                        slot.gameObject.SetActive(false);
                        Debug.Log($"[팀 패널] Player_{i + 1}: 껐습니다 (무쌍은 두 명까지)");
                        continue;
                    }

                    BuildSlot(slot, i, frames[i]);
                }

                PrefabUtility.SaveAsPrefabAsset(root, HudPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            Verify();
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// 칸 하나를 초상 카드로 만든다. <b>있는 자식은 옮기기만 한다.</b>
        ///
        /// <c>State</c> 글자와 <c>Health</c> 막대는 프레젠터가 인스펙터로 물고 있는 것이라
        /// 새로 만들지 않고 자리만 바꾼다. 지웠다 다시 만들면 참조가 끊긴다.
        /// </summary>
        private static void BuildSlot(RectTransform slot, int index, string frameSprite)
        {
            slot.gameObject.SetActive(true);

            // 왼쪽 끝 기준. 판이 줄어도 칸이 있던 자리에 남는다. (배와 같은 이유)
            slot.anchorMin = new Vector2(0f, 0.5f);
            slot.anchorMax = new Vector2(0f, 0.5f);
            slot.pivot = new Vector2(0.5f, 0.5f);
            slot.anchoredPosition = new Vector2(13f + SlotSize.x * 0.5f + index * SlotGap, 0f);
            slot.sizeDelta = SlotSize;

            // 칸 자체는 바탕을 깐다. 테두리는 초상 접시가 쓴다.
            Image slotImage = slot.GetComponent<Image>();
            if (slotImage != null) slotImage.color = new Color(1f, 1f, 1f, 0f);

            // ── 초상 접시 ──────────────────────────────────────────
            RectTransform plate = Child(slot, "Backplate");
            plate.anchorMin = plate.anchorMax = new Vector2(0.5f, 0.5f);
            plate.pivot = new Vector2(0.5f, 0.5f);
            plate.anchoredPosition = new Vector2(0f, 18f);
            plate.sizeDelta = new Vector2(100f, 100f);
            Skin(plate, "portrait-backplate", 1f);

            // ⚠ Image 가 아니라 **RawImage** 다. 사진이 스프라이트가 아니라 카메라가 찍은
            //    RenderTexture 이기 때문이다. (WarriorsPortrait 가 넣어 준다)
            //    접시 안에 가두려고 Mask 를 쓴다 — 어깨가 접시 밖으로 삐져나오면 지저분하다.
            Mask mask = plate.GetComponent<Mask>() ?? plate.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = true;

            RectTransform face = Child(plate, "Face");
            face.anchorMin = Vector2.zero;
            face.anchorMax = Vector2.one;
            face.offsetMin = Vector2.zero;
            face.offsetMax = Vector2.zero;

            RawImage faceImage = face.GetComponent<RawImage>() ?? face.gameObject.AddComponent<RawImage>();
            faceImage.raycastTarget = false;

            // 사진이 아직 없을 때 보이는 것. 찍히면 덮인다.
            if (faceImage.texture == null) faceImage.color = new Color(1f, 1f, 1f, .15f);

            // ⚠ **테두리는 접시 밖에 둔다.** 접시에 Mask 가 걸려 있어, 자식으로 넣으면
            //    테두리까지 잘려 나간다. 칸의 자식으로 두고 접시 자리에 맞춘다.
            RectTransform ring = Child(slot, "PortraitFrame");
            ring.anchorMin = ring.anchorMax = new Vector2(0.5f, 0.5f);
            ring.pivot = new Vector2(0.5f, 0.5f);
            ring.anchoredPosition = plate.anchoredPosition;
            ring.sizeDelta = plate.sizeDelta + new Vector2(12f, 12f);
            Skin(ring, frameSprite, 1f);
            ring.SetAsLastSibling();

            // ── 있는 자식 옮기기 ───────────────────────────────────
            RectTransform state = Find(slot.gameObject, "State");

            if (state != null)
            {
                state.anchorMin = state.anchorMax = new Vector2(0.5f, 0.5f);
                state.pivot = new Vector2(0.5f, 0.5f);
                state.anchoredPosition = new Vector2(0f, -56f);
                state.sizeDelta = new Vector2(120f, 22f);

                TMP_Text text = state.GetComponent<TMP_Text>();
                if (text != null) text.alignment = TextAlignmentOptions.Center;
            }

            RectTransform health = Find(slot.gameObject, "Health");

            if (health != null)
            {
                health.anchorMin = health.anchorMax = new Vector2(0.5f, 0.5f);
                health.pivot = new Vector2(0.5f, 0.5f);
                health.anchoredPosition = new Vector2(0f, -76f);
                health.sizeDelta = new Vector2(104f, 12f);
            }

            Debug.Log($"[팀 패널] Player_{index + 1}: 초상 카드로 바꿨습니다 ({frameSprite})");
        }

        /// <summary>
        /// **촬영용 레이어를 마련한다.** 사진기는 이 레이어만 본다.
        ///
        /// 배 게임이 쓰는 것과 <b>같은 이름</b>을 쓴다. 레이어는 32칸뿐이라 게임마다 하나씩
        /// 새로 만들면 금방 바닥난다. 촬영장이 서로 5000m 떨어져 있어 섞일 일도 없다.
        ///
        /// ⚠ 이미 있으면 아무것도 하지 않는다. 남의 레이어를 덮어쓰면 그 게임이 조용히 깨진다.
        /// </summary>
        private static void EnsurePortraitLayer()
        {
            if (LayerMask.NameToLayer(WarriorsPortrait.PortraitLayerName) >= 0)
            {
                Debug.Log($"[팀 패널] '{WarriorsPortrait.PortraitLayerName}' 레이어가 이미 있습니다.");
                return;
            }

            SerializedObject tags = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);

            SerializedProperty layers = tags.FindProperty("layers");

            // 0~7 은 유니티가 쓰는 자리다. 8 부터 본다.
            for (int i = 8; i < layers.arraySize; i++)
            {
                SerializedProperty slot = layers.GetArrayElementAtIndex(i);

                if (!string.IsNullOrEmpty(slot.stringValue)) continue;

                slot.stringValue = WarriorsPortrait.PortraitLayerName;
                tags.ApplyModifiedProperties();

                Debug.Log($"[팀 패널] '{WarriorsPortrait.PortraitLayerName}' 레이어를 {i}번에 만들었습니다.");
                return;
            }

            Debug.LogError("[팀 패널] 빈 레이어 자리가 없습니다. 사진을 찍을 수 없습니다.");
        }

        // ── 손도구 ────────────────────────────────────────────────

        private static RectTransform Find(GameObject root, string name)
        {
            return root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t => t.name == name);
        }

        private static RectTransform Child(RectTransform parent, string name)
        {
            Transform found = parent.Find(name);

            if (found != null) return (RectTransform)found;

            GameObject go = new GameObject(name, typeof(RectTransform));
            RectTransform rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.localScale = Vector3.one;
            return rt;
        }

        private static void Skin(RectTransform rt, string sprite, float alpha)
        {
            Image image = rt.GetComponent<Image>() ?? rt.gameObject.AddComponent<Image>();
            image.sprite = Load(sprite);
            image.type = Image.Type.Sliced;
            image.color = new Color(1f, 1f, 1f, alpha);
            image.raycastTarget = false;
        }

        private static void Frame(RectTransform parent, string sprite)
        {
            RectTransform rt = Child(parent, "ThemeFrame");
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Skin(rt, sprite, 1f);
            rt.SetAsLastSibling();
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

        /// <summary>⚠ 저장을 믿지 않고 디스크에서 다시 읽는다.</summary>
        private static void Verify()
        {
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(HudPrefabPath, ImportAssetOptions.ForceUpdate);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(HudPrefabPath);

            for (int i = 0; i < 4; i++)
            {
                RectTransform slot = Find(saved, $"Player_{i + 1}");

                if (slot == null)
                {
                    Debug.LogError($"[팀 패널] Player_{i + 1} 이 사라졌습니다. 참조가 끊깁니다.");
                    return;
                }

                bool shouldBeOn = i < Slots;

                if (slot.gameObject.activeSelf != shouldBeOn)
                {
                    Debug.LogError($"[팀 패널] Player_{i + 1} 의 켜짐 상태가 저장되지 않았습니다.");
                    return;
                }

                if (!shouldBeOn) continue;

                if (slot.Find("Backplate") == null)
                {
                    Debug.LogError($"[팀 패널] Player_{i + 1} 에 초상 접시가 없습니다.");
                    return;
                }
            }

            Debug.Log($"[팀 패널] ✅ 두 칸으로 바꿨습니다. 네 칸 오브젝트는 모두 남아 있습니다.");
        }
    }
}
