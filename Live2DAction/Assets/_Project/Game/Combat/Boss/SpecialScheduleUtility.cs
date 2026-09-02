namespace Live2DAction.Combat.Boss
{
    // spec WUSHI_COMBAT_ENGINEERING_SPEC.md §9.2 (M4 項目 8). The shared-cooldown gate for the boss's
    // periodic special pool, pulled out so BossStateMachine's scheduling rule is unit-testable.
    public static class SpecialScheduleUtility
    {
        // True when another periodic special may fire: the feature is off (cooldownSeconds <= 0), or
        // enough time has passed since the last one. lastFireTime is a Time.time stamp (a large
        // negative sentinel before any special has fired).
        public static bool SharedCooldownReady(float lastFireTime, float now, float cooldownSeconds)
        {
            return cooldownSeconds <= 0f || now - lastFireTime >= cooldownSeconds;
        }
    }
}
