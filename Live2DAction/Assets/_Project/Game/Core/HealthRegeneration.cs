using UnityEngine;

namespace Live2DAction.Core
{
    // 2026-08-16, explicit user request: 敵我雙方閒置10秒鐘沒受到傷害時，每秒回復2點生命值 - both
    // player and enemy characters passively heal after 10s without taking damage.
    //
    // Polls Health.CurrentHealth every frame and compares it to the previous frame's value to
    // detect damage, rather than subscribing to Health.Damaged in OnEnable() - subscribing
    // there hits the same wiring-order trap RespawnController's own class comment documents:
    // editor tools call AddComponent() then set this component's `health` field via
    // SerializedObject/reflection afterward, so OnEnable() (which runs synchronously during
    // AddComponent()) would always subscribe while `health` is still null and the event would
    // never actually fire. Polling also has the nice side effect of correctly resetting the
    // idle timer on ANY health decrease, not just ones that happened to go through
    // Health.ApplyDamage.
    public class HealthRegeneration : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private float idleSecondsBeforeRegen = 10f;
        [SerializeField] private float regenPerSecond = 2f;

        private float? _lastKnownHealth;
        private float _secondsSinceLastDamage;

        private void Update()
        {
            if (health == null || health.IsDead)
            {
                return;
            }

            float currentHealth = health.CurrentHealth;
            if (!_lastKnownHealth.HasValue)
            {
                _lastKnownHealth = currentHealth;
            }

            _secondsSinceLastDamage = HealthRegenerationUtility.AdvanceIdleTimer(
                _lastKnownHealth.Value, currentHealth, _secondsSinceLastDamage, Time.deltaTime);
            _lastKnownHealth = currentHealth;

            if (HealthRegenerationUtility.ShouldRegenerate(_secondsSinceLastDamage, idleSecondsBeforeRegen, currentHealth, health.MaxHealth))
            {
                health.Heal(regenPerSecond * Time.deltaTime);
            }
        }
    }
}
