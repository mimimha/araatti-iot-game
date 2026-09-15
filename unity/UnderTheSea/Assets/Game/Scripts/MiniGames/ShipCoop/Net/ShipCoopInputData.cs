using Fusion;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.Net
{
    /// <summary>
    /// 배 협동 게임에서 클라이언트가 서버로 보내는 입력 한 틱분.
    ///
    /// <b>기기가 주는 것을 그대로 담는다.</b> "조타", "발사" 같은 해석은 넣지 않는다.
    /// 그 해석은 <c>ShipCoopInput</c> 이 서버에서 하고, 여기는 <c>IHandDevice</c> 가
    /// 주는 값을 옮기기만 한다. 그래야 조작 배치를 바꿀 때 이 구조체를 안 건드린다.
    ///
    /// <code>
    ///     IHandDevice          기기가 주는 것 (스틱 · IMU · 압력 · 버튼)
    ///          ↓ 클라이언트가 채운다
    ///     ShipCoopInputData    ← 여기. 그대로 담아 보낸다
    ///          ↓ 서버가 푼다
    ///     ShipCoopNetworkedController : IPlayerController
    ///          ↓
    ///     ShipCoopInput        "조타는 양손 기울기 평균"
    /// </code>
    ///
    /// ⚠ <b>Lobby 의 <c>NetworkInputData</c> 와 섞이지 않는다.</b>
    ///    Fusion 은 구조체 타입마다 다른 Key 로 넣고 꺼낸다. 클라이언트가 넣지 않은 타입을
    ///    서버가 <c>GetInput&lt;T&gt;</c> 로 꺼내면 <b>false 를 돌려주고 값은 기본값</b>이다.
    ///    남의 구조체 바이트를 잘못 읽는 일은 없다. Dedicated Server 로 실측했다.
    ///
    /// 문서: SHIPCOOP.md 11장 (손당 float 4 + bool 5, 손 둘)
    /// </summary>
    public struct ShipCoopInputData : INetworkInput
    {
        /// <summary>왼손 스틱. 캐릭터 이동.</summary>
        public Vector2 Move;

        /// <summary>오른손 스틱. 카메라와 대포 조준이 나눠 쓴다.</summary>
        public Vector2 Look;

        /// <summary>
        /// 이 사람 카메라의 y 각도.
        ///
        /// 화면 기준 이동에 쓴다. 카메라는 사람마다 다르게 돌아가 있고
        /// <b>서버에는 카메라가 없어</b> 이 값을 스스로 알 수 없다. (11장)
        /// </summary>
        public float LookYaw;

        /// <summary>왼손 IMU 기울기(roll). 조타.</summary>
        public float LeftTilt;

        /// <summary>오른손 IMU 기울기(roll).</summary>
        public float RightTilt;

        /// <summary>왼손 IMU 회전(yaw). 돛 장력.</summary>
        public float LeftRotation;

        /// <summary>오른손 IMU 회전(yaw).</summary>
        public float RightRotation;

        /// <summary>
        /// 누르고 있는 것들. <see cref="ShipCoopButton"/> 순서다.
        ///
        /// <b>"눌린 순간" 을 보내지 않고 "누르고 있는 상태" 를 보낸다.</b>
        /// 순간을 보내면 그 한 틱이 유실될 때 입력이 통째로 사라진다.
        /// 서버가 <c>GetPressed(직전)</c> 로 순간을 스스로 만들어 낸다.
        /// </summary>
        public NetworkButtons Buttons;
    }

    /// <summary>
    /// <see cref="ShipCoopInputData.Buttons"/> 의 비트 자리.
    ///
    /// ⚠ <b>순서를 바꾸지 않는다.</b> 서버와 클라이언트가 같은 자리를 봐야 한다.
    ///    가운데에 끼워 넣지 말고 뒤에 붙인다.
    /// </summary>
    public enum ShipCoopButton
    {
        /// <summary>왼손 압력센서 HOLD</summary>
        LeftGrip = 0,

        /// <summary>오른손 압력센서 HOLD</summary>
        RightGrip = 1,

        /// <summary>왼손 버튼 1 — 도움 요청</summary>
        LeftButton1 = 2,

        /// <summary>왼손 버튼 2 — 달리기</summary>
        LeftButton2 = 3,

        /// <summary>오른손 버튼 1 — 붙기 · 집기 / 놓기</summary>
        RightButton1 = 4,

        /// <summary>오른손 버튼 2 — 대포 발사</summary>
        RightButton2 = 5,

        /// <summary>
        /// 오른손 내리치기 — 망치질.
        ///
        /// 이것만 <b>상태가 아니라 사건</b>이다. IMU 가 순간을 잡아 주고 누르고 있는 상태가 없다.
        /// 클라이언트가 감지한 틱에 한 번 켜서 보낸다.
        /// </summary>
        RightSwing = 6,

        /// <summary>기기를 2대 들고 있는가. 1대면 왼손이 곧 오른손이다.</summary>
        TwoDevices = 7,
    }
}
