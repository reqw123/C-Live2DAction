using UnityEngine;
using Live2DAction.Characters;

namespace Live2DAction.Combat.Boss
{
    // 2026-08-25 rewrite, user feedback ("飛踢時碰撞到玩家的一瞬間 玩家就應該飛出去並受到傷害 而非是有種
    // 延遲 不銜接的感覺") - the original version below (kept in history) ran its OWN independent
    // vertical/horizontal velocity and its OWN separate CharacterController.Move() call every
    // Update(), entirely uncoordinated with CharacterMovement's own gravity accumulation and its
    // own Move() call that ALSO runs every Update(). Two components both integrating gravity and
    // both calling Move() in the same frame is exactly the kind of fight CharacterMovement's own
    // ApplyUpwardLaunch already has a comment warning about ("without fighting its own gravity
    // accumulation... getting silently overwritten... on every subsequent frame") - for an
    // Updraft's sustained push that's merely wasteful; for a one-shot launch it reads as a weak,
    // stuttering, delayed "shove" instead of an immediate clean arc, which matches the reported
    // symptom exactly. Fixed by routing straight into the same two methods CheckpointGate/Updraft
    // already use for external forces (ApplyDash for horizontal, ApplyUpwardLaunch for vertical) -
    // one authoritative velocity/Move() pipeline, no second system to desync from it.
    [RequireComponent(typeof(CharacterMovement))]
    public class KnockbackReceiver : MonoBehaviour, IKnockbackReceiver
    {
        // 2026-08-25, user feedback ("碰撞體判定碰到玩家的一瞬間就讓玩家做飛出動作") - raised from 4:
        // the physics/damage application (OnTriggerEnter -> ApplyKnockback) is already synchronous,
        // but at 4 the airborne beat (~0.8s round trip against gravity) was so brief it barely gave
        // Player's existing Grounded-driven Fall/Jump animator states (see CharacterAnimatorLink)
        // time to register before landing again - reads as "hit and immediately back down" rather
        // than an actual "flew out" moment even though nothing was literally delayed.
        [SerializeField] private float launchUpwardSpeed = 7f;

        // A small guaranteed Move() snap on top of the decaying velocity so the hit still reads as
        // "shoved right now" rather than gradually accelerating into motion.
        // 2026-09-03, user feedback ("非常近距離接觸時 boss 擊退玩家的動畫不自然, 感覺有點掉帧, 順移的
        // 感覺") - at 0.15 the snap was force*0.15 = ~1.5-1.8m of position teleport in a single frame,
        // with nothing animating across it - that's the "順移/掉帧" look. Dropped to a token nudge;
        // the decaying _dashVelocity (force*0.25 over DashDecaySeconds) carries the actual distance
        // smoothly frame-by-frame. Boss knockback forces were bumped to keep the total push.
        [SerializeField] private float instantDisplacementFraction = 0.04f;

        private CharacterMovement _movement;

        private void Awake()
        {
            _movement = GetComponent<CharacterMovement>();
        }

        public void ApplyKnockback(Vector3 horizontalDirection, float force, bool launchesUpward)
        {
            if (_movement == null) return;

            Vector3 dir = horizontalDirection;
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.0001f)
            {
                _movement.ApplyDash(dir.normalized, force, force * instantDisplacementFraction);
            }

            if (launchesUpward)
            {
                _movement.ApplyUpwardLaunch(launchUpwardSpeed);
            }
        }
    }
}
