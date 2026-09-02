using UnityEngine;
using UnityEngine.InputSystem;

namespace Live2DAction.Vehicles
{
    // 2026-08-26, explicit user request (vehicle spec 十四) - "若已有Cinemachine,優先使用" - this
    // project has no active Cinemachine usage anywhere (com.unity.cinemachine only exists as a
    // Package Cache dependency, never actually instantiated/referenced by any project script -
    // confirmed by search before writing this), and the player's own ThirdPersonCameraController
    // is a free-look, character-combat-specific camera (lock-on, aiming, first-person swap) that
    // doesn't fit a vehicle. Simple, purpose-built follow camera instead, per the spec's own
    // fallback instruction.
    public class VehicleCameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 followOffset = new Vector3(0f, 3.5f, -7f);
        [SerializeField] private Vector3 lookAtOffset = new Vector3(0f, 1f, 0f);
        [SerializeField] private float positionSmoothTime = 0.25f;
        [SerializeField] private float lookAtSmoothTime = 0.15f;
        [SerializeField] private float thirdPersonFieldOfView = 60f;

        // 2026-08-26, real playtested bug ("進入車體時視角錯誤") - root cause: followOffset is a
        // fixed -7 units behind the car with no awareness of the world around it. Parked close to
        // a boundary wall (as explicitly requested - "本地的右下角圍牆下"), the naive offset placed
        // the camera OUTSIDE the wall entirely, rendering its back face up close instead of the
        // car. Same fix family as ThirdPersonCameraController's own FindObstructionDistance/
        // ClampDistanceForObstruction (see that file's 2026-08-23 history) - SphereCast from the
        // look-at point toward the desired camera position and pull the camera in if something
        // solid is in the way, excluding the car's own body/wheel colliders (target.root) so the
        // vehicle's own mesh never counts as "in the way" of following itself. Chase mode only -
        // first-person mode is rigidly fixed inside the cabin and never needs this.
        [SerializeField] private bool enableCameraCollision = true;
        [SerializeField] private float cameraCollisionRadius = 0.2f;
        [SerializeField] private float cameraCollisionSkin = 0.15f;
        [SerializeField] private float minCollisionDistance = 1.5f;

        // 2026-08-26, real playtested bug ("原本的按V切換視角功能不見了") - the Player's own V-key
        // first-person toggle lives on ThirdPersonCameraController, which sits on the now-
        // deactivated Main Camera GameObject while driving (see VehicleEntrySystem.EnterVehicle) -
        // so V simply had no listener left while in the car. Same key, same "V toggles first
        // person" concept, reimplemented here as the vehicle's own cockpit view instead of
        // forwarding to the player's controller (that class's eye offset/aim/weapon-hiding
        // machinery is all character-specific and doesn't apply to a car). Read directly from
        // Keyboard.current, matching every other input read in this vehicle subsystem (see
        // VehicleController.ReadInput's own comment on why) - only needs to run while this
        // GameObject is active, which VehicleEntrySystem already guarantees is exactly "while
        // driving", so no extra IsDriving check is needed here.
        [Header("First-person driver view (V key, spec: 賽車遊戲第一人稱駕駛)")]
        [Tooltip("Driver eye position, LOCAL to the car - starts near DriverSeatAnchor's own local position plus head height; hand-tune in Play Mode against the actual cabin mesh.")]
        [SerializeField] private Vector3 firstPersonLocalOffset = new Vector3(0f, 1.15f, 0.15f);
        [SerializeField] private float firstPersonFieldOfView = 82f;
        // Racing-game "sense of speed" FOV kick - widens further the faster the car is going.
        // Cockpit view only; a chase cam doesn't need it, the world already visibly rushes past.
        [SerializeField] private float speedFovBoost = 12f;
        [SerializeField] private float speedFovReferenceKmh = 120f;
        [SerializeField] private float fovSmoothSpeedDegrees = 40f;

        private Vector3 _positionVelocity;
        private Vector3 _currentLookAt;
        private bool _initialized;
        private bool _firstPerson;

        // 2026-08-30, user report ("貓咪主駕駛且 V第一人稱時 會看到貓咪的臉") - the driver model is
        // visible now (追加57), so cockpit view looks straight at it. VehicleEntrySystem reads this
        // to hide the driver while first-person is active.
        public bool IsFirstPerson => _firstPerson;
        private Camera _camera;
        private Rigidbody _targetRigidbody;

