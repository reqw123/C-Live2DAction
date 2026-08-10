// URP-compatible stand-in for Cubism's built-in "Live2D Cubism/Unlit" shader (which is
// written against Built-in RP's CGPROGRAM pipeline and does not render under URP).
// Mask/clipping is intentionally not implemented - unmasked models only. See KNOWN_ISSUES.md.
Shader "Live2DAction/CubismUnlitURP"
{
    Properties
    {
        [PerRendererData] _MainTex("Main Texture", 2D) = "white" {}
        [PerRendererData] cubism_ModelOpacity("Model Opacity", Float) = 1
        [PerRendererData] cubism_MultiplyColor("Multiply Color", Color) = (1, 1, 1, 1)
        [PerRendererData] cubism_ScreenColor("Screen Color", Color) = (0, 0, 0, 1)

        _SrcColor("Source Color", Float) = 5
        _DstColor("Destination Color", Float) = 10
        _SrcAlpha("Source Alpha", Float) = 5
        _DstAlpha("Destination Alpha", Float) = 10
        _Cull("Culling", Float) = 0
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

        Cull [_Cull]
        ZWrite Off
        Blend [_SrcColor][_DstColor], [_SrcAlpha][_DstAlpha]

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
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            float cubism_ModelOpacity;
            float4 cubism_MultiplyColor;
            float4 cubism_ScreenColor;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float4 textureColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                textureColor.rgb *= cubism_MultiplyColor.rgb;
                textureColor.rgb = (textureColor.rgb + cubism_ScreenColor.rgb) - (textureColor.rgb * cubism_ScreenColor.rgb);

                float4 outColor = textureColor * IN.color;
                outColor.a *= cubism_ModelOpacity;
                return outColor;
            }
            ENDHLSL
        }
    }
}
