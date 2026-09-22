using UnityEditor;
using UnityEngine;

namespace Warriors.Net.Editor
{
    /// <summary>
    /// 🗡 <b>IoT 검이 게임에 닿을 수 있게 자리를 만들어 둔다.</b>
    ///
    /// 장치 담당자가 붙이는 것은 <b>전송 계층</b>(BLE · UDP · 시리얼)이고, 그것이 부를
    /// 대상은 이미 게임 안에 있어야 한다. 그 대상 둘을 <c>WarriorsGameRoot</c> 에 놓는다.
    ///
    /// <code>
    ///   들어오는 쪽  WarriorsIoTInput        장치 → 게임. 스윙 · IMU 원시값
    ///   나가는 쪽    WarriorsIoTFeedbackHub  게임 → 장치. 진동
    /// </code>
    ///
    /// <b>왜 여기인가.</b> <c>WarriorsGameRoot</c> 는 네트워크 씬(<c>WarriorsNet</c>)과 혼자
    /// 하는 검증 씬 양쪽에 들어가는 프리팹이다. 여기 놓으면 두 경로가 같은 자리를 본다.
    /// 씬마다 따로 놓으면 한쪽만 고치고 마는 일이 생긴다.
    ///
    /// ⚠ <b>가짜 장치를 만들지 않는다.</b> 이 도구는 자리만 만든다. 아무도
    ///    <c>OnSwing</c> 을 부르지 않으면 두 부품은 조용히 있고 키보드만으로 정상 동작한다.
    /// </summary>
    public static class WarriorsIoTSetup
    {
        private const string GameRootPath =
            "Assets/Game/Prefabs/MiniGames/Warriors/Core/WarriorsGameRoot.prefab";

        [MenuItem("Tools/아라아띠/Warriors IoT 자리 만들기")]
        public static void Wire()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(GameRootPath);

            if (root == null)
            {
                Debug.LogError($"[Warriors IoT] 프리팹을 열지 못했습니다 — {GameRootPath}");
                return;
            }

            try
            {
                bool added = false;

                added |= EnsureComponent<WarriorsIoTInput>(root, "장치 → 게임 (스윙 · IMU)");
                added |= EnsureComponent<WarriorsIoTFeedbackHub>(root, "게임 → 장치 (진동)");
                added |= EnsureComponent<WarriorsFakeImuInput>(root, "개발용 가짜 IMU (Alt+J·K·L)");

                if (added) PrefabUtility.SaveAsPrefabAsset(root, GameRootPath);
                else Debug.Log("[Warriors IoT] 이미 둘 다 있습니다. 바꾼 것이 없습니다.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            // ⚠ **넣었다고 믿지 말고 다시 읽어 본다.** (WarriorsAdmissionSetup 과 같은 이유)
            //    저장이 조용히 실패하면 "만들었습니다" 만 남고 장치는 영영 닿지 못한다.
            //
            // ⚠ 다시 읽기 전에 **디스크에 내리고 다시 읽어들인다.** 같은 배치 실행 안에서는
            //    AssetDatabase 가 메모리에 들고 있던 옛 사본을 돌려준다. 그것을 보고
            //    "저장이 되지 않았습니다" 라고 **거짓 경고**를 냈던 적이 있다.
            //    바꾼 것이 없을 때도 확인한다 — 자리가 실제로 있는지가 이 도구의 결론이다.
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(GameRootPath, ImportAssetOptions.ForceUpdate);

            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(GameRootPath);

            bool hasInput = saved != null && saved.GetComponentInChildren<WarriorsIoTInput>(true) != null;
            bool hasHub = saved != null && saved.GetComponentInChildren<WarriorsIoTFeedbackHub>(true) != null;
            bool hasFake = saved != null && saved.GetComponentInChildren<WarriorsFakeImuInput>(true) != null;

            if (!hasInput || !hasHub || !hasFake)
            {
                Debug.LogError(
                    "[Warriors IoT] 저장이 되지 않았습니다. 프리팹을 직접 확인해 주세요. " +
                    $"(입력 {(hasInput ? "있음" : "없음")}, 진동 허브 {(hasHub ? "있음" : "없음")}, " +
                    $"가짜 IMU {(hasFake ? "있음" : "없음")})");
                return;
            }

            Debug.Log("[Warriors IoT] 자리를 만들었습니다 — 입력 · 진동 허브 · 가짜 IMU 모두 확인.");
        }

        public static void WireFromCommandLine()
        {
            Wire();
            EditorApplication.Exit(0);
        }

        /// <summary>없으면 붙인다. 붙였으면 참.</summary>
        private static bool EnsureComponent<T>(GameObject root, string label) where T : Component
        {
            if (root.GetComponentInChildren<T>(true) != null)
            {
                Debug.Log($"[Warriors IoT] {typeof(T).Name} 은 이미 있습니다. ({label})");
                return false;
            }

            root.AddComponent<T>();
            Debug.Log($"[Warriors IoT] {typeof(T).Name} 을 붙였습니다. ({label})");
            return true;
        }
    }
}
