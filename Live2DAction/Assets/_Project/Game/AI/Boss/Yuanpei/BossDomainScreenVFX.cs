using UnityEngine;
using Live2DAction.VFX.Rendering;

namespace Live2DAction.AI.Boss.Yuanpei
{
    public enum BossDomainState { Inactive, Entering, Active, PhasePulse, Exiting }

    // ---------------------------------------------------------------------------------------------
    // Pure envelope maths for the Boss domain screen effect - state machine + enter/exit/pulse
    // timelines - extracted so it is unit-testable with no MonoBehaviour, no renderer, no frame
    // loop (same idea as YuanpeiPhaseLogic). No allocations; Tick() is the only per-frame work.
    // ---------------------------------------------------------------------------------------------
    public sealed class BossDomainEnvelope
    {
        public float EnterDuration = 1.2f;
        public float ExitDuration = 2.0f;
        public float PulseDuration = 0.6f;
        public float PulseStrengthDefault = 1f;   // used when SetPhase() auto-fires a pulse

        public BossDomainState State { get; private set; } = BossDomainState.Inactive;
        public float EnterExit { get; private set; }        // 0..1 fade in/out envelope
        public float Intensity { get; private set; } = 1f;  // external SetIntensity() multiplier
        public float Pulse { get; private set; }            // 0..1 transient
        public int Phase { get; private set; } = 1;

        float _pulseT = 999f;
        float _pulseStrength;

        // Anything to render this frame? False only once a full exit has finished.
        public bool IsRendering => State != BossDomainState.Inactive || EnterExit > 0.0001f;

        public void Begin()
        {
            if (State == BossDomainState.Inactive || State == BossDomainState.Exiting)
                State = BossDomainState.Entering;
            Phase = 1;
            Pulse = 0f;
            _pulseT = 999f;
        }

        public void End()
        {
            if (State == BossDomainState.Inactive) return;
            State = BossDomainState.Exiting;
        }

        public void SetPhase(int phase)
        {
            phase = Mathf.Clamp(phase, 1, 3);
            bool changed = phase != Phase;
            Phase = phase;
            if (changed && State != BossDomainState.Inactive && State != BossDomainState.Exiting)
                FirePulse(PulseStrengthDefault);
        }

        // "Pulse(float strength)" - a one-shot flare + inward wave. strength <= 0 means "full".
        public void FirePulse(float strength)
        {
            _pulseStrength = Mathf.Clamp01(strength <= 0f ? 1f : strength);
            _pulseT = 0f;
            if (State == BossDomainState.Active) State = BossDomainState.PhasePulse;
        }

        public void SetIntensity(float value) => Intensity = Mathf.Clamp01(value);

        public void Tick(float dt)
        {
            dt = Mathf.Max(0f, dt);

            switch (State)
            {
                case BossDomainState.Inactive:
                    EnterExit = Mathf.MoveTowards(EnterExit, 0f, dt / Mathf.Max(0.01f, ExitDuration));
                    break;
                case BossDomainState.Entering:
                    EnterExit = Mathf.MoveTowards(EnterExit, 1f, dt / Mathf.Max(0.01f, EnterDuration));
                    if (EnterExit >= 1f) State = BossDomainState.Active;
                    break;
                case BossDomainState.Active:
                case BossDomainState.PhasePulse:
                    EnterExit = Mathf.MoveTowards(EnterExit, 1f, dt / Mathf.Max(0.01f, EnterDuration));
                    break;
                case BossDomainState.Exiting:
                    EnterExit = Mathf.MoveTowards(EnterExit, 0f, dt / Mathf.Max(0.01f, ExitDuration));
                    if (EnterExit <= 0.0001f)
                    {
                        EnterExit = 0f;
                        State = BossDomainState.Inactive;
                        Phase = 1;
                    }
                    break;
            }

            if (_pulseT < PulseDuration)
            {
                _pulseT += dt;
                float k = Mathf.Clamp01(_pulseT / Mathf.Max(0.01f, PulseDuration));
                Pulse = Mathf.Clamp01(_pulseStrength * (k < 0.25f ? k / 0.25f : 1f - (k - 0.25f) / 0.75f));
            }
            else
            {
                Pulse = 0f;
                if (State == BossDomainState.PhasePulse) State = BossDomainState.Active;
            }
        }
    }

