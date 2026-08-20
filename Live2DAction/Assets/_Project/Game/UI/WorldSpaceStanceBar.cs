using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Combat;

namespace Live2DAction.UI
{
    // Stance/架式 world-space bar (2026-08-18, explicit user request: "架式條圖案，放在能量條下
    // 方，邏輯與能量條同理"). Deliberately mirrors WorldSpaceEnergyBar's own structure field-for-
    // field (poll every frame, billboard to camera, pulse when "ready") rather than a shared
    // base class - same reasoning as that class's own header comment (this codebase's
    // established preference for small duplicated components until generalizing actually pays
    // off). "Ready" here means IsStaggered (bar is full) rather than IsFull, since that's this
    // bar's equivalent moment - the character is now executable.
    public class WorldSpaceStanceBar : MonoBehaviour
    {
        [SerializeField] private StancePoise stance;
        [SerializeField] private Image fillImage;

        [SerializeField] private float pulseSpeed = 6f;
        [SerializeField] private float pulseMinBrightness = 1f;
        [SerializeField] private float pulseMaxBrightness = 1.8f;

        private Color? _baseFillColor;

        private void Update()
        {
            if (stance == null || fillImage == null)
            {
                return;
            }

            if (!_baseFillColor.HasValue)
            {
                _baseFillColor = fillImage.color;
            }

            fillImage.fillAmount = HealthBarUtility.ComputeFillAmount(stance.CurrentStance, stance.MaxStance);

            float brightness = EnergyBarPulseUtility.ComputePulseBrightness(
                stance.IsStaggered, Time.time, pulseSpeed, pulseMinBrightness, pulseMaxBrightness);
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
