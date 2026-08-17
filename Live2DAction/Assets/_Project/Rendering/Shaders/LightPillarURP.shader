// 2026-08-17, explicit user request ("把傳送門從平面做成立體綠色光柱") - a solid vertical light
// column for the sky-island ("鳥居") portal, replacing PortalVortexURP's flat swirl disc for that
// one portal specifically. Fresnel-driven (grazing-angle silhouette glows brighter/more opaque
// than the face-on center) so a plain Cylinder primitive reads as a glowing light VOLUME rather
// than a flat-shaded tube - the standard cheap "light beam" technique (no texture, no particles
// required, though Portal.cs still adds a ParticleSystem alongside this for extra motion).
Shader "Live2DAction/LightPillarURP"
{
    Properties
    {
        _Color("Pillar Color", Color) = (0.15, 1.0, 0.35, 1)
        _FresnelPower("Fresnel Power", Range(0.5, 6)) = 2.5
        _CoreAlpha("Face-On Alpha", Range(0, 1)) = 0.22
        _RimAlpha("Silhouette Alpha", Range(0, 1)) = 0.95
        _ScrollSpeed("Vertical Scroll Speed", Float) = 1.6
        _BandFrequency("Rising Band Frequency", Float) = 7
        _TopFadeStart("Top Fade Start (0-1 height)", Range(0, 1)) = 0.55
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
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 positionWS  : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float2 uv          : TEXCOORD2;
                float  localY01    : TEXCOORD3;
            };

            float4 _Color;
            float _FresnelPower;
            float _CoreAlpha;
            float _RimAlpha;
            float _ScrollSpeed;
            float _BandFrequency;
            float _TopFadeStart;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = IN.uv;
                // Unity's built-in Cylinder primitive spans local Y -1..1 - remap to 0 (base) .. 1
                // (top) so the top-fade math below doesn't need to know that range itself.
                OUT.localY01 = saturate((IN.positionOS.y + 1.0) * 0.5);
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);
                float3 normal = normalize(IN.normalWS);
                // Grazing angle (silhouette edge, normal near-perpendicular to view) -> 1;
                // face-on (normal pointing at camera) -> 0. Classic light-volume fresnel.
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);

                // Rising energy bands scrolling up the pillar over time - breaks up the fresnel
                // gradient's otherwise-static look, reads as energy flowing upward through it.
                float band = sin(IN.uv.y * _BandFrequency - _Time.y * _ScrollSpeed) * 0.5 + 0.5;

                float alpha = lerp(_CoreAlpha, _RimAlpha, fresnel);
                alpha *= lerp(0.7, 1.0, band);

                // Fade out near the top so the beam dissipates into the sky instead of ending in
                // a hard flat cap.
                float topFade = 1.0 - smoothstep(_TopFadeStart, 1.0, IN.localY01);
                alpha *= topFade;

                float3 color = _Color.rgb * (1.0 + fresnel * 0.6);
                return float4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
