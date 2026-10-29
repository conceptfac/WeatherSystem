# Concept Factory — Weather System

`com.conceptfactory.weather` is a Unity package for physically grounded environment and weather simulation systems.

## Included

**Celestial**
- `WeatherSolarController` — drives a directional light using approximate real-world solar astronomy
- `LunarController` — drives a directional moon light using approximate lunar astronomy sourced from a `WeatherSolarController`
- `SolarPositionCalculator` — reusable astronomy utility for latitude, longitude, local date/time, and UTC offset
- `SolarPositionData` — immutable result payload with solar angles and Unity-ready direction
- `LunarPositionCalculator` — reusable astronomy utility for approximate moon direction and phase
- `LunarPositionData` — immutable result payload with lunar angles, direction, and illumination
- `LunarVisualPreset` — ScriptableObject that defines moon light colour and intensity per phase

**Atmosphere / Clouds**
- `NimbusCloudController` — runtime cloud layer controller with configurable render mode
- `NimbusCloudRenderMode` — enum that selects between flat, volumetric, and hybrid cloud rendering
- `VolumetricCloudsVolume` — volume override for the URP volumetric cloud layer
- `VolumetricCloudsURP` — URP renderer feature that injects the volumetric cloud pass

**Environment Lighting**
- `BuildingLights` — automatically enables window light emissives at dusk and disables them at dawn
- `LightPole` — street light that turns on/off driven by the solar controller

**Traffic**
- `TrafficIntersectionController` — manages a full intersection cycle across multiple `TrafficSignalUnit` instances
- `TrafficSignalUnit` — a single traffic signal head with red/amber/green state

## Unity Coordinates

- North: `Vector3.forward` (`+Z`)
- South: `Vector3.back` (`-Z`)
- East: `Vector3.right` (`+X`)
- West: `Vector3.left` (`-X`)
- Up: `Vector3.up` (`+Y`)

## Usage

1. Add `WeatherSolarController` to a scene object.
2. Assign a `Directional Light` as the Sun.
3. Set latitude, longitude, and UTC offset.
4. Set the local date/time or enable automatic time progression.
5. Add `LunarController` to another scene object for moon lighting; assign the scene's `WeatherSolarController`.
6. Add `NimbusCloudController` to drive cloud layer changes from the weather state.
7. Optionally add `BuildingLights` and `LightPole` components to scene objects that should react to the time of day.

The package intentionally avoids external APIs and computes all celestial positions locally.

## Requirements

- Unity 6000.0+
- Universal Render Pipeline (`com.unity.render-pipelines.universal`)
