using System.Collections.Generic;
using UnderTheSea.Inventory;
using UnityEngine;
using UnityEngine.UI;

namespace UnderTheSea.Lobby
{
    /// <summary>
    /// 오른쪽 위 바다의 심장 조각 보유 수량. <c>[조각 아이콘] X 12</c> 모양으로 그린다.
    ///
    /// <b>값을 계산하지 않는다.</b> <see cref="PlayerInventory.SeaHeartFragment"/> 는 서버가 준 숫자를
    /// 들고만 있는 캐시이고, 여기서는 그대로 받아 그린다. 봉헌 · 보상으로 바뀌면
    /// <see cref="PlayerInventory.Changed"/> 가 올라 다시 그린다.
    ///
    /// 숫자는 글꼴이 아니라 그림 11장(0~9 · X)이다. 자릿수가 늘면 숫자 칸을 더 만든다.
    ///
    /// <code>
    ///   SeaHeartCounter      이 부품이 붙어 있다
    ///     Row                오른쪽 정렬 가로줄 (HorizontalLayoutGroup)
    ///       Icon             조각 그림
    ///       Multiply         X
    ///       Digit0, 1, ...   숫자. 필요한 만큼 만들고 남는 칸은 끈다
    /// </code>
    ///
    /// 만드는 곳: <c>Tools/아라아띠/로비 조각 수량 프리팹 만들기</c> (SeaHeartCounterSetup)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SeaHeartCounterView : MonoBehaviour
    {
        [SerializeField] private RectTransform row;
        [SerializeField] private GameObject content;

        [Tooltip("0 부터 9 까지 순서대로")]
        [SerializeField] private Sprite[] digitSprites = new Sprite[10];

        [Tooltip("숫자 한 칸의 높이. 가로는 그림 비율을 따른다.")]
        [SerializeField] private float digitHeight = 56f;

        private readonly List<Image> digits = new List<Image>();

        private void OnEnable()
        {
            // ⚠ 같은 구독이 쌓이지 않게 떼고 붙인다.
            PlayerInventory.Changed -= Render;
            PlayerInventory.Changed += Render;

            // 미니게임 보상을 받고 로비로 돌아오면 다시 켜진다. 그때 새 값을 묻는다.
            PlayerInventory.RequestRefresh();
            Render();
        }

        private void OnDisable()
        {
            PlayerInventory.Changed -= Render;
        }

        private void Render()
        {
            // 서버 값을 한 번도 못 받았으면 0 을 그리지 않는다. 0 개인 것과 모르는 것은 다르다.
            bool known = PlayerInventory.HasValue;
            if (content != null && content.activeSelf != known)
            {
                content.SetActive(known);
            }

            if (!known || row == null)
            {
                return;
            }

            string text = System.Math.Max(0L, PlayerInventory.SeaHeartFragment).ToString();

            while (digits.Count < text.Length)
            {
                digits.Add(CreateDigit(digits.Count));
            }

            for (int i = 0; i < digits.Count; i++)
            {
                bool used = i < text.Length;
                digits[i].gameObject.SetActive(used);

                if (used)
                {
                    SetSprite(digits[i], digitSprites[text[i] - '0']);
                }
            }
        }

        private Image CreateDigit(int index)
        {
            GameObject go = new GameObject("Digit" + index, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(row, worldPositionStays: false);

            Image image = go.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>숫자마다 폭이 달라서(1 은 좁고 8 은 넓다) 그림을 바꿀 때 칸 폭도 맞춘다.</summary>
        private void SetSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;

            if (sprite == null)
            {
                return;
            }

            float aspect = sprite.rect.width / sprite.rect.height;
            ((RectTransform)image.transform).sizeDelta = new Vector2(digitHeight * aspect, digitHeight);
        }
    }
}
