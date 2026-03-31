// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Skybox/Simple"
{
    Properties
    {
    }

    CGINCLUDE

    #include "UnityCG.cginc"
	#include "Lighting.cginc"

    struct appdata
    {
        float4 position : POSITION;
        float3 texcoord : TEXCOORD0;
    };
    
    struct v2f
    {
        float4 position : SV_POSITION;
        float3 texcoord : TEXCOORD0;
    };
    
    half4 _WeatherAmbientSky, _WeatherAmbientGround, _WeatherAmbientHorizon;
    half _WeatherTopSkyFalloff;
    half _WeatherBottomSkyFalloff;

    half _WeatherSkyIntensity;

    half3 _WeatherSunColor, _WeatherSunDirection;
    half _WeatherSunIntensity;

    half _WeatherSunFalloff;
    half _WeatherSunSize;
    
    v2f vert(appdata v)
    {
        v2f o;
        o.position = UnityObjectToClipPos(v.position);
        o.texcoord = v.texcoord;
        return o;
    }
    
    half4 frag(v2f i) : COLOR
    {
        float3 v = normalize(i.texcoord);
        half3 ambientSky = dot(_WeatherAmbientSky.rgb, _WeatherAmbientSky.rgb) > 0.0001h
            ? _WeatherAmbientSky.rgb
            : half3(0.02h, 0.03h, 0.07h);
        half3 ambientHorizon = dot(_WeatherAmbientHorizon.rgb, _WeatherAmbientHorizon.rgb) > 0.0001h
            ? _WeatherAmbientHorizon.rgb
            : half3(0.03h, 0.04h, 0.08h);
        half3 ambientGround = dot(_WeatherAmbientGround.rgb, _WeatherAmbientGround.rgb) > 0.0001h
            ? _WeatherAmbientGround.rgb
            : half3(0.01h, 0.015h, 0.03h);
        half skyIntensity = _WeatherSkyIntensity > 0.0001h ? _WeatherSkyIntensity : 0.03h;
        half topSkyFalloff = _WeatherTopSkyFalloff > 0.0001h ? _WeatherTopSkyFalloff : 5.8h;
        half bottomSkyFalloff = _WeatherBottomSkyFalloff > 0.0001h ? _WeatherBottomSkyFalloff : 15.2h;
        half3 sunDirection = dot(_WeatherSunDirection, _WeatherSunDirection) > 0.0001h
            ? normalize(_WeatherSunDirection)
            : half3(0.0h, 1.0h, 0.0h);
        half3 sunColor = dot(_WeatherSunColor.rgb, _WeatherSunColor.rgb) > 0.0001h
            ? _WeatherSunColor.rgb
            : half3(1.0h, 0.96h, 0.86h);
        half sunIntensity = max(_WeatherSunIntensity, 0.0h);
        half sunFalloff = _WeatherSunFalloff > 0.0001h ? _WeatherSunFalloff : 650.0h;
        half sunSize = _WeatherSunSize > 0.0001h ? _WeatherSunSize : 0.92h;

        float p = v.y;
        float p1 = 1 - pow(min(1, 1 - p), topSkyFalloff);
        float p3 = 1 - pow(min(1, 1 + p), bottomSkyFalloff);
        float p2 = 1 - p1 - p3;

        half3 c_sky = ambientSky * p1 + ambientHorizon * p2 + ambientGround * p3;
        half3 c_sun = sunColor * min(pow(max(0, dot(v, sunDirection)), sunFalloff) * sunSize, 1);

        return half4(c_sky * skyIntensity + c_sun * sunIntensity, 0);
    }

    ENDCG

    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" }
        Pass
        {
            ZWrite Off
            Cull Off
            Fog { Mode Off }
            CGPROGRAM
            #pragma fragmentoption ARB_precision_hint_fastest
            #pragma vertex vert
            #pragma fragment frag
            ENDCG
        }
    } 
}
