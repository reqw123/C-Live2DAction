namespace Live2DAction.Combat.Boss
{
    // Permanent HP-gated stage lock (>50% vs <=50%) - see BossStateMachine's own phase-lock
    // comment for why this is a one-way ratchet (Phase2, once entered, never reverts even if the
    // boss heals back above 50%).
    public enum BossPhase
    {
        Phase1,
        Phase2,
    }
}
