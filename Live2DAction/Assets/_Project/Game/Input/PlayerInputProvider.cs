using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Input
{
    // 2026-08-29 - runs before every consumer (movement / combat / camera / CatPounce /
    // CatChargeAttack, all at order >= -10) so they read THIS frame's input, not last frame's.
    // Before this, CatPounce (order -8) polled MoveInput a frame stale, which let a pounce fire
    // from movement input the player had already released - "有時普通攻擊也會衝刺". The Input
    // System evaluates wasPressedThisFrame off the event timeline, not poll order, so moving the
    // poll earlier can't double-consume or miss an edge.
    [DefaultExecutionOrder(-100)]
    public class PlayerInputProvider : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; private set; }
        public bool AttackPressed { get; private set; }

        // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 3.2) - HELD state of the
        // melee button (isPressed, not the edge), for the cat's charged heavy. Deliberately NOT
        // on IInputCommand: charging is a player-only mechanic (the enemy cat never charges, see
        // the design doc), so CatChargeAttack reads this off the concrete PlayerInputProvider
        // and no AI stub / interface implementer needs to change. Same !AimPressed guard as
        // AttackPressed so holding to charge a swing and holding to keep aiming stay separate.
        public bool AttackHeld { get; private set; }

        public bool DodgePressed { get; private set; }
        public bool LockOnPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool UltimatePressed { get; private set; }
        public bool FlyPressed { get; private set; }
        public bool FlyDescendPressed { get; private set; }
        public bool BoostPressed { get; private set; }
        // 2026-08-31, user request ("移除射擊系統") - shooting is retired, these never go true
        // anymore (right mouse drives GuardPressed now). Kept as members so RangedWeapon and the
        // test stubs still compile; RangedWeapon.Update just never sees an aim/fire and stays inert.
        public bool AimPressed => false;
        public bool FirePressed => false;

        // 2026-08-31, user request ("把滑鼠右鍵改成武士刀防禦") - held state of right mouse, drives
        // PlayerGuard (katana block). Was AimPressed's binding before the shooting system was cut.
        public bool GuardPressed { get; private set; }

        // 2026-09-01, Sekiro deflect - the press edge of right mouse, opens the parry window once
        // per press (PlayerGuard). Held GuardPressed keeps the guard up but never re-opens parry.
        public bool GuardPressedThisFrame { get; private set; }

        public bool ViewTogglePressed { get; private set; }
        public bool ZoomInPressed { get; private set; }
        public bool ZoomOutPressed { get; private set; }
        public bool WalkTogglePressed { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                MoveInput = Vector2.zero;
                AttackPressed = false;
                AttackHeld = false;
                DodgePressed = false;
                LockOnPressed = false;
                JumpPressed = false;
                UltimatePressed = false;
                FlyPressed = false;
                FlyDescendPressed = false;
                BoostPressed = false;
                GuardPressed = false;
                GuardPressedThisFrame = false;
                ViewTogglePressed = false;
                ZoomInPressed = false;
                ZoomOutPressed = false;
                WalkTogglePressed = false;
                return;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed) y += 1f;
            MoveInput = new Vector2(x, y);

            // 2026-08-31, user request ("把滑鼠右鍵改成武士刀防禦") - right mouse held raises the
            // katana guard (PlayerGuard reads this). Was AimPressed's binding while the shooting
            // system existed; that system is retired (RangedWeapon left inert, asset kept on disk).
            // Held, not wasPressedThisFrame - the block stays up for the whole hold.
            GuardPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;
            GuardPressedThisFrame = Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame;

            // Left-click is the melee swing, but not while the guard is up - you can't attack out
            // of a block without releasing it first. Same "gate the click on the other mouse
            // button's held state" shape this used to have against AimPressed.
            bool leftClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            AttackPressed = leftClicked && !GuardPressed;
            AttackHeld = Mouse.current != null && Mouse.current.leftButton.isPressed && !GuardPressed;

            // 2026-08-23, explicit user request ("V鍵切換成第一視角(機制與右鍵瞄準同理)") - V was
            // completely unused before this (checked the rest of this method).
            ViewTogglePressed = keyboard.vKey.wasPressedThisFrame;

            DodgePressed = keyboard.leftShiftKey.wasPressedThisFrame;
            // Changed from Q to the mouse wheel's click (middleButton is the scroll wheel
            // being pressed down, not scrolled - Input System has no separate "wheel press"
            // control distinct from middle-click).
            LockOnPressed = Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
            JumpPressed = keyboard.spaceKey.wasPressedThisFrame;
            // 2026-08-13, explicit user request - ultimate skill trigger (R key).
            UltimatePressed = keyboard.rKey.wasPressedThisFrame;

            // 2026-08-18, explicit user request (flight: "按住鍵自由飛行") - Left Ctrl to
            // ascend/enter flight, held Left Shift to descend while flying. isPressed (a level,
            // not an edge) since flight needs to keep responding for as long as the key stays
            // down. Reusing the same physical Shift key DodgePressed already uses is safe -
            // DodgePressed reads wasPressedThisFrame (fires once on press), this reads isPressed
            // (true for the whole hold), so a dodge-tap and a flight-descend-hold don't fight
            // over the same control, they're just two different questions asked of it.
            FlyPressed = keyboard.leftCtrlKey.isPressed;
            FlyDescendPressed = keyboard.leftShiftKey.isPressed;

            // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 3.4) - Q was
            // completely unused before this (checked the rest of this method) - held, not
            // wasPressedThisFrame, same reasoning as FlyPressed above (boost needs to keep
            // responding for the whole hold, not just the instant it's pressed).
            BoostPressed = keyboard.qKey.isPressed;

            // 2026-08-23, explicit user request ("改成 q/e") - originally bound to A (which
            // conflicted with strafe-left, see this comment's own prior revision), moved to Q/E.
            // Held, not wasPressedThisFrame, same reasoning as FlyPressed/BoostPressed above
            // (zoom needs to keep adjusting for the whole hold). ThirdPersonCameraController only
            // ever acts on these while actually aiming, so it's unaffected outside that state -
            // but ZoomInPressed now reuses the same physical Q key BoostPressed already reads
            // just above: flight boost only matters while flying, aim-zoom only matters while
            // aiming, and this project's flight and aim/first-person systems were never designed
            // to be active at the same time, so holding Q reads as "boost" during flight and
            // "zoom in" during aim without the two ever actually fighting over what it means. E
            // remains unused anywhere else.
            ZoomInPressed = keyboard.qKey.isPressed;
            ZoomOutPressed = keyboard.eKey.isPressed;

            // 2026-08-30, explicit user request ("設計像原神那樣的 切換式 跑步/慢走 沉浸式體驗") -
            // Left Alt was completely unused before this (checked the rest of this method). Edge
            // trigger (wasPressedThisFrame), like ViewTogglePressed - one tap flips the persistent
            // walk/run mode in CharacterMovement, it doesn't need to stay held.
            WalkTogglePressed = keyboard.leftAltKey.wasPressedThisFrame;
        }
    }
}
