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
    ///   noteHitHorizontal  noteHitVertical  noteHitThrust  noteMiss  krakenHurt  krakenRoar
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
            "noteHitHorizontal", "noteHitVertical", "noteHitThrust", "noteMiss", "krakenHurt", "krakenRoar",
            "playerHurt", "playerDown", "roundChange", "countdownTick", "countdownGo",
        };

        private static readonly string[] Extensions = { ".ogg", ".wav", ".mp3" };

        /// <summary>
        /// 제 파일이 없으면 앞 칸의 클립을 이어 쓰는 칸. 지금은 <c>bgmRound1</c> · <c>bgmRound2</c> ← <c>bgmWaiting</c>
        /// (대기부터 2라운드까지 한 곡이 끊기지 않고 이어지고, 3라운드만 곡이 바뀐다).
        ///
        /// <b>왜 파일을 복사해 두지 않는가.</b> 허브는 "같은 곡이면 끊지 않고 잇는다" 를 <b>클립 에셋이 같은가</b>로
        /// 판단한다(<c>AudioHub.PlayMusic</c>). 같은 곡을 두 파일로 두면 서로 다른 클립이라, 대기에서 1라운드로
        /// 넘어갈 때 같은 곡이 처음부터 다시 교차 페이드된다. 한 에셋을 여러 칸에 꽂아야 그대로 이어진다.
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<string, string> SharedFallback = new()
        {
            { "bgmRound1", "bgmWaiting" },
            { "bgmRound2", "bgmWaiting" },
        };

        /// <summary>아레나 카메라가 든 프리팹. 이 카메라가 곧 플레이어의 귀다 (<see cref="EnsureListener"/>).</summary>
        private const string ArenaPrefab = "Assets/Game/Prefabs/MiniGames/Warriors/Arena/WarriorsBeachArena.prefab";

        /// <summary>
        /// 아레나 카메라에 <see cref="AudioListener"/> 가 붙어 있게 한다. <b>없으면 아무 소리도 안 들린다.</b>
        ///
        /// 귀는 부트 씬 카메라에만 있었다. 그런데 <c>WarriorsLocalView</c> 는 화면이 겹치지 않게
        /// <b>남길 카메라 하나만 빼고 끄면서 그 카메라의 AudioListener 도 같이 끈다.</b> 남는 것은 아레나
        /// 카메라인데 거기엔 귀가 없어, 켜 줄 것이 없어서 <b>켜진 리스너가 0개</b>가 되었다.
        /// 판은 서버가 돌리니 멀쩡히 진행되는데 소리만 안 나서 원인을 찾기 어려웠다. (실측 2026-09-22)
        /// </summary>
        private static void EnsureListener()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(ArenaPrefab);

            if (root == null)
            {
                Debug.LogError($"[WarriorsAudio] {ArenaPrefab} 을 열지 못했습니다. 귀를 넣지 못했습니다.");
                return;
            }

            try
            {
                Camera camera = root.GetComponentInChildren<Camera>(true);

                if (camera == null)
                {
                    Debug.LogError($"[WarriorsAudio] {ArenaPrefab} 안에 카메라가 없습니다. 귀를 넣지 못했습니다.");
                    return;
                }

                if (camera.GetComponent<AudioListener>() != null)
                {
                    Debug.Log($"[WarriorsAudio] 귀는 이미 '{camera.name}' 에 있습니다.");
                    return;
                }

                camera.gameObject.AddComponent<AudioListener>();
                PrefabUtility.SaveAsPrefabAsset(root, ArenaPrefab);
                Debug.Log($"[WarriorsAudio] '{camera.name}' 에 귀(AudioListener)를 붙였습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        [MenuItem("Tools/아라아띠/Warriors 소리 놓고 클립 채우기")]
        public static void Install()
        {
            EnsureListener();

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
                    // 단 이어 쓰는 칸(SharedFallback)은 제 파일이 없으면 앞 칸의 클립을 **같은 에셋으로** 쓴다.
                    AudioClip clip = FindClip(field);
                    if (clip == null && SharedFallback.TryGetValue(field, out string from)) clip = FindClip(from);
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
