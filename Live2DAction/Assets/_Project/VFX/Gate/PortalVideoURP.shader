Shader "Live2DAction/PortalVideoURP"
{
    // 2026-09-03 - SceneGate portal surface. Samples a RenderTexture fed by a looping mp4
    // (VideoPlayer) and keys out the near-black background so the rectangular quad has no
    // visible border - only the glowing swirl shows. Additive (Blend One One): black pixels
    // contribute nothing, so there is no grey pedestal / "grey box" the earlier attempts had.
    Properties
    {
        [MainTexture] _BaseMap ("Video (RenderTexture)", 2D) = "black" {}
        _Tint ("Tint", Color) = (1,1,1,1)
        _Intensity ("Intensity", Float) = 1.6
        _KeyLow ("Key Low (fade start)", Range(0,1)) = 0.05
        _KeyHigh ("Key High (fully lit)", Range(0,1)) = 0.32
        _EdgeFade ("Quad Edge Fade (uv)", Range(0,0.5)) = 0.12
        _PortalFade ("Portal Fade", Range(0,1)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        Blend One One
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

                // soften the rectangular quad edges so there is no hard seam
                float2 d = min(IN.uv, 1.0 - IN.uv);
                float edge = saturate(min(d.x, d.y) / max(_EdgeFade, 1e-4));
                a *= edge * saturate(_PortalFade);

                half3 outCol = c * _Tint.rgb * _Intensity * a;
                return half4(outCol, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
