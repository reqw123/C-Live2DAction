using UnityEngine;
using Live2DAction.Input;

namespace Live2DAction.Targeting
{
    public class TargetLockController : MonoBehaviour, ILockOnSource
    {
        [SerializeField] private MonoBehaviour inputSource;

        // Used only to decide which candidates count as "in view" when acquiring a target;
        // falls back to this transform's own forward if unset (e.g. in tests).
        [SerializeField] private Transform viewOrigin;

        [SerializeField] private float maxLockRange = 15f;
        [SerializeField] private float maxLockAngleDegrees = 60f;

        // Deliberately larger than maxLockRange so a target doesn't immediately drop the
        // instant it's acquired at the edge of range.
        [SerializeField] private float breakRange = 20f;

        private Transform _lockedTarget;

        // Resolved on every use rather than cached in Awake(), matching this codebase's
        // established convention (see CharacterMovement/PlayerCombat) so tests assigning
        // inputSource via reflection after AddComponent still take effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;

        public Transform LockedTarget => _lockedTarget;
        public bool IsLocked => _lockedTarget != null;

        private void Update()
        {
            IInputCommand inputCommand = InputCommand;
            bool lockPressed = inputCommand != null && inputCommand.LockOnPressed;

            if (lockPressed)
            {
                _lockedTarget = IsLocked ? null : FindTarget();
            }

            if (IsLocked && !TargetLockUtility.IsStillValid(transform.position, _lockedTarget, breakRange))
            {
                _lockedTarget = null;
            }
        }

        private Transform FindTarget()
        {
            LockOnTarget[] candidates = Object.FindObjectsByType<LockOnTarget>(FindObjectsSortMode.None);
            Vector3 viewDirection = viewOrigin != null ? viewOrigin.forward : transform.forward;

            var aimPoints = new Transform[candidates.Length];
            for (int i = 0; i < candidates.Length; i++)
            {
                aimPoints[i] = candidates[i].AimPoint;
            }

            return TargetLockUtility.FindBestTarget(transform.position, viewDirection, maxLockRange, maxLockAngleDegrees, aimPoints);
        }
    }
}
