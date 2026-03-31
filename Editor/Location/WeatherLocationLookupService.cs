using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace ConceptFactory.Weather.Editor.Location
{
    public interface IWeatherReverseGeocodingProvider
    {
        Task<WeatherLocationLookupResult> ReverseGeocodeAsync(float latitude, float longitude, CancellationToken cancellationToken);
    }

    public sealed class WeatherLocationLookupService : IDisposable
    {
        private readonly IWeatherReverseGeocodingProvider _provider;
        private CancellationTokenSource _cancellationTokenSource;
        private string _lastQueryKey;
        private WeatherLocationLookupResult _cachedResult;

        public WeatherLocationLookupService(IWeatherReverseGeocodingProvider provider = null)
        {
            _provider = provider ?? new NominatimWeatherReverseGeocodingProvider();
        }

        public async Task<WeatherLocationLookupResult> LookupAsync(float latitude, float longitude)
        {
            string queryKey = BuildQueryKey(latitude, longitude);
            if (_cachedResult != null && string.Equals(_lastQueryKey, queryKey, StringComparison.Ordinal))
            {
                return _cachedResult;
            }

            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            _lastQueryKey = queryKey;
            _cachedResult = await _provider.ReverseGeocodeAsync(latitude, longitude, _cancellationTokenSource.Token);
            return _cachedResult;
        }

        public void Dispose()
        {
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
        }

        private static string BuildQueryKey(float latitude, float longitude)
        {
            return Math.Round(latitude, 3).ToString(CultureInfo.InvariantCulture) + ":" +
                   Math.Round(longitude, 3).ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class NominatimWeatherReverseGeocodingProvider : IWeatherReverseGeocodingProvider
    {
        private const string EndpointTemplate =
            "https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={0}&lon={1}&zoom=10&addressdetails=1&accept-language=en";

        public async Task<WeatherLocationLookupResult> ReverseGeocodeAsync(float latitude, float longitude, CancellationToken cancellationToken)
        {
            string url = string.Format(
                CultureInfo.InvariantCulture,
                EndpointTemplate,
                latitude,
                longitude);

            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.SetRequestHeader("User-Agent", "ConceptFactoryWeatherEditor/1.0");
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return WeatherLocationLookupResult.CreateFailure("Location unavailable");
            }

            ReverseGeocodeResponse response = JsonUtility.FromJson<ReverseGeocodeResponse>(request.downloadHandler.text);
            if (response == null)
            {
                return WeatherLocationLookupResult.CreateFailure("Location unavailable");
            }

            string city =
                FirstNonEmpty(
                    response.address.city,
                    response.address.town,
                    response.address.village,
                    response.address.municipality,
                    response.address.county,
                    response.address.state);

            string region = FirstNonEmpty(response.address.state, response.address.country);

            if (string.IsNullOrWhiteSpace(city))
            {
                city = "Unknown place";
            }

            string shortLabel = string.IsNullOrWhiteSpace(region) ? city : city + ", " + region;
            return WeatherLocationLookupResult.CreateSuccess(shortLabel, response.display_name);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            foreach (string value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        [Serializable]
        private sealed class ReverseGeocodeResponse
        {
            public string display_name;
            public ReverseGeocodeAddress address = new();
        }

        [Serializable]
        private sealed class ReverseGeocodeAddress
        {
            public string city;
            public string town;
            public string village;
            public string municipality;
            public string county;
            public string state;
            public string country;
        }
    }

    public sealed class WeatherLocationLookupResult
    {
        private WeatherLocationLookupResult(bool success, string shortLabel, string fullLabel)
        {
            Success = success;
            ShortLabel = shortLabel;
            FullLabel = fullLabel;
        }

        public bool Success { get; }
        public string ShortLabel { get; }
        public string FullLabel { get; }

        public static WeatherLocationLookupResult CreateSuccess(string shortLabel, string fullLabel)
        {
            return new WeatherLocationLookupResult(true, shortLabel, fullLabel);
        }

        public static WeatherLocationLookupResult CreateFailure(string label)
        {
            return new WeatherLocationLookupResult(false, label, label);
        }
    }
}
