using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // Blue world-space bar showing UltimateEnergy.CurrentEnergy / MaxEnergy (2026-08-13,
    // explicit user request: "藍色能量條"). Same structure as WorldSpaceHealthBar (poll every
    // frame, billboard to camera every LateUpdate) - deliberately a separate class rather
    // than a generalized "world space bar" base, matching this codebase's established
    // preference for small duplicated components over a shared abstraction until a third
    // user actually needs one (see CLAUDE.md-level precedent: HealthBarSetup.AddHealthBar
    // itself stayed a plain static method reused by Player2/Player3 instead of being
    // generalized further than that).
    public class WorldSpaceEnergyBar : MonoBehaviour
    {
        [SerializeField] private UltimateEnergy energy;
        [SerializeField] private Image fillImage;

        private void Update()
        {
            if (energy == null || fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = HealthBarUtility.ComputeFillAmount(energy.CurrentEnergy, energy.MaxEnergy);
        }

        private void LateUpdate()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                return;
            }

            transform.rotation = mainCamera.transform.rotation;
        }
    }
}
