using System;
using System.Collections;
using UnderTheSea.Account;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 계정 서버에 한 줄 평을 묻는다. 서버가 LLM 을 부르고 문장만 돌려준다.
    ///
    /// ⚠ <b>API 키를 클라이언트에 두지 않는다.</b> 그래서 LLM 을 직접 부르지 않고 우리 서버를 거친다. (MINE.md 7장)
    ///
    /// ⚠ <b>토큰을 붙이지 않는다.</b> 부르는 쪽이 로그인한 사람이 아니라 데디케이티드 서버라서다.
    ///    대신 서버가 같은 머신(루프백)에서 온 요청만 받는다. 광산 서버와 API 가 떨어지면 403 이 온다.
    ///
    /// 서버 주소는 다른 Http 서비스와 같다 — 실행 인자 <c>-api</c>, 없으면 <c>HttpApiConfig.DefaultBaseUrl</c>.
    /// </summary>
    public sealed class HttpMineReviewService : IMineReviewService
    {
#pragma warning disable 0649
        [Serializable]
        private class ReviewBody
        {
            public string comment;
        }
#pragma warning restore 0649

        public IEnumerator Request(MineReviewRequest request, Action<string> onComment)
        {
            string url = HttpApiConfig.Combine(HttpApiConfig.EffectiveBaseUrl, HttpApiConfig.MineReviewPath);

            HttpJsonResult result = default;
            yield return HttpJson.Send(url, "POST", JsonUtility.ToJson(request), null, r => result = r);

            if (!result.IsSuccess)
            {
                Debug.LogWarning($"[MineReview] 한 줄 평을 받지 못했습니다. 고정 문구를 씁니다. — {result.FailureMessage}");
                onComment?.Invoke(null);
                yield break;
            }

            if (!HttpJson.TryParse(result.Body, out ReviewBody parsed, out string parseFailure)
                || string.IsNullOrWhiteSpace(parsed.comment))
            {
                Debug.LogWarning($"[MineReview] 한 줄 평 응답이 비었습니다. 고정 문구를 씁니다. — {parseFailure}");
                onComment?.Invoke(null);
                yield break;
            }

            onComment?.Invoke(parsed.comment.Trim());
        }
    }
}
