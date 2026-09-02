namespace Live2DAction.Core
{
    // 2026-08-31, user request ("把滑鼠右鍵改成武士刀防禦") - a generic pre-damage hook so a
    // component can inspect and reshape an incoming DamageInfo (reduce Amount, override poise,
    // etc.) BEFORE Health applies it, without Health having to know why. Currently the only
    // implementer is PlayerGuard (katana block = frontal health-damage mitigation, poise still
    // accumulates in full - mirrors the boss Boxing_Guard precedent noted in BossHitbox), but a
    // future perfect-parry / armour / elemental-resist system would hang off this same interface.
    //
    // Health collects these from its OWN GameObject only (GetComponents, not InChildren) and runs
    // them in component order. A modifier returns the (possibly unchanged) DamageInfo to pass on;
    // returning it as-is is a no-op. Disabled Behaviours are skipped by Health.
    public interface IIncomingDamageModifier
    {
        DamageInfo ModifyIncoming(DamageInfo incoming);
    }
}
