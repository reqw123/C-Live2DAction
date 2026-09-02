using UnityEngine;

namespace Live2DAction.VFX
{
    // 2026-08-24, explicit user request ("將我提供的三段攻擊特效影片...製作成可用於 3D 動作遊戲的 2.5D
    // VFX 技能特效...每個 Prefab 可調整播放速度、大小、方向、生命週期、透明度、亮度...特效播放完成後
    // 自動回收或 Destroy") - sits on the root of each Attack01/02/03 prefab (see
    // SlashVfxSetup.cs for how the prefab itself is built: one flipbook ParticleSystem using
    // SlashFlipbookURP.shader + a few small spark/smoke/glow child ParticleSystems). Applies every
    // tunable field to the actual ParticleSystem/material instances on Awake, then schedules its
    // own Destroy() for the moment the slowest child system finishes playing - nothing about this
    // reads from or writes to PlayerCombat/EnemyAI/ComboAttackState, this only ever touches
    // components under its own GameObject.
    //
    // Direction: renderMode defaults to Mesh (a quad whose facing comes directly from THIS
    // transform's own rotation, set by whoever spawns it - e.g. SlashVfxSpawner using the
    // character's forward/weapon transform), matching the explicit requirement "劍氣方向應以角色
    // Forward / Weapon Transform 為基準" rather than always facing the camera. billboardToCamera
    // flips every ParticleSystemRenderer under this object to Billboard instead, for whoever
    // explicitly wants camera-facing per "視需要使用 Billboard / Camera Facing".
    [DisallowMultipleComponent]
    public class SlashVfxController : MonoBehaviour
    {
        [SerializeField] private float playbackSpeed = 1f;
        [SerializeField] private float sizeMultiplier = 1f;
        // <= 0 means "leave each ParticleSystem's own authored startLifetime alone".
        [SerializeField] private float lifetimeSecondsOverride = 0f;
        [SerializeField] [Range(0f, 1f)] private float opacity = 1f;
        [SerializeField] private float brightness = 1.5f;
        [SerializeField] private bool billboardToCamera;

        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int OpacityId = Shader.PropertyToID("_Opacity");

        private void Awake()
        {
            ParticleSystem[] allSystems = GetComponentsInChildren<ParticleSystem>(true);
            ParticleSystemRenderer[] allRenderers = GetComponentsInChildren<ParticleSystemRenderer>(true);
            // Only the main flipbook renderer (this GameObject's own, per SlashVfxSetup's
            // hierarchy) is ever Mesh-mode with a quad assigned - the Sparks/Smoke/Glow children
            // are authored as plain camera-facing Billboards (round soft-dot sprites, no mesh) and
            // must stay that way regardless of billboardToCamera. Real bug this fixes: forcing
            // EVERY descendant renderer into Mesh mode left those three with no mesh assigned,
            // which rendered as jagged faceted blobs instead of soft dots - found via an in-Play-
            // Mode screenshot before this was caught.
            ParticleSystemRenderer mainRenderer = GetComponent<ParticleSystemRenderer>();

            // Instanced per-renderer (not the shared material asset) so this prefab's own runtime
            // tuning never bleeds into the project asset - same reasoning/pattern as every other
            // runtime material tweak in this codebase (LightPillarURP/HealthEnergyFlowUI).
            foreach (ParticleSystemRenderer renderer in allRenderers)
            {
                if (renderer.sharedMaterial == null)
                {
                    continue;
                }

                var instance = new Material(renderer.sharedMaterial);
                renderer.material = instance;
                if (instance.HasProperty(BrightnessId))
                {
                    instance.SetFloat(BrightnessId, brightness);
                }
                if (instance.HasProperty(OpacityId))
                {
                    instance.SetFloat(OpacityId, opacity);
                }

                if (renderer == mainRenderer)
                {
                    renderer.renderMode = billboardToCamera ? ParticleSystemRenderMode.Billboard : ParticleSystemRenderMode.Mesh;
                }
            }

            transform.localScale = Vector3.one * sizeMultiplier;

            float longestDuration = 0f;
            foreach (ParticleSystem system in allSystems)
            {
                ParticleSystem.MainModule main = system.main;
                main.simulationSpeed = playbackSpeed;
                if (lifetimeSecondsOverride > 0f)
                {
                    main.startLifetime = lifetimeSecondsOverride;
                }

                float ownDuration = (main.duration + main.startLifetime.constantMax) / Mathf.Max(0.01f, playbackSpeed);
                longestDuration = Mathf.Max(longestDuration, ownDuration);
            }

            // 2026-08-31 (追加78): the R-ultimate cast VFX (PlayerUltimateAura / CatDarkQi) now
            // carry an AudioSource whose clip was cut from the source video and can outlast the
            // visual (a fire pillar's roar tails past the flame). Keep the GameObject - and so
            // the AudioSource - alive until the sound has also finished, otherwise Destroy()
            // would cut it off. Null-safe: prefabs with no AudioSource are unaffected.
            AudioSource audioSource = GetComponent<AudioSource>();
            if (audioSource != null && audioSource.clip != null)
            {
                float pitch = Mathf.Approximately(audioSource.pitch, 0f) ? 1f : Mathf.Abs(audioSource.pitch);
                longestDuration = Mathf.Max(longestDuration, audioSource.clip.length / pitch);
            }

            // Small safety margin on top of the slowest child system's own playback time, so a
            // trailing spark/smoke particle never gets cut off mid-fade by Destroy() firing a
            // frame early.
            Destroy(gameObject, longestDuration + 0.15f);
        }
    }
}
