using UnityEngine;

namespace Live2DAction.World
{
    // 2026-09-12, user request ("露營區全景圖.exr 當全景圖") - a streamed sub-scene (e.g. Map_Camp)
    // wants its own skybox while the player is inside it, but RenderSettings.skybox is a single
    // global value, not per-scene at runtime - loading a scene additively does NOT swap it. This
    // component pushes an override skybox on enable and restores whatever was active before on
    // destroy, so the base scene's sky comes back once this region unloads.
    public class RegionSkyboxOverride : MonoBehaviour
    {
        [SerializeField] private Material skyboxMaterial;

        private Material _previousSkybox;
        private bool _applied;

        private void OnEnable()
        {
            if (skyboxMaterial == null) return;
            _previousSkybox = RenderSettings.skybox;
            RenderSettings.skybox = skyboxMaterial;
            DynamicGI.UpdateEnvironment();
            _applied = true;
        }

        private void OnDisable() => Restore();
        private void OnDestroy() => Restore();

        private void Restore()
        {
            if (!_applied) return;
            _applied = false;
            RenderSettings.skybox = _previousSkybox;
            DynamicGI.UpdateEnvironment();
        }
    }
}
