using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.Combat;
using Live2DAction.Core;
using Live2DAction.Targeting;
using Live2DAction.Vehicles;

namespace Live2DAction.CameraSystem
{
    // 2026-08-28, explicit user request ("這是一隻貓，並請向玩家一樣提供他攝影機視角並且可將視角切換
    // 到他身上，注意他視線較低，與先前攝影機風格不同") - a possession swap between the player and the
    // Cat: press C (or call FocusCat()/FocusPlayer()) and you both SEE through and CONTROL the other
    // one. Instant hard cut, not an eased establishing pan (that's ViewFocusDirector's job for the
    // 守望者 spectator view) - this is "you are now the cat".
    //
    // Two mechanisms, both already precedented in this project:
    //   - camera swap: SetActive-toggle between the two Camera GameObjects, exactly like
    //     VehicleEntrySystem does for the on-foot vs vehicle camera. Each camera carries its own
    //     ThirdPersonCameraController (the Cat's is tuned for its low eyeline - see CatCharacterSetup),
    //     and that controller's own OnEnable/OnDisable already hands the locked cursor back and forth.
    //   - control hand-off: exactly one side's control components (CharacterMovement, ...) are enabled
    //     at a time so WASD only ever drives the character you're looking at. Unlike
    //     ViewFocusDirector.suspendWhileWatching this is a hard "this set on, that set off" - the
    //     player's and the cat's movement components exist only to be governed by possession, nothing
    //     else toggles them.
    //
    // Lives on its own always-active GameObject (NOT on a camera) so it survives either camera being
    // SetActive-toggled. [DefaultExecutionOrder] above the camera controllers (0) is not strictly
    // needed (this only acts on key-press frames) but keeps it deterministic if it ever grows.
    [DefaultExecutionOrder(150)]
    public class CameraPossessionSwitcher : MonoBehaviour
    {
        public enum Possessed { Player, Cat, GuessWho }

        [SerializeField] private GameObject playerCamera;
        [SerializeField] private GameObject catCamera;

        [Tooltip("Components enabled ONLY while the player is possessed - the player's CharacterMovement " +
                 "(and anything else that eats WASD), so the player stands still while you're the cat.")]
        [SerializeField] private Behaviour[] playerControl;

        [Tooltip("Components enabled ONLY while the cat is possessed - the Cat's CharacterMovement etc.")]
        [SerializeField] private Behaviour[] catControl;

        [Tooltip("Key that toggles player <-> cat. None disables the key (FocusCat()/FocusPlayer() " +
                 "still work). C was unused before this (T = 守望者 view, V = first person).")]
        [SerializeField] private Key toggleKey = Key.C;

        // 2026-09-11, user request ("給個按鍵 像t/c一樣給猜猜看攝影機視角，且他必須擁有跟player一樣的機制") -
        // a third possessable character (GuessWhoPlayerParitySetup.cs - a full Player-parity clone,
        // own Humanoid-retargeted rig). Deliberately a SEPARATE dedicated key rather than folded
        // into the C toggle cycle (that's what the user asked for): G jumps straight to/from
        // 猜猜看, C keeps its original strict Player<->Cat meaning. If you're on 猜猜看 and press C,
        // or on Player/Cat and press G a second time from 猜猜看, you land on Player - the same
        // "leaving a special mode returns to a known-good default" pattern the Watcher (T) view
        // already uses below, not a full 3-way ring cycle.
        [SerializeField] private GameObject guessWhoCamera;

        [Tooltip("Components enabled ONLY while 猜猜看 is possessed - mirrors playerControl exactly " +
                 "(CharacterMovement/PlayerCombat/TargetLockController/UltimateAbility/ExecutionAbility/PlayerGuard).")]
        [SerializeField] private Behaviour[] guessWhoControl;

        [SerializeField] private Key guessWhoToggleKey = Key.G;

        // Optional / null-safe, mirrors catHealth below - auto-drop back to Player if 猜猜看 dies
        // while possessed.
        [SerializeField] private Health guessWhoHealth;

