using System.IO;
using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🖼 <b>모델을 찍어 HUD 아이콘 PNG 를 만든다.</b>
    ///
    /// 하단 카드의 물고기 · 게 · 해파리 아이콘이 그렇게 만들어졌습니다(WARRIORS.md 7장).
    /// 같은 방식으로 <b>크라켄</b> 아이콘을 만듭니다 — 상단 진행 바에 배 게임의 돛단배가
    /// 그려져 있는데, 무쌍에서 그 자리의 주인공은 크라켄이기 때문입니다.
    ///
    /// ⚠ 배경은 <b>투명</b>입니다. 판 위에 얹을 것이라 배경이 있으면 판을 가립니다.
    ///
    /// ⚠ 찍고 나서 <b>임포터를 Sprite 로 바꿔 줍니다.</b> 안 하면 Texture 로 들어와
    ///    <c>Image.sprite</c> 에 넣을 수 없습니다. 실제로 그렇게 한 번 막혔습니다.
    /// </summary>
    public static class WarriorsIconRender
    {
        private const string KrakenModelPath =
            "Assets/Game/Art/MiniGames/Warriors/Models/KrakenPhase2/" +
            "Meshy_AI_Grumpy_Grape_Octopus_0911005752_texture.fbx";

        private const string OutputPath =
            "Assets/Game/Art/MiniGames/Warriors/UI/Icons/Icon_Kraken.png";

        private const int Size = 256;

        [MenuItem("Tools/아라아띠/Warriors 크라켄 아이콘 굽기")]
        public static void Render()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(KrakenModelPath);

            if (model == null)
            {
                Debug.LogError($"[아이콘] 크라켄 모델을 찾지 못했습니다 — {KrakenModelPath}");
                return;
            }

            // 화면 밖에 세운다. 다른 카메라에 잡히면 안 된다.
            GameObject stand = Object.Instantiate(model);
            stand.transform.position = new Vector3(0f, -9000f, 0f);
            stand.transform.rotation = Quaternion.Euler(0f, 160f, 0f);

            RenderTexture shot = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32)
            {
                antiAliasing = 8,
            };

            Camera cam = new GameObject("아이콘 사진기").AddComponent<Camera>();

            try
            {
                Bounds box = Measure(stand);

                if (box.size.magnitude <= 0.001f)
                {
                    Debug.LogError("[아이콘] 모델에서 크기를 재지 못했습니다.");
                    return;
                }

                float reach = box.size.magnitude;

                cam.transform.position = box.center + new Vector3(0f, reach * 0.12f, -reach);
                cam.transform.LookAt(box.center);
                cam.orthographic = true;

                // 살짝 여유를 둬서 촉수 끝이 잘리지 않게 한다.
                cam.orthographicSize = Mathf.Max(box.extents.x, box.extents.y) * 1.15f;

                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
                cam.nearClipPlane = 0.01f;
                cam.farClipPlane = reach * 4f;
                cam.targetTexture = shot;

                cam.Render();

                RenderTexture was = RenderTexture.active;
                RenderTexture.active = shot;

                Texture2D flat = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
                flat.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
                flat.Apply();

                RenderTexture.active = was;

                Directory.CreateDirectory(Path.GetDirectoryName(OutputPath)!);
                File.WriteAllBytes(OutputPath, flat.EncodeToPNG());

                Object.DestroyImmediate(flat);
            }
            finally
            {
                Object.DestroyImmediate(cam.gameObject);
                Object.DestroyImmediate(stand);
                shot.Release();
                Object.DestroyImmediate(shot);
            }

            AssetDatabase.ImportAsset(OutputPath, ImportAssetOptions.ForceUpdate);

            // ⚠ Sprite 로 들여와야 Image 에 넣을 수 있다.
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(OutputPath);

            if (importer != null)
            {
                importer.textureType = TextureImporterType.Sprite;

                // ⚠ **Single 이어야 한다.** 기본값이 Multiple 이면 스프라이트가 하위 에셋으로
                //    들어가, 파일 경로로 <c>LoadAssetAtPath&lt;Sprite&gt;</c> 하면 null 이 돌아온다.
                //    실제로 그것 때문에 HUD 가 크라켄을 못 찾아 돛단배가 그대로 남았다.
                importer.spriteImportMode = SpriteImportMode.Single;

                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.SaveAndReimport();
            }

            Sprite made = AssetDatabase.LoadAssetAtPath<Sprite>(OutputPath);

            if (made == null)
            {
                Debug.LogError($"[아이콘] 저장은 됐는데 Sprite 로 읽히지 않습니다 — {OutputPath}");
                return;
            }

            Debug.Log($"[아이콘] ✅ 크라켄 아이콘을 구웠습니다 — {OutputPath} ({made.rect.width:F0}×{made.rect.height:F0})");
        }

        public static void RenderFromCommandLine()
        {
            Render();
            EditorApplication.Exit(0);
        }

        private static Bounds Measure(GameObject go)
        {
            Renderer[] draws = go.GetComponentsInChildren<Renderer>(true);

            if (draws.Length == 0) return new Bounds(go.transform.position, Vector3.zero);

            Bounds box = draws[0].bounds;

            for (int i = 1; i < draws.Length; i++) box.Encapsulate(draws[i].bounds);

            return box;
        }
    }
}
