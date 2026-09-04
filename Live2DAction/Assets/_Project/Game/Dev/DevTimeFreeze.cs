// Dev-only freeze toggle - compiled into the Editor and Development builds, stripped from
// release builds (its GameObject in GreyboxTest then loads as a harmless missing-script slot).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Dev
{
    // 2026-09-01, user request ("提供一個按鍵讓畫面可以直接停止(模擬play mode stop)"). A dev-only
    // freeze toggle: the key flips Time.timeScale between its running value and 0, so the whole game
    // (physics, animation, movement, coroutines on scaled time) halts in place - the closest thing to
    // "Play Mode stop" you can get without leaving Play. Press again to resume.
    //
    // Reads the key directly via the new Input System (same pattern as ExecutionAbility's F key) so it
    // doesn't need a binding in the shared IInputCommand. The file-level #if keeps it out of release
    // builds so `\`` can't freeze a shipped game.
    public class DevTimeFreeze : MonoBehaviour
    {
        // Backquote (` , the key left of 1 / above Tab) - the classic dev-console key. Backspace is
        // the vehicle flip/reset (VehicleController), Space is jump, F9 is the deflect debug overlay.
        [Tooltip("Key that toggles the freeze. Backquote (`) by default.")]
        [SerializeField] private Key toggleKey = Key.Backquote;

        [Tooltip("Show a 'PAUSED' banner while frozen.")]
        [SerializeField] private bool showBanner = true;

        private bool _frozen;
        private float _resumeTimeScale = 1f;

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null || !kb[toggleKey].wasPressedThisFrame)
            {
                return;
            }

            if (_frozen)
            {
                Time.timeScale = _resumeTimeScale;
                _frozen = false;
            }
            else
            {
                _resumeTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                _frozen = true;
            }
        }

        private void OnDisable()
        {
            // Never leave the game frozen because this component got disabled / the scene unloaded.
            if (_frozen)
            {
                Time.timeScale = _resumeTimeScale;
                _frozen = false;
            }
        }

        private void OnGUI()
        {
            if (!_frozen || !showBanner)
            {
                return;
            }
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 22,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            style.normal.textColor = Color.white;
            const float w = 280f, h = 54f;
            GUI.color = new Color(0f, 0f, 0f, 0.75f);
            string keyLabel = toggleKey == Key.Backquote ? "`" : toggleKey.ToString();
            GUI.Box(new Rect((Screen.width - w) * 0.5f, 24f, w, h), $"PAUSED  —  {keyLabel} to resume", style);
            GUI.color = Color.white;
        }
    }
}
#endif
