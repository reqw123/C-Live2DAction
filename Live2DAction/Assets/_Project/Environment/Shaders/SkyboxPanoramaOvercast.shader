// 2026-09-12, user request ("晴天全景...能否...渲染成陰天...從底部到最高去做緩慢的層次渲染") - same
// vertical reveal-band technique as SkyboxNightPanorama's day->night sweep (see that shader's own
// comment), applied to 露營區's CampPanorama.exr instead: no second (overcast) photo, the "overcast"
// look is the SAME panorama pixel desaturated + dimmed + given a flat grey-blue tint, so it needs no
// new art asset - purely a parameter-driven reinterpretation of the one photo already in use.
Shader "Live2DAction/Environment/SkyboxPanoramaOvercast"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Panorama (equirectangular)", 2D) = "grey" {}
        _Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1)
        [Gamma] _Exposure ("Exposure", Range(0, 4)) = 1.0
        _Rotation ("Rotation (deg)", Range(0, 360)) = 0

        // 0 = untouched sunny photo, 1 = fully overcast. Driven by CampSkyOvercastOnMotorcycle while
        // riding; resting value is 0 so this shader looks identical to the plain Skybox/Panoramic
        // material everywhere else.
        _OvercastRise ("Overcast Rise (0 clear .. 1 overcast)", Range(0, 1)) = 0
        _RevealSoftness ("Reveal Band Softness", Range(0.01, 0.6)) = 0.18
        _OvercastDesaturate ("Overcast Desaturate", Range(0, 1)) = 0.85
        _OvercastExposure ("Overcast Dimming", Range(0, 1)) = 0.55
        _OvercastTint ("Overcast Tint", Color) = (0.62, 0.66, 0.70, 1)
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
            half   _OvercastRise;
            half   _RevealSoftness;
            half   _OvercastDesaturate;
            half   _OvercastExposure;
            half4  _OvercastTint;

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

                float2 uv;
                uv.x = atan2(d.x, -d.z) * (1.0 / (2.0 * UNITY_PI)) + 0.5;
                uv.y = asin(clamp(d.y, -1.0, 1.0)) * (1.0 / UNITY_PI) + 0.5;

                half3 original = tex2D(_MainTex, uv).rgb * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;

                // same photo, reinterpreted: flatten toward luminance, dim, tint grey-blue.
                half luminance = dot(original, half3(0.299, 0.587, 0.114));
                half3 overcast = lerp(original, luminance.xxx, _OvercastDesaturate);
                overcast *= _OvercastTint.rgb * _OvercastExposure;

                // reveal band sweeps from below everything (_OvercastRise 0) to above everything (1),
                // so the overcast look creeps UP from the horizon, matching the day->night boss-intro
                // sky wipe's own technique.
                half sweep = lerp(-1.15, 1.15, _OvercastRise);
                half overcastAmount = smoothstep(sweep + _RevealSoftness, sweep - _RevealSoftness, d.y);

                half3 col = lerp(original, overcast, overcastAmount);
                return half4(col, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
