using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // spec WUSHI_COMBAT_ENGINEERING_SPEC.md §6.2 (M3 項目 5, sub-step 5A - 程式化攻擊位移).
    //
    // The 武士 runs at scale 4 with applyRootMotion off, and its Meshy attack clips bake forward
    // travel into the hips - so on a lunging attack (ChargeCut, DoubleCombo's second beat) the
    // VISIBLE blade/body slides several metres forward while the gameplay root stays planted, and
    // the hit ends up landing well ahead of where the boss "is". 5A closes that gap in code, before
    // the proper fix (5B: rescale to 1 + re-import the clips without the baked drift) - the boss's
    // CharacterController is walked forward along a locked commit direction on a normalized-time
    // curve, so the gameplay root tracks the visual lunge.
    //
    // forwardDistance 0 (the default) = no displacement = every existing attack behaves exactly as
    // before. Only ChargeCut / DoubleCombo opt in.
    [System.Serializable]
    public sealed class BossAttackMotionProfile
    {
        [Tooltip("Normalized clip time the forward lunge begins.")]
        [Range(0f, 1f)] public float moveStartNormalized = 0.1f;

        [Tooltip("Normalized clip time the lunge is fully spent.")]
        [Range(0f, 1f)] public float moveEndNormalized = 0.5f;

        [Tooltip("Total world metres the gameplay root travels along the commit direction over the " +
                 "window. 0 = this attack has no programmatic displacement (the default).")]
        public float forwardDistance = 0f;

        [Tooltip("0->1 shape of the lunge across the window (x = window progress, y = fraction of " +
                 "forwardDistance covered). Linear if left empty.")]
        public AnimationCurve movementCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Tooltip("If true, a Recoil-reaction perfect parry (spec item 1) freezes the remaining lunge " +
                 "in place instead of letting it slide through.")]
        public bool stopOnDeflectRecoil = true;

        public bool HasDisplacement => forwardDistance > 0.0001f;

        // Fraction of forwardDistance that should have been travelled by this normalized clip time.
        public float TravelFraction01(float normalized)
        {
            if (normalized <= moveStartNormalized) return 0f;
            if (normalized >= moveEndNormalized) return 1f;
            float span = Mathf.Max(1e-4f, moveEndNormalized - moveStartNormalized);
            float windowProgress = Mathf.Clamp01((normalized - moveStartNormalized) / span);
            float curved = movementCurve != null && movementCurve.length > 0
                ? movementCurve.Evaluate(windowProgress)
                : windowProgress;
            return Mathf.Clamp01(curved);
        }
    }
}
