namespace Live2DAction.AI
{
    // Pure state-decision logic, kept separate from EnemyAI/MonoBehaviour polling so it's
    // directly EditMode-testable (mirrors AttackResolver's existing pure-logic pattern).
    public static class EnemyBehaviorUtility
    {
        public static EnemyState DetermineState(float distanceToTarget, float detectionRange, float attackRange)
        {
            if (distanceToTarget > detectionRange)
            {
                return EnemyState.Idle;
            }

            if (distanceToTarget > attackRange)
            {
                return EnemyState.Chasing;
            }

            return EnemyState.Attacking;
        }
    }
}
