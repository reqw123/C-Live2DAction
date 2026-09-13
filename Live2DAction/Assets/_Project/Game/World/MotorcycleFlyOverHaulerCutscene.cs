using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Live2DAction.Vehicles;

namespace Live2DAction.World
{
    // 2026-09-12, user request ("我想做一個騎著摩托車飛越小牛的動畫") - a fully scripted flight, not a
    // real WheelCollider-physics jump: CampGardenHauler is 48m across, and the ramp only climbs to
    // seat height (~10m of its 32m total) - no realistic ramp speed could actually clear it under
    // real physics, and there's no room to land BEYOND it either (the south camp wall sits only ~2m
    // past the hauler's own far edge). Rigidbody goes kinematic, VehicleController stands down for
    // the duration, and control is handed back once it lands.
    //
    // 續 (user: "翻越的時候偏移了，請往坐墊那個方向去翻越" + camera choreography request) - the first
    // version LERPed straight from the launch point to a landing spot to the WEST, which reads as
    // veering sideways immediately instead of flying forward over the seat. Fixed with a quadratic
    // Bezier through a THIRD point placed straight ahead (the bike's own forward at launch - i.e.
    // toward/over the seat) at the peak height, so the path visibly heads forward-and-up first and
    // only curves toward the (still necessarily offset, for landing-room reasons) touchdown point
    // afterward. Camera choreography: tight close-up on the bike at the exact peak of the arc, then
    // orbits + pulls back to a wide shot revealing the whole hauler by touchdown - implemented as two
    // lerp segments (launch->peak, peak->landing) over distance/height/orbit-angle, not a single
    // constant offset like the old version.
    //
    // 續 (user: "翻越的時候 小牛車不要移動 且摩托超載整個過程抖動 不自然不連續") - two real physics bugs,
    // not path/camera tuning: (1) CampGardenHaulerVehicle's Rigidbody is a 20000kg DYNAMIC body (it
    // needs to be non-kinematic for its own WheelCollider driving) - the bike's kinematic Move()
    // clipping through its box colliders at liftoff was shoving it, so it's now forced kinematic (+
    // IgnoreCollision against the bike's colliders) for the flight's duration only. (2) the bike's own
    // Rigidbody had interpolation=None, so a kinematic Move() only visually updates once per physics
    // tick instead of every rendered frame - looks like discrete steps ("不連續"); fixed by switching
    // it to Interpolate for the flight. Also found MotorcycleJumpZone's camera/slow-mo zone spatially
    // overlaps this trigger, so a real ramp jump already in progress could still be running its own
    // camera + Time.timeScale shot underneath this one - force-disabled for the flight (its OnDisable
    // already calls EndJump()) so the two don't fight.
    //
    // 續 (user: "落點偏向了後車箱...近距離特寫不夠聚焦...放慢的力度也不夠") - three fixes:
    // (1) landing was (-83, 0.5, -126) - inside the hauler's own 48m-wide VISUAL mesh footprint
    // (only clear of its narrower BoxColliders), at the SAME z-depth as the hauler's mid/rear body,
    // with only ~3m to the west wall - there was never a genuinely open, wide landing pad there.
    // Measured the actual courtyard (露營區 bounds + CampWall_* positions) and found the hauler's
    // FRONT edge sits at z=-101.4, well south of the wide-open ~20m entrance apron the ramp already
    // sits in (raycast-confirmed flat ground at y=0.5) - moved the landing there instead, aligned
    // with the ramp/seat's own X so the bike loops up over the hauler's rear cargo bed (still the
    // dramatic part of the shot, cleared by the 40m peak height) and comes back down on real open
    // ground in front of the ramp, not skewed into the truck. (2) close-up eased in tighter + more
    // level (was a plain dead-behind view) and now sweeps through an angled 3/4 orbit the whole time
    // (closeOrbitDegrees), plus a push into a narrower FOV and a small handheld-style jitter timed to
    // the peak, for a first-person-adjacent "in the moment" feel instead of a static tracking shot.
    // (3) added an actual Time.timeScale dip of its own (this cutscene has none - the earlier
    // MotorcycleJumpCamera slow-mo is force-disabled during this whole shot, so there was nothing
    // slowing time here at all) that eases in/out around peakK, strong enough to be felt.
    //
    // 續 (user: "翻越時翻過去就過去了別回來...近距離特寫結束後轉背向著整台小牛拍攝再切為摩托視角...
    // 畫面抖動嚴重") - measured the courtyard precisely: the hauler's real BoxColliders end at
    // z=-140.1, but its decorative visual mesh runs to z=-149.77, and the south wall's own collider
    // starts at z=-151.5 - so there genuinely is a landable (if tight, ~1.7m) strip of open physics
    // space directly behind the hauler. Moved landing there (z=-150, still centered on the ramp/seat
    // X) instead of looping back north, so the bike now flies straight over the hauler's full length
    // and keeps going, matching "翻過去就過去了". Camera now has a third phase after the close-up: it
    // swings on around (reusing the same orbit math, just carried out to 180 degrees) to a position
    // AHEAD of the bike looking back - the whole hauler framed receding behind it - then at
    // onboardCutK simply stops driving the shot and deactivates itself, handing control back to
    // IntegratedRiderVehicleEntry's own onboard camera (which has been sitting active underneath this
    // whole time, per the "render on top via higher Camera.depth" convention - no explicit "switch"
    // needed, just stop covering it). Also found the handheld jitter added last round used
    // Time.unscaledTime while the slow-mo dip slows Time.timeScale down to 0.3x - the jitter kept
    // oscillating at full real-world speed while everything else crawled, reading as violent shaking
    // relative to the slowed footage; switched it to scaled Time.time (so it slows down WITH the
    // shot) and cut its amplitude/frequency well down.
    //
    // 續 (user: "整體完美...翻越時攝影機畫面會有明顯卡頓不連貫") - this WAS a design bug, introduced by
    // the earlier "不自然不連續" fix: driving the bike via rb.Move() + Rigidbody.interpolation=
    // Interpolate is correct for a body that needs continuous collision detection against other
    // dynamic bodies, but interpolation computes the RENDERED position by blending between physics
    // steps using a timeScale-dependent fraction - and this shot changes Time.timeScale continuously,
    // every single Update, for the slow-mo dip. Interpolation was never designed for the timeScale it
    // depends on to be a moving target frame-to-frame, so it visibly hitched. Fix: this flight never
    // actually needed Rigidbody-mediated motion at all - collision with the hauler is already
    // IgnoreCollision'd for the whole shot and nothing else sits in the flight path - so it now writes
    // the Transform directly (bike.SetPositionAndRotation) every frame instead of going through
    // rb.Move()/interpolation. A direct Transform write always renders exactly at the position set
    // that frame, with zero dependency on FixedUpdate cadence or timeScale, which is what actually
    // fixes both the old "不連續" stepping AND this new interpolation/timeScale stutter at once.
    //
    // 續 (user: "摩托翻越調整...改成翻越到小牛搬運車的駕駛座，接觸到小牛駕駛座後自動變成小牛駕駛狀態，
    // 且可以向CAR一樣的移動控制邏輯") - the stunt's payoff changed from "land and keep riding the bike"
    // to "land ON the hauler and immediately drive IT": at the end of FlyOver(), instead of
    // re-enabling motorcycleController, the rider is handed directly to the hauler's own
    // IntegratedRiderVehicleEntry via TransferRiderTo() (a vehicle-to-vehicle transfer, not a real
    // Dismount()+Mount() - the rider is never actually on foot in between). The hauler already drives
    // with car-style WASD via the same VehicleController the bike uses, so "跟CAR一樣的移動控制邏輯"
    // came for free once the rider is aboard it. landingWorldPosition was re-tuned to the hauler's
    // NEW rear-seat-adjacent open ground after the same-session 90-degree body/wheel-alignment fix
    // (the old value was measured against the pre-rotation body footprint).
    [RequireComponent(typeof(Collider))]
    public class MotorcycleFlyOverHaulerCutscene : MonoBehaviour
    {
        [SerializeField] private VehicleController motorcycleController;
        [SerializeField] private GameObject flyCamera;

