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

        // 2026-08-19, real playtested bug ("蹲下時準確地落在胸口 所有角色都一樣") - wired by
        // ExecutionIndicatorSetup to the character's own Animator Chest bone (null for anything
        // without a Humanoid Animator, e.g. Player2/機甲戰士 - see that setup script's own
        // fallback comment). Needed because a plain BONE-LOCAL forward offset (the first attempt
        // at this fix) breaks down on an extreme pose: this project's Staggered/kneel clip
        // (KneelingDown.fbx) folds the whole spine forward so far that the Chest bone's own local
        // "forward" axis ends up pointing DOWN toward the thighs instead of out toward the camera
        // (screenshot-verified: it landed the ring on the knee, not the chest). Recomputing the
        // offset direction toward the CAMERA every frame in LateUpdate below - rather than baking
        // a fixed local offset once - keeps it reading as "on the chest, facing the viewer" at any
        // bone rotation, standing or deep-kneeling alike (also screenshot-verified both ways).
        [SerializeField] private Transform positionAnchor;

        // How far off the bone's own pivot to push, straight toward the camera - just enough to
        // clear the body mesh's surface without visibly detaching from it. Tuned by eye at
        // 0.15 against this project's character scale (see positionAnchor's own comment for the
        // screenshot process) - the OLD root-relative fallback path uses its own, larger
        // ExecutionIndicatorSetup.ChestForwardOffset instead, since that one has to clear an
        // entire body's worth of distance from the root pivot rather than just a bone's local
        // skin surface.
        [SerializeField] private float chestSurfaceOffset = 0.15f;

        // 2026-08-19, explicit user request ("心臟 人的核心部位，你要做出那種要被處決的恐懼感，
        // 紅色 鮮豔 脈動") - replaced the old independent sine "breathing" ring / power-curve
        // "blinking" dot (two DIFFERENT rhythms that happened to both loop) with one shared
        // heartbeat rhythm driving both (see ExecutionIndicatorPulseUtility.ComputeHeartbeatAlpha/
        // Scale) - the ring and dot now visibly beat in lockstep, like the target's own heart is
        // racing under the reticle. 110 BPM (versus a calm resting ~70) reads as adrenaline/panic
        // rather than a relaxed pulse - this is meant to unsettle, not soothe.
        [SerializeField] private float heartbeatBPM = 110f;

        [SerializeField] private float ringMinAlpha = 0.55f;
        [SerializeField] private float ringMaxAlpha = 1f;
        // Subtle size throb on the ring - swells slightly on each beat rather than staying a
        // static circle, so the dread reads in silhouette too, not just brightness.
        [SerializeField] private float ringMinScale = 0.94f;
        [SerializeField] private float ringMaxScale = 1.08f;

        [SerializeField] private float dotMinAlpha = 0.2f;
        [SerializeField] private float dotMaxAlpha = 1f;
        // The dot throbs harder than the ring (bigger scale swing) - it's standing in for the
        // heart itself at the center of the reticle, the ring is just the targeting frame around
        // it.
        [SerializeField] private float dotMinScale = 0.8f;
        [SerializeField] private float dotMaxScale = 1.35f;

        // 2026-08-18, real playtested bug ("目前看不到紅圈提示") - this used to capture
        // ringImage.color/dotImage.color THE FIRST TIME Update() ran and treat that as a "100%
        // visible" reference to multiply the pulse against. That's fragile in a way that bit
        // twice: whatever alpha happens to be on the Image at that exact moment becomes the
        // permanent ceiling - the first time (setup script bug, long fixed) it was because the
        // Image started pre-zeroed; THIS time it was because the scene got saved mid-session
        // while not staggered (alpha correctly 0 at save time) - reopening the scene resets
        // _colorsCaptured, so the NEXT Update() recaptured that saved 0 as the new "base", and
        // multiplying by 0 stays 0 forever regardless of how "ready" is computed afterward. The
        // sprite's own texture already bakes in the red/glow color (see CreateCenteredImage's
        // own comment - the Image tint is always meant to be identity white), so there was never
        // a real "base color" worth preserving here - removed the capture-and-multiply pattern
        // entirely and just write RGB=white with alpha set DIRECTLY from the pulse functions
        // (which already return the intended final alpha, not a 0-1 multiplier).
        private void Update()
        {
            if (stance == null || ringImage == null || dotImage == null)
            {
                return;
            }

            bool ready = stance.IsStaggered;
            float ringAlpha = ready
                ? ExecutionIndicatorPulseUtility.ComputeHeartbeatAlpha(Time.time, heartbeatBPM, ringMinAlpha, ringMaxAlpha)
                : 0f;
            float dotAlpha = ready
                ? ExecutionIndicatorPulseUtility.ComputeHeartbeatAlpha(Time.time, heartbeatBPM, dotMinAlpha, dotMaxAlpha)
                : 0f;

            ringImage.color = new Color(1f, 1f, 1f, ringAlpha);
            dotImage.color = new Color(1f, 1f, 1f, dotAlpha);

            // Scale throb stays live (not gated on `ready`) same as the billboard rotation below -
            // harmless at alpha 0, and avoids a visible size "pop" back to 1x the instant IsStaggered
            // flips true.
            float ringScale = ExecutionIndicatorPulseUtility.ComputeHeartbeatScale(Time.time, heartbeatBPM, ringMinScale, ringMaxScale);
            float dotScale = ExecutionIndicatorPulseUtility.ComputeHeartbeatScale(Time.time, heartbeatBPM, dotMinScale, dotMaxScale);
            ringImage.rectTransform.localScale = new Vector3(ringScale, ringScale, 1f);
            dotImage.rectTransform.localScale = new Vector3(dotScale, dotScale, 1f);
        }

        // Same billboard pattern as WorldSpaceStanceBar/WorldSpaceEnergyBar's own LateUpdate -
        // always faces the camera regardless of current alpha, cheap enough not to bother gating
        // on `ready`. Runs AFTER the Animator has already evaluated this frame's pose (Unity's
        // execution order guarantees LateUpdate follows Update-phase animation), so reading
        // positionAnchor.position here always reflects the CURRENT pose, kneeling included.
        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            if (positionAnchor != null)
            {
                Vector3 towardCamera = (mainCamera.transform.position - positionAnchor.position).normalized;
                transform.position = positionAnchor.position + towardCamera * chestSurfaceOffset;
            }

            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
