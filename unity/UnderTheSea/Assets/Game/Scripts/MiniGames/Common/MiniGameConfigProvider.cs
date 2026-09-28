using UnityEngine;

namespace MiniGames.Common
{
    /// <summary>
    /// 세 미니게임 설정을 한 곳에 모아 둔 목록. Systems/MiniGameConfigProvider.
    ///
    /// 실제 게임에서는 각 포탈이 자기 설정을 직접 들고 <see cref="CommonMatchingUI.Show"/> 에
    /// 넘기므로 이 목록이 꼭 필요하지는 않다. 게임 종류를 바꿔 보는 테스트 씬과, 설정 에셋을
    /// 어디서 찾는지 한눈에 보고 싶을 때를 위한 것이다.
    /// </summary>
    public sealed class MiniGameConfigProvider : MonoBehaviour
    {
        [SerializeField] private MiniGameConfig sword;
        [SerializeField] private MiniGameConfig mining;
        [SerializeField] private MiniGameConfig ship;

        public MiniGameConfig Sword => sword;
        public MiniGameConfig Mining => mining;
        public MiniGameConfig Ship => ship;

        public MiniGameConfig Get(MiniGameId id) => id switch
        {
            MiniGameId.Sword => sword,
            MiniGameId.Mining => mining,
            MiniGameId.Ship => ship,
            _ => null,
        };
    }
}
