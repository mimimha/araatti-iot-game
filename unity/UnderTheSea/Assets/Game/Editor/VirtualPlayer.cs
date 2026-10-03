#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace UnderTheSea.Editor
{
    /// <summary>
    /// **지금 이 에디터가 Multiplayer Play Mode 의 가상 플레이어(클론)인가.**
    ///
    /// <b>왜 알아야 하는가.</b> 클론은 <c>Assets</c> 를 심링크로 공유하는 <b>별도 Unity
    /// 프로젝트</b>다. 그래서 <c>[InitializeOnLoad]</c> 로 도는 에디터 스크립트가 클론에서도
    /// 똑같이 돈다. 그중 <b>에셋을 쓰는</b> 것들이 문제가 된다 — 클론의 AssetDatabase 는
    /// 읽기 전용이라 쓰기에 실패하고, 그 과정에서 이 오류가 쏟아진다.
    ///
    /// <code>
    ///   Asset Database is set to Read Only, but it has found out-of-date assets.
    ///   This should not happen!
    ///      → InvalidOperationException: Failed to load NetworkProjectConfigAsset
    ///      → 클론이 Fusion 세션에 못 들어감
    /// </code>
    ///
    /// <b>막아도 잃는 것이 없다.</b> 이 생성기들이 만드는 에셋(캐릭터 프리팹 · 한글 폰트 ·
    /// TMP 대체 폰트 등록)은 <b>이미 저장소에 들어 있다.</b> 클론은 심링크로 같은 파일을
    /// 읽으므로 다시 만들 필요가 없다.
    ///
    /// ⚠ <b>메뉴로 직접 부르는 것은 막지 않는다.</b> 사람이 일부러 눌렀는데 아무 일도
    ///    안 일어나면 그게 더 혼란스럽다. 자동으로 도는 경로만 물러난다.
    /// </summary>
    internal static class VirtualPlayer
    {
        private static bool resolved;
        private static bool isClone;

        /// <summary>이 에디터가 가상 플레이어면 참.</summary>
        internal static bool IsClone
        {
            get
            {
                if (resolved) return isClone;

                resolved = true;
                isClone = Detect();

                if (isClone)
                {
                    Debug.Log(
                        "[가상 플레이어] 이 에디터는 Multiplayer Play Mode 클론입니다. " +
                        "에셋을 만드는 자동 작업은 건너뜁니다.");
                }

                return isClone;
            }
        }

        /// <summary>
        /// 표식 두 가지를 본다. 둘 중 하나만 맞아도 클론이다.
        ///
        /// <code>
        ///   실행 인자   -vpId=mppm3dd14a4c        MPPM 이 클론을 띄울 때 붙인다
        ///   프로젝트 위치  …/Library/VP/mppm…/     클론의 루트는 여기 안에 있다
        /// </code>
        ///
        /// 둘을 같이 보는 이유는, 한쪽이 Unity 판올림으로 바뀌어도 다른 쪽이 남기 때문이다.
        /// 잘못 판단해서 <b>메인 에디터</b>를 클론으로 보면 생성기가 영영 안 돌아
        /// 새로 받은 사람이 프리팹 없이 시작하게 된다. 그쪽이 더 위험하므로 보수적으로 본다.
        /// </summary>
        private static bool Detect()
        {
            try
            {
                foreach (string arg in Environment.GetCommandLineArgs())
                {
                    if (arg != null && arg.StartsWith("-vpId", StringComparison.OrdinalIgnoreCase)) return true;
                }
            }
            catch
            {
                // 인자를 못 읽어도 아래 경로 검사가 남아 있다.
            }

            string root = Application.dataPath.Replace('\\', '/');
            return root.Contains("/Library/VP/");
        }
    }
}
#endif
