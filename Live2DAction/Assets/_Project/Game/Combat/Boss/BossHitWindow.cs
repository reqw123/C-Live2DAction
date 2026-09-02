using UnityEngine;

namespace Live2DAction.Combat.Boss
{
    // (DeflectReaction lives in Live2DAction.Combat/BladeClash.cs - same assembly.)

    // One visible strike within an attack clip - e.g. Punch_Combo has 3 of these (one per punch
    // that actually connects), never a single window spanning the whole animation. Times are
    // normalized (0-1 against the owning AnimationClip's own length) rather than raw frames, so
    // they stay correct if a clip's import frame range is ever re-sliced.
    //
    // startNormalized/endNormalized are measured, not guessed - see BossAttackDefinition's own
    // "needsHumanConfirmation" field for how uncertain measurements are flagged rather than
    // silently treated as exact.
    [System.Serializable]
    public class BossHitWindow
    {
        [Tooltip("Which physical hitbox this window enables (see BossHitbox).")]
        public BossHitboxPart part = BossHitboxPart.RightHand;

        [Range(0f, 1f)] public float startNormalized;
        [Range(0f, 1f)] public float endNormalized;

        [Tooltip("Multiplies the attack's own healthDamage/poiseDamage for THIS window only - " +
                 "lets a combo's later hits weigh more without duplicating a whole AttackData.")]
        public float damageMultiplier = 1f;

        [Tooltip("2026-09-01 (spec item 1): what a perfect PARRY of THIS window does to the boss. " +
                 "Recoil (default, = every existing window) interrupts the swing into a short " +
                 "HitReaction. ContinueCombo still lands the parry's posture damage + spark/SFX but " +
                 "lets this attack's LATER windows keep playing - so parrying a combo's first hit " +
                 "doesn't cancel the whole combo. CancelAttack drops the attack entirely. A parry " +
                 "that maxes the boss's posture always wins over this with PostureBroken.")]
        public DeflectReaction deflectReaction = DeflectReaction.Recoil;

        [Tooltip("True only for a window measured directly against the real clip (bone position " +
                 "sampled at candidate contact frames). False means the timing is a first-pass " +
                 "estimate from clip proportions and still needs a human to confirm the real " +
                 "contact frame in the Animation window before shipping.")]
        public bool measured;

        // Stable identity for this window within its owning AttackDefinition - BossHitbox uses
        // (attackDefinition, windowIndex) as the key for its per-target "already hit this
        // window" set, NOT Unity's own instance ID (which isn't stable across domain reloads).
        public int WindowIndex(BossAttackDefinition owner)
        {
            return System.Array.IndexOf(owner.HitWindows, this);
        }
    }
}
