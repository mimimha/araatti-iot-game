using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🐙 크라켄 겉모습을 <b>뼈가 든 모델로 바꾼다.</b> 2라운드 머리와 3라운드 최종 형태 둘 다.
    ///
    /// 원래 두 모델(<c>Meshy_AI_…fbx</c>)은 <b>뼈도 애니메이션도 없는 정지 메시</b>라 다리가 한 번도
    /// 움직이지 않았다. 같은 메시에 블렌더로 팔마다 뼈 사슬을 넣어 <c>*_Rigged.fbx</c> 를 만들었고
    /// (만든 방법은 <c>art/tools/warriors_kraken_rig.py</c>), 이 도구가 프리팹의 겉모습만 갈아 끼운다.
    ///
    /// <code>
    ///   자리          옛 겉모습 것을 그대로
    ///   크기          옛 겉모습의 **월드 크기에 맞춰 다시 계산** (파일마다 단위가 달라 숫자를 옮기면 86배가 된다)
    ///   방향          옛 모델과 **같은 모양의 상자**가 되는 90도 회전을 찾아 덧붙임 (블렌더는 Z-up)
    ///   재질          옛 겉모습이 쓰던 것을 그대로
    ///   움직임        WarriorsKrakenLegs 를 붙인다 (잔물결 · 움찔 · 포효)
    /// </code>
    ///
    /// 여러 번 돌려도 같다. 이미 뼈가 든 겉모습이면 크기 · 방향만 다시 맞춘다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod Warriors.Net.Editor.WarriorsKrakenRigSwap.Swap
    /// </summary>
    public static class WarriorsKrakenRigSwap
    {
        private const string BossPrefab = "Assets/Game/Prefabs/MiniGames/Warriors/Boss/WarriorsKrakenBoss.prefab";
        private const string VisualName = "MeshyVisual";

        /// <summary>갈아 끼울 겉모습 하나. 두 라운드가 서로 다른 모델을 쓴다.</summary>
        private readonly struct Form
        {
            public readonly string Parent;     // 프리팹에서 겉모습이 달린 오브젝트
            public readonly string Rigged;     // 뼈가 든 새 모델
            public readonly string Original;   // 옛 모델 (크기 · 방향 기준)
            public readonly float Scale;       // 프리팹에 원래 박혀 있던 배율
            public readonly Vector3 Home;      // 프리팹에 원래 박혀 있던 자리
            public readonly Vector3 Turn;      // 모델 축 기준 덧회전
            public readonly Vector3 Flip;      // **월드 축 기준** 덧회전 (눈으로 보고 정한다)

            public Form(string parent, string rigged, string original, float scale, Vector3 home, Vector3 turn, Vector3 flip)
            {
                Parent = parent; Rigged = rigged; Original = original; Scale = scale;
                Home = home; Turn = turn; Flip = flip;
            }
        }

        private static readonly Form[] Forms =
        {
            new Form("FinalKrakenForm",
                "Assets/Game/Art/MiniGames/Warriors/Models/KrakenFinal/KrakenFinal_Rigged.fbx",
                "Assets/Game/Art/MiniGames/Warriors/Models/KrakenFinal/Meshy_AI_Octohex_the_Furious_0911011605_texture.fbx",
                864.08496f, new Vector3(0f, 2.891178f, 0f), new Vector3(90f, 0f, 0f), Vector3.zero),

            new Form("TentaclePhaseHead",
                "Assets/Game/Art/MiniGames/Warriors/Models/KrakenPhase2/KrakenPhase2_Rigged.fbx",
                "Assets/Game/Art/MiniGames/Warriors/Models/KrakenPhase2/Meshy_AI_Grumpy_Grape_Octopus_0911005752_texture.fbx",
                // ⚠ 월드 X 180도 — **위아래와 앞뒤가 한 번에 뒤집힌다.** 화면에서 머리가 거꾸로 매달린 채
                //    등을 보이고 있었다. 둘 다 이 한 번으로 바로 선다.
                575f, new Vector3(0f, 2.1471086f, 0f), new Vector3(90f, 180f, 0f), new Vector3(180f, 0f, 0f)),
        };

        /// <summary>
        /// 프리팹에 <b>원래 박혀 있던</b> 겉모습 회전. 두 형태가 같은 값이다.
        /// 방향 보정은 늘 이 값에서 출발한다 — 지금 값에 곱하면 돌릴 때마다 90도씩 더 돌아간다.
        /// </summary>
        private static readonly Quaternion OriginalVisualRotation = new Quaternion(0f, 0.7071068f, 0.7071068f, 0f);

        [MenuItem("Tools/아라아띠/Warriors 크라켄에 뼈 넣기")]
        public static void Swap()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefab);
            bool changed = false;

            try
            {
                foreach (Form form in Forms) changed |= SwapOne(root, form);

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
                    Debug.Log($"[KrakenRig] {BossPrefab} 을 저장했습니다.");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool SwapOne(GameObject root, Form form)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(form.Rigged);
            GameObject originalAsset = AssetDatabase.LoadAssetAtPath<GameObject>(form.Original);

            if (model == null || originalAsset == null)
            {
                Debug.LogError($"[KrakenRig] {form.Parent} — 모델을 찾지 못했습니다 " +
                               $"(새 {model != null} · 옛 {originalAsset != null}).");
                return false;
            }

            Transform parent = FindChild(root.transform, form.Parent);
            if (parent == null)
            {
                Debug.LogError($"[KrakenRig] 프리팹에서 {form.Parent} 를 찾지 못했습니다.");
                return false;
            }

            Transform old = FindChild(parent, VisualName);
            if (old == null)
            {
                Debug.LogError($"[KrakenRig] {form.Parent} 밑에서 {VisualName} 을 찾지 못했습니다.");
                return false;
            }

            // 화면에서 차지해야 하는 크기. 옛 모델 파일과 프리팹에 박혀 있던 배율로 되짚는다.
            float target = Longest(LocalBox(originalAsset.transform)) * form.Scale;
            if (target <= 0.0001f)
            {
                Debug.LogError($"[KrakenRig] {form.Parent} — 옛 모델 크기를 재지 못했습니다.");
                return false;
            }

            // 이미 뼈가 들어 있으면 크기 · 방향만 다시 맞춘다.
            if (old.GetComponentInChildren<SkinnedMeshRenderer>(true) != null)
            {
                old.localRotation = Upright(form);
                Fit(old, target);
                Centre(old, originalAsset.transform, form);
                Attach(old);

                Debug.Log($"[KrakenRig] {form.Parent} — 이미 뼈가 있습니다. 크기 · 방향만 다시 맞췄습니다. " +
                          $"배율 {old.localScale.x:F3} · 가장 긴 변 {Longest(LocalBox(old)) * old.localScale.x:F2}m · " +
                          $"뼈 {old.GetComponentInChildren<SkinnedMeshRenderer>(true).bones.Length}개 · 팔 {CountLegs(old)}개");
                return true;
            }

            Vector3 localPos = old.localPosition;
            Material material = null;
            var oldRenderer = old.GetComponentInChildren<MeshRenderer>(true);
            if (oldRenderer != null) material = oldRenderer.sharedMaterial;

            var created = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
            created.name = VisualName;
            created.transform.localPosition = localPos;
            created.transform.localScale = Vector3.one;
            created.transform.localRotation = Upright(form);

            if (!Fit(created.transform, target))
            {
                Debug.LogError($"[KrakenRig] {form.Parent} — 새 모델 크기를 재지 못했습니다.");
                Object.DestroyImmediate(created);
                return false;
            }

            Centre(created.transform, originalAsset.transform, form);

            var skin = created.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (skin != null && material != null) skin.sharedMaterial = material;
            Attach(created.transform);

            Debug.Log($"[KrakenRig] {form.Parent} — 뼈 모델로 교체. 배율 {created.transform.localScale.x:F3} · " +
                      $"가장 긴 변 {Longest(LocalBox(created.transform)) * created.transform.localScale.x:F2}m (목표 {target:F2}m) · " +
                      $"뼈 {(skin != null ? skin.bones.Length : 0)}개 · 팔 {CountLegs(created.transform)}개 · " +
                      $"재질 {(material != null ? material.name : "없음")}");

            Object.DestroyImmediate(old.gameObject);
            return true;
        }

        /// <summary>움직이는 부품을 붙이고, 뼈가 돌아도 안 사라지게 한다.</summary>
        private static void Attach(Transform visual)
        {
            if (visual.GetComponent<WarriorsKrakenLegs>() == null)
                visual.gameObject.AddComponent<WarriorsKrakenLegs>();

            var skin = visual.GetComponentInChildren<SkinnedMeshRenderer>(true);
            // 뼈가 돌면 원래 경계 밖으로 나간다. 경계로 컬링하면 화면 끝에서 통째로 사라진다.
            if (skin != null) skin.updateWhenOffscreen = true;
        }

        /// <summary>
        /// 새 모델이 <b>옛 모델과 같은 자세로 서게</b> 하는 회전.
        ///
        /// 두 파일은 같은 메시인데 축 변환이 다르다(옛것은 Meshy, 새것은 블렌더 내보내기).
        /// 그래서 <b>겉모습 안에서 메시가 차지하는 상자 모양</b>을 견줘 90도 단위 24가지 중 맞는 것을 고른다.
        ///
        /// ⚠ 상자는 <b>가장 긴 변을 1 로 맞춘 뒤</b> 견준다. 두 파일은 단위가 달라 그냥 빼면
        ///   모양이 아니라 크기 차이를 본다 — 실제로 그래서 엉뚱한 90도를 골랐다.
        /// </summary>
        /// <summary>
        /// 새 모델이 옛 모델과 같은 자세로 서게 하는 회전.
        ///
        /// ⚠ <b>자동으로 못 고른다.</b> 상자 모양도, 속을 채운 덩어리 모양(8×8×8)도 견줘 봤지만
        ///    이 모델들은 <b>좌우 대칭</b>이라 앞뒤가 뒤집힌 회전과 구별되지 않는다.
        ///    실제로 2라운드 머리가 <b>등을 보인 채</b> 서 있었다(얼굴이 안 보이고 빨판 안쪽이 보였다).
        ///    그래서 <see cref="Form.Turn"/> 에 <b>눈으로 보고 정한 값</b>을 적어 둔다.
        ///    새 모델을 갈아 끼울 때 얼굴이 안 보이면 그 값의 Y 를 180 더하거나 빼면 된다.
        /// </summary>
        private static Quaternion Upright(Form form) =>
            Quaternion.Euler(form.Flip) * OriginalVisualRotation * Quaternion.Euler(form.Turn);

        /// <summary>한 변을 몇 칸으로 나눠 모양을 견줄 것인가.</summary>
        private const int Cells = 8;

        /// <summary>
        /// 모델을 <paramref name="turn"/> 만큼 돌린 뒤 <b>제 상자 안에서 어느 칸을 채우는지</b> 표로 만든다.
        /// 크기와 상관없이 모양만 남으므로, 파일 단위가 달라도 두 모델을 견줄 수 있다.
        /// </summary>
        private static bool[] Occupancy(Transform root, Quaternion turn)
        {
            var cells = new bool[Cells * Cells * Cells];
            var points = new List<Vector3>();

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;
                if (mesh == null) continue;

                Matrix4x4 m = Matrix4x4.Rotate(turn) * root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                Vector3[] verts = mesh.vertices;

                // 5천 개를 다 보지 않아도 모양은 충분히 나온다. 일정 간격으로 훑는다.
                int step = Mathf.Max(1, verts.Length / 2000);
                for (int i = 0; i < verts.Length; i += step) points.Add(m.MultiplyPoint3x4(verts[i]));
            }

            if (points.Count == 0) return cells;

            Vector3 min = points[0], max = points[0];
            foreach (Vector3 p in points)
            {
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }

            Vector3 size = max - min;
            size = new Vector3(Mathf.Max(size.x, 1e-5f), Mathf.Max(size.y, 1e-5f), Mathf.Max(size.z, 1e-5f));

            foreach (Vector3 p in points)
            {
                int cx = Mathf.Clamp((int)((p.x - min.x) / size.x * Cells), 0, Cells - 1);
                int cy = Mathf.Clamp((int)((p.y - min.y) / size.y * Cells), 0, Cells - 1);
                int cz = Mathf.Clamp((int)((p.z - min.z) / size.z * Cells), 0, Cells - 1);
                cells[(cx * Cells + cy) * Cells + cz] = true;
            }

            return cells;
        }

        /// <summary>
        /// 새 겉모습을 <b>옛 모델이 차지하던 자리</b>에 놓는다.
        ///
        /// 두 파일은 원점(피벗)이 다를 수 있어 자리 숫자를 그대로 옮기면 크라켄이 뜨거나 가라앉는다.
        /// 그래서 숫자가 아니라 <b>메시 상자의 한가운데</b>를 맞춘다.
        /// </summary>
        private static void Centre(Transform visual, Transform originalAsset, Form form)
        {
            // 옛 모델이 있던 상자 한가운데 (부모 기준)
            Vector3 wantCentre = OriginalVisualRotation * (BoxCentre(originalAsset) * form.Scale) + form.Home;

            // 지금 새 모델의 상자 한가운데
            Vector3 haveCentre = visual.localRotation * Vector3.Scale(BoxCentre(visual), visual.localScale)
                                 + visual.localPosition;

            visual.localPosition += wantCentre - haveCentre;

            Debug.Log($"[KrakenRig] {form.Parent} 자리 — 옛 가운데 {wantCentre:F2} · 옮긴 뒤 {visual.localPosition:F2}");
        }

        /// <summary>메시 상자의 한가운데 (뿌리 기준, 뿌리 자신의 배율은 뺀 값).</summary>
        private static Vector3 BoxCentre(Transform root)
        {
            Bounds? total = Box(root);
            return total?.center ?? Vector3.zero;
        }

        /// <summary>가장 긴 변이 <paramref name="target"/> 미터가 되도록 배율을 정한다.</summary>
        private static bool Fit(Transform t, float target)
        {
            t.localScale = Vector3.one;
            float unit = Longest(LocalBox(t));
            if (unit <= 0.00001f) return false;

            t.localScale = Vector3.one * (target / unit);
            return true;
        }

        /// <summary>이름이 <c>Leg&lt;번호&gt;_1</c> 인 뼈를 세어 팔 개수를 알아본다.</summary>
        private static int CountLegs(Transform t)
        {
            int count = 0;
            foreach (Transform one in t.GetComponentsInChildren<Transform>(true))
                if (one.name.StartsWith("Leg") && one.name.EndsWith("_1")) count++;

            return count;
        }

        /// <summary>겉모습 안에서 메시가 차지하는 상자. <b>자식의 회전까지 넣어</b> 재고, 뿌리 자신의 배율은 뺀다.</summary>
        private static Vector3 LocalBox(Transform root) => Box(root)?.size ?? Vector3.zero;

        private static Bounds? Box(Transform root)
        {
            Bounds? total = null;

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = r is SkinnedMeshRenderer skin ? skin.sharedMesh
                    : r.GetComponent<MeshFilter>() != null ? r.GetComponent<MeshFilter>().sharedMesh : null;

                if (mesh == null) continue;

                Matrix4x4 m = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                Bounds b = mesh.bounds;

                for (int corner = 0; corner < 8; corner++)
                {
                    var p = new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);

                    Vector3 w = m.MultiplyPoint3x4(p);
                    if (total.HasValue) { Bounds t2 = total.Value; t2.Encapsulate(w); total = t2; }
                    else total = new Bounds(w, Vector3.zero);
                }
            }

            return total;
        }

        private static Vector3 Normalized(Vector3 size)
        {
            float longest = Longest(size);
            return longest > 0.00001f ? size / longest : size;
        }

        private static float Longest(Vector3 v) =>
            Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;

            return null;
        }
    }
}
