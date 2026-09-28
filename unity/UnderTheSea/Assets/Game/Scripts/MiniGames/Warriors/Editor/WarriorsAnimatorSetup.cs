using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🎬 <b>빠져 있던 동작 두 가지를 애니메이터에 넣는다.</b> 찌르기와 쓰러짐.
    ///
    /// 조사해 보니 컨트롤러에 상태가 <b>셋뿐</b>이었다 — 이동 · 가로베기 · 세로베기.
    /// 그래서 찌르기는 칼만 움직이고(코드가 칼을 직접 돌린다), 쓰러져도 선 채로 있었다.
    ///
    /// <code>
    ///   WarriorsAttackC   찌르기    트리거로 들어가고 끝나면 이동으로 돌아온다
    ///   WarriorsDown      쓰러짐    복제되는 IsDown 을 보고 들어가고, 풀리면 나온다
    /// </code>
    ///
    /// ⚠ <b>쓰러짐은 트리거가 아니라 bool 이다.</b> 쓰러진 상태는 한 순간이 아니라 계속이고,
    ///    판이 다시 시작되면 풀려야 한다. 트리거로 만들면 [다시 하기] 뒤에도 누워 있게 된다.
    ///
    /// ⚠ <b>쓰는 클립은 프로젝트에 이미 있는 것이다.</b> 새로 만들지 않았다.
    ///    무릎 꿇는 찌르기 클립은 이 프로젝트에 없어서, 팔을 앞으로 뻗는 펀치 클립을 썼다.
    ///    (실측: 어깨 앞뒤 값 폭 3.07 로 이 에셋에서 팔을 가장 크게 앞으로 내미는 클립)
    /// </summary>
    public static class WarriorsAnimatorSetup
    {
        private const string ControllerPath =
            "Assets/Game/Animations/MiniGames/Warriors/WarriorsCombat.controller";

        private const string ThrustClipPath =
            "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Attack_Punch_001.anim";

        private const string DownClipPath =
            "Assets/ithappy/Cute_Characters/Animations/Animations/Skeleton_01_Death_Forward.anim";

        private const string ThrustTrigger = "WarriorsAttackC";
        private const string DownBool = "IsDown";
        private const string ThrustState = "WarriorsAttackC";
        private const string DownState = "WarriorsDown";
        private const string LocomotionState = "TwoHandedLocomotion";

        [MenuItem("Tools/아라아띠/Warriors 찌르기·쓰러짐 애니메이션 넣기")]
        public static void Wire()
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);

            if (controller == null)
            {
                Debug.LogError($"[애니 설정] 컨트롤러를 찾지 못했습니다 — {ControllerPath}");
                return;
            }

            AnimationClip thrustClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(ThrustClipPath);
            AnimationClip downClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(DownClipPath);

            if (thrustClip == null || downClip == null)
            {
                Debug.LogError(
                    $"[애니 설정] 클립을 찾지 못했습니다 — " +
                    $"찌르기 {(thrustClip == null ? "없음" : "있음")}, 쓰러짐 {(downClip == null ? "없음" : "있음")}");
                return;
            }

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = FindState(machine, LocomotionState);

            if (locomotion == null)
            {
                Debug.LogError($"[애니 설정] '{LocomotionState}' 상태를 찾지 못했습니다.");
                return;
            }

            EnsureParameter(controller, ThrustTrigger, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, DownBool, AnimatorControllerParameterType.Bool);

            SwapSlashClips(machine);

            // ── 찌르기 ────────────────────────────────────────────────
            // 가로·세로와 같은 모양으로 만든다. 속도도 같은 3.4 여야 한 판 안에서 리듬이 맞는다.
            AnimatorState thrust = EnsureState(machine, ThrustState, thrustClip, 3.4f);

            if (!HasAnyTransitionTo(machine, thrust))
            {
                AnimatorStateTransition enter = machine.AddAnyStateTransition(thrust);
                enter.AddCondition(AnimatorConditionMode.If, 0f, ThrustTrigger);
                enter.hasExitTime = false;
                enter.duration = 0.06f;

                // ⚠ 쓰러진 뒤에는 찌르기로 들어가지 않는다. 안 막으면 누운 채로 칼을 뻗는다.
                enter.AddCondition(AnimatorConditionMode.IfNot, 0f, DownBool);

                Debug.Log("[애니 설정] Any → 찌르기 전이를 넣었습니다.");
            }

            if (!HasTransition(thrust, locomotion))
            {
                AnimatorStateTransition back = thrust.AddTransition(locomotion);
                back.hasExitTime = true;
                back.exitTime = 0.96f;
                back.duration = 0.08f;
                Debug.Log("[애니 설정] 찌르기 → 이동 전이를 넣었습니다.");
            }

            // ── 쓰러짐 ────────────────────────────────────────────────
            // 속도 1 그대로 둔다. 쓰러지는 것은 빠르게 감을 이유가 없다.
            AnimatorState down = EnsureState(machine, DownState, downClip, 1f);

            if (!HasAnyTransitionTo(machine, down))
            {
                AnimatorStateTransition fall = machine.AddAnyStateTransition(down);
                fall.AddCondition(AnimatorConditionMode.If, 0f, DownBool);
                fall.hasExitTime = false;
                fall.duration = 0.12f;

                // ⚠ 자기 자신으로 다시 들어가지 않게 막는다. 켜져 있는 동안 매 프레임
                //    다시 전이하면 쓰러지는 동작이 첫 프레임에서 계속 되감긴다.
                fall.canTransitionToSelf = false;

                Debug.Log("[애니 설정] Any → 쓰러짐 전이를 넣었습니다.");
            }

            if (!HasTransition(down, locomotion))
            {
                // 판이 다시 시작되면 IsDown 이 풀린다. 그때 일어난다.
                AnimatorStateTransition rise = down.AddTransition(locomotion);
                rise.AddCondition(AnimatorConditionMode.IfNot, 0f, DownBool);
                rise.hasExitTime = false;
                rise.duration = 0.2f;
                Debug.Log("[애니 설정] 쓰러짐 → 이동 전이를 넣었습니다. ([다시 하기] 로 일어난다)");
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(ControllerPath, ImportAssetOptions.ForceUpdate);

            Verify();
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// ⚠ <b>넣었다고 믿지 말고 다시 읽어 본다.</b> 다른 셋업 도구와 같은 이유다.
        /// 이 도구는 한 번 거짓 경고를 낸 적이 있어, 반드시 디스크에서 다시 읽는다.
        /// </summary>
        private static void Verify()
        {
            AnimatorController saved = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            AnimatorStateMachine machine = saved.layers[0].stateMachine;

            foreach (string name in new[] { ThrustState, DownState })
            {
                AnimatorState state = FindState(machine, name);

                if (state == null || state.motion == null)
                {
                    Debug.LogError($"[애니 설정] '{name}' 이 저장되지 않았습니다.");
                    return;
                }

                bool entered = machine.anyStateTransitions.Any(t => t.destinationState == state);
                bool left = state.transitions.Length > 0;

                Debug.Log(
                    $"[애니 설정] ✅ '{name}' → '{state.motion.name}' " +
                    $"(들어오는 전이 {(entered ? "있음" : "❌ 없음")}, 나가는 전이 {(left ? "있음" : "❌ 없음")})");

                if (!entered || !left)
                {
                    Debug.LogError($"[애니 설정] '{name}' 의 전이가 빠졌습니다.");
                    return;
                }
            }

            Debug.Log("[애니 설정] 찌르기·쓰러짐 모두 확인했습니다.");
        }

        /// <summary>
        /// **가로베기와 세로베기가 바뀌어 있었다.** 두 상태의 클립을 맞바꾼다.
        ///
        /// <c>J</c>(가로)를 쳤는데 위에서 내려찍고 <c>K</c>(세로)를 쳤는데 옆으로 베고 있었다.
        /// 클립 파일 이름(<c>AttackA</c> · <c>AttackB</c>)이 실제 동작과 반대로 붙어 있던 것이다.
        ///
        /// ⚠ <b>이것은 눈으로 확인한 사실이다.</b> 두 클립의 머슬 값은 거의 같아
        /// (어깨 Down-Up 1.51 대 1.47) <b>커브만 봐서는 어느 쪽이 가로인지 알 수 없다.</b>
        /// 그래서 재보고 정한 것이 아니라 플레이로 확인한 것을 따랐다.
        ///
        /// 트리거 이름은 그대로 둔다. <c>WarriorsAttackA</c> = 가로베기라는 약속은 코드가 쥐고
        /// 있고, 여기서 바꾸면 코드까지 함께 뒤집어야 해서 되레 헷갈린다.
        ///
        /// ⚠ <b>두 번 실행해도 두 번 바뀌지 않는다.</b> 이미 맞바뀐 상태면 그냥 넘어간다.
        ///    아니면 도구를 돌릴 때마다 좌우가 뒤집혀 "고쳤다 안 고쳤다" 를 반복한다.
        /// </summary>
        private static void SwapSlashClips(AnimatorStateMachine machine)
        {
            AnimatorState a = FindState(machine, "WarriorsAttackA");
            AnimatorState b = FindState(machine, "WarriorsAttackB");

            if (a == null || b == null || a.motion == null || b.motion == null)
            {
                Debug.LogWarning("[애니 설정] 가로·세로 상태를 찾지 못해 맞바꾸지 않았습니다.");
                return;
            }

            // 이미 바뀌어 있는가. 이름이 상태와 어긋나 있으면 그것이 맞바꾼 상태다.
            bool alreadySwapped = a.motion.name.EndsWith("AttackB") && b.motion.name.EndsWith("AttackA");

            if (alreadySwapped)
            {
                Debug.Log("[애니 설정] 가로·세로는 이미 맞바뀌어 있습니다. 그대로 둡니다.");
                return;
            }

            (a.motion, b.motion) = (b.motion, a.motion);

            Debug.Log(
                $"[애니 설정] 가로·세로 클립을 맞바꿨습니다 — " +
                $"가로(WarriorsAttackA) → '{a.motion.name}', 세로(WarriorsAttackB) → '{b.motion.name}'");
        }

        private static AnimatorState FindState(AnimatorStateMachine machine, string name)
        {
            return machine.states.FirstOrDefault(s => s.state.name == name).state;
        }

        private static AnimatorState EnsureState(
            AnimatorStateMachine machine, string name, AnimationClip clip, float speed)
        {
            AnimatorState existing = FindState(machine, name);

            if (existing != null)
            {
                existing.motion = clip;
                existing.speed = speed;
                Debug.Log($"[애니 설정] '{name}' 은 이미 있어 클립만 맞췄습니다 — '{clip.name}'");
                return existing;
            }

            AnimatorState made = machine.AddState(name);
            made.motion = clip;
            made.speed = speed;
            Debug.Log($"[애니 설정] '{name}' 상태를 만들었습니다 — '{clip.name}' (속도 {speed})");
            return made;
        }

        private static void EnsureParameter(
            AnimatorController controller, string name, AnimatorControllerParameterType type)
        {
            if (controller.parameters.Any(p => p.name == name))
            {
                Debug.Log($"[애니 설정] 파라미터 '{name}' 은 이미 있습니다.");
                return;
            }

            controller.AddParameter(name, type);
            Debug.Log($"[애니 설정] 파라미터 '{name}' ({type}) 을 만들었습니다.");
        }

        private static bool HasAnyTransitionTo(AnimatorStateMachine machine, AnimatorState state) =>
            machine.anyStateTransitions.Any(t => t.destinationState == state);

        private static bool HasTransition(AnimatorState from, AnimatorState to) =>
            from.transitions.Any(t => t.destinationState == to);
    }
}
