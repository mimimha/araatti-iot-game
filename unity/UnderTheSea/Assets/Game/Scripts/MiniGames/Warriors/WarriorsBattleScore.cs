using System;
using UnityEngine;

namespace Warriors
{
    [ExecuteAlways]
    public sealed class WarriorsBattleScore : MonoBehaviour
    {
        [SerializeField, Min(1)] private int targetKills = 30;
        [SerializeField, Min(10f)] private float timeLimitSeconds = 180f;
        private float remainingSeconds;
        private float totalElapsedSeconds;
        private BattleState state;
        private bool clockRunning;
        private float scoreMultiplier = 1f;
        [SerializeField] private WarriorsGameFlow gameFlow;
        [SerializeField] private WarriorsHealth playerHealth;
        [SerializeField] private WarriorsComboSystem comboSystem;
        [SerializeField] private WarriorsRhythmBattle rhythmBattle;
        [SerializeField] private bool drawLegacyHud = true;
        private static Texture2D panelTexture;
        private static Texture2D shadowTexture;
        private static Texture2D insetTexture;
        public int Kills { get; private set; }
        public int Score { get; private set; }
        public int TargetKills => targetKills;
        public float RemainingSeconds => remainingSeconds;
        public float ElapsedSeconds => Mathf.Max(0f, timeLimitSeconds - remainingSeconds);

        /// <summary>
        /// Every round restarts the clock with its own limit, so ElapsedSeconds only ever
        /// measures the round in progress - it reported 00:00 on the result card for a run
        /// that had just ended. This keeps running across all three rounds.
        /// </summary>
        public float TotalElapsedSeconds => totalElapsedSeconds;
        public float Progress => Mathf.Clamp01((float)Kills / targetKills);
        public bool IsRunning => state == BattleState.Playing;
        public event Action<bool, int> BattleFinished;
        private enum BattleState { Playing, Cleared, Failed }

        private void OnEnable()
        {
            Kills = 0; Score = 0; scoreMultiplier = 1f; remainingSeconds = timeLimitSeconds; totalElapsedSeconds = 0f; state = BattleState.Playing; clockRunning = true;
            if (rhythmBattle == null) rhythmBattle = UnityEngine.Object.FindFirstObjectByType<WarriorsRhythmBattle>(FindObjectsInactive.Include);
        }
        private void Update() { if (!Application.isPlaying || !clockRunning) return;
            // ⚠ 네트워크 Warriors 는 **시간 제한으로 지지 않는다.** 승패는 목표 수치로만 갈린다.
            //    싱글 씬(Runner 없음)에서는 이 줄이 거짓이라 예전 그대로 시계가 돈다.
            if (Warriors.Net.WarriorsNet.IsNetworked) return; totalElapsedSeconds += Time.deltaTime; remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime); if (remainingSeconds <= 0f) { clockRunning = false; state = BattleState.Failed; BattleFinished?.Invoke(false, Score); } }
        public void RegisterKill(int points) { if (!IsRunning) return; Kills++; Score += Mathf.RoundToInt(Mathf.Max(0, points) * scoreMultiplier); if (Kills >= targetKills) Finish(true); }
        public void RegisterBossHit(int points) { Score += Mathf.Max(0, points); }
        public void SetScoreMultiplier(float value) => scoreMultiplier = Mathf.Max(1f, value);

        /// <summary>
        /// Two players clear the beach about twice as fast, so a fixed goal would just end
        /// ROUND 1 in half the time. Scaling the goal keeps the round the same length and
        /// gives the pair more to cut, which is the part that is supposed to be fun.
        /// </summary>
        public void ConfigureTargetKills(int kills) => targetKills = Mathf.Max(1, kills);
        private void Finish(bool success) { if (!IsRunning) return; state = success ? BattleState.Cleared : BattleState.Failed; BattleFinished?.Invoke(success, Score); }
        public void ConfigureFlow(WarriorsGameFlow flow) => gameFlow = flow;
        public void StopClock() => clockRunning = false;

        /// <summary>
        /// Starts a fresh countdown for the round that is beginning.  Each round owns its
        /// own clock - ROUND 1's timer used to keep running through the kraken fight, so
        /// reaching ROUND 2 with time to spare still ended the run in TIME OVER.
        /// </summary>
        public void RestartClock(float seconds)
        {
            timeLimitSeconds = Mathf.Max(10f, seconds);
            remainingSeconds = timeLimitSeconds;
            state = BattleState.Playing;
            clockRunning = true;
        }
        public void SetLegacyHudVisible(bool visible) => drawLegacyHud = visible;

