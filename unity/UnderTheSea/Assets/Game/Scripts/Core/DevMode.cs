using UnityEngine;

namespace UnderTheSea.Core
{
    /// <summary>
    /// **개발자 모드를 써도 되는가.** 세 미니게임의 개발자 패널이 모두 이 하나를 본다.
    ///
    /// <code>
    ///   에디터        항상 켜짐
    ///   빌드          실행 인자 -devmode 가 있을 때만 켜짐   (Release 빌드도 된다)
    /// </code>
    ///
    /// <b>왜 필요한가.</b> 시연은 Release 빌드로 한다. 시연 시간이 짧아 개발자 패널로 판을
    /// 빨리 넘겨야 하는데, 패널을 <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c> 로 감싸 두면
    /// Release 빌드에서는 코드째로 빠져 시연 때 나오지 않는다. 검 게임이 그랬다.
    /// 컴파일 때가 아니라 **실행할 때** 정하면 같은 Release 빌드에서 켜고 끌 수 있다.
    ///
    /// <b>클라이언트와 서버가 둘 다 본다.</b>
    /// <code>
    ///   클라이언트  꺼져 있으면 패널을 만들지 않는다. 키를 눌러도 아무 일이 없다
    ///   서버        꺼져 있으면 개발자 명령 RPC 가 와도 실행하지 않는다
    /// </code>
    /// 클라이언트만 막으면 누구든 <c>게임시작.bat</c> 에 <c>-devmode</c> 를 붙여 열 수 있다.
    /// 서버가 같이 막아야 서버를 <c>-devmode</c> 없이 띄우는 것만으로 확실히 닫힌다.
    ///
    /// ⚠ <b>개발자 명령 RPC 는 <c>#if</c> 로 감싸지 않는다.</b> Fusion 은 RPC 에 컴파일 때 번호를
    ///    매긴다. 한쪽 빌드에만 RPC 가 있으면 클라이언트와 서버의 번호가 어긋나 연결이 깨진다.
    ///    RPC 는 늘 두고, 안에서 <see cref="Enabled"/> 를 본다.
    ///
    /// ⚠ <b>지금은 시연·배포 모두 <c>-devmode</c> 를 붙여 띄운다</b>(2026-09 결정, 우리끼리만 플레이).
    ///    끄려면 <c>tools/deploy/pack-client.ps1</c> 의 <c>게임시작.bat</c> 과
    ///    <c>tools/deploy/deploy-servers.sh</c> 의 서버 실행 인자에서 <c>-devmode</c> 를 빼고
    ///    다시 배포하면 된다. 다시 빌드할 필요는 없다.
    /// </summary>
    public static class DevMode
    {
        /// <summary>개발자 모드를 여는 실행 인자.</summary>
        public const string Key = "-devmode";

        /// <summary>
        /// 개발자 패널을 켜고 끄는 공용 키. 세 게임이 같은 키를 쓴다 — 시연하는 사람이 게임마다
        /// 다른 키를 외우지 않게.
        ///
        /// ⚠ <b>인스펙터 칸이 아니라 코드에 둔다.</b> 패널마다 <c>[SerializeField]</c> 키 칸이 있는데,
        ///    씬에 저장된 값이 코드 기본값보다 우선한다. 검 게임 씬에는 옛 키(`)가 저장돼 있어서
        ///    기본값만 P 로 바꿔서는 P 가 먹지 않았다. 패널은 이 키를 <b>늘 추가로</b> 받는다.
        /// </summary>
        public const UnityEngine.InputSystem.Key PanelKey = UnityEngine.InputSystem.Key.P;

        private static bool? cached;

        /// <summary>
        /// 지금 개발자 모드를 써도 되는가.
        ///
        /// 실행 인자는 프로세스가 도는 동안 바뀌지 않으므로 처음 한 번만 읽는다.
        /// 개발자 패널은 매 프레임 이것을 볼 수 있다.
        /// </summary>
        public static bool Enabled => cached ??= Resolve();

        private static bool Resolve()
        {
#if UNITY_EDITOR
            // 에디터에서는 늘 쓴다. 전에 UNITY_EDITOR 로 감싸던 것과 같은 동작이다.
            return true;
#else
            bool on = FusionLaunchArguments.HasFlag(Key);
            Debug.Log(on
                ? $"[개발자 모드] 켜짐 — {Key} 로 실행됐습니다. 개발자 패널과 명령을 씁니다."
                : $"[개발자 모드] 꺼짐 — {Key} 없이 실행됐습니다. 개발자 패널과 명령을 쓰지 않습니다.");
            return on;
#endif
        }

        /// <summary>
        /// 켜질 때 한 번 읽어 로그에 남긴다.
        ///
        /// 처음 물어볼 때 읽게만 두면, 서버는 개발자 명령이 오기 전까지 읽지 않아서
        /// <b>서버 로그만 보고는 -devmode 로 떴는지 알 수 없다.</b> 배포 뒤 확인할 때 필요하다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Announce()
        {
            _ = Enabled;
        }

        /// <summary>
        /// 플레이 모드에 들어갈 때마다 비운다. 에디터에서 Reload Domain 을 꺼 두면
        /// static 이 이전 플레이의 값을 들고 있다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            cached = null;
        }
    }
}
