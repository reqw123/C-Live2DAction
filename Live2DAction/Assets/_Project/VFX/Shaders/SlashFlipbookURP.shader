// 2026-08-24, explicit user request ("將我提供的三段攻擊特效影片...製作成可用於 3D 動作遊戲的 2.5D
// VFX 技能特效...Unity 端使用 Shader Graph + Particle System 實作 Flipbook 動畫") - the project has
// NO Shader Graph package installed (confirmed via manage_packages.list_packages - only
// com.unity.render-pipelines.universal is present, no com.unity.shadergraph), and there is no
// tool available in this environment that can programmatically author a .shadergraph node file
// (manage_shader only creates/edits plain HLSL text) - hand-authoring that JSON format blind,
// with no way to open/verify it in the actual Shader Graph editor, risks shipping a broken/pink
// asset with no way to catch it before the user opens the Editor. This hand-written URP HLSL
// shader delivers the same feature set Shader Graph would have (flipbook via Particle System's
// own Texture Sheet Animation module driving per-vertex UVs - no in-shader tiling math needed;
// runtime-switchable Additive/Alpha blend; HDR-range emission so URP Bloom picks it up) and
// matches every other shader already in this project (LightPillarURP/HealthEnergyFlowUI/
// AdditiveUnlit - all hand-written HLSL, zero Shader Graph usage anywhere in this project's
// history), so it's consistent with the established convention rather than a one-off deviation.
//
// Runtime blend-mode switching (Additive vs Alpha Blend) without needing two shader variants:
// _SrcBlend/_DstBlend are exposed as float properties referenced directly in the ShaderLab Blend
// command (a standard Unity technique) - PlayerHealthBarFx-style scripts, or just
// Material.SetFloat, can flip a material between the two at runtime/from the Inspector via the
// _BlendMode enum below, which SlashVfxController applies on Awake.
Shader "Live2DAction/VFX/SlashFlipbook"
{
    Properties
    {
        [PerRendererData] _MainTex ("Flipbook Sprite Sheet", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        // Multiplies the sampled color's RGB before output - push above 1 to feed URP Bloom
        // (Bloom thresholds against the final HDR color, so this is the ONLY thing this shader
        // needs to do for "HDR/Bloom 相容" - actually enabling Bloom itself is a Volume/URP
        // Renderer Feature setting, deliberately left untouched here per "避免修改既有戰鬥系統":
        // that's a project-wide render-settings change, not something an isolated VFX shader
        // should silently flip on).
        _Brightness ("Brightness / HDR Emission Intensity", Float) = 1.5
        // Independent of _Color.a and _Brightness - a single "fade the whole effect" knob
        // SlashVfxController's own opacity field writes here, so runtime fade-out doesn't have
        // to fight with the tint color's own alpha.
        _Opacity ("Opacity Multiplier", Range(0, 1)) = 1

        // Set both to 1 (One/One) for Additive, or SrcBlend=5 (SrcAlpha)/DstBlend=10
        // (OneMinusSrcAlpha) for standard Alpha Blend - SlashVfxSetup's EnsureMaterial sets these
        // directly from a plain bool per material; no ShaderGUI is written for this project, so a
        // [Enum] dropdown here would just be decorative without one actually translating it into
        // these two values - plain floats instead, driven by script.
        _SrcBlend ("Src Blend Factor (1=One, 5=SrcAlpha)", Float) = 1
        _DstBlend ("Dst Blend Factor (1=One, 10=OneMinusSrcAlpha)", Float) = 1
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        Cull Off
        ZWrite Off
        Blend [_SrcBlend] [_DstBlend]

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
            float _Brightness;
            float _Opacity;

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                // Particle System's Texture Sheet Animation module already writes the correct
                // per-frame tile offset/scale into this mesh's own UV0 stream every frame (that's
                // the actual "flipbook" mechanism - nothing here needs to compute it) -
                // TRANSFORM_TEX still applied on top so the material's own Tiling/Offset fields
                // keep working for anyone who wants to hand-tweak framing.
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                // Particle color-over-lifetime (alpha fade-out etc, set on the ParticleSystem's
                // Main/Color-over-Lifetime modules) arrives here - multiplied in below alongside
                // the material's own _Color/_Opacity/_Brightness knobs.
                OUT.color = IN.color;
                return OUT;
            }

            float4 Frag(Varyings IN) : SV_Target
            {
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                float4 color = tex * _Color * IN.color;
                color.rgb *= _Brightness;
                color.a *= _Opacity;

                // 2026-08-24, real bug found via in-Play-Mode screenshot ("Glow 顯示成一個實心方
                // 塊而不是柔和圓點") - Additive blend (Blend One One, used by every material this
                // shader ships with by default) does NOT read the alpha channel as a blend factor
                // at all, so a texture with correct alpha but constant/flat RGB (SoftDot.png is
                // solid white everywhere, only alpha carries the falloff) was contributing its
                // FULL RGB across the whole quad regardless of alpha, reading as a hard-edged
                // solid box instead of a soft dot. Premultiplying RGB by alpha here makes the
                // falloff live in the RGB channel too, so Additive (One/One, which ignores alpha)
                // and premultiplied Alpha Blend (One/OneMinusSrcAlpha - see SlashVfxSetup's own
                // AlphaSrc/AlphaDst comment for why it's One, not SrcAlpha) both composite
                // correctly from the exact same shader output.
                color.rgb *= color.a;
                return color;
            }
            ENDHLSL
        }
    }
}
