using UnityEngine;

namespace Live2DAction.Combat
{
    // Pure function turning frame-data phase timing into a procedural swing angle, used by
    // AttackPoseVisualizer as a placeholder attack pose for characters with no authored
    // attack animation clips yet (Maya's Animator only has Idle/Walk/Run/Jump/Fall; the
    // enemy is an unrigged capsule - see Docs/DEVELOPMENT_ROADMAP.md Phase 2 Step 2 known
    // limitations). Kept separate from any MonoBehaviour, matching AttackResolver/
    // ComboAttackState's pure-logic-first pattern, so the shape of the swing (wind up during
    // Startup, snap through during Active, ease back during Recovery) is EditMode-testable.
    public static class AttackPoseUtility
    {
        public static float ComputeSwingAngle(AttackPhase phase, float phaseProgress, float windUpAngleDegrees, float swingAngleDegrees)
        {
            float t = Mathf.Clamp01(phaseProgress);
            switch (phase)
            {
                case AttackPhase.Startup:
                    return Mathf.Lerp(0f, -windUpAngleDegrees, t);
                case AttackPhase.Active:
                    return Mathf.Lerp(-windUpAngleDegrees, swingAngleDegrees, t);
                case AttackPhase.Recovery:
                    return Mathf.Lerp(swingAngleDegrees, 0f, t);
                default:
                    return 0f;
            }
        }
    }
}
