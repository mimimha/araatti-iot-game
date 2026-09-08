using System.Collections.Generic;
using System.Linq;
using FishingMiniGame.Runtime;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FishingMiniGame.Editor
{
    public static class FishingFreeArtSetup
    {
        private const string ExternalRoot = "Assets/FishingMiniGame/Art/External/QuaterniusPirateKit";
        private const string AnimatedFishRoot = "Assets/FishingMiniGame/Art/External/QuaterniusAnimatedFish";
        private const string PrefabRoot = "Assets/FishingMiniGame/Art/Prefabs";
        private const string MaterialRoot = "Assets/FishingMiniGame/Art/Materials";
        private const string ResourceRoot = "Assets/FishingMiniGame/Resources";
        private const string AtlasPath = ExternalRoot + "/Atlas_Pirate.png";
        private const string CharacterPath = ExternalRoot + "/Characters_Anne.fbx";
        private const string MackerelPath = AnimatedFishRoot + "/Fish2.fbx";
        private const string BreamPath = AnimatedFishRoot + "/Fish1.fbx";
        private const string TunaPath = ExternalRoot + "/Prop_Fish_Tuna.fbx";
        private const string VisualSetPath = ResourceRoot + "/FishingVisualSet.asset";

        [MenuItem("Tools/Fishing Mini Game/Apply Free CC0 Art")]
        public static void ApplyFreeArt()
        {
            EnsureFolder(PrefabRoot);
            EnsureFolder(MaterialRoot);
            EnsureFolder(ResourceRoot);
            ConfigureCharacterImporter(CharacterPath);
            ConfigureAnimatedModelImporter(MackerelPath);
            ConfigureAnimatedModelImporter(BreamPath);

            Material pirateMaterial = CreateOrUpdatePirateMaterial();
            Material oceanMaterial = CreateOrUpdateOceanMaterial();
            RuntimeAnimatorController idleController = CreateOrUpdateIdleController(CharacterPath);
            RuntimeAnimatorController mackerelController = CreateOrUpdateSwimController(MackerelPath, "FreeMackerel");
            RuntimeAnimatorController breamController = CreateOrUpdateSwimController(BreamPath, "FreeBream");

            GameObject anglerPrefab = CreateAnglerPrefab(pirateMaterial, idleController);
            GameObject mackerelPrefab = CreateFishPrefab("FreeMackerel", MackerelPath, 2.25f, null, mackerelController, Vector3.forward);
            GameObject breamPrefab = CreateFishPrefab("FreeBream", BreamPath, 2.05f, null, breamController, Vector3.forward);
            GameObject tunaPrefab = CreateFishPrefab("FreeTuna", TunaPath, 2.55f, pirateMaterial, null, Vector3.right);
            GameObject environmentPrefab = CreateEnvironmentPrefab(pirateMaterial);

            FishingPrefabPlacement anglerPlacement = new FishingPrefabPlacement();
            anglerPlacement.Configure(anglerPrefab, Vector3.zero, Vector3.zero, Vector3.one);
            FishingPrefabPlacement environmentPlacement = new FishingPrefabPlacement();
            environmentPlacement.Configure(environmentPrefab, Vector3.zero, Vector3.zero, Vector3.one);

            FishingFishVisualEntry[] fishEntries =
            {
                new FishingFishVisualEntry("blue_mackerel", mackerelPrefab, Vector3.zero, Vector3.zero, Vector3.one, Color.white),
                new FishingFishVisualEntry("red_sea_bream", breamPrefab, Vector3.zero, Vector3.zero, Vector3.one, Color.white),
                new FishingFishVisualEntry("greater_amberjack", tunaPrefab, Vector3.zero, Vector3.zero, new Vector3(1.12f, 0.92f, 1f), Color.white)
            };

            FishingVisualSet visualSet = AssetDatabase.LoadAssetAtPath<FishingVisualSet>(VisualSetPath);
            if (visualSet == null)
            {
                visualSet = ScriptableObject.CreateInstance<FishingVisualSet>();
                AssetDatabase.CreateAsset(visualSet, VisualSetPath);
            }
            visualSet.Configure("Quaternius CC0 Coastal Set", anglerPlacement, environmentPlacement, false, oceanMaterial, fishEntries);
            EditorUtility.SetDirty(visualSet);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Selection.activeObject = visualSet;
            Debug.Log("[Fishing] Free CC0 art configured with swappable prefabs and visual adapters.");
        }

        private static void ConfigureCharacterImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            // The Pirate Kit character uses its own compact skeleton and does not
            // contain every bone Unity requires for a Humanoid avatar (notably feet).
            // Generic keeps the bundled animation clips usable without producing a
            // broken-avatar warning, while the adapter remains animation-rig agnostic.
            bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
                           importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                           !importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            if (changed) importer.SaveAndReimport();
        }

        private static void ConfigureAnimatedModelImporter(string path)
        {
            ModelImporter importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) return;
            bool changed = importer.animationType != ModelImporterAnimationType.Generic ||
                           importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                           !importer.importAnimation;
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.importCameras = false;
            importer.importLights = false;
            if (changed) importer.SaveAndReimport();
        }

        private static Material CreateOrUpdatePirateMaterial()
        {
            const string path = MaterialRoot + "/QuaterniusPirateURP.mat";
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(AtlasPath);
            material.color = Color.white;
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", atlas);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", atlas);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.28f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateOrUpdateOceanMaterial()
        {
            const string path = MaterialRoot + "/StylizedOcean.mat";
            Shader shader = Shader.Find("FishingMiniGame/StylizedOcean");
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }
            material.SetColor("_BaseColor", new Color(0.025f, 0.48f, 0.69f, 1f));
            material.SetColor("_DeepColor", new Color(0.012f, 0.14f, 0.30f, 1f));
            material.SetColor("_FoamColor", new Color(0.76f, 0.97f, 1f, 1f));
            material.SetFloat("_Smoothness", 0.86f);
            material.SetFloat("_FoamStrength", 0.32f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static RuntimeAnimatorController CreateOrUpdateIdleController(string modelPath)
        {
            const string controllerPath = MaterialRoot + "/FreeAngler.controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AddParameterIfMissing(controller, "FishingState", AnimatorControllerParameterType.Int);
            AddParameterIfMissing(controller, "CastPower", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "FightIntensity", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "FightDirection", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "Reeling", AnimatorControllerParameterType.Bool);

            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__") &&
                                        string.Equals(clip.name, "Idle", System.StringComparison.OrdinalIgnoreCase));
            if (idle == null)
            {
                idle = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                    .OfType<AnimationClip>()
                    .FirstOrDefault(clip => !clip.name.StartsWith("__preview__") &&
                                            clip.name.ToLowerInvariant().Contains("idle"));
            }
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState idleState = stateMachine.states.Select(item => item.state).FirstOrDefault(state => state.name == "Idle");
            if (idleState == null) idleState = stateMachine.AddState("Idle");
            idleState.motion = idle;
            stateMachine.defaultState = idleState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static RuntimeAnimatorController CreateOrUpdateSwimController(string modelPath, string controllerName)
        {
            string controllerPath = MaterialRoot + "/" + controllerName + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            AddParameterIfMissing(controller, "SwimSpeed", AnimatorControllerParameterType.Float);
            AddParameterIfMissing(controller, "FightIntensity", AnimatorControllerParameterType.Float);

            AnimationClip swim = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__") &&
                                        clip.name.ToLowerInvariant().Contains("swim"));
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            AnimatorState swimState = stateMachine.states.Select(item => item.state).FirstOrDefault(state => state.name == "Swim");
            if (swimState == null) swimState = stateMachine.AddState("Swim");
            swimState.motion = swim;
            stateMachine.defaultState = swimState;
            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static void AddParameterIfMissing(AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(parameter => parameter.name == name)) return;
            controller.AddParameter(name, type);
        }

        private static GameObject CreateAnglerPrefab(Material material, RuntimeAnimatorController controller)
        {
            GameObject wrapper = new GameObject("Free CC0 Angler");
            GameObject model = AddNormalizedModel(wrapper.transform, CharacterPath, "Anne Fisher", 2.05f, true, Vector3.zero, Vector3.zero, material);
            Animator animator = model != null ? model.GetComponentInChildren<Animator>() : null;
            Transform rodGrip = null;
            if (model != null)
            {
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (!renderers[i].name.StartsWith("Weapon_", System.StringComparison.OrdinalIgnoreCase)) continue;
                    Transform weaponBone = renderers[i].transform.parent;
                    if (rodGrip == null && weaponBone != null)
                    {
                        rodGrip = new GameObject("RodGripAnchor").transform;
                        rodGrip.SetParent(weaponBone, false);
                        rodGrip.localPosition = Vector3.zero;
                        rodGrip.localRotation = Quaternion.identity;
                    }
                    renderers[i].gameObject.SetActive(false);
                }
            }
            if (animator == null && model != null) animator = model.AddComponent<Animator>();
            if (animator != null)
            {
                Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(CharacterPath).OfType<Avatar>().FirstOrDefault();
                animator.avatar = avatar;
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            AnglerVisualAdapter adapter = wrapper.AddComponent<AnglerVisualAdapter>();
            adapter.Configure(animator, rodGrip);
            string path = PrefabRoot + "/FreeAngler.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, path);
            Object.DestroyImmediate(wrapper);
            return prefab;
        }

        private static GameObject CreateFishPrefab(
            string name,
            string modelPath,
            float targetLength,
            Material material,
            RuntimeAnimatorController controller,
            Vector3 hookDirection)
        {
            GameObject wrapper = new GameObject(name);
            GameObject model = AddNormalizedModel(wrapper.transform, modelPath, name + " Model", targetLength, false, Vector3.zero, Vector3.zero, material);
            Animator animator = null;
            if (model != null && controller != null)
            {
                animator = model.GetComponentInChildren<Animator>();
                if (animator == null) animator = model.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
                animator.runtimeAnimatorController = controller;
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            }
            Transform hookAnchor = CreateFishHookAnchor(wrapper.transform, model, hookDirection);
            FishVisualAdapter adapter = wrapper.AddComponent<FishVisualAdapter>();
            adapter.Configure(animator, null, hookAnchor);
            string path = PrefabRoot + "/" + name + ".prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(wrapper, path);
            Object.DestroyImmediate(wrapper);
            return prefab;
        }

        private static Transform CreateFishHookAnchor(Transform wrapper, GameObject model, Vector3 direction)
        {
            Transform anchor = new GameObject("HookAnchor").transform;
            anchor.SetParent(wrapper, false);
            if (model == null) return anchor;

            Bounds bounds = CalculateBounds(model);
            Vector3 worldPosition = bounds.center;
            Vector3 absoluteDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            if (absoluteDirection.x >= absoluteDirection.y && absoluteDirection.x >= absoluteDirection.z)
            {
                worldPosition.x = direction.x >= 0f ? bounds.max.x : bounds.min.x;
            }
            else if (absoluteDirection.y >= absoluteDirection.z)
            {
                worldPosition.y = direction.y >= 0f ? bounds.max.y : bounds.min.y;
            }
            else
            {
                worldPosition.z = direction.z >= 0f ? bounds.max.z : bounds.min.z;
            }
            anchor.localPosition = wrapper.InverseTransformPoint(worldPosition);
            return anchor;
        }

        private static GameObject CreateEnvironmentPrefab(Material material)
        {
            GameObject root = new GameObject("Free Coastal Dressing");
            AddNormalizedModel(root.transform, ExternalRoot + "/Prop_Barrel.fbx", "Bait Barrel", 0.72f, true, new Vector3(-1.82f, 0.92f, -4.2f), new Vector3(0f, 25f, 0f), material);
            AddNormalizedModel(root.transform, ExternalRoot + "/Prop_Bucket.fbx", "Fish Bucket", 0.58f, true, new Vector3(1.78f, 0.92f, -2.5f), Vector3.zero, material);
            AddNormalizedModel(root.transform, ExternalRoot + "/Prop_Anchor.fbx", "Dock Anchor", 1.05f, true, new Vector3(-1.75f, 0.92f, -0.15f), new Vector3(0f, -28f, 0f), material);
            AddNormalizedModel(root.transform, ExternalRoot + "/Environment_Rock_1.fbx", "Near Sea Rock", 1.8f, true, new Vector3(6.6f, 0.20f, 11.2f), new Vector3(0f, 18f, 0f), material);
            AddNormalizedModel(root.transform, ExternalRoot + "/Environment_Rock_2.fbx", "Far Sea Rock", 2.7f, true, new Vector3(-8.2f, 0.10f, 15.8f), new Vector3(0f, -12f, 0f), material);
            string path = PrefabRoot + "/FreeCoastalEnvironment.prefab";
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static GameObject AddNormalizedModel(
            Transform parent,
            string assetPath,
            string objectName,
            float targetSize,
            bool alignToGround,
            Vector3 position,
            Vector3 eulerAngles,
            Material material)
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (source == null)
            {
                Debug.LogWarning("[Fishing] Missing free art source: " + assetPath);
                return null;
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null) instance = Object.Instantiate(source);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(eulerAngles);
            instance.transform.localScale = Vector3.one;
            AssignMaterial(instance, material);

            Bounds bounds = CalculateBounds(instance);
            float measuredSize = alignToGround
                ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z))
                : Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
            if (measuredSize > 0.0001f) instance.transform.localScale *= targetSize / measuredSize;
            bounds = CalculateBounds(instance);
            Vector3 offset = alignToGround
                ? new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z)
                : -bounds.center;
            instance.transform.position += offset;
            instance.transform.localPosition += position;
            return instance;
        }

        private static Bounds CalculateBounds(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.one);
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void AssignMaterial(GameObject instance, Material material)
        {
            if (material == null) return;
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Material[] materials = renderers[i].sharedMaterials;
                for (int materialIndex = 0; materialIndex < materials.Length; materialIndex++) materials[materialIndex] = material;
                renderers[i].sharedMaterials = materials;
                renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderers[i].receiveShadows = true;
            }
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
