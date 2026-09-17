using System.Collections.Generic;
using System.Text;
using Fusion;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UnderTheSea.Diagnostics
{
    /// <summary>
    /// **임시 계측.** 1초마다 한 줄씩 남긴다. 기본은 꺼져 있고 <c>-perfstat</c> 로만 켠다.
    ///
    /// 미니게임에서 Lobby 로 돌아온 직후 조작이 밀리는 이유를 <b>세 가지로 갈라 보기</b> 위한 것이다.
    ///
    /// <code>
    ///   틱 밀림   tick 이 초당 몇 칸 나갔는가. 64 면 정상, 64 를 크게 넘으면 밀린 것을 몰아서 따라잡는 중
    ///   연결      rtt 가 얼마이고 흔들리는가. runners 가 2 이상이면 Runner 가 겹친 것
    ///   프레임    fps 와 worst(그 1초 중 가장 오래 걸린 프레임). gc 는 그 사이 수거 횟수
    /// </code>
    ///
    /// 셋 다 정상인데도 느리면 남는 설명은 <b>예측 이동이 없다</b>는 구조 쪽이다.
    ///
    /// ⚠ 이 파일은 원인을 가린 뒤 지운다. 지울 때 지장이 없도록 아무도 참조하지 않는다.
    /// </summary>
    public sealed class _NetPerfProbe : MonoBehaviour
    {
        public const string Key = "-perfstat";

        /// <summary>몇 초마다 한 줄 남길지.</summary>
        private const float Every = 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (!FusionLaunchArguments.HasFlag(Key)) return;

            GameObject host = new GameObject("_NetPerfProbe");
            host.AddComponent<_NetPerfProbe>();
            DontDestroyOnLoad(host);

            Debug.Log($"[계측] 켜졌습니다. 1초마다 한 줄 남깁니다. ({Key})");
        }

        private float since;
        private int frames;
        private float worst;
        private int gcAt;
        private int tickAt = -1;
        private string sceneAt = "";

        private readonly StringBuilder line = new StringBuilder(256);

        private void Awake()
        {
            gcAt = System.GC.CollectionCount(0);
        }

        private void Update()
        {
            frames++;

            // ⚠ unscaled 로 잰다. timeScale 이 1 이 아닐 때도 진짜 멈춘 시간을 보기 위해서다.
            float dt = Time.unscaledDeltaTime;
            if (dt > worst) worst = dt;

            since += dt;
            if (since < Every) return;

            Report();

            since = 0f;
            frames = 0;
            worst = 0f;
        }

        private void Report()
        {
            line.Clear();
            line.Append("[계측] ");

            // ---- 프레임 ----
            line.Append("fps ").Append(Mathf.RoundToInt(frames / since));
            line.Append(" | worst ").Append((worst * 1000f).ToString("0")).Append("ms");

            int gcNow = System.GC.CollectionCount(0);
            line.Append(" | gc ").Append(gcNow - gcAt);
            gcAt = gcNow;

            // ---- Runner ----
            NetworkRunner runner = null;
            int running = 0;

            foreach (NetworkRunner candidate in NetworkRunner.Instances)
            {
                if (candidate == null || !candidate.IsRunning) continue;
                running++;
                if (runner == null) runner = candidate;
            }

            line.Append(" | runners ").Append(running);

            if (runner == null)
            {
                line.Append(" | (Runner 없음)");
            }
            else
            {
                // ---- 틱 ----
                //
                // Tick 은 서버가 확정한 시뮬레이션 칸 번호다. 1초 동안 몇 칸 나갔는지 보면
                // 설정값(보통 64)을 지키는지, 밀린 것을 몰아 따라잡는지가 그대로 드러난다.
                int tickNow = runner.Tick.Raw;

                if (tickAt >= 0)
                {
                    int moved = tickNow - tickAt;
                    line.Append(" | tick +").Append(moved);

                    // 설정값에서 크게 벗어난 구간만 눈에 띄게 표시한다.
                    if (moved > 80) line.Append(" ◀따라잡는중");
                    else if (moved < 50) line.Append(" ◀밀림");
                }

                tickAt = tickNow;

                // ---- 연결 ----
                //
                // 클라이언트는 PlayerRef.None 으로 물으면 서버까지의 왕복 시간이 나온다.
                double rtt = runner.GetPlayerRtt(PlayerRef.None);
                line.Append(" | rtt ").Append((rtt * 1000d).ToString("0")).Append("ms");

                line.Append(" | ").Append(runner.IsServer ? "서버" : "클라");
            }

            // ---- 씬 ----
            //
            // 돌아온 뒤에도 미니게임 씬이 남아 돌고 있으면 여기서 드러난다.
            int loaded = SceneManager.sceneCount;
            string names = NamesOfLoadedScenes();

            if (names != sceneAt)
            {
                line.Append(" | 씬 ").Append(loaded).Append("개 → ").Append(names);
                sceneAt = names;
            }
            else
            {
                line.Append(" | 씬 ").Append(loaded).Append("개");
            }

            Debug.Log(line.ToString());
        }

        private static string NamesOfLoadedScenes()
        {
            List<string> names = new List<string>();

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded) names.Add(scene.name);
            }

            return string.Join(", ", names);
        }
    }
}
