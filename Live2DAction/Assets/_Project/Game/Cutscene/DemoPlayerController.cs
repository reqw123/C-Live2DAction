using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Cutscene
{
    // 2026-09-01, /grill-with-docs exploration — see Docs/BOSS_INTRO_EXPLORATION.md. NOT in the
    // shipped game. A throwaway WASD walker so you can stroll into the BossRoomTrigger in the demo
    // scene. BossIntroManager disables THIS Behaviour for the cutscene (it stands in for the real
    // project's CharacterMovement + PlayerCombat + ... in the disable list).
    //
    // Reads the new Input System directly (Keyboard.current), same as the real PlayerInputProvider -
    // the project's active input handling is the Input System package, not the legacy Input Manager.
    [RequireComponent(typeof(CharacterController))]
    public class DemoPlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 3.5f;
        [SerializeField] private float turnSpeed = 540f;
        [SerializeField] private float gravity = -20f;

        private CharacterController _cc;
        private float _vy;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
        }

        private void Update()
        {
            Keyboard k = Keyboard.current;
            float x = 0f, z = 0f;
            if (k != null)
            {
                if (k.dKey.isPressed) x += 1f;
                if (k.aKey.isPressed) x -= 1f;
                if (k.wKey.isPressed) z += 1f;
                if (k.sKey.isPressed) z -= 1f;
            }
            Vector3 dir = new Vector3(x, 0f, z);
            if (dir.sqrMagnitude > 1f)
            {
                dir.Normalize();
            }

            if (_cc.isGrounded && _vy < 0f)
            {
                _vy = -1f;
            }
            _vy += gravity * Time.deltaTime;

            Vector3 motion = dir * moveSpeed;
            motion.y = _vy;
            _cc.Move(motion * Time.deltaTime);

            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion want = Quaternion.LookRotation(new Vector3(dir.x, 0f, dir.z), Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, want, turnSpeed * Time.deltaTime);
            }
        }
    }
}