        [Header("Safety - the hauler must not react to the bike clipping through it mid-stunt")]
        [Tooltip("CampGardenHaulerVehicle's own Rigidbody - it is a 20000kg DYNAMIC body (needed for its own WheelCollider driving), so the bike's kinematic Move() through its colliders was shoving it around. Forced kinematic for the flight's duration only.")]
        [SerializeField] private Rigidbody haulerRigidbody;
        [Tooltip("MotorcycleJumpZone's camera zone overlaps this trigger (both sit around the ramp top), so a real airborne jump can still be mid-shot (its own camera + slow-mo Time.timeScale) when this cutscene's trigger fires. Disabling it for the flight's duration force-ends that shot (its OnDisable already calls EndJump()) so the two systems stop fighting over the camera and timescale.")]
        [SerializeField] private MotorcycleJumpCamera conflictingJumpCamera;
        [Tooltip("The hauler's front tip in ITS OWN local space (not world) - used to face the bike toward the nose at touchdown. Computed relative to haulerRigidbody.transform at landing time so it stays correct if the hauler is ever repositioned again, instead of a hardcoded world rotation going stale.")]
        [SerializeField] private Vector3 haulerNoseLocalOffset = new Vector3(8.15f, 0f, 24f);

        [Header("Path (quadratic Bezier: launch -> forward/high control point -> landing)")]
        [Tooltip("How far along the bike's own forward direction (at launch) the control point sits - this is what makes the arc visibly head toward/over the seat first.")]
        [SerializeField] private float forwardControlDistance = 22f;
        [SerializeField] private float peakWorldHeight = 40f;
        [Tooltip("2026-09-13, user request ('落向駕駛座...碰到坐墊時直接觸發駕駛小牛') - the flight now lands ON the hauler's seat platform itself (elevated, on the flatbed) instead of open ground, so the existing end-of-flight transfer (TransferRiderTo) fires right as it arrives there. Fallback only, used if haulerRigidbody isn't wired - the REAL landing point is haulerSeatLocalOffset below, computed live off the hauler's current transform every time this fires (see FlyOver()) so it can't go stale again the way this hardcoded value already has, four times this session, every time the hauler got repositioned.")]
        [SerializeField] private Vector3 landingWorldPosition = new Vector3(-82.23f, 11.2f, -126.23f);
        [Tooltip("The hauler's seat platform position in ITS OWN local space (not world) - the actual landing target, computed live each flight via haulerRigidbody.transform.TransformPoint() so repositioning the hauler again doesn't require re-tuning this cutscene.")]
        [SerializeField] private Vector3 haulerSeatLocalOffset = new Vector3(8.15f, -6.52f, -0.23f);
        [SerializeField] private float durationSeconds = 3.5f;
        [SerializeField] private float spinDegreesTotal = 360f; // one full barrel roll for showmanship

