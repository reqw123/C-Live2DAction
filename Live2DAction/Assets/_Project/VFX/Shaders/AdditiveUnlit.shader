// Minimal additive-blend unlit shader for VFX flipbooks (2026-08-13, real bug report -
// "動畫又看不見了"). Exists purely because Universal Render Pipeline/Particles/Unlit's
// _SrcBlend/_DstBlend, when forced to One/One via script to bypass its "Additive" dropdown's
// real mapping (SrcAlpha/One - see Attack3SlashEffectSetup's CreateOrUpdateMaterial history),
// gets silently RESET back to that dropdown's canonical values the next time its custom
// ShaderGUI revalidates the material (confirmed: just reopening the Editor was enough to flip
// it back and wash the effect back out). `Blend One One` here is a hardcoded part of the pass
// declaration, not a runtime-editable property pair driven by any dropdown/GUI logic, so
// there is nothing left for any revalidation pass to "helpfully" overwrite.
Shader "Live2DAction/VFX/AdditiveUnlit"
{
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Blend One One
            ZWrite Off
            ZTest LEqual
            Cull Off

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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            float4 _BaseMap_ST;
            float4 _BaseColor;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Shuriken's Texture Sheet Animation module writes the current frame's UV
                // offset/scale directly into the mesh's own UV0 stream per-particle - no
                // shader-side frame math needed, TRANSFORM_TEX (the material's own _ST tiling)
                // is the only transform this shader applies on top of that.
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.color = IN.color;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                return tex * _BaseColor * IN.color;
            }
            ENDHLSL
        }
    }
}
