using UnityEngine;
using UnityEngine.UI;

namespace Live2DAction.World
{
    // Genshin-style "you've reached an unopened area boundary" screen cue, explicit user request
    // ("能不能在任何角色碰撞此牆時設計一個被阻擋的特效，像是原神玩家不小心走到的未開放地圖邊界一樣")
    // - a soft translucent vignette that pulses in from the screen edges and fades back out.
    // Deliberately player-only (unlike BoundaryBlockEffect's world-space ripple, which fires for
    // ANY character and is visible to an observing camera regardless of who touched the wall) -
    // a screen-space overlay only means anything for whoever is actually looking through the
    // camera.
    //
    // Single persistent Screen Space - Overlay Image, same convention as
    // SkyIslandTimeTrialSetup's own HUD (CreateStatusUi) - a screen cue needs to stay readable
    // regardless of where the 3D camera is looking, unlike this project's other (world-space,
    // per-character) UI effects.
    public class BoundaryBlockHud : MonoBehaviour
    {
        [SerializeField] private Image vignetteImage;
        [SerializeField] private float pulseInSeconds = 0.15f;
        [SerializeField] private float holdSeconds = 0.25f;
        [SerializeField] private float pulseOutSeconds = 0.5f;
        [SerializeField] private float maxAlpha = 0.55f;

        // Only one instance ever exists in this scene (created once by
        // BoundaryWallBlockEffectSetup) - every BoundaryBlockEffect on every wall shares it
        // rather than each wall owning its own screen-space Canvas.
        public static BoundaryBlockHud Instance { get; private set; }

        private float _pulseTimer = -1f;
        private float _totalDuration;

        private void Awake()
        {
            Instance = this;
            _totalDuration = pulseInSeconds + holdSeconds + pulseOutSeconds;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void Pulse()
        {
            _pulseTimer = 0f;
        }

        private void Update()
        {
            if (vignetteImage == null || _pulseTimer < 0f)
            {
                return;
            }

            _pulseTimer += Time.deltaTime;

            float alpha;
            if (_pulseTimer < pulseInSeconds)
            {
                alpha = Mathf.Lerp(0f, maxAlpha, _pulseTimer / pulseInSeconds);
            }
            else if (_pulseTimer < pulseInSeconds + holdSeconds)
            {
                alpha = maxAlpha;
            }
            else if (_pulseTimer < _totalDuration)
            {
                float t = (_pulseTimer - pulseInSeconds - holdSeconds) / pulseOutSeconds;
                alpha = Mathf.Lerp(maxAlpha, 0f, t);
            }
            else
            {
                alpha = 0f;
                _pulseTimer = -1f; // done - stop ticking until the next Pulse()
            }

            Color c = vignetteImage.color;
            c.a = alpha;
            vignetteImage.color = c;
        }
    }
}
