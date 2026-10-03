using System;
using UnityEngine;

namespace Warriors
{
    /// <summary>
    /// **게임 → 장치.** 검을 울려 달라는 요청이 모이는 곳.
    ///
    /// 게임은 "정타였다 · 맞았다 · 동료가 쓰러졌다" 를 여기에 흘려보내기만 한다.
    /// 그것을 실제 진동으로 바꾸는 것은 장치 담당자가 <see cref="FeedbackRequested"/> 를
    /// 구독해서 한다. 구독자가 없으면 아무 일도 일어나지 않고, 게임은 그대로 돌아간다.
    ///
    /// <b>⚠ 이 클래스는 반드시 파일 이름과 같아야 한다.</b>
    ///
    /// 예전에는 <c>WarriorsIoTFeedback.cs</c> 안에 열거형 · 구조체와 함께 들어 있었다.
    /// 유니티는 <b>파일 이름과 클래스 이름이 다른 MonoBehaviour 의 스크립트 참조를 만들지
    /// 못한다.</b> 그래서 프리팹에 붙이면 <c>m_Script: {fileID: 0}</c> 인 <b>빈 컴포넌트</b>가
    /// 써졌다. 오류도 나지 않고, 인스펙터에서만 "Missing" 으로 보인다.
    ///
    /// 실제로 그 일이 있었다. 프리팹에 붙였다는 로그가 남았는데 다시 읽으면 없었고,
    /// 그래서 <c>WarriorsNetFeedbackBridge</c> 가 허브를 영영 찾지 못했다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WarriorsIoTFeedbackHub : MonoBehaviour
    {
        /// <summary>
        /// 울려 달라는 요청. <b>장치 담당자가 구독하는 자리다.</b>
        ///
        /// 한 판에 여러 번, 아무 때나 올라온다. 받는 쪽에서 너무 잦으면 걸러도 된다 —
        /// 게임은 보낸 뒤 결과를 확인하지 않는다.
        /// </summary>
        public event Action<WarriorsIoTFeedback> FeedbackRequested;

        /// <summary>지금 듣고 있는 쪽이 있는가. 로그로 연결 여부를 확인할 때 쓴다.</summary>
        public bool HasListener => FeedbackRequested != null;

        public void Request(int playerId, WarriorsIoTFeedbackType type, float intensity = 1f)
        {
            FeedbackRequested?.Invoke(new WarriorsIoTFeedback(playerId, type, intensity));
        }
    }
}
