using System;
using System.Collections;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 서버 없이 한 줄 평을 흉내낸다.
    ///
    /// ⚠ HTTP 를 쓰지 않는다. 그림도 보지 않는다 — 도안 이름과 승패만으로 고른다.
    ///    진짜 AI 가 들어올 자리의 <b>흐름</b>(늦게 도착 · 복제 · 성적표 교체)을 확인하는 용도다.
    /// </summary>
    public sealed class FakeMineReviewService : IMineReviewService
    {
        /// <summary>LLM 왕복을 흉내내는 지연(초).</summary>
        private const float ResponseDelay = 1.5f;

        private static readonly string[] SuccessLines =
        {
            "{0}, 누가 봐도 {0}! 광부들 손끝이 예술이네요.",
            "이 정도면 {0} 도감에 올려도 되겠어요.",
            "살짝 삐뚤지만 {0}라고 우기면 통합니다!",
        };

        private static readonly string[] FailLines =
        {
            "{0}라기보단 슬리퍼 같은데요. 그래도 노력은 인정!",
            "{0}를 그리려다 새 생물을 발견하셨네요.",
            "{0}… 아마도요. 곡괭이가 너무 자유로웠어요.",
        };

        public IEnumerator Request(MineReviewRequest request, Action<string> onComment)
        {
            yield return new WaitForSeconds(ResponseDelay);

            string[] lines = request.success ? SuccessLines : FailLines;
            string target = string.IsNullOrEmpty(request.targetName) ? "그림" : request.targetName;

            onComment?.Invoke(string.Format(lines[UnityEngine.Random.Range(0, lines.Length)], target));
        }
    }
}
