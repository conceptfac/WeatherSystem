using UnityEngine;

namespace ConceptFactory.Weather
{
    /// <summary>
    /// Result of a solar position query for a specific location and local civil time.
    /// </summary>
    public readonly struct SolarPositionData
    {
        public SolarPositionData(
            Vector3 sunDirection,
            float elevationDegrees,
            float apparentElevationDegrees,
            float azimuthDegrees,
            float declinationDegrees,
            float hourAngleDegrees,
            float equationOfTimeMinutes,
            float daylightFactor)
        {
            SunDirection = sunDirection;
            ElevationDegrees = elevationDegrees;
            ApparentElevationDegrees = apparentElevationDegrees;
            AzimuthDegrees = azimuthDegrees;
            DeclinationDegrees = declinationDegrees;
            HourAngleDegrees = hourAngleDegrees;
            EquationOfTimeMinutes = equationOfTimeMinutes;
            DaylightFactor = daylightFactor;
        }

        /// <summary>
        /// Unit vector from world origin toward the Sun in Unity coordinates.
        /// </summary>
        public Vector3 SunDirection { get; }

        /// <summary>
        /// Geometric solar elevation in degrees.
        /// </summary>
        public float ElevationDegrees { get; }

        /// <summary>
        /// Apparent solar elevation in degrees after a small atmospheric refraction correction.
        /// </summary>
        public float ApparentElevationDegrees { get; }

        /// <summary>
        /// Solar azimuth in degrees, clockwise from north.
        /// </summary>
        public float AzimuthDegrees { get; }

        public float DeclinationDegrees { get; }

        public float HourAngleDegrees { get; }

        public float EquationOfTimeMinutes { get; }

        /// <summary>
        /// Smooth daylight amount based on apparent elevation. 0 = night, 1 = full day.
        /// </summary>
        public float DaylightFactor { get; }
    }
}
