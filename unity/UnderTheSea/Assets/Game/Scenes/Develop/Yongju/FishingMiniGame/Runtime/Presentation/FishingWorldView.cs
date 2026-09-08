using System.Collections.Generic;
using FishingMiniGame.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace FishingMiniGame.Runtime
{
    [DisallowMultipleComponent]
    public sealed class FishingWorldView : MonoBehaviour
    {
        [SerializeField] private FishingGameController controller;
        [SerializeField] private FishingVisualSet visualSet;
        [SerializeField] private FishingV2PresentationTuning v2PresentationTuning = new FishingV2PresentationTuning();

        private readonly List<LineRenderer> _waveCrests = new List<LineRenderer>();
        private readonly List<Transform> _seabirds = new List<Transform>();
        private Transform _worldRoot;
        private Transform _angler;
        private Transform _rodGripAnchor;
        private Transform _rodPosePivot;
        private Transform _rodReactionPivot;
        private Transform _rodTip;
        private Transform _rod;
        private Transform _rodMiddle;
        private Transform _rodUpper;
        private Transform _bobber;
        private Transform _fish;
        private Transform _fishBody;
        private Transform _fishHead;
        private Transform _fishTail;
        private Transform _pectoralFin;
        private Transform _reel;
        private GameObject _externalAnglerModel;
        private GameObject _externalFishModel;
        private Renderer[] _fallbackAnglerRenderers;
        private Renderer[] _externalAnglerRenderers;
        private Renderer[] _fallbackFishRenderers;
        private AnglerVisualAdapter _anglerVisualAdapter;
        private FishVisualAdapter _fishVisualAdapter;
        private GameObject _mackerelMarkings;
        private GameObject _breamMarking;
        private GameObject _amberjackStripe;
        private LineRenderer _line;
        private LineRenderer _ripple;
        private Mesh _oceanMesh;
        private Vector3[] _oceanBaseVertices;
        private Vector3[] _oceanVertices;
        private Material _oceanMaterial;
        private Material _fishBodyMaterial;
        private Material _fishFinMaterial;
        private Material _fishBellyMaterial;
        private string _lastFishId;
        private float _nextNormalRefresh;
        private bool _lastRodFocusedMode;
        private bool _presentationModeInitialized;
        private FishingV2PresentationFeedback _v2Feedback;
        private FishingV2VisualFrame _v2Visual;
        private Camera _sceneCamera;
        private Vector3 _baseCameraPosition;
        private Quaternion _baseCameraRotation;
        private readonly Vector3 _waterTarget = new Vector3(1.35f, 0.44f, 5.3f);
        private readonly Vector3 _neutralRodTipOffset = new Vector3(0f, 0.55f, 3.15f);

        public FishingV2VisualFrame CurrentV2Visual => _v2Visual;
        public Vector3 BaseCameraPosition => _baseCameraPosition;
        public Quaternion BaseCameraRotation => _baseCameraRotation;

        public void ConfigureVisuals(FishingVisualSet value)
        {
            visualSet = value;
        }

        private void Awake()
        {
            if (controller == null) controller = GetComponent<FishingGameController>();
            if (visualSet == null) visualSet = Resources.Load<FishingVisualSet>("FishingVisualSet");
            _v2Feedback = new FishingV2PresentationFeedback(v2PresentationTuning);
            BuildCoastalWorld();
        }

        private void Update()
        {
            AnimateOcean();
            AnimateScenery();
            if (controller == null || controller.Snapshot == null || _bobber == null) return;

            FishingSnapshot snapshot = controller.Snapshot;
            UpdatePresentationMode();
            bool useV2Presentation = controller.Mode == FishingGameMode.SingleFishSession;
            if (useV2Presentation)
            {
                if (_v2Feedback == null)
                    _v2Feedback = new FishingV2PresentationFeedback(v2PresentationTuning);
                _v2Visual = _v2Feedback.Tick(
                    FishingV2PresentationInput.FromSnapshot(snapshot),
                    Time.unscaledDeltaTime,
                    controller.IsPaused);
            }
            else
            {
                _v2Visual = default;
            }
            ApplyFishVisual(snapshot);
            UpdateAnglerAndRod(snapshot);

            Vector3 rodPosition = _rodTip.position;
            Vector3 bobberPosition = rodPosition;
            float visualPhase = useV2Presentation ? _v2Visual.AnimationPhaseSeconds : Time.time;
            switch (snapshot.State)
            {
                case FishingPlayerState.Casting:
                    bobberPosition = Vector3.Lerp(rodPosition, _waterTarget, EaseOut(snapshot.CastPower));
                    bobberPosition.y += Mathf.Sin(snapshot.CastPower * Mathf.PI) * 2.3f;
                    break;
                case FishingPlayerState.Waiting:
                case FishingPlayerState.Hooked:
                    if (useV2Presentation)
                    {
                        float transientJitter = Mathf.Sin(visualPhase * 18f) * _v2Visual.LineJitterNormalized;
                        bobberPosition = _waterTarget + new Vector3(
                            _v2Visual.LateralPullNormalized * 0.18f + transientJitter * 0.07f,
                            Mathf.Sin(Time.time * 2.2f) * 0.04f - _v2Visual.BobberDipMeters,
                            transientJitter * 0.035f);
                    }
                    else
                    {
                        float nibbleKick = snapshot.IsNibbling ? Mathf.Sin(Time.time * 20f) * 0.11f - 0.04f : 0f;
                        bobberPosition = _waterTarget + new Vector3(
                            snapshot.IsNibbling ? Mathf.Sin(Time.time * 15f) * 0.06f : 0f,
                            Mathf.Sin(Time.time * 2.2f) * 0.05f + nibbleKick,
                            0f);
                    }
                    break;
                case FishingPlayerState.BiteWindow:
                    if (useV2Presentation)
                    {
                        float biteJerk = Mathf.Sin(visualPhase * 22f) * _v2Visual.BitePulseNormalized;
                        bobberPosition = _waterTarget + new Vector3(
                            biteJerk * 0.13f,
                            -0.10f - _v2Visual.BobberDipMeters,
                            Mathf.Cos(visualPhase * 19f) * _v2Visual.BitePulseNormalized * 0.09f);
                    }
                    else
                    {
                        bobberPosition = _waterTarget + new Vector3(
                            Mathf.Sin(Time.time * 13f) * 0.08f,
                            Mathf.Sin(Time.time * 18f) * 0.16f - 0.05f,
                            Mathf.Cos(Time.time * 12f) * 0.06f);
                    }
                    break;
                case FishingPlayerState.Fighting:
                    if (useV2Presentation)
                    {
                        float movement = _v2Visual.FishMotionNormalized;
                        bobberPosition = _waterTarget + new Vector3(
                            _v2Visual.LateralPullNormalized * 1.22f + Mathf.Sin(visualPhase * 4.7f) * movement * 0.18f,
                            -0.06f - _v2Visual.RodLoadNormalized * 0.07f,
                            Mathf.Cos(visualPhase * 3.1f) * (0.10f + movement * 0.24f));
                    }
                    else
                    {
                        bobberPosition = _waterTarget + new Vector3(
                            snapshot.FightDirection * (0.52f + snapshot.Feedback.Intensity * 0.78f) + Mathf.Sin(Time.time * 4.2f) * 0.22f,
                            -0.08f,
                            Mathf.Cos(Time.time * 2.5f) * 0.30f);
                    }
                    break;
                case FishingPlayerState.Caught:
                    bobberPosition = Vector3.Lerp(_waterTarget, rodPosition + Vector3.down * 0.7f,
                        Mathf.Clamp01(snapshot.StateElapsedSeconds * 0.9f));
                    break;
            }

            _bobber.position = bobberPosition;
            ApplyLineGeometry(
                _line,
                rodPosition,
                bobberPosition,
                useV2Presentation ? _v2Visual.LineSagMeters : 0f,
                useV2Presentation ? _v2Visual.LineJitterNormalized * 0.035f : 0f);

            bool onWater = snapshot.State == FishingPlayerState.Waiting ||
                snapshot.State == FishingPlayerState.BiteWindow ||
                snapshot.State == FishingPlayerState.Hooked ||
                snapshot.State == FishingPlayerState.Fighting;
            _ripple.gameObject.SetActive(onWater);
            if (onWater)
            {
                _ripple.transform.position = new Vector3(bobberPosition.x, WaveHeight(bobberPosition.x, bobberPosition.z, Time.time) + 0.035f, bobberPosition.z);
                float disturbance = useV2Presentation ? _v2Visual.SurfaceDisturbanceNormalized : controller.LastFeedback.Intensity;
                float speed = useV2Presentation
                    ? Mathf.Lerp(1.2f, 7.2f, disturbance)
                    : snapshot.State == FishingPlayerState.BiteWindow ? 5.5f :
                        snapshot.Feedback.State == FishingFeedbackState.Run ? 4.2f : 1.35f;
                float pulse = 0.58f + Mathf.Repeat(visualPhase * speed, 1f) * (0.58f + disturbance * 0.88f);
                _ripple.transform.localScale = new Vector3(
                    pulse * (1f + Mathf.Abs(useV2Presentation ? _v2Visual.LateralPullNormalized : 0f) * 0.32f),
                    1f,
                    pulse);
                Color rippleColor = snapshot.State == FishingPlayerState.BiteWindow
                    ? new Color(1f, 0.78f, 0.23f, 0.95f)
                    : Color.Lerp(new Color(0.82f, 0.98f, 1f, 0.58f), new Color(0.30f, 0.92f, 1f, 0.92f), disturbance);
                _ripple.startColor = rippleColor;
                _ripple.endColor = new Color(rippleColor.r, rippleColor.g, rippleColor.b, 0f);
            }

            bool showFish = snapshot.State == FishingPlayerState.Fighting || snapshot.State == FishingPlayerState.Caught;
            _fish.gameObject.SetActive(showFish);
            if (showFish)
            {
                float resistance = useV2Presentation
                    ? 0.35f + _v2Visual.FishMotionNormalized
                    : 0.55f + controller.LastFeedback.Intensity;
                float direction = useV2Presentation ? snapshot.V2FishDirectionNormalized : snapshot.FightDirection;
                bool isCaught = snapshot.State == FishingPlayerState.Caught;
                Vector3 fishOffset = isCaught
                    ? new Vector3(-0.72f, -0.52f, 0f)
                    : new Vector3(
                        direction * (0.28f + resistance * 0.35f) + Mathf.Sin(visualPhase * (3f + resistance)) * resistance * 0.35f,
                        -0.66f,
                        0.35f + Mathf.Cos(visualPhase * 2.1f) * 0.16f);
                _fish.position = bobberPosition + fishOffset;
                _fish.rotation = Quaternion.Euler(
                    Mathf.Sin(visualPhase * 4.4f) * 9f,
                    92f + direction * 22f + Mathf.Sin(visualPhase * 2.8f) * (18f + resistance * 10f),
                    isCaught ? -24f : -7f);
                if (isCaught && _fishVisualAdapter != null && _fishVisualAdapter.HasHookAnchor)
                {
                    Vector3 desiredHookPosition = bobberPosition + Vector3.down * 0.08f;
                    _fish.position += desiredHookPosition - _fishVisualAdapter.HookAnchorPosition;
                }
                float tailSwing = Mathf.Sin(visualPhase * (9f + resistance * 5f)) * (24f + resistance * 11f);
                if (_fishVisualAdapter != null)
                {
                    _fishVisualAdapter.Apply(snapshot, resistance, visualPhase, controller.IsPaused,
                        useV2Presentation ? Mathf.Abs(_v2Visual.LineJitterNormalized) : 0f);
                }
                else
                {
                    if (_fishTail != null) _fishTail.localRotation = Quaternion.Euler(0f, tailSwing, 0f);
                    if (_pectoralFin != null) _pectoralFin.localRotation = Quaternion.Euler(0f, 0f, -26f + Mathf.Sin(visualPhase * 7f) * 16f);
                }
            }

            ApplyCameraFeedback(useV2Presentation);
        }

        private void BuildCoastalWorld()
        {
            _worldRoot = new GameObject("CoastalFishingWorld").transform;
            _worldRoot.SetParent(transform, false);
            ConfigureEnvironment();
            BuildOcean();
            if (visualSet == null || !visualSet.ReplaceDefaultEnvironment) BuildBreakwater();
            BuildExternalEnvironment();
            BuildAnglerAndRod();
            BuildFishingObjects();
            BuildCoastalScenery();
        }

        private void ConfigureEnvironment()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.53f, 0.73f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.31f, 0.52f, 0.62f);
            RenderSettings.ambientGroundColor = new Color(0.15f, 0.20f, 0.23f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.48f, 0.70f, 0.80f);
            RenderSettings.fogStartDistance = 24f;
            RenderSettings.fogEndDistance = 68f;

            Shader skyShader = Shader.Find("Skybox/Procedural");
            if (skyShader != null)
            {
                Material sky = new Material(skyShader);
                if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.30f, 0.60f, 0.84f));
                if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.28f, 0.52f, 0.63f));
                if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.85f);
                if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.18f);
                RenderSettings.skybox = sky;
            }

            _sceneCamera = Camera.main;
            if (_sceneCamera == null)
            {
                GameObject cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                _sceneCamera = cameraObject.AddComponent<Camera>();
            }
            _sceneCamera.transform.position = new Vector3(-1.25f, 2.65f, -2.25f);
            _sceneCamera.transform.rotation = Quaternion.LookRotation(
                new Vector3(1.20f, 1.25f, 6.0f) - _sceneCamera.transform.position,
                Vector3.up);
            _sceneCamera.fieldOfView = 60f;
            _sceneCamera.nearClipPlane = 0.05f;
            _sceneCamera.farClipPlane = 110f;
            _sceneCamera.clearFlags = CameraClearFlags.Skybox;
            _baseCameraPosition = _sceneCamera.transform.position;
            _baseCameraRotation = _sceneCamera.transform.rotation;

            Light sun = FindAnyObjectByType<Light>();
            if (sun == null)
            {
                GameObject lightObject = new GameObject("Coastal Sun");
                sun = lightObject.AddComponent<Light>();
            }
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.91f, 0.76f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.68f;
            sun.transform.rotation = Quaternion.Euler(46f, -28f, 0f);
            RenderSettings.sun = sun;
        }

        private void BuildOcean()
        {
            const int columns = 44;
            const int rows = 34;
            const float width = 100f;
            const float depth = 72f;
            _oceanBaseVertices = new Vector3[(columns + 1) * (rows + 1)];
            _oceanVertices = new Vector3[_oceanBaseVertices.Length];
            Vector2[] uv = new Vector2[_oceanBaseVertices.Length];
            int[] triangles = new int[columns * rows * 6];

            for (int z = 0; z <= rows; z++)
            {
                for (int x = 0; x <= columns; x++)
                {
                    int index = z * (columns + 1) + x;
                    float px = -width * 0.5f + width * x / columns;
                    float pz = -0.5f + depth * z / rows;
                    _oceanBaseVertices[index] = new Vector3(px, 0.30f, pz);
                    _oceanVertices[index] = _oceanBaseVertices[index];
                    uv[index] = new Vector2((float)x / columns * 5f, (float)z / rows * 5f);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < rows; z++)
            {
                for (int x = 0; x < columns; x++)
                {
                    int current = z * (columns + 1) + x;
                    triangles[triangleIndex++] = current;
                    triangles[triangleIndex++] = current + columns + 1;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = current + 1;
                    triangles[triangleIndex++] = current + columns + 1;
                    triangles[triangleIndex++] = current + columns + 2;
                }
            }

            _oceanMesh = new Mesh { name = "AnimatedCoastalOcean" };
            _oceanMesh.vertices = _oceanVertices;
            _oceanMesh.uv = uv;
            _oceanMesh.triangles = triangles;
            _oceanMesh.RecalculateNormals();
            _oceanMesh.RecalculateBounds();

            GameObject ocean = new GameObject("Animated Ocean");
            ocean.transform.SetParent(_worldRoot, false);
            ocean.AddComponent<MeshFilter>().sharedMesh = _oceanMesh;
            MeshRenderer renderer = ocean.AddComponent<MeshRenderer>();
            _oceanMaterial = visualSet != null && visualSet.OceanMaterial != null
                ? new Material(visualSet.OceanMaterial)
                : CreateMaterial(new Color(0.035f, 0.40f, 0.63f), 0.82f, 0.18f);
            renderer.sharedMaterial = _oceanMaterial;

            Material crestMaterial = CreateUnlitMaterial(new Color(0.80f, 0.97f, 1f, 0.40f));
            for (int i = 0; i < 6; i++)
            {
                GameObject crestObject = new GameObject($"Wave Crest {i + 1}");
                crestObject.transform.SetParent(_worldRoot, false);
                LineRenderer crest = crestObject.AddComponent<LineRenderer>();
                crest.useWorldSpace = false;
                crest.positionCount = 22;
                crest.startWidth = 0.045f;
                crest.endWidth = 0.012f;
                crest.numCornerVertices = 2;
                crest.material = crestMaterial;
                crest.startColor = new Color(0.90f, 1f, 1f, 0.30f - i * 0.018f);
                crest.endColor = new Color(0.75f, 0.96f, 1f, 0f);
                float x = -9.5f + (i * 5.7f) % 18f;
                float z = 4.2f + i * 4.1f;
                crest.transform.position = new Vector3(x, 0f, z);
                crest.transform.localScale = new Vector3(0.72f + (i % 3) * 0.22f, 1f, 1f);
                _waveCrests.Add(crest);
            }
        }

        private bool BuildExternalEnvironment()
        {
            FishingPrefabPlacement placement = visualSet != null ? visualSet.Environment : null;
            if (placement == null || placement.Prefab == null) return false;

            GameObject instance = Instantiate(placement.Prefab, _worldRoot);
            instance.name = "Swappable Coastal Environment";
            ApplyPlacement(instance.transform, placement.LocalPosition, placement.LocalEulerAngles, placement.LocalScale);
            return true;
        }

        private void BuildBreakwater()
        {
            Transform pier = new GameObject("Coastal Breakwater").transform;
            pier.SetParent(_worldRoot, false);
            Material concrete = CreateMaterial(new Color(0.31f, 0.38f, 0.41f), 0.16f);
            Material concreteEdge = CreateMaterial(new Color(0.52f, 0.58f, 0.59f), 0.20f);
            Material wetConcrete = CreateMaterial(new Color(0.18f, 0.28f, 0.31f), 0.45f);
            Material wood = CreateMaterial(new Color(0.38f, 0.23f, 0.14f), 0.23f);
            Material woodLight = CreateMaterial(new Color(0.50f, 0.33f, 0.20f), 0.20f);
            Material metal = CreateMaterial(new Color(0.09f, 0.15f, 0.17f), 0.58f, 0.72f);
            Material rope = CreateMaterial(new Color(0.71f, 0.59f, 0.38f), 0.14f);

            CreatePrimitive("Concrete Foundation", PrimitiveType.Cube, pier, new Vector3(0f, 0.18f, -2.0f), new Vector3(5.8f, 1.05f, 8.5f), concrete);
            CreatePrimitive("Wet Tide Line", PrimitiveType.Cube, pier, new Vector3(0f, 0.43f, 2.02f), new Vector3(5.85f, 0.28f, 0.30f), wetConcrete);
            CreatePrimitive("Safety Edge Left", PrimitiveType.Cube, pier, new Vector3(-2.76f, 0.86f, -2.0f), new Vector3(0.28f, 0.34f, 8.4f), concreteEdge);
            CreatePrimitive("Safety Edge Right", PrimitiveType.Cube, pier, new Vector3(2.76f, 0.86f, -2.0f), new Vector3(0.28f, 0.34f, 8.4f), concreteEdge);

            for (int i = 0; i < 12; i++)
            {
                float z = -5.55f + i * 0.62f;
                CreatePrimitive($"Deck Slat {i + 1:00}", PrimitiveType.Cube, pier, new Vector3(0f, 0.79f, z),
                    new Vector3(4.8f, 0.12f, 0.54f), i % 2 == 0 ? wood : woodLight);
            }

            BuildBollard(pier, new Vector3(-2.05f, 1.12f, 0.95f), metal);
            BuildBollard(pier, new Vector3(2.05f, 1.12f, -3.75f), metal);
            BuildRopeCoil(pier, new Vector3(-1.78f, 0.94f, -3.15f), rope);

            Transform tackle = new GameObject("Tackle Box").transform;
            tackle.SetParent(pier, false);
            Material tackleBlue = CreateMaterial(new Color(0.06f, 0.28f, 0.40f), 0.32f);
            Material tackleOrange = CreateMaterial(new Color(0.94f, 0.44f, 0.14f), 0.28f);
            CreatePrimitive("Box", PrimitiveType.Cube, tackle, new Vector3(1.72f, 1.14f, -2.7f), new Vector3(0.78f, 0.48f, 0.50f), tackleBlue);
            CreatePrimitive("Lid", PrimitiveType.Cube, tackle, new Vector3(1.72f, 1.41f, -2.7f), new Vector3(0.84f, 0.10f, 0.56f), tackleOrange);
            CreateCylinderBetween("Handle", tackle, new Vector3(1.48f, 1.62f, -2.7f), new Vector3(1.96f, 1.62f, -2.7f), 0.035f, metal);

            Material bucketMaterial = CreateMaterial(new Color(0.80f, 0.86f, 0.84f), 0.48f, 0.35f);
            CreatePrimitive("Bait Bucket", PrimitiveType.Cylinder, pier, new Vector3(-1.76f, 1.13f, -1.35f), new Vector3(0.32f, 0.36f, 0.32f), bucketMaterial);
        }

        private void BuildAnglerAndRod()
        {
            _angler = new GameObject("Coastal Angler").transform;
            _angler.SetParent(_worldRoot, false);
            _angler.localPosition = new Vector3(-0.42f, 0.84f, -0.8f);

            Material boots = CreateMaterial(new Color(0.055f, 0.075f, 0.08f), 0.35f);
            Material pants = CreateMaterial(new Color(0.06f, 0.16f, 0.23f), 0.25f);
            Material shirt = CreateMaterial(new Color(0.14f, 0.43f, 0.55f), 0.25f);
            Material vest = CreateMaterial(new Color(0.94f, 0.38f, 0.12f), 0.26f);
            Material vestDark = CreateMaterial(new Color(0.34f, 0.13f, 0.08f), 0.22f);
            Material skin = CreateMaterial(new Color(0.86f, 0.61f, 0.43f), 0.34f);
            Material hat = CreateMaterial(new Color(0.08f, 0.27f, 0.28f), 0.24f);
            Material hair = CreateMaterial(new Color(0.11f, 0.065f, 0.035f), 0.18f);
            Material rodMaterial = CreateMaterial(new Color(0.055f, 0.07f, 0.065f), 0.58f, 0.8f);

            CreatePrimitive("Left Boot", PrimitiveType.Cube, _angler, new Vector3(-0.20f, 0.23f, 0.08f), new Vector3(0.25f, 0.22f, 0.52f), boots);
            CreatePrimitive("Right Boot", PrimitiveType.Cube, _angler, new Vector3(0.20f, 0.23f, 0.08f), new Vector3(0.25f, 0.22f, 0.52f), boots);
            CreatePrimitive("Left Leg", PrimitiveType.Capsule, _angler, new Vector3(-0.20f, 0.67f, 0f), new Vector3(0.25f, 0.46f, 0.25f), pants);
            CreatePrimitive("Right Leg", PrimitiveType.Capsule, _angler, new Vector3(0.20f, 0.67f, 0f), new Vector3(0.25f, 0.46f, 0.25f), pants);
            CreatePrimitive("Torso", PrimitiveType.Capsule, _angler, new Vector3(0f, 1.35f, 0f), new Vector3(0.50f, 0.66f, 0.39f), shirt);
            CreatePrimitive("Life Vest Back", PrimitiveType.Capsule, _angler, new Vector3(0f, 1.42f, -0.13f), new Vector3(0.46f, 0.56f, 0.34f), vest);
            CreatePrimitive("Life Vest Left", PrimitiveType.Cube, _angler, new Vector3(-0.25f, 1.42f, 0.25f), new Vector3(0.26f, 0.72f, 0.17f), vest);
            CreatePrimitive("Life Vest Right", PrimitiveType.Cube, _angler, new Vector3(0.25f, 1.42f, 0.25f), new Vector3(0.26f, 0.72f, 0.17f), vest);
            CreatePrimitive("Back Reflective Strip", PrimitiveType.Cube, _angler, new Vector3(0f, 1.43f, -0.45f), new Vector3(0.36f, 0.075f, 0.025f), CreateMaterial(new Color(0.98f, 0.78f, 0.29f), 0.36f));
            CreatePrimitive("Vest Buckle Top", PrimitiveType.Cube, _angler, new Vector3(0f, 1.58f, 0.36f), new Vector3(0.35f, 0.08f, 0.055f), vestDark);
            CreatePrimitive("Vest Buckle Bottom", PrimitiveType.Cube, _angler, new Vector3(0f, 1.30f, 0.36f), new Vector3(0.35f, 0.08f, 0.055f), vestDark);
            CreatePrimitive("Neck", PrimitiveType.Cylinder, _angler, new Vector3(0f, 1.94f, 0f), new Vector3(0.16f, 0.16f, 0.16f), skin);
            CreatePrimitive("Head", PrimitiveType.Sphere, _angler, new Vector3(0f, 2.20f, 0.02f), new Vector3(0.46f, 0.53f, 0.46f), skin);
            CreatePrimitive("Left Ear", PrimitiveType.Sphere, _angler, new Vector3(-0.43f, 2.20f, 0.03f), new Vector3(0.10f, 0.15f, 0.10f), skin);
            CreatePrimitive("Right Ear", PrimitiveType.Sphere, _angler, new Vector3(0.43f, 2.20f, 0.03f), new Vector3(0.10f, 0.15f, 0.10f), skin);
            CreatePrimitive("Nose", PrimitiveType.Sphere, _angler, new Vector3(0f, 2.18f, 0.45f), new Vector3(0.10f, 0.12f, 0.10f), skin);
            Material faceDetail = CreateMaterial(new Color(0.025f, 0.035f, 0.035f), 0.48f);
            CreatePrimitive("Left Eye", PrimitiveType.Sphere, _angler, new Vector3(-0.15f, 2.29f, 0.43f), Vector3.one * 0.055f, faceDetail);
            CreatePrimitive("Right Eye", PrimitiveType.Sphere, _angler, new Vector3(0.15f, 2.29f, 0.43f), Vector3.one * 0.055f, faceDetail);
            CreatePrimitive("Hair", PrimitiveType.Sphere, _angler, new Vector3(0f, 2.33f, -0.07f), new Vector3(0.45f, 0.34f, 0.42f), hair);
            CreatePrimitive("Cap Crown", PrimitiveType.Cylinder, _angler, new Vector3(0f, 2.52f, 0f), new Vector3(0.36f, 0.12f, 0.36f), hat);
            GameObject capBrim = CreatePrimitive("Cap Brim", PrimitiveType.Cube, _angler, new Vector3(0f, 2.45f, 0.32f), new Vector3(0.52f, 0.055f, 0.36f), hat);
            capBrim.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            Vector3 leftShoulder = _angler.TransformPoint(new Vector3(-0.36f, 1.67f, 0f));
            Vector3 rightShoulder = _angler.TransformPoint(new Vector3(0.36f, 1.67f, 0f));
            Vector3 leftHand = _angler.TransformPoint(new Vector3(-0.10f, 1.39f, 0.67f));
            Vector3 rightHand = _angler.TransformPoint(new Vector3(0.28f, 1.52f, 0.84f));
            CreateCylinderBetween("Left Sleeve", _angler, leftShoulder, leftHand, 0.145f, shirt);
            CreateCylinderBetween("Right Sleeve", _angler, rightShoulder, rightHand, 0.145f, shirt);
            CreatePrimitive("Left Hand", PrimitiveType.Sphere, _angler, _angler.InverseTransformPoint(leftHand), Vector3.one * 0.21f, skin);
            CreatePrimitive("Right Hand", PrimitiveType.Sphere, _angler, _angler.InverseTransformPoint(rightHand), Vector3.one * 0.21f, skin);

            _fallbackAnglerRenderers = _angler.GetComponentsInChildren<Renderer>(true);
            BuildExternalAngler();

            _rodGripAnchor = new GameObject("RodGripAnchor").transform;
            _rodGripAnchor.SetParent(_worldRoot, false);
            _rodPosePivot = new GameObject("RodPosePivot").transform;
            _rodPosePivot.SetParent(_rodGripAnchor, false);
            _rodReactionPivot = new GameObject("RodReactionPivot").transform;
            _rodReactionPivot.SetParent(_rodPosePivot, false);

            GameObject rodObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            rodObject.name = "Fishing Rod";
            rodObject.transform.SetParent(_rodReactionPivot, false);
            Collider rodCollider = rodObject.GetComponent<Collider>();
            if (rodCollider != null) rodCollider.enabled = false;
            rodObject.GetComponent<Renderer>().sharedMaterial = rodMaterial;
            _rod = rodObject.transform;

            GameObject middleRodObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            middleRodObject.name = "Fishing Rod Middle";
            middleRodObject.transform.SetParent(_rodReactionPivot, false);
            Collider middleCollider = middleRodObject.GetComponent<Collider>();
            if (middleCollider != null) middleCollider.enabled = false;
            middleRodObject.GetComponent<Renderer>().sharedMaterial = rodMaterial;
            _rodMiddle = middleRodObject.transform;

            GameObject upperRodObject = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            upperRodObject.name = "Fishing Rod Upper";
            upperRodObject.transform.SetParent(_rodReactionPivot, false);
            Collider upperCollider = upperRodObject.GetComponent<Collider>();
            if (upperCollider != null) upperCollider.enabled = false;
            upperRodObject.GetComponent<Renderer>().sharedMaterial = rodMaterial;
            _rodUpper = upperRodObject.transform;

            GameObject tip = new GameObject("Rod Tip");
            tip.transform.SetParent(_rodReactionPivot, false);
            tip.transform.localPosition = _neutralRodTipOffset;
            _rodTip = tip.transform;

            Material reelMaterial = CreateMaterial(new Color(0.82f, 0.85f, 0.83f), 0.64f, 0.8f);
            GameObject reel = CreatePrimitive("Spinning Reel", PrimitiveType.Cylinder, _rodReactionPivot, new Vector3(0f, -0.11f, 0.14f), new Vector3(0.15f, 0.10f, 0.15f), reelMaterial);
            reel.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            _reel = reel.transform;
        }

        private void BuildExternalAngler()
        {
            FishingPrefabPlacement placement = visualSet != null ? visualSet.Angler : null;
            if (placement == null || placement.Prefab == null) return;

            SetRenderersEnabled(_fallbackAnglerRenderers, false);
            _externalAnglerModel = Instantiate(placement.Prefab, _angler);
            _externalAnglerModel.name = "Swappable Angler Model";
            ApplyPlacement(_externalAnglerModel.transform, placement.LocalPosition, placement.LocalEulerAngles, placement.LocalScale);
            _anglerVisualAdapter = _externalAnglerModel.GetComponent<AnglerVisualAdapter>();
            if (_anglerVisualAdapter == null) _anglerVisualAdapter = _externalAnglerModel.AddComponent<AnglerVisualAdapter>();
            _anglerVisualAdapter.AutoBind();
            _externalAnglerRenderers = _externalAnglerModel.GetComponentsInChildren<Renderer>(true);
        }

        private void BuildFishingObjects()
        {
            Material bobberRed = CreateMaterial(new Color(0.96f, 0.18f, 0.10f), 0.45f);
            Material bobberWhite = CreateMaterial(new Color(0.98f, 0.97f, 0.86f), 0.40f);
            GameObject bobberRoot = new GameObject("Ocean Bobber");
            bobberRoot.transform.SetParent(_worldRoot, false);
            CreatePrimitive("Bobber Top", PrimitiveType.Sphere, bobberRoot.transform, new Vector3(0f, 0.09f, 0f), new Vector3(0.17f, 0.24f, 0.17f), bobberRed);
            CreatePrimitive("Bobber Bottom", PrimitiveType.Sphere, bobberRoot.transform, new Vector3(0f, -0.08f, 0f), new Vector3(0.17f, 0.20f, 0.17f), bobberWhite);
            _bobber = bobberRoot.transform;

            GameObject lineObject = new GameObject("Fishing Line");
            lineObject.transform.SetParent(_worldRoot, false);
            _line = lineObject.AddComponent<LineRenderer>();
            _line.positionCount = 3;
            _line.startWidth = 0.014f;
            _line.endWidth = 0.007f;
            _line.numCornerVertices = 3;
            _line.useWorldSpace = true;
            _line.material = CreateUnlitMaterial(new Color(0.90f, 0.97f, 1f, 0.85f));

            GameObject rippleObject = new GameObject("Water Ripple");
            rippleObject.transform.SetParent(_worldRoot, false);
            _ripple = rippleObject.AddComponent<LineRenderer>();
            _ripple.useWorldSpace = false;
            _ripple.loop = true;
            _ripple.positionCount = 48;
            _ripple.startWidth = 0.035f;
            _ripple.endWidth = 0.015f;
            _ripple.material = CreateUnlitMaterial(Color.white);
            for (int i = 0; i < _ripple.positionCount; i++)
            {
                float angle = Mathf.PI * 2f * i / _ripple.positionCount;
                _ripple.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.48f, 0f, Mathf.Sin(angle) * 0.26f));
            }
            _ripple.gameObject.SetActive(false);

            BuildFishModel();
        }

        private void BuildFishModel()
        {
            _fish = new GameObject("Sea Fish").transform;
            _fish.SetParent(_worldRoot, false);
            _fishBodyMaterial = CreateMaterial(new Color(0.34f, 0.60f, 0.72f), 0.62f, 0.28f);
            _fishFinMaterial = CreateMaterial(new Color(0.13f, 0.32f, 0.42f), 0.45f, 0.16f);
            _fishBellyMaterial = CreateMaterial(new Color(0.77f, 0.88f, 0.88f), 0.55f, 0.12f);
            Material eyeWhite = CreateMaterial(new Color(0.94f, 0.93f, 0.82f), 0.56f);
            Material eyeBlack = CreateMaterial(new Color(0.012f, 0.018f, 0.02f), 0.70f);

            _fishBody = CreatePrimitive("Body", PrimitiveType.Sphere, _fish, Vector3.zero, new Vector3(1.28f, 0.55f, 0.44f), _fishBodyMaterial).transform;
            _fishHead = CreatePrimitive("Head", PrimitiveType.Sphere, _fish, new Vector3(0.72f, 0.02f, 0f), new Vector3(0.58f, 0.48f, 0.42f), _fishBodyMaterial).transform;
            CreatePrimitive("Belly", PrimitiveType.Sphere, _fish, new Vector3(0.08f, -0.22f, 0f), new Vector3(0.96f, 0.29f, 0.40f), _fishBellyMaterial);

            _fishTail = new GameObject("Animated Tail").transform;
            _fishTail.SetParent(_fish, false);
            _fishTail.localPosition = new Vector3(-1.08f, 0f, 0f);
            CreateFin("Tail Upper", _fishTail, new Vector3(0f, 0f, 0f), new Vector3(-0.48f, 0.46f, 0f), new Vector3(-0.39f, 0f, 0f), _fishFinMaterial);
            CreateFin("Tail Lower", _fishTail, new Vector3(0f, 0f, 0f), new Vector3(-0.48f, -0.46f, 0f), new Vector3(-0.39f, 0f, 0f), _fishFinMaterial);
            CreateFin("Dorsal Fin", _fish, new Vector3(-0.42f, 0.35f, 0f), new Vector3(-0.10f, 0.69f, 0f), new Vector3(0.38f, 0.34f, 0f), _fishFinMaterial);
            _pectoralFin = CreateFin("Pectoral Fin", _fish, new Vector3(0.30f, -0.01f, -0.34f), new Vector3(-0.04f, -0.30f, -0.47f), new Vector3(0.47f, -0.20f, -0.40f), _fishFinMaterial).transform;

            CreatePrimitive("Eye White Near", PrimitiveType.Sphere, _fish, new Vector3(0.87f, 0.17f, -0.36f), Vector3.one * 0.13f, eyeWhite);
            CreatePrimitive("Eye Pupil Near", PrimitiveType.Sphere, _fish, new Vector3(0.90f, 0.18f, -0.45f), Vector3.one * 0.067f, eyeBlack);
            CreatePrimitive("Eye White Far", PrimitiveType.Sphere, _fish, new Vector3(0.87f, 0.17f, 0.36f), Vector3.one * 0.13f, eyeWhite);
            CreatePrimitive("Eye Pupil Far", PrimitiveType.Sphere, _fish, new Vector3(0.90f, 0.18f, 0.45f), Vector3.one * 0.067f, eyeBlack);
            GameObject mouth = CreatePrimitive("Mouth", PrimitiveType.Cube, _fish, new Vector3(1.18f, -0.06f, -0.01f), new Vector3(0.07f, 0.04f, 0.22f), eyeBlack);
            mouth.transform.localRotation = Quaternion.Euler(0f, 0f, -10f);

            _mackerelMarkings = new GameObject("Mackerel Stripes");
            _mackerelMarkings.transform.SetParent(_fish, false);
            Material stripe = CreateUnlitMaterial(new Color(0.035f, 0.15f, 0.24f, 0.95f));
            for (int i = 0; i < 6; i++)
            {
                float x = -0.62f + i * 0.23f;
                GameObject marking = CreatePrimitive($"Stripe {i + 1}", PrimitiveType.Cube, _mackerelMarkings.transform,
                    new Vector3(x, 0.24f + Mathf.Abs(i - 2.5f) * -0.012f, -0.405f), new Vector3(0.055f, 0.20f, 0.018f), stripe);
                marking.transform.localRotation = Quaternion.Euler(0f, 0f, -18f + i * 4f);
            }

            _breamMarking = new GameObject("Sea Bream Details");
            _breamMarking.transform.SetParent(_fish, false);
            Material breamRed = CreateMaterial(new Color(0.72f, 0.16f, 0.14f), 0.45f);
            GameObject gillPlate = CreatePrimitive("Gill Plate", PrimitiveType.Cylinder, _breamMarking.transform, new Vector3(0.63f, 0.02f, -0.40f), new Vector3(0.28f, 0.018f, 0.28f), breamRed);
            gillPlate.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            _amberjackStripe = new GameObject("Amberjack Gold Stripe");
            _amberjackStripe.transform.SetParent(_fish, false);
            Material gold = CreateUnlitMaterial(new Color(1f, 0.67f, 0.06f, 0.96f));
            CreatePrimitive("Gold Side Stripe", PrimitiveType.Cube, _amberjackStripe.transform, new Vector3(0f, 0.06f, -0.43f), new Vector3(1.72f, 0.10f, 0.025f), gold);
            CreatePrimitive("Gold Tail Band", PrimitiveType.Cube, _amberjackStripe.transform, new Vector3(-0.92f, 0f, -0.40f), new Vector3(0.18f, 0.45f, 0.025f), gold);

            _fallbackFishRenderers = _fish.GetComponentsInChildren<Renderer>(true);
            _fish.gameObject.SetActive(false);
        }

        private void BuildCoastalScenery()
        {
            Material rockBack = CreateMaterial(new Color(0.25f, 0.33f, 0.36f), 0.12f);
            Material rockFront = CreateMaterial(new Color(0.34f, 0.42f, 0.43f), 0.15f);
            BuildIsland(new Vector3(-13f, 0.3f, 27f), new Vector3(9f, 2.6f, 4.8f), rockBack);
            BuildIsland(new Vector3(13f, 0.25f, 31f), new Vector3(11f, 3.2f, 5.6f), rockBack);
            BuildIsland(new Vector3(8.0f, 0.25f, 20f), new Vector3(4.2f, 1.6f, 3.0f), rockFront);

            Transform lighthouse = new GameObject("Distant Lighthouse").transform;
            lighthouse.SetParent(_worldRoot, false);
            lighthouse.localPosition = new Vector3(-11.3f, 1.1f, 25.5f);
            Material white = CreateMaterial(new Color(0.92f, 0.91f, 0.82f), 0.24f);
            Material red = CreateMaterial(new Color(0.73f, 0.12f, 0.09f), 0.32f);
            Material glass = CreateUnlitMaterial(new Color(1f, 0.82f, 0.36f, 1f));
            CreatePrimitive("Tower", PrimitiveType.Cylinder, lighthouse, new Vector3(0f, 2.2f, 0f), new Vector3(0.48f, 2.2f, 0.48f), white);
            CreatePrimitive("Red Band", PrimitiveType.Cylinder, lighthouse, new Vector3(0f, 2.45f, 0f), new Vector3(0.51f, 0.38f, 0.51f), red);
            CreatePrimitive("Lamp Room", PrimitiveType.Cylinder, lighthouse, new Vector3(0f, 4.65f, 0f), new Vector3(0.60f, 0.35f, 0.60f), glass);
            CreatePrimitive("Roof", PrimitiveType.Sphere, lighthouse, new Vector3(0f, 5.05f, 0f), new Vector3(0.72f, 0.28f, 0.72f), red);

            Material buoyRed = CreateMaterial(new Color(0.92f, 0.14f, 0.08f), 0.42f, 0.48f);
            Transform buoy = new GameObject("Navigation Buoy").transform;
            buoy.SetParent(_worldRoot, false);
            buoy.localPosition = new Vector3(6.8f, 0.35f, 10.5f);
            CreatePrimitive("Buoy Float", PrimitiveType.Sphere, buoy, Vector3.zero, new Vector3(0.32f, 0.42f, 0.32f), buoyRed);
            CreatePrimitive("Buoy Mast", PrimitiveType.Cylinder, buoy, new Vector3(0f, 0.47f, 0f), new Vector3(0.055f, 0.44f, 0.055f), buoyRed);

            Material cloud = CreateUnlitMaterial(new Color(0.94f, 0.98f, 1f, 0.92f));
            BuildCloud(new Vector3(-5.5f, 8.3f, 26f), 1.25f, cloud);
            BuildCloud(new Vector3(8.3f, 7.4f, 31f), 0.92f, cloud);
            BuildSeabird(new Vector3(-4.2f, 6.7f, 15f), 0.42f);
            BuildSeabird(new Vector3(3.4f, 7.7f, 19f), 0.34f);
            BuildSeabird(new Vector3(7.6f, 6.1f, 23f), 0.28f);
        }

        private void UpdateAnglerAndRod(FishingSnapshot snapshot)
        {
            bool rodFocused = controller.Mode == FishingGameMode.SingleFishSession;
            float fight = rodFocused
                ? _v2Visual.RodLoadNormalized
                : snapshot.State == FishingPlayerState.Fighting ? controller.LastFeedback.Intensity : 0f;
            if (_anglerVisualAdapter != null) _anglerVisualAdapter.Apply(snapshot, fight);
            _angler.localRotation = rodFocused
                ? Quaternion.identity
                : Quaternion.Euler(
                    snapshot.State == FishingPlayerState.Fighting ? -4f - fight * 8f : 0f,
                    snapshot.State == FishingPlayerState.Fighting ? Mathf.Sin(Time.time * 2.2f) * 4f : 0f,
                    0f);

            Vector3 basePoint = rodFocused
                ? _angler.TransformPoint(new Vector3(0.88f, 1.12f, 0.55f))
                : _anglerVisualAdapter != null && _anglerVisualAdapter.HasRodGrip
                    ? _anglerVisualAdapter.RodGripPosition
                    : _angler.TransformPoint(new Vector3(0.27f, 1.49f, 0.78f));
            _rodGripAnchor.position = basePoint;
            _rodGripAnchor.rotation = _angler.rotation;

            FishingInputFrame input = controller.LastInputFrame;
            float pitchDegrees = input.RodPitch >= 0f
                ? -input.RodPitch * 25f
                : -input.RodPitch * 25f;
            Quaternion targetPose = Quaternion.Euler(pitchDegrees, input.RodYaw * 32f, 0f);
            float poseBlend = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
            _rodPosePivot.localRotation = Quaternion.Slerp(_rodPosePivot.localRotation, targetPose, poseBlend);

            float reactionPitch = 0f;
            float reactionYaw = 0f;
            if (snapshot.State == FishingPlayerState.Casting)
            {
                reactionPitch = Mathf.Lerp(-38f, 8f, EaseOut(snapshot.CastPower));
            }
            else if (rodFocused && snapshot.State != FishingPlayerState.Idle)
            {
                reactionPitch = _v2Visual.RodLoadNormalized * 11f + _v2Visual.RodKickNormalized * 15f;
                reactionYaw = _v2Visual.LateralPullNormalized * 13f +
                    Mathf.Sin(_v2Visual.AnimationPhaseSeconds * 17f) * _v2Visual.LineJitterNormalized * 3.5f;
                if (snapshot.State == FishingPlayerState.Hooked)
                {
                    reactionPitch -= Mathf.Lerp(12f, 0f, Mathf.Clamp01(snapshot.StateElapsedSeconds * 5f));
                }
            }
            else if (snapshot.State == FishingPlayerState.Fighting)
            {
                reactionPitch = 4f + fight * 8f;
                reactionYaw = snapshot.FightDirection * (4f + fight * 5f) + Mathf.Sin(Time.time * 4f) * 2f;
            }
            else if (snapshot.State == FishingPlayerState.BiteWindow)
            {
                reactionPitch = 4f + Mathf.Sin(Time.time * 15f) * 2.5f;
            }
            else if (snapshot.State == FishingPlayerState.Hooked)
            {
                reactionPitch = Mathf.Lerp(-10f, 0f, Mathf.Clamp01(snapshot.StateElapsedSeconds * 5f));
            }
            _rodReactionPivot.localRotation = Quaternion.Euler(reactionPitch, reactionYaw, 0f);
            _rodTip.localPosition = _neutralRodTipOffset + (rodFocused
                ? new Vector3(
                    _v2Visual.LateralPullNormalized * 0.18f,
                    -_v2Visual.RodLoadNormalized * 0.36f - _v2Visual.RodKickNormalized * 0.15f,
                    0f)
                : Vector3.zero);
            PositionRodSegments(basePoint, rodFocused ? _v2Visual.RodLoadNormalized : 0f,
                rodFocused ? _v2Visual.LateralPullNormalized : 0f);
            if (_reel != null)
            {
                Vector3 rodDirection = (_rodTip.position - basePoint).normalized;
                _reel.position = basePoint + rodDirection * 0.10f + Vector3.down * 0.11f;
                _reel.rotation = _rod.rotation * Quaternion.Euler(90f, 0f, 0f);
            }
        }

        private void PositionRodSegments(Vector3 basePoint, float load, float lateralPull)
        {
            Vector3 tipPosition = _rodTip.position;
            Vector3 rodVector = tipPosition - basePoint;
            Vector3 lateral = _rodReactionPivot.TransformDirection(Vector3.right) * lateralPull * 0.16f;
            Vector3 bend = Vector3.down * load * 0.30f;
            Vector3 lowerEnd = basePoint + rodVector * 0.34f + lateral * 0.12f + bend * 0.10f;
            Vector3 middleEnd = basePoint + rodVector * 0.68f + lateral * 0.52f + bend * 0.52f;
            PositionCylinder(_rod, basePoint, lowerEnd, 0.038f);
            PositionCylinder(_rodMiddle, lowerEnd, middleEnd, 0.032f);
            PositionCylinder(_rodUpper, middleEnd, tipPosition, 0.023f);
        }

        public static void ApplyLineGeometry(
            LineRenderer line,
            Vector3 start,
            Vector3 end,
            float sagMeters,
            float lateralJitterMeters = 0f)
        {
            if (line == null) return;
            line.positionCount = 3;
            line.SetPosition(0, start);
            line.SetPosition(1, Vector3.Lerp(start, end, 0.52f) +
                Vector3.down * Mathf.Max(0f, sagMeters) +
                Vector3.right * lateralJitterMeters);
            line.SetPosition(2, end);
        }

        private void ApplyCameraFeedback(bool useV2Presentation)
        {
            if (_sceneCamera == null) return;
            Vector3 offset = useV2Presentation ? _v2Visual.CameraPositionOffset : Vector3.zero;
            Quaternion rotationOffset = useV2Presentation
                ? Quaternion.Euler(_v2Visual.CameraRotationEuler)
                : Quaternion.identity;
            _sceneCamera.transform.position = _baseCameraPosition + offset;
            _sceneCamera.transform.rotation = _baseCameraRotation * rotationOffset;
        }

        private void UpdatePresentationMode()
        {
            bool rodFocused = controller.Mode == FishingGameMode.SingleFishSession;
            if (_presentationModeInitialized && rodFocused == _lastRodFocusedMode) return;
            _presentationModeInitialized = true;
            _lastRodFocusedMode = rodFocused;

            bool hasExternalAngler = _externalAnglerModel != null;
            SetRenderersEnabled(_fallbackAnglerRenderers, !rodFocused && !hasExternalAngler);
            SetRenderersEnabled(_externalAnglerRenderers, !rodFocused);
        }

        private void AnimateOcean()
        {
            if (_oceanMesh == null || _oceanVertices == null) return;
            float now = Time.time;
            for (int i = 0; i < _oceanVertices.Length; i++)
            {
                Vector3 baseVertex = _oceanBaseVertices[i];
                baseVertex.y = WaveHeight(baseVertex.x, baseVertex.z, now);
                _oceanVertices[i] = baseVertex;
            }
            _oceanMesh.vertices = _oceanVertices;
            if (now >= _nextNormalRefresh)
            {
                _oceanMesh.RecalculateNormals();
                _nextNormalRefresh = now + 0.12f;
            }

            float shimmer = 0.5f + Mathf.Sin(now * 0.35f) * 0.5f;
            SetMaterialColor(_oceanMaterial, Color.Lerp(new Color(0.025f, 0.34f, 0.58f), new Color(0.04f, 0.49f, 0.69f), shimmer));
            for (int i = 0; i < _waveCrests.Count; i++)
            {
                LineRenderer crest = _waveCrests[i];
                float z = crest.transform.localPosition.z;
                float originX = crest.transform.localPosition.x;
                for (int point = 0; point < crest.positionCount; point++)
                {
                    float x = -3.6f + 7.2f * point / (crest.positionCount - 1);
                    float worldX = originX + x * crest.transform.localScale.x;
                    float stagger = Mathf.Sin(point * 0.58f + i * 1.3f + now * 0.8f) * 0.16f;
                    crest.SetPosition(point, new Vector3(x, WaveHeight(worldX, z + stagger, now) + 0.07f, stagger));
                }
            }
        }

        private void AnimateScenery()
        {
            for (int i = 0; i < _seabirds.Count; i++)
            {
                Transform bird = _seabirds[i];
                float flap = Mathf.Sin(Time.time * (3.2f + i * 0.5f)) * 13f;
                bird.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.25f + i) * 9f, flap);
            }
        }

        private void ApplyFishVisual(FishingSnapshot snapshot)
        {
            if (_fish == null || snapshot.FishId == _lastFishId) return;
            _lastFishId = snapshot.FishId;
            string fishId = string.IsNullOrWhiteSpace(snapshot.FishId) ? "blue_mackerel" : snapshot.FishId;
            _fish.gameObject.name = string.IsNullOrWhiteSpace(snapshot.FishDisplayName) ? "Sea Fish" : snapshot.FishDisplayName;
            if (BuildExternalFish(fishId))
            {
                _fish.localScale = Vector3.one;
                return;
            }

            ClearExternalFish();
            SetRenderersEnabled(_fallbackFishRenderers, true);
            _mackerelMarkings.SetActive(fishId == "blue_mackerel");
            _breamMarking.SetActive(fishId == "red_sea_bream");
            _amberjackStripe.SetActive(fishId == "greater_amberjack");

            if (fishId == "red_sea_bream")
            {
                SetMaterialColor(_fishBodyMaterial, new Color(0.92f, 0.38f, 0.35f));
                SetMaterialColor(_fishFinMaterial, new Color(0.68f, 0.12f, 0.16f));
                SetMaterialColor(_fishBellyMaterial, new Color(1f, 0.72f, 0.67f));
                _fish.localScale = Vector3.one * 0.98f;
                _fishBody.localScale = new Vector3(1.05f, 0.72f, 0.48f);
                _fishHead.localScale = new Vector3(0.60f, 0.58f, 0.47f);
            }
            else if (fishId == "greater_amberjack")
            {
                SetMaterialColor(_fishBodyMaterial, new Color(0.25f, 0.48f, 0.56f));
                SetMaterialColor(_fishFinMaterial, new Color(0.86f, 0.55f, 0.08f));
                SetMaterialColor(_fishBellyMaterial, new Color(0.78f, 0.86f, 0.80f));
                _fish.localScale = Vector3.one * 1.22f;
                _fishBody.localScale = new Vector3(1.46f, 0.48f, 0.43f);
                _fishHead.localScale = new Vector3(0.62f, 0.45f, 0.42f);
            }
            else
            {
                SetMaterialColor(_fishBodyMaterial, new Color(0.18f, 0.48f, 0.65f));
                SetMaterialColor(_fishFinMaterial, new Color(0.045f, 0.20f, 0.31f));
                SetMaterialColor(_fishBellyMaterial, new Color(0.78f, 0.88f, 0.88f));
                _fish.localScale = Vector3.one * 0.80f;
                _fishBody.localScale = new Vector3(1.34f, 0.48f, 0.40f);
                _fishHead.localScale = new Vector3(0.56f, 0.43f, 0.39f);
            }
        }

        private bool BuildExternalFish(string fishId)
        {
            FishingFishVisualEntry entry;
            if (visualSet == null || !visualSet.TryGetFish(fishId, out entry)) return false;

            ClearExternalFish();
            SetRenderersEnabled(_fallbackFishRenderers, false);
            _externalFishModel = Instantiate(entry.Prefab, _fish);
            _externalFishModel.name = $"Swappable Fish Model - {fishId}";
            ApplyPlacement(_externalFishModel.transform, entry.LocalPosition, entry.LocalEulerAngles, entry.LocalScale);
            _fishVisualAdapter = _externalFishModel.GetComponent<FishVisualAdapter>();
            if (_fishVisualAdapter == null) _fishVisualAdapter = _externalFishModel.AddComponent<FishVisualAdapter>();
            _fishVisualAdapter.AutoBind();
            _fishVisualAdapter.ApplyTint(entry.Tint);
            return true;
        }

        private void ClearExternalFish()
        {
            _fishVisualAdapter = null;
            if (_externalFishModel == null) return;
            Destroy(_externalFishModel);
            _externalFishModel = null;
        }

        private void BuildBollard(Transform parent, Vector3 position, Material material)
        {
            CreatePrimitive("Bollard Base", PrimitiveType.Cylinder, parent, position, new Vector3(0.24f, 0.14f, 0.24f), material);
            CreatePrimitive("Bollard Post", PrimitiveType.Cylinder, parent, position + Vector3.up * 0.30f, new Vector3(0.13f, 0.30f, 0.13f), material);
            CreatePrimitive("Bollard Cap", PrimitiveType.Cylinder, parent, position + Vector3.up * 0.60f, new Vector3(0.23f, 0.08f, 0.23f), material);
        }

        private void BuildRopeCoil(Transform parent, Vector3 position, Material material)
        {
            for (int ring = 0; ring < 3; ring++)
            {
                GameObject coilObject = new GameObject($"Rope Coil {ring + 1}");
                coilObject.transform.SetParent(parent, false);
                coilObject.transform.localPosition = position + Vector3.up * ring * 0.045f;
                LineRenderer coil = coilObject.AddComponent<LineRenderer>();
                coil.useWorldSpace = false;
                coil.loop = true;
                coil.positionCount = 36;
                coil.startWidth = 0.055f;
                coil.endWidth = 0.055f;
                coil.material = material;
                float radius = 0.30f - ring * 0.035f;
                for (int i = 0; i < coil.positionCount; i++)
                {
                    float angle = Mathf.PI * 2f * i / coil.positionCount;
                    coil.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius * 0.72f));
                }
            }
        }

        private void BuildIsland(Vector3 position, Vector3 scale, Material material)
        {
            Transform island = new GameObject("Rocky Island").transform;
            island.SetParent(_worldRoot, false);
            island.localPosition = position;
            for (int i = 0; i < 5; i++)
            {
                float t = i / 4f;
                Vector3 local = new Vector3(Mathf.Lerp(-scale.x * 0.32f, scale.x * 0.32f, t), Mathf.Sin(t * Mathf.PI) * scale.y * 0.34f, (i % 2) * 0.25f);
                CreatePrimitive($"Island Rock {i + 1}", PrimitiveType.Sphere, island, local,
                    new Vector3(scale.x * 0.25f, scale.y * (0.35f + Mathf.Sin(t * Mathf.PI) * 0.22f), scale.z * 0.38f), material);
            }
        }

        private void BuildCloud(Vector3 position, float scale, Material material)
        {
            Transform cloud = new GameObject("Sea Cloud").transform;
            cloud.SetParent(_worldRoot, false);
            cloud.position = position;
            CreatePrimitive("Cloud A", PrimitiveType.Sphere, cloud, new Vector3(-0.9f, 0f, 0f) * scale, new Vector3(1.4f, 0.55f, 0.65f) * scale, material);
            CreatePrimitive("Cloud B", PrimitiveType.Sphere, cloud, Vector3.zero, new Vector3(1.6f, 0.75f, 0.75f) * scale, material);
            CreatePrimitive("Cloud C", PrimitiveType.Sphere, cloud, new Vector3(1.0f, -0.05f, 0f) * scale, new Vector3(1.3f, 0.50f, 0.62f) * scale, material);
        }

        private void BuildSeabird(Vector3 position, float scale)
        {
            Transform bird = new GameObject("Seabird").transform;
            bird.SetParent(_worldRoot, false);
            bird.localPosition = position;
            Material birdMaterial = CreateUnlitMaterial(new Color(0.96f, 0.98f, 0.96f, 0.95f));
            CreateCylinderBetween("Left Wing", bird, bird.TransformPoint(Vector3.zero), bird.TransformPoint(new Vector3(-scale, 0.18f * scale, 0f)), 0.025f, birdMaterial);
            CreateCylinderBetween("Right Wing", bird, bird.TransformPoint(Vector3.zero), bird.TransformPoint(new Vector3(scale, 0.18f * scale, 0f)), 0.025f, birdMaterial);
            _seabirds.Add(bird);
        }

        private static void ApplyPlacement(Transform target, Vector3 position, Vector3 eulerAngles, Vector3 scale)
        {
            target.localPosition = position;
            target.localRotation = Quaternion.Euler(eulerAngles);
            target.localScale = scale;
        }

        private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
        {
            if (renderers == null) return;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null) renderers[i].enabled = enabled;
            }
        }

        private GameObject CreateFin(string objectName, Transform parent, Vector3 a, Vector3 b, Vector3 c, Material material)
        {
            GameObject fin = new GameObject(objectName);
            fin.transform.SetParent(parent, false);
            Mesh mesh = new Mesh { name = objectName + " Mesh" };
            mesh.vertices = new[] { a, b, c, a, c, b };
            mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            fin.AddComponent<MeshFilter>().sharedMesh = mesh;
            fin.AddComponent<MeshRenderer>().sharedMaterial = material;
            return fin;
        }

        private GameObject CreatePrimitive(string objectName, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject instance = GameObject.CreatePrimitive(type);
            instance.name = objectName;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            Renderer renderer = instance.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = instance.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            return instance;
        }

        private GameObject CreateCylinderBetween(string objectName, Transform parent, Vector3 startWorld, Vector3 endWorld, float radius, Material material)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = objectName;
            cylinder.transform.SetParent(parent, true);
            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            PositionCylinder(cylinder.transform, startWorld, endWorld, radius);
            return cylinder;
        }

        private static void PositionCylinder(Transform cylinder, Vector3 startWorld, Vector3 endWorld, float radius)
        {
            Vector3 direction = endWorld - startWorld;
            if (direction.sqrMagnitude < 0.0001f) direction = Vector3.up * 0.01f;
            cylinder.position = (startWorld + endWorld) * 0.5f;
            cylinder.rotation = Quaternion.FromToRotation(Vector3.up, direction.normalized);
            cylinder.localScale = new Vector3(radius, direction.magnitude * 0.5f, radius);
        }

        private static Material CreateMaterial(Color color, float smoothness, float metallic = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            Material material = new Material(shader);
            SetMaterialColor(material, color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            return material;
        }

        private static Material CreateUnlitMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader);
            SetMaterialColor(material, color);
            return material;
        }

        private static void SetMaterialColor(Material material, Color color)
        {
            if (material == null) return;
            material.color = color;
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        }

        private static float WaveHeight(float x, float z, float time)
        {
            return 0.30f +
                Mathf.Sin(x * 0.48f + z * 0.31f + time * 0.72f) * 0.09f +
                Mathf.Sin(x * -0.22f + z * 0.57f + time * 1.12f) * 0.055f +
                Mathf.Cos(x * 0.74f + z * 0.19f + time * 0.48f) * 0.035f;
        }

        private static float EaseOut(float value)
        {
            value = Mathf.Clamp01(value);
            return 1f - (1f - value) * (1f - value);
        }
    }
}
