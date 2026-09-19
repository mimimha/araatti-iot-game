using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheSea.Character
{
    /// <summary>
    /// 외형 스냅샷을 캐릭터 모델에 입힌다. **UI 를 전혀 건드리지 않는다.**
    ///
    /// 캐릭터 생성 화면(<see cref="CharacterCustomizationController"/>)에 있던
    /// "파츠를 뼈에 붙이는 일" 과 "피부색을 칠하는 일" 만 떼어낸 것이다.
    /// 버튼 · 페이징 · 닉네임 검증 같은 화면 책임은 가져오지 않았다.
    ///
    /// <b>왜 떼어냈나.</b>
    /// 커마 컨트롤러는 <c>Awake()</c> 에서 <c>categoryButtons</c> · <c>nicknameInput</c> 같은
    /// UI 참조를 null 검사 없이 쓴다. 그래서 UI 없는 오브젝트에 붙이면 즉시
    /// <c>NullReferenceException</c> 이 난다. 로비의 캐릭터에는 그 UI 가 없다.
    ///
    /// <b>쓰는 곳</b>
    ///   · 캐릭터 생성 화면 — 컨트롤러가 이 컴포넌트에 적용을 위임한다 (겉보기 동작은 그대로)
    ///   · (PRD 09-2 예정) 로비의 NetworkPlayer — 복제받은 값으로 이걸 부른다
    ///
    /// <b>빈 스냅샷은 아무것도 하지 않는다.</b>
    /// 파츠가 하나도 없으면 모델을 프리팹 상태 그대로 둔다. 지우거나 감추지 않는다.
    /// 개발용 직접 진입(로그인 없이 Lobby 실행)에서 기본 외형을 그대로 쓰기 위한 규칙이다.
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 09-1)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CharacterAppearanceApplier : MonoBehaviour
    {
        /// <summary>
        /// 파츠를 입히는 순서.
        ///
        /// ⚠ 순서가 중요하다. 서로 가리는 파츠의 결과가 항상 같아야 한다.
        ///    신발이 하의보다 먼저여야 일체형 부츠가 신발을 가릴 수 있다.
        ///    <c>CharacterCustomizationPersistence.PersistedCategories</c> 와 같은 순서다.
        /// </summary>
        public static readonly string[] ApplyOrder =
        {
            "Face", "Hair", "Shoes", "Top", "Bottom", "Accessory"
        };

        /// <summary>몸의 자리 하나와 그 자리를 그리는 렌더러.</summary>
        [Serializable]
        public sealed class SlotBinding
        {
            public WearSlot slot;
            public SkinnedMeshRenderer renderer;
        }

        [Header("카탈로그")]
        [Tooltip("파츠 이름 → 프리팹. 이 에셋이 없으면 아무것도 입힐 수 없다.")]
        [SerializeField] private CharacterPartCatalog catalog;

        [Header("모델")]
        [Tooltip("몸의 자리별 렌더러. 파츠는 여기 뼈를 빌려 붙는다.")]
        [SerializeField] private SlotBinding[] slotBindings = Array.Empty<SlotBinding>();

        [Tooltip("피부색을 칠할 렌더러들. (몸 · 얼굴 · 귀)")]
        [SerializeField] private SkinnedMeshRenderer[] skinRenderers = Array.Empty<SkinnedMeshRenderer>();

        [Tooltip("피부 재질의 원본. 비어 있으면 skinRenderers 의 첫 재질을 쓴다.")]
        [SerializeField] private Material skinSourceMaterial;

        public CharacterPartCatalog Catalog => catalog;

        /// <summary>모델 전체가 감춰져 있는가.</summary>
        public bool ModelHidden => modelHidden;

        /// <summary>
        /// 모델 전체를 감추거나 보인다. <b>자리별 규칙과 한 곳에서 계산된다.</b>
        ///
        /// 바깥(<c>NetworkPlayerAppearance</c>)이 렌더러를 직접 만지지 않고 이걸 부르는 이유는,
        /// "전체 감추기" 와 "자리별 감추기" 가 <b>같은 스위치</b>를 쓰기 때문이다.
        /// 두 곳에서 따로 쓰면 서로를 덮는다.
        /// </summary>
        public void SetModelHidden(bool hidden)
        {
            modelHidden = hidden;
            RefreshVisibility();
        }

        /// <summary>
        /// 지금 입고 있는 파츠의 키.
        ///
        /// 서로 가리는 파츠를 벗기는 처리는 <c>Destroy</c> 를 쓰는데, Unity 의 <c>Destroy</c> 는
        /// 프레임 끝에 실제로 지운다. 그래서 **씬의 오브젝트를 세면 방금 벗은 것까지 잡힌다.**
        /// 무엇을 입고 있는지 정확히 알아야 할 때는 이 목록을 봐야 한다.
        /// (PRD 09-2 에서 서버가 복제할 값을 뽑을 때도 이 목록을 쓴다)
        /// </summary>
        public IEnumerable<string> EquippedKeys
        {
            get
            {
                foreach (CharacterPartCatalog.Entry entry in equippedParts.Values)
                {
                    yield return entry.Key;
                }
            }
        }

        /// <summary>지금 입고 있는 것. 자리 하나에 하나씩.</summary>
        private readonly Dictionary<WearSlot, GameObject> equippedObjects = new Dictionary<WearSlot, GameObject>();
        private readonly Dictionary<WearSlot, CharacterPartCatalog.Entry> equippedParts =
            new Dictionary<WearSlot, CharacterPartCatalog.Entry>();

        private Color currentSkinColor = Color.white;
        private Material originalSkinMaterial;
        private Texture2D originalSkinTexture;
        private Material runtimeSkinMaterial;
        private Texture2D runtimeSkinTexture;
        private bool skinAssetsCached;

        /// <summary>
        /// 모델 전체가 감춰져 있는가. <b>자리별 규칙보다 이것이 먼저다.</b>
        ///
        /// 외형이 도착하기 전에 잠깐 기본 옷을 보여 주면 "옷이 바뀌는 버그" 처럼 보인다.
        /// 그래서 준비되기 전에는 통째로 감춰 두고, 준비된 뒤에 자리별 규칙을 입힌다.
        /// </summary>
        private bool modelHidden;

        // ------------------------------------------------------------
        // 스냅샷 적용 — 바깥에서 부르는 입구
        // ------------------------------------------------------------

        /// <summary>
        /// 스냅샷 하나를 통째로 입힌다.
        ///
        /// <b>빈 스냅샷이면 아무것도 하지 않고 <c>true</c> 를 돌려준다.</b>
        /// 모델은 프리팹 기본 외형 그대로 남는다. (개발용 직접 진입 규칙)
        ///
        /// 카탈로그에 없는 파츠는 **그 칸만 건너뛰고 계속 진행한다.** 에셋에서 파츠가 사라져도
        /// 캐릭터 전체가 안 나오는 것보다 낫다. 건너뛴 것은 경고로 남긴다.
        /// </summary>
        /// <returns>하나라도 입혔으면 true. 입힐 것이 없었어도 true. 카탈로그가 없으면 false.</returns>
        public bool ApplySnapshot(CharacterAppearanceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return true;
            }

            bool hasParts = snapshot.parts != null && snapshot.parts.Length > 0;
            bool hasColor = !string.IsNullOrEmpty(snapshot.bodyColorHex);

            if (!hasParts && !hasColor)
            {
                // 개발용 직접 진입 등 외형 값이 비어 있는 경우다.
                // 프리팹 기본 외형을 그대로 쓴다. 여기서 아무것도 하지 않는 것이 그 규칙의 구현이다.
                return true;
            }

            if (catalog == null)
            {
                Debug.LogError(
                    "[CharacterAppearanceApplier] 파츠 카탈로그가 비어 있어 외형을 입힐 수 없습니다. " +
                    "Inspector 의 '카탈로그' 칸을 확인해 주세요.", this);
                return false;
            }

            int applied = 0;
            int missing = 0;

            if (hasParts)
            {
                // 저장 순서가 아니라 ApplyOrder 순서로 입힌다. 서로 가리는 파츠의 결과를 고정한다.
                foreach (string category in ApplyOrder)
                {
                    foreach (CharacterPartSnapshot part in snapshot.parts)
                    {
                        if (!string.Equals(part.slot, category, StringComparison.Ordinal))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(part.prefabName))
                        {
                            continue;
                        }

                        if (!catalog.TryFind(part.prefabName, out CharacterPartCatalog.Entry entry))
                        {
                            Debug.LogWarning(
                                $"[CharacterAppearanceApplier] 파츠 \"{part.prefabName}\" ({category}) 를 " +
                                "카탈로그에서 찾지 못했습니다. 이 칸은 그대로 둡니다.", this);
                            missing++;
                            continue;
                        }

                        if (ApplyEntry(entry))
                        {
                            applied++;
                        }
                    }
                }
            }

            // 피부색은 파츠 뒤에 칠한다. 이미 입은 파츠까지 다시 칠해줘야 하기 때문이다.
            if (hasColor)
            {
                ApplySkinColorHex(snapshot.bodyColorHex);
            }

            Debug.Log(
                $"[CharacterAppearanceApplier] 외형을 적용했습니다. " +
                $"파츠 {applied}개" + (missing > 0 ? $", 찾지 못한 파츠 {missing}개" : string.Empty) +
                (hasColor ? $", 피부색 {snapshot.bodyColorHex}" : string.Empty), this);

            return true;
        }

        // ------------------------------------------------------------
        // 파츠 하나 — 커마 화면도 이 경로를 쓴다
        // ------------------------------------------------------------

        /// <summary>프리팹 하나를 입힌다. 카탈로그에 없는 프리팹이면 false.</summary>
        public bool TryApplyPart(GameObject prefab)
        {
            if (catalog == null || !catalog.TryFind(prefab, out CharacterPartCatalog.Entry entry))
            {
                return false;
            }

            return ApplyEntry(entry);
        }

        /// <summary>프리팹 하나를 벗는다. 입고 있지 않았으면 false.</summary>
        public bool TryUnequipPart(GameObject prefab)
        {
            if (catalog == null || !catalog.TryFind(prefab, out CharacterPartCatalog.Entry entry))
            {
                return false;
            }

            if (!equippedParts.TryGetValue(entry.slot, out CharacterPartCatalog.Entry equipped)
                || equipped.prefab != prefab)
            {
                return false;
            }

            if (equippedObjects.TryGetValue(entry.slot, out GameObject equippedObject) && equippedObject != null)
            {
                equippedObject.SetActive(false);
                Destroy(equippedObject);
            }

            equippedObjects.Remove(entry.slot);
            equippedParts.Remove(entry.slot);
            RefreshVisibility();
            return true;
        }

        /// <summary>이 프리팹을 지금 입고 있는가.</summary>
        public bool IsEquipped(GameObject prefab)
        {
            if (prefab == null)
            {
                return false;
            }

            foreach (CharacterPartCatalog.Entry entry in equippedParts.Values)
            {
                if (entry.prefab == prefab)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 실제로 메시를 붙이는 곳.
        ///
        /// 파츠 프리팹의 <c>SkinnedMeshRenderer</c> 를 그대로 쓰지 않고 **복제해 붙인다.**
        /// 뼈 배열을 이 모델의 뼈로 다시 이어야 애니메이션이 따라가기 때문이다.
        /// </summary>
        private bool ApplyEntry(CharacterPartCatalog.Entry entry)
        {
            if (entry?.prefab == null)
            {
                return false;
            }

            SkinnedMeshRenderer target = FindBinding(entry.slot);
            if (target == null)
            {
                Debug.LogError(
                    $"[CharacterAppearanceApplier] 자리 {entry.slot} 에 연결된 렌더러가 없습니다. " +
                    $"\"{entry.Key}\" 를 입히지 못했습니다. Inspector 의 슬롯 배선을 확인해 주세요.", this);
                return false;
            }

            SkinnedMeshRenderer[] sources = entry.prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (sources.Length == 0)
            {
                Debug.LogError(
                    $"[CharacterAppearanceApplier] \"{entry.Key}\" 에 메시가 없습니다.", this);
                return false;
            }

            // 같은 자리에 있던 것과, 서로 가리는 관계인 것을 먼저 벗는다.
            List<WearSlot> replaced = new List<WearSlot>();
            foreach (KeyValuePair<WearSlot, CharacterPartCatalog.Entry> old in equippedParts)
            {
                if (old.Key == entry.slot
                    || (old.Value.covers & entry.slot) != 0
                    || (entry.covers & old.Key) != 0)
                {
                    replaced.Add(old.Key);
                }
            }

            foreach (WearSlot slot in replaced)
            {
                if (equippedObjects.TryGetValue(slot, out GameObject old) && old != null)
                {
                    old.SetActive(false);
                    Destroy(old);
                }

                equippedObjects.Remove(slot);
                equippedParts.Remove(slot);
            }

            GameObject root = new GameObject(entry.prefab.name + " Equipped");
            root.transform.SetParent(target.transform.parent, false);
            root.transform.localPosition = target.transform.localPosition;
            root.transform.localRotation = target.transform.localRotation;
            root.transform.localScale = target.transform.localScale;

            Dictionary<string, Transform> bonesByName = new Dictionary<string, Transform>();
            foreach (Transform bone in target.bones)
            {
                if (bone != null)
                {
                    bonesByName[bone.name] = bone;
                }
            }

            foreach (SkinnedMeshRenderer source in sources)
            {
                GameObject child = new GameObject(source.name);
                child.transform.SetParent(root.transform, false);

                SkinnedMeshRenderer renderer = child.AddComponent<SkinnedMeshRenderer>();
                renderer.sharedMesh = source.sharedMesh;
                renderer.sharedMaterials = source.sharedMaterials;

                Transform[] bones = new Transform[source.bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (source.bones[i] == null || !bonesByName.TryGetValue(source.bones[i].name, out bones[i]))
                    {
                        Debug.LogError(
                            $"[CharacterAppearanceApplier] \"{entry.Key}\" 의 뼈 " +
                            $"\"{(source.bones[i] != null ? source.bones[i].name : "null")}\" 를 " +
                            "이 모델에서 찾지 못했습니다. 파츠를 입히지 못했습니다.", this);
                        Destroy(root);
                        return false;
                    }
                }

                renderer.bones = bones;
                renderer.rootBone = target.rootBone;
                renderer.localBounds = source.localBounds;
                renderer.updateWhenOffscreen = true;

                if (entry.skin)
                {
                    ApplySkinMaterialTo(renderer);
                }
            }

            equippedObjects[entry.slot] = root;
            equippedParts[entry.slot] = entry;
            RefreshVisibility();
            return true;
        }

        private SkinnedMeshRenderer FindBinding(WearSlot slot)
        {
            foreach (SlotBinding binding in slotBindings)
            {
                if (binding != null && binding.slot == slot)
                {
                    return binding.renderer;
                }
            }

            return null;
        }

        // ------------------------------------------------------------
        // 가시성 — 기본 메시와 입은 파츠가 겹치지 않게 한다
        // ------------------------------------------------------------

        /// <summary>
        /// 무엇을 그릴지 다시 정한다. <b>이 컴포넌트가 가시성을 정하는 유일한 곳이다.</b>
        ///
        /// ⚠ <b>이것이 없으면 파츠가 겹쳐 보인다.</b> 프리팹의 기본 메시(예: Outwear)가 보이는 채로
        ///    그 위에 입은 상의가 덧그려져 서로 뚫고 나온다.
        ///
        /// <b>순서가 규칙이다.</b>
        /// <code>
        ///   모델 전체가 감춰져 있으면   관련 렌더러 전부 감춘다. 자리별 규칙은 보지 않는다
        ///   모델 전체가 보이면
        ///       기본 슬롯 렌더러         그 자리를 덮는 파츠를 입었으면 감춘다
        ///       빈 기본 슬롯 렌더러      몸·얼굴·머리·귀·상의·하의·신발만 보인다. 나머지는 감춘다
        ///       런타임 Equipped 파츠     보인다
        /// </code>
        ///
        /// ⚠ <b><c>enabled</c> 가 아니라 <see cref="Renderer.forceRenderingOff"/> 를 쓴다.</b>
        ///    <c>PeerMode.Multiple</c> 에서 Fusion 의 <c>RunnerVisibilityLink</c> 가
        ///    <b><c>enabled</c> 를 자기 것으로 여긴다.</b> 스폰 순간의 값(= 파츠가 오기 전이라 전부 켜짐)을
        ///    기억해 두었다가, <b>다른 NetworkObject 가 스폰될 때마다</b> 그 값으로 되돌려 놓는다.
        ///    그래서 여기서 <c>enabled = false</c> 로 감추면 구멍 하나만 생겨도 기본 얼굴이 도로 나타난다.
        ///    실측으로 확인했다 — 커마 얼굴 위에 프리팹 기본 얼굴이 겹쳐 보였다.
        ///    <c>forceRenderingOff</c> 는 Fusion 이 건드리지 않으므로 우리 판단이 그대로 남는다.
        /// </summary>
        public void RefreshVisibility()
        {
            // 1) 먼저 전체 규칙을 깐다. 감춰야 하면 여기서 끝난다.
            foreach (Renderer draw in GetComponentsInChildren<Renderer>(true))
            {
                if (draw != null)
                {
                    draw.forceRenderingOff = modelHidden;
                }
            }

            if (modelHidden)
            {
                return;
            }

            // 2) 보이는 상태에서만 자리별 규칙을 덧씌운다.
            //    위에서 전부 false 로 깔았으므로, 런타임 Equipped 파츠는 그대로 보인다.
            WearSlot occupied = 0;
            foreach (CharacterPartCatalog.Entry entry in equippedParts.Values)
            {
                occupied |= entry.slot | entry.covers;
            }

            foreach (SlotBinding binding in slotBindings)
            {
                if (binding?.renderer == null)
                {
                    continue;
                }

                // ⚠ **머리카락과 귀도 여기 들어간다.** 둘 다 프리팹 기본 메시가 실제로 있고
                //    (`Hairstyle` · `Ears`), 몸의 일부지 갈아입는 옷이 아니다. 빠져 있으면
                //    **파츠를 안 입은 사람이 민머리에 귀 없는 채로** 돌아다닌다.
                //    로그인을 안 거치는 미니게임 직접 진입이 정확히 그 경우다.
                //
                //    덮이는 경우는 아래 `occupied` 가 알아서 처리한다 — 머리 파츠를 입거나
                //    모자가 머리를 덮으면(`covers`) 그때 꺼진다.
                bool core = binding.slot == WearSlot.Body
                    || binding.slot == WearSlot.Face
                    || binding.slot == WearSlot.Hair
                    || binding.slot == WearSlot.Ears
                    || binding.slot == WearSlot.Top
                    || binding.slot == WearSlot.Bottom
                    || binding.slot == WearSlot.Shoes;

                binding.renderer.forceRenderingOff = !(core && (occupied & binding.slot) == 0);
            }
        }

        // ------------------------------------------------------------
        // 피부색 — UI 갱신은 하지 않는다
        // ------------------------------------------------------------

        /// <summary>"#RRGGBB" 문자열로 피부색을 칠한다. 읽을 수 없는 값이면 그대로 둔다.</summary>
        public bool ApplySkinColorHex(string hex)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return false;
            }

            if (!ColorUtility.TryParseHtmlString(hex, out Color color))
            {
                Debug.LogWarning(
                    $"[CharacterAppearanceApplier] 피부색 \"{hex}\" 를 읽지 못했습니다. " +
                    "\"#RRGGBB\" 형식이어야 합니다. 기본 피부색을 유지합니다.", this);
                return false;
            }

            ApplySkinColor(color);
            return true;
        }

        /// <summary>
        /// 피부색을 칠한다. **팔레트 UI 를 갱신하지 않는다.**
        ///
        /// 커마 화면에서는 이것을 부른 뒤 화면 쪽에서 따로 팔레트를 갱신한다.
        /// 그래야 UI 가 없는 곳에서도 같은 코드로 색을 칠할 수 있다.
        /// </summary>
        public void ApplySkinColor(Color color)
        {
            currentSkinColor = color;
            RebuildSkinMaterial();

            foreach (SkinnedMeshRenderer renderer in skinRenderers)
            {
                ApplySkinMaterialTo(renderer);
            }

            // 이미 입은 파츠 중 피부 취급인 것들도 다시 칠한다.
            foreach (KeyValuePair<WearSlot, CharacterPartCatalog.Entry> entry in equippedParts)
            {
                if (!entry.Value.skin || !equippedObjects.TryGetValue(entry.Key, out GameObject equipped))
                {
                    continue;
                }

                foreach (SkinnedMeshRenderer renderer in equipped.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    ApplySkinMaterialTo(renderer);
                }
            }
        }

        /// <summary>지금 피부 재질을 이 렌더러에 입힌다.</summary>
        public void ApplySkinMaterialTo(Renderer renderer)
        {
            if (renderer == null || runtimeSkinMaterial == null)
            {
                return;
            }

            renderer.sharedMaterial = runtimeSkinMaterial;

            // 이전 구현에서 남은 전체 렌더러 색상 틴트를 제거한다.
            renderer.SetPropertyBlock(null);
        }

        private void RebuildSkinMaterial()
        {
            CacheOriginalSkinAssets();

            if (originalSkinMaterial == null || originalSkinTexture == null)
            {
                Debug.LogError(
                    "[CharacterAppearanceApplier] 피부색 변경에 쓸 원본 재질 또는 텍스처를 찾지 못했습니다.", this);
                return;
            }

            DestroyRuntimeSkinAssets();

            // 항상 변경되지 않은 원본에서 다시 생성해야 밝은 색으로 되돌릴 수 있다.
            runtimeSkinTexture = CreateRecoloredSkinTexture(originalSkinTexture, currentSkinColor);
            runtimeSkinMaterial = new Material(originalSkinMaterial)
            {
                name = originalSkinMaterial.name + " (Runtime Skin)"
            };
            runtimeSkinMaterial.SetTexture("_BaseMap", runtimeSkinTexture);
            runtimeSkinMaterial.SetColor("_BaseColor", Color.white);
        }

        private void CacheOriginalSkinAssets()
        {
            if (skinAssetsCached)
            {
                return;
            }

            skinAssetsCached = true;

            if (skinSourceMaterial != null)
            {
                originalSkinMaterial = skinSourceMaterial;
                originalSkinTexture = skinSourceMaterial.GetTexture("_BaseMap") as Texture2D;
                return;
            }

            foreach (SkinnedMeshRenderer renderer in skinRenderers)
            {
                if (renderer == null || renderer.sharedMaterial == null)
                {
                    continue;
                }

                originalSkinMaterial = renderer.sharedMaterial;
                originalSkinTexture = originalSkinMaterial.GetTexture("_BaseMap") as Texture2D;
                return;
            }
        }

        /// <summary>
        /// 피부 텍스처의 살색 계열 픽셀만 원하는 색으로 바꾼 사본을 만든다.
        ///
        /// 커마 컨트롤러에 있던 것을 그대로 옮겼다. 동작을 바꾸지 않았다.
        /// </summary>
        public static Texture2D CreateRecoloredSkinTexture(Texture2D source, Color skinColor)
        {
            RenderTexture temporary = RenderTexture.GetTemporary(
                source.width,
                source.height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;

            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;

            Texture2D result = new Texture2D(source.width, source.height, TextureFormat.RGBA32, source.mipmapCount > 1);
            result.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0);
            result.Apply(false, false);

            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(temporary);

            Color[] pixels = result.GetPixels();
            for (int i = 0; i < pixels.Length; i++)
            {
                Color pixel = pixels[i];

                // Cute Characters 팔레트에서 피부 메시가 참조하는 빨강/주황 계열만 교체한다.
                bool isSkinPalette = pixel.r > 0.22f
                    && pixel.r > pixel.g * 1.12f
                    && pixel.r > pixel.b * 1.12f;
                if (!isSkinPalette)
                {
                    continue;
                }

                float brightness = Mathf.Clamp01(Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)));
                Color recolored = skinColor * brightness;
                recolored.a = pixel.a;
                pixels[i] = recolored;
            }

            result.SetPixels(pixels);
            result.wrapMode = source.wrapMode;
            result.filterMode = source.filterMode;
            result.anisoLevel = source.anisoLevel;
            result.name = source.name + " (Runtime Skin)";
            result.Apply(source.mipmapCount > 1, false);
            return result;
        }

        private void DestroyRuntimeSkinAssets()
        {
            if (runtimeSkinMaterial != null)
            {
                Destroy(runtimeSkinMaterial);
            }

            if (runtimeSkinTexture != null)
            {
                Destroy(runtimeSkinTexture);
            }

            runtimeSkinMaterial = null;
            runtimeSkinTexture = null;
        }

        private void OnDestroy()
        {
            DestroyRuntimeSkinAssets();
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 마이그레이션 도구가 배선을 옮길 때만 쓴다.
        ///
        /// ⚠ 런타임 코드에서 부르지 마라. 에디터 어셈블리에서 보여야 해서 public 이지만,
        ///    <c>#if UNITY_EDITOR</c> 안이라 빌드에는 들어가지 않는다.
        /// </summary>
        public void EditorWire(
            CharacterPartCatalog catalogAsset,
            SlotBinding[] bindings,
            SkinnedMeshRenderer[] skins,
            Material skinMaterial)
        {
            catalog = catalogAsset;
            slotBindings = bindings ?? Array.Empty<SlotBinding>();
            skinRenderers = skins ?? Array.Empty<SkinnedMeshRenderer>();
            skinSourceMaterial = skinMaterial;
        }
#endif
    }
}
