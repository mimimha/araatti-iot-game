using System.IO;
using UnityEditor;
using UnityEngine;

namespace UnderTheSea.MiniGames.ShipCoop.EditorTools
{
    /// <summary>
    /// 🔎 <c>Assets/Game/Audio/ShipCoop/</c> 클립들의 길이 · 앞 무음 · 크기(RMS)를 찍는다.
    /// "소리가 늦게 난다" · "너무 크다" 를 추측 대신 숫자로 볼 때 쓴다. 자산을 바꾸지 않는다.
    ///
    ///     Unity.exe -batchmode -quit -nographics -projectPath &lt;경로&gt; ^
    ///       -executeMethod UnderTheSea.MiniGames.ShipCoop.EditorTools.ShipCoopAudioClipReport.Run
    /// </summary>
    public static class ShipCoopAudioClipReport
    {
        private const string Folder = "Assets/Game/Audio/ShipCoop";
        private const float Threshold = 0.02f;

        [MenuItem("아라아띠/배 협동/소리 클립 재기 (길이 · 앞 무음 · 크기)")]
        public static void Run()
        {
            if (!Directory.Exists(Folder))
            {
                Debug.LogWarning($"[ClipReport] {Folder} 가 없습니다.");
                return;
            }

            foreach (string path in Directory.GetFiles(Folder))
            {
                if (path.EndsWith(".meta") || path.EndsWith(".md")) continue;

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path.Replace('\\', '/'));
                if (clip == null) continue;

                if (clip.loadState != AudioDataLoadState.Loaded) clip.LoadAudioData();

                int count = clip.samples * clip.channels;
                var data = new float[count];

                if (!clip.GetData(data, 0))
                {
                    Debug.Log($"[ClipReport] {clip.name} — 데이터를 못 읽음 (loadType {clip.loadType})");
                    continue;
                }

                int first = count, last = -1, peakAt = 0, attackAt = count;
                double sum = 0;
                float peak = 0f;

                for (int i = 0; i < count; i++)
                {
                    float a = Mathf.Abs(data[i]);
                    if (a >= Threshold) { if (first == count) first = i; last = i; }
                    sum += a * a;
                    if (a > peak) { peak = a; peakAt = i; }
                }

                // 큰 소리가 시작되는 곳 — 피크의 30% 를 처음 넘는 지점. 문 소리는 앞의 스침 뒤에 '탁' 이 온다.
                for (int i = 0; i < count; i++)
                {
                    if (Mathf.Abs(data[i]) >= peak * 0.3f) { attackAt = i; break; }
                }

                float Sec(int idx) => (float)(idx / clip.channels) / clip.frequency;
                float leadIn = first >= count ? clip.length : Sec(first);
                float tail = last < 0 ? 0f : clip.length - Sec(last);
                float rms = Mathf.Sqrt((float)(sum / Mathf.Max(1, count)));

                Debug.Log($"[ClipReport] {clip.name,-12} 길이 {clip.length:F2}s · 앞 무음 {leadIn:F3}s · 큰 소리 시작 {Sec(attackAt):F3}s · 피크 {peak:F2} @ {Sec(peakAt):F3}s · 뒤 무음 {tail:F2}s · RMS {rms:F3} · {clip.frequency}Hz {clip.channels}ch");
            }
        }
    }
}
