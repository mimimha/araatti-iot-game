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
        public readonly bool IsSuccessfulHit;

        public WarriorsRhythmNoteView(WarriorsAttackDirection type, int playerIndex, float travel, bool isSuccessfulHit = false)
        {
            Type = type;
            PlayerIndex = playerIndex;
            Travel = travel;
            IsSuccessfulHit = isSuccessfulHit;
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
        [SerializeField, Range(2, 5)] private int patternLength = 3;
        [SerializeField, Min(.35f)] private float noteInterval = .72f;
        [SerializeField, Min(1)] private int finisherDamage = 22;
        [SerializeField, Min(1)] private int partialDamage = 9;
        [SerializeField, Min(.4f)] private float counterSeconds = 1.5f;
        [SerializeField, Min(1)] private int counterDamage = 10;
        [SerializeField, Min(.4f)] private float counterTelegraphSeconds = 1.1f;
        [SerializeField, Range(5, 40)] private int lastChancePercent = 25;
        [SerializeField, Min(1f)] private float noteTravelSeconds = 2.2f;
        // Tight on purpose: the swing has to land while the icon is on the line, not
        // somewhere near it. Wider than this and the note is visibly past the line by the
        // time a hit still counts, which is what made the timing feel arbitrary.
        [SerializeField, Range(.05f, .3f)] private float perfectWindow = .1f;
        [SerializeField, Range(.1f, .6f)] private float goodWindow = .2f;
        [SerializeField, Range(-.5f, .5f)] private float inputOffset;
        [SerializeField] private bool drawLegacyHud = true;

        /// <summary>Handed over by the flow, the same way the boss gets it.</summary>
        public void ConfigurePlayer(WarriorsHealth health) => playerHealth = health;

        private WarriorsHealth playerHealth;

        /// <summary>
        /// Open while the kraken is winding up its counter. Answering inside it is what
        /// makes the kraken's turn a beat the player plays rather than a beat they watch.
        /// </summary>
        private bool counterWindowOpen;
        private bool counterAnswered;

        private readonly List<RhythmNote> notes = new();
        private IWarriorsInputSource InputSource => inputSource as IWarriorsInputSource;
        private IWarriorsPlayerInputSource PlayerInputSource => inputSource as IWarriorsPlayerInputSource;
        private int resolvedNotes;
        private int successfulNotes;
        private int combo;
        private int bestCombo;
        private readonly int[] playerCombos = new int[4];
        private readonly int[] playerBestCombos = new int[4];
        private string judgement;
        private float judgementUntil;
        private bool finishing;
        private readonly int[] patternHits = new int[4];
        private int patternIndex;
        private Coroutine patternLoop;

        /// <summary>True while the kraken is taking its turn and no note is falling.</summary>
        public bool IsCountering { get; private set; }

        public bool IsActive { get; private set; }
        public int Combo => combo;
        public int BestCombo => bestCombo;
        public float Progress => notes.Count == 0 ? 0f : Mathf.Clamp01(resolvedNotes / (float)notes.Count);
        public float Accuracy => resolvedNotes == 0 ? 1f : successfulNotes / (float)resolvedNotes;
        public string ActiveJudgement => Time.time < judgementUntil ? judgement : string.Empty;
        public int ActivePlayerCount => gameFlow != null ? Mathf.Clamp(gameFlow.ActivePlayerCount, 1, 4) : 1;
        public int GetCombo(int playerId) => playerId >= 0 && playerId < playerCombos.Length ? playerCombos[playerId] : 0;
        public WarriorsAttackDirection NextExpectedType
        {
            get
            {
                foreach (RhythmNote note in notes) if (!note.Resolved) return note.Type;
                return WarriorsAttackDirection.None;
            }
        }
        public WarriorsAttackDirection GetNextExpectedType(int playerId)
        {
            foreach (RhythmNote note in notes)
                if (!note.Resolved && note.PlayerIndex == playerId)
                    return note.Type;
            return WarriorsAttackDirection.None;
        }
        public float SecondsUntilNextNote
        {
            get
            {
                foreach (RhythmNote note in notes) if (!note.Resolved) return note.HitTime - (Time.time + inputOffset);
                return 0f;
            }
        }
        public float GetSecondsUntilNextNote(int playerId)
        {
            foreach (RhythmNote note in notes)
                if (!note.Resolved && note.PlayerIndex == playerId)
                    return note.HitTime - (Time.time + inputOffset);
            return 0f;
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
                // A note that was struck disappears on the hit. One that was missed keeps
                // falling for a moment and slides out under the judgement line, so the line
                // reads as something notes pass through rather than a wall they vanish at.
                if (note.Resolved && (note.Successful || Time.time > note.ResolvedAt + .45f)) continue;
                float travel = 1f - (note.HitTime - now) / noteTravelSeconds;
                // Past 1 the note is below the line and on its way out, which is what makes
                // the line read as a line rather than a wall.
                if (travel < -.1f || travel > 1.45f) continue;
                output.Add(new WarriorsRhythmNoteView(note.Type, note.PlayerIndex, travel));
            }
        }

        private sealed class RhythmNote
        {
            public WarriorsAttackDirection Type;
            public float HitTime;
            public int PlayerIndex;
            public bool Resolved;
            public bool Successful;
            public float SuccessFeedbackUntil;
            public float ResolvedAt;
        }

        private void OnEnable()
        {
            SubscribeInput();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        public void Configure(MonoBehaviour source, WarriorsBattleScore battleScore, WarriorsKrakenBoss boss)
        {
            if (isActiveAndEnabled) UnsubscribeInput();
            inputSource = source;
            score = battleScore;
            kraken = boss;
            if (gameFlow == null) gameFlow = UnityEngine.Object.FindFirstObjectByType<WarriorsGameFlow>(FindObjectsInactive.Include);
            if (playerCombat == null) playerCombat = UnityEngine.Object.FindFirstObjectByType<WarriorsPlayerCombat>(FindObjectsInactive.Include);
            if (combatCamera == null) combatCamera = UnityEngine.Object.FindFirstObjectByType<WarriorsThirdPersonCamera>(FindObjectsInactive.Include);
            if (isActiveAndEnabled) SubscribeInput();
        }

        private void SubscribeInput()
        {
            if (PlayerInputSource != null)
                PlayerInputSource.PlayerAttackRequested += HandlePlayerAttack;
            else if (InputSource != null)
                InputSource.AttackRequested += HandleLegacyAttack;
            // The dodge the IoT sword already sends - reused rather than replaced.
            if (InputSource != null) InputSource.DodgeRequested += HandleDodge;
        }

        private void UnsubscribeInput()
        {
            if (PlayerInputSource != null)
                PlayerInputSource.PlayerAttackRequested -= HandlePlayerAttack;
            else if (InputSource != null)
                InputSource.AttackRequested -= HandleLegacyAttack;
            if (InputSource != null) InputSource.DodgeRequested -= HandleDodge;
        }

        public void Begin()
        {
            notes.Clear();
            resolvedNotes = 0;
            successfulNotes = 0;
            combo = 0;
            bestCombo = 0;
            Array.Clear(playerCombos, 0, playerCombos.Length);
            Array.Clear(playerBestCombos, 0, playerBestCombos.Length);
            Array.Clear(patternHits, 0, patternHits.Length);
            judgement = string.Empty;
            finishing = false;
            patternIndex = 0;
            IsCountering = false;
            IsActive = true;
            if (patternLoop != null) StopCoroutine(patternLoop);
            patternLoop = StartCoroutine(PatternLoop());
        }

        /// <summary>
        /// ROUND 3 is a boss fight, not an endless note stream: a short pattern of attacks
        /// opens, the player answers it, the kraken takes the damage that answer earned, and
        /// then the kraken gets a turn of its own.  The fight ends when its health is gone,
        /// so what the player actually hits is what decides the round.
        /// </summary>
        private IEnumerator PatternLoop()
        {
            while (IsActive && kraken != null && kraken.FinalFormHealth > 0)
            {
                bool lastChance = kraken.FinalFormHealthPercent <= lastChancePercent;
                if (lastChance)
                {
                    judgement = "마지막 공격 기회!";
                    judgementUntil = Time.time + 1.4f;
                    yield return new WaitForSeconds(.9f);
                }

                SpawnPattern();
                yield return new WaitUntil(() => AllNotesResolved() || !IsActive);
                if (!IsActive) yield break;
                yield return new WaitForSeconds(.3f);

                int counter = ApplyPatternOutcome();
                if (kraken == null || kraken.FinalFormHealth <= 0) break;

                yield return KrakenCounterRoutine(counter);
                patternIndex++;
            }

            IsActive = false;
            IsCountering = false;
            // The kraken dying raises FinalFormDefeated, and the flow clears the round from
            // there, so nothing is published here.
        }

        private bool AllNotesResolved()
        {
            foreach (RhythmNote note in notes) if (!note.Resolved) return false;
            return true;
        }

        /// <summary>
        /// One pattern per player, <see cref="patternLength"/> notes long.  Later patterns sit
        /// closer together, but never so close that a real sword swing cannot keep up.
        /// </summary>
        private void SpawnPattern()
        {
            notes.Clear();
            resolvedNotes = 0;
            Array.Clear(patternHits, 0, patternHits.Length);

            float ramp = Mathf.Clamp01(patternIndex / 5f);
            float gap = Mathf.Lerp(noteInterval, noteInterval * .72f, ramp);
            float firstHit = Time.time + noteTravelSeconds + .35f;
            int playerCount = ActivePlayerCount;

            for (int playerIndex = 0; playerIndex < playerCount; playerIndex++)
            {
                WarriorsAttackDirection previous = WarriorsAttackDirection.None;
                for (int i = 0; i < patternLength; i++)
                {
                    WarriorsAttackDirection type = (WarriorsAttackDirection)WarriorsRun.Range(0, 3);
                    // Controlled random: the same swing twice in a row is allowed, three times
                    // is not, so a pattern still reads as something to be looked at.
                    if (type == previous && WarriorsRun.Range(0, 100) < 70)
                        type = (WarriorsAttackDirection)(((int)type + WarriorsRun.Range(1, 3)) % 3);
                    notes.Add(new RhythmNote { Type = type, HitTime = firstHit + i * gap, PlayerIndex = playerIndex });
                    previous = type;
                }
            }
            notes.Sort((a, b) => a.HitTime.CompareTo(b.HitTime));
        }

        /// <summary>
        /// Turns what each player landed in the pattern into damage.  A clean pattern is the
        /// big moment of the round; one miss still lands a normal blow, so a single slip does
        /// not throw the whole fight away.
        /// </summary>
        /// <returns>What the kraken takes back on its turn.</returns>
        private int ApplyPatternOutcome()
        {
            int playerCount = ActivePlayerCount;
            int totalDamage = 0;
            int finishers = 0;
            for (int playerIndex = 0; playerIndex < playerCount && playerIndex < patternHits.Length; playerIndex++)
            {
                int hits = patternHits[playerIndex];
                if (hits >= patternLength) { totalDamage += finisherDamage; finishers++; }
                else if (hits == patternLength - 1) totalDamage += partialDamage;
            }

            if (finishers > 0)
            {
                judgement = finishers > 1 ? "TEAM FINISH!" : "COMBO FINISH!";
                judgementUntil = Time.time + 1.1f;
                playerCombat?.RequestAttack(NextFinisherSwing(), 1.5f);
                // Three clean swings should not land like one lucky swing, so the finisher
                // gets the loud version of everything that already exists: a wider trail,
                // a harder shake, and a single frame of held time on the impact.
                combatCamera?.Shake(.55f);
                StartCoroutine(FinisherFlourish());
            }
            else if (totalDamage > 0)
            {
                combatCamera?.Shake(.16f);
            }
            else
            {
                judgement = "공격 실패";
                judgementUntil = Time.time + .9f;
            }

            kraken?.ApplyPatternDamage(totalDamage, finishers > 0);

            // Clearing a pattern buys the counter off; a partial one softens it. Without
            // this the counter was a camera shake, and ROUND 3 could not be lost at all.
            return finishers > 0 ? 0 : totalDamage > 0 ? counterDamage / 2 : counterDamage;
        }

        private IEnumerator FinisherFlourish()
        {
            var equipper = playerCombat != null
                ? playerCombat.GetComponent<WarriorsWeaponEquipper>() : null;
            equipper?.SetTrailBoost(true);

            // Short enough to read as weight rather than as the game stalling.
            float previousScale = Time.timeScale;
            Time.timeScale = .35f;
            yield return new WaitForSecondsRealtime(.09f);
            Time.timeScale = previousScale;

            yield return new WaitForSeconds(.45f);
            equipper?.SetTrailBoost(false);
        }

        private WarriorsAttackDirection NextFinisherSwing()
        {
            for (int i = notes.Count - 1; i >= 0; i--)
                if (notes[i].Successful) return notes[i].Type;
            return WarriorsAttackDirection.HorizontalSlash;
        }

        /// <summary>
        /// The kraken's turn. Without it ROUND 3 is just notes falling forever; with it the
        /// round reads as attack, answer, attack.
        /// </summary>
        private IEnumerator KrakenCounterRoutine(int damage)
        {
            IsCountering = true;
            notes.Clear();

            // The kraken's turn used to be a camera shake and a number coming off the health
            // bar, with nothing the player could do about it. Now it winds up first, and the
            // wind up is long enough to answer - which is what turns the counter into a beat
            // the player plays rather than one they sit through. It tightens as the fight runs.
            float telegraph = Mathf.Max(.45f, counterTelegraphSeconds - patternIndex * .07f);
            counterAnswered = false;
            counterWindowOpen = true;
            judgement = "크라켄의 반격!";
            judgementUntil = Time.time + telegraph;
            kraken?.PlayRhythmHit(false);
            combatCamera?.Shake(.12f);
            yield return new WaitForSeconds(telegraph);
            counterWindowOpen = false;

            if (counterAnswered || damage <= 0)
            {
                judgement = counterAnswered ? "막아냈다!" : "반격을 흘려보냈다!";
                judgementUntil = Time.time + .7f;
                combatCamera?.Shake(.1f);
            }
            else
            {
                playerHealth?.TryApplyDamage(damage);
                combatCamera?.Shake(.32f);
            }

            // A breath before the next pattern, so the kraken's turn and the player's turn
            // read as two beats instead of one continuous stream.
            yield return new WaitForSeconds(Mathf.Max(.3f, counterSeconds - patternIndex * .08f));
            IsCountering = false;
        }

        private void Update()
        {
            if (!IsActive || finishing || IsCountering) return;
            float now = Time.time + inputOffset;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved) continue;
                if (now <= note.HitTime + goodWindow) break;
                Resolve(note, false, "MISS", 0);
            }
        }

        private void HandleLegacyAttack(WarriorsAttackDirection type, float strength)
        {
            HandlePlayerAttack(new WarriorsAttackInput(
                0,
                type,
                strength,
                Time.realtimeSinceStartupAsDouble));
        }

        private void HandleDodge()
        {
            if (counterWindowOpen) counterAnswered = true;
        }

        private void HandlePlayerAttack(WarriorsAttackInput input)
        {
            WarriorsAttackDirection type = input.AttackType;
            float strength = input.Strength;
            if (!IsActive || finishing || type == WarriorsAttackDirection.None) return;
            // Meeting the blow with the blade counts, so the counter is answerable on a
            // keyboard as well as with the sword's dodge gesture.
            if (counterWindowOpen) { counterAnswered = true; return; }
            if (input.PlayerId < 0 || input.PlayerId >= ActivePlayerCount) return;
            float now = Time.time + inputOffset;
            RhythmNote closest = null;
            float closestDelta = float.MaxValue;
            foreach (RhythmNote note in notes)
            {
                if (note.Resolved || note.PlayerIndex != input.PlayerId) continue;
                float delta = Mathf.Abs(now - note.HitTime);
                if (delta < closestDelta) { closest = note; closestDelta = delta; }
                if (note.HitTime > now + goodWindow) break;
            }

            if (closest == null || closestDelta > goodWindow) return;
            if (closest.Type != type)
            {
                Resolve(closest, false, "MISS", 0);
                return;
            }

            bool perfect = closestDelta <= perfectWindow;
            Resolve(closest, true, perfect ? "PERFECT!" : "GOOD", perfect ? 300 : 150);
            // Damage is settled once per pattern, not per note, so that three clean swings
            // are worth more than three scattered ones.
            playerCombat?.RequestAttack(type);
            combatCamera?.Shake(.12f);
        }

        private void Resolve(RhythmNote note, bool success, string label, int points)
        {
            if (note.Resolved) return;
            note.Resolved = true;
            note.Successful = success;
            note.SuccessFeedbackUntil = success ? Time.time + .18f : 0f;
            note.ResolvedAt = Time.time;
            resolvedNotes++;
            // PERFECT and GOOD are how the timing is scored, not something to shout on every
            // note - three of them inside a second buried the pattern result underneath. A
            // miss still says so, because that is the one the player has to notice.
            if (!success)
            {
                judgement = label;
                judgementUntil = Time.time + .45f;
            }
            if (success)
            {
                successfulNotes++;
                int playerIndex = Mathf.Clamp(note.PlayerIndex, 0, playerCombos.Length - 1);
                patternHits[playerIndex]++;
                playerCombos[playerIndex]++;
                playerBestCombos[playerIndex] = Mathf.Max(playerBestCombos[playerIndex], playerCombos[playerIndex]);
                combo = MaxValue(playerCombos);
                bestCombo = MaxValue(playerBestCombos);
                score?.RegisterBossHit(points);
            }
            else
            {
                int playerIndex = Mathf.Clamp(note.PlayerIndex, 0, playerCombos.Length - 1);
                playerCombos[playerIndex] = 0;
                combo = MaxValue(playerCombos);
            }
        }

        private static int MaxValue(int[] values)
        {
            int result = 0;
            foreach (int value in values) result = Mathf.Max(result, value);
            return result;
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
                float y = Mathf.LerpUnclamped(topY, hitY, normalized);
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
