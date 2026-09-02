using UnityEngine;

namespace Live2DAction.Combat
{
    // 2026-09-01, user request — Sekiro-style deflect (「玩家防禦、一般格擋、完美彈反」). Shared
    // vocabulary for the blade-clash path, kept generic (no PlayerGuard / BossHitbox types here) so
    // either side can be swapped later.
    //
    // Flow: a swept boss weapon hitbox, on the FIRST intersection along its sweep with a guard
    // volume, calls IBladeClashReceiver.TryResolveClash. The receiver reads its OWN state at that
    // instant (guarding? frontal? within the parry window?) and returns the outcome. The attacker
    // then applies the boss-side consequence (posture damage / recoil on Parried). If the receiver
    // returns None the attacker treats the guard volume as absent and continues resolving its sweep
    // against the body hurtbox.

    public enum BladeClashResult
    {
        None,     // not actually a valid block (guard down / not frontal) - fall through to the body
        Guarded,  // in the guard window but past the parry window
        Parried,  // within the parry window - perfect deflect
    }

    // 2026-09-01, Wushi combat engineering spec item 1 (彈反反應與 Boss 連段控制). What a perfect
    // PARRY does to the attacker's current move - decided PER HIT WINDOW (BossHitWindow.deflectReaction),
    // not "every parry always interrupts". A parry ALWAYS lands its posture damage + spark/SFX/hitstop
    // regardless of this; this only controls whether the swing is interrupted.
    //
    // Ordered so the ZERO value is Recoil - the pre-spec behaviour (every parry -> HitReaction) - so
    // every existing BossHitWindow that has no deflectReaction serialized keeps behaving exactly as
    // before with no asset migration.
    public enum DeflectReaction
    {
        Recoil = 0,        // parry interrupts the swing into a short HitReaction (the old always-on behaviour)
        ContinueCombo = 1, // parry lands posture + feedback, but this attack's LATER hit windows still play
        CancelAttack = 2,  // parry fully drops the attack back to a safe recovery
    }

    public readonly struct BladeClashInfo
    {
        public readonly GameObject Attacker;      // the boss GameObject (DamageInfo.Source equivalent)
        public readonly float HealthDamage;       // what a clean body hit would have dealt
        public readonly float PoiseDamage;        // ditto, poise
        public readonly Vector3 ContactPoint;     // where the sweep first met the guard volume (world)
        public readonly Vector3 AttackDirectionFlat; // horizontal, pointing AWAY from the attacker (DamageInfo.Direction convention)
        public readonly DeflectReaction Reaction; // what a PARRY of this window does to the attacker's move

        public BladeClashInfo(GameObject attacker, float healthDamage, float poiseDamage,
            Vector3 contactPoint, Vector3 attackDirectionFlat,
            DeflectReaction reaction = DeflectReaction.Recoil)
        {
            Attacker = attacker;
            HealthDamage = healthDamage;
            PoiseDamage = poiseDamage;
            ContactPoint = contactPoint;
            AttackDirectionFlat = attackDirectionFlat;
            Reaction = reaction;
        }
    }

    public interface IBladeClashReceiver
    {
        // Called by an attacker's swept weapon at the FIRST point its sweep crosses this receiver's
        // guard volume. Return None to say "not a block" (attacker then hits the body).
        BladeClashResult TryResolveClash(in BladeClashInfo info);
    }

    // Pure decision logic behind PlayerGuard - unit-testable with no MonoBehaviour / scene / physics,
    // same split as PlayerGuardUtility / StancePoise-adjacent helpers.
    public static class BladeClashUtility
    {
        // The single source of truth for the spec's priority chain. Order is fixed:
        //   frontal? -> in parry window? -> guard button held? -> else None
        //
        // 2026-09-01 (user: "沒辦法透過單點防禦按鍵來彈反") - a PARRY only needs the press-edge to
        // land within the window; the button does NOT have to still be held (Sekiro tap-to-deflect).
        // Only the sustained GUARD needs the button down.
        public static BladeClashResult Classify(bool isFrontal, bool withinParryWindow, bool guardHeld)
        {
            if (!isFrontal)
            {
                return BladeClashResult.None;
            }
            if (withinParryWindow)
            {
                return BladeClashResult.Parried;
            }
            if (guardHeld)
            {
                return BladeClashResult.Guarded;
            }
            return BladeClashResult.None;
        }

        // Parry window is open for [0, parryWindowDuration] measured from the guard-button PRESS
        // EDGE (guardStartTime). A "no press recorded" sentinel (large negative) fails the upper
        // bound on its own - no special-case check (which used to also reject a legitimately small
        // guardStartTime early in a session).
        public static bool WithinParryWindow(float now, float guardStartTime, float parryWindowDuration)
        {
            float elapsed = now - guardStartTime;
            return elapsed >= 0f && elapsed <= Mathf.Max(0f, parryWindowDuration);
        }

        // A short debounce so one continuous scrape (or two hit-windows back to back) can't spawn a
        // burst of sparks / SFX / posture ticks. Returns true if enough time has passed since the
        // last resolved clash.
        public static bool ClashCooldownElapsed(float now, float lastClashTime, float cooldownSeconds)
        {
            return now - lastClashTime >= Mathf.Max(0f, cooldownSeconds);
        }
    }
}
