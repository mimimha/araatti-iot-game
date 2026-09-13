using System;

namespace Warriors
{
    public interface IWarriorsInputSource
    {
        event Action<WarriorsAttackDirection, float> AttackRequested;
        event Action DodgeRequested;
    }

    public interface IWarriorsPlayerInputSource
    {
        event Action<WarriorsAttackInput> PlayerAttackRequested;
    }
}
