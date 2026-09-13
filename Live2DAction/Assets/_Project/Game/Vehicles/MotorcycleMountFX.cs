using System.Collections;
using UnityEngine;

namespace Live2DAction.Vehicles
{
    // 2026-09-12, user request ("駕駛摩托時撥放這個配樂 並且...做一整套過場動畫") - the "whole package"
    // for mounting CampMotorcycle: BGM fades in/out with IntegratedRiderVehicleEntry.IsOccupied, and
    // a short (~1.5s) establishing camera swoop plays the instant you mount, before settling into
    // the normal driving view. Deliberately a separate companion script (same shape as
    // CampSkyOvercastOnMotorcycle / MotorcycleJumpCamera) rather than touching the shared
    // IntegratedRiderVehicleEntry - none of Player/Cat/猜猜看's other vehicles need this specific
    // flourish, and this way a fault here can't break mounting itself.
    //
    // The swoop camera uses the same "render on top via higher Camera.depth" trick as
    // MotorcycleJumpCamera instead of fighting IntegratedRiderVehicleEntry's own LateUpdate (which
    // re-asserts its vehicleCamera active every frame while ridden) - it starts at a wide dramatic
    // pose and eases to EXACTLY match the real vehicleCamera's current pose, then deactivates itself;
    // since the two poses coincide at that instant, the handoff back to the normal camera underneath
    // is seamless.
    public class MotorcycleMountFX : MonoBehaviour
    {
        [SerializeField] private IntegratedRiderVehicleEntry motorcycleEntry;
        [SerializeField] private Transform vehicleCameraTransform; // the entry's own driving camera, to swoop INTO
        [SerializeField] private GameObject swoopCamera;

        [Header("Swoop-in (mount)")]
        [SerializeField] private float swoopSeconds = 1.5f;
        [Tooltip("Local offset (relative to the vehicle) the swoop camera starts from - wide/high establishing angle.")]
        [SerializeField] private Vector3 swoopStartLocalOffset = new Vector3(-10f, 6f, -6f);

        [Header("Music")]
        [SerializeField] private AudioClip bgm;
        [SerializeField, Range(0f, 1f)] private float bgmVolume = 0.6f;
        [SerializeField] private float fadeInSeconds = 0.8f;
        [SerializeField] private float fadeOutSeconds = 1.2f;

        private AudioSource _audio;
        private bool _wasOccupied;
        private Coroutine _swoopRoutine;
        private Coroutine _fadeRoutine;

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.clip = bgm;
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f; // music, not a positioned sfx
            _audio.volume = 0f;
        }

        private void Update()
        {
            if (motorcycleEntry == null) { Debug.LogWarning("[MotorcycleMountFX] motorcycleEntry is null - not wired"); return; }
            bool occupied = motorcycleEntry.IsOccupied;
            if (occupied == _wasOccupied) return;
            _wasOccupied = occupied;
            Debug.Log("[MotorcycleMountFX] occupied changed to " + occupied
                + " swoopCamera=" + (swoopCamera != null) + " vehicleCameraTransform=" + (vehicleCameraTransform != null)
                + " bgm=" + (bgm != null));

            if (occupied)
            {
                PlayFlourish(swoopStartLocalOffset, swoopSeconds);
                StartFade(bgmVolume, fadeInSeconds, true);
            }
            else
            {
                if (_swoopRoutine != null) { StopCoroutine(_swoopRoutine); _swoopRoutine = null; }
                if (swoopCamera != null) swoopCamera.SetActive(false);
                StartFade(0f, fadeOutSeconds, false);
            }
        }

        // 2026-09-12, user request ("駕駛摩托車到梯子頂端-小牛坐墊前的交會點時...觸發動畫") - exposed so
        // a separate trigger-zone component (e.g. at the ramp-top/seat junction) can reuse this exact
        // "swoop from a wide angle into the real driving camera's current pose" flourish, without
        // duplicating it - it no longer only fires from Update()'s own mount-detection.
        public void PlayFlourish(Vector3 startLocalOffset, float duration)
        {
            if (_swoopRoutine != null) StopCoroutine(_swoopRoutine);
            _swoopRoutine = StartCoroutine(SwoopIn(startLocalOffset, duration));
        }

        private IEnumerator SwoopIn(Vector3 startLocalOffset, float duration)
        {
            if (swoopCamera == null || vehicleCameraTransform == null)
            {
                Debug.LogWarning("[MotorcycleMountFX] SwoopIn aborted - missing reference (swoopCamera=" + (swoopCamera != null) + " vehicleCameraTransform=" + (vehicleCameraTransform != null) + ")");
                yield break;
            }
            Debug.Log("[MotorcycleMountFX] SwoopIn starting, activating " + swoopCamera.name);
            swoopCamera.SetActive(true);
            Transform vt = motorcycleEntry.transform;
            float t = 0f;
            duration = Mathf.Max(0.05f, duration);
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                Vector3 startPos = vt.TransformPoint(startLocalOffset);
                swoopCamera.transform.position = Vector3.Lerp(startPos, vehicleCameraTransform.position, k);
                swoopCamera.transform.rotation = Quaternion.Slerp(
                    Quaternion.LookRotation((vt.position - startPos).normalized, Vector3.up),
                    vehicleCameraTransform.rotation, k);
                yield return null;
            }
            swoopCamera.transform.SetPositionAndRotation(vehicleCameraTransform.position, vehicleCameraTransform.rotation);
            swoopCamera.SetActive(false); // real vehicleCamera underneath is already at the same pose
            Debug.Log("[MotorcycleMountFX] SwoopIn finished, handed back to " + vehicleCameraTransform.name);
            _swoopRoutine = null;
        }

        private void StartFade(float target, float duration, bool ensurePlaying)
        {
            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            if (ensurePlaying && !_audio.isPlaying) _audio.Play();
            _fadeRoutine = StartCoroutine(FadeVolume(target, duration, !ensurePlaying));
        }

        private IEnumerator FadeVolume(float target, float duration, bool stopWhenDone)
        {
            float start = _audio.volume;
            float t = 0f;
            duration = Mathf.Max(0.01f, duration);
            while (t < duration)
            {
                t += Time.unscaledDeltaTime; // unscaled - MotorcycleJumpCamera's slow-mo shouldn't stretch the fade
                _audio.volume = Mathf.Lerp(start, target, t / duration);
                yield return null;
            }
            _audio.volume = target;
            if (stopWhenDone && target <= 0f) _audio.Stop();
            _fadeRoutine = null;
        }
    }
}
