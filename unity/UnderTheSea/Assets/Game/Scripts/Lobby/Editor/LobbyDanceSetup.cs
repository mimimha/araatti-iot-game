using System.IO;
using System.Linq;
using UnderTheSea.Lobby.Dance;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 **춤** 을 준비한다. 여러 번 눌러도 같은 결과다.
    ///
    ///   1. <see cref="Dances"/> 의 클립을 반복 재생으로 맞춘다.
    ///   2. 공용 애니메이터(<c>Character_Movement</c>)에 <c>Dance</c> 파라미터와 <c>Dance1</c> ~ <c>Dance5</c> 상태를 단다.
    ///      <c>Movement</c> ↔ <c>DanceN</c> 은 <c>Dance == N</c> 으로 오간다. 기존 상태는 건드리지 않는다.
    ///      <c>Dance</c> 를 바꾸는 것은 로비의 <see cref="NetworkPlayerMover"/> 뿐이다.
    ///   3. 춤 휠 프리팹(<c>Resources/DanceWheel.prefab</c>)의 칸 이름을 <see cref="Dances"/> 의 이름으로 바꾼다.
    ///
    /// <b>춤을 바꾸거나 빼려면</b> <see cref="Dances"/> 의 줄을 고치고 다시 누른다. 뺀 클립 파일은
    /// 지워도 된다 — 이 목록 말고는 그 파일을 가리키는 곳이 없다.
    ///
    /// <code>
    ///   Tools/아라아띠/로비 춤 설치
    ///   Tools/아라아띠/로비 춤 자세 미리보기   — 우리 캐릭터에 입혀 PNG 로 찍는다(팔이 머리를 뚫는지 본다)
    /// </code>
    ///
    /// 클립 출처: A Hat in Time 팬 추출본(Hat Kid V2 Animations). Humanoid 근육 값이라 우리 캐릭터에 옮겨진다.
    /// ⚠ 상용 게임에서 뽑은 것이다. 공개 배포 전에는 다른 춤으로 바꾼다.
    /// </summary>
    public static class LobbyDanceSetup
    {
        private const string ClipFolder = "Assets/Game/Art/Animations/Lobby/Dance";
        private const string ControllerPath =
            "Assets/ithappy/Cute_Characters/Animations/Animation_Controllers/Character_Movement.controller";
        private const string WheelPrefabPath = "Assets/Game/Resources/DanceWheel.prefab";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";

        private const string MovementStateName = "Movement";
        private const string DanceStatePrefix = "Dance";

        /// <summary><see cref="NetworkPlayerMover"/> 의 danceId 와 같아야 한다.</summary>
        public const string DanceParam = "Dance";

        /// <summary>
        /// 휠 칸 순서(위에서 시계 방향)대로의 춤. 이름은 휠에 그대로 뜬다.
        /// 칸 수는 <see cref="NetworkPlayerMover.MaxDance"/> 와 같아야 한다.
        /// </summary>
        private static readonly (string name, string file)[] Dances =
        {
            ("Dance", "Dance.anim"),
            ("Dance Slow", "Dance Slow.anim"),
            ("Victory", "Victory.anim"),
            ("Idle_Taunt1", "Idle_Taunt1.anim"),
            ("idle_taunt3", "idle_taunt3.anim"),
        };

        [MenuItem("Tools/아라아띠/로비 춤 설치")]
        public static void Install()
        {
            if (Dances.Length != NetworkPlayerMover.MaxDance)
            {
                Debug.LogError($"[춤] 춤이 {Dances.Length}개입니다. 휠 칸 수({NetworkPlayerMover.MaxDance})와 같아야 합니다.");
                return;
            }

            AnimationClip[] clips = new AnimationClip[Dances.Length];
            for (int i = 0; i < Dances.Length; i++)
            {
                string path = $"{ClipFolder}/{Dances[i].file}";
                clips[i] = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if (clips[i] == null)
                {
                    Debug.LogError($"[춤] 클립이 없습니다: {path}");
                    return;
                }

                MakeLooping(clips[i]);
            }

            if (!WireController(clips))
            {
                return;
            }

            NameWheelSlots();

            AssetDatabase.SaveAssets();
            Debug.Log($"[춤] 설치했습니다 — {string.Join(" · ", Dances.Select(d => d.name))}");
        }

        public static void InstallFromCommandLine() => Install();

        private static void MakeLooping(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            if (settings.loopTime)
            {
                return;
            }

            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            EditorUtility.SetDirty(clip);
        }

        // ─────────────────────────────────────────────── 애니메이터

        private static bool WireController(AnimationClip[] clips)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[춤] {ControllerPath} 가 없습니다.");
                return false;
            }

            if (controller.parameters.All(p => p.name != DanceParam))
            {
                controller.AddParameter(DanceParam, AnimatorControllerParameterType.Int);
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == MovementStateName);
            if (movement == null)
            {
                Debug.LogError($"[춤] 애니메이터에 '{MovementStateName}' 상태가 없습니다.");
                return false;
            }

            // 다시 눌러도 쌓이지 않게, 춤 상태는 모두 지우고 새로 만든다. Movement 에서 나가던 전이도 같이 사라진다.
            foreach (ChildAnimatorState child in machine.states.Where(s => IsDanceState(s.state)).ToArray())
            {
                machine.RemoveState(child.state);
            }

            for (int i = 0; i < clips.Length; i++)
            {
                int number = i + 1;
                AnimatorState state = machine.AddState(DanceStatePrefix + number, new Vector3(520f, 40f + i * 60f, 0f));
                state.motion = clips[i];
                state.writeDefaultValues = true;

                AnimatorStateTransition enter = movement.AddTransition(state);
                enter.AddCondition(AnimatorConditionMode.Equals, number, DanceParam);
                ConfigureTransition(enter);

                AnimatorStateTransition exit = state.AddTransition(movement);
                exit.AddCondition(AnimatorConditionMode.NotEqual, number, DanceParam);
                ConfigureTransition(exit);
            }

            EditorUtility.SetDirty(controller);
            return true;
        }

        private static bool IsDanceState(AnimatorState state)
        {
            return state.name.StartsWith(DanceStatePrefix)
                   && int.TryParse(state.name.Substring(DanceStatePrefix.Length), out _);
        }

        private static void ConfigureTransition(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.25f;
        }

        // ─────────────────────────────────────────────── 휠 이름

        private static void NameWheelSlots()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(WheelPrefabPath);
            try
            {
                DanceWheelView view = root.GetComponent<DanceWheelView>();
                if (view == null)
                {
                    Debug.LogError($"[춤] {WheelPrefabPath} 에 DanceWheelView 가 없습니다.");
                    return;
                }

                var so = new SerializedObject(view);
                SerializedProperty names = so.FindProperty("danceNames");
                names.arraySize = Dances.Length;
                for (int i = 0; i < Dances.Length; i++)
                {
                    names.GetArrayElementAtIndex(i).stringValue = Dances[i].name;
                }
                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, WheelPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ─────────────────────────────────────────────── 미리보기

        /// <summary>
        /// 춤마다 우리 캐릭터에 입혀 앞 · 옆에서 8장씩 찍는다(<c>%TEMP%/DancePreview</c>).
        /// 발이 뜨거나 가라앉는지, 옆으로 흘러가는지도 로그로 남긴다.
        ///
        /// ⚠ <b>열린 씬을 건드리지 않는다.</b> 따로 만든 미리보기 씬에서 찍는다(헤엄 미리보기와 다른 점).
        ///    에디터가 열려 있을 때 눌러도 저장 안 한 작업이 사라지지 않는다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 춤 자세 미리보기")]
        public static void Preview()
        {
            PreviewTo(Path.Combine(Path.GetTempPath(), "DancePreview"));
        }

        public static void PreviewFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-previewOut");
            PreviewTo(at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetTempPath(), "DancePreview"));
        }

        private static void PreviewTo(string outDir)
        {
            Directory.CreateDirectory(outDir);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab == null)
            {
                Debug.LogError($"[춤 미리보기] {PlayerPrefabPath} 가 없습니다.");
                return;
            }

            Scene scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var light = new GameObject("Light").AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.2f;
                light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
                SceneManager.MoveGameObjectToScene(light.gameObject, scene);

                var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

                Animator animator = player.GetComponentInChildren<Animator>();
                animator.applyRootMotion = false;

                var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
                SceneManager.MoveGameObjectToScene(camera.gameObject, scene);
                camera.scene = scene;
                camera.orthographic = true;
                camera.orthographicSize = 1.0f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.92f, 0.92f, 0.92f);
                camera.nearClipPlane = 0.01f;
                camera.farClipPlane = 20f;

                const int Tile = 256;
                var rt = new RenderTexture(Tile, Tile, 24);
                camera.targetTexture = rt;

                SkinnedMeshRenderer[] skins = player.GetComponentsInChildren<SkinnedMeshRenderer>()
                    .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null)
                    .ToArray();

                // 캐릭터는 +Z 를 본다. 앞에서 · 게임 카메라처럼 뒤쪽 위에서 · 옆에서.
                var views = new (string name, Vector3 dir)[]
                {
                    ("front", Vector3.forward),
                    ("game", new Vector3(0f, 0.8f, -1f).normalized),
                    ("side", Vector3.right),
                };
                Vector3 focus = new Vector3(0f, 0.75f, 0f);

                const int Phases = 8;

                for (int d = 0; d < Dances.Length; d++)
                {
                    var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{Dances[d].file}");
                    if (clip == null)
                    {
                        Debug.LogError($"[춤 미리보기] 클립이 없습니다: {Dances[d].file}");
                        continue;
                    }

                    var sheets = views.Select(_ => new Texture2D(Tile * 4, Tile * 2, TextureFormat.RGB24, false)).ToArray();

                    AnimationClipPlayable playable = AnimationPlayableUtilities.PlayClip(animator, clip, out PlayableGraph graph);
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

                    // ⚠ 그래프를 만든 직후 처음 두어 번의 Evaluate 는 자세가 들어가지 않는다. 헛돌린다(헤엄 미리보기와 같다).
                    for (int i = 0; i < 3; i++)
                    {
                        playable.SetTime(0.1f * i);
                        graph.Evaluate();
                    }

                    float minY = float.MaxValue, maxY = float.MinValue, maxDrift = 0f;

                    for (int p = 0; p < Phases; p++)
                    {
                        playable.SetTime((p + 0.5f) / Phases * clip.length);
                        graph.Evaluate();

                        Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                        maxDrift = Mathf.Max(maxDrift, new Vector2(hips.position.x, hips.position.z).magnitude);

                        // ⚠ camera.Render() 는 스킨 메시를 다시 굽지 않아 T 자세로 찍힌다. 지금 자세를 구워 찍는다.
                        GameObject baked = BakePose(skins, scene);
                        Bounds body = BodyBounds(baked);
                        minY = Mathf.Min(minY, body.min.y);
                        maxY = Mathf.Max(maxY, body.max.y);

                        for (int v = 0; v < views.Length; v++)
                        {
                            Vector3 dir = views[v].dir;
                            camera.transform.position = focus + dir * 5f;
                            camera.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
                            CaptureInto(camera, rt, sheets[v], (p % 4) * Tile, (1 - p / 4) * Tile);
                        }

                        Object.DestroyImmediate(baked);
                        foreach (SkinnedMeshRenderer skin in skins)
                        {
                            skin.enabled = true;
                        }
                    }

                    graph.Destroy();

                    string safe = $"{d + 1}_{Dances[d].name.Replace(' ', '_')}";
                    for (int v = 0; v < views.Length; v++)
                    {
                        File.WriteAllBytes(Path.Combine(outDir, $"dance{safe}_{views[v].name}.png"), sheets[v].EncodeToPNG());
                        Object.DestroyImmediate(sheets[v]);
                    }

                    Debug.Log($"[춤 미리보기] {d + 1}. {Dances[d].name}  길이 {clip.length:F1}초 · 몸 y {minY:F2}~{maxY:F2} " +
                              $"· 엉덩이가 제자리에서 벗어난 최대 거리 {maxDrift:F2}m");
                }

                camera.targetTexture = null;
                Object.DestroyImmediate(rt);
                Debug.Log($"[춤 미리보기] → {outDir}");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static GameObject BakePose(SkinnedMeshRenderer[] skins, Scene scene)
        {
            var root = new GameObject("BakedPose");
            SceneManager.MoveGameObjectToScene(root, scene);

            foreach (SkinnedMeshRenderer skin in skins)
            {
                var mesh = new Mesh();
                skin.BakeMesh(mesh, true);

                var part = new GameObject(skin.name);
                part.transform.SetParent(root.transform, false);
                part.transform.SetPositionAndRotation(skin.transform.position, skin.transform.rotation);

                part.AddComponent<MeshFilter>().sharedMesh = mesh;
                part.AddComponent<MeshRenderer>().sharedMaterials = skin.sharedMaterials;

                skin.enabled = false;
            }

            return root;
        }

        private static Bounds BodyBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            Bounds b = renderers[0].bounds;
            foreach (Renderer r in renderers.Skip(1))
            {
                b.Encapsulate(r.bounds);
            }

            return b;
        }

        private static void CaptureInto(Camera camera, RenderTexture rt, Texture2D sheet, int x, int y)
        {
            camera.Render();

            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = rt;
            sheet.ReadPixels(new Rect(0, 0, rt.width, rt.height), x, y);
            sheet.Apply();
            RenderTexture.active = previous;
        }
    }
}
