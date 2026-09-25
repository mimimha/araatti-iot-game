using System.Collections.Generic;
using ithappy.Cute_Characters.Controller;
using UnderTheSea.Network;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 로비 바다에 **잠수했을 때의 화면.** 서버는 모른다. 각 클라이언트가 자기 화면에만 그린다.
    ///
    ///   · 내 캐릭터가 잠수하면 카메라를 수면 아래로 끌어내린다(<see cref="ThirdPersonCamera.CameraCeiling"/>)
    ///   · 카메라가 수면 아래면 푸른 안개 · 색 보정 · 아래에서 본 수면 무늬를 켠다
    ///   · 잠수한 캐릭터(남의 것 포함)의 머리에서 물방울을 띄운다
    ///
    /// 헤엄 여부는 서버가 정한 <see cref="NetworkPlayerMover.Swimming"/> 을 본다.
    /// 머리가 잠겼는지는 복제된 위치로 각자 계산한다. 네트워크로 보낼 만큼 중요한 값이 아니다.
    ///
    /// <see cref="LobbyUnderwaterInstaller"/> 가 <c>Resources/LobbyUnderwater.prefab</c> 을 띄운다.
    /// 참조(재질 · 볼륨 · 물방울)는 <c>Tools/아라아띠/로비 물속 연출 프리팹 만들기</c> 가 채운다.
    /// </summary>
    public class LobbyUnderwaterView : MonoBehaviour
    {
        [Tooltip("해수면 높이. NetworkPlayerMover 의 seaLevel 과 같아야 한다.")]
        [SerializeField] private float seaLevel = 0f;

        [Tooltip("캐릭터 피벗(발)에서 머리 한가운데까지(m). 헤엄 자세에서 머리는 피벗 위 약 0.6m 에 있다.")]
        [SerializeField] private float headHeight = 0.6f;

        [Header("잠수 카메라")]
        [Tooltip("잠수 중 카메라가 바라보는 높이(발 기준). 평소 1.5m 로 두면 주시점이 물 밖에 남는다.\n\n" +
                 "머리가 막 잠긴 깊이(발 −0.6m)에서도 주시점이 카메라 천장보다 아래여야 카메라가 물속으로 내려온다.")]
        [SerializeField] private float submergedLookHeight = 0.3f;

        [Tooltip("잠수 중 카메라가 올라갈 수 있는 높이(수면 기준, m). 수면에 딱 붙으면 화면이 반씩 갈려 깜빡인다.")]
        [SerializeField] private float cameraBelowSurface = 0.15f;

        [SerializeField] private float lookHeightSpeed = 3f;

        [Header("물속 화면")]
        [Tooltip("색 보정 · 비네트. 카메라가 수면 아래일 때만 켠다.")]
        [SerializeField] private VolumeProfile underwaterProfile;

        [Tooltip("에메랄드빛 열대 바다. 파랑 쪽으로 두면 어둡고 차갑게 보인다(처음 값 0.16, 0.55, 0.62 이 그랬다).")]
        [SerializeField] private Color fogColor = new Color(0.2f, 0.78f, 0.68f);

        [Tooltip("지수² 안개 밀도. 0.055 면 15m 앞이 절반쯤 흐려진다. 맑은 바다처럼 보이게 옅게 둔다.\n\n" +
                 "짙게(0.1) 두면 8m 만 가도 안개색 벽이 되어 탁하고 어둡게 느껴진다.")]
        [SerializeField] private float fogDensity = 0.055f;

        [Tooltip("물에 들어가고 나올 때 화면이 바뀌는 빠르기. 0 이면 한 번에 바뀐다.")]
        [SerializeField] private float blendSpeed = 8f;

        [Header("아래에서 본 수면")]
        [Tooltip("로비 바다 표면(WaterPlane_01)은 뒷면을 그리지 않아 물속에서 올려다보면 사라진다. 그 자리에 까는 물결 무늬(가산)다.")]
        [SerializeField] private Material surfaceMaterial;

        [Tooltip("물결 무늬 밑에 까는 반투명 청록 막. 없으면 수면이 유리처럼 투명해 물 밖 섬이 그대로 비친다.")]
        [SerializeField] private Material surfaceTintMaterial;

        [SerializeField] private float surfaceSize = 300f;
        [SerializeField] private float surfaceTiles = 40f;
        [SerializeField] private Vector2 surfaceScroll = new Vector2(0.03f, 0.018f);

        [Header("물방울")]
        [SerializeField] private ParticleSystem bubblesPrefab;

        [Tooltip("물방울이 나오는 곳(캐릭터 기준). 헤엄 자세의 머리 앞쪽이다.")]
        [SerializeField] private Vector3 bubbleOffset = new Vector3(0f, 0.6f, 0.35f);

        /// <summary>내 캐릭터를 따라가는 카메라. 캐릭터가 바뀌면 다시 찾는다.</summary>
        private ThirdPersonCamera followCamera;
        private Transform followCameraFor;
        private Camera viewCamera;
        private float defaultLookHeight;

        private Volume volume;
        private GameObject surface;
        private Material surfaceInstance;

        /// <summary>0 = 물 밖, 1 = 물속. 안개 · 볼륨을 이 값으로 섞는다.</summary>
        private float underwater;

        private bool fogSaved;
        private bool savedFog;
        private Color savedFogColor;
        private FogMode savedFogMode;
        private float savedFogDensity;

        private readonly Dictionary<NetworkPlayerMover, ParticleSystem> bubbles =
            new Dictionary<NetworkPlayerMover, ParticleSystem>();

        private readonly List<NetworkPlayerMover> stale = new List<NetworkPlayerMover>();
        private NetworkPlayerMover[] swimmers = System.Array.Empty<NetworkPlayerMover>();
        private float nextSwimmerScan;

        private void Awake()
        {
            volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;

            // 씬의 GlobalVolume 보다 위에서 섞는다.
            volume.priority = 50f;
            volume.weight = 0f;
            volume.sharedProfile = underwaterProfile;

            // 수면 판은 카메라를 따라다니는 빈 오브젝트 아래에 둔다. 막과 무늬 두 장이다.
            surface = new GameObject("UnderwaterSurface");
            surface.transform.SetParent(transform, false);

            if (surfaceTintMaterial != null)
            {
                AddSurfaceLayer("Tint", surfaceTintMaterial, 0f);
            }

            if (surfaceMaterial != null)
            {
                // 무늬는 막보다 조금 아래(= 물속 카메라에 더 가깝게) 둔다.
                surfaceInstance = new Material(surfaceMaterial);
                surfaceInstance.mainTextureScale = Vector2.one * surfaceTiles;
                AddSurfaceLayer("Ripples", surfaceInstance, -0.01f);
            }

            surface.SetActive(false);
        }

        private void AddSurfaceLayer(string name, Material material, float y)
        {
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Plane);
            layer.name = name;
            Destroy(layer.GetComponent<Collider>());
            layer.transform.SetParent(surface.transform, false);

            // 기본 Plane 은 위를 본다. 뒤집어야 아래(물속)에서 보인다.
            layer.transform.localPosition = new Vector3(0f, y, 0f);
            layer.transform.localRotation = Quaternion.Euler(180f, 0f, 0f);
            layer.transform.localScale = Vector3.one * (surfaceSize / 10f);

            var renderer = layer.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private void OnDisable()
        {
            // 로비를 벗어나면 숨겨진다. 안개 · 카메라를 원래대로 돌려 두지 않으면 미니게임까지 물속처럼 보인다.
            underwater = 0f;
            ApplyScreen(0f);
            RestoreFog();

            if (followCamera != null)
            {
                followCamera.CameraCeiling = null;
                followCamera.LookHeight = defaultLookHeight;
            }

            followCamera = null;
            followCameraFor = null;

            foreach (ParticleSystem ps in bubbles.Values)
            {
                if (ps != null) Destroy(ps.gameObject);
            }

            bubbles.Clear();
        }

        private void OnDestroy()
        {
            if (surfaceInstance != null)
            {
                Destroy(surfaceInstance);
            }
        }

        private void Update()
        {
            UpdateLocalCamera();
            UpdateScreen();
            UpdateBubbles();
        }

        // ─────────────────────────────────────────────── 내 카메라

        private void UpdateLocalCamera()
        {
            Transform me = LocalPlayer.Transform;

            if (me == null)
            {
                return;
            }

            if (followCamera == null || followCameraFor != me || followCamera.Player != me)
            {
                BindCamera(me);

                if (followCamera == null)
                {
                    return;
                }
            }

            var mover = me.GetComponent<NetworkPlayerMover>();
            bool submerged = IsSubmerged(mover);

            float wanted = submerged ? submergedLookHeight : defaultLookHeight;
            followCamera.LookHeight = Mathf.MoveTowards(followCamera.LookHeight, wanted, lookHeightSpeed * Time.deltaTime);
            followCamera.CameraCeiling = submerged ? seaLevel - cameraBelowSurface : (float?)null;
        }

        private void BindCamera(Transform me)
        {
            if (followCamera != null)
            {
                followCamera.CameraCeiling = null;
                followCamera.LookHeight = defaultLookHeight;
            }

            followCamera = null;
            followCameraFor = me;

            foreach (ThirdPersonCamera candidate in FindObjectsByType<ThirdPersonCamera>(FindObjectsSortMode.None))
            {
                if (candidate.Player == me)
                {
                    followCamera = candidate;
                    break;
                }
            }

            if (followCamera == null)
            {
                return;
            }

            defaultLookHeight = followCamera.LookHeight;
            viewCamera = followCamera.GetComponentInChildren<Camera>();
        }

        // ─────────────────────────────────────────────── 물속 화면

        private void UpdateScreen()
        {
            Camera cam = viewCamera != null && viewCamera.isActiveAndEnabled ? viewCamera : Camera.main;

            bool below = cam != null && cam.transform.position.y < seaLevel;

            underwater = blendSpeed <= 0f
                ? (below ? 1f : 0f)
                : Mathf.MoveTowards(underwater, below ? 1f : 0f, blendSpeed * Time.deltaTime);

            ApplyScreen(underwater);

            if (surface != null)
            {
                bool show = underwater > 0f && cam != null;
                if (surface.activeSelf != show)
                {
                    surface.SetActive(show);
                }

                if (show)
                {
                    Vector3 at = cam.transform.position;

                    // 판은 카메라를 따라다닌다. 무늬는 월드에 붙어 있어야 하므로 그만큼 되돌리고, 물결처럼 흘린다.
                    // (판이 X 로 뒤집혀 있어 z 방향 부호가 반대다)
                    surface.transform.position = new Vector3(at.x, seaLevel - 0.01f, at.z);

                    if (surfaceInstance != null)
                    {
                        Vector2 world = new Vector2(at.x, -at.z) / surfaceSize * surfaceTiles;
                        surfaceInstance.mainTextureOffset = world + surfaceScroll * Time.time;
                    }
                }
            }
        }

        private void ApplyScreen(float amount)
        {
            if (volume != null)
            {
                volume.weight = amount;
            }

            if (amount <= 0f)
            {
                RestoreFog();
                return;
            }

            if (!fogSaved)
            {
                savedFog = RenderSettings.fog;
                savedFogColor = RenderSettings.fogColor;
                savedFogMode = RenderSettings.fogMode;
                savedFogDensity = RenderSettings.fogDensity;
                fogSaved = true;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(savedFogColor, fogColor, amount);
            RenderSettings.fogDensity = Mathf.Lerp(savedFogDensity, fogDensity, amount);
        }

        private void RestoreFog()
        {
            if (!fogSaved)
            {
                return;
            }

            RenderSettings.fog = savedFog;
            RenderSettings.fogColor = savedFogColor;
            RenderSettings.fogMode = savedFogMode;
            RenderSettings.fogDensity = savedFogDensity;
            fogSaved = false;
        }

        // ─────────────────────────────────────────────── 물방울

        private void UpdateBubbles()
        {
            if (bubblesPrefab == null)
            {
                return;
            }

            // 캐릭터는 드나든다. 매 프레임 찾으면 비싸서 가끔만 다시 센다.
            if (Time.unscaledTime >= nextSwimmerScan)
            {
                nextSwimmerScan = Time.unscaledTime + 0.5f;
                swimmers = FindObjectsByType<NetworkPlayerMover>(FindObjectsSortMode.None);
            }

            foreach (NetworkPlayerMover mover in swimmers)
            {
                if (mover == null)
                {
                    continue;
                }

                bool emit = IsSubmerged(mover);
                bubbles.TryGetValue(mover, out ParticleSystem ps);

                if (emit && ps == null)
                {
                    ps = CreateBubbles(mover.transform);
                    bubbles[mover] = ps;
                }

                if (ps == null)
                {
                    continue;
                }

                if (emit && !ps.isEmitting)
                {
                    ps.Play(true);
                }
                else if (!emit && ps.isEmitting)
                {
                    // 멈추기만 한다. 이미 나온 방울은 마저 떠오르게 둔다.
                    ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }

            // 나간 캐릭터의 물방울을 치운다.
            stale.Clear();
            foreach (KeyValuePair<NetworkPlayerMover, ParticleSystem> pair in bubbles)
            {
                if (pair.Key == null)
                {
                    stale.Add(pair.Key);
                    if (pair.Value != null) Destroy(pair.Value.gameObject);
                }
            }

            foreach (NetworkPlayerMover gone in stale)
            {
                bubbles.Remove(gone);
            }
        }

        /// <summary>
        /// Synty 물방울(반지름 1m 구에서 5초 동안 뿜는 배경용)을 캐릭터 머리 크기로 줄여 붙인다.
        /// 방울은 월드에 남아 떠오르게 하고, 나오는 자리만 캐릭터를 따라간다.
        /// </summary>
        private ParticleSystem CreateBubbles(Transform owner)
        {
            ParticleSystem ps = Instantiate(bubblesPrefab, owner);
            ps.name = "UnderwaterBubbles";
            ps.transform.localPosition = bubbleOffset;
            ps.transform.localRotation = Quaternion.identity;

            ParticleSystem.MainModule main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2f);
            main.maxParticles = 40;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radius = 0.12f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 6f;

            return ps;
        }

        /// <summary>헤엄치는 중이고 머리가 수면 아래인가.</summary>
        private bool IsSubmerged(NetworkPlayerMover mover)
        {
            if (mover == null || mover.Object == null || !mover.Object.IsValid || !mover.Swimming)
            {
                return false;
            }

            return mover.transform.position.y + headHeight < seaLevel;
        }
    }
}