    // ---------------------------------------------------------------------------------------------
    // 2026-09-06, explicit user request. Reusable controller for the "Boss 支配領域全螢幕邊界特效"
    // (yuanpei_LogoSky). Drives a RUNTIME material instance (never the .mat asset) that
    // BossDomainScreenVFXRendererFeature borrows to run its URP Full Screen Pass - registered only
    // while something is on screen, cleared afterwards so the pass is fully inert (and free)
    // outside a boss fight (§7).
    //
    // Public API (spec §4): BeginDomain / SetPhase(int) / Pulse(float) / EndDomain / SetIntensity.
    // States (§4): Inactive / Entering / Active / PhasePulse / Exiting (see BossDomainEnvelope).
    //
    // Wiring (BossDomainScreenVFXSetup does this on Map_School): YuanpeiEncounter calls
    // BeginDomain() when the fight arms and EndDomain() on victory / defeat; if `bossVitals` is
    // set this also auto-fires SetPhase() when the boss crosses a phase HP threshold. `onPhasePulse`
    // is an optional UnityEvent left for the user to hook the sky-sword's own brighten (§5.2.4 -
    // NOT force-coupled).
    // ---------------------------------------------------------------------------------------------
    [DisallowMultipleComponent]
    public class BossDomainScreenVFX : MonoBehaviour
    {
        [Header("Source (a runtime instance is made from this - the asset is never modified)")]
        [Tooltip("BossDomainScreenVFX.mat - shader Live2DAction/VFX/BossDomainScreenVFX.")]
        [SerializeField] private Material sourceMaterial;
        [Tooltip("Optional ancient-rune / sword-pattern texture (R channel). None = runes off.")]
        [SerializeField] private Texture2D runeTexture;

        [Header("Auto phase (optional)")]
        [Tooltip("If set, SetPhase() fires automatically when the boss crosses a phase HP threshold.")]
        [SerializeField] private YuanpeiBossVitals bossVitals;

        [Header("Base Intensity / envelope")]
        [Range(0f, 1f)] [SerializeField] private float baseIntensity = 1f;
        [SerializeField] private float enterDuration = 1.2f;
        [SerializeField] private float exitDuration = 2.0f;
        [SerializeField] private float pulseDuration = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float pulseStrength = 1f;
        [Tooltip("Enter/exit/pulse and the breathing all run on unscaled time (keeps animating through hit-stop / pause).")]
        [SerializeField] private bool useUnscaledTime = true;

        [Header("Shape (per spec §4 defaults - tuned in-play against the night arena)")]
        [Tooltip("Border thickness as a fraction of screen HEIGHT (§4: 0.08 - 0.15).")]
        [Range(0.03f, 0.30f)] [SerializeField] private float edgeWidth = 0.12f;
        [Range(0f, 3f)] [SerializeField] private float cornerStrength = 1.5f;
        [Range(0f, 1f)] [SerializeField] private float fogOpacity = 0.38f;
        [Range(0f, 3f)] [SerializeField] private float flameIntensity = 1.1f;

        [Header("Motion")]
        [SerializeField] private float emissionSpeed = 0.6f;
        [SerializeField] private float noiseScale = 3.2f;
        [SerializeField] private float noiseSpeed = 0.05f;
        [Tooltip("RESTING edge warp - keep very low (§3). A pulse briefly adds its own burst.")]
        [Range(0f, 0.05f)] [SerializeField] private float distortionStrength = 0.004f;
        [Range(0f, 1f)] [SerializeField] private float runeIntensity = 0.25f;
        [Tooltip("Seconds per brightness breath cycle (§4: 5 - 8).")]
        [SerializeField] private float breathPeriod = 6.5f;
        [Range(0f, 0.4f)] [SerializeField] private float breathAmount = 0.12f;

        [Header("Colour")]
        [Tooltip("Soul-green emission. Keep it a deep jade so the sky sword stays the main green focus (§2.8).")]
        [ColorUsage(false, true)] [SerializeField] private Color domainColor = new Color(0.10f, 0.85f, 0.55f, 1f);