        // 2026-09-11 follow-up: 猜猜看 moved to stand in 露營區 (Map_Camp.unity, additive - only
        // loaded while the player is physically at the camp), so guessWhoCamera/guessWhoControl/
        // guessWhoHealth above go null (destroyed, not just unassigned) every time that scene
        // unloads and must be found again once it streams back in - a plain serialized reference
        // captured once wouldn't survive the first unload/reload cycle. Throttled scan (once a
        // second while unresolved) rather than every frame; costs nothing once found.
        [SerializeField] private float guessWhoRelinkIntervalSeconds = 1f;
        private float _nextGuessWhoRelinkScan;

        private void TryRelinkGuessWho()
        {
            // NOT GameObject.Find - GuessWhoCamera starts SetActive(false) (it only turns on once
            // G is pressed) and Find() silently skips inactive objects, so it could never be
            // found this way; that was bug #1 (2026-09-11 user report: "沒辦透過g切換到猜猜看視角").
            // Scanning loaded scenes' root objects directly finds it regardless of active state.
            GameObject gw = null;
            GameObject cam = null;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (root.name == "猜猜看") gw = root;
                    else if (root.name == "GuessWhoCamera") cam = root;
                }
            }
            if (gw == null) return;

            guessWhoCamera = cam;
            guessWhoHealth = gw.GetComponent<Health>();
            guessWhoControl = new Behaviour[]
            {
                gw.GetComponent<CharacterMovement>(),
                gw.GetComponent<PlayerCombat>(),
                gw.GetComponent<TargetLockController>(),
                gw.GetComponent<UltimateAbility>(),
                gw.GetComponent<ExecutionAbility>(),
                gw.GetComponent<PlayerGuard>(),
            };

