namespace Live2DAction.Combat.Boss
{
    // Which physical hitbox a HitWindow drives. Matches the fixed set of attack hitboxes every
    // BossAttackDefinition's HitWindows reference by name - see BossHitbox for the actual
    // trigger colliders these correspond to on the character rig.
    public enum BossHitboxPart
    {
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot,
        Body,
        LandingAOE,
        // 2026-08-26, explicit user request (Boss AI/Katana spec, section 七) - a rigid weapon
        // (e.g. a katana socketed to RightHand) needs its own BladeHitbox distinct from the bare
        // RightHand hitbox: "BladeHitbox使用Trigger Collider,只覆蓋有效刀刃,不能包含劍柄", and kicks
        // must keep working with the weapon still equipped ("踢擊使用獨立腳部Hitbox,踢擊期間武士刀
        // 仍在右手,但刀刃Hitbox必須關閉") - so blade reach/timing has to be independently
        // configurable per attack rather than reusing RightHand's window data.
        Weapon,
    }
}
