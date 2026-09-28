using System;
using UnityEngine;

namespace UnderTheSea.Character
{
    /// <summary>
    /// 캐릭터의 파츠 하나. 슬롯 이름과 프리팹 이름만 담는다.
    ///
    /// ⚠ 배열 인덱스를 저장하지 않는다.
    ///    인덱스는 Inspector 의 partPrefabs 배열 순서라서, 에셋을 정렬하거나
    ///    파츠를 중간에 하나 추가하면 저장된 모든 캐릭터의 외형이 밀린다.
    ///    프리팹 이름("Costume_14_01")은 순서와 무관하다.
    /// </summary>
    [Serializable]
    public struct CharacterPartSnapshot
    {
        /// <summary>어느 칸에 입는지. 캐릭터 생성 화면의 카테고리 이름과 같다. (Face / Hair / Top / Bottom / Shoes / Accessory)</summary>
        public string slot;

        /// <summary>입힐 파츠 프리팹의 이름. 예: "Costume_14_01"</summary>
        public string prefabName;

        public CharacterPartSnapshot(string slot, string prefabName)
        {
            this.slot = slot;
            this.prefabName = prefabName;
        }
    }

    /// <summary>
    /// 캐릭터 하나의 이름과 외형 전체.
    ///
    /// JsonUtility 로 직렬화한다. 그래서 Dictionary 를 쓰지 않고 배열만 쓴다.
    /// (JsonUtility 는 Dictionary 를 직렬화하지 못한다)
    ///
    /// 나중에 서버로 보내는 요청 본문도 이 모양을 그대로 쓴다.
    /// (docs/prd/auth-character-roadmap.md 2-3 · 3-3)
    /// </summary>
    [Serializable]
    public class CharacterAppearanceSnapshot
    {
        /// <summary>지금 코드가 읽고 쓰는 형식 번호. 모양이 바뀌면 이 숫자를 올린다.</summary>
        public const int CurrentVersion = 1;

        public int version = CurrentVersion;

        /// <summary>
        /// 캐릭터 이름.
        ///
        /// "캐릭터가 있는지" 를 판단하는 기준은 여전히 PlayerPrefs 의 "PlayerNickname" 이다.
        /// (SceneFlow.HasCharacter) 여기 있는 이름은 같은 클릭에서 함께 저장되는 사본이며,
        /// 서버 연동 단계에서 그대로 요청 본문에 실린다.
        /// </summary>
        public string nickname;

        /// <summary>피부색. "#RRGGBB" 형식. 팔레트 인덱스를 저장하지 않는다.</summary>
        public string bodyColorHex;

        public CharacterPartSnapshot[] parts = Array.Empty<CharacterPartSnapshot>();

        public string ToJson()
        {
            return JsonUtility.ToJson(this);
        }
    }

    /// <summary>
    /// 캐릭터 외형을 이 PC 에 저장하고 읽어오는 곳.
    ///
    /// PlayerPrefs 키 하나에 JSON 문자열로 담는다.
    /// 키 이름에 형식 번호(V1)가 들어 있으므로, 모양이 크게 바뀌면 새 키를 쓰면 된다.
    /// 그러면 예전 저장값을 읽다가 깨지는 일이 없다.
    ///
    /// ⚠ 이것은 로컬 저장이다. 서버 저장은 이후 단계에서 붙인다.
    ///    (docs/prd/auth-character-roadmap.md PRD 05 · 07)
    /// </summary>
    public static class CharacterAppearanceStore
    {
        /// <summary>저장 키. 형식이 바뀌면 V2 로 새 키를 만든다.</summary>
        public const string Key = "CharacterAppearanceSnapshotV1";

        /// <summary>저장된 외형이 있는지.</summary>
        public static bool HasSaved => !string.IsNullOrEmpty(PlayerPrefs.GetString(Key, string.Empty));

        public static void Save(CharacterAppearanceSnapshot snapshot)
        {
            if (snapshot == null)
            {
                Debug.LogWarning("[CharacterAppearanceStore] 빈 외형은 저장하지 않습니다.");
                return;
            }

            snapshot.version = CharacterAppearanceSnapshot.CurrentVersion;
            PlayerPrefs.SetString(Key, snapshot.ToJson());
            PlayerPrefs.Save();
        }

        /// <summary>
        /// 저장된 외형을 읽는다. 없거나 읽을 수 없으면 false 를 돌려준다.
        ///
        /// 저장값이 깨져 있어도 예외를 던지지 않는다. 캐릭터 생성 화면이 열리지 못하면
        /// 사용자는 손쓸 방법이 없기 때문이다. 읽지 못하면 기본 캐릭터로 시작한다.
        /// </summary>
        public static bool TryLoad(out CharacterAppearanceSnapshot snapshot)
        {
            snapshot = null;

            string json = PlayerPrefs.GetString(Key, string.Empty);
            if (string.IsNullOrEmpty(json))
                return false;

            try
            {
                snapshot = JsonUtility.FromJson<CharacterAppearanceSnapshot>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning(
                    "[CharacterAppearanceStore] 저장된 외형을 읽지 못했습니다. 기본 캐릭터로 시작합니다. " +
                    exception.Message);
                snapshot = null;
                return false;
            }

            if (snapshot == null)
                return false;

            if (snapshot.version != CharacterAppearanceSnapshot.CurrentVersion)
            {
                Debug.LogWarning(
                    $"[CharacterAppearanceStore] 저장된 외형의 형식 번호가 다릅니다. " +
                    $"(저장 {snapshot.version} / 지금 {CharacterAppearanceSnapshot.CurrentVersion}) " +
                    "기본 캐릭터로 시작합니다.");
                snapshot = null;
                return false;
            }

            if (snapshot.parts == null)
                snapshot.parts = Array.Empty<CharacterPartSnapshot>();

            return true;
        }

        /// <summary>저장된 외형을 지운다. 다음에 캐릭터 생성 화면을 열면 기본 캐릭터가 나온다.</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(Key);
            PlayerPrefs.Save();
        }
    }
}
