using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🔊 무쌍 네트워크 씬에 <see cref="WarriorsAudio"/> 를 놓고,
    /// <c>Assets/Game/Audio/Warriors/</c> 의 클립을 <b>이름대로</b> 채운다. (AUDIO.md 5장)
    ///
    /// 클립 파일 이름이 곧 연출가의 필드 이름이다 (확장자는 .ogg · .wav · .mp3 중 무엇이든).
    ///
    /// <code>
    ///   bgmWaiting  bgmRound1  bgmRound2  bgmRound3  stingerClear  stingerFail
    ///   waveLoop  krakenLoop
    ///   swingHorizontal  swingVertical  swingThrust  monsterHit  comboUp  monsterKill
    ///   tentacleHit  tentacleCut  finishWindow  comboSet
    ///   noteHit  noteMiss  krakenHurt  krakenRoar
    ///   playerHurt  playerDown  roundChange  countdownTick  countdownGo
    /// </code>
    ///
    /// 없는 파일은 <b>칸을 비운다</b> — 그 소리만 안 난다. 파일을 넣고 다시 돌리면 채워지고,
    /// 파일을 지우고 다시 돌리면 비워진다. 여러 번 돌려도 결과는 같다.
    ///
    /// ⚠ 1인 검증 씬(<c>WarriorsTest.unity</c>)에는 놓지 않는다. 그 씬에는
    ///   <c>WarriorsMatchState</c> 가 없어 연출가가 읽을 값이 없다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod Warriors.Net.Editor.WarriorsAudioInstaller.Install
    /// </summary>
    public static class WarriorsAudioInstaller
    {
        private const string AudioFolder = "Assets/Game/Audio/Warriors";
        private const string ObjectName = "Audio";

        private static readonly string[] Scenes =
        {
            "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity",
        };

        /// <summary>연출가의 <c>AudioClip</c> 칸 이름. 파일 이름이 이것과 같아야 연결된다.</summary>
        private static readonly string[] ClipFields =
        {
            "bgmWaiting", "bgmRound1", "bgmRound2", "bgmRound3", "stingerClear", "stingerFail",
            "waveLoop", "krakenLoop",
            "swingHorizontal", "swingVertical", "swingThrust", "monsterHit", "comboUp", "monsterKill",
            "tentacleHit", "tentacleCut", "finishWindow", "comboSet",
            "noteHit", "noteMiss", "krakenHurt", "krakenRoar",
            "playerHurt", "playerDown", "roundChange", "countdownTick", "countdownGo",
        };

        private static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };

        [MenuItem("Tools/아라아띠/Warriors 소리 놓고 클립 채우기")]
        public static void Install()
        {
            if (!AssetDatabase.IsValidFolder(AudioFolder))
            {
                Debug.LogWarning($"[WarriorsAudio] {AudioFolder} 가 없습니다. 오브젝트만 놓고 클립은 비워 둡니다. " +
                                 "(CONVENTION.md 6장 — 오디오는 Assets/Game/Audio/)");
            }

            foreach (string path in Scenes)
            {
                Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

                WarriorsAudio audio = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    audio = root.GetComponentInChildren<WarriorsAudio>(true);
                    if (audio != null) break;
                }

                if (audio == null)
                {
                    var go = new GameObject(ObjectName);
                    audio = go.AddComponent<WarriorsAudio>();
                    Debug.Log($"[WarriorsAudio] {scene.name} 에 {ObjectName} 를 놓았습니다.");
                }

                int filled = 0, missing = 0;
                var so = new SerializedObject(audio);

                foreach (string field in ClipFields)
                {
                    SerializedProperty p = so.FindProperty(field);

                    if (p == null)
                    {
                        Debug.LogError($"[WarriorsAudio] WarriorsAudio 에 {field} 칸이 없습니다. 이름이 바뀌었나요?");
                        continue;
                    }

                    // 파일이 없으면 칸을 **비운다.** 파일을 지워 소리를 빼는 것도 이 도구 한 번으로 끝나게.
                    AudioClip clip = FindClip(field);
                    p.objectReferenceValue = clip;

                    if (clip != null) filled++;
                    else missing++;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);

                Debug.Log($"[WarriorsAudio] {scene.name} — 클립 {filled}개 채움, {missing}개 비어 있음.");
            }

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
