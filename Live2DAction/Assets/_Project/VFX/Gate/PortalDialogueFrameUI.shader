Shader "Live2DAction/UI/PortalDialogueFrame"
{
    // 2026-09-06 - the Boss-map portal interaction prompt (對話系統ui框.mp4). The clip is a H.264
    // silver dialogue frame + blue-white energy edges on a SOLID BLACK background (no real alpha),
    // so a plain RawImage would paint a black rectangle over the 3D scene. This keys the near-black
    // pixels to transparent (low threshold so the mid-luma SILVER frame survives), crops the outer
    // UV border to trim the stray starfield outside the frame, and adds a mild additive lift only
    // on the bright BLUE energy lines. The dark readable panel behind the text is a SEPARATE
    // Image (DarkBackdrop) - this shader deliberately does not try to also be that panel (spec 三).
    //
    // Single untagged URP HLSL pass on a Screen Space - Overlay canvas, same as
    // Live2DAction/UI/HealthEnergyFlow (overlay canvas draws outside the SRP camera loop, so no
    // LightMode tag is needed). Standard alpha blend - NOT pure additive - so DarkBackdrop reads
    // through underneath it.
    Properties
    {
        [PerRendererData] _MainTex ("Video RT", 2D) = "black" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _BlackThreshold ("Black Threshold (key start)", Range(0,1)) = 0.055
        _Softness ("Softness (key ramp width)", Range(0.001,0.6)) = 0.14
        _Intensity ("Intensity", Float) = 1.05
        _GlowBoost ("Blue Energy Additive Boost", Range(0,2)) = 0.35
        _CropInset ("Crop Inset (uv from edge)", Range(0,0.45)) = 0.055
        _CropSoftness ("Crop Softness (uv)", Range(0.001,0.3)) = 0.05
        _MasterAlpha ("Master Alpha", Range(0,1)) = 1

        // UI stencil plumbing (lets the frame sit inside a Mask if one is ever added). Defaults are no-ops.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
            "CanUseSpriteAtlas" = "True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        ZWrite Off
        ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

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
            float _BlackThreshold;
            float _Softness;
            float _Intensity;
            float _GlowBoost;
            float _CropInset;
            float _CropSoftness;
            float _MasterAlpha;

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
                half3 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).rgb;
                half luma = max(c.r, max(c.g, c.b));

                // Key near-black to transparent. Low threshold keeps the mid-luma silver frame.
                half a = smoothstep(_BlackThreshold, _BlackThreshold + _Softness, luma);

                // Trim the rectangular UV border (stray starfield / dust outside the frame art).
                // _CropInset <= 0 fully disables this (use the RawImage uvRect to crop instead,
                // which keeps the shader's 0..1 edge math from clipping a tight crop).
                if (_CropInset > 0.0001)
                {
                    float2 d = min(IN.uv, 1.0 - IN.uv);
                    a *= smoothstep(_CropInset, _CropInset + _CropSoftness, min(d.x, d.y));
                }

                a *= _MasterAlpha * _Color.a * IN.color.a;

                half3 rgb = c * _Color.rgb * IN.color.rgb * _Intensity;

                // Mild additive lift ONLY where blue clearly dominates and the pixel is bright -
                // the glowing energy edge, not the silver frame or the dark background.
                half blueDom = saturate((c.b - max(c.r, c.g)) * 3.0) * saturate((luma - 0.35) * 2.0);
                rgb += c * blueDom * _GlowBoost;

                return half4(rgb, saturate(a));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
