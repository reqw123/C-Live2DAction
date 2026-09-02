using NUnit.Framework;
using Live2DAction.UI;

public class PossessionHudTests
{
    [Test]
    public void ShowCatHud_OnlyWhenCatPossessedThroughAValidSwitcher()
    {
        Assert.IsTrue(PossessionHud.ShowCatHud(hasSwitcher: true, catIsPossessed: true));
        Assert.IsFalse(PossessionHud.ShowCatHud(hasSwitcher: true, catIsPossessed: false));
    }

    [Test]
    public void ShowCatHud_MissingSwitcher_NeverShowsTheCatHud()
    {
        // A broken wiring must fall back to the player HUD, not strand the player on cat bars.
        Assert.IsFalse(PossessionHud.ShowCatHud(hasSwitcher: false, catIsPossessed: true));
        Assert.IsFalse(PossessionHud.ShowCatHud(hasSwitcher: false, catIsPossessed: false));
    }
}
