using System;

namespace UnderTheSea.Character
{
    /// <summary>
    /// 파츠가 몸의 어느 자리를 차지하는가.
    ///
    /// 원래 <see cref="CharacterCustomizationController"/> 안의 중첩 열거형이었다.
    /// 커마 화면 밖(<see cref="CharacterAppearanceApplier"/> · <see cref="CharacterPartCatalog"/>)에서도
    /// 같은 값을 써야 해서 네임스페이스 수준으로 꺼냈다.
    ///
    /// ⚠ <b>숫자 값을 바꾸지 마라.</b> 프리팹과 카탈로그 에셋에 `slot: 64` 처럼 정수로 직렬화돼 있다.
    ///    값을 바꾸면 이미 저장된 378개 항목의 슬롯이 전부 어긋난다.
    ///
    /// <b>이것은 스냅샷의 `slot` 문자열과 다른 개념이다.</b>
    ///   · <c>WearSlot</c>        — 몸의 자리. 13가지. 파츠끼리 서로를 가리는지 판단할 때 쓴다
    ///   · 스냅샷의 <c>slot</c>   — 커마 화면의 카테고리 이름. 6가지 (Face/Hair/Top/Bottom/Shoes/Accessory)
    ///   한 카테고리가 여러 WearSlot 을 담는다. (예: Accessory 에 Hat · Glasses 가 함께 들어간다)
    ///
    /// 문서: docs/prd/fusion-dedicated-lobby-roadmap.md (PRD 09-1)
    /// </summary>
    [Flags]
    public enum WearSlot
    {
        Face = 1,
        Hair = 2,
        Top = 4,
        Bottom = 8,
        Shoes = 16,
        Glasses = 32,
        Body = 64,
        Ears = 128,
        Gloves = 256,
        Socks = 512,
        Hat = 1024,
        FaceAccessory = 2048,
        Outfit = 4096
    }
}
