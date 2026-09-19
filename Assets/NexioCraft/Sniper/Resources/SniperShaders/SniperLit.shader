// Flat-shaded vertex-colour lighting for the sniper world.
// Lighting and fog come from globals set by SniperLighting (no keywords, so no variants to strip),
// and everything is evaluated per vertex, which is cheap on phones and suits the faceted look.
// Vertex alpha below 1 marks self-lit surfaces (lit windows, neon, robot visors).
Shader "NexioCraft/Sniper/Lit"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            half _Emission;
            half3 _NxSunDir;
            half3 _NxSunColor;
            half3 _NxSkyAmbient;
            half3 _NxGroundAmbient;
            half3 _NxFogColor;
            float4 _NxFogParams; // x: fog start (m), y: 1 / (end - start), z: maximum fog amount

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 color : COLOR0;
                half fog : TEXCOORD0;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.pos = UnityWorldToClipPos(worldPos);
                half3 n = UnityObjectToWorldNormal(v.normal);
                half sun = saturate(dot(n, _NxSunDir));
                half3 ambient = lerp(_NxGroundAmbient, _NxSkyAmbient, n.y * 0.5 + 0.5);
                half3 albedo = v.color.rgb * _Tint.rgb;
                half glow = saturate((1.0 - v.color.a) + _Emission);
                o.color = lerp(albedo * (ambient + _NxSunColor * sun), albedo, glow);
                float dist = distance(worldPos, _WorldSpaceCameraPos);
                o.fog = saturate((dist - _NxFogParams.x) * _NxFogParams.y) * _NxFogParams.z;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(lerp(i.color, _NxFogColor, i.fog), 1);
            }
            ENDCG
        }
    }
}
