using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.CameraSystem
{
    // 2026-08-27, explicit user request ("有一個角色紫色頭髮穿著泳裝 我想讓他在空中(本地正上方)待著,
    // 在他身上掛攝影機 提供我一個方式可以將視角從player轉向守望者(給他的新名字)" + follow-ups:
    // key(T) + code API both; framing = the Watcher's own POV looking DOWN at the battlefield;
    // "讓他可用w/a/s/d移動攝影機視角"; "player駕駛車輛狀態也必須支援t按鍵視角轉換").
    //
    // A spectator/establishing-shot director that moves whichever player camera is currently live
    // (on-foot ThirdPersonCameraController camera, OR the vehicle VehicleCameraController camera -
    // the two are mutually exclusive, VehicleEntrySystem SetActive-toggles them) between the normal
    // follow view and a fixed viewpoint mounted on the sky-hovering "守望者" (Watcher), with a
    // smooth eased pan both ways. While in the Watcher view, W/A/S/D (+ Q/E for down/up) free-move
    // the camera across the battlefield.
    //
    // Why not Cinemachine: this project drives its player cameras with its own controllers (see
    // ThirdPersonCameraController's comment on why yaw/pitch live in exactly one place). Rather than
    // add a Brain and fight over the transform, this director disables the live camera's controller
    // for as long as the Watcher view is active and writes the transform itself; on the way back it
    // re-enables the controller and eases from the held Watcher pose toward wherever the controller
    // now wants the camera, so the hand-off is seamless.
    //
    // Lives on its own always-active GameObject (NOT on a camera) so it keeps running across a
    // vehicle enter/exit that SetActive-toggles the cameras themselves. [DefaultExecutionOrder]
    // above the camera controllers (unset = 0) so this LateUpdate runs AFTER them.
    [DefaultExecutionOrder(200)]
    public class ViewFocusDirector : MonoBehaviour
    {
        public enum ViewState { Player, ToWatcher, Watcher, ToPlayer }

        [Header("On-foot camera")]
        [SerializeField] private Camera onFootCamera;
        [Tooltip("The component that drives onFootCamera (ThirdPersonCameraController). Disabled while the Watcher view is active, re-enabled on the way back.")]
        [SerializeField] private Behaviour onFootController;

        [Header("Vehicle camera (optional - leave null if the project has no vehicles)")]
        [SerializeField] private Camera vehicleCamera;
        [Tooltip("The component that drives vehicleCamera (VehicleCameraController).")]
        [SerializeField] private Behaviour vehicleController;

        // 2026-08-29, user request ("讓 player 守望者/cat 三者可以互相切換視角"). CameraPossessionSwitcher
        // SetActive-swaps Main Camera <-> CatCamera; whichever is live is the one T should take
        // over. Same on-foot/vehicle pattern - ActiveCamera()/ControllerFor() just gained a third
        // candidate. Optional / null-safe (a scene with no cat leaves these unset). suspendWhileWatching
        // should also list the cat's own control set (CharacterMovement etc) so W/A/S/D in the
        // Watcher view doesn't drive the cat - the snapshot-restore in SetSuspended handles the
        // "already disabled because you were the player, not the cat" case.
        [Header("Cat camera (optional - CameraPossessionSwitcher)")]
        [SerializeField] private Camera catCamera;
        [Tooltip("The component that drives catCamera (ThirdPersonCameraController on CatCamera).")]
        [SerializeField] private Behaviour catController;

        [Header("Watcher")]
        [Tooltip("Empty child on 守望者 marking where/which-way the camera sits in Watcher view. Its own (disabled) Camera component is only an editor framing aid - this director copies the Transform, not the settings.")]
        [SerializeField] private Transform watcherViewpoint;
        [Tooltip("守望者's Visual root - its renderers are hidden while the Watcher POV is active (the camera sits at her head; her body would otherwise fill the bottom of frame). Optional.")]
        [SerializeField] private Transform watcherVisualRoot;

        [Header("Blend")]
        [Tooltip("Seconds for the pan between the two views, each direction. 0 = instant hard cut.")]
        [SerializeField] private float blendDuration = 1.5f;
        [SerializeField] private AnimationCurve blendEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("Field of view in the Watcher view (blended in/out). 0 = leave FOV untouched. The director only ever touches FOV, never any other camera setting.")]
        [SerializeField] private float watcherFieldOfView = 70f;

        [Header("Watcher free-move (while in the Watcher view)")]
        [Tooltip("Horizontal pan speed (units/sec) for W/A/S/D, relative to where the camera is currently looking. A/D strafe, W/S go forward/back on the ground plane.")]
        [SerializeField] private float panSpeed = 16f;
        [Tooltip("Vertical pan speed (units/sec) for E (up) / Q (down). 0 disables vertical panning.")]
        [SerializeField] private float verticalPanSpeed = 12f;
        [Tooltip("Free mouse-look while in the Watcher view (locks the cursor for the duration, restores it on the way back).")]
        [SerializeField] private bool watcherMouseLook = true;
        [Tooltip("Degrees of camera rotation per pixel of mouse movement in the Watcher view.")]
        [SerializeField] private float mouseLookSensitivity = 0.12f;
        [SerializeField] private float watcherMinPitch = -80f;
        [SerializeField] private float watcherMaxPitch = 85f;
        [Tooltip("Scroll-wheel FOV (zoom) step per notch in the Watcher view. 0 disables scroll zoom.")]
        [SerializeField] private float scrollZoomStep = 4f;
        [SerializeField] private float watcherMinFov = 15f;
        [SerializeField] private float watcherMaxFov = 110f;
        [Tooltip("If true, every FocusWatcher() starts back at the home pose (clears any W/A/S/D pan, mouse-look and zoom from a previous visit). Home = the committed WatcherViewConfig if one is saved, otherwise the scene's authored Viewpoint.")]
        [SerializeField] private bool resetViewOnFocus = true;
        [Tooltip("Floor the 守望者 rig's world Y can never go below - W/A/S/D flying and any saved/loaded WatcherViewConfig are clamped to this, so the observer can't end up under the ground (invisible).")]
        [SerializeField] private float watcherMinHeight = 1.5f;
        [Tooltip("Max horizontal distance (world units) the 守望者 rig may be flown from its authored mount point - a runaway pan can't lose the observer off the map. 0 = no limit.")]
        [SerializeField] private float watcherMaxFlyRadius = 120f;

        [Header("Save the Watcher view")]
        [Tooltip("Persistable home pose for the Watcher view. Press commitViewKey while watching to bake the current fly position / look angle / zoom into this asset - it survives exiting Play Mode, and later FocusWatcher() calls start from it. Uncheck its hasSavedView (or leave this null) to always use the scene's authored Viewpoint.")]
        [SerializeField] private WatcherViewConfig viewConfig;
        [Tooltip("Extra key that saves the current Watcher view on demand (mid-view, without leaving it). Editor-only. None disables it.")]
        [SerializeField] private Key commitViewKey = Key.K;
        [Tooltip("Auto-save the Watcher view into viewConfig whenever you leave it (switch back to the player, or stop Play Mode) - no key press needed. Editor-only.")]
        [SerializeField] private bool autoSaveView = true;

        [Header("Suspend while watching")]
        [Tooltip("Components disabled for as long as the Watcher view is active and re-enabled on return - so W/A/S/D only pans the camera and doesn't also drive the player / the car. e.g. the player's CharacterMovement and the vehicle's VehicleController.")]
        [SerializeField] private Behaviour[] suspendWhileWatching;

        [Header("Trigger")]
        [Tooltip("Key that toggles between the player view and the Watcher view. None disables the key (code API still works).")]
        [SerializeField] private Key toggleKey = Key.T;
        [SerializeField] private bool startFocusedOnWatcher;

        public ViewState State { get; private set; } = ViewState.Player;
        public bool IsFocusedOnWatcher => State == ViewState.Watcher || State == ViewState.ToWatcher;

        private float _blendT;
        private Pose _blendFrom;
        private float _blendFromFov;
        private Vector3 _flyOffset;   // accumulated W/A/S/D/E/Q translation of the whole 守望者 rig
        private float _lookYaw;       // also drives the 守望者 root yaw so she faces where you look
        private float _lookPitch;     // camera-only (she stays upright)
        private bool _lookSeeded;
        private bool _visualHidden;
        private Camera _drivenCamera;
        private Behaviour _drivenController;
        private bool _suspendedActive;

        // 2026-08-27, user request ("可讓守望者隨著攝影機移動嗎 但保有現在攝影機參數設定") - W/A/S/D
        // and mouse-yaw now move/turn the WHOLE 守望者 root (so Maya flies along and the player sees
        // her wherever you left her), while the Viewpoint child's own local offset + pitch + the
        // director's FOV are untouched - "保有現在攝影機參數設定".
        private Transform _watcherRoot;
        private Vector3 _authoredRootPos;   // the scene's mount point - never overwritten by config/fly
        private Vector3 _rootBasePos;
        private float _rootBaseYaw;
        private float _basePitch;   // camera pitch the current home pose starts at
        private float _homeFov;     // FOV the current home pose starts at
        private float _watcherFov;  // live FOV in the Watcher view (scroll-wheel adjustable)
        private bool _rootMoved;

        private void Awake()
        {
            if (watcherViewpoint != null)
            {
                _watcherRoot = watcherViewpoint.parent != null ? watcherViewpoint.parent : watcherViewpoint;
                _authoredRootPos = _watcherRoot.position;
                ResolveHomePose();
            }
        }

        // The "home" the Watcher view starts from and resets to: the committed WatcherViewConfig if
        // one is saved, otherwise the scene's authored 守望者 root + Viewpoint child.
        private void ResolveHomePose()
        {
            if (viewConfig != null && viewConfig.hasSavedView)
            {
                _rootBasePos = ClampWatcherPos(viewConfig.rootPosition);
                _rootBaseYaw = viewConfig.rootYaw;
                _basePitch = viewConfig.cameraPitch;
                _homeFov = viewConfig.fieldOfView > 0f ? viewConfig.fieldOfView : watcherFieldOfView;
            }
            else if (_watcherRoot != null)
            {
                _rootBasePos = _watcherRoot.position;
                _rootBaseYaw = _watcherRoot.eulerAngles.y;
                float p = watcherViewpoint != null ? watcherViewpoint.eulerAngles.x : 0f;
                _basePitch = p > 180f ? p - 360f : p;
                _homeFov = watcherFieldOfView;
            }
        }

        // Keeps the observer above ground and within reach of its mount point - a bad saved
        // WatcherViewConfig (e.g. a garbage Y from a glitched session) or a runaway W/A/S/D pan
        // can't put Maya underground / off the map where she reads as "just gone".
        private Vector3 ClampWatcherPos(Vector3 pos)
        {
            pos.y = Mathf.Max(pos.y, watcherMinHeight);
            if (watcherMaxFlyRadius > 0f)
            {
                Vector2 flat = new Vector2(pos.x - _authoredRootPos.x, pos.z - _authoredRootPos.z);
                if (flat.magnitude > watcherMaxFlyRadius)
                {
                    flat = flat.normalized * watcherMaxFlyRadius;
                    pos.x = _authoredRootPos.x + flat.x;
                    pos.z = _authoredRootPos.z + flat.y;
                }
            }
            return pos;
        }

        private void Start()
        {
            if (startFocusedOnWatcher && ActiveCamera() != null && watcherViewpoint != null)
            {
                _drivenCamera = ActiveCamera();
                _drivenController = ControllerFor(_drivenCamera);
                _flyOffset = Vector3.zero;
                SeedLook();
                ApplyWatcherRig();
                SetControllerEnabled(_drivenController, false);
                SetSuspended(true);
                SetWatcherVisualHidden(true);
                State = ViewState.Watcher;
                _blendT = 1f;
                ApplyWatcher();
            }
        }

#if UNITY_EDITOR
        private void OnEnable()
        {
            UnityEditor.EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            // Stopping Play while still in the Watcher view: bake the flown view before the editor
            // tears everything down (OnDisable's SaveAssetIfDirty is unreliable mid-teardown).
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode && autoSaveView && IsFocusedOnWatcher)
            {
                CommitCurrentView();
            }
        }
