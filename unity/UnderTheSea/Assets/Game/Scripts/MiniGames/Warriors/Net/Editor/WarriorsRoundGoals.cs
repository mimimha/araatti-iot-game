using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🎯 <b>라운드 목표 수치를 씬에 박는다.</b>
    ///
    /// 이 값들은 코드 기본값이 아니라 <b>씬에 저장된 인스펙터 값</b>이다. 소스의
    /// <c>= 15</c> 같은 초기값을 고쳐도 이미 저장된 씬은 따라오지 않는다. 실제로
    /// 소스는 15 인데 화면에는 34 가 떠 있었다 — 씬에 20 이 박혀 있었기 때문이다.
    ///
    /// <b>1라운드 목표는 인원으로 계산된다.</b>
    /// <code>
    ///   목표 = phase1TargetKills + (인원 - 1) * phase1KillsPerExtraPlayer
    /// </code>
    /// 그래서 "2인에서 50" 을 만들려면 두 값을 함께 정해야 한다. 한쪽만 바꾸면
    /// 1인 플레이가 터무니없어진다.
    /// </summary>
    public static class WarriorsRoundGoals
    {
        private const string ScenePath = "Assets/Game/Scenes/Main/MiniGames/WarriorsNet.unity";

        /// <summary>혼자 할 때의 1라운드 처치 목표.</summary>
        private const int Phase1Base = 30;

        /// <summary>사람이 한 명 늘 때마다 더할 처치 수. 2인이면 30 + 20 = 50.</summary>
        private const int Phase1PerExtra = 20;

        /// <summary>정원(2인)에서 나와야 하는 값. 검증에 쓴다.</summary>
        private const int ExpectedAtFullCrew = 50;

        /// <summary>3라운드 리듬 성공 목표, 한 사람 몫(× 인원). 16 → 14.</summary>
        private const int Phase3PerPlayer = 14;

        [MenuItem("Tools/아라아띠/Warriors 라운드 목표 맞추기")]
        public static void Wire()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            WarriorsMatchState match = Object.FindAnyObjectByType<WarriorsMatchState>(FindObjectsInactive.Include);

            if (match == null)
            {
                Debug.LogError("[라운드 목표] WarriorsMatchState 를 찾지 못했습니다.");
                return;
            }

            SerializedObject so = new SerializedObject(match);

            SerializedProperty baseKills = so.FindProperty("phase1TargetKills");
            SerializedProperty perExtra = so.FindProperty("phase1KillsPerExtraPlayer");

            if (baseKills == null || perExtra == null)
            {
                Debug.LogError("[라운드 목표] 목표 항목을 찾지 못했습니다. 이름이 바뀌었는지 확인하세요.");
                return;
            }

            Debug.Log(
                $"[라운드 목표] 바꾸기 전 — 혼자 {baseKills.intValue} · 한 명당 +{perExtra.intValue} " +
                $"→ 2인 {baseKills.intValue + perExtra.intValue}");

            baseKills.intValue = Phase1Base;
            perExtra.intValue = Phase1PerExtra;

            // 3라운드 한 사람 몫. 16 은 실측에서 "조금 길다" — 14 로 (2인 28 · 약 40초).
            SerializedProperty rhythm = so.FindProperty("phase3TargetRhythmHits");
            if (rhythm != null) rhythm.intValue = Phase3PerPlayer;

            so.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(match);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            // ⚠ 저장을 믿지 않고 씬을 다시 열어 확인한다.
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            WarriorsMatchState again = Object.FindAnyObjectByType<WarriorsMatchState>(FindObjectsInactive.Include);
            SerializedObject check = new SerializedObject(again);

            int savedBase = check.FindProperty("phase1TargetKills").intValue;
            int savedExtra = check.FindProperty("phase1KillsPerExtraPlayer").intValue;
            int atTwo = savedBase + savedExtra;

            if (savedBase != Phase1Base || savedExtra != Phase1PerExtra || atTwo != ExpectedAtFullCrew)
            {
                Debug.LogError(
                    $"[라운드 목표] 저장되지 않았습니다 — 혼자 {savedBase}(바라던 {Phase1Base}) · " +
                    $"한 명당 +{savedExtra}(바라던 {Phase1PerExtra}) · 2인 {atTwo}(바라던 {ExpectedAtFullCrew})");
                return;
            }

            Debug.Log($"[라운드 목표] ✅ 1라운드 — 혼자 {savedBase} · 2인 {atTwo}");
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }
    }
}
