namespace Live2DAction.Core
{
    // Pure timing/threshold logic for HealthRegeneration, kept separate from the MonoBehaviour
    // that owns Time.deltaTime/Health so it's directly EditMode-testable (mirrors
    // AttackResolver/EnemyBehaviorUtility/HealthBarUtility's existing pure-logic pattern).
    public static class HealthRegenerationUtility
    {
        // Resets the idle timer to 0 if health dropped since last frame (damage, from any
        // source - not tied to a specific ApplyDamage call), otherwise advances it by
        // deltaTime. Healing (health going up, e.g. from this same regen ticking) does NOT
        // reset the timer - only a drop counts as "took damage".
        public static float AdvanceIdleTimer(float previousHealth, float currentHealth, float secondsSinceLastDamage, float deltaTime)
        {
            if (currentHealth < previousHealth)
            {
                return 0f;
            }

            return secondsSinceLastDamage + deltaTime;
        }

        public static bool ShouldRegenerate(float secondsSinceLastDamage, float idleSecondsBeforeRegen, float currentHealth, float maxHealth)
        {
            return secondsSinceLastDamage >= idleSecondsBeforeRegen && currentHealth < maxHealth;
        }
    }
}
