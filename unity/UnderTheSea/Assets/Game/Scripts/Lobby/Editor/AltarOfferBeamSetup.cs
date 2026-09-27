using System.IO;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Lobby.Editor
{
    /// <summary>
    /// 제단 봉헌 빛줄기(<see cref="AltarOfferBeam"/>)를 <c>P_HeartAltar</c> 프리팹에 붙인다. 여러 번 눌러도 같은 결과다.
    ///
    /// <code>
    ///   1. 가장자리로 갈수록 옅어지는 부드러운 띠 그림을 만든다   AltarOfferBeam_Soft.png
    ///   2. 더하기 섞기(additive) 재질을 만든다                  AltarOfferBeam.mat  (URP Particles/Unlit)
    ///   3. 프리팹에 부품을 붙이고 재질 · 시작점(제단 돌기둥)을 꽂는다
    /// </code>
    ///
    /// 시작점은 <see cref="AltarVfxController"/> 가 쓰는 돌기둥 렌더러를 그대로 쓴다 — 문양이 켜지는 그 기둥이다.
    ///
    /// ⚠ <b>로비 씬은 건드리지 않는다.</b> 로비에 놓인 제단은 이 프리팹의 인스턴스라 따라온다.
    /// ⚠ 재질을 프리팹에서 가리키므로 빌드에 셰이더가 같이 들어간다. <c>Shader.Find</c> 로 실행 중에 찾지 않는 이유다.
    /// </summary>
    public static class AltarOfferBeamSetup
    {
        private const string PrefabPath = "Assets/Game/Prefabs/HeartAltar/P_HeartAltar.prefab";
        private const string Folder = "Assets/Game/Art/Materials/Altar";
        private const string TexturePath = Folder + "/AltarOfferBeam_Soft.png";
        private const string MaterialPath = Folder + "/AltarOfferBeam.mat";
        private const string ShaderName = "Universal Render Pipeline/Particles/Unlit";

        [MenuItem("Tools/아라아띠/제단 봉헌 빛줄기 설치")]
        public static void Install()
        {
            Texture2D soft = MakeSoftTexture();
            Material material = MakeMaterial(soft);
            if (material == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                AltarVfxController vfx = root.GetComponentInChildren<AltarVfxController>(true);
                if (vfx == null)
                {
                    Debug.LogError($"[봉헌 빛줄기] {PrefabPath} 에 AltarVfxController 가 없습니다.");
                    return;
                }

                // 있으면 떼고 새로 붙인다. 프리팹에는 처음 붙일 때의 값이 저장되므로, 코드의 기본값(색 · 굵기 ·
                // 시간)을 바꿔도 그대로 붙어 있으면 옛 값이 남는다. 다시 누르면 코드 기본값으로 맞춰진다.
                AltarOfferBeam old = vfx.GetComponent<AltarOfferBeam>();
                if (old != null)
                {
                    Object.DestroyImmediate(old, true);
                }
                AltarOfferBeam beam = vfx.gameObject.AddComponent<AltarOfferBeam>();

                var monolith = new SerializedObject(vfx).FindProperty("monolithRenderer").objectReferenceValue;

                var so = new SerializedObject(beam);
                so.FindProperty("material").objectReferenceValue = material;
                so.FindProperty("origin").objectReferenceValue = monolith;
                so.ApplyModifiedPropertiesWithoutUndo();

                EnsureCameraPoint(root, vfx.transform, beam);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                Debug.Log($"[봉헌 빛줄기] 설치했습니다 — {PrefabPath} (시작점: {(monolith != null ? monolith.name : "제단 위치")})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        public static void InstallFromCommandLine() => Install();

        private const string CameraPointName = "OfferCameraPoint";

        /// <summary>
        /// 연출 카메라 자리(<see cref="AltarOfferCameraPoint"/>)가 없으면 만든다. <b>있으면 건드리지 않는다</b> —
        /// 사람이 씬 뷰에서 골라 저장한 각도를 설치 메뉴가 덮어쓰면 안 된다.
        ///
        /// 처음 자리는 예전 계산과 같다. 제단에서 바치는 자리(<see cref="AltarInteraction"/>) 쪽으로 22m, 13m 위에서
        /// 제단 꼭대기 4m 위를 겨눈다. 바치는 자리를 못 찾으면 제단 앞(-Z) 쪽.
        /// </summary>
        private static void EnsureCameraPoint(GameObject root, Transform parent, AltarOfferBeam beam)
        {
            if (root.GetComponentInChildren<AltarOfferCameraPoint>(true) != null)
            {
                return;
            }

            var go = new GameObject(CameraPointName);
            go.transform.SetParent(parent, worldPositionStays: false);
            go.AddComponent<AltarOfferCameraPoint>();

            Vector3 top = beam.BasePosition;
            AltarInteraction stand = root.GetComponentInChildren<AltarInteraction>(true);
            Vector3 side = stand != null ? stand.transform.position - top : Vector3.back;
            side.y = 0f;
            side = side.sqrMagnitude > 0.01f ? side.normalized : Vector3.back;

            Vector3 pos = top + side * CamDistance + Vector3.up * CamHeight;
            Vector3 aim = top + Vector3.up * CamAimAbove;
            go.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(aim - pos, Vector3.up));
            Debug.Log($"[봉헌 빛줄기] 카메라 자리를 만들었습니다 — {CameraPointName} (씬 뷰에서 Tools/아라아띠/제단 봉헌 카메라 고르기 로 바꿉니다)");
        }

        /// <summary>
        /// 제단 프리팹에 다 뻗은 빛줄기를 띄우고 <b>봉헌 연출과 같은 카메라 각도</b>로 찍는다(<c>%TEMP%/AltarBeamPreview</c>).
        /// 굵기 · 색 · 카메라 거리를 바꿀 때마다 눌러 본다. 열린 씬은 건드리지 않는다(미리보기 씬).
        ///
        /// ⚠ 카메라 숫자는 <see cref="AltarOfferCinematic"/> 의 Distance · Height · AimAbove 와 같게 둔다.
        /// </summary>
        [MenuItem("Tools/아라아띠/제단 봉헌 빛줄기 미리보기")]
        public static void Preview()
        {
            PreviewTo(Path.Combine(Path.GetTempPath(), "AltarBeamPreview"));
        }

        public static void PreviewFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-previewOut");
            PreviewTo(at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetTempPath(), "AltarBeamPreview"));
        }

        /// <summary>배치 모드용: 설치하고 바로 찍는다.</summary>
        public static void InstallAndPreviewFromCommandLine()
        {
            Install();
            PreviewFromCommandLine();
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name) return t;
            }
            return null;
        }

        private const float CamDistance = 22f;
        private const float CamHeight = 13f;
        private const float CamAimAbove = 4f;

        private static void PreviewTo(string outDir)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null) return;

            Directory.CreateDirectory(outDir);
            UnityEngine.SceneManagement.Scene scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.1f;
                light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(light.gameObject, scene);

                var altar = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                altar.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                AltarOfferBeam beam = altar.GetComponentInChildren<AltarOfferBeam>(true);
                if (beam == null)
                {
                    Debug.LogError("[봉헌 빛줄기 미리보기] 프리팹에 AltarOfferBeam 이 없습니다. 먼저 설치하세요.");
                    return;
                }
                GameObject still = beam.BuildStill();

                const int W = 1920, H = 1080;
                var rt = new RenderTexture(W, H, 24);
                var camera = new GameObject("Camera").AddComponent<Camera>();
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.45f, 0.66f, 0.82f); // 로비 하늘 쪽 색
                camera.fieldOfView = 60f;
                camera.farClipPlane = 500f;
                camera.targetTexture = rt;

                Vector3 basePos = beam.BasePosition;
                AltarOfferCameraPoint point = altar.GetComponentInChildren<AltarOfferCameraPoint>(true);
                if (point != null)
                {
                    // 연출과 같은 자리 — 사람이 고른 카메라 자리.
                    camera.transform.SetPositionAndRotation(point.transform.position, point.transform.rotation);
                    camera.fieldOfView = point.FieldOfView;
                }
                else
                {
                    Vector3 aim = basePos + Vector3.up * CamAimAbove;
                    camera.transform.position = basePos + Vector3.back * CamDistance + Vector3.up * CamHeight;
                    camera.transform.rotation = Quaternion.LookRotation(aim - camera.transform.position, Vector3.up);
                }

                var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
                void Shot(string file)
                {
                    camera.Render();
                    RenderTexture previous = RenderTexture.active;
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                    tex.Apply();
                    RenderTexture.active = previous;
                    File.WriteAllBytes(Path.Combine(outDir, file), tex.EncodeToPNG());
                }

                Shot("altar_beam_aerial.png");

                // 평소 하늘색 빛기둥(AltarBeam)을 뺀 모습도 같이 — 노란 빛줄기가 그 안에 묻히는지 비교한다.
                Transform baseBeam = FindDeep(altar.transform, "AltarBeam");
                if (baseBeam != null)
                {
                    baseBeam.gameObject.SetActive(false);
                    Shot("altar_beam_aerial_no_base.png");
                }

                Object.DestroyImmediate(tex);
                Object.DestroyImmediate(still);
                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Debug.Log($"[봉헌 빛줄기 미리보기] 제단 꼭대기 {basePos.ToString("F2")} → {outDir}");
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        /// <summary>
        /// 선의 폭 방향(v)으로 가운데가 밝고 가장자리가 0 인 띠. LineRenderer 는 v 를 선의 폭에 쓴다.
        /// 길이 방향(u)은 늘 같아서 몇 픽셀이면 된다.
        /// </summary>
        private static Texture2D MakeSoftTexture()
        {
            const int W = 4, H = 64;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            for (int y = 0; y < H; y++)
            {
                // -1 ~ 1 에서 가우스 비슷하게. 가장자리에서 정확히 0 이 되게 끝을 눌러 준다.
                float v = (y + 0.5f) / H * 2f - 1f;
                float a = Mathf.Exp(-v * v * 5f) * (1f - v * v);
                for (int x = 0; x < W; x++)
                {
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }
            tex.Apply();

            Directory.CreateDirectory(Folder);
            File.WriteAllBytes(TexturePath, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(TexturePath);

            var importer = (TextureImporter)AssetImporter.GetAtPath(TexturePath);
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.sRGBTexture = true;
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material MakeMaterial(Texture2D soft)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null)
            {
                Debug.LogError($"[봉헌 빛줄기] 셰이더를 찾지 못했습니다: {ShaderName}");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            material.shader = shader;

            // 투명 + 일반 섞기(alpha). 처음엔 더하기 섞기였는데, 로비의 밝은 하늘 위에서 노란색이 흰색으로
            // 바래 버렸다(미리보기에서 확인). 일반 섞기는 배경이 밝아도 노란색이 남는다.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;

            // 색은 LineRenderer 의 꼭짓점 색으로 준다. 재질은 흰색 + 부드러운 띠만.
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", soft);

            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
