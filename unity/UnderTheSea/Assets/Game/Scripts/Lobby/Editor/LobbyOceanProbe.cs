using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// 바다 주변 지형을 재기만 하는 도구. **씬을 고치지 않는다.**
    ///
    /// 플레이어가 바다에 들어가면 빠지고 못 나온다. 막으려면 먼저
    /// "바다가 어디서 시작하는지" 와 "빠지면 어디에 서 있게 되는지" 를 알아야 한다.
    /// 눈으로 씬을 볼 수 없으니 레이로 재서 숫자로 남긴다.
    /// </summary>
    public static class LobbyOceanProbe
    {
        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";

        /// <summary>스폰 지점 근처. 여기서 사방으로 훑는다.</summary>
        private static readonly Vector3 Origin = new Vector3(20.84f, 1.73f, 48.56f);

        [MenuItem("Tools/아라아띠/로비 바다 지형 재기")]
        public static void Probe()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || scene.path != LobbyScenePath)
            {
                scene = EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            }

            // 1) 스폰에서 사방으로 뻗으며 지면이 어떻게 변하는지 본다.
            Debug.Log("[바다] === 스폰에서 방향별 지형 단면 (2m 간격) ===");
            var directions = new (string name, Vector3 dir)[]
            {
                ("북 +Z", Vector3.forward),
                ("남 -Z", Vector3.back),
                ("동 +X", Vector3.right),
                ("서 -X", Vector3.left),
            };

            foreach ((string name, Vector3 dir) in directions)
            {
                var line = new List<string>();
                string firstOcean = null;
                float firstOceanDist = -1f;

                for (float d = 0f; d <= 160f; d += 2f)
                {
                    Vector3 at = Origin + dir * d;
                    if (!TrySample(at, out float y, out Collider hit))
                    {
                        line.Add($"{d:F0}m:없음");
                        continue;
                    }

                    line.Add($"{d:F0}m:{y:F1}({Short(hit)})");

                    if (firstOcean == null && hit != null && hit.gameObject.name == "OceanCollider")
                    {
                        firstOcean = $"{d:F0}m 지점, 높이 {y:F2}";
                        firstOceanDist = d;
                    }
                }

                Debug.Log($"[바다] {name}\n    " + string.Join("  ", line));
                Debug.Log($"[바다] {name} — OceanCollider 를 처음 밟는 곳: " +
                          (firstOcean ?? "없음") +
                          (firstOceanDist >= 0 ? "" : " (이 방향엔 바다가 없다)"));
            }

            // 2) 넓게 훑어 무엇을 밟게 되는지 종류별로 센다.
            Debug.Log("[바다] === 넓은 범위 표본 (x -160..200, z -60..260, 4m 격자) ===");
            var counts = new Dictionary<string, int>();
            var heightByCollider = new Dictionary<string, (float min, float max)>();
            int none = 0;

            for (float x = -160f; x <= 200f; x += 4f)
            {
                for (float z = -60f; z <= 260f; z += 4f)
                {
                    if (!TrySample(new Vector3(x, 0f, z), out float y, out Collider hit))
                    {
                        none++;
                        continue;
                    }

                    string key = hit != null ? hit.gameObject.name : "?";
                    counts.TryGetValue(key, out int c);
                    counts[key] = c + 1;

                    if (heightByCollider.TryGetValue(key, out var range))
                    {
                        heightByCollider[key] = (Mathf.Min(range.min, y), Mathf.Max(range.max, y));
                    }
                    else
                    {
                        heightByCollider[key] = (y, y);
                    }
                }
            }

            foreach (var kv in counts.OrderByDescending(k => k.Value).Take(12))
            {
                var r = heightByCollider[kv.Key];
                Debug.Log($"[바다]   {kv.Key,-28} {kv.Value,6}칸   높이 {r.min:F2} ~ {r.max:F2}");
            }

            Debug.Log($"[바다]   (아무것도 못 맞춘 칸: {none})");

            // 3) OceanCollider 자체 정보
            GameObject ocean = scene.GetRootGameObjects()
                .SelectMany(g => g.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(t => t.name == "OceanCollider")?.gameObject;

            if (ocean != null)
            {
                Collider c = ocean.GetComponent<Collider>();
                Renderer r = ocean.GetComponent<Renderer>();
                Debug.Log($"[바다] OceanCollider — 위치 {ocean.transform.position} · 스케일 {ocean.transform.lossyScale} " +
                          $"· 회전 {ocean.transform.rotation.eulerAngles} · 콜라이더 {(c != null && c.enabled ? "켜짐" : "꺼짐")} " +
                          $"· 렌더러 {(r != null && r.enabled ? "켜짐" : "꺼짐")} · bounds {(c != null ? c.bounds.ToString() : "-")}");
            }
        }

        /// <summary>한 점 위에서 아래로 쏴, 밟고 설 면의 높이와 그 콜라이더를 돌려준다.</summary>
        private static bool TrySample(Vector3 at, out float y, out Collider hit)
        {
            y = float.MinValue;
            hit = null;

            RaycastHit[] hits = Physics.RaycastAll(
                new Vector3(at.x, 120f, at.z), Vector3.down, 400f, ~0, QueryTriggerInteraction.Ignore);

            foreach (RaycastHit h in hits)
            {
                if (h.point.y > y)
                {
                    y = h.point.y;
                    hit = h.collider;
                }
            }

            return hit != null;
        }

        private static string Short(Collider c)
        {
            if (c == null) return "?";
            string n = c.gameObject.name;
            return n.Length <= 12 ? n : n.Substring(0, 12);
        }

        /// <summary>배치 모드 진입점.</summary>
        public static void ProbeFromCommandLine()
        {
            Probe();
        }
    }
}
