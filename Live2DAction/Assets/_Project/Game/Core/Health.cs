using System;
using UnityEngine;

namespace Live2DAction.Core
{
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float maxHealth = 100f;

        // Lazily initialized instead of relying on Awake(), which is not guaranteed to
        // have run yet for a component added moments ago (e.g. from an EditMode test
        // or another script's Awake) - avoids order-of-initialization bugs.
        private float? currentHealth;

        public float MaxHealth => maxHealth;

        public float CurrentHealth
        {
            get
            {
                if (!currentHealth.HasValue)
                {
                    currentHealth = maxHealth;
                }

                return currentHealth.Value;
            }
            private set => currentHealth = value;
        }

        public bool IsDead { get; private set; }

        // 2026-08-19, explicit user request ("所有角色復活後1.5內無敵 不受傷害") - StancePoise
        // needed a SECOND, independent source of invulnerability (post-stagger recovery) on top
        // of the existing dodge one (CharacterMovement mirroring its DodgeState), and the two
        // must not be able to clobber each other - a plain settable bool can't do that safely:
        // if dodge writes it unconditionally every frame (see CharacterMovement's own Update)
        // and StancePoise also wrote `true`/`false` directly, whichever one runs LAST each frame
        // would silently stomp the other's grant. Backed by a source set instead - true as long
        // as ANY source still holds it, and one source ending can never clear another's. The
        // plain bool setter below is kept for every EXISTING caller (CharacterMovement, tests)
        // as a fixed "default" source so none of them need to change - only a genuinely NEW,
        // independently-tracked source (StancePoise) needs the explicit SetInvulnerable overload.
        private static readonly object DefaultInvulnerabilitySource = new object();
        private readonly System.Collections.Generic.HashSet<object> _invulnerabilitySources = new System.Collections.Generic.HashSet<object>();

        public bool IsInvulnerable
        {
            get => _invulnerabilitySources.Count > 0;
            set => SetInvulnerable(DefaultInvulnerabilitySource, value);
        }

        // Grants (or releases) invulnerability from a specific, caller-owned source - pass a
        // stable reference (e.g. `this`) so releasing your own grant can never accidentally
        // release someone else's. Health still doesn't know or care WHY any given source wants
        // this, only that damage should be ignored while at least one source holds it.
        public void SetInvulnerable(object source, bool invulnerable)
        {
            if (invulnerable)
            {
                _invulnerabilitySources.Add(source);
            }
            else
            {
                _invulnerabilitySources.Remove(source);
            }
        }

        // 2026-08-18, explicit user request ("將這個動作作為所有角色死亡時的共同動作") - default
        // false preserves the original "deactivate the instant it dies" behavior for anything
        // that doesn't opt in (Mecha, the Live2D 076/077 billboards - neither has a compatible
        // Humanoid rig for the new death clip, see DeathAnimationLink's own comment). Set true by
        // DeathAnimationSetup on whichever characters get a DeathAnimationLink wired, so THAT
        // component can play the Dying animation for its own measured duration before deactivating
        // the GameObject itself - deactivating synchronously here, in the same call that fires
        // Died, never gave any Died listener a chance to render even a single frame of a death
        // animation first.
        [SerializeField] private bool deferDeactivationToDeathAnimation;

        public event Action<DamageInfo> Damaged;
        public event Action Died;

        public void ApplyDamage(DamageInfo damageInfo)
        {
            if (IsDead || IsInvulnerable)
            {
                return;
            }

            CurrentHealth = Mathf.Max(0f, CurrentHealth - damageInfo.Amount);
            Damaged?.Invoke(damageInfo);

            if (CurrentHealth <= 0f)
            {
                IsDead = true;
                Died?.Invoke();
                if (!deferDeactivationToDeathAnimation)
                {
                    gameObject.SetActive(false);
                }
            }
        }

        // For whatever wants to restore some (not necessarily all) health outside of the
        // damage pipeline (e.g. HealthRegeneration) - clamps to maxHealth and is a no-op while
        // dead, same guard ApplyDamage uses, so a still-ticking regen timer on a just-died
        // character can't quietly revive it a moment later.
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        }

        // For whatever wants to bring a dead GameObject back (e.g. RespawnController) - must
        // be called AFTER re-activating the GameObject, or before, doesn't matter which, since
        // this only touches Health's own state, not the GameObject's active flag.
        // 2026-08-12: added alongside RespawnController (then named PlayerRespawnController) -
        // see that class's comment for why "dead" previously meant permanently gone with no way
        // back for the player.
        public void ResetHealth()
        {
            CurrentHealth = maxHealth;
            IsDead = false;
        }
    }
}
