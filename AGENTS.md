# com.conceptfactory.weather — Agent Guide

Physically grounded weather and environment simulation. No external APIs — all celestial positions computed locally. Requires URP.

## Package layout

```
Runtime/Scripts/
  Celestial/
    WeatherSolarController.cs     drives a DirectionalLight using real solar astronomy
    LunarController.cs            drives moon light, sourced from WeatherSolarController
    SolarPositionCalculator.cs    reusable utility (lat, lon, date/time, UTC offset → direction)
    SolarPositionData.cs          immutable result (solar angles + Unity Vector3 direction)
    LunarPositionCalculator.cs    approximate moon direction and phase
    LunarPositionData.cs          immutable result (lunar angles, direction, illumination)
    LunarVisualPreset.cs          ScriptableObject — moon light colour/intensity per phase
  Atmosphere/
    NimbusCloudController.cs      runtime cloud layer controller
    NimbusCloudRenderMode.cs      enum: flat | volumetric | hybrid
    VolumetricClouds/
      VolumetricCloudsVolume.cs   URP volume override for volumetric cloud layer
      VolumetricCloudsURP.cs      URP renderer feature — injects the cloud pass
      Editor/
        VolumetricCloudsVolumeEditor.cs
  BuildingLights.cs               enables window emissives at dusk, disables at dawn
  LightPole.cs                    street light driven by solar controller
  TrafficIntersectionController.cs  manages full intersection cycle
  TrafficSignalUnit.cs            single signal head (red/amber/green)
Editor/
  NimbusCloudControllerEditor.cs
```

## Unity coordinate convention

North = `+Z`, South = `-Z`, East = `+X`, West = `-X`, Up = `+Y`

## Key design rules

- **No external APIs.** Solar and lunar positions are computed from first principles (astronomy formulas), not from web services.
- **`WeatherSolarController` is the authority.** `LunarController`, `BuildingLights`, and `LightPole` all read from it — set it up first.
- **VolumetricCloudsURP is a URP Renderer Feature.** It must be added to the URP Renderer asset in ProjectSettings, not just placed in the scene.

## Dependencies

- Unity 6000.0+
- Universal Render Pipeline (`com.unity.render-pipelines.universal`)
