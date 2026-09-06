// Dev overlay only - compiled into the Editor and Development builds, stripped from release
// builds (its GameObject in Map_School then loads as a harmless missing-script slot).
#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Live2DAction.AI.Boss.Yuanpei;

namespace Live2DAction.DebugTools
{
    // 2026-09-05, user request ("有沒有一種開發者模式 可讓我讓清楚看到boss的每一種攻擊手段的機制 外觀
    // ui 範圍等等 專門用來優化美術系統的"). Same spirit as BossAnimationDebugMode (F7, 武士/屁孩王's
    // Animator-clip inspector) but yuanpei_LogoSky has no Animator-driven attacks - every attack is a
    // procedural coroutine in YuanpeiAttacks, selected at random by YuanpeiScheduler behind cooldown/
    // range/energy gates. This tool bypasses the scheduler entirely: F8 pauses the boss's own FSM
    // (YuanpeiBoss.Update, so it stops hovering/picking attacks on its own) and lets the number keys
    // fire any pool attack directly via YuanpeiAttacks.Run(), on demand, regardless of range/cooldown/
    // energy - repeatable, so you can watch the same telegraph/hit-window/VFX over and over.
    //
    // 續 141 (user feedback on the first version): (1) charges need real distance to travel to be
    // observable, so a fixed boss + wherever-the-player-happens-to-stand isn't enough - added a
    // movable 稻草人 (straw-man) target dummy that attacks aim at instead of the real player, freely
    // repositionable (arrow keys), plus Shift+arrow to reposition the boss itself so any charge
    // distance can be set up on demand. (2) attacks aiming at the live player was hard to observe for
    // the same reason - the dummy fixes this too, it just sits still where you put it. (3) pausing (P)
    // used to freeze the whole game via Time.timeScale=0, which also froze ThirdPersonCameraController
    // (its rotation smoothing reads Time.deltaTime = 0 at timescale 0) - added a self-contained
    // free-look/fly camera (mouse look + WASD) that takes over ONLY while paused, so multi-angle
    // inspection of a frozen frame is possible without touching the shared camera controller at all.
    //
    // 續 144 (user: "稻草人只能水平移動不能上下移動") - arrow keys only ever drove XZ; added
    // PageUp/PageDown for Y (Shift+either moves the boss's height the same way), tracked as a
    // persistent lift offset so the ground-follow snap after a horizontal move doesn't erase it.
    //
    // Setup: menu "Tools/Live2DAction/[Debug] Setup Yuanpei Attack Debug Mode" (Map_School.unity).
    [DefaultExecutionOrder(200)]
    public class YuanpeiAttackDebugMode : MonoBehaviour
    {
        // 續 155 (user: "進入C模式後按技能都沒反應") - real cause: several of this tool's default keys
        // silently double-booked onto ALWAYS-ACTIVE gameplay bindings that read raw Keyboard.current
        // independently of this tool (PlayerInputProvider / CameraPossessionSwitcher / ViewFocusDirector
        // - none of them "consume" input, so both sides fire on the same keypress). C is the game's own
        // possess-the-cat toggle (CameraPossessionSwitcher.toggleKey) - pressing C for Close-up mode
        // ALSO swapped Camera.main + control over to the Cat's own camera/controller, which this tool
        // never touched, so its isolated/slow-motion setup kept being applied to a camera nobody was
        // looking through anymore: digit-key fires were actually working, just invisible. Audited every
        // key this tool binds against every raw `keyboard.<x>Key`/`Key.<X>` check in `_Project` and
        // moved every collision found (T = 守望者 spectator view/ViewFocusDirector, V = first-person
        // toggle/PlayerInputProvider.ViewTogglePressed, R = player Ultimate/PlayerInputProvider.
        // UltimatePressed) to keys with zero hits anywhere in the project.
        [SerializeField] private Key toggleKey = Key.F8;
        [SerializeField] private Key pauseKey = Key.P;
        [SerializeField] private Key replayKey = Key.Y;   // was R - collided with the player's Ultimate key
        [SerializeField] private Key slowerKey = Key.Minus;
        [SerializeField] private Key fasterKey = Key.Equals;
        [Tooltip("Toggles the arena-boundary + selected attack's min/max-range rings.")]
        [SerializeField] private Key rangeRingKey = Key.G;
        [Tooltip("Snaps the target dummy to the real player's current position.")]
        [SerializeField] private Key snapDummyToPlayerKey = Key.J;   // was T - collided with 守望者 view toggle
        [Tooltip("ALWAYS puts the target dummy at a known-good ground spot next to the boss, no " +
                 "physics raycast involved - a guaranteed escape hatch if it ever ends up stuck " +
                 "somewhere odd (e.g. the boss's un-triggered idle sky-logo height).")]
        [SerializeField] private Key resetDummyKey = Key.Home;
        [Tooltip("Toggles the camera to the 稻草人 target dummy's own point of view (mouse-look only, " +
                 "no fly) so hit-timing/telegraph reads can be checked from the receiving end.")]
        [SerializeField] private Key dummyViewKey = Key.L;   // was V - collided with the player's first-person toggle
        [Tooltip("Toggles VFX Inspect mode: whatever the next fired attack spawns (projectile mesh, " +
                 "particle burst, ground decal - 長矛型光彈/六連彈/雷擊標記 etc.) fills the whole screen " +
                 "alone on a solid background, nothing else in the scene rendered.")]
        [SerializeField] private Key inspectKey = Key.I;
        [Tooltip("Hides this tool's OnGUI text panel (useful for a clean screenshot/recording, " +
                 "especially in VFX Inspect / Close-up mode) - a small reminder stays on screen.")]
        [SerializeField] private Key hidePanelKey = Key.H;
        [Tooltip("Toggles VFX Close-up mode: only the FIRST GameObject the next fired attack spawns " +
                 "is tracked (multi-projectile attacks like 六連彈/長矛連發 show just one instance, not " +
                 "all of them at once), camera hugs it tight to fill almost the whole screen, and " +
                 "playback runs in slow motion - for reading a single VFX's own animation/shape clearly.")]
        [SerializeField] private Key closeupKey = Key.U;   // was C - collided with the possess-the-cat toggle
        // 續 171/173 (user: "能不能在F8模式 U模式 同時射出有無特效版本的比較" + "感覺k鍵沒有正確觸發") -
        // only meaningful for SpearVolley (the only attack with a video-baked flipbook overlay so far,
        // 續169/170); toggled independently of Close-up itself so it stays on across repeated fires
        // until turned off. Was Key.K in 續171 - collided with ViewFocusDirector.commitViewKey (also
        // K); even though SetWorldInputLocked disables that director for the F8 session, moved to N
        // ("no VFX") to keep this tool's bindings collision-free on principle (same rule as 續155).
        [Tooltip("While in VFX Close-up mode and firing 長矛型光彈(9), also spawns a second copy with " +
                 "the video-baked flipbook overlay force-disabled, stacked ABOVE the real one, for a " +
                 "direct side-by-side comparison. No effect on any other attack.")]
        [SerializeField] private Key compareKey = Key.N;
        // 續 161 (user: "起始等待時間長") - 0.2 (5x slowdown) applied to EVERYTHING including the
        // telegraph/windup, which compounded with those already being ~0.35-1s of real attack timing
        // into several real seconds of nothing visible before the flight even starts. The flight
        // itself is independently slowed via `closeupProjectileSpeed`, so the global timescale no
        // longer needs to carry that job alone - eased up so the wait is shorter without losing the
        // slow-motion feel during the part that actually matters.
        [SerializeField] private float closeupTimeScale = 0.5f;   // was 0.2
        // 續 161 (user: "起始位置...感覺在畫面邊界上") - 1.15 left the staged boss/dummy only ~13% short
        // of the frame edge (viewport ~0.07/0.93 measured live) - visibly clipped/uncomfortably tight.
        [SerializeField] private float closeupFrameMargin = 1.6f;   // was 1.15

        // 續 156 (user: "還是沒有反應 並且進入到這種f7 f8開發者模式,照理說要停用原本世界的按鍵邏輯 提供
        // 一個全新的鍵盤邏輯控制才對") - renaming this tool's own keys one collision at a time (續155)
        // only closes the specific holes found so far; the user's ask is structural: while this tool
        // owns the camera, nothing else in the "real world" should be able to reach in and take it
        // back. `CameraPossessionSwitcher` (C, swaps Camera.main + control to the Cat) and
        // `ViewFocusDirector` (T, 守望者 spectator view - "takes over whichever camera is live", per
        // its own header comment) are the two systems that actually hijack the camera on a keypress -
        // disabled for the WHOLE F8 session (not just the sub-modes that need the camera), restored on
        // Exit. Deliberately NOT touching `PlayerInputProvider`/`CharacterMovement` - those hold
        // continuous state (MoveInput, GuardPressed) that would freeze stale (e.g. stuck mid-guard or
        // mid-stride) if disabled mid-frame rather than cleanly zeroing next Update, which is a worse
        // bug than the one being fixed; movement/combat were never the reported symptom, only the
        // camera-hijacking pair actually was.
        private Behaviour _lockedPossessionSwitcher;
        private Behaviour _lockedViewFocusDirector;
        private bool _lockedPossessionSwitcherWas, _lockedViewFocusDirectorWas;

        private void SetWorldInputLocked(bool locked)
        {
            if (locked)
            {
                _lockedPossessionSwitcher = UnityEngine.Object.FindFirstObjectByType(
                    typeof(Live2DAction.CameraSystem.CameraPossessionSwitcher)) as Behaviour;
                _lockedViewFocusDirector = UnityEngine.Object.FindFirstObjectByType(
                    typeof(Live2DAction.CameraSystem.ViewFocusDirector)) as Behaviour;
                if (_lockedPossessionSwitcher != null)
                {
                    _lockedPossessionSwitcherWas = _lockedPossessionSwitcher.enabled;
                    _lockedPossessionSwitcher.enabled = false;
                }
                if (_lockedViewFocusDirector != null)
                {
                    _lockedViewFocusDirectorWas = _lockedViewFocusDirector.enabled;
                    _lockedViewFocusDirector.enabled = false;
                }
            }
            else
            {
                if (_lockedPossessionSwitcher != null) _lockedPossessionSwitcher.enabled = _lockedPossessionSwitcherWas;
                if (_lockedViewFocusDirector != null) _lockedViewFocusDirector.enabled = _lockedViewFocusDirectorWas;
                _lockedPossessionSwitcher = null;
                _lockedViewFocusDirector = null;
            }
        }

        [Header("Reposition (arrows = dummy XZ, PageUp/Down = dummy Y, Shift+either = boss)")]
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private LayerMask groundMask = ~0;
        [Tooltip("Vertical lift for the dummy (PageUp/PageDown), Shift+these move the boss's height instead.")]
        [SerializeField] private Key verticalUpKey = Key.PageUp;
        [SerializeField] private Key verticalDownKey = Key.PageDown;

