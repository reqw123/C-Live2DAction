using UnityEngine;

namespace Live2DAction.Characters
{
    // Simple decorative wandering for static-standee characters like Mecha's mecha - not a
    // full character controller (no gravity/input/combat, just Transform.position += velocity
    // each frame), so it works on objects that only have a plain Collider, not a
    // CharacterController. Picks a new random horizontal direction every few seconds via
    // WanderUtility, and steers back toward the origin instead of a random angle whenever it's
    // past boundaryHalfExtent - stays inside the BoundaryWall_* colliders without needing to
    // actually touch them.
    public class WanderMovement : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 0.5f;

        // Comfortably inside BoundaryWall_North/South/East/West (at world +/-15, see
        // GreyboxSceneBuilder.CreateBoundaryWalls) so this turns around before ever touching
        // them.
        [SerializeField] private float boundaryHalfExtent = 13f;

        [SerializeField] private float directionChangeIntervalSeconds = 3f;

        // Eased turning (Mathf.SmoothDampAngle), not a hand-computed quaternion or a constant
        // degrees/sec rate - see CharacterMovement's rotationSmoothTime for the same reasoning,
        // and Docs/KNOWN_ISSUES.md for why a hand-typed quaternion is a real, previously-hit
        // footgun (an unnormalized one spammed "Quaternion To Matrix conversion failed" every
        // GUI event).
        [SerializeField] private float rotationSmoothTime = 0.3f;

        // 2026-08-18, explicit user request ("機甲戰士也給他架式條") - optional (null-safe
        // below), same "freeze movement while staggered" gate CharacterMovement/EnemyAI already
        // apply for their own characters. A wandering standee with no combat of its own still
        // shouldn't keep strolling around while kneeling/dazed.
        [SerializeField] private Live2DAction.Combat.StancePoise stance;

        private Vector3 _direction;
        private float _timeUntilDirectionChange;
        private float _yawAngularVelocity;

        private void Awake()
        {
            _direction = transform.forward;
        }

        private void Update()
        {
            if (stance != null && stance.IsStaggered)
            {
                return;
            }

            _timeUntilDirectionChange -= Time.deltaTime;
            bool pastBoundary = Mathf.Abs(transform.position.x) > boundaryHalfExtent || Mathf.Abs(transform.position.z) > boundaryHalfExtent;

            if (_timeUntilDirectionChange <= 0f || pastBoundary)
            {
                _direction = WanderUtility.ComputeDirection(transform.position, _direction, boundaryHalfExtent, () => Random.Range(0f, 360f));
                _timeUntilDirectionChange = directionChangeIntervalSeconds;
            }

            transform.position += _direction * moveSpeed * Time.deltaTime;

            if (_direction.sqrMagnitude > 0.0001f)
            {
                float currentYaw = transform.eulerAngles.y;
                float targetYaw = Quaternion.LookRotation(_direction, Vector3.up).eulerAngles.y;
                float newYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref _yawAngularVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
        }
    }
}
