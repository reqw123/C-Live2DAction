using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Live2DAction.Characters;
using Live2DAction.CameraSystem;
using Live2DAction.Combat;

namespace Live2DAction.Vehicles
{
    // 2026-09-12, user request ("摩托超載...按f能夠把它讓車輛駕駛，但是要隱藏顯示猜猜看，因為摩托超載
    // 本身就是人+車，攝影機視角直接從後座往前拍就行，像行車紀錄器，並且像car一樣可以前進後退 加速等等") -
    // a single-rider vehicle whose OWN mesh already bakes in a rider (helmet, jacket, hunched-over
    // pose, junk piled on the back), unlike the buggy where Player/Cat sit visibly IN a car body.
    // Driving it therefore means hiding the ACTUAL driver's whole model (there's no separate "seat"
    // to sit in - the mesh IS the seated rider) rather than VehicleEntrySystem's partial leg-collapse,
    // and a rigidly-mounted "dashcam" camera (a plain child transform, no chase/spring logic) instead
    // of VehicleCameraController's chase cam. Physics reuses the same VehicleController the buggy
    // uses (spec: "像car一樣可以前進後退 加速等等") - narrow 4-WheelCollider pairs standing in for two
    // wheels, same approach the buggy already validated (see VehicleController's own header comment
    // for why this project doesn't attempt real motorcycle lean/balance).
    //
    // Deliberately a separate, single-seat script rather than extending VehicleEntrySystem itself -
    // that class's driver/passenger seats, per-occupant partial-hide (leg collapse + renderer list)
    // and GTA-style swap-while-parked handling are all tailored to the buggy's 2-seat, visible-rider
    // design and don't apply here; retrofitting a third occupant kind onto it risked regressing the
    // already-tuned, working buggy for a mechanic that doesn't share its shape.
    //
    // Whoever's nearest/currently possessed when F is pressed can drive it - Player/Cat/猜猜看 all
    // resolve the same way (by name, via `possession.Current`), not 猜猜看-only.
    public class IntegratedRiderVehicleEntry : MonoBehaviour
    {
        [Header("Vehicle")]
        [SerializeField] private VehicleController vehicleController;
        [Tooltip("Rigid child transform with the dashcam Camera - moves exactly with the bike, no smoothing.")]
        [SerializeField] private GameObject vehicleCamera;
        [SerializeField] private float enterRange = 3f;
        [Tooltip("Where the rider is placed on exit, in the vehicle's local space.")]
        [SerializeField] private Vector3 exitLocalOffset = new Vector3(-1.2f, 0.2f, 0f);

        // 2026-09-12, user request for CampGardenHauler specifically ("我希望對小牛按f是這樣的畫面" +
        // a screenshot of 猜猜看 standing fully visible on the seat, seen from behind/above like an
        // ordinary third-person shot) - unlike CampMotorcycle (whose own mesh already bakes in a
        // rider, so hiding the real driver entirely was correct there), this vehicle's seat is an
        // empty platform the real character visibly stands on. `hideRider=false` keeps them visible
        // and places them at `riderStandLocalOffset` (the vehicle's local space) instead of hiding
        // them at the origin; `vehicleCamera` should then be an ordinary over-the-shoulder third-
        // person shot aimed at that stand point, not a dashcam. Defaults preserve the motorcycle's
        // existing hide-everything behavior.
        [SerializeField] private bool hideRider = true;
        [SerializeField] private Vector3 riderStandLocalOffset = Vector3.zero;

        // 2026-09-13, user report (CampGardenHauler specifically: "後輪是翹起的，沒有著地...你可改成
        // 動畫進行到猜猜看駕駛小牛時，再讓車身下來到平地，然後確保可以移動控制") - repeated suspension/
        // center-of-mass tuning attempts (torque-vs-wheel-radius, center-of-mass offset, spring/damper
        // ratio - see the same session's CHANGELOG entries) couldn't be verified without a live Play
        // test and evidently still left it resting tilted with an axle off the ground. Rather than
        // keep guessing at suspension numbers blind, this takes the user's own suggested approach: at
        // the moment driving actually begins, force the body level and grounded (raycast under each
        // wheel) and freeze pitch/roll for the ride's duration - a giant 20-ton prototype vehicle
        // doesn't need realistic suspension tipping, it needs to reliably be flat and drivable the
        // instant someone gets on. Off by default (Vector3.zero-equivalent = false) so the motorcycle,
        // which SHOULD lean/tip naturally over its jump ramp, is untouched.
        [Tooltip("On mount, snap the body level (zero pitch/roll, keep yaw) and raycast-ground all 4 wheels to the terrain beneath them, then freeze pitch/roll rotation for as long as this vehicle is occupied. For giant/heavy vehicles whose suspension can't be trusted to settle flat on its own (e.g. CampGardenHauler). Leave off for vehicles that should tip/lean naturally (e.g. the motorcycle's jump ramp).")]
        [SerializeField] private bool groundAndLevelOnMount = false;
        private RigidbodyConstraints _constraintsBeforeMount;

