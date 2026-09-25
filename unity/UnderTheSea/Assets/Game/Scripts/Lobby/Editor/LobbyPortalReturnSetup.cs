using System.Linq;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// **미니게임 포탈마다 "돌아올 때 설 자리" 를 만든다.** 한 번 눌러 쓰는 도구다.
    ///
    /// 게임을 마치고 로비로 돌아온 사람은 그 포탈 앞에, 포탈을 등지고 선다
    /// (<c>MiniGamePortal.returnPoint</c> → <c>PlayerSpawner.TryReturnSpawn</c>).
    ///
    /// <b>왜 자리를 따로 두는가.</b> 포탈 입구도 소용돌이도 회전이 0 이라 씬 어디에도 "포탈 앞" 이
    /// 어느 쪽인지가 적혀 있지 않다. 계산으로 알아낼 수 없어서 자리를 하나 두고 눈으로 정하게 한다.
    ///
    /// <b>처음 자리는 짐작한다.</b> 포탈마다 앞에 이정표를 세워 두었으므로, 가장 가까운 이정표 쪽을
    /// "앞" 으로 본다. 거기서 <see cref="Distance"/> 만큼 떨어진 바닥에, 포탈을 등지게 둔다.
    /// 마음에 안 들면 씬에서 <c>ReturnPoint</c> 를 옮기고 돌리면 된다. 파란 화살표가 포탈 반대쪽을 보게.
    ///
    /// ⚠ <b>이미 있는 자리는 덮어쓰지 않는다.</b> 손으로 맞춘 자리가 도구를 다시 누를 때마다
    ///    지워지면 안 된다. 다시 짐작하게 하려면 <c>ReturnPoint</c> 를 지우고 누른다.
    ///
    /// ⚠ <b>바닥은 포탈 높이에 가장 가까운 면을 고른다.</b> 위에서 쏴서 처음 닿는 면을 쓰면 절벽 위나
    ///    나뭇가지에 선다. 이정표를 놓을 때 실제로 그랬다. 해안선 투명벽(<c>Blocker_</c>)도 바닥이 아니다.
    ///
    /// 메뉴: Tools > 아라아띠 > 포탈 앞 돌아오는 자리 만들기
    /// </summary>
    public static class LobbyPortalReturnSetup
    {
        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";
        private const string PointName = "ReturnPoint";

        /// <summary>
        /// 포탈에서 얼마나 떨어져 설까(m).
        ///
        /// 포탈의 입장 거리(기본 4m)보다 멀어야 한다. 가까우면 돌아오자마자 "F 로 들어가기" 안내가 뜨고,
        /// 무심코 F 를 누르면 방금 끝낸 게임에 다시 들어간다.
        /// </summary>
        private const float Distance = 5f;

        /// <summary>이 거리 안에 이정표가 없으면 짐작할 근거가 없다고 본다(m).</summary>
        private const float SignpostSearchRadius = 30f;

        [MenuItem("Tools/아라아띠/포탈 앞 돌아오는 자리 만들기")]
        public static void Run()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != LobbyScenePath)
            {
                EditorUtility.DisplayDialog("포탈 앞 돌아오는 자리",
                    "Lobby 씬을 열고 다시 눌러 주세요.\n" + LobbyScenePath, "확인");
                return;
            }

            int made = Place();
            if (made > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>
        /// 배치 모드용. Lobby 를 열고, 자리를 만들고, 저장한다.
        ///
        ///   Unity.exe -batchmode -quit -projectPath ... -executeMethod Lobby.Editor.LobbyPortalReturnSetup.RunBatch
        /// </summary>
        public static void RunBatch()
        {
            Scene scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            int made = Place();
            if (made > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        private static int Place()
        {
            Physics.SyncTransforms();

            MiniGamePortal[] portals = Object.FindObjectsByType<MiniGamePortal>(FindObjectsInactive.Include);
            SignpostTeleport[] signposts = Object.FindObjectsByType<SignpostTeleport>(FindObjectsInactive.Include);

            int made = 0;

            foreach (MiniGamePortal portal in portals.OrderBy(p => p.name))
            {
                if (portal.ReturnPoint != null)
                {
                    Debug.Log($"[돌아오는 자리] '{portal.name}' 은 이미 있습니다. 건드리지 않습니다. " +
                              $"({portal.ReturnPoint.position.ToString("F2")})");
                    continue;
                }

                Vector3 origin = portal.transform.position;

                if (!TryGuessFront(origin, signposts, out Vector3 front, out string basis))
                {
                    Debug.LogWarning($"[돌아오는 자리] '{portal.name}' 은 {SignpostSearchRadius}m 안에 이정표가 없어 " +
                                     "앞이 어느 쪽인지 짐작할 수 없습니다. 손으로 만들어 주세요.", portal);
                    continue;
                }

                Vector3 at = origin + front * Distance;
                at.y = GroundNear(at, origin.y, portal.transform);

                var point = new GameObject(PointName).transform;
                point.SetParent(portal.transform, worldPositionStays: false);
                point.position = at;
                point.rotation = Quaternion.LookRotation(front, Vector3.up);

                var so = new SerializedObject(portal);
                so.FindProperty("returnPoint").objectReferenceValue = point;
                so.ApplyModifiedPropertiesWithoutUndo();

                Debug.Log($"[돌아오는 자리] '{portal.name}' ({portal.SceneName}) — {at.ToString("F2")}, " +
                          $"바라보는 쪽 {point.eulerAngles.y:0}°. 근거: {basis}");
                made++;
            }

            Debug.Log($"[돌아오는 자리] {made}곳을 만들었습니다. 씬에서 보고 필요하면 옮겨 주세요.");
            return made;
        }

        /// <summary>포탈에서 가장 가까운 이정표 쪽을 "앞" 으로 본다. 좌우 평면에서만.</summary>
        private static bool TryGuessFront(
            Vector3 origin, SignpostTeleport[] signposts, out Vector3 front, out string basis)
        {
            front = default;
            basis = null;

            SignpostTeleport nearest = null;
            float best = SignpostSearchRadius;

            foreach (SignpostTeleport post in signposts)
            {
                Vector3 gap = post.transform.position - origin;
                gap.y = 0f;

                float d = gap.magnitude;
                if (d > 0.5f && d < best)
                {
                    best = d;
                    nearest = post;
                }
            }

            if (nearest == null) return false;

            Vector3 dir = nearest.transform.position - origin;
            dir.y = 0f;
            front = dir.normalized;
            basis = $"이정표 \"{nearest.DisplayName}\" 쪽 ({best:0.0}m)";
            return true;
        }

        /// <summary>
        /// 그 자리의 바닥 높이. 위에서 아래로 쏴서 닿는 면들 가운데 <b>포탈 높이에 가장 가까운</b> 것을 쓴다.
        /// 아무것도 안 닿으면 포탈 높이를 그대로 쓴다.
        /// </summary>
        private static float GroundNear(Vector3 at, float referenceY, Transform ignore)
        {
            Vector3 from = new Vector3(at.x, referenceY + 20f, at.z);
            RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 60f, ~0, QueryTriggerInteraction.Ignore);

            float bestY = referenceY;
            float bestGap = float.MaxValue;

            foreach (RaycastHit hit in hits)
            {
                Transform t = hit.collider.transform;
                if (t.IsChildOf(ignore)) continue;
                if (IsBlocker(t)) continue;

                float gap = Mathf.Abs(hit.point.y - referenceY);
                if (gap < bestGap)
                {
                    bestGap = gap;
                    bestY = hit.point.y;
                }
            }

            // 바닥에 딱 붙이면 캐릭터 발이 바닥 속에서 시작할 수 있다. 조금 띄워 두면 중력이 앉힌다.
            return bestY + 0.1f;
        }

        private static bool IsBlocker(Transform t)
        {
            for (Transform p = t; p != null; p = p.parent)
            {
                if (p.name.StartsWith("Blocker_") || p.name == "WaterBlockers" || p.name == "OceanCollider")
                {
                    return true;
                }
            }

            return false;
        }
    }
}
