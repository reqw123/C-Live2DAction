using UnityEngine;

namespace Live2DAction.AI.Boss
{
    // 2026-08-29, user request ("這兩招應該都是有位移的 目前沒看到") - PW2_LeapSmash / PW2_ChargeSlam
    // are Meshy travel-attacks whose forward motion lives in the clip's root curve. BossStateMachine
    // already knows how to feed Animator.deltaPosition into its own CharacterController.Move (see
    // ApplyMotion / BossAttackDefinition.useRootMotion), but Unity only *computes* avatar root
    // motion when the Animator's own GameObject either has applyRootMotion on OR defines
    // OnAnimatorMove. This tiny relay does both: turns applyRootMotion on, and defines an empty
    // OnAnimatorMove so Unity does NOT auto-apply the motion to this (child) transform - it just
    // makes animator.deltaPosition valid for BossStateMachine on the parent to read and apply to
    // the real capsule. Sits on the boss's "Visual" child (where the Animator is), harmless for
    // every non-root-motion state since nothing reads deltaPosition unless the current attack
    // opted in.
    [RequireComponent(typeof(Animator))]
    [DisallowMultipleComponent]
    public class BossAnimatorRootMotionRelay : MonoBehaviour
    {
        private void Awake()
        {
            GetComponent<Animator>().applyRootMotion = true;
        }

        // Intentionally empty - see class comment. Its mere presence is what makes
        // Animator.deltaPosition / deltaRotation populated while suppressing auto-application.
        private void OnAnimatorMove()
        {
        }
    }
}
