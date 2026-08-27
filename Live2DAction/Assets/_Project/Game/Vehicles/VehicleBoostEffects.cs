using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Live2DAction.Vehicles
{
    // 2026-08-26, explicit user request ("駕駛時SHIFT觸發期間給予特效設計") - purely reactive to
    // VehicleController.IsBoosting, kept as its own component (not stuffed into VehicleController
    // itself) so the physics/input class doesn't grow VFX/post-process concerns - same separation
    // this vehicle subsystem already uses for camera (VehicleCameraController) and entry
    // (VehicleEntrySystem). Two layers of feedback: a rear particle trail (visible in chase view)
    // and a global post-process kick (visible in every view, first-person included, where the
    // particle trail behind the car isn't).
    public class VehicleBoostEffects : MonoBehaviour
    {
        [SerializeField] private VehicleController vehicleController;
        [SerializeField] private ParticleSystem boostParticles;

        // The scene's single global PostProcessingVolume - Vignette/ChromaticAberration overrides
        // were added to its shared profile specifically for this (both started at intensity 0, so
        // normal on-foot/non-boost gameplay is completely unaffected until this component pushes
        // them). Global and shared across the whole scene deliberately - a per-camera volume would
        // need duplicating for Main Camera AND VehicleCamera for no benefit.
        [SerializeField] private Volume postProcessVolume;

        [SerializeField, Range(0f, 1f)] private float vignetteBoostIntensity = 0.3f;
        [SerializeField, Range(0f, 1f)] private float chromaticAberrationBoostIntensity = 0.4f;
        [SerializeField] private float effectSmoothSpeed = 5f;

        private Vignette _vignette;
        private ChromaticAberration _chromaticAberration;
        private float _currentEffectAmount;

        private void Awake()
        {
            if (postProcessVolume != null && postProcessVolume.profile != null)
            {
                postProcessVolume.profile.TryGet(out _vignette);
                postProcessVolume.profile.TryGet(out _chromaticAberration);
            }
        }

        // Reset on disable, not just "stop pushing new values" - if the whole component/GameObject
        // gets disabled mid-boost (e.g. Buggy despawned), the post-process shouldn't stay stuck
        // showing a permanent vignette over normal gameplay.
        private void OnDisable()
        {
            SetIntensity(0f);
            if (boostParticles != null && boostParticles.isPlaying) boostParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        private void Update()
        {
            bool boosting = vehicleController != null && vehicleController.enabled && vehicleController.IsBoosting;

            if (boostParticles != null)
            {
                if (boosting && !boostParticles.isPlaying) boostParticles.Play();
                else if (!boosting && boostParticles.isPlaying) boostParticles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            float target = boosting ? 1f : 0f;
            _currentEffectAmount = Mathf.MoveTowards(_currentEffectAmount, target, effectSmoothSpeed * Time.deltaTime);
            SetIntensity(_currentEffectAmount);
        }

        private void SetIntensity(float amount)
        {
            if (_vignette != null) _vignette.intensity.value = amount * vignetteBoostIntensity;
            if (_chromaticAberration != null) _chromaticAberration.intensity.value = amount * chromaticAberrationBoostIntensity;
        }
    }
}