            // Bug #2 (same report: "我控制player時猜猜看會受到影響") - a freshly streamed-in 猜猜看
            // loads with whatever `enabled` state was serialized on his components, which is TRUE
            // (that's how he was saved - Player's own components are enabled by default). Nothing
            // else ever turns them off for him specifically, so he'd run his own CharacterMovement/
            // PlayerCombat off the same keyboard the instant Map_Camp streams in, in parallel with
            // whoever you're actually possessing. Enforce the CURRENT possession state on him the
            // moment he's linked, instead of trusting his saved enabled flags.
            bool guessWhoPossessedNow = Current == Possessed.GuessWho;
            SetEnabled(guessWhoControl, guessWhoPossessedNow);
            SetActiveSafe(guessWhoCamera, guessWhoPossessedNow);
        }

        // 2026-08-29, user request ("讓 player 守望者/cat 三者可以互相切換視角"). Optional / null-safe.
        // While the Watcher (T) view is active it has taken over whichever camera is live - a C
        // possession swap in that moment would SetActive-swap the camera out from under the
        // director. Ignore C while watching; press T first to come back, then C.
        [SerializeField] private ViewFocusDirector viewDirector;

        // 2026-08-29, user request ("讓貓咪也可以使用車輛 F功能" -> "PLAYER和CAT在駕駛車輛時沒辦法
        // 互相切換視角嗎", GTA-style) - C keeps working while one character is in the car. When you
        // swap TO a character that VehicleEntrySystem has parked in the seat, Apply() below leaves
        // its own camera / control off (VehicleEntrySystem owns the vehicle camera + keeps the
        // parked passenger inert); swap AWAY from the driver and the car just parks itself.
        // Optional / null-safe.
        [SerializeField] private VehicleEntrySystem vehicleEntry;

        [SerializeField] private Possessed startPossessed = Possessed.Player;

        // 2026-08-29, user request ("貓咪死後5秒復活") - if the cat dies while you're possessing it,
        // its GameObject is SetActive(false)'d (Health.ApplyDamage) and you'd be stuck looking
        // through a dead CatCamera at nothing for the 5s until RespawnController brings it back.
        // Auto-drop back to the player the moment the cat dies; press C again after it respawns.
        // Optional / null-safe.
        [SerializeField] private Health catHealth;

        public Possessed Current { get; private set; } = Possessed.Player;

        private bool _applied;

        [Tooltip("Scene 猜猜看 lives in (additive, not loaded by default) - only consulted when " +
                 "startPossessed is GuessWho, to load it before the game can start possessing him.")]
        [SerializeField] private string guessWhoHomeSceneName = "Map_Camp";

        private void Start()
        {
            if (startPossessed == Possessed.GuessWho)
            {
                // 2026-09-11, user request ("我希望現在進入遊戲都從 g視角開始") - 猜猜看 only exists in
                // Map_Camp (additive, not part of the normal single-scene start), so starting
                // possessed as him means loading that scene first. A synchronous Apply() here
                // would find nothing (TryRelinkGuessWho only scans ALREADY loaded scenes) and
                // leave both playerCamera and guessWhoCamera off - no active camera at all.
                StartCoroutine(LoadCampAndPossessGuessWho());
            }
            else
            {
                Apply(startPossessed, force: true);
            }
        }

        private IEnumerator LoadCampAndPossessGuessWho()
        {
            Apply(Possessed.Player, force: true); // a safe, fully-linked default while the camp streams in
            Scene camp = SceneManager.GetSceneByName(guessWhoHomeSceneName);
            if (!camp.IsValid() || !camp.isLoaded)
            {
                AsyncOperation op = SceneManager.LoadSceneAsync(guessWhoHomeSceneName, LoadSceneMode.Additive);
                if (op != null)
                {
                    while (!op.isDone) yield return null;
                }
            }
            yield return null; // let the newly-loaded scene's own Awake/Start run before we link to it

            TryRelinkGuessWho();
            if (guessWhoCamera != null)
            {
                Apply(Possessed.GuessWho, force: true);
            }
            else
            {
                Debug.LogWarning("[CameraPossession] startPossessed=GuessWho but '" + guessWhoHomeSceneName +
                                  "' didn't yield a linkable 猜猜看/GuessWhoCamera - staying on Player.");
            }
        }

        private void OnDisable()
        {
            // Torn down mid-swap - don't leave the player's control permanently disabled.
            if (_applied && Current != Possessed.Player)
            {
                Apply(Possessed.Player, force: true);
            }
        }

        private void Update()
        {
            if (guessWhoCamera == null && Time.unscaledTime >= _nextGuessWhoRelinkScan)
            {
                _nextGuessWhoRelinkScan = Time.unscaledTime + guessWhoRelinkIntervalSeconds;
                TryRelinkGuessWho();
            }

            // Cat/猜猜看 died while possessed -> hand control/view back to the player.
            if (Current == Possessed.Cat && catHealth != null && catHealth.IsDead)
            {
                FocusPlayer();
                return;
            }
            if (Current == Possessed.GuessWho && guessWhoHealth != null && guessWhoHealth.IsDead)
            {
                FocusPlayer();
                return;
            }

            bool watching = viewDirector != null && viewDirector.IsFocusedOnWatcher;

            if (toggleKey != Key.None && Keyboard.current != null && Keyboard.current[toggleKey].wasPressedThisFrame)
            {
                if (watching)
                {
                    // 2026-08-29, user report ("可以從 c 轉回 t 但反過來不行") - C used to be a no-op in
                    // the 守望者 view, so pressing C to "get back into my character" did nothing.
                    // Now it leaves the Watcher view the same as T does, back to whoever you were
                    // possessing. A second C then swaps player <-> cat as usual. (Not a
                    // leave-and-swap in one press: ViewFocusDirector's return path restores the
                    // pre-Watcher control snapshot in LateUpdate, which would stomp a same-frame
                    // possession swap done here in Update.)
                    Debug.Log("[CameraPossession] " + toggleKey + " -> leaving 守望者 view (back to " + Current + ")");
                    viewDirector.FocusPlayer();
                }
                else
                {
                    // 2026-09-11: C keeps its original strict Player<->Cat meaning even with a
                    // third character in play - if you're on 猜猜看, C is "get back to my usual
                    // character" (Player), not a 3-way cycle.
                    Possessed next = Current == Possessed.GuessWho ? Possessed.Player : Other(Current);
                    // 2026-08-29, user report ("C按鍵並沒有對應在貓身上") - a visible Console line every
                    // time the key registers, so it's obvious whether the press is being seen at all
                    // (vs a focus / key-conflict problem) and which side you're now controlling.
                    Debug.Log("[CameraPossession] " + toggleKey + " pressed -> switching to " + next);
                    Apply(next);
                }
                return;
            }

            if (guessWhoToggleKey != Key.None && Keyboard.current != null && Keyboard.current[guessWhoToggleKey].wasPressedThisFrame)
            {
                if (watching)
                {
                    Debug.Log("[CameraPossession] " + guessWhoToggleKey + " -> leaving 守望者 view (back to " + Current + ")");
                    viewDirector.FocusPlayer();
                }
                else
                {
                    Possessed next = Current == Possessed.GuessWho ? Possessed.Player : Possessed.GuessWho;
                    Debug.Log("[CameraPossession] " + guessWhoToggleKey + " pressed -> switching to " + next);
                    Apply(next);
                }
            }
        }

        // ---- public API (key binding + cutscenes / scripted events / tests) ----

        public void Toggle() => Apply(Current == Possessed.GuessWho ? Possessed.Player : Other(Current));
        public void FocusCat() => Apply(Possessed.Cat);
        public void FocusPlayer() => Apply(Possessed.Player);
        public void FocusGuessWho() => Apply(Possessed.GuessWho);

        // Pure, so the flip is directly EditMode-testable (same convention as
        // ViewFocusDirector.BlendPose / ThirdPersonCameraController.ComputeCameraPosition).
        // Only meaningful for the Player/Cat pair - GuessWho is handled separately (see Toggle()).
        public static Possessed Other(Possessed p) => p == Possessed.Player ? Possessed.Cat : Possessed.Player;

        // ---- internals ----

        private void Apply(Possessed who, bool force = false)
        {
            if (_applied && !force && who == Current)
            {
                return;
            }
            Current = who;
            _applied = true;
            bool cat = who == Possessed.Cat;
            bool guessWho = who == Possessed.GuessWho;
            bool player = who == Possessed.Player;

            // Vehicle awareness (2026-08-29): the DRIVER's view is the vehicle camera
            // (VehicleEntrySystem owns it), so don't turn that character's own third-person camera
            // on. A PASSENGER keeps their own camera (you see them riding on the flatbed). Anyone
            // seated - driver or passenger - has their control consumers held off by
            // VehicleEntrySystem; don't re-enable them here. 猜猜看 can't enter vehicles yet
            // (VehicleEntrySystem only knows Player/Cat), so it's never "seated".
            bool whoIsDriver = !guessWho && vehicleEntry != null && vehicleEntry.DriverOccupant ==
                (cat ? VehicleEntrySystem.Occupant.Cat : VehicleEntrySystem.Occupant.Player);
            bool playerSeated = vehicleEntry != null && vehicleEntry.PlayerSeat != VehicleEntrySystem.Seat.None;
            bool catSeated = vehicleEntry != null && vehicleEntry.CatSeat != VehicleEntrySystem.Seat.None;

            SetActiveSafe(playerCamera, player && !whoIsDriver);
            SetActiveSafe(catCamera, cat && !whoIsDriver);
            SetActiveSafe(guessWhoCamera, guessWho);

            SetEnabled(playerControl, player && !playerSeated);
            SetEnabled(catControl, cat && !catSeated);
            SetEnabled(guessWhoControl, guessWho);
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active)
            {
                go.SetActive(active);
            }
        }

        private static void SetEnabled(Behaviour[] set, bool enabled)
        {
            if (set == null)
            {
                return;
            }
            foreach (Behaviour b in set)
            {
                if (b != null && b.enabled != enabled)
                {
                    b.enabled = enabled;
                }
            }
        }
    }
}
