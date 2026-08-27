using UnityEngine;

namespace Live2DAction.AI
{
    // 2026-08-23, explicit user request ("enemy則是不希望玩家能夠站在他頭上 要把頂下來") - the
    // project's existing generic "slide off if standing on another CharacterController" logic
    // (GroundSlopeUtility, wired into CharacterMovement/EnemyAI) has a real gap, confirmed via an
    // EditorApplication.Step()-driven test: a player landing on Enemy's rounded capsule top can
    // get physically WEDGED partway down the dome - Unity's own CharacterController.isGrounded
    // reads true from the sideways/diagonal contact resolved during Move() even though
    // TryGetGroundNormal's own downward probe finds nothing there, holding the player in a
    // stable, un-falling equilibrium instead of ever fully clearing them.
    //
    // This is a deterministic backstop specifically for characters that must never be stood on
    // (unlike 076, who now has FlatStandableCollider for the opposite reason) - rather than
    // depending on the ambiguous capsule-vs-capsule slide physics, it directly checks "is the
    // target above my own head height and within my own footprint" every frame and firmly shoves
    // them outward and down via their own CharacterController.Move, so they can never settle
    // anywhere on/above this character regardless of what the generic physics-based slide is
    // doing.
    [RequireComponent(typeof(CharacterController))]
    public class PerchRejector : MonoBehaviour
    {
        [SerializeField] private CharacterController target;
        [SerializeField] private float pushSpeed = 6f;

        // Extra clearance beyond this character's own (scaled) capsule radius - a perched target
        // sitting right at the radius boundary would otherwise flicker in/out of the reject zone
        // every frame as the push itself moves them past and back across that exact line.
        // 2026-08-23, real playtested bug: a plain landing right at this margin's original
        // value (0.3) confirmed the player can get wedged partway down the dome OUTSIDE that
        // radius (observed at horizontal offset ~0.79 against a 0.4-radius capsule, i.e. margin
        // ~0.39 was already too tight) - widened well past the worst observed wedge point so the
        // reject zone covers the whole dome's "shoulder" region, not just directly overhead.
        [SerializeField] private float horizontalMargin = 0.6f;

        // How far above/into this character's own head height counts as "sitting on/against
        // me" - widened for the same reason as horizontalMargin above: the observed wedge points
        // sat noticeably BELOW the exact head-top Y (down to ~7.6 against a 7.70 top), not just
        // level with the very tip.
        [SerializeField] private float heightMargin = 0.3f;

        private CharacterController _ownController;

        private void Awake()
        {
            _ownController = GetComponent<CharacterController>();
        }

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            float ownTop = _ownController.bounds.max.y;
            Vector3 toTarget = target.transform.position - transform.position;
            float horizontalDistance = new Vector2(toTarget.x, toTarget.z).magnitude;
            float rejectRadius = _ownController.bounds.extents.x + horizontalMargin;

            bool perching = target.transform.position.y > ownTop - heightMargin && horizontalDistance < rejectRadius;
            if (!perching)
            {
                return;
            }

            // Same "undefined at dead-center, default to a fixed direction" convention
            // GroundSlopeUtility.ComputeFallbackAwayDirection already uses for this exact
            // degenerate case.
            Vector3 pushDirection = horizontalDistance > 0.01f
                ? new Vector3(toTarget.x, 0f, toTarget.z).normalized
                : Vector3.right;

            Vector3 motion = (pushDirection * pushSpeed + Vector3.down * pushSpeed) * Time.deltaTime;
            target.Move(motion);
        }
    }
}
