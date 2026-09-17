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

        [Tooltip("무슨 사건인지 보여주는 그림. 사건 종류에 따라 바뀐다.")]
        public Image icon;

        [Tooltip("왼쪽 경고선. 예고인지 이미 터졌는지를 색으로 보여준다.")]
        public Image accent;
    }

    /// <summary>
    /// 팀원 초상화 한 칸. (SHIPCOOP.md 9장)
    ///
    /// ⚠ **층까지만 보여줍니다. 어느 자리에 붙었는지는 절대 보여주지 않습니다.**
    ///
    /// <code>
    /// 층   "민화는 뒷갑판에 있다"   →  전략을 짤 재료
    /// 자리 "돛이 비어 있다"         →  답을 그냥 준다
    /// </code>
    ///
    /// 갑판이 3층이라 서로 안 보입니다. 누가 어디 있는지는 알아야 역할을 나눌 수 있지만,
    /// **그 사람이 조타를 잡았는지 지나가는 중인지는 물어봐야** 합니다.
    /// 자리까지 띄우면 "돛 비었어!" 라는 말이 통째로 사라집니다.
    ///
    /// 하트는 없습니다. 개인 HP 가 존재하지 않기 때문입니다. (2장)
    /// </summary>
    [System.Serializable]
    public class PortraitSlot
    {
        public GameObject root;

        [Tooltip("플레이어를 구분하는 색 테두리. 직업이 아니라 사람 구분용이다.")]
        public Image frame;

        [Tooltip("지금 있는 갑판 이름. 자리 이름을 넣지 않는다.")]
        public TextMeshProUGUI deckLabel;

        [Tooltip("🆘 를 눌렀을 때 켜진다. 초상화 아래쪽에 겹치는 빨간 글씨.")]
        public GameObject helpBadge;

        // ⚠ Image 가 아니라 RawImage 입니다. 사진이 스프라이트가 아니라
        //    카메라가 찍은 RenderTexture 라서 그렇습니다. (`ShipCoopPortrait`)
        [Tooltip("그 사람의 캐릭터를 찍은 사진. ShipCoopPortrait 가 채운다.")]
        public RawImage face;
    }

    [Header("연결 — 비워두면 씬에서 자동으로 찾는다")]
    [SerializeField] private ShipCoopGame game;
    [SerializeField] private ShipHealth health;
    [SerializeField] private ShipVoyage voyage;
    [SerializeField] private ShipFlooding flooding;

    [Header("배 HP")]
    [SerializeField] private Image hpFill;
    [SerializeField] private TextMeshProUGUI hpLabel;

    [Header("남은 시간")]
    [SerializeField] private TextMeshProUGUI timeLabel;

    [Header("페이즈")]
    [Tooltip("지금 어느 구간인지. 왼쪽 위.\n\n" +
             "페이즈가 시간이 아니라 진행도로 갈리기 때문에(3장) 언제 넘어갔는지가 안 보인다.\n" +
             "'페이즈 3' 이 떴다는 것 자체가 '이제 겹쳐서 온다' 는 예고가 된다.")]
    [SerializeField] private TextMeshProUGUI phaseLabel;

    [Tooltip("남은 시간이 이 아래로 내려가면 글자 색이 바뀐다. (초)")]
    [SerializeField] private float timeWarningSeconds = 60f;

    [Header("진행도")]
    [Tooltip("지금까지 나아간 만큼 채워진다.")]
    [SerializeField] private Image progressFill;

    [Tooltip("배 표시. 진행도에 따라 좌우로 움직인다.")]
    [SerializeField] private RectTransform shipMarker;

    [Tooltip("지금 있어야 할 위치. 이 선보다 배가 뒤에 있으면 늦고 있다.")]
    [SerializeField] private RectTransform expectedMarker;

    [Tooltip("배와 기준선 사이를 메우는 붉은 칸. 늦은 만큼 벌어진다.\n" +
             "앞서 있으면 벌어질 것이 없으므로 꺼진다.")]
    [SerializeField] private RectTransform delayFill;

    [Tooltip("늦고 있을 때만 켜진다.")]
    [SerializeField] private GameObject behindWarning;

    [Header("침수")]
    [Tooltip("물이 찼을 때만 켜진다. 물이 0 이면 아무것도 안 보여야 한다.\n" +
             "빈 게이지가 늘 떠 있으면 '물이 처음부터 있다' 로 읽힌다.")]
    [SerializeField] private GameObject floodRoot;

    [Tooltip("찬 물의 양. Image Type 을 Filled 로 둔다.")]
    [SerializeField] private Image floodFill;

    [Tooltip("찬 정도와 그것 때문에 초당 깎이는 HP.")]
    [SerializeField] private TextMeshProUGUI floodLabel;

    [Tooltip("조금 찼을 때의 색")]
    [SerializeField] private Color floodShallow = new Color(0.35f, 0.62f, 0.90f, 0.95f);

    [Tooltip("가득 찼을 때의 색. 깎이는 속도를 색으로도 읽게 한다.")]
    [SerializeField] private Color floodDeep = new Color(0.90f, 0.30f, 0.30f, 0.95f);

    [Tooltip("얕음과 깊음 사이를 지나가는 색.\n\n" +
             "하늘색에서 빨강으로 곧장 섞으면 **중간이 보라색**이 된다.\n" +
             "위험해 보이지도 않고 물 같지도 않다. 주황을 한 번 거치면 그 구간이 사라진다.")]
    [SerializeField] private Color floodMid = new Color(1f, 0.74f, 0.33f, 0.95f);

    [Header("사건 알림 (왼쪽)")]
    [Tooltip("동시 발생 상한보다 넉넉하게 준비한다. 남는 줄은 꺼진다.")]
    [SerializeField] private EventRow[] eventRows;

    [Header("사건 그림")]
    [Tooltip("선체 파손")]
    [SerializeField] private Sprite eventIconHull;

    [Tooltip("돌풍 — 돛이 풀린다")]
    [SerializeField] private Sprite eventIconSail;

    [Tooltip("적선 — 대포로 제거한다")]
    [SerializeField] private Sprite eventIconCannon;

    [Tooltip("암초와 파도 — 조타로 넘긴다")]
    [SerializeField] private Sprite eventIconHelm;

    [Tooltip("🪨 암초. 없으면 조타 아이콘을 쓴다.")]
    [SerializeField] private Sprite eventIconReef;

    [Tooltip("🌊 파도. 없으면 조타 아이콘을 쓴다.")]
    [SerializeField] private Sprite eventIconWave;

    [Tooltip("🏴‍☠️ 적선. 없으면 대포 아이콘을 쓴다.")]
    [SerializeField] private Sprite eventIconEnemy;

    [Header("팀원 초상화 — 층과 🆘 만 보여준다")]
    [Tooltip("왼쪽 아래 4칸. 사람이 없는 칸은 꺼진다.")]
    [SerializeField] private PortraitSlot[] portraits;

    [Tooltip("아직 어느 갑판인지 모를 때 (배 밖이거나 떨어지는 중)")]
    [SerializeField] private string unknownDeckText = "—";

    [Header("상호작용")]
    [SerializeField] private GameObject interactPanel;
    [SerializeField] private TextMeshProUGUI interactLabel;

    [Tooltip("작업 진행도. Image Type 을 Filled 로 둔다.")]
    [SerializeField] private Image interactGauge;

    [Tooltip("무슨 작업인지 보여주는 그림. 자리에 따라 바뀐다.")]
    [SerializeField] private Image interactIcon;

    [Header("작업 그림")]
    [SerializeField] private Sprite taskIconHelm;
    [SerializeField] private Sprite taskIconSails;
    [SerializeField] private Sprite taskIconCannon;
    [SerializeField] private Sprite taskIconRepair;

    [Header("사건 단계 색")]
    [Tooltip("예고 중. 아직 아무것도 안 깎였다는 것을 흐리게 보여준다.")]
    [SerializeField] private Color eventWarning = new Color(1f, 1f, 1f, 0.45f);

    [Tooltip("이미 터졌다. 제한 시간을 세는 중.")]
    [SerializeField] private Color eventRunning = new Color(1f, 0.45f, 0.35f, 1f);

    [Header("색")]
    [SerializeField] private Color hpHealthy = new Color(0.45f, 0.85f, 0.55f);
    [SerializeField] private Color hpHurt = new Color(0.95f, 0.75f, 0.35f);
    [SerializeField] private Color hpCritical = new Color(0.95f, 0.40f, 0.35f);
    [SerializeField] private Color timeNormal = Color.white;
    [SerializeField] private Color timeWarning = new Color(0.95f, 0.55f, 0.45f);

    /// <summary>
    /// 화면을 볼 사람. 상호작용 안내와 카메라가 비출 갑판이 이 사람 기준으로 정해진다.
    ///
    /// <code>
    /// 싱글 테스트 씬   씬에서 살아있는 첫 TaskWorker 를 스스로 찾는다 (예전 그대로)
    /// 네트워크 씬      ShipCoopLocalView 가 자기 캐릭터를 SetLocalWorker 로 넣어 준다
    /// </code>
    /// </summary>
    public TaskWorker LocalWorker { get; private set; }

    /// <summary>밖에서 넣어 준 적이 있는가. 있으면 스스로 찾지 않는다.</summary>
    private bool _localWorkerInjected;

    /// <summary>
    /// 출항 전에 페이즈 자리에 띄울 안내. 비어 있으면 예전처럼 페이즈 이름이 뜬다.
    ///
    /// 네트워크에서 "두 명을 기다리는 중" · "3초 뒤 출항" 을 보여주는 데 쓴다.
    /// 혼자 하는 씬에서는 아무도 넣지 않으므로 하나도 바뀌지 않는다.
    /// </summary>
    public string StartNotice { get; set; }

    /// <summary>
    /// "화면을 보는 사람은 이 사람이다" 라고 알려준다.
    ///
    /// ⚠ 네트워크에서는 <b>반드시</b> 이걸 불러야 한다. 안 부르면 HUD 가 스스로 찾다가
    ///    <b>남의 캐릭터</b>를 집을 수 있고, 그러면 카메라가 남이 선 갑판을 비춘다.
    ///    한 번 넣은 뒤에는 스스로 찾는 길이 막히므로 다른 사람으로 바뀌지 않는다.
    /// </summary>
    public void SetLocalWorker(TaskWorker worker)
    {
        LocalWorker = worker;
        _localWorkerInjected = true;
    }

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<ShipCoopGame>(FindObjectsInactive.Include);
        if (health == null) health = FindAnyObjectByType<ShipHealth>(FindObjectsInactive.Include);
        if (voyage == null) voyage = FindAnyObjectByType<ShipVoyage>(FindObjectsInactive.Include);
        if (flooding == null) flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);

        _portrait = FindAnyObjectByType<ShipCoopPortrait>(FindObjectsInactive.Include);
    }

    // 프로필 사진을 찍어 두는 쪽. 없으면 사진 없이 굴러간다.
    private ShipCoopPortrait _portrait;

    private void Update()
    {
        // 사람은 도중에 들어오거나 나갈 수 있다. 매 프레임 확인이 부담될 정도는 아니다.
        // 밖에서 넣어 준 적이 있으면 찾지 않는다 — 남의 캐릭터를 집게 된다.
        if (LocalWorker == null && !_localWorkerInjected)
        {
            LocalWorker = FindAnyObjectByType<TaskWorker>(FindObjectsInactive.Exclude);
        }

        UpdateHealth();
        UpdateTime();
        UpdatePhase();
        UpdateProgress();
        UpdateFlooding();
        UpdateEvents();
        UpdatePortraits();
        UpdateInteract();
    }

    /// <summary>
    /// 지금 어느 구간인지. (SHIPCOOP.md 3장)
    ///
    /// 페이즈는 시간이 아니라 **진행도로** 갈립니다. 그래서 언제 넘어갔는지가
    /// 화면 어디에도 안 보입니다. "페이즈 3" 이 떴다는 것 자체가
    /// **"이제 셋씩 겹쳐서 온다"** 는 예고가 됩니다.
    /// </summary>
    private void UpdatePhase()
    {
        if (phaseLabel == null || game == null)
        {
            return;
        }

        // 출항 전 안내가 있으면 그쪽이 먼저다. 아직 페이즈가 없는 동안만 쓰인다.
        if (!string.IsNullOrEmpty(StartNotice))
        {
            phaseLabel.text = StartNotice;
            return;
        }

        VoyagePhase phase = game.CurrentPhase;
        phaseLabel.text = phase != null ? phase.name : string.Empty;
    }

    /// <summary>
    /// 팀원 초상화. **누가 어느 갑판에 있는지와 🆘 만** 보여준다.
    ///
    /// 갑판이 3층이 되면서 화면에 배 전체가 안 나옵니다. 그래서 여기가
    /// "누가 어디 있나" 를 아는 **유일한 곳**이 됐습니다.
    ///
    /// 자리(조타 · 돛 · 대포)를 보여주지 않는 이유는 <see cref="PortraitSlot"/> 에 적어 두었습니다.
    /// </summary>
    private void UpdatePortraits()
    {
        if (portraits == null || portraits.Length == 0)
        {
            return;
        }

        // 사람 순서가 프레임마다 바뀌면 초상화가 자리를 바꿔 가며 깜빡인다.
        // FindObjectsByType 의 순서는 보장되지 않으므로 고정된 키로 줄을 세운다.
        //
        // ⚠ **이름으로 정렬하면 안 된다.** 네트워크로 스폰된 플레이어는 이름이 전부
        //    `ShipCoopPlayer(Clone)` 로 같아서, Array.Sort 가 같은 키끼리 순서를 보장하지 않는다.
        //    그래서 칸이 매 프레임 뒤바뀌며 카드가 지직거렸다. 실측으로 확인했다.
        //    ShipCoopPortrait 가 사진을 보관할 때 쓰는 키와 **같은 키**를 쓴다.
        TaskWorker[] crew = FindObjectsByType<TaskWorker>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        System.Array.Sort(crew, (a, b) => string.CompareOrdinal(
            ShipCoopPortrait.StableKeyOf(a), ShipCoopPortrait.StableKeyOf(b)));

        // 패널 폭은 **늘 4칸 그대로**다. 이 게임은 4명 고정이라(2장) 빈 칸이 생기지 않는다.
        // 테스트 씬에서만 사람이 모자라 비어 보인다. 그걸 맞추려고 폭을 줄이면,
        // 정작 진짜 게임에서 누가 빠졌을 때 **빠진 것이 안 보이게** 된다.
        for (int i = 0; i < portraits.Length; i++)
        {
            PortraitSlot slot = portraits[i];

            if (slot == null)
            {
                continue;
            }

            bool has = i < crew.Length && crew[i] != null;

            if (slot.root != null)
            {
                slot.root.SetActive(has);
            }

            if (!has)
            {
                continue;
            }

            if (slot.deckLabel != null)
            {
                ShipDeck deck = ShipDeck.At(crew[i].transform.position);
                slot.deckLabel.text = deck != null ? deck.DeckName : unknownDeckText;
            }

            if (slot.helpBadge != null)
            {
                ShipCoopHelp help = crew[i].GetComponent<ShipCoopHelp>();
                slot.helpBadge.SetActive(help != null && help.IsCalling);
            }

            // ⚠ "없을 때만 넣기" 로 두면 안 됩니다. 다시 찍으면(`Retake`) 사진이
            //    새로 만들어지는데, 옛 사진을 든 채로 있으면 **버려진 텍스처**를
            //    그리게 됩니다. 달라졌을 때만 넣으면 비용도 없습니다.
            if (slot.face != null && _portrait != null)
            {
                Texture shot = _portrait.Of(crew[i]);

                if (shot != null && slot.face.texture != shot)
                {
                    slot.face.texture = shot;
                    slot.face.color = Color.white;
                }
            }
        }
    }

    /// <summary>
    /// 침수. **물이 있을 때만 뜬다.**
    ///
    /// 침수는 **물이 남아 있는 동안 초당 HP 를 깎습니다.** 그래서 화면에 안 띄우면
    /// **HP 가 왜 줄어드는지 알 방법이 아무 데도 없습니다.** 사건도 안 떠 있는데
    /// HP 만 깎이면 버그로 읽힙니다. (2장 · 4장)
    ///
    /// 물이 0 일 때 빈 게이지를 띄우지 않는 것도 같은 이유입니다.
    /// 늘 떠 있으면 "물은 원래 있는 것" 이 되고, 퍼내야 한다는 신호가 죽습니다.
    /// </summary>
    private void UpdateFlooding()
    {
        if (floodRoot == null)
        {
            return;
        }

        bool has = flooding != null && flooding.HasWater;
        floodRoot.SetActive(has);

        if (!has)
        {
            return;
        }

        if (floodFill != null)
        {
            floodFill.fillAmount = flooding.Level01;

            // 많이 찰수록 붉어진다. 깎이는 속도를 색으로도 읽게 한다.
            // 하늘색 → 주황 → 빨강. 곧장 섞으면 중간이 보라색이 된다.
            float level = flooding.Level01;
            floodFill.color = level < 0.5f
                ? Color.Lerp(floodShallow, floodMid, level * 2f)
                : Color.Lerp(floodMid, floodDeep, (level - 0.5f) * 2f);
        }

        if (floodLabel != null)
        {
            // 속도가 아니라 **초당 깎이는 HP** 를 띄운다.
            // 속도가 주는 것은 아무도 못 느낀다. 진행도 바를 계속 봐야 알 수 있는데
            // 물이 찼을 때는 그럴 여유가 없다.
            //
            // 구멍이 열려 있으면 **퍼내지 말라고 말해준다.**
            // 구멍 하나가 초당 2.5% 를 붓고 양동이 왕복이 4초라, 막기 전에 퍼내면
            // 물이 0 으로 안 내려가고 제자리를 돈다. 게이지만 보면 양동이로 손이 가는데
            // 그게 바로 함정이다.
            floodLabel.text = WithHint(
                $"침수 {flooding.Level01:P0}  ·  초당 -{flooding.DamagePerSecond:F1} HP",
                flooding.LeakingPoints > 0
                    ? $"구멍 {flooding.LeakingPoints}개가 새는 중 — 수리가 먼저다"
                    : "양동이로 퍼내라");
        }
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
        SpanOnBar(delayFill, progress, expected);

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

    /// <summary>
    /// 바 위의 배 위치부터 기준선까지를 채운다. 앞서 있으면 채울 것이 없으므로 끈다.
    ///
    /// 늦은 정도를 길이로 보여주는 것이 요점이다. 배가 기준선에서 얼마나 떨어졌는지를
    /// 숫자 없이 폭 하나로 읽게 한다.
    /// </summary>
    private static void SpanOnBar(RectTransform fill, float from, float to)
    {
        if (fill == null)
        {
            return;
        }

        from = Mathf.Clamp01(from);
        to = Mathf.Clamp01(to);

        bool behind = to > from;
        fill.gameObject.SetActive(behind);

        if (!behind)
        {
            return;
        }

        fill.anchorMin = new Vector2(from, fill.anchorMin.y);
        fill.anchorMax = new Vector2(to, fill.anchorMax.y);
        fill.sizeDelta = new Vector2(0f, fill.sizeDelta.y);
        fill.anchoredPosition = new Vector2(0f, fill.anchoredPosition.y);
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
                // 무슨 일인지(WarningLine) 아래에 무엇을 해야 하는지(LiveHint)를 붙인다.
                // 경고만 띄우면 뭘 해야 하는지가 화면 어디에도 없다. 파도가 그랬다.
                //
                // WarningLine 은 갑판이 정해지는 사건이면 층 이름까지 넣어준다.
                // 갑판이 3층이라 화면 밖에서 벌어지는 일이 생겼기 때문이다. (9장)
                //
                // ⚠ **둘째 줄은 예고 중에만 띄운다.**
                //
                // 예고는 아무것도 안 깎이는 구간이라 글을 읽을 여유가 있는 유일한 때다.
                // 발생하면 이미 몸이 움직이고 있고, 그때는 색과 그림이 대신 말해준다.
                // (파도는 정면이면 흰파랑 · 벗어나면 빨강, 바위는 다가온다 — 5장)
                //
                // 발생 중에도 띄우면 카드가 여러 장일 때 읽을 것만 늘어난다.
                // 다만 "지금 안 하면 진다" 는 신호(HintIsUrgent)는 예외다.
                bool showHint = e.IsWarning || e.HintIsUrgent;
                row.label.text = WithHint(e.WarningLine, showHint ? e.LiveHint() : null);
            }

            if (row.icon != null)
            {
                Sprite icon = IconOf(e);
                row.icon.sprite = icon;
                row.icon.enabled = icon != null;
            }

            // 예고인지 이미 터진 것인지 색으로 가른다. (9장)
            //
            // 그림이 이미 붉고 주황이라 곱해지는 색으로는 더 밝게 만들 수 없다.
            // 그래서 예고는 흐리게, 발생은 진하고 붉게 간다.
            Color stage = e.IsWarning ? eventWarning : eventRunning;

            if (row.accent != null)
            {
                row.accent.color = stage;
            }

            if (row.timer != null)
            {
                // 셀 것이 없으면 게이지를 숨긴다. 줄어들지 않는 게이지는 오해를 준다.
                // 예고 중이면 발생까지, 터진 뒤면 실패까지 센다.
                bool timed = e.HasCountdown;
                row.timer.gameObject.SetActive(timed);

                if (timed)
                {
                    row.timer.fillAmount = e.Remaining01;
                    row.timer.color = stage;
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
            Show(WithHint($"{CarryTask.NameOf(carry.Carrying)} 운반 중", CarryHintOf(carry)),
                 -1f, taskIconCannon);
            return;
        }

        if (current != null)
        {
            Show(WithHint(current.DisplayName, HintOf(current)), GaugeOf(current), IconOf(current),
                 TwoSidedGauge(current));
            return;
        }

        if (nearby != null)
        {
            Show($"{nearby.DisplayName}  —  Space", -1f, IconOf(nearby));
            return;
        }

        // 포탄 상자는 자리(TaskBase)가 아니라서 Nearby 에 잡히지 않는다.
        // 그래서 상자 앞에 서면 아무 안내도 안 떴다. 무거운 포탄은 쥐어야 들리므로
        // 자리에 붙는 것과 키가 다르다. 그 차이를 여기서 알려준다.
        // 상자마다 나오는 것이 다르다. 무엇이 나오는지 말해주지 않으면
        // 갑판에 색깔 큐브만 놓여 있고 무슨 상자인지 알 수가 없다.
        if (carry != null)
        {
            // 갑판에 놓인 것이 먼저다. 상자보다 가까이 있고, 주우러 온 것이다.
            DroppedCargo lying = carry.FindReachableDrop();
            if (lying != null)
            {
                Show(WithHint($"{CarryTask.NameOf(lying.Kind)} 줍기", "두고 간 것을 다시 든다"),
                     -1f, IconOfCargo(lying.Kind));
                return;
            }

            AmmoBox box = carry.FindReachableBox();
            if (box != null)
            {
                Show(WithHint($"{CarryTask.NameOf(box.Kind)} 집기", BoxHintOf(box)),
                     -1f, IconOfCargo(box.Kind));
                return;
            }
        }

        interactPanel.SetActive(false);
    }

    private void Show(string text, float gauge01, Sprite icon)
    {
        Show(text, gauge01, icon, false);
    }

    // ------------------------------------------------------------
    // ⚠ **조타 게이지는 가운데에서 좌우로 찹니다.**
    //
    //    전에는 절댓값(`|Heading01|`)을 한 방향으로만 채웠습니다. 그러면
    //    **좌현 끝과 우현 끝이 둘 다 "가득 참"** 으로 보입니다. 어느 쪽으로
    //    꺾여 있는지 게이지만 봐서는 알 수 없습니다.
    //
    //    거대한 파도(`BigWave`)는 초당 45도로 조타를 미는데 사람은 35도로만
    //    돌립니다. **버텨도 밀립니다.** 그래서 꺾어둔 키가 0을 지나 반대편으로
    //    넘어가고, 게이지가 찼다가 비었다가 다시 차는 것처럼 보입니다.
    //    내가 꺾고 있는 것인지 밀리고 있는 것인지 구분이 안 됩니다.
    //
    //    링은 이미 `Radial360` · 시작점이 **위**입니다. 그래서 부호만 살려
    //    `fillClockwise` 를 뒤집으면 위(중앙)에서 좌우로 갈라져 찹니다.
    // ------------------------------------------------------------

    /// <summary>양쪽으로 차는 게이지가 한쪽으로 최대일 때 링의 몇 할을 채우는가.</summary>
    private const float HalfRing = 0.5f;

    /// <param name="gauge01">0~1. 양쪽으로 차는 것은 -1~+1 이고 부호가 방향이다.</param>
    /// <param name="twoSided">가운데(위)에서 좌우로 갈라져 차는가.</param>
    private void Show(string text, float gauge01, Sprite icon, bool twoSided)
    {
        interactPanel.SetActive(true);

        if (interactLabel != null)
        {
            interactLabel.text = text;
        }

        if (interactIcon != null)
        {
            interactIcon.sprite = icon;
            interactIcon.enabled = icon != null;
        }

        if (interactGauge == null)
        {
            return;
        }

        // 양쪽으로 차는 것은 음수가 정상이다. 한쪽으로만 차는 것만 -1 이 "없음" 이다.
        bool hasGauge = twoSided || gauge01 >= 0f;
        interactGauge.gameObject.SetActive(hasGauge);

        if (!hasGauge)
        {
            return;
        }

        if (twoSided)
        {
            // 우현(양수)은 시계방향, 좌현(음수)은 반시계방향으로 찬다.
            interactGauge.fillClockwise = gauge01 >= 0f;
            interactGauge.fillAmount = Mathf.Clamp01(Mathf.Abs(gauge01)) * HalfRing;
            return;
        }

        // ⚠ 되돌려 놓습니다. 조타를 보다가 다른 자리로 가면 반시계로 남습니다.
        interactGauge.fillClockwise = true;
        interactGauge.fillAmount = Mathf.Clamp01(gauge01);
    }

    /// <summary>이 작업의 게이지가 가운데에서 좌우로 차는가. 조타만 그렇다.</summary>
    private static bool TwoSidedGauge(TaskBase task)
    {
        return task is HelmTask;
    }

    /// <summary>작업마다 게이지가 무엇을 뜻하는지 다르다. 없으면 -1.</summary>
    private static float GaugeOf(TaskBase task)
    {
        switch (task)
        {
            // ⚠ **절댓값을 쓰지 마세요.** 좌우 구분이 사라집니다. (위 주석)
            case HelmTask helm: return helm.Heading01;
            case SailTask sail: return sail.SailPower01;
            case RepairTask repair: return repair.Progress01;
            case CannonTask cannon: return cannon.MaxAmmo <= 0 ? 0f : (float)cannon.Ammo / cannon.MaxAmmo;
            case DummyTask dummy: return dummy.Progress01;
            default: return -1f;
        }
    }

    /// <summary>
    /// 사건에 붙는 그림. 무엇을 해야 넘길 수 있는지로 고른다.
    ///
    /// 암초와 파도는 둘 다 조타로 넘긴다. 그래서 같은 그림을 쓴다.
    /// 글자를 읽기 전에 "누가 가야 하는가" 가 먼저 보이는 것이 목적이다.
    /// </summary>
    private Sprite IconOf(VoyageEvent e)
    {
        // 사건마다 다른 그림이어야 한다. 암초와 파도가 같은 아이콘을 쓰면
        // **알림 목록에서 둘이 구분되지 않는다.** 대응 동작이 정반대인데도. (5장)
        switch (e)
        {
            case HullDamage _: return eventIconHull;
            case Squall _: return eventIconSail;
            case EnemyShip _: return eventIconEnemy != null ? eventIconEnemy : eventIconCannon;
            case Reef _: return eventIconReef != null ? eventIconReef : eventIconHelm;
            case BigWave _: return eventIconWave != null ? eventIconWave : eventIconHelm;
            default: return null;
        }
    }

    /// <summary>
    /// 자리에 붙은 다음에 무엇을 눌러야 하는지.
    ///
    /// 붙기 전에는 "— Space" 가 뜨는데, 붙고 나면 자리 이름만 남아서
    /// **거기서 뭘 해야 하는지 화면에 아무 데도 없었습니다.**
    /// 특히 대포는 붙어야만 X 가 먹기 때문에, 안 붙고 X 를 누르면
    /// 아무 일도 안 일어나고 이유도 안 보입니다.
    ///
    /// 대포는 포탄 수까지 함께 띄웁니다. 없으면 쏘는 게 아니라 날라야 합니다.
    /// </summary>
    /// <summary>
    /// 상자 앞에 섰을 때의 안내.
    ///
    /// 양동이는 **구멍이 열려 있으면 집지 말라고** 말해줍니다.
    /// 손이 실제로 가는 자리가 여기라서, 게이지에만 적어두면 늦습니다.
    /// </summary>
    private string BoxHintOf(AmmoBox box)
    {
        if (box.Kind == Cargo.Water && flooding != null && flooding.LeakingPoints > 0)
        {
            return "구멍부터 막아라 — 퍼내도 다시 찬다";
        }

        return "Shift + Space";
    }

    /// <summary>들고 있는 것을 어디로 가져가야 하는지. 손에 든 것마다 목적지가 다르다.</summary>
    private static string CarryHintOf(CarryTask carry)
    {
        switch (carry.Carrying)
        {
            case Cargo.Ammo:
                return carry.FindLoadableCannon() != null ? "Shift 를 놓아 싣기" : "대포로";

            case Cargo.Plank:
                return carry.FindPointWantingPlank() != null ? "Shift 를 놓아 건네기" : "빨간 파손 지점으로";

            case Cargo.Water:
                return carry.FindReachableDump() != null ? "Shift 를 놓아 버리기" : "파란 뱃전으로";

            default:
                return null;
        }
    }

    /// <summary>들고 있는 것에 맞는 그림. 자재와 물은 아직 전용 그림이 없다.</summary>
    private Sprite IconOfCargo(Cargo cargo)
    {
        return cargo == Cargo.Ammo ? taskIconCannon : taskIconRepair;
    }

    private static string HintOf(TaskBase task)
    {
        switch (task)
        {
            case CannonTask cannon:
                return cannon.Ammo > 0
                    ? $"X 로 발사  ·  포탄 {cannon.Ammo}/{cannon.MaxAmmo}"
                    : "포탄이 없다 — 상자에서 날라라";

            // 자재가 없으면 두드려도 안 먹는다. 그 말을 안 하면 고장 난 줄 안다.
            case RepairTask repair:
                return repair.CanHammer ? "F 를 연타" : "자재가 필요하다 — 갈색 상자에서";
            case HelmTask _: return "A · D 로 꺾기";
            case SailTask _: return "D 로 당기기";
            default: return null;
        }
    }

    /// <summary>자리 이름 아래에 작은 글씨로 안내를 붙인다.</summary>
    private static string WithHint(string displayName, string hint)
    {
        return string.IsNullOrEmpty(hint)
            ? displayName
            : $"{displayName}\n<size=60%>{hint}</size>";
    }

    /// <summary>지금 붙어 있는(또는 붙을 수 있는) 자리의 그림. 없으면 null.</summary>
    private Sprite IconOf(TaskBase task)
    {
        switch (task)
        {
            case HelmTask _: return taskIconHelm;
            case SailTask _: return taskIconSails;
            case CannonTask _: return taskIconCannon;
            case RepairTask _: return taskIconRepair;
            default: return null;
        }
    }
}
