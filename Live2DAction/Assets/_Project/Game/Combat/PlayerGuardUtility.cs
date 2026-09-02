using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-08-31, user request ("把滑鼠右鍵改成武士刀防禦"). Pure functions behind PlayerGuard so
    // the block geometry / mitigation math is unit-testable with no MonoBehaviour, no scene, no
    // Animator - same "extract the rules, test them in EditMode" split TenLeggedBugGaitUtility /
    // AttackPoseUtility / CharacterMovement.NextWalkMode already use.
    public static class PlayerGuardUtility
    {
        // DamageInfo.Direction points "away from the attacker" (target - attacker, flattened - see
        // AttackResolver). So the attacker sits along -damageDirection. A block connects when that
        // incoming direction lies within guardArcDegrees (full cone, not half) of where the
        // defender is facing.
        public static bool IsFrontalBlock(Vector3 guardForwardFlat, Vector3 damageDirectionFlat, float guardArcDegrees)
        {
            guardForwardFlat.y = 0f;
            damageDirectionFlat.y = 0f;
            if (guardForwardFlat.sqrMagnitude < 1e-6f || damageDirectionFlat.sqrMagnitude < 1e-6f)
            {
                return false;
            }

            Vector3 towardAttacker = -damageDirectionFlat.normalized;
            float angle = Vector3.Angle(guardForwardFlat.normalized, towardAttacker);
            return angle <= Mathf.Max(0f, guardArcDegrees) * 0.5f;
        }

        // Health damage that actually gets through a successful block.
        public static float MitigatedAmount(float incomingAmount, float blockedDamageMultiplier)
        {
            return Mathf.Max(0f, incomingAmount) * Mathf.Clamp01(blockedDamageMultiplier);
        }

        // Poise/stance gain a blocked hit should still deliver - "in full", i.e. the same as if the
        // hit had NOT been blocked (mirrors the boss Boxing_Guard precedent: guarding cuts HP
        // damage only, posture keeps building so a turtling player still gets stagger-broken).
        // poiseMultiplier is kept in step with StancePoise.stanceGainMultiplier by hand - the two
        // are coupled by design, exactly like that field's own comment already warns for damage numbers.
        public static float FullPoiseAmount(float incomingAmount, float poiseMultiplier)
        {
            return Mathf.Max(0f, incomingAmount) * Mathf.Max(0f, poiseMultiplier);
        }

        // 2026-09-01, Wushi combat engineering spec item 6 (一般格擋使用每招 PoiseDamage). A plain
        // guard now costs the ATTACK's own poise damage, not a flat number - so guarding a heavy
        // SwordJudgment (poise 22) pressures the player's stance far more than a ChargeCut (12).
        // attackPoiseDamage is the per-hit value the attacker already computed (BossHitbox hands it
        // over in BladeClashInfo.PoiseDamage / DamageInfo.ExplicitPoiseAmount); fallbackPoiseDamage
        // is only used when that's missing/zero (a clash with no attack data behind it).
        public static float GuardPoiseGain(float attackPoiseDamage, float guardPoiseMultiplier, float fallbackPoiseDamage)
        {
            float basePoise = attackPoiseDamage > 0f ? attackPoiseDamage : Mathf.Max(0f, fallbackPoiseDamage);
            return basePoise * Mathf.Max(0f, guardPoiseMultiplier);
        }

        // 2026-09-01, Wushi combat engineering spec item 2 (Tap Guard / GuardVolume / Animator
        // consistency). The spec's None / Guard / Parry chain, in ONE place - every reader
        // (GuardVolume, Animator link, movement slowdown, weapon pose, visual telegraph, debug)
        // must resolve its state through this, not by each re-combining IsBlocking + the tap window
        // (that mismatch is exactly what let a released tap leave an invisible-but-live guard).
        //   0 = None, 1 = Guard, 2 = Parry  (matches PlayerGuard.DefenseState)
        public static int DefenseStateCode(bool inParryWindow, bool defenseActionActive)
        {
            if (inParryWindow) return 2;
            return defenseActionActive ? 1 : 0;
        }

        // Eased 0..1 guard-pose weight. Frame-rate independent MoveTowards, same idiom as the
        // cat/bug procedural blends.
        public static float StepBlend(float current, float target, float blendSpeed, float deltaTime)
        {
            return Mathf.MoveTowards(current, Mathf.Clamp01(target), Mathf.Max(0f, blendSpeed) * Mathf.Max(0f, deltaTime));
        }
    }
}
