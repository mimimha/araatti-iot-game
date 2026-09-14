using System;

namespace Warriors
{
    [Serializable]
    public readonly struct WarriorsResult
    {
        public bool Success { get; }
        public int Score { get; }

        public WarriorsResult(bool success, int score)
        {
            Success = success;
            Score = Math.Max(0, score);
        }
    }
}
