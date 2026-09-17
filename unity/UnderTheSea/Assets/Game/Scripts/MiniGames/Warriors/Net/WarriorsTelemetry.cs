using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Warriors.Net
{
    /// <summary>
    /// **측정 로그.** 밸런스를 감이 아니라 숫자로 고치기 위한 기록이다.
    ///
    /// 왜 필요한가. 지금까지 "무적 같다" · "1라운드가 15에서 끝난다" 같은 보고를 확인할 수 없었다.
    /// 2·3라운드는 피해를 로그로 남기지만 <b>1라운드에는 아무 기록이 없어</b>, 피해가 0이었는지
    /// 로그가 없었을 뿐인지 구분할 방법이 없었다. 여기서 한곳에 모은다.
    ///
    /// <code>
    ///   빌드 식별   서버와 클라이언트가 다른 빌드인지 바로 보인다
    ///   매치 시작   Crew · 세 라운드 목표 · 제한 시간
    ///   라운드      시작 · 종료 시각과 길이
    ///   스윙        사람마다 전체 · 정답 · 오답 · 빗나감, IoT/키보드 구분
    ///   피해        받은 피해 · Down · 구조 성공과 실패
    ///   빈 시간     입력이 없던 구간
    /// </code>
    ///
    /// ⚠ <b>출시 빌드에서는 조용하다.</b> <c>DEVELOPMENT_BUILD</c> 나 에디터에서만 찍는다.
    ///    그 밖에서는 <see cref="Enabled"/> 가 false 라 문자열조차 만들지 않는다.
    /// </summary>
    public static class WarriorsTelemetry
    {
        /// <summary>
        /// 켜져 있는가. **출시 빌드에서는 기본이 꺼짐이다.**
        ///
        /// 에디터와 개발 빌드에서는 그냥 켜진다. 그런데 데디케이티드 서버는 릴리스로 빌드하므로
        /// 그것만으로는 <b>정작 판정을 하는 쪽에서 아무것도 남지 않는다.</b> 실측을 하려고
        /// 만든 기능이 실측 때 꺼져 있으면 의미가 없다.
        ///
        /// 그래서 <c>-telemetry</c> 실행 인자로도 켤 수 있게 둔다. 출시 빌드를 그냥 실행하면
        /// 인자가 없으므로 조용하고, 측정할 때만 붙인다.
        /// </summary>
        public static bool Enabled
        {
            get
            {
                if (resolved) return enabled;

                resolved = true;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                enabled = true;
#else
                enabled = false;
#endif

                foreach (string argument in System.Environment.GetCommandLineArgs())
                {
                    if (!string.Equals(argument, "-telemetry", System.StringComparison.OrdinalIgnoreCase)) continue;

                    enabled = true;
                    break;
                }

                return enabled;
            }
        }

        private static bool enabled;
        private static bool resolved;

        /// <summary>
        /// 이 빌드를 알아보는 값. **서버와 클라이언트가 다른 빌드일 때 드러나야 한다.**
        ///
        /// 커밋 해시를 심는 장치가 없으므로 유니티가 주는 것들을 엮는다.
        /// 값이 서로 다르면 둘 중 하나가 오래된 빌드다.
        /// </summary>
        public static string BuildId =>
            $"{Application.version}/{Application.unityVersion}/{Application.buildGUID}";

        private sealed class PlayerStat
        {
            public int Swings;          // 휘두른 횟수
            public int Correct;         // 노트·몬스터 종류가 맞은 것
            public int Wrong;           // 방향은 왔는데 종류가 틀린 것
            public int Missed;          // 아무 대상도 잡지 못한 것
            public int DamageTaken;
            public int Downs;           // 쓰러진 횟수. 되살아나지 않으므로 사실상 0 또는 1
            public int IoTSwings;       // 그중 실제 장치에서 온 것
        }

        private static readonly Dictionary<int, PlayerStat> Stats = new();
        private static readonly Dictionary<int, float> RoundStart = new();

        private static float matchStartTime;

        private static PlayerStat Of(int lane)
        {
            if (!Stats.TryGetValue(lane, out PlayerStat stat))
            {
                stat = new PlayerStat();
                Stats[lane] = stat;
            }

            return stat;
        }

        public static void MatchStarted(int crew, int p1, int p2, int p3, float seconds)
        {
            if (!Enabled) return;

            Stats.Clear();
            RoundStart.Clear();
            matchStartTime = Time.unscaledTime;

            Debug.Log($"[측정] 매치 시작 — 빌드 {BuildId}");
            Debug.Log($"[측정] 인원 {crew} · 목표 1R {p1} / 2R {p2} / 3R {p3} · 제한 {seconds:F0}초");
        }

        public static void RoundStarted(int round)
        {
            if (!Enabled) return;

            RoundStart[round] = Time.unscaledTime;
            Debug.Log($"[측정] {round}라운드 시작 — 매치 경과 {Time.unscaledTime - matchStartTime:F1}초");
        }

        public static void RoundEnded(int round)
        {
            if (!Enabled) return;

            float began = RoundStart.TryGetValue(round, out float value) ? value : Time.unscaledTime;
            Debug.Log($"[측정] {round}라운드 종료 — 길이 {Time.unscaledTime - began:F1}초");
        }

        /// <summary>한 번 휘둘렀다. <paramref name="result"/> 0 정답 · 1 오답 · 2 빗나감.</summary>
        public static void Swing(int lane, int result, bool fromDevice)
        {
            if (!Enabled) return;

            PlayerStat stat = Of(lane);
            stat.Swings++;
            if (fromDevice) stat.IoTSwings++;

            switch (result)
            {
                case 0: stat.Correct++; break;
                case 1: stat.Wrong++; break;
                default: stat.Missed++; break;
            }
        }

        public static void DamageTaken(int lane, int amount)
        {
            if (!Enabled) return;

            Of(lane).DamageTaken += amount;
            Debug.Log($"[측정] {lane + 1}P 피해 {amount} (누적 {Of(lane).DamageTaken})");
        }

        public static void Down(int lane)
        {
            if (!Enabled) return;

            Of(lane).Downs++;
            Debug.Log($"[측정] {lane + 1}P 쓰러짐 (누적 {Of(lane).Downs})");
        }

        // ⚠ 구조 측정(Rescue)은 삭제했다. 구조·부활이 규칙에서 빠졌다.

        /// <summary>판이 끝났을 때 한 번에 요약한다.</summary>
        public static void MatchEnded(bool cleared, int score)
        {
            if (!Enabled) return;

            StringBuilder text = new();
            text.AppendLine($"[측정] 매치 종료 — {(cleared ? "클리어" : "실패")} · 점수 {score} · " +
                            $"전체 {Time.unscaledTime - matchStartTime:F1}초");

            foreach (KeyValuePair<int, PlayerStat> pair in Stats)
            {
                PlayerStat s = pair.Value;
                float accuracy = s.Swings > 0 ? s.Correct / (float)s.Swings * 100f : 0f;

                text.AppendLine(
                    $"[측정] {pair.Key + 1}P 스윙 {s.Swings} (정답 {s.Correct} · 오답 {s.Wrong} · 빗나감 {s.Missed}) " +
                    $"정확도 {accuracy:F0}% · IoT {s.IoTSwings} · 피해 {s.DamageTaken} · " +
                    $"Down {s.Downs}");
            }

            Debug.Log(text.ToString().TrimEnd());
        }
    }
}
