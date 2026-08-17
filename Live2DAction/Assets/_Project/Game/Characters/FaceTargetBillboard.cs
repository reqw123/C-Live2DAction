using UnityEngine;

namespace Live2DAction.Characters
{
    // 2026-08-18, real bug report ("077和玩家的外觀上似乎有顯示不完整問題" - the 077 half of it).
    // Root cause: 077 is a flat Live2D/Cubism standee with NO facing behavior at all. Its own
    // CubismBillboard (which at least kept it facing the camera) was removed earlier this
    // session on explicit request ("如裹077也有的話也移除") - but unlike 076, which got
    // EnemyAI.alwaysFaceTarget as a proper replacement (統一面對玩家), 077 has no EnemyAI/combat
    // of its own to hang that logic off of, so it was left with literally no substitute and has
    // been sitting at whatever rotation it happened to be placed at ever since. A flat plane
    // viewed from anywhere but face-on is nearly invisible - confirmed by screenshotting it from
    // the side, where it vanishes to a sliver - which reads exactly as "incomplete/broken" the
    // instant the player walks past instead of straight at it.
    //
    // Minimal standalone version of EnemyAI's own always-face-target rotation block, for purely
    // decorative standees that have no AI/CharacterController to attach that logic to.
    public class FaceTargetBillboard : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float rotationSpeedDegrees = 480f;

        private void Update()
        {
            if (target == null)
            {
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Quaternion targetRotation = Quaternion.LookRotation(toTarget, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, rotationSpeedDegrees * Time.deltaTime);
        }
    }
}
