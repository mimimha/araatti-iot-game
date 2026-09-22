#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// **무쌍 씬의 하늘을 우리 것으로 만든다.**
    ///
    /// 왜 필요한가. 씬은 지금까지 Unity <b>기본 Default-Skybox</b>(내장 에셋)를 그대로 썼다.
    /// 내장 에셋은 값을 고칠 수 없어서 "하늘이 비 내릴 것처럼 어둡다" 를 조정할 손잡이가
    /// 아예 없었다. 같은 Skybox/Procedural 셰이더로 <b>프로젝트 안에</b> 머티리얼을 만들고
    /// 씬에 물리면, 그때부터 밝기·탁도·색을 여기 상수만 고쳐 조절할 수 있다.
    ///
    /// ⚠ 1인 검증 씬(<c>Develop/SeoYeon/WarriorsTest.unity</c>)은 건드리지 않는다.
    /// </summary>
    public static class WarriorsSkySetup
    {
        private const string ScenePath =
            "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";

        private const string SkyFolder = "Assets/Game/Art/MiniGames/Warriors/Sky";
        private const string SkyPath = SkyFolder + "/WarriorsSky.mat";

        // ── 하늘 값. "맑은 한낮의 열대 바다" 를 기준으로 잡았다 ─────────────────────────
        //
        // 기본 Default-Skybox 의 값은 Exposure 1.3 · AtmosphereThickness 1.0 이다.
        // 그 조합이 화면에서 **흐린 날**처럼 읽혔다 — 대기가 두꺼울수록 빛이 흩어져
        // 파랑이 빠지고 잿빛이 된다. 그래서 대기를 얇게, 노출을 밝게 간다.

        /// <summary>대기 두께. 낮을수록 탁한 기운이 걷히고 파랑이 선명해진다. (기본 1.0)</summary>
        private const float AtmosphereThickness = .72f;

        /// <summary>전체 밝기. (기본 1.3)</summary>
        private const float Exposure = 1.62f;

        /// <summary>해 크기. 기본값 그대로 두되 해가 <b>보이게</b> 한다.</summary>
        private const float SunSize = .045f;

        /// <summary>하늘 색조. 기본 회색(0.5)에서 살짝 밝은 하늘색 쪽으로.</summary>
        private static readonly Color SkyTint = new(.54f, .63f, .74f, 1f);

        /// <summary>수평선 아래 색. 바다에 가려 거의 안 보이지만 반사광에 쓰인다.</summary>
        private static readonly Color GroundColor = new(.62f, .60f, .56f, 1f);

        [MenuItem("Warriors/하늘 밝게 맞추기")]
        public static void Apply()
        {
            Material sky = BuildSkyMaterial();

            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            if (!scene.IsValid())
            {
                Debug.LogError($"[하늘] '{ScenePath}' 를 열지 못했습니다.");
                EditorApplication.Exit(1);
                return;
            }

            RenderSettings.skybox = sky;

            // **해를 씬의 방향광에 물린다.** 비워 두면 Unity 가 알아서 가장 밝은 방향광을 쓰지만,
            // 명시해 두면 하늘의 해와 그림자 방향이 어긋날 일이 없다.
            Light sun = FindDirectionalLight(scene);
            if (sun != null)
            {
                RenderSettings.sun = sun;
                Debug.Log($"[하늘] 해를 '{sun.name}' 에 물렸습니다. (강도 {sun.intensity})");
            }
            else
            {
                Debug.LogWarning("[하늘] 씬에서 방향광을 찾지 못했습니다. 해는 Unity 가 고릅니다.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log(
                $"[하늘] 적용 완료 — 대기 {AtmosphereThickness} · 노출 {Exposure} · " +
                $"머티리얼 '{SkyPath}'");
        }

        /// <summary>하늘 머티리얼을 만들거나, 이미 있으면 값만 다시 맞춘다.</summary>
        private static Material BuildSkyMaterial()
        {
            EnsureFolder(SkyFolder);

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            bool created = sky == null;

            if (created)
            {
                Shader shader = Shader.Find("Skybox/Procedural");

                if (shader == null)
                {
                    Debug.LogError("[하늘] 'Skybox/Procedural' 셰이더를 찾지 못했습니다.");
                    EditorApplication.Exit(1);
                    return null;
                }

                sky = new Material(shader) { name = "WarriorsSky" };
                AssetDatabase.CreateAsset(sky, SkyPath);
            }

            sky.SetFloat("_AtmosphereThickness", AtmosphereThickness);
            sky.SetFloat("_Exposure", Exposure);
            sky.SetFloat("_SunSize", SunSize);
            sky.SetFloat("_SunDisk", 2f);            // 2 = High Quality. 해가 보여야 맑은 날로 읽힌다
            sky.SetColor("_SkyTint", SkyTint);
            sky.SetColor("_GroundColor", GroundColor);

            EditorUtility.SetDirty(sky);
            AssetDatabase.SaveAssets();

            Debug.Log($"[하늘] 머티리얼 {(created ? "생성" : "갱신")} — {SkyPath}");
            return sky;
        }

        private static Light FindDirectionalLight(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Light one in root.GetComponentsInChildren<Light>(true))
                {
                    if (one.type == LightType.Directional) return one;
                }
            }

            return null;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string[] parts = path.Split('/');
            string built = parts[0];

            for (int i = 1; i < parts.Length; i++)
            {
                string next = built + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(built, parts[i]);
                built = next;
            }
        }
    }
}
#endif
