using Fusion;
using UnityEngine;

namespace Mine.Net
{
    /// <summary>
    /// **격자 한 장을 서버가 계산하고 모두가 같은 것을 본다.**
    ///
    /// <code>
    ///   서버    시드로 돌을 깔고 · 파고 · 되메운다. 계산은 여기서 한 번만 일어난다
    ///   모두    시드로 같은 돌을 깔고, 그 뒤로는 복제된 칸 상태를 받아 그린다
    /// </code>
    ///
    /// <b>왜 칸마다 2비트인가.</b> 20×20 = 400칸이고 칸 하나가 가질 수 있는 값은
    /// 0(파임) · 1(한 번 남음) · 2(두 번 남음) 셋뿐이다. 칸당 <c>int</c> 를 쓰면
    /// 1600바이트가 되는데, 2비트로 담으면 <b>25개</b> 로 끝난다.
    ///
    /// <b>왜 시드를 나눠 갖는가.</b> 돌 배치는 <c>System.Random(seed)</c> 로 만든다.
    /// 같은 시드면 어느 컴퓨터에서나 같은 배치가 나오므로, 처음 한 장을 통째로
    /// 보낼 필요가 없다. 그 뒤의 변화만 위의 2비트로 따라간다.
    ///
    /// ⚠ <b>클라이언트는 <c>MineGrid.Hit</c> 을 부르지 않는다.</b> 각자 계산하면
    ///    네 명이 서로 다른 그림을 그리게 된다. 받은 값을 <c>ShowCell</c> 로
    ///    적기만 한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MineGridSync : NetworkBehaviour
    {
        /// <summary>한 <c>int</c> 에 담는 칸 수. 2비트씩 16칸.</summary>
        private const int CellsPerWord = 16;

        /// <summary>20×20 격자를 담는 데 필요한 <c>int</c> 개수.</summary>
        public const int WordCount = 25;

        [Header("채점 (MINE.md 7장 — MineGame 과 같은 값)")]
        [Tooltip("몇 칸까지 어긋남을 봐줄 것인가. 0칸이면 만점, 이 값 이상 떨어지면 0점.")]
        [SerializeField, Min(0.5f)] private float shapeTolerance = 2f;

        [Tooltip("위치를 맞출 때 최대 몇 칸까지 밀 수 있는가. 크게 잡을수록 " +
                 "'어디에 그렸는가' 를 안 보게 된다.")]
        [SerializeField, Min(0)] private int maxAlign = 0;

        [Header("도안")]
        [Tooltip("서버가 여기서 하나를 골라 모두에게 알린다. 생성 도구가 채운다.")]
        [SerializeField] private MineDrawingTarget[] drawings = new MineDrawingTarget[0];

        /// <summary>칸마다 남은 타격 수를 2비트씩 담은 것.</summary>
        [Networked, Capacity(WordCount)]
        public NetworkArray<int> Cells { get; }

        /// <summary>이번 판의 도안 번호. -1 이면 아직 안 정해졌다.</summary>
        [Networked] public int DrawingIndex { get; private set; }

        /// <summary>서버가 격자를 깔았는가. 클라이언트가 이 값을 보고 자기 격자를 맞춘다.</summary>
        [Networked] public int BoardStamp { get; private set; }

        public static MineGridSync Current { get; private set; }

        /// <summary>이 판의 격자. 조준 칸을 계산할 때도 같은 것을 쓴다.</summary>
        public MineGrid Grid => _grid;

        private MineGrid _grid;
        private MineGridView _view;
        private int _shownStamp;

        public override void Spawned()
        {
            Current = this;
            _grid = FindFirstObjectByType<MineGrid>(FindObjectsInactive.Include);
            _view = FindFirstObjectByType<MineGridView>(FindObjectsInactive.Include);

            if (_grid == null)
            {
                Debug.LogError("[MineGridSync] MineGrid 를 찾지 못했습니다. 격자를 맞출 수 없습니다.", this);
                return;
            }

            if (!HasStateAuthority) return;

            DrawingIndex = -1;
        }

        public override void Despawned(NetworkRunner runner, bool hasState)
        {
            if (Current == this) Current = null;
        }

        // ------------------------------------------------------------
        // 서버가 정하는 것
        // ------------------------------------------------------------

        /// <summary>
        /// **판을 깐다.** 매치가 시작될 때 서버가 한 번 부른다.
        ///
        /// 돌 배치는 시드로 만들고, 도안은 목록에서 시드로 고른다. 둘 다 복제되므로
        /// 클라이언트는 같은 판을 스스로 만들어 낼 수 있다.
        /// </summary>
        public void ServerOpenBoard(int seed)
        {
            if (!HasStateAuthority || _grid == null) return;

            DrawingIndex = drawings.Length > 0 ? Mathf.Abs(seed) % drawings.Length : -1;

            if (DrawingIndex >= 0) _grid.SetTarget(drawings[DrawingIndex]);

            _grid.ResetAll(seed);

            PublishAll();
            BoardStamp++;

            Debug.Log($"[MineGridSync] 판을 깔았습니다 — 시드 {seed}, 도안 " +
                      $"{(DrawingIndex >= 0 ? drawings[DrawingIndex].displayName : "없음")}");
        }

        /// <summary>
        /// **발밑을 한 번 친다.** 서버만 부른다.
        ///
        /// 무른 돌은 바로 깨지고 단단한 돌은 처음에 금만 간다 — 그 규칙은
        /// <c>MineGrid.Hit</c> 안에 그대로 있다. 여기서는 부르고 결과를 복제할 뿐이다.
        /// </summary>
        public MineHitResult ServerDig(Vector3 worldPosition)
        {
            if (!HasStateAuthority || _grid == null) return MineHitResult.None;
            if (!_grid.WorldToCell(worldPosition, out int x, out int y)) return MineHitResult.None;

            MineHitResult result = _grid.Hit(x, y);

            if (result != MineHitResult.None) PublishCell(x, y);

            return result;
        }

        /// <summary>
        /// **발밑을 되메운다.** 안 파인 칸이면 거짓을 돌려준다.
        ///
        /// 잘못 눌러 귀한 복구 블록이 날아가지 않도록, 블록을 깎을지는 부른 쪽이
        /// 이 반환값을 보고 정한다. <c>MineGame.TryRestore</c> 와 같은 규칙이다.
        /// </summary>
        public bool ServerRestore(Vector3 worldPosition)
        {
            if (!HasStateAuthority || _grid == null) return false;
            if (!_grid.WorldToCell(worldPosition, out int x, out int y)) return false;

            if (!_grid.Restore(x, y)) return false;

            PublishCell(x, y);
            return true;
        }

        /// <summary>
        /// **판 하나를 채점한다. 서버가 딱 한 번 부른다.**
        ///
        /// 규칙은 <c>MineGame.EnterFinished</c> 와 같다 — 완성된 격자와 목표 도안을
        /// <c>MineShapeSimilarity</c> 로 비교하고, 그 퍼센트가 곧 점수다.
        ///
        /// ⚠ <b>인원과 무관한 규칙이다.</b> 공유 격자 한 장을 도안과 비교할 뿐이라
        ///    1인 기준 가정이 들어 있지 않다. 그래서 그대로 가져왔다.
        /// </summary>
        public MineSimilarityResult ServerScore()
        {
            if (_grid == null || _grid.TargetCells == null) return default;

            IMineSimilarity judge = new MineShapeSimilarity(shapeTolerance, maxAlign);

            return judge.Evaluate(_grid.Cells, _grid.TargetCells, _grid.Size);
        }

        /// <summary>결과 화면으로 바꾼다. 맞은 칸과 틀린 칸을 색으로 가른다. (MINE.md 7장)</summary>
        public void ShowResult(Vector2Int alignment)
        {
            if (_view == null) return;

            _view.SetTargetOffset(alignment);
            _view.SetOverlay(MineOverlay.Result);
        }

        private void PublishAll()
        {
            for (int i = 0; i < WordCount; i++) Cells.Set(i, 0);

            int size = _grid.Size;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    PublishCell(x, y);
        }

        private void PublishCell(int x, int y)
        {
            int cell = y * _grid.Size + x;
            int word = cell / CellsPerWord;
            int shift = (cell % CellsPerWord) * 2;

            if (word < 0 || word >= WordCount) return;

            int packed = Cells.Get(word);
            packed &= ~(0b11 << shift);
            packed |= (_grid.RemainingAt(x, y) & 0b11) << shift;

            Cells.Set(word, packed);
        }

        private int ReadCell(int x, int y)
        {
            int cell = y * _grid.Size + x;
            int word = cell / CellsPerWord;
            int shift = (cell % CellsPerWord) * 2;

            if (word < 0 || word >= WordCount) return 0;

            return (Cells.Get(word) >> shift) & 0b11;
        }

        // ------------------------------------------------------------
        // 표시 — 클라이언트는 받은 값을 그리기만 한다
        // ------------------------------------------------------------

        public override void Render()
        {
            if (HasStateAuthority || _grid == null || BoardStamp == 0) return;

            // 판이 새로 깔렸다. 같은 시드로 돌을 깔고 같은 도안을 건다.
            // 여기까지는 계산이 아니라 **재현**이다 — 서버와 같은 입력으로 같은 결과를 만든다.
            if (_shownStamp != BoardStamp)
            {
                _shownStamp = BoardStamp;

                if (DrawingIndex >= 0 && DrawingIndex < drawings.Length)
                {
                    _grid.SetTarget(drawings[DrawingIndex]);
                }

                MineMatchState match = MineMatchState.Current;
                if (match != null) _grid.ResetAll(match.BoardSeed);
            }

            int size = _grid.Size;

            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                    _grid.ShowCell(x, y, ReadCell(x, y));
        }

#if UNITY_EDITOR
        /// <summary>생성 도구가 도안 목록을 넣는다. **에디터 전용이다.**</summary>
        public void EditorSetDrawings(MineDrawingTarget[] list) => drawings = list;
#endif
    }
}
