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

        // 2026-08-28, explicit user request ("如何像隻狼那樣的動作遊戲，畫面中同時能看到玩家與大型boss，
        // 並且以玩家視角能夠清楚看到雙方動作...要從玩家與武士的體積來看") - when set, the camera runs a
        // dedicated locked-duel framing while THIS target is locked (see
        // ThirdPersonCameraController.UpdateDuelCamera): it computes yaw/pitch/distance/look-at
        // point each frame from the player's and this target's real volumes so BOTH stay framed
        // and readable, instead of the crude fixed cameraDistanceMultiplier/cameraFrameBias above.
        // Those two are left as the fallback for regular (non-boss) targets that don't set this.
        [Tooltip("Big-boss locked-duel camera: computes framing from both volumes so player + boss " +
                 "stay on screen and readable. Overrides cameraDistanceMultiplier/cameraFrameBias " +
                 "for this target. Leave off for regular enemies.")]
        [SerializeField] private bool useDuelCamera = false;

        [Tooltip("This target's full standing height in world units (feet to top of head) - the " +
                 "duel camera uses it to fit the boss in frame. 0 (or negative) = auto-measure from " +
                 "renderer bounds at Awake, so a new boss needs no manual number. Set an explicit " +
                 "value to override (武士 = 4.1) when the auto measure is off - e.g. a boss whose " +
                 "SkinnedMeshRenderer bounds run large, or one that spends its idle pose crouched.")]
        [SerializeField] private float duelTargetHeight = 0f;

        // Cached auto-measured height, only computed (once, lazily) when duelTargetHeight <= 0.
        private float _autoDuelHeight = -1f;

        public Transform AimPoint => aimPoint != null ? aimPoint : transform;
        public float CameraDistanceMultiplier => cameraDistanceMultiplier;
        public float CameraFrameBias => cameraFrameBias;
        public bool UseDuelCamera => useDuelCamera;

        public float DuelTargetHeight
        {
            get
            {
                if (duelTargetHeight > 0.1f)
                {
                    return duelTargetHeight;
                }
                if (_autoDuelHeight < 0f)
                {
                    _autoDuelHeight = MeasureHeight();
                }
                return _autoDuelHeight;
            }
        }

        // Renderer-bounds union along Y. Sanity-clamped so a degenerate/stale SkinnedMeshRenderer
        // bounds (a known hazard in this project - see Docs/KNOWN_ISSUES.md) can't feed an absurd
        // number into the camera framing; a clearly-bad measure falls back to 2.
        private float MeasureHeight()
        {
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return 2f;
            }
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                b.Encapsulate(renderers[i].bounds);
            }
            float h = b.size.y;
            return (h < 0.3f || h > 60f) ? 2f : h;
        }
    }
}
