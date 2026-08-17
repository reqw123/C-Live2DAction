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
        bool JumpPressed { get; }

        // 2026-08-13, explicit user request (ultimate skill, R key).
        bool UltimatePressed { get; }

        // 2026-08-18, explicit user request (flight: "按住鍵自由飛行") - both are HELD signals
        // (isPressed, not wasPressedThisFrame), unlike every bool above this one, which are all
        // single-frame edge triggers for a one-shot action (attack/dodge/lock-on/jump/ultimate).
        // Flight needs to know "is the key still down right now" every frame to keep
        // ascending/descending for as long as it's held, not just the instant it was pressed.
        bool FlyPressed { get; }
        bool FlyDescendPressed { get; }
    }
}
