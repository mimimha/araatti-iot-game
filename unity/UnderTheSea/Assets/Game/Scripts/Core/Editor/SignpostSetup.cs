#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Core.Editor
{
    /// <summary>
    /// **이정표(Signpost) 에셋을 쓸 수 있는 상태로 만든다.** 한 번 돌리면 끝나는 도구다.
    ///
    /// Tripo 로 만든 FBX 와 텍스처를 프로젝트 규칙에 맞춰 넣은 뒤, 사람이 인스펙터에서
    /// 열 번쯤 눌러야 하는 일을 대신한다.
    ///
    /// <code>
    ///   1. 텍스처 임포트 설정          노멀맵은 Normal Map 으로, 나머지는 기본
    ///   2. URP Lit 머티리얼 생성       BaseColor · Normal 연결
    ///   3. FBX 에 머티리얼 물리기       임포트된 모델의 머티리얼을 우리 것으로 바꾼다
    ///   4. 프리팹 저장                 씬에 끌어다 놓을 수 있게
    /// </code>
    ///
    /// ⚠ <b>Metallic/Roughness 는 연결하지 않는다.</b> URP Lit 은 거칠기(roughness)를 받지
    ///    않고 <b>매끄러움(smoothness)</b> 을 쓴다. 둘은 서로 반대라 그대로 꽂으면 거꾸로
    ///    보인다. 제대로 하려면 금속도를 RGB 에, (1 - 거칠기) 를 A 에 넣은 텍스처를 따로
    ///    만들어야 한다. 나무 이정표라 금속도가 0 이고, 슬라이더 두 개로 충분해서 그렇게 했다.
    ///    필요해지면 그때 합성 텍스처를 만들어 <c>_MetallicGlossMap</c> 에 넣는다.
    ///
    /// 메뉴: Tools > 아라아띠 > 이정표 에셋 만들기
    /// </summary>
    public static class SignpostSetup
    {
        private const string Model = "Assets/Game/Art/Models/Lobby/Signpost.fbx";
        private const string TextureFolder = "Assets/Game/Art/Textures/Lobby";
        private const string MaterialPath = "Assets/Game/Art/Materials/Lobby/Signpost.mat";
        private const string PrefabPath = "Assets/Game/Prefabs/Environment/Signpost.prefab";

        private const string BaseColor = TextureFolder + "/Signpost_BaseColor.jpg";
        private const string Normal = TextureFolder + "/Signpost_Normal.jpg";

        /// <summary>나무라서 금속이 아니다.</summary>
        private const float Metallic = 0f;

        /// <summary>낡은 나무. 번들거리면 이상하다.</summary>
        private const float Smoothness = 0.15f;

        [MenuItem("Tools/아라아띠/이정표 에셋 만들기")]
        public static void Build()
        {
            if (!File.Exists(Model))
            {
                Debug.LogError($"[이정표] 모델이 없습니다: {Model}");
                return;
            }

            FixTextureImport(Normal, isNormalMap: true);
            FixTextureImport(BaseColor, isNormalMap: false);

            Material material = MakeMaterial();
            if (material == null) return;

            AttachToModel(material);

            GameObject prefab = MakePrefab();
            if (prefab == null) return;

            AssetDatabase.SaveAssets();
            Report(prefab);
        }

        /// <summary>
        /// 노멀맵은 <b>반드시 Normal Map 으로 표시</b>해야 한다. 안 하면 Unity 가 일반
        /// 색 텍스처로 읽어 울퉁불퉁함이 엉뚱하게 나오고, 인스펙터에 경고가 뜬다.
        /// </summary>
        private static void FixTextureImport(string path, bool isNormalMap)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                Debug.LogWarning($"[이정표] 텍스처를 못 찾았습니다: {path}");
                return;
            }

            TextureImporterType wanted = isNormalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

            if (importer.textureType == wanted)
            {
                return;
            }

            importer.textureType = wanted;
            importer.SaveAndReimport();
            Debug.Log($"[이정표] {Path.GetFileName(path)} → {wanted}");
        }

        private static Material MakeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[이정표] URP Lit 셰이더를 못 찾았습니다.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(shader);
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath) ?? string.Empty);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColor));

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(Normal);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }

            material.SetFloat("_Metallic", Metallic);
            material.SetFloat("_Smoothness", Smoothness);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// FBX 가 들고 온 머티리얼 대신 우리 것을 쓰게 한다.
        ///
        /// Tripo 가 붙여 온 머티리얼은 Built-in 셰이더라 URP 에서 분홍색으로 보인다.
        /// </summary>
        private static void AttachToModel(Material material)
        {
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (importer == null)
            {
                Debug.LogWarning("[이정표] 모델 임포터를 못 찾았습니다.");
                return;
            }

            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.SaveAndReimport();

            // 모델 안의 모든 렌더러가 이 머티리얼을 쓰도록 리맵을 건다.
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null) return;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                foreach (Material slot in renderer.sharedMaterials)
                {
                    if (slot == null) continue;

                    importer.AddRemap(
                        new AssetImporter.SourceAssetIdentifier(typeof(Material), slot.name),
                        material);
                }
            }

            importer.SaveAndReimport();
        }

        private static GameObject MakePrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (model == null)
            {
                Debug.LogError("[이정표] 임포트된 모델을 못 읽었습니다.");
                return null;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath) ?? string.Empty);

            // ⚠ **모델을 그대로 프리팹으로 만들면 안 된다.**
            //
            //    임포트된 FBX 의 루트에는 <b>X축 -90도와 100배</b>가 붙어 있다. Blender 는
            //    Z축이 위, Unity 는 Y축이 위여서 임포터가 변환을 루트 트랜스폼에 얹기
            //    때문이다. 그 루트를 프리팹 루트로 쓰면 <b>여기에 붙이는 모든 것</b>이
            //    기울고 100배가 된 좌표계에서 계산해야 한다. 실제로 겪은 것들:
            //
            //      화살표를 localPosition 으로 올렸더니 위가 아니라 옆으로 갔다
            //      0.34m 원뿔이 50m 로 나왔다
            //      LookRotation 으로 방향을 돌렸더니 이정표가 바닥에 누웠다
            //
            //    매번 상쇄 코드를 넣는 것은 증상만 가리는 짓이다. 그래서 <b>회전도 크기도
            //    없는 빈 루트</b>를 하나 씌우고 모델을 그 자식으로 넣는다. 변환은 자식이
            //    그대로 들고 있고, 프리팹 루트는 깨끗하다. 여기에 무엇을 붙이든 평범한
            //    지역 좌표로 계산하면 된다.
            //
            //    임포터 설정(bakeAxisConversion · useFileScale)으로도 고칠 수 있지만,
            //    끄고 켜는 조합에 따라 메시 크기가 100배로 튀는 것을 확인했다. 래퍼 루트는
            //    임포터가 무엇을 하든 결과가 같다.
            var root = new GameObject("Signpost");

            GameObject body = Object.Instantiate(model);
            body.name = "Model";
            body.transform.SetParent(root.transform, worldPositionStays: false);

            // 씬에 놓으면 부딪혀야 하는 물건이다. 콜라이더가 없으면 그냥 통과한다.
            if (root.GetComponentInChildren<Collider>(true) == null)
            {
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null) continue;

                    var box = filter.gameObject.AddComponent<BoxCollider>();
                    box.center = filter.sharedMesh.bounds.center;
                    box.size = filter.sharedMesh.bounds.size;
                }
            }

            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);

            return saved;
        }

        /// <summary>
        /// 만든 결과를 숫자로 알린다.
        ///
        /// <b>삼각형 수를 꼭 본다.</b> Tripo 같은 도구는 사진에서 만들어 내느라 면이 아주
        /// 많은 메시를 뱉는다. 로우폴리 게임에 그대로 넣으면 이 물건 하나가 섬 전체보다
        /// 무거울 수 있다. 우리 로비는 렌더러가 이미 9천 개가 넘는다.
        /// </summary>
        private static void Report(GameObject prefab)
        {
            int triangles = 0;
            int vertices = 0;
            int renderers = 0;

            foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null) continue;

                triangles += filter.sharedMesh.triangles.Length / 3;
                vertices += filter.sharedMesh.vertexCount;
                renderers++;
            }

            Debug.Log(
                $"[이정표] 다 만들었습니다.\n" +
                $"  프리팹    {PrefabPath}\n" +
                $"  머티리얼  {MaterialPath}\n" +
                $"  삼각형    {triangles:N0}\n" +
                $"  정점      {vertices:N0}\n" +
                $"  렌더러    {renderers}");
        }
    }
}
#endif
