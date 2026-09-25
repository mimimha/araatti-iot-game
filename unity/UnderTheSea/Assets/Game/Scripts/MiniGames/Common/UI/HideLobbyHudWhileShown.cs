using UnderTheSea.Lobby;
using UnityEngine;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// **이 오브젝트가 켜져 있는 동안 로비 HUD(채팅창 · 섬 회복도 바)를 감춘다.**
    ///
    /// 포탈을 타면 뜨는 인원 선택 창과 매칭 화면에 붙는다. 두 HUD 가 위 · 왼쪽 아래를 차지해
    /// 판과 겹치고, 포탈 화면을 보는 동안에는 채팅을 칠 일도 회복도를 볼 일도 없다.
    ///
    /// 켜고 끄는 일은 각 설치기(<see cref="LobbyChatInstaller"/> · <see cref="IslandRecoveryInstaller"/>)가
    /// 한다. "로비 밖에서는 숨긴다" 규칙과 한 곳에서 계산해야 서로를 덮지 않는다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HideLobbyHudWhileShown : MonoBehaviour
    {
        private void OnEnable() => SetHidden(true);

        private void OnDisable() => SetHidden(false);

        private void SetHidden(bool hidden)
        {
            LobbyChatInstaller.SetHiddenBy(this, hidden);
            IslandRecoveryInstaller.SetHiddenBy(this, hidden);
        }
    }
}