        private void OnGUI()
        {
            if (!drawLegacyHud) return;
            float scale = Mathf.Clamp(Screen.height / 900f, .85f, 1.6f);
            float margin = 22f * scale;
            float displayedSeconds = Application.isPlaying ? remainingSeconds : timeLimitSeconds;
            int minutes = Mathf.CeilToInt(displayedSeconds) / 60;
            int seconds = Mathf.CeilToInt(displayedSeconds) % 60;
            GUIStyle label = TextStyle(Mathf.RoundToInt(24f * scale));
            GUIStyle title = TextStyle(Mathf.RoundToInt(15f * scale));
            GUIStyle panel = new(GUI.skin.box); panel.normal.background = PanelTexture(); panel.border = new RectOffset(16, 16, 16, 16);

            if (gameFlow != null && gameFlow.Phase == WarriorsBattlePhase.FinalKrakenPhase && rhythmBattle != null && rhythmBattle.IsActive) return;
            if (gameFlow != null && (gameFlow.Phase == WarriorsBattlePhase.FinalSwingPhase || gameFlow.Phase == WarriorsBattlePhase.Clear || gameFlow.Phase == WarriorsBattlePhase.Failed))
            {
                DrawFinalOverlay(gameFlow.Phase, gameFlow.FinalSwingTitle, gameFlow.FinalSwingLabel, gameFlow.FailureLabel, gameFlow.SuccessfulSwingPlayerCount, gameFlow.ActivePlayerCount, Score, Kills, ElapsedSeconds, scale, panel);
                return;
            }

            bool tentaclePhase = gameFlow != null && gameFlow.Phase == WarriorsBattlePhase.KrakenTentaclePhase;
            if (playerHealth == null) playerHealth = UnityEngine.Object.FindFirstObjectByType<WarriorsLocalPlayerController>(FindObjectsInactive.Include)?.GetComponent<WarriorsHealth>();
            if (comboSystem == null) comboSystem = UnityEngine.Object.FindFirstObjectByType<WarriorsComboSystem>(FindObjectsInactive.Include);
            int hp = playerHealth != null ? playerHealth.CurrentHealth : 100;

            float leftWidth = 252f * scale;
            float topHeight = 132f * scale;
            DrawPanel(new Rect(margin, margin, leftWidth, topHeight), panel);
            GUI.Label(new Rect(margin + 18f * scale, margin + 8f * scale, leftWidth - 36f * scale, 25f * scale), tentaclePhase ? "ROUND 2  ·  TENTACLE" : "ROUND 1  ·  BEACH RAID", TextStyle(Mathf.RoundToInt(16f * scale), Gold()));
            GUI.Label(new Rect(margin + 18f * scale, margin + 34f * scale, 60f * scale, 42f * scale), "TIME", TextStyle(Mathf.RoundToInt(13f * scale), new Color(.55f, .82f, .9f)));
            GUI.Label(new Rect(margin + 72f * scale, margin + 31f * scale, leftWidth - 90f * scale, 48f * scale), $"{minutes:00}:{seconds:00}", TextStyle(Mathf.RoundToInt(30f * scale)));
            GUI.Label(new Rect(margin + 18f * scale, margin + 87f * scale, 57f * scale, 24f * scale), $"HP {hp}", title);
            DrawBar(new Rect(margin + 78f * scale, margin + 91f * scale, leftWidth - 98f * scale, 16f * scale), hp / 100f, new Color(.12f, .78f, .95f));

            float centerWidth = Mathf.Min(520f * scale, Screen.width * .40f);
            float centerX = Screen.width * .5f - centerWidth * .5f;
            float centerHeight = 108f * scale;
            DrawPanel(new Rect(centerX, margin, centerWidth, centerHeight), panel);
            string phaseTitle = tentaclePhase ? "TENTACLE PHASE" : "WAVE PROGRESS";
            string centerLabel = tentaclePhase ? $"촉수 전투   {gameFlow.TentacleSuccesses} / {gameFlow.TentacleSuccessesRequired}" : $"처치 수   {Kills} / {targetKills}";
            GUI.Label(new Rect(centerX + 18f * scale, margin + 7f * scale, centerWidth - 36f * scale, 24f * scale), phaseTitle, TextStyle(Mathf.RoundToInt(14f * scale), Gold()));
            GUI.Label(new Rect(centerX + 18f * scale, margin + 30f * scale, centerWidth - 36f * scale, 35f * scale), centerLabel, TextStyle(Mathf.RoundToInt(22f * scale)));
            float barX = centerX + 36f * scale;
            float barY = margin + 70f * scale;
            float barWidth = centerWidth - 72f * scale;
            float displayedProgress = tentaclePhase ? gameFlow.TentacleSuccesses / (float)Mathf.Max(1, gameFlow.TentacleSuccessesRequired) : Progress;
            DrawBar(new Rect(barX, barY, barWidth, 20f * scale), displayedProgress, new Color(1f, .67f, .12f));

            float scoreWidth = 236f * scale;
            float scoreX = Screen.width - margin - scoreWidth;
            DrawPanel(new Rect(scoreX, margin, scoreWidth, topHeight), panel);
            GUI.Label(new Rect(scoreX + 12f * scale, margin + 7f * scale, scoreWidth - 24f * scale, 24f * scale), "SCORE", TextStyle(Mathf.RoundToInt(14f * scale), new Color(.55f, .82f, .9f)));
            GUI.Label(new Rect(scoreX + 12f * scale, margin + 27f * scale, scoreWidth - 24f * scale, 48f * scale), $"{Score:N0}", TextStyle(Mathf.RoundToInt(31f * scale), Gold()));
            GUI.Label(new Rect(scoreX + 12f * scale, margin + 79f * scale, scoreWidth - 24f * scale, 32f * scale), $"COMBO   {(comboSystem != null ? comboSystem.Combo : 0)}", TextStyle(Mathf.RoundToInt(17f * scale)));

            int activePlayers = gameFlow?.ActivePlayerCount ?? 1;
            float slotY = margin + topHeight + 10f * scale;
            for (int i = 0; i < 4; i++)
                PlayerSlot(new Rect(scoreX, slotY + i * 42f * scale, scoreWidth, 36f * scale), i, i < activePlayers, hp, scale);

            float gap = 14f * scale;
            float cardWidth = 190f * scale;
            float cardHeight = 72f * scale;
            float cardsWidth = cardWidth * 3f + gap * 2f;
            float cardX = (Screen.width - cardsWidth) * .5f;
            float cardY = Screen.height - margin - cardHeight;
            CompactCard(new Rect(cardX, cardY, cardWidth, cardHeight), "1", "↔", "가로베기", new Color(.18f,.48f,.95f), title, panel);
            CompactCard(new Rect(cardX + cardWidth + gap, cardY, cardWidth, cardHeight), "2", "↕", "세로베기", new Color(.94f,.29f,.2f), title, panel);
            CompactCard(new Rect(cardX + (cardWidth + gap) * 2f, cardY, cardWidth, cardHeight), "3", "⊙", "찌르기", new Color(.92f,.62f,.12f), title, panel);
            if (gameFlow != null)
            {
                string message = gameFlow.Phase switch
                {
                    WarriorsBattlePhase.Clear => "GAME CLEAR",
                    WarriorsBattlePhase.Failed => gameFlow.FailureLabel,
                    _ => string.Empty
                };
                // TransitionLabel is rendered by DrawTransitionOverlay below.
                // Do not also render it through the generic center message.
                if (gameFlow.IsTransitioning) message = string.Empty;
                if (!string.IsNullOrEmpty(message)) GUI.Label(new Rect(0, Screen.height * .36f, Screen.width, 90), message, TextStyle(Mathf.RoundToInt(46f * scale)));
                float guideY = cardY - 38f * scale;
                if (tentaclePhase) GUI.Label(new Rect(0, guideY, Screen.width, 30f * scale), "4개의 촉수를 올바른 방향으로 베어 BREAK를 만드세요.", title);
                if (gameFlow.IsTransitioning) DrawTransitionOverlay(gameFlow.TransitionLabel, scale, panel);
            }
            if (comboSystem != null && !string.IsNullOrEmpty(comboSystem.ActiveSpecial))
                GUI.Label(new Rect(0, Screen.height * .29f, Screen.width, 72f * scale), comboSystem.ActiveSpecial, TextStyle(Mathf.RoundToInt(38f * scale), new Color(1f, .82f, .2f)));
        }

