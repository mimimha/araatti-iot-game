using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 배 협동 게임의 HUD. (SHIPCOOP.md 9장)
///
/// 담는 것
///   - 배 HP 와 남은 시간
///   - 진행도 바 + **지금 있어야 할 위치 기준선**
///   - 왼쪽 사건 알림
///   - 작업 앞에 섰을 때의 상호작용 안내와 게이지
///
/// **왼쪽 사건 알림이 이 게임에서 가장 중요한 UI 입니다.**
/// 지금 무슨 일이 벌어지는지 한눈에 보여야 "누가 무엇을 할지" 판단할 수 있습니다.
///
/// **진행도 바의 기준선은 장식이 아니라 필수 기능입니다.**
/// 남은 시간을 숫자로만 띄우면 늦고 있다는 것을 아무도 알아채지 못합니다.
/// 배 표시가 기준선보다 뒤에 있으면 늦고 있는 것이고, 그때 경고를 띄웁니다.
///
/// 아직 없는 것
///   초상화 4개와 개인 하트는 넣지 않았습니다.
///   배 HP 는 4명이 공유하는 단 하나의 값이고 개인 HP 는 없습니다. (2장)
///   초상화는 플레이어가 실제로 4명 있어야 하므로 네트워크 이후입니다.
/// </summary>
public class ShipCoopHud : MonoBehaviour
{
    /// <summary>사건 알림 한 줄</summary>
    [System.Serializable]
    public class EventRow
    {
        public GameObject root;
        public TextMeshProUGUI label;

        [Tooltip("남은 시간을 보여주는 게이지. Image Type 을 Filled 로 둔다.")]
        public Image timer;
    }

    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private ShipCoopGame game;
    [SerializeField] private ShipHealth health;
    [SerializeField] private ShipVoyage voyage;

    [Header("배 HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI hpLabel;

    [Header("남은 시간")]
    [SerializeField] private TextMeshProUGUI timeLabel;

    [Tooltip("남은 시간이 이 아래로 내려가면 글자 색이 바뀐다. (초)")]
    [SerializeField] private float timeWarningSeconds = 60f;

    [Header("진행도")]
    [Tooltip("지금까지 나아간 만큼 채워진다.")]
    [SerializeField] private Image progressFill;

    [Tooltip("배 표시. 진행도에 따라 좌우로 움직인다.")]
    [SerializeField] private RectTransform shipMarker;

    [Tooltip("지금 있어야 할 위치. 이 선보다 배가 뒤에 있으면 늦고 있다.")]
    [SerializeField] private RectTransform expectedMarker;

    [Tooltip("늦고 있을 때만 켜진다.")]
    [SerializeField] private GameObject behindWarning;

    [Header("사건 알림 (왼쪽)")]
    [Tooltip("동시 발생 상한보다 넉넉하게 준비한다. 남는 줄은 꺼진다.")]
    [SerializeField] private EventRow[] eventRows;

    [Header("상호작용 안내 (아래)")]
    [SerializeField] private GameObject interactPanel;
    [SerializeField] private TextMeshProUGUI interactLabel;

    [Tooltip("작업 진행도. Image Type 을 Filled 로 둔다.")]
    [SerializeField] private Image interactGauge;

    [Header("색")]
    [SerializeField] private Color hpHealthy = new Color(0.45f, 0.85f, 0.55f);
    [SerializeField] private Color hpHurt = new Color(0.95f, 0.75f, 0.35f);
    [SerializeField] private Color hpCritical = new Color(0.95f, 0.40f, 0.35f);
    [SerializeField] private Color timeNormal = Color.white;
    [SerializeField] private Color timeWarning = new Color(0.95f, 0.55f, 0.45f);

    /// <summary>
    /// 화면을 볼 사람. 상호작용 안내가 이 사람 기준으로 뜬다.
    ///
    /// 지금은 씬에서 살아있는 첫 TaskWorker 를 쓴다.
    /// 네트워크가 붙으면 자기 컴퓨터의 로컬 플레이어가 들어온다.
    /// </summary>
    public TaskWorker LocalWorker { get; private set; }

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        if (health == null) health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        if (voyage == null) voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
    }

    private void Update()
    {
        // 사람은 도중에 들어오거나 나갈 수 있다. 매 프레임 확인이 부담될 정도는 아니다.
        if (LocalWorker == null)
        {
            LocalWorker = FindAnyObjectByType<TaskWorker>(FindObjectsInactive.Exclude);
        }

        UpdateHealth();
        UpdateTime();
        UpdateProgress();
        UpdateEvents();
        UpdateInteract();
    }

    private void UpdateHealth()
    {
        if (health == null)
        {
            return;
        }

        float ratio = health.MaxHp <= 0f ? 0f : Mathf.Clamp01(health.CurrentHp / health.MaxHp);

        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
            hpFill.color = ratio > 0.6f ? hpHealthy : ratio > 0.3f ? hpHurt : hpCritical;
        }

        if (hpLabel != null)
        {
            hpLabel.text = $"{health.CurrentHp:F0} / {health.MaxHp:F0}";
        }
    }

