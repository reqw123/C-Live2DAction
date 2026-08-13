using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Characters;
using Live2DAction.Targeting;

namespace Live2DAction.CameraSystem
{
    // Free-look mouse-orbit camera (RPG-style: moving the mouse turns the camera immediately,
    // no button needs to be held), the same model this project used before a same-day
    // 2026-08-12 detour into a rigidly character-locked "right shoulder" camera (tried, then
    // reverted back to this by explicit request - "改回剛剛那樣視角可以左右上下移動...參考原神
    // 鳴潮等等" - free camera + WASD strafes relative to it, like those games). See
    // Docs/KNOWN_ISSUES.md for the earlier Cinemachine version's "camera spins in a circle"
    // bug and why it doesn't recur here: yaw/pitch live in exactly ONE place (_yaw/_pitch
    // below), driven only by mouse input, and that same state drives both the camera's own
    // rotation AND what CharacterMovement reads via YawDegrees for camera-relative movement -
    // there is no second component (like Cinemachine's separate Body/Aim, or the reverted
    // right-shoulder rig reading the character's own rotation back) that could react to the
    // character's motion and feed back into this yaw. CharacterMovement's WASD-strafes-relative-
    // to-camera + auto-turn-to-face-movement-direction scheme depends on this independence -
    // see that class's own comment for why locking camera yaw to the character's facing broke
    // it (confirmed by CameraRelativeMovementRegressionTests). Locking onto an enemy does not
    // change the camera at all (see ILockOnSource usage history in CharacterMovement) - only
    // the character's own facing turns towards the target.
    //
    // initialYaw/initialPitch only seed the starting angle; from then on the mouse drives
    // _yaw/_pitch every frame during Play. distance/targetOffset are the user's own hands-on
    // Inspector tuning (see the fields' own comments) - treat them as user-owned data, not
    // something to "fix" toward a coded default without asking first.
    //
    // [ExecuteAlways] so LateUpdate also runs in the Editor outside Play mode: without it, this
    // GameObject's Transform only ever gets repositioned while actually playing, so the Game
    // view "preview" before pressing Play shows whatever position it was last left at - not
    // where distance/targetOffset/initialYaw/initialPitch actually say it should be. Mouse
    // input is only read while Application.isPlaying (there's no meaningful mouse delta to read
    // in Edit mode), so the Edit-mode preview always shows the initial/starting angle.
    [ExecuteAlways]
    public class ThirdPersonCameraController : MonoBehaviour, ICameraYawSource
    {
        [SerializeField] private Transform target;

        // distance=2 is the user's own explicit request (2026-08-12, alongside troubleshooting
        // the "character disappears near Player4" report - see enableCameraCollision below)
        // - raised from the previous 0.8 for more breathing room. targetOffset.y=0.5 is
        // separate hands-on tuning from 2026-08-11/12, unchanged. 0 distance would be true
        // first-person (camera at the eye point, no offset) - SetOwnVisualHidden below still
        // handles that case if distance is ever tuned back to 0, but it's not the current mode.
        [SerializeField] private float distance = 2f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

        // 2026-08-12: real bug report ("很靠近敵人時角色1突然消失，畫面定格") persisted even
        // after fixing the CharacterController-climbing root cause and raising distance to 2 -
        // this project genuinely never had any camera collision avoidance (documented as a
        // known gap since 2026-08-12's earlier greybox pass), and a naive orbit camera with no
        // obstruction check WILL end up positioned inside nearby geometry (Player4's own mesh,
        // a cover block, a boundary wall) whenever the player stands close enough to something
        // - from inside a mesh, backface culling typically shows nothing/the skybox behind it,
        // which reads exactly like "the character disappeared and the screen froze" even
        // though the game is still running fine. SphereCasting from the look-at point out to
        // the desired camera position and clamping distance to whatever it hits first (see
        // FindObstructionDistance/ClampDistanceForObstruction) is the standard fix.
        [SerializeField] private bool enableCameraCollision = true;

