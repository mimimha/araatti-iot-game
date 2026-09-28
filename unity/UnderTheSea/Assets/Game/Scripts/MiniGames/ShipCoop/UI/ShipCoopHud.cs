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

        [Tooltip("타이머 바탕. 셀 것이 없는 사건(선체 파손)에서는 채움과 함께 꺼진다.")]
        public GameObject timerTrack;

        [Tooltip("오른쪽 위 '예고 / 발생' 배지 바탕. 색으로 단계를 말한다.")]
        public Image badge;

        [Tooltip("배지 안의 글자.")]
        public TextMeshProUGUI badgeLabel;

        [Tooltip("남은 초. **지금은 비워 둔다** — 타이머 바가 대신 말한다.\n\n" +
                 "숫자로도 보여주고 싶어지면 여기에 글자를 꽂으면 그때부터 다시 뜬다. " +
                 "셀 것이 없는 사건(선체 파손)에서는 꺼진다.")]
        public TextMeshProUGUI seconds;

        [Tooltip("사건 이름. 갑판은 여기 넣지 않는다 — 아래 보조 문구로 간다.")]
        public TextMeshProUGUI title;
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

    [Header("늦었다는 알림 — 임시(TMP 글씨 + 삼각형)")]
    // ⚠ **붉은 구간만으로는 아무도 못 알아봅니다.**
    //
    //    붉은 칸은 "배와 기준선 사이"라는 뜻인데, 그걸 읽으려면 기준선이 무엇인지
    //    부터 알아야 합니다. 처음 보는 사람에게는 그냥 빨간 줄입니다. 그림만으로
    //    전해지는 개념이 아니라서, **벌어지는 순간에 글씨로 한 번 가르쳐** 줍니다.
    //
    //    손글씨 그림이 나오기 전까지 쓰는 임시 모양입니다. 자리 · 크기 · 타이밍을
    //    먼저 눈으로 보려고 만든 것이라, 삼각형도 코드로 굽습니다.
    //    (ShipCoopHurryArrowArt)
    [Tooltip("붉은 구간 위에 잠깐 떴다 사라지는 묶음. 꺼진 채로 만들어 둔다.")]
    [SerializeField] private GameObject hurryCallout;

    [Tooltip("사라질 때 흐려지도록. hurryCallout 에 붙어 있다.")]
    [SerializeField] private CanvasGroup hurryGroup;

    [Tooltip("무엇을 해야 하는지 적는다. '서둘러'가 아니라 '돛을 당겨'.")]
    [SerializeField] private TextMeshProUGUI hurryLabel;

    [Tooltip("붉은 구간을 가리키는 삼각형. 떠 있는 동안 위아래로 까딱인다.")]
    [SerializeField] private RectTransform hurryArrow;

    [Tooltip("이만큼 뒤처져야 뜬다. 0.05 는 3분 항해에서 약 9초.\n" +
             "조금만 뒤처져도 띄우면 출항 직후부터 떠 있게 되어 아무도 안 본다.")]
    [SerializeField, Range(0f, 0.5f)] private float hurryBehindAtLeast = 0.05f;

    [Tooltip("뒤처진 정도가 이 아래로 회복되면 '처음 벌어진 것'으로 다시 셈한다.")]
    [SerializeField, Range(0f, 0.5f)] private float hurryClearBelow = 0.02f;

    [Tooltip("한 번 띄운 뒤, 그때보다 이만큼 더 나빠지면 다시 띄운다.\n" +
             "회복만 기다리지 않는 이유 — 끝까지 늦는 판에서는 영영 한 번뿐이 된다.")]
    [SerializeField, Range(0.01f, 0.5f)] private float hurryWorsenBy = 0.06f;

    [Tooltip("이 시간 내내 뒤처져 있어야 뜬다. 경계에서 깜빡이는 것을 막는다. (초)")]
    [SerializeField, Min(0f)] private float hurryHoldSeconds = 2f;

    [Tooltip("떠 있는 시간. (초) 계속 떠 있으면 두 번째부터는 아무도 안 본다.")]
    [SerializeField, Min(0.5f)] private float hurryShowSeconds = 3f;

    [Tooltip("아무리 나빠져도 이 시간 안에는 다시 안 뜬다. (초)")]
    [SerializeField, Min(0f)] private float hurryCooldownSeconds = 20f;

    [Tooltip("돛 힘이 이 아래면 늦은 원인을 돛으로 본다.\n" +
             "속도 = 돛 힘 × 항로 계수라서, 둘 중 더 많이 깎아먹는 쪽을 짚는다.")]
    [SerializeField, Range(0f, 1f)] private float hurrySailBelow = 0.6f;

    /// <summary>톡 튀어나오는 데 걸리는 시간. (초)</summary>
    private const float HurryPopSeconds = 0.18f;

    /// <summary>사라지기 전 흐려지는 시간. (초)</summary>
    private const float HurryFadeSeconds = 0.45f;

    /// <summary>화살표가 까딱이는 빠르기. (rad/s)</summary>
    private const float HurryBobSpeed = 8f;

    /// <summary>화살표가 까딱이는 폭. (px)</summary>
    private const float HurryBobPixels = 4f;

    /// <summary>연속으로 <see cref="hurryBehindAtLeast"/> 넘게 뒤처진 시간. (초)</summary>
    private float _hurryBehindFor;

    /// <summary>이 시각까지 떠 있는다. 지났으면 꺼진다.</summary>
    private float _hurryUntil = -1f;

    /// <summary>이 시각 전에는 다시 안 띄운다.</summary>
    private float _hurryNextAt;

    /// <summary>마지막으로 띄웠을 때 뒤처진 정도. 음수면 아직 한 번도 안 띄웠다.</summary>
    private float _hurryWarnedAtGap = -1f;

    /// <summary>화살표가 까딱이기 전 원래 높이. 빌더가 놓은 자리를 그대로 쓴다.</summary>
    private float _hurryArrowBaseY;

    /// <summary>알림 묶음의 RectTransform. 매 프레임 붉은 구간 한가운데로 옮긴다.</summary>
    private RectTransform _hurryRect;

    // ⚠ **흰색/파란색 트랙 그림 두 장을 통째로 겹쳐서 쓴다.** (voyage-track-white-from-blue-hq /
    //    voyage-track-filled-blue-v3-hq) 체크포인트 5개는 이제 파란색 그림 안에 이미 칠해져
    //    있어서, 가로 Fill 마스크가 그 위치를 지나면 원도 같이 파랗게 드러난다 — 예전처럼
    //    별도 원 오브젝트(voyageActiveNodes)를 얹어 중복으로 그릴 필요가 없어 뺐다.
    //
    //    두 그림은 **캔버스 전체(2152×731)를 그대로 담는다.** 그래서 progressFill.fillAmount 에
    //    진행률(0~1)을 그대로 넣으면 안 된다 — 실제 트랙(색이 채워지는 구간)은 그림 안에서
    //    X 359~1787에만 있고, 그 앞뒤(항구 · 섬 배지가 있는 자리)는 채워지면 안 되기 때문이다.
    //    <see cref="ToVoyageFillAmount"/> 가 이 변환을 맡는다.
    private const float VoyageImageWidth = 2152f;
    private const float VoyageTrackStartX = 359f;
    private const float VoyageTrackEndX = 1787f;

    /// <summary>
    /// 항해 진행률(0~1)을 <c>progressFill.fillAmount</c> 값으로 바꾼다.
    ///
    /// 흰색 · 파란색 두 그림이 캔버스 전체(0~<see cref="VoyageImageWidth"/>)를 덮으므로,
    /// 실제 트랙 구간(<see cref="VoyageTrackStartX"/>~<see cref="VoyageTrackEndX"/>)이
    /// 캔버스에서 차지하는 자리를 그대로 fillAmount 로 옮겨야 한다.
    /// </summary>
    private static float ToVoyageFillAmount(float progress01)
    {
        float progress = Mathf.Clamp01(progress01);
        float currentX = Mathf.Lerp(VoyageTrackStartX, VoyageTrackEndX, progress);

        return currentX / VoyageImageWidth;
    }

    [Header("항로 이탈 경고")]
    // ⚠ **조타를 놓친 것은 화면 어디에도 안 나와 있었습니다.**
    //
    //    뱃머리가 틀어지면 속도가 깎이는데, 깎이고 있다는 사실을 알려주는 것이
    //    아무것도 없었습니다. 배는 그냥 느려지고 왜 느린지는 아무도 모릅니다.
    //    조타에 붙어 있는 사람만 게이지로 알 수 있고, 나머지 셋은 알 방법이 없습니다.
    //
    //    손실은 40° 근처부터 급해집니다. (0° 기준 30°는 -10%, 45°는 -22%, 60°는 -38%)
    //    그래서 그 언저리부터 화면 가장자리를 붉게 물들여 **누구든** 알아채게 합니다.
    [Tooltip("항로를 벗어났을 때 켜지는 묶음. 화면 가장자리 붉은 테두리와 문구.")]
    [SerializeField] private GameObject courseWarningRoot;

    [Tooltip("화면 가장자리에 까는 비네트. 깜빡임은 이 색의 알파로 준다.")]
    [SerializeField] private Image courseWarningVignette;

    [Tooltip("무엇을 해야 하는지 알려주는 문구.")]
    [SerializeField] private TextMeshProUGUI courseWarningLabel;

    [Tooltip("항로 계수(뱃머리가 목적지를 향한 정도)가 이 아래로 내려가면 경고를 켠다.\n" +
             "0.82 는 약 35°. 이보다 덜 틀어진 것은 속도 손실이 5% 안쪽이라 알릴 값이 아니다.")]
    [SerializeField, Range(0f, 1f)] private float courseWarnBelow = 0.82f;

    [Tooltip("이 아래로는 경고가 가장 세게 뜬다.\n0.55 는 약 57°. 조타 최대각(60°)이 거의 여기다.")]
    [SerializeField, Range(0f, 1f)] private float courseCriticalBelow = 0.55f;

    [Tooltip("깜빡이는 빠르기 (초당 회)")]
    [SerializeField, Min(0f)] private float coursePulseSpeed = 2.2f;

    [Tooltip("가장 약할 때와 가장 셀 때의 진하기")]
    [SerializeField, Range(0f, 1f)] private float courseAlphaMin = 0.16f;

    [SerializeField, Range(0f, 1f)] private float courseAlphaMax = 0.52f;

    [Tooltip("암초가 지나간 뒤 뱃머리를 되돌릴 틈. 이 시간 동안은 경고를 켜지 않는다. (초)\n" +
             "0 이면 바위가 사라지는 순간 바로 붉어져서, 아직 꺾여 있는 배를 두고 혼내게 된다.")]
    [SerializeField, Min(0f)] private float courseWarnGraceSeconds = 2f;

    /// <summary>이 시각까지는 항로 이탈 경고를 켜지 않는다. 암초를 피하는 중이었다.</summary>
    private float _courseGraceUntil;

    [Header("사건 알림 (상단 가운데)")]
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

    [Tooltip("🪣 침수. 사건이 아니라 상태지만 같은 카드로 보여준다.")]
    [SerializeField] private Sprite eventIconFlood;

    [Header("상호작용")]
    [SerializeField] private GameObject interactPanel;
    [SerializeField] private TextMeshProUGUI interactLabel;

    [Tooltip("작업 진행도. Image Type 을 Filled 로 둔다.")]
    [SerializeField] private Image interactGauge;

    [Tooltip("무슨 작업인지 보여주는 그림. 자리에 따라 바뀐다.")]
    [SerializeField] private Image interactIcon;

    /// <summary>
    /// 오른쪽 아래 키캡에 적히는 글자. 상황에 따라 바뀐다.
    ///
    /// ⚠ 연결하지 않으면 씬에 적혀 있는 글자가 그대로 남는다. 예전에는 연결이 없어
    ///    **무엇을 하든 "Space" 로 고정**돼 있었고, 대포에 붙은 사람이 Space 를 눌러
    ///    아무 일도 안 일어나는 것을 보게 됐다.
    /// </summary>
    [Tooltip("오른쪽 아래 키캡 글자. 자리에 따라 Space · K · J L 로 바뀌고, 완드가 붙으면 A · B · L R 로 바뀐다.")]
    [SerializeField] private TextMeshProUGUI interactKeycap;

    [Tooltip("\"길게 누르세요\" 같은 홀드 안내. 꾹 누르고 있어야 하는 동작에서만 켠다.")]
    /// <summary>
    /// 옛 "길게 누르세요" 줄. **지금은 비워 둔다** — 안내 문구가 이미 그 말을 한다.
    ///
    /// 다시 띄우고 싶으면 여기에 글자를 꽂으면 그때부터 <c>isHold</c> 에 맞춰 켜진다.
    /// </summary>
    [SerializeField] private TextMeshProUGUI interactHoldHint;

    [Header("상호작용 — 캐릭터 옆구리에 띄운다")]
    // ⚠ **월드 좌표(m)로 옆·위를 밀었더니 안 맞았습니다.** 캐릭터 루트가 발밑이 아니라
    //    어디에 있는지 몰라서(치비 캐릭터라 원점이 불확실) 늘 머리 위로 떴습니다.
    //    그래서 사람 위치를 화면 좌표로 바꾼 **뒤에** 픽셀로만 민다 — 카메라 각도나
    //    캐릭터 원점과 무관하게 항상 같은 자리(화면에서 옆으로 몇 px)로 뜬다.
    // ⚠ **이름을 새로 짓습니다.** 예전 필드(interactWorldLift · interactSideOffset ·
    //    interactScreenLift)는 이미 씬에 값이 박혀 있어서, 코드 기본값을 고쳐도
    //    씬에 남은 옛 값이 이깁니다. 새 필드는 처음 붙는 것이라 코드 기본값을 제대로 받습니다.
    // ⚠ **205.4 / 17.8 은 Play 모드에서 눈으로 맞춰서 찾은 값입니다.** (링이 커지면서
    //    130 으로는 캐릭터 몸통과 겹쳤습니다 — RingSize(104) × ActionScale(1.2) 로 반지름이
    //    이미 62px 라 130 으로는 절반 넘게 파고듭니다.) `ShipCoopHud.prefab` 에도 같은 값을
    //    직접 넣어 뒀습니다 — 여기 기본값만 고쳐서는 프리팹의 저장된 값을 못 이깁니다.
    [Tooltip("사람의 화면 좌표에서 오른쪽으로 이만큼(px) 민다. 옆구리 쪽으로 보이게 한다.")]
    [SerializeField] private float interactBadgeRightPx = 205.4f;

    [Tooltip("사람의 화면 좌표에서 위로 이만큼(px) 민다. 발밑이 아니라 몸통 높이로 올린다.")]
    [SerializeField] private float interactBadgeUpPx = 17.8f;

    /// <summary>지금 위에 떠 있어야 할 대상. 없으면 패널이 꺼져 있다.</summary>
    private Transform _interactAnchor;

    private RectTransform _interactRect;
    private RectTransform _canvasRect;
    private Camera _viewCamera;

    [Header("작업 그림")]
    [SerializeField] private Sprite taskIconHelm;
    [SerializeField] private Sprite taskIconSails;
    [SerializeField] private Sprite taskIconCannon;
    [SerializeField] private Sprite taskIconRepair;

    [Header("운반물 그림")]
    // 예전에는 작업 그림을 돌려썼다. 포탄이 대포로, 자재와 물이 나란히 망치로 나와서
    // 무엇을 들고 있는지 그림만으로는 알 수가 없었다. 셋 다 전용 그림이 생겨 나눈다.
    [SerializeField] private Sprite taskIconAmmo;
    [SerializeField] private Sprite taskIconPlank;
    [SerializeField] private Sprite taskIconWater;

    [Header("사건 단계 색")]
    [Tooltip("예고 중. 아직 아무것도 안 깎였다는 것을 흐리게 보여준다.")]
    [SerializeField] private Color eventWarning = new Color(1f, 1f, 1f, 0.45f);

    [Tooltip("이미 터졌다. 제한 시간을 세는 중.")]
    [SerializeField] private Color eventRunning = new Color(1f, 0.45f, 0.35f, 1f);

    [Header("색")]
    [SerializeField] private Color hpHealthy = new Color(0.45f, 0.85f, 0.55f);
    [SerializeField] private Color hpHurt = new Color(0.95f, 0.75f, 0.35f);
    [SerializeField] private Color hpCritical = new Color(0.95f, 0.40f, 0.35f);

    [Tooltip("체력 숫자가 이 아래로 내려가면 빨갛게 켜진다. 그 전에는 흰색 — 초록은 갑판 나무색과 겹쳐 잘 안 보였다.")]
    [SerializeField] private float hpCriticalBelow = 30f;
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

        if (interactPanel != null)
        {
            _interactRect = interactPanel.GetComponent<RectTransform>();
        }

        // 이 스크립트는 캔버스 뿌리에 붙어 있다. 상호작용 배지를 그 캔버스 좌표로 옮기려면
        // 그 캔버스의 RectTransform 이 필요하다. (Screen Space - Overlay)
        _canvasRect = GetComponent<RectTransform>();

        // 빌더가 놓아 둔 자리를 기억해 둔다. 까딱임은 이 높이를 기준으로 오르내린다.
        if (hurryCallout != null)
        {
            _hurryRect = hurryCallout.GetComponent<RectTransform>();
        }

        if (hurryArrow != null)
        {
            _hurryArrowBaseY = hurryArrow.anchoredPosition.y;
        }
    }

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

        UpdateCourseWarning();
        UpdateEvents();
        UpdateInteract();
    }

    /// <summary>
    /// 상호작용 배지를 **지금 화면을 보는 사람(LocalWorker)의 옆구리**로 옮긴다.
    ///
    /// 대상(자리 · 상자 · 짐) 위가 아니라 사람 몸 기준이다. 조타륜처럼 자리가 크거나
    /// 운반처럼 대상이 계속 바뀌는 경우, 대상 위에 띄우면 배지가 뚝뚝 튀거나 물건에
    /// 가려진다. 사람은 늘 화면 가운데 가깝게 있어 안정적이다.
    ///
    /// ⚠ **카메라가 다 움직인 뒤에 해야 합니다.** 배가 돌 때 카메라도 같이 따라 도는데
    ///    (ShipCoopShipTurn · ShipCoopCamera 모두 LateUpdate), Update 시점의 카메라 위치로
    ///    화면 좌표를 뽑으면 한 프레임 늦어 배지가 살짝 떨립니다.
    /// </summary>
    private void LateUpdate()
    {
        if (_interactRect == null || _canvasRect == null)
        {
            return;
        }

        if (_interactAnchor == null || !interactPanel.activeSelf)
        {
            return;
        }

        if (_viewCamera == null)
        {
            _viewCamera = Camera.main;
        }

        if (_viewCamera == null)
        {
            return;
        }

        Vector3 screen = _viewCamera.WorldToScreenPoint(_interactAnchor.position);

        // 대상이 카메라 뒤에 있으면(등 뒤에서 붙잡는 경우는 없지만 혹시나) 그리지 않는다.
        if (screen.z < 0f)
        {
            interactPanel.SetActive(false);
            return;
        }

        // 사람의 화면 좌표가 나온 뒤에 픽셀로만 민다. (위 헤더 주석 참고)
        screen.x += interactBadgeRightPx;
        screen.y += interactBadgeUpPx;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local))
        {
            _interactRect.anchoredPosition = local;
        }
    }

    /// <summary>
    /// 뱃머리가 틀어졌다고 화면 가장자리로 알린다.
    ///
    /// **속도가 왜 떨어지는지를 말해주는 유일한 표시입니다.** 조타에 붙은 사람은
    /// 게이지로 알지만, 수리하거나 포탄을 나르는 나머지 셋은 알 방법이 없었습니다.
    /// 가장자리를 물들이면 어디를 보고 있든 눈에 들어옵니다.
    ///
    /// ⚠ **조금 틀어진 것으로는 뜨지 않습니다.** cos 곡선이라 30° 까지는 손실이
    ///    10% 뿐인데, 그걸로도 경고를 띄우면 경고가 늘 켜져 있는 것이 되어
    ///    아무도 안 봅니다. 손실이 급해지는 35° 근처부터 켭니다.
    /// </summary>
    private void UpdateCourseWarning()
    {
        if (courseWarningRoot == null || voyage == null)
        {
            return;
        }

        // ⚠ **암초를 피하는 중에는 끈다.** 암초는 꺾어서 피하는 사건이라(Reef), 시키는
        //    대로 꺾으면 **반드시** 항로를 벗어난다. 그때 화면을 붉게 물들이면 잘하고
        //    있는 사람을 혼내는 꼴이고, 경고가 늘 켜져 있으면 정작 진짜로 항로를
        //    놓쳤을 때 아무도 안 본다. (파도는 반대라 그대로 둔다 — BigWave)
        //
        //    바위가 사라져도 배는 아직 꺾여 있으므로, 뱃머리를 되돌릴 틈을 주고 켠다.
        if (IsDodgingByLeavingCourse())
        {
            _courseGraceUntil = Time.time + courseWarnGraceSeconds;
        }

        if (Time.time < _courseGraceUntil)
        {
            if (courseWarningRoot.activeSelf)
            {
                courseWarningRoot.SetActive(false);
            }

            return;
        }

        // 1 이면 정면, 0.5 면 60° 로 최대까지 틀어진 상태다.
        float course = Mathf.Clamp01(voyage.CourseFactor);

        // 켤지 말지와 얼마나 세게 켤지. 경계에서 갑자기 켜지지 않도록 사이를 편다.
        float severity = Mathf.InverseLerp(courseWarnBelow, courseCriticalBelow, course);

        bool show = severity > 0f;
        if (courseWarningRoot.activeSelf != show)
        {
            courseWarningRoot.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        if (courseWarningVignette != null)
        {
            // 깜빡임. 0 까지 내려가면 꺼진 것처럼 보여서 바닥을 남긴다.
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.time * coursePulseSpeed * Mathf.PI * 2f);

            Color color = courseWarningVignette.color;
            color.a = Mathf.Lerp(courseAlphaMin, courseAlphaMax, severity) * pulse;
            courseWarningVignette.color = color;
        }

        if (courseWarningLabel != null)
        {
            // 각도를 숫자로 적지 않는다. 얼마나 틀어졌는지가 아니라
            // **무엇을 해야 하는지**가 필요한 순간이다.
            courseWarningLabel.text = severity >= 1f
                ? "항로를 크게 벗어났다\n<size=65%>조타를 가운데로 — 속도가 절반 가까이 떨어진다</size>"
                : "뱃머리가 틀어졌다\n<size=65%>조타를 가운데로</size>";
        }
    }

    /// <summary>
    /// 지금 **꺾어서 피해야 하는** 사건이 떠 있는가. (예고 중에도 참이다)
    ///
    /// 암초가 그렇다. 이 동안에는 항로를 벗어나는 것이 정답이므로 경고를 끈다.
    /// 어느 사건이 그런지는 사건 스스로가 안다.
    /// (<see cref="VoyageEvent.DodgedByLeavingCourse"/>)
    /// </summary>
    private static bool IsDodgingByLeavingCourse()
    {
        var active = VoyageEvent.Active;

        for (int i = 0; i < active.Count; i++)
        {
            if (active[i] != null && active[i].DodgedByLeavingCourse)
            {
                return true;
            }
        }

        return false;
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


    private void UpdateHealth()
    {
        if (health == null)
        {
            return;
        }

        float ratio = health.MaxHp <= 0f ? 0f : Mathf.Clamp01(health.CurrentHp / health.MaxHp);

        // 막대를 뺐다. 얼마나 남았는지는 이제 글자 색과 숫자만으로 말한다.
        if (hpFill != null)
        {
            hpFill.fillAmount = ratio;
            hpFill.color = ratio > 0.6f ? hpHealthy : ratio > 0.3f ? hpHurt : hpCritical;
        }

        if (hpLabel != null)
        {
            hpLabel.text = $"{health.CurrentHp:F0}";
            hpLabel.color = health.CurrentHp <= hpCriticalBelow ? hpCritical : Color.white;
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

        float progress = Mathf.Clamp01(game.Progress01);
        float expected = game.ExpectedProgress01;

        if (progressFill != null)
        {
            progressFill.fillAmount = ToVoyageFillAmount(progress);
        }

        // 배 마커 · 기준선 · 지연 칸은 흰색/파란색 그림과 달리 여전히 트랙 구간
        // (X 359~1787)만 감싼 자식(Track) 위에서 움직이므로, 진행률을 그대로 쓴다.
        PlaceOnBar(shipMarker, progress);
        PlaceOnBar(expectedMarker, expected);
        SpanOnBar(delayFill, progress, expected);
        UpdateHurryCallout(progress, expected);

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

    /// <summary>
    /// 늦었다는 것을 **한 번 짚어 주는** 알림. 기준선 위에 떴다 사라지며 아래를 가리킨다.
    ///
    /// 붉은 구간은 "이만큼 늦었다"를 늘 보여주지만 그게 무슨 뜻인지는 말해주지
    /// 않는다. 벌어지는 순간에 글씨로 한 번 가르쳐 주면, 그 다음부터는 붉은 구간만
    /// 봐도 읽힌다. 그림이 못 하는 일을 글씨가 대신 하는 자리다.
    ///
    /// ⚠ **계속 띄우면 안 된다.** 3분 내내 "서둘러"가 떠 있으면 두 번째부터는
    ///    아무도 안 본다. 잠깐 떴다 사라지고, **더 나빠졌을 때만** 다시 온다.
    /// </summary>
    private void UpdateHurryCallout(float progress, float expected)
    {
        if (hurryCallout == null)
        {
            return;
        }

        float gap = expected - progress;

        // 충분히 뒤처진 상태가 얼마나 이어졌는지. 경계에서 깜빡이면 알림도 깜빡인다.
        _hurryBehindFor = gap >= hurryBehindAtLeast ? _hurryBehindFor + Time.deltaTime : 0f;

        // 거의 따라잡았으면 다음 번을 "처음 벌어진 것" 으로 셈한다.
        if (gap <= hurryClearBelow)
        {
            _hurryWarnedAtGap = -1f;
        }

        // 아직 한 번도 안 띄웠으면 기준값을, 이미 띄웠으면 그때보다 더 나빠졌는지를 본다.
        float trigger = _hurryWarnedAtGap < 0f
            ? hurryBehindAtLeast
            : _hurryWarnedAtGap + hurryWorsenBy;

        if (gap >= trigger && _hurryBehindFor >= hurryHoldSeconds && Time.time >= _hurryNextAt)
        {
            _hurryUntil = Time.time + hurryShowSeconds;
            _hurryNextAt = Time.time + hurryCooldownSeconds;
            _hurryWarnedAtGap = gap;

            if (hurryLabel != null)
            {
                hurryLabel.text = HurryMessage();
            }
        }

        bool show = Time.time < _hurryUntil;
        if (hurryCallout.activeSelf != show)
        {
            hurryCallout.SetActive(show);
        }

        if (!show || _hurryRect == null)
        {
            return;
        }

        // **기준선 자리**. 붉은 구간의 오른쪽 끝, 곧 "지금쯤 여기 와 있어야 한다" 는 곳이다.
        //
        // 구간 한가운데에 놓아 봤더니 "이 빨간 게 문제다" 까지만 말하고 끝났다. 끝을
        // 가리키면 **가야 할 곳**을 말하게 되고, 붉은 구간은 거기까지 남은 빚으로 읽힌다.
        // 늦을수록 기준선이 앞서가므로 팻말도 같이 오른쪽으로 밀려난다.
        PlaceOnBar(_hurryRect, expected);

        float shown = hurryShowSeconds - (_hurryUntil - Time.time);

        // 톡 튀어나오기. 살짝 넘겼다 돌아와야 곁눈에도 걸린다.
        float pop = EaseOutBack(Mathf.Clamp01(shown / HurryPopSeconds));
        _hurryRect.localScale = Vector3.one * Mathf.LerpUnclamped(0.55f, 1f, pop);

        if (hurryGroup != null)
        {
            // 끝에서 흐려지며 사라진다. 툭 꺼지면 뭐가 있었는지도 모르고 지나간다.
            hurryGroup.alpha = Mathf.Clamp01((_hurryUntil - Time.time) / HurryFadeSeconds);
        }

        if (hurryArrow != null)
        {
            hurryArrow.anchoredPosition =
                new Vector2(0f, _hurryArrowBaseY + Mathf.Sin(shown * HurryBobSpeed) * HurryBobPixels);
        }
    }

    /// <summary>
    /// 왜 늦는지를 짚어서 말한다.
    ///
    /// ⚠ **"서둘러라" 는 정보가 없다.** 처음 하는 사람은 자기가 왜 느린지를 모른다.
    ///    속도는 <c>SailPower01 × CourseFactor</c> 둘로만 정해지므로(ShipVoyage),
    ///    둘 중 발목을 잡는 쪽을 그대로 **지시**로 바꾼다.
    ///
    /// 돛을 먼저 본다. 돛은 아예 0 까지 풀릴 수 있어서 손해가 훨씬 크다.
    /// (<see cref="SailTask.IsSlack"/> 와 같은 값을 <see cref="voyage"/> 에서 바로 읽는다 —
    /// SailTask 를 따로 연결하지 않아도 되고, 돛 작업이 없는 씬에서도 안전하다)
    /// </summary>
    private string HurryMessage()
    {
        if (voyage != null)
        {
            if (voyage.SailPower01 < hurrySailBelow)
            {
                return "돛을 당겨!";
            }

            // 항로도 같은 이유로 걸러 낸다 — 암초를 피하느라 꺾은 사람에게
            // "키를 잡아" 라고 하면 안 된다. (UpdateCourseWarning 의 주석 참고)
            if (voyage.CourseFactor < courseWarnBelow && !IsDodgingByLeavingCourse())
            {
                return "키를 잡아!";
            }
        }

        return "더 빨리!";
    }

    /// <summary>끝에서 살짝 넘겼다 돌아오는 곡선. 톡 튀어나오는 느낌을 만든다.</summary>
    private static float EaseOutBack(float t)
    {
        const float overshoot = 1.70158f;

        t -= 1f;
        return 1f + (overshoot + 1f) * t * t * t + overshoot * t * t;
    }


    /// <summary>
    /// 상단 가운데 사건 카드.
    ///
    /// **맨 윗줄은 침수가 먼저 쓴다.** 침수는 사건이 아니라 상태지만
    /// (<see cref="VoyageEvent.Active"/> 에 안 들어온다) 플레이어 입장에서는
    /// "지금 벌어지고 있어서 누군가 가야 하는 일" 이라 같은 카드로 보여준다.
    ///
    /// ⚠ **침수는 구멍을 막아도 안 끝난다.** 선체 파손 사건은 수리하는 순간
    ///    <c>Succeed</c> 로 목록에서 빠지는데, 들어온 물은 그대로 남아 계속 HP 를 깎는다.
    ///    그래서 물이 남아 있는 동안 이 카드가 대신 떠 있어야 한다. 안 그러면
    ///    **HP 가 왜 줄어드는지 화면 어디에도 없다.** (2장 · 4장)
    /// </summary>
    private void UpdateEvents()
    {
        if (eventRows == null)
        {
            return;
        }

        var active = VoyageEvent.Active;

        bool flood = flooding != null && flooding.HasWater;
        int offset = flood ? 1 : 0;

        for (int i = 0; i < eventRows.Length; i++)
        {
            EventRow row = eventRows[i];
            if (row == null || row.root == null)
            {
                continue;
            }

            if (flood && i == 0)
            {
                row.root.SetActive(true);
                FillFloodRow(row);
                continue;
            }

            bool used = i - offset < active.Count;
            row.root.SetActive(used);

            if (!used)
            {
                continue;
            }

            VoyageEvent e = active[i - offset];

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

            // 제목과 보조 문구를 따로 쓰는 카드. (`label` 하나로 쓰던 옛 방식과 함께 둔다)
            //
            // 갑판 이름은 **제목이 아니라 보조 문구로** 간다. 제목은 무슨 일인지만
            // 말해야 크게 뽑을 수 있고, 어디인지는 그 아래 작은 줄이 말해준다.
            if (row.title != null)
            {
                row.title.text = e.WarningText;
            }

            if (row.label != null && row.title != null)
            {
                string hint = e.IsWarning || e.HintIsUrgent ? e.LiveHint() : null;
                ShipDeck deck = e.Where;

                if (deck != null)
                {
                    hint = string.IsNullOrEmpty(hint) ? deck.DeckName : $"{hint}  ·  {deck.DeckName}";
                }

                row.label.text = hint ?? string.Empty;
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

            // 예고인지 발생인지를 글자로도 말한다. 색만으로는 색각 이상이 있으면 안 읽힌다.
            if (row.badge != null)
            {
                row.badge.color = stage;
            }

            if (row.badgeLabel != null)
            {
                row.badgeLabel.text = e.IsWarning ? "예고" : "발생";
            }

            // 셀 것이 없으면 게이지도 숫자도 숨긴다. 줄어들지 않는 게이지는 오해를 준다.
            // 예고 중이면 발생까지, 터진 뒤면 실패까지 센다.
            bool timed = e.HasCountdown;

            if (row.timer != null)
            {
                row.timer.gameObject.SetActive(timed);

                if (timed)
                {
                    row.timer.fillAmount = e.Remaining01;
                    row.timer.color = stage;
                }
            }

            if (row.timerTrack != null)
            {
                row.timerTrack.SetActive(timed);
            }

            if (row.seconds != null)
            {
                row.seconds.gameObject.SetActive(timed);

                if (timed)
                {
                    row.seconds.text = $"{e.RemainingSeconds}초";
                    row.seconds.color = stage;
                }
            }
        }
    }

    /// <summary>
    /// 침수 카드 한 장을 채운다.
    ///
    /// 게이지를 안 띄운다. 양이 40% 든 45% 든 할 일이 안 바뀐다 — 퍼내러 가거나,
    /// 구멍이 열려 있으면 먼저 막거나 둘 중 하나다. 그래서 **무엇을 해야 하는지만** 적는다.
    ///
    /// ⚠ 구멍이 열려 있으면 **퍼내지 말라고 말해준다.** 구멍 하나가 초당 2.5% 를 붓고
    ///    양동이 왕복이 4초라, 막기 전에 퍼내면 물이 0 으로 안 내려가고 제자리를 돈다.
    /// </summary>
    private void FillFloodRow(EventRow row)
    {
        bool leaking = flooding.LeakingPoints > 0;

        if (row.title != null)
        {
            row.title.text = "침수";
        }

        if (row.label != null)
        {
            row.label.text = leaking
                ? $"구멍 {flooding.LeakingPoints}개가 새는 중 — 수리가 먼저다"
                : "양동이로 퍼내라";
        }

        if (row.icon != null)
        {
            row.icon.sprite = eventIconFlood;
            row.icon.enabled = eventIconFlood != null;
        }

        // 침수는 예고 없이 이미 벌어진 일이다. 늘 '발생' 쪽 색을 쓴다.
        if (row.accent != null)
        {
            row.accent.color = eventRunning;
        }

        if (row.badge != null)
        {
            row.badge.color = eventRunning;
        }

        if (row.badgeLabel != null)
        {
            row.badgeLabel.text = "발생";
        }

        // 셀 것이 없다. 물은 시간이 아니라 양동이로만 줄어든다.
        if (row.timer != null)
        {
            row.timer.gameObject.SetActive(false);
        }

        if (row.timerTrack != null)
        {
            row.timerTrack.SetActive(false);
        }

        if (row.seconds != null)
        {
            row.seconds.gameObject.SetActive(false);
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
            _interactAnchor = null;
            return;
        }

        // 붙어 있으면 그 작업의 진행도를, 근처면 붙으라는 안내를 띄운다.
        TaskBase current = LocalWorker.Current;
        TaskBase nearby = LocalWorker.Nearby;

        var carry = LocalWorker.GetComponent<CarryTask>();
        if (carry != null && carry.IsCarrying)
        {
            // ⚓ 들고 있는 동안은 손을 떼면 놓치므로 계속 눌러야 한다는 것을 말해준다.
            // 배지는 대상이 아니라 **사람 옆구리**에 뜬다 — 대상은 걷는 동안 계속 바뀐다.
            Show(WithHint($"{CarryTask.NameOf(carry.Carrying)} 운반 중", CarryHintOf(carry)),
                 -1f, IconOfCargo(carry.Carrying), LocalWorker.transform, isHold: true);
            return;
        }

        if (current != null)
        {
            Show(WithHint(current.DisplayName, HintOf(current)), GaugeOf(current), IconOf(current),
                 TwoSidedGauge(current), KeyOf(current), LocalWorker.transform, isHold: false);
            return;
        }

        // 🔨 자재를 받은 구멍 앞이면 붙기 전이라도 **K 를 띄운다.** K 한 번에 붙고 친다(TaskWorker).
        //    다른 자리가 더 가까워도 이쪽이 먼저다 — K 는 어차피 이 구멍으로 간다.
        RepairTask hammerable = RepairTask.FindHammerable(LocalWorker, LocalWorker.transform.position);
        if (hammerable != null)
        {
            Show(WithHint(hammerable.DisplayName, HintOf(hammerable)), GaugeOf(hammerable), IconOf(hammerable),
                 false, KeyOf(hammerable), LocalWorker.transform, isHold: false);
            return;
        }

        // 자재가 아직 없는 구멍. 붙어 봐야 두드려도 안 먹으니 Space 대신 자재부터 가져오라고 말한다.
        if (nearby is RepairTask waiting)
        {
            Show(WithHint(waiting.DisplayName, HintOf(waiting)), GaugeOf(waiting), IconOf(waiting),
                 false, KeyOf(waiting), LocalWorker.transform, isHold: false);
            return;
        }

        // 붙기 전이다. 키는 키캡이 말해주므로 이름에 또 적지 않는다.
        if (nearby != null)
        {
            Show(nearby.DisplayName, -1f, IconOf(nearby), LocalWorker.transform, isHold: false);
            return;
        }

        // 포탄 상자는 자리(TaskBase)가 아니라서 Nearby 에 잡히지 않는다.
        // 그래서 상자 앞에 서면 아무 안내도 안 떴다.
        // 자리에 붙는 것과 같은 키를 쓰지만, 무엇이 나오는지는 말해줘야 한다.
        // 상자마다 나오는 것이 다르다. 무엇이 나오는지 말해주지 않으면
        // 갑판에 색깔 큐브만 놓여 있고 무슨 상자인지 알 수가 없다.
        if (carry != null)
        {
            // 갑판에 놓인 것이 먼저다. 상자보다 가까이 있고, 주우러 온 것이다.
            DroppedCargo lying = carry.FindReachableDrop();
            if (lying != null)
            {
                Show(WithHint($"{CarryTask.NameOf(lying.Kind)} 줍기", "길게 눌러서 들고 가기"),
                     -1f, IconOfCargo(lying.Kind), LocalWorker.transform, isHold: true);
                return;
            }

            AmmoBox box = carry.FindReachableBox();
            if (box != null)
            {
                // 침수 중인 양동이 앞은 "집지 말라"는 경고라 홀드 안내가 아니다.
                bool boxIsHold = box.Kind != Cargo.Water || flooding == null || flooding.LeakingPoints == 0;

                Show(WithHint($"{CarryTask.NameOf(box.Kind)} 집기", BoxHintOf(box)),
                     -1f, IconOfCargo(box.Kind), LocalWorker.transform, boxIsHold);
                return;
            }
        }

        interactPanel.SetActive(false);
        _interactAnchor = null;
    }

    /// <summary>
    /// 붙기 · 집기 · 놓기. 자리에 붙기 전에는 언제나 이 키다.
    ///
    /// ⚠ **&lt;size&gt; 로 한 번 줄여서 쓴다.** 다른 키캡은 글자 한두 개(<c>K</c> ·
    ///    <c>J · L</c>)인데 이것만 다섯 글자다. 자동 크기는 상한(KeyFontMax)까지 키우고
    ///    멈추는데, "Space" 는 그 상한에서도 칸에 들어가 버려서 <b>결국 "K" 와 같은
    ///    크기로 그려진다.</b> 그러면 도넛 구멍을 가로로 꽉 채워 답답하다.
    ///    이 태그가 그 한 글자짜리 상한을 이것에만 80% 로 낮춘다.
    /// </summary>
    private const string KeyInteract = "<size=80%>Space</size>";

    // ------------------------------------------------------------
    // 🎮 **완드가 붙으면 키캡을 완드 버튼으로 바꾼다.** (KEY_MAPPING.md 배 협동)
    //
    //    완드에는 버튼이 A(버튼 1) · B(버튼 2) 로 새겨져 있고, 배가 키캡에 띄우는 것은
    //    전부 오른손 쪽이다.
    //
    //      키보드   완드
    //      Space    A      붙기 · 집기 · 놓기 · 장전   오른손 버튼 1
    //      K        B      발사 · 망치질              오른손 버튼 2 (내리치기도 됨)
    //      J · L    L · R  조타 · 돛                  양손 기울기 · 비틀기
    //
    //    ⚠ **글자 크기는 키보드와 같다.** 같은 칸 · 같은 자동 크기 범위를 그대로 쓴다.
    //       "A" · "B" 는 "K" 와 같은 한 글자라 같은 크기로 서고, "L · R" 은 "J · L" 과
    //       생김새가 같아 같은 크기로 줄어든다.
    //
    //    ⚠ 조타 · 돛에 "기울기" 같은 한글을 쓰지 않는다. 키캡 글꼴(Fredoka)에는 한글이
    //       없어 네모로 나온다. 어떻게 움직이는지는 아래 안내 줄(HintOf)이 한글로 말한다.
    // ------------------------------------------------------------

    private const string WandInteract = "A";
    private const string WandFire = "B";
    private const string WandTilt = "L · R";

    /// <summary>
    /// 이 컴퓨터에서 지금 완드로 하고 있는가.
    ///
    /// 네트워크에서는 캐릭터의 입력이 서버가 되살린 것(ShipCoopNetworkedController)이라
    /// 그걸 봐서는 무엇이 꽂혀 있는지 모른다. 러너에 붙은 <b>이 컴퓨터의 기기</b>를 먼저 본다.
    /// 로컬 테스트 씬에는 러너가 없으므로 그때는 캐릭터에 붙은 것을 본다.
    ///
    /// 동글만 꽂고 완드를 안 켰으면 키보드로 하고 있는 것이니 키보드 키를 보여준다.
    /// </summary>
    private bool UsingWand()
    {
        IPlayerController devices = UnderTheSea.MiniGames.ShipCoop.Net.ShipCoopInputProvider.LocalDevices;

        if (devices == null && LocalWorker != null)
        {
            devices = LocalWorker.Input;
        }

        return devices is IotPlayerController wand && wand.AnyWandConnected;
    }

    private void Show(string text, float gauge01, Sprite icon, Transform anchor, bool isHold)
    {
        Show(text, gauge01, icon, false, UsingWand() ? WandInteract : KeyInteract, anchor, isHold);
    }

    /// <summary>
    /// 그 자리에서 **실제로 일하는** 키. 키캡에 그대로 나간다.
    ///
    /// ⚠ 자리에 **붙는** 키(Space · A)와 붙은 다음에 **일하는** 키는 다르다.
    ///    붙고 나면 Space 는 그 자리에서 할 일이 없다.
    /// </summary>
    private string KeyOf(TaskBase task)
    {
        bool wand = UsingWand();

        switch (task)
        {
            case CannonTask _: return wand ? WandFire : "K";
            case RepairTask _: return wand ? WandFire : "K";
            case HelmTask _: return wand ? WandTilt : "J · L";
            case SailTask _: return wand ? WandTilt : "J · L";
            default: return wand ? WandInteract : KeyInteract;
        }
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
    /// <param name="anchor">배지를 띄울 기준. 지금은 늘 LocalWorker.transform(사람 옆구리)이다.</param>
    /// <param name="isHold">떼면 놓치는 동작인가. "길게 누르세요" 안내를 켤지 정한다.</param>
    private void Show(string text, float gauge01, Sprite icon, bool twoSided, string key, Transform anchor, bool isHold)
    {
        interactPanel.SetActive(true);
        _interactAnchor = anchor;

        if (interactLabel != null)
        {
            interactLabel.text = text;
        }

        if (interactKeycap != null)
        {
            interactKeycap.text = key;
        }

        if (interactHoldHint != null)
        {
            interactHoldHint.gameObject.SetActive(isHold);
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
    /// 붙기 전에는 키캡이 Space 를 보여주는데, 붙고 나면 자리 이름만 남아서
    /// **거기서 뭘 해야 하는지 화면에 아무 데도 없었습니다.**
    /// 특히 대포는 붙어야만 K 가 먹기 때문에, 안 붙고 K 를 누르면
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

        // ⚓ 셋 다 누르고 있어야 들려 있다. 집는 순간 그 말을 해줘야 한다.
        //    한 번 누르고 손을 떼면 그 자리에 도로 떨어져서 고장 난 줄 안다.
        //
        // 아래 "길게 누르세요" 줄은 뺐다. 이 줄이 이미 같은 말을 하고 있어서
        // 한 화면에 두 번 적혀 있었다. (ShipCoopHudV2Art.BuildAction)
        return "길게 눌러서 들고 가기";
    }

    /// <summary>들고 있는 것을 어디로 가져가야 하는지. 손에 든 것마다 목적지가 다르다.</summary>
    private string CarryHintOf(CarryTask carry)
    {
        switch (carry.Carrying)
        {
            // ⚓ **셋 다 꾹 누르고 있어야 들려 있다.** 떼는 순간이 곧 내려놓는 순간이라
            //    "싣기 · 건네기 · 버리기" 가 아니라 **손을 떼라**고 말해줘야 한다.
            //    누르라고 적으면 이미 누르고 있는 사람에게 누르라고 말하는 셈이다.
            //
            // ⚠ "수리 지점으로" 가 아니라 "**자재가 필요한** 수리 지점으로" 다.
            //    이미 자재를 받은 지점 앞에서 손을 떼면 건네기가 안 되고 갑판에 떨어진다.
            //    그냥 "수리 지점으로" 라고 하면 이미 그 앞에 서 있는 사람에게
            //    거기로 가라고 말하는 셈이라 고장 난 줄 안다.
            //
            //    이 문구만 두 줄이 된다. **줄바꿈을 직접 넣는다.** 자동 줄바꿈에
            //    맡겼더니 칸 폭이 아슬아슬해서 "자재가 필요한 수리 지점으" 에서
            //    잘렸다. 어디서 끊길지는 글꼴 · 화면 크기에 따라 달라지므로,
            //    끊길 자리를 문구가 직접 정한다.
            //
            //    둘째 줄은 `<nobr>` 로 묶는다. 안 묶으면 한글은 아무 데서나 잘려서
            //    "수리 지점으 / 로" 가 된다.
            case Cargo.Ammo:
                return carry.FindLoadableCannon() != null
                    ? "손 떼서 싣기"
                    : "꾹 누른 채로 · 대포로";

            // 건넨 다음 할 일(K 연타)까지 미리 말한다. 건네고 나면 손이 비어 무엇을 누를지 몰랐다.
            // 키 이름은 한글 글꼴로 나가는 줄이라 K · B 같은 영문 한 글자만 쓴다.
            case Cargo.Plank:
                return carry.FindPointWantingPlank() != null
                    ? $"손 떼서 건네기 ·\n<nobr>이어서 {(UsingWand() ? WandFire : "K")} 연타로 수리</nobr>"
                    : "꾹 누른 채로 ·\n<nobr>자재가 필요한 수리 지점으로</nobr>";

            case Cargo.Water:
                return carry.FindReachableDump() != null
                    ? "손 떼서 버리기"
                    : "꾹 누른 채로 · 노란색 뱃전으로";

            default:
                return null;
        }
    }

    /// <summary>들고 있는 것에 맞는 그림.</summary>
    private Sprite IconOfCargo(Cargo cargo)
    {
        switch (cargo)
        {
            case Cargo.Ammo: return taskIconAmmo;
            case Cargo.Plank: return taskIconPlank;
            case Cargo.Water: return taskIconWater;
            default: return null;
        }
    }

    private string HintOf(TaskBase task)
    {
        switch (task)
        {
            // 키 이름은 키캡이 말한다. 여기는 **무엇을 하는지**만 적는다.
            case CannonTask cannon:
                return cannon.Ammo > 0
                    ? $"연타해서 발사  ·  포탄 {cannon.Ammo}/{cannon.MaxAmmo}"
                    : "포탄이 없다 — 상자에서 날라라";

            // 자재가 없으면 두드려도 안 먹는다. 그 말을 안 하면 고장 난 줄 안다.
            // 자재를 받았으면 붙기 전에도 이 줄이 뜬다 — Space 없이 연타만 하면 된다.
            case RepairTask repair:
                return repair.CanHammer ? "연타해서 수리" : "자재가 필요하다 — 갈색 상자에서";

            // 조타와 돛은 두 키가 서로 반대 방향이라 어느 쪽이 무엇인지 적어준다.
            // 완드는 키가 아니라 손 동작이라 어떻게 움직이는지를 적는다. (ShipCoopInput.Steer · SailPull)
            case HelmTask _: return UsingWand() ? "양손 기울여 좌 · 우" : "J 좌 · L 우";
            case SailTask _: return UsingWand() ? "양손 비틀어 당기기 · 풀기" : "L 당기기 · J 풀기";
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
