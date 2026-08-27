using UnityEngine;
using UnityEngine.UI;
using Live2DAction.Targeting;

namespace Live2DAction.UI
{
    // 2026-08-23, explicit user request ("玩家在使用滑鼠滾輪鎖定敵人是 必須能視覺上判定成功鎖定到了
    // 誰") - TargetLockController already tracks _lockedTarget internally (driving the camera's
    // auto-center gate and CharacterMovement's facing), but nothing ever showed the player WHICH
    // target that was. One single indicator (not one per LockOnTarget candidate) that repositions
    // itself to whichever target is currently locked - same "always-active Canvas, drive alpha
    // every frame" convention as ExecutionReadyIndicator/InvulnerabilityRippleEffect, and the same
    // "recompute a camera-facing offset from the live position every frame" positioning approach as
    // ExecutionReadyIndicator's own LateUpdate, just keyed off the dynamic LockedTarget instead of a
    // fixed per-character bone.
    public class LockOnIndicator : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour lockOnSource;
        [SerializeField] private Image ringImage;

        // How far off the target's AimPoint to push, straight toward the camera - same purpose as
        // ExecutionReadyIndicator.chestSurfaceOffset (clear the target's own body mesh so the ring
        // doesn't get buried inside it), but targets here vary in size (Enemy vs 中立者1/2/3 vs
        // Mecha), so this errs a bit larger than that indicator's 0.15 to stay clear of the widest
        // body this project has.
        [SerializeField] private float targetSurfaceOffset = 0.25f;

        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;

        private void LateUpdate()
        {
            Transform locked = LockOnSource?.LockedTarget;
            bool visible = locked != null;

            Camera mainCamera = Camera.main;
            if (visible && mainCamera != null)
            {
                Vector3 towardCamera = (mainCamera.transform.position - locked.position).normalized;
                transform.position = locked.position + towardCamera * targetSurfaceOffset;
                transform.rotation = mainCamera.transform.rotation;
            }

            if (ringImage != null)
            {
                ringImage.color = new Color(1f, 1f, 1f, visible ? 1f : 0f);
            }
        }
    }
}
