using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// **세 미니게임 설정을 런타임에 찾을 수 있게 모아 둔 목록.**
    /// <c>Assets/Game/Resources/MiniGameCatalog.asset</c> 한 장이다.
    ///
    /// <b>왜 <see cref="MiniGameConfigProvider"/> 로는 안 되는가.</b> 그쪽은 씬에 놓는
    /// 컴포넌트이고, 확인해 보니 <c>CommonMatchResultTest</c> 테스트 씬에만 있다.
    /// 로비 전용 서버는 그 씬을 열지 않으므로 설정을 볼 방법이 없다.
    ///
    /// <b>왜 서버가 설정을 봐야 하는가.</b> 매칭 신청에 실린 인원이 그 게임이 허용하는
    /// 범위 안인지 <b>서버가 한 번 더</b> 확인한다. 화면이 막는 것은 고쳐 보낼 수 있다.
    ///
    /// ⚠ 설정 에셋 자체는 옮기지 않았다. 이 목록이 <c>ScriptableObjects/MiniGames/Common</c>
    ///    에 있는 것들을 가리킬 뿐이라, 서연님 테스트 씬도 그대로 돌아간다.
    /// </summary>
    [CreateAssetMenu(menuName = "아라아띠/미니게임 목록", fileName = "MiniGameCatalog")]
    public sealed class MiniGameCatalog : ScriptableObject
    {
        /// <summary><c>Resources.Load</c> 에 넘기는 이름. 확장자도 폴더도 붙이지 않는다.</summary>
        public const string ResourcePath = "MiniGameCatalog";

        [SerializeField] private MiniGameConfig sword;
        [SerializeField] private MiniGameConfig mining;
        [SerializeField] private MiniGameConfig ship;

        private static MiniGameCatalog cached;

        /// <summary>
        /// 목록을 읽는다. 한 번 읽으면 들고 있는다.
        ///
        /// 없으면 <c>null</c> 이고, 부른 쪽이 그 사실을 알아야 한다 — 조용히 빈 설정을
        /// 만들어 주면 "왜 아무도 매칭이 안 되지" 로 끝난다.
        /// </summary>
        public static MiniGameCatalog Load()
        {
            if (cached != null) return cached;

            cached = Resources.Load<MiniGameCatalog>(ResourcePath);

            if (cached == null)
            {
                Debug.LogError(
                    $"[미니게임 목록] Resources/{ResourcePath} 을(를) 찾지 못했습니다. " +
                    "매칭이 인원을 검사하지 못합니다.");
            }

            return cached;
        }

        public MiniGameConfig Get(MiniGameId id)
        {
            switch (id)
            {
                case MiniGameId.Sword:  return sword;
                case MiniGameId.Mining: return mining;
                case MiniGameId.Ship:   return ship;
                default:                return null;
            }
        }

        /// <summary>목록에서 찾아 준다. 없으면 null 이고 이유를 남긴다.</summary>
        public static MiniGameConfig Find(MiniGameId id)
        {
            MiniGameCatalog catalog = Load();
            if (catalog == null) return null;

            MiniGameConfig config = catalog.Get(id);

            if (config == null)
            {
                Debug.LogError($"[미니게임 목록] {id} 의 설정이 비어 있습니다. 목록 에셋을 확인해 주세요.");
            }

            return config;
        }
    }
}
