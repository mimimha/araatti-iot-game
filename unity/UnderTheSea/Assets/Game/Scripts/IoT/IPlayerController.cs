using UnityEngine;

/// <summary>
/// IoT 기기 1대가 주는 것.
///
/// 기기 하나의 구성
///   조이스틱 1개 · 버튼 2개 · IMU 센서 · 진동 모터
///
/// 플레이어는 이 기기를 **양손에 하나씩 2대** 드는 것을 기본으로 합니다.
/// 다만 1대만 들 수도 있습니다. (IPlayerController 참고)
/// </summary>
public interface IHandDevice
{
    /// <summary>조이스틱. -1 ~ +1</summary>
    Vector2 Stick { get; }

    /// <summary>IMU 기울기(roll). -1 ~ +1. 손을 좌우로 눕히는 정도.</summary>
    float Tilt { get; }

    /// <summary>IMU 회전(yaw). -1 ~ +1. 손목을 비트는 정도.</summary>
    float Rotation { get; }

    /// <summary>버튼 1 을 지금 누르고 있는지</summary>
    bool Button1 { get; }

    /// <summary>버튼 2 를 지금 누르고 있는지</summary>
    bool Button2 { get; }

    /// <summary>
    /// 버튼 1 이 새로 눌렸으면 true 를 한 번 돌려주고 스스로 지운다.
    /// ⚠ 한 프레임에 두 곳에서 부르면 한쪽이 놓친다. 받는 쪽은 한 곳이어야 한다.
    /// </summary>
    bool ConsumeButton1Press();

    /// <summary>버튼 2 가 새로 눌렸으면 true 를 한 번 돌려주고 스스로 지운다.</summary>
    bool ConsumeButton2Press();

    /// <summary>IMU 가 내리치는 동작을 감지했으면 true 를 한 번 돌려주고 스스로 지운다.</summary>
    bool ConsumeSwing();

    /// <summary>이 손의 진동 모터를 울린다. strength 0 ~ 1.</summary>
    void Vibrate(float strength, float seconds);
}

/// <summary>
/// 플레이어 한 명분의 입력 전체. 왼손 기기 + 오른손 기기.
///
/// ⚠ 이것은 ShipCoop 전용이 아닙니다.
///    무쌍 · 배 협동 · 광산이 같은 장치를 공유하므로,
///    INetworkService 와 마찬가지로 여러 담당자가 함께 정하는 공용 경계입니다.
///    수정할 때는 미니게임 담당자들과 IoT 담당자가 함께 정하고, 문서에도 반영합니다.
///    (GAME_STRUCTURE.md 9장)
///
/// **여기에 "조타", "발사" 같은 이름을 넣지 않습니다.**
/// 그건 배 협동 게임의 사정이고, 무쌍과 광산은 전혀 다른 행동을 씁니다.
/// 이 인터페이스는 **장치가 무엇을 주는지**까지만 말하고,
/// 그것을 무슨 행동으로 해석할지는 각 미니게임 폴더에서 정합니다.
///
///     IHandDevice        기기가 주는 것
///          ↑
///     IPlayerController  플레이어 한 명 = 왼손 + 오른손
///          ↑
///     ShipCoopInput      "조타는 양손 기울기 평균" 같은 우리 게임의 해석
///
/// 기기를 1대만 들 수도 있습니다. 그때는 Right 가 Left 와 **같은 기기**를 돌려줍니다.
/// 게임 코드는 Left / Right 를 그냥 쓰면 되고, 개수를 신경 쓰지 않아도 됩니다.
/// 양손이어야만 되는 연출이 필요하면 HasTwoDevices 로 갈라 쓰면 됩니다.
/// </summary>
public interface IPlayerController
{
    /// <summary>왼손 기기</summary>
    IHandDevice Left { get; }

    /// <summary>오른손 기기. 1대만 들면 Left 와 같은 것을 돌려준다.</summary>
    IHandDevice Right { get; }

    /// <summary>
    /// 기기를 2대 들고 있는지.
    /// 양손이어야만 성립하는 조작(양손으로 물건 들기 등)을 넣을 때만 본다.
    /// </summary>
    bool HasTwoDevices { get; }

    /// <summary>캐릭터 이동. 왼손 스틱.</summary>
    Vector2 Move { get; }

    /// <summary>카메라 회전. 오른손 스틱. 1대만 들면 왼손 스틱을 나눠 쓴다.</summary>
    Vector2 Look { get; }

    /// <summary>양손을 함께 울린다. 방향이 없는 충격(침몰 경고 등)에 쓴다.</summary>
    void VibrateBoth(float strength, float seconds);
}
