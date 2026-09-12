namespace Warriors
{
    /// <summary>
    /// 한 판 동안 쓰는 난수원.
    ///
    /// 멀티에서 네 명이 같은 판을 보려면 난수가 클라이언트마다 따로 돌면 안 된다.
    /// <c>UnityEngine.Random</c> 은 각자의 전역 상태를 쓰기 때문에, 같은 코드를 돌려도
    /// 사람마다 다른 몬스터가 다른 자리에 뜨고 촉수 약점이 다르고 리듬 악보가 달라진다.
    /// 그러면 "저 촉수는 세로베기야" 같은 말이 통하지 않는다.
    ///
    /// 그래서 판이 시작될 때 <b>시드 하나</b>를 정하고 모두가 그 시드로 같은 수열을 뽑는다.
    /// 지금은 싱글이라 <see cref="BeginRun()"/> 가 스스로 시드를 고르고,
    /// 네트워크가 붙으면 서버가 정한 시드를 <see cref="BeginRun(int)"/> 로 넣어 주면 된다.
    /// 그 외의 코드는 바꿀 것이 없다.
    ///
    /// ⚠ 같은 수열을 뽑으려면 <b>뽑는 순서</b>도 같아야 한다. 스폰·보스·리듬처럼
    ///    판의 진행을 정하는 곳에서만 쓰고, 연출용 흔들림 같은 곳에는 쓰지 않는다.
    ///    그런 것까지 섞으면 클라이언트마다 호출 횟수가 달라져 수열이 어긋난다.
    /// </summary>
    public static class WarriorsRun
    {
        private static System.Random rng = new(0);

        /// <summary>이번 판의 시드. 네트워크가 붙으면 서버가 이 값을 모두에게 알려 준다.</summary>
        public static int Seed { get; private set; }

        /// <summary>시드를 직접 정해 판을 시작한다. 서버가 시드를 쥐는 경우에 쓴다.</summary>
        public static void BeginRun(int seed)
        {
            Seed = seed;
            rng = new System.Random(seed);
        }

        /// <summary>시드를 스스로 골라 판을 시작한다. 싱글 플레이와 에디터 검증용.</summary>
        public static void BeginRun() => BeginRun(System.Environment.TickCount);

        /// <summary>
        /// <paramref name="minInclusive"/> 이상 <paramref name="maxExclusive"/> 미만의 정수.
        /// UnityEngine.Random.Range(int, int) 와 같은 범위 규칙이다.
        /// </summary>
        public static int Range(int minInclusive, int maxExclusive) =>
            maxExclusive <= minInclusive ? minInclusive : rng.Next(minInclusive, maxExclusive);

        /// <summary>
        /// <paramref name="min"/> 이상 <paramref name="max"/> 이하의 실수.
        /// UnityEngine.Random.Range(float, float) 와 같은 범위 규칙이다.
        /// </summary>
        public static float Range(float min, float max) =>
            max <= min ? min : min + (float)rng.NextDouble() * (max - min);
    }
}
