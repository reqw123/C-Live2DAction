using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Input
{
    public class PlayerInputProvider : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool LockOnPressed { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool UltimatePressed { get; private set; }
        public bool FlyPressed { get; private set; }
        public bool FlyDescendPressed { get; private set; }
        public bool BoostPressed { get; private set; }
        public bool AimPressed { get; private set; }
        public bool FirePressed { get; private set; }
        public bool ViewTogglePressed { get; private set; }
        public bool ZoomInPressed { get; private set; }
        public bool ZoomOutPressed { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                MoveInput = Vector2.zero;
                AttackPressed = false;
                DodgePressed = false;
                LockOnPressed = false;
                JumpPressed = false;
                UltimatePressed = false;
                FlyPressed = false;
                FlyDescendPressed = false;
                BoostPressed = false;
                AimPressed = false;
                FirePressed = false;
                ViewTogglePressed = false;
                ZoomInPressed = false;
                ZoomOutPressed = false;
                return;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed) y += 1f;
            MoveInput = new Vector2(x, y);

            // 2026-08-23, explicit user request ("加入瞄準-射擊機制 模擬槍戰") - right mouse button
            // was completely unused before this (checked the rest of this method). Held, not
            // wasPressedThisFrame, same reasoning as FlyPressed/BoostPressed below - aiming needs
            // to stay active for the whole hold, not just the instant it's pressed.
            AimPressed = Mouse.current != null && Mouse.current.rightButton.isPressed;

            // Left-click is shared between melee and the ranged weapon - which one it means
            // depends on whether AimPressed is already held this same frame, so left-click still
            // does exactly what it always did (melee attack) whenever the player ISN'T aiming,
            // and only fires the gun while they are. Computed from AimPressed above rather than
            // each being a fully independent read of the mouse, so the two can never both fire
            // off the same physical click.
            bool leftClicked = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            AttackPressed = leftClicked && !AimPressed;
            FirePressed = leftClicked && AimPressed;

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
        }
    }
}
