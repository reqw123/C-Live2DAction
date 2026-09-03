Shader "Live2DAction/GroundStrikeURP"
{
    // 2026-09-03 - ground AoE-telegraph decal. Was a RenderTexture(VideoPlayer) feed; runtime-
    // spawned VideoPlayers render black on this D3D11 box (same class of bug as APIOnly dropping
    // the red channel), so the source clip (紅圈攻擊特效.mp4) is now baked to a 6x6 flipbook atlas
    // (RedCircleStrike_Flip.png, 36 frames) and a script drives _Frame. The clip is a rendered
    // scene with a dark STONE FLOOR baked in (not clean black/alpha) so this keys on "bright OR
    // saturated-red" (the rune circle + fire pillar) and drops the desaturated floor. Additive so
    // it glows on the real ground.
    Properties
    {
        [MainTexture] _BaseMap ("Flipbook Atlas", 2D) = "black" {}
        _Cols ("Atlas Columns", Float) = 6
        _Rows ("Atlas Rows", Float) = 6
        _Frame ("Current Frame", Float) = 0
        _Tint ("Tint", Color) = (1, 0.30, 0.16, 1)
        _Intensity ("Intensity (RGB gain)", Float) = 1.5
        _Opacity ("Opacity", Range(0,3)) = 1.6
        _FloorCut ("Floor Luma Cut", Range(0,1)) = 0.20
        _KeyWidth ("Key Softness", Range(0.01,0.6)) = 0.30
        _RedBoost ("Saturated-Red Boost", Float) = 2.0
        _AddBright ("Additive Hot-Core Threshold", Range(0,2)) = 0.72
        _EdgeFade ("Quad Edge Fade (uv)", Range(0,0.5)) = 0.06
        _Fade ("Global Fade", Range(0,1)) = 1
        // circular crop - the atlas tile is a rectangle with a dark stone floor baked into the
        // corners; mask everything outside a disc so only the round rune circle shows.
        _MaskCenterY ("Circle Mask Centre Y", Range(0,1)) = 0.5
        _MaskRadius ("Circle Mask Radius (uv)", Range(0.1,0.8)) = 0.5
        _MaskSoft ("Circle Mask Softness", Range(0.001,0.3)) = 0.09
        _MaskAspectY ("Circle Mask Y Squash (art is an ellipse)", Range(0.5,2)) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        LOD 100
        // alpha-blend so the circle actually PAINTS the ground (readable on a bright sunlit plaza -
        // pure-additive washed out to nothing). The hot core (pillar/sparks) still adds on top via
        // premultiplied output: rgb carries key*extra so bright bits punch through.
        Blend One OneMinusSrcAlpha
        ZWrite Off
        // normal depth test so the PLAYER (opaque, drawn first) occludes the decal when standing on
        // it; polygon offset pulls the decal toward the camera just enough to beat z-fighting with
        // the floor without needing ZTest Always (which also drew over the player - user complaint).
        ZTest LEqual
        Offset -1, -1
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
                float _Cols;
                float _Rows;
                float _Frame;
                float4 _Tint;
                float _Intensity;
                float _Opacity;
                float _FloorCut;
                float _KeyWidth;
                float _RedBoost;
                float _AddBright;
                float _EdgeFade;
                float _Fade;
                float _MaskCenterY;
                float _MaskRadius;
                float _MaskSoft;
                float _MaskAspectY;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // flipbook tile - frame 0 is TOP-LEFT of the atlas (ffmpeg tile=6x6 order),
                // Unity uv.v=0 is the bottom, so flip the row.
                float cols = max(_Cols, 1.0);
                float rows = max(_Rows, 1.0);
                float f = floor(_Frame + 0.001);
                float col = fmod(f, cols);
                float row = floor(f / cols);
                float2 tile = float2(1.0 / cols, 1.0 / rows);
                float2 uv = IN.uv * tile + float2(col * tile.x, (rows - 1.0 - row) * tile.y);

                half3 c = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half luma = max(c.r, max(c.g, c.b));
                half redExcess = max(0.0h, c.r - max(c.g, c.b));

                half key = saturate((luma - _FloorCut) / max(_KeyWidth, 1e-4));
                key = saturate(key + redExcess * _RedBoost);

                float2 d = min(IN.uv, 1.0 - IN.uv);
                float edge = saturate(min(d.x, d.y) / max(_EdgeFade, 1e-4));

                // circular crop: distance from the disc centre, in uv, with an optional Y squash
                // for the perspective ellipse in the source art.
                float2 mc = float2(IN.uv.x - 0.5, (IN.uv.y - _MaskCenterY) * _MaskAspectY);
                float rd = length(mc);
                float circle = 1.0 - smoothstep(_MaskRadius - _MaskSoft, _MaskRadius, rd);

                half k = key * edge * circle * saturate(_Fade);

                // base: paint the ground with the tinted circle (alpha blend via premultiplied rgb).
                half a = saturate(k * _Opacity);
                half3 rgb = _Tint.rgb * _Intensity * a;

                // hot core: the pillar / sparks (luma above _AddBright) also ADD on top - premult
                // output with Blend One OneMinusSrcAlpha means extra rgb with no extra alpha = additive.
                half hot = saturate((luma - _AddBright) / 0.3h) * k;
                rgb += c * _Intensity * hot * 1.5h;

                return half4(rgb, a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
