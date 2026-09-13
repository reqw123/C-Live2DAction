using UnityEngine;
using Live2DAction.Vehicles;

namespace Live2DAction.World
{
    // 2026-09-12, user request ("希望當猜猜看對摩托觸發駕駛時，能對晴天從底部到最高去做緩慢的層次渲染") -
    // while CampMotorcycle is being ridden, sweep the camp's sky from the plain sunny photo
    // (CampPanoramaOvercast.mat's resting _OvercastRise=0, visually identical to the old
    // CampPanorama.mat) to a desaturated/dimmed "overcast" reinterpretation of that SAME photo,
    // creeping up from the horizon - same reveal-band technique as the yuanpei boss intro's
    // day->night sky wipe (see SkyboxPanoramaOvercast.shader's own comment), just parameter-driven
    // instead of a second photo.
    //
    // Deliberately NOT touching RegionSkyboxOverride - that component owns "what's the camp's
    // baseline skybox while you're inside it" (set once on enable, restored to whatever was active
    // before on exit); this only animates a property on the material CURRENTLY assigned to
    // RenderSettings.skybox while riding, and always through its OWN Material instance (never the
    // shared .mat asset) so nothing here can leak an edit into the source asset.
    public class CampSkyOvercastOnMotorcycle : MonoBehaviour
    {
        [SerializeField] private IntegratedRiderVehicleEntry motorcycleEntry;
        [SerializeField] private float riseSeconds = 6f;   // clear -> overcast while riding
        [SerializeField] private float fallSeconds = 4f;   // overcast -> clear once dismounted

        private Material _instance;
        private float _currentRise;

        private void Update()
        {
            if (motorcycleEntry == null) return;

            float target = motorcycleEntry.IsOccupied ? 1f : 0f;
            float duration = motorcycleEntry.IsOccupied ? riseSeconds : fallSeconds;
            _currentRise = Mathf.MoveTowards(_currentRise, target, Time.deltaTime / Mathf.Max(0.01f, duration));

            if (_currentRise <= 0f && !motorcycleEntry.IsOccupied) return; // fully settled back to clear - nothing to drive

            EnsureInstance();
            if (_instance != null) _instance.SetFloat("_OvercastRise", _currentRise);
        }

        // Grabs whatever RegionSkyboxOverride most recently put in RenderSettings.skybox (the camp's
        // CampPanoramaOvercast.mat) and swaps in a runtime-only copy the first time it's actually
        // needed - not in Awake, since the camp's own skybox override may not have applied yet.
        private void EnsureInstance()
        {
            if (_instance != null) return;
            Material current = RenderSettings.skybox;
            if (current == null) return;
            _instance = new Material(current);
            RenderSettings.skybox = _instance;
        }
    }
}