        // 2026-09-13, user request ("動畫進行小牛車接替駕駛時先做一個小牛搬運車的近距離外型360度環繞特寫
        // 然後再回復到小牛駕駛視角進行移動操作") - on mount, play a scripted close orbit around the
        // vehicle's exterior BEFORE handing control to the player, instead of cutting straight to the
        // driving camera. Lives here (not in the flyover cutscene) so it fires for EITHER path into
        // driving this vehicle - walking up and pressing F, or arriving via
        // MotorcycleFlyOverHaulerCutscene's transfer - since a vehicle this size deserves the same
        // reveal moment regardless of how you got in. vehicleController and the real driving camera
        // both stay off for the whole orbit so there's no control/view flash before the reveal
        // finishes - see OrbitRevealThenHandControl().
        [Header("Mount reveal (optional close orbit before handing over control)")]
        [Tooltip("Play a close 360-degree orbit around the vehicle's exterior on mount, before switching to the driving camera and enabling control. For a vehicle whose scale/reveal deserves a moment (e.g. CampGardenHauler). Leave off for ordinary vehicles that should just hand over control immediately.")]
        [SerializeField] private bool orbitRevealOnMount = false;
        [Tooltip("Dedicated camera GameObject for the reveal orbit (separate from vehicleCamera) - a plain Camera, no follow script, driven entirely by OrbitRevealThenHandControl().")]
        [SerializeField] private GameObject orbitCamera;
        [SerializeField] private float orbitDurationSeconds = 4f;
        [SerializeField] private float orbitDistance = 35f;
        [SerializeField] private float orbitHeight = 12f;
        [Tooltip("The point the orbit circles around and looks at, in this vehicle's local space - aim for the body's actual visual center, not the root pivot (which can sit far from it on an irregular mesh like this one).")]
        [SerializeField] private Vector3 orbitCenterLocalOffset = Vector3.zero;
        private bool _orbiting;

        // 2026-09-12 - NOT [SerializeField]: CameraPossessionSwitcher and the three on-foot cameras
        // all live in the persistent GreyboxTest scene, a DIFFERENT scene than this vehicle
        // (Map_Camp). Unity does not persist cross-scene object references to the scene file at all
        // - wiring these via SerializedObject in the Editor "worked" only until the next scene
        // save/reload silently nulled them back out (confirmed directly: wired, saved, reloaded from
        // disk, all four were null again). CameraPossessionSwitcher's own guessWhoCamera/
        // guessWhoHealth fields hit this identical wall for the identical reason - see its own
        // "2026-09-11 follow-up" comment - and solve it the same way this does: resolve by name at
        // runtime instead of trusting a serialized reference to survive.
        private CameraPossessionSwitcher _possession;
        private GameObject _playerCamera, _catCamera, _guessWhoCamera;

        private Transform _rider;
        private Scene _riderOriginalScene;
        private Renderer[] _riderRenderers;
        private CharacterMovement _riderMovement;
        private PlayerCombat _riderCombat;
        private CharacterController _riderCC;

        public bool IsOccupied => _rider != null;
        public Transform CurrentRider => _rider;

