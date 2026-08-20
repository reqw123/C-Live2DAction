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
    // itself stayed a plain static method reused by Mecha/TrainingDummy instead of being
    // generalized further than that).
    public class WorldSpaceEnergyBar : MonoBehaviour
    {
        [SerializeField] private UltimateEnergy energy;
        [SerializeField] private Image fillImage;

        // 2026-08-16, explicit user request: a full-state effect on the energy bar itself to
        // hint the ultimate is ready, rather than separate prompt UI - a pulsing glow on the
        // existing bar is the smallest change that reads as "ready", and doesn't need any new
        // UI elements. Only pulses while energy.IsFull (see EnergyBarPulseUtility's own
        // comment) - a bar that's always pulsing wouldn't communicate anything.
        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float pulseMinBrightness = 1f;
        [SerializeField] private float pulseMaxBrightness = 1.8f;

        // Captured on first Update rather than Awake/a serialized default, so this always
        // pulses relative to whatever color the bar was actually set up with (currently the
        // fixed blue from UltimateAbilitySetup, but this way a re-tinted bar doesn't need a
        // matching change here).
        private Color? _baseFillColor;

        private void Update()
        {
            if (energy == null || fillImage == null)
            {
                return;
            }

            if (!_baseFillColor.HasValue)
            {
                _baseFillColor = fillImage.color;
            }

            fillImage.fillAmount = HealthBarUtility.ComputeFillAmount(energy.CurrentEnergy, energy.MaxEnergy);

            float brightness = EnergyBarPulseUtility.ComputePulseBrightness(
                energy.IsFull, Time.time, pulseSpeed, pulseMinBrightness, pulseMaxBrightness);
            Color baseColor = _baseFillColor.Value;
            fillImage.color = new Color(baseColor.r * brightness, baseColor.g * brightness, baseColor.b * brightness, baseColor.a);
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
