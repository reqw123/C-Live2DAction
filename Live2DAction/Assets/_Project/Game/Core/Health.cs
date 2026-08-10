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

        // Set by whatever grants temporary invulnerability (e.g. CharacterMovement mirroring
        // its DodgeState) - Health doesn't know or care why, only that damage should be
        // ignored while true.
        public bool IsInvulnerable { get; set; }

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
                gameObject.SetActive(false);
            }
        }
    }
}
