using UnityEngine;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// **코드로 그리는 로비 창이 쓸 그림.** 이정표 이동 창(<see cref="SignpostTeleportUI"/>)이 읽는다.
    ///
    /// 코드로 그리는 창은 인스펙터 칸이 없어 그림을 직접 물 수 없다. 그렇다고 그림을
    /// <c>Resources</c> 로 옮기면 Art 폴더 규칙(CONVENTION.md)이 깨지므로, 이 작은 에셋만
    /// <c>Resources</c> 에 두고 그림은 제자리에서 가리킨다.
    ///
    /// <code>
    ///   Assets/Game/Resources/LobbyPanelSkin.asset
    ///     panelFrame   Art/UI/Common/panel-gold-navy-plain.png (9-slice 금테 남색 판.
    ///                  무쌍 HUD 의 Panel_GoldOrnate 에서 안쪽 모서리 동그라미 네 개를 지운 것)
    ///     rowButton    Art/UI/Common/button-row-gold.png   (매칭 버튼에서 양 끝 징을 지운 것)
    ///     rowStud      Art/UI/Common/button-stud-gold.png  (줄 왼쪽에 하나 다는 작은 징)
    /// </code>
    ///
    /// 버튼 · 글꼴은 여기 두지 않는다. 매칭 화면(<c>MatchPanelPresenter</c>)의 것을 복제해 쓴다 —
    /// 매칭 화면을 고치면 따라 바뀌게.
    /// </summary>
    [CreateAssetMenu(fileName = "LobbyPanelSkin", menuName = "아라아띠/로비 창 모양", order = 110)]
    public sealed class LobbyPanelSkin : ScriptableObject
    {
        public const string ResourcePath = "LobbyPanelSkin";

        [Tooltip("판 배경. 높이가 목록 길이에 따라 바뀌므로 9-slice 그림이어야 한다 (금테가 찌그러지지 않게).")]
        public Sprite panelFrame;

        [Tooltip("금테 두께 배율. 크면 테가 얇아진다. 무쌍 HUD 작은 카드는 2 를 쓴다.")]
        [Min(.25f)] public float framePixelsPerUnitMultiplier = 1.5f;

        [Tooltip("목록 줄 버튼 그림. 매칭 버튼(button-login-base-balanced-gold)에서 양 끝 징을 지운 것.\n" +
                 "비워 두면 매칭 화면의 [게임 시작] 버튼 그림을 그대로 쓴다.")]
        public Sprite rowButton;

        [Tooltip("줄 왼쪽에 하나만 다는 작은 둥근 징.\n" +
                 "⚠ 버튼 그림에 넣지 않고 따로 둔다. 9-slice 로 늘리면 끝과 가운데가 다른 비율로 줄어 " +
                 "징이 납작한 타원이 됐다. 따로 두면 어떤 크기의 줄에서도 동그랗다.")]
        public Sprite rowStud;

        private static LobbyPanelSkin cached;

        /// <summary>에셋을 읽는다. 없으면 null — 부르는 쪽은 단색으로 그린다.</summary>
        public static LobbyPanelSkin Load()
        {
            if (cached == null) cached = Resources.Load<LobbyPanelSkin>(ResourcePath);
            return cached;
        }
    }
}