        // Small buffer so the camera stops just short of whatever it hit instead of exactly at
        // its surface (which would still clip at the near clip plane).
        [SerializeField] private float cameraCollisionRadius = 0.2f;
        [SerializeField] private float cameraCollisionSkin = 0.15f;

        [SerializeField] private float initialYaw;
        [SerializeField] private float initialPitch;

        // 2026-08-12: Genshin/Wuthering-Waves-style optional auto-center - NOT a return to the
        // same-day right-shoulder detour this class comment describes above. That version set
        // _yaw = target.eulerAngles.y unconditionally every frame, which is what created the
        // closed feedback loop with CharacterMovement's camera-relative-strafe-then-turn-to-
        // face-it scheme (see CameraRelativeMovementRegressionTests). This is safe specifically
        // BECAUSE it isn't that: it's a slow, damped Lerp (not an instant hard assignment)
        // that's fully gated off (a) the instant any mouse delta arrives this frame and (b)
        // for autoCenterDelay seconds after the last one - so it can never compound every
        // single frame the way the reverted version did. Player rotation still only ever
        // chases camera-relative input (never the other way around); this just occasionally
        // nudges the camera's own yaw toward wherever the player has already ended up facing,
        // the same direction a player's own SmoothDampAngle turn already trends toward when
        // walking forward, so in the common case it barely does anything perceptible - it
        // mainly matters after strafing or turning around and then walking away hands-off.
        [SerializeField] private bool enableAutoCenter = true;

        // How long to wait, after the last detected mouse-look input, before auto-center may
        // start pulling the camera back behind the player.
        [SerializeField] private float autoCenterDelay = 0.8f;

        // Framerate-independent-ish exponential approach rate (Mathf.LerpAngle(yaw, target,
        // autoCenterSpeed * Time.deltaTime) every frame - not a literal degrees/second constant
        // rate, despite the name; this is the same idiom the request that added this feature
        // specified). Bigger = snappier recenter. A reasoned starting point (roughly settles
        // within ~1.5s), not yet confirmed by eye.
        [SerializeField] private float autoCenterSpeed = 2f;

        // Optional: auto-center must defer to lock-on rather than fight it (locking an enemy
        // doesn't move the camera at all - see the class comment - so simply not auto-centering
        // while locked is enough to not conflict with it, rather than needing to coordinate
        // with TargetLockController's own facing logic).
        [SerializeField] private MonoBehaviour lockOnSource;

        // Degrees of rotation per pixel of mouse movement. Mouse.delta is already a per-frame
        // pixel delta (not a per-second rate), so this must NOT also be scaled by
        // Time.deltaTime - an earlier version did that by mistake, which silently divided the
        // effective sensitivity by ~1/frameRate (at 60fps, roughly 60x too small) and was
        // reported as "mouse-look barely moves the camera at all".
        [SerializeField] private float mouseSensitivity = 0.15f;

        // Clamped well short of straight up/down - a third-person camera flipping past vertical
        // loses its sense of "behind the character" and can gimbal-lock-feel disorienting.
        [SerializeField] private float minPitch = -40f;
        [SerializeField] private float maxPitch = 70f;

        private float _yaw;
        private float _pitch;

        // Built lazily on first LateUpdate rather than in Awake/OnEnable, same reasoning as
        // CharacterMovement/PlayerCombat's lazy-init fields: tests assign initialYaw/
        // initialPitch via reflection right after AddComponent, which already runs
        // Awake/OnEnable synchronously.
        private bool _initialized;

        // Seconds since the last frame with a nonzero mouse delta - drives the auto-center
        // gate below. Starts at a large value so a scene that never receives mouse input
        // doesn't need to "wait out" the delay from zero.
        private float _timeSinceLookInput = 999f;

