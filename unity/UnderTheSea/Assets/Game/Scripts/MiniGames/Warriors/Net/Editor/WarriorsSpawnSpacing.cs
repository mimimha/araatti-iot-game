using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🧍 <b>두 사람의 시작 자리를 벌린다.</b>
    ///
    /// 지금은 둘이 2m 밖에 안 떨어져 있어 카메라에서 실루엣이 겹친다. 화면만 보고는
    /// <b>두 명인지 한 명인지</b> 바로 읽히지 않는다.
    ///
    /// ⚠ <b>과하게 벌리지 않는다.</b> 1라운드는 둘이 같은 몬스터 무리를 상대하므로 너무 멀면
    ///    협동이 끊긴다. 서로 실루엣을 침범하지 않을 만큼만 띄운다.
    ///
    /// ⚠ 좌우 대칭을 지킨다. 한쪽으로 쏠리면 두 화면이 서로 다른 구도가 된다.
    /// </summary>
    public static class WarriorsSpawnSpacing
    {
        private const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";

        /// <summary>
        /// 두 사람 사이 거리(m). 캐릭터 어깨폭이 1m 남짓이라 3.6m 면 사이에 한 사람이
        /// 더 들어갈 만큼 벌어진다 — 겹쳐 보이지 않으면서 한 화면에 같이 담긴다.
        /// </summary>
        private const float Apart = 3.6f;

        /// <summary>원래 깔려 있던 간격(m). 쓰이지 않는 바깥 자리를 되돌릴 때 쓴다.</summary>
        private const float Original = 2f;

        [MenuItem("Tools/아라아띠/Warriors 두 사람 시작 자리 벌리기")]
        public static void Wire()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            WarriorsSpawnPoint[] points = Ordered();

            if (points.Length < 2)
            {
                Debug.LogError($"[스폰] 자리가 {points.Length}개뿐입니다. 두 개 이상 있어야 합니다.");
                return;
            }

            Debug.Log("[스폰] ───── 옮기기 전 ─────");
            foreach (WarriorsSpawnPoint p in points)
            {
                Debug.Log($"[스폰]   {p.name}  {p.transform.position}");
            }

            // ⚠ **실제로 쓰이는 것은 가운데 쌍뿐이다.**
            //
            //    스포너는 이름순으로 줄 세운 뒤 가운데 둘을 2인에게 준다
            //    (WarriorsPlayerSpawner.ResolveSpawn 의 offset 계산). 그래서 벌려야 하는
            //    것은 가운데 쌍이고, 그 한가운데가 x=0 이어야 두 화면이 좌우 대칭이 된다.
            //
            //    한 번은 x 순으로 앞의 둘을 집어 엉뚱한 쌍(_01·_02)을 옮겼다.
            //    스포너와 같은 규칙으로 줄 세우는 것이 유일하게 맞는 방법이다.
            //
            //    바깥 둘(_01·_04)은 쓰이지 않지만 <b>원래 자리로 되돌린다.</b> 같이 밀면
            //    발판 밖으로 나가고, 3·4인을 붙일 때 허공에서 떨어진다.
            int middle = Mathf.Max(0, (points.Length - 2) / 2);

            float wasApart = Mathf.Abs(
                points[Mathf.Min(middle + 1, points.Length - 1)].transform.position.x -
                points[middle].transform.position.x);

            // ⚠ **앞뒤(z)도 맞춘다.** 좌우만 맞춰 놓았더니 한 사람이 카메라 쪽으로 더
            //    튀어나와 서서, 같은 줄에서 함께 막는 구도로 안 보였다. 화면에서도
            //    한쪽이 크게 보여 시선이 쏠린다. 가운데 쌍의 z 를 같은 값으로 묶는다.
            float line = points[middle].transform.position.z;

            for (int i = 0; i < points.Length; i++)
            {
                float slot = i - (points.Length - 1) * 0.5f;   // -1.5 -0.5 +0.5 +1.5

                // 가운데 쌍은 ±Apart/2, 바깥은 원래 간격(2m)의 자리로.
                float x = i == middle || i == middle + 1
                    ? Mathf.Sign(slot) * Apart * 0.5f
                    : slot * Original;

                Vector3 at = points[i].transform.position;
                at.x = x;
                at.z = line;
                points[i].transform.position = at;

                EditorUtility.SetDirty(points[i]);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ⚠ 저장을 믿지 않고 씬을 다시 열어 확인한다.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            WarriorsSpawnPoint[] check = Ordered();

            float p1 = check[middle].transform.position.x;
            float p2 = check[middle + 1].transform.position.x;
            float now = Mathf.Abs(p2 - p1);
            float centre = (p1 + p2) * 0.5f;
            float depth = Mathf.Abs(check[middle + 1].transform.position.z - check[middle].transform.position.z);

            if (Mathf.Abs(now - Apart) > 0.01f || Mathf.Abs(centre) > 0.01f || depth > 0.01f)
            {
                Debug.LogError(
                    $"[스폰] 저장되지 않았거나 줄이 안 맞습니다. 사이 {now:F2}m (바라던 {Apart:F1}), " +
                    $"한가운데 {centre:F2} (0 이어야 함), 앞뒤 차이 {depth:F2} (0 이어야 함)");
                return;
            }

            Debug.Log("[스폰] ───── 옮긴 뒤 ─────");
            foreach (WarriorsSpawnPoint p in check) Debug.Log($"[스폰]   {p.name}  {p.transform.position}");

            Debug.Log(
                $"[스폰] ✅ 두 사람 사이 {wasApart:F1}m → {now:F1}m · 한가운데 {centre:F1} · " +
                $"앞뒤 같은 줄(z {line:F1})");
        }

        /// <summary>
        /// 스포너와 <b>똑같은 순서</b>로 자리를 줄 세운다.
        ///
        /// ⚠ WarriorsPlayerSpawner.ResolveSpawnPoints 가 이름을 Ordinal 로 정렬한다.
        ///    여기서 다른 기준(예: x 좌표)으로 세면 엉뚱한 자리를 옮기게 된다.
        /// </summary>
        private static WarriorsSpawnPoint[] Ordered()
        {
            return Object
                .FindObjectsByType<WarriorsSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .OrderBy(point => point.name, System.StringComparer.Ordinal)
                .ToArray();
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }
    }
}
