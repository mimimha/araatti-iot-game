using System.Collections.Generic;
using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 미니게임 씬에서 플레이어가 설 자리.
    ///
    /// 이름으로 찾지 않고 이 컴포넌트로 찾는다. "PlayerSpawn_1" 같은 이름 규칙은 오타 하나로
    /// 조용히 어긋나지만, 컴포넌트는 붙어 있거나 없거나 둘 중 하나라서 빠뜨리면 바로 드러난다.
    ///
    /// 자리 번호는 <see cref="PlayerRoster"/> 의 슬롯 순서와 같다.
    ///
    ///     검   0~1 사용
    ///     광산 0~3 사용
    ///     배   0~3 사용
    ///
    /// ⚠ 실제 스폰(네트워크 포함)은 이 파일이 하지 않는다. 자리만 알려 준다.
    ///    스폰하는 쪽은 <see cref="For"/> 로 자리를 받아 쓰고, 외형은
    ///    <see cref="PlayerEntry.CharacterPresetId"/> 를 읽으면 된다.
    /// </summary>
    public sealed class PlayerSpawnPoint : MonoBehaviour
    {
        [SerializeField, Min(0)]
        [Tooltip("몇 번째 자리인가. 0 부터. 같은 씬 안에서 겹치지 않게 둔다.")]
        private int index;

        public int Index => index;

        /// <summary>지금 씬에 있는 자리들을 번호순으로. 없으면 빈 배열.</summary>
        public static PlayerSpawnPoint[] AllInScene()
        {
            PlayerSpawnPoint[] points =
                FindObjectsByType<PlayerSpawnPoint>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            System.Array.Sort(points, (a, b) => a.index.CompareTo(b.index));
            return points;
        }

        /// <summary>번호에 해당하는 자리. 없으면 null.</summary>
        public static PlayerSpawnPoint For(int index)
        {
            foreach (PlayerSpawnPoint p in AllInScene())
                if (p.index == index) return p;
            return null;
        }

        /// <summary>
        /// 자리가 인원만큼 있는지 확인한다. 모자라면 어느 번호가 비었는지 로그로 알려 준다.
        /// 미니게임 씬을 만들 때 한 번 불러 보면 연결 실수를 일찍 찾는다.
        /// </summary>
        public static bool Validate(int requiredCount)
        {
            var seen = new HashSet<int>();
            foreach (PlayerSpawnPoint p in AllInScene())
            {
                if (!seen.Add(p.index))
                    Debug.LogError($"[PlayerSpawnPoint] 자리 번호 {p.index} 가 둘 이상입니다.", p);
            }

            bool ok = true;
            for (int i = 0; i < requiredCount; i++)
            {
                if (seen.Contains(i)) continue;

                Debug.LogError($"[PlayerSpawnPoint] 자리 번호 {i} 가 씬에 없습니다. " +
                               $"{requiredCount}명짜리 게임이라 0~{requiredCount - 1} 이 모두 필요합니다.");
                ok = false;
            }
            return ok;
        }
    }
}
