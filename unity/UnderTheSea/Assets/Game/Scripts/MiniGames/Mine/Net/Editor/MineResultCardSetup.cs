using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Mine.Net.Editor
{
    /// <summary>
    /// 광산 HUD 의 <b>성적표 판</b>(결과 2단계)을 그림에 맞춰 짓는다.
    ///
    /// 판 그림에는 <b>안 바뀌는 글자가 전부 박혀 있다</b> — 제목("성공!"/"실패!") ·
    /// "도안 유사도" · "목표" · "채굴". 그래서 이 도구가 얹는 것은 <b>바뀌는 값 넷</b>뿐이다.
    ///
    /// <code>
    ///   ① 큰 칸      점수        "87점"      ("도안 유사도" 탭 위)
    ///   ② 가로 칸    한 줄 평    점수 구간별 한 문장
    ///   ③ 왼쪽       목표 칸 수  "20칸"      ("목표" 라벨 아래)
    ///   ③ 오른쪽     채굴 칸 수  "30칸"      ("채굴" 라벨 아래)
    /// </code>
    ///
    /// <b>왜 도구로 하는가.</b> 프리팹을 손으로 고치면 참조 하나를 빠뜨려도 눈에 안 띄고
    /// 실행해야 드러난다. <see cref="MineResultOverlaySetup"/> 과 같은 이유다 —
    /// 그래서 저장한 뒤 프리팹을 다시 읽어 일곱 참조가 실제로 들어갔는지 확인한다.
    ///
    /// <b>여러 번 돌려도 된다.</b> 이미 지어진 칸은 이름으로 찾아 다시 쓴다. 옛 이름
    /// (<c>Result</c> · <c>ResultDetail</c>)도 찾아 이름만 바꾼다 — 그래야 글꼴과
    /// 머티리얼을 이어받아 한글이 안 깨진다.
    ///
    /// ⚠ <b>자리는 그림에서 잰 값이다.</b> 판 그림을 다시 만들면 아래 네 상자도 다시 재야 한다.
    ///
    /// Tools > 아라아띠 > 광산 성적표 칸 만들기
    /// </summary>
    public static class MineResultCardSetup
    {
        private const string PrefabPath = "Assets/Game/Prefabs/MiniGames/Mine/MineHudCanvas.prefab";
        private const string SuccessPath = "Assets/Game/Art/UI/MineHud/result-panel-success.png";
        private const string FailPath = "Assets/Game/Art/UI/MineHud/result-panel-fail.png";

        /// <summary>
        /// 판 크기. 그림이 1608 × 646 이라 <b>그 비율(2.489 : 1)을 지킨다.</b>
        /// 안 지키면 모서리 볼트와 수정이 찌그러진다. 1920 × 1080 기준 화면의 절반 폭이다.
        /// </summary>
        private static readonly Vector2 PanelSize = new Vector2(960f, 386f);

        // 칸 자리. 그림 안에서 잰 픽셀을 비율로 바꾼 값이다.
        // ⚠ Unity 답게 **아래가 0** 이다. 그림에서 잰 값(위가 0)을 뒤집어 둔 것이다.
        private static readonly Rect ScoreBox = Rect.MinMaxRect(0.1300f, 0.4272f, 0.8694f, 0.6765f);
        private static readonly Rect CommentBox = Rect.MinMaxRect(0.1642f, 0.2755f, 0.8340f, 0.3947f);
        // ⚠ 아래 두 상자는 **칸이 아니라 박힌 라벨에 맞춘다.**
        //   그림의 "목표"·"채굴" 이 칸 한가운데가 아니다 — 목표는 2.4px 왼쪽,
        //   채굴은 2.3px 오른쪽에 찍혀 있다. 칸 기준으로 가운데를 잡았더니 숫자가
        //   라벨과 어긋나 보였다. 라벨 중심(0.3197 · 0.6781)에 상자를 맞춰 둔다.
        private static readonly Rect TargetBox = Rect.MinMaxRect(0.1642f, 0.1068f, 0.4751f, 0.1935f);
        private static readonly Rect DugBox = Rect.MinMaxRect(0.5215f, 0.1068f, 0.8346f, 0.1935f);

        /// <summary>
        /// 점수를 칸 한가운데에서 얼마나 <b>위로</b> 올릴 것인가. 판 높이에 대한 비율이다.
        ///
        /// 칸 한가운데에 두면 아래 "도안 유사도" 탭과 너무 붙어 보인다. 숫자가 제목 바로
        /// 밑에서 먼저 읽히고 탭이 그 아래 설명으로 떨어지는 편이 낫다.
        ///
        /// <b>0.01 이 판에서 약 3.9px 다.</b> (960 × 386 기준) 더 올리거나 내리려면
        /// 이 값 하나만 고친다. <c>0f</c> 이면 칸 한가운데다.
        /// 0.045 는 위 테두리에 붙었고 0.022 는 조금 낮았다. 화면을 보며 맞춘 값이다.
        ///
        /// ⚠ <b>상자를 옮길 뿐 크기는 안 바꾼다.</b> 높이를 줄여서 올리면 자동 크기가
        ///   글자까지 같이 줄여 버린다. 상자는 글자가 앉을 자리일 뿐이라 칸 테두리를
        ///   넘어가도 된다 — 가운데 정렬이라 글자는 칸 안에 그대로 있다.
        /// </summary>
        private const float ScoreLift = 0.030f;

        /// <summary>
        /// 점수와 한 줄 평의 색 (#FFEFD5). <b>순백이 아니라 따뜻한 미색</b>이다.
        ///
        /// 순백(1, 1, 1)은 금빛 판 위에서 너무 날카롭게 튀었다. 화면을 보며 인스펙터에서
        /// 맞춘 값이라 계산으로 나온 숫자가 아니다.
        /// </summary>
        private static readonly Color TextColor = new Color32(255, 239, 213, 255);

        /// <summary>
        /// 목표·채굴 숫자의 색 (#FFD594). <b>위 둘보다 한 단계 내린 금빛</b>이다.
        ///
        /// 이 둘은 가장 작은 보조 정보라(<see cref="MineHud"/> 의 읽는 순서:
        /// 성공 여부 → 점수 → 한 줄 평 → 세부) 점수와 같은 색이면 너무 나선다.
        /// 바로 위에 박힌 "목표"·"채굴" 라벨이 그림에서 #FFFDCE 언저리의 금빛이라
        /// 그쪽에 붙여 한 덩어리로 읽히게 한다.
        /// </summary>
        private static readonly Color StatColor = new Color32(255, 213, 148, 255);

        /// <summary>저장 뒤에 들어갔는지 확인할 <see cref="MineHud"/> 의 칸들.</summary>
        private static readonly string[] Wired =
        {
            "resultPanelImage", "resultSuccessSprite", "resultFailSprite",
            "resultScoreText", "resultCommentText", "resultTargetText", "resultDugText",
        };

        [MenuItem("Tools/아라아띠/광산 성적표 칸 만들기")]
        public static void Apply()
        {
            Sprite success = AssetDatabase.LoadAssetAtPath<Sprite>(SuccessPath);
            Sprite fail = AssetDatabase.LoadAssetAtPath<Sprite>(FailPath);

            if (success == null || fail == null)
            {
                Debug.LogError(
                    $"[광산 성적표] 판 그림을 Sprite 로 읽지 못했습니다. " +
                    $"임포트 설정이 Sprite 인지 보세요 — {SuccessPath} · {FailPath}");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);

            try
            {
                MineHud hud = root.GetComponent<MineHud>();

                if (hud == null)
                {
                    Debug.LogError($"[광산 성적표] {PrefabPath} 에 MineHud 가 없습니다.");
                    return;
                }

                Transform panel = root.transform.Find("ResultPanel");

                if (panel == null)
                {
                    Debug.LogError("[광산 성적표] 캔버스 밑에 ResultPanel 이 없습니다.");
                    return;
                }

                Image art = Dress(panel, success);

                // 옛 두 칸은 이름만 바꿔 이어 쓴다. 글꼴과 머티리얼이 그대로 따라온다.
                TMP_Text score = Adopt(panel, "Result", "ResultScore");
                TMP_Text comment = Adopt(panel, "ResultDetail", "ResultComment");

                if (score == null || comment == null)
                {
                    Debug.LogError("[광산 성적표] ResultPanel 밑에서 글자 칸을 못 찾았습니다.");
                    return;
                }

                // 아래 두 칸은 한 줄 평을 복제해 만든다. 새로 만들면 글꼴을 다시 꽂아야 한다.
                TMP_Text target = CloneOf(panel, comment, "ResultTarget");
                TMP_Text dug = CloneOf(panel, comment, "ResultDug");

                // ⚠ 여백(margin)이 핵심이다. 상자는 칸의 **안쪽 끝**에 딱 맞춰 뒀기 때문에,
                //   여백이 0 이면 자동 크기가 글자를 금테에 닿을 때까지 키운다. 실제로
                //   한 줄 평이 좌우 금테에 붙어 나왔다. 여백만큼 안으로 물려 재게 한다.
                //
                //   넘친 것은 **가로**였다. 그래서 좌우를 넉넉히 주고 위아래는 거의 건드리지
                //   않는다 — 아래 두 칸은 높이가 33px 뿐이라 위아래를 물리면 숫자가 확 작아진다.
                //
                //   한 줄 평이 좌우 26 으로 가장 넉넉하다. 여섯 문구 중 가장 긴 것이
                //   한글 20자쯤이라(MineHud.CommentFor) 칸 643px 를 거의 다 쓰기 때문이다.
                //   최대 26pt 면 가장 긴 문구도 안쪽 591px 에 들어간다.
                //
                // ⚠ <b>점수 크기는 최대값이 곧 실제 크기다.</b> 상자 높이에 맞춰 잰 값이
                //   60.9pt 라, 최대를 그보다 낮게 두면 높이가 아니라 이 값이 크기를 정한다.
                //   그래서 54 라고 적으면 화면에 54pt 로 나온다. 인스펙터의 Font Size 는
                //   Auto Size 가 켜져 있는 동안 TMP 가 써 넣는 칸이라 손으로 고쳐도 덮인다.
                Place(score, Lift(ScoreBox, ScoreLift), 24f, 54f, new Vector4(16f, 4f, 16f, 4f));
                Place(comment, CommentBox, 12f, 26f, new Vector4(26f, 3f, 26f, 3f));
                Place(target, TargetBox, 12f, 18f, new Vector4(16f, 2f, 16f, 2f));
                Place(dug, DugBox, 12f, 18f, new Vector4(16f, 2f, 16f, 2f));

                // 네 칸 다 같은 미색이다. 성공·실패로 물들이지 않는다 — 제목이 판 그림에
                // 이미 크게 박혀 있어 같은 말을 두 번 하게 된다. (MineHud.DrawResultCard)
                score.color = TextColor;
                comment.color = TextColor;
                target.color = StatColor;
                dug.color = StatColor;

                // 프리팹에 남는 글자. 실행하면 곧바로 덮이지만, 배선이 끊기면 이것이 그대로
                // 보이므로 **0점짜리 판과 똑같이** 둔다. 옛 판("실패 0.0%")과 같은 뜻이다.
                score.text = "0점";
                comment.text = "팀워크보다 창의력이 너무 앞서갔습니다!";
                target.text = "0칸";
                dug.text = "0칸";

                var so = new SerializedObject(hud);
                so.FindProperty("resultPanelImage").objectReferenceValue = art;
                so.FindProperty("resultSuccessSprite").objectReferenceValue = success;
                so.FindProperty("resultFailSprite").objectReferenceValue = fail;
                so.FindProperty("resultScoreText").objectReferenceValue = score;
                so.FindProperty("resultCommentText").objectReferenceValue = comment;
                so.FindProperty("resultTargetText").objectReferenceValue = target;
                so.FindProperty("resultDugText").objectReferenceValue = dug;
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();

            if (Verify()) Debug.Log("[광산 성적표] 칸 넷을 짓고 배선까지 확인했습니다.");
        }

        /// <summary>판을 그림으로 갈아입힌다. 색 사각형이던 것을 흰색으로 되돌려야 그림이 제 색으로 나온다.</summary>
        private static Image Dress(Transform panel, Sprite success)
        {
            Image art = panel.GetComponent<Image>();
            if (art == null) art = panel.gameObject.AddComponent<Image>();

            art.sprite = success;

            // ⚠ 옛 판은 반투명 검정(0.04, 0.05, 0.07, 0.86)이었다. 그대로 두면 그림이 어둡게 물든다.
            art.color = Color.white;

            // 9-slice 로 늘리지 않는다. 모서리 볼트와 수정이 그려져 있어 늘리면 뭉개진다.
            art.type = Image.Type.Simple;
            art.preserveAspect = false;

            ((RectTransform)panel).sizeDelta = PanelSize;

            return art;
        }

        /// <summary>상자를 <paramref name="by"/> 만큼 위로 민다. 크기는 그대로다.</summary>
        private static Rect Lift(Rect box, float by)
        {
            return Rect.MinMaxRect(box.xMin, box.yMin + by, box.xMax, box.yMax + by);
        }

        /// <summary>새 이름으로 찾고, 없으면 옛 이름으로 찾아 이름을 바꾼다. 다시 돌려도 안전하다.</summary>
        private static TMP_Text Adopt(Transform panel, string was, string now)
        {
            Transform found = panel.Find(now);
            if (found == null) found = panel.Find(was);
            if (found == null) return null;

            found.name = now;

            return found.GetComponent<TMP_Text>();
        }

        /// <summary>없으면 본보기를 복제해 만든다. 글꼴 · 머티리얼 · 색을 그대로 물려받는다.</summary>
        private static TMP_Text CloneOf(Transform panel, TMP_Text sample, string name)
        {
            Transform found = panel.Find(name);

            if (found == null)
            {
                GameObject made = Object.Instantiate(sample.gameObject, panel);
                made.name = name;
                found = made.transform;
            }

            return found.GetComponent<TMP_Text>();
        }

        /// <summary>
        /// 글자 칸 하나를 자리에 앉힌다.
        ///
        /// 네 칸 다 <b>가운데 정렬 · 줄바꿈 없음 · 자동 크기</b>다. 줄바꿈을 켜면 긴 한 줄 평이
        /// 두 줄로 접혀 칸을 넘치고, 자동 크기를 끄면 긴 문장이 잘린다.
        ///
        /// ⚠ <paramref name="margin"/> 을 빼먹으면 <b>글자가 금테에 닿는다.</b> 상자가 칸의
        ///   안쪽 끝에 맞춰져 있어서 자동 크기가 거기까지 키우기 때문이다. 순서는
        ///   왼쪽 · 위 · 오른쪽 · 아래다.
        /// </summary>
        private static void Place(TMP_Text text, Rect box, float min, float max, Vector4 margin)
        {
            var rt = (RectTransform)text.transform;

            rt.anchorMin = box.min;
            rt.anchorMax = box.max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.localScale = Vector3.one;
            rt.localRotation = Quaternion.identity;

            text.alignment = TextAlignmentOptions.Center;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.enableAutoSizing = true;
            text.fontSizeMin = min;
            text.fontSizeMax = max;
            text.fontSize = max;
            text.margin = margin;
        }

        /// <summary>
        /// 저장된 프리팹을 <b>다시 읽어</b> 일곱 참조가 실제로 들어갔는지 본다.
        ///
        /// 도구가 "썼다" 고 말하고 실제로는 안 쓴 적이 있다. (<see cref="MineResultOverlaySetup"/>)
        /// </summary>
        private static bool Verify()
        {
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            MineHud hud = saved != null ? saved.GetComponent<MineHud>() : null;

            if (hud == null)
            {
                Debug.LogError("[광산 성적표] 저장한 프리팹을 다시 읽지 못했습니다.");
                return false;
            }

            var so = new SerializedObject(hud);
            bool ok = true;

            foreach (string field in Wired)
            {
                SerializedProperty prop = so.FindProperty(field);

                if (prop == null || prop.objectReferenceValue == null)
                {
                    Debug.LogError($"[광산 성적표] '{field}' 가 비어 있습니다. 저장이 안 먹었습니다.");
                    ok = false;
                }
            }

            return ok;
        }
    }
}
