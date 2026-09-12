using System;
using System.Collections.Generic;
using UnityEngine;

namespace Warriors
{
    /// <summary>
    /// Accepts classifier output only. Sensor transport and model inference remain
    /// outside the game layer and can be replaced without changing combat code.
    /// </summary>
    public sealed class WarriorsAIInputAdapter : MonoBehaviour, IWarriorsInputSource, IWarriorsPlayerInputSource
    {
        [Header("Routing")]
        [SerializeField, Range(0, 3)] private int localPlayerId;

        [Header("Prediction filtering")]
        [SerializeField, Range(0f, 1f)] private float minConfidence = .7f;
        [SerializeField, Min(.05f)] private float predictionCooldownSeconds = .45f;
        [SerializeField, Min(0f)] private double duplicateTimestampTolerance = .001d;

        [Header("Development overlay")]
        [SerializeField] private bool showDebugOverlay;
        [SerializeField, Range(0, 3)] private int debugPlayerId;
        [SerializeField, Range(0f, 1f)] private float debugConfidence = .95f;
        [SerializeField, Range(0f, 1f)] private float debugStrength = .8f;

        private readonly Dictionary<int, double> lastAcceptedTimestamp = new();
        private WarriorsAIPrediction lastPrediction;
        private bool hasPrediction;

        public float MinConfidence => minConfidence;
        public event Action<WarriorsAttackDirection, float> AttackRequested;
        public event Action DodgeRequested;
        public event Action<WarriorsAttackInput> PlayerAttackRequested;
        public event Action<WarriorsAIPrediction, bool> PredictionProcessed;

        public bool OnActionPredicted(
            int playerId,
            WarriorsAttackDirection attackType,
            float confidence,
            float strength,
            double timestamp)
        {
            WarriorsAIPrediction prediction = new(playerId, attackType, confidence, strength, timestamp);
            lastPrediction = prediction;
            hasPrediction = true;

            bool accepted = IsAccepted(prediction);
            PredictionProcessed?.Invoke(prediction, accepted);
            if (!accepted) return false;

            lastAcceptedTimestamp[prediction.PlayerId] = prediction.Timestamp;
            WarriorsAttackInput input = new(
                prediction.PlayerId,
                prediction.AttackType,
                prediction.Strength,
                prediction.Timestamp);
            PlayerAttackRequested?.Invoke(input);
            if (prediction.PlayerId == localPlayerId)
                AttackRequested?.Invoke(prediction.AttackType, prediction.Strength);
            return true;
        }

        public bool OnActionPredicted(WarriorsAIPrediction prediction) => OnActionPredicted(
            prediction.PlayerId,
            prediction.AttackType,
            prediction.Confidence,
            prediction.Strength,
            prediction.Timestamp);

        private bool IsAccepted(WarriorsAIPrediction prediction)
        {
            if (prediction.AttackType == WarriorsAttackDirection.None) return false;
            if (prediction.Confidence < minConfidence) return false;
            if (double.IsNaN(prediction.Timestamp) || double.IsInfinity(prediction.Timestamp)) return false;
            if (!lastAcceptedTimestamp.TryGetValue(prediction.PlayerId, out double previous)) return true;
            double elapsed = prediction.Timestamp - previous;
            return elapsed > duplicateTimestampTolerance && elapsed >= predictionCooldownSeconds;
        }

        [ContextMenu("Debug AI/Horizontal Slash")]
        private void DebugHorizontal() => SubmitDebug(WarriorsAttackDirection.HorizontalSlash);

        [ContextMenu("Debug AI/Vertical Slash")]
        private void DebugVertical() => SubmitDebug(WarriorsAttackDirection.VerticalSlash);

        [ContextMenu("Debug AI/Thrust")]
        private void DebugThrust() => SubmitDebug(WarriorsAttackDirection.Thrust);

        [ContextMenu("Debug AI/None")]
        private void DebugNone() => SubmitDebug(WarriorsAttackDirection.None);

        private void SubmitDebug(WarriorsAttackDirection type)
        {
            OnActionPredicted(
                debugPlayerId,
                type,
                debugConfidence,
                debugStrength,
                Time.realtimeSinceStartupAsDouble);
        }

        private void OnGUI()
        {
            if (!showDebugOverlay || !hasPrediction) return;
            string state = lastPrediction.AttackType == WarriorsAttackDirection.None ||
                           lastPrediction.Confidence < minConfidence ? "IGNORED" : "ACCEPTED";
            GUI.Box(new Rect(18f, Screen.height - 112f, 260f, 90f), string.Empty);
            GUI.Label(new Rect(30f, Screen.height - 104f, 236f, 24f), "AI INPUT");
            GUI.Label(new Rect(30f, Screen.height - 78f, 236f, 24f), $"{lastPrediction.AttackType}  {state}");
            GUI.Label(new Rect(30f, Screen.height - 52f, 236f, 24f), $"Confidence {lastPrediction.Confidence:P0}");
        }
    }
}
