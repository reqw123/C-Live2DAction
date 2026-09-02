using System.Collections.Generic;
using Live2DAction.AI.Boss;
using Live2DAction.Combat;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Vehicles
{
    // 2026-08-26, explicit user request ("建立一套可實際駕駛的四輪車輛系統") - Rigidbody +
    // WheelCollider driven buggy. Reads input directly from Keyboard.current, matching this
    // project's own established convention (see Live2DAction.Input.PlayerInputProvider) rather
    // than the Input Actions asset workflow - the existing IInputCommand interface is combat/
    // character-movement vocabulary (AttackPressed, DodgePressed, ...) that doesn't fit a vehicle,
    // so this reads the keyboard directly the same way PlayerInputProvider does instead of forcing
    // a mismatched interface.
    public enum VehicleDriveType
    {
        FWD,
        RWD,
        AWD,
    }

    [RequireComponent(typeof(Rigidbody))]
    public class VehicleController : MonoBehaviour
    {
        [Header("Wheel colliders (physics only, never rendered)")]
        [SerializeField] private WheelCollider frontLeft;
        [SerializeField] private WheelCollider frontRight;
        [SerializeField] private WheelCollider rearLeft;
        [SerializeField] private WheelCollider rearRight;

        [Header("Matching visual sync components (spec 五)")]
        [SerializeField] private WheelVisualSync frontLeftVisual;
        [SerializeField] private WheelVisualSync frontRightVisual;
        [SerializeField] private WheelVisualSync rearLeftVisual;
        [SerializeField] private WheelVisualSync rearRightVisual;

        [Header("Drive type - switchable in Inspector (spec 四)")]
        [SerializeField] private VehicleDriveType driveType = VehicleDriveType.AWD;

        [Header("Motor / brake torque")]
        [SerializeField] private float motorTorque = 3500f;
        [SerializeField] private float reverseTorque = 2200f;
        [SerializeField] private float brakeTorque = 4000f;
        [SerializeField] private float handbrakeTorque = 8000f;

        [Header("Speed limits (km/h)")]
        [SerializeField] private float maximumSpeed = 90f;
        [SerializeField] private float maximumReverseSpeed = 35f;

        [Header("Steering")]
        // 2026-08-29, user report ("感覺car有點難操控" -> "轉向太靈敏/容易過彎過度、甩尾"). Calmed the
        // turn-in: less lock (32->24), the speed falloff starts biting much sooner (70->45 km/h)
        // and the ramp to a new angle is a touch more progressive (120->90 deg/s). Paired with more
        // sideways grip + angular damping below.
        [SerializeField] private float maximumSteeringAngle = 24f;
        [Tooltip("Speed (km/h) at which max steering angle has decayed to minSteeringAngleFraction (spec 十).")]
        [SerializeField] private float steeringSpeedFalloffReference = 45f;
        [SerializeField, Range(0f, 1f)] private float minSteeringAngleFraction = 0.35f;
        [Tooltip("Degrees/second the actual steering angle can change - smooths 0->target instead of snapping (spec 十).")]
        [SerializeField] private float steeringSmoothSpeedDegrees = 90f;

        [Header("Mass / center of mass")]
        [SerializeField] private float vehicleMass = 950f;
        [Tooltip("Lowered below the model's own geometric center to resist rollover (spec 二).")]
        [SerializeField] private Vector3 centerOfMassOffset = new Vector3(0f, -0.45f, 0f);

        // 2026-08-29, user report ("容易打滑/原地打轉") - Unity's Rigidbody angular drag default
        // (0.05) is near-zero, so once the chassis started yawing (a kerb, a hard turn, a bump) it
        // just kept spinning with nothing to settle it - the "原地打轉" symptom. Applied in Awake/
        // OnValidate like mass/centerOfMass so it's one place with the rest of the vehicle tuning.
        [Tooltip("Rigidbody angular drag - higher settles a chassis yaw/spin faster (0.05 = Unity default, basically none).")]
        [SerializeField] private float angularDamping = 0.5f;

        [Header("Wheel friction (spec 十一)")]
        [SerializeField] private WheelFrictionCurve forwardFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.4f, extremumValue = 1f, asymptoteSlip = 0.8f, asymptoteValue = 0.5f, stiffness = 1.8f,
        };
        // 2026-08-29, user report ("容易打滑/原地打轉/甩尾") - more lateral grip so the rear doesn't
        // break loose so readily, and it holds grip further into a slide (asymptoteSlip 0.5->0.7,
        // asymptoteValue 0.75->0.85) instead of dropping to a low tail-happy value the moment the
        // slip peak is passed.
        [SerializeField] private WheelFrictionCurve sidewaysFriction = new WheelFrictionCurve
        {
            extremumSlip = 0.3f, extremumValue = 1f, asymptoteSlip = 0.7f, asymptoteValue = 0.85f, stiffness = 2.2f,
        };

        [Header("Suspension (spec 二 / 十二)")]
        [SerializeField] private float suspensionDistance = 0.28f;
        [SerializeField] private float suspensionSpring = 35000f;
        [SerializeField] private float suspensionDamper = 4500f;
        [Tooltip("How far below the wheel's resting position the spring is already compressed - keeps a heavy buggy from bottoming out on landing.")]
        [SerializeField, Range(0f, 1f)] private float suspensionTargetPosition = 0.5f;

        // 2026-08-26, explicit user request ("車子被牆卡住或翻車時要怎麼處理" - "加一個手動重置鍵") -
        // periodically snapshots the car's own pose whenever it's genuinely settled (grounded,
        // upright), then R teleports back to that snapshot. Deliberately NOT "last position every
        // frame" - that would happily record a pose mid-flip or wedged against a wall a moment
        // before getting stuck, which would just reset back into the same stuck state.
        [Header("Stuck recovery (spec: 手動重置鍵)")]
        [SerializeField] private float safePoseRecordInterval = 0.5f;
        [Tooltip("dot(transform.up, world up) must exceed this to count as \"upright\" for recording a safe pose.")]
        [SerializeField, Range(0f, 1f)] private float safePoseUprightDotThreshold = 0.8f;
        [SerializeField] private float safePoseMaxAngularSpeed = 30f;
        [SerializeField] private float resetLiftHeight = 0.3f;

        // 2026-08-26, explicit user request ("車子撞到武士時 不造成傷害但可以擊退 依照車行駛速度決定
        // 擊退距離") - a CharacterController-driven character (Wushi, the Player when on foot) is
        // never pushed by ordinary Rigidbody collision response (CharacterController ignores
        // physics forces entirely, see WushiTuning/BossStateMachine's own movement - it only ever
        // moves via its own Move() calls), so without this the car would just silently stop dead
        // against them like hitting a wall. This is a one-shot CharacterController.Move() nudge,
        // not a physics force, specifically because that's the only thing that actually displaces
        // a CharacterController. Deliberately generic (any CharacterController, not Boss-specific)
        // so it also affects the Player themselves if walked into while not driving.
        [Header("Enemy/player knockback on impact - no damage (spec: 撞到武士)")]
        [SerializeField] private float knockbackMetersPerKmh = 0.15f;
        [SerializeField] private float minKnockbackSpeedKmh = 8f;
        [SerializeField] private float maxKnockbackDistance = 4f;
        [Tooltip("Re-trigger cooldown per struck character - collision stays fire every physics step while still touching, this stops that from being one continuous shove.")]
        [SerializeField] private float knockbackCooldownSeconds = 0.5f;

        // 2026-08-26, explicit user request ("駕駛模式時的shift提供加速功能") - hold-to-boost, forward
        // acceleration only (reverse untouched - a "nitro" pushing you backward faster isn't a
        // real use case). Left Shift specifically, per the request - yes, that's the same physical
        // key as the Player's own Dodge/fly-descend (PlayerInputProvider), but playerMovement is
        // already disabled for the whole drive (see VehicleEntrySystem.EnterVehicle), so those
        // consumers are inert while this is live; same reasoning as the R-vs-Backspace note on the
        // reset key above, just the opposite conclusion because here Shift was explicitly named.
        // Raises both the torque curve AND the speed cap it tapers toward (see
        // ApplyMotorAndBrakes) rather than just adding flat torque, so boost actually raises top
        // speed instead of only accelerating harder up to the same old ceiling.
        //
        // Torque alone measurably did NOT work, confirmed by direct testing - the base tuning's
        // forwardFriction is already close to its traction ceiling at low speed (a real car's
        // problem too: horsepower past the tires' grip limit just spins the wheels, doesn't
        // accelerate you faster), so a torque-only boost was mostly wasted as extra wheelspin with
        // near-zero effect on actual velocity. boostForwardGripMultiplier raises the traction
        // ceiling alongside the torque so the extra power actually reaches the ground - see
        // ApplyBoostFriction.
        [Header("Shift boost (spec: 駕駛模式Shift加速)")]
        [SerializeField] private float boostMotorTorqueMultiplier = 1.6f;
        [SerializeField] private float boostMaxSpeedMultiplier = 1.35f;
        [SerializeField] private float boostForwardGripMultiplier = 1.6f;

        private Rigidbody _rigidbody;
        private float _currentSteerAngle;
        private float _throttleInput;
        private bool _boostInput;
        private float _steerInput;
        private bool _handbrakeInput;
        private bool _resetRequested;
        private float _safePoseTimer;
        private Vector3 _lastSafePosition;
        private Quaternion _lastSafeRotation;
        private readonly Dictionary<CharacterController, float> _lastKnockbackTime = new Dictionary<CharacterController, float>();

        // Debug readout (spec 十五)
        public float CurrentSpeedKmh { get; private set; }
        public float CurrentMotorTorque { get; private set; }
        public float CurrentSteeringAngle => _currentSteerAngle;
        public bool FrontLeftGrounded => frontLeft != null && frontLeft.isGrounded;
        public bool FrontRightGrounded => frontRight != null && frontRight.isGrounded;
        public bool RearLeftGrounded => rearLeft != null && rearLeft.isGrounded;
        public bool RearRightGrounded => rearRight != null && rearRight.isGrounded;
        public bool AnyWheelGrounded => FrontLeftGrounded || FrontRightGrounded || RearLeftGrounded || RearRightGrounded;

        // 2026-08-30, vehicle flight (VehicleFlightController) - while true, the flight controller
        // owns the Rigidbody directly (velocity written each FixedUpdate, gravity off). Beyond
        // skipping steering/motor/brake in FixedUpdate, the transition also:
        //   - DISABLES the 4 WheelColliders: an enabled WheelCollider still runs its own
        //     suspension raycast + applies spring force the moment it grazes any geometry, which
        //     PhysX-kicks the chassis and fights the hard-set flight velocity = the "空中抖動" the
        //     user reported. Off entirely while flying, back on for the landing.
        //   - switches Rigidbody.interpolation to Interpolate: with None (the ground default) the
        //     visual pose only updates at the 50 Hz physics rate, which reads as jitter on a
        //     smoothly-moving airborne body.
        private bool _flightModeActive;
        private RigidbodyInterpolation _groundInterpolation;
        public bool FlightModeActive
        {
            get => _flightModeActive;
            set
            {
                if (_flightModeActive == value) return;
                _flightModeActive = value;
                if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
                WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
                if (value)
                {
                    _groundInterpolation = _rigidbody.interpolation;
                    _rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
                    foreach (var w in wheels) if (w != null) w.enabled = false;
                }
                else
                {
                    _rigidbody.interpolation = _groundInterpolation;
                    foreach (var w in wheels) if (w != null) w.enabled = true;
                }
            }
        }

        // 2026-08-26, explicit user request ("駕駛時SHIFT觸發期間給予特效設計") - read-only exposure
        // of the private input flag so VehicleBoostEffects (particles + post-process) can react to
        // boost state without VehicleController needing to know anything about VFX itself.
        public bool IsBoosting => _boostInput;

        private void Awake()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _rigidbody.mass = vehicleMass;
            _rigidbody.centerOfMass = centerOfMassOffset;
            _rigidbody.angularDamping = angularDamping;
            ApplyWheelTuning();

            _lastSafePosition = transform.position;
            _lastSafeRotation = transform.rotation;
        }

        // 2026-08-26, explicit user request ("靠近CAR可用F鍵進入車體...W/A/S/D正式由CAR接管") -
        // VehicleEntrySystem toggles this component's own `enabled` to hand control between the
        // player and the car (simplest possible gate - Update/FixedUpdate just stop running while
        // disabled, no separate "IsPlayerDriving" flag to keep in sync). The one thing that needs
        // to happen exactly once on the transition, which just NOT running Update can't give us for
        // free: motorTorque/brakeTorque are WheelCollider state that persists on its own once set,
        // so if the last FixedUpdate before disabling happened to apply throttle, the car would
        // keep quietly coasting/rolling forever with nobody driving it. Parking brake on exit
        // avoids that; re-applied on enable too in case Reset()/prefab defaults left stale values.
        private void OnEnable() => ApplyParkingBrake(0f);
        private void OnDisable()
        {
            // Dismounted mid-flight - restore the wheels / interpolation the flight setter changed
            // (VehicleFlightController also ends flight next frame, this just closes the 1-frame gap).
            FlightModeActive = false;
            ApplyParkingBrake(brakeTorque);
        }

        private void ApplyParkingBrake(float brake)
        {
            if (frontLeft != null) { frontLeft.motorTorque = 0f; frontLeft.brakeTorque = brake; }
            if (frontRight != null) { frontRight.motorTorque = 0f; frontRight.brakeTorque = brake; }
            if (rearLeft != null) { rearLeft.motorTorque = 0f; rearLeft.brakeTorque = brake; }
            if (rearRight != null) { rearRight.motorTorque = 0f; rearRight.brakeTorque = brake; }
        }

        // Every WheelCollider/suspension/friction field is exposed in the Inspector (spec 三) and
        // can be live-tweaked in Play Mode - re-applied every frame in edit-friendly fashion via
        // OnValidate, and once at Awake for the values baked in from a prefab instance.
        private void OnValidate()
        {
            if (_rigidbody == null) _rigidbody = GetComponent<Rigidbody>();
            if (_rigidbody != null)
            {
                _rigidbody.mass = vehicleMass;
                _rigidbody.centerOfMass = centerOfMassOffset;
                _rigidbody.angularDamping = angularDamping;
            }
            ApplyWheelTuning();
        }

        private void ApplyWheelTuning()
        {
            WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
            foreach (var w in wheels)
            {
                if (w == null) continue;
                w.suspensionDistance = suspensionDistance;
                JointSpring spring = w.suspensionSpring;
                spring.spring = suspensionSpring;
                spring.damper = suspensionDamper;
                spring.targetPosition = suspensionTargetPosition;
                w.suspensionSpring = spring;
                w.forwardFriction = forwardFriction;
                w.sidewaysFriction = sidewaysFriction;
            }
        }

        // Runs every FixedUpdate (not just once in Awake/OnValidate like ApplyWheelTuning) since
        // it needs to react to _boostInput toggling live. Always recomputed FROM the base
        // `forwardFriction` field, never compounded onto whatever the curve currently is - the
        // opposite would runaway-multiply every frame boost stays held.
        private void ApplyBoostFriction()
        {
            WheelFrictionCurve friction = forwardFriction;
            if (_boostInput) friction.stiffness *= boostForwardGripMultiplier;

            WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
            foreach (var w in wheels)
            {
                if (w == null) continue;
                w.forwardFriction = friction;
            }
        }

        private void Update()
        {
            ReadInput();
        }

        private void ReadInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                _throttleInput = 0f;
                _steerInput = 0f;
                _handbrakeInput = false;
                _boostInput = false;
                return;
            }

            float throttle = 0f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) throttle += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) throttle -= 1f;
            _throttleInput = throttle;

            float steer = 0f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) steer -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) steer += 1f;
            _steerInput = steer;

            _handbrakeInput = keyboard.spaceKey.isPressed;
            _boostInput = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;

            // Backspace, not R - R is already the Player's Ultimate key (PlayerInputProvider).
            // PlayerCombat is disabled while driving so it wouldn't actually fire, but reusing the
            // same physical key for two unrelated actions is exactly the kind of thing that bites
            // later (e.g. a leftover press read the instant control hands back on exit).
            if (keyboard.backspaceKey.wasPressedThisFrame) _resetRequested = true;
        }

        private void FixedUpdate()
        {
            if (_resetRequested)
            {
                _resetRequested = false;
                ResetToSafePosition();
            }

            CurrentSpeedKmh = _rigidbody.linearVelocity.magnitude * 3.6f;

            if (FlightModeActive)
            {
                // VehicleFlightController is flying the Rigidbody - wheels stand down. Clear any
                // torque so a re-land doesn't inherit stale throttle/brake from before liftoff.
                // Skip the visual sync too: it pins each wheel bone to WheelCollider.GetWorldPose,
                // which on a DISABLED collider returns the stale ground pose - the bone would stay
                // on the ground while the chassis flies off, stretching the mesh between them.
                // Not syncing = the wheel bones keep their liftoff local pose and just ride the
                // chassis, which is what we want in the air.
                ApplyParkingBrake(0f);
            }
            else
            {
                ApplyBoostFriction();
                ApplySteering();
                ApplyMotorAndBrakes();
                ApplyHandbrake();

                // Sync visuals from the exact physics step that just ran, not Update's own timing -
                // see WheelVisualSync.SyncVisual's own comment on why this avoids a frame of lag.
                frontLeftVisual?.SyncVisual();
                frontRightVisual?.SyncVisual();
                rearLeftVisual?.SyncVisual();
                rearRightVisual?.SyncVisual();
            }

            UpdateSafePoseSnapshot();
        }

        // Called every FixedUpdate but only actually records at safePoseRecordInterval - checked
        // here (not gated behind a coroutine) so it shares FixedUpdate's own already-fresh
        // isGrounded/velocity reads instead of re-querying WheelColliders on its own timer.
        private void UpdateSafePoseSnapshot()
        {
            _safePoseTimer += Time.fixedDeltaTime;
            if (_safePoseTimer < safePoseRecordInterval) return;
            _safePoseTimer = 0f;

            bool allGrounded = FrontLeftGrounded && FrontRightGrounded && RearLeftGrounded && RearRightGrounded;
            bool upright = Vector3.Dot(transform.up, Vector3.up) >= safePoseUprightDotThreshold;
            bool settled = _rigidbody.angularVelocity.magnitude <= safePoseMaxAngularSpeed;
            if (!allGrounded || !upright || !settled) return;

            _lastSafePosition = transform.position;
            _lastSafeRotation = transform.rotation;
        }

        // Teleport, not a physics nudge - a car properly wedged (upside down in a corner, stuck on
        // top of a wall) often can't reach a valid pose through normal forces at all, which is
        // exactly the case this exists for.
        private void ResetToSafePosition()
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;

            Vector3 resetPosition = _lastSafePosition + Vector3.up * resetLiftHeight;
            _rigidbody.position = resetPosition;
            _rigidbody.rotation = _lastSafeRotation;
            transform.SetPositionAndRotation(resetPosition, _lastSafeRotation);
        }

        private void OnCollisionEnter(Collision collision) => TryKnockback(collision);

        // OnCollisionStay too, not just Enter - a car pinning a character against a wall stays in
        // continuous contact rather than a single Enter event, and the cooldown below is what
        // keeps repeated Stay events from reading as one continuous shove.
        private void OnCollisionStay(Collision collision) => TryKnockback(collision);

        private void TryKnockback(Collision collision)
        {
            CharacterController hitController = collision.collider as CharacterController;
            if (hitController == null || CurrentSpeedKmh < minKnockbackSpeedKmh) return;

            float lastTime;
            if (_lastKnockbackTime.TryGetValue(hitController, out lastTime) && Time.time - lastTime < knockbackCooldownSeconds) return;
            _lastKnockbackTime[hitController] = Time.time;

            Vector3 pushDirection = hitController.transform.position - transform.position;
            pushDirection.y = 0f;
            if (pushDirection.sqrMagnitude < 0.0001f) pushDirection = transform.forward;
            pushDirection.Normalize();

            float distance = Mathf.Min(CurrentSpeedKmh * knockbackMetersPerKmh, maxKnockbackDistance);
            hitController.Move(pushDirection * distance);

            // 2026-08-26, explicit user request ("被撞的生物體如果有死亡、受傷動作的話就用動作來呈現
            // 擊退後效果") - the physical Move() above happens regardless, but a struck character
            // that HAS a hit-reaction animation should visibly sell it instead of sliding through
            // whatever it was already doing (chasing, mid-attack, idle) with no reaction at all.
            //
            // 2026-08-26 follow-up, real playtested bug ("開車撞到人後並沒有讓他做動作") - the first
            // version only checked for BossStateMachine, which ONLY exists on 武士/屁孩王. Every
            // OTHER damageable character (Player, Enemy, 中立者1/2/3, ...) uses a completely
            // different, non-Boss hurt-reaction system: StancePoise/StaggerAnimationLink (the
            // Souls-like poise bar - see StancePoise's own history). Checking for BOTH, Boss first,
            // covers every character in the project that has ANY hit-reaction animation at all -
            // still purely optional per character (a TrainingDummy/Mecha with neither component
            // just keeps the plain physical Move() with no animation, per "如果有...的話").
            BossStateMachine hitBoss = hitController.GetComponentInParent<BossStateMachine>();
            if (hitBoss != null)
            {
                hitBoss.RequestBeHitFlyUp();
            }
            else
            {
                // AddPostureDamage is the same public integration hook BossStateMachine itself
                // uses (see StancePoise.AddPostureDamage's own comment) - decoupled from
                // Health.ApplyDamage entirely, so this cannot deal HP damage even indirectly.
                // Passing the character's own MaxStance guarantees an instant stagger in one call
                // regardless of that character's own tuning, rather than guessing a flat number
                // that might undershoot a character with a larger poise bar.
                StancePoise hitStance = hitController.GetComponentInParent<StancePoise>();
                if (hitStance != null) hitStance.AddPostureDamage(hitStance.MaxStance);
            }
        }

        private void ApplySteering()
        {
            // Spec 十 - less steering authority at speed, so a fast buggy doesn't snap-rotate and
            // roll. Linearly falls from full maximumSteeringAngle at 0 speed down to
            // minSteeringAngleFraction of it at steeringSpeedFalloffReference and beyond.
            float speedFraction = Mathf.Clamp01(CurrentSpeedKmh / Mathf.Max(1f, steeringSpeedFalloffReference));
            float effectiveMaxAngle = Mathf.Lerp(maximumSteeringAngle, maximumSteeringAngle * minSteeringAngleFraction, speedFraction);
            float targetAngle = _steerInput * effectiveMaxAngle;

            // Spec 十 - smoothed toward target, never an instant snap from 0.
            _currentSteerAngle = Mathf.MoveTowards(_currentSteerAngle, targetAngle, steeringSmoothSpeedDegrees * Time.fixedDeltaTime);

            if (frontLeft != null) frontLeft.steerAngle = _currentSteerAngle;
            if (frontRight != null) frontRight.steerAngle = _currentSteerAngle;
            // Spec 六 - rear wheels never steer, deliberately untouched.
        }

        private void ApplyMotorAndBrakes()
        {
            bool movingForward = Vector3.Dot(_rigidbody.linearVelocity, transform.forward) >= -0.1f;
            float appliedMotor = 0f;
            float appliedBrake = 0f;

            if (_throttleInput > 0f)
            {
                if (movingForward || CurrentSpeedKmh < 1f)
                {
                    // Accelerating forward - respect maximumSpeed by tapering torque near the cap
                    // rather than a hard velocity clamp (keeps this feeling like real torque, not
                    // an invisible wall). Shift raises BOTH the torque and the cap it tapers
                    // toward, not just flat extra torque - that's what actually raises top speed
                    // instead of only reaching the old ceiling faster.
                    float effectiveMotorTorque = _boostInput ? motorTorque * boostMotorTorqueMultiplier : motorTorque;
                    float effectiveMaxSpeed = _boostInput ? maximumSpeed * boostMaxSpeedMultiplier : maximumSpeed;
                    float speedRatio = Mathf.Clamp01(CurrentSpeedKmh / Mathf.Max(1f, effectiveMaxSpeed));
                    appliedMotor = effectiveMotorTorque * _throttleInput * (1f - speedRatio);
                }
                else
                {
                    // Moving backward but throttle pressed forward - brake into the reversal first,
                    // same as a real pedal, instead of instantly reversing the torque direction.
                    appliedBrake = brakeTorque;
                }
            }
            else if (_throttleInput < 0f)
            {
                if (!movingForward || CurrentSpeedKmh < 1f)
                {
                    float speedRatio = Mathf.Clamp01(CurrentSpeedKmh / Mathf.Max(1f, maximumReverseSpeed));
                    appliedMotor = -reverseTorque * -_throttleInput * (1f - speedRatio);
                }
                else
                {
                    appliedBrake = brakeTorque;
                }
            }

            float flMotor = 0f, frMotor = 0f, rlMotor = 0f, rrMotor = 0f;
            switch (driveType)
            {
                case VehicleDriveType.FWD:
                    flMotor = appliedMotor; frMotor = appliedMotor;
                    break;
                case VehicleDriveType.RWD:
                    rlMotor = appliedMotor; rrMotor = appliedMotor;
                    break;
                case VehicleDriveType.AWD:
                default:
                    // Even split - a real AWD center diff is more involved than this project needs
                    // for "先完成可靠的車輛物理" (spec's own explicit priority, deferring drift/
                    // advanced drivetrain simulation).
                    flMotor = appliedMotor * 0.5f; frMotor = appliedMotor * 0.5f;
                    rlMotor = appliedMotor * 0.5f; rrMotor = appliedMotor * 0.5f;
                    break;
            }

            CurrentMotorTorque = appliedMotor;

            if (frontLeft != null) { frontLeft.motorTorque = flMotor; frontLeft.brakeTorque = appliedBrake; }
            if (frontRight != null) { frontRight.motorTorque = frMotor; frontRight.brakeTorque = appliedBrake; }
            if (rearLeft != null) { rearLeft.motorTorque = rlMotor; rearLeft.brakeTorque = appliedBrake; }
            if (rearRight != null) { rearRight.motorTorque = rrMotor; rearRight.brakeTorque = appliedBrake; }
        }

        private void ApplyHandbrake()
        {
            // Spec: Space = handbrake, rear wheels only (standard handbrake behavior) - locks the
            // rear axle for a controlled slide rather than braking all four (which would just be a
            // second normal brake).
            float hb = _handbrakeInput ? handbrakeTorque : 0f;
            if (rearLeft != null) rearLeft.brakeTorque = Mathf.Max(rearLeft.brakeTorque, hb);
            if (rearRight != null) rearRight.brakeTorque = Mathf.Max(rearRight.brakeTorque, hb);
        }

        // Spec 十五 - Scene View debug: wheel positions, suspension direction, ground contact,
        // forward direction, all four WheelColliders at a glance without entering Play Mode.
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawRay(transform.position, transform.forward * 2f);

            WheelCollider[] wheels = { frontLeft, frontRight, rearLeft, rearRight };
            foreach (var w in wheels)
            {
                if (w == null) continue;
                w.GetWorldPose(out Vector3 pos, out Quaternion rot);
                Gizmos.color = w.isGrounded ? Color.green : Color.red;
                Gizmos.DrawWireSphere(pos, w.radius);
                Gizmos.color = Color.yellow;
                Gizmos.DrawRay(pos, -w.transform.up * w.suspensionDistance);
            }
        }
    }
}
