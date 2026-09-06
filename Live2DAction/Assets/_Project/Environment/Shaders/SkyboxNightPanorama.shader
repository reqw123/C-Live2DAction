// 2026-09-06, user: use rogland_clear_night_4k.exr (Poly Haven "Rogland Clear Night", CC0) as the
// yuanpei_LogoSky boss-fight sky. Downscaled to 2K (rogland_clear_night_2k.exr). The source is a
// full 360 environment - its lower half is a dark desert-hills landscape, not empty sky - and the
// user asked to darken the lower hemisphere IN THE MATERIAL (the boss arena is a walled school
// plaza, so only the upper sky reads; the desert horizon would clash if it poked over the walls).
//
// Compact equirectangular (lat/long) skybox in legacy CG (same style as Unity's stock
// Skybox/Panoramic - renders fine under URP): exposure + tint + Y-rotation + a horizon-down darken
// gradient.
Shader "Live2DAction/Environment/SkyboxNightPanorama"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (lat/long, HDR)", 2D) = "grey" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1)
        [Gamma] _Exposure ("Exposure", Range(0, 4)) = 0.65
        _Rotation ("Rotation (deg)", Range(0, 360)) = 0
        _HorizonDarken ("Horizon Darken (below)", Range(0, 1)) = 0.9
        _HorizonHeight ("Horizon Height (dir.y)", Range(-0.5, 0.5)) = 0.02
        _HorizonSoftness ("Horizon Softness", Range(0.01, 1)) = 0.35

        // 2026-09-06 (續 180) - the boss-intro cinematic wipes a clear DAY sky into this night
        // panorama from the horizon upward. _NightRise 0 = full day, 1 = full night (the resting
        // value - so outside the cinematic this shader is identical to before).
        _NightRise ("Night Rise (0 day .. 1 night)", Range(0, 1)) = 1
        _RevealSoftness ("Reveal Band Softness", Range(0.01, 0.6)) = 0.18
        [HDR] _DayZenith ("Day Zenith Colour", Color) = (0.26, 0.46, 0.85, 1)
        [HDR] _DayHorizon ("Day Horizon Colour", Color) = (0.72, 0.82, 0.92, 1)
        [HDR] _DayGround ("Day Ground Colour", Color) = (0.42, 0.40, 0.36, 1)
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            half4  _Tint;
            half   _Exposure;
            float  _Rotation;
            half   _HorizonDarken;
            half   _HorizonHeight;
            half   _HorizonSoftness;
            half   _NightRise;
            half   _RevealSoftness;
            half4  _DayZenith;
            half4  _DayHorizon;
            half4  _DayGround;

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 dir : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 RotateAroundYInDegrees(float3 v, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, v.xz), v.y).xzy;
            }

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.dir = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                return o;
            }

            half4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);

                // equirectangular lat/long lookup
                float2 uv;
                uv.x = atan2(d.x, -d.z) * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                uv.y = asin(clamp(d.y, -1.0, 1.0)) * (1.0 / UNITY_PI) + 0.5;

                half3 night = tex2D(_MainTex, uv).rgb;
                night *= _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                // fade the night sky toward black from the horizon downward (kills the desert half)
                half below = smoothstep(_HorizonHeight, _HorizonHeight - _HorizonSoftness, d.y);
                night *= lerp(1.0, 1.0 - _HorizonDarken, below);

                // procedural clear-day sky: ground -> horizon -> zenith by dir.y
                half up = saturate(d.y);
                half3 day = lerp(_DayHorizon.rgb, _DayZenith.rgb, pow(up, 0.55));
                day = lerp(day, _DayGround.rgb, saturate(-d.y * 3.0));

                // reveal band sweeps from below everything (_NightRise 0) to above everything (1),
                // so night creeps UP from the horizon. Below the band = night, above = day.
                half sweep = lerp(-1.15, 1.15, _NightRise);
                half nightAmount = smoothstep(sweep + _RevealSoftness, sweep - _RevealSoftness, d.y);

                half3 col = lerp(day, night, nightAmount);
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
