namespace AraAtti.Api.Contracts;

/// <summary>
/// 광산 결과 한 줄 평 요청. 광산 데디케이티드 서버가 판이 끝날 때 한 번 보낸다.
///
/// ⚠ Unity 의 Mine.Net.MineReviewRequest 와 필드 이름이 같아야 한다.
///    (JSON 으로는 targetName · score · success · imagePngBase64)
///
/// ⚠ 점수와 승패는 이미 정해진 것을 알려 주기만 한다. AI 가 판정하지 않는다. (MINE.md 7장)
/// </summary>
/// <param name="TargetName">목표 도안 이름. 예: "하트"</param>
/// <param name="Score">0~100 점수.</param>
/// <param name="Success">성공했는가.</param>
/// <param name="ImagePngBase64">우리가 판 그림. 판 칸 검정 · 안 판 칸 흰색인 PNG 를 base64 로.</param>
public sealed record MineReviewRequest(string TargetName, int Score, bool Success, string ImagePngBase64);

/// <summary>한 줄 평. 성적표 가운데 칸에 그대로 뜬다.</summary>
public sealed record MineReviewResponse(string Comment);
