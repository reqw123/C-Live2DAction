using UnityEngine;

namespace Live2DAction.Input
{
    // Shared by player input and (from Phase 2 onward) AI decision-makers,
    // so combat/movement code never needs to know where input came from.
    public interface IInputCommand
    {
        Vector2 MoveInput { get; }
        bool AttackPressed { get; }
        bool DodgePressed { get; }
        bool LockOnPressed { get; }
    }
}
