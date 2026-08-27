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

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.3/3.4) - flight
        // boost, same HELD-signal reasoning as FlyPressed/FlyDescendPressed above (needs to keep
        // boosting for as long as it's down, not just the instant it was pressed).
        bool BoostPressed { get; }

        // 2026-08-23, explicit user request ("加入瞄準-射擊機制 模擬槍戰") - AimPressed is a HELD
        // signal (same reasoning as FlyPressed/BoostPressed above: aiming needs to stay active for
        // as long as the button is down, not just the instant it's pressed), FirePressed is a
        // single-frame edge trigger like AttackPressed/DodgePressed (one shot per press, not one
        // shot per frame held).
        bool AimPressed { get; }
        bool FirePressed { get; }

        // 2026-08-23, explicit user request ("V鍵切換成第一視角(機制與右鍵瞄準同理)") - an edge
        // trigger (one toggle per press, like AttackPressed/DodgePressed) rather than a held
        // signal like AimPressed: pressing V flips a persistent first-person state, it doesn't
        // need to stay held the way aiming does.
        bool ViewTogglePressed { get; }

        // 2026-08-23, explicit user request ("新增只有在瞄準時作用鍵盤按鍵 :a 視角持續放大 e 視角持續
        // 變小") - HELD signals (same reasoning as AimPressed/FlyPressed above: zoom needs to keep
        // adjusting for as long as the key is down, not just the instant it's pressed). Only
        // ThirdPersonCameraController's aiming path ever reads these - see its own zoom fields'
        // comments for the actual FOV math.
        bool ZoomInPressed { get; }
        bool ZoomOutPressed { get; }
    }
}
