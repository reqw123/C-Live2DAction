using UnityEngine;

namespace Live2DAction.AI
{
    // 2026-08-23, explicit user request ("076的碰撞體能不能是扁平的 讓我能直接站在他的表層上" -
    // clarified as BOTH standing on top of her AND being able to walk right up flush against her
    // front/back). CharacterController is always capsule-shaped (Unity has no flat/box
    // CharacterController option), and a rounded capsule top is exactly what
    // GroundSlopeUtility's own "slide off another character" rule targets - standing on ANOTHER
    // CharacterController's domed top is treated as inherently unstable by design (see
    // EnemyAI.TryGetGroundNormal/the ground-slope fix history), so genuinely standing on her
    // needs a real flat surface, not her own movement capsule.
    //
    // Two parts:
    // 1. The player specifically is made to ignore 076's own CharacterController (Physics.
    //    IgnoreCollision) - only the player, so she still physically collides with everything
    //    else (terrain, other characters) exactly as before, this doesn't touch her own
    //    locomotion at all.
    // 2. A separate BoxCollider on a CHILD GameObject (deliberately NOT this same GameObject -
    //    AttackResolver.ResolveHits/IsAnyDamageableInRange use TryGetComponent<IDamageable>,
    //    which only checks the collider's OWN GameObject, not its parent; putting a second
    //    collider directly alongside Health here would make a single melee swing double-hit
    //    through both colliders at once) becomes the only thing the player actually touches: a
    //    flat top to stand on, and a thin front/back face to walk flush up against instead of
    //    being stopped short by her wide capsule radius.
    [RequireComponent(typeof(CharacterController))]
    public class FlatStandableCollider : MonoBehaviour
    {
        [SerializeField] private CharacterController playerController;

        // World-space size/center of the flat platform box - defaults match 076's own actual
        // rendered bounds (see the CharacterController height-matching fix's own comment for
        // where 4.1447/2.072 come from): full height from her feet to the top of her head, a
        // body-representative width (not her outstretched flame VFX - see the capsule radius
        // fix's own comment for why 0.9m was chosen over the full effect bounds), and a thin
        // depth matching her actual paper-flat Live2D plane rather than a true zero (degenerate
        // box colliders behave unreliably in PhysX).
        [SerializeField] private Vector3 platformWorldSize = new Vector3(1.8f, 4.1447f, 0.4f);

        // OFFSET from this GameObject's own position (root), not an absolute world coordinate -
        // 0.0928 = the visual bounds' world center Y (2.0724) minus 076's own root Y (1.97957),
        // matching the exact same offset CharacterController.center.y already uses (see that
        // fix's own comment) so the platform sits flush with the capsule's own height match.
        [SerializeField] private Vector3 platformWorldCenterOffset = new Vector3(0f, 0.0928f, 0f);

        private void Start()
        {
            if (playerController != null)
            {
                Physics.IgnoreCollision(GetComponent<CharacterController>(), playerController, true);
            }
        }

        // Exposed so an editor bootstrap (or this component's own first-time setup) can build the
        // child box at the right local size regardless of this GameObject's own scale - the child
        // inherits that scale, so the box's own size/center fields must be pre-divided by it to
        // land on the intended WORLD dimensions.
        public void EnsurePlatformChild()
        {
            Transform existing = transform.Find("StandablePlatform");
            GameObject platformGo = existing != null ? existing.gameObject : new GameObject("StandablePlatform");
            platformGo.transform.SetParent(transform, false);

            BoxCollider box = platformGo.GetComponent<BoxCollider>();
            if (box == null)
            {
                box = platformGo.AddComponent<BoxCollider>();
            }

            Vector3 scale = transform.lossyScale;
            box.size = new Vector3(
                platformWorldSize.x / Mathf.Max(0.0001f, scale.x),
                platformWorldSize.y / Mathf.Max(0.0001f, scale.y),
                platformWorldSize.z / Mathf.Max(0.0001f, scale.z));
            box.center = new Vector3(
                platformWorldCenterOffset.x / Mathf.Max(0.0001f, scale.x),
                platformWorldCenterOffset.y / Mathf.Max(0.0001f, scale.y),
                platformWorldCenterOffset.z / Mathf.Max(0.0001f, scale.z));
        }
    }
}
