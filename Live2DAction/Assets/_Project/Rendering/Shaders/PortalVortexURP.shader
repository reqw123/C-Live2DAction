// 2026-08-17, explicit user request ("優化傳送門的外觀, 搜尋網路上精美傳送門的作法") - a procedural
// swirling-vortex disc shader for the portal pads, following the standard "portal shader" recipe
// (polar-coordinate remap -> spiral distortion -> time animation) described in community
// breakdowns of this exact effect (see https://www.cyanilux.com/tutorials/portal-shader-breakdown/
// and https://www.patreon.com/posts/portal-effect-88069142 - both build the swirl from polar
// coordinates the same way, just via Shader Graph nodes instead of hand-written HLSL). Written by
// hand instead of as a Shader Graph asset to match this project's existing custom-shader
// convention (see CubismUnlitURP.shader, the only other custom shader in the project) and because
// a Shader Graph's .shadergraph asset is a binary/JSON node graph that has to be built by hand in
// the Editor's visual node tool - not something scriptable the way a plain HLSL file is.
//
// Deliberately noise-texture-free (most tutorials sample a tileable noise texture for the spiral
// detail) - generating/importing a texture asset isn't scriptable here either, so the spiral
// pattern is instead built entirely from a sine wave over (angle*armCount - radius*frequency),
// which produces the same "rotating spiral arms" read without any texture dependency.
Shader "Live2DAction/PortalVortexURP"
{
    Properties
    {
        _ColorA("Swirl Color A (core)", Color) = (0.55, 0.15, 1.0, 1)
        _ColorB("Swirl Color B (arms)", Color) = (0.15, 0.85, 1.0, 1)
        _RimColor("Rim Glow Color", Color) = (0.85, 0.95, 1.0, 1)
        _ArmCount("Spiral Arm Count", Float) = 4
        _SwirlStrength("Swirl Twist Strength", Float) = 2.2
        _RadialFrequency("Radial Band Frequency", Float) = 9
        _RotationSpeed("Rotation Speed", Float) = 1.4
        _FlowSpeed("Inward Flow Speed", Float) = 2.5
        _EdgeSoftness("Outer Edge Softness", Range(0.01, 0.5)) = 0.12
        _RimWidth("Rim Width", Range(0.01, 0.5)) = 0.16
        _BaseAlpha("Base Alpha", Range(0, 1)) = 0.55
        _SwirlAlphaBoost("Swirl Alpha Boost", Range(0, 1)) = 0.4
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
            };

            float4 _ColorA;
            float4 _ColorB;
            float4 _RimColor;
            float _ArmCount;
            float _SwirlStrength;
            float _RadialFrequency;
            float _RotationSpeed;
            float _FlowSpeed;
            float _EdgeSoftness;
            float _RimWidth;
            float _BaseAlpha;
            float _SwirlAlphaBoost;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                // Polar remap: centered UV (-0.5..0.5), radius 0 at the disc's middle to ~1 at
                // its rim (mesh is expected to be a unit-UV disc/cylinder cap, e.g. the built-in
                // Cylinder primitive's top face).
                float2 centered = IN.uv - 0.5;
                float radius = length(centered) * 2.0;
                float angle = atan2(centered.y, centered.x);

                // Classic vortex twist: angular offset grows sharply as radius shrinks (1/r,
                // clamped so the singularity at the exact center doesn't spin infinitely fast/
                // flicker) - this is the same "spiral inward" read every portal-shader writeup
                // above builds via Polar Coordinates + Twirl, just computed directly here instead
                // of through Shader Graph nodes.
                float safeRadius = max(radius, 0.12);
                float twist = _SwirlStrength / safeRadius;
                float spiralPhase = angle * _ArmCount + twist - radius * _RadialFrequency
                    + _Time.y * _RotationSpeed - _Time.y * _FlowSpeed;

                float arms = sin(spiralPhase) * 0.5 + 0.5;

                // Second, slower/coarser sine layer so the pattern doesn't read as a single flat
                // frequency - breaks up any obvious repetition from the first layer alone.
                float arms2 = sin(spiralPhase * 0.5 - _Time.y * _RotationSpeed * 0.6) * 0.5 + 0.5;
                float swirl = saturate(arms * 0.65 + arms2 * 0.35);

                float3 swirlColor = lerp(_ColorA.rgb, _ColorB.rgb, swirl);

                // Bright energy core at the very center, independent of the spiral pattern - most
                // portal shaders keep a hot, near-white core the swirl arms emerge from.
                float core = saturate(1.0 - radius * 2.2);
                swirlColor += _ColorA.rgb * core * core * 1.5;

                // Glowing rim band just inside the outer edge.
                float rim = smoothstep(1.0 - _RimWidth, 1.0, radius) * (1.0 - smoothstep(1.0, 1.0 + _EdgeSoftness, radius));
                float3 finalColor = swirlColor + _RimColor.rgb * rim;

                // Soft circular falloff so the mesh's actual (square-ish) UV bounds don't show as
                // hard corners - fully opaque well inside the disc, fading to nothing past radius 1.
                float discMask = 1.0 - smoothstep(1.0 - _EdgeSoftness, 1.0, radius);
                float alpha = discMask * saturate(_BaseAlpha + swirl * _SwirlAlphaBoost + rim);

                return float4(finalColor, alpha);
            }
            ENDHLSL
        }
    }
}