        [Header("Camera - close-up at the peak, then swing around to a reveal, then cut to onboard")]
        [SerializeField] private float peakK = 0.5f; // where in the flight (0..1) the arc is highest
        [SerializeField] private float closeDistance = 2.2f;
        [SerializeField] private float closeHeight = 1.1f;
        [Tooltip("Orbit angle the close-up shot itself sweeps THROUGH (0..this) as it eases in, instead of sitting dead-behind - continues on toward revealOrbitDegrees afterward so the whole shot is one continuous sweep.")]
        [SerializeField] private float closeOrbitDegrees = 25f;
        [SerializeField] private float establishDistance = 14f;
        [SerializeField] private float establishHeight = 6f;
        [Tooltip("Fraction of the flight (0..1) by which the camera has fully swung around to the reveal position (orbitDeg=180, i.e. directly ahead of the bike looking back at the whole hauler). Must be > peakK.")]
        [SerializeField] private float revealK = 0.75f;
        [SerializeField] private float revealDistance = 30f;
        [SerializeField] private float revealHeight = 14f;
        [SerializeField] private float revealOrbitDegrees = 180f; // 180 = directly ahead of the bike, looking back - reuses the same "back" rotation math, just carried past the side and around to the front
        [Tooltip("Fraction of the flight (0..1) at which this cutscene stops driving its own camera and hands off to the vehicle's normal onboard camera (already active underneath). Must be > revealK.")]
        [SerializeField] private float onboardCutK = 0.85f;

        [Header("Camera - close-up FOV punch + handheld jitter, both centered on the peak")]
        [SerializeField] private float fovNormal = 60f;
        [SerializeField] private float fovClose = 74f;
        [Tooltip("Kept small and driven by SCALED Time.time (not unscaled) so it slows down along with the slow-mo dip instead of shaking at full real-world speed against slowed footage.")]
        [SerializeField] private float jitterAmplitude = 0.02f;
        [SerializeField] private float jitterFrequency = 9f;

