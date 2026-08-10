using UnityEngine;

namespace Live2DAction.Targeting
{
    // Mirrors ICameraYawSource's pattern: an optional serialized MonoBehaviour reference cast
    // to this interface, so CharacterMovement and ThirdPersonCameraController can both react
    // to the current lock-on target without depending on TargetLockController directly.
    public interface ILockOnSource
    {
        Transform LockedTarget { get; }
    }
}
