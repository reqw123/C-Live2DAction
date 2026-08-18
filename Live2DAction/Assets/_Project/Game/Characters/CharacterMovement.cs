using UnityEngine;
using Live2DAction.CameraSystem;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour inputSource;

        // Optional: a yaw driven only by explicit mouse-look input (see ICameraYawSource /
        // ThirdPersonCameraController - this must not be the camera's fully-composed
        // Transform.forward). Falls back to Camera.main's yaw if unassigned, for tests that
        // don't set up a real camera. 2026-08-12: reinstated after a same-day detour into
        // tank controls (A/D turn in place) paired with a camera rigidly locked to the
        // character's own facing - reverted back to this camera-relative-strafe scheme by
        // explicit request ("改回剛剛那樣...參考原神鳴潮等等"). The critical invariant this
        // depends on: cameraYawSource must be driven independently of this component's own
        // rotation (mouse input, not read back from the character) - see
        // ThirdPersonCameraController's class comment and
        // CameraRelativeMovementRegressionTests for what breaks if that's ever violated again
        // (the character spins in a continuous circle on any pure-strafe input).
        [SerializeField] private MonoBehaviour cameraYawSource;

        // Matches the top threshold of Maya's Locomotion blend tree (CharacterAnimatorLink)
        // so translation speed and the Run clip's authored pace line up - a mismatch here
        // is what caused the reported foot-sliding, since these clips have no root motion
        // to derive the "correct" speed from and must be tuned by eye instead.
        [SerializeField] private float moveSpeed = 2f;

        // Eased (SmoothDamp/SmoothDampAngle) rather than constant-rate (MoveTowards/
        // RotateTowards): a constant rate accelerates linearly and then cuts off the instant
        // it reaches the target, which reads as mechanical - reported as "movement doesn't
        // feel natural". SmoothDamp approaches the target asymptotically, giving the
        // character a bit of weight both starting and stopping, and is the standard
        // technique third-person controllers use for natural turning without a dedicated
        // turn-in-place animation (see Docs/Research/CAMERA_MOVEMENT_RESEARCH.md). Smaller
        // values are snappier; these are reasonable starting guesses tuned by eye, not
        // derived from any authored animation data (same caveat as moveSpeed below).
        [SerializeField] private float accelerationSmoothTime = 0.08f;

        // Lowered from 0.12s after "releasing the move key doesn't stop the character right
        // away" was reported - 0.12s (deliberately slower than acceleration, for a bit of
        // trailing weight on stopping) read as too much coast/slide once the character was
        // actually being played. Still eased, not an instant MoveTowards-style stop (that was
        // the "movement doesn't feel natural" complaint this smoothing originally fixed) - just
        // eased fast enough that the coast is barely noticeable instead of a deliberate feature.
        [SerializeField] private float decelerationSmoothTime = 0.05f;
        [SerializeField] private float rotationSmoothTime = 0.1f;
        [SerializeField] private float gravity = -20f;

        // sqrt(2 * |gravity| * desired peak height) would give an exact peak height, but
        // there's no specific target height requested - this is a reasonable starting guess
        // (roughly a 1.5-2 unit hop at gravity=-20), tune by eye.
        [SerializeField] private float jumpSpeed = 7f;

        // 2026-08-16, real bug report ("跳躍有機會卡在敵人頭上，需要自行下來") - see
        // GroundSlopeUtility's own comment for the root cause. Faster than moveSpeed (2) so a
        // stuck landing resolves itself quickly and reads as "sliding off", not another slow
        // walk-speed crawl.
        [SerializeField] private float slideSpeed = 4f;

        [SerializeField] private DodgeData dodgeData;

        // Optional: while this reports a locked target, the character always faces it
        // (unless dodging) instead of the movement direction, so attacks aim at the target
        // even while strafing around it or standing still.
        [SerializeField] private MonoBehaviour lockOnSource;

        // Optional: kept in sync with IsDodgeInvulnerable every frame so dodging actually
        // avoids damage, not just an inert flag nothing consumes.
        [SerializeField] private Health health;

        // 2026-08-17, explicit user request ("敵我雙方都套用架式條") - optional (null-safe
        // below) so a character with no stance bar at all behaves exactly as before. Mirrors
        // EnemyAI's own "stance" field/comment for the enemy side of the same mechanic - while
        // staggered, move/dodge/jump INPUT is zeroed out (not the whole component disabled),
        // same reasoning as EnemyAI: gravity/grounding/the character-slide-off-another-character
        // safety net all still need to keep running every frame regardless, only the player's
        // own control inputs should stop mattering.
        [SerializeField] private Live2DAction.Combat.StancePoise stance;

        // 2026-08-18, explicit user request ("接下來我想做飛行功能...按住鍵自由飛行") - reuses
        // UltimateEnergy as a generic regen-over-time resource pool (it was already written
        // generically despite the name - see that class's own header comment) rather than a
        // dedicated FlightEnergy class; this is a SEPARATE instance/asset from the ultimate
        // skill's own energy, wired independently. Optional (null-safe below) - flight simply
        // never activates without one wired.
        [SerializeField] private UltimateEnergy flightEnergy;
        [SerializeField] private float flightAscendSpeed = 6f;
        [SerializeField] private float flightDescendSpeed = 4f;
        [SerializeField] private float flightEnergyDrainPerSecond = 20f;

        // 2026-08-18, explicit user request (flight system grilling session - see CONTEXT.md's
        // own "Glide" entry) - the fallback state when Flight Energy runs out mid-air. A soft,
        // fixed-rate descent instead of Flight just handing control back to normal gravity (which
        // at that point would usually mean falling from a serious height) - echoes Wuthering
        // Waves' "drop back to glider, not a hard fall" behavior. Costs no energy (flightEnergy
        // regenerates normally while gliding, same passive regen it already has), and horizontal
        // movement stays fully controllable - only the vertical rate changes from Flight's.
        [SerializeField] private float glideDescendSpeed = 2f;

        // 2026-08-18, real playtested bug ("飛行有能耗 非一半就停下來導致飛行軌跡奇異") - Glide
        // originally resumed Flight the instant CurrentEnergy > 0f while the key was still held.
        // With this project's actual tuning (drain 20/sec, regen only 10/sec) that meant once
        // energy hit zero, holding the key produced a rapid stutter instead of a clean Glide:
        // regen ticks 10 energy back in, Flight instantly resumes, drains it back to 0 in just
        // 0.5s, drops back to Glide, repeat - a visible up/down "hop-glide-hop-glide" sawtooth
        // rather than a real recovery. Requiring a real reserve (not just >0) before Flight can
        // resume gives Glide enough uninterrupted time to actually read as its own state.
        [SerializeField] private float flightResumeEnergyThreshold = 30f;

        // 2026-08-18, explicit user request (aerial combat grilling session, Q3/Q5) - until now
        // this class only ever rotated Yaw (Quaternion.Euler(0, yaw, 0)) - fine for every
        // ground fight so far since nothing was ever meaningfully above/below the player, but a
        // locked-on aerial target needs the character to actually tip its head/body up or down
        // to face it, or its attack capsule (which extends along attackOrigin.forward) would
        // still only ever reach horizontally. Clamped to +/-maxPitchDegrees so a target directly
        // overhead doesn't contort the character toward looking straight up - see AimUtility's
        // own comment for the shared clamping math (EnemyAI uses the same utility for Player4's
        // side of the same problem).
        [SerializeField] private float maxPitchDegrees = 60f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private bool _isFlying;
        private bool _isGliding;
        private float _pitch;
        private float _pitchAngularVelocity;
        private float _desiredPitchDegrees;

        // 2026-08-18, REVERTED same day from applying _pitch to the root transform.rotation -
        // real playtested bug on EnemyAI's identical setup (see that class's own comment for the
        // full story): CharacterController's capsule "up" axis follows the transform's own local
        // Y axis, so pitching the CharacterController's own transform doesn't just turn the
        // character's aim, it physically tips the collision capsule over, fighting vertical
        // movement and reading as the body flickering between standing and lying flat. Cached
        // here instead so pitch can be applied to the "Visual" child (which only holds the
        // Animator, not the CharacterController) - same visual look-up cue, no capsule tilt.
        private Transform _visual;

        // SmoothDamp's own internal "current rate of change" state - not the same value as
        // _horizontalVelocity itself. Reset to zero whenever a dodge takes over so the eased
        // ramp doesn't inherit a stale rate once normal movement resumes.
        private Vector3 _horizontalVelocitySmoothDampRef;
        private float _verticalVelocity;

        // SmoothDampAngle's internal angular-velocity state, mirroring _horizontalVelocitySmoothDampRef above.
        private float _yawAngularVelocity;
        private DodgeState _dodgeState;

        // Resolved on every use rather than cached in Awake(), so assigning inputSource
        // after the component has already Awoken (e.g. from a test) still takes effect.
        private IInputCommand InputCommand => inputSource as IInputCommand;
        private ICameraYawSource CameraYawSource => cameraYawSource as ICameraYawSource;
        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;

        public float MoveSpeed => moveSpeed;
        public float CurrentHorizontalSpeed => _horizontalVelocity.magnitude;
        public DodgePhase CurrentDodgePhase => _dodgeState != null ? _dodgeState.Phase : DodgePhase.Idle;
        public bool IsDodgeInvulnerable => _dodgeState != null && _dodgeState.IsInvulnerable;

        // Exposed for CharacterAnimatorLink (drives the Animator's existing but previously-
        // unused "Fly" bool) and for a wing visual to toggle itself on/off. Deliberately NOT
        // true while Gliding - WingFlap's own idle/flying two-tier flap rate treats Gliding as
        // "not flying" on purpose (see glideDescendSpeed's own comment: gliding should read as a
        // gentle flap, matching the idle rate, not the energetic flying one).
        public bool IsFlying => _isFlying;

        // See glideDescendSpeed's own comment for what Glide is and why it exists.
        public bool IsGliding => _isGliding;

        // 2026-08-18, explicit user request ("上升氣流，任何人碰到...會快速飛向空中") - lets an
        // external trigger volume (Updraft) push this character upward without fighting its own
        // gravity accumulation. Mathf.Max rather than a flat assignment/addition: Updraft calls
        // this every physics tick for as long as the character overlaps it, so Max just keeps
        // re-clamping the velocity back up to at least `speed` each tick (countering however
        // much gravity ate into it since the last tick) instead of stacking additively into an
        // ever-growing value, or (if called just once on trigger-enter) getting silently
        // overwritten by this class's own `_verticalVelocity += gravity * Time.deltaTime` on
        // every subsequent frame before the character ever leaves the volume.
        public void ApplyUpwardLaunch(float speed)
        {
            _verticalVelocity = Mathf.Max(_verticalVelocity, speed);
        }

        // Raw camera-relative input axes this frame (y = W/S, x = A/D), not the resulting
        // world-space direction - exposed so ThirdPersonCameraController's auto-center can
        // tell "walking forward/back" apart from "strafing sideways" (see that class's field
        // comment: auto-centering during a held pure-strafe measurably drifted the character's
        // facing, confirmed by CameraRelativeMovementRegressionTests, because the camera
        // easing toward a facing that's itself still chasing a camera-relative strafe target
        // converges far slower than walking forward does).
        public Vector2 CurrentMoveInput { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _visual = transform.Find("Visual");
        }

        private void Update()
        {
            // Built lazily rather than in Awake, same reasoning as PlayerCombat's
            // ComboAttackState: tests assign dodgeData via reflection right after
            // AddComponent, which already runs Awake synchronously.
            if (_dodgeState == null)
            {
                _dodgeState = new DodgeState(dodgeData);
            }

            bool staggered = stance != null && stance.IsStaggered;

            IInputCommand inputCommand = InputCommand;
            Vector2 moveInput = !staggered && inputCommand != null ? inputCommand.MoveInput : Vector2.zero;
            CurrentMoveInput = moveInput;
            bool dodgePressed = !staggered && inputCommand != null && inputCommand.DodgePressed;
            bool jumpPressed = !staggered && inputCommand != null && inputCommand.JumpPressed;
            Vector3 desiredDirection = CameraRelativeDirection(moveInput, CurrentCameraYawDegrees());

            // Dodge backward (relative to current facing) if there's no move input held,
            // matching the common "backstep" convention when dodging from a standstill.
            Vector3 dodgeDirectionIfStarting = desiredDirection.sqrMagnitude > 0.0001f ? desiredDirection : -transform.forward;
            Vector3 dodgeVelocity = _dodgeState.Tick(Time.deltaTime, dodgePressed, dodgeDirectionIfStarting);

            if (health != null)
            {
                health.IsInvulnerable = _dodgeState.IsInvulnerable;
            }

            Vector3 facingDirection;
            if (_dodgeState.Phase == DodgePhase.Dodging)
            {
                // A dodge commits to its locked-in direction and speed for its whole
                // duration - it overrides normal eased movement entirely rather than
                // blending with it.
                _horizontalVelocity = dodgeVelocity;
                _horizontalVelocitySmoothDampRef = Vector3.zero;
                facingDirection = _dodgeState.Direction;
                _desiredPitchDegrees = 0f; // stay level while dodging, regardless of any lock-on
            }
            else
            {
                Vector3 desiredVelocity = desiredDirection * moveSpeed;
                float smoothTime = desiredVelocity.sqrMagnitude > 0.0001f ? accelerationSmoothTime : decelerationSmoothTime;
                _horizontalVelocity = Vector3.SmoothDamp(_horizontalVelocity, desiredVelocity, ref _horizontalVelocitySmoothDampRef, smoothTime);

                Transform lockedTarget = LockOnSource?.LockedTarget;
                if (lockedTarget != null)
                {
                    Vector3 toTarget = lockedTarget.position - transform.position;
                    // 2026-08-18, explicit user request (aerial combat) - the RAW (unflattened)
                    // offset feeds the pitch calc below; facingDirection itself stays horizontal-
                    // only, same as before, since yaw is computed separately via LookRotation on
                    // a flat vector further down.
                    _desiredPitchDegrees = AimUtility.ClampedPitchDegrees(toTarget, maxPitchDegrees);
                    toTarget.y = 0f;
                    facingDirection = toTarget;
                }
                else
                {
                    _desiredPitchDegrees = 0f;
                    facingDirection = desiredDirection;
                }
            }

            if (_controller.isGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = -1f;
            }

            // Ground-only (no air jump/double jump) - checked after the grounded reset above
            // so a jump this frame isn't immediately clobbered back down to -1.
            if (jumpPressed && _controller.isGrounded)
            {
                _verticalVelocity = jumpSpeed;
            }

            bool flyHeld = !staggered && inputCommand != null && inputCommand.FlyPressed;
            bool flyDescendHeld = !staggered && inputCommand != null && inputCommand.FlyDescendPressed;
            UpdateFlightState(flyHeld);

            if (_isFlying)
            {
                // Gravity is fully overridden while flying - ascend/descend/hover are all
                // direct vertical speeds rather than forces, so flight feels immediately
                // responsive instead of fighting the normal fall acceleration every frame.
                // Holding descend takes priority over ascend if somehow both are held; holding
                // neither hovers in place (see UpdateFlightState's own comment for why release
                // doesn't end flight outright).
                _verticalVelocity = flyDescendHeld ? -flightDescendSpeed : (flyHeld ? flightAscendSpeed : 0f);
                if (flightEnergy != null)
                {
                    flightEnergy.Drain(flightEnergyDrainPerSecond * Time.deltaTime);
                }
            }
            else if (_isGliding)
            {
                // Fixed gentle sink, no energy cost, horizontal movement untouched (the
                // _horizontalVelocity computed above already applies regardless of state) - see
                // glideDescendSpeed's own comment.
                _verticalVelocity = -glideDescendSpeed;
            }
            else
            {
                _verticalVelocity += gravity * Time.deltaTime;
            }

            // See GroundSlopeUtility's own comment - isGrounded alone doesn't mean "standing
            // somewhere walkable" (a jump can land directly on another character's rounded
            // CharacterController capsule), so an active push is needed to actually slide off
            // instead of just resting there indefinitely.
            //
            // 2026-08-16 correction: originally gated purely on IsTooSteepToStandOn against
            // _controller.slopeLimit (45° default) - but a jump landing near the center of a
            // small-radius capsule's dome (e.g. Player4's radius 0.4) contacts it at well
            // under 45° from vertical, the same as any normal walkable slope, even though
            // standing on a character's own collision capsule was never meant to be valid
            // footing regardless of the exact angle (confirmed against the real regression:
            // LandingOnTopOfPlayer4_SlidesOffWithoutAnyInput still failed with a mild ~16°
            // contact normal). Now also unconditionally slides whenever the ground hit belongs
            // to another character's CharacterController, on top of the original slope check
            // (which still covers genuinely steep terrain, if any is ever added).
            Vector3 slideVelocity = Vector3.zero;
            if (_controller.isGrounded && TryGetGroundNormal(out Vector3 groundNormal, out CharacterController standingOnCharacter))
            {
                bool standingOnAnotherCharacter = standingOnCharacter != null;
                bool tooSteep = GroundSlopeUtility.IsTooSteepToStandOn(groundNormal, _controller.slopeLimit);
                if (standingOnAnotherCharacter || tooSteep)
                {
                    Vector3 slideDirection = GroundSlopeUtility.ComputeSlideDirection(groundNormal);
                    if (slideDirection == Vector3.zero && standingOnAnotherCharacter)
                    {
                        slideDirection = GroundSlopeUtility.ComputeFallbackAwayDirection(transform.position, standingOnCharacter.transform.position);
                    }

                    slideVelocity = slideDirection * slideSpeed;
                }
            }

            Vector3 motion = _horizontalVelocity + slideVelocity;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            // Pitch eases toward _desiredPitchDegrees (0 unless locked onto a vertically-offset
            // target - see the lock-on block above), independently of the facingDirection gate
            // below so it settles back level even in the one frame a dodge/idle-no-lock-on
            // state briefly reports a zero facingDirection. Applied to _visual's LOCAL rotation
            // only, never to this transform - see _visual's own field comment for why (the
            // CharacterController capsule must stay upright).
            _pitch = Mathf.SmoothDampAngle(_pitch, _desiredPitchDegrees, ref _pitchAngularVelocity, rotationSmoothTime);
            if (_visual != null)
            {
                // Negated: Unity's Euler X convention is inverted from AimUtility's
                // "positive = looking up" (confirmed empirically - Quaternion.Euler(+X,0,0) *
                // forward tips DOWN, not up). This was a real latent sign bug in the original
                // (pre-Visual-child) version of this line - never actually caught before because
                // only _desiredPitchDegrees's raw value was reflection-tested, not the resulting
                // forward vector.
                _visual.localRotation = Quaternion.Euler(-_pitch, 0f, 0f);
            }

            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                // SmoothDampAngle instead of a constant-degrees/sec RotateTowards, so the
                // turn eases out near the target facing instead of stopping dead the instant
                // it arrives - see the field comment above for why.
                float currentYaw = transform.eulerAngles.y;
                float targetYaw = Quaternion.LookRotation(facingDirection, Vector3.up).eulerAngles.y;
                float newYaw = Mathf.SmoothDampAngle(currentYaw, targetYaw, ref _yawAngularVelocity, rotationSmoothTime);
                transform.rotation = Quaternion.Euler(0f, newYaw, 0f);
            }
            // else: no yaw change this frame - transform.rotation already yaw-only, nothing to do.
        }

        // Entering flight requires holding the key with energy available (grounded or airborne
        // - lifting straight off the ground is intentional, "自由飛行" reads as more than just
        // an air-only ability). Once active, flight PERSISTS regardless of whether the key is
        // still held - see Update()'s own flight block: releasing simply hovers instead of
        // falling - and only actually ends on landing (isGrounded true again, which physically
        // can't happen mid-ascent) or running out of energy. This asymmetry (easy entry
        // condition, sticky exit condition) is deliberate: "按住鍵自由飛行" describes holding
        // the key to CONTROL flight, not to merely stay airborne.
        //
        // 2026-08-18 extended with Glide (see glideDescendSpeed's own comment): running out of
        // energy mid-air now drops into Glide instead of straight back to normal gravity/falling.
        // Glide itself ends on landing, or resumes real Flight the moment the key is held again
        // AND energy has regenerated past zero - same entry condition as the empty-handed case
        // below, just checked from the Glide branch too.
        private void UpdateFlightState(bool flyHeld)
        {
            if (_isFlying)
            {
                if (_controller.isGrounded)
                {
                    _isFlying = false;
                }
                else if (flightEnergy == null || flightEnergy.CurrentEnergy <= 0f)
                {
                    _isFlying = false;
                    _isGliding = true;
                }

                return;
            }

            if (_isGliding)
            {
                if (_controller.isGrounded)
                {
                    _isGliding = false;
                    return;
                }

                // flightResumeEnergyThreshold, not just > 0f - see that field's own comment for
                // the stutter bug this avoids.
                if (flyHeld && flightEnergy != null && flightEnergy.CurrentEnergy >= flightResumeEnergyThreshold)
                {
                    _isGliding = false;
                    _isFlying = true;
                }

                return;
            }

            if (flyHeld && flightEnergy != null && flightEnergy.CurrentEnergy > 0f)
            {
                _isFlying = true;
            }
        }

        // Physics.SphereCastAll (not a single SphereCast/Raycast) so a self-hit on the
        // player's own CharacterController capsule can be explicitly filtered out rather than
        // risking it being the first/only result - the cast origin sits exactly at the
        // capsule's own bottom hemisphere center, so a self-overlap at the very start of the
        // cast is expected, not just a theoretical edge case. transform.root comparison (not
        // just transform) so this still correctly excludes self even if the capsule ever
        // gains child colliders later.
        //
        // 2026-08-16 bug this fixes: the origin was originally computed as
        // capsuleBottomLocalY + radius + 0.15 - an extra +0.15 on TOP of already adding the
        // full radius, which places the origin well up inside the capsule's cylindrical body
        // (e.g. local Y=0.05 for height=1/radius=0.4, nowhere near the actual bottom surface)
        // instead of at the bottom hemisphere. The cast still technically ran, but from a
        // point already deep inside solid geometry - confirmed via a failing regression test
        // (LandingOnTopOfPlayer4_SlidesOffWithoutAnyInput) that the slide never actually
        // triggered. capsuleBottomLocalY + radius alone is the correct bottom-hemisphere-
        // center reference point.
        // otherCharacterController is non-null when the closest hit's collider belongs to
        // another CharacterController (i.e. another character, not terrain/environment) -
        // used by Update() to unconditionally slide off another character regardless of the
        // exact contact angle, see that call site's own comment for why the slope-angle check
        // alone wasn't enough.
        private bool TryGetGroundNormal(out Vector3 normal, out CharacterController otherCharacterController)
        {
            float capsuleBottomLocalY = _controller.center.y - _controller.height / 2f;
            Vector3 origin = transform.position + new Vector3(0f, capsuleBottomLocalY + _controller.radius, 0f);
            float castDistance = _controller.radius + 0.3f;
            float castRadius = Mathf.Max(0.05f, _controller.radius * 0.8f);

            RaycastHit[] hits = Physics.SphereCastAll(origin, castRadius, Vector3.down, castDistance, ~0, QueryTriggerInteraction.Ignore);
            float closestDistance = float.MaxValue;
            normal = Vector3.up;
            otherCharacterController = null;
            bool found = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.root == transform.root)
                {
                    continue;
                }

                if (hit.distance < closestDistance)
                {
                    closestDistance = hit.distance;
                    normal = hit.normal;
                    otherCharacterController = hit.collider.GetComponent<CharacterController>();
                    found = true;
                }
            }

            return found;
        }

        private float CurrentCameraYawDegrees()
        {
            ICameraYawSource yawSource = CameraYawSource;
            if (yawSource != null)
            {
                return yawSource.YawDegrees;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null ? mainCamera.transform.eulerAngles.y : 0f;
        }

        public static Vector3 CameraRelativeDirection(Vector2 moveInput, float cameraYawDegrees)
        {
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Quaternion yaw = Quaternion.Euler(0f, cameraYawDegrees, 0f);
            Vector3 forward = yaw * Vector3.forward;
            Vector3 right = yaw * Vector3.right;
            return (forward * moveInput.y + right * moveInput.x).normalized;
        }
    }
}
