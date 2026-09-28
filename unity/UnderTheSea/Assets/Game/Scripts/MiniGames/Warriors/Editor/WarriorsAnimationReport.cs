using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🔍 <b>애니메이션 실태 조사.</b> 고치지 않고 <b>보기만</b> 한다.
    ///
    /// 클립이 실제로 어느 뼈를 움직이는지 YAML 로는 알 수 없다. 압축된 커브는 뼈 이름이
    /// 해시로 들어가기 때문이다. 그래서 유니티에게 직접 묻는다.
    ///
    /// "팔을 움직이는 클립인가" 를 눈이 아니라 <b>커브 목록</b>으로 답하기 위한 도구다.
    /// </summary>
    public static class WarriorsAnimationReport
    {
        private const string ControllerPath =
            "Assets/Game/Animations/MiniGames/Warriors/WarriorsCombat.controller";

        [MenuItem("Tools/아라아띠/Warriors 애니메이션 실태 보기")]
        public static void Report()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError($"[애니 조사] 컨트롤러를 찾지 못했습니다 — {ControllerPath}");
                return;
            }

            Debug.Log("[애니 조사] ───── 컨트롤러 상태 → 클립 ─────");

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            Debug.Log($"[애니 조사] 기본 상태: '{machine.defaultState?.name ?? "없음"}'  · 레이어 {controller.layers.Length}개");

            foreach (ChildAnimatorState child in machine.states)
            {
                AnimatorState state = child.state;
                Motion motion = state.motion;

                Debug.Log(
                    $"[애니 조사] 상태 '{state.name}'  속도 {state.speed}  →  " +
                    (motion == null ? "❌ 클립 없음" : $"'{motion.name}'  ({AssetDatabase.GetAssetPath(motion)})"));

                foreach (AnimatorStateTransition t in state.transitions)
                {
                    Debug.Log($"[애니 조사]     나가는 전이 → '{t.destinationState?.name ?? "(없음)"}'  {DescribeConditions(t)}");
                }
            }

            // ⚠ **Any State 전이를 따로 본다.** 공격처럼 어느 상태에서나 튀어나와야 하는 것은
            //    보통 여기에 걸린다. 상태별 전이만 보면 "아무 전이도 없다" 로 잘못 읽는다.
            Debug.Log($"[애니 조사] ───── Any State 전이 {machine.anyStateTransitions.Length}개 ─────");

            foreach (AnimatorStateTransition t in machine.anyStateTransitions)
            {
                Debug.Log($"[애니 조사]  Any → '{t.destinationState?.name ?? "(없음)"}'  {DescribeConditions(t)}");
            }

            Debug.Log("[애니 조사] ───── 후보 클립이 움직이는 뼈 ─────");

            string[] look =
            {
                "Assets/Game/Animations/MiniGames/Warriors/Clips/Warriors_TwoHanded_AttackA.anim",
                "Assets/Game/Animations/MiniGames/Warriors/Clips/Warriors_TwoHanded_AttackB.anim",
                "Assets/Game/Animations/MiniGames/Warriors/Clips/Warriors_TwoHanded_Idle.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Idle_Relaxed.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Attack_Punch_001.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Attack_Kick_001.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Death_Forward.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Death_Backward.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Dodge_Roll.anim",
                "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Crouch_Walk.anim",
            };

            foreach (string path in look) Describe(path);
        }

        public static void ReportFromCommandLine()
        {
            Report();
            EditorApplication.Exit(0);
        }

        /// <summary>전이 하나를 한 줄로. 조건이 없으면 그것도 중요한 사실이다.</summary>
        private static string DescribeConditions(AnimatorStateTransition t)
        {
            string conditions = t.conditions.Length == 0
                ? (t.hasExitTime ? "조건 없음 (재생이 끝나면)" : "⚠ 조건도 종료시간도 없음")
                : string.Join(" 그리고 ", t.conditions.Select(c => $"{c.parameter} {c.mode} {c.threshold}"));

            return $"[{conditions}]  종료시간 {(t.hasExitTime ? t.exitTime.ToString("F2") : "안 씀")}" +
                   $" · 전환 {t.duration:F2}초";
        }

        private static void Describe(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);

            if (clip == null)
            {
                Debug.LogWarning($"[애니 조사] 없음 — {path}");
                return;
            }

            EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

            // ⚠ **휴머노이드 클립은 뼈 경로가 비어 있다.** 몸은 머슬 커브가 움직이고,
            //    그 이름이 propertyName 에 들어간다. path 만 보면 펀치 클립조차
            //    "팔을 안 움직인다" 로 나온다 — 실제로 한 번 그렇게 잘못 읽었다.
            HashSet<string> names = new HashSet<string>();

            foreach (EditorCurveBinding b in bindings)
            {
                names.Add(string.IsNullOrEmpty(b.path) ? b.propertyName : b.path.Split('/').Last());
            }

            // 팔을 움직이는가. 이것이 이 도구의 질문이다.
            string[] armWords = { "arm", "hand", "fore", "shoulder", "elbow" };
            List<string> arms = names.Where(n => armWords.Any(w => n.ToLower().Contains(w))).ToList();

            // 움직임의 크기를 머슬별로 잰다. **가로인지 세로인지는 어느 축이 크게 움직이냐로 갈린다.**
            //   좌우로 베면  In-Out · Front-Back 이 크다
            //   위아래로 베면 Down-Up 이 크다
            List<(string name, float span)> spans = new List<(string, float)>();

            foreach (EditorCurveBinding b in bindings)
            {
                AnimationCurve curve = AnimationUtility.GetEditorCurve(clip, b);
                if (curve == null || curve.length == 0) continue;

                float lo = float.MaxValue, hi = float.MinValue;
                foreach (Keyframe k in curve.keys) { lo = Mathf.Min(lo, k.value); hi = Mathf.Max(hi, k.value); }

                spans.Add((b.propertyName, hi - lo));
            }

            // 오른팔(칼 든 손)만 본다. 왼팔은 두 손 잡이라 따라 움직여 구분에 도움이 안 된다.
            var rightArm = spans
                .Where(s => s.name.StartsWith("Right") &&
                            (s.name.Contains("Arm") || s.name.Contains("Shoulder") || s.name.Contains("Hand")))
                .OrderByDescending(s => s.span)
                .Take(5)
                .Select(s => $"{s.name} {s.span:F2}");

            Debug.Log(
                $"[애니 조사] {clip.name}\n" +
                $"    길이 {clip.length:F2}초 · 휴머노이드 {clip.humanMotion} · 커브 {bindings.Length}개 · 팔 머슬 {arms.Count}개\n" +
                $"    오른팔 큰 순서: {string.Join("  |  ", rightArm)}");
        }
    }
}