        [Header("Slow motion around the peak - this cutscene's own dip (independent of MotorcycleJumpCamera, which is disabled for the whole shot)")]
        [SerializeField, Range(0.05f, 1f)] private float slowMoTimeScale = 0.3f;
        [Tooltip("How wide (in 0..1 flight fraction) the slow-mo ease-in/out spans around peakK - e.g. 0.3 means it starts ramping in at peakK-0.3 and is back to normal speed by peakK+0.3.")]
        [SerializeField] private float slowMoWindow = 0.3f;

        private Camera _flyCam;
        private bool _triggered;

        private void Reset()
        {
            var c = GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered || motorcycleController == null || !motorcycleController.enabled) return;
            if (other.transform.root != motorcycleController.transform.root) return;
            _triggered = true;
            StartCoroutine(FlyOver());
        }

        private static Vector2 BezierXZ(Vector2 p0, Vector2 p1, Vector2 p2, float t)
        {
            float u = 1f - t;
            return u * u * p0 + 2f * u * t * p1 + t * t * p2;
        }

        private IEnumerator FlyOver()
        {
            Transform bike = motorcycleController.transform;
            Rigidbody rb = motorcycleController.GetComponent<Rigidbody>();

            // Computed live off the hauler's CURRENT transform (not the serialized fallback) so this
            // stays correct no matter how many more times the hauler gets repositioned - see
            // haulerSeatLocalOffset's field comment.
            Vector3 landingWorldPosition = haulerRigidbody != null
                ? haulerRigidbody.transform.TransformPoint(haulerSeatLocalOffset)
                : this.landingWorldPosition;

            Vector3 start = bike.position;
            Quaternion startRot = bike.rotation;
            Vector3 launchForward = bike.forward;
            launchForward.y = 0f;
            if (launchForward.sqrMagnitude < 0.001f) launchForward = Vector3.forward;
            launchForward.Normalize();

            // control point: straight ahead of the launch (toward/over the seat) - XZ only, drives
            // the horizontal curve toward/over the seat. Height is handled separately below so the
            // apex always actually reaches peakWorldHeight (a plain 3-point Bezier UNDERSHOOTS the
            // control point's Y whenever the endpoints sit at very different heights, which is what
            // made the previous version's vault noticeably lower than intended - user report
            // "翻越的高度不對 要跟上次的高度一樣").
            Vector3 control = start + launchForward * forwardControlDistance;

            Vector2 startXZ = new Vector2(start.x, start.z);
            Vector2 controlXZ = new Vector2(control.x, control.z);
            Vector2 landXZ = new Vector2(landingWorldPosition.x, landingWorldPosition.z);

            float peakDenom = Mathf.Max(0.0001f, peakK * (1f - peakK));
            float baselineAtPeak = Mathf.Lerp(start.y, landingWorldPosition.y, peakK);
            float bumpAmount = peakWorldHeight - baselineAtPeak;

            // freeze anything that could still push back against a fast kinematic body clipping
            // through it (user report: "小牛車不要移動" - the 20000kg hauler was getting shoved by
            // collision resolution against the bike at liftoff) and stop any already-running jump
            // shot from fighting this one over the camera/timescale (user report: "抖動 不自然不連續")
            bool haulerWasKinematic = false;
            if (haulerRigidbody != null)
            {
                haulerWasKinematic = haulerRigidbody.isKinematic;
                haulerRigidbody.linearVelocity = Vector3.zero;
                haulerRigidbody.angularVelocity = Vector3.zero;
                haulerRigidbody.isKinematic = true;
            }
            if (conflictingJumpCamera != null) conflictingJumpCamera.enabled = false; // its own OnDisable calls EndJump()

            List<Collider> bikeCols = rb != null ? new List<Collider>(rb.GetComponentsInChildren<Collider>()) : new List<Collider>();
            List<Collider> haulerCols = haulerRigidbody != null ? new List<Collider>(haulerRigidbody.GetComponentsInChildren<Collider>()) : new List<Collider>();
            for (int bi = 0; bi < bikeCols.Count; bi++)
                for (int hi = 0; hi < haulerCols.Count; hi++)
                    Physics.IgnoreCollision(bikeCols[bi], haulerCols[hi], true);

            motorcycleController.enabled = false;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            if (flyCamera != null)
            {
                flyCamera.SetActive(true);
                if (_flyCam == null) _flyCam = flyCamera.GetComponent<Camera>();
            }

            float prevTimeScale = Time.timeScale;
            float slowMoDenom = Mathf.Max(0.001f, slowMoWindow);

            float t = 0f;
            while (t < durationSeconds)
            {
                // slow-mo dip: bell-shaped intensity centered on peakK, based on THIS frame's own k
                // (one frame stale, negligible) so time itself visibly stretches through the close-up
                // instead of the shot just playing at normal speed with a tight camera - the previous
                // version had no timescale change here at all (user report "放慢的力度也不夠").
                float prevK = Mathf.Clamp01(t / durationSeconds);
                float slowMoIntensity = Mathf.Clamp01(1f - Mathf.Abs(prevK - peakK) / slowMoDenom);
                slowMoIntensity = Mathf.SmoothStep(0f, 1f, slowMoIntensity);
                Time.timeScale = Mathf.Lerp(prevTimeScale, slowMoTimeScale, slowMoIntensity);

                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / durationSeconds);

                Vector2 xz = BezierXZ(startXZ, controlXZ, landXZ, k);
                float baseline = Mathf.Lerp(start.y, landingWorldPosition.y, k);
                float bumpShape = k * (1f - k) / peakDenom; // 0 at k=0/1, exactly 1 at k=peakK
                float y = baseline + bumpAmount * bumpShape;
                Vector3 pos = new Vector3(xz.x, y, xz.y);

                Quaternion rot = startRot * Quaternion.Euler(0f, 0f, -spinDegreesTotal * k);
                bike.SetPositionAndRotation(pos, rot); // direct Transform write, not rb.Move() - see class comment on why

                if (flyCamera != null)
                {
                    if (k < onboardCutK) PositionCamera(pos, launchForward, k, slowMoIntensity);
                    else if (flyCamera.activeSelf) flyCamera.SetActive(false); // hand off to the onboard camera already active underneath
                }
                yield return null;
            }

