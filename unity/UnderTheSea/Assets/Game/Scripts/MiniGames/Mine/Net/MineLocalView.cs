using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// 이 컴퓨터의 **카메라 주인**을 정하고, 내 턴과 관전을 오간다.
    ///
    /// <code>
    ///   내 턴     내 캐릭터를 따라간다 · 마우스로 시점을 돌린다 · 그 각도가 서버로 간다
    ///   관전      지금 턴인 사람을 따라간다 · 마우스를 받지 않는다 ·
    ///             그 사람의 복제된 yaw/pitch 를 그대로 쓴다
    /// </code>
    ///
    /// <b>왜 자리만 따라가면 안 되는가.</b> 같은 사람을 따라다녀도 각자 다른 쪽을 보면
    /// 화면에 잡히는 것이 다르다. 광산은 <b>랜턴 반경만 밝은</b> 게임이라 (MINE.md 6장)
    /// 보는 방향이 곧 보이는 범위다. 각도를 안 맞추면 관전자는 남이 무엇을 파는지
    /// 영영 못 본다.
    ///
    /// ⚠ 이 부품은 **입력 권한이 있는 사람 하나만** 카메라를 만진다.
    ///    남의 캐릭터 복사본까지 카메라를 잡으려 들면 매 프레임 서로 뺏는다.
    ///    그래서 이 컴퓨터에서 실제로 도는 것은 언제나 정확히 하나다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MineNetPlayer))]
    public sealed class MineLocalView : NetworkBehaviour
    {
        private MineNetPlayer _who;
        private MineCamera _camera;
        private MineVision _vision;
        private MineGridView _board;
        private MineCursor _cursor;
        private MineHud _hud;

        [Tooltip("결과 화면에서 몇 초마다 정답과 내 그림을 바꿀 것인가. 솔로(MineAnswerView 의 Swap Seconds)와 같게 둔다.")]
        [SerializeField, Min(0.3f)] private float resultSwapSeconds = 1.5f;

        [Tooltip("힌트 볼 때 몇 초마다 바꿀 것인가. 힌트는 짧으므로 더 빠르게 넘긴다.")]
        [SerializeField, Min(0.1f)] private float hintSwapSeconds = 0.5f;

        private int _shownSlot = int.MinValue;
        private bool? _shownTarget;

        /// <summary>지금 정답을 보여주고 있는가. 같은 겹침을 매 프레임 다시 칠하지 않으려고 기억한다.</summary>
        private bool? _shownAnswer;

        /// <summary>
        /// 교대를 세기 시작한 시각. 결과 화면이나 힌트가 열린 순간이다.
        ///
        /// 이것이 없으면 <c>SimulationTime</c> 의 절대값으로 홀짝을 따지게 되어
        /// <b>화면이 정답부터 열릴 수 있다.</b> 내 그림을 먼저 봐야 한다.
        /// </summary>
        private double _toggleAnchor;

        public override void Spawned()
        {
            _who = GetComponent<MineNetPlayer>();

            if (!Object.HasInputAuthority) return;

            _camera = FindAnyObjectByType<MineCamera>();
            _vision = FindAnyObjectByType<MineVision>();
            _board = FindAnyObjectByType<MineGridView>();
            _cursor = FindAnyObjectByType<MineCursor>(FindObjectsInactive.Include);
            _hud = FindAnyObjectByType<MineHud>(FindObjectsInactive.Include);

            if (_camera == null)
            {
                Debug.LogWarning("[MineLocalView] MineCamera 를 찾지 못했습니다. 화면이 따라가지 않습니다.", this);
                return;
            }

            KeepOnlyThisViewer(_camera.GetComponent<Camera>());
        }

        /// <summary>
        /// 화면에 그려지는 카메라를 **하나로** 만든다.
        ///
        /// ⚠ <c>PeerMode.Multiple</c> 에서는 시작 씬(<c>MineBoot</c>)이 게임 씬과 함께
        ///    떠 있다. 시작 씬의 <c>BootCamera</c> 가 그대로 살아 있으면 광산 카메라와
        ///    <b>같은 depth 로 둘 다 그려져</b>, 화면에 보이는 시점이 어느 쪽인지
        ///    프레임마다 달라진다. "관전 시점이 안 맞는다" 로 보이는 것의 정체가 이것이다.
        ///    AudioListener 도 둘이 되어 경고가 난다. Warriors 에서 겪은 것과 같다.
        ///
        /// RenderTexture 로 그리는 카메라(반사 · 프리뷰)는 화면을 건드리지 않으므로 놔둔다.
        /// </summary>
        private void KeepOnlyThisViewer(Camera keep)
        {
            if (keep == null) return;

            foreach (Camera other in FindObjectsByType<Camera>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (other == keep || other.targetTexture != null) continue;

                other.enabled = false;

                AudioListener ear = other.GetComponent<AudioListener>();
                if (ear != null) ear.enabled = false;

                Debug.Log(
                    $"[MineLocalView] 화면에 겹쳐 그려지던 카메라 '{other.name}'" +
                    $"(씬 '{other.gameObject.scene.name}')를 껐습니다.");
            }

            keep.enabled = true;

            AudioListener keepEar = keep.GetComponent<AudioListener>();
            if (keepEar != null) keepEar.enabled = true;

            if (!keep.CompareTag("MainCamera")) keep.tag = "MainCamera";

            Debug.Log(
                $"[MineLocalView] 내 카메라 '{keep.name}'(씬 '{keep.gameObject.scene.name}') 확정. " +
                $"Camera.main = '{(Camera.main != null ? Camera.main.name : "없음")}'");
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            // 나갈 때 카메라를 잠근 채로 두면 다음 사람이 못 돌린다.
            if (_camera != null) _camera.AcceptsMouse = true;
        }

        /// <summary>
        /// HUD 힌트 칸에 적을 글자. <c>MineHud.RefreshHint</c> 의 규칙 그대로다.
        ///
        /// ⚠ <b>쓸 수 없는 때</b>와 <b>써버린 때</b>를 가른다. 내 턴이 아니면 "대기" 다.
        ///   그걸 "사용함" 으로 적으면 공개 7초에 쓰지도 않은 힌트가 이미 쓴 것처럼 보인다.
        ///
        /// 키는 <b>J</b> 다. 솔로도 같다 — 솔로는 <c>KeyboardPlayerController</c> 의
        /// button2 가 J 로 묶여 있고, 네트워크는 <c>MineInputProvider</c> 의 jKey 다.
        /// </summary>
        private string HintCell(MineMatchState match)
        {
            if (match == null || _who == null || _who.Slot < 0) return string.Empty;

            if (IsWatchingMyHint(match)) return "보는 중";
            if (!_who.IsMyTurn) return "대기";

            return _who.HintUsed ? "사용함" : "J · 1회";
        }

        /// <summary>힌트가 아직 살아 있는가. 보는 중이거나 내 턴에 아직 안 썼으면 살아 있다.</summary>
        private bool HintAlive(MineMatchState match)
        {
            if (match == null || _who == null || _who.Slot < 0) return false;

            return IsWatchingMyHint(match) || (_who.IsMyTurn && !_who.HintUsed);
        }

        private bool IsWatchingMyHint(MineMatchState match)
        {
            return match.HintLeft > 0f && match.HintSlot == _who.Slot;
        }

        /// <summary>
        /// 내가 판 그림과 정답을 번갈아 보여준다. 솔로의 <c>MineAnswerView</c> 와 같은 일이다.
        ///
        /// ⚠ <b>각자 타이머를 돌리지 않고 복제되는 시간에서 뽑는다.</b> 로컬 타이머는
        ///   들어온 시각이 사람마다 달라서 화면끼리 박자가 엇갈린다. 옆 사람 화면을
        ///   같이 보며 "저기 봐" 하는 게임인데 서로 다른 것을 보고 있으면 안 된다.
        ///   <c>SimulationTime</c> 은 모두가 같은 값을 본다.
        ///
        /// 결과 화면에서 캐릭터가 떠밀릴 걱정은 없다. 정답 보기가 파인 칸을 끌어올리지만
        /// 그때는 <c>MineNetPlayer.CanMoveNow</c> 가 false 여서 <c>MineNetPlayerMover</c>
        /// 가 이미 <c>CharacterMover</c> 를 꺼 둔다. 힌트 중에는 자기 턴이라 켜져 있으므로
        /// <c>CanMoveNow</c> 에서 따로 막는다.
        /// </summary>
        private void TickAnswerToggle(MineMatchState match, float period, bool startOnAnswer)
        {
            if (_board == null || period <= 0f) return;

            // (long) 으로 받는다. 판이 길어져도 int 로는 넘칠 수 있다.
            long step = (long)((match.Runner.SimulationTime - _toggleAnchor) / period);
            bool answer = ((step & 1) == 1) != startOnAnswer;

            if (_shownAnswer == answer) return;

            _shownAnswer = answer;
            _board.SetOverlay(answer ? MineOverlay.Answer : MineOverlay.Result);
        }

        /// <summary>
        /// 화면은 매 프레임 정한다. 틱보다 촘촘해야 시점이 끊기지 않는다.
        /// </summary>
        public override void Render()
        {
            if (!Object.HasInputAuthority) return;

            MineMatchState match = MineMatchState.Current;

            // 내가 몇 번인지는 판이 어떤 상태든 늘 표시해 둔다. HUD 가 그 줄의 테두리를
            // 다르게 칠한다. 창이 뜬 순서로는 알 수 없다 — 창은 프로세스가 시작한 순서,
            // 슬롯은 서버에 붙은 순서다. 접속이 늦으면 먼저 띄운 창이 P2 가 된다.
            //
            // 힌트 칸도 여기서 넣는다. 판 전체를 보는 MineMatchState 는 이 화면의
            // 주인이 누구인지 모르는데, 힌트는 남의 것이 아니라 **내 것**을 적어야 한다.
            if (_hud != null)
            {
                _hud.NetworkSelfSlot = _who != null ? _who.Slot : -1;

                _hud.NetworkHintText = HintCell(match);
                _hud.NetworkHintLit = HintAlive(match);
            }

            if (match == null) return;

            // 시작 카운트다운 동안에는 돌 종류를 감춘다. 아직 아무도 못 파는 시간인데
            // 단단한 돌이 어두운 얼룩으로 먼저 드러나면 판이 지저분해 보이고
            // 어디가 단단한지도 미리 알려준다. 솔로(MineGame.EnterCountdown)와 같다.
            //
            // ⚠ 카메라보다 먼저 본다. 카메라를 못 찾은 화면에서도 판은 그려진다.
            if (_board != null) _board.SetUniformStone(match.Phase == MineMatchPhase.Countdown);

            if (_camera == null) return;

            // 판이 끝났다. 완성된 그림을 위에서 보여 준다. (MINE.md 3장 7번)
            // 늦게 들어온 사람도 Phase 가 복제되므로 같은 화면을 받는다.
            if (match.Phase == MineMatchPhase.Finished)
            {
                _camera.AcceptsMouse = false;

                if (_shownTarget != null)
                {
                    _shownTarget = null;
                    _shownSlot = int.MinValue;
                    _shownAnswer = null;
                    _toggleAnchor = match.Runner.SimulationTime;

                    if (_cursor != null) _cursor.ShowCell(-1, -1);

                    MineGridSync board = MineGridSync.Current;
                    if (board != null) board.ShowResult(new Vector2Int(match.ResultAlignX, match.ResultAlignY));

                    if (_vision != null) { _vision.SetLit(true); _vision.Follow(null); }
                    _camera.ShowBoard(finale: true);
                }

                // 내가 판 그림과 정답을 번갈아 보여준다. 솔로의 MineAnswerView 와 같다.
                TickAnswerToggle(match, resultSwapSeconds, startOnAnswer: false);
                return;
            }

            // ⚠ 목표를 보여 주는 두 경우를 <b>갈라서</b> 다룬다.
            //
            //   공개(7초)  판이 시작하기 전이니 모두가 같이 본다.
            //   힌트      그 사람이 자기 한 번을 쓴 것이다. <b>쓴 사람만 본다.</b>
            //
            //   예전에는 둘 다 <c>ShowingTarget</c> 하나로 묶어 모두에게 탑뷰를 보였다.
            //   그러면 관전자가 남의 힌트를 공짜로 같이 본다.
            bool showTarget = match.Phase == MineMatchPhase.Reveal
                             || (match.HintLeft > 0f && _who != null && _who.Slot == match.HintSlot);

            // 남이 힌트를 보는 동안에는 보던 시점 그대로 두고 글자만 알려 준다.
            if (_hud != null)
            {
                _hud.NetworkCenterNotice = (match.HintLeft > 0f && !showTarget) ? "힌트타임" : string.Empty;
            }

            // ⚠ **목표를 보는 사람만** 탑뷰로 바뀐다. 공개 7초는 모두, 힌트는 쓴 사람만이다.
            //    탑뷰 + 밝히기 + 도안 켜기가 함께 움직여야 한다. 셋 중 하나라도 빠지면
            //    반쪽이 된다. (MINE.md 6장)
            if (showTarget)
            {
                _camera.AcceptsMouse = false;

                if (_shownTarget != true)
                {
                    _shownTarget = true;
                    if (_cursor != null) _cursor.ShowCell(-1, -1);
                    _shownSlot = int.MinValue;   // 끝나면 다시 붙이도록 기억을 지운다

                    // ⚠ Drawing 이 아니라 Answer 다. 이 분기는 공개와 힌트를 같이
                    //   다루는데, Drawing 은 이미 판 칸을 회색으로 빼므로 힌트에서
                    //   정답 위에 내가 판 자리가 겹쳐 보인다. 공개 때는 아직 파인 칸이
                    //   없어 둘이 똑같이 그려지므로, Answer 로 두면 힌트만 달라진다.
                    if (_board != null) { _board.SetTargetOffset(Vector2Int.zero); _board.SetOverlay(MineOverlay.Answer); }
                    if (_vision != null) { _vision.SetLit(true); _vision.Follow(null); }
                    _camera.ShowBoard();

                    _shownAnswer = true;   // 위에서 Answer 로 켰다
                    _toggleAnchor = match.Runner.SimulationTime;
                }

                // ⚠ 힌트만 번갈아 보여준다. 공개 7초는 **외우는 시간**이라 그대로 둔다 —
                //   거기서 내 그림으로 넘어가면 외울 대상이 사라진다. 어차피 공개 때는
                //   파인 칸이 없어 내 그림이 빈 판이다.
                if (match.HintLeft > 0f) TickAnswerToggle(match, hintSwapSeconds, startOnAnswer: true);

                return;
            }

            if (_shownTarget != false)
            {
                _shownTarget = false;

                // 여기부터는 기억으로 그린다. 도안을 감추고 다시 어두워진다.
                if (_board != null) _board.SetOverlay(MineOverlay.None);
            }

            bool mine = _who != null && _who.IsMyTurn;

            // 마우스는 내 턴에만 받는다. 관전 중에는 시점이 복제로 들어온다.
            _camera.AcceptsMouse = mine;

            MineNetPlayer subject = mine ? _who : match.FindBySlot(match.CurrentSlot);

            if (subject == null)
            {
                // 아직 아무 턴도 아니다(대기 · 카운트다운 · 종료). 판 전체를 보여 준다.
                if (_shownSlot != -999)
                {
                    _shownSlot = -999;
                    _camera.ShowBoard();
                    if (_vision != null) { _vision.SetLit(true); _vision.Follow(null); }
                }

                return;
            }

            // 보는 대상이 바뀌었을 때만 카메라를 다시 붙인다.
            // 매 프레임 부르면 FollowPlayer 가 전환 연출을 계속 처음부터 재생한다.
            if (_shownSlot != subject.Slot)
            {
                _shownSlot = subject.Slot;

                _camera.FollowPlayer(subject.transform);

                // 랜턴은 지금 턴인 사람을 따라간다. (MINE.md 6장 — 관전은 시야 제한)
                if (_vision != null)
                {
                    _vision.SetLit(false);
                    _vision.Follow(subject.transform);
                }
            }

            // ⚠ 각도는 **Follow 를 바꾼 뒤 같은 프레임에** 넣는다. 순서가 뒤집히면
            //    전환된 프레임 한 번은 앞사람의 각도로 새 대상을 보게 된다.
            //    MineCamera.LateUpdate 가 이 값을 읽어 실제 화면을 만든다.
            if (!mine) _camera.ApplyOrbit(subject.CameraYaw, subject.CameraPitch);

            // ⚠ 조준 표시는 **서버가 고른 칸**을 그대로 가리킨다. 각자 자기 화면의
            //    캐릭터 자리로 계산하면 보간 때문에 칸 경계에서 한 칸씩 어긋나,
            //    표시된 칸과 실제로 파이는 칸이 달라진다. 관전자도 같은 칸을 본다.
            if (_cursor != null)
            {
                int cell = subject.FocusCell;
                MineGrid grid = MineGridSync.Current != null ? MineGridSync.Current.Grid : null;

                if (cell < 0 || grid == null) _cursor.ShowCell(-1, -1);
                else _cursor.ShowCell(cell % grid.Size, cell / grid.Size);
            }
        }
    }
}
