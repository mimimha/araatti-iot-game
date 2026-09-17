using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 채팅 목록의 한 줄. 시안처럼 **칸을 나눠** 세로로 줄을 맞춘다.
///
/// <code>
///   ●  지훈     광장에 모여요!              19:24
///   ●  서연     심장 제단 같이 할 분?        19:24
///   점  이름칸   내용(남는 자리 전부)         시각칸
/// </code>
///
/// <b>왜 한 줄로 이어 붙이지 않는가.</b> 이름 길이가 저마다 달라서
/// 내용이 시작하는 자리가 들쭉날쭉해진다. 이름 칸을 고정하면 눈이 한 줄기로 내려간다.
///
/// 내용만 <see cref="LayoutElement.flexibleWidth"/> 를 갖는다. 이름과 시각은 고정이고
/// 남는 자리를 내용이 전부 가져간다. 그래서 창을 넓히면 내용 칸만 넓어진다.
/// </summary>
public class LobbyChatLine : MonoBehaviour
{
    [SerializeField] private Image dot;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text messageLabel;
    [SerializeField] private TMP_Text timeLabel;

    /// <summary>
    /// 사람마다 다른 색. 같은 이름이면 언제나 같은 색이 나온다.
    ///
    /// 서버가 색을 정해 보내지 않아도 되고, 늦게 들어온 사람도 같은 색으로 본다.
    /// </summary>
    private static readonly Color[] Palette =
    {
        new Color(0.45f, 0.85f, 0.55f),   // 초록
        new Color(0.40f, 0.72f, 0.98f),   // 하늘
        new Color(0.98f, 0.70f, 0.35f),   // 주황
        new Color(0.92f, 0.52f, 0.78f),   // 분홍
        new Color(0.72f, 0.62f, 0.98f),   // 보라
        new Color(0.98f, 0.85f, 0.40f),   // 노랑
    };

    /// <summary>줄 하나를 채운다.</summary>
    public void Set(string speaker, string message, string time)
    {
        Color color = ColorFor(speaker);

        if (dot != null)
        {
            dot.color = color;
        }

        if (nameLabel != null)
        {
            nameLabel.text = speaker;
            nameLabel.color = color;
        }

        if (messageLabel != null)
        {
            messageLabel.text = message;
        }

        if (timeLabel != null)
        {
            timeLabel.text = time;
        }
    }

    /// <summary>
    /// 이름으로 색을 고른다. 머리 위 말풍선도 같은 색을 써야 해서 밖에서 부를 수 있다.
    /// </summary>
    public static Color ColorFor(string speaker)
    {
        if (string.IsNullOrEmpty(speaker))
        {
            return Palette[0];
        }

        // 이름의 글자를 더해 고른다. 같은 이름이면 늘 같은 자리가 나온다.
        int sum = 0;

        foreach (char c in speaker)
        {
            sum += c;
        }

        return Palette[Mathf.Abs(sum) % Palette.Length];
    }
}