        // Resolved lazily (like CharacterMovement/TargetLockController's own
        // reflection-friendly properties) rather than in Awake, and re-resolved if target
        // changes - only used for the optional auto-center's "is the player actually moving"
        // check.
        private CharacterMovement _targetMovement;
        private Transform _targetMovementFor;

        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;

        public float YawDegrees => _yaw;

        // Without confining the cursor, Mouse.delta keeps reporting real physical mouse
        // movement regardless of where the OS cursor visually is - any mouse use that isn't
        // "looking around" (moving toward a taskbar, a second monitor, just cursor jitter) still
        // feeds into _yaw/_pitch and reads as the view slowly drifting on its own. Locking the
        // cursor during Play makes 100% of mouse movement go towards look input as intended.
        private void OnEnable()
        {
            if (Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (Application.isPlaying)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                return;
            }

            if (!_initialized)
            {
                _yaw = initialYaw;
                _pitch = initialPitch;
                _initialized = true;
            }

            if (Application.isPlaying)
            {
                // OnEnable only locks the cursor once, at Play start. Losing that lock later
                // (pressing Escape, the Game view losing OS focus after Alt-Tab, or Play
                // starting before the Game view ever had focus in the first place - all
                // reported as "mouse-look stopped working"/"cursor wanders off the window")
                // otherwise leaves it unlocked forever, since nothing else re-requests it.
                // Re-locking on the next click is the standard fix: it re-engages as soon as
                // the player clicks back into the window, without permanently trapping the
                // cursor (Escape still works as an explicit "let me out" while testing).
                if (Cursor.lockState != CursorLockMode.Locked && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }

                Vector2 delta = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
                bool isLooking = delta.sqrMagnitude > 0.0001f;
                _timeSinceLookInput = isLooking ? 0f : _timeSinceLookInput + Time.deltaTime;

                _yaw += delta.x * mouseSensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * mouseSensitivity, minPitch, maxPitch);

                // Auto-center: see the field's own comment for why this can't reintroduce the
                // reverted right-shoulder rig's feedback loop. isLooking already cedes control
                // back the instant the mouse moves again (checked every frame, not just when
                // the delay first elapses), satisfying "玩家一有滑鼠輸入，自動置中要立刻停止".
                if (enableAutoCenter && !isLooking && _timeSinceLookInput >= autoCenterDelay && IsPlayerWalkingForwardOrBack() && LockOnSource?.LockedTarget == null)
                {
                    _yaw = ComputeAutoCenterYaw(_yaw, target.eulerAngles.y, autoCenterSpeed, Time.deltaTime);
                }
            }

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            float usedDistance = distance;

            // Physics.SphereCast needs the live scene, so this can't live in the pure
            // ComputeCameraPosition helper below - it only runs while actually playing, same
            // as the mouse-look block above (there's nothing meaningful to collide against
            // while just previewing the starting angle in Edit mode, and Physics queries
            // against a non-playing scene's colliders aren't reliable anyway).
            if (Application.isPlaying && enableCameraCollision)
            {
                Vector3 lookAtPoint = target.position + targetOffset;
                float? obstruction = FindObstructionDistance(lookAtPoint, rotation, distance);
                usedDistance = ClampDistanceForObstruction(distance, obstruction, cameraCollisionSkin);
            }

            Vector3 position = ComputeCameraPosition(target.position, rotation, usedDistance, targetOffset);

            transform.SetPositionAndRotation(position, rotation);

            SetOwnVisualHidden(Mathf.Approximately(distance, 0f));
        }

        // SphereCasts from the look-at point (roughly the character's head/chest) out toward
        // where the camera would naively sit, and returns the distance to the first thing hit
        // that isn't part of the target's own hierarchy (so the character's own body doesn't
        // immediately "obstruct" its own camera). Null means nothing in the way.
        private float? FindObstructionDistance(Vector3 lookAtPoint, Quaternion rotation, float desiredDistance)
        {
            if (desiredDistance <= 0.0001f)
            {
                return null;
            }

            Vector3 direction = -(rotation * Vector3.forward);
            RaycastHit[] hits = Physics.SphereCastAll(lookAtPoint, cameraCollisionRadius, direction, desiredDistance, ~0, QueryTriggerInteraction.Ignore);

            float? closest = null;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.root == target)
                {
                    continue;
                }