        [Header("Domain sky (optional - swapped for the fight, restored on exit)")]
        [Tooltip("Night panorama skybox material shown while the domain is up. None = leave the scene skybox alone. " +
                 "The map streaming never makes Map_School the active scene, so this runtime swap is how the boss " +
                 "arena gets its dark sky at all - it also reads as 'the sky itself changed when you entered the domain'.")]
        [SerializeField] private Material domainSkybox;
        [Tooltip("Also darken the ambient light + turn on a low fog while the domain is up.")]
        [SerializeField] private bool darkenEnvironment = true;
        [Tooltip("Ambient BRIGHTNESS while the domain is up (1 = unchanged). The swapped night sky + this " +
                 "is what tints everything in the arena, the boss included.")]
        [Range(0f, 1f)] [SerializeField] private float domainAmbientIntensity = 0.5f;
        [Tooltip("How far the ambient COLOUR shifts toward domainAmbientColor. 0 = keep the scene's own " +
                 "ambient hue (only dim it) so the boss/props don't pick up a blue-green cast; 1 = full shift.")]
        [Range(0f, 1f)] [SerializeField] private float domainAmbientColorTint = 0.35f;
        [ColorUsage(false, false)] [SerializeField] private Color domainAmbientColor = new Color(0.055f, 0.08f, 0.10f, 1f);
        [Tooltip("Rebuild the ambient probe + default reflection from the night sky (makes glossy surfaces " +
                 "reflect the dark sky). Off = keep the reflections the scene was baked/lit with, so the boss's " +
                 "look barely changes.")]
        [SerializeField] private bool updateReflectionsFromSky = false;
        [Tooltip("Optional. A Light (usually on / aimed at the boss) switched ON while the domain is up and " +
                 "OFF on exit - use it to re-light the boss to its intended look while the rest of the arena " +
                 "stays dark. Leave null if the ambient tint alone is fine.")]
        [SerializeField] private Light bossFillLight;

        [Header("Events (optional - not force-coupled, §5.2.4)")]
        [SerializeField] private UnityEngine.Events.UnityEvent onPhasePulse;

        // ---- runtime ----
        private readonly BossDomainEnvelope _env = new BossDomainEnvelope();
        private Material _mat;
        private float _time;
        private int _lastPolledPhase = 1;
        private bool _registered;
        private bool _configDirty = true;

        // cached environment state so a fight can be reversed cleanly (even after a rematch)
        private bool _envApplied;
        private Material _prevSkybox;
        private UnityEngine.Rendering.AmbientMode _prevAmbientMode;
        private Color _prevAmbientLight;
        private Color _prevAmbientSky, _prevAmbientEquator, _prevAmbientGround;
        private float _prevAmbientIntensity;
        private bool _prevFog;
        private Color _prevFogColor;
        private float _prevFogDensity;
        private bool _fillLightWasEnabled;

        // runtime instance of domainSkybox (never the .mat asset) so the intro cinematic can animate
        // its _NightRise (day -> night wipe) without a permanent asset edit.
        private Material _skyInstance;
        static readonly int ID_NightRise = Shader.PropertyToID("_NightRise");

        /// <summary>The runtime skybox material in use while the domain is up (null otherwise).</summary>
        public Material RuntimeSkybox => _skyInstance;

        /// <summary>Intro cinematic drives this 0 (clear day) .. 1 (full night) during the sky wipe.</summary>
        public void SetNightRise(float value)
        {
            if (_skyInstance != null) _skyInstance.SetFloat(ID_NightRise, Mathf.Clamp01(value));
        }

        static readonly int ID_Master = Shader.PropertyToID("_MasterIntensity");
        static readonly int ID_EnterExit = Shader.PropertyToID("_EnterExit");
        static readonly int ID_Phase = Shader.PropertyToID("_Phase");
        static readonly int ID_Pulse = Shader.PropertyToID("_Pulse");
        static readonly int ID_Time = Shader.PropertyToID("_TimeSeconds");
        static readonly int ID_Color = Shader.PropertyToID("_DomainColor");
        static readonly int ID_EdgeWidth = Shader.PropertyToID("_EdgeWidth");
        static readonly int ID_CornerStrength = Shader.PropertyToID("_CornerStrength");
        static readonly int ID_FogOpacity = Shader.PropertyToID("_FogOpacity");
        static readonly int ID_FlameIntensity = Shader.PropertyToID("_FlameIntensity");
        static readonly int ID_EmissionSpeed = Shader.PropertyToID("_EmissionSpeed");
        static readonly int ID_NoiseScale = Shader.PropertyToID("_NoiseScale");
        static readonly int ID_NoiseSpeed = Shader.PropertyToID("_NoiseSpeed");
        static readonly int ID_Distortion = Shader.PropertyToID("_DistortionStrength");
        static readonly int ID_RuneIntensity = Shader.PropertyToID("_RuneIntensity");
        static readonly int ID_BreathPeriod = Shader.PropertyToID("_BreathPeriod");
        static readonly int ID_BreathAmount = Shader.PropertyToID("_BreathAmount");
        static readonly int ID_RuneTex = Shader.PropertyToID("_RuneTex");
        static readonly int ID_HasRune = Shader.PropertyToID("_HasRuneTex");

