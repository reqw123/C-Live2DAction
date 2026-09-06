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

        // 2026-09-06 - drop any current lock immediately, from outside the normal
        // press-to-toggle / drifted-past-breakRange flow. Used when a scripted sequence ends and
        // needs the camera handed cleanly back to a plain follow of the player (e.g.
        // YuanpeiEncounter victory / defeat): disabling a boss's LockOnTarget doesn't release an
        // already-acquired lock on its own - _lockedTarget is a raw Transform and IsStillValid
        // only checks activeInHierarchy + horizontal range, so the lock (and its camera-distance
        // multiplier) would otherwise linger until the boss is destroyed by the scene unload.
        public void ForceRelease() => _lockedTarget = null;

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

        // 2026-08-13, explicit user request ("能不能把 攻擊距離 警備距離 用不同顏色線條呈現嗎
        // 角色1和4都要") - Player has no EnemyAI/detectionRange (it's player-controlled, not
        // AI), so maxLockRange (how far it can spot/lock a target) is the closest analog to
        // Player4's "警備距離"/detectionRange - both answer "how far can this character notice
        // something". Cyan, deliberately distinct from PlayerCombat's own attack-range Gizmo
        // (red/orange/yellow per combo step), so the two concepts read as separate at a glance.
        // Only ever called by the Editor (see PlayerCombat.OnDrawGizmosSelected's own comment
        // for why no #if UNITY_EDITOR guard is needed).
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireSphere(transform.position, maxLockRange);
        }
    }
}
