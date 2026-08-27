using UnityEngine;

namespace Live2DAction.Targeting
{
    // Marker component for anything the player can lock onto. AimPoint lets a target expose
    // a specific look-at point (e.g. chest height) instead of its root transform; defaults to
    // the root when unset.
    public class LockOnTarget : MonoBehaviour
    {
        [SerializeField] private Transform aimPoint;

        // 2026-08-26, explicit user request ("隻狼那種3d動作中,玩家小體積面對boss大體積的視角") - lets
        // a large target (a boss like 武士/Wushi) ask the camera to pull back further than the
        // player's own hands-on-tuned base `distance` (see ThirdPersonCameraController.distance's
        // own comment - never overwritten, only multiplied, same pattern as flightDistanceMultiplier)
        // so a giant target still fits in frame once locked on. 1 = no change (default, every
        // existing LockOnTarget - regular enemies - keeps today's exact behavior).
        [Tooltip("Multiplies ThirdPersonCameraController's base lock-on distance while THIS target " +
                 "is locked - use >1 for large bosses so they fit in frame. 1 = no change.")]
        [SerializeField] private float cameraDistanceMultiplier = 1f;

        // 2026-08-26, explicit user request ("把攝影機視角拉到足以讓玩家在畫面邊界但有看的到身體的
        // 位置 然後讓boss佔整個螢幕的大部分畫面") - Shadow of the Colossus/Sekiro-style big-boss
        // framing: shifts the camera's look-at point a FRACTION of the way from the player toward
        // this target's AimPoint (see ThirdPersonCameraController's own lock-on distance block,
        // right above where this is read), instead of looking straight at the player. 0 (default)
        // = today's exact behavior for every existing LockOnTarget (regular enemies); ~0.3-0.4 =
        // player pushed toward frame edge while the boss dominates. Deliberately a blend factor,
        // not "look straight at the boss" (1.0 would lose the player entirely) - "但有看的到身體".
        [Tooltip("0-1: how far the lock-on look-at point shifts from the player toward this target's " +
                 "AimPoint. 0 = look at player (default/current behavior). ~0.3-0.4 = big-boss framing " +
                 "(player toward frame edge, boss dominates). Avoid 1.0 - the player would leave frame " +
                 "entirely (2026-08-26: raised the slider cap from 0.6 to 0.95 per explicit user request, " +
                 "still stopping short of 1.0 for that reason).")]
        [SerializeField, Range(0f, 0.95f)] private float cameraFrameBias = 0f;

        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
        public float CameraDistanceMultiplier => cameraDistanceMultiplier;
        public float CameraFrameBias => cameraFrameBias;
    }
}
