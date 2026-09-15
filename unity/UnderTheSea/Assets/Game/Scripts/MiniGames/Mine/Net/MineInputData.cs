using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>광산에서 누르는 것들. 비트 번호는 바꾸지 않는다 — 통신 규격이다.</summary>
    public enum MineButton
    {
        /// <summary>달리기. LeftShift.</summary>
        Run = 0,

        /// <summary>점프. Space.</summary>
        Jump = 1,

        /// <summary>휘두르기. F. (2단계에서 격자에 닿는다)</summary>
        Swing = 2,

        /// <summary>되메우기. C. (2단계)</summary>
        Restore = 3,

        /// <summary>힌트. V. (2단계)</summary>
        Hint = 4,
    }

    /// <summary>
    /// 한 틱에 서버로 가는 입력 한 덩어리.
    ///
    /// <b>카메라 각도를 같이 보내는 이유가 둘이다.</b>
    /// <code>
    ///   이동   서버에는 카메라가 없다. MineMoveInput 은 cam.Yaw 로 '앞' 을 정하므로
    ///          그 각도를 받아야 화면 기준으로 걷는다
    ///   관전   나머지 세 명이 같은 시점을 보려면 지금 턴인 사람의 각도가 필요하다
    /// </code>
    /// 두 용도가 <b>같은 값</b>이라 한 번만 보내면 둘 다 풀린다.
    ///
    /// ⚠ Pitch 는 이동에는 안 쓰이고 관전에만 쓰인다. 그래도 같이 보낸다 —
    ///   따로 보내면 두 값이 서로 다른 틱의 것이 되어 관전 화면이 미세하게 어긋난다.
    /// </summary>
    public struct MineInputData : INetworkInput
    {
        /// <summary>WASD. 화면 기준으로 풀린다.</summary>
        public Vector2 Move;

        /// <summary>카메라 좌우 각도(도).</summary>
        public float LookYaw;

        /// <summary>카메라 상하 각도(도).</summary>
        public float LookPitch;

        /// <summary><see cref="MineButton"/> 묶음.</summary>
        public NetworkButtons Buttons;
    }
}
