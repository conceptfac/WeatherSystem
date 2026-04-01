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
    half _WeatherNightValue;
    float _WeatherSkyTime;

    half3 _WeatherSunColor, _WeatherSunDirection;
    half _WeatherSunIntensity;

    half _WeatherSunFalloff;
    half _WeatherSunSize;

    half3 _WeatherMoonColor, _WeatherMoonDirection;
    half _WeatherMoonIntensity;
    half _WeatherMoonFalloff;
    half _WeatherMoonSize;
    half _WeatherMoonPhaseAngle;
    half _WeatherMoonIllumination;
    half _WeatherMoonTextureExposure;
    half _WeatherMoonDarkTextureExposure;
    half _WeatherMoonTerminatorSoftness;
    half _WeatherMoonDarkSideVisibility;
    half3 _WeatherMoonHaloColor;
    half _WeatherMoonHaloIntensity;
    half _WeatherMoonHaloInnerSize;
    half _WeatherMoonHaloOuterSize;
    half _WeatherMoonHaloTerminator;
    half3 _WeatherMoonBorderHaloColor;
    half _WeatherMoonBorderHaloIntensity;
    half _WeatherMoonBorderHaloInnerSize;
    half _WeatherMoonBorderHaloOuterSize;
    half _WeatherMoonBorderHaloTerminator;
    half _WeatherMoonSkyVisibility;
    half _WeatherMoonDarkSkyVisibility;
    half _WeatherMoonUseTexture;
    half _WeatherStarSize;
    half _WeatherStarDensity;
    half _WeatherStarTwinkleAmount;
    half _WeatherStarColorFlicker;
    half _WeatherStarHorizonChromaticShift;
    sampler2D _WeatherMoonTexture;
    sampler2D _WeatherMoonDarkTexture;
    sampler2D _WeatherMoonPhaseMaskTexture;
    sampler2D _WeatherStarColorRamp;

    inline float hash21(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }

    inline void ComputeStableMoonBasis(float3 moonDirection, out float3 right, out float3 up)
    {
        // Stable orthonormal basis from the moon direction to avoid UV flips over time.
        if (moonDirection.z < -0.9999999)
        {
            right = float3(0.0, -1.0, 0.0);
            up = float3(-1.0, 0.0, 0.0);
            return;
        }

        float a = 1.0 / (1.0 + moonDirection.z);
        float b = -moonDirection.x * moonDirection.y * a;
        right = normalize(float3(1.0 - moonDirection.x * moonDirection.x * a, b, -moonDirection.x));
        up = normalize(float3(b, 1.0 - moonDirection.y * moonDirection.y * a, -moonDirection.y));
    }

    inline float2 ComputeMoonLocalUv(float3 viewDirection, float3 moonDirection, float moonAngularRadius)
    {
        float3 right;
        float3 up;
        ComputeStableMoonBasis(moonDirection, right, up);
        float forward = dot(viewDirection, moonDirection);

        // Prevent mirrored copies of the moon on the opposite side of the sky.
        if (forward <= 0.0001)
        {
            return float2(10000.0, 10000.0);
        }

        float x = dot(viewDirection, right) / forward;
        float y = dot(viewDirection, up) / forward;
        float projectedRadius = max(tan(moonAngularRadius), 0.0001);
        return float2(x, y) / projectedRadius;
    }

    inline half ComputeMoonDiskMask(float2 local)
    {
        return saturate(1.0 - smoothstep(0.94, 1.0, length(local)));
    }

    inline float ComputeMoonPhaseOrientation()
    {
        return _WeatherMoonPhaseAngle > 180.0h ? -1.0 : 1.0;
    }

    inline half ComputeMoonPhaseMask(float3 viewDirection, float3 moonDirection, float3 sunDirection, float moonAngularRadius)
    {
        float2 local = ComputeMoonLocalUv(viewDirection, moonDirection, moonAngularRadius);
        float diskMask = ComputeMoonDiskMask(local);
        float terminatorSoftness = max(_WeatherMoonTerminatorSoftness, 0.001h);
        float illumination = saturate(_WeatherMoonIllumination);
        float phaseOrientation = ComputeMoonPhaseOrientation();

        // Circular occulting mask sliding on the moon's local horizontal axis.
        float shadowOffset = lerp(0.0, 2.1, illumination);
        float2 shadowCenter = float2(-phaseOrientation * shadowOffset, 0.0);
        float shadowDistance = length(local - shadowCenter);
        float shadowMask = 1.0 - smoothstep(1.0 - terminatorSoftness, 1.0 + terminatorSoftness, shadowDistance);
        float illuminatedMask = saturate(diskMask - shadowMask);
        return illuminatedMask;
    }

    inline half4 SampleMoonTexture(sampler2D moonTexture, float2 local)
    {
        float2 moonUv = saturate(local * 0.5 + 0.5);
        moonUv = clamp(moonUv, 0.002, 0.998);
        return tex2D(moonTexture, moonUv);
    }

    inline half SampleMoonAlpha(sampler2D moonTexture, float2 local)
    {
        float2 moonUv = saturate(local * 0.5 + 0.5);
        moonUv = clamp(moonUv, 0.002, 0.998);
        return tex2D(moonTexture, moonUv).a;
    }

    inline half SampleMoonMaskTexture(float2 local)
    {
        float2 moonUv = local * 0.5 + 0.5;
        return tex2D(_WeatherMoonPhaseMaskTexture, moonUv).a;
    }

    inline half3 ComputeProceduralStars(float3 viewDirection, half nightValue)
    {
        float2 uv;
        uv.x = atan2(viewDirection.x, viewDirection.z) / 6.28318530718 + 0.5;
        uv.y = asin(clamp(viewDirection.y, -1.0, 1.0)) / 3.14159265359 + 0.5;

        float2 scaledUv = uv * float2(420.0, 180.0);
        float2 cell = floor(scaledUv);
        float2 cellUv = frac(scaledUv) - 0.5;

        float starSeed = hash21(cell);
        float densityThreshold = lerp(0.998, 0.92, saturate((_WeatherStarDensity - 0.1h) / 9.9h));
        float starPresence = step(densityThreshold, starSeed);
        float cellDistance = length(cellUv);
        float starScale = max(_WeatherStarSize, 0.05h);
        float innerRadius = 0.03 * starScale;
        float outerRadius = 0.14 * starScale;
        float starCore = saturate(1.0 - smoothstep(innerRadius, outerRadius, cellDistance));
        float twinkleControl = saturate(_WeatherStarTwinkleAmount / 10.0);
        float twinkleSpeed = lerp(1.8, 7.2, hash21(cell + 7.13));
        float eventSpacing = lerp(0.22, 0.55, hash21(cell + 5.91));
        float twinklePhase = (_Time.y * twinkleSpeed * eventSpacing) + (starSeed * 18.0);
        float burstPhase = (_Time.y * twinkleSpeed) + (starSeed * 18.0);
        float eventRate = lerp(0.08, 0.22, hash21(cell + 11.37));
        float eventPhase = frac((_Time.y * eventRate) + starSeed * 9.0);
        float eventWindow = smoothstep(0.0, 0.04, eventPhase) * (1.0 - smoothstep(0.14, 0.24, eventPhase));
        float twinkleWave = 0.5 + 0.5 * sin(burstPhase);
        float twinkleBurstA = pow(saturate(twinkleWave), lerp(18.0, 5.0, hash21(cell + 19.41)));
        float twinkleBurst = twinkleBurstA * eventWindow;
        float vanishCycle = 0.5 + 0.5 * sin((twinklePhase * lerp(0.08, 0.22, hash21(cell + 53.17))) + (starSeed * 41.0));
        float vanishGate = pow(saturate(vanishCycle), lerp(1.8, 3.8, hash21(cell + 59.73)));
        float vanishDepth = lerp(0.65, 1.35, hash21(cell + 61.37));
        float vanishHoldThreshold = lerp(0.58, 0.82, hash21(cell + 67.19));
        float vanishHold = step(vanishHoldThreshold, vanishGate);
        float hardVanishStar = step(0.72, hash21(cell + 71.53));
        float hardVanishThreshold = lerp(0.72, 0.9, hash21(cell + 73.11));
        float hardVanishGate = step(hardVanishThreshold, vanishGate) * hardVanishStar;
        float twinkleBase = 0.58 + (0.12 * twinkleWave);
        float vanishFactor = saturate(1.0 - max(vanishGate * vanishDepth, vanishHold * vanishDepth) * twinkleControl);
        vanishFactor = lerp(vanishFactor, 0.0, hardVanishGate * twinkleControl);
        float brightnessVariance = lerp(0.85, 2.4, hash21(cell + 79.11));
        float twinkle = lerp(1.0, (twinkleBase + (twinkleBurst * 3.2 * brightnessVariance)) * vanishFactor, twinkleControl);
        float horizonFade = saturate(smoothstep(-0.08, 0.28, viewDirection.y));
        float brightness = starPresence * starCore * twinkle * horizonFade * nightValue;

        float hue = hash21(cell + 13.17);
        half3 starColor = tex2D(_WeatherStarColorRamp, float2(hue, 0.5)).rgb;
        float chromaSeed = hash21(cell + 41.73);
        float chromaWave = sin(twinklePhase * lerp(0.72, 1.46, chromaSeed) + (chromaSeed * 14.0));
        float rareColorStar = step(0.8, chromaSeed);
        float greenChance = step(0.972, chromaSeed);
        float chromaIntensity = saturate(_WeatherStarColorFlicker) * rareColorStar * lerp(0.08, 0.8, saturate(_WeatherStarHorizonChromaticShift) * (1.0 - horizonFade));
        float3 chromaAxis = normalize(float3(
            lerp(sin(chromaSeed * 21.0), -0.1, greenChance),
            lerp(sin(chromaSeed * 37.0 + 1.2), 1.0, greenChance),
            lerp(sin(chromaSeed * 53.0 + 2.4), -0.18, greenChance)));
        starColor = saturate(starColor + (chromaAxis * chromaWave * chromaIntensity * 0.16));
        return starColor * brightness * 1.35h;
    }
    
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
        half nightValue = saturate(_WeatherNightValue);
        half3 moonDirection = dot(_WeatherMoonDirection, _WeatherMoonDirection) > 0.0001h
            ? normalize(_WeatherMoonDirection)
            : half3(0.0h, -1.0h, 0.0h);
        half3 moonColor = dot(_WeatherMoonColor.rgb, _WeatherMoonColor.rgb) > 0.0001h
            ? _WeatherMoonColor.rgb
            : half3(0.84h, 0.9h, 1.0h);
        half moonIntensity = max(_WeatherMoonIntensity, 0.0h);
        half moonFalloff = _WeatherMoonFalloff > 0.0001h ? _WeatherMoonFalloff : 1400.0h;
        half moonSize = _WeatherMoonSize > 0.0001h ? _WeatherMoonSize : 0.38h;

        float p = v.y;
        float p1 = 1 - pow(min(1, 1 - p), topSkyFalloff);
        float p3 = 1 - pow(min(1, 1 + p), bottomSkyFalloff);
        float p2 = 1 - p1 - p3;

        half3 c_sky = ambientSky * p1 + ambientHorizon * p2 + ambientGround * p3;
        half3 c_sun = sunColor * min(pow(max(0, dot(v, sunDirection)), sunFalloff) * sunSize, 1);
        half moonAngularRadius = max(0.004h, 0.012h * moonSize);
        float2 moonLocal = ComputeMoonLocalUv(v, moonDirection, moonAngularRadius);
        half moonDiskMask = ComputeMoonDiskMask(moonLocal);
        float phaseOrientation = ComputeMoonPhaseOrientation();
        half moonPhaseMask = ComputeMoonPhaseMask(v, moonDirection, sunDirection, moonAngularRadius);
        half4 litMoonSample = SampleMoonTexture(_WeatherMoonTexture, moonLocal);
        half4 darkMoonSample = SampleMoonTexture(_WeatherMoonDarkTexture, moonLocal);
        half phaseMaskTexture = SampleMoonMaskTexture(moonLocal);
        litMoonSample.rgb *= max(_WeatherMoonTextureExposure, 0.0h);
        darkMoonSample.rgb *= max(_WeatherMoonDarkTextureExposure, 0.0h);
        half litSideMask = moonPhaseMask * phaseMaskTexture;
        half baseDiskMask = moonDiskMask;
        half litCutout = step(0.5h, litMoonSample.a);
        half darkCutout = step(0.5h, darkMoonSample.a);
        half3 litMoon = lerp(moonColor, (litMoonSample.rgb * litCutout) * moonColor, saturate(_WeatherMoonUseTexture));
        half darkVisibility = saturate(_WeatherMoonDarkSideVisibility);
        half litBlend = saturate(litSideMask);
        half shadowBlend = saturate(1.0h - litBlend);
        half3 darkMoonBase = darkMoonSample.rgb * darkCutout * darkVisibility * shadowBlend;
        half litMoonVisibility = saturate(_WeatherMoonSkyVisibility);
        half darkMoonVisibility = saturate(_WeatherMoonDarkSkyVisibility);
        half3 moonSurface = ((darkMoonBase * darkMoonVisibility) + ((litMoon * moonIntensity) * litBlend * litMoonVisibility)) * baseDiskMask;
        half moonCoverage = saturate(max((darkCutout * darkVisibility * darkMoonVisibility * shadowBlend), (litCutout * litMoonVisibility * litBlend)) * baseDiskMask);
        half haloOcclusion = moonCoverage;
        float moonDistance = length(moonLocal);
        float innerRadius = 1.0 + _WeatherMoonHaloInnerSize;
        float outerRadius = 1.0 + max(_WeatherMoonHaloOuterSize, _WeatherMoonHaloInnerSize + 0.0001h);
        float haloSoftness = max(_WeatherMoonHaloTerminator, 0.0001h);
        float innerMask = smoothstep(innerRadius - haloSoftness, innerRadius + haloSoftness, moonDistance);
        float outerMask = 1.0 - smoothstep(outerRadius - haloSoftness, outerRadius + haloSoftness, moonDistance);
        float haloMask = saturate(innerMask * outerMask);
        half haloDayVisibility = darkMoonVisibility;
        half3 moonHalo = _WeatherMoonHaloColor * _WeatherMoonHaloIntensity * haloMask * moonIntensity * haloDayVisibility;
        float borderInnerRadius = 1.0 + _WeatherMoonBorderHaloInnerSize;
        float borderOuterRadius = 1.0 + max(_WeatherMoonBorderHaloOuterSize, _WeatherMoonBorderHaloInnerSize + 0.0001h);
        float borderSoftness = max(_WeatherMoonBorderHaloTerminator, 0.0001h);
        float borderInnerMask = smoothstep(borderInnerRadius - borderSoftness, borderInnerRadius + borderSoftness, moonDistance);
        float borderOuterMask = 1.0 - smoothstep(borderOuterRadius - borderSoftness, borderOuterRadius + borderSoftness, moonDistance);
        float borderHaloMask = saturate(borderInnerMask * borderOuterMask);
        half3 borderHalo = _WeatherMoonBorderHaloColor * _WeatherMoonBorderHaloIntensity * borderHaloMask * moonIntensity * haloDayVisibility;
        half3 c_stars = ComputeProceduralStars(v, nightValue);
        half3 background = c_sky * skyIntensity + c_sun * sunIntensity;
        half starOcclusion = saturate(max(haloMask, borderHaloMask));
        half3 starsLayer = c_stars * (1.0h - starOcclusion);
        half3 finalColor = lerp(background + starsLayer + moonHalo + borderHalo, half3(0.0h, 0.0h, 0.0h), haloOcclusion) + moonSurface;
        return half4(finalColor, 0);
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
