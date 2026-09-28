namespace AraAtti.Api.Endpoints;

/// <summary>
/// 광산 한 줄 평에 쓰는 LLM 설정. appsettings 의 "MineReview" 구역을 그대로 받는다.
///
/// OpenAI 호환 형식(POST {BaseUrl}/chat/completions)으로 부른다. 그래서 Gemini 가 아닌
/// 다른 곳(Upstage 등)으로 옮길 때도 BaseUrl · Model · ApiKey 만 바꾸면 된다.
/// ⚠ 단, 그림을 받는 모델이어야 한다. Upstage solar-pro4 는 이미지 입력을 거절한다.
///
/// ⚠ ApiKey 는 커밋되는 파일에 넣지 않는다.
///    appsettings.Development.json(.gitignore 대상) 이나 환경 변수 MineReview__ApiKey 로 넣는다.
///    비어 있으면 API 가 503 을 돌려주고, Unity 는 고정 문구를 쓴다.
/// </summary>
public sealed class MineReviewOptions
{
    public const string SectionName = "MineReview";

    /// <summary>OpenAI 호환 주소. 끝에 /chat/completions 를 붙여 부른다.</summary>
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/openai";

    /// <summary>
    /// 모델 이름. 그림을 받는 가벼운 모델이면 된다.
    ///
    /// ⚠ gemini-2.5-flash 는 목록에는 있어도 새 프로젝트에서 404 가 났다. (2026-09 실측)
    /// </summary>
    public string Model { get; set; } = "gemini-3.5-flash-lite";

    /// <summary>Google AI Studio 에서 받은 키. 커밋되는 파일에 넣지 않는다.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// LLM 을 기다리는 최대 시간(초).
    ///
    /// 성적표는 판이 끝나고 7초 뒤에 사라진다. 그 뒤에 온 답은 아무도 못 본다.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 5;
}
