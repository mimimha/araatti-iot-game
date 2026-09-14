using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 작업 자리에 **모든 컴퓨터에서 같은 번호**를 붙인다.
    ///
    /// <b>왜 필요한가.</b> 서버가 "이 사람은 조타륜에 붙었다" 를 클라이언트에 알리려면
    /// 그 자리를 가리킬 이름이 필요하다. 그런데 <c>TaskBase</c> 는 <c>NetworkObject</c> 가
    /// 아니라서 <c>NetworkId</c> 가 없다. 씬에 처음부터 놓여 있는 물건이기 때문이다.
    ///
    /// <b>왜 목록 순서를 쓰지 않는가.</b> <c>TaskBase.All</c> 의 순서는 <c>OnEnable</c> 이
    /// 불린 순서다. 그 순서가 모든 컴퓨터에서 같다는 보장이 없다. 한 번이라도 어긋나면
    /// <b>조타륜에 붙었는데 남의 화면에선 대포에 붙어 있는</b> 일이 난다. 조용히 틀린다.
    ///
    /// 그래서 <b>계층 경로</b>로 번호를 만든다. 모두 같은 씬 파일을 열므로
    /// "Ship/Deck_Main/HelmWheel" 같은 경로는 어디서나 같다.
    ///
    /// 0 은 "자리 없음" 이다.
    /// </summary>
    public static class ShipCoopTaskIds
    {
        private static readonly Dictionary<TaskBase, int> Numbers = new Dictionary<TaskBase, int>();
        private static readonly Dictionary<int, TaskBase> Places = new Dictionary<int, TaskBase>();

        /// <summary>이 자리의 번호. 자리가 없으면 0.</summary>
        public static int IdOf(TaskBase task)
        {
            if (task == null)
            {
                return 0;
            }

            if (Numbers.TryGetValue(task, out int cached))
            {
                return cached;
            }

            Rebuild();
            return Numbers.TryGetValue(task, out int found) ? found : 0;
        }

        /// <summary>번호로 자리를 되찾는다. 0 이거나 못 찾으면 null.</summary>
        public static TaskBase Find(int id)
        {
            if (id == 0)
            {
                return null;
            }

            if (Places.TryGetValue(id, out TaskBase cached) && cached != null)
            {
                return cached;
            }

            Rebuild();
            return Places.TryGetValue(id, out TaskBase found) ? found : null;
        }

        /// <summary>
        /// 지금 씬에 있는 자리를 다시 훑는다.
        ///
        /// 작업 자리는 게임 도중에 생기고 사라진다. (수리가 끝나면 파손 지점이 꺼진다)
        /// 그래서 한 번 만들고 마는 것이 아니라 모를 때마다 다시 만든다.
        /// </summary>
        private static void Rebuild()
        {
            Numbers.Clear();
            Places.Clear();

            IReadOnlyList<TaskBase> all = TaskBase.All;

            for (int i = 0; i < all.Count; i++)
            {
                TaskBase task = all[i];

                if (task == null)
                {
                    continue;
                }

                int id = Hash(PathOf(task.transform));

                if (Places.TryGetValue(id, out TaskBase already) && already != null && already != task)
                {
                    // 경로가 같은 자리가 둘이면 서로를 가리키게 된다. 조용히 틀리느니 알린다.
                    Debug.LogError(
                        $"[ShipCoopTaskIds] 작업 자리 번호가 겹쳤습니다 — " +
                        $"'{PathOf(already.transform)}' 와 '{PathOf(task.transform)}'. " +
                        "둘 중 하나의 이름을 바꿔 주세요.", task);
                    continue;
                }

                Numbers[task] = id;
                Places[id] = task;
            }
        }

        /// <summary>루트부터의 이름 경로. 씬 이름은 넣지 않는다 — Fusion 이 씬 이름을 바꾼다.</summary>
        private static string PathOf(Transform target)
        {
            StringBuilder path = new StringBuilder(target.name);

            for (Transform step = target.parent; step != null; step = step.parent)
            {
                path.Insert(0, '/').Insert(0, step.name);
            }

            return path.ToString();
        }

        /// <summary>FNV-1a. 0 은 "자리 없음" 이므로 피한다.</summary>
        private static int Hash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;

                for (int i = 0; i < text.Length; i++)
                {
                    hash ^= text[i];
                    hash *= 16777619;
                }

                int id = (int)hash;
                return id == 0 ? 1 : id;
            }
        }
    }
}
