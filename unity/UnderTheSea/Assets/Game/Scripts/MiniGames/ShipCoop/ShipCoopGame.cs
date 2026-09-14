using System;
using System.Collections.Generic;
using UnityEngine;
using UnderTheSea.MiniGames.ShipCoop.Net;

/// <summary>게임의 진행 상태</summary>
public enum ShipCoopState
{
    /// <summary>아직 출항 전</summary>
    Ready,

    /// <summary>항해 중</summary>
    Sailing,

    /// <summary>목적지 도착 — 성공</summary>
    Cleared,

    /// <summary>침몰 — 실패</summary>
    Sunk,

    /// <summary>시간 초과 — 실패</summary>
    TimeOver,
}

/// <summary>
/// 항로상의 한 구간. 시간이 아니라 진행도로 나뉜다.
/// 진행도가 startProgress01 을 넘으면 이 페이즈가 시작된다.
/// </summary>
[Serializable]
public class VoyagePhase
{
    public string name = "페이즈";

    [Range(0f, 1f)]
    public float startProgress01 = 0f;
}

/// <summary>
/// 배 협동 게임 전체를 굴리는 곳. 씬에 하나만 둔다.
///
/// 하는 일
///   - 제한시간과 진행도를 센다
///   - 페이즈를 넘긴다
///   - 승패를 판정하고 점수를 계산한다
///   - 결과를 로비에 보고한다
///
/// 승패
///   성공   제한시간 안에 목적지 도착
///   실패   배 HP 0 (침몰)  또는  시간 초과
///
/// 자세한 규칙은 SHIPCOOP.md 를 따른다.
/// </summary>
public class ShipCoopGame : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private ShipHealth health;
    [SerializeField] private ShipVoyage voyage;

    [Tooltip("비워두면 씬에서 자동으로 찾는다. 없으면 침수가 굴러가지 않는다.")]
    [SerializeField] private ShipFlooding flooding;

    [Header("제한시간 (초)")]
    [SerializeField] private float timeLimit = 180f;

    // 출항 구간(0 ~ 페이즈 1)을 짧게 두는 이유
    //
    //   10% 는 60m 다. 돛을 안 잡으면 48초, 잡아도 12초.
    //   혼자 테스트하면 그만큼을 빈 바다에서 보내게 된다. (SHIPCOOP.md 5장)
    //
    // 그래서 문턱을 5% 로 내렸다. 여기를 다시 올릴 거면 출항 구간의
    // EventScheduler 계획도 같이 봐야 한다. 둘이 짝이다.
    //
    // 페이즈 문턱은 시간이 아니라 진행도 비율이라 제한시간을 줄여도 손댈 것이 없다.
    // 3분 기준으로 각 구간이 차지하는 시간은 대략 이렇게 된다. (평균 속도로 갔을 때)
    //
    //   출항 0~9초 · 페이즈1 9~63초 · 페이즈2 63~108초 · 페이즈3 108~153초 · FINAL 153~180초
    //
    // FINAL 이 27초로 제일 짧은데, 이건 모든 것이 잘 풀렸을 때의 바닥값이다.
    // 그 구간은 돌풍과 파도로 속도가 떨어지므로 실제로는 더 길게 머문다.
    // 그래도 짧게 느껴지면 FINAL 문턱만 0.85 → 0.80 으로 내린다.
    [Header("페이즈 — 진행도가 이 지점을 넘으면 시작한다")]
    [SerializeField]
    private List<VoyagePhase> phases = new List<VoyagePhase>
    {
        new VoyagePhase { name = "출항",        startProgress01 = 0.00f },
        new VoyagePhase { name = "페이즈 1",    startProgress01 = 0.05f },
        new VoyagePhase { name = "페이즈 2",    startProgress01 = 0.35f },
        new VoyagePhase { name = "페이즈 3",    startProgress01 = 0.60f },
        new VoyagePhase { name = "FINAL STORM", startProgress01 = 0.85f },
    };

    [Header("점수 배점")]
    [SerializeField] private float hpScore = 10f;
    [SerializeField] private float enemyScore = 100f;
    [SerializeField] private float leakScore = 30f;
    [SerializeField] private float obstacleScore = 20f;

    [Tooltip("남은 시간 1초당 점수. 성공했을 때만 준다. 돛을 잘 다룬 보상이다.\n\n" +
             "제한시간을 줄이면 여기도 같이 올려야 한다. 남길 수 있는 시간의 상한이\n" +
             "곧 이 보상의 상한이라, 제한시간이 줄면 돛의 값어치가 조용히 떨어진다.\n" +
             "3분(여유 60초)에서 8 이면 상한 480 으로, 5분(여유 100초)에 5 였을 때의 500 과 비슷하다.")]
    [SerializeField] private float timeBonusPerSecond = 8f;

    [Header("시작")]
    [Tooltip("켜면 씬이 시작되자마자 출항한다. Develop 씬에서 혼자 테스트할 때 쓴다.")]
    [SerializeField] private bool autoStart = true;

    private float _elapsed;
    private int _phaseIndex = -1;
    private int _enemiesDestroyed;
    private int _leaksSealed;
    private int _obstaclesAvoided;

    /// <summary>지금 상태</summary>
    public ShipCoopState State { get; private set; } = ShipCoopState.Ready;

    /// <summary>
    /// **이 컴퓨터가 게임을 계산하는 쪽인가.** (SHIPCOOP.md 11장)
    ///
    /// 판정을 4대가 각자 하면 서로 다른 답이 나옵니다. 내 화면에선 물을 다 퍼냈는데
    /// 옆 사람 화면에선 아직 차 있고, 둘 다 자기가 맞다고 믿습니다.
    /// **그래서 계산은 한 대만 하고 나머지는 결과를 받아 그립니다.**
    ///
    /// 화면 쪽(HUD · 카메라 · 소리)은 이 값을 보지 않습니다. 전원이 다 그려야 하니까요.
    /// 보는 것은 **공유 상태를 바꾸는 쪽**뿐입니다.
    ///
    /// <code>
    /// Runner 없음 (ShipCoopTest.unity)  =&gt; 참    — 예전 그대로 혼자 다 계산한다
    /// Dedicated Server                  =&gt; 참
    /// Client                            =&gt; 거짓
    /// </code>
    /// </summary>
    public bool IsAuthority => ShipCoopNet.IsAuthorityHere;

    /// <summary>출항 후 지난 시간 (초)</summary>
    public float Elapsed => _elapsed;

    /// <summary>남은 시간 (초)</summary>
    public float RemainingTime => Mathf.Max(0f, timeLimit - _elapsed);

    /// <summary>제한시간 (초)</summary>
    public float TimeLimit => timeLimit;

    /// <summary>지금 진행도. 0 ~ 1</summary>
    public float Progress01 => voyage != null ? voyage.Progress01 : 0f;

    /// <summary>
    /// 제한시간에 맞추려면 지금 있어야 할 위치. 0 ~ 1.
    ///
    /// **HUD 진행도 바의 점선이 이 값이다.**
    /// 남은 시간을 숫자로만 띄우면 늦고 있다는 것을 아무도 알아채지 못한다.
    /// 이 기준선이 있어야 "돛 비었어!" 가 나온다.
    /// </summary>
    public float ExpectedProgress01 => timeLimit <= 0f ? 1f : Mathf.Clamp01(_elapsed / timeLimit);

    /// <summary>지금 늦고 있는지. true 면 HUD 에 경고를 띄운다.</summary>
    public bool IsBehindSchedule => Progress01 < ExpectedProgress01;

    /// <summary>지금 페이즈. 아직 시작 전이면 null.</summary>
    public VoyagePhase CurrentPhase =>
        _phaseIndex >= 0 && _phaseIndex < phases.Count ? phases[_phaseIndex] : null;

    /// <summary>지금 페이즈 번호. 아직 시작 전이면 -1.</summary>
    public int CurrentPhaseIndex => _phaseIndex;

    /// <summary>페이즈가 바뀌었다. (페이즈, 번호)</summary>
    public event Action<VoyagePhase, int> PhaseChanged;

    /// <summary>게임이 끝났다. (성공 여부, 점수) — 결과 화면이 이걸 듣는다.</summary>
    public event Action<bool, int> Finished;

    private void Start()
    {
        if (autoStart)
        {
            StartVoyage();
        }
    }

    private void Update()
    {
        if (State != ShipCoopState.Sailing)
        {
            return;
        }

        // 계산하는 쪽만 시간을 센다. 나머지는 호스트가 보내주는 값을 받는다. (11장)
        if (!IsAuthority)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        _elapsed += deltaTime;
        voyage.Tick(deltaTime);

        // 침수도 항해 중일 때만 찬다. 끝난 뒤에 물이 계속 차면 안 된다.
        if (flooding != null)
        {
            flooding.Tick(deltaTime);
        }

        UpdatePhase();
        CheckEnd();
    }

    /// <summary>출항한다.</summary>
    public void StartVoyage()
    {
        if (State == ShipCoopState.Sailing)
        {
            return;
        }

        if (health == null || voyage == null)
        {
            Debug.LogError("[ShipCoopGame] ShipHealth 와 ShipVoyage 를 Inspector 에 연결하세요.", this);
            return;
        }

        _elapsed = 0f;
        _phaseIndex = -1;
        _enemiesDestroyed = 0;
        _leaksSealed = 0;
        _obstaclesAvoided = 0;

        voyage.ResetVoyage();

        if (flooding == null)
        {
            flooding = FindAnyObjectByType<ShipFlooding>(FindObjectsInactive.Include);
        }

        if (flooding != null)
        {
            flooding.ResetFlooding();
        }

        State = ShipCoopState.Sailing;

        Debug.Log($"[ShipCoopGame] 출항. 제한시간 {timeLimit:0}초", this);
        UpdatePhase();
    }

    private void UpdatePhase()
    {
        // 진행도가 넘어선 페이즈 중 가장 마지막 것이 지금 페이즈다.
        int next = _phaseIndex;
        float progress = Progress01;

        for (int i = 0; i < phases.Count; i++)
        {
            if (progress >= phases[i].startProgress01)
            {
                next = i;
            }
        }

        if (next == _phaseIndex)
        {
            return;
        }

        _phaseIndex = next;
        Debug.Log($"[ShipCoopGame] {CurrentPhase?.name} 시작 (진행도 {progress:P0})", this);
        PhaseChanged?.Invoke(CurrentPhase, _phaseIndex);
    }

    private void CheckEnd()
    {
        // 순서가 중요하다. 도착과 침몰이 같은 프레임에 겹치면 침몰이 이긴다.
        if (health.IsSunk)
        {
            Finish(ShipCoopState.Sunk);
            return;
        }

        if (voyage.HasArrived)
        {
            Finish(ShipCoopState.Cleared);
            return;
        }

        if (_elapsed >= timeLimit)
        {
            Finish(ShipCoopState.TimeOver);
        }
    }

    private void Finish(ShipCoopState result)
    {
        State = result;

        bool success = result == ShipCoopState.Cleared;
        int score = CalculateScore(success);

        string reason = result switch
        {
            ShipCoopState.Cleared => "목적지 도착",
            ShipCoopState.Sunk => "침몰",
            ShipCoopState.TimeOver => "시간 초과",
            _ => result.ToString(),
        };

        Debug.Log($"[ShipCoopGame] 종료 — {reason} / 성공: {success} / 점수: {score}", this);

        Finished?.Invoke(success, score);
        Report(success, score);
    }

    private int CalculateScore(bool success)
    {
        float total =
            health.CurrentHp * hpScore
            + _enemiesDestroyed * enemyScore
            + _leaksSealed * leakScore
            + _obstaclesAvoided * obstacleScore;

        // 시간 보너스는 성공했을 때만 준다. 실패한 팀에게 남은 시간은 의미가 없다.
        if (success)
        {
            total += RemainingTime * timeBonusPerSecond;
        }

        return Mathf.Max(0, Mathf.RoundToInt(total));
    }

    private void Report(bool success, int score)
    {
        // 이 한 줄이 로비와 이어지는 유일한 지점이다. (GAME_STRUCTURE.md 4장)
        if (NetworkServiceLocator.IsReady)
        {
            NetworkServiceLocator.Current.ReportMiniGameResult(success, score);
            return;
        }

        Debug.LogWarning(
            "[ShipCoopGame] 네트워크 서비스가 없어 결과를 보고하지 못했습니다. " +
            "Develop 씬에서 혼자 테스트하는 중이라면 정상입니다.", this);
    }

    // ------------------------------------------------------------
    // 점수 집계 — 사건 쪽에서 불러준다
    // ------------------------------------------------------------

    /// <summary>적선을 격파했다.</summary>
    public void ReportEnemyDestroyed()
    {
        _enemiesDestroyed++;
    }

    /// <summary>침수를 막았다.</summary>
    public void ReportLeakSealed()
    {
        _leaksSealed++;
    }

    /// <summary>암초나 파도를 피했다.</summary>
    public void ReportObstacleAvoided()
    {
        _obstaclesAvoided++;
    }
}
