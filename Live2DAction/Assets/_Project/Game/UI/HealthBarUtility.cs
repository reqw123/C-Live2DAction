using UnityEngine;

namespace Live2DAction.UI
{
    // Pure fill-amount math, kept separate from the MonoBehaviour that reads Health/writes an
    // Image every frame so it's directly EditMode-testable (mirrors AttackResolver/
    // TargetLockUtility's existing pure-logic pattern in this codebase).
    public static class HealthBarUtility
    {
        public static float ComputeFillAmount(float currentHealth, float maxHealth)
        {
            if (maxHealth <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(currentHealth / maxHealth);
        }
    }
}
