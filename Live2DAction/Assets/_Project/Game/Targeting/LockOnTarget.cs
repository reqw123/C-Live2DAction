using UnityEngine;

namespace Live2DAction.Targeting
{
    // Marker component for anything the player can lock onto. AimPoint lets a target expose
    // a specific look-at point (e.g. chest height) instead of its root transform; defaults to
    // the root when unset.
    public class LockOnTarget : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;

        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
    }
}
