using TMPro;
using UnityEngine;

/// <summary>
/// 항해가 끝났을 때 결과를 보여주는 화면.
///
/// ShipCoopGame 의 Finished 알림만 듣습니다. 승패 판정과 점수 계산은 하지 않습니다.
/// (SHIPCOOP.md 2장 — 점수는 ShipCoopGame 이 정수 하나로 만들어 준다)
///
/// 사용법
///   1. ShipCoopResult 프리팹을 씬에 올린다.
///   2. 끝. 씬에 ShipCoopGame 이 하나뿐이면 자동으로 찾는다.
/// </summary>
public class ShipCoopResultView : MonoBehaviour
{
    [Header("연결")]
    [Tooltip("비워두면 씬에서 자동으로 찾는다. 씬에 두 개 이상 있을 때만 직접 지정한다.")]
    [SerializeField] private ShipCoopGame game;

    [Header("화면")]
    [Tooltip("결과 패널. 항해 중에는 꺼두고 끝날 때 켠다.")]
    [SerializeField] private GameObject panel;

    [SerializeField] private TextMeshProUGUI titleLabel;
    [SerializeField] private TextMeshProUGUI scoreLabel;

    [Tooltip("진행도와 걸린 시간. 없어도 동작한다.")]
    [SerializeField] private TextMeshProUGUI detailLabel;

    [Header("문구")]
    [SerializeField] private string clearedText = "항해 성공";
    [SerializeField] private string sunkText = "침몰";
    [SerializeField] private string timeOverText = "시간 초과";

    [Header("글자 색")]
    [SerializeField] private Color successColor = new Color(0.55f, 0.90f, 0.65f);
    [SerializeField] private Color failureColor = new Color(0.95f, 0.55f, 0.50f);

    /// <summary>결과 화면이 지금 떠 있는지</summary>
    public bool IsShowing => panel != null && panel.activeSelf;

    private void Awake()
    {
        if (game == null)
        {
            game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        }

        if (game == null)
        {
            Debug.LogError(
                "[ShipCoopResultView] 씬에서 ShipCoopGame 을 찾지 못했습니다. " +
                "Inspector 에서 연결해 주세요.", this);
        }

        Hide();
    }

    private void OnEnable()
    {
        if (game != null)
        {
            game.Finished += HandleFinished;
            game.Restarted += Hide;
        }
    }

    private void OnDisable()
    {
        if (game != null)
        {
            game.Finished -= HandleFinished;
            game.Restarted -= Hide;
        }
    }

    /// <summary>결과 화면을 닫는다. 다시 출항할 때 쓴다.</summary>
    public void Hide()
    {
        if (panel != null) panel.SetActive(false);
    }

    /// <summary>항해가 끝났을 때 ShipCoopGame 이 불러준다.</summary>
    private void HandleFinished(bool success, int score)
    {
        if (panel != null) panel.SetActive(true);

        if (titleLabel != null)
        {
            titleLabel.text = TitleFor(game != null ? game.State : ShipCoopState.Sunk);
            titleLabel.color = success ? successColor : failureColor;
        }

        if (scoreLabel != null)
        {
            scoreLabel.text = score.ToString("N0");
        }

        if (detailLabel != null && game != null)
        {
            // P0 은 "100 %" 처럼 사이에 공백이 들어가므로 직접 붙인다.
            detailLabel.text = $"진행도 {game.Progress01 * 100f:F0}%   ·   {FormatTime(game.Elapsed)}";
        }
    }

    /// <summary>끝난 이유를 화면 문구로 바꾼다.</summary>
    private string TitleFor(ShipCoopState state)
    {
        return state switch
        {
            ShipCoopState.Cleared => clearedText,
            ShipCoopState.Sunk => sunkText,
            ShipCoopState.TimeOver => timeOverText,
            _ => state.ToString(),
        };
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.Max(0, Mathf.FloorToInt(seconds));
        return $"{total / 60}분 {total % 60}초";
    }
}
