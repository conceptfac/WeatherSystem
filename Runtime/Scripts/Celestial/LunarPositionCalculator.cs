using System;
using UnityEngine;

namespace ConceptFactory.Weather
{
    /// <summary>
    /// Lightweight lunar astronomy based on the same coordinate model used by SunCalc.
    /// It is more stable than the previous approximation for moon/sun separation during the day.
    /// </summary>
    public static class LunarPositionCalculator
    {
        private const double DegreesToRadians = Math.PI / 180.0;
        private const double RadiansToDegrees = 180.0 / Math.PI;
        private const double ObliquityRadians = 23.4397 * DegreesToRadians;
        private const double SynodicMonthDays = 29.53058867;
        private const double SolarDistanceKilometers = 149598000.0;

        public static LunarPositionData CalculateLunarPosition(
            DateTime localDateTime,
            double latitudeDegrees,
            double longitudeDegrees,
            double utcOffsetHours)
        {
            latitudeDegrees = Math.Clamp(latitudeDegrees, -90.0, 90.0);
            longitudeDegrees = Math.Clamp(longitudeDegrees, -180.0, 180.0);

            double julianDate = ToJulianDate(localDateTime, utcOffsetHours);
            double daysSinceJ2000 = julianDate - 2451545.0;

            SphericalCoordinates moonCoordinates = CalculateMoonCoordinates(daysSinceJ2000);
            EquatorialCoordinates sunCoordinates = CalculateSunCoordinates(daysSinceJ2000);

            double rightAscensionRadians = moonCoordinates.RightAscensionRadians;
            double declinationRadians = moonCoordinates.DeclinationRadians;

            double latitudeRadians = latitudeDegrees * DegreesToRadians;
            double localSiderealTimeRadians = CalculateLocalSiderealTimeRadians(daysSinceJ2000, longitudeDegrees);
            double hourAngleRadians = NormalizeRadians(localSiderealTimeRadians - rightAscensionRadians);

            double elevationRadians =
                Math.Asin(
                    (Math.Sin(latitudeRadians) * Math.Sin(declinationRadians)) +
                    (Math.Cos(latitudeRadians) * Math.Cos(declinationRadians) * Math.Cos(hourAngleRadians)));

            double elevationDegrees = elevationRadians * RadiansToDegrees;
            double apparentElevationDegrees = elevationDegrees + CalculateAtmosphericRefractionDegrees(elevationDegrees);

            double azimuthRadians = Math.Atan2(
                Math.Sin(hourAngleRadians),
                (Math.Cos(hourAngleRadians) * Math.Sin(latitudeRadians)) -
                (Math.Tan(declinationRadians) * Math.Cos(latitudeRadians)));
            double azimuthDegrees = RepeatDegrees((azimuthRadians * RadiansToDegrees) + 180.0);

            IlluminationData illumination = CalculateIllumination(moonCoordinates, sunCoordinates);
            double phaseAngleDegrees = RepeatDegrees(illumination.PhaseAngleRadians * RadiansToDegrees);
            double lunarAgeDays = (phaseAngleDegrees / 360.0) * SynodicMonthDays;

            Vector3 moonDirection = SolarPositionCalculator.ToUnitySunDirection(azimuthDegrees, apparentElevationDegrees);

            return new LunarPositionData(
                moonDirection.normalized,
                (float)elevationDegrees,
                (float)apparentElevationDegrees,
                (float)azimuthDegrees,
                (float)(declinationRadians * RadiansToDegrees),
                (float)(hourAngleRadians * RadiansToDegrees),
                Mathf.Clamp01((float)illumination.IlluminationFraction),
                (float)phaseAngleDegrees,
                (float)lunarAgeDays);
        }

        private static SphericalCoordinates CalculateMoonCoordinates(double daysSinceJ2000)
        {
            double meanLongitudeRadians = RepeatDegrees(218.316 + (13.176396 * daysSinceJ2000)) * DegreesToRadians;
            double meanAnomalyRadians = RepeatDegrees(134.963 + (13.064993 * daysSinceJ2000)) * DegreesToRadians;
            double meanDistanceRadians = RepeatDegrees(93.272 + (13.229350 * daysSinceJ2000)) * DegreesToRadians;

            double eclipticLongitudeRadians = meanLongitudeRadians + (6.289 * DegreesToRadians * Math.Sin(meanAnomalyRadians));
            double eclipticLatitudeRadians = 5.128 * DegreesToRadians * Math.Sin(meanDistanceRadians);
            double distanceKilometers = 385001.0 - (20905.0 * Math.Cos(meanAnomalyRadians));

            return new SphericalCoordinates(
                distanceKilometers,
                RightAscension(eclipticLongitudeRadians, eclipticLatitudeRadians),
                Declination(eclipticLongitudeRadians, eclipticLatitudeRadians));
        }

        private static EquatorialCoordinates CalculateSunCoordinates(double daysSinceJ2000)
        {
            double solarMeanAnomalyRadians = RepeatDegrees(357.5291 + (0.98560028 * daysSinceJ2000)) * DegreesToRadians;
            double equationOfCenterRadians =
                (1.9148 * DegreesToRadians * Math.Sin(solarMeanAnomalyRadians)) +
                (0.02 * DegreesToRadians * Math.Sin(2.0 * solarMeanAnomalyRadians)) +
                (0.0003 * DegreesToRadians * Math.Sin(3.0 * solarMeanAnomalyRadians));
            double perihelionRadians = 102.9372 * DegreesToRadians;
            double eclipticLongitudeRadians = solarMeanAnomalyRadians + equationOfCenterRadians + perihelionRadians + Math.PI;

            return new EquatorialCoordinates(
                RightAscension(eclipticLongitudeRadians, 0.0),
                Declination(eclipticLongitudeRadians, 0.0));
        }