        [Header("Free-look/fly camera while paused")]
        [SerializeField] private float freeLookSensitivity = 0.12f;
        [SerializeField] private float freeFlySpeed = 8f;

        [SerializeField] private YuanpeiBoss boss;
        [SerializeField] private YuanpeiAttacks attacks;
        [SerializeField] private YuanpeiBossHUD hud;

        [Tooltip("If the target dummy ends up this far below the arena floor after a debug-fired " +
                 "attack (ChargeCrush's void-punt bypasses the normal Defeat()/teleport-back flow " +
                 "since this tool never starts a real YuanpeiEncounter), snap it back next to the boss.")]
        [SerializeField] private float fallRecoverDepth = 8f;

        public bool Active { get; private set; }

        private Transform _player;
        private Live2DAction.Core.Health _playerHealth;
        private GameObject _dummy;
        private bool _hudWasVisible;
        private float _animSpeed = 1f;
        private bool _paused;
        private bool _showRings = true;
        private int _selected = -1;      // index into boss.AttackPool of the last fired/highlighted attack
        private Coroutine _running;
        private Vector3 _restPos;
        private bool _hasRestPos;
        // manual lift above whatever floor SnapToGround finds for the dummy's current XZ - keeps
        // vertical adjustment independent of the ground-follow snap, since the dummy is otherwise
        // always glued back to the raycast floor after every horizontal move (續 144, user: "稻草人
        // 只能水平移動不能上下移動").
        private float _dummyHeightOffset;

        private Behaviour _camController;
        private bool _camControllerWasEnabled;
        private float _freeYaw, _freePitch;
        private bool _dummyView;   // 續 151 (user: "我需要一個按鍵進入稻草人視角")

        // 續 152 (user: "長矛型光彈、六連彈、雷擊標記...全螢幕單一物件動畫展示的視覺檢查比較清楚") - VFX
        // Inspect mode: isolates whatever GameObjects the currently-playing attack spawns (read via
        // YuanpeiAttacks.SpawnedCount/GetSpawnedAt, a plain index-watermark scan - no attack-specific
        // knowledge needed) onto their own dedicated layer with everything else culled out.
        // 續 153 (user: "改為從畫面最左邊發射 往右邊飛行 主要是為了觀察物件外觀,與boss本身和稻草人位置
        // 無關") - the camera no longer tracks/follows the VFX every frame (that always re-centred it,
        // so nothing ever visibly travelled left-to-right on screen). Instead: a FIXED camera + a
        // temporary "showcase stage" that puts the boss on screen-left and the dummy on screen-right
        // along a world axis the camera looks straight across - any attack's own real player-relative
        // aim direction (boss -> dummy) then naturally reads as a left-to-right flight, with no per-
        // attack-specific staging logic needed. Boss/dummy are restored to wherever they really were
        // the moment Inspect mode turns off - this is a transient presentation, not a real reposition.
        private bool _inspectMode;
        private int _inspectLayer = -1;
        private int _inspectWatermark;
        private readonly List<GameObject> _inspectTargets = new List<GameObject>();
        private CameraClearFlags _inspectCamClearFlagsWas;
        private Color _inspectCamBgWas;
        private int _inspectCamCullingMaskWas;
        private float _inspectCamFovWas;
        [Header("VFX Inspect showcase stage (fixed camera, boss=left / dummy=right)")]
        [SerializeField] private float showcaseHalfSeparation = 8f;
        [SerializeField] private float showcaseFrameMargin = 1.3f;
        private Vector3 _preShowcaseBossPos;
        private Quaternion _preShowcaseBossRot;
        private Vector3 _preShowcaseDummyPos;
        private bool _hasShowcaseSave;

        // 續 154 (user: "另一種情境:能夠看到每個攻擊物件的特效與移動動畫,因此必須是單個物件占用畫面非常
        // 大 且慢速撥放 一次就一個物件(子彈)才能看得清楚") - a second, separate isolated-camera mode.
        // Unlike VFX Inspect (which stages the whole boss->dummy flight left-to-right at normal speed
        // to see the TRAVEL), Close-up fires with `count` overridden to 1 (BuildCloseupFireDef) so
        // there's only ever one projectile/mark, but tracks EVERY GameObject that single fire spawns
        // (續 164, user: "除了模型之外也請附上該攻擊具有的特效") - a shot's own muzzle-charge glow,
        // trail, and impact burst are each separate GameObjects added to `_spawned` over the attack's
        // whole run, not just the one at the very start; only the FIRST is resized to fill the frame
        // (`ScaleCloseupTargetToFit` - that's the actual bullet/mark), the rest are left at their
        // authored scale (forcing a particle system or light to some arbitrary size reads wrong) and
        // just isolated onto the same visible layer. Shares the same isolated layer/solid-background
        // mechanism as Inspect mode but is otherwise independent - boss/dummy positions are left
        // untouched by whichever objects get tracked here.
        private bool _closeupMode;
        private int _closeupWatermark;
        private readonly List<GameObject> _closeupTargets = new List<GameObject>();

        // 續 171 - the no-VFX comparison twin (SpearVolley only). Not tracked via `attacks.SpawnedCount`
        // (it's created directly by this tool, not by YuanpeiAttacks - `attacks.CancelAll()` never
        // touches it), so it needs its own manual cleanup at every point something else destroys the
        // real spawned objects: start of the next Fire(), and Close-up mode turning off.
        private bool _compareMode;
        private GameObject _compareClone;
        [SerializeField] private float compareStackGapFraction = 1.3f;

        // 續 154 (user: "提供一個按鍵能隱藏i模式下的面板提示文字") - hides the OnGUI text panel for a
        // clean screenshot/recording; a tiny one-line reminder stays so the key to bring it back isn't
        // forgotten.
        private bool _hidePanel;

        private readonly List<GameObject> _rings = new List<GameObject>();

        // 續 154 - four separate modes (paused free-look, 稻草人視角, VFX Inspect, VFX Close-up) all
        // drive Camera.main directly and disable ThirdPersonCameraController - only one can own it at
        // a time. Centralised here instead of each toggle site manually turning off "the other one or
        // two it happens to know about" (which is how 續151/152 grew increasingly tangled pairwise
        // checks - this scales to N modes without an O(N^2) mess of hand-written exclusions).
        private enum SpecialMode { None, Paused, DummyView, Inspect, Closeup }

        private void ExitSpecialModesExcept(SpecialMode keep)
        {
            if (keep != SpecialMode.Paused && _paused) { _paused = false; Time.timeScale = _animSpeed; SetFreeLook(false); }
            if (keep != SpecialMode.DummyView && _dummyView) { _dummyView = false; SetDummyView(false); }
            if (keep != SpecialMode.Inspect && _inspectMode) { _inspectMode = false; SetInspectMode(false); }
            if (keep != SpecialMode.Closeup && _closeupMode) { _closeupMode = false; SetCloseupMode(false); }
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            if (toggleKey != Key.None && kb[toggleKey].wasPressedThisFrame)
            {
                if (Active) Exit(); else Enter();
                return;
            }
            if (!Active) return;

            var pool = boss != null ? boss.AttackPool : null;
            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0 };
            if (pool != null)
            {
                for (int d = 0; d < digits.Length; d++)
                {
                    if (!kb[digits[d]].wasPressedThisFrame) continue;
                    if (d >= pool.Count) continue;
                    // 續 159 (user: "u模式的話就只提供那三個技能得按鍵") - Close-up only makes sense for
                    // the three attacks it actually knows how to stage/override (projectile-type moves);
                    // every other digit is a no-op while it's active, instead of firing an attack that
                    // never gets the single-shot/slow/enlarge treatment.
                    if (_closeupMode && !IsCloseupEligible(pool[d])) continue;
                    Fire(d);
                }
            }
            if (replayKey != Key.None && kb[replayKey].wasPressedThisFrame && _selected >= 0
                && (!_closeupMode || (_selected < pool.Count && IsCloseupEligible(pool[_selected]))))
            {
                Fire(_selected);
            }
            if (pauseKey != Key.None && kb[pauseKey].wasPressedThisFrame)
            {
                ExitSpecialModesExcept(SpecialMode.Paused);
                _paused = !_paused;
                Time.timeScale = _paused ? 0f : _animSpeed;
                SetFreeLook(_paused);
            }
            // 續 168 (user: "進入 u或i模式後 有時會出不來") - real cause: these three toggles used to
            // require `!_paused`. If the user pressed P at any point while inside I/U/L (even by
            // habit/muscle memory from another debug tool), `_paused` goes true and every subsequent
            // press of the SAME key that got them there is silently swallowed by this guard - no log,
            // no feedback, reads exactly like "stuck, won't let me out" (the actual way out, P again,
            // isn't obvious once you're not sure why nothing responds). `ExitSpecialModesExcept`
            // already correctly un-pauses as part of switching to another mode - the only thing
            // stopping that from happening was this redundant extra guard, removed below so I/U/L
            // always work regardless of pause state.
            if (dummyViewKey != Key.None && kb[dummyViewKey].wasPressedThisFrame)
            {
                ExitSpecialModesExcept(SpecialMode.DummyView);
                _dummyView = !_dummyView;
                SetDummyView(_dummyView);
            }
            if (inspectKey != Key.None && kb[inspectKey].wasPressedThisFrame)
            {
                ExitSpecialModesExcept(SpecialMode.Inspect);
                _inspectMode = !_inspectMode;
                SetInspectMode(_inspectMode);
            }
            if (closeupKey != Key.None && kb[closeupKey].wasPressedThisFrame)
            {
                ExitSpecialModesExcept(SpecialMode.Closeup);
                _closeupMode = !_closeupMode;
                SetCloseupMode(_closeupMode);
            }
            if (hidePanelKey != Key.None && kb[hidePanelKey].wasPressedThisFrame)
            {
                _hidePanel = !_hidePanel;
            }
            // 續 171 (user: "能不能在F8模式 U模式 同時射出有無特效版本的比較") - a plain flag, not a
            // camera-owning mode, so it doesn't go through ExitSpecialModesExcept; just changes what
            // the NEXT SpearVolley fire in Close-up does. Toggling it off destroys any comparison twin
            // still on screen right away rather than waiting for the next fire.
            if (compareKey != Key.None && kb[compareKey].wasPressedThisFrame)
            {
                _compareMode = !_compareMode;
                if (!_compareMode && _compareClone != null) { Destroy(_compareClone); _compareClone = null; }
                Debug.Log("[YuanpeiAttackDebug] VFX Compare (無特效對照) " + (_compareMode ? "ON" : "OFF") +
                          " - only affects 長矛型光彈(9) while in Close-up.");
            }
            // 續 160 (user: "提供speed調整手段") - while Close-up is active, -/= tune the bullet's own
            // flight speed instead of the global playback speed (that's already fixed by
            // closeupTimeScale and isn't what needed adjusting) - same two keys, contextual meaning.
            if (slowerKey != Key.None && kb[slowerKey].wasPressedThisFrame)
            {
                if (_closeupMode)
                {
                    closeupProjectileSpeed = Mathf.Max(0.05f, closeupProjectileSpeed - 0.15f);
                    Debug.Log("[YuanpeiAttackDebug] Close-up bullet speed = " + closeupProjectileSpeed.ToString("0.00"));
                }
                else
                {
                    _animSpeed = Mathf.Max(0.05f, _animSpeed - 0.15f);
                    _paused = false;
                    Time.timeScale = _animSpeed;
                    SetFreeLook(false);
                }
            }
            if (fasterKey != Key.None && kb[fasterKey].wasPressedThisFrame)
            {
                if (_closeupMode)
                {
                    closeupProjectileSpeed = Mathf.Min(10f, closeupProjectileSpeed + 0.15f);
                    Debug.Log("[YuanpeiAttackDebug] Close-up bullet speed = " + closeupProjectileSpeed.ToString("0.00"));
                }
                else
                {
                    _animSpeed = Mathf.Min(2f, _animSpeed + 0.15f);
                    _paused = false;
                    Time.timeScale = _animSpeed;
                    SetFreeLook(false);
                }
            }
            if (rangeRingKey != Key.None && kb[rangeRingKey].wasPressedThisFrame)
            {
                _showRings = !_showRings;
                RefreshRings();
            }
            if (snapDummyToPlayerKey != Key.None && kb[snapDummyToPlayerKey].wasPressedThisFrame && _dummy != null && _player != null)
            {
                _dummyHeightOffset = 0f;
                _dummy.transform.position = _player.position;
                SnapToGround(_dummy.transform);
                SaveDummyPrefs();
            }
            if (resetDummyKey != Key.None && kb[resetDummyKey].wasPressedThisFrame)
            {
                ResetDummyPosition();
            }

