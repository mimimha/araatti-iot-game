using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Profiling.Editor;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace UnderTheSea.Network.Editor
{
    /// <summary>
    /// 프로파일러 캡처 파일(<c>.raw</c>)을 열어 <b>어디서 시간을 쓰는지 표로 뽑는다.</b>
    ///
    /// <b>왜 필요한가.</b> Dedicated Server 는 창이 없어 Profiler 창을 눈으로 보기 번거롭고,
    /// 무엇보다 <b>눈으로 본 인상은 근거로 남지 않는다.</b> 이 도구는 같은 캡처를 몇 번을 돌려도
    /// 같은 숫자를 내고, 결과를 텍스트로 남겨 비교할 수 있다.
    ///
    /// 서버는 <c>-profiler-log-file</c> 로 캡처를 만든다.
    /// <code>
    /// AraAtti-Server.exe -batchmode -nographics -session prof-lobby -port 27015
    ///   -profiler-enable -profiler-log-file &lt;경로&gt;\lobby-capture.raw
    ///   -profiler-capture-frame-count 600
    /// </code>
    ///
    /// ⚠ 이 캡처는 Deep Profile 이 아니다. 즉 <b>우리 스크립트의 개별 메서드는 안 보이고</b>
    ///    PlayerLoop 의 큰 단계(Update · 물리 · 애니메이션 · 렌더링 준비 등)까지만 보인다.
    ///    그게 맞는 순서다 — 먼저 어느 단계가 비싼지 좁히고, 그다음에 필요하면 깊게 판다.
    /// </summary>
    public static class ProfilerCaptureReport
    {
        /// <summary>표에 남길 상위 항목 수. 너무 많으면 읽을 수가 없다.</summary>
        private const int TopCount = 30;

        [MenuItem("Tools/아라아띠/프로파일러 캡처 분석 (.raw 열기)")]
        public static void Analyze()
        {
            string path = EditorUtility.OpenFilePanel("프로파일러 캡처 고르기", "", "raw");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            AnalyzeFile(path);
        }

        /// <summary>
        /// 경로를 직접 받아 분석한다. 메뉴를 거치지 않고 부를 수 있게 공개해 둔다.
        /// </summary>
        public static void AnalyzeFile(string path)
        {
            if (!File.Exists(path))
            {
                Debug.LogError($"[프로파일 분석] 파일이 없습니다 — {path}");
                return;
            }

            // ⚠ 지금 열려 있는 프로파일 데이터를 덮어쓴다. 캡처를 불러오는 유일한 길이다.
            if (!ProfilerDriver.LoadProfile(path, false))
            {
                Debug.LogError(
                    $"[프로파일 분석] 캡처를 열지 못했습니다 — {path}\n" +
                    "Unity 버전이 다른 곳에서 만든 캡처이거나 파일이 손상됐을 수 있습니다.");
                return;
            }

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;

            if (first < 0 || last < first)
            {
                Debug.LogError("[프로파일 분석] 캡처에 프레임이 없습니다.");
                return;
            }

            // 앞쪽 프레임은 기동 중이라 대표성이 없다. 뒤쪽 절반만 본다.
            int from = first + (last - first) / 2;

            // ⚠ **메인 스레드만 보면 안 된다.**
            //    처음엔 스레드 0 만 읽었는데, 그 합이 0.21코어분인데 실측 CPU 는 1.70코어였다.
            //    나머지가 워커 스레드에 있었다. 그래서 모든 스레드를 훑는다.
            var perThread = new Dictionary<string, Accumulated>(StringComparer.Ordinal);
            var totals = new Dictionary<string, Accumulated>(StringComparer.Ordinal);
            int counted = 0;
            double frameTimeSum = 0d;
            int threadCount = 0;

            using (var probe = new ProfilerFrameDataIterator())
            {
                threadCount = probe.GetThreadCount(from);
            }

            for (int frame = from; frame <= last; frame++)
            {
                bool countedThisFrame = false;

                for (int thread = 0; thread < threadCount; thread++)
                {
                    using HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(
                        frame,
                        thread,
                        HierarchyFrameDataView.ViewModes.Default,
                        (int)HierarchyFrameDataView.columnTotalTime,
                        sortAscending: false);

                    if (view == null || !view.valid) continue;

                    if (!countedThisFrame)
                    {
                        countedThisFrame = true;
                        counted++;
                        frameTimeSum += view.frameTimeMs;
                    }

                    // 스레드 이름은 비어 있을 수 있다. 그때는 번호로 구분한다.
                    string threadName = view.threadName;
                    if (string.IsNullOrEmpty(threadName)) threadName = $"(이름 없음 #{thread})";
                    string key = $"{thread:D2} {threadName}";

                    if (!perThread.TryGetValue(key, out Accumulated t))
                    {
                        t = new Accumulated();
                        perThread[key] = t;
                    }

                    int root = view.GetRootItemID();
                    t.TotalMs += view.GetItemColumnDataAsSingle(root, HierarchyFrameDataView.columnTotalTime);

                    Collect(view, root, totals, depthLimit: 4);
                }
            }

            if (counted == 0)
            {
                Debug.LogError("[프로파일 분석] 읽을 수 있는 프레임이 없었습니다.");
                return;
            }

            Report(path, counted, frameTimeSum, threadCount, perThread, totals);
        }

        /// <summary>한 프레임의 트리를 훑어 항목별 self/total 시간을 더한다.</summary>
        private static void Collect(
            HierarchyFrameDataView view, int itemId, Dictionary<string, Accumulated> totals, int depthLimit)
        {
            var children = new List<int>();
            view.GetItemChildren(itemId, children);

            foreach (int child in children)
            {
                string name = view.GetItemName(child);

                if (!totals.TryGetValue(name, out Accumulated acc))
                {
                    acc = new Accumulated();
                    totals[name] = acc;
                }

                acc.TotalMs += view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnTotalTime);
                acc.SelfMs += view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnSelfTime);
                acc.Calls += (int)view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnCalls);

                if (depthLimit > 1)
                {
                    Collect(view, child, totals, depthLimit - 1);
                }
            }
        }

        private static void Report(
            string path, int frames, double frameTimeSum, int threadCount,
            Dictionary<string, Accumulated> perThread, Dictionary<string, Accumulated> totals)
        {
            var text = new StringBuilder();

            text.AppendLine($"[프로파일 분석] {Path.GetFileName(path)}");
            text.AppendLine($"  분석한 프레임 {frames}개, 스레드 {threadCount}개, " +
                            $"평균 프레임 시간 {frameTimeSum / frames:F3} ms");
            text.AppendLine();

            // 스레드별 총합을 먼저 낸다. 어느 스레드가 비싼지가 제일 먼저 알아야 할 것이다.
            text.AppendLine($"  {"스레드",-44} {"프레임당 ms",12}");
            text.AppendLine($"  {new string('-', 44)} {new string('-', 12)}");

            foreach (KeyValuePair<string, Accumulated> row in perThread
                         .OrderByDescending(p => p.Value.TotalMs)
                         .Take(15))
            {
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0,-44} {1,12:F3}", Trim(row.Key, 44), row.Value.TotalMs / frames));
            }

            text.AppendLine();
            text.AppendLine($"  {"항목",-46} {"self ms",10} {"total ms",10} {"호출",8}");
            text.AppendLine($"  {new string('-', 46)} {new string('-', 10)} {new string('-', 10)} {new string('-', 8)}");

            foreach (KeyValuePair<string, Accumulated> row in totals
                         .OrderByDescending(p => p.Value.SelfMs)
                         .Take(TopCount))
            {
                text.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "  {0,-46} {1,10:F3} {2,10:F3} {3,8:F0}",
                    Trim(row.Key, 46),
                    row.Value.SelfMs / frames,
                    row.Value.TotalMs / frames,
                    (double)row.Value.Calls / frames));
            }

            string outPath = Path.ChangeExtension(path, ".분석.txt");
            File.WriteAllText(outPath, text.ToString(), Encoding.UTF8);

            Debug.Log(text + $"\n  표를 파일로도 남겼습니다 — {outPath}");
        }

        private static string Trim(string value, int width)
        {
            return value.Length <= width ? value : value.Substring(0, width - 1) + "…";
        }

        private class Accumulated
        {
            public double SelfMs;
            public double TotalMs;
            public int Calls;
        }
    }
}
