using TMPro;
using UnityEngine;

/// <summary>
/// <b>정답과 내가 판 그림을 번갈아 보여준다.</b> 결과 화면과 힌트 두 곳에서 돈다.
///
/// <b>왜 번갈아인가</b> — 정답판을 옆에 같이 띄우는 방식을 두 번 시도하고 버렸다.
///
/// <list type="number">
///   <item>월드에 판을 하나 더 놓고 나란히 담기 — 두 판을 담으려면 화면 폭의 83% 가
///   필요한데 HUD 가 좌우를 쓰고 있어 80% 밖에 없다. <b>판 사이 간격을 바꿔도 이
///   83% 는 안 변한다</b> — 카메라가 담을 것에 비례해 물러나기 때문이다.</item>
///
///   <item>20×20 두 색 텍스처를 UI 에 띄우기 — 공개 화면에서 도안이 읽히는 것은
///   색이 아니라 <b>돌 무늬가 다르기 때문</b>이고(색 차이는 22.7% 뿐이다), 무늬 없는
///   평면으로는 재현할 수 없다.</item>
/// </list>
///
/// 번갈아 보여주면 판을 <b>full 크기로 돌 재질 그대로</b> 쓴다. 같은 자리에서 겹쳐
/// 보므로 차이를 찾기에도 나란히 놓는 것보다 낫다 — 눈이 위치를 다시 안 잡는다.
///
/// ⚠ 지금은 <b>솔로 전용</b>이다. 네트워크는 <c>MineLocalView</c> 가 단계를 정하므로
///   거기에 따로 붙여야 한다.
/// </summary>
public class MineAnswerView : MonoBehaviour
{
    [Header("연결 — 비워두면 씬에서 찾는다")]
    [SerializeField] private MineGame game;
    [SerializeField] private MineGridView view;

    [Header("바꾸는 주기")]
    [Tooltip("결과 화면에서 몇 초마다 바꿀 것인가.")]
    [SerializeField, Min(0.3f)] private float swapSeconds = 1.5f;

    [Tooltip("힌트 볼 때 몇 초마다 바꿀 것인가. 힌트는 짧으므로(기본 3초) 더 빠르게 넘긴다.")]
    [SerializeField, Min(0.1f)] private float hintSwapSeconds = 0.5f;

    [Header("글자 — 없어도 된다")]
    [Tooltip("지금 무엇이 보이는지 적을 곳.")]
    [SerializeField] private TMP_Text label;

    [SerializeField] private string myDrawingLabel = "내가 판 그림";
    [SerializeField] private string answerLabel = "정답";

    private float _timer;
    private bool _showingAnswer;

    /// <summary>지금 교대를 돌리는 중인가. 시작 방향을 한 번만 맞추려고 기억한다.</summary>
    private bool _toggling;

    private void Awake()
    {
        if (game == null) game = FindAnyObjectByType<MineGame>();
        if (view == null) view = FindAnyObjectByType<MineGridView>();
    }

    private void Update()
    {
        if (game == null || view == null) return;

        bool finished = game.State == MineState.Finished;
        bool hinting = game.HintShowing;

        if (finished || hinting)
        {
            // ⚠ 교대를 시작할 때 드라이버가 이미 켜 둔 쪽에서 출발해야 한다.
            //   힌트는 TryHint 가 Answer 를, 결과는 EnterFinished 가 Result 를 켠다.
            //   안 맞추면 첫 번째 교대에서 같은 그림으로 바뀌어 한 박자 쉰다.
            if (!_toggling)
            {
                _toggling = true;
                _showingAnswer = hinting;
                _timer = 0f;
            }

            Tick(hinting ? hintSwapSeconds : swapSeconds);
            return;
        }

        _toggling = false;

        // ⚠ 교대를 멈출 때 정답이 보이는 채로 남으면 안 된다. 결과 화면에서 이
        //   컴포넌트를 끈 경우이므로 내가 판 그림으로 돌려놓는다. 판이 아직 안 끝났을
        //   때는 드라이버가 겹칠 단계를 정하므로 건드리지 않는다 — 힌트가 끝나면
        //   TickHint 가 None 으로 돌려놓는다.
        if (_showingAnswer && finished) view.SetOverlay(MineOverlay.Result);

        _timer = 0f;
        _showingAnswer = false;
        if (label != null) label.text = string.Empty;
    }

    private void Tick(float period)
    {
        _timer += Time.deltaTime;

        if (_timer >= period)
        {
            _timer = 0f;
            _showingAnswer = !_showingAnswer;

            // ⚠ Drawing 이 아니라 Answer 다. Drawing 은 힌트용이라 판 칸을 회색으로
            //   빼는데, 그러면 정답 위에 내가 판 자리가 겹쳐 보여서 헷갈린다.
            //   Answer 는 공개 7초와 똑같이 정답만 그린다.
            //
            //   자리 기준은 EnterFinished 가 넣어준 SetTargetOffset 을 그대로 쓴다.
            //   채점이 맞춘 자리와 같아야 비교가 된다.
            view.SetOverlay(_showingAnswer ? MineOverlay.Answer : MineOverlay.Result);
        }

        if (label != null) label.text = _showingAnswer ? answerLabel : myDrawingLabel;
    }
}