            HandleReposition(kb);
            if (_paused) DriveFreeLook();
            else if (_dummyView) DriveDummyView();
            else if (_inspectMode) DriveInspectView();
            else if (_closeupMode) DriveCloseupView();
            else RestorePlayerCameraControl();

            // hold position between fires - YuanpeiBoss.Update() is disabled so nothing else does this
            if (_running == null && boss != null && _hasRestPos)
                boss.transform.position = _restPos;

            RefreshRings();
        }

        // 續 148 (user: "有時使用boss動作時 玩家視角會改變且無法再控制") - real cause: `ChargeCrush`'s
        // `CrushEjectCam` disables `ThirdPersonCameraController` for its wide eject shot and normally
        // counts on `YuanpeiEncounter.Defeat()` to re-enable it once the real death screen/teleport
        // flow finishes - this debug tool never starts a real `YuanpeiEncounter`, so that re-enable
        // never runs and the player is left with a dead camera. Rather than patch every attack that
        // could disable the controller (more could be added later), self-heal every frame instead:
        // whenever this tool isn't itself holding the camera for the paused free-look, force the
        // normal controller back on if anything left it off.
        private void RestorePlayerCameraControl()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var ctrl = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
            if (ctrl != null && !ctrl.enabled) ctrl.enabled = true;
        }

        // 續 141 point 1+2, 續 142 fixes - arrow keys reposition the target dummy, Shift+arrow keys
        // reposition the boss itself, both in real (unscaled) time so this still works while paused
        // (P). Camera-relative (up = away from camera, matching normal movement-key intuition) so
        // "which world axis is +Z right now" isn't a guessing game. Lets you set up any charge-attack
        // travel distance on demand instead of being stuck wherever the boss/player happened to be
        // standing when F8 was pressed.
        private void HandleReposition(Keyboard kb)
        {
            bool shift = kb[Key.LeftShift].isPressed || kb[Key.RightShift].isPressed;

            // vertical (續 144, user: "稻草人只能水平移動不能上下移動") - world-space up/down, unambiguous
            // regardless of camera angle, so it gets its own keys instead of overloading the
            // camera-relative arrow-key axes.
            float vertical = 0f;
            if (verticalUpKey != Key.None && kb[verticalUpKey].isPressed) vertical += 1f;
            if (verticalDownKey != Key.None && kb[verticalDownKey].isPressed) vertical -= 1f;
            if (vertical != 0f)
            {
                float dy = vertical * moveSpeed * Time.unscaledDeltaTime;
                if (shift)
                {
                    if (boss != null)
                    {
                        var p = boss.transform.position;
                        p.y += dy;
                        boss.transform.position = p;
                        _restPos = boss.transform.position;
                        _hasRestPos = true;
                    }
                }
                else if (_dummy != null)
                {
                    // this offset is what keeps the lift from being erased the next time a
                    // horizontal move re-snaps the dummy to the raycast floor.
                    _dummyHeightOffset += dy;
                    var p = _dummy.transform.position;
                    p.y += dy;
                    _dummy.transform.position = p;
                    SaveDummyPrefs();
                }
            }

            Vector3 input = Vector3.zero;
            if (kb[Key.UpArrow].isPressed) input += Vector3.forward;
            if (kb[Key.DownArrow].isPressed) input += Vector3.back;
            if (kb[Key.LeftArrow].isPressed) input += Vector3.left;
            if (kb[Key.RightArrow].isPressed) input += Vector3.right;
            if (input.sqrMagnitude < 0.0001f) return;

            var cam = Camera.main;
            Vector3 camFwd = cam != null ? cam.transform.forward : Vector3.forward;
            Vector3 camRight = cam != null ? cam.transform.right : Vector3.right;
            camFwd.y = 0f; camRight.y = 0f;
            if (camFwd.sqrMagnitude < 0.0001f) camFwd = Vector3.forward; else camFwd.Normalize();
            if (camRight.sqrMagnitude < 0.0001f) camRight = Vector3.right; else camRight.Normalize();
            Vector3 move = (camFwd * input.z + camRight * input.x).normalized * moveSpeed * Time.unscaledDeltaTime;

            if (shift)
            {
                if (boss == null) return;
                boss.transform.position += move;
                _restPos = boss.transform.position;
                _hasRestPos = true;
            }
            else
            {
                if (_dummy == null) return;
                _dummy.transform.position += move;
                SnapToGround(_dummy.transform, _dummyHeightOffset);
                SaveDummyPrefs();
            }
        }

        // 續 142 (user: "必須讓稻草人可以移動位置") - real cause: this raycast starts FROM the dummy's
        // own position and shoots straight down through the dummy's own CapsuleCollider on the way to
        // the real floor - Physics.Raycast returns the FIRST hit, so it kept snapping the dummy's Y
        // back to its own body height (~1.8m) every single time it moved, making horizontal movement
        // look broken/stuck. RaycastAll + skip anything rooted at the object being snapped.
        //
        // 續 143 (user: "稻草人在非常高的高空") - a raycast can still legitimately find nothing (starts
        // too far from any collider, wrong layer, etc). Rather than silently leaving the dummy at
        // whatever height it started at, fall back to the arena's own known floor Y (`arenaCenter.y`,
        // authored by the level, always sane) instead of trusting the raycast unconditionally.
        // `heightOffset` (續 144) - the manual PageUp/PageDown lift the dummy has accumulated, added
        // on top of whatever floor height the raycast finds, so re-snapping after a horizontal move
        // doesn't erase a deliberate vertical adjustment. Callers that don't want a lift (boss,
        // or a hard reset) just omit it.
        private void SnapToGround(Transform t, float heightOffset = 0f)
        {
            var hits = Physics.RaycastAll(t.position + Vector3.up * 60f, Vector3.down, 300f, groundMask, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].collider == null || hits[i].collider.transform.root == t) continue;
                if (boss != null && hits[i].collider.transform.root == boss.transform.root) continue;
                // 續 146 (user: "稻草人還在天空... 我在boss旁邊") - real cause: a REAL boss encounter had
                // been triggered earlier (walking into BossRoomTrigger), leaving `YuanpeiEncounter`'s
                // arena-lockdown ceiling/floor panels (續 134/135, solid non-trigger BoxColliders,
                // ArenaWall_Ceiling sits at Y≈45-46) still standing when F8 was pressed - this tool
                // never goes through YuanpeiEncounter so it has no way to know that lockdown exists.
                // The raycast hit the CEILING panel first and happily "snapped" the dummy to it,
                // reading as "stuck in the sky" with no obvious cause. Skip anything owned by
                // YuanpeiEncounter the same way self/boss are already skipped.
                if (hits[i].collider.GetComponentInParent<Live2DAction.AI.Boss.Yuanpei.YuanpeiEncounter>() != null) continue;
                var p = t.position;
                p.y = hits[i].point.y + heightOffset;
                t.position = p;
                return;
            }
            // raycast found nothing usable - fall back to the arena's authored floor height
            if (boss != null && boss.Config != null)
            {
                var p = t.position;
                p.y = boss.Config.arenaCenter.y + 0.5f + heightOffset;
                t.position = p;
            }
        }

        // Guaranteed, physics-independent way to put the dummy somewhere sane - no raycast involved
        // at all, so it can never fail the way SnapToGround's raycast theoretically could. Bound to
        // Home (續 143, user: "你需要提供一種方法移動稻草人 並且現在稻草人在非常高的高空").
        private void ResetDummyPosition()
        {
            if (_dummy == null || boss == null) return;
            _dummyHeightOffset = 0f;
            // 續 145 (user: "讓他在元培廣場就行 參照物件做逼近") - anchor to the actual named plaza
            // GameObject's XZ instead of the boss's arenaCenter ScriptableObject field. Both land on
            // the same real floor (學校 ground, Y=0.5) so this doesn't change where the dummy ends
            // up in practice, but it's a landmark the user can see and name, not an SO value that
            // could silently drift from the level's real geometry.
            var plaza = GameObject.Find("yuanpei_QuietCampusPlaza");
            Vector3 center = plaza != null
                ? plaza.transform.position
                : (boss.Config != null ? boss.Config.arenaCenter : boss.transform.position);
            _dummy.transform.position = new Vector3(center.x, center.y, center.z) + Vector3.forward * 5f;
            SnapToGround(_dummy.transform);
            SaveDummyPrefs();
            Debug.Log("[YuanpeiAttackDebug] target dummy reset to ground at 元培廣場" + (plaza == null ? " (fallback: arena center, plaza object not found)" : "") + ".");
        }

        // 續 149 (user: "請記住上次稻草人設定位置") - PlayerPrefs so the dummy comes back wherever it was
        // last left, across separate Play sessions too (not just F8 toggles within one session). Only
        // written when the dummy actually moves (every reposition path above), read once on a FRESH
        // dummy build in Enter() - Home/T still give an explicit, always-available way back to a known
        // ground spot if a saved position ever turns out to be bad for some reason.
        private const string PrefKeyX = "YuanpeiDebugDummy.X";
        private const string PrefKeyY = "YuanpeiDebugDummy.Y";
        private const string PrefKeyZ = "YuanpeiDebugDummy.Z";
        private const string PrefKeyOffset = "YuanpeiDebugDummy.HeightOffset";

        private void SaveDummyPrefs()
        {
            if (_dummy == null) return;
            Vector3 p = _dummy.transform.position;
            PlayerPrefs.SetFloat(PrefKeyX, p.x);
            PlayerPrefs.SetFloat(PrefKeyY, p.y);
            PlayerPrefs.SetFloat(PrefKeyZ, p.z);
            PlayerPrefs.SetFloat(PrefKeyOffset, _dummyHeightOffset);
        }

        private bool TryLoadDummyPrefs()
        {
            if (_dummy == null || !PlayerPrefs.HasKey(PrefKeyX)) return false;
            _dummy.transform.position = new Vector3(
                PlayerPrefs.GetFloat(PrefKeyX), PlayerPrefs.GetFloat(PrefKeyY), PlayerPrefs.GetFloat(PrefKeyZ));
            _dummyHeightOffset = PlayerPrefs.GetFloat(PrefKeyOffset, 0f);
            Debug.Log("[YuanpeiAttackDebug] target dummy restored to last-remembered position " + _dummy.transform.position + ".");
            return true;
        }

        // 續 141 point 3 - a fully self-contained free-look/fly camera used ONLY while paused, so a
        // frozen frame can be inspected from any angle. Deliberately does not touch
        // ThirdPersonCameraController's own code (its rotation smoothing reads Time.deltaTime, which
        // is 0 at Time.timeScale=0 - that's why the normal camera froze); this just disables it and
        // drives Camera.main directly with unscaled input while paused, then hands control back.
        private void SetFreeLook(bool on)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (on)
            {
                _camController = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
                if (_camController != null)
                {
                    _camControllerWasEnabled = _camController.enabled;
                    _camController.enabled = false;
                }
                Vector3 e = cam.transform.rotation.eulerAngles;
                _freeYaw = e.y;
                _freePitch = e.x > 180f ? e.x - 360f : e.x;
            }
            else
            {
                if (_camController != null) _camController.enabled = _camControllerWasEnabled;
                _camController = null;
            }
        }

        private void DriveFreeLook()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                _freeYaw += d.x * freeLookSensitivity;
                _freePitch = Mathf.Clamp(_freePitch - d.y * freeLookSensitivity, -85f, 85f);
                cam.transform.rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
            }
            var kb = Keyboard.current;
            if (kb == null) return;
            Vector3 mv = Vector3.zero;
            if (kb[Key.W].isPressed) mv += cam.transform.forward;
            if (kb[Key.S].isPressed) mv -= cam.transform.forward;
            if (kb[Key.A].isPressed) mv -= cam.transform.right;
            if (kb[Key.D].isPressed) mv += cam.transform.right;
            if (kb[Key.Space].isPressed) mv += Vector3.up;
            if (kb[Key.LeftCtrl].isPressed) mv -= Vector3.up;
            if (mv.sqrMagnitude > 0.0001f)
                cam.transform.position += mv.normalized * freeFlySpeed * Time.unscaledDeltaTime;
        }

        // 續 151 (user: "我需要一個按鍵進入稻草人視角") - same camera hand-off pattern as SetFreeLook
        // (disable ThirdPersonCameraController, drive Camera.main directly), but anchored at the
        // dummy's eye position instead of flying free - the point is to see an attack from the
        // receiving end (hit-timing/telegraph reads), not to fly around the level.
        private void SetDummyView(bool on)
        {
            var cam = Camera.main;
            if (cam == null) return;
            if (on)
            {
                _camController = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
                if (_camController != null)
                {
                    _camControllerWasEnabled = _camController.enabled;
                    _camController.enabled = false;
                }
                if (_dummy != null)
                {
                    Vector3 eye = _dummy.transform.position + Vector3.up * 1.6f;
                    Vector3 lookAt = boss != null ? boss.transform.position : eye + Vector3.forward;
                    Quaternion rot = Quaternion.LookRotation((lookAt - eye).normalized, Vector3.up);
                    cam.transform.SetPositionAndRotation(eye, rot);
                    Vector3 e = rot.eulerAngles;
                    _freeYaw = e.y;
                    _freePitch = e.x > 180f ? e.x - 360f : e.x;
                }
                Debug.Log("[YuanpeiAttackDebug] 稻草人視角 ON (" + dummyViewKey + " to exit) - mouse to look around.");
            }
            else
            {
                if (_camController != null) _camController.enabled = _camControllerWasEnabled;
                _camController = null;
            }
        }

        private void DriveDummyView()
        {
            var cam = Camera.main;
            if (cam == null || _dummy == null) return;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 d = mouse.delta.ReadValue();
                _freeYaw += d.x * freeLookSensitivity;
                _freePitch = Mathf.Clamp(_freePitch - d.y * freeLookSensitivity, -85f, 85f);
            }
            // position stays pinned to the dummy's eye every frame (in case it gets repositioned
            // while this view is active) - only the look direction is free.
            cam.transform.position = _dummy.transform.position + Vector3.up * 1.6f;
            cam.transform.rotation = Quaternion.Euler(_freePitch, _freeYaw, 0f);
        }

        // 續 152/153 (user: "長矛型光彈、六連彈、雷擊標記...全螢幕單一物件動畫展示的視覺檢查比較清楚" then
        // "改為從畫面最左邊發射 往右邊飛行 主要是為了觀察物件外觀,與boss本身和稻草人位置無關") - VFX
        // Inspect mode. On: hand the camera off like SetDummyView/SetFreeLook do, restrict what it can
        // even SEE to one dedicated layer (solid-colour background, no scene geometry at all), and
        // stage boss=screen-left / dummy=screen-right on a FIXED camera (see EnterShowcaseStage) so
        // any attack's own real aim direction (boss -> dummy) reads as a left-to-right flight without
        // this tool needing any attack-specific staging logic. Off: everything restored, including
        // the boss/dummy's real pre-showcase transforms.
        // 續 168 (user: "進入 u或i模式後 有時會出不來") - the "off" branch used to re-query `Camera.main`
        // fresh rather than reusing whichever camera was actually modified when "on" ran. If anything
        // else ever changes which camera is "main" while Inspect/Close-up is active (a spectator/
        // vehicle/possession swap this tool doesn't know about), the restore would land on the WRONG
        // (current) camera while the ORIGINAL one stays stuck showing the solid-colour isolated view
        // forever - toggling the mode off "worked" (the bool flipped) but nothing visibly changed back,
        // reading exactly like "won't let me out". `_isolatedCam` is cached at entry and reused at exit
        // so the SAME camera that got hijacked is always the one that gets un-hijacked.
        private Camera _isolatedCam;

        private void SetInspectMode(bool on)
        {
            var cam = on ? Camera.main : _isolatedCam;
            if (cam == null) return;
            if (_inspectLayer < 0) _inspectLayer = LayerMask.NameToLayer("YuanpeiVfxInspect");
            if (on)
            {
                _isolatedCam = cam;
                _camController = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
                if (_camController != null)
                {
                    _camControllerWasEnabled = _camController.enabled;
                    _camController.enabled = false;
                }
                _inspectCamClearFlagsWas = cam.clearFlags;
                _inspectCamBgWas = cam.backgroundColor;
                _inspectCamCullingMaskWas = cam.cullingMask;
                _inspectCamFovWas = cam.fieldOfView;
                if (_inspectLayer >= 0)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
                    cam.cullingMask = 1 << _inspectLayer;
                }
                _inspectTargets.Clear();
                _inspectHeroFound = false;
                _inspectWatermark = attacks != null ? attacks.SpawnedCount : 0;
                EnterShowcaseStage(cam, showcaseHalfSeparation, showcaseFrameMargin);
                if (hud != null) hud.SetVisible(false);   // 續 157 (user: "i模式和u模式下要隱藏boss血量條")
                Debug.Log("[YuanpeiAttackDebug] VFX Inspect ON (" + inspectKey + " to exit) - fire any attack (1-9/0): " +
                          "boss=left, 稻草人=right, fixed camera, its VFX flies left-to-right alone on screen." +
                          (_inspectLayer < 0 ? " WARNING: layer 'YuanpeiVfxInspect' not found - background won't be hidden." : ""));
            }
            else
            {
                cam.clearFlags = _inspectCamClearFlagsWas;
                cam.backgroundColor = _inspectCamBgWas;
                cam.cullingMask = _inspectCamCullingMaskWas;
                cam.fieldOfView = _inspectCamFovWas;
                if (_camController != null) _camController.enabled = _camControllerWasEnabled;
                _camController = null;
                _inspectTargets.Clear();
                _inspectHeroFound = false;
                ExitShowcaseStage();
                if (hud != null) hud.SetVisible(_hudWasVisible);
                _isolatedCam = null;
            }
        }

        // Saves the boss/dummy's real transforms (restored in ExitShowcaseStage) then places them on
        // a fixed world-X line either side of `arenaCenter` - boss at -X (screen-left), dummy at +X
        // (screen-right) - and points a completely static camera straight across that line so the
        // separation projects as left-edge-to-right-edge regardless of where either of them actually
        // is in the real level. Kept on real ground height (not some arbitrary sky point) so every
        // attack's own ProjectToGround/SampleFloorY raycasts still find a real floor exactly like
        // normal use - only the horizontal placement is staged.
        // 續 159 - the dummy's own CapsuleCollider centre sits this far above its base transform (see
        // BuildDummy's `cc.center = (0, 1.1, 0)`), which is what attacks actually aim at (`PlayerCenter`
        // reads the collider bounds, not the raw transform). Matching the boss's firing HEIGHT to this
        // exact value (instead of the usual authored hover height) makes a flat-trajectory shot level,
        // not diagonal - kept as one named constant instead of retyping 1.1 in two places.
        private const float DummyAimCenterOffsetY = 1.1f;

        private void EnterShowcaseStage(Camera cam, float half, float margin, bool flatTrajectory = false)
        {
            if (boss == null) return;
            _preShowcaseBossPos = boss.transform.position;
            _preShowcaseBossRot = boss.transform.rotation;
            _preShowcaseDummyPos = _dummy != null ? _dummy.transform.position : Vector3.zero;
            _hasShowcaseSave = true;

            RestageShowcase(cam, half, margin, flatTrajectory);
        }

        // Re-applies the fixed left/right showcase positions without touching the saved "real"
        // transforms - called once on entry and again before every fire while Inspect/Close-up mode
        // is active, so a previous attack that physically moved the boss (a charge dash, say) doesn't
        // leave the next demo starting from a random spot. `half`/`margin` are shared with Close-up
        // mode (續 157) - a much smaller `half` there makes the object fill far more of the screen
        // while still visibly crossing it left-to-right, same mechanism as Inspect just zoomed in.
        // `flatTrajectory` (續 159, user: "目前是從左上往右下發射,我要直接從畫面左邊直線往右邊飛行") -
        // Inspect wants the boss at its real authored hover height (a representative arc); Close-up
        // wants a perfectly level shot for a clean left-to-right read, so it puts the boss at the
        // dummy's own aim-centre height instead of hovering above it.
        private void RestageShowcase(Camera cam, float half, float margin, bool flatTrajectory = false)
        {
            if (boss == null) return;
            Vector3 anchor = boss.Config != null ? boss.Config.arenaCenter : boss.transform.position;
            float bossY, dummyY;
            if (flatTrajectory)
            {
                dummyY = anchor.y;
                bossY = anchor.y + DummyAimCenterOffsetY;
            }
            else
            {
                float hover = boss.Config != null ? boss.Config.hoverHeight : 3f;
                bossY = anchor.y + hover;
                dummyY = anchor.y;
            }

            Vector3 bossPos = new Vector3(anchor.x - half, bossY, anchor.z);
            Vector3 dummyPos = new Vector3(anchor.x + half, dummyY, anchor.z);

            boss.transform.SetPositionAndRotation(bossPos, Quaternion.LookRotation(Vector3.right, Vector3.up));
            _restPos = bossPos;
            _hasRestPos = true;
            if (_dummy != null) _dummy.transform.position = dummyPos;

            if (cam != null)
            {
                float midY = (bossY + dummyY) * 0.5f;
                // 續 163 (user: "不夠下面 繼續往下移動" - the 續162 pivot-recentre fix wasn't the whole
                // story, there's a genuine residual bias beyond it) - raising the camera above the
                // geometric midpoint, while still looking level, pushes everything in view further
                // DOWN the frame (anything at a fixed world height projects lower once the camera
                // itself is higher above it). Expressed as a fraction of `half` so it scales with
                // whatever the stage size happens to be, only applied for Close-up's flat stage
                // (Inspect was never reported as having this problem).
                float verticalBias = flatTrajectory ? half * closeupVerticalBiasFraction : 0f;
                Vector3 camAnchor = new Vector3(anchor.x, midY + verticalBias, anchor.z);
                float vFov = cam.fieldOfView * Mathf.Deg2Rad * 0.5f;
                float aspect = cam.aspect > 0.01f ? cam.aspect : 1.77f;
                // 續 158 (found while wiring Close-up's tighter framing) - the boss sits `hoverHeight`
                // above the dummy, a FIXED vertical gap regardless of how tight `half` is. At Inspect's
                // wide half(8) the horizontal need dwarfs it and this never showed up; at Close-up's
                // tight half(1.6) the vertical gap is now the LARGER of the two needs, so fitting only
                // the horizontal half-width (as this used to) left both ends pushed off the top/bottom
                // of frame entirely (viewport y well outside 0-1). Fit BOTH axes and take whichever
                // needs more distance.
                float halfWidthPerUnit = Mathf.Max(0.05f, Mathf.Tan(vFov) * aspect);
                float halfHeightPerUnit = Mathf.Max(0.05f, Mathf.Tan(vFov));
                float distForWidth = (half * margin) / halfWidthPerUnit;
                float distForHeight = (Mathf.Abs(bossY - dummyY) * 0.5f * margin) / halfHeightPerUnit;
                float dist = Mathf.Max(distForWidth, distForHeight);
                cam.transform.SetPositionAndRotation(camAnchor - Vector3.forward * dist,
                    Quaternion.LookRotation(Vector3.forward, Vector3.up));
            }
        }

        private void ExitShowcaseStage()
        {
            if (!_hasShowcaseSave) return;
            _hasShowcaseSave = false;
            if (boss != null)
            {
                boss.transform.SetPositionAndRotation(_preShowcaseBossPos, _preShowcaseBossRot);
                _restPos = _preShowcaseBossPos;
                _hasRestPos = true;
            }
            if (_dummy != null) _dummy.transform.position = _preShowcaseDummyPos;
        }

        // Camera is fixed now (續 153) - this only keeps pulling newly-spawned VFX GameObjects onto
        // the isolated layer every frame (an attack can spawn its VFX progressively over its whole
        // run - e.g. SpearVolley's 9 shots, ProjectileBurst's burst - not all at once at the start).
        // 續 165 (user: "接下來i模式的[9] 讓他本體放大三倍") - only the actual attack BODY gets the
        // extra multiplier, and only when the fire is SpearVolley - companion VFX (trail/glow) stay
        // untouched, same reasoning as Close-up's per-attack scaling.
        [SerializeField] private float inspectSpearVolleyExtraScale = 3f;
        private bool _inspectHeroFound;

        // 續 167 (user: "還是很小 有用及時座標烘培嗎") - real cause: "the first object this fire spawns
        // is the hero" (used since 續157/165) is WRONG for SpearVolley and ProjectileBurst - both spawn
        // their OWN pre-shot telegraph VFX first ("YuanpeiSpearMuzzleGlow" / "YuanpeiMuzzleCharge",
        // SpearVolley/ProjectileBurst's signature charge-up glow) and register it into `_spawned`
        // BEFORE the actual projectile. Every resize (Close-up's fit-to-frame, Inspect's SpearVolley
        // ×3) was silently being applied to that small telegraph glow instead - the real spear/orb
        // model was left at its tiny natural scale the entire time, which is exactly what read as
        // "still too small" no matter how large a multiplier got dialled in. Name-match the actual
        // attack body's GameObject name instead of just taking whichever spawned first.
        private static bool IsAttackHeroObject(GameObject go)
        {
            switch (go.name)
            {
                case "YuanpeiLightOrb":         // ProjectileBurst
                case "YuanpeiSpearProjectile":  // SpearVolley
                case "YuanpeiHazard":           // LightningMark (no separate telegraph object - spawns straight in)
                    return true;
                default:
                    return false;
            }
        }

        private void DriveInspectView()
        {
            if (attacks == null) return;
            int count = attacks.SpawnedCount;
            for (int i = _inspectWatermark; i < count; i++)
            {
                var go = attacks.GetSpawnedAt(i);
                if (go == null) continue;
                if (!_inspectHeroFound && IsAttackHeroObject(go))
                {
                    _inspectHeroFound = true;
                    var pool = boss != null ? boss.AttackPool : null;
                    if (pool != null && _selected >= 0 && _selected < pool.Count && pool[_selected] != null
                        && pool[_selected].attackId == YuanpeiAttackId.SpearVolley)
                    {
                        ScaleObjectPreserveCenter(go, inspectSpearVolleyExtraScale);
                    }
                }
                _inspectTargets.Add(go);
                if (_inspectLayer >= 0) SetLayerRecursive(go, _inspectLayer);
            }
            _inspectWatermark = count;
            _inspectTargets.RemoveAll(g => g == null);
        }

        // 續 154 (user: "另一種情境...必須是單個物件占用畫面非常大 且慢速撥放 一次就一個物件(子彈)才能
        // 看得清楚"). On: same camera hand-off + isolated-layer/solid-background trick as Inspect mode,
        // plus slow motion (Time.timeScale = closeupTimeScale) so a fast attack's own animation reads
        // frame by frame.
        // 續 157 (user: "此模式下 主要是看物件發射型攻擊...子彈從左往右飛行且非常緩慢...i模式和u模式下
        // 要隱藏boss血量條") - originally a dynamically-tracking camera (kept re-centring on the object,
        // which read as "stuck in the middle", not "flying across the screen"). Replaced with the SAME
        // fixed left/right showcase stage Inspect mode uses (`EnterShowcaseStage`), just zoomed in far
        // tighter (`closeupHalfSeparation` instead of `showcaseHalfSeparation`) so the bullet still
        // fills most of the screen while genuinely crossing it left-to-right. HUD hidden here too.
        private void SetCloseupMode(bool on)
        {
            var cam = on ? Camera.main : _isolatedCam;   // 續168 - see _isolatedCam's own comment
            if (cam == null) return;
            if (_inspectLayer < 0) _inspectLayer = LayerMask.NameToLayer("YuanpeiVfxInspect");
            if (on)
            {
                _isolatedCam = cam;
                _camController = cam.GetComponent(typeof(Live2DAction.CameraSystem.ThirdPersonCameraController)) as Behaviour;
                if (_camController != null)
                {
                    _camControllerWasEnabled = _camController.enabled;
                    _camController.enabled = false;
                }
                _inspectCamClearFlagsWas = cam.clearFlags;
                _inspectCamBgWas = cam.backgroundColor;
                _inspectCamCullingMaskWas = cam.cullingMask;
                _inspectCamFovWas = cam.fieldOfView;
                if (_inspectLayer >= 0)
                {
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.05f, 0.05f, 0.07f);
                    cam.cullingMask = 1 << _inspectLayer;
                }
                _closeupTargets.Clear();
                _closeupHeroFound = false;
                _closeupWatermark = attacks != null ? attacks.SpawnedCount : 0;
                _paused = false;
                _animSpeed = closeupTimeScale;
                Time.timeScale = closeupTimeScale;
                EnterShowcaseStage(cam, closeupHalfSeparation, closeupFrameMargin, flatTrajectory: true);
                if (hud != null) hud.SetVisible(false);
                Debug.Log("[YuanpeiAttackDebug] VFX Close-up ON (" + closeupKey + " to exit) - only 1(六連彈)/3(雷擊標記)/9(長矛型光彈) " +
                          "fire anything here, one slow oversized instance flying flat left-to-right; every other digit is a no-op. " +
                          "Slow motion x" + closeupTimeScale.ToString("0.00") + "." +
                          (_inspectLayer < 0 ? " WARNING: layer 'YuanpeiVfxInspect' not found - background won't be hidden." : ""));
            }
            else
            {
                cam.clearFlags = _inspectCamClearFlagsWas;
                cam.backgroundColor = _inspectCamBgWas;
                cam.cullingMask = _inspectCamCullingMaskWas;
                cam.fieldOfView = _inspectCamFovWas;
                if (_camController != null) _camController.enabled = _camControllerWasEnabled;
                _camController = null;
                _closeupTargets.Clear();
                _closeupHeroFound = false;
                _animSpeed = 1f;
                Time.timeScale = 1f;
                ExitShowcaseStage();
                if (hud != null) hud.SetVisible(_hudWasVisible);
                if (_closeupDefClone != null) { Destroy(_closeupDefClone); _closeupDefClone = null; }
                if (_compareClone != null) { Destroy(_compareClone); _compareClone = null; }
                _isolatedCam = null;
            }
        }

        // Camera is fixed (RestageShowcase) now, not tracking - this pulls in every GameObject this
        // fire spawns (續 164, user: "除了模型之外也請附上該攻擊具有的特效") - a shot's muzzle-charge
        // glow / trail / impact burst are each separate GameObjects added to `_spawned` progressively
        // over the attack's whole run (not just one at the start), so this keeps scanning every frame
        // rather than stopping after the first find. Only the very FIRST one found (the actual bullet/
        // mark) gets resized to fill the frame; every one after that is companion VFX and is left at
        // its authored scale (forcing a particle system or point light to some arbitrary size reads
        // wrong) - just isolated onto the same visible layer so it isn't culled out. No per-frame
        // camera movement needed since the stage is already fixed.
        private bool _closeupHeroFound;

        private void DriveCloseupView()
        {
            if (attacks == null) return;
            int count = attacks.SpawnedCount;
            for (int i = _closeupWatermark; i < count; i++)
            {
                var go = attacks.GetSpawnedAt(i);
                _closeupWatermark = i + 1;
                if (go == null) continue;
                if (_inspectLayer >= 0) SetLayerRecursive(go, _inspectLayer);
                // 續 167 - "first object = hero" used to pick SpearVolley/ProjectileBurst's own
                // pre-shot telegraph glow instead of the real projectile (see IsAttackHeroObject's
                // comment) - name-match the real attack body now.
                if (!_closeupHeroFound && IsAttackHeroObject(go))
                {
                    _closeupHeroFound = true;
                    // 續 162 (user: "長矛型光彈請整體放大2倍") - an extra multiplier ON TOP of the
                    // normal fit-to-frame sizing, only for SpearVolley - `_selected` already tracks
                    // which pool entry this fire came from (set in Fire() right before this coroutine
                    // started).
                    float extra = 1f;
                    var pool = boss != null ? boss.AttackPool : null;
                    bool isSpearVolley = pool != null && _selected >= 0 && _selected < pool.Count && pool[_selected] != null
                        && pool[_selected].attackId == YuanpeiAttackId.SpearVolley;
                    // 續 173 (user: "感覺k鍵沒有正確觸發") - with the SpearVolley extra boost active
                    // (2x on top of fit-to-frame) a single spear already ~fills the frame vertically;
                    // stacking a second copy above it then can't fit and the twin gets clipped clean
                    // off the top - reads as "K did nothing". While comparing, drop the boost so BOTH
                    // fit stacked.
                    bool comparing = isSpearVolley && _compareMode;
                    if (isSpearVolley && !comparing) extra = closeupSpearVolleyExtraScale;
                    ScaleCloseupTargetToFit(go, extra);

                    // 續 171/173 (user: "能不能在F8模式 U模式 同時射出有無特效版本的比較") - only for
                    // SpearVolley (the only attack with a video-baked flipbook overlay, 續169/170).
                    if (comparing) BuildCompareClone(go);
                }
                _closeupTargets.Add(go);
            }
            _closeupTargets.RemoveAll(g => g == null);
        }

        // 續 160 (user: "要考量到畫面大小 物件不能沒有限制的放大") - a flat multiplier made sense for a
        // small bullet but blew up badly on LightningMark's ground mark (real radius ~2.4m already,
        // ×2.5 = a ~12m-wide disc filling FAR more than the whole tightly-zoomed stage - "都沒聚焦在
        // 畫面上", the mark was too big to read as a shape at all, not too small). Resize to a TARGET
        // on-screen radius instead of a blind multiply - scales small objects up and big ones down,
        // both converging on roughly the same, frame-appropriate size. Target radius is a fraction of
        // `closeupHalfSeparation` itself (the actual stage width), so it stays sane if that's ever
        // retuned, and the scale factor is clamped so a degenerate near-zero bounds (a freshly spawned
        // particle with nothing visible yet) can't divide out into an absurd size.
        [SerializeField] private float closeupTargetRadiusFraction = 0.28f;
        [SerializeField] private float closeupMinScaleFactor = 0.15f;
        [SerializeField] private float closeupMaxScaleFactor = 8f;   // 續166: the margin fix above raised the target radius enough that SpearVolley's fit×extraScale could exceed the old 5x cap and get silently clamped short of its intended 2x boost
        [SerializeField] private float closeupSpearVolleyExtraScale = 2f;

        // `extraScale` - a per-attack multiplier applied on top of the normal fit-to-frame factor
        // (續162, SpearVolley specifically wants to read bigger than the frame-fit alone would give it).
        //
        // 續 166 (user: "感覺更小了") - real cause: `targetRadius` used to be `half * fraction` only,
        // missing `margin`. The camera distance (RestageShowcase) is solved so the frame's ACTUAL
        // visible half-width equals `half * margin`, not just `half` - `margin` is purely a buffer/
        // positioning knob (續161 bumped it 1.15→1.6 to stop the stage sitting right at the screen
        // edge) and was never meant to also dilute how big a resized object reads. Since the old
        // formula didn't include `margin`, raising it for positioning silently shrank every scaled
        // object's on-screen fraction too (0.28/1.15=24% of the frame at the old margin vs
        // 0.28/1.6=17.5% now - a real, measurable ~28% shrink, not a perception issue). Multiplying
        // by `margin` here makes `targetRadius / (half*margin)` reduce to exactly `fraction` again,
        // regardless of whatever `margin`/`half` happen to be tuned to - true screen-size invariance.
        private void ScaleCloseupTargetToFit(GameObject go, float extraScale = 1f)
        {
            var rends = GetSizeRenderers(go);
            if (rends.Count == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Count; i++) b.Encapsulate(rends[i].bounds);
            float currentRadius = Mathf.Max(0.02f, b.extents.magnitude);
            float targetRadius = Mathf.Max(0.05f, closeupHalfSeparation * closeupFrameMargin * closeupTargetRadiusFraction);
            float factor = Mathf.Clamp(targetRadius / currentRadius, closeupMinScaleFactor, closeupMaxScaleFactor) * extraScale;
            ScaleObjectPreserveCenter(go, factor);
        }

        // 續 171/173 (user: "能不能在F8模式 U模式 同時射出有無特效版本的比較" + "感覺k鍵沒有正確觸發") -
        // clones the already-scaled/positioned/isolated hero object (simplest way to guarantee an
        // identical size/orientation twin) and disables its flipbook child, stacked ABOVE the real
        // one. Above, not below: Close-up raises the camera (closeupVerticalBiasFraction) so the
        // staged line sits low in frame - there's headroom up top, almost none below (the old
        // "below" placement 續171 put the twin off the bottom edge = invisible = "K沒反應"). Gap is a
        // fraction of the twin's own on-screen HEIGHT (measured after the shared scale), clamped so
        // even a degenerate near-zero bounds can't collapse them onto each other. `hero` itself is
        // never moved - its spot is exactly where the camera-fit math wants it. The clone keeps its
        // own live `YuanpeiProjectile` (Instantiate copies runtime state, not just serialized fields)
        // and flies/self-destructs on its own - a visual twin, not a re-fired attack.
        private void BuildCompareClone(GameObject hero)
        {
            if (_compareClone != null) { Destroy(_compareClone); _compareClone = null; }

            var rends = GetSizeRenderers(hero);
            if (rends.Count == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Count; i++) b.Encapsulate(rends[i].bounds);
            float gap = Mathf.Clamp(b.size.y * compareStackGapFraction, 0.3f, 6f);

            var clone = Instantiate(hero, hero.transform.parent);
            clone.name = hero.name + "_NoVFX_Compare";
            clone.transform.position = hero.transform.position + Vector3.up * gap;

            Transform flip = clone.transform.Find("SpearFlipbookVFX");
            if (flip != null) flip.gameObject.SetActive(false);
            if (_inspectLayer >= 0) SetLayerRecursive(clone, _inspectLayer);
            _compareClone = clone;
            Debug.Log("[YuanpeiAttackDebug] compare twin spawned (上=無特效 / 下=有特效, gap " + gap.ToString("0.0") + ")");
        }

        // 續 167 (found while verifying the above fix) - a freshly-added TrailRenderer with no
        // recorded history yet reports a wildly wrong `Renderer.bounds` (measured on the REAL
        // CrimsonVoidSpear projectile right at spawn: TrailRenderer bounds size (1, 9, 132) vs the
        // actual model's MeshRenderer bounds (0.51, 0.46, 1.2) - two orders of magnitude off). Both
        // ProjectileBurst's orbs and SpearVolley's spear add a TrailRenderer at spawn, so this wasn't
        // a one-off: `currentRadius` was reading ~66 instead of ~0.69, driving `targetRadius/
        // currentRadius` down near the MIN clamp instead of scaling up - the bullet was being SHRUNK
        // toward the floor, not enlarged, no matter how big `extraScale` was dialled ("還是很小" 續166
        // never had a chance - this bug stayed live under it). A trail's own length isn't "how big the
        // model is" anyway, so TrailRenderer (and LineRenderer, same class of transient/degenerate
        // bounds) are excluded from every size measurement and every pivot-preserving recentre below.
        //
        // 續 171/172 (found while debugging "按下K沒發現區別" - the SpearFlipbookVFX overlay added in
        // 續169 introduced a THIRD instance of this exact bug class): a `ParticleSystemRenderer` that
        // has never had `Simulate()`/a real Update tick reach it reports `bounds` as a zero-size box
        // centred at WORLD ORIGIN (0,0,0) - not even at its own transform's position. `Encapsulate`-ing
        // that into the hero's real bounds (measured out at the arena, hundreds of units from the
        // origin) stretched the "size" all the way down to world 0 - `ScaleCloseupTargetToFit` then
        // measured a wildly oversized `currentRadius` and SHRANK the whole SpearVolley hero object
        // instead of enlarging it (confirmed via a controlled non-Play test: scale factor came out
        // 0.30 instead of the expected several-times enlargement), and `BuildCompareClone`'s gap
        // (derived from that same contaminated bounds) came out ~910 units - the comparison twin was
        // being placed nearly a kilometre away, completely off-camera, which is exactly why K produced
        // no visible difference. Excluded the same way TrailRenderer/LineRenderer already are.
        private static List<Renderer> GetSizeRenderers(GameObject go)
        {
            var all = go.GetComponentsInChildren<Renderer>();
            var list = new List<Renderer>(all.Length);
            foreach (var r in all)
            {
                if (r is TrailRenderer || r is LineRenderer || r is ParticleSystemRenderer) continue;
                list.Add(r);
            }
            return list;
        }

        // 續 162 (user: "還是偏畫面上方 請往下移動一點到螢幕中心") - scaling around a pivot that isn't at
        // the mesh's visual centre (common for capsule/character-shaped projectile meshes, whose pivot
        // often sits at one end rather than the middle) grows the object lopsided - reads as "drifting"
        // even when the stage itself is measured symmetric. Re-measure after scaling and shift position
        // by however much the rendered centre moved, so growing/shrinking an object never displaces
        // where it visually sits. Shared by Close-up's fit-to-frame resize and Inspect's flat per-attack
        // multiplier (續165, "i模式的9讓他本體放大三倍") - same problem, same fix, either mode.
        private static void ScaleObjectPreserveCenter(GameObject go, float factor)
        {
            if (Mathf.Approximately(factor, 1f)) return;
            var rends = GetSizeRenderers(go);
            if (rends.Count == 0) { go.transform.localScale *= factor; return; }
            Bounds before = rends[0].bounds;
            for (int i = 1; i < rends.Count; i++) before.Encapsulate(rends[i].bounds);

            go.transform.localScale *= factor;

            var rends2 = GetSizeRenderers(go);
            if (rends2.Count == 0) return;
            Bounds after = rends2[0].bounds;
            for (int i = 1; i < rends2.Count; i++) after.Encapsulate(rends2[i].bounds);
            go.transform.position += (before.center - after.center);
        }

        private static void SetLayerRecursive(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursive(child.gameObject, layer);
        }

        private void Fire(int index)
        {
            var pool = boss.AttackPool;
            if (index < 0 || index >= pool.Count || pool[index] == null || _dummy == null) return;
            if (_running != null) StopCoroutine(_running);
            if (_compareClone != null) { Destroy(_compareClone); _compareClone = null; }
            _selected = index;
            var fireDef = pool[index];

            // 續 161 (user: "要確保每次按下數字或y都能撥放") - real cause: the watermarks below used
            // to be captured from `attacks.SpawnedCount` BEFORE `FireRoutine`'s own `attacks.CancelAll()`
            // actually ran (that only fires once the StartCoroutine below executes it) - so every Fire()
            // after the very first one latched a STALE, too-high watermark from the list's pre-clear
            // state. `_spawned` then gets cleared and repopulated from index 0 by the new attack, but
            // the stale watermark could never fall inside that fresh range again - newly-spawned VFX
            // never got layer-isolated (invisible against the culled Inspect/Close-up camera) and
            // never got picked up as `_closeupTarget`. Cancel HERE, synchronously, before either
            // watermark is captured, so both always start counting from a genuinely empty list.
            if (attacks != null) attacks.CancelAll();

            if (_inspectMode)
            {
                // fresh watermark so this fire's VFX isolates cleanly from whatever the previous one
                // left, and reset boss/dummy back onto the fixed showcase line in case the previous
                // attack physically moved the boss (a charge dash, say).
                _inspectTargets.Clear();
                _inspectHeroFound = false;
                _inspectWatermark = attacks != null ? attacks.SpawnedCount : 0;
                RestageShowcase(Camera.main, showcaseHalfSeparation, showcaseFrameMargin);
            }
            if (_closeupMode)
            {
                _closeupTargets.Clear();
                _closeupHeroFound = false;
                _closeupWatermark = attacks != null ? attacks.SpawnedCount : 0;
                // 續 157 (user: "u模式...主要是看物件發射型攻擊,主要是六連彈、長矛型光彈...一次只射出
                // 一個子彈,這個子彈從左往右飛行且非常緩慢") - same left/right staging Inspect uses, just
                // zoomed in tight (a much smaller half-separation) so the single bullet fills far more
                // of the screen while still visibly crossing it.
                RestageShowcase(Camera.main, closeupHalfSeparation, closeupFrameMargin, flatTrajectory: true);
                fireDef = BuildCloseupFireDef(fireDef);
            }
            _running = StartCoroutine(FireRoutine(fireDef));
        }

        // 續 157/159 - ProjectileBurst (六連彈), SpearVolley (長矛型光彈), LightningMark (雷擊標記) are
        // multi-instance attacks by design (real combat wants the volley/barrage); Close-up mode wants
        // exactly the opposite - ONE instance, slow, so its own shape reads clearly. Rather than touch
        // the real ScriptableObject asset (would change actual combat balance), clone it in memory for
        // just this one fire and override count/speed/homing on the clone - `attacks.Run` never knows
        // the difference. These are also the only three attacks Close-up's digit keys respond to
        // (`IsCloseupEligible`) - every other attack in the pool is a no-op while Close-up is active,
        // since this override only exists for these three.
        [Header("VFX Close-up: single-bullet override for ProjectileBurst/SpearVolley/LightningMark")]
        // 續 164 (user: "子彈本身飛行一段很小距離就消失了 請讓延長飛行距離") - real cause: the projectile
        // (YuanpeiProjectile) destroys itself the instant its own surface overlaps the 稻草人's real
        // CapsuleCollider (OrbSurfaceHitsPlayer) - working as designed (that's how it "hits" in real
        // combat), but at the old 1.6 half-separation (3.2m total width) it reaches hit range almost
        // immediately after leaving the boss. Every other Close-up quantity (target size fraction,
        // vertical bias, frame margin) is expressed as a fraction OF this value, so widening it alone
        // gives more travel distance without changing how big the bullet reads on screen or where it
        // sits in frame - the whole stage just scales up together.
        [SerializeField] private float closeupHalfSeparation = 5f;   // was 1.6
        [SerializeField] private float closeupProjectileSpeed = 0.6f;   // 續159 (user: "飛行速度大幅放緩") 2.5→0.6; adjustable live with -/= while Close-up is active (續160)
        [SerializeField] private float closeupVerticalBiasFraction = 0.15f;   // 續163 (user: "不夠下面 繼續往下移動") - fraction of `closeupHalfSeparation`, raises the camera to push the stage down-frame
        private YuanpeiAttackDef _closeupDefClone;

        private static bool IsCloseupEligible(YuanpeiAttackDef def)
        {
            return def != null && (def.attackId == YuanpeiAttackId.ProjectileBurst
                || def.attackId == YuanpeiAttackId.SpearVolley
                || def.attackId == YuanpeiAttackId.LightningMark);
        }

        private YuanpeiAttackDef BuildCloseupFireDef(YuanpeiAttackDef original)
        {
            if (original == null || !IsCloseupEligible(original)) return original;

            if (_closeupDefClone != null) Destroy(_closeupDefClone);
            var clone = Instantiate(original);
            clone.count = 1;

            // 續 161 (user: "起始等待時間長") - telegraphSeconds/windupSeconds are the generic Run()
            // wrapper's pre-attack pulse (spec §3.2), shared machinery every attack goes through before
            // its own coroutine even starts - shrinking them cuts dead time up front without touching
            // each attack's own distinctive telegraph (MuzzleCharge/SpearMuzzleGlow/rune-circle warn),
            // which is still worth watching, not just "waiting".
            clone.telegraphSeconds = Mathf.Min(clone.telegraphSeconds, 0.15f);
            clone.windupSeconds = Mathf.Min(clone.windupSeconds, 0.05f);

            if (original.attackId == YuanpeiAttackId.LightningMark)
            {
                // number1/number3 here are radius/stagger-interval, not flight speed - LightningMark
                // doesn't travel across space at all (it just marks the dummy's own spot), so there's
                // no flight speed to slow down; count=1 alone already gives "one instance" (續159, the
                // only requirement that actually applies to this move). number2 is the rune-circle
                // warn duration - halved (not zeroed) so the telegraph animation is still watchable,
                // just not the full drawn-out real value on top of the slow-motion multiplier.
                clone.number2 = Mathf.Min(clone.number2, 0.6f);
            }
            else
            {
                clone.number1 = closeupProjectileSpeed;   // both projectile attacks use number1 as flight speed
                if (original.attackId == YuanpeiAttackId.SpearVolley)
                {
                    clone.number3 = 0f;   // homingStrength off - a single bullet should fly dead straight
                    clone.number4 = 0f;   // homingSeconds off
                }
            }
            _closeupDefClone = clone;
            return clone;
        }

        private IEnumerator FireRoutine(YuanpeiAttackDef def)
        {
            attacks.CancelAll();
            Debug.Log("[YuanpeiAttackDebug] firing " + def.attackId + " (" + def.displayName + ")");
            yield return attacks.Run(def, _dummy.transform, boss, phase => { });
            _hasRestPos = true;
            _restPos = boss.transform.position;

            // safety net: ChargeCrush's void-punt drops its target ~46m and only YuanpeiEncounter.
            // Defeat() normally teleports the real player back - this tool never starts a real
            // encounter, and the dummy has no CharacterController to catch it either.
            float floorY = boss.Config != null ? boss.Config.arenaCenter.y : 0f;
            if (_dummy != null && _dummy.transform.position.y < floorY - fallRecoverDepth)
            {
                ResetDummyPosition();
            }
            _running = null;
        }

        public void Enter()
        {
            if (Active || boss == null || attacks == null) return;
            Active = true;
            _animSpeed = 1f;
            _paused = false;
            Time.timeScale = 1f;

            _player = ResolvePlayer();
            _playerHealth = _player != null ? _player.GetComponentInChildren<Live2DAction.Core.Health>() : null;
            if (_playerHealth != null) _playerHealth.SetInvulnerable(this, true);
            SetWorldInputLocked(true);

            // 續 143 (user: "稻草人在非常高的高空") - if a real encounter was never triggered first, the
            // boss just sits at its authored idle "giant sky logo" transform (~Y42, scale 1700, no
            // IntroRoutine ever ran). Snap it to a sane combat position/scale instantly (no 2.6s
            // cinematic needed for a dev tool) BEFORE placing anything relative to it.
            boss.SnapToCombatPose(boss.Config != null ? boss.Config.arenaCenter : boss.transform.position);
            boss.enabled = false;   // stop the FSM's own scheduling/hover/scoring - direct control only
            _hasRestPos = true;
            _restPos = boss.transform.position;

            bool freshDummy = _dummy == null;
            if (freshDummy) BuildDummy();
            _dummy.SetActive(true);
            // 續 149 - prefer wherever the user left the dummy last time (any previous Play session),
            // only falling back to the default plaza spot if nothing was ever saved.
            if (freshDummy && !TryLoadDummyPrefs()) ResetDummyPosition();

            if (hud != null) { _hudWasVisible = true; hud.SetVisible(true); }

            RefreshRings();
            Debug.Log("[YuanpeiAttackDebug] ON - digits 1-" + (boss.AttackPool != null ? boss.AttackPool.Count.ToString() : "?") +
                      " fire that pool attack at the 稻草人 target dummy (bypasses cooldown/range/energy). " +
                      "Arrow keys move the dummy, " + verticalUpKey + "/" + verticalDownKey + " lift it up/down, " +
                      "Shift+either does the same to the boss, " + snapDummyToPlayerKey + " snaps the dummy to you, " +
                      resetDummyKey + " resets it to the ground next to the boss, " + dummyViewKey + " toggles 稻草人視角, " +
                      inspectKey + " toggles VFX Inspect (boss=left/dummy=right, fixed cam, real speed), " +
                      closeupKey + " toggles VFX Close-up (one object, screen-filling, slow motion), " + hidePanelKey + " hides this panel. " +
                      replayKey + " replay, P pause (free-look with mouse+WASD while paused), -/= speed, " + rangeRingKey + " toggle range rings. " +
                      "貓咪附身(C)/守望者視角(T) 已暫時停用，不會再搶走這個工具的攝影機。");
        }

        public void Exit()
        {
            if (!Active) return;
            Active = false;
            if (_running != null) { StopCoroutine(_running); _running = null; }
            attacks.CancelAll();
            if (_paused) SetFreeLook(false);
            if (_dummyView) { SetDummyView(false); _dummyView = false; }
            if (_inspectMode) { SetInspectMode(false); _inspectMode = false; }
            if (_closeupMode) { SetCloseupMode(false); _closeupMode = false; }
            SetWorldInputLocked(false);
            _paused = false;
            Time.timeScale = 1f;

            if (_playerHealth != null) _playerHealth.SetInvulnerable(this, false);
            _playerHealth = null;

            if (boss != null) boss.enabled = true;
            if (hud != null) hud.SetVisible(_hudWasVisible);
            if (_dummy != null) _dummy.SetActive(false);

            ClearRings();
            Debug.Log("[YuanpeiAttackDebug] OFF");
        }

        private void OnDisable()
        {
            if (Active) Exit();
        }

        // Player + Cat both carry PlayerInputProvider - prefer the one named "Player" (mirrors
        // YuanpeiBoss.ResolvePlayer / YuanpeiEncounter.ResolvePlayerFrom's convention).
        private Transform ResolvePlayer()
        {
            var providers = FindObjectsByType<Live2DAction.Input.PlayerInputProvider>(FindObjectsSortMode.None);
            Transform first = null;
            foreach (var p in providers)
            {
                Transform root = p.transform.root;
                if (first == null) first = root;
                if (root.name == "Player") return root;
            }
            return first;
        }

        // ---------------------------------------------------------------- 稻草人 target dummy

        private void BuildDummy()
        {
            if (_dummy != null) return;
            _dummy = new GameObject("YuanpeiDebugTargetDummy");

            // purely a visual silhouette so it reads as "a target standing here" from any angle -
            // post + straw body + crossed arms + head, all primitives, no imported asset needed for
            // a throwaway dev-only stand-in.
            var post = MakePart(PrimitiveType.Cube, "Post", new Vector3(0f, 0.4f, 0f), new Vector3(0.12f, 0.8f, 0.12f), new Color(0.35f, 0.22f, 0.12f));
            var body = MakePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 1.1f, 0f), new Vector3(0.55f, 0.7f, 0.55f), new Color(0.85f, 0.72f, 0.35f));
            var head = MakePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.85f, 0f), Vector3.one * 0.42f, new Color(0.75f, 0.6f, 0.3f));
            var arms = MakePart(PrimitiveType.Cube, "Arms", new Vector3(0f, 1.45f, 0f), new Vector3(1.5f, 0.1f, 0.1f), new Color(0.4f, 0.28f, 0.14f));

            // tall bright beacon above the head so the dummy's XZ position is unmistakable from any
            // distance/angle while repositioning it (續 142, user: "必須讓稻草人可以移動位置").
            var beaconMat = new Material(Shader.Find("Live2DAction/VFX/AdditiveUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit"));
            beaconMat.color = new Color(0.3f, 1f, 0.5f);
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Beacon";
            beacon.transform.SetParent(_dummy.transform, false);
            Destroy(beacon.GetComponent<Collider>());
            beacon.transform.localPosition = new Vector3(0f, 4.5f, 0f);
            beacon.transform.localScale = new Vector3(0.08f, 3f, 0.08f);
            var beaconR = beacon.GetComponent<Renderer>();
            beaconR.material = beaconMat;
            beaconR.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // real capsule collider so hit-checks that look for "col.transform.root == target" (same
            // shape the attacks already use against the real player) find something here too.
            var cc = _dummy.AddComponent<CapsuleCollider>();
            cc.center = new Vector3(0f, 1.1f, 0f);
            cc.height = 1.8f;
            cc.radius = 0.4f;

            // 續 147 (user: "稻草人必須無限血量") - it's a repeatable art/VFX target, not something that
            // should ever actually die (a dead Health could trip death-reaction code in whatever attack
            // just hit it, or simply zero out and stop being a useful visual reference for repeat fires).
            var dummyHealth = _dummy.AddComponent<Live2DAction.Core.Health>();
            dummyHealth.SetInvulnerable(this, true);
        }

        private Transform MakePart(PrimitiveType type, string name, Vector3 localPos, Vector3 localScale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_dummy.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor(Shader.PropertyToID("_BaseColor"), color);
            r.SetPropertyBlock(mpb);
            return go.transform;
        }

        // ---------------------------------------------------------------- range rings (視覺化範圍)

        private GameObject RingFor(int i, Color c, float radius, Vector3 center)
        {
            while (_rings.Count <= i)
            {
                var go = new GameObject("YuanpeiDebugRing_" + _rings.Count);
                var lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.loop = true;
                lr.positionCount = 48;
                lr.widthMultiplier = 0.08f;
                var sh = Shader.Find("Live2DAction/VFX/AdditiveUnlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
                lr.material = new Material(sh);
                lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                _rings.Add(go);
            }
            var ring = _rings[i];
            var line = ring.GetComponent<LineRenderer>();
            line.startColor = line.endColor = c;
            for (int k = 0; k < line.positionCount; k++)
            {
                float a = (k / (float)line.positionCount) * Mathf.PI * 2f;
                line.SetPosition(k, center + new Vector3(Mathf.Cos(a) * radius, 0.05f, Mathf.Sin(a) * radius));
            }
            return ring;
        }

        private void RefreshRings()
        {
            if (!_showRings || boss == null)
            {
                ClearRings();
                return;
            }
            var cfg = boss.Config;
            if (cfg != null) RingFor(0, new Color(1f, 1f, 1f, 0.35f), cfg.arenaRadius, cfg.arenaCenter).SetActive(true);

            var pool = boss.AttackPool;
            if (pool != null && _selected >= 0 && _selected < pool.Count && pool[_selected] != null)
            {
                var def = pool[_selected];
                Vector3 c = boss.transform.position;
                RingFor(1, new Color(0.3f, 1f, 0.4f, 0.6f), def.minRange, c).SetActive(def.minRange > 0.05f);
                RingFor(2, new Color(1f, 0.35f, 0.25f, 0.6f), def.maxRange, c).SetActive(def.maxRange > 0.05f);
            }
            else if (_rings.Count > 1)
            {
                for (int i = 1; i < _rings.Count; i++) if (_rings[i] != null) _rings[i].SetActive(false);
            }
        }

        private void ClearRings()
        {
            foreach (var r in _rings) if (r != null) Destroy(r);
            _rings.Clear();
        }

        private void OnGUI()
        {
            if (!Active) return;

            // 續 154 (user: "提供一個按鍵能隱藏i模式下的面板提示文字") - a clean shot for screenshots/
            // recording, especially in VFX Inspect/Close-up. Still Active underneath (digits etc. all
            // keep working) - just the text overlay is gone, with a tiny reminder of the key to bring
            // it back so it's never fully forgotten.
            if (_hidePanel)
            {
                GUI.color = Color.white;
                GUI.Label(new Rect(12, 12, 200, 20), "(" + hidePanelKey + " to show panel)");
                return;
            }

            // 續 160 (user: "u模式下清理掉非必要提示") - Close-up only ever cares about 3 of the 9
            // attacks and none of the reposition/range-ring stuff (boss/dummy are on a fixed staged
            // line, not something you walk around while this is up) - the full panel below is mostly
            // noise here. Separate, short panel instead of trying to cut lines out of the big one.
            if (_closeupMode)
            {
                var csb = new System.Text.StringBuilder();
                csb.AppendLine("VFX CLOSE-UP  (" + closeupKey + " exit close-up, " + toggleKey + " exit debug, " + hidePanelKey + " hide)");
                csb.AppendLine("1=六連彈  3=雷擊標記  9=長矛型光彈   (其餘數字鍵無作用)");
                csb.AppendLine("bullet speed " + closeupProjectileSpeed.ToString("0.00") + "  (-/= 調整)   " + replayKey + " 重播上一招");
                csb.AppendLine(compareKey + " 有/無特效對照 " + (_compareMode ? "ON" : "off") + "（僅長矛型光彈9，疊在下方）");
                GUI.color = Color.white;
                GUI.Box(new Rect(12, 12, 460, 96), csb.ToString());
                return;
            }

            var pool = boss != null ? boss.AttackPool : null;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("YUANPEI ATTACK DEBUG  (" + toggleKey + " exit, " + hidePanelKey + " hide panel)");
            sb.AppendLine("digits fire at 稻草人 | speed " + _animSpeed.ToString("0.00")
                + (_paused ? " [PAUSED - mouse+WASD free-look]" : (_dummyView ? " [稻草人視角 - mouse to look]"
                    : (_inspectMode ? " [VFX INSPECT - fire an attack]" : "")))
                + "  (-/= speed, P pause, " + replayKey + " replay, " + rangeRingKey + " rings " + (_showRings ? "ON" : "off") + ")");
            sb.AppendLine(dummyViewKey + " 稻草人視角, " + inspectKey + " VFX Inspect (左boss/右稻草人, 固定鏡頭, 正常速度), "
                + closeupKey + " VFX Close-up (單一物件全螢幕, 慢速x" + closeupTimeScale.ToString("0.00") + ")");
            sb.AppendLine("arrows = move 稻草人 (XZ), " + verticalUpKey + "/" + verticalDownKey + " = lift 稻草人 (Y), Shift+either = move boss instead, "
                + snapDummyToPlayerKey + " = snap 稻草人 to you, " + resetDummyKey + " = reset 稻草人 to ground");
            sb.AppendLine("green ring = minRange, red ring = maxRange, white ring = arena boundary");
            sb.AppendLine("");
            int shown = 0;
            if (pool != null)
            {
                shown = Mathf.Min(pool.Count, 10);
                for (int i = 0; i < shown; i++)
                {
                    var d = pool[i];
                    if (d == null) continue;
                    string key = "" + ((i + 1) % 10);
                    sb.AppendLine("  [" + key + "] " + d.attackId + "  (" + d.displayName + ")"
                        + (i == _selected ? "   ◀ selected" : "")
                        + "   range " + d.minRange.ToString("0.#") + "-" + d.maxRange.ToString("0.#") + "m"
                        + (d.isMajorHazard ? "  [major]" : ""));
                }
            }
            GUI.color = Color.white;
            GUI.Box(new Rect(12, 12, 620, 98 + 20 * (shown + 2)), sb.ToString());
        }
    }
}
#endif
