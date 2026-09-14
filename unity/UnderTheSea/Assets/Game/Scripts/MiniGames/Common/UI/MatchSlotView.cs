using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MiniGames.Common.UI
{
    /// <summary>
    /// 매칭 화면의 자리 하나.
    ///
    /// 사람이 앉으면 캐릭터 초상이 제 색으로 켜지고, 빈 자리면 같은 그림을 어두운
    /// 실루엣으로 눕힌다. 빈 자리에 물음표만 두면 "아직 아무도 없다"는 것은 알겠지만
    /// 몇 명짜리 판인지가 눈에 안 들어온다. 실루엣은 자리의 모양을 남기면서도
    /// 찬 자리와 확실히 구분된다.
    ///
    /// 자리마다 색(<see cref="accent"/>)과 초상은 씬을 지을 때 정해 준다.
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
            Paint(filledCard, filledBack, Color.white, accent, accent);

            if (nameText != null)
            {
                nameText.text = entry.IsLocal ? "나" : entry.DisplayName;
                nameText.color = nameOn;
            }

            // 들어와 있는 것과 준비를 마친 것은 다르다. 네트워크가 붙으면 준비를 누르기
            // 전까지 시간이 생기므로, 자리는 찼지만 아직 아닌 상태가 보여야 한다.
            bool ready = entry.IsReady;
            Color mark = ready ? readyGreen : pendingAmber;

            if (stateText != null)
            {
                stateText.text = ready ? "준비 완료" : "대기 중";
                stateText.color = mark;
            }

            if (readyDot != null) readyDot.color = mark;
            if (readyPlate != null) readyPlate.color = new Color(.02f, .06f, .12f, .85f);
        }

        /// <summary>아직 아무도 없는 자리.</summary>
        public void ShowEmpty()
        {
            Paint(emptyCard, emptyBack, silhouette, new Color(0f, 0f, 0f, 0f), emptyAccent);

            if (nameText != null)
            {
                nameText.text = "매칭 중...";
                nameText.color = nameOff;
            }

            if (stateText != null)
            {
                stateText.text = "WAIT";
                stateText.color = nameOff;
            }

            if (readyDot != null) readyDot.color = emptyAccent;
            if (readyPlate != null) readyPlate.color = new Color(.043f, .086f, .149f, .85f);
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
