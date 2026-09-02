using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-29, cat combat design (Docs/CAT_COMBAT_DESIGN.md 4.1). A short global time-scale
    // dip on a landed hit ("hitstop" / freeze-frame). Scene-single; does nothing until Request
    // is called, so a scene with this component but no caller behaves identically to before.
    //
    // SCOPE: this is a generic controller, but the ONLY caller wired in this project is
    // CatCombatFeedback, which calls Request only while the cat is possessed and calls
    // CancelAndRestore the instant possession switches back to the player. So in practice
    // hitstop is a cat-possession-only effect and the player's / boss's Time.timeScale is never
    // touched. Timer runs on unscaledDeltaTime so the dip is a real wall-clock duration.
    public class HitStopController : MonoBehaviour
    {
        public static HitStopController Instance { get; private set; }

        [SerializeField] private float defaultScale = 0.05f;
        [SerializeField] private float defaultSeconds = 0.06f;

        private float _timer;
        private float _restoreTo = 1f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                RestoreNow();
                Instance = null;
            }
        }

        public static void Request(float seconds = -1f, float scale = -1f)
        {
            if (Instance != null)
            {
                Instance.Begin(seconds, scale);
            }
        }

        public static void CancelAndRestore()
        {
            if (Instance != null)
            {
                Instance.RestoreNow();
            }
        }

        private void Begin(float seconds, float scale)
        {
            if (_timer <= 0f)
            {
                // Capture whatever the timescale was BEFORE we dip it (normally 1), so we
                // restore to that, not to a hard-coded 1 - chained hits keep the first capture.
                _restoreTo = Time.timeScale > 0f ? Time.timeScale : 1f;
            }
            _timer = Mathf.Max(_timer, seconds > 0f ? seconds : defaultSeconds);
            Time.timeScale = scale > 0f ? scale : defaultScale;
        }

        private void Update()
        {
            if (_timer <= 0f)
            {
                return;
            }
            _timer -= Time.unscaledDeltaTime;
            if (_timer <= 0f)
            {
                RestoreNow();
            }
        }

        private void RestoreNow()
        {
            _timer = 0f;
            Time.timeScale = _restoreTo > 0f ? _restoreTo : 1f;
        }
    }
}
