using System.Collections;
using System.IO;
using UnityEngine;

namespace UnderTheSea.Network
{
    /// <summary>
    /// 🛑 <b>Dedicated Server 를 곱게 끄는 길.</b> 파일 하나가 생기면 스스로 종료한다.
    ///
    /// <code>
    ///   AraAtti-Server.exe -quitfile C:\temp\stop-lobby
    ///   ...
    ///   (끄고 싶을 때)  New-Item C:\temp\stop-lobby
    /// </code>
    ///
    /// <b>왜 필요한가.</b> 창이 없는 서버는 밖에서 종료 신호를 받을 방법이 없다.
    /// <c>MainWindowHandle</c> 이 0 이라 창을 닫을 수도 없고, 다른 콘솔에서 시작한 프로세스라
    /// Ctrl+C 도 닿지 않는다. 그래서 지금까지는 <c>Stop-Process -Force</c> 로 죽였다.
    ///
    /// <b>강제로 죽이면 Photon 이 방을 안 놓아준다.</b> Fusion 은 <c>OnApplicationQuit</c> 에서
    /// <c>Runner.Shutdown()</c> 을 불러 "이 방 닫는다" 를 Photon 에 알린다. 강제 종료는 그
    /// 단계를 건너뛰므로 Photon 은 죽은 상대를 시간으로 알아채야 한다. 그동안 같은 이름으로
    /// 다시 띄우면 이렇게 거절당한다.
    ///
    /// <code>
    ///   GameIdAlreadyExists — A game with the specified id already exist. (32766)
    /// </code>
    ///
    /// 실측으로 <b>5분 넘게</b> 기다린 적이 있다. 하루에 서버를 열 번씩 다시 띄우는 QA 에서는
    /// 그대로 개발 속도가 된다.
    ///
    /// ⚠ <b>인자를 주지 않으면 이 부품은 만들어지지도 않는다.</b> 운영 빌드에 켜진 채로
    ///    남을 일이 없다. 파일 이름을 아는 사람만 끌 수 있으므로 여는 문도 아니다.
    ///
    /// ⚠ 서버에서만 돈다. 클라이언트는 창이 있으니 그냥 닫으면 된다.
    /// </summary>
    public sealed class DedicatedServerQuitWatcher : MonoBehaviour
    {
        /// <summary>이 파일이 생기면 끈다. 값으로 경로를 받는다.</summary>
        public const string Key = "-quitfile";

        /// <summary>얼마나 자주 보는지. 사람이 기다리는 시간이라 1초면 넉넉하다.</summary>
        private const float EverySeconds = 1f;

        private string watched;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (!FusionLaunchArguments.IsDedicatedServerProcess())
            {
                return;
            }

            string path = FusionLaunchArguments.GetString(Key, null);

            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            GameObject host = new GameObject(nameof(DedicatedServerQuitWatcher));
            DontDestroyOnLoad(host);
            host.AddComponent<DedicatedServerQuitWatcher>().watched = path;
        }

        private IEnumerator Start()
        {
            // 지난번에 남은 파일이 있으면 켜자마자 꺼진다. 먼저 치운다.
            Forget();

            Debug.Log($"[서버 종료] '{watched}' 이 생기면 곱게 끕니다. ({Key})");

            WaitForSecondsRealtime wait = new WaitForSecondsRealtime(EverySeconds);

            while (true)
            {
                yield return wait;

                if (!Exists()) continue;

                Debug.Log($"[서버 종료] '{watched}' 을 보았습니다. 세션을 닫고 끝냅니다.");

                // 다음에 다시 띄울 때 또 꺼지지 않게 치우고 나간다.
                Forget();

                // Fusion 이 OnApplicationQuit 에서 Runner.Shutdown() 을 부른다.
                // 그래야 Photon 이 방을 바로 놓아준다.
                Application.Quit();
                yield break;
            }
        }

        private bool Exists()
        {
            try
            {
                return File.Exists(watched);
            }
            catch (IOException)
            {
                // 파일이 만들어지는 중일 수 있다. 다음 차례에 다시 본다.
                return false;
            }
        }

        private void Forget()
        {
            try
            {
                if (File.Exists(watched)) File.Delete(watched);
            }
            catch (IOException error)
            {
                Debug.LogWarning($"[서버 종료] '{watched}' 을 지우지 못했습니다. {error.Message}");
            }
        }
    }
}