        // 2026-09-12 - tried Physics.IgnoreCollision on all 6 camp decor props here first (user:
        // "不管地面情況都能騎行上去 無阻通行"), but the user rejected it once they saw it in motion:
        // riding through a 10-48m tent/tree/hauler with zero collision reads as clipping, not
        // driving. Real "ride up and bump over it" (their own comparison: a low drain cover) is only
        // physically coherent for something roughly wheel-height, so this list is now empty by
        // default - the 5 giant landmarks (帳篷/遮雨棚/柵欄樹/黃樹/小牛搬運車) went back to being
        // ordinary solid obstacles you steer around, and CampLawnMower (the one genuinely
        // curb-scale prop) instead got its OWN collider's height cut down to the wheels'
        // suspension-absorbable range (see CampLawnMower_Collision in Map_Camp.unity) so it plays
        // as a real bump, not a wall or a no-clip hole. Kept as a configurable list (rather than
        // deleted outright) in case a future prop genuinely needs the no-clip treatment.
        [SerializeField] private string[] decorativeObstacleNames = new string[0];

        private void Reset() => vehicleController = GetComponent<VehicleController>();

        private void Awake()
        {
            ResolvePossessionRefs();
            IgnoreDecorativeObstacleCollisions();

            // 2026-09-12, user report ("摩托超載會自己移動" + "猜猜看f駕駛時也無法動") - a MonoBehaviour
            // added via script defaults to enabled=true, and nothing in Mount()/Dismount() ever
            // touched that INITIAL state (only toggled it on/off around an actual F press). So from
            // the moment the scene loaded, VehicleController.Update()/FixedUpdate() were already
            // running and reading raw Keyboard.current WASD every frame - the exact same keys the
            // rider's own on-foot movement uses - and quietly driving the parked bike on its own
            // any time the user pressed WASD for themselves, nobody having "mounted" it at all. By
            // the time someone actually pressed F, the bike could already be off its spawn pose /
            // wedged against something from that uncommanded drift, which reads as "can't move even
            // while actually driving". Force it off at startup; Mount() is the only thing allowed to
            // turn it on.
            if (vehicleController != null) vehicleController.enabled = false;
        }

        private void IgnoreDecorativeObstacleCollisions()
        {
            var vehicleColliders = GetComponentsInChildren<Collider>(true);
            if (vehicleColliders.Length == 0 || decorativeObstacleNames == null) return;

            foreach (var obstacleName in decorativeObstacleNames)
            {
                var obstacle = GameObject.Find(obstacleName);
                if (obstacle == null) continue;
                var obstacleCollider = obstacle.GetComponent<Collider>();
                if (obstacleCollider == null) continue;
                foreach (var vc in vehicleColliders)
                    Physics.IgnoreCollision(vc, obstacleCollider, true);
            }
        }

        // Retried lazily (not just once in Awake) in case this vehicle's scene streams in before
        // GreyboxTest's own objects have finished their Awake/Start - mirrors
        // CameraPossessionSwitcher.TryRelinkGuessWho's own throttled-rescan reasoning.
        private void ResolvePossessionRefs()
        {
            if (_possession == null) _possession = FindFirstObjectByType<CameraPossessionSwitcher>();
            if (_playerCamera != null && _catCamera != null && _guessWhoCamera != null) return;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                foreach (GameObject root in SceneManager.GetSceneAt(i).GetRootGameObjects())
                {
                    if (root.name == "Main Camera") _playerCamera = root;
                    else if (root.name == "CatCamera") _catCamera = root;
                    else if (root.name == "GuessWhoCamera") _guessWhoCamera = root;
                }
            }
        }

        private Transform CurrentPossessedTransform()
        {
            if (_possession == null) return null;
            string name = _possession.Current == CameraPossessionSwitcher.Possessed.Cat ? "Cat"
                        : _possession.Current == CameraPossessionSwitcher.Possessed.GuessWho ? "猜猜看"
                        : "Player";
            GameObject go = GameObject.Find(name);
            return go != null ? go.transform : null;
        }

        private void Update()
        {
            if (_possession == null || _playerCamera == null || _catCamera == null || _guessWhoCamera == null)
                ResolvePossessionRefs();

            if (_orbiting) return; // reveal orbit is uninterruptible - F is a no-op until it hands over control

            Keyboard kb = Keyboard.current;
            if (kb == null || !kb.fKey.wasPressedThisFrame) return;

            if (_rider != null) { Dismount(); return; }

            Transform candidate = CurrentPossessedTransform();
            if (candidate == null) return;
            if (!IsWithinEnterRange(candidate.position)) return;
            Mount(candidate);
        }

