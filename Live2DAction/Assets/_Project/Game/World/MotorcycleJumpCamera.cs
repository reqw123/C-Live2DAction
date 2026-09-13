using UnityEngine;
using Live2DAction.Vehicles;

namespace Live2DAction.World
{
    // 2026-09-12, user request ("攝影機動畫...猜猜看駕駛摩托翻閱小牛搬運車") - a scripted side-tracking
    // shot (+ a slow-mo dip, matching this project's existing hitstop/slow-mo convention - see
    // YuanpeiIntroCinematic's clash slow-mo) that takes over the instant the motorcycle actually goes
    // airborne off the CampGardenHauler ramp, and hands back the moment it lands. Deliberately gated
    // on BOTH being inside this zone AND all four wheels being off the ground - a zone-only trigger
    // would also fire if the rider just walks/drives through slowly without ever launching, and a
    // wheels-only check with no zone would fire for every ordinary pothole bump anywhere in camp.
    //
    // Renders on TOP of IntegratedRiderVehicleEntry's own vehicleCamera via a higher Camera.depth
    // instead of fighting that component's own LateUpdate() (which re-forces vehicleCamera active
    // every frame while ridden) - simplest way to override the view without touching shared vehicle
    // code. No AudioListener on the jump camera on purpose: only one listener may be active at a
    // time, and the vehicle camera's is left alone rather than juggled for a few seconds of shot.
    [RequireComponent(typeof(Collider))]
    public class MotorcycleJumpCamera : MonoBehaviour
    {
        [SerializeField] private VehicleController motorcycleController;
        [SerializeField] private GameObject jumpCamera;
        [Tooltip("World-space offset from the bike this camera holds while tracking (side-on tracking shot).")]
        [SerializeField] private Vector3 sideOffset = new Vector3(14f, 5f, 0f);
        [SerializeField] private float slowMoTimeScale = 0.5f;
        [Tooltip("Failsafe - ends the shot even if the wheels never register grounded again (e.g. stuck upside down mid-air).")]
        [SerializeField] private float maxJumpSeconds = 5f;

        private bool _inZone;
        private bool _jumping;
        private float _jumpStartUnscaledTime;
        private float _prevTimeScale = 1f;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other) { if (IsMotorcycle(other)) _inZone = true; }
        private void OnTriggerExit(Collider other) { if (IsMotorcycle(other)) { _inZone = false; EndJump(); } }

        private bool IsMotorcycle(Collider other) =>
            motorcycleController != null && other.transform.root == motorcycleController.transform.root;

        private void Update()
        {
            if (motorcycleController == null) return;

            if (!_jumping && _inZone && motorcycleController.enabled && !motorcycleController.AnyWheelGrounded)
                StartJump();

            if (!_jumping) return;

            bool landed = motorcycleController.AnyWheelGrounded;
            bool timedOut = Time.unscaledTime - _jumpStartUnscaledTime > maxJumpSeconds;
            if (landed || timedOut) EndJump();
            else TrackCamera();
        }

        private void StartJump()
        {
            _jumping = true;
            _jumpStartUnscaledTime = Time.unscaledTime;
            _prevTimeScale = Time.timeScale;
            Time.timeScale = slowMoTimeScale;
            if (jumpCamera != null) jumpCamera.SetActive(true);
            TrackCamera();
        }

        private void TrackCamera()
        {
            if (jumpCamera == null) return;
            Vector3 bikePos = motorcycleController.transform.position;
            Vector3 wantPos = bikePos + sideOffset;
            jumpCamera.transform.position = wantPos;
            jumpCamera.transform.rotation = Quaternion.LookRotation((bikePos - wantPos).normalized, Vector3.up);
        }

        private void EndJump()
        {
            if (!_jumping) return;
            _jumping = false;
            Time.timeScale = _prevTimeScale;
            if (jumpCamera != null) jumpCamera.SetActive(false);
        }

        private void OnDisable() => EndJump();

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = _jumping ? Color.red : (_inZone ? Color.yellow : Color.green);
            var c = GetComponent<Collider>();
            if (c != null) Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
        }
    }
}
