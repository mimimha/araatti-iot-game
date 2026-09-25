using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Lobby.Editor
{
    /// <summary>
    /// 로비 바다 **헤엄** 에 쓰는 모션을 준비한다. 여러 번 눌러도 같은 결과다.
    ///
    ///   1. <c>Swimming.fbx</c>(Mixamo, 스킨 없음)를 Humanoid 로 임포트한다.
    ///      뼈 길이가 다른 캐릭터에 입힐 수 있는 것은 Humanoid 리타깃 덕분이다.
    ///   2. 그 클립을 <c>Swim.anim</c> 으로 옮기며 **우리 캐릭터 비율에 맞게 근육 값을 보정**한다.
    ///      Mixamo 는 머리가 작은 어른 몸이라 그대로 입히면 팔이 큰 머리를 뚫고 지나간다.
    ///      보정 값은 <see cref="MuscleOffsets"/> 에 모아 둔다.
    ///   3. 공용 애니메이터(<c>Character_Movement</c>)에 <c>Swim</c> 상태와 파라미터를 더한다.
    ///      기존 상태는 건드리지 않는다. <c>IsSwimming</c> 을 켜는 것은 로비의 <see cref="NetworkPlayerMover"/> 뿐이다.
    ///
    /// <code>
    ///   Tools/아라아띠/로비 헤엄 모션 설치
    ///   Tools/아라아띠/로비 헤엄 자세 미리보기   — PNG 로 찍어 비율을 눈으로 본다
    ///   Tools/아라아띠/로비 헤엄 가능 수역 진단  — 해안선 벽 안쪽 바다가 얼마나 깊은지 센다
    /// </code>
    /// </summary>
    public static class LobbySwimSetup
    {
        private const string SourceFbx = "Assets/Game/Art/Animations/Lobby/Swimming.fbx";
        private const string SwimClipPath = "Assets/Game/Art/Animations/Lobby/Swim.anim";
        private const string ControllerPath =
            "Assets/ithappy/Cute_Characters/Animations/Animation_Controllers/Character_Movement.controller";
        private const string PlayerPrefabPath = "Assets/Game/Prefabs/Characters/NetworkPlayer.prefab";
        private const string LobbyScenePath = "Assets/Game/Scenes/Main/CoreGames/Lobby.unity";

        private const string SwimStateName = "Swim";
        private const string MovementStateName = "Movement";

        /// <summary><see cref="NetworkPlayerMover"/> 와 이름이 같아야 한다.</summary>
        public const string SwimmingParam = "IsSwimming";
        public const string SwimSpeedParam = "SwimSpeed";

        /// <summary>
        /// 우리 캐릭터 비율에 맞춘 근육 보정. 클립의 근육 값(-1 ~ 1)에 그대로 더한다.
        ///
        /// 이름은 <see cref="HumanTrait.MuscleName"/> 그대로다. 값은 미리보기 PNG 를 보며 맞췄다.
        ///
        /// 원본은 평영이다. Mixamo 어른 몸에 맞춘 동작이라 우리 캐릭터(머리가 몸만 하다)에 그대로 입히면
        ///   · 앞으로 뻗은 팔이 큰 머리 밑으로 파고들어 팔이 없어 보이고
        ///   · 얼굴을 물에 박은 채라 뒤에서 보면 뒤통수만 떠 있다.
        /// 팔은 앞으로 덜 뻗고(Front-Back −) 조금 벌리며(Down-Up +),
        /// 목과 머리를 들어(Nod +) 얼굴이 앞을 보고 머리가 물 위에 뜨게 한다.
        ///
        /// ⚠ Down-Up 을 크게 올리면 엎드린 몸에서는 팔이 머리 쪽으로 붙어 오히려 더 겹친다. 0.35 로 해 보고 확인했다.
        /// </summary>
        private static readonly Dictionary<string, float> MuscleOffsets = new Dictionary<string, float>
        {
            { "Left Arm Front-Back", -0.35f },
            { "Right Arm Front-Back", -0.35f },
            { "Left Arm Down-Up", 0.1f },
            { "Right Arm Down-Up", 0.1f },
            { "Neck Nod Down-Up", 0.6f },
            { "Head Nod Down-Up", 0.6f },
        };

        [MenuItem("Tools/아라아띠/로비 헤엄 모션 설치")]
        public static void Install()
        {
            if (!ConfigureImport())
            {
                return;
            }

            AnimationClip swim = BuildSwimClip();
            if (swim == null)
            {
                return;
            }

            WireController(swim);

            AssetDatabase.SaveAssets();
            Debug.Log("[헤엄] 모션 설치를 마쳤다.");
        }

        // ─────────────────────────────────────────────── 1. 임포트

        private static bool ConfigureImport()
        {
            var importer = AssetImporter.GetAtPath(SourceFbx) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"[헤엄] {SourceFbx} 가 없다.");
                return false;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;

            ModelImporterClipAnimation[] clips = importer.clipAnimations.Length > 0
                ? importer.clipAnimations
                : importer.defaultClipAnimations;

            if (clips.Length == 0)
            {
                Debug.LogError($"[헤엄] {SourceFbx} 에 애니메이션이 없다.");
                return false;
            }

            ModelImporterClipAnimation clip = clips[0];
            clip.name = "Swimming";
            clip.loopTime = true;
            clip.loopPose = true;

            // 제자리에서 헤엄치게 뿌리 움직임을 자세에 굳힌다. 실제로 나아가는 것은 서버가 정한다.
            // Original 기준이어야 Mixamo 의 "엎드린 몸" 이 그대로 남는다.
            clip.lockRootRotation = true;
            clip.keepOriginalOrientation = true;
            clip.lockRootHeightY = true;
            clip.keepOriginalPositionY = true;
            clip.lockRootPositionXZ = true;
            clip.keepOriginalPositionXZ = true;

            importer.clipAnimations = new[] { clip };
            importer.SaveAndReimport();

            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(SourceFbx).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid)
            {
                Debug.LogError("[헤엄] Humanoid 아바타를 만들지 못했다. Mixamo 뼈 이름이 맞는지 확인해라.");
                return false;
            }

            return true;
        }

        // ─────────────────────────────────────────────── 2. 비율 보정 클립

        private static AnimationClip LoadSourceClip()
        {
            return AssetDatabase.LoadAllAssetsAtPath(SourceFbx)
                .OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));
        }

        private static AnimationClip BuildSwimClip()
        {
            AnimationClip source = LoadSourceClip();
            if (source == null)
            {
                Debug.LogError("[헤엄] 임포트한 클립을 못 찾았다.");
                return null;
            }

            var swim = AssetDatabase.LoadAssetAtPath<AnimationClip>(SwimClipPath);
            if (swim == null)
            {
                swim = new AnimationClip { name = "Swim" };
                AssetDatabase.CreateAsset(swim, SwimClipPath);
            }

            swim.ClearCurves();
            swim.frameRate = source.frameRate;

            var unused = new HashSet<string>(MuscleOffsets.Keys);

            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(source))
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(source, binding);

                if (MuscleOffsets.TryGetValue(binding.propertyName, out float offset))
                {
                    unused.Remove(binding.propertyName);

                    Keyframe[] keys = curve.keys;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        keys[i].value = Mathf.Clamp(keys[i].value + offset, -1f, 1f);
                    }

                    curve.keys = keys;
                }

                AnimationUtility.SetEditorCurve(swim, binding, curve);
            }

            foreach (string name in unused)
            {
                Debug.LogWarning($"[헤엄] 보정할 근육 '{name}' 이 클립에 없다. 이름을 확인해라.");
            }

            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            settings.loopTime = true;
            AnimationUtility.SetAnimationClipSettings(swim, settings);

            EditorUtility.SetDirty(swim);
            return swim;
        }

        // ─────────────────────────────────────────────── 3. 애니메이터

        private static void WireController(AnimationClip swim)
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                Debug.LogError($"[헤엄] {ControllerPath} 가 없다.");
                return;
            }

            if (controller.parameters.All(p => p.name != SwimmingParam))
            {
                controller.AddParameter(SwimmingParam, AnimatorControllerParameterType.Bool);
            }

            if (controller.parameters.All(p => p.name != SwimSpeedParam))
            {
                controller.AddParameter(new AnimatorControllerParameter
                {
                    name = SwimSpeedParam,
                    type = AnimatorControllerParameterType.Float,
                    defaultFloat = 1f,
                });
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;
            AnimatorState movement = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == MovementStateName);
            if (movement == null)
            {
                Debug.LogError($"[헤엄] 애니메이터에 '{MovementStateName}' 상태가 없다.");
                return;
            }

            AnimatorState swimState = machine.states.Select(s => s.state).FirstOrDefault(s => s.name == SwimStateName)
                                      ?? machine.AddState(SwimStateName, new Vector3(170f, 380f, 0f));

            swimState.motion = swim;
            swimState.speedParameterActive = true;
            swimState.speedParameter = SwimSpeedParam;
            swimState.writeDefaultValues = true;

            // 다시 눌러도 전이가 쌓이지 않게 이 두 상태 사이의 것만 지우고 새로 단다.
            foreach (AnimatorStateTransition t in movement.transitions.Where(t => t.destinationState == swimState).ToArray())
            {
                movement.RemoveTransition(t);
            }

            foreach (AnimatorStateTransition t in swimState.transitions.ToArray())
            {
                swimState.RemoveTransition(t);
            }

            AnimatorStateTransition enter = movement.AddTransition(swimState);
            enter.AddCondition(AnimatorConditionMode.If, 0f, SwimmingParam);
            ConfigureTransition(enter);

            AnimatorStateTransition exit = swimState.AddTransition(movement);
            exit.AddCondition(AnimatorConditionMode.IfNot, 0f, SwimmingParam);
            ConfigureTransition(exit);

            EditorUtility.SetDirty(controller);
        }

        private static void ConfigureTransition(AnimatorStateTransition t)
        {
            t.hasExitTime = false;
            t.hasFixedDuration = true;
            t.duration = 0.3f;
        }

        // ─────────────────────────────────────────────── 미리보기

        /// <summary>
        /// 헤엄 자세를 옆 · 앞 · 위에서 찍어 PNG 로 남긴다. 몸 부위 높이도 로그로 남긴다.
        /// <see cref="NetworkPlayerMover"/> 의 헤엄 높이는 이 수치로 정한다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 헤엄 자세 미리보기")]
        public static void Preview()
        {
            PreviewTo(Path.Combine(Path.GetTempPath(), "SwimPreview"));
        }

        private static void PreviewTo(string outDir)
        {
            Directory.CreateDirectory(outDir);

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SwimClipPath);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (clip == null || prefab == null)
            {
                Debug.LogError("[헤엄 미리보기] Swim.anim 이나 NetworkPlayer 프리팹이 없다. 먼저 설치해라.");
                return;
            }

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var light = new GameObject("Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(40f, -30f, 0f);

            var player = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            player.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            foreach (SkinnedMeshRenderer smr in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.updateWhenOffscreen = true;
            }

            Animator animator = player.GetComponentInChildren<Animator>();
            animator.applyRootMotion = false;

            // 수면(y=0) 표시. 캐릭터 발(피벗)은 원점이다. 높이는 실제 수치를 본 뒤 맞춘다.
            var water = GameObject.CreatePrimitive(PrimitiveType.Cube);
            water.name = "WaterLine";
            water.transform.localScale = new Vector3(3f, 0.005f, 3f);
            var waterMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            waterMat.color = new Color(0.2f, 0.55f, 0.9f);
            water.GetComponent<Renderer>().sharedMaterial = waterMat;
            water.SetActive(false);

            var camera = new GameObject("PreviewCamera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 0.9f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.92f, 0.92f, 0.92f);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 20f;

            const int Tile = 256;
            var rt = new RenderTexture(Tile, Tile, 24);
            camera.targetTexture = rt;

            AnimationClipPlayable playable = AnimationPlayableUtilities.PlayClip(animator, clip, out PlayableGraph graph);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);

            // ⚠ 그래프를 만든 직후 처음 두어 번의 Evaluate 는 자세가 들어가지 않는다(T 자세로 남는다). 헛돌린다.
            for (int i = 0; i < 3; i++)
            {
                playable.SetTime(0.1f * i);
                graph.Evaluate();
            }

            const int Phases = 8;

            // 게임 카메라처럼 뒤쪽 위에서 내려다본 것, 그리고 같은 각도에 수면을 깐 것.
            Vector3 gameDir = new Vector3(0f, 0.8f, -1f).normalized;
            var views = new (string name, Vector3 dir, bool water)[]
            {
                ("side", Vector3.right, false),
                ("top", Vector3.up, false),
                ("bottom", Vector3.down, false),
                ("game", gameDir, false),
                ("game_water", gameDir, true),
            };

            // 칸마다 같은 자리를 보게 중심을 고정한다. 그래야 칸끼리 움직임이 비교된다.
            Vector3 focus = new Vector3(0f, 0.3f, 0.3f);
            water.transform.position = new Vector3(0f, PreviewWaterY, 0f);

            var sheets = views.Select(_ => new Texture2D(Tile * 4, Tile * 2, TextureFormat.RGB24, false)).ToArray();

            float minY = float.MaxValue, maxY = float.MinValue, hipsSum = 0f;

            SkinnedMeshRenderer[] skins = player.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null)
                .ToArray();

            for (int p = 0; p < Phases; p++)
            {
                // ⚠ 첫 칸은 헛돌려도 선 자세로 찍힐 때가 있다. 미리보기 그래프 문제다.
                //    클립 자체는 첫 키부터 누워 있다(RootQ 로 확인했다). 첫 칸은 무시하고 본다.
                float phase = (p + 0.5f) / Phases;

                playable.SetTime(phase * clip.length);
                graph.Evaluate();

                Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
                Transform head = animator.GetBoneTransform(HumanBodyBones.Head);

                // ⚠ 배치 모드의 camera.Render() 는 스킨 메시를 다시 굽지 않아 T 자세로 찍힌다.
                //    그 순간의 자세를 메시로 구워 놓고 찍는다. 경계도 이것으로 잰다.
                GameObject baked = BakePose(skins);

                // 몸이 차지하는 높이. 뼈 위치만으로 재면 머리 · 손 끝이 빠지므로 렌더러 경계로 잰다.
                Bounds body = BodyBounds(baked);
                minY = Mathf.Min(minY, body.min.y);
                maxY = Mathf.Max(maxY, body.max.y);
                hipsSum += hips.position.y;

                Debug.Log($"[헤엄 미리보기] t={phase:F2}  엉덩이 y={hips.position.y:F2} · 머리 y={head.position.y:F2} " +
                          $"· 몸 y {body.min.y:F2}~{body.max.y:F2} · 몸 중심 {body.center.ToString("F2")} " +
                          $"· 머리 방향 {(head.position - hips.position).normalized.ToString("F2")}");

                for (int v = 0; v < views.Length; v++)
                {
                    var (_, dir, showWater) = views[v];

                    water.SetActive(showWater);
                    camera.transform.position = focus + dir * 5f;
                    bool vertical = Mathf.Abs(dir.y) > 0.99f;
                    camera.transform.rotation = Quaternion.LookRotation(-dir, vertical ? Vector3.forward : Vector3.up);

                    // 위 줄이 앞 네 칸, 아래 줄이 뒤 네 칸이다. 텍스처는 아래에서 위로 쌓인다.
                    CaptureInto(camera, rt, sheets[v], (p % 4) * Tile, (1 - p / 4) * Tile);
                }

                Object.DestroyImmediate(baked);
            }

            for (int v = 0; v < views.Length; v++)
            {
                File.WriteAllBytes(Path.Combine(outDir, $"swim_{views[v].name}.png"), sheets[v].EncodeToPNG());
                Object.DestroyImmediate(sheets[v]);
            }

            Debug.Log($"[헤엄 미리보기] 전체  몸 y {minY:F2}~{maxY:F2} · 엉덩이 평균 y {hipsSum / Phases:F2}  → {outDir}");

            graph.Destroy();
            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
        }

        /// <summary>미리보기에서 수면을 깔 높이(피벗 기준). <see cref="NetworkPlayerMover"/> 의 swimSinkDepth 와 같게 둔다.</summary>
        private const float PreviewWaterY = 0.3f;

        /// <summary>스킨 메시들을 지금 자세로 구워 한 오브젝트 아래에 모은다. 원래 렌더러는 끈다.</summary>
        private static GameObject BakePose(SkinnedMeshRenderer[] skins)
        {
            var root = new GameObject("BakedPose");

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
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy)
                .ToArray();

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

        // ─────────────────────────────────────────────── 진단

        /// <summary>
        /// 해안선 벽 안쪽에서 **실제로 걸어 들어갈 수 있는 바다가 얼마나 깊은지** 센다. 씬을 고치지 않는다.
        ///
        /// 벽은 물이 어깨까지 차는 곳에 서 있으므로(<see cref="LobbyCoastlineBlocker"/>),
        /// 헤엄칠 수 있는 곳은 그보다 얕은 띠다. 그 띠가 얼마나 넓은지 보고 헤엄 시작 깊이를 정한다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 헤엄 가능 수역 진단")]
        public static void Report()
        {
            Survey(out _);
        }

        /// <summary>
        /// <see cref="Report"/> 의 본체. 스폰에서 닿는 바다 가운데 **스폰에서 가장 가까운 깊은 곳**(0.9m 이상)도
        /// 돌려준다. 물속 미리보기를 거기서 찍는다. 없으면 null.
        /// </summary>
        private static void Survey(out Vector3? deepSpot)
        {
            deepSpot = null;
            float bestDistance = float.MaxValue;

            Scene open = SceneManager.GetActiveScene();
            if (!open.IsValid() || open.path != LobbyScenePath)
            {
                EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            }

            var min = new Vector2(-200f, -120f);
            var max = new Vector2(240f, 320f);
            const float cell = 1f;
            var spawn = new Vector3(20.84f, 1.73f, 48.56f);

            float shoulder = LobbyWaterBlocker.ShoulderDepth();

            int nx = Mathf.CeilToInt((max.x - min.x) / cell);
            int nz = Mathf.CeilToInt((max.y - min.y) / cell);

            var depth = new float[nx, nz];
            var buffer = new RaycastHit[32];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float x = min.x + (i + 0.5f) * cell, z = min.y + (j + 0.5f) * cell;
                    int count = Physics.RaycastNonAlloc(new Vector3(x, 150f, z), Vector3.down, buffer, 400f,
                        ~0, QueryTriggerInteraction.Ignore);

                    float top = float.MinValue;
                    for (int k = 0; k < count; k++)
                    {
                        if (buffer[k].collider.transform.root.name == "WaterBlockers") continue;
                        top = Mathf.Max(top, buffer[k].point.y);
                    }

                    depth[i, j] = top > float.MinValue ? -top : float.MaxValue;
                }
            }

            int si = Mathf.FloorToInt((spawn.x - min.x) / cell);
            int sj = Mathf.FloorToInt((spawn.z - min.y) / cell);

            var seen = new bool[nx, nz];
            var queue = new Queue<(int, int)>();
            queue.Enqueue((si, sj));
            seen[si, sj] = true;

            float[] edges = { 0.3f, 0.4f, 0.5f, 0.6f, 0.7f, 0.8f, 0.9f };
            var buckets = new int[edges.Length];

            while (queue.Count > 0)
            {
                (int ci, int cj) = queue.Dequeue();

                for (int b = 0; b < edges.Length; b++)
                {
                    if (depth[ci, cj] >= edges[b]) buckets[b]++;
                }

                if (depth[ci, cj] >= 0.9f)
                {
                    var at = new Vector3(min.x + (ci + 0.5f) * cell, -depth[ci, cj], min.y + (cj + 0.5f) * cell);
                    float d = Vector2.Distance(new Vector2(at.x, at.z), new Vector2(spawn.x, spawn.z));

                    if (d < bestDistance)
                    {
                        bestDistance = d;
                        deepSpot = at;
                    }
                }

                foreach ((int di, int dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || seen[ni, nj]) continue;
                    if (depth[ni, nj] >= shoulder) continue;

                    seen[ni, nj] = true;
                    queue.Enqueue((ni, nj));
                }
            }

            string lines = string.Join("  ", edges.Select((e, b) => $"≥{e:F1}m:{buckets[b]}칸"));
            Debug.Log($"[헤엄 진단] 어깨 깊이(벽 기준) {shoulder:F2}m. 스폰에서 닿는 바다 깊이별 칸 수(1m²) — {lines}" +
                      (deepSpot is Vector3 spot ? $" · 스폰에서 가장 가까운 0.9m 이상 {spot.ToString("F1")}" : ""));
        }

        /// <summary>
        /// 해안선 벽을 바깥으로 옮기기 전에, **벽 너머 바다가 해안에서 멀어질수록 얼마나 깊어지는지** 잰다.
        /// 씬을 고치지 않는다.
        ///
        /// 지금 걸어 다닐 수 있는 곳(스폰에서 닿는 칸)에서 바다 쪽으로 한 칸씩 넓혀 가며
        /// 해안에서의 거리별로 바닥 깊이 분포와, 물 위로 솟은 바위 · 바닥 없는 칸의 비율을 센다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 바깥 바다 깊이 진단")]
        public static void ReportOuterSea()
        {
            if (SceneManager.GetActiveScene().path != LobbyScenePath)
            {
                EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            }

            var min = new Vector2(-200f, -120f);
            var max = new Vector2(240f, 320f);
            const float cell = 1f;
            var spawn = new Vector3(20.84f, 1.73f, 48.56f);
            float shoulder = LobbyWaterBlocker.ShoulderDepth();

            int nx = Mathf.CeilToInt((max.x - min.x) / cell);
            int nz = Mathf.CeilToInt((max.y - min.y) / cell);

            // 바닥 높이. 없으면 NaN.
            var ground = new float[nx, nz];
            var buffer = new RaycastHit[32];

            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < nz; j++)
                {
                    float x = min.x + (i + 0.5f) * cell, z = min.y + (j + 0.5f) * cell;
                    int count = Physics.RaycastNonAlloc(new Vector3(x, 150f, z), Vector3.down, buffer, 400f,
                        ~0, QueryTriggerInteraction.Ignore);

                    float top = float.NaN;
                    for (int k = 0; k < count; k++)
                    {
                        if (buffer[k].collider.transform.root.name == "WaterBlockers") continue;
                        top = float.IsNaN(top) ? buffer[k].point.y : Mathf.Max(top, buffer[k].point.y);
                    }

                    ground[i, j] = top;
                }
            }

            // 1) 지금 갈 수 있는 곳.
            var dist = new int[nx, nz];
            for (int i = 0; i < nx; i++) for (int j = 0; j < nz; j++) dist[i, j] = -1;

            var steps = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            var queue = new Queue<(int, int)>();
            int si = Mathf.FloorToInt((spawn.x - min.x) / cell), sj = Mathf.FloorToInt((spawn.z - min.y) / cell);
            dist[si, sj] = 0;
            queue.Enqueue((si, sj));

            var frontier = new List<(int, int)>();

            while (queue.Count > 0)
            {
                (int ci, int cj) = queue.Dequeue();
                foreach ((int di, int dj) in steps)
                {
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || dist[ni, nj] >= 0) continue;

                    float g = ground[ni, nj];
                    if (!float.IsNaN(g) && -g < shoulder)
                    {
                        dist[ni, nj] = 0;
                        queue.Enqueue((ni, nj));
                    }
                    else if (!float.IsNaN(g) && -g >= shoulder)
                    {
                        // 벽 바로 너머 깊은 물. 여기서부터 바깥으로 센다.
                        dist[ni, nj] = 1;
                        frontier.Add((ni, nj));
                    }
                }
            }

            // 2) 벽 너머 깊은 물을 한 칸씩 넓혀 간다. 물 위로 솟은 곳(바위 · 다른 섬)은 넘지 않는다.
            const int MaxDistance = 60;
            foreach ((int, int) f in frontier) queue.Enqueue(f);

            while (queue.Count > 0)
            {
                (int ci, int cj) = queue.Dequeue();
                if (dist[ci, cj] >= MaxDistance) continue;

                foreach ((int di, int dj) in steps)
                {
                    int ni = ci + di, nj = cj + dj;
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= nz || dist[ni, nj] >= 0) continue;

                    dist[ni, nj] = dist[ci, cj] + 1;
                    float g = ground[ni, nj];

                    // 바닥이 없거나 물 위로 솟았으면 거기서 멈춘다(그 칸은 세기만 한다).
                    if (float.IsNaN(g) || g >= 0f) continue;
                    queue.Enqueue((ni, nj));
                }
            }

            // 3) 거리 띠별로 센다.
            int[] bands = { 5, 10, 15, 20, 25, 30, 40, 50, 60 };
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"[바깥 바다 진단] 지금 벽(어깨 {shoulder:F2}m) 너머, 해안선에서의 거리별 바닥 깊이:");

            int lo = 1;
            foreach (int hi in bands)
            {
                var depths = new List<float>();
                int dry = 0, voids = 0;

                for (int i = 0; i < nx; i++)
                {
                    for (int j = 0; j < nz; j++)
                    {
                        int d = dist[i, j];
                        if (d < lo || d > hi) continue;

                        float g = ground[i, j];
                        if (float.IsNaN(g)) voids++;
                        else if (g >= 0f) dry++;
                        else depths.Add(-g);
                    }
                }

                depths.Sort();
                string q(float p) => depths.Count == 0 ? "-" : depths[Mathf.Clamp((int)(p * depths.Count), 0, depths.Count - 1)].ToString("F1");

                sb.AppendLine($"  {lo,2}~{hi,2}m : 물 {depths.Count,5}칸  깊이 최소 {q(0f)} · 10% {q(0.1f)} · 중간 {q(0.5f)} · 90% {q(0.9f)} · 최대 {q(0.999f)}" +
                              $"  | 솟은 곳 {dry} · 바닥 없음 {voids}");
                lo = hi + 1;
            }

            Debug.Log(sb.ToString());
        }

        /// <summary><paramref name="center"/> 에서 가장 가까운, 바닥 깊이가 [min, max] 인 곳의 바닥 좌표. 1m 간격으로 찾는다.</summary>
        private static Vector3? FindSeabedNear(Vector3 center, float minDepth, float maxDepth, int radius)
        {
            var buffer = new RaycastHit[32];
            Vector3? best = null;
            float bestDistance = float.MaxValue;

            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dz = -radius; dz <= radius; dz++)
                {
                    float d = dx * dx + dz * dz;
                    if (d >= bestDistance || d > radius * radius) continue;

                    var at = new Vector3(center.x + dx, 150f, center.z + dz);
                    int count = Physics.RaycastNonAlloc(at, Vector3.down, buffer, 400f, ~0, QueryTriggerInteraction.Ignore);

                    float top = float.MinValue;
                    for (int k = 0; k < count; k++)
                    {
                        if (buffer[k].collider.transform.root.name == "WaterBlockers") continue;
                        top = Mathf.Max(top, buffer[k].point.y);
                    }

                    if (top > float.MinValue && -top >= minDepth && -top <= maxDepth)
                    {
                        bestDistance = d;
                        best = new Vector3(at.x, top, at.z);
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// 해안선 벽이 제대로 들어 있는지 센다. 씬을 고치지 않는다.
        /// 스폰 앞바다(미리보기 지점)에서 바깥으로 수평 광선을 쏴, 몇 m 앞에서 벽에 닿는지도 본다.
        /// </summary>
        public static void CheckCoastlineFromCommandLine()
        {
            EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);

            GameObject root = SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g => g.name == "WaterBlockers");
            Transform coast = root != null ? root.transform.Find("Blocker_Coastline") : null;

            if (coast == null)
            {
                Debug.LogError("[벽 확인] WaterBlockers/Blocker_Coastline 이 없다.");
                return;
            }

            BoxCollider[] boxes = coast.GetComponents<BoxCollider>();
            float deepest = boxes.Min(b => b.bounds.min.y);

            Vector3 spawn = new Vector3(20.84f, 0f, 48.56f);
            Vector3? spot = FindSeabedNear(spawn, 1.5f, 2.5f, 60);
            string probe = "-";

            if (spot is Vector3 at)
            {
                Vector3 dir = new Vector3(at.x - spawn.x, 0f, at.z - spawn.z).normalized;
                Vector3 from = new Vector3(at.x, at.y + 0.5f, at.z);

                probe = Physics.Raycast(from, dir, out RaycastHit hit, 200f, ~0, QueryTriggerInteraction.Ignore)
                    ? $"{at.ToString("F1")}(깊이 {-at.y:F1}m) 에서 바다 쪽으로 {hit.distance:F1}m 앞 '{hit.collider.name}'"
                    : "아무것도 안 걸림";
            }

            Debug.Log($"[벽 확인] 해안선 벽 {boxes.Length}장 · 가장 깊은 아래끝 y={deepest:F1} · {probe}");
        }

        public static void ReportOuterSeaFromCommandLine()
        {
            ReportOuterSea();
        }

        // ─────────────────────────────────────────────── 물속 연출

        private const string UnderwaterPrefabPath = "Assets/Game/Resources/LobbyUnderwater.prefab";
        private const string UnderwaterSurfaceMatPath = "Assets/Game/Art/Materials/Lobby/Lobby_UnderwaterSurface.mat";
        private const string UnderwaterTintMatPath = "Assets/Game/Art/Materials/Lobby/Lobby_UnderwaterSurfaceTint.mat";
        private const string UnderwaterProfilePath = "Assets/Game/Art/Rendering/Lobby_Underwater.asset";
        private const string CausticsTexturePath = "Assets/Synty/PNB_Core/Textures/caustics_color_split.png";
        private const string BubblesPrefabPath = "Assets/Synty/PolygonNatureBiomes/PNB_Tropical_Jungle/FX/FX_Prefabs/FX_Bubbles_01.prefab";

        /// <summary>
        /// <see cref="UnderTheSea.Lobby.LobbyUnderwaterView"/> 가 쓰는 재질 · 볼륨을 만들고
        /// <c>Resources/LobbyUnderwater.prefab</c> 에 이어 붙인다. 여러 번 눌러도 같은 결과다.
        ///
        /// 에셋으로 만드는 이유: 런타임에 <c>new Material</c> 로 투명 · 가산 키워드를 켜면
        /// 빌드에서 그 셰이더 변형이 빠져 분홍색으로 나온다. 에셋이 프리팹에 걸려 있어야 빌드에 들어간다.
        /// </summary>
        [MenuItem("Tools/아라아띠/로비 물속 연출 프리팹 만들기")]
        public static void BuildUnderwater()
        {
            Material surface = BuildSurfaceMaterial();
            Material tint = BuildSurfaceTintMaterial();
            VolumeProfile profile = BuildUnderwaterProfile();

            var bubbles = AssetDatabase.LoadAssetAtPath<GameObject>(BubblesPrefabPath);
            ParticleSystem bubblesSystem = bubbles != null ? bubbles.GetComponent<ParticleSystem>() : null;

            if (surface == null || tint == null || profile == null || bubblesSystem == null)
            {
                Debug.LogError("[물속] 재질 · 볼륨 · 물방울 중 하나를 만들거나 찾지 못했다. 위 오류를 봐라.");
                return;
            }

            var root = new GameObject("LobbyUnderwater");
            var view = root.AddComponent<UnderTheSea.Lobby.LobbyUnderwaterView>();

            var so = new SerializedObject(view);
            so.FindProperty("underwaterProfile").objectReferenceValue = profile;
            so.FindProperty("surfaceMaterial").objectReferenceValue = surface;
            so.FindProperty("surfaceTintMaterial").objectReferenceValue = tint;
            so.FindProperty("bubblesPrefab").objectReferenceValue = bubblesSystem;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(root, UnderwaterPrefabPath);
            Object.DestroyImmediate(root);

            AssetDatabase.SaveAssets();
            Debug.Log($"[물속] {UnderwaterPrefabPath} 를 만들었다.");
        }

        /// <summary>
        /// 아래에서 올려다본 수면. URP Unlit 을 <b>가산</b>으로 두고 물결 무늬를 얹는다.
        /// 뒤로 하늘이 비치면서 밝은 물결 줄이 겹쳐 보인다.
        /// </summary>
        private static Material BuildSurfaceMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            var caustics = AssetDatabase.LoadAssetAtPath<Texture2D>(CausticsTexturePath);

            if (shader == null || caustics == null)
            {
                Debug.LogError($"[물속] URP Unlit 셰이더나 {CausticsTexturePath} 가 없다.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(UnderwaterSurfaceMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, UnderwaterSurfaceMatPath);
            }

            mat.shader = shader;
            mat.SetTexture("_BaseMap", caustics);
            mat.SetColor("_BaseColor", new Color(0.6f, 1f, 0.9f, 1f));
            MakeTransparent(mat, additive: true);

            // 청록 막보다 뒤에 그려야 무늬가 막 위에 얹힌다.
            mat.renderQueue = (int)RenderQueue.Transparent + 1;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// 아래에서 본 수면의 **청록 막.** 물 밖 섬 · 하늘을 흐리게 가린다.
        ///
        /// 물결 무늬(가산)만 깔면 수면이 유리처럼 투명해 물 밖의 섬이 그대로 비쳐,
        /// 물속이 아니라 "파란 필터를 씌운 뭍" 처럼 보였다. 미리보기로 확인했다.
        /// 실제 물속에서 수면은 거울처럼 빛을 되돌려 바깥이 잘 안 보인다.
        /// </summary>
        private static Material BuildSurfaceTintMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                Debug.LogError("[물속] URP Unlit 셰이더가 없다.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(UnderwaterTintMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, UnderwaterTintMatPath);
            }

            mat.shader = shader;
            mat.SetTexture("_BaseMap", null);
            mat.SetColor("_BaseColor", new Color(0.4f, 0.88f, 0.78f, 0.78f));
            MakeTransparent(mat, additive: false);
            mat.renderQueue = (int)RenderQueue.Transparent;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>URP Unlit 을 투명으로 바꾼다. 인스펙터가 해 주는 설정을 손으로 똑같이 맞춘다.</summary>
        private static void MakeTransparent(Material mat, bool additive)
        {
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_Cull", (float)CullMode.Back);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)(additive ? BlendMode.One : BlendMode.OneMinusSrcAlpha));
            mat.SetFloat("_ZWrite", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        /// <summary>물속 색 보정. 초록빛 도는 파랑으로 물들이고 가장자리를 어둡게 한다.</summary>
        private static VolumeProfile BuildUnderwaterProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(UnderwaterProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, UnderwaterProfilePath);
            }

            // 다시 눌러도 쌓이지 않게 비우고 새로 단다. 효과는 프로필의 하위 에셋이라 같이 지운다.
            foreach (VolumeComponent old in profile.components.ToArray())
            {
                Object.DestroyImmediate(old, true);
            }

            profile.components.Clear();

            var color = profile.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            // 에메랄드빛으로 밝게. 처음에 노출 −0.2 · 파란 필터로 했더니 밤바다처럼 어두웠다.
            color.colorFilter.Override(new Color(0.85f, 1f, 0.93f));
            color.saturation.Override(10f);
            color.postExposure.Override(0.35f);

            // 가장자리도 검정이 아니라 짙은 에메랄드로 살짝만 누른다.
            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.color.Override(new Color(0.03f, 0.28f, 0.24f));
            vignette.intensity.Override(0.18f);
            vignette.smoothness.Override(0.6f);

            foreach (VolumeComponent c in profile.components)
            {
                c.hideFlags = HideFlags.HideInInspector | HideFlags.HideInHierarchy;
                AssetDatabase.AddObjectToAsset(c, profile);
            }

            EditorUtility.SetDirty(profile);
            return profile;
        }

        /// <summary>
        /// 로비 얕은 바다에 들어가 물속을 찍어 본다. 씬은 저장하지 않는다.
        /// <see cref="UnderTheSea.Lobby.LobbyUnderwaterView"/> 가 켜는 것과 같은 안개 · 볼륨 · 수면을 켜고 찍는다.
        /// </summary>
        private static void PreviewUnderwaterTo(string outDir)
        {
            Directory.CreateDirectory(outDir);

            if (SceneManager.GetActiveScene().path != LobbyScenePath)
            {
                EditorSceneManager.OpenScene(LobbyScenePath, OpenSceneMode.Single);
            }

            // 해안선 벽을 15m 바깥으로 옮긴 뒤로는 제대로 깊은 곳까지 간다. 스폰에서 가장 가까운 3.5~5.5m 바다에서 찍는다.
            Vector3? found = FindSeabedNear(new Vector3(20.84f, 0f, 48.56f), 3.5f, 5.5f, 80);
            if (!(found is Vector3 spot))
            {
                Debug.LogError("[물속 미리보기] 스폰 근처에서 3.5~5.5m 깊이의 바다를 못 찾았다.");
                return;
            }

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UnderwaterPrefabPath);
            var playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(SwimClipPath);
            if (prefab == null || playerPrefab == null || clip == null)
            {
                Debug.LogError("[물속 미리보기] 물속 프리팹 · 캐릭터 · 헤엄 클립 중 하나가 없다. 먼저 설치해라.");
                return;
            }

            var view = prefab.GetComponent<UnderTheSea.Lobby.LobbyUnderwaterView>();
            var so = new SerializedObject(view);
            var profile = (VolumeProfile)so.FindProperty("underwaterProfile").objectReferenceValue;
            var surfaceMat = (Material)so.FindProperty("surfaceMaterial").objectReferenceValue;
            Color fogColor = so.FindProperty("fogColor").colorValue;
            float fogDensity = so.FindProperty("fogDensity").floatValue;

            // 캐릭터를 물 한가운데에 헤엄 자세로 놓는다. 잠수한 채 앞으로 나아가는 모습이다.
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            Vector3 pivot = new Vector3(spot.x, spot.y * 0.5f, spot.z);

            // 스폰(뭍) 반대쪽, 즉 바다 쪽을 보게 한다.
            Vector3 toSea = new Vector3(spot.x - 20.84f, 0f, spot.z - 48.56f).normalized;
            player.transform.SetPositionAndRotation(pivot, Quaternion.LookRotation(toSea));

            Animator animator = player.GetComponentInChildren<Animator>();
            AnimationClipPlayable playable = AnimationPlayableUtilities.PlayClip(animator, clip, out PlayableGraph graph);
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var cameraGo = new GameObject("UnderwaterPreviewCamera");
            var camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.05f;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var rt = new RenderTexture(960, 540, 24);
            camera.targetTexture = rt;

            // ⚠ 배치 모드에서는 Evaluate 만으로는 자세가 뼈에 안 들어가고, 한 번 그린 뒤에야 들어간다
            //    (자세 미리보기의 첫 칸이 늘 T 자세였던 까닭). 그래서 한 번 헛그린 뒤 다시 돌린다.
            playable.SetTime(1f);
            graph.Evaluate();
            camera.Render();
            graph.Evaluate();

            GameObject baked = BakePose(player.GetComponentsInChildren<SkinnedMeshRenderer>()
                .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.sharedMesh != null).ToArray());

            var volumeGo = new GameObject("UnderwaterPreviewVolume");
            var volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 50f;
            volume.sharedProfile = profile;

            // LobbyUnderwaterView 와 같은 두 장(막 + 물결 무늬)을 깐다.
            var tintMat = (Material)so.FindProperty("surfaceTintMaterial").objectReferenceValue;
            var surface = new GameObject("UnderwaterSurface");
            surface.transform.position = new Vector3(pivot.x, -0.01f, pivot.z);

            foreach ((Material m, float y) in new[] { (tintMat, 0f), (new Material(surfaceMat) { mainTextureScale = Vector2.one * 40f }, -0.01f) })
            {
                var layer = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.DestroyImmediate(layer.GetComponent<Collider>());
                layer.transform.SetParent(surface.transform, false);
                layer.transform.localPosition = new Vector3(0f, y, 0f);
                layer.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
                layer.transform.localScale = Vector3.one * 30f;
                layer.GetComponent<MeshRenderer>().sharedMaterial = m;
            }

            bool fog = RenderSettings.fog;
            Color oldFogColor = RenderSettings.fogColor;
            FogMode oldFogMode = RenderSettings.fogMode;
            float oldFogDensity = RenderSettings.fogDensity;

            // 게임 카메라처럼 뒤에서 수면 바로 아래 높이로 본다. 하나는 물속 연출을 켜고, 하나는 끄고 찍어 비교한다.
            Vector3 look = pivot + Vector3.up * 0.3f;
            Vector3 back = -toSea;

            // 게임 카메라 위치와 비슷하게 — 뒤로 3m, 조금 위. 수면(−0.15)은 넘지 않는다.
            Vector3 behind = look + back * 3f + Vector3.up * 0.8f;
            behind.y = Mathf.Min(behind.y, -0.15f);

            // 안개 · 볼륨 · 수면 판을 따로 켜서도 찍는다. 어느 것이 화면을 망치는지 가르려고.
            var shots = new (string name, Vector3 eye, bool fogOn, bool volumeOn, bool surfaceOn)[]
            {
                ("underwater_behind", behind, true, true, true),
                ("underwater_behind_raw", behind, false, false, false),
                ("underwater_behind_fog", behind, true, false, false),
                ("underwater_behind_volume", behind, false, true, false),
                ("underwater_behind_surface", behind, false, false, true),
                ("underwater_lookup", new Vector3(look.x, look.y - 0.1f, look.z) + back * 1.2f, true, true, true),
            };

            foreach ((string name, Vector3 eye, bool fogOn, bool volumeOn, bool surfaceOn) in shots)
            {
                volume.weight = volumeOn ? 1f : 0f;
                surface.SetActive(surfaceOn);
                RenderSettings.fog = fogOn || fog;
                RenderSettings.fogMode = fogOn ? FogMode.ExponentialSquared : oldFogMode;
                RenderSettings.fogColor = fogOn ? fogColor : oldFogColor;
                RenderSettings.fogDensity = fogOn ? fogDensity : oldFogDensity;

                camera.transform.position = eye;
                camera.transform.LookAt(name == "underwater_lookup" ? look + toSea * 3f + Vector3.up * 2.5f : look + toSea * 1.5f);

                camera.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
                tex.Apply();
                RenderTexture.active = null;

                File.WriteAllBytes(Path.Combine(outDir, name + ".png"), tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }

            RenderSettings.fog = fog;
            RenderSettings.fogColor = oldFogColor;
            RenderSettings.fogMode = oldFogMode;
            RenderSettings.fogDensity = oldFogDensity;

            graph.Destroy();
            camera.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(baked);

            Debug.Log($"[물속 미리보기] {spot.ToString("F1")} (깊이 {-spot.y:F2}m) 에서 찍었다 → {outDir}");
        }

        // ─────────────────────────────────────────────── 배치 모드 진입점

        public static void InstallFromCommandLine()
        {
            Install();
        }

        /// <summary><c>-swimPreviewDir &lt;폴더&gt;</c> 로 저장할 곳을 준다.</summary>
        public static void PreviewFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-swimPreviewDir");
            string dir = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetTempPath(), "SwimPreview");

            PreviewTo(dir);
        }

        public static void ReportFromCommandLine()
        {
            Report();
        }

        public static void BuildUnderwaterFromCommandLine()
        {
            BuildUnderwater();
        }

        /// <summary><c>-swimPreviewDir &lt;폴더&gt;</c> 로 저장할 곳을 준다. 로비 씬을 열지만 저장하지 않는다.</summary>
        public static void PreviewUnderwaterFromCommandLine()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int at = System.Array.IndexOf(args, "-swimPreviewDir");
            string dir = at >= 0 && at + 1 < args.Length ? args[at + 1] : Path.Combine(Path.GetTempPath(), "SwimPreview");

            PreviewUnderwaterTo(dir);
        }
    }
}
