using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🐙 <b>베는 촉수를 크라켄 머리의 진짜 팔에 묶는다.</b> (2라운드)
    ///
    /// 예전에는 촉수 자리마다 프리팹이 코드로 만든 12각 튜브가 서 있었다 — 화면에서 <b>막대</b>로 보였고
    /// 뒤의 크라켄 머리와 따로 놀았다. 머리 모델(<c>KrakenPhase2_Rigged</c>)이 이제 자기 팔 넷을
    /// 뼈로 가지고 있으므로, <b>그 팔이 곧 베는 대상</b>이다.
    ///
    /// <code>
    ///   임시 튜브   지운다 (TentacleMesh)
    ///   짝짓기      촉수 자리와 팔을 **가로 위치 순서**로 하나씩 맞춘다
    ///   잇기        WarriorsTentacleArmLink 를 붙이고 팔 번호를 꽂는다
    ///   자리        판정 상자를 그 팔 끝 아래로 옮긴다 (런타임에도 팔을 따라간다)
    /// </code>
    ///
    /// 판정 · 약점 · 반격 규칙은 <b>하나도 건드리지 않는다.</b> <c>WarriorsTarget</c> 과 서버가 하던 그대로다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod Warriors.Net.Editor.WarriorsTentacleRig.Rig
    /// </summary>
    public static class WarriorsTentacleRig
    {
        private const string BossPrefab = "Assets/Game/Prefabs/MiniGames/Warriors/Boss/WarriorsKrakenBoss.prefab";
        private const string HeadName = "TentaclePhaseHead";
        private const string MeshName = "TentacleMesh";

        /// <summary>팔 끝이 겨냥할 높이(m). 판정 상자 위 이만큼을 향해 팔이 뻗어 온다.</summary>
        private const float AimHeight = 3f;

        /// <summary>
        /// 팔 끝 넷의 <b>가운데가 와야 하는 자리.</b> 옛 임시 튜브가 서 있던 자리에서 뽑았다 —
        /// 실측으로 튜브는 x ±1.96 · ±5.88, z 6.44 · 7.35 였다. 칼이 닿는 거리이자 화면 구도의 기준이다.
        ///
        /// ⚠ 높이는 판정 상자가 <see cref="DropFromTip"/> 만큼 내려간 뒤 <b>사람 키 높이</b>가 되도록 잡는다.
        /// </summary>
        /// <summary>
        /// 머리가 설 자리. **3라운드 최종 크라켄과 같은 z(11)** 로 맞춘다 —
        /// 원래 2라운드 머리는 z 8.5 라 3라운드보다 2.5m 앞에 있었고, 화면에서 너무 가까워 보였다.
        /// </summary>
        private static readonly Vector3 HeadHome = new Vector3(0f, 0f, 11f);

        /// <summary>촉수 판정 상자가 원래 서 있던 자리 (프리팹 실측). 칼이 닿는 거리다.</summary>
        private static readonly Vector3[] TentacleHome =
        {
            new Vector3(-4.2f, 0.05f, 5.25f),
            new Vector3(-1.4f, 0.05f, 4.6f),
            new Vector3(1.4f, 0.05f, 4.6f),
            new Vector3(4.2f, 0.05f, 5.25f),
        };

        [MenuItem("Tools/아라아띠/Warriors 촉수를 크라켄 팔에 잇기")]
        public static void Rig()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(BossPrefab);

            try
            {
                Transform head = FindChild(root.transform, HeadName);
                if (head == null)
                {
                    Debug.LogError($"[TentacleRig] 프리팹에서 {HeadName} 를 찾지 못했습니다.");
                    return;
                }

                var legs = head.GetComponentInChildren<WarriorsKrakenLegs>(true);
                if (legs == null)
                {
                    Debug.LogError($"[TentacleRig] {HeadName} 에 WarriorsKrakenLegs 가 없습니다. " +
                                   "먼저 '크라켄에 뼈 넣기' 를 돌리세요.");
                    return;
                }

                // 머리의 팔 끝 — 뼈 이름으로 찾는다 (에디터에서는 Awake 가 안 돌아 부품에 못 묻는다).
                var tips = new SortedDictionary<int, Transform>();
                foreach (Transform t in head.GetComponentsInChildren<Transform>(true))
                {
                    if (!t.name.StartsWith("Leg")) continue;

                    int bar = t.name.IndexOf('_');
                    if (bar < 4) continue;
                    if (!int.TryParse(t.name.Substring(3, bar - 3), out int index)) continue;
                    if (!int.TryParse(t.name.Substring(bar + 1), out int seg)) continue;

                    // 사슬의 마지막 마디가 팔 끝이다. 더 큰 번호가 나오면 그것으로 바꾼다.
                    if (!tips.TryGetValue(index, out Transform have) || Segment(have) < seg) tips[index] = t;
                }

                if (tips.Count == 0)
                {
                    Debug.LogError("[TentacleRig] 머리에서 팔 뼈(Leg<번호>_<마디>)를 찾지 못했습니다.");
                    return;
                }

                // 머리는 **원래 자리 그대로** 둔다. 팔을 사람 쪽으로 옮기는 대신
                // 팔이 판정 상자 쪽으로 뻗어 오게 했다(WarriorsTentacleArmLink).
                head.localPosition = HeadHome;

                var arms = new List<KeyValuePair<int, Transform>>(tips);
                arms.Sort((a, b) => a.Value.position.x.CompareTo(b.Value.position.x));

                var tentacles = new List<Transform>();
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("Tentacle_")) tentacles.Add(t);
                tentacles.Sort((a, b) => a.position.x.CompareTo(b.position.x));

                if (tentacles.Count == 0)
                {
                    Debug.LogError("[TentacleRig] 촉수(Tentacle_*)를 찾지 못했습니다.");
                    return;
                }

                Debug.Log($"[TentacleRig] 촉수 {tentacles.Count}개 · 머리 팔 {arms.Count}개 — 가로 순서로 짝짓습니다.");

                int linked = 0;
                for (int i = 0; i < tentacles.Count; i++)
                {
                    Transform arm = tentacles[i];
                    KeyValuePair<int, Transform> pick = arms[Mathf.Min(i, arms.Count - 1)];

                    // 임시 튜브를 지운다. 뼈를 심어 둔 것도 함께 사라진다.
                    Transform visual = FindChild(arm, MeshName);
                    Vector3 wasAt = arm.position;

                    if (visual != null) Object.DestroyImmediate(visual.gameObject);

                    var link = arm.GetComponent<WarriorsTentacleArmLink>();
                    if (link == null) link = arm.gameObject.AddComponent<WarriorsTentacleArmLink>();

                    var so = new SerializedObject(link);
                    so.FindProperty("head").objectReferenceValue = legs;
                    so.FindProperty("leg").intValue = pick.Key;
                    so.ApplyModifiedPropertiesWithoutUndo();

                    // 판정 상자는 **원래 자리**로. 칼이 닿는 거리에 있어야 한다.
                    arm.localPosition = TentacleHome[Mathf.Min(i, TentacleHome.Length - 1)];

                    Debug.Log($"[TentacleRig] {arm.name} → 머리 Leg{pick.Key} " +
                              $"(팔 끝 {pick.Value.position:F2} · 촉수 자리 {wasAt:F2} → {arm.position:F2})");
                    linked++;
                }

                PrefabUtility.SaveAsPrefabAsset(root, BossPrefab);
                Debug.Log($"[TentacleRig] 촉수 {linked}개를 머리 팔에 이었습니다. {BossPrefab} 을 저장했습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static int Segment(Transform t)
        {
            int bar = t.name.IndexOf('_');
            return bar >= 0 && int.TryParse(t.name.Substring(bar + 1), out int seg) ? seg : -1;
        }

        private static Transform FindChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;

            return null;
        }
    }
}
