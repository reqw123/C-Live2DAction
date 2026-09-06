// 2026-09-06, explicit user request: a "Boss 支配領域全螢幕邊界特效" for the yuanpei_LogoSky aerial
// boss - a screen-space soul-domain effect fixed to the SCREEN edges (not a world Particle System,
// not a hurt-frame), showing the player has entered the boss's abnormal domain. Runs the whole
// fight, dies with the boss.
//
// Rendered by BossDomainScreenVFXRendererFeature (URP Full Screen Pass, injected
// BeforeRenderingPostProcessing so the green emission can feed Bloom if a project adds it later -
// there is none in this project today, so the flame carries its own soft ridge glow and does not
// depend on it). Screen-Space-Overlay HUD/血條/提示 all draw AFTER the whole render pipeline, so
// they always sit on top of this with zero extra work.
//
// Every animated quantity is driven from BossDomainScreenVFX.cs through a RUNTIME material instance
// (never the .mat asset) via cached property IDs - no per-frame GC. All noise is procedural (no
// texture) except the optional _RuneTex slot.
//
// Design rules honoured in the maths below:
//  - Edge Mask from screen UV, converted to screen-HEIGHT units so 16:9 / 16:10 / 21:9 keep the
//    same visible border thickness (§2 / §6.5 / §8.5).
//  - The central ~75% is a hard early-out `return scene` - it is NEVER read from, tinted, blurred
//    or distorted (§1 / §3 "避免中央畫面持續色偏、模糊或扭曲").
//  - Corners stronger than the side midpoints (§2.3).
//  - Two noise layers, different scale + scroll direction (§3).
//  - No clean rectangle / UI frame / four straight bands - the border is dissolved and pushed
//    in/out by noise, with occasional tongues licking toward centre (§2.10).
//  - Resting distortion is ~0; a brief punch is only added while _Pulse > 0 (§3).
Shader "Live2DAction/VFX/BossDomainScreenVFX"
{
    // Declared so the .mat serialises them and they're inspectable. At runtime BossDomainScreenVFX.cs
    // overrides every one on a per-instance material every frame - these values are only the "off"
    // template (master 0 = the shader early-outs to a straight passthrough).
    Properties
    {
        _MasterIntensity ("Master Intensity", Float) = 0
        _EnterExit ("Enter/Exit Envelope", Range(0,1)) = 0
        _Phase ("Phase", Float) = 1
        _Pulse ("Pulse", Range(0,1)) = 0
        [HDR] _DomainColor ("Domain Colour", Color) = (0.10, 0.85, 0.55, 1)
        _EdgeWidth ("Edge Width (frac of screen height)", Range(0.03,0.30)) = 0.12
        _CornerStrength ("Corner Strength", Range(0,3)) = 1.5
        _FogOpacity ("Fog Opacity", Range(0,1)) = 0.38
        _FlameIntensity ("Flame Intensity", Range(0,3)) = 1.1
        _EmissionSpeed ("Emission Speed", Float) = 0.6
        _NoiseScale ("Noise Scale", Float) = 3.2
        _NoiseSpeed ("Noise Speed", Float) = 0.05
        _DistortionStrength ("Distortion Strength (resting)", Range(0,0.05)) = 0.004
        _RuneIntensity ("Rune Intensity", Range(0,1)) = 0.25
        _BreathPeriod ("Breath Period (s)", Float) = 6.5
        _BreathAmount ("Breath Amount", Range(0,0.4)) = 0.12
        _TimeSeconds ("Time (fed by controller)", Float) = 0
        _HasRuneTex ("Has Rune Texture", Float) = 0
        [NoScaleOffset] _RuneTex ("Rune / Sword Pattern (R)", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off

        Pass
        {
            Name "BossDomainScreenVFX"

            HLSLPROGRAM
            // Same include set as URP's own BlitWithMaterial RenderGraph sample - Core.hlsl sets up
            // the platform texture macros, then core's Blit.hlsl provides Vert / Varyings (.texcoord)
            // / _BlitTexture / sampler_LinearClamp / _BlitMipLevel.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5

            // -------- control parameters (BossDomainScreenVFX.cs, runtime material instance) --------
            float  _MasterIntensity;     // 0 = fully off (early-out), scales the whole effect
            float  _EnterExit;           // 0..1 fade envelope - enter ramps up, exit ramps down
            float  _Phase;               // 1 / 2 / 3 - higher = a touch hotter & greener
            float  _Pulse;               // 0..1 one-shot transient fired on a phase change
            float4 _DomainColor;         // rgb = soul-green emission colour (a unused)
            float  _EdgeWidth;           // border thickness as a fraction of screen HEIGHT
            float  _CornerStrength;      // extra weight at the four corners
            float  _FogOpacity;          // how dark the black soul-mist pushes the border
            float  _FlameIntensity;      // green flame brightness
            float  _EmissionSpeed;       // flame flicker speed
            float  _NoiseScale;          // base noise frequency
            float  _NoiseSpeed;          // base noise scroll speed
            float  _DistortionStrength;  // RESTING edge warp (keep tiny); pulse adds a burst
            float  _RuneIntensity;       // optional rune overlay strength (0 = off)
            float  _BreathPeriod;        // seconds per brightness breath cycle
            float  _BreathAmount;        // breath depth (0..~0.3)
            float  _TimeSeconds;         // controller-fed clock (honours Use Unscaled Time)
            float  _HasRuneTex;          // 1 when _RuneTex is assigned

            TEXTURE2D(_RuneTex);   SAMPLER(sampler_RuneTex);

            // ---------------- procedural value noise / fbm (no texture) ----------------
            float hash21(float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float vnoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            float fbm(float2 p)
            {
                float v = 0.0;
                float amp = 0.5;
                [unroll]
                for (int k = 0; k < 5; k++)
                {
                    v += amp * vnoise(p);
                    p = p * 2.02 + 19.19;
                    amp *= 0.5;
                }
                return v;
            }

            half4 Frag(Varyings input) : SV_Target0
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;

                half4 scene = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);

                float master = saturate(_MasterIntensity) * saturate(_EnterExit);
                if (master <= 0.001)
                    return scene;

                float aspect = _ScreenParams.x / max(_ScreenParams.y, 1.0);
                float t = _TimeSeconds;

                // centred coords: y in [-0.5,0.5], x widened by aspect so the border is a uniform
                // thickness in screen-HEIGHT units on any aspect ratio.
                float2 c = uv - 0.5;
                c.x *= aspect;
                float dx = 0.5 * aspect - abs(c.x);   // distance to nearest L/R border (height units)
                float dy = 0.5 - abs(c.y);            // distance to nearest T/B border
                float edgeDist = min(dx, dy);

                float ew = max(_EdgeWidth, 0.001);

                // --- hard clear centre: never read/modify the middle of the screen ---
                if (edgeDist > ew * 2.4)
                    return scene;

                // --- two scrolling noise layers, different scale + direction (§3) ---
                float2 dir1 = float2(0.55, -1.0);
                float2 dir2 = float2(-1.0, -0.30);
                float n1 = fbm(uv * _NoiseScale          + dir1 * t * _NoiseSpeed);
                float n2 = fbm(uv * (_NoiseScale * 2.35)  + dir2 * t * (_NoiseSpeed * 1.7) + 53.1);
                float flameNoise = saturate(n1 * 0.65 + n2 * 0.45);

                // --- irregular border mask - never a clean rectangle (§2.10) ---
                float baseEdge = 1.0 - smoothstep(0.0, ew, edgeDist);
                // noise pushes the boundary in/out and carves occasional tongues toward centre
                float reach = baseEdge + (flameNoise - 0.45) * 0.85 * smoothstep(ew * 2.4, 0.0, edgeDist);
                float edge = saturate(reach);
                // ragged outer lip
                edge *= smoothstep(0.06, 0.55, flameNoise + edge * 0.6);

                // --- corners hotter than the sides (§2.3) ---
                float cs = ew * 3.0;
                float corner = (1.0 - smoothstep(0.0, cs, dx)) * (1.0 - smoothstep(0.0, cs, dy));
                corner = pow(saturate(corner), 0.7);

                // during ENTER the mist comes from the corners first, then fills the edges (§5)
                float fill = smoothstep(0.12, 0.95, _EnterExit);
                float shownEdge = lerp(corner, saturate(edge + corner * 0.6), fill);

                float breath   = 1.0 + sin(t * (6.28318 / max(_BreathPeriod, 0.5))) * _BreathAmount;
                float phaseHot = 1.0 + (_Phase - 1.0) * 0.28;
                float pulse    = _Pulse * _Pulse;

                // edge-only micro warp: `* shownEdge` guarantees zero warp in the clear centre.
                float warpAmt = (_DistortionStrength + pulse * 0.022) * shownEdge;
                float2 wuv = uv;
                if (warpAmt > 0.00005)
                {
                    float2 w = float2(fbm(uv * 6.0 + t * 0.6),
                                      fbm(uv * 6.0 + 41.0 - t * 0.5)) - 0.5;
                    wuv += w * warpAmt;
                }
                float3 col = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, wuv, _BlitMipLevel).rgb;

                // --- black soul mist / vignette ---
                float mistN = fbm(uv * (_NoiseScale * 0.55) + dir2 * t * (_NoiseSpeed * 0.4) + 9.7);
                float mist  = saturate(shownEdge * (0.5 + 0.6 * mistN)) * _FogOpacity * master;
                col = lerp(col, col * 0.055, saturate(mist));

                // --- green soul flame (added, so it can feed Bloom; also carries its own ridge glow) ---
                float flameShape = saturate(edge * (0.35 + corner * _CornerStrength));
                float flame = flameShape * flameNoise * _FlameIntensity * breath * phaseHot * master;
                flame *= 0.72 + 0.28 * sin(t * _EmissionSpeed * 6.0 + (uv.x + uv.y) * 27.0 + flameNoise * 8.0);
                flame = max(flame, 0.0);

                // phase-change: a single green wave sweeps inward, plus a whole-border flare
                float waveFront = ew * (1.0 + _Pulse * 9.0);
                float wave = (1.0 - smoothstep(waveFront - ew, waveFront, edgeDist))
                           * (1.0 - smoothstep(waveFront, waveFront + ew, edgeDist));
                flame += wave * pulse * _FlameIntensity * 1.5 * master;
                flame += edge * pulse * _FlameIntensity * 0.8 * master;

                float3 flameCol = _DomainColor.rgb * flame;

                // thin bright sword-scars along the noise ridges, licking inward (§2.4/2.5, subtle)
                float ridge = 1.0 - abs(flameNoise * 2.0 - 1.0);
                ridge = pow(saturate(ridge), 6.0);
                flameCol += _DomainColor.rgb * ridge * edge * _FlameIntensity * 0.5 * breath * master;

                // sparse upward-drifting green ashes, border only, no strobing
                float2 ashUv = uv * float2(aspect, 1.0) * 40.0;
                ashUv.y += t * 1.2;
                float ashCell = hash21(floor(ashUv));
                float ash = step(0.986, ashCell) * (0.4 + 0.6 * frac(sin(t * 2.7 + ashCell * 40.0) * 0.5 + 0.5));
                flameCol += _DomainColor.rgb * ash * edge * 1.1 * master;

                // optional ancient rune / sword pattern - rare (visible ~1/8 of the time), faint
                if (_HasRuneTex > 0.5 && _RuneIntensity > 0.001)
                {
                    float2 rUv = uv * float2(aspect, 1.0) * 1.5 + dir1 * t * 0.01;
                    float rune = SAMPLE_TEXTURE2D(_RuneTex, sampler_RuneTex, rUv).r;
                    float appear = saturate(sin(t * 0.13) * 3.0 - 2.1);
                    flameCol += _DomainColor.rgb * rune * edge * appear * _RuneIntensity * breath * master;
                }

                col += flameCol;

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
