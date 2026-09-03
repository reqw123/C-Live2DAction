Shader "Live2DAction/PortalVideoAlphaURP"
{
    // 2026-09-03 - for a portal video that already contains the whole gate (frame + vortex) on a
    // pure-black background, e.g. VoidmoonGateVideo.mp4. Alpha-blends: keys out only the near-black
    // surround (so the quad has no visible border) and shows everything else - the dark gate frame
    // included - at full opacity, unlike the additive PortalVideoURP which would wash the dark
    // frame out. _PortalFade (0..1) drives the proximity fade-in from PortalVideoSurface.
    Properties
    {
        [MainTexture] _BaseMap ("Video (RenderTexture)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1.0
        _KeyLow ("Key Low (fully transparent)", Range(0,1)) = 0.015
        _KeyHigh ("Key High (fully opaque)", Range(0,1)) = 0.06
        _EdgeFade ("Quad Edge Fade (uv)", Range(0,0.5)) = 0.04
        _PortalFade ("Portal Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings   { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _Tint;
                float _Intensity;
                float _KeyLow;
                float _KeyHigh;
                float _EdgeFade;
                float _PortalFade;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                half luma = max(c.r, max(c.g, c.b));
                half a = smoothstep(_KeyLow, _KeyHigh, luma);

                float2 d = min(IN.uv, 1.0 - IN.uv);
                float edge = saturate(min(d.x, d.y) / max(_EdgeFade, 1e-4));
                a *= edge * saturate(_PortalFade);

                return half4(c * _Tint.rgb * _Intensity, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