            Time.timeScale = prevTimeScale;

            // 2026-09-13, user request ("摩托翻越的落點請朝向小牛搬運車的龍頭") - face the bike toward
            // the hauler's nose at touchdown instead of just keeping whatever rotation it launched
            // with (startRot). Computed from haulerRigidbody.transform at landing time (not a
            // hardcoded world rotation) so this stays correct if the hauler is repositioned again -
            // it's already moved twice this session for unrelated placement requests.
            Quaternion landingRot = startRot;
            if (haulerRigidbody != null)
            {
                Vector3 noseWorld = haulerRigidbody.transform.TransformPoint(haulerNoseLocalOffset);
                Vector3 toNose = noseWorld - landingWorldPosition;
                toNose.y = 0f;
                if (toNose.sqrMagnitude > 0.01f) landingRot = Quaternion.LookRotation(toNose.normalized, Vector3.up);
            }
            bike.SetPositionAndRotation(landingWorldPosition, landingRot);
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            // 2026-09-13, user request ("摩托翻越...改成翻越到小牛搬運車的駕駛座，接觸到小牛駕駛座後自動
            // 變成小牛駕駛狀態，且可以向CAR一樣的移動控制邏輯") - land the stunt as a vehicle-to-vehicle
            // transfer instead of handing control back to the bike: the rider hops off onto the
            // hauler's seat and immediately drives IT (same VehicleController-driven car-style WASD
            // the hauler already has - see the 續3/續4 rounds this session). The abandoned bike is
            // left non-kinematic (settles under gravity, a normal inert prop) but its
            // vehicleController stays DISABLED - re-enabling it here would free-drift it on raw WASD
            // with nobody aboard (the exact bug IntegratedRiderVehicleEntry.Awake() already guards
            // against for the normal mount/dismount path).
            // 2026-09-13, user report (console: "Setting linear velocity of a kinematic body is not
            // supported" / "angular velocity...") - this MUST run before TransferRiderTo below: the
            // hauler's Rigidbody has been forced kinematic since the start of the flight (so the
            // bike's kinematic Move() through its colliders wouldn't shove it), and TransferRiderTo
            // hands the rider to the hauler's Mount(), which (via GroundAndLevel(), since this hauler
            // has groundAndLevelOnMount=true) tries to zero out linearVelocity/angularVelocity - which
            // Unity refuses on a still-kinematic body. Restoring the hauler's real physics state FIRST
            // means it's back to normal dynamic before Mount() ever touches it.
            for (int bi = 0; bi < bikeCols.Count; bi++)
                for (int hi = 0; hi < haulerCols.Count; hi++)
                    Physics.IgnoreCollision(bikeCols[bi], haulerCols[hi], false);
            if (haulerRigidbody != null) haulerRigidbody.isKinematic = haulerWasKinematic;
            if (conflictingJumpCamera != null) conflictingJumpCamera.enabled = true;

