#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnderTheSea.Lobby;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderTheSea.Lobby.Editor
{
    /// <summary>
    /// **이정표 위에 뜨는 화살표를 만들어 붙인다.** 한 번 눌러 쓰는 도구다.
    ///
    /// 직접 하려면 원뿔 메시를 만들고, 머티리얼을 만들고, 발광을 켜고, 프리팹을 열어
    /// 자식으로 넣고, 컴포넌트 두 개를 붙이고, 칸을 연결해야 한다. 그걸 대신한다.
    ///
    /// <code>
    ///   1. 원뿔 메시 생성        Unity 기본 도형에 원뿔이 없어서 직접 만든다
    ///   2. 주황 발광 머티리얼     Bloom 이 이미 켜져 있어 밝기만 올리면 번진다
    ///   3. 프리팹에 자식으로 넣기  아래를 가리키게 돌려서
    ///   4. 컴포넌트 붙이고 연결    SignpostBeacon · SignpostTeleport
    /// </code>
    ///
    /// <b>모양은 로우폴리로 맞춘다.</b> 옆면이 <see cref="Sides"/> 개뿐이고 면마다 법선을
    /// 따로 줘서 각져 보인다. 섬의 Synty 에셋들과 같은 결이다.
    ///
    /// ⚠ <b>그림자를 끈다.</b> 공중에 뜬 작은 표식이라 그림자가 있으면 모래에 점이 생겨
    ///    지저분하다. 캐스케이드 4단이라 그리는 값도 네 번 든다.
    ///
    /// 메뉴: Tools > 아라아띠 > 이정표 화살표 붙이기
    /// </summary>
    public static class SignpostBeaconSetup
    {
        private const string PrefabPath = "Assets/Game/Prefabs/Environment/Signpost.prefab";
        private const string MeshPath = "Assets/Game/Art/Models/Lobby/SignpostArrow.asset";
        private const string MaterialPath = "Assets/Game/Art/Materials/Lobby/SignpostArrow.mat";

        private const string MarkerName = "Beacon";

        /// <summary>옆면 개수. 적을수록 각진다. 8이면 로우폴리 결에 맞는다.</summary>
        private const int Sides = 8;

        /// <summary>원뿔 높이(m).</summary>
        private const float Height = 0.22f;

        /// <summary>밑면 반지름(m).</summary>
        private const float Radius = 0.11f;

        /// <summary>
        /// 이정표 원점에서 얼마나 위에 띄우는가(m).
        ///
        /// <see cref="SignpostBeacon"/> 의 기본값과 같게 맞춰 둔다. 에디터에서 보이는 자리와
        /// 실행 중 자리가 다르면 위치를 잡을 때 헷갈린다.
        /// </summary>
        private const float BeaconHeight = 0.8f;

        /// <summary>
        /// 주황. <b>1을 넘는 값</b>이라 Bloom 임계값(0.9)을 넘어 번진다.
        ///
        /// ⚠ <b>밝게 할수록 노랗게 보인다.</b> 값이 크면 톤매핑이 흰색 쪽으로 눌러서
        ///    주황이 노랑이 된다. 처음에 (3.2, 1.1, 0.15) 로 줬더니 노란 원뿔이 나왔다.
        ///    주황으로 보이게 하려면 <b>초록을 빨강 대비 낮추고</b>, 전체 밝기도 조금 내린다.
        /// <code>
        ///   (3.2, 1.10, 0.15)   G/R 0.34   노랑에 가깝다
        ///   (2.4, 0.62, 0.05)   G/R 0.26   주황
        /// </code>
        /// </summary>
        private static readonly Color Emission = new Color(2.4f, 0.62f, 0.05f, 1f);

        /// <summary>발광이 아닌 본래 색. 어두운 곳에서 실루엣이 보이게 한다.</summary>
        private static readonly Color BaseColor = new Color(1f, 0.38f, 0.05f, 1f);

        [MenuItem("Tools/아라아띠/이정표 화살표 붙이기")]
        public static void Run()
        {
            Mesh mesh = MakeCone();
            Material material = MakeMaterial();
            if (material == null) return;

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            if (root == null)
            {
                Debug.LogError($"[이정표] 프리팹을 못 열었습니다: {PrefabPath}");
                return;
            }

            try
            {
                Transform marker = Attach(root, mesh, material);
                Wire(root, marker);

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();

            Debug.Log(
                "[이정표] 화살표를 붙였습니다.\n" +
                $"  메시      {MeshPath}  (삼각형 {Sides * 2})\n" +
                $"  머티리얼   {MaterialPath}\n" +
                "  프리팹에 SignpostBeacon · SignpostTeleport 가 붙었습니다.\n" +
                "  밝기나 흔들림은 인스펙터에서 바꿀 수 있습니다.");
        }

        /// <summary>
        /// 아래를 가리키는 원뿔을 만든다.
        ///
        /// 꼭짓점이 아래(-Y), 밑면이 위에 있다. 이정표를 콕 집어 가리키는 모양이다.
        /// 면마다 정점을 따로 두어 각지게 만든다 — 공유하면 둥글게 뭉개진다.
        /// </summary>
        private static Mesh MakeCone()
        {
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = "SignpostArrow" };
                Directory.CreateDirectory(Path.GetDirectoryName(MeshPath) ?? string.Empty);
                AssetDatabase.CreateAsset(mesh, MeshPath);
            }

            mesh.Clear();

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            Vector3 apex = Vector3.zero;
            Vector3 center = new Vector3(0f, Height, 0f);

            for (int i = 0; i < Sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / Sides;
                float a1 = 2f * Mathf.PI * (i + 1) / Sides;

                Vector3 p0 = new Vector3(Mathf.Cos(a0) * Radius, Height, Mathf.Sin(a0) * Radius);
                Vector3 p1 = new Vector3(Mathf.Cos(a1) * Radius, Height, Mathf.Sin(a1) * Radius);

                // 옆면
                int v = vertices.Count;
                vertices.Add(apex);
                vertices.Add(p1);
                vertices.Add(p0);
                triangles.Add(v);
                triangles.Add(v + 1);
                triangles.Add(v + 2);

                // 윗면(뚜껑)
                v = vertices.Count;
                vertices.Add(center);
                vertices.Add(p0);
                vertices.Add(p1);
                triangles.Add(v);
                triangles.Add(v + 1);
                triangles.Add(v + 2);
            }

            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Material MakeMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("[이정표] URP Lit 셰이더를 못 찾았습니다.");
                return null;
            }

            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            // ⚠ **이미 있으면 색을 덮어쓰지 않는다.** 색은 눈으로 맞추는 값이라 인스펙터에서
            //    고치게 된다. 메뉴를 다시 누를 때마다 그게 지워지면 맞출 수가 없다.
            //    기본값을 바꿔 다시 받고 싶으면 이 .mat 파일을 지우고 누르면 된다.
            bool isNew = material == null;

            if (isNew)
            {
                material = new Material(shader);
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialPath) ?? string.Empty);
                AssetDatabase.CreateAsset(material, MaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            if (isNew)
            {
                material.SetColor("_BaseColor", BaseColor);
                material.SetColor("_EmissionColor", Emission);
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.4f);
            }

            // 이 키워드를 안 켜면 _EmissionColor 를 넣어도 빛나지 않는다. 이건 늘 맞춰 둔다.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Transform Attach(GameObject root, Mesh mesh, Material material)
        {
            Transform existing = root.transform.Find(MarkerName);
            GameObject marker = existing != null
                ? existing.gameObject
                : new GameObject(MarkerName);

            marker.transform.SetParent(root.transform, false);

            // 프리팹 루트가 깨끗해서 평범한 지역 좌표로 놓으면 된다.
            // SignpostBeacon 이 ExecuteAlways 라 에디터에서도 같은 자리로 다시 잡는다.
            marker.transform.localPosition = Vector3.up * BeaconHeight;
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one;

            // ⚠ **여기서 `??` 를 쓰면 안 된다.** Unity 는 파괴된 오브젝트를 "가짜 null" 로
            //    다루려고 `==` 를 따로 정의해 놨는데, `??` 는 그 정의를 무시하고 진짜 참조만
            //    본다. 그래서 `GetComponent<T>() ?? AddComponent<T>()` 가 붙어 있지도 않은
            //    컴포넌트를 돌려주고, 거기에 값을 넣는 순간 MissingComponentException 이 난다.
            //    실제로 그렇게 짰다가 겪었다. `== null` 로 써야 Unity 의 정의를 탄다.
            MeshFilter filter = marker.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = marker.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = mesh;

            MeshRenderer renderer = marker.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = marker.AddComponent<MeshRenderer>();
            }

            renderer.sharedMaterial = material;

            // 공중에 뜬 작은 표식이다. 그림자가 지면 모래에 점이 생겨 지저분하다.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return marker.transform;
        }

        private static void Wire(GameObject root, Transform marker)
        {
            // 위와 같은 이유로 `??` 를 쓰지 않는다.
            SignpostBeacon beacon = root.GetComponent<SignpostBeacon>();
            if (beacon == null)
            {
                beacon = root.AddComponent<SignpostBeacon>();
            }

            // 인스펙터에서 손으로 바꿔도 덮어쓰지 않도록, 비어 있을 때만 채운다.
            SerializedObject so = new SerializedObject(beacon);
            SerializedProperty markerProp = so.FindProperty("marker");

            if (markerProp != null && markerProp.objectReferenceValue == null)
            {
                markerProp.objectReferenceValue = marker;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            if (root.GetComponent<SignpostTeleport>() == null)
            {
                root.AddComponent<SignpostTeleport>();
            }
        }
    }
}
#endif
