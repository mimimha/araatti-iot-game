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

        /// <summary>모델 전체가 감춰져 있는가. 두 이유 중 하나라도 걸리면 참이다.</summary>
        public bool ModelHidden => modelHidden || presenceHidden;

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
        /// <b>지금 이 자리에 있어도 되는 사람인가.</b> 아니면 감춘다.
        ///
        /// <see cref="SetModelHidden"/> 과 <b>이유가 다르다.</b> 저쪽은 "외형이 아직 안 왔다",
        /// 이쪽은 "지금 네 차례가 아니다" 다. 광산에서 대기 중인 사람을 감출 때 쓴다.
        ///
        /// ⚠ <b>두 이유를 한 스위치에 담지 않는다.</b> 둘 다 <c>forceRenderingOff</c> 하나를
        ///    쓰는데, 각자 <c>SetModelHidden</c> 을 부르면 <b>나중에 부른 쪽이 앞의 판단을
        ///    지운다.</b> 외형이 도착하는 순간 대기 중인 사람이 도로 나타나는 식이다.
        ///    그래서 이유를 따로 들고 있다가 <b>하나라도 걸리면 감춘다.</b>
        /// </summary>
        public void SetPresenceHidden(bool hidden)
        {
            presenceHidden = hidden;
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

        /// <summary>
        /// 이 자리의 파츠가 가려서 벗겨 둔 파츠들. 그 파츠를 벗으면 되돌려 입힌다.
        ///
        /// 모자는 머리카락을 가린다(<c>covers: Hair</c>). 가린 쪽을 <b>지워 버리면</b>
        /// 모자를 벗었을 때 민머리가 된다. 사용자는 머리카락을 벗은 적이 없다.
        /// 그래서 가려서 벗긴 것은 여기 적어 뒀다가, 가린 파츠를 벗을 때 돌려준다.
        ///
        /// ⚠ 입고 있는 목록(<see cref="equippedParts"/>)에는 넣지 않는다. 저장 · 복제되는 값은
        ///    예전처럼 <b>보이는 것만</b>이다. 이 목록은 이 화면에서 벗을 때만 쓰인다.
        /// </summary>
        private readonly Dictionary<WearSlot, List<CharacterPartCatalog.Entry>> displacedBy =
            new Dictionary<WearSlot, List<CharacterPartCatalog.Entry>>();

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

        /// <summary>"지금 차례가 아니라서" 감춘 상태인가. <see cref="modelHidden"/> 과 따로 센다.</summary>
        private bool presenceHidden;

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

            // 이 파츠가 가려서 벗겨 뒀던 것을 돌려 입힌다. (모자를 벗으면 머리카락이 돌아온다)
            if (displacedBy.TryGetValue(entry.slot, out List<CharacterPartCatalog.Entry> displaced))
            {
                displacedBy.Remove(entry.slot);
                foreach (CharacterPartCatalog.Entry back in displaced)
                {
                    RestoreDisplaced(back);
                }
            }

            RefreshVisibility();
            return true;
        }

        /// <summary>
        /// 가려서 벗겨 뒀던 파츠를 되돌린다.
        ///
        /// 그 사이 같은 자리에 다른 것을 입었으면 돌려주지 않는다. 사용자가 새로 고른 쪽이 이긴다.
        /// 아직 다른 파츠가 그 자리를 가리고 있으면 그 파츠의 목록으로 옮긴다.
        /// </summary>
        private void RestoreDisplaced(CharacterPartCatalog.Entry back)
        {
            if (back?.prefab == null || equippedParts.ContainsKey(back.slot))
            {
                return;
            }

            foreach (KeyValuePair<WearSlot, CharacterPartCatalog.Entry> other in equippedParts)
            {
                if ((other.Value.covers & back.slot) != 0)
                {
                    AddDisplaced(other.Key, back);
                    return;
                }
            }

            ApplyEntry(back);
        }

        private void AddDisplaced(WearSlot coverer, CharacterPartCatalog.Entry covered)
        {
            if (!displacedBy.TryGetValue(coverer, out List<CharacterPartCatalog.Entry> list))
            {
                list = new List<CharacterPartCatalog.Entry>();
                displacedBy[coverer] = list;
            }

            if (!list.Contains(covered))
            {
                list.Add(covered);
            }
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
            //   · 같은 자리          바꿔 입는다. 앞의 것이 가려 두었던 목록은 이어받는다
            //   · 새 것을 가리던 것   벗는다. 새로 고른 것이 보여야 한다
            //   · 새 것이 가리는 것   벗되 기억해 둔다. 새 것을 벗으면 돌려준다
            List<WearSlot> replaced = new List<WearSlot>();
            List<CharacterPartCatalog.Entry> carried = new List<CharacterPartCatalog.Entry>();
            foreach (KeyValuePair<WearSlot, CharacterPartCatalog.Entry> old in equippedParts)
            {
                bool sameSlot = old.Key == entry.slot;
                bool coveredByNew = !sameSlot && (entry.covers & old.Key) != 0;
                if (!sameSlot && !coveredByNew && (old.Value.covers & entry.slot) == 0)
                {
                    continue;
                }

                replaced.Add(old.Key);
                if (coveredByNew)
                {
                    carried.Add(old.Value);
                }

                if ((sameSlot || coveredByNew)
                    && displacedBy.TryGetValue(old.Key, out List<CharacterPartCatalog.Entry> inherited))
                {
                    carried.AddRange(inherited);
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
                displacedBy.Remove(slot);
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

                if (NeedsSkinMaterial(entry))
                {
                    ApplySkinMaterialTo(renderer);
                }
            }

            equippedObjects[entry.slot] = root;
            equippedParts[entry.slot] = entry;

            // 이어받은 것 중 새 것이 가리지 않는 자리는 바로 돌려준다.
            // (앞의 모자는 머리를 가렸는데 새로 고른 것은 안 가리는 경우)
            List<CharacterPartCatalog.Entry> uncovered = new List<CharacterPartCatalog.Entry>();
            foreach (CharacterPartCatalog.Entry covered in carried)
            {
                if ((entry.covers & covered.slot) != 0)
                {
                    AddDisplaced(entry.slot, covered);
                }
                else
                {
                    uncovered.Add(covered);
                }
            }

            RefreshVisibility();

            foreach (CharacterPartCatalog.Entry back in uncovered)
            {
                RestoreDisplaced(back);
            }

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
        ///       빈 기본 슬롯 렌더러      프리팹 기본 메시가 그대로 보인다
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
                    draw.forceRenderingOff = ModelHidden;
                }
            }

            if (ModelHidden)
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

                // ⚠ **허용 목록을 두지 않는다.** 예전에는 몸·얼굴·상의·하의·신발만
                //    보이게 했는데, 세 캐릭터 프리팹(ShipCoopPlayer · NetworkPlayer ·
                //    P_JaeYoung) 모두 **머리카락 · 귀 · 안경 · 얼굴장식까지 아홉 개**에
                //    기본 메시가 들어 있다. 목록에 없던 넷은 파츠를 입었든 말든 늘 꺼져서,
                //    커마를 안 거친 사람이 민머리에 귀도 안경도 없는 채로 나왔다.
                //
                //    규칙은 원래 이 한 줄이면 된다 — **그 자리를 파츠가 차지했을 때만 감춘다.**
                //    메시가 없는 슬롯(모자 · 장갑 · 양말 등)은 켜 둬도 그릴 것이 없다.
                binding.renderer.forceRenderingOff = (occupied & binding.slot) != 0;
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
                if (!NeedsSkinMaterial(entry.Value) || !equippedObjects.TryGetValue(entry.Key, out GameObject equipped))
                {
                    continue;
                }

                foreach (SkinnedMeshRenderer renderer in equipped.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    ApplySkinMaterialTo(renderer);
                }
            }
        }

        /// <summary>
        /// 이 파츠에 피부 재질을 입혀야 하는가.
        ///
        /// ⚠ <b>표정(<see cref="WearSlot.Face"/>)은 카탈로그에 skin 으로 표시돼 있어도 입히지 않는다.</b>
        ///    표정 메시는 눈 · 입 · 볼터치만 있는 얹는 메시라 피부 칸을 한 군데도 쓰지 않는다.
        ///    (40개 전부 UV 로 실측. 머리 피부는 Body 메시에 있다)
        ///    그런데 피부 재질은 <see cref="CreateRecoloredSkinTexture"/> 가 팔레트의 빨강 · 주황 칸을
        ///    전부 피부색으로 바꾼 것이라, 입히면 입술 · 볼터치 · 하트 눈 · 혀가 피부색으로 지워진다.
        ///    표정 25개가 그런 빨강 계열 칸을 쓴다. 원래 재질 그대로 두면 제 색이 나온다.
        /// </summary>
        private static bool NeedsSkinMaterial(CharacterPartCatalog.Entry entry)
        {
            return entry.skin && entry.slot != WearSlot.Face;
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