        private void Awake()
        {
            _camera = GetComponent<Camera>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.vKey.wasPressedThisFrame)
            {
                _firstPerson = !_firstPerson;
                // Force a hard cut on the next LateUpdate instead of smoothing across the mode
                // switch - sliding from a chase-cam position into the cockpit (or back) would read
                // as the camera flying across the map for one frame, not a cut.
                _initialized = false;
            }
        }

        private void LateUpdate()
        {
            if (target == null) return;

            bool isCut = !_initialized;

            if (_firstPerson) UpdateFirstPerson();
            else UpdateChase();

            UpdateFieldOfView(isCut);
        }

        // Rigid, 1:1 attachment to the car's own transform - deliberately NOT smoothed. This is the
        // actual "racing game first person" feel the request asks for: you're bolted into the seat,
        // so the camera should read every bump and turn instantly, the same way a chase cam
        // shouldn't. TransformPoint/rotation computed fresh each LateUpdate (not literal parenting)
        // for the same FixedUpdate/Update timestep-jitter reason noted in UpdateChase below.
        private void UpdateFirstPerson()
        {
            transform.position = target.TransformPoint(firstPersonLocalOffset);
            transform.rotation = target.rotation;
            _initialized = true;
        }

        private void UpdateChase()
        {
            Vector3 rawDesiredPosition = target.TransformPoint(followOffset);
            Vector3 desiredLookAt = target.TransformPoint(lookAtOffset);
            Vector3 desiredPosition = ClampForObstruction(desiredLookAt, rawDesiredPosition);

            if (!_initialized)
            {
                // Snap on the very first frame (or right after a mode switch) - smoothing from the
                // previous pose would otherwise show the camera sweeping across the map for a frame.
                transform.position = desiredPosition;
                _currentLookAt = desiredLookAt;
                _initialized = true;
            }
            else
            {
                // 2026-08-26 - "車輛不應成為車體Rigidbody子物件而產生劇烈抖動": this camera is a
                // plain scene object (not parented to the vehicle), smoothed independently every
                // LateUpdate rather than rigidly following the Rigidbody's own FixedUpdate-stepped
                // transform - avoids the jitter a child-of-Rigidbody camera gets from FixedUpdate/
                // Update timestep mismatch.
                transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref _positionVelocity, positionSmoothTime);
                _currentLookAt = Vector3.Lerp(_currentLookAt, desiredLookAt, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(0.001f, lookAtSmoothTime)));
            }

            transform.rotation = Quaternion.LookRotation((_currentLookAt - transform.position).normalized, Vector3.up);
        }

        private void UpdateFieldOfView(bool isCut)
        {
            if (_camera == null) return;

            if (_targetRigidbody == null) _targetRigidbody = target.GetComponent<Rigidbody>();
            float speedKmh = _targetRigidbody != null ? _targetRigidbody.linearVelocity.magnitude * 3.6f : 0f;

            float targetFov = thirdPersonFieldOfView;
            if (_firstPerson)
            {
                float speedFraction = Mathf.Clamp01(speedKmh / Mathf.Max(1f, speedFovReferenceKmh));
                targetFov = firstPersonFieldOfView + speedFovBoost * speedFraction;
            }

            _camera.fieldOfView = isCut
                ? targetFov
                : Mathf.MoveTowards(_camera.fieldOfView, targetFov, fovSmoothSpeedDegrees * Time.deltaTime);
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _targetRigidbody = null;
            _initialized = false;
        }

        // Called by VehicleEntrySystem on every entry so the car is never entered already sitting
        // in whichever mode the last drive happened to end in - always starts in chase view, same
        // as the Player's own ThirdPersonCameraController never remembers first-person across
        // sessions either.
        public void ResetView()
        {
            _firstPerson = false;
            _initialized = false;
        }

        // Perf fix (2026-08-27, playtest report - "play mode模式下遊戲會卡頓") - ClampForObstruction
        // below runs every LateUpdate while driving and used to call Physics.SphereCastAll,
        // allocating a fresh RaycastHit[] every frame. Reused buffer + SphereCastNonAlloc instead -
        // same query (mask/QueryTriggerInteraction unchanged).
        private readonly RaycastHit[] _obstructionHitsBuffer = new RaycastHit[16];

        private Vector3 ClampForObstruction(Vector3 lookAtPoint, Vector3 desiredPosition)
        {
            if (!enableCameraCollision) return desiredPosition;

            Vector3 toCamera = desiredPosition - lookAtPoint;
            float desiredDistance = toCamera.magnitude;
            if (desiredDistance <= 0.0001f) return desiredPosition;

            Vector3 direction = toCamera / desiredDistance;
            int hitCount = Physics.SphereCastNonAlloc(lookAtPoint, cameraCollisionRadius, direction, _obstructionHitsBuffer, desiredDistance, ~0, QueryTriggerInteraction.Ignore);

            float? closest = null;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _obstructionHitsBuffer[i];
                if (hit.collider == null || hit.collider.transform.root == target) continue;
                if (!closest.HasValue || hit.distance < closest.Value) closest = hit.distance;
            }

            if (!closest.HasValue) return desiredPosition;

            float clampedDistance = Mathf.Clamp(closest.Value - cameraCollisionSkin, minCollisionDistance, desiredDistance);
            return lookAtPoint + direction * clampedDistance;
        }
    }
}
