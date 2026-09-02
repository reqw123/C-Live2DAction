using UnityEngine;

namespace Live2DAction.Cutscene
{
    // 2026-09-01, user request ("武士的開場演戲讓攝影機做2.5秒鐘的360度武士外觀近距離拍攝，最後視角
    // 回到正面看武士做揮刀動作"). Drives its OWN transform: a close 360° orbit around the boss for
    // orbitSeconds, then a short settle to a fixed front-on pose which it holds. Put this on the boss-
    // intro CinemachineCamera GameObject (a plain vcam with no Follow/LookAt just copies its transform
    // to the brain) - the Timeline's Cinemachine Shot keeps it live for the whole cutscene while this
    // does the movement.
    [DefaultExecutionOrder(-20)] // set the vcam transform in Update, before the CinemachineBrain's LateUpdate samples it
    public class IntroOrbitCamera : MonoBehaviour
    {
        [Header("Target")]
        [Tooltip("What to orbit / look at. The boss root; the aim point is target.position + aimOffset.")]
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 aimOffset = new Vector3(0f, 2.6f, 0f); // ~chest of the 4x 武士

        [Header("Orbit (phase 1)")]
        [SerializeField] private float orbitSeconds = 2.5f;
        [SerializeField] private float orbitRadius = 4.6f;
        [SerializeField] private float orbitHeight = 3.1f;
        [Tooltip("Degrees around +Y the orbit starts at (0 = directly in front of the boss's -Z facing). " +
                 "360 total is swept over orbitSeconds.")]
        [SerializeField] private float startAngleDegrees = 200f;
        [Tooltip("+1 = clockwise seen from above, -1 = counter-clockwise.")]
        [SerializeField] private float direction = 1f;

        [Header("Settle to front (phase 2)")]
        [SerializeField] private float settleSeconds = 0.7f;
        [Tooltip("Final resting pose, world space - a front-on shot of the boss doing its swing. " +
                 "If frontPoseAnchor is set it wins over these.")]
        [SerializeField] private Vector3 frontPosition = new Vector3(-1.6f, 3.25f, 4.1f);
        [SerializeField] private Vector3 frontLookAtOffset = new Vector3(0f, 2.3f, 0f);
        [SerializeField] private Transform frontPoseAnchor;

        private float _elapsed;

        private void OnEnable()
        {
            _elapsed = 0f;
            Place(0f);
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            Place(_elapsed);
        }

        private void Place(float t)
        {
            if (target == null)
            {
                return;
            }
            Vector3 aim = target.position + aimOffset;

            // Orbit pose at time t.
            float frac = orbitSeconds > 0.001f ? Mathf.Clamp01(t / orbitSeconds) : 1f;
            float angle = startAngleDegrees + direction * 360f * frac;
            Vector3 offset = Quaternion.Euler(0f, angle, 0f) * new Vector3(0f, 0f, orbitRadius);
            Vector3 orbitPos = new Vector3(aim.x + offset.x, target.position.y + orbitHeight, aim.z + offset.z);

            // Front pose.
            Vector3 frontPos = frontPoseAnchor != null ? frontPoseAnchor.position : frontPosition;
            Vector3 frontAim = frontPoseAnchor != null ? aim : target.position + frontLookAtOffset;

            Vector3 pos;
            Vector3 lookAt;
            if (t <= orbitSeconds)
            {
                pos = orbitPos;
                lookAt = aim;
            }
            else
            {
                float s = settleSeconds > 0.001f ? Mathf.Clamp01((t - orbitSeconds) / settleSeconds) : 1f;
                s = s * s * (3f - 2f * s); // smoothstep
                pos = Vector3.Lerp(orbitPos, frontPos, s);
                lookAt = Vector3.Lerp(aim, frontAim, s);
            }

            transform.position = pos;
            Vector3 dir = lookAt - pos;
            if (dir.sqrMagnitude > 1e-5f)
            {
                transform.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        public void EditorConfigure(Transform orbitTarget, float seconds, float radius, float height,
            float startAngle, Vector3 finalPos, Vector3 finalLookAtOffset)
        {
            target = orbitTarget;
            orbitSeconds = seconds;
            orbitRadius = radius;
            orbitHeight = height;
            startAngleDegrees = startAngle;
            frontPosition = finalPos;
            frontLookAtOffset = finalLookAtOffset;
        }
    }
}
