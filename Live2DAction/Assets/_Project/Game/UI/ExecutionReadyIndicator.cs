using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Combat;

namespace Live2DAction.UI
{
    // Execution-ready indicator (2026-08-18, explicit user request: "架勢條滿格時，角色的胸口上出現
    // 紅色圓圈 圓圈內有個紅色小點在閃爍，整體視覺幫我用最專業的精緻外觀來設計"). Purely a visual
    // "this target can be executed right now" cue - appears ONLY while StancePoise.IsStaggered,
    // distinct from WorldSpaceStanceBar itself (that bar tracks stance BUILDING toward full; this
    // only cares about the single moment it's already full and staying that way, matching the
    // exact window ExecutionAbility/EnemyExecutionAbility can actually land a deathblow in). Its
    // own small component rather than folded into WorldSpaceStanceBar - same "small dedicated
    // component over one growing one" convention this codebase already follows for
    // StaggerAnimationLink being separate from StancePoise.
    //
    // Ring/dot stay invisible via alpha=0 rather than SetActive(false) - mirrors
    // WorldSpaceStanceBar/WorldSpaceEnergyBar's own "always-active Canvas, drive fill/alpha every
    // frame" pattern, so this component keeps running (and re-evaluating IsStaggered) without
    // needing a separate always-on parent to toggle it back on.
    public class ExecutionReadyIndicator : MonoBehaviour
    {
        [SerializeField] private StancePoise stance;
        [SerializeField] private Image ringImage;
        [SerializeField] private Image dotImage;

        // Slow breathing glow on the ring.
        [SerializeField] private float ringPulseSpeed = 2.5f;
        [SerializeField] private float ringMinAlpha = 0.75f;
        [SerializeField] private float ringMaxAlpha = 1f;

        // Sharper on/off blink on the inner dot - see ExecutionIndicatorPulseUtility's own
        // comment for why this needs a different wave shape than the ring's.
        [SerializeField] private float dotBlinkSpeed = 7f;
        [SerializeField] private float dotMinAlpha = 0.15f;
        [SerializeField] private float dotMaxAlpha = 1f;

        private Color _ringBaseColor;
        private Color _dotBaseColor;
        private bool _colorsCaptured;

        private void Update()
        {
            if (stance == null || ringImage == null || dotImage == null)
            {
                return;
            }

            // Captured lazily rather than in Awake - same reasoning as WorldSpaceStanceBar's own
            // _baseFillColor, keeps whatever tint was authored on the Image in the Inspector
            // instead of hard-baking a color here.
            if (!_colorsCaptured)
            {
                _ringBaseColor = ringImage.color;
                _dotBaseColor = dotImage.color;
                _colorsCaptured = true;
            }

            bool ready = stance.IsStaggered;
            float ringAlpha = ready
                ? ExecutionIndicatorPulseUtility.ComputeRingAlpha(Time.time, ringPulseSpeed, ringMinAlpha, ringMaxAlpha)
                : 0f;
            float dotAlpha = ready
                ? ExecutionIndicatorPulseUtility.ComputeDotBlinkAlpha(Time.time, dotBlinkSpeed, dotMinAlpha, dotMaxAlpha)
                : 0f;

            ringImage.color = new Color(_ringBaseColor.r, _ringBaseColor.g, _ringBaseColor.b, _ringBaseColor.a * ringAlpha);
            dotImage.color = new Color(_dotBaseColor.r, _dotBaseColor.g, _dotBaseColor.b, _dotBaseColor.a * dotAlpha);
        }

        // Same billboard pattern as WorldSpaceStanceBar/WorldSpaceEnergyBar's own LateUpdate -
        // always faces the camera regardless of current alpha, cheap enough not to bother gating
        // on `ready`.
        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