        // 2026-09-13, user report ("小牛由於體積大，需要讓猜猜看貼近車身任何一地方就可以觸發F") - the
        // plain Vector3.Distance-to-root check above worked fine for the motorcycle/buggy (root sits
        // near ground, close to the visible body), but CampGardenHauler's root pivot floats ~13.5m
        // above the ground (the mesh's own origin isn't at the wheel base - see the camera-offset fix
        // in the same session), so a fixed-radius sphere around the root barely reaches the ground at
        // all, let alone the 48m-wide flatbed sticking out on both sides. Measuring against the
        // closest point on the vehicle's actual colliders (body BoxColliders + WheelColliders,
        // approximated as spheres since WheelCollider isn't a convex shape ClosestPoint supports)
        // instead of the root transform makes "near any part of the body" work regardless of how far
        // the root pivot itself is from the visible mesh - and generalizes cleanly to the smaller
        // motorcycle too (CapsuleCollider body), which only had this bug latently (root already close
        // to the body there, so nobody noticed).
        private bool IsWithinEnterRange(Vector3 point)
        {
            float bestSqrDistance = float.MaxValue;
            foreach (Collider col in GetComponentsInChildren<Collider>(true))
            {
                if (col == null || !col.enabled) continue;
                float sqrDistance;
                if (col is WheelCollider wheel)
                {
                    float surfaceDistance = Vector3.Distance(point, wheel.transform.position) - wheel.radius;
                    sqrDistance = surfaceDistance * surfaceDistance;
                }
                else
                {
                    Vector3 closest = col.ClosestPoint(point);
                    sqrDistance = (closest - point).sqrMagnitude;
                }
                if (sqrDistance < bestSqrDistance) bestSqrDistance = sqrDistance;
            }
            if (bestSqrDistance == float.MaxValue) bestSqrDistance = (point - transform.position).sqrMagnitude;
            return bestSqrDistance <= enterRange * enterRange;
        }

        private void Mount(Transform rider)
        {
            _rider = rider;
            _riderOriginalScene = rider.gameObject.scene;

            _riderCC = rider.GetComponent<CharacterController>();
            if (_riderCC != null) _riderCC.enabled = false;

            _riderMovement = rider.GetComponentInChildren<CharacterMovement>(true);
            if (_riderMovement != null) _riderMovement.enabled = false;
            _riderCombat = rider.GetComponentInChildren<PlayerCombat>(true);
            if (_riderCombat != null) _riderCombat.enabled = false;

            // 2026-09-13 - always set the EXPLICIT state (not just disable-if-hidden) so a rider
            // handed over mid-transfer from another vehicle (TransferRiderTo, e.g. the motorcycle's
            // flyover stunt landing them on this hauler) gets correctly re-shown if the SOURCE
            // vehicle had hidden them (hideRider=true, motorcycle) but this one wants them visible
            // (hideRider=false, hauler) - the old "only ever disable" version left them invisible
            // forever in that case, since nothing ever flipped them back on.
            _riderRenderers = rider.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in _riderRenderers) if (r != null) r.enabled = !hideRider;

            rider.SetParent(transform, false);
            rider.localPosition = hideRider ? Vector3.zero : riderStandLocalOffset;
            rider.localRotation = Quaternion.identity;

            // Ground/level BEFORE either camera path, so the reveal orbit (if any) shows the vehicle
            // already in its final resting pose, not mid-settle.
            if (groundAndLevelOnMount) GroundAndLevel();

            SetActiveSafe(_playerCamera, false);
            SetActiveSafe(_catCamera, false);
            SetActiveSafe(_guessWhoCamera, false);