        private static void DrawBar(Rect rect, float progress, Color fill)
        {
            GUI.Box(rect, string.Empty);
            Color old = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(new Rect(rect.x + 3f, rect.y + 3f, Mathf.Max(0f, (rect.width - 6f) * Mathf.Clamp01(progress)), Mathf.Max(1f, rect.height - 6f)), Texture2D.whiteTexture);
            GUI.color = old;
        }

        private static void CompactCard(Rect rect, string key, string glyph, string attack, Color accent, GUIStyle text, GUIStyle panel)
        {
            DrawPanel(rect, panel);
            Color old = GUI.color; GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x + 7f, rect.y + 8f, 44f, rect.height - 16f), InsetTexture()); GUI.color = old;
            GUI.Label(new Rect(rect.x + 7f, rect.y + 8f, 44f, rect.height - 16f), key, TextStyle(Mathf.RoundToInt(rect.height * .29f), accent));
            GUI.Label(new Rect(rect.x + 58f, rect.y + 5f, 48f, rect.height - 10f), glyph, TextStyle(Mathf.RoundToInt(rect.height * .36f), accent));
            GUI.Label(new Rect(rect.x + 103f, rect.y + 5f, rect.width - 111f, rect.height - 10f), attack, text);
        }

        private static void PlayerSlot(Rect rect, int index, bool active, int hp, float scale)
        {
            GUIStyle slot = new(GUI.skin.box) { normal = { background = InsetTexture() }, border = new RectOffset(12, 12, 12, 12) };
            GUI.Box(rect, string.Empty, slot);
            Color[] colors = { new(.13f,.72f,.96f), new(.94f,.42f,.28f), new(.72f,.38f,.95f), new(.3f,.86f,.48f) };
            Color tint = active ? colors[index] : new Color(.32f, .38f, .42f);
            Color old = GUI.color; GUI.color = tint;
            GUI.DrawTexture(new Rect(rect.x + 7f * scale, rect.y + 5f * scale, 27f * scale, 27f * scale), Texture2D.whiteTexture);
            GUI.color = old;
            GUI.Label(new Rect(rect.x + 40f * scale, rect.y, 38f * scale, rect.height), $"{index + 1}P", TextStyle(Mathf.RoundToInt(13f * scale)));
            GUI.Label(new Rect(rect.x + 76f * scale, rect.y, 54f * scale, rect.height), active ? "READY" : "WAIT", TextStyle(Mathf.RoundToInt(11f * scale), active ? Gold() : new Color(.55f,.62f,.66f)));
            DrawBar(new Rect(rect.x + 136f * scale, rect.y + 12f * scale, rect.width - 146f * scale, 11f * scale), active ? hp / 100f : 0f, tint);
        }

        private static void DrawFinalOverlay(WarriorsBattlePhase phase, string swingTitle, string swingLabel, string failureLabel, int readyPlayers, int activePlayers, int score, int kills, float elapsedSeconds, float scale, GUIStyle panel)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .82f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), string.Empty, panel);
            GUI.color = previous;
            string eyebrow = phase == WarriorsBattlePhase.FinalSwingPhase ? swingTitle : string.Empty;
            string message = phase switch
            {
                WarriorsBattlePhase.FinalSwingPhase => swingLabel,
                WarriorsBattlePhase.Clear => "GAME CLEAR",
                _ => failureLabel
            };
            if (!string.IsNullOrEmpty(eyebrow))
                GUI.Label(new Rect(0, Screen.height * .20f, Screen.width, 70f * scale), eyebrow, TextStyle(Mathf.RoundToInt(32f * scale), new Color(1f, .82f, .22f)));
            GUI.Label(new Rect(0, Screen.height * .32f, Screen.width, 170f * scale), message, TextStyle(Mathf.RoundToInt(82f * scale), new Color(1f, .82f, .22f)));
            if (phase == WarriorsBattlePhase.FinalSwingPhase && swingLabel == "SWING!")
                GUI.Label(new Rect(0, Screen.height * .57f, Screen.width, 48f * scale), $"지금, 모두 함께 공격하세요!   {readyPlayers} / {activePlayers}", TextStyle(Mathf.RoundToInt(23f * scale)));
            if (phase == WarriorsBattlePhase.Clear)
            {
                int minutes = Mathf.FloorToInt(elapsedSeconds) / 60;
                int seconds = Mathf.FloorToInt(elapsedSeconds) % 60;
                Rect resultRect = new(Screen.width * .5f - 270f * scale, Screen.height * .55f, 540f * scale, 150f * scale);
                DrawPanel(resultRect, panel);
                GUI.Label(new Rect(resultRect.x + 20f * scale, resultRect.y + 12f * scale, resultRect.width - 40f * scale, 42f * scale), $"최종 점수   {score:N0}", TextStyle(Mathf.RoundToInt(27f * scale), new Color(1f, .84f, .25f)));
                GUI.Label(new Rect(resultRect.x + 20f * scale, resultRect.y + 58f * scale, resultRect.width - 40f * scale, 34f * scale), $"플레이 시간   {minutes:00}:{seconds:00}", TextStyle(Mathf.RoundToInt(20f * scale)));
                GUI.Label(new Rect(resultRect.x + 20f * scale, resultRect.y + 96f * scale, resultRect.width - 40f * scale, 34f * scale), $"몬스터 처치   {kills}", TextStyle(Mathf.RoundToInt(20f * scale)));
            }
        }

        private static void DrawTransitionOverlay(string message, float scale, GUIStyle panel)
        {
            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, .72f);
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), string.Empty, panel);
            GUI.color = previous;
            GUI.Label(new Rect(0, Screen.height * .40f, Screen.width, 100f * scale), message, TextStyle(Mathf.RoundToInt(48f * scale), new Color(1f, .84f, .25f)));
        }

        private static GUIStyle TextStyle(int size, Color? color = null) { GUIStyle style = new(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter }; style.normal.textColor = color ?? Color.white; return style; }
        private static Texture2D PanelTexture()
        {
            if (panelTexture != null) return panelTexture;
            panelTexture = RoundedTexture(new Color(.025f, .075f, .11f, .88f), new Color(1f, .72f, .18f, .65f), 12f, 2f);
            return panelTexture;
        }

        private static Texture2D ShadowTexture()
        {
            if (shadowTexture != null) return shadowTexture;
            shadowTexture = RoundedTexture(new Color(0f, 0f, 0f, .38f), Color.clear, 12f, 0f);
            return shadowTexture;
        }

        private static Texture2D InsetTexture()
        {
            if (insetTexture != null) return insetTexture;
            insetTexture = RoundedTexture(new Color(.015f, .12f, .16f, .92f), new Color(.16f, .55f, .62f, .55f), 10f, 1f);
            return insetTexture;
        }

        private static Color Gold() => new(1f, .78f, .2f, 1f);

        private static Texture2D RoundedTexture(Color fill, Color border, float radius, float borderWidth)
        {
            const int size = 32;
            Texture2D texture = new(size, size) { hideFlags = HideFlags.HideAndDontSave };
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0f);
                float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0f);
                float distance = Mathf.Sqrt(dx * dx + dy * dy);
                Color color = distance > radius ? Color.clear : distance > radius - borderWidth ? border : fill;
                texture.SetPixel(x, y, color);
            }
            texture.Apply();
            return texture;
        }

        private static void DrawPanel(Rect rect, GUIStyle panel)
        {
            GUIStyle shadow = new(panel) { normal = { background = ShadowTexture() } };
            GUI.Box(new Rect(rect.x + 5f, rect.y + 7f, rect.width, rect.height), string.Empty, shadow);
            GUI.Box(rect, string.Empty, panel);
        }
        private static void Card(Rect rect, string key, string target, string attack, Color accent, GUIStyle text, GUIStyle panel)
        {
            DrawPanel(rect, panel); Color previous = GUI.color; GUI.color = accent;
            GUI.DrawTexture(new Rect(rect.x, rect.y, Mathf.Max(6f, rect.height * .075f), rect.height), Texture2D.whiteTexture); GUI.color = previous;
            float unit = rect.height / 88f;
            GUI.Label(new Rect(rect.x + 14f * unit, rect.y + 7f * unit, 42f * unit, rect.height - 14f * unit), key, text);
            GUI.Label(new Rect(rect.x + 58f * unit, rect.y + 7f * unit, rect.width - 70f * unit, 35f * unit), target, text);
            GUI.Label(new Rect(rect.x + 58f * unit, rect.y + 43f * unit, rect.width - 70f * unit, 34f * unit), attack, text);
        }
    }
}
