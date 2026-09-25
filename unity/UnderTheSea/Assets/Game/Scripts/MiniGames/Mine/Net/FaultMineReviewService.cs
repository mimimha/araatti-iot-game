#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// <b>장애 재현용.</b> 한 줄 평이 실패하거나 늦을 때 게임이 멀쩡한지 보려고 만든다. (MINE.md 7장 "장애 재현")
    ///
    /// ⚠ <b>출시 빌드에는 없다.</b> 파일 전체가 <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c> 로 감싸여 있고,
    ///    광산 서버 빌드는 <c>BuildOptions.None</c> 이다. 쓰려면 "광산 서버 빌드 — 한 줄 평 장애 재현용" 으로 만든다.
    ///
    /// ⚠ <b>실행 인자를 줘야만 켜진다.</b> 개발 빌드라도 인자가 없으면 <see cref="MineReviewServices.Active"/> 그대로다.
    ///
    /// <code>
    ///   AraAtti-MineServer.exe ... -minereviewfault timeout
    ///   AraAtti-MineServer.exe ... -minereviewfault delayed -minereviewdelay 20
    ///
    ///   success    1.5초 뒤 문장              TC01
    ///   timeout    5초 뒤 실패(null)          TC03  서버가 5초에 504 를 주는 것과 같은 박자
    ///   empty      1초 뒤 공백 문장            TC04  MineMatchState 의 빈 문장 거르기까지 본다
    ///   httperror  0.5초 뒤 실패(null)        TC05
    ///   delayed    9초 뒤 문장                 TC06 · TC07  성적표(3.5~7초)가 걷힌 뒤에 온다
    /// </code>
    ///
    /// TC02(키 없음)는 이것 없이 진짜 <see cref="HttpMineReviewService"/> 로 본다 — 계정 서버에서 키만 비운다.
    ///
    /// 문장에 요청 시각을 넣는다. 다음 판 성적표에 이전 판 문장이 새어 들었는지 눈으로 가릴 수 있게.
    /// </summary>
    public sealed class FaultMineReviewService : IMineReviewService
    {
        public const string ModeKey = "-minereviewfault";

        /// <summary>기다리는 초를 바꾼다. 주지 않으면 방식마다 정해 둔 값.</summary>
        public const string DelayKey = "-minereviewdelay";

        public enum Mode
        {
            Success,
            Timeout,
            Empty,
            HttpError,
            Delayed,
        }

        private readonly Mode mode;
        private readonly float delay;

        private FaultMineReviewService(Mode mode, float delay)
        {
            this.mode = mode;
            this.delay = delay;
        }

        /// <summary>실행 인자에 <see cref="ModeKey"/> 가 있으면 만든다. 없거나 모르는 값이면 false.</summary>
        public static bool TryCreateFromArgs(out IMineReviewService service)
        {
            service = null;

            string raw = FusionLaunchArguments.GetString(ModeKey, null);
            if (string.IsNullOrWhiteSpace(raw)) return false;

            if (!Enum.TryParse(raw.Trim(), true, out Mode mode))
            {
                Debug.LogWarning($"[MineReviewFault] {ModeKey} \"{raw}\" 을 모릅니다. 장애 재현 없이 평소대로 갑니다. " +
                                 "(success · timeout · empty · httperror · delayed)");
                return false;
            }

            float delay = FusionLaunchArguments.GetInt(DelayKey, -1, -1, 120);
            service = new FaultMineReviewService(mode, delay >= 0 ? delay : DefaultDelay(mode));
            return true;
        }

        private static float DefaultDelay(Mode mode)
        {
            switch (mode)
            {
                case Mode.Timeout: return 5f;
                case Mode.Empty: return 1f;
                case Mode.HttpError: return 0.5f;
                case Mode.Delayed: return 9f;
                default: return 1.5f;
            }
        }

        public IEnumerator Request(MineReviewRequest request, Action<string> onComment)
        {
            string askedAt = DateTime.Now.ToString("HH:mm:ss");

            Debug.LogWarning($"[MineReviewFault] ⚠ 장애 재현 중 — {mode}, {delay:0.0}초 뒤 답합니다. " +
                             $"({request.score}점 · {(request.success ? "성공" : "실패")})");

            yield return new WaitForSeconds(delay);

            switch (mode)
            {
                case Mode.Success:
                case Mode.Delayed:
                    string target = string.IsNullOrEmpty(request.targetName) ? "그림" : request.targetName;
                    onComment?.Invoke($"[테스트 {mode}] {target} · {askedAt} 요청");
                    break;

                case Mode.Empty:
                    onComment?.Invoke("   ");
                    break;

                default:
                    // HttpMineReviewService 가 실패할 때와 같은 모양이다.
                    Debug.LogWarning($"[MineReview] 한 줄 평을 받지 못했습니다. 고정 문구를 씁니다. — [MineReviewFault] {mode}");
                    onComment?.Invoke(null);
                    break;
            }
        }
    }
}
#endif
