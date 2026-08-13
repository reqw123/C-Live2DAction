using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Core;

namespace Live2DAction.UI
{
    // A red fill bar floating above a character's head (World Space Canvas child), showing
    // Health.CurrentHealth / Health.MaxHealth. Polls every frame rather than subscribing to
    // Health.Damaged/Died, matching this codebase's established "resolved on every use"
    // convention (see CharacterMovement.InputCommand, PlayerCombat.InputCommand) - simpler
    // than managing subscribe/unsubscribe lifetimes for a purely cosmetic readout.
    public class WorldSpaceHealthBar : MonoBehaviour
    {
        [SerializeField] private Health health;
        [SerializeField] private Image fillImage;

        private void Update()
        {
            if (health == null || fillImage == null)
            {
                return;
            }

            fillImage.fillAmount = HealthBarUtility.ComputeFillAmount(health.CurrentHealth, health.MaxHealth);
        }

        // Plain "match the camera's rotation" billboard (not CubismBillboard's yaw-only
        // variant) - a flat UI bar should stay exactly parallel to the camera's view plane
        // regardless of pitch, the standard approach for floating HUD nameplates/health bars.
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
