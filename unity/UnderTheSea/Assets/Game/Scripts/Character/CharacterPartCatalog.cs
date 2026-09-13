using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnderTheSea.Character
{
    /// <summary>
    /// 캐릭터 파츠 전체 목록. **UI 프리팹에서 떼어낸 카탈로그다.**
    ///
    /// 예전에는 이 데이터가 <c>CharacterCustomization.prefab</c> 안
    /// <c>CharacterCustomizationController.catalogParts</c> 배열(378개)에만 있었다.
    /// 그래서 캐릭터 생성 화면이 없으면 외형을 입힐 방법이 없었다.
    /// 이 에셋으로 꺼내면 **UI 없이도** 스냅샷을 모델에 적용할 수 있다.
    /// (<see cref="CharacterAppearanceApplier"/>)
    ///
    /// <b>키는 프리팹 이름 문자열이다.</b> 배열 인덱스가 아니다.
    /// 인덱스는 Inspector 배열 순서라서 에셋을 정렬하면 저장된 모든 외형이 밀린다. (PRD 01 에서 확인)
    /// 프리팹 이름은 순서와 무관하고, 서버 DB <c>character_parts.prefab_name</c> 과
    /// Unity <c>CharacterPartSnapshot.prefabName</c> 이 이미 같은 문자열이라 변환이 0이다.
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 09-1)
    /// </summary>
    [CreateAssetMenu(
        fileName = "CharacterPartCatalog",
        menuName = "아라아띠/캐릭터 파츠 카탈로그",
        order = 100)]
    public sealed class CharacterPartCatalog : ScriptableObject
    {
        /// <summary>
        /// Fusion 전송용 키의 최대 길이. (PRD 09-2 의 <c>NetworkString&lt;_32&gt;</c>)
        ///
        /// ⚠ <b>서버 · DB 의 제한(64자)과 다른 값이다. 서버를 이 값에 맞추지 마라.</b>
        ///    영속 데이터의 제약을 네트워크 전송 사정에 맞춰 좁히면,
        ///    나중에 긴 이름이 필요할 때 이미 저장된 데이터까지 문제가 된다.
        ///    여기서는 **경고만** 낸다. 넘는 파츠가 생기면 그때 짧은 별도 키를 도입한다.
        /// </summary>
        public const int MaxNetworkKeyLength = 32;

        /// <summary>커마 화면의 카테고리 이름. 스냅샷의 <c>slot</c> 문자열과 같은 값이다.</summary>
        public static readonly string[] Categories =
        {
            "Face", "Hair", "Shoes", "Top", "Bottom", "Accessory"
        };

        /// <summary>파츠 하나.</summary>
        [Serializable]
        public sealed class Entry
        {
            /// <summary>커마 화면의 카테고리 이름. 스냅샷의 <c>slot</c> 과 맞춘다.</summary>
            public string category;

            /// <summary>입힐 프리팹. 이름(<c>prefab.name</c>)이 곧 안정된 키다.</summary>
            public GameObject prefab;

            /// <summary>몸의 어느 자리를 차지하는가.</summary>
            public WearSlot slot;

            /// <summary>이 파츠가 함께 가리는 자리. (예: 일체형 의상이 상의와 하의를 덮는다)</summary>
            public WearSlot covers;

            /// <summary>피부색을 입혀야 하는 파츠인가. (얼굴 · 몸 · 귀)</summary>
            public bool skin;

            /// <summary>안정된 키. 프리팹 이름이다.</summary>
            public string Key => prefab != null ? prefab.name : string.Empty;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public int Count => entries != null ? entries.Length : 0;

        /// <summary>이름 → 항목. 처음 찾을 때 한 번만 만든다.</summary>
        private Dictionary<string, Entry> byKey;

        /// <summary>프리팹 → 항목. 커마 화면이 프리팹 참조로 찾을 때 쓴다.</summary>
        private Dictionary<GameObject, Entry> byPrefab;

        /// <summary>
        /// 안정된 키로 파츠를 찾는다.
        ///
        /// ⚠ 카테고리를 함께 받지 않는다. 파츠 이름이 **380개 전체에서 유일**함을 실측으로 확인했다.
        ///    카테고리까지 맞춰야 찾히게 만들면, 나중에 파츠를 다른 카테고리로 옮겼을 때
        ///    이미 저장된 캐릭터가 그 파츠를 잃는다. 이름만으로 찾는 편이 저장값에 더 관대하다.
        ///    (이름 중복은 <see cref="Validate"/> 가 잡는다)
        /// </summary>
        public bool TryFind(string key, out Entry entry)
        {
            entry = null;

            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            EnsureIndex();
            return byKey.TryGetValue(key, out entry) && entry.prefab != null;
        }

        /// <summary>프리팹 참조로 찾는다. 커마 화면이 쓴다.</summary>
        public bool TryFind(GameObject prefab, out Entry entry)
        {
            entry = null;

            if (prefab == null)
            {
                return false;
            }

            EnsureIndex();
            return byPrefab.TryGetValue(prefab, out entry);
        }

        private void EnsureIndex()
        {
            if (byKey != null && byPrefab != null)
            {
                return;
            }

            byKey = new Dictionary<string, Entry>(StringComparer.Ordinal);
            byPrefab = new Dictionary<GameObject, Entry>();

            foreach (Entry entry in entries)
            {
                if (entry?.prefab == null)
                {
                    continue;
                }

                byKey[entry.Key] = entry;
                byPrefab[entry.prefab] = entry;
            }
        }

        /// <summary>에셋을 고치면 색인을 버린다. 에디터에서 항목을 편집했을 때 반영되게.</summary>
        private void OnValidate()
        {
            byKey = null;
            byPrefab = null;
        }

        /// <summary>
        /// 카탈로그가 성한지 검사하고 문제를 문장으로 돌려준다. 문제가 없으면 빈 목록.
        ///
        /// 검사 항목
        ///   1. 빈 프리팹
        ///   2. 이름 중복 — 이름으로 저장하므로 둘이면 어느 쪽인지 가릴 수 없다
        ///   3. 키 길이 32자 초과 — Fusion <c>NetworkString&lt;_32&gt;</c> 한도 (서버 제한과 무관)
        ///   4. 알 수 없는 카테고리
        /// </summary>
        public List<string> Validate()
        {
            List<string> problems = new List<string>();
            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);

            for (int i = 0; i < Count; i++)
            {
                Entry entry = entries[i];

                if (entry == null || entry.prefab == null)
                {
                    problems.Add($"[{i}] 프리팹이 비어 있습니다.");
                    continue;
                }

                string key = entry.Key;

                if (!seen.Add(key))
                {
                    problems.Add($"[{i}] 이름이 중복입니다: \"{key}\". 이름으로 저장하므로 어느 파츠인지 가릴 수 없습니다.");
                }

                if (key.Length > MaxNetworkKeyLength)
                {
                    problems.Add(
                        $"[{i}] \"{key}\" 가 {key.Length}자로 Fusion 전송 한도({MaxNetworkKeyLength}자)를 넘습니다. " +
                        "짧고 안정된 별도 networkKey 를 도입할 시점입니다. " +
                        "서버 · DB 의 64자 제한은 그대로 두십시오.");
                }

                // 카테고리가 비어 있는 것은 정상이다. 커마 화면의 6개 탭에 노출되지 않는
                // 파츠(몸통 메시 등)가 여기 해당한다. 스냅샷에는 실리지 않는다.
                if (!string.IsNullOrEmpty(entry.category)
                    && Array.IndexOf(Categories, entry.category) < 0)
                {
                    problems.Add(
                        $"[{i}] \"{key}\" 의 카테고리 \"{entry.category}\" 를 알 수 없습니다. " +
                        $"쓸 수 있는 값: {string.Join(", ", Categories)} (또는 빈 값)");
                }
            }

            return problems;
        }

#if UNITY_EDITOR
        /// <summary>
        /// 에디터 도구가 항목을 채울 때만 쓴다.
        ///
        /// ⚠ 런타임 코드에서 부르지 마라. 에디터 어셈블리(Assembly-CSharp-Editor)에서 보여야 해서
        ///    public 이지만, <c>#if UNITY_EDITOR</c> 안이라 빌드에는 들어가지 않는다.
        /// </summary>
        public void EditorSetEntries(Entry[] value)
        {
            entries = value ?? Array.Empty<Entry>();
            byKey = null;
            byPrefab = null;
        }
#endif
    }
}
