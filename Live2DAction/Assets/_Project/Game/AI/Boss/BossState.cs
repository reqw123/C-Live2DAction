namespace Live2DAction.AI.Boss
{
    // The boss's own program-level state machine - the Animator only plays whatever clip this
    // state currently maps to (see BossAnimatorBridge); it never decides combat logic itself.
    // See BossStateMachine's own priority-order comment for how conflicts between these resolve
    // in any single frame.
    public enum BossState
    {
        Dormant,
        Alert,
        Idle,
        Approach,
        Attack,
        DodgeCounter,
        // 2026-08-26, explicit user request - periodic combat flourish that also lands a real hit
        // (Breakdance_1990). Triggered purely by BossTuning.BreakdanceTriggerSeconds of accumulated
        // combat time (see BossStateMachine.UpdateCombatTimer), not by the normal weighted-pool
        // distance/angle selection normalAttackPool uses - see TryEnterBreakdance's own comment.
        Breakdance,
        // 2026-08-27, explicit user request ("定時小技能，戰鬥每經過20秒就觸發...先飛升到空中，然後落
        // 地劈砍...落地時請直接鎖定玩家，並且落下的期間全程具有攻擊幀 範圍大") - same "queued by a
        // BossTuning.*TriggerSeconds combat-time timer" pattern as Breakdance above, not the normal
        // weighted-pool distance/angle selection - see BossStateMachine.TryEnterLeapSlam's own
        // comment. Distinct from the existing Vanishing/DiveAttack pair (that's a teleport-then-fall
        // sequence tied to the Vanish cycle, with a single-frame landing-only AOE) - this move's own
        // clip has the whole rise-and-fall baked into its bone animation with the root never moving,
        // so no separate airborne-physics state is needed, and the hit window stays open for the
        // WHOLE descent per the explicit request, not just the landing instant.
        LeapSlam,
        UltimateReposition,
        UltimatePrepare,
        UltimateAttack,
        Vanishing,
        DiveAttack,
        HitReaction,
        PostureBroken,
        Victory,
        Dead,
    }
}
