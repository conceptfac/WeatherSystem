# Concept Factory Weather System

`com.conceptfactory.weather` is a Unity package for physically grounded environment and weather simulation systems.

## Included

- `WeatherSolarController`: drives a directional light using approximate real-world solar astronomy.
- `LunarController`: drives a directional moon light using approximate lunar astronomy sourced from a `WeatherSolarController`.
- `SolarPositionCalculator`: reusable astronomy utility for latitude, longitude, local date/time, and UTC offset.
- `SolarPositionData`: immutable result payload with solar angles and Unity-ready direction.
- `LunarPositionCalculator`: reusable astronomy utility for approximate moon direction and phase.
- `LunarPositionData`: immutable result payload with lunar angles, direction and illumination.

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
5. Add `LunarController` to another scene object if you also want moon lighting, then assign the scene's `WeatherSolarController`.

The package intentionally avoids external APIs and computes solar position locally.
"# WeatherSystem" 