                if (!closest.HasValue || hit.distance < closest.Value)
                {
                    closest = hit.distance;
                }
            }

            return closest;
        }

        // Pure so the clamping arithmetic is directly EditMode-testable without a live physics
        // scene - FindObstructionDistance above is what actually queries Physics.
        public static float ClampDistanceForObstruction(float desiredDistance, float? obstructionDistance, float skin)
        {
            if (!obstructionDistance.HasValue)
            {
                return desiredDistance;
            }

            return Mathf.Clamp(obstructionDistance.Value - skin, 0f, desiredDistance);
        }

        // Gates auto-center on "walking forward/back", not "strafing" - see the
        // enableAutoCenter field comment. A held pure-strafe measurably drifted the
        // character's facing under auto-center (134 degrees over 1.5s, confirmed by
        // CameraRelativeMovementRegressionTests): the character's facing there is itself
        // still chasing a camera-relative strafe target, so easing the camera toward that
        // still-moving facing converges far slower than it does for forward walking, where
        // the character's own SmoothDampAngle turn already settles onto the camera's
        // direction quickly on its own. Requires both actual translation (CurrentHorizontalSpeed,
        // not just a held key against a wall) and a forward/back-dominant input axis.
        private bool IsPlayerWalkingForwardOrBack()
        {
            if (_targetMovement == null || _targetMovementFor != target)
            {
                _targetMovement = target != null ? target.GetComponent<CharacterMovement>() : null;
                _targetMovementFor = target;
            }

            if (_targetMovement == null || _targetMovement.CurrentHorizontalSpeed <= 0.05f)
            {
                return false;
            }

            Vector2 moveInput = _targetMovement.CurrentMoveInput;
            return Mathf.Abs(moveInput.y) >= Mathf.Abs(moveInput.x);
        }

        // Pure so the auto-center easing itself is directly EditMode-testable, same reasoning
        // as ComputeCameraPosition/CameraRelativeDirection - the gating conditions (isLooking,
        // delay, moving, lock-on) live in LateUpdate since they need live component state.
        public static float ComputeAutoCenterYaw(float currentYaw, float targetYaw, float autoCenterSpeed, float deltaTime)
        {
            return Mathf.LerpAngle(currentYaw, targetYaw, autoCenterSpeed * deltaTime);
        }

        // Pure so the positioning math can be verified directly in EditMode tests without a
        // live scene or Play loop.
        public static Vector3 ComputeCameraPosition(Vector3 targetPosition, Quaternion rotation, float distance, Vector3 targetOffset)
        {
            Vector3 lookAtPoint = targetPosition + targetOffset;
            return lookAtPoint - rotation * Vector3.forward * distance;
        }

        // At distance=0 the camera sits exactly at the eye point, inside the character's own
        // head mesh - without this, true first-person would render the inside of Maya's head
        // rather than the world. Only disables Renderers (not the GameObject itself, via
        // SetActive) so the Animator underneath keeps running - disabling the whole visual was
        // tried once before for a different reason and caused CharacterAnimatorLink to spam
        // warnings calling SetFloat on a disabled Animator (see Docs/KNOWN_ISSUES.md), which
        // this sidesteps entirely. Not the current mode (distance=0.8), kept as a no-op safety
        // net in case distance is ever tuned back to 0. "Visual" matches the child name every
        // PlayerXVisualSetup.cs script uses for the swapped-in character model.
        private void SetOwnVisualHidden(bool hidden)
        {
            Transform visual = target.Find("Visual");
            if (visual == null)
            {
                return;
            }

            foreach (Renderer visualRenderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                visualRenderer.enabled = !hidden;
            }
        }
    }
}
