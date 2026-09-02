using UnityEngine;
using Live2DAction.CameraSystem;

namespace Live2DAction.UI
{
    // 2026-08-31, user request ("為貓咪補上三個血量條 能量條 架式條" + chose "只在操控貓時顯示"). The
    // cat is a possessable character (C swaps player <-> cat, see CameraPossessionSwitcher). Its
    // three bars (生命 / 能量 / 架式) live in their own CatCornerHud canvas at the same top-right
    // spot as the player's PlayerCornerHud; this shows exactly one of the two HUDs at a time,
    // following whoever you're currently controlling.
    //
    // Toggles Canvas.enabled (not GameObject.SetActive) so both HUDs' *BarFx components keep their
    // Update()s running while hidden - the bars are already at the right fill the instant that HUD
    // reappears, no snap-in (same reasoning as WushiBossHudVisibility). NOT gated on combat state -
    // "只在操控貓時顯示", full stop, no hide-until-fighting like the boss HUD.
    //
    // Fail-safe: if the switcher reference is missing, defaults to showing the player HUD and
    // hiding the cat HUD, so a broken wiring never leaves the player staring at cat bars.
    [DisallowMultipleComponent]
    public class PossessionHud : MonoBehaviour
    {
        [SerializeField] private CameraPossessionSwitcher possession;
        [SerializeField] private Canvas playerHud;
        [SerializeField] private Canvas catHud;

        private void OnEnable()
        {
            Apply(); // no visible flip on the first frame after a scene load / re-enable
        }

        private void LateUpdate()
        {
            Apply();
        }

        private void Apply()
        {
            bool catPossessed = possession != null
                                && possession.Current == CameraPossessionSwitcher.Possessed.Cat;
            bool showCat = ShowCatHud(possession != null, catPossessed);

            if (catHud != null && catHud.enabled != showCat)
            {
                catHud.enabled = showCat;
            }
            if (playerHud != null && playerHud.enabled == showCat)
            {
                playerHud.enabled = !showCat;
            }
        }

        // Pure decision: show the cat HUD only while a valid switcher says the cat is possessed.
        // hasSwitcher false (missing reference) -> player HUD, never the cat one.
        public static bool ShowCatHud(bool hasSwitcher, bool catIsPossessed)
        {
            return hasSwitcher && catIsPossessed;
        }
    }
}
