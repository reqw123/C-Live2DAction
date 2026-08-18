using System.Collections;
using UnityEngine;
using Live2DAction.Core;

namespace Live2DAction.Combat
{
    // 2026-08-18, explicit user request ("將這個動作作為所有角色死亡時的共同動作" - use this
    // (Dying.fbx) as the shared death animation for every character). Drives the Animator's
    // "Dead" trigger from Health.Died, same wiring pattern as StaggerAnimationLink, then holds
    // the GameObject active for exactly as long as the Dying clip takes to play before
    // deactivating it itself - Health.ApplyDamage used to deactivate synchronously in the same
    // call that fires Died, which never gave any listener a chance to actually render a death
    // animation (see Health.deferDeactivationToDeathAnimation's own comment, which this
    // component requires be set true on the same GameObject for its own deactivation to be the
    // one that actually happens).
    //
    // Only wired onto characters with a compatible Humanoid rig (Player/Player3/Player4, sharing
    // Maya's/Arisa's Animator Controllers - see SpecialMoveAnimatorSetup's own "Dead" comment).
    // Player2 (no Animator at all currently) and 076/077 (Live2D Cubism billboards, an entirely
    // different animation system) keep Health's original immediate-deactivation behavior instead
    // - deferDeactivationToDeathAnimation stays false for them, so there's no risk of a
    // now-permanently-active corpse if this component is simply absent.
    //
    // A Coroutine on the character's own GameObject is safe here specifically because its last
    // action IS deactivating that same GameObject - unlike RespawnController (which has to live
    // off-character because it needs to come back and REACTIVATE something already deactivated),
    // this coroutine never needs to run again after the line that turns itself off.
    [RequireComponent(typeof(Health))]
    public class DeathAnimationLink : MonoBehaviour
    {
        [SerializeField] private Animator animator;

        // 2026-08-18: Dying.fbx measured at 3.4667s (30fps, 104 frames) via
        // AnimationClip.length right after import - not read from the clip at runtime, same
        // "measure once, hardcode, comment why" precedent as ExecutionAbility's own
        // executionAnimationSeconds field.
        [SerializeField] private float deathAnimationSeconds = 3.5f;

        private Health _health;
        private static readonly int DeadTrigger = Animator.StringToHash("Dead");

        private void Awake()
        {
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            _health.Died -= OnDied;
        }

        private void OnDied()
        {
            if (animator != null)
            {
                animator.SetTrigger(DeadTrigger);
            }

            StartCoroutine(DeactivateAfterDelay());
        }

        private IEnumerator DeactivateAfterDelay()
        {
            yield return new WaitForSeconds(deathAnimationSeconds);
            gameObject.SetActive(false);
        }
    }
}
