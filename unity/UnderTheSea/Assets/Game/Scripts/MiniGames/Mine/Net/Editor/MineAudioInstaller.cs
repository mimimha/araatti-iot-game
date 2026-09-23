using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mine.Net.Editor
{
    /// <summary>
    /// 🔊 광산 네트워크 씬에 <see cref="MineAudio"/> 를 놓고, <c>Assets/Game/Audio/Mine/</c> 의 클립을 이름대로 채운다.
    /// (본보기는 <c>ShipCoopAudioInstaller</c>)
    ///
    /// 클립 파일 이름이 곧 필드 이름이다 (확장자는 .ogg · .wav · .mp3 중 무엇이든).
    ///
    /// <code>
    ///   bgmWaiting  bgmPlaying  stingerClear  stingerFail
    ///   caveLoop
    ///   countTick  countGo  turnStart  timeWarn
    ///   stoneCrack  stoneBreak  restorePlace  hintOpen  swingMiss
    /// </code>
    ///
    /// 없는 파일은 비워 둔다 — 그 소리만 안 난다. 파일을 넣고 다시 돌리면 채워진다. 여러 번 돌려도 같다.
    ///
    /// ⚠ <c>광산 네트워크 씬 만들기</c>(<see cref="MineSceneSetup"/>)로 씬을 다시 만들면 Audio 오브젝트도
    ///   사라진다. 그 뒤에 이 메뉴를 한 번 더 돌린다.
    /// </summary>
    public static class MineAudioInstaller
    {
        private const string AudioFolder = "Assets/Game/Audio/Mine";
        private const string ObjectName = "Audio";

        private static readonly string[] ClipFields =
        {
            "bgmWaiting", "bgmPlaying", "stingerClear", "stingerFail",
            "caveLoop",
            "countTick", "countGo", "turnStart", "timeWarn",
            "stoneCrack", "stoneBreak", "restorePlace", "hintOpen", "swingMiss",
        };

        private static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };

        [MenuItem("아라아띠/광산/소리 놓고 클립 채우기")]
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                Debug.LogWarning($"[Audio] {AudioFolder} 가 없습니다. 오브젝트만 놓고 클립은 비워 둡니다. (CONVENTION.md 6장 — 오디오는 Assets/Game/Audio/)");
            }

            Scene scene = EditorSceneManager.OpenScene(MineNet.ScenePath, OpenSceneMode.Single);

            MineAudio audio = null;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                audio = root.GetComponentInChildren<MineAudio>(true);
                if (audio != null) break;
            }

            if (audio == null)
            {
                var go = new GameObject(ObjectName);
                audio = go.AddComponent<MineAudio>();
                Debug.Log($"[Audio] {scene.name} 에 {ObjectName} 를 놓았습니다.");
            }

            int filled = 0, missing = 0;
            var so = new SerializedObject(audio);

            foreach (string field in ClipFields)
            {
                SerializedProperty p = so.FindProperty(field);
                if (p == null)
                {
                    Debug.LogError($"[Audio] MineAudio 에 {field} 칸이 없습니다. 이름이 바뀌었나요?");
                    continue;
                }

                // 파일이 없으면 칸을 **비운다.** 파일을 지워서 소리를 빼는 것도 이 도구 한 번으로 끝나게.
                AudioClip clip = FindClip(field);
                p.objectReferenceValue = clip;

                if (clip != null) filled++;
                else missing++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[Audio] {scene.name} — 클립 {filled}개 채움, {missing}개 비어 있음.");

            AssetDatabase.SaveAssets();
        }

        private static AudioClip FindClip(string field)
        {
            foreach (string ext in Extensions)
            {
                string path = $"{AudioFolder}/{field}{ext}";

                if (File.Exists(path))
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
                    if (clip != null) return clip;
                }
            }

            return null;
        }
    }
}
