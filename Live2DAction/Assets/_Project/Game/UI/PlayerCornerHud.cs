using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;
using Live2DAction.Combat;

namespace Live2DAction.UI
{
    // 2026-08-23, explicit user request ("將玩家的血量條 能量等等統一移動到畫面右上角固定顯示") - the
    // player's own Health/UltimateEnergy/StancePoise/FlightEnergy bars used to only exist as
    // world-space canvases floating above their own head (WorldSpaceHealthBar/WorldSpaceEnergyBar/
    // WorldSpaceStanceBar), which the player can never actually see from their own camera. This is
    // a plain screen-space HUD stack in the top-right corner instead, reading the exact same
    // underlying data sources - same "poll every frame" convention those already use. Enemy/076's
    // own world-space bars are untouched; those are for the player to see when looking AT an
    // opponent, which still works fine floating above the opponent's head.
    //
    // 2026-08-23 follow-up, explicit user request ("玩家血量條...程式即時控制/平滑Tween/Delayed
    // Health Bar/Edge Glow/Shader能量流動...") - the health row's health/healthFill/healthText
    // fields were removed from here: PlayerHealthBarFx now owns that row completely (tweened
    // fill, delayed ghost bar, edge glow, flow-shader material, damage flash/shake/spark), and
    // having BOTH this Update() and PlayerHealthBarFx.Update() write healthFill.fillAmount every
    // frame would race unpredictably over who wins each frame. 必殺/架勢/飛行 are untouched -
    // only the health row's presentation was in scope for that request.
    //
    // 2026-08-25, explicit user request ("以這樣圖渲染能量條(所有具有能量機制的共用)") - same removal,
    // now for 必殺 specifically: ultimateEnergy/ultimateEnergyFill/ultimateEnergyText are gone,
    // UltimateEnergyBarFx now owns that row completely (same tween/delayed-fill/edge-glow/flow-
    // shader/activation-flash treatment as the health row). 架勢/飛行 remain untouched here.
    //
    // 2026-08-25 follow-up, explicit user request ("同理自作架勢條ui") - same removal again, now
    // for 架勢: stance/stanceFill/stanceText are gone, StancePoiseBarFx now owns that row. 飛行
    // remains untouched (out of scope - no reference image provided for it yet).
    public class PlayerCornerHud : MonoBehaviour
    {
        [SerializeField] private UltimateEnergy flightEnergy;

        [SerializeField] private Image flightEnergyFill;

        // 2026-08-23, real playtested bug report ("玩家狀態條...扣血/用技能後完全不動") - added purely
        // as a diagnostic aid: this component has been directly verified correct multiple times
        // (forced frame-stepping shows fillAmount responds instantly), but the bug report persists
        // from real interactive Play Mode testing, which this session's own automation can't
        // reproduce. Numeric "current/max" text next to each bar answers the next diagnostic
        // question directly on-screen: if the NUMBER also never changes, the whole component
        // genuinely isn't ticking (a real Update-not-running bug); if the number updates but the
        // bar doesn't visually move, the bug is in Image rendering specifically, not the data.
        // Optional/null-safe like the Fill fields above, so a setup that doesn't wire these still
        // works exactly as before.
        [SerializeField] private Text flightEnergyText;

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - flightEnergyText was
        // rebuilt via string interpolation every Update() regardless of whether the displayed
        // numbers changed - same fix as PlayerHealthBarFx/StancePoiseBarFx/UltimateEnergyBarFx's
        // own valueText guards.
        private int _lastFlightEnergyTextCurrent = int.MinValue;
        private int _lastFlightEnergyTextMax = int.MinValue;

        private void Update()
        {
            if (flightEnergy != null && flightEnergyFill != null)
            {
                flightEnergyFill.fillAmount = HealthBarUtility.ComputeFillAmount(flightEnergy.CurrentEnergy, flightEnergy.MaxEnergy);
            }
            if (flightEnergy != null && flightEnergyText != null)
            {
                int currentInt = Mathf.CeilToInt(flightEnergy.CurrentEnergy);
                int maxInt = Mathf.CeilToInt(flightEnergy.MaxEnergy);
                if (currentInt != _lastFlightEnergyTextCurrent || maxInt != _lastFlightEnergyTextMax)
                {
                    flightEnergyText.text = $"{currentInt}/{maxInt}";
                    _lastFlightEnergyTextCurrent = currentInt;
                    _lastFlightEnergyTextMax = maxInt;
                }
            }
        }
    }
}