#endif

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
#endif
            // Safety: if this director is torn down mid-Watcher-view, don't leave the camera
            // controller / player / vehicle permanently disabled.
            if (State != ViewState.Player)
            {
                SetControllerEnabled(_drivenController, true);
                SetSuspended(false);
                SetWatcherVisualHidden(false);
                RestoreWatcherRig();
                ReleaseCursor();
                State = ViewState.Player;
            }
        }

        private void ReleaseCursor()
        {
            if (!watcherMouseLook) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // ---- public API (key binding + cutscenes / scripted events) ----

        public void Toggle()
        {
            if (IsFocusedOnWatcher) FocusPlayer();
            else FocusWatcher();
        }

        public void FocusWatcher()
        {
            if (IsFocusedOnWatcher) return;
            Camera cam = ActiveCamera();
            if (cam == null || watcherViewpoint == null) return;

            _drivenCamera = cam;
            _drivenController = ControllerFor(cam);
            ResolveHomePose();
            if (resetViewOnFocus)
            {
                _flyOffset = Vector3.zero;
                SeedLook();
            }
            else if (!_lookSeeded)
            {
                SeedLook();
            }
            ApplyWatcherRig();
            SetControllerEnabled(_drivenController, false);
            SetSuspended(true);
            SetWatcherVisualHidden(true);
            if (watcherMouseLook)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            BeginBlend(ViewState.ToWatcher);
        }

        public void FocusPlayer()
        {
            if (State == ViewState.Player || State == ViewState.ToPlayer) return;
            if (autoSaveView) CommitCurrentView(); // "自動保存" - bake the flown view on the way out
            // Re-enable immediately so the controller starts producing the live target pose the
            // return blend eases toward; the camera transform stays overwritten until it completes.
            SetControllerEnabled(_drivenController, true);
            SetSuspended(false);
            // The on-foot controller re-locks the cursor itself in its OnEnable; anything else
            // (vehicle / none) doesn't use mouse-look, so hand the cursor back free.
            if (watcherMouseLook && _drivenController != onFootController)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            BeginBlend(ViewState.ToPlayer);
        }

        private void SeedLook()
        {
            _lookYaw = _rootBaseYaw;
            _lookPitch = _basePitch;
            _watcherFov = _homeFov;
            _lookSeeded = true;
        }

        // Moves/turns the whole 守望者 rig to its flown position + yaw (Maya + Viewpoint child ride
        // along). The Viewpoint child's local offset/pitch and the director's FOV are never touched.
        private void ApplyWatcherRig()
        {
            if (_watcherRoot == null) return;
            Vector3 pos = ClampWatcherPos(_rootBasePos + _flyOffset);
            // fold any clamp back into _flyOffset so W/A/S/D doesn't keep "pushing" past the limit
            _flyOffset = pos - _rootBasePos;
            _watcherRoot.SetPositionAndRotation(pos, Quaternion.Euler(0f, _lookYaw, 0f));
            _rootMoved = true;
        }

        private void RestoreWatcherRig()
        {
            if (_watcherRoot == null || !_rootMoved) return;
            if (resetViewOnFocus)
            {
                _watcherRoot.SetPositionAndRotation(_rootBasePos, Quaternion.Euler(0f, _rootBaseYaw, 0f));
                _flyOffset = Vector3.zero;
                _lookSeeded = false;
            }
            _rootMoved = false;
        }

        // ---- internals ----

        private Camera ActiveCamera()
        {
            if (onFootCamera != null && onFootCamera.isActiveAndEnabled) return onFootCamera;
            if (catCamera != null && catCamera.isActiveAndEnabled) return catCamera;
            if (vehicleCamera != null && vehicleCamera.isActiveAndEnabled) return vehicleCamera;
            return null;
        }

        private Behaviour ControllerFor(Camera cam)
        {
            if (cam == onFootCamera) return onFootController;
            if (cam == catCamera) return catController;
            if (cam == vehicleCamera) return vehicleController;
            return null;
        }

        private void BeginBlend(ViewState next)
        {
            if (_drivenCamera == null) _drivenCamera = ActiveCamera();
            if (_drivenCamera != null)
            {
                _blendFrom = new Pose(_drivenCamera.transform.position, _drivenCamera.transform.rotation);
                _blendFromFov = _drivenCamera.fieldOfView;
            }
            _blendT = blendDuration > 0.0001f ? 0f : 1f;
            State = next;
        }

        private void LateUpdate()
        {
            if (toggleKey != Key.None && Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                Toggle();
            }

            // 2026-08-28, playtested bug ("玩家視角看不見他") - Maya's renderers were toggled only on
            // state-transition events, which could get out of sync (auto-save commit paths, play-mode
            // exit, a swapped camera). Assert it declaratively every frame instead: her body is
            // hidden ONLY while her own POV camera is what you're looking at (Watcher / blending in),
            // and visible any other time - including the whole blend BACK to the player, so you see
            // her as the camera pans away. The _visualHidden guard makes this a no-op when unchanged.
            SetWatcherVisualHidden(IsFocusedOnWatcher);

            if (State == ViewState.Player) return;

            // The camera we took over got SetActive-swapped from under us (player entered/exited a
            // vehicle while watching). Restore that camera's controller and drop back to Player -
            // whatever camera is now live just shows its own normal view.
            if (_drivenCamera == null || !_drivenCamera.isActiveAndEnabled)
            {
                if (autoSaveView) CommitCurrentView();
                SetControllerEnabled(_drivenController, true);
                SetSuspended(false);
                SetWatcherVisualHidden(false);
                RestoreWatcherRig();
                ReleaseCursor();
                State = ViewState.Player;
                return;
            }

            switch (State)
            {
                case ViewState.Watcher:
                    ReadMouseLook();
                    ReadScrollZoom();
                    ReadPanInput();
                    ReadCommitKey();
                    ApplyWatcherRig();
                    ApplyWatcher();
                    break;

                case ViewState.ToWatcher:
                {
                    _blendT = Mathf.Clamp01(_blendT + BlendStep());
                    float e = blendEase.Evaluate(_blendT);
                    ApplyPose(BlendPose(_blendFrom, WatcherPose(), e));
                    if (_watcherFov > 0f) _drivenCamera.fieldOfView = Mathf.Lerp(_blendFromFov, _watcherFov, e);
                    if (_blendT >= 1f) State = ViewState.Watcher;
                    break;
                }

                case ViewState.ToPlayer:
                {
                    _blendT = Mathf.Clamp01(_blendT + BlendStep());
                    float e = blendEase.Evaluate(_blendT);
                    // The controller (re-enabled in FocusPlayer, runs before this) has already put
                    // the camera at the live player pose+FOV this frame - read them as the target.
                    Pose target = new Pose(_drivenCamera.transform.position, _drivenCamera.transform.rotation);
                    float targetFov = _drivenCamera.fieldOfView;
                    ApplyPose(BlendPose(_blendFrom, target, e));
                    if (_watcherFov > 0f) _drivenCamera.fieldOfView = Mathf.Lerp(_blendFromFov, targetFov, e);
                    if (_blendT >= 1f)
                    {
                        State = ViewState.Player;
                        SetWatcherVisualHidden(false);
                        RestoreWatcherRig();
                    }
                    break;
                }
            }
        }

        private float BlendStep() => blendDuration > 0.0001f ? Time.unscaledDeltaTime / blendDuration : 1f;

        private void ReadMouseLook()
        {
            if (!watcherMouseLook) return;
            // Keep the cursor captured for the whole Watcher view (same idiom ThirdPersonCameraController
            // uses) - re-asserted every frame so an Alt-Tab / Escape that drops the lock re-grabs it.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            Vector2 d = mouse.delta.ReadValue();
            _lookYaw += d.x * mouseLookSensitivity;
            _lookPitch = Mathf.Clamp(_lookPitch - d.y * mouseLookSensitivity, watcherMinPitch, watcherMaxPitch);
        }

        private void ReadScrollZoom()
        {
            if (scrollZoomStep <= 0f || _watcherFov <= 0f) return;
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll == 0f) return;
            _watcherFov = Mathf.Clamp(_watcherFov - Mathf.Sign(scroll) * scrollZoomStep, watcherMinFov, watcherMaxFov);
        }

        private void ReadCommitKey()
        {
            if (commitViewKey == Key.None || Keyboard.current == null) return;
            if (Keyboard.current[commitViewKey].wasPressedThisFrame) CommitCurrentView();
        }

        // Bakes the current flown pose / look angle / zoom into the home pose, and (in the Editor)
        // persists it to the WatcherViewConfig asset so it survives leaving Play Mode - "要能保存
        // 守望者視角中攝影機的變更設置".
        private void CommitCurrentView()
        {
            // Never persist a pose that would hide the observer (underground / off the map).
            Vector3 pos = ClampWatcherPos(_watcherRoot != null ? _watcherRoot.position : (_rootBasePos + _flyOffset));
            _rootBasePos = pos;
            _rootBaseYaw = _lookYaw;
            _basePitch = _lookPitch;
            _flyOffset = Vector3.zero;

#if UNITY_EDITOR
            if (viewConfig != null)
            {
                viewConfig.hasSavedView = true;
                viewConfig.rootPosition = pos;
                viewConfig.rootYaw = _lookYaw;
                viewConfig.cameraPitch = _lookPitch;
                viewConfig.fieldOfView = _watcherFov;
                UnityEditor.EditorUtility.SetDirty(viewConfig);
                UnityEditor.AssetDatabase.SaveAssetIfDirty(viewConfig);
                Debug.Log($"[ViewFocusDirector] Watcher view saved to {UnityEditor.AssetDatabase.GetAssetPath(viewConfig)} " +
                          $"(pos {pos:F2}, yaw {_lookYaw:F0}, pitch {_lookPitch:F0}, fov {_watcherFov:F0}).");
            }
            else
            {
                Debug.LogWarning("[ViewFocusDirector] commit key pressed but no WatcherViewConfig assigned - the view is re-homed for this session only, nothing persisted.");
            }
#endif
        }

        private void ReadPanInput()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            float x = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
            float z = (kb.wKey.isPressed ? 1f : 0f) - (kb.sKey.isPressed ? 1f : 0f);
            float y = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
            if (x == 0f && y == 0f && z == 0f) return;

            // W/S/A/D fly along the ground plane relative to the current yaw (so W is "where you're
            // facing" regardless of how far down you've pitched); E/Q are straight world up/down.
            Quaternion yawOnly = Quaternion.Euler(0f, _lookYaw, 0f);
            Vector3 forward = yawOnly * Vector3.forward;
            Vector3 right = yawOnly * Vector3.right;

            float dt = Time.unscaledDeltaTime;
            _flyOffset += (right * x + forward * z) * panSpeed * dt;
            _flyOffset += Vector3.up * y * verticalPanSpeed * dt;
        }

        private Quaternion LookRotation()
        {
            if (!_lookSeeded) SeedLook();
            return Quaternion.Euler(_lookPitch, _lookYaw, 0f);
        }

        // Camera pose in the Watcher view. Position comes from the Viewpoint child (which
        // ApplyWatcherRig has already moved/yawed with the whole 守望者 rig this frame, keeping its
        // authored local offset intact); rotation adds the camera-only pitch on top of the yaw.
        private Pose WatcherPose()
        {
            Vector3 pos = watcherViewpoint != null ? watcherViewpoint.position : (_rootBasePos + _flyOffset);
            return new Pose(pos, LookRotation());
        }

        private void ApplyWatcher()
        {
            ApplyPose(WatcherPose());
            if (_watcherFov > 0f && _drivenCamera != null) _drivenCamera.fieldOfView = _watcherFov;
        }

        private void ApplyPose(Pose pose)
        {
            if (_drivenCamera != null) _drivenCamera.transform.SetPositionAndRotation(pose.position, pose.rotation);
        }

        private static void SetControllerEnabled(Behaviour controller, bool value)
        {
            if (controller != null && controller.enabled != value) controller.enabled = value;
        }

        private bool[] _suspendedPrevEnabled;

        private void SetSuspended(bool suspended)
        {
            if (suspendWhileWatching == null || _suspendedActive == suspended) return;
            _suspendedActive = suspended;

            if (suspended)
            {
                // 2026-08-28, playtested bug ("非駕駛模式時控制w/a/s/d時會玩家連同car一直做移動控制")
                // - the old code blindly re-enabled everything on the way out, so the Buggy's
                // VehicleController (kept disabled by VehicleEntrySystem while on foot) came back
                // enabled after a Watcher-view visit and the parked car started eating W/A/S/D
                // alongside the player. Snapshot each component's ACTUAL enabled state now and
                // restore exactly that, so a component that was already off stays off.
                _suspendedPrevEnabled = new bool[suspendWhileWatching.Length];
                for (int i = 0; i < suspendWhileWatching.Length; i++)
                {
                    Behaviour b = suspendWhileWatching[i];
                    _suspendedPrevEnabled[i] = b != null && b.enabled;
                    if (b != null) b.enabled = false;
                }
            }
            else if (_suspendedPrevEnabled != null)
            {
                for (int i = 0; i < suspendWhileWatching.Length && i < _suspendedPrevEnabled.Length; i++)
                {
                    Behaviour b = suspendWhileWatching[i];
                    if (b != null) b.enabled = _suspendedPrevEnabled[i];
                }
            }
        }

        private void SetWatcherVisualHidden(bool hidden)
        {
            if (watcherVisualRoot == null || _visualHidden == hidden) return;
            _visualHidden = hidden;
            foreach (Renderer r in watcherVisualRoot.GetComponentsInChildren<Renderer>(true))
            {
                r.enabled = !hidden;
            }
        }

        // Pure, so the pan math is directly EditMode-testable without a live scene - same convention
        // as ThirdPersonCameraController.ComputeCameraPosition / ClampDistanceForObstruction.
        public static Pose BlendPose(Pose from, Pose to, float t)
        {
            t = Mathf.Clamp01(t);
            return new Pose(
                Vector3.LerpUnclamped(from.position, to.position, t),
                Quaternion.SlerpUnclamped(from.rotation, to.rotation, t));
        }
    }
}