            if (orbitRevealOnMount && orbitCamera != null)
            {
                // Driving camera and control both stay off for the whole orbit - LateUpdate's own
                // "force vehicleCamera on every frame while ridden" is suppressed via _orbiting so it
                // can't fight this mid-shot (see LateUpdate()).
                SetActiveSafe(vehicleCamera, false);
                if (vehicleController != null) vehicleController.enabled = false;
                _orbiting = true;
                StartCoroutine(OrbitRevealThenHandControl());
            }
            else
            {
                SetActiveSafe(vehicleCamera, true);
                if (vehicleController != null) vehicleController.enabled = true;
            }
        }

        // See orbitRevealOnMount's field comment. A fixed-duration close orbit around the vehicle's
        // exterior, then hands off to the normal driving camera + enables control - the same
        // hand-over the non-orbit Mount() path does immediately.
        private IEnumerator OrbitRevealThenHandControl()
        {
            orbitCamera.SetActive(true);
            Vector3 center = transform.TransformPoint(orbitCenterLocalOffset);
            float duration = Mathf.Max(0.1f, orbitDurationSeconds);
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                float angle = k * 360f;
                Vector3 offset = Quaternion.Euler(0f, angle, 0f) * (Vector3.back * orbitDistance) + Vector3.up * orbitHeight;
                Vector3 camPos = center + offset;
                orbitCamera.transform.SetPositionAndRotation(camPos,
                    Quaternion.LookRotation((center - camPos).normalized, Vector3.up));
                yield return null;
            }

            orbitCamera.SetActive(false);
            _orbiting = false;
            SetActiveSafe(vehicleCamera, true);
            if (vehicleController != null) vehicleController.enabled = true;
        }

        // See groundAndLevelOnMount's field comment. Levels pitch/roll (keeps yaw - don't spin the
        // vehicle to face an arbitrary direction, just stop it pointing nose-up/down or leaning), then
        // raycasts straight down from each WheelCollider's position to find the terrain underneath and
        // shifts the whole body vertically so the wheels' contact point sits exactly on it, instead of
        // trusting wherever suspension/gravity had already settled it.
        private void GroundAndLevel()
        {
            var rvb = GetComponent<Rigidbody>();
            WheelCollider[] wheels = GetComponentsInChildren<WheelCollider>(true);
            if (wheels.Length == 0) return;

            Vector3 euler = transform.eulerAngles;
            transform.eulerAngles = new Vector3(0f, euler.y, 0f);

            // 2026-09-13, user report ("下來後小牛車不見了") - the first version's raycast could hit
            // ANY collider in its path, triggers included (Physics.Raycast hits triggers by default)
            // and even this vehicle's own huge body BoxColliders if the ray's start/geometry lined up
            // wrong - either one returns a ground height nowhere near the real floor, and `delta`
            // (uncapped) would fling the 20-ton body to that wrong height in one frame, easily far
            // enough off-map to read as "disappeared". Now: ignore triggers (MotorcycleJumpZone etc.
            // sit right around this area), skip any hit belonging to this vehicle's own hierarchy, and
            // clamp the final correction so a bad reading can only nudge the car, never launch it.
            float sumGroundY = 0f;
            float sumWheelBottomY = 0f;
            int hits = 0;
            foreach (WheelCollider w in wheels)
            {
                Vector3 wheelPos = w.transform.position;
                RaycastHit[] hitsAlongRay = Physics.RaycastAll(wheelPos + Vector3.up * 10f, Vector3.down, 100f,
                    ~0, QueryTriggerInteraction.Ignore);
                float bestHitY = float.NaN;
                float bestHitDistance = float.MaxValue;
                foreach (RaycastHit candidate in hitsAlongRay)
                {
                    if (candidate.collider.transform.IsChildOf(transform)) continue; // skip this vehicle's own colliders
                    if (candidate.distance < bestHitDistance)
                    {
                        bestHitDistance = candidate.distance;
                        bestHitY = candidate.point.y;
                    }
                }
                if (!float.IsNaN(bestHitY))
                {
                    sumGroundY += bestHitY;
                    sumWheelBottomY += wheelPos.y - w.radius;
                    hits++;
                }
            }
            if (hits > 0)
            {
                float delta = (sumGroundY / hits) - (sumWheelBottomY / hits);
                const float maxCorrection = 5f; // a real "settle onto the ground" nudge, never a teleport
                delta = Mathf.Clamp(delta, -maxCorrection, maxCorrection);
                transform.position += Vector3.up * delta;
            }

            if (rvb != null)
            {
                rvb.linearVelocity = Vector3.zero;
                rvb.angularVelocity = Vector3.zero;
                _constraintsBeforeMount = rvb.constraints;
                rvb.constraints |= RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        // 2026-09-13, user request ("摩托翻越...改成翻越到小牛搬運車的駕駛座，接觸到小牛駕駛座後自動變成
        // 小牛駕駛狀態") - a scripted stunt (MotorcycleFlyOverHaulerCutscene) hands its rider directly
        // to ANOTHER vehicle instead of returning them to normal on-foot control. This is deliberately
        // NOT "Dismount() then Mount()" - a real Dismount() re-enables the rider's on-foot camera/
        // control and teleports them to exitLocalOffset, all of which a vehicle-to-vehicle transfer
        // must skip entirely (they're never on foot in between). The SOURCE just lets go (stops
        // driving/rendering its own view) and the DESTINATION's own Mount() takes full ownership of
        // the rider's hidden/disabled state from there - including correctly re-showing them if the
        // source had hidden them but this vehicle doesn't want that (see the hideRider fix above).
        public void TransferRiderTo(IntegratedRiderVehicleEntry destination)
        {
            if (_rider == null || destination == null) return;
            Transform rider = _rider;
            _rider = null;
            if (vehicleController != null) vehicleController.enabled = false;
            SetActiveSafe(vehicleCamera, false);
            if (groundAndLevelOnMount)
            {
                var rvb = GetComponent<Rigidbody>();
                if (rvb != null) rvb.constraints = _constraintsBeforeMount;
            }
            destination.MountExternal(rider);
        }

        // Entry point for a scripted stunt to force-mount a rider, bypassing the normal F-key +
        // enterRange gate (the rider is already mid-transfer, not walking up on foot).
        public void MountExternal(Transform rider) => Mount(rider);

        private void Dismount()
        {
            Transform rider = _rider;
            _rider = null;

            if (vehicleController != null) vehicleController.enabled = false; // OnDisable = parking brake
            SetActiveSafe(vehicleCamera, false);

            if (groundAndLevelOnMount)
            {
                var rvb = GetComponent<Rigidbody>();
                if (rvb != null) rvb.constraints = _constraintsBeforeMount;
            }

            if (rider != null)
            {
                rider.SetParent(null, false);
                // SetParent(null) keeps the GameObject in ITS CURRENT scene, not its original one -
                // without this it would silently end up owned by this vehicle's scene (Map_Camp) and
                // get destroyed the next time that scene unloads (the exact bug class already fixed
                // once for 猜猜看 living inside Map_Camp - see CameraPossessionSwitcher's own history).
                if (rider.gameObject.scene != _riderOriginalScene)
                    SceneManager.MoveGameObjectToScene(rider.gameObject, _riderOriginalScene);

                rider.position = transform.TransformPoint(exitLocalOffset);
                rider.rotation = Quaternion.identity;
            }

            foreach (Renderer r in _riderRenderers) if (r != null) r.enabled = true;
            _riderRenderers = null;

            if (_riderMovement != null) _riderMovement.enabled = true;
            _riderMovement = null;
            if (_riderCombat != null) _riderCombat.enabled = true;
            _riderCombat = null;

            if (_riderCC != null) _riderCC.enabled = true;
            _riderCC = null;

            // Back to whichever on-foot camera the possessed character actually uses.
            if (_possession != null)
            {
                GameObject back = _possession.Current == CameraPossessionSwitcher.Possessed.Cat ? _catCamera
                                 : _possession.Current == CameraPossessionSwitcher.Possessed.GuessWho ? _guessWhoCamera
                                 : _playerCamera;
                SetActiveSafe(back, true);
            }
        }

        // Re-assert every frame while ridden - a stray G/C press mid-ride would otherwise flip
        // CameraPossessionSwitcher.Current and re-enable an on-foot camera alongside the dashcam.
        private void LateUpdate()
        {
            if (_rider == null) return;
            SetActiveSafe(_playerCamera, false);
            SetActiveSafe(_catCamera, false);
            SetActiveSafe(_guessWhoCamera, false);
            // Skip forcing vehicleCamera on while the reveal orbit is running - it deliberately keeps
            // that camera off (and orbitCamera on instead) for the whole shot; without this guard,
            // this same re-assertion (which exists for the ordinary "stray G/C press mid-ride" case)
            // would stomp the orbit every single frame.
            if (!_orbiting) SetActiveSafe(vehicleCamera, true);
        }

        private void OnDisable()
        {
            if (_rider != null) Dismount();
        }

        private static void SetActiveSafe(GameObject go, bool active)
        {
            if (go != null && go.activeSelf != active) go.SetActive(active);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = IsOccupied ? Color.red : Color.green;
            Gizmos.DrawWireSphere(transform.position, enterRange);
        }
    }
}
