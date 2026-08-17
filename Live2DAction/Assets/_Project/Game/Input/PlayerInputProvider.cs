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
                return;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed) y += 1f;
            MoveInput = new Vector2(x, y);

            // Space used to double as an attack button alongside left-click; freed up for jump
            // (attack is unaffected - left-click still works on its own).
            AttackPressed = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

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
        }
    }
}
