using System;
using UnityEngine;

/// <summary>
/// "지금 화면을 사용자에게 넘겨도 되는가" 를 알려 주는 상태판.
///
/// <b>왜 필요한가.</b>
/// 화면이 바뀔 때는 씬이 먼저 뜨고 준비가 나중에 끝난다.
/// Fusion 클라이언트는 씬을 띄운 뒤 접속하고, 서버가 캐릭터를 스폰해 줘야 카메라가 붙을 대상이 생긴다.
/// 그 사이에는 하늘이나 씬 초기 카메라 위치가 그대로 보인다.
/// <b>"접속 성공" 은 기준이 될 수 없다</b> — 캐릭터와 카메라가 준비된 뒤라야 보여 줄 만한 화면이 된다.
///
/// 같은 문제가 Lobby 진입에만 있는 것이 아니다.
/// Lobby → Game, Game → Lobby, 재접속 모두 "준비가 끝나기 전" 구간이 생긴다.
/// 그래서 특정 화면에 묶지 않고 전환 일반으로 둔다.
///
/// <b>쓰는 법.</b> 부르는 쪽이 문구를 정한다.
/// <code>
///     TransitionStatus.SetLoading("Lobby에 접속 중...");
///     TransitionStatus.SetLoading("게임에 입장 중...");
///     TransitionStatus.SetLoading("Lobby로 돌아가는 중...");
///     TransitionStatus.SetReady();                 // 실제로 플레이 가능해진 지점에서만
///     TransitionStatus.SetFailed("연결이 끊어졌습니다.");
/// </code>
///
/// <b>책임 분리.</b> 여기는 <b>상태와 문구만</b> 정한다. 화면에 무엇을 어떻게 그릴지는 모른다.
///   상태를 올리는 쪽  전환을 아는 코드 (네트워크 · 씬 전환)
///   상태를 보는 쪽    UI (기본 구현은 Assets/Game/Scripts/UI/TransitionOverlay.cs)
/// UI 를 예쁜 프리팹으로 갈아 끼워도 이 파일과 네트워크·카메라 코드는 그대로다.
///
/// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 08-2)
/// </summary>
public static class TransitionStatus
{
    public enum Phase
    {
        /// <summary>전환 중이 아니다. 가릴 것이 없다.</summary>
        Idle,

        /// <summary>준비 중이다. 아직 보여 줄 화면이 아니다.</summary>
        Loading,

        /// <summary>준비가 끝났다. 화면과 입력을 사용자에게 넘겨도 된다.</summary>
        Ready,

        /// <summary>실패했다. 화면이 가려진 채로 멈추면 안 되므로 사유를 남긴다.</summary>
        Failed
    }

    private static Phase phase = Phase.Idle;

    public static Phase Current => phase;

    /// <summary>사용자에게 보여 줄 한 줄. 부르는 쪽이 정한다.</summary>
    public static string Message { get; private set; } = string.Empty;

    /// <summary>화면과 입력을 사용자에게 넘겨도 되는가.</summary>
    public static bool IsReady => phase == Phase.Ready;

    /// <summary>
    /// 상태가 바뀔 때마다 불린다. 늦게 구독한 쪽은 <see cref="Current"/> 를 바로 읽으면 된다.
    /// </summary>
    public static event Action<Phase, string> Changed;

    /// <summary>준비를 시작했다. <paramref name="message"/> 로 무엇을 기다리는지 알린다.</summary>
    public static void SetLoading(string message)
    {
        Set(Phase.Loading, message);
    }

    /// <summary>
    /// 실제로 플레이할 수 있게 됐다.
    ///
    /// ⚠ "접속 성공" 이나 "씬 로드 완료" 에서 부르지 않는다.
    ///    조작할 대상이 생기고 카메라가 그것을 비추게 된 뒤에만 부른다.
    /// </summary>
    public static void SetReady()
    {
        Set(Phase.Ready, string.Empty);
    }

    /// <summary>실패 · 연결 종료. 사용자가 원인을 알 수 있게 문구를 남긴다.</summary>
    public static void SetFailed(string message)
    {
        Set(Phase.Failed, message);
    }

    private static void Set(Phase next, string message)
    {
        // 같은 상태라도 문구가 다르면 알린다. (실패 사유가 바뀌는 경우)
        if (phase == next && Message == message)
        {
            return;
        }

        phase = next;
        Message = message ?? string.Empty;

        Debug.Log($"[TransitionStatus] {phase}{(string.IsNullOrEmpty(Message) ? string.Empty : " — " + Message)}");

        Changed?.Invoke(phase, Message);
    }

    /// <summary>
    /// 플레이 모드에 들어갈 때마다 비운다.
    /// 에디터에서 "Reload Domain" 을 꺼 두면 static 이 이전 플레이의 값을 그대로 들고 있다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetOnPlay()
    {
        phase = Phase.Idle;
        Message = string.Empty;
        Changed = null;
    }
}
