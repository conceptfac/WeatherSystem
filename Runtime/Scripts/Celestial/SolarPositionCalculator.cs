using System;
using UnityEngine;

namespace ConceptFactory.Weather
{
    /// <summary>
    /// Approximate solar astronomy utilities based on common NOAA-style equations.
    /// Longitude is east-positive, west-negative. UTC offset is hours relative to UTC.
    /// </summary>
    public static class SolarPositionCalculator
    {
        private const double DegreesToRadians = Math.PI / 180.0;
        private const double RadiansToDegrees = 180.0 / Math.PI;

        public static SolarPositionData CalculateSolarPosition(
            DateTime localDateTime,
            double latitudeDegrees,
            double longitudeDegrees,
            double utcOffsetHours)
        {
            latitudeDegrees = Math.Clamp(latitudeDegrees, -90.0, 90.0);
            longitudeDegrees = Math.Clamp(longitudeDegrees, -180.0, 180.0);

            int dayOfYear = localDateTime.DayOfYear;
            double localHours = localDateTime.Hour +
                (localDateTime.Minute / 60.0) +
                (localDateTime.Second / 3600.0) +
                (localDateTime.Millisecond / 3600000.0);

            double fractionalYear = (2.0 * Math.PI / 365.0) * (dayOfYear - 1 + ((localHours - 12.0) / 24.0));
            double equationOfTimeMinutes = CalculateEquationOfTimeMinutes(fractionalYear);
            double declinationRadians = CalculateSolarDeclinationRadians(fractionalYear);

            double trueSolarMinutes = (localHours * 60.0) + equationOfTimeMinutes + (4.0 * longitudeDegrees) - (60.0 * utcOffsetHours);
            trueSolarMinutes = Repeat(trueSolarMinutes, 1440.0);

            double hourAngleDegrees = (trueSolarMinutes / 4.0) - 180.0;
            if (hourAngleDegrees < -180.0)
            {
                hourAngleDegrees += 360.0;
            }

            double latitudeRadians = latitudeDegrees * DegreesToRadians;
            double hourAngleRadians = hourAngleDegrees * DegreesToRadians;

            double cosZenith =
                (Math.Sin(latitudeRadians) * Math.Sin(declinationRadians)) +
                (Math.Cos(latitudeRadians) * Math.Cos(declinationRadians) * Math.Cos(hourAngleRadians));
            cosZenith = Math.Clamp(cosZenith, -1.0, 1.0);

            double zenithRadians = Math.Acos(cosZenith);
            double elevationDegrees = 90.0 - (zenithRadians * RadiansToDegrees);
            double apparentElevationDegrees = elevationDegrees + CalculateAtmosphericRefractionDegrees(elevationDegrees);

            double azimuthDegrees = Math.Atan2(
                Math.Sin(hourAngleRadians),
                (Math.Cos(hourAngleRadians) * Math.Sin(latitudeRadians)) - (Math.Tan(declinationRadians) * Math.Cos(latitudeRadians))) * RadiansToDegrees;
            azimuthDegrees = Repeat(azimuthDegrees + 180.0, 360.0);

            Vector3 sunDirection = ToUnitySunDirection(azimuthDegrees, apparentElevationDegrees);
            float daylightFactor = Mathf.InverseLerp(-6f, 6f, (float)apparentElevationDegrees);

            return new SolarPositionData(
                sunDirection.normalized,
                (float)elevationDegrees,
                (float)apparentElevationDegrees,
                (float)azimuthDegrees,
                (float)(declinationRadians * RadiansToDegrees),
                (float)hourAngleDegrees,
                (float)equationOfTimeMinutes,
                daylightFactor);
        }

        public static Vector3 ToUnitySunDirection(double azimuthDegrees, double elevationDegrees)
        {
            double azimuthRadians = azimuthDegrees * DegreesToRadians;
            double elevationRadians = elevationDegrees * DegreesToRadians;
            double horizontal = Math.Cos(elevationRadians);

            return new Vector3(
                (float)(Math.Sin(azimuthRadians) * horizontal),
                (float)Math.Sin(elevationRadians),
                (float)(Math.Cos(azimuthRadians) * horizontal));
        }

        private static double CalculateEquationOfTimeMinutes(double fractionalYear)
        {
            return 229.18 * (
                0.000075 +
                (0.001868 * Math.Cos(fractionalYear)) -
                (0.032077 * Math.Sin(fractionalYear)) -
                (0.014615 * Math.Cos(2.0 * fractionalYear)) -
                (0.040849 * Math.Sin(2.0 * fractionalYear)));
        }

        private static double CalculateSolarDeclinationRadians(double fractionalYear)
        {
            return
                0.006918 -
                (0.399912 * Math.Cos(fractionalYear)) +
                (0.070257 * Math.Sin(fractionalYear)) -
                (0.006758 * Math.Cos(2.0 * fractionalYear)) +
                (0.000907 * Math.Sin(2.0 * fractionalYear)) -
                (0.002697 * Math.Cos(3.0 * fractionalYear)) +
                (0.00148 * Math.Sin(3.0 * fractionalYear));
        }

        private static double CalculateAtmosphericRefractionDegrees(double elevationDegrees)
        {
            if (elevationDegrees >= 85.0)
            {
                return 0.0;
            }

            double tangent = Math.Tan(elevationDegrees * DegreesToRadians);

            if (elevationDegrees > 5.0)
            {
                return (58.1 / tangent - 0.07 / Math.Pow(tangent, 3.0) + 0.000086 / Math.Pow(tangent, 5.0)) / 3600.0;
            }

            if (elevationDegrees > -0.575)
            {
                return (1735.0 + elevationDegrees * (-518.2 + elevationDegrees * (103.4 + elevationDegrees * (-12.79 + elevationDegrees * 0.711)))) / 3600.0;
            }

            return (-20.772 / tangent) / 3600.0;
        }

        private static double Repeat(double value, double length)
        {
            return value - Math.Floor(value / length) * length;
        }
    }
}
