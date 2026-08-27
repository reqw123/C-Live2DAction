// 2026-08-23, explicit user request ("血條內部加入持續流動的紅色能量/電流效果 優先使用 Shader/UV
// Flow/Noise 不要用大量序列動畫") - drives the health fill's moving current-of-energy look
// entirely from a 2-octave value-noise scroll in the fragment shader (no noise texture asset,
// no per-frame sprite swap), plus runtime-driven uniforms PlayerHealthBarFx pushes every frame:
// _GlowIntensity (low-HP pulse, ramped by HealthBarTweenUtility.ComputeLowHealthIntensity and
// briefly spiked on hit) and _FlashIntensity (whites the fill out for a beat right after taking
// damage, decays back to 0). All default to their no-op values so the material looks like a
// plain flat-tinted fill if nothing ever sets them.
//
// 2026-08-23 follow-up, explicit user request (reference mockup: "血量越低,能量流動越強、越不穩定"
// / "70~30%: 流動加快,輕微電流分叉" / "30~0%: 流動劇烈,強烈閃爍與扭曲") - added _HpRatio (the
// live fill ratio, distinct from _GlowIntensity: this one drives SPEED/DISTORTION/FLICKER, not
// brightness, so the two effects can be tuned independently) and _SpeedBoost (a separate runtime
// spike PlayerHealthBarFx briefly raises right on a hit, for "能量層短暫加速" - decays back to 0
// same as _FlashIntensity does). Both tiers described in the mockup are continuous functions of
// _HpRatio (smoothstep-shaped, breakpoints at 0.7/0.3) rather than hard branches, so there's no
// visible pop crossing 70%/30% - just a curve that happens to sit flat above 70%.
//
// 2026-08-24 follow-up, explicit user request ("把途中的ui結構分層 把各階層圖扣下來 作層次渲染") -
// _MainTex is now the actual "05 能量流動層" artwork cropped straight out of the reference mockup
// (see PlayerHealthBarFxSetup.BakeArt), not a plain white mask - the flow mechanism switched from
// pure procedural noise to literally scrolling THAT texture's UV (same "UV -> Time*Speed ->
// Offset -> Sample" mechanism as the earlier UVScrollDemo.shader tutorial), with the value-noise
// kept on only as a secondary UV-jitter distortion for the low-HP "扭曲" tier, not as the primary
// visual anymore. Texture Wrap Mode = Repeat (set at import) is what makes the scroll loop.
//
// URP HLSL (Core.hlsl) rather than a CGPROGRAM UI/Default derivative to match every other
// shader already in this project (see LightPillarURP.shader/AdditiveUnlit.shader) - Screen
// Space Overlay Canvas rendering doesn't need a "LightMode" pass tag (it's drawn outside the
// SRP's per-camera render loop), so this only needs a single untagged Pass.
//
// 2026-08-25, explicit user request ("接下來以這樣圖渲染能量條 (所有具有能量機制的共用)") - reused
// as-is for UltimateEnergyBarFx (same UV-scroll/instability mechanism, "能量越低越不穩定" maps onto
// the exact same _HpRatio-driven curve as "血量越低越不穩定"), but the low-resource glow used to be
// a HARDCODED reddish tint (float3(0.35,0.05,0.02)) baked for the health bar specifically - added
// _GlowColor so energy bars can push purple instead without forking the shader. Defaults to that
// same reddish value, so every existing health-bar material instance (Player/076/屁孩王's HP bars)
// renders identically unless something explicitly sets _GlowColor otherwise.
Shader "Live2DAction/UI/HealthEnergyFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _GlowColor ("Low-Resource + Hit Glow Color", Color) = (0.35, 0.05, 0.02, 1)
        _FlowSpeed ("Flow Speed (100-70% baseline)", Float) = 1.5
        _NoiseScale ("Noise Scale", Float) = 6
        _GlowIntensity ("Low-Resource + Hit Glow (runtime)", Range(0, 1)) = 0
        _FlashIntensity ("Damage/Activation Flash (runtime)", Range(0, 1)) = 0
        _HpRatio ("Fill Ratio (runtime)", Range(0, 1)) = 1
        _SpeedBoost ("Hit/Activation Speed Boost (runtime)", Range(0, 2)) = 0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "CanUseSpriteAtlas" = "True" }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;
            float4 _Color;
            float4 _GlowColor;
            float _FlowSpeed;
            float _NoiseScale;
            float _GlowIntensity;
            float _FlashIntensity;
            float _HpRatio;
            float _SpeedBoost;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            // Bilinear value noise - two octaves of this (see Frag below) is enough to read as
            // a crackling current, without needing an authored noise texture asset.
            float ValueNoise(float2 uv)
            {
                float2 i = floor(uv);
                float2 f = frac(uv);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // 0 above 70% HP (flat - "流動穩定,速度正常"), ramping to 1 at 0% HP - the single
                // driver behind the speed/distortion/flicker tiers below. Squared rather than
                // linear so the 70-30% band reads as a gentle ramp ("輕微電流分叉") and most of
                // the intensity is held back for the 30-0% band ("流動劇烈,強烈閃爍與扭曲").
                float unstable = saturate((0.7 - _HpRatio) / 0.7);
                unstable = unstable * unstable;

                // --- UV Scroll mechanism: UV -> Time x Speed -> Offset -> Texture Sampling ---
                float speedMul = 1.0 + unstable * 1.8 + _SpeedBoost;
                float2 flowUv = IN.uv;
                flowUv.x -= _Time.y * _FlowSpeed * speedMul;

                // UV distortion (the "扭曲" in the 30-0% tier) - a noise field jitters the
                // sample's V coordinate, using its own faster time scroll so it reads as
                // crackling instability rather than just a wavier version of the same scroll.
                float2 distortUv = IN.uv * float2(_NoiseScale * 2.3, _NoiseScale * 0.9) + float2(_Time.y * 1.7, 0.0);
                float jitter = (ValueNoise(distortUv) - 0.5) * unstable * 0.05;
                flowUv.y += jitter;

                float4 baseTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, flowUv);
                // --- end UV Scroll mechanism ---

                // Fast strobe multiplier, only present once "unstable" has actually ramped up -
                // the "強烈閃爍" (strong flicker) called for specifically in the 30-0% tier.
                float flicker = 1.0 - unstable * 0.35 * (0.5 + 0.5 * sin(_Time.y * 45.0));

                float4 color = baseTex * _Color * IN.color;
                color.rgb *= flicker * (1.0 + unstable * 0.6 + _GlowIntensity * 0.5);
                color.rgb += _GlowIntensity * _GlowColor.rgb * (0.5 + 0.5 * sin(_Time.y * 6.0));
                color.rgb = lerp(color.rgb, float3(1.0, 1.0, 1.0), _FlashIntensity * 0.85);

                return color;
            }
            ENDHLSL
        }
    }
}
