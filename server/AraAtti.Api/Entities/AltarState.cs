namespace AraAtti.Api.Entities;

/// <summary>
/// 제단의 전역 상태. <b>행이 정확히 하나</b>입니다 (id = 1).
///
/// 그 한 행은 런타임의 "없으면 만든다" 가 아니라 마이그레이션의 InsertData 로 넣습니다.
/// 런타임에 만들면 두 요청이 동시에 만들려다 부딪힙니다. (설계 문서 STEP 1)
///
/// ⚠ is_open 같은 플래그를 두지 않습니다. 완료 여부는 언제나 두 수치로 판정합니다.
///    (설계 문서 10.7절)
/// </summary>
public class AltarState
{
    /// <summary>언제나 1. 자동 증가가 아닙니다.</summary>
    public int Id { get; set; }

    /// <summary>지금까지 봉헌된 총량. 0 이상 TargetOffering 이하 — DB 의 CHECK 가 지킵니다.</summary>
    public ulong TotalOffered { get; set; }

    /// <summary>목표 봉헌량. 기획값 1000.</summary>
    public uint TargetOffering { get; set; }

    /// <summary>
    /// 제단이 완성된 시각.
    ///
    /// ⚠ 감사·로그용입니다. 완료 판정에 쓰지 않습니다. (설계 문서 11.4.16)
    /// </summary>
    public DateTime? ActivatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }
}
