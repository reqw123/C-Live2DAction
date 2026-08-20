using UnityEngine;
using Live2DAction.CameraSystem;
using Live2DAction.Core;
using Live2DAction.Input;
using Live2DAction.Targeting;

namespace Live2DAction.Characters
{
    [RequireComponent(typeof(CharacterController))]
    public class CharacterMovement : MonoBehaviour, ICharacterSpeedSource
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

        // 2026-08-20, explicit user request ("請讓泉水點也支援回復體力條") - Player carries TWO
        // UltimateEnergy instances (this one, and the ultimate skill's own, wired completely
        // independently - see flightEnergy's own comment), so a plain
        // GetComponentInParent<UltimateEnergy>() from an external system like HealingSpring can't
        // tell them apart and just grabs whichever happens to resolve first. Exposing this
        // specific reference directly (same "expose the internal state something external needs"
        // idiom as IsFlying/CurrentBankRollDegrees below) lets HealingSpring target the flight
        // instance unambiguously instead of guessing.
        public UltimateEnergy FlightEnergy => flightEnergy;

        [SerializeField] private float flightAscendSpeed = 6f;

        // 2026-08-20, real playtested feedback ("俯視飛行似乎沒辦法做到真的低或直直落下") -
        // measured the actual achieved descend speed directly rather than guessing: even with the
        // dive bonus fully maxed out (camera pitched to 70°, holding descend), steady-state
        // vertical velocity only reached the OLD flightDescendSpeed(4) * diveMaxSpeedMultiplier
        // (1.4) = -5.6/sec - slower than plain ascend (6) and far slower than horizontal cruise
        // (flightMoveSpeed=9, let alone boosted). Descending never had any mechanism actually
        // BLOCKING it from reaching the ground or going low (a direct straight-down descent test
        // confirmed it reaches ground level and lands cleanly) - the complaint was real, just
        // about FEEL: nothing about "diving" read as fast or committed next to every other flight
        // speed in the kit. Raised to match flightAscendSpeed exactly (6) as the new plain-
        // descend baseline - diving is no longer weirdly slower than climbing by default.
        [SerializeField] private float flightDescendSpeed = 6f;