        public BossDomainState State => _env.State;
        public bool IsActive => _env.State != BossDomainState.Inactive;

        // -------------------------------------------------- public API (spec §4)

        public void BeginDomain()
        {
            EnsureMaterial();
            _time = 0f;
            _lastPolledPhase = 1;
            _env.Begin();
            Register();
            ApplyEnvironment();
        }

        public void EndDomain() => _env.End();   // RestoreEnvironment() runs once the exit fully finishes

        public void SetPhase(int phase)
        {
            int before = _env.Phase;
            _env.SetPhase(phase);
            if (_env.Phase != before) onPhasePulse?.Invoke();
        }

        public void Pulse(float strength)
        {
            _env.FirePulse(strength > 0f ? strength : pulseStrength);
            onPhasePulse?.Invoke();
        }

        public void SetIntensity(float value)
        {
            baseIntensity = Mathf.Clamp01(value);
            _env.SetIntensity(1f);   // envelope keeps its own 0..1; baseIntensity is the real knob
            _configDirty = true;
        }

        // -------------------------------------------------- lifecycle

        private void Awake()
        {
            _env.EnterDuration = enterDuration;
            _env.ExitDuration = exitDuration;
            _env.PulseDuration = pulseDuration;
            _env.PulseStrengthDefault = pulseStrength;
            EnsureMaterial();
        }

        private void OnDisable()
        {
            Unregister();
            RestoreEnvironment();   // never leave the scene stuck on the dark sky
        }

        private void OnDestroy()
        {
            Unregister();
            RestoreEnvironment();
            if (_mat != null)
            {
                if (Application.isPlaying) Destroy(_mat); else DestroyImmediate(_mat);
                _mat = null;
            }
            if (_skyInstance != null)
            {
                if (Application.isPlaying) Destroy(_skyInstance); else DestroyImmediate(_skyInstance);
                _skyInstance = null;
            }
        }

        private void OnValidate()
        {
            _env.EnterDuration = Mathf.Max(0.01f, enterDuration);
            _env.ExitDuration = Mathf.Max(0.01f, exitDuration);
            _env.PulseDuration = Mathf.Max(0.01f, pulseDuration);
            _env.PulseStrengthDefault = pulseStrength;
            _configDirty = true;
        }

        private void Update()
        {
            float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

            // auto phase follow
            if (bossVitals != null && _env.State != BossDomainState.Inactive && _env.State != BossDomainState.Exiting)
            {
                int p = bossVitals.Phase;
                if (p != _lastPolledPhase)
                {
                    _lastPolledPhase = p;
                    SetPhase(p);
                }
            }

            _env.Tick(dt);

            if (_env.IsRendering)
            {
                _time += dt;
                Register();
                PushParams();
            }
            else
            {
                Unregister();
                RestoreEnvironment();   // exit fully finished -> hand the scene sky/lighting back
            }
        }

        // -------------------------------------------------- domain sky / lighting swap

        private void ApplyEnvironment()
        {
            if (_envApplied || !darkenEnvironment) return;
            _envApplied = true;

            _prevSkybox = RenderSettings.skybox;
            _prevAmbientMode = RenderSettings.ambientMode;
            _prevAmbientLight = RenderSettings.ambientLight;
            _prevAmbientSky = RenderSettings.ambientSkyColor;
            _prevAmbientEquator = RenderSettings.ambientEquatorColor;
            _prevAmbientGround = RenderSettings.ambientGroundColor;
            _prevAmbientIntensity = RenderSettings.ambientIntensity;
            _prevFog = RenderSettings.fog;
            _prevFogColor = RenderSettings.fogColor;
            _prevFogDensity = RenderSettings.fogDensity;

            if (domainSkybox != null)
            {
                if (_skyInstance == null) _skyInstance = new Material(domainSkybox) { name = domainSkybox.name + " (runtime)" };
                // start already at full night unless an intro cinematic takes it over and wipes up
                _skyInstance.SetFloat(ID_NightRise, 1f);
                RenderSettings.skybox = _skyInstance;
            }

            // Flat ambient, its colour a blend from the scene's own ambient toward the domain colour -
            // domainAmbientColorTint 0 keeps the scene's hue (boss doesn't pick up a cast), 1 = full shift.
            Color sceneAmbient = _prevAmbientMode == UnityEngine.Rendering.AmbientMode.Flat
                ? _prevAmbientLight
                : _prevAmbientSky;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(sceneAmbient, domainAmbientColor, domainAmbientColorTint);
            RenderSettings.ambientIntensity = domainAmbientIntensity;

            RenderSettings.fog = true;
            RenderSettings.fogColor = Color.Lerp(_prevFogColor, domainAmbientColor, 0.6f);
            RenderSettings.fogDensity = Mathf.Max(RenderSettings.fogDensity, 0.006f);

            if (bossFillLight != null)
            {
                _fillLightWasEnabled = bossFillLight.enabled;
                bossFillLight.enabled = true;
            }

            // Flat ambient takes effect on its own. Only rebuild the ambient probe + default reflection
            // from the (now dark) sky if the user opted in - otherwise the boss/props keep reflecting
            // whatever they did before, so their look barely shifts.
            if (updateReflectionsFromSky) DynamicGI.UpdateEnvironment();
        }

