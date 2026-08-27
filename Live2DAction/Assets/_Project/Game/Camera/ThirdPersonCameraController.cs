using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Input;
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
        // the "character disappears near Enemy" report - see enableCameraCollision below)
        // - raised from the previous 0.8 for more breathing room. targetOffset.y=0.5 is
        // separate hands-on tuning from 2026-08-11/12, unchanged. 0 distance would be true
        // first-person (camera at the eye point, no offset) - SetOwnVisualHidden below still
        // handles that case if distance is ever tuned back to 0, but it's not the current mode.
        [SerializeField] private float distance = 2f;
        [SerializeField] private Vector3 targetOffset = new Vector3(0f, 0.5f, 0f);

        // 2026-08-23, explicit user request ("接下來我要調整第一視角 有辦法讓我自己調整到滿意位置
        // 再給你微調嗎") - split out from targetOffset above specifically so live-tweaking the
        // first-person eye position in Play Mode can never disturb the already-hand-tuned
        // third-person targetOffset/distance (see that field's own comment - user-owned data,
        // not to be touched incidentally). Defaults to whatever targetOffset's current live value
        // was at the moment this field was added, so first-person starts from the exact same eye
        // position it already used before this split - a pure refactor, no visual change on its
        // own.
        [SerializeField] private Vector3 firstPersonEyeOffset = new Vector3(0.5f, 0.5f, 0f);

        // 2026-08-23, explicit user request ("第一視角下要盡量讓敵人能夠全身進入到畫面正中間") - at
        // typical melee range (~1.2m) an enemy roughly the player's own height doesn't fit
        // head-to-toe within the base Camera component's third-person FOV (65°, tuned for the
        // over-the-shoulder view, never meant for anything this close) - the bottom of their
        // model gets cropped off-screen. Independent first-person-only FOV, same "split so tuning
        // one view can't disturb the other" convention as firstPersonEyeOffset - third person
        // keeps whatever FOV is set on the Camera component itself.
        [SerializeField] private float firstPersonFieldOfView = 78f;

        // Captured from the Camera component the first time it's needed, so third person always
        // reverts to whatever FOV was actually authored on the component (not a hardcoded
        // default) once aiming ends.
        private Camera _camera;
        private float? _thirdPersonFieldOfView;

        // 2026-08-23, explicit user request ("新增只有在瞄準時作用鍵盤按鍵 :a 視角持續放大 e 視角持續
        // 變小") - a scope-style zoom layered ON TOP of firstPersonFieldOfView while actually
        // aiming: holding A narrows the FOV (magnifies, "放大") toward minZoomFieldOfView, holding
        // E widens it back out (de-magnifies, "變小") no further than firstPersonFieldOfView
        // itself - E backs off an existing zoom-in, it was never meant to zoom OUT past the
        // normal aim view into something wider. Resets to firstPersonFieldOfView every time
        // aiming freshly starts (same off->on edge as the yaw-snap-to-facing block below) so a
        // leftover zoom level from a previous aim never silently carries into the next one.
        [SerializeField] private float minZoomFieldOfView = 20f;
        [SerializeField] private float zoomSpeedDegreesPerSecond = 40f;
        private float _currentZoomFieldOfView = -1f;

        // 2026-08-23, explicit user request ("第一人稱視角不能360度環繞 只能是正面下水平與垂直控制
        // 視角") - how far (degrees) the player can look away from the facing/lock-on direction
        // while in first person, each side. Third person is untouched (still full 360 free orbit) -
        // see the yaw-clamp block in LateUpdate for where this is applied and why.
        [SerializeField] private float firstPersonMaxYawDeviation = 80f;

        // 2026-08-23, real playtested bug ("第一人稱下看不到r技能整個特效過程") - the R ultimate's
        // sword-throw sequence (spin/rise/fly-to-target/embed/return) plays out well away from the
        // player's own head, which a first-person camera glued to the back of that head simply
        // isn't pointed at for most of it. Forces third person for the ultimate's whole active
        // window regardless of held aim/V-toggle, same "player's own choice gets overridden for a
        // specific scripted window" precedent as aiming itself overriding the flight-distance
        // multiplier. Optional or null-safe (see the aiming computation in LateUpdate) so a
        // character/test setup without an UltimateAbility wired in behaves exactly as before.
        [SerializeField] private UltimateAbility ultimateAbility;

        // 2026-08-23, explicit user request ("我在調整First Person Eye Offset時為何從game畫面看不到
        // 玩家的角色外觀") - first-person always hides the body (SetOwnVisualHidden below) so you
        // don't see the inside of your own head, which is correct for normal play but makes it
        // hard to SEE the head/eye position you're trying to line firstPersonEyeOffset up with
        // while tuning it live. Checking this box keeps the body visible even while
        // aiming/toggled into first-person, purely so the Scene/Game view still shows where the
        // head actually is as a reference point - leave unchecked for normal play.
        [SerializeField] private bool debugKeepVisualVisibleInFirstPerson;

        // 2026-08-23, explicit user request ("第一人稱通常都會看的到自己的攻擊部位 不然不知道自己
        // 甚麼時候出招") - SetOwnVisualHidden below hides EVERY renderer under Visual so you don't
        // see the inside of your own head, but that includes the equipped weapon too, which is
        // exactly what a player needs to see swing to tell an attack actually happened. Optional
        // reference to the weapon's own root Transform (e.g. Player's WolfsGravestone) - anything
        // under it is excluded from the hide, same "everything else hidden, this one thing stays"
        // convention a first-person view-model uses. Left null is safe (falls back to hiding
        // everything, today's behavior) for any character that doesn't wire one in.
        [SerializeField] private Transform firstPersonVisibleWeapon;

        // 2026-08-23, explicit user request ("我調整了大劍到想要位置 但是第一人稱下會遮擋視線") - the
        // sword (WolfsGravestone) is a detached back accessory parented directly under Player, NOT
        // under "Visual", so SetOwnVisualHidden below never touches it - it stays rendered at full
        // opacity regardless of first/third person. That's fine in third person (it's the whole
        // point of the accessory), but once the eye camera sits at firstPersonEyeOffset near the
        // character's own head/back, the sword ends up right in front of the lens and blocks the
        // view. Opposite convention from firstPersonVisibleWeapon above: this one gets hidden
        // WHILE aiming/first-person and shown otherwise, independent of the Visual-hide policy.
        // Left null is safe (no-op) for setups without a back accessory.
        [SerializeField] private Transform firstPersonHiddenAccessory;

        // 2026-08-18, explicit user request (flight system grilling session, Q5 - "飛行時鏡頭
        //通常會拉遠一點") - multiplies `distance` (never overwrites it - see that field's own
        // comment on why it's user-owned tuning) while the target is Flying/Gliding, so more of
        // the terrain below is visible during open-world traversal. Applied BEFORE the existing
        // obstruction clamp below, not instead of it - flying near a cliff face still pulls the
        // camera back in exactly the same way normal movement already does, this only changes
        // the desired/unobstructed baseline.
        [SerializeField] private float flightDistanceMultiplier = 1.4f;

        // 2026-08-12: real bug report ("很靠近敵人時角色1突然消失，畫面定格") persisted even
        // after fixing the CharacterController-climbing root cause and raising distance to 2 -
        // this project genuinely never had any camera collision avoidance (documented as a
        // known gap since 2026-08-12's earlier greybox pass), and a naive orbit camera with no
        // obstruction check WILL end up positioned inside nearby geometry (Enemy's own mesh,
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

        // 2026-08-23, real playtested bug ("第三人稱與enemy近距離打鬥時 視角會突然靠得很近 甚至穿模
        // 到enemy頭部") - ClampDistanceForObstruction's old floor was a bare 0f: nothing stopped an
        // obstruction detected right up close (an Enemy's own body standing near the camera's
        // orbit path during melee range - exactly the "avoid clipping into Enemy" case this whole
        // system exists for, see enableCameraCollision's own history above) from collapsing
        // usedDistance down toward zero, landing the camera almost AT the look-at point - inside
        // whatever's nearby, including Enemy's own head. This floor keeps the camera at least
        // this far out even when something is detected closer than that, so avoidance can still
        // pull the camera in but never all the way into point-blank/inside-the-mesh range.
        [SerializeField] private float minCollisionDistance = 0.8f;

        // Same bug's other half: the clamp used to apply instantly every frame with no damping,
        // so a transient obstruction (Enemy's own body briefly crossing the SphereCast path while
        // circling during a fight) read as the camera literally teleporting close then snapping
        // back out the instant it cleared - "突然靠得很近". Smoothed via MoveTowards in LateUpdate
        // below (see _smoothedCollisionDistance) at this many units/second - fast enough to still
        // catch a player running straight at a wall before it's visibly clipped, slow enough that
        // a passing Enemy reads as the camera easing in and out rather than cutting.
        [SerializeField] private float collisionDistanceSmoothSpeed = 12f;

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

        // 2026-08-23, explicit user request ("瞄準時瞬間變成第一視角") - reuses this class's own
        // pre-existing distance=0 true-first-person mode (see `distance` field's own comment:
        // "0 distance would be true first-person... SetOwnVisualHidden below still handles that
        // case") rather than building a second camera/visual-hiding path - aiming just becomes
        // another source that can drive the EFFECTIVE distance to 0 for as long as it's held,
        // same as flightDistanceMultiplier is another source that scales it up. "瞬間" (instant)
        // is satisfied by simply not lerping this at all - the assignment in LateUpdate below is
        // a hard cut every frame, on or off, no transition.
        [SerializeField] private MonoBehaviour inputSource;

        // 2026-08-20, explicit user request ("玩家飛行下降的視角也要跟隨壓低") - same
        // gated-easing idiom as enableAutoCenter above (cedes control the instant real mouse
        // input arrives, same isLooking check), but for PITCH instead of yaw, and gated on
        // CharacterMovement.IsDescending instead of "walking forward/back". Complements the
        // existing dive-speed-boost (CharacterMovement 2.4, which reads THIS camera's pitch to
        // decide how much to accelerate a dive) - that one reacts to the player already looking
        // down while descending; this one is the other half, nudging the camera to actually look
        // down in the first place once you start descending, so the two reinforce each other
        // (hold descend -> camera eases down -> now looking down while still descending -> dive
        // bonus kicks in) instead of requiring the player to manually aim down first.
        [SerializeField] private bool enableDescendAutoPitch = true;

        // How far down the camera eases toward while descending - short of maxPitch(70) so it
        // reads as "leaning to look at what's below", not slammed to the same steep angle the
        // dive bonus's own top end uses.
        [SerializeField] private float descendAutoPitchTargetDegrees = 45f;

        // Same Lerp-per-frame idiom as autoCenterSpeed, not a literal degrees/second rate.
        [SerializeField] private float descendAutoPitchSpeed = 2.5f;

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

        // 2026-08-23, explicit user request ("V鍵切換成第一視角(機制與右鍵瞄準同理)") - a persistent
        // toggle (not a held state like AimPressed itself): pressing V flips this, and it stays
        // flipped until pressed again. Combined with `aiming` in LateUpdate below via OR, so
        // either holding right-click OR having toggled this on drives the exact same distance=0
        // true-first-person path - whichever is active, releasing/toggling off the OTHER one
        // still leaves you in first-person until BOTH are off.
        private bool _viewToggledFirstPerson;

        // 2026-08-23, real playtested bug ("我現在的play mode視角跑到了側面(第一人稱模式)") - _yaw is
        // free-look and only auto-centers behind the player while walking forward/back (see
        // enableAutoCenter's gate below), so standing still or strafing after looking around
        // leaves _yaw wherever the mouse last put it. Entering first person (aim or V-toggle)
        // used to reuse that stale _yaw as-is, so the eye camera could end up staring off to the
        // side of the character's own facing instead of straight ahead. Tracked here purely to
        // detect the off->on edge below and snap _yaw to the character's facing at that instant.
        private bool _wasAimingLastFrame;

        // Built lazily on first LateUpdate rather than in Awake/OnEnable, same reasoning as
        // CharacterMovement/PlayerCombat's lazy-init fields: tests assign initialYaw/
        // initialPitch via reflection right after AddComponent, which already runs
        // Awake/OnEnable synchronously.
        private bool _initialized;

        // Seconds since the last frame with a nonzero mouse delta - drives the auto-center
        // gate below. Starts at a large value so a scene that never receives mouse input
        // doesn't need to "wait out" the delay from zero.
        private float _timeSinceLookInput = 999f;

        // See collisionDistanceSmoothSpeed's own comment - the actual applied camera distance
        // eases toward the collision-clamped target rather than snapping to it every frame.
        // Negative sentinel so LateUpdate's first frame initializes it to that frame's own
        // desiredDistance instead of easing FROM zero (which would read as the camera starting
        // buried at the look-at point and rushing outward on the very first frame).
        private float _smoothedCollisionDistance = -1f;

        // Resolved lazily (like CharacterMovement/TargetLockController's own
        // reflection-friendly properties) rather than in Awake, and re-resolved if target
        // changes - only used for the optional auto-center's "is the player actually moving"
        // check.
        private CharacterMovement _targetMovement;
        private Transform _targetMovementFor;

        private ILockOnSource LockOnSource => lockOnSource as ILockOnSource;
        private IInputCommand InputCommand => inputSource as IInputCommand;

        public float YawDegrees => _yaw;

        // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 3.1/3.2) - lets
        // CharacterMovement read how far down the camera is currently looking (dive-speed-boost
        // condition needs both "holding descend" AND "looking down past a threshold").
        public float PitchDegrees => _pitch;

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

            // Resolved here (rather than down where roll consumes it below) so the descend
            // auto-pitch block inside the isPlaying check right below can also read
            // targetMovement.IsDescending without a second lookup.
            CharacterMovement targetMovement = ResolveTargetMovement();

            if (Application.isPlaying)
            {
                // 2026-08-23, explicit user request ("V鍵切換成第一視角") - flips the persistent
                // toggle exactly once per press (ViewTogglePressed is already an edge trigger from
                // PlayerInputProvider, not a held signal, so this can't double-flip within a
                // single held frame).
                if (InputCommand != null && InputCommand.ViewTogglePressed)
                {
                    _viewToggledFirstPerson = !_viewToggledFirstPerson;
                }

                // 2026-08-23, explicit user request ("進入第一人稱時自動對齊角色朝向") - the instant
                // aim/V-toggle turns first person ON (off->on edge only, not held), snap yaw to
                // the character's current facing so first person always starts looking straight
                // ahead instead of wherever free-look last left the camera. Only yaw - pitch is
                // left untouched since "facing" has no up/down component and the player's last
                // deliberate up/down look is still worth keeping.
                bool aimingNow = InputCommand != null && (InputCommand.AimPressed || _viewToggledFirstPerson);
                if (aimingNow && !_wasAimingLastFrame)
                {
                    _yaw = target.eulerAngles.y;
                    _currentZoomFieldOfView = firstPersonFieldOfView;
                }

                // See minZoomFieldOfView/zoomSpeedDegreesPerSecond's own comment - only adjusts
                // while actually aiming, held keys otherwise have no effect (checked separately
                // from aimingNow above so this can't run stale on the frame aiming just ended).
                if (aimingNow && InputCommand != null)
                {
                    if (InputCommand.ZoomInPressed)
                    {
                        _currentZoomFieldOfView = Mathf.MoveTowards(_currentZoomFieldOfView, minZoomFieldOfView, zoomSpeedDegreesPerSecond * Time.deltaTime);
                    }
                    if (InputCommand.ZoomOutPressed)
                    {
                        _currentZoomFieldOfView = Mathf.MoveTowards(_currentZoomFieldOfView, firstPersonFieldOfView, zoomSpeedDegreesPerSecond * Time.deltaTime);
                    }
                }
                _wasAimingLastFrame = aimingNow;

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

                // 2026-08-20, explicit user request ("玩家飛行下降的視角也要跟隨壓低") - same
                // isLooking gate as auto-center above (manual mouse input always wins instantly),
                // but no delay before it engages - holding descend is already a deliberate,
                // explicit action (unlike "walking forward" which auto-center deliberately waits
                // out in case it's incidental), so there's no need to wait out a quiet period
                // first. Not gated on lock-on either - auto-center defers to lock-on because a
                // locked camera has its own facing logic entirely, but pitch during a lock-on is
                // still whatever the lock-on aim leaves it at, nothing here conflicts with that.
                //
                // 2026-08-20, real playtested bug ("我打鏡頭抬到最上方 為何SHIFT還會往下") - this
                // used to fire purely off IsDescending (just "is Shift held"), with no regard for
                // which way the player had just manually pitched the camera. The instant the
                // mouse stopped moving (isLooking goes false the very next frame, even right
                // after deliberately pitching all the way up to minPitch) while still holding
                // Shift, this yanked the camera straight back down toward
                // descendAutoPitchTargetDegrees - completely overriding a just-made deliberate
                // look-up, and once it crossed CharacterMovement's own dive threshold, real
                // descend kicked in right along with it. Since camera angle now GATES whether
                // Shift descends at ALL (not just a speed bonus - see
                // CharacterMovement.divePitchThresholdDegrees' own history), silently dragging
                // the camera down against the player's own explicit choice defeats the entire
                // point of that gate. `_pitch >= 0f` added: only ever assists FROM level or
                // already-somewhat-down, never fights a genuine look-up - if the player has
                // pitched up at all, this auto-assist backs off entirely and leaves the camera
                // exactly where they put it.
                if (enableDescendAutoPitch && !isLooking && _pitch >= 0f && targetMovement != null && targetMovement.IsDescending)
                {
                    _pitch = Mathf.Lerp(_pitch, descendAutoPitchTargetDegrees, descendAutoPitchSpeed * Time.deltaTime);
                }

                // 2026-08-23, explicit user request ("改成第一人稱一樣可以360環繞 但滾輪鎖定時則限制
                // 視角") - supersedes the last two revisions of this block (see git history for the
                // "always clamp to own facing" and "always clamp to enemy direction" attempts this
                // replaced). First person with NO lock-on is now full free 360 orbit, exactly like
                // third person - the restriction only exists at all while a lock-on (mouse-wheel,
                // TargetLockController) is actually active, same Sekiro locked-duel feel as before
                // but now opt-in via the player's own choice to lock on, rather than a blanket
                // restriction on first person itself. Third person is untouched either way (still
                // full free orbit, lock-on or not - see the auto-center gate above, still keyed off
                // LockOnSource, for third person's own separate lock-on behavior).
                bool aimingForYawClamp = InputCommand != null
                    && (InputCommand.AimPressed || _viewToggledFirstPerson)
                    && (ultimateAbility == null || !ultimateAbility.IsActive);
                Transform lockedTargetForYawClamp = LockOnSource?.LockedTarget;
                if (aimingForYawClamp && lockedTargetForYawClamp != null)
                {
                    Vector3 toLockedTarget = lockedTargetForYawClamp.position - target.position;
                    toLockedTarget.y = 0f;
                    float centerYaw = toLockedTarget.sqrMagnitude > 0.0001f
                        ? Mathf.Atan2(toLockedTarget.x, toLockedTarget.z) * Mathf.Rad2Deg
                        : target.eulerAngles.y;

                    _yaw = ClampYawToFacingCone(_yaw, centerYaw, firstPersonMaxYawDeviation);
                }
            }

            // 2026-08-20, flight system design (Docs/FLIGHT_SYSTEM_DESIGN.md, 2.5/3.2) - the
            // camera USED TO bank by the same angle as the character's own visual roll during
            // flight strafing. CharacterMovement.CurrentBankRollDegrees itself is untouched (the
            // character's own visual lean during flight still happens, and anything else that
            // wants to read it still can) - only the CAMERA's own consumption of it is removed.
            //
            // 2026-08-25, user feedback ("鎖定目標後無論距離多少都保持直線站立", confirmed with a
            // screenshot and restated unconditionally as "角色應該相對螢幕來說是直立的而非傾斜") -
            // first tried zeroing roll only while locked on; the user then clarified the
            // requirement is unconditional, not lock-on-specific - the character should read as
            // upright on screen at all times, full stop. Camera roll is now always 0.
            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);

            // 2026-08-20, real playtested feedback ("體力條歸0時要快速掉落到地面 停止飛行") - Glide
            // (the soft fixed-rate fallback state) has been removed entirely from
            // CharacterMovement (see that class's own UpdateFlightState comment) - running out
            // of energy now just falls under normal gravity, so this only needs to check
            // IsFlying any more.
            bool targetAirborneUnderControl = targetMovement != null && targetMovement.IsFlying;
            float desiredDistance = targetAirborneUnderControl ? distance * flightDistanceMultiplier : distance;

            // 2026-08-26, explicit user request ("隻狼那種3d動作中,玩家小體積面對boss大體積的視角") -
            // see LockOnTarget.CameraDistanceMultiplier's own comment. Multiplies on top of
            // whatever desiredDistance already is (flight multiplier included) rather than
            // overwriting - `distance` itself (the user's own hands-on-tuned base value) is never
            // touched, same non-destructive precedent as flightDistanceMultiplier.
            Transform lockedAimPoint = LockOnSource?.LockedTarget;
            if (lockedAimPoint != null)
            {
                var lockOnTargetComp = lockedAimPoint.GetComponentInParent<LockOnTarget>();
                if (lockOnTargetComp != null)
                {
                    desiredDistance *= lockOnTargetComp.CameraDistanceMultiplier;
                }
            }

            // Aiming overrides everything else about distance (flight multiplier included) -
            // an instant hard cut to true first-person for as long as the button is held, not a
            // blend, per "瞬間變成第一視角" above. 2026-08-23: ORed with the V-key toggle (see
            // _viewToggledFirstPerson's own comment) - "機制與右鍵瞄準同理" (same mechanism as
            // right-click aim) means both drive the identical first-person path, not a second
            // parallel one.
            bool aiming = Application.isPlaying
                && InputCommand != null
                && (InputCommand.AimPressed || _viewToggledFirstPerson)
                && (ultimateAbility == null || !ultimateAbility.IsActive);
            if (aiming)
            {
                desiredDistance = 0f;
            }

            // See firstPersonFieldOfView's own comment - swaps the Camera component's FOV for
            // first person only, restoring whatever third-person FOV was there before.
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }
            if (_camera != null)
            {
                if (!_thirdPersonFieldOfView.HasValue)
                {
                    _thirdPersonFieldOfView = _camera.fieldOfView;
                }
                float aimFieldOfView = _currentZoomFieldOfView >= 0f ? _currentZoomFieldOfView : firstPersonFieldOfView;
                _camera.fieldOfView = aiming ? aimFieldOfView : _thirdPersonFieldOfView.Value;
            }

            float usedDistance = desiredDistance;

            // 2026-08-23 - first-person uses its own independent offset (see
            // firstPersonEyeOffset's own comment for why this was split out of targetOffset),
            // third-person keeps using targetOffset exactly as before.
            //
            // 2026-08-23, explicit user request ("第一人稱的部分能不能讓攝影機起始位置與玩家並列
            // (在玩家右手邊) 去拍向前方視角") - ComputeCameraPosition below adds this offset to
            // target.position as a PLAIN WORLD-SPACE vector, unrotated. That's fine for
            // targetOffset (third person already orbits on the camera's own free-look yaw, so a
            // small non-rotating world nudge to the look-at point is imperceptible), but it means
            // firstPersonEyeOffset was never actually "beside the character" at all - it was only
            // correct at whatever single world orientation happened to be current when it was
            // tuned, and would silently drift to the wrong side/depth (even back INSIDE the body,
            // undoing the Z-depth fix from earlier this session) the moment the character turned
            // to face any other direction. Rotating by target.rotation here makes
            // firstPersonEyeOffset a proper body-local (right, up, forward) offset that tracks the
            // character's own facing correctly - third person's targetOffset is untouched, still
            // the exact same non-rotating world vector it always was.
            Vector3 effectiveOffset = aiming ? target.rotation * firstPersonEyeOffset : targetOffset;

            // 2026-08-26, explicit user request (big-boss framing - see LockOnTarget.CameraFrameBias's
            // own comment) - shifts the look-at point from the player toward the locked target's
            // AimPoint by a tunable fraction, so the boss dominates frame while the player is still
            // visible near the edge rather than dead-center. Only affects third person (aiming's
            // first-person offset above is untouched); 0 bias (every existing LockOnTarget) is a
            // no-op add of Vector3.zero, so normal enemy lock-on behavior is bit-for-bit unchanged.
            if (!aiming)
            {
                Transform lockedAimPointForFraming = LockOnSource?.LockedTarget;
                if (lockedAimPointForFraming != null)
                {
                    var framingTargetComp = lockedAimPointForFraming.GetComponentInParent<LockOnTarget>();
                    if (framingTargetComp != null && framingTargetComp.CameraFrameBias > 0f)
                    {
                        Vector3 towardBoss = lockedAimPointForFraming.position - (target.position + effectiveOffset);
                        effectiveOffset += towardBoss * framingTargetComp.CameraFrameBias;
                    }
                }
            }

            // Physics.SphereCast needs the live scene, so this can't live in the pure
            // ComputeCameraPosition helper below - it only runs while actually playing, same
            // as the mouse-look block above (there's nothing meaningful to collide against
            // while just previewing the starting angle in Edit mode, and Physics queries
            // against a non-playing scene's colliders aren't reliable anyway).
            if (Application.isPlaying && enableCameraCollision)
            {
                Vector3 lookAtPoint = target.position + effectiveOffset;
                float? obstruction = FindObstructionDistance(lookAtPoint, rotation, desiredDistance);
                float clampedDistance = ClampDistanceForObstruction(desiredDistance, obstruction, cameraCollisionSkin, minCollisionDistance);

                if (_smoothedCollisionDistance < 0f)
                {
                    _smoothedCollisionDistance = clampedDistance;
                }
                _smoothedCollisionDistance = Mathf.MoveTowards(_smoothedCollisionDistance, clampedDistance, collisionDistanceSmoothSpeed * Time.deltaTime);
                usedDistance = _smoothedCollisionDistance;
            }
            else
            {
                _smoothedCollisionDistance = -1f;
            }

            Vector3 position = ComputeCameraPosition(target.position, rotation, usedDistance, effectiveOffset);

            transform.SetPositionAndRotation(position, rotation);

            // 2026-08-26, explicit user request ("第一人稱下角色要隱藏 不然會遮擋") - reverts the
            // 2026-08-23 "always show the body in first person" experiment (see git history for
            // that comment) back to hiding it. debugKeepVisualVisibleInFirstPerson still overrides
            // this back to visible when checked (tuning aid, see its own comment) -
            // firstPersonVisibleWeapon still keeps the equipped weapon rendered even while the rest
            // of the body is hidden, so attacks stay readable in first person.
            SetOwnVisualHidden(aiming && !debugKeepVisualVisibleInFirstPerson);

            // See firstPersonHiddenAccessory's own comment - hides the back-mounted sword only
            // while the eye camera is active, independent of the body-visible policy above.
            SetAccessoryHidden(firstPersonHiddenAccessory, aiming);
        }

        private static void SetAccessoryHidden(Transform accessory, bool hidden)
        {
            if (accessory == null)
            {
                return;
            }

            foreach (Renderer accessoryRenderer in accessory.GetComponentsInChildren<Renderer>(true))
            {
                accessoryRenderer.enabled = !hidden;
            }
        }

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - FindObstructionDistance
        // below runs every LateUpdate for the active player camera and used to call
        // Physics.SphereCastAll, allocating a fresh RaycastHit[] every frame. Reused buffer +
        // SphereCastNonAlloc instead - same query (mask/QueryTriggerInteraction unchanged).
        private readonly RaycastHit[] _obstructionHitsBuffer = new RaycastHit[16];

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

            // 2026-08-26, real playtested bug ("玩家過於靠近武士時鎖定圖標會消失並且視角突然變得很近")
            // - same bug class as the 2026-08-23 Enemy one below (player.transform.root already
            // excluded), but for a LOCKED TARGET instead: the locked boss's own body is deliberately
            // supposed to fill the frame at close range (see LockOnTarget.CameraDistanceMultiplier/
            // CameraFrameBias), so its collider must never count as "an obstruction in the way" the
            // same way a wall would - otherwise every desired-distance/frame-bias tuning gets
            // silently overridden the instant the boss's own CharacterController crosses the
            // SphereCast path, which for a screen-filling giant is most of the time. The lock-on
            // indicator "disappearing" was a symptom of this, not a separate bug - LockOnIndicator
            // positions itself relative to the (now yanked-in) camera and can end up behind/inside
            // its near clip plane once distance collapses to near-zero.
            Transform lockedTargetRoot = LockOnSource?.LockedTarget != null
                ? LockOnSource.LockedTarget.root
                : null;

            Vector3 direction = -(rotation * Vector3.forward);
            int hitCount = Physics.SphereCastNonAlloc(lookAtPoint, cameraCollisionRadius, direction, _obstructionHitsBuffer, desiredDistance, ~0, QueryTriggerInteraction.Ignore);

            float? closest = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _obstructionHitsBuffer[i];
                if (hit.collider == null || hit.collider.transform.root == target || hit.collider.transform.root == lockedTargetRoot)
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
        //
        // 2026-08-23, real playtested bug ("第三人稱與enemy近距離打鬥時 視角會突然靠得很近 甚至穿模
        // 到enemy頭部") - minDistance added as an optional param (defaults to 0f, the previous
        // hardcoded floor - existing tests calling the 3-arg form keep testing that exact
        // behavior unchanged) so a nearby obstruction (most often Enemy's own body during melee
        // range - the very thing this whole system exists to avoid clipping into) can no longer
        // collapse the camera all the way down to point-blank/inside-the-mesh range.
        public static float ClampDistanceForObstruction(float desiredDistance, float? obstructionDistance, float skin, float minDistance = 0f)
        {
            if (!obstructionDistance.HasValue)
            {
                return desiredDistance;
            }

            return Mathf.Clamp(obstructionDistance.Value - skin, minDistance, desiredDistance);
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
            CharacterMovement movement = ResolveTargetMovement();
            if (movement == null || movement.CurrentHorizontalSpeed <= 0.05f)
            {
                return false;
            }

            Vector2 moveInput = movement.CurrentMoveInput;
            return Mathf.Abs(moveInput.y) >= Mathf.Abs(moveInput.x);
        }

        // Extracted from IsPlayerWalkingForwardOrBack's own former inline lazy-resolve so the
        // flight distance check in LateUpdate can share the same cached lookup instead of
        // re-resolving GetComponent every frame independently - same "resolved lazily, re-
        // resolved if target changes" reasoning that field's own comment already documents.
        private CharacterMovement ResolveTargetMovement()
        {
            if (_targetMovement == null || _targetMovementFor != target)
            {
                _targetMovement = target != null ? target.GetComponent<CharacterMovement>() : null;
                _targetMovementFor = target;
            }

            return _targetMovement;
        }

        // Pure so the auto-center easing itself is directly EditMode-testable, same reasoning
        // as ComputeCameraPosition/CameraRelativeDirection - the gating conditions (isLooking,
        // delay, moving, lock-on) live in LateUpdate since they need live component state.
        public static float ComputeAutoCenterYaw(float currentYaw, float targetYaw, float autoCenterSpeed, float deltaTime)
        {
            return Mathf.LerpAngle(currentYaw, targetYaw, autoCenterSpeed * deltaTime);
        }

        // Pure so the clamp math is directly EditMode-testable, same convention as
        // ComputeAutoCenterYaw/ClampDistanceForObstruction above. Mathf.DeltaAngle gives the
        // shortest signed difference in [-180, 180] regardless of how far currentYaw has
        // accumulated past a single turn (see _yaw's own unbounded-accumulation comment), so this
        // stays correct no matter how many times the player has spun the camera historically.
        public static float ClampYawToFacingCone(float currentYaw, float centerYaw, float maxDeviationDegrees)
        {
            float delta = Mathf.DeltaAngle(centerYaw, currentYaw);
            float clampedDelta = Mathf.Clamp(delta, -maxDeviationDegrees, maxDeviationDegrees);
            return centerYaw + clampedDelta;
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
                // The weapon stays visible even while everything else is hidden - see
                // firstPersonVisibleWeapon's own comment for why.
                bool isVisibleWeapon = firstPersonVisibleWeapon != null && visualRenderer.transform.IsChildOf(firstPersonVisibleWeapon);
                visualRenderer.enabled = !hidden || isVisibleWeapon;
            }
        }
    }
}
