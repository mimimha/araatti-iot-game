using UnityEngine;

namespace UnderTheSea.Character
{
    /// <summary>
    /// 캐릭터 생성 화면이 파츠를 입히고 벗는 부분.
    ///
    /// <b>실제 작업은 <see cref="CharacterAppearanceApplier"/> 가 한다.</b> (PRD 09-1)
    /// 예전에는 이 파일이 파츠 목록(378개)과 뼈 매핑 코드를 직접 들고 있었다.
    /// 그래서 캐릭터 생성 화면이 없으면 외형을 입힐 방법이 없었고,
    /// 로비의 <c>NetworkPlayer</c> 가 같은 일을 하려면 코드를 두 벌로 만들어야 했다.
    ///
    /// 지금 이 파일에 남은 것은 **화면 쪽에서 부르던 이름을 그대로 유지하기 위한 얇은 위임**뿐이다.
    /// 컨트롤러 본체의 호출부를 고치지 않으려고 메서드 이름을 바꾸지 않았다.
    ///
    /// 파츠 목록은 <c>Assets/Game/ScriptableObjects/Character/CharacterPartCatalog.asset</c> 에 있다.
    /// 파츠를 추가했으면 메뉴 <c>Tools > 아라아띠 > 캐릭터 외형 > 파츠 카탈로그 만들기 / 갱신</c> 를 누른다.
    /// </summary>
    public sealed partial class CharacterCustomizationController
    {
        // ── PRD 09-1: 외형 적용은 UI 없는 컴포넌트가 한다 ────────────────────
        //
        // 예전의 catalogParts(378개) · slotBindings(13개) · skinRenderers · skinSourceMaterial 은
        // 모두 Applier 와 카탈로그 에셋으로 옮겼다. 마이그레이션 도구가 프리팹을 배선한다.
        [Header("외형 적용 (PRD 09-1)")]
        [Tooltip("파츠를 실제로 입히는 컴포넌트. 같은 오브젝트에 있다.")]
        [SerializeField] private CharacterAppearanceApplier appearance;

        [Tooltip("파츠 목록 에셋. 화면이 어떤 파츠를 보여줄지 고를 때 쓴다.")]
        [SerializeField] private CharacterPartCatalog catalog;

        /// <summary>
        /// 배선이 빠졌을 때 화면이 조용히 아무것도 안 하는 상황을 막는다.
        ///
        /// 여기서 자동으로 찾아 붙이지 않는다. 슬롯 배선까지는 만들어 줄 수 없어서
        /// "붙긴 했는데 아무것도 안 보이는" 더 헷갈리는 상태가 되기 때문이다.
        /// </summary>
        private bool HasAppearance
        {
            get
            {
                if (appearance != null)
                {
                    return true;
                }

                Debug.LogError(
                    "[CharacterCustomization] 외형 적용 컴포넌트가 연결되지 않았습니다. " +
                    "메뉴 Tools > 아라아띠 > 캐릭터 외형 > 파츠 카탈로그 만들기 / 갱신 을 실행해 주세요.", this);
                return false;
            }
        }

        private bool TryApplyCatalogPart(GameObject prefab)
        {
            return HasAppearance && appearance.TryApplyPart(prefab);
        }

        private bool TryUnequipCatalogPart(GameObject prefab)
        {
            return HasAppearance && appearance.TryUnequipPart(prefab);
        }

        private bool IsCatalogPartEquipped(GameObject prefab)
        {
            return appearance != null && appearance.IsEquipped(prefab);
        }

        private void RefreshCatalogVisibility()
        {
            if (appearance != null)
            {
                appearance.RefreshVisibility();
            }
        }
    }
}
