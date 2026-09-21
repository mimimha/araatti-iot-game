using System;
using UnityEngine;

/// <summary>
/// **접속할 때 들고 가는 쪽지.** 지금은 "이번 판이 몇 명인가" 한 가지만 적혀 있다.
///
/// Fusion 의 <c>StartGameArgs.ConnectionToken</c> 은 바이트 묶음이다. 그것을 읽고 쓰는
/// 규칙을 한 곳에 둔다 — 보내는 쪽(런처 셋)과 읽는 쪽(<c>MiniGameAdmission</c>)이
/// 각자 <c>BitConverter</c> 를 부르면, 나중에 쪽지에 한 줄 더할 때 네 군데를 고쳐야 한다.
///
/// ⚠ <b>이 값을 그대로 믿지 않는다.</b> 클라이언트가 보내는 것이라 고쳐 보낼 수 있다.
///    읽는 쪽에서 게임의 허용 범위로 한 번 더 자른다.
///
/// ⚠ 쪽지가 없거나 모양이 다르면 0 이다. 실행 인자로 서버만 띄워 QA 하던 예전 방식은
///    쪽지 없이 들어오므로, 0 을 "모른다" 로 다루고 예전 규칙을 그대로 쓴다.
/// </summary>
public static class MatchCrewToken
{
    /// <summary>쪽지에 인원을 적는다. 0 이면 빈 쪽지(null)다.</summary>
    public static byte[] Write(int crew)
    {
        return crew <= 0 ? null : BitConverter.GetBytes(crew);
    }

    /// <summary>쪽지에서 인원을 읽는다. 없거나 모양이 다르면 0.</summary>
    public static int Read(byte[] token)
    {
        if (token == null || token.Length < sizeof(int)) return 0;

        int crew = BitConverter.ToInt32(token, 0);

        // 말도 안 되는 수는 버린다. 아래에서 게임별 범위로 다시 자른다.
        return crew is > 0 and <= 64 ? crew : 0;
    }

    /// <summary>
    /// 읽은 값을 그 게임이 허용하는 범위로 자른다.
    ///
    /// <paramref name="config"/> 가 없으면 자를 기준이 없으므로 읽은 값을 그대로 쓴다.
    /// </summary>
    public static int Clamp(int crew, MiniGames.Common.MiniGameConfig config)
    {
        if (crew <= 0) return 0;
        if (config == null) return crew;

        int safe = Mathf.Clamp(crew, config.MinPlayers, config.MaxPlayers);

        if (safe != crew)
        {
            Debug.LogWarning(
                $"[판 인원] 들고 온 값 {crew}명이 {config.DisplayName} 의 범위" +
                $"({config.MinPlayers}~{config.MaxPlayers}명)를 벗어나 {safe}명으로 맞췄습니다.");
        }

        return safe;
    }
}
