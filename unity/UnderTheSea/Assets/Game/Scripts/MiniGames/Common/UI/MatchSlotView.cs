using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 매칭 화면의 자리 하나. 네 자리가 전부 이 하나를 재사용한다.
    ///
    ///     참가한 사람   초상(제 색) + 이름 + READY (준비 전이면 WAIT, 노란 점)
    ///     빈 자리       실루엣 + "매칭 중..." + WAIT   (카운트다운 중엔 한 단 더 어둡게)
    ///     나            금색 테두리·이름표, 카드 한 단 밝게, 이름 뒤 "(나)"
    ///
    /// 상태 배지는 READY / WAIT 두 단어만 쓴다. 짧아서 카드 폭에 맞고 한눈에 구분된다.
    ///
    /// 빈 자리에 물음표만 두면 "아직 아무도 없다"는 것은 알겠지만 몇 명짜리 판인지가
    /// 눈에 안 들어온다. 실루엣은 자리의 모양을 남기면서도 찬 자리와 확실히 구분된다.
    /// 빈 카드도 카드로 보여야 하므로 완전 검정까지 내리지 않고 어두운 네이비에서 멈춘다.
    ///
    /// 자리마다 색(<see cref="accent"/>)과 초상은 씬을 지을 때 정해 준다.
    /// 캐릭터 외형(<see cref="PlayerEntry.CharacterPresetId"/>)을 초상에 반영하는 일은
    /// 아직 하지 않는다 — 자리는 <see cref="portrait"/> 하나라 나중에 그 스프라이트만 바꾸면 된다.
    /// </summary>
    public sealed class MatchSlotView : MonoBehaviour
    {
        [Header("조각")]
        [SerializeField] private Image card;
        [SerializeField] private Image portraitBack;
        [SerializeField] private Image portrait;
        [SerializeField] private Image portraitFrame;
        [SerializeField] private Image namePlate;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private Image readyPlate;
        [SerializeField] private Image readyDot;
        [SerializeField] private TMP_Text stateText;

        [Header("이 자리의 색")]
        [Tooltip("초상 테두리와 이름표에 쓰는 색. 자리마다 다르게 준다.")]
        [SerializeField] private Color accent = new(.94f, .35f, .38f, 1f);

        // 카드 색은 순백 스프라이트에 얹는다. 원래 어두운 판에 곱하면 무엇을 넣어도
        // 거의 검정이 되어, 네이비 패널 안에서 카드가 구멍처럼 뚫려 보였다.
        [Header("찬 자리")]
        [SerializeField] private Color filledCard = new(.047f, .149f, .286f, 1f);
        [SerializeField] private Color filledBack = new(.082f, .208f, .365f, 1f);
        [SerializeField] private Color readyGreen = new(.25f, .87f, .53f, 1f);
        [Tooltip("자리는 찼지만 아직 준비를 누르지 않은 사람.")]
        [SerializeField] private Color pendingAmber = new(1f, .78f, .35f, 1f);
        [SerializeField] private Color nameOn = new(1f, 1f, 1f, 1f);

        [Header("나 (이 기기의 플레이어)")]
        [Tooltip("내 자리의 테두리·이름표 색. 금색. 빨강은 '문제 있음' 으로 읽혀서 쓰지 않는다.")]
        [SerializeField] private Color localAccent = new(.98f, .80f, .36f, 1f);
        [Tooltip("내 카드는 다른 사람보다 한 단만 밝게. 너무 튀면 다른 READY 카드가 죽는다.")]
        [SerializeField] private Color localCard = new(.070f, .190f, .350f, 1f);

        // 빈 자리도 카드로 보여야 한다. 메인 패널보다 한 단 어두운 네이비까지만 내린다.
        [Header("빈 자리")]
        [SerializeField] private Color emptyCard = new(.027f, .090f, .184f, 1f);
        [SerializeField] private Color emptyBack = new(.043f, .125f, .227f, 1f);
        [Tooltip("빈 자리 초상을 눌러 실루엣으로 만드는 색. 아주 까맣게 누르면 자리가 비었는지 고장인지 헷갈려서, 형태는 읽히는 선에서 멈춘다.")]
        [SerializeField] private Color silhouette = new(.247f, .329f, .451f, 1f);
        [SerializeField] private Color emptyAccent = new(.32f, .42f, .56f, 1f);
        [SerializeField] private Color nameOff = new(.60f, .70f, .83f, 1f);

        /// <summary>사람이 앉아 있는 자리.</summary>
        public void ShowMember(PlayerEntry entry, int slotNumber)
        {
            bool mine = entry.IsLocal;
            Color tint = mine ? localAccent : accent;

            Paint(mine ? localCard : filledCard, filledBack, Color.white, tint, tint);

            if (nameText != null)
            {
                string name = string.IsNullOrEmpty(entry.DisplayName) ? $"P{slotNumber}" : entry.DisplayName;
                nameText.text = mine ? $"{name} (나)" : name;
                nameText.color = nameOn;
            }

            // 들어와 있는 것과 준비를 마친 것은 다르다. 네트워크가 붙으면 준비를 누르기
            // 전까지 시간이 생기므로, 자리는 찼지만 아직 아닌 상태가 보여야 한다.
            bool ready = entry.IsReady;
            Color mark = ready ? readyGreen : pendingAmber;

            if (stateText != null)
            {
                stateText.text = ready ? "READY" : "WAIT";
                stateText.color = mark;
            }

            if (readyDot != null) readyDot.color = mark;
            if (readyPlate != null) readyPlate.color = new Color(.02f, .06f, .12f, .85f);
        }

        /// <summary>
        /// 아직 아무도 없는 자리.
        /// <paramref name="dimmed"/> 는 카운트다운 중 — 이번 판에는 들어오지 않는 자리라 한 단 더 어둡게.
        /// 그래도 카드 윤곽은 남긴다. 완전 검정이면 자리가 비었는지 고장인지 헷갈린다.
        /// </summary>
        public void ShowEmpty(bool dimmed = false)
        {
            float k = dimmed ? .72f : 1f;
            Paint(emptyCard * k, emptyBack * k, silhouette * k, new Color(0f, 0f, 0f, 0f), emptyAccent * k);

            if (nameText != null)
            {
                nameText.text = "매칭 중...";
                nameText.color = nameOff * k;
            }

            if (stateText != null)
            {
                stateText.text = "WAIT";
                stateText.color = nameOff * k;
            }

            if (readyDot != null) readyDot.color = emptyAccent * k;
            if (readyPlate != null) readyPlate.color = new Color(.043f, .086f, .149f, .85f);
        }

        /// <summary>
        /// **이번 판에서는 쓰지 않는 자리.** ✕ 로 막아 둔다.
        ///
        /// 광산은 2 · 3 · 4명 중에 고른다. 3명을 골랐으면 네 번째 칸은 "아직 안 온 사람"
        /// 이 아니라 <b>영영 안 올 자리</b>다. 빈 자리와 같은 모양으로 두면 한 명을 더
        /// 기다리는 줄 알고 안 떠나는 사람이 생긴다.
        ///
        /// ⚠ 칸을 <b>감추지 않고</b> 막는다. 감추면 고른 인원에 따라 판 너비가 들쭉날쭉해져서,
        ///    2명을 고른 광산과 4명을 고른 광산이 아예 다른 화면처럼 보인다.
        /// </summary>
        public void ShowBlocked()
        {
            const float k = .55f;
            Paint(emptyCard * k, emptyBack * k, new Color(0f, 0f, 0f, 0f),
                new Color(0f, 0f, 0f, 0f), emptyAccent * k);

            if (nameText != null)
            {
                nameText.text = "✕";            // ✕
                nameText.color = nameOff * k;
            }

            if (stateText != null)
            {
                stateText.text = string.Empty;
                stateText.color = nameOff * k;
            }

            if (readyDot != null) readyDot.color = new Color(0f, 0f, 0f, 0f);
            if (readyPlate != null) readyPlate.color = new Color(.043f, .086f, .149f, .45f);
        }

        private void Paint(Color cardColour, Color backColour, Color portraitColour,
            Color frameColour, Color plateColour)
        {
            if (card != null) card.color = cardColour;
            if (portraitBack != null) portraitBack.color = backColour;
            if (portrait != null) portrait.color = portraitColour;
            if (namePlate != null) namePlate.color = plateColour;

            // 색 테두리는 찬 자리에서만 켠다. 색이 있는 그림은 곱셈으로 중화할 수 없어서,
            // 빈 자리에 노란 테두리를 어둡게 칠하면 올리브색이 되어 버린다.
            if (portraitFrame != null)
            {
                portraitFrame.enabled = frameColour.a > 0f;
                portraitFrame.color = frameColour;
            }
        }
    }
}