        private void RestoreEnvironment()
        {
            if (!_envApplied) return;
            _envApplied = false;

            RenderSettings.skybox = _prevSkybox;
            RenderSettings.ambientMode = _prevAmbientMode;
            RenderSettings.ambientLight = _prevAmbientLight;
            RenderSettings.ambientSkyColor = _prevAmbientSky;
            RenderSettings.ambientEquatorColor = _prevAmbientEquator;
            RenderSettings.ambientGroundColor = _prevAmbientGround;
            RenderSettings.ambientIntensity = _prevAmbientIntensity;
            RenderSettings.fog = _prevFog;
            RenderSettings.fogColor = _prevFogColor;
            RenderSettings.fogDensity = _prevFogDensity;

            if (bossFillLight != null) bossFillLight.enabled = _fillLightWasEnabled;

            if (updateReflectionsFromSky) DynamicGI.UpdateEnvironment();
        }

        // -------------------------------------------------- material / feature plumbing

        private void EnsureMaterial()
        {
            if (_mat != null) return;
            if (sourceMaterial != null)
            {
                _mat = new Material(sourceMaterial) { name = "BossDomainScreenVFX (runtime)" };
            }
            else
            {
                var sh = Shader.Find("Live2DAction/VFX/BossDomainScreenVFX");
                if (sh != null) _mat = new Material(sh) { name = "BossDomainScreenVFX (runtime, shader-only)" };
            }
            _configDirty = true;
        }

        private void Register()
        {
            if (_registered || _mat == null) return;
            BossDomainScreenVFXRendererFeature.SetMaterial(_mat);
            _registered = true;
            _configDirty = true;
        }

        private void Unregister()
        {
            if (!_registered) return;
            BossDomainScreenVFXRendererFeature.ClearMaterial(_mat);
            _registered = false;
        }

        private void PushParams()
        {
            if (_mat == null) return;

            // animated every frame (all GC-free int-id SetFloat / SetVector native calls)
            _mat.SetFloat(ID_Master, Mathf.Clamp01(baseIntensity) * _env.Intensity);
            _mat.SetFloat(ID_EnterExit, _env.EnterExit);
            _mat.SetFloat(ID_Phase, _env.Phase);
            _mat.SetFloat(ID_Pulse, _env.Pulse);
            _mat.SetFloat(ID_Time, _time);

            if (_configDirty)
            {
                _configDirty = false;
                _mat.SetColor(ID_Color, domainColor);
                _mat.SetFloat(ID_EdgeWidth, edgeWidth);
                _mat.SetFloat(ID_CornerStrength, cornerStrength);
                _mat.SetFloat(ID_FogOpacity, fogOpacity);
                _mat.SetFloat(ID_FlameIntensity, flameIntensity);
                _mat.SetFloat(ID_EmissionSpeed, emissionSpeed);
                _mat.SetFloat(ID_NoiseScale, noiseScale);
                _mat.SetFloat(ID_NoiseSpeed, noiseSpeed);
                _mat.SetFloat(ID_Distortion, distortionStrength);
                _mat.SetFloat(ID_RuneIntensity, runeIntensity);
                _mat.SetFloat(ID_BreathPeriod, breathPeriod);
                _mat.SetFloat(ID_BreathAmount, breathAmount);
                _mat.SetFloat(ID_HasRune, runeTexture != null ? 1f : 0f);
                if (runeTexture != null) _mat.SetTexture(ID_RuneTex, runeTexture);
            }
        }
    }
}
