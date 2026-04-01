using UnityEngine;

namespace ConceptFactory.Weather
{
    /// <summary>
    /// Result of a lunar position query for a specific location and local civil time.
    /// </summary>
    public readonly struct LunarPositionData
    {
        public LunarPositionData(
            Vector3 moonDirection,
            float elevationDegrees,
            float apparentElevationDegrees,
            float azimuthDegrees,
            float declinationDegrees,
            float hourAngleDegrees,
            float illuminationFraction,
            float phaseAngleDegrees,
            float lunarAgeDays)
        {
            MoonDirection = moonDirection;
            ElevationDegrees = elevationDegrees;
            ApparentElevationDegrees = apparentElevationDegrees;
            AzimuthDegrees = azimuthDegrees;
            DeclinationDegrees = declinationDegrees;
            HourAngleDegrees = hourAngleDegrees;
            IlluminationFraction = illuminationFraction;
            PhaseAngleDegrees = phaseAngleDegrees;
            LunarAgeDays = lunarAgeDays;
        }

        public Vector3 MoonDirection { get; }
        public float ElevationDegrees { get; }
        public float ApparentElevationDegrees { get; }
        public float AzimuthDegrees { get; }
        public float DeclinationDegrees { get; }
        public float HourAngleDegrees { get; }
        public float IlluminationFraction { get; }
        public float PhaseAngleDegrees { get; }
        public float LunarAgeDays { get; }
    }
}
