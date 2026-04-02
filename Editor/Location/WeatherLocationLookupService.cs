using System;
using System.Globalization;
using System.Text;
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

    public interface IWeatherLocationSearchProvider
    {
        Task<WeatherLocationSearchResult[]> SearchAsync(string query, int maxResults, CancellationToken cancellationToken);
    }

    public sealed class WeatherLocationLookupService : IDisposable
    {
        private readonly IWeatherReverseGeocodingProvider _reverseProvider;
        private readonly IWeatherLocationSearchProvider _searchProvider;
        private CancellationTokenSource _reverseLookupCancellationTokenSource;
        private CancellationTokenSource _searchCancellationTokenSource;
        private string _lastReverseQueryKey;
        private WeatherLocationLookupResult _cachedReverseResult;
        private string _lastSearchQueryKey;
        private WeatherLocationSearchResult[] _cachedSearchResults;

        public WeatherLocationLookupService(
            IWeatherReverseGeocodingProvider reverseProvider = null,
            IWeatherLocationSearchProvider searchProvider = null)
        {
            NominatimWeatherLocationProvider defaultProvider = new();
            _reverseProvider = reverseProvider ?? defaultProvider;
            _searchProvider = searchProvider ?? defaultProvider;
        }

        public async Task<WeatherLocationLookupResult> LookupAsync(float latitude, float longitude)
        {
            string queryKey = BuildQueryKey(latitude, longitude);
            if (_cachedReverseResult != null && string.Equals(_lastReverseQueryKey, queryKey, StringComparison.Ordinal))
            {
                return _cachedReverseResult;
            }

            _reverseLookupCancellationTokenSource?.Cancel();
            _reverseLookupCancellationTokenSource?.Dispose();
            _reverseLookupCancellationTokenSource = new CancellationTokenSource();

            _lastReverseQueryKey = queryKey;
            _cachedReverseResult = await _reverseProvider.ReverseGeocodeAsync(latitude, longitude, _reverseLookupCancellationTokenSource.Token);
            return _cachedReverseResult;
        }

        public async Task<WeatherLocationSearchResult[]> SearchAsync(string query, int maxResults = 6)
        {
            string normalizedQuery = WeatherLocationSearchTextUtility.Normalize(query);
            string queryKey = normalizedQuery + "|" + maxResults;
            if (_cachedSearchResults != null && string.Equals(_lastSearchQueryKey, queryKey, StringComparison.Ordinal))
            {
                return _cachedSearchResults;
            }

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = new CancellationTokenSource();

            _lastSearchQueryKey = queryKey;
            _cachedSearchResults = await _searchProvider.SearchAsync(normalizedQuery, maxResults, _searchCancellationTokenSource.Token);
            return _cachedSearchResults;
        }

        public void Dispose()
        {
            _reverseLookupCancellationTokenSource?.Cancel();
            _reverseLookupCancellationTokenSource?.Dispose();
            _reverseLookupCancellationTokenSource = null;

            _searchCancellationTokenSource?.Cancel();
            _searchCancellationTokenSource?.Dispose();
            _searchCancellationTokenSource = null;
        }

        private static string BuildQueryKey(float latitude, float longitude)
        {
            return Math.Round(latitude, 3).ToString(CultureInfo.InvariantCulture) + ":" +
                   Math.Round(longitude, 3).ToString(CultureInfo.InvariantCulture);
        }
    }

    public sealed class NominatimWeatherLocationProvider : IWeatherReverseGeocodingProvider, IWeatherLocationSearchProvider
    {
        private const string ReverseEndpointTemplate =
            "https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat={0}&lon={1}&zoom=10&addressdetails=1&accept-language=en";
        private const string SearchEndpointTemplate =
            "https://nominatim.openstreetmap.org/search?format=jsonv2&addressdetails=1&limit={0}&accept-language=en&q={1}";
        private const string ElevationEndpointTemplate =
            "https://api.open-meteo.com/v1/elevation?latitude={0}&longitude={1}";

        public async Task<WeatherLocationLookupResult> ReverseGeocodeAsync(float latitude, float longitude, CancellationToken cancellationToken)
        {
            string url = string.Format(
                CultureInfo.InvariantCulture,
                ReverseEndpointTemplate,
                latitude,
                longitude);

            string payload = await SendRequestAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return WeatherLocationLookupResult.CreateFailure("Location unavailable");
            }

            ReverseGeocodeResponse response = JsonUtility.FromJson<ReverseGeocodeResponse>(payload);
            if (response == null || response.address == null)
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

        public async Task<WeatherLocationSearchResult[]> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Array.Empty<WeatherLocationSearchResult>();
            }

            await Task.Delay(250, cancellationToken);

            string normalizedQuery = WeatherLocationSearchTextUtility.Normalize(query);
            string requestQuery = string.IsNullOrWhiteSpace(normalizedQuery) ? query.Trim() : normalizedQuery;

            string url = string.Format(
                CultureInfo.InvariantCulture,
                SearchEndpointTemplate,
                Mathf.Max(6, maxResults * 4),
                UnityWebRequest.EscapeURL(requestQuery));

            string payload = await SendRequestAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return Array.Empty<WeatherLocationSearchResult>();
            }

            SearchResponseWrapper wrapper = JsonUtility.FromJson<SearchResponseWrapper>("{\"items\":" + payload + "}");
            if (wrapper?.items == null || wrapper.items.Length == 0)
            {
                return Array.Empty<WeatherLocationSearchResult>();
            }

            WeatherLocationSearchResult[] results = new WeatherLocationSearchResult[Mathf.Min(wrapper.items.Length, Mathf.Max(1, maxResults))];
            int resultCount = 0;

            for (int index = 0; index < wrapper.items.Length; index++)
            {
                SearchResponseItem item = wrapper.items[index];
                if (!TryParseCoordinate(item.lat, out float latitude) || !TryParseCoordinate(item.lon, out float longitude))
                {
                    continue;
                }

                string primaryName = BuildSearchPrimaryName(item);
                if (!IsRelevantSearchMatch(normalizedQuery, primaryName, item.display_name))
                {
                    continue;
                }

                string shortLabel = BuildSearchShortLabel(item);
                if (ContainsLocation(results, resultCount, shortLabel, latitude, longitude))
                {
                    continue;
                }

                results[resultCount++] = new WeatherLocationSearchResult(shortLabel, item.display_name, latitude, longitude);
                if (resultCount >= results.Length)
                {
                    break;
                }
            }

            WeatherLocationSearchResult[] compacted = CompactResults(results, resultCount);
            await PopulateAltitudesAsync(compacted, cancellationToken);
            return compacted;
        }

        private static async Task<string> SendRequestAsync(string url, CancellationToken cancellationToken)
        {
            using UnityWebRequest request = UnityWebRequest.Get(url);
            request.timeout = 5;
            request.SetRequestHeader("User-Agent", "ConceptFactoryWeatherEditor/1.0");
            UnityWebRequestAsyncOperation operation = request.SendWebRequest();

            while (!operation.isDone)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await Task.Yield();
            }

            if (request.result != UnityWebRequest.Result.Success)
            {
                return null;
            }

            return request.downloadHandler.text;
        }

        private static async Task PopulateAltitudesAsync(WeatherLocationSearchResult[] results, CancellationToken cancellationToken)
        {
            if (results == null || results.Length == 0)
            {
                return;
            }

            string[] latitudeValues = new string[results.Length];
            string[] longitudeValues = new string[results.Length];
            for (int index = 0; index < results.Length; index++)
            {
                WeatherLocationSearchResult result = results[index];
                latitudeValues[index] = result.Latitude.ToString(CultureInfo.InvariantCulture);
                longitudeValues[index] = result.Longitude.ToString(CultureInfo.InvariantCulture);
            }

            string url = string.Format(
                CultureInfo.InvariantCulture,
                ElevationEndpointTemplate,
                string.Join(",", latitudeValues),
                string.Join(",", longitudeValues));

            string payload = await SendRequestAsync(url, cancellationToken);
            if (string.IsNullOrWhiteSpace(payload))
            {
                return;
            }

            ElevationResponse response = JsonUtility.FromJson<ElevationResponse>(payload);
            if (response?.elevation == null || response.elevation.Length == 0)
            {
                return;
            }

            int count = Mathf.Min(results.Length, response.elevation.Length);
            for (int index = 0; index < count; index++)
            {
                results[index].AltitudeMeters = response.elevation[index];
            }
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

        private static bool TryParseCoordinate(string value, out float coordinate)
        {
            return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out coordinate);
        }

        private static string BuildSearchPrimaryName(SearchResponseItem item)
        {
            return FirstNonEmpty(item.address.city, item.address.town, item.address.village, item.address.municipality, item.name);
        }

        private static string BuildSearchShortLabel(SearchResponseItem item)
        {
            string city = FirstNonEmpty(item.address.city, item.address.town, item.address.village, item.address.municipality, item.name, item.address.county);

            if (string.IsNullOrWhiteSpace(city))
            {
                city = "Unknown place";
            }

            return city;
        }

        private static bool IsRelevantSearchMatch(string normalizedQuery, string primaryName, string fullLabel)
        {
            if (string.IsNullOrWhiteSpace(normalizedQuery))
            {
                return false;
            }

            string normalizedPrimaryName = WeatherLocationSearchTextUtility.Normalize(primaryName);
            if (!string.IsNullOrWhiteSpace(normalizedPrimaryName) && normalizedPrimaryName.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                return true;
            }

            string normalizedFullLabel = WeatherLocationSearchTextUtility.Normalize(fullLabel);
            return normalizedFullLabel.StartsWith(normalizedQuery, StringComparison.Ordinal);
        }

        private static bool ContainsLocation(WeatherLocationSearchResult[] results, int resultCount, string shortLabel, float latitude, float longitude)
        {
            for (int index = 0; index < resultCount; index++)
            {
                WeatherLocationSearchResult existingResult = results[index];
                if (existingResult == null)
                {
                    continue;
                }

                bool sameName = string.Equals(existingResult.ShortLabel, shortLabel, StringComparison.OrdinalIgnoreCase);
                bool sameCoordinates = Math.Abs(existingResult.Latitude - latitude) < 0.001f && Math.Abs(existingResult.Longitude - longitude) < 0.001f;
                if (sameName || sameCoordinates)
                {
                    return true;
                }
            }

            return false;
        }

        private static WeatherLocationSearchResult[] CompactResults(WeatherLocationSearchResult[] results, int resultCount)
        {
            if (resultCount <= 0)
            {
                return Array.Empty<WeatherLocationSearchResult>();
            }

            if (resultCount == results.Length)
            {
                return results;
            }

            WeatherLocationSearchResult[] compacted = new WeatherLocationSearchResult[resultCount];
            Array.Copy(results, compacted, resultCount);
            return compacted;
        }

        [Serializable]
        private sealed class ReverseGeocodeResponse
        {
            public string display_name;
            public ReverseGeocodeAddress address = new();
        }

        [Serializable]
        private sealed class SearchResponseWrapper
        {
            public SearchResponseItem[] items;
        }

        [Serializable]
        private sealed class SearchResponseItem
        {
            public string lat;
            public string lon;
            public string name;
            public string display_name;
            public ReverseGeocodeAddress address = new();
        }

        [Serializable]
        private sealed class ElevationResponse
        {
            public float[] elevation;
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

    public sealed class WeatherLocationSearchResult
    {
        public WeatherLocationSearchResult(string shortLabel, string fullLabel, float latitude, float longitude, float altitudeMeters = 0f)
        {
            ShortLabel = shortLabel;
            FullLabel = fullLabel;
            Latitude = latitude;
            Longitude = longitude;
            AltitudeMeters = altitudeMeters;
        }

        public string ShortLabel { get; }
        public string FullLabel { get; }
        public float Latitude { get; }
        public float Longitude { get; }
        public float AltitudeMeters { get; set; }
    }

    internal static class WeatherLocationSearchTextUtility
    {
        public static string Normalize(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder builder = new(normalized.Length);
            for (int index = 0; index < normalized.Length; index++)
            {
                char character = normalized[index];
                UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(character);
                if (category == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(character) || char.IsWhiteSpace(character))
                {
                    builder.Append(character);
                }
            }

            return builder.ToString().Normalize(NormalizationForm.FormC);
        }
    }
}
