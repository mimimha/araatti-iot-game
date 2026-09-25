using System;
using System.Collections;

namespace Mine.Net
{
    /// <summary>
    /// 한 줄 평을 달라고 보내는 것. 서버 요청 본문의 필드 이름과 같아서 JsonUtility 가 그대로 쓴다.
    ///
    /// <code>
    ///   POST /api/mine/review
    ///   { "targetName": "물고기", "score": 71, "success": true, "imagePngBase64": "iVBOR..." }
    ///   → 200 { "comment": "물고기라기보단 슬리퍼 같은데요. 그래도 지느러미는 인정합니다." }
    /// </code>
    ///
    /// ⚠ 점수와 승패는 <b>이미 정해진 것을 알려 주기만</b> 한다. AI 가 판정하지 않는다. (MINE.md 7장)
    /// </summary>
    [Serializable]
    public class MineReviewRequest
    {
        /// <summary>목표 도안 이름. <c>MineDrawingTarget.displayName</c>.</summary>
        public string targetName;

        public int score;
        public bool success;

        /// <summary>우리가 판 그림. 판 칸이 검정, 안 판 칸이 흰색인 PNG. (<see cref="MineBoardImage"/>)</summary>
        public string imagePngBase64;
    }

    /// <summary>
    /// 한 줄 평을 받아 오는 경계. <b>데디케이티드 서버만 부른다.</b> 판당 한 번이다.
    ///
    /// ⚠ 실패해도 게임은 멀쩡해야 한다. 실패하면 null 을 돌려주고,
    ///    성적표는 점수 구간별 고정 문구(<c>MineHud.CommentFor</c>)를 그대로 보여 준다.
    /// </summary>
    public interface IMineReviewService
    {
        /// <summary>코루틴이다. 받은 한 줄 평을, 실패했으면 null 을 <paramref name="onComment"/> 로 알린다.</summary>
        IEnumerator Request(MineReviewRequest request, Action<string> onComment);
    }

    /// <summary>
    /// 한 줄 평 서비스를 고른다.
    ///
    /// <b>여기가 가짜 ↔ 진짜를 갈아끼우는 유일한 지점이다.</b> (<c>AccountServiceBootstrap</c> 과 같은 방식)
    ///
    ///     Active = Implementation.Http   계정 서버의 POST /api/mine/review 를 부른다  ← 지금 설정
    ///     Active = Implementation.Fake   서버 없이 1~2초 뒤 그럴듯한 문장을 돌려준다
    ///
    /// ⚠ 계정 서버에 Gemini 키(MineReview:ApiKey)가 없으면 503 이 오고 고정 문구가 뜬다.
    ///    키 없이 흐름만 볼 때는 Fake 로 바꾼다.
    /// </summary>
    public static class MineReviewServices
    {
        public enum Implementation
        {
            Http,
            Fake
        }

        /// <summary>
        /// ★ 지금 쓰는 구현체.
        ///
        /// const 가 아니라 static readonly 인 이유: const 로 두면 아래 분기 중 한쪽이
        /// "도달할 수 없는 코드" 경고를 낸다.
        /// </summary>
        private static readonly Implementation Active = Implementation.Http;

        public static IMineReviewService Create()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            // 장애 재현 — 실행 인자 -minereviewfault 가 있을 때만. 출시 빌드에는 이 줄이 없다. (FaultMineReviewService)
            if (FaultMineReviewService.TryCreateFromArgs(out IMineReviewService fault)) return fault;
#endif
            return Active == Implementation.Http
                ? new HttpMineReviewService()
                : new FakeMineReviewService();
        }
    }
}
