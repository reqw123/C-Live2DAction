using UnityEngine;

namespace Live2DAction.CameraSystem
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4.2). A decaying positional
    // shake added on top of whatever ThirdPersonCameraController positioned the camera at this
    // frame. Execution order 100 = AFTER the camera controller's LateUpdate, and it only offsets
    // the transform for this frame (the controller repositions from scratch next frame), so the
    // shake never accumulates. Uses unscaledDeltaTime so it still animates during a hitstop dip.
    //
    // Wired on both Main Camera and CatCamera by the setup, but CatCombatFeedback only pokes the
    // CatCamera one - the player camera's stays dormant (Shake never called on it).
    [DefaultExecutionOrder(100)]
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private float frequency = 26f;

        private float _amplitude;
        private float _duration;
        private float _timer;

        public void Shake(float amplitude, float seconds)
        {
            if (amplitude <= 0f || seconds <= 0f)
            {
                return;
            }
            _amplitude = Mathf.Max(_amplitude, amplitude);
            _duration = Mathf.Max(_duration, seconds);
            _timer = _duration;
        }

        private void LateUpdate()
        {
            if (_timer <= 0f)
            {
                return;
            }
            _timer -= Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_timer / Mathf.Max(0.001f, _duration));
            float n = _amplitude * k * k; // ease-out (squared) so the tail is gentle

            float t = Time.unscaledTime * frequency;
            float x = (Mathf.PerlinNoise(t, 0.37f) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(0.71f, t) - 0.5f) * 2f;
            transform.position += transform.right * (x * n) + transform.up * (y * n);

            if (_timer <= 0f)
            {
                _amplitude = 0f;
                _duration = 0f;
            }
        }
    }
}
