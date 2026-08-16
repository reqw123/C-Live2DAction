using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.DebugTools
{
    // Lets the user interactively nudge the Genshin sword display's height and overall scale
    // while in Play mode (2026-08-13, explicit user request) - a stand-in for the visual
    // verification this environment can't do (see GenshinSwordDisplaySetup's own comment on
    // why its placement/scale numbers were never confirmed by eye, only derived from
    // measured bounds). Dev-only tuning helper, not real gameplay input - Z/X/C/V were
    // picked because none of them collide with PlayerInputProvider's own WASD/Space/
    // Shift/mouse bindings.
    //
    // Z/X move the whole display straight up/down (Y only, world space); C/V scale it up/
    // down uniformly. All four are held-key continuous adjustments (same isPressed-every-
    // frame convention as PlayerInputProvider's own WASD movement), not single-press
    // triggers - hold the key down to keep moving/scaling, release to stop.
    public class SwordDisplayAdjuster : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1f; // world units per second
        [SerializeField] private float scaleSpeed = 0.5f; // multiplicative factor per second
        [SerializeField] private float minScale = 0.01f; // floor so V can't shrink it to zero/negative

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float dt = Time.deltaTime;

            if (keyboard.zKey.isPressed)
            {
                transform.position += Vector3.up * (moveSpeed * dt);
            }

            if (keyboard.xKey.isPressed)
            {
                transform.position -= Vector3.up * (moveSpeed * dt);
            }

            if (keyboard.cKey.isPressed)
            {
                ApplyScale(1f + scaleSpeed * dt);
            }

            if (keyboard.vKey.isPressed)
            {
                ApplyScale(1f - scaleSpeed * dt);
            }
        }

        // Multiplicative (not additive) so the same held-key feels equally responsive whether
        // the display is currently tiny or huge, matching how C/V are framed as "scale up/
        // down" rather than "add/subtract a fixed amount". Re-uniforms via .x every frame so
        // floating-point drift can't slowly skew X/Y/Z apart from each other.
        private void ApplyScale(float factor)
        {
            float uniform = Mathf.Max(transform.localScale.x * factor, minScale);
            transform.localScale = Vector3.one * uniform;
        }
    }
}
