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
        // 2026-08-29, user request ("一旦碰到邊界時不要直接傳回原本位置...一直在門口觀望著目標") - a
        // 精怪 confined to 本地 (confineToArena) can't follow the player through the vehicle doorway,
        // so once the player leaves it walks to the boundary and holds there facing them, never
        // attacking, until the player either re-enters (-> Idle) or moves out of watch range
        // (-> ReturnHome). Driven entirely by TryLeashReset.
        GateWatch,
        // 2026-08-29, user request ("脫離追擊範圍後 要使用跑步歸位") - after disengaging (distance
        // leash, or GateWatch giving up) the boss RUNS back to its guard post playing the run
        // blend tree, instead of the old instant SnapToHome teleport. On arrival it snaps the exact
        // pose/rotation and drops to Dormant; if the player re-enters AlertRange mid-jog it
        // re-engages straight away.
        ReturnHome,
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
        //
        // 2026-08-28, explicit user request ("飛天前有1秒前搖的蹲下準備姿態 才飛向天空") - a telegraphed
        // crouch/charge hold before the leap. Same "stand still, face player, hold N seconds, then
        // commit" pattern as UltimatePrepare. Enters LeapSlam once tuning.LeapSlamWindupSeconds
        // elapses (and consumes the leap energy at that commit point, mirroring UltimatePrepare).
        LeapSlamWindup,
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
        // 2026-08-31, explicit user request ("復活時間到慢慢站起來") - the transient rise between
        // Dead (held corpse pose for reviveDelaySeconds) and re-engaging. Plays the death take in
        // reverse over BossTuning.StandUpSeconds (no dedicated stand-up clip in either boss pack),
        // then drops to Alert. Nothing pre-empts it - it's a short scripted beat, same as the
        // phase-transition visual. Appended last so no existing BossState ordinal shifts.
        GettingUp,
    }
}
