using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    public readonly struct WarriorsRhythmNoteView
    {
        public readonly WarriorsAttackDirection Type;
        public readonly int PlayerIndex;
        public readonly float Travel;

        public WarriorsRhythmNoteView(WarriorsAttackDirection type, int playerIndex, float travel)
        {
            Type = type;
            PlayerIndex = playerIndex;
            Travel = travel;
        }
    }

    public sealed class WarriorsRhythmBattle : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;
        [SerializeField] private WarriorsBattleScore score;
        [SerializeField] private WarriorsKrakenBoss kraken;
        [SerializeField] private WarriorsGameFlow gameFlow;
        [SerializeField] private WarriorsPlayerCombat playerCombat;
        [SerializeField] private WarriorsThirdPersonCamera combatCamera;
        [SerializeField, Range(8, 40)] private int noteCount = 18;
        [SerializeField, Min(.35f)] private float noteInterval = .72f;
        [SerializeField, Min(1f)] private float noteTravelSeconds = 2.2f;
        [SerializeField, Range(.05f, .3f)] private float perfectWindow = .12f;
        [SerializeField, Range(.15f, .6f)] private float goodWindow = .3f;
        [SerializeField, Range(-.5f, .5f)] private float inputOffset;
        [SerializeField] private bool drawLegacyHud = true;

        private readonly List<RhythmNote> notes = new();
        private IWarriorsInputSource InputSource => inputSource as IWarriorsInputSource;
        private int resolvedNotes;
        private int successfulNotes;
        private int combo;
        private int bestCombo;
        private string judgement;
        private float judgementUntil;
        private bool finishing;

        public bool IsActive { get; private set; }
        public int Combo => combo;
        public int BestCombo => bestCombo;
        public float Progress => notes.Count == 0 ? 0f : Mathf.Clamp01(resolvedNotes / (float)notes.Count);
        public float Accuracy => resolvedNotes == 0 ? 1f : successfulNotes / (float)resolvedNotes;
        public string ActiveJudgement => Time.time < judgementUntil ? judgement : string.Empty;
        public int ActivePlayerCount => gameFlow != null ? Mathf.Clamp(gameFlow.ActivePlayerCount, 1, 4) : 1;
        public WarriorsAttackDirection NextExpectedType
        {
            get
            {
                foreach (RhythmNote note in notes) if (!note.Resolved) return note.Type;
                return WarriorsAttackDirection.None;
            }
        }
        public float SecondsUntilNextNote
        {
            get
            {
                foreach (RhythmNote note in notes) if (!note.Resolved) return note.HitTime - (Time.time + inputOffset);
                return 0f;
            }
        }
        public event Action Completed;

        public void SetLegacyHudVisible(bool visible) => drawLegacyHud = visible;

        public void CopyVisibleNotes(List<WarriorsRhythmNoteView> output)
        {
            output.Clear();
            if (!IsActive) return;
            float now = Time.time + inputOffset;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved) continue;
                float travel = 1f - (note.HitTime - now) / noteTravelSeconds;
                if (travel < -.1f || travel > 1.18f) continue;
                output.Add(new WarriorsRhythmNoteView(note.Type, note.PlayerIndex, travel));
            }
        }

        private sealed class RhythmNote
        {
            public WarriorsAttackDirection Type;
            public float HitTime;
            public int PlayerIndex;
            public bool Resolved;
        }

        private void OnEnable()
        {
            if (InputSource != null) InputSource.AttackRequested += HandleAttack;
        }

        private void OnDisable()
        {
            if (InputSource != null) InputSource.AttackRequested -= HandleAttack;
        }

        public void Configure(MonoBehaviour source, WarriorsBattleScore battleScore, WarriorsKrakenBoss boss)
        {
            if (isActiveAndEnabled && InputSource != null) InputSource.AttackRequested -= HandleAttack;
            inputSource = source;
            score = battleScore;
            kraken = boss;
            if (gameFlow == null) gameFlow = UnityEngine.Object.FindFirstObjectByType<WarriorsGameFlow>(FindObjectsInactive.Include);
            if (playerCombat == null) playerCombat = UnityEngine.Object.FindFirstObjectByType<WarriorsPlayerCombat>(FindObjectsInactive.Include);
            if (combatCamera == null) combatCamera = UnityEngine.Object.FindFirstObjectByType<WarriorsThirdPersonCamera>(FindObjectsInactive.Include);
            if (isActiveAndEnabled && InputSource != null) InputSource.AttackRequested += HandleAttack;
        }

        public void Begin()
        {
            notes.Clear();
            resolvedNotes = 0;
            successfulNotes = 0;
            combo = 0;
            bestCombo = 0;
            judgement = string.Empty;
            finishing = false;
            float firstHit = Time.time + noteTravelSeconds + .75f;
            WarriorsAttackDirection previous = WarriorsAttackDirection.None;
            int repeated = 0;
            for (int i = 0; i < noteCount; i++)
            {
                WarriorsAttackDirection type = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);
                if (type == previous) repeated++; else repeated = 1;
                if (repeated > 2)
                {
                    type = (WarriorsAttackDirection)(((int)type + WarriorsRun.Range(1, 3)) % 3);
                    repeated = 1;
                }
                previous = type;
                int playerCount = gameFlow != null ? Mathf.Clamp(gameFlow.ActivePlayerCount, 1, 4) : 1;
                notes.Add(new RhythmNote { Type = type, HitTime = firstHit + i * noteInterval, PlayerIndex = i % playerCount });
            }
            IsActive = true;
        }

        private void Update()
        {
            if (!IsActive || finishing) return;
            float now = Time.time + inputOffset;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved) continue;
                if (now <= note.HitTime + goodWindow) break;
                Resolve(note, false, "MISS", 0);
            }
            TryFinish();
        }

        private void HandleAttack(WarriorsAttackDirection type, float strength)
        {
            if (!IsActive || finishing || type == WarriorsAttackDirection.None) return;
            float now = Time.time + inputOffset;
            RhythmNote closest = null;
            float closestDelta = float.MaxValue;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved) continue;
                float delta = Mathf.Abs(now - note.HitTime);
                if (delta < closestDelta) { closest = note; closestDelta = delta; }
                if (note.HitTime > now + goodWindow) break;
            }

            if (closest == null || closestDelta > goodWindow) return;
            if (closest.Type != type)
            {
                Resolve(closest, false, "MISS", 0);
                TryFinish();
                return;
            }

            bool perfect = closestDelta <= perfectWindow;
            Resolve(closest, true, perfect ? "PERFECT!" : "GOOD", perfect ? 300 : 150);
            bool strong = perfect || combo > 0 && combo % 5 == 0;
            playerCombat?.RequestAttack(type);
            kraken?.ApplyRhythmHit(strong);
            combatCamera?.Shake(strong ? .28f : .16f);
            TryFinish();
        }

        private void Resolve(RhythmNote note, bool success, string label, int points)
        {
            if (note.Resolved) return;
            note.Resolved = true;
            resolvedNotes++;
            judgement = label;
            judgementUntil = Time.time + .55f;
            if (success)
            {
                successfulNotes++;
                combo++;
                bestCombo = Mathf.Max(bestCombo, combo);
                score?.RegisterBossHit(points);
            }
            else combo = 0;
        }

        private void TryFinish()
        {
            if (finishing || resolvedNotes < notes.Count) return;
            finishing = true;
            StartCoroutine(FinishRoutine());
        }

        private IEnumerator FinishRoutine()
        {
            judgement = "FINAL COMBO";
            judgementUntil = float.PositiveInfinity;
            kraken?.CompleteRhythmBattle();
            yield return new WaitForSeconds(1.15f);
            IsActive = false;
            Completed?.Invoke();
        }

        private void OnGUI()
        {
            if (!drawLegacyHud) return;
            if (!IsActive) return;
            float scale = Mathf.Clamp(Screen.height / 900f, .8f, 1.5f);
            GUIStyle centered = new(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(22f * scale) };
            centered.normal.textColor = Color.white;
            GUIStyle large = new(centered) { fontSize = Mathf.RoundToInt(46f * scale) };
            large.normal.textColor = new Color(1f, .82f, .2f);

            GUI.Box(new Rect(18f * scale, 18f * scale, 205f * scale, 86f * scale), string.Empty);
            GUI.Label(new Rect(28f * scale, 22f * scale, 185f * scale, 34f * scale), "ROUND 3  FINAL", centered);
            int secondsLeft = score != null ? Mathf.CeilToInt(score.RemainingSeconds) : 0;
            GUI.Label(new Rect(28f * scale, 57f * scale, 185f * scale, 36f * scale), $"TIME  {secondsLeft / 60:00}:{secondsLeft % 60:00}", centered);

            float bossWidth = Mathf.Min(660f * scale, Screen.width * .52f);
            float bossX = (Screen.width - bossWidth) * .5f;
            GUI.Box(new Rect(bossX, 20f * scale, bossWidth, 70f * scale), string.Empty);
            GUI.Label(new Rect(bossX, 20f * scale, bossWidth, 34f * scale), "KRAKEN  ·  RHYTHM BREAK", centered);
            GUI.Box(new Rect(bossX + 30f * scale, 60f * scale, bossWidth - 60f * scale, 14f * scale), string.Empty);
            Color old = GUI.color; GUI.color = new Color(1f, .2f, .42f);
            float krakenHealth = kraken != null ? kraken.FinalFormHealthPercent / 100f : 1f - Progress;
            GUI.DrawTexture(new Rect(bossX + 33f * scale, 63f * scale, (bossWidth - 66f * scale) * krakenHealth, 8f * scale), Texture2D.whiteTexture); GUI.color = old;

            float rightX = Screen.width - 225f * scale;
            GUI.Box(new Rect(rightX, 18f * scale, 205f * scale, 104f * scale), string.Empty);
            GUI.Label(new Rect(rightX, 22f * scale, 205f * scale, 42f * scale), $"SCORE  {(score != null ? score.Score : 0):N0}", centered);
            GUI.Label(new Rect(rightX, 65f * scale, 205f * scale, 45f * scale), $"COMBO  {combo}", large);

            float hitY = Screen.height * .70f;
            float topY = Screen.height * .16f;
            float centerX = Screen.width * .5f;
            int playerCount = gameFlow != null ? Mathf.Clamp(gameFlow.ActivePlayerCount, 1, 4) : 1;
            float laneGap = 150f * scale;
            float lanesWidth = laneGap * (playerCount - 1);
            GUI.color = new Color(.4f, .85f, 1f, .65f);
            GUI.DrawTexture(new Rect(centerX - Mathf.Max(90f * scale, lanesWidth * .5f + 65f * scale), hitY,
                Mathf.Max(180f * scale, lanesWidth + 130f * scale), 4f * scale), Texture2D.whiteTexture);
            GUI.color = old;

            float now = Time.time + inputOffset;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved) continue;
                float normalized = 1f - (note.HitTime - now) / noteTravelSeconds;
                if (normalized < -.1f || normalized > 1.18f) continue;
                float laneX = centerX - lanesWidth * .5f + note.PlayerIndex * laneGap;
                float y = Mathf.Lerp(topY, hitY, normalized);
                Rect noteRect = new(laneX - 35f * scale, y - 35f * scale, 70f * scale, 70f * scale);
                GUI.Box(noteRect, string.Empty);
                GUI.Label(noteRect, Glyph(note.Type), large);
            }

            if (Time.time < judgementUntil)
                GUI.Label(new Rect(0, Screen.height * .49f, Screen.width, 80f * scale), judgement, large);

            float cardY = Screen.height - 86f * scale;
            DrawInputCard(centerX - 285f * scale, cardY, "1", "↔", "가로베기", centered, scale);
            DrawInputCard(centerX - 90f * scale, cardY, "2", "↕", "세로베기", centered, scale);
            DrawInputCard(centerX + 105f * scale, cardY, "3", "⊙", "찌르기", centered, scale);
        }

        private static string Glyph(WarriorsAttackDirection type) => type switch
        {
            WarriorsAttackDirection.HorizontalSlash => "↔",
            WarriorsAttackDirection.VerticalSlash => "↕",
            _ => "⊙"
        };

        private static void DrawInputCard(float x, float y, string key, string glyph, string label, GUIStyle style, float scale)
        {
            Rect rect = new(x, y, 180f * scale, 66f * scale);
            GUI.Box(rect, string.Empty);
            GUI.Label(new Rect(rect.x + 8f * scale, rect.y, 35f * scale, rect.height), key, style);
            GUI.Label(new Rect(rect.x + 42f * scale, rect.y, 48f * scale, rect.height), glyph, style);
            GUI.Label(new Rect(rect.x + 88f * scale, rect.y, 84f * scale, rect.height), label, style);
        }
    }
}