        // 2026-08-20, explicit user request ("玩家飛行時的耐力條很快就沒了") - Drain fires every
        // frame the whole time _isFlying is true (ascending, descending, OR just hovering - see
        // the Update() block below, it's unconditional on the vertical direction).
        //
        // 2026-08-20 follow-up, explicit user request ("設計為飛行體力500 只有在閒置3秒沒有消耗體力
        // 後才會逐漸恢復體力 恢復速度提高") - the flightEnergy instance's regen no longer nets
        // against this drain in real time at all (see UltimateEnergy.regenIdleDelaySeconds) -
        // every Drain() call while flying pushes regen back out by a further 3 seconds, so this
        // value is now the FULL, un-netted cost of sustained flight: at the current 500 max (see
        // that instance's own maxEnergy), that's ~33 seconds of continuous flight before running
        // dry, with regen only actually starting 3s after landing/stopping rather than
        // continuously fighting the drain mid-flight.
        [SerializeField] private float flightEnergyDrainPerSecond = 15f;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.2) - ascend/descend
        // used to snap _verticalVelocity to its target instantly every frame (no easing at all),
        // unlike every other velocity in this class (all SmoothDamp'd). Short on purpose - this
        // is meant to remove the jarring instant-flip when toggling hover/ascend/descend, not to
        // make flight feel heavy or sluggish (compare to horizontal's 0.05-0.08s - this is
        // deliberately a bit longer than that, but still short). Only used while ACTIVELY holding
        // ascend or descend now - see flightVerticalStopSmoothTime below for releasing both.
        [SerializeField] private float flightVerticalSmoothTime = 0.18f;

        // 2026-08-20, real playtested bug ("現在我明明直直地面對前方還會下墜") - releasing descend
        // after the dive-speed tuning above (flightDescendSpeed/diveMaxSpeedMultiplier both
        // raised) left a MUCH bigger residual velocity to bleed off through the single shared
        // flightVerticalSmoothTime(0.18s) - a full dive's -15/sec took ~0.3-0.4s (measured
        // directly, not guessed) to actually settle back near 0 after letting go of everything,
        // which reads exactly like "I'm looking straight ahead, holding nothing, and still
        // falling" even though nothing is actually broken - it's genuine momentum, just not
        // shed fast enough to feel intentional. Mirrors the horizontal velocity's own existing
        // accel/decel split (accelerationSmoothTime vs decelerationSmoothTime) - a separate,
        // much shorter smooth time specifically for "returning to hover" (neither ascend nor
        // descend held), so committing to a fast dive still feels deliberate to enter but stops
        // almost immediately once you actually let go, instead of coasting for a third of a
        // second. Diving/ascending themselves are UNCHANGED (still ease in via
        // flightVerticalSmoothTime above) - only the "stopping" case got faster.
        [SerializeField] private float flightVerticalStopSmoothTime = 0.08f;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.3) - flight's own
        // horizontal cruise speed, decoupled from moveSpeed (which is tuned to match the
        // ground Locomotion animation, not remotely fast enough to read as "free flight").
        // Only applies while _isFlying - moveSpeed still governs ground movement (and, since
        // Glide's removal, plain falling too) unchanged.
        [SerializeField] private float flightMoveSpeed = 9f;

        // 2026-08-20, flight system design, explicit user request ("按鍵衝刺") - a SEPARATE
        // mechanic from the dive-speed-boost below (see that field's own comment for why they're
        // not the same thing), triggered by a dedicated held key (BoostPressed, see
        // IInputCommand) rather than any movement/camera state. Multiplies flightMoveSpeed
        // (works in whatever direction the player is already flying, not a fixed forward dash),
        // and only while genuinely Flying - see IsBoosting's own comment for why Glide is
        // excluded.
        [SerializeField] private float boostSpeedMultiplier = 1.8f;

        // Stacks ON TOP OF flightEnergyDrainPerSecond while boosting (not a replacement) - see
        // the design doc's own 2.3 for why this is a real, meaningfully faster drain rather than
        // a token cost, so boosting reads as "spend a burst of my reserve to go fast right now",
        // not something to just hold permanently.
        [SerializeField] private float boostEnergyDrainPerSecond = 25f;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.4) - "diving" is
        // deliberately BOTH conditions at once (camera pitched down past this threshold AND
        // actually holding descend) - either alone was rejected: camera-angle-only lets the
        // player free-ride the bonus just by looking down while flying level, and
        // descend-only drops the "look where you're diving" feel the whole feature was about.
        //
        // 2026-08-20 follow-up, real playtested feedback ("視角往下+shift時才會往下飛行") - this
        // threshold now gates whether descend happens AT ALL, not just how much of a speed bonus
        // it gets - see `diving`'s own usage further down (holding descend with the camera level
        // or looking up no longer moves the character down at all any more).
        //
        // 2026-08-20, real playtested feedback ("門檻太低 太容易觸發下淺") - doubled from 15 to
        // 30 - the gate itself was working (level camera genuinely didn't descend), but 15° is a
        // very slight tilt to already trigger a real dive off of, so it kept firing sooner than
        // felt intentional. 30° requires committing to a genuinely downward look before descend
        // engages at all, while still leaving real headroom under maxPitch(70°) for the
        // diveMultiplier to keep scaling up as you look further down, and under the existing
        // descend-auto-pitch's own 45° target (still clears this new threshold on its own).
        [SerializeField] private float divePitchThresholdDegrees = 30f;

        // Multiplier at the top of the dive range (camera pitched all the way to
        // ThirdPersonCameraController's own maxPitch, 70°) - scales in smoothly from 1x at the
        // threshold above, not a hard on/off switch, so "how hard you're looking down" reads as
        // "how much faster you're diving".
        // 2026-08-20, real playtested feedback ("俯視飛行似乎沒辦法做到真的低或直直落下") - raised
        // from 1.4 (only 5.6/sec max descend at the OLD flightDescendSpeed(4), barely different
        // from a level descend) so committing to a real look-straight-down dive (camera pitched
        // to maxPitch, holding descend) actually reads as dramatically faster than a plain
        // descend, not a marginal tweak - at the new flightDescendSpeed(6), full dive now reaches
        // 15/sec, close to boosted horizontal cruise, which is the intended "you chose to
        // commit to a real dive, it should feel powerful" payoff. The existing
        // descend-auto-pitch (eases toward 45°, not the full 70° a manual look-down reaches)
        // still gets a meaningfully fast partial dive (~11/sec) without the player needing to
        // manually aim the camera all the way down.
        [SerializeField] private float diveMaxSpeedMultiplier = 2.5f;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.5) - banking tilt,
        // driven directly by strafe input STRENGTH (moveInput.x), not actual turn rate - this
        // class's own facing/yaw is itself SmoothDampAngle'd (see the facingDirection block
        // below), so deriving bank from the turn rate would be a lagged signal on top of an
        // already-lagged signal and read as sluggish. Reading raw input directly keeps banking
        // exactly as responsive as every other flight control here.
        // 2026-08-20, real playtested feedback ("A/D搖晃角度過大") - halved from the original 20,
        // the first-pass number read as excessive once actually flown.
        [SerializeField] private float maxBankRollDegrees = 10f;
        [SerializeField] private float bankRollSmoothTime = 0.12f;

        // 2026-08-20, real playtested feedback ("體力條歸0時要快速掉落到地面 停止飛行") - Glide (a
        // soft fixed-rate fallback descent, echoing Wuthering Waves' "drop back to glider, not a
        // hard fall") existed here from 2026-08-18 through this same day, but direct play testing
        // asked for the opposite: running out of energy should drop you fast and end flight
        // outright, not ease into a second lingering airborne state. Removed entirely -
        // UpdateFlightState now just ends _isFlying on empty energy (same as landing) and lets
        // the normal gravity branch below take over immediately, same as walking off any other
        // ledge.
        //
        // Still gates (re-)entering Flight at all, not just resuming after a Glide that no longer
        // exists - see the original bug this prevents in UpdateFlightState's own comment
        // ("飛行有能耗 非一半就停下來導致飛行軌跡奇異": entering the instant CurrentEnergy > 0f
        // let a single regen tick immediately restart Flight, drain back to 0 in a fraction of a
        // second, and repeat - a visible stutter rather than a clean state change). Requiring a
        // real reserve applies to every entry now, not just a Glide-specific resume case.
        [SerializeField] private float flightResumeEnergyThreshold = 30f;

        // 2026-08-18, explicit user request (aerial combat grilling session, Q3/Q5) - until now
        // this class only ever rotated Yaw (Quaternion.Euler(0, yaw, 0)) - fine for every
        // ground fight so far since nothing was ever meaningfully above/below the player, but a
        // locked-on aerial target needs the character to actually tip its head/body up or down
        // to face it, or its attack capsule (which extends along attackOrigin.forward) would
        // still only ever reach horizontally. Clamped to +/-maxPitchDegrees so a target directly
        // overhead doesn't contort the character toward looking straight up - see AimUtility's
        // own comment for the shared clamping math (EnemyAI uses the same utility for Enemy's
        // side of the same problem).
        [SerializeField] private float maxPitchDegrees = 60f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private bool _isFlying;
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

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 3.3) - SmoothDamp
        // state for flightVerticalSmoothTime, kept separate from _horizontalVelocitySmoothDampRef
        // (different SmoothDamp call, own independent "current rate of change").
        private float _verticalVelocitySmoothDampRef;

        // SmoothDampAngle state for the banking-roll visual (maxBankRollDegrees/bankRollSmoothTime).
        private float _bankRollDegrees;
        private float _bankRollAngularVelocity;

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
        // unused "Fly" bool) and for a wing visual to toggle itself on/off.
        public bool IsFlying => _isFlying;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.5) - single source
        // of truth for the current banking-tilt angle, read by both this class's own Visual
        // child (see the pitch/roll application near the bottom of Update()) and
        // ThirdPersonCameraController (so the camera banks in sync with the character instead of
        // computing its own independent lean that could drift out of sync).
        public float CurrentBankRollDegrees => _bankRollDegrees;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.3) - true only while
        // actively spending the boost's extra energy drain (Flying + BoostPressed held), not
        // during Glide (see boostSpeedMultiplier's own comment for why boosting is excluded
        // there). Exposed publicly for any future visual/audio reacting to boost specifically,
        // same idiom as IsFlying already being exposed for WingFlap.
        public bool IsBoosting { get; private set; }

        // 2026-08-20, explicit user request ("玩家飛行下降的視角也要跟隨壓低") - true only while
        // actively holding descend during real Flight (not Glide, which no longer exists - see
        // UpdateFlightState's own comment). ThirdPersonCameraController reads this to auto-pitch
        // the camera down while descending, same "exposed for the camera to react to" idiom as
        // IsFlying/CurrentBankRollDegrees above.
        public bool IsDescending { get; private set; }

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

        // 2026-08-19, explicit user request ("光環碰到時玩家會短暫被加速") - a CheckpointGate
        // boost pad calls this the instant the player flies through it.
        //
        // 2026-08-19 follow-up, explicit user request ("經過光環時需要有向前短距離衝刺的作用") -
        // the original design multiplied moveSpeed into desiredDirection*moveSpeed, which only did
        // anything while the player was ALREADY holding a move direction - flying through a gate
        // while gliding/hovering with no input held (the common case right after an updraft
        // launch) produced literally no speed change. Replaced with a genuine one-shot forward
        // dash: ApplyDash sets an ADDITIVE world-space velocity, independent of player input, that
        // Update() sums into `motion` alongside the normal input-driven _horizontalVelocity and
        // linearly decays to zero over DashDecaySeconds - so it always visibly shoves the
        // character forward the instant it's applied, matching "短距離衝刺" (a short burst, not a
        // sustained buff) far better than the old multi-second multiplier ever did.
        private const float DashDecaySeconds = 0.5f;
        private Vector3 _dashVelocity;
        private float _dashDecayTimer;

        // 2026-08-19 follow-up, explicit user request ("穿過光圈需有短位移向前衝刺") - the pure
        // velocity-decay version above apparently didn't read as an actual "位移" (displacement/
        // dash) since it only ever built up gradually through the normal eased motion pipeline.
        // instantDisplacement adds a guaranteed, immediate CharacterController.Move() snap the
        // instant this is called - safe to call outside Update() (CharacterController.Move() is
        // designed to be callable anytime), so a fast-moving flying player gets an unmistakable
        // "shoved forward right now" pop regardless of frame timing, with the decaying velocity
        // below providing a few more frames of lingering follow-through afterward.
        public void ApplyDash(Vector3 direction, float speed, float instantDisplacement)
        {
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }
            Vector3 dir = direction.normalized;
            _controller.Move(dir * instantDisplacement);
            _dashVelocity = dir * speed;
            _dashDecayTimer = DashDecaySeconds;
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

            // 2026-08-18, explicit user request (death animation) - merged directly into the
            // same `staggered` gate everything below already uses, rather than a separate check
            // at each site: nothing in this file drives the Staggered ANIMATOR bool itself (that
            // reads StancePoise.IsStaggered directly via StaggerAnimationLink, unaffected by this
            // local variable), so `staggered` here is purely a movement/input freeze gate - safe
            // to fold death into. Without this, a dying character would keep walking/dodging/
            // flying for the ~3.5s the Dying animation plays before DeathAnimationLink actually
            // deactivates the GameObject.
            bool staggered = (stance != null && stance.IsStaggered) || (health != null && health.IsDead);

            IInputCommand inputCommand = InputCommand;
            Vector2 moveInput = !staggered && inputCommand != null ? inputCommand.MoveInput : Vector2.zero;
            CurrentMoveInput = moveInput;
            bool dodgePressed = !staggered && inputCommand != null && inputCommand.DodgePressed;
            bool jumpPressed = !staggered && inputCommand != null && inputCommand.JumpPressed;

            // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 3.3) - moved ahead
            // of the horizontal-velocity block below (it used to live right before the vertical-
            // velocity block, much further down) specifically so _isFlying is
            // already CURRENT for this frame by the time desiredVelocity picks flightMoveSpeed
            // vs moveSpeed - computing it in its old position meant horizontal speed used LAST
            // frame's flight state, a one-frame-stale read that only mattered once flight speed
            // diverged from ground speed (which it didn't, before this design). Moving the call
            // doesn't change UpdateFlightState's own behavior at all - it only reads
            // _controller.isGrounded and flightEnergy, neither affected by this file's ordering.
            bool flyHeld = !staggered && inputCommand != null && inputCommand.FlyPressed;
            bool flyDescendHeld = !staggered && inputCommand != null && inputCommand.FlyDescendPressed;
            UpdateFlightState(flyHeld);

            // Boost (Docs/FLIGHT_SYSTEM_DESIGN.md 2.3) - a dedicated held key, only while
            // genuinely Flying (not Glide - see boostSpeedMultiplier's own field comment).
            bool boostHeld = !staggered && inputCommand != null && inputCommand.BoostPressed;
            IsBoosting = _isFlying && boostHeld;
            if (IsBoosting && flightEnergy != null)
            {
                // Stacks on top of the base flightEnergyDrainPerSecond drain below (that one
                // fires unconditionally while _isFlying, this is purely additive on top of it).
                flightEnergy.Drain(boostEnergyDrainPerSecond * Time.deltaTime);
            }
            float boostMultiplier = IsBoosting ? boostSpeedMultiplier : 1f;

            // 2026-08-20, explicit user request ("玩家飛行下降的視角也要跟隨壓低") - exposed so
            // ThirdPersonCameraController can auto-pitch the camera down while actively
            // HOLDING descend in flight (not while actually falling - see `diving` right below
            // for that, this is deliberately the raw held-key signal instead), the same way it
            // already auto-centers yaw while walking forward/back (see enableDescendAutoPitch's
            // own comment on that camera class). Has to stay keyed off the raw key, not `diving`
            // below - its whole job is easing the camera down PAST `diving`'s own threshold in
            // the first place, so gating it on `diving` would create a deadlock (camera can never
            // start moving toward the very state that's supposed to unlock it).
            IsDescending = _isFlying && flyDescendHeld;

            // Dive (Docs/FLIGHT_SYSTEM_DESIGN.md 2.4, tightened 2026-08-20 - "視角往下+shift時才
            // 會往下飛行") - BOTH camera pitched down past the threshold AND actually holding
            // descend, not either alone (see divePitchThresholdDegrees' own field comment for
            // why). This is no longer just a speed-bonus condition - `diving` itself now GATES
            // whether descend happens at all (see targetVertical's own comment down in the
            // vertical-velocity block) - holding descend with a level or upward camera does
            // nothing any more, only holding it while genuinely looking down actually descends.
            float cameraPitch = CameraYawSource?.PitchDegrees ?? 0f;
            bool diving = _isFlying && flyDescendHeld && cameraPitch > divePitchThresholdDegrees;
            // 70f = ThirdPersonCameraController's own maxPitch - the top of the camera's real
            // look-down range, not a separate tunable copy of that number.
            float diveT = diving ? Mathf.InverseLerp(divePitchThresholdDegrees, 70f, cameraPitch) : 0f;
            float diveMultiplier = Mathf.Lerp(1f, diveMaxSpeedMultiplier, diveT);

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
                // Docs/FLIGHT_SYSTEM_DESIGN.md 2.3/2.4/3.3-d - flightMoveSpeed (not moveSpeed)
                // while actively Flying, then boost/dive stack multiplicatively on top - both
                // naturally settle to 1x whenever their own conditions aren't met (grounded,
                // falling, not boosting, not diving), so this line needs no extra branching for
                // those cases. Real playtested feedback ("只有在飛行時視角才能跟隨a/d") narrowed
                // this from "Flying or Gliding" to Flying alone - Glide itself has since been
                // removed entirely (see UpdateFlightState's own comment), so a plain fall now
                // correctly uses ground moveSpeed for horizontal control, not flight speed.
                float baseSpeed = _isFlying ? flightMoveSpeed : moveSpeed;
                Vector3 desiredVelocity = desiredDirection * (baseSpeed * boostMultiplier * diveMultiplier);
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

            // 2026-08-20, real playtested bug ("地面上按ctrl會有bug") - this reset used to run
            // completely unconditionally, including while _isFlying. Ascend now SmoothDamps up
            // from whatever _verticalVelocity currently is (see the flight block below) rather
            // than snapping straight to a positive target - starting from the ground, that eased
            // ramp spends its first several frames still technically negative while climbing
            // toward flightAscendSpeed. Without this !_isFlying guard, THIS reset kept slamming
            // it back to exactly -1 every single one of those frames (still grounded, still
            // negative), fighting the SmoothDamp so hard the character could never actually
            // accumulate enough upward velocity to leave the ground at all - holding Ctrl while
            // grounded just sat there vibrating instead of taking off. The old instant-assignment
            // version never hit this because it jumped straight to a positive value in one frame,
            // never re-entering this reset's `< 0f` condition again.
            if (_controller.isGrounded && _verticalVelocity < 0f && !_isFlying)
            {
                _verticalVelocity = -1f;
            }

            // Ground-only (no air jump/double jump) - checked after the grounded reset above
            // so a jump this frame isn't immediately clobbered back down to -1.
            if (jumpPressed && _controller.isGrounded)
            {
                _verticalVelocity = jumpSpeed;
            }

            if (_isFlying)
            {
                // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md 2.2/2.4/3.3-a/e)
                // - ascend/descend/hover now SmoothDamp toward their target instead of snapping
                // instantly, and the descend target additionally scales by diveMultiplier.
                // Gravity is still fully overridden while flying either way - these are direct
                // target speeds being eased toward, not forces.
                //
                // 2026-08-20, real playtested feedback ("視角往下+shift時才會往下飛行") - descend
                // now requires `diving` (camera actually pitched down past
                // divePitchThresholdDegrees, not just flyDescendHeld alone) - holding Shift with
                // a level or upward-looking camera no longer does anything at all (falls through
                // to the flyHeld/hover branch), where it used to descend regardless of where the
                // camera was pointed. This supersedes the earlier "still falling while looking
                // straight ahead" fix (a faster momentum-decay smooth time) - that fix addressed
                // residual velocity bleeding off after RELEASING descend, but the user was
                // holding Shift the whole time with a level camera, which the OLD design
                // correctly (by its own old rules) read as "keep descending" - not a bug under
                // that design, just not the behavior actually wanted. The auto-pitch below still
                // gates on plain `flyDescendHeld` (not `diving`) deliberately - it has to, since
                // its whole job is easing the camera down PAST this same threshold in the first
                // place; gating it on `diving` would mean it could never start.
                float targetVertical = diving ? -flightDescendSpeed * diveMultiplier : (flyHeld ? flightAscendSpeed : 0f);
                // See flightVerticalStopSmoothTime's own comment - releasing both keys (or
                // holding descend without looking down, which no longer actually descends either)
                // sheds whatever vertical momentum existed much faster than actively climbing or
                // diving builds it up.
                float verticalSmoothTime = (flyHeld || diving) ? flightVerticalSmoothTime : flightVerticalStopSmoothTime;
                _verticalVelocity = Mathf.SmoothDamp(_verticalVelocity, targetVertical, ref _verticalVelocitySmoothDampRef, verticalSmoothTime);
                if (flightEnergy != null)
                {
                    flightEnergy.Drain(flightEnergyDrainPerSecond * Time.deltaTime);
                }
            }
            else
            {
                // 2026-08-20, real playtested feedback ("體力條歸0時要快速掉落到地面 停止飛行") -
                // Glide (a separate soft-descent fallback state) used to live here as its own
                // branch; removed entirely, so running out of energy mid-flight now falls straight
                // into this same plain-gravity branch immediately, same as any other fall.
                _verticalVelocity += gravity * Time.deltaTime;
            }

            // See GroundSlopeUtility's own comment - isGrounded alone doesn't mean "standing
            // somewhere walkable" (a jump can land directly on another character's rounded
            // CharacterController capsule), so an active push is needed to actually slide off
            // instead of just resting there indefinitely.
            //
            // 2026-08-16 correction: originally gated purely on IsTooSteepToStandOn against
            // _controller.slopeLimit (45° default) - but a jump landing near the center of a
            // small-radius capsule's dome (e.g. Enemy's radius 0.4) contacts it at well
            // under 45° from vertical, the same as any normal walkable slope, even though
            // standing on a character's own collision capsule was never meant to be valid
            // footing regardless of the exact angle (confirmed against the real regression:
            // LandingOnTopOfEnemy_SlidesOffWithoutAnyInput still failed with a mild ~16°
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

            // Linear ease-out to zero over DashDecaySeconds - see ApplyDash's own comment. Kept
            // as a separate additive term rather than folded into _horizontalVelocity itself so
            // it survives regardless of dodge state/input and never gets clobbered by the
            // SmoothDamp above re-targeting toward whatever the player's own input wants.
            Vector3 dashContribution = Vector3.zero;
            if (_dashDecayTimer > 0f)
            {
                dashContribution = _dashVelocity * (_dashDecayTimer / DashDecaySeconds);
                _dashDecayTimer -= Time.deltaTime;
            }

            Vector3 motion = _horizontalVelocity + slideVelocity + dashContribution;
            motion.y = _verticalVelocity;
            _controller.Move(motion * Time.deltaTime);

            // Pitch eases toward _desiredPitchDegrees (0 unless locked onto a vertically-offset
            // target - see the lock-on block above), independently of the facingDirection gate
            // below so it settles back level even in the one frame a dodge/idle-no-lock-on
            // state briefly reports a zero facingDirection. Applied to _visual's LOCAL rotation
            // only, never to this transform - see _visual's own field comment for why (the
            // CharacterController capsule must stay upright).
            _pitch = Mathf.SmoothDampAngle(_pitch, _desiredPitchDegrees, ref _pitchAngularVelocity, rotationSmoothTime);

            // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md 2.5/3.3-f), scope
            // narrowed by real playtested feedback ("只有在飛行時視角才能跟隨a/d") - banking tilt
            // only while actively Flying, driven directly by strafe input strength (moveInput.x),
            // not turn rate (see maxBankRollDegrees' own field comment for why). Decays back to 0
            // the instant Flying ends (landing, falling, dodging - moveInput.x still reads
            // normally there, but targetBankRoll is forced to 0 so the character never banks on
            // the ground or during a plain fall).
            float targetBankRoll = _isFlying ? -moveInput.x * maxBankRollDegrees : 0f;
            _bankRollDegrees = Mathf.SmoothDampAngle(_bankRollDegrees, targetBankRoll, ref _bankRollAngularVelocity, bankRollSmoothTime);

            if (_visual != null)
            {
                // Negated: Unity's Euler X convention is inverted from AimUtility's
                // "positive = looking up" (confirmed empirically - Quaternion.Euler(+X,0,0) *
                // forward tips DOWN, not up). This was a real latent sign bug in the original
                // (pre-Visual-child) version of this line - never actually caught before because
                // only _desiredPitchDegrees's raw value was reflection-tested, not the resulting
                // forward vector.
                _visual.localRotation = Quaternion.Euler(-_pitch, 0f, _bankRollDegrees);
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

        // Entering flight requires holding the key with a real energy reserve available
        // (grounded or airborne - lifting straight off the ground is intentional, "自由飛行"
        // reads as more than just an air-only ability). Once active, flight PERSISTS regardless
        // of whether the key is still held - see Update()'s own flight block: releasing simply
        // hovers instead of falling - and only actually ends on landing or running out of
        // energy. This asymmetry (easy entry condition, sticky exit condition) is deliberate:
        // "按住鍵自由飛行" describes holding the key to CONTROL flight, not to merely stay
        // airborne.
        //
        // 2026-08-20, real playtested feedback ("體力條歸0時要快速掉落到地面 停止飛行") - a Glide
        // fallback state used to live here (2026-08-18 through this same day): running out of
        // energy dropped into a soft fixed-rate descent instead of ending flight outright. Real
        // play testing asked for the opposite - removed entirely, so running out of energy now
        // just ends _isFlying immediately, same as landing, and the normal gravity branch in
        // Update() takes over right away (a real, accelerating fall, not a lingering glide).
        //
        // Re-entry requires flightResumeEnergyThreshold (not just > 0f) even on a completely
        // fresh first-ever entry now, not only when recovering from a just-emptied tank - a real
        // playtested bug this avoids (previously only guarded on the Glide-resume path):
        // requiring just > 0f let a single passive regen tick immediately restart Flight the
        // instant it ran dry, drain back to 0 in a fraction of a second, and repeat - a visible
        // stutter rather than a clean state change. Applying the same threshold universally
        // means that bug can't resurface now that Glide (its own dedicated guard) is gone.
        private void UpdateFlightState(bool flyHeld)
        {
            if (_isFlying)
            {
                // 2026-08-20, real playtested bug ("地面上按ctrl會有bug") - this used to be plain
                // `_controller.isGrounded` alone, which was fine when ascend snapped the vertical
                // velocity straight to its target in one frame (see flightVerticalSmoothTime's
                // own comment) - a single frame of +flightAscendSpeed was already enough real
                // displacement to clear the ground before the NEXT frame's check ever ran. Once
                // ascend became a SmoothDamp ramp instead, liftoff from a standstill takes several
                // frames to build real upward speed - isGrounded can still legitimately read true
                // for those first few frames purely because the character genuinely hasn't moved
                // far enough yet, NOT because flight ended. Checking isGrounded alone killed
                // _isFlying on literally the first of those frames, which reset the vertical
                // SmoothDamp's target back toward gravity/off, which kept it grounded, which
                // killed flight again next frame too - holding Ctrl on the ground just sat there
                // vibrating, never actually taking off. `!flyHeld` excludes exactly that window:
                // as long as the key commanding ascend is still held, a transient "still touching
                // the ground mid-liftoff" reading doesn't count as landing. Releasing Ctrl while
                // still genuinely airborne (isGrounded false) still correctly keeps hovering
                // rather than ending flight either - only BOTH being true (actually on the ground
                // AND not currently trying to ascend) reads as a real landing.
                bool landed = _controller.isGrounded && !flyHeld;
                if (landed || flightEnergy == null || flightEnergy.CurrentEnergy <= 0f)
                {
                    _isFlying = false;
                }

                return;
            }

            if (flyHeld && flightEnergy != null && flightEnergy.CurrentEnergy >= flightResumeEnergyThreshold)
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
        // (LandingOnTopOfEnemy_SlidesOffWithoutAnyInput) that the slide never actually
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
