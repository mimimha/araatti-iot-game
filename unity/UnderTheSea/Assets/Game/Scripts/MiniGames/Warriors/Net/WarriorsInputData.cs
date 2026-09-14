using Fusion;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// Warriors 에서 클라이언트가 서버로 보내는 입력 한 틱분.
    ///
    /// 1단계는 이동만 쓴다. 공격 비트는 자리만 잡아 두었고 3단계에서 서버가 읽기 시작한다.
    /// 미리 넣어 두는 이유는 나중에 구조체가 바뀌면 서버·클라 빌드를 같이 갈아야 하기 때문이다.
    ///
    /// ⚠ 다른 미니게임의 입력 구조체와 섞이지 않는다. Fusion 은 타입마다 다른 Key 로
    ///    넣고 꺼내며, 클라이언트가 넣지 않은 타입을 서버가 꺼내면 <b>false 와 기본값</b>이 온다.
    ///    ShipCoop 에서 Dedicated Server 로 실측했다.
    /// </summary>
    public struct WarriorsInputData : INetworkInput
    {
        /// <summary>이동 스틱. 화면 기준이 아니라 <b>원시 입력</b>이다.</summary>
        public Vector2 Move;

        /// <summary>
        /// 이 사람 카메라의 y 각도.
        ///
        /// <b>서버에는 카메라가 없다.</b> 화면 기준 이동을 서버가 계산하려면 이 값이 필요하다.
        /// </summary>
        public float LookYaw;

        /// <summary>누르고 있는 것들. <see cref="WarriorsButton"/> 순서다.</summary>
        public NetworkButtons Buttons;
    }

    /// <summary>
    /// <see cref="WarriorsInputData.Buttons"/> 의 비트 자리.
    ///
    /// ⚠ <b>순서를 바꾸지 않는다.</b> 서버와 클라이언트가 같은 자리를 봐야 한다.
    ///    가운데에 끼워 넣지 말고 뒤에 붙인다.
    ///
    /// 공격 3종은 몬스터 종류와 짝지어져 있다. (WARRIORS.md 2장)
    /// <code>
    ///   가로베기  물고기    키보드 1
    ///   세로베기  게        키보드 2
    ///   찌르기    해파리    키보드 3
    /// </code>
    /// </summary>
    public enum WarriorsButton
    {
        /// <summary>가로베기 — 물고기</summary>
        HorizontalSlash = 0,

        /// <summary>세로베기 — 게</summary>
        VerticalSlash = 1,

        /// <summary>찌르기 — 해파리</summary>
        Thrust = 2,

        /// <summary>회피</summary>
        Dodge = 3,
    }
}
