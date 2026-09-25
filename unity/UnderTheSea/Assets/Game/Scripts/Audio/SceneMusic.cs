using UnityEngine;

namespace UnderTheSea.Audio
{
    /// <summary>
    /// 🎵 이 씬의 배경음악. 씬에 하나 놓고 클립을 꽂으면 끝이다.
    ///
    /// 켜질 때 <see cref="AudioHub"/> 에 곡을 부탁한다. 허브는 씬을 넘어 살아 있어서
    /// <b>앞 씬과 같은 곡이면 끊기지 않고 이어지고, 다른 곡이면 교차 페이드</b>된다.
    /// 다음 씬에 이 컴포넌트가 없으면 음악은 그대로 이어진다 — 로비 → 채널 선택처럼
    /// 같은 분위기를 이어 갈 때 편하다. 끊고 싶으면 <see cref="stopWhenLeaving"/> 을 켠다.
    ///
    /// 미니게임처럼 상태에 따라 곡이 바뀌는 씬은 이걸 쓰지 않고 자기 연출가(예: ShipCoopAudio)가
    /// 직접 <c>AudioHub.PlayMusic</c> 을 부른다. 둘을 같은 씬에 두면 서로 곡을 바꿔 댄다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SceneMusic : MonoBehaviour
    {
        [Tooltip("이 씬의 배경음악. 비워두면 앞 씬 음악을 멈춘다.")]
        [SerializeField] private AudioClip music;

        [Tooltip("앞 곡과 겹치는 시간 (초).")]
        [SerializeField, Range(0.1f, 5f)] private float fadeSeconds = 1.5f;

        [Tooltip("이 곡의 상대 크기 (1 이 기본). 허브가 곡마다 크기를 고르게 맞춘 뒤에 곱한다.\n" +
                 "미니게임 음악과 크기를 맞출 때 쓴다. ShipCoop 음악은 0.448 (musicLevel 0.56 × masterLevel 0.8) 로 튼다.")]
        [SerializeField, Range(0f, 1f)] private float level = 1f;

        [Tooltip("이 씬을 떠날 때 음악을 멈춘다. 끄면 다음 씬에 SceneMusic 이 없을 때 그대로 이어진다.")]
        [SerializeField] private bool stopWhenLeaving = false;

        private void Start()
        {
            AudioHub hub = AudioHub.Instance;

            if (hub == null)
            {
                return;
            }

            if (music != null)
            {
                hub.PlayMusic(music, fadeSeconds, level);
            }
            else
            {
                hub.StopMusic(fadeSeconds);
            }
        }

        private void OnDestroy()
        {
            if (!stopWhenLeaving)
            {
                return;
            }

            AudioHub hub = AudioHub.Instance;

            if (hub != null && hub.CurrentMusic == music)
            {
                hub.StopMusic(fadeSeconds);
            }
        }
    }
}
