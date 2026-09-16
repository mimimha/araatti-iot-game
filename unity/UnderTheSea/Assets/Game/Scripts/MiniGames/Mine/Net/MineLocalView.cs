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

        private int _shownSlot = int.MinValue;
        private bool? _shownTarget;

        public override void Spawned()
        {
            _who = GetComponent<MineNetPlayer>();

            if (!Object.HasInputAuthority) return;

            _camera = FindAnyObjectByType<MineCamera>();
            _vision = FindAnyObjectByType<MineVision>();
            _board = FindAnyObjectByType<MineGridView>();
            _cursor = FindAnyObjectByType<MineCursor>(FindObjectsInactive.Include);

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
        /// 화면은 매 프레임 정한다. 틱보다 촘촘해야 시점이 끊기지 않는다.
        /// </summary>
        public override void Render()
        {
            if (!Object.HasInputAuthority || _camera == null) return;

            MineMatchState match = MineMatchState.Current;
            if (match == null) return;

            // 판이 끝났다. 완성된 그림을 위에서 보여 준다. (MINE.md 3장 7번)
            // 늦게 들어온 사람도 Phase 가 복제되므로 같은 화면을 받는다.
            if (match.Phase == MineMatchPhase.Finished)
            {
                _camera.AcceptsMouse = false;

                if (_shownTarget != null)
                {
                    _shownTarget = null;
                    _shownSlot = int.MinValue;

                    if (_cursor != null) _cursor.ShowCell(-1, -1);

                    MineGridSync board = MineGridSync.Current;
                    if (board != null) board.ShowResult(new Vector2Int(match.ResultAlignX, match.ResultAlignY));

                    if (_vision != null) { _vision.SetLit(true); _vision.Follow(null); }
                    _camera.ShowBoard(finale: true);
                }

                return;
            }

            bool showTarget = match.ShowingTarget;

            // ⚠ **목표를 보는 동안에는 모두가 같은 화면을 본다.** 공개 7초도, 힌트도 같다.
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

                    if (_board != null) { _board.SetTargetOffset(Vector2Int.zero); _board.SetOverlay(MineOverlay.Drawing); }
                    if (_vision != null) { _vision.SetLit(true); _vision.Follow(null); }
                    _camera.ShowBoard();
                }

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