        private static IlluminationData CalculateIllumination(SphericalCoordinates moonCoordinates, EquatorialCoordinates sunCoordinates)
        {
            double phi = Math.Acos(
                (Math.Sin(sunCoordinates.DeclinationRadians) * Math.Sin(moonCoordinates.DeclinationRadians)) +
                (Math.Cos(sunCoordinates.DeclinationRadians) * Math.Cos(moonCoordinates.DeclinationRadians) *
                 Math.Cos(sunCoordinates.RightAscensionRadians - moonCoordinates.RightAscensionRadians)));

            double inc = Math.Atan2(
                SolarDistanceKilometers * Math.Sin(phi),
                moonCoordinates.DistanceKilometers - (SolarDistanceKilometers * Math.Cos(phi)));

            double angle = Math.Atan2(
                Math.Cos(sunCoordinates.DeclinationRadians) * Math.Sin(sunCoordinates.RightAscensionRadians - moonCoordinates.RightAscensionRadians),
                (Math.Sin(sunCoordinates.DeclinationRadians) * Math.Cos(moonCoordinates.DeclinationRadians)) -
                (Math.Cos(sunCoordinates.DeclinationRadians) * Math.Sin(moonCoordinates.DeclinationRadians) *
                 Math.Cos(sunCoordinates.RightAscensionRadians - moonCoordinates.RightAscensionRadians)));

            double illuminationFraction = (1.0 + Math.Cos(inc)) * 0.5;
            double phaseAngleRadians = RepeatRadians(0.5 - (0.5 * inc * Math.Sign(angle)) + Math.PI) - Math.PI;
            phaseAngleRadians = RepeatRadians(phaseAngleRadians);

            return new IlluminationData(illuminationFraction, phaseAngleRadians);
        }

        private static double RightAscension(double eclipticLongitudeRadians, double eclipticLatitudeRadians)
        {
            return Math.Atan2(
                (Math.Sin(eclipticLongitudeRadians) * Math.Cos(ObliquityRadians)) -
                (Math.Tan(eclipticLatitudeRadians) * Math.Sin(ObliquityRadians)),
                Math.Cos(eclipticLongitudeRadians));
        }

        private static double Declination(double eclipticLongitudeRadians, double eclipticLatitudeRadians)
        {
            return Math.Asin(
                (Math.Sin(eclipticLatitudeRadians) * Math.Cos(ObliquityRadians)) +
                (Math.Cos(eclipticLatitudeRadians) * Math.Sin(ObliquityRadians) * Math.Sin(eclipticLongitudeRadians)));
        }

        private static double CalculateLocalSiderealTimeRadians(double daysSinceJ2000, double longitudeDegrees)
        {
            double longitudeRadians = longitudeDegrees * DegreesToRadians;
            return NormalizeRadians((280.16 + (360.9856235 * daysSinceJ2000)) * DegreesToRadians + longitudeRadians);
        }

        private static double ToJulianDate(DateTime localDateTime, double utcOffsetHours)
        {
            DateTime utcDateTime = localDateTime - TimeSpan.FromHours(utcOffsetHours);
            int year = utcDateTime.Year;
            int month = utcDateTime.Month;
            double day = utcDateTime.Day +
                (utcDateTime.Hour / 24.0) +
                (utcDateTime.Minute / 1440.0) +
                (utcDateTime.Second / 86400.0) +
                (utcDateTime.Millisecond / 86400000.0);

            if (month <= 2)
            {
                year -= 1;
                month += 12;
            }

            int a = year / 100;
            int b = 2 - a + (a / 4);

            return Math.Floor(365.25 * (year + 4716))
                 + Math.Floor(30.6001 * (month + 1))
                 + day + b - 1524.5;
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

        private static double NormalizeRadians(double value)
        {
            value %= 2.0 * Math.PI;
            if (value < -Math.PI)
            {
                value += 2.0 * Math.PI;
            }
            else if (value > Math.PI)
            {
                value -= 2.0 * Math.PI;
            }

            return value;
        }

        private static double RepeatRadians(double value)
        {
            value %= 2.0 * Math.PI;
            if (value < 0.0)
            {
                value += 2.0 * Math.PI;
            }

            return value;
        }

        private static double RepeatDegrees(double value)
        {
            value %= 360.0;
            if (value < 0.0)
            {
                value += 360.0;
            }

            return value;
        }

        private readonly struct SphericalCoordinates
        {
            public SphericalCoordinates(double distanceKilometers, double rightAscensionRadians, double declinationRadians)
            {
                DistanceKilometers = distanceKilometers;
                RightAscensionRadians = rightAscensionRadians;
                DeclinationRadians = declinationRadians;
            }

            public double DistanceKilometers { get; }
            public double RightAscensionRadians { get; }
            public double DeclinationRadians { get; }
        }

        private readonly struct EquatorialCoordinates
        {
            public EquatorialCoordinates(double rightAscensionRadians, double declinationRadians)
            {
                RightAscensionRadians = rightAscensionRadians;
                DeclinationRadians = declinationRadians;
            }

            public double RightAscensionRadians { get; }
            public double DeclinationRadians { get; }
        }

        private readonly struct IlluminationData
        {
            public IlluminationData(double illuminationFraction, double phaseAngleRadians)
            {
                IlluminationFraction = illuminationFraction;
                PhaseAngleRadians = phaseAngleRadians;
            }

            public double IlluminationFraction { get; }
            public double PhaseAngleRadians { get; }
        }
    }
}
