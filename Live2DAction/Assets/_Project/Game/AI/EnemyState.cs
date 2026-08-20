namespace Live2DAction.AI
{
    public enum EnemyState
    {
        Idle,
        Chasing,
        Attacking,

        // 2026-08-17, explicit user request ("想要製作斬殺系統...滿格會陷入僵直") - forced by
        // EnemyAI whenever its StancePoise reports IsStaggered, overriding whatever
        // EnemyBehaviorUtility.DetermineState would otherwise have returned. Deliberately not
        // produced BY DetermineState itself (stance/poise isn't a function of distance to the
        // target, unlike Idle/Chasing/Attacking) - see EnemyAI.Update's own comment.
        Staggered
    }
}
