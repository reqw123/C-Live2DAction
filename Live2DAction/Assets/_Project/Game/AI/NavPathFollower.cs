using UnityEngine;
using UnityEngine.AI;

namespace Live2DAction.AI
{
    // 2026-08-31, user request ("所有角色在移動時有可能會被地圖物件擋住路線從卡住 有沒有演算法可以避
    // 開這個問題" -> picked "AI: NavMesh 路徑跟隨"). A drop-in steering helper for the AI classes that
    // already drive their own CharacterController. EnemyAI and BossStateMachine.MoveTowardTarget
    // both pushed straight at the target - `(target - self).normalized * speed` - which just
    // presses into any wall / pillar / building between them and the player, and the
    // CharacterController stops the mover dead ("被地圖物件擋住路線卡住"). This asks the baked
    // NavMesh for a path and hands back the direction toward the next path corner instead.
    //
    // The movement code is otherwise untouched: the caller still turns this unit direction into a
    // horizontal velocity and threads it through its own _controller.Move(), so gravity, grounding,
    // knockback, slope-slide, root-motion attacks and aerial combat all behave byte-for-byte as
    // before. Deliberately NOT a NavMeshAgent - this project has exactly one movement system
    // (CharacterController) and this does not add a second (BossStateMachine's own class comment
    // makes the same "no NavMeshAgent anywhere" point).
    //
    // Fail-open in every degenerate case - no NavMesh baked under the character, the target off the
    // mesh (player mid-jump / outside a baked area), or an unreachable target - all fall back to
    // the raw straight-line direction, i.e. exactly today's behaviour. An AI with this component
    // but no baked mesh is never worse off than one without it.
    [DisallowMultipleComponent]
    public class NavPathFollower : MonoBehaviour
    {
        [Tooltip("Seconds between path recomputes. Also recomputes early when the target moves " +
                 "further than 'Repath If Target Moved By' from where the current path was aimed.")]
        [SerializeField] private float repathInterval = 0.3f;

        [SerializeField] private float repathIfTargetMovedBy = 1.5f;

        [Tooltip("Horizontal distance to a path corner at which the follower advances to the next one.")]
        [SerializeField] private float cornerReachDistance = 0.75f;

        [Tooltip("Radius used to snap this character / the target onto the NavMesh when querying a " +
                 "path. Keep comfortably above the character's own world radius so a slightly-off " +
                 "capsule still finds the mesh.")]
        [SerializeField] private float navSampleRadius = 2.5f;

        // Created in Awake, not as a field initializer - NavMeshPath's ctor is not allowed to run
        // from a MonoBehaviour field initializer.
        private NavMeshPath _path;
        private float _nextRepathTime;
        private Vector3 _pathAimedAt;
        private bool _hasUsablePath;
        private int _corner;

        private void Awake()
        {
            _path = new NavMeshPath();
        }

        // True while the last query produced a real multi-corner detour (i.e. the follower is
        // actively routing around something). Handy for a caller that wants to e.g. not start an
        // attack while still pathing around a wall - purely informational, no one has to read it.
        public bool IsDetouring => _hasUsablePath && _path.corners.Length > 2;

        // The one entry point. Returns a horizontal unit direction to move THIS frame to head
        // toward worldTargetPos, routed around NavMesh obstacles. Vector3.zero when already
        // essentially on top of the target / the current corner (caller should treat that as
        // "stop", same as a zeroed straight-line direction).
        public Vector3 SteeringDirection(Vector3 worldTargetPos)
        {
            MaybeRepath(worldTargetPos);

            Vector3 self = transform.position;
            Vector3 aim;
            if (_hasUsablePath && _path.corners.Length >= 2)
            {
                _corner = AdvanceCorner(_path.corners, self, _corner, cornerReachDistance);
                aim = _path.corners[_corner];
            }
            else
            {
                aim = worldTargetPos; // fail-open: straight at the target, today's behaviour
            }

            Vector3 to = aim - self;
            to.y = 0f;
            return to.sqrMagnitude > 0.0001f ? to.normalized : Vector3.zero;
        }

        private void MaybeRepath(Vector3 worldTargetPos)
        {
            bool due = Time.time >= _nextRepathTime;
            bool targetMoved = (worldTargetPos - _pathAimedAt).sqrMagnitude
                               > repathIfTargetMovedBy * repathIfTargetMovedBy;
            if (!due && !targetMoved && _hasUsablePath)
            {
                return;
            }

            _nextRepathTime = Time.time + repathInterval;
            _pathAimedAt = worldTargetPos;
            _hasUsablePath = TryComputePath(transform.position, worldTargetPos);
            // corners[0] is always ~= the start point; steer toward [1] first.
            _corner = 1;
        }

        private bool TryComputePath(Vector3 from, Vector3 to)
        {
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            if (!NavMesh.SamplePosition(from, out NavMeshHit fromHit, navSampleRadius, NavMesh.AllAreas))
            {
                return false; // no mesh under us at all - nothing to route with
            }

            // A target off the mesh (player airborne / outside a baked patch) still gives a useful
            // path to the nearest reachable point; only its exact endpoint is snapped.
            Vector3 toOnMesh = NavMesh.SamplePosition(to, out NavMeshHit toHit, navSampleRadius, NavMesh.AllAreas)
                ? toHit.position
                : fromHit.position;

            if (!NavMesh.CalculatePath(fromHit.position, toOnMesh, NavMesh.AllAreas, _path))
            {
                return false;
            }

            // PathPartial is fine - walk as far along it as we can, the leftover gap falls back to
            // straight-line on the next query once we're closer. PathInvalid is not usable.
            return _path.status != NavMeshPathStatus.PathInvalid && _path.corners.Length >= 2;
        }

        // ---- pure, EditMode-testable ----

        // Given the path corners, the character's position and the corner it is currently steering
        // toward, return the corner it should steer toward now: skip past any leading corners that
        // are already within reachDistance (horizontally), never past the last one.
        public static int AdvanceCorner(Vector3[] corners, Vector3 self, int current, float reachDistance)
        {
            if (corners == null || corners.Length == 0)
            {
                return 0;
            }
            int i = Mathf.Clamp(current, 0, corners.Length - 1);
            while (i < corners.Length - 1 && HorizontalDistance(self, corners[i]) <= reachDistance)
            {
                i++;
            }
            return i;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