            IntegratedRiderVehicleEntry motorcycleEntry = motorcycleController.GetComponent<IntegratedRiderVehicleEntry>();
            IntegratedRiderVehicleEntry haulerEntry = haulerRigidbody != null ? haulerRigidbody.GetComponent<IntegratedRiderVehicleEntry>() : null;
            if (motorcycleEntry != null && haulerEntry != null && motorcycleEntry.CurrentRider != null)
            {
                motorcycleEntry.TransferRiderTo(haulerEntry);
            }
            else
            {
                motorcycleController.enabled = true; // fallback - no hauler entry wired, behave as before
            }
            if (flyCamera != null) flyCamera.SetActive(false);

            _triggered = false; // re-armed for next time
        }

        // Three phases, one continuous orbit sweep (0 -> closeOrbitDegrees -> revealOrbitDegrees):
        // launch->peak: establishing distance eases IN to a tight, angled close-up, timed to land
        // exactly at peakK (the highest point of the arc). peak->revealK: swings on around (past the
        // side, out to 180 degrees by default = directly ahead of the bike) while pulling back to a
        // wide shot, so the whole hauler comes into frame receding behind the bike. revealK->cut: the
        // reveal composition just holds (tracking the bike from the same relative offset) until
        // onboardCutK stops calling this method entirely and hands off to the onboard camera.
        // slowMoIntensity (0..1, peaks at peakK) drives the FOV punch-in and handheld jitter so both
        // land exactly on the slow-mo dip for a unified "in the moment" beat.
        private void PositionCamera(Vector3 bikePos, Vector3 launchForward, float k, float slowMoIntensity)
        {
            float distance, height, orbitDeg;
            if (k <= peakK)
            {
                float p = Mathf.Clamp01(k / Mathf.Max(0.001f, peakK));
                float ease = Mathf.SmoothStep(0f, 1f, p);
                distance = Mathf.Lerp(establishDistance, closeDistance, ease);
                height = Mathf.Lerp(establishHeight, closeHeight, ease);
                orbitDeg = Mathf.Lerp(0f, closeOrbitDegrees, ease);
            }
            else if (k <= revealK)
            {
                float p = Mathf.Clamp01((k - peakK) / Mathf.Max(0.001f, revealK - peakK));
                float ease = Mathf.SmoothStep(0f, 1f, p);
                distance = Mathf.Lerp(closeDistance, revealDistance, ease);
                height = Mathf.Lerp(closeHeight, revealHeight, ease);
                orbitDeg = Mathf.Lerp(closeOrbitDegrees, revealOrbitDegrees, ease);
            }
            else
            {
                distance = revealDistance;
                height = revealHeight;
                orbitDeg = revealOrbitDegrees;
            }

            Vector3 back = Quaternion.Euler(0f, orbitDeg, 0f) * (-launchForward);
            Vector3 camPos = bikePos + back * distance + Vector3.up * height;

            if (slowMoIntensity > 0.001f)
            {
                // scaled Time.time - this must slow down WITH the timeScale dip, or it visibly shakes
                // at full real-world speed against the slowed-down footage (user report "抖動嚴重").
                float n = Time.time * jitterFrequency;
                Vector3 jitter = new Vector3(
                    Mathf.PerlinNoise(n, 0f) - 0.5f,
                    Mathf.PerlinNoise(0f, n) - 0.5f,
                    Mathf.PerlinNoise(n, n) - 0.5f);
                camPos += jitter * (jitterAmplitude * slowMoIntensity);
            }

            flyCamera.transform.position = camPos;
            flyCamera.transform.rotation = Quaternion.LookRotation((bikePos - camPos).normalized, Vector3.up);
            if (_flyCam != null) _flyCam.fieldOfView = Mathf.Lerp(fovNormal, fovClose, slowMoIntensity);
        }
    }
}