    private void UpdateTime()
    {
        if (timeLabel == null || game == null)
        {
            return;
        }

        float remaining = game.RemainingTime;
        int total = Mathf.Max(0, Mathf.CeilToInt(remaining));

        timeLabel.text = $"{total / 60}:{total % 60:00}";
        timeLabel.color = remaining <= timeWarningSeconds ? timeWarning : timeNormal;
    }

    private void UpdateProgress()
    {
        if (game == null)
        {
            return;
        }

        float progress = game.Progress01;
        float expected = game.ExpectedProgress01;

        if (progressFill != null)
        {
            progressFill.fillAmount = progress;
        }

        PlaceOnBar(shipMarker, progress);
        PlaceOnBar(expectedMarker, expected);

        if (behindWarning != null)
        {
            behindWarning.SetActive(game.IsBehindSchedule);
        }
    }

    /// <summary>바 위의 t (0~1) 지점에 표시를 놓는다.</summary>
    private static void PlaceOnBar(RectTransform marker, float t)
    {
        if (marker == null)
        {
            return;
        }

        t = Mathf.Clamp01(t);

        marker.anchorMin = new Vector2(t, marker.anchorMin.y);
        marker.anchorMax = new Vector2(t, marker.anchorMax.y);
        marker.anchoredPosition = new Vector2(0f, marker.anchoredPosition.y);
    }

    private void UpdateEvents()
    {
        if (eventRows == null)
        {
            return;
        }

        var active = VoyageEvent.Active;

        for (int i = 0; i < eventRows.Length; i++)
        {
            EventRow row = eventRows[i];
            if (row == null || row.root == null)
            {
                continue;
            }

            bool used = i < active.Count;
            row.root.SetActive(used);

            if (!used)
            {
                continue;
            }

            VoyageEvent e = active[i];

            if (row.label != null)
            {
                row.label.text = e.WarningText;
            }

            if (row.timer != null)
            {
                // 제한이 없는 사건(선체 파손)은 게이지를 숨긴다. 줄어들지 않으니 오해를 준다.
                bool timed = e.Duration > 0f;
                row.timer.gameObject.SetActive(timed);

                if (timed)
                {
                    row.timer.fillAmount = e.Remaining01;
                }
            }
        }
    }

    private void UpdateInteract()
    {
        if (interactPanel == null)
        {
            return;
        }

        if (LocalWorker == null)
        {
            interactPanel.SetActive(false);
            return;
        }

        // 붙어 있으면 그 작업의 진행도를, 근처면 붙으라는 안내를 띄운다.
        TaskBase current = LocalWorker.Current;
        TaskBase nearby = LocalWorker.Nearby;

        var carry = LocalWorker.GetComponent<CarryTask>();
        if (carry != null && carry.IsCarrying)
        {
            // 이모지는 한글 폰트에 글리프가 없어 □ 로 나온다. 아이콘은 에셋이 온 뒤
            // TMP Sprite Asset 으로 붙인다. 그때까지는 글자로만 쓴다.
            Show($"포탄 운반 중  ·  {(carry.FindLoadableCannon() != null ? "Shift 를 놓아 싣기" : "대포로")}", -1f);
            return;
        }

        if (current != null)
        {
            Show(current.DisplayName, GaugeOf(current));
            return;
        }

        if (nearby != null)
        {
            Show($"{nearby.DisplayName}  —  Space", -1f);
            return;
        }

        interactPanel.SetActive(false);
    }

    private void Show(string text, float gauge01)
    {
        interactPanel.SetActive(true);

        if (interactLabel != null)
        {
            interactLabel.text = text;
        }

        if (interactGauge == null)
        {
            return;
        }

        bool hasGauge = gauge01 >= 0f;
        interactGauge.gameObject.SetActive(hasGauge);

        if (hasGauge)
        {
            interactGauge.fillAmount = Mathf.Clamp01(gauge01);
        }
    }

    /// <summary>작업마다 게이지가 무엇을 뜻하는지 다르다. 없으면 -1.</summary>
    private static float GaugeOf(TaskBase task)
    {
        switch (task)
        {
            case HelmTask helm: return Mathf.Abs(helm.Heading01);
            case SailTask sail: return sail.SailPower01;
            case RepairTask repair: return repair.Progress01;
            case CannonTask cannon: return cannon.MaxAmmo <= 0 ? 0f : (float)cannon.Ammo / cannon.MaxAmmo;
            case DummyTask dummy: return dummy.Progress01;
            default: return -1f;
        }
    }
}
