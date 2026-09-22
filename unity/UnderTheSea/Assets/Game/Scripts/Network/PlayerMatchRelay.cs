using System;
using Fusion;
using MiniGames.Common;
using UnityEngine;

/// <summary>
/// **매칭 신청과 결과가 오가는 통로.** 플레이어 오브젝트에 붙는다.
///
/// <code>
///   내 화면   Rpc_Join   ──▶  로비 서버        Rpc_State   ──▶  내 화면만
///   (신청)    Rpc_Leave  ──▶  (LobbyMatchHost)  Rpc_Launch  ──▶  내 화면만
/// </code>
///
/// <b>왜 나에게만 돌려보내는가.</b> 내 매칭 사정은 남이 알 필요가 없다. 모두에게 뿌리면
/// 로비에 서 있는 사람 수만큼 쓸데없는 패킷이 돌고, 남의 매칭을 엿볼 수 있게 된다.
/// <see cref="RpcTargets.InputAuthority"/> 는 그 캐릭터의 주인에게만 간다.
///
/// <b>규칙은 여기 없다.</b> 누구를 누구와 묶을지는 <see cref="LobbyMatchmaker"/> 가 정한다.
/// 이 부품은 말을 옮기기만 한다. <see cref="LobbyChatRelay"/> 와 같은 자리, 같은 모양이다.
///
/// ⚠ <b>인원은 서버가 다시 본다.</b> 보낸 값을 그대로 믿으면 고쳐 보낸 화면이
///    배를 2명으로 시작시킬 수 있다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerMatchRelay : NetworkBehaviour
{
    /// <summary>내 캐릭터에 붙은 통로. 화면이 여기로 신청한다.</summary>
    public static PlayerMatchRelay Mine { get; private set; }

    /// <summary>매칭 사정이 바뀌었다. (단계, 게임, 목표 인원, 지금 인원)</summary>
    public static event Action<LobbyMatchmaker.Phase, MiniGameId, int, int> StateChanged;

    /// <summary>출발한다. (게임, 인원, 방 이름)</summary>
    public static event Action<MiniGameId, int, string> Launching;

    public override void Spawned()
    {
        if (HasStateAuthority)
        {
            // 서버다. 이 사람에게 말을 걸 수 있도록 등록해 둔다.
            LobbyMatchHost.Ensure().Register(Object.InputAuthority, this);
        }

        if (HasInputAuthority) Mine = this;
    }

    public override void Despawned(NetworkRunner runner, bool hasState)
    {
        if (HasStateAuthority && LobbyMatchHost.Current != null)
        {
            LobbyMatchHost.Current.Unregister(Object.InputAuthority);
        }

        if (Mine == this) Mine = null;
    }

    // ───────────────────────────── 화면이 부르는 것 ─────────────────────────────

    /// <summary>**매칭 신청.** 인원은 딱 그 수다 — 범위가 아니다.</summary>
    public static void Request(MiniGameConfig config, int crew)
    {
        if (config == null) return;

        if (Mine == null)
        {
            Debug.LogWarning("[매칭] 아직 로비에 접속하지 않아 신청할 수 없습니다.");
            return;
        }

        Mine.Rpc_Join((int)config.GameId, crew);
    }

    /// <summary>**매칭 취소.** 화면만 닫고 로비에 그대로 서 있는다.</summary>
    public static void Cancel()
    {
        if (Mine == null) return;

        Mine.Rpc_Leave();

        // 서버 답을 기다리지 않고 화면부터 닫는다. 취소는 되돌릴 것이 없다.
        StateChanged?.Invoke(LobbyMatchmaker.Phase.None, default, 0, 0);
    }

    // ───────────────────────────── 클라이언트 → 서버 ─────────────────────────────

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_Join(int gameId, int crew, RpcInfo info = default)
    {
        if (info.Source != Object.InputAuthority) return;

        MiniGameConfig config = MiniGameCatalog.Find((MiniGameId)gameId);
        if (config == null) return;

        LobbyMatchHost.Ensure().Matchmaker.Join(Object.InputAuthority, config, crew);
    }

    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void Rpc_Leave(RpcInfo info = default)
    {
        if (info.Source != Object.InputAuthority) return;
        if (LobbyMatchHost.Current == null) return;

        LobbyMatchHost.Current.Matchmaker.Leave(Object.InputAuthority);

        // 빠진 자리를 누가 채울 수 있는지 바로 다시 본다.
        LobbyMatchHost.Current.Matchmaker.Poll();
    }

    // ───────────────────────────── 서버 → 그 사람 ─────────────────────────────

    /// <summary>서버가 부른다. 이 캐릭터의 주인에게만 간다.</summary>
    public void PushState(LobbyMatchmaker.Phase phase, MiniGameId game, int crew, int filled)
        => Rpc_State((int)phase, (int)game, crew, filled);

    /// <summary>서버가 부른다. 이 캐릭터의 주인에게만 간다.</summary>
    public void PushLaunch(MiniGameId game, int crew, string session)
        => Rpc_Launch((int)game, crew, session);

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void Rpc_State(int phase, int gameId, int crew, int filled)
        => StateChanged?.Invoke((LobbyMatchmaker.Phase)phase, (MiniGameId)gameId, crew, filled);

    [Rpc(RpcSources.StateAuthority, RpcTargets.InputAuthority)]
    private void Rpc_Launch(int gameId, int crew, string session)
        => Launching?.Invoke((MiniGameId)gameId, crew, session);
}
