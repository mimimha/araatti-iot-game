using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace UnderTheSea.Account
{
    /// <summary>한 번의 HTTP 요청 결과. 성공이면 Body, 실패면 화면에 보여줄 FailureMessage 가 채워진다.</summary>
    internal readonly struct HttpJsonResult
    {
        public readonly bool IsSuccess;
        public readonly long StatusCode;
        public readonly string Body;

        /// <summary>실패 이유. 사용자에게 그대로 보여줄 수 있는 한국어 문구다.</summary>
        public readonly string FailureMessage;

        private HttpJsonResult(bool isSuccess, long statusCode, string body, string failureMessage)
        {
            IsSuccess = isSuccess;
            StatusCode = statusCode;
            Body = body;
            FailureMessage = failureMessage;
        }

        public static HttpJsonResult Success(long statusCode, string body)
        {
            return new HttpJsonResult(true, statusCode, body, string.Empty);
        }

        public static HttpJsonResult Failure(long statusCode, string failureMessage)
        {
            return new HttpJsonResult(false, statusCode, null, failureMessage);
        }
    }

    /// <summary>
    /// JSON 을 주고받는 HTTP 요청 하나를 처리한다.
    ///
    /// 인증 서비스와 캐릭터 서비스가 똑같은 일(요청 보내기 · 오류 해석 · JSON 파싱)을 하므로
    /// 두 벌로 만들지 않고 여기에 모았다.
    ///
    /// 실패는 **언제나 사용자에게 보여줄 수 있는 문구**로 바꿔서 돌려준다.
    /// 그래서 서비스 구현이 UnityWebRequest 의 오류 종류를 다시 해석할 필요가 없다.
    ///
    /// ⚠ 요청 본문과 토큰을 로그로 찍지 않는다. 비밀번호와 토큰이 Console 에 남으면 안 된다.
    /// </summary>
    internal static class HttpJson
    {
        /// <summary>
        /// 응답을 기다리는 최대 시간(초).
        ///
        /// 0(무한)으로 두면 서버가 응답하지 않을 때 화면이 영구히 "확인하는 중..." 에 머문다.
        /// </summary>
        public const int TimeoutSeconds = 10;

        /// <summary>서버의 오류 응답 형태. (server/AraAtti.Api — ErrorResponse)</summary>
        // JsonUtility 가 응답을 읽어 채우는 필드들이다. 코드에서 대입하는 곳이 없으므로
        // "값이 대입되지 않았다"(CS0649) 경고가 나는데, 여기서는 정상이라 끈다.
#pragma warning disable 0649
        [Serializable]
        private class ErrorBody
        {
            public string code;
            public string message;
        }
#pragma warning restore 0649

        /// <summary>
        /// 요청을 보내고 결과를 콜백으로 알린다.
        ///
        /// 코루틴이므로 부르는 쪽에서 <c>yield return HttpJson.Send(...)</c> 로 기다린다.
        /// </summary>
        /// <param name="jsonBody">본문이 없으면 null.</param>
        /// <param name="bearerToken">토큰이 필요 없으면 null.</param>
        public static IEnumerator Send(
            string url,
            string method,
            string jsonBody,
            string bearerToken,
            Action<HttpJsonResult> onComplete)
        {
            using (UnityWebRequest request = new UnityWebRequest(url, method))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                request.timeout = TimeoutSeconds;

                if (!string.IsNullOrEmpty(jsonBody))
                {
                    // 한글이 섞여도 깨지지 않게 UTF-8 로 보낸다.
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json; charset=utf-8");
                }

                request.SetRequestHeader("Accept", "application/json");

                if (!string.IsNullOrEmpty(bearerToken))
                {
                    request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                }

                yield return request.SendWebRequest();

                onComplete?.Invoke(Interpret(request, url));
            }
        }

        private static HttpJsonResult Interpret(UnityWebRequest request, string url)
        {
            string body = request.downloadHandler != null ? request.downloadHandler.text : null;

            if (request.result == UnityWebRequest.Result.Success)
            {
                return HttpJsonResult.Success(request.responseCode, body);
            }

            if (request.result == UnityWebRequest.Result.ConnectionError)
            {
                // 서버가 꺼져 있거나 주소가 틀렸다. 응답 자체가 오지 않은 경우다.
                return HttpJsonResult.Failure(
                    request.responseCode,
                    $"서버에 연결할 수 없습니다. 서버가 실행 중인지 확인해 주세요. ({url})");
            }

            // 서버가 이유를 알려줬으면 그것을 그대로 쓴다. (조건: message 우선)
            string serverMessage = TryReadErrorMessage(body);
            if (!string.IsNullOrWhiteSpace(serverMessage))
            {
                return HttpJsonResult.Failure(request.responseCode, serverMessage);
            }

            return HttpJsonResult.Failure(
                request.responseCode,
                $"요청을 처리하지 못했습니다. (HTTP {request.responseCode})");
        }

        /// <summary>오류 본문에서 message 를 꺼낸다. 형태가 다르면 null.</summary>
        private static string TryReadErrorMessage(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }

            try
            {
                ErrorBody error = JsonUtility.FromJson<ErrorBody>(body);
                return error != null && !string.IsNullOrWhiteSpace(error.message) ? error.message : null;
            }
            catch (Exception)
            {
                // JSON 이 아니다. (예: 개발 서버의 HTML 오류 페이지) 호출한 쪽이 기본 문구를 쓴다.
                return null;
            }
        }

        /// <summary>
        /// 성공 응답 본문을 파싱한다.
        ///
        /// 실패하면 false 를 돌려주고 화면에 보여줄 문구를 채운다.
        /// 파싱 오류로 예외가 화면까지 올라가지 않게 한다.
        /// </summary>
        public static bool TryParse<T>(string body, out T value, out string failureMessage) where T : class
        {
            value = null;
            failureMessage = null;

            if (string.IsNullOrWhiteSpace(body))
            {
                failureMessage = "서버가 빈 응답을 보냈습니다.";
                return false;
            }

            try
            {
                value = JsonUtility.FromJson<T>(body);
            }
            catch (Exception exception)
            {
                // 예외 메시지는 Console 에만 남기고, 화면에는 짧은 문구를 보여준다.
                Debug.LogWarning($"[HttpJson] 응답을 해석하지 못했습니다: {exception.Message}");
                failureMessage = "서버 응답을 해석하지 못했습니다.";
                return false;
            }

            if (value == null)
            {
                failureMessage = "서버 응답을 해석하지 못했습니다.";
                return false;
            }

            return true;
        }
    }
}
