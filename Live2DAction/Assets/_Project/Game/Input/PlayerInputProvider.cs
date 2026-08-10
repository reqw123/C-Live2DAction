using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Input
{
    public class PlayerInputProvider : MonoBehaviour, IInputCommand
    {
        public Vector2 MoveInput { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool DodgePressed { get; private set; }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                MoveInput = Vector2.zero;
                AttackPressed = false;
                DodgePressed = false;
                return;
            }

            float x = 0f;
            float y = 0f;
            if (keyboard.aKey.isPressed) x -= 1f;
            if (keyboard.dKey.isPressed) x += 1f;
            if (keyboard.sKey.isPressed) y -= 1f;
            if (keyboard.wKey.isPressed) y += 1f;
            MoveInput = new Vector2(x, y);

            bool attackKey = keyboard.spaceKey.wasPressedThisFrame;
            bool attackMouse = Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
            AttackPressed = attackKey || attackMouse;

            DodgePressed = keyboard.leftShiftKey.wasPressedThisFrame;
        }
    }
}
