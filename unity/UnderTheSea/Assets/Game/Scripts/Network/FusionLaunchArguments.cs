using System;
using UnityEngine;

/// <summary>
/// 실행할 때 붙인 커맨드라인 인자를 읽는다.
///
/// Dedicated Server 는 창이 없으므로 Inspector 로 설정할 수 없다.
/// 어떤 세션을 유지할지, 어떤 포트를 쓸지를 실행 명령으로 정한다.
///
///     AraAtti-Server.exe -batchmode -nographics -session lobby-ch1 -port 27015
///
/// 인자가 없으면 부르는 쪽이 기본값을 쓴다. 값이 잘못되어도 예외를 던지지 않는다.
/// 서버가 인자 하나 때문에 아예 뜨지 못하면 원인을 찾기가 더 어렵다.
/// </summary>
public static class FusionLaunchArguments
{
    /// <summary>세션(방) 이름. 채널 하나가 세션 하나다.</summary>
    public const string SessionKey = "-session";

    /// <summary>서버가 열 포트.</summary>
    public const string PortKey = "-port";

    /// <summary>기동 모드를 강제로 지정할 때. (server / client / autohostorclient)</summary>
    public const string ModeKey = "-mode";

    /// <summary>
    /// <paramref name="key"/> 다음에 오는 값을 돌려준다. 없으면 <paramref name="fallback"/>.
    ///
    /// 예: <c>-session lobby-ch1</c> → GetString("-session", "기본값") == "lobby-ch1"
    /// </summary>
    public static string GetString(string key, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length - 1; i++)
        {
            if (!string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string value = args[i + 1];

            // 다음 것도 인자면(예: "-session -port 27015") 값이 빠진 것이다.
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith("-", StringComparison.Ordinal))
            {
                Debug.LogWarning($"[FusionLaunchArguments] {key} 뒤에 값이 없습니다. 기본값 \"{fallback}\" 을 씁니다.");
                return fallback;
            }

            return value;
        }

        return fallback;
    }

    /// <summary>포트처럼 숫자를 읽는다. 숫자가 아니면 기본값을 쓰고 경고만 남긴다.</summary>
    public static ushort GetPort(string key, ushort fallback)
    {
        string raw = GetString(key, null);

        if (string.IsNullOrEmpty(raw))
        {
            return fallback;
        }

        if (!ushort.TryParse(raw, out ushort port))
        {
            Debug.LogWarning($"[FusionLaunchArguments] {key} 값 \"{raw}\" 을 포트로 읽지 못했습니다. 기본값 {fallback} 을 씁니다.");
            return fallback;
        }

        return port;
    }

    /// <summary>이동 위치를 로그로 남길지. 창이 없는 서버의 동기화를 확인할 때 쓴다.</summary>
    public const string LogMovesKey = "-logmoves";

    /// <summary>값 없이 있기만 하면 되는 인자인지 본다. (예: <c>-logmoves</c>)</summary>
    public static bool HasFlag(string key)
    {
        string[] args = Environment.GetCommandLineArgs();

        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], key, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 이 프로세스가 Dedicated Server 인가.
    ///
    /// 카메라·오디오처럼 <b>씬에 이미 들어 있는</b> 것들을 서버에서만 꺼야 할 때 쓴다.
    /// <see cref="FusionLauncher"/> 의 모드 결정과 같은 기준이다.
    ///   1. 커맨드라인 <c>-mode server</c>
    ///   2. Dedicated Server 빌드(UNITY_SERVER)
    /// </summary>
    public static bool IsDedicatedServerProcess()
    {
        string raw = GetString(ModeKey, null);

        if (!string.IsNullOrEmpty(raw))
        {
            // -mode 를 명시했으면 그 값이 빌드 종류를 이긴다. client 로 강제한 서버 빌드도 있을 수 있다.
            return string.Equals(raw, "server", StringComparison.OrdinalIgnoreCase);
        }

#if UNITY_SERVER
        return true;
#else
        return false;
#endif
    }

    /// <summary>지금 실행에 붙은 인자를 한 줄로 남긴다. 서버 로그 맨 앞에 찍어두면 원인 추적이 쉽다.</summary>
    public static string Describe()
    {
        string[] args = Environment.GetCommandLineArgs();
        return args.Length <= 1 ? "(인자 없음)" : string.Join(" ", args, 1, args.Length - 1);
    }
}
