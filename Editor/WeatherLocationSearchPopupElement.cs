using ConceptFactory.Weather.Editor.Location;
using System;
using System.Threading;
using UnityEditor;
using UnityEngine.UIElements;

namespace ConceptFactory.Weather.Editor
{
    public sealed class WeatherLocationSearchPopupElement : VisualElement
    {
        private const string UxmlPath = "Packages/com.conceptfactory.weather/Editor/UI/WeatherLocationSearchPopupWindow.uxml";

        private readonly SerializedObject _serializedObject;
        private readonly WeatherLocationLookupService _lookupService;
        private readonly Action<WeatherLocationSearchResult> _onLocationApplied;
        private readonly Action _onClosed;

        private TextField _searchField;
        private Label _resultsCountLabel;
        private VisualElement _resultsCountContainer;
        private ScrollView _resultsScrollView;
        private string _currentSearchQuery;

        public WeatherLocationSearchPopupElement(
            SerializedObject sourceSerializedObject,
            WeatherLocationLookupService lookupService,
            Action<WeatherLocationSearchResult> onLocationApplied,
            Action onClosed = null)
        {
            _serializedObject = new SerializedObject(sourceSerializedObject.targetObjects);
            _lookupService = lookupService ?? new WeatherLocationLookupService();
            _onLocationApplied = onLocationApplied;
            _onClosed = onClosed;

            AddToClassList("cfw-location-search-inline");
            BuildUi();
        }

        private void BuildUi()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree != null)
            {
                Add(visualTree.CloneTree());
            }

            _searchField = this.Q<TextField>("locationSearchField");
            _resultsCountLabel = this.Q<Label>("locationSearchCount");
            _resultsCountContainer = this.Q<VisualElement>("LocationCount");
            _resultsScrollView = this.Q<ScrollView>("locationSearchResults");

            if (_searchField != null)
            {
                _searchField.RegisterValueChangedCallback(evt => RefreshSearchResults(evt.newValue));
                _searchField.SetValueWithoutNotify(string.Empty);
                _searchField.textEdition.placeholder = "<Search location>";
                _searchField.schedule.Execute(() => _searchField.Focus()).StartingIn(20);
            }

            RefreshSearchResults(string.Empty);
        }

        private async void RefreshSearchResults(string query)
        {
            _currentSearchQuery = query?.Trim() ?? string.Empty;
            RefreshResultsCountVisibility();

            if (_currentSearchQuery.Length < 2 || _lookupService == null)
            {
                SetResultsCountText(string.Empty);
                ShowResultsMessage("Type at least 2 letters to search for a city.");
                return;
            }

            string requestQuery = _currentSearchQuery;
            SetResultsCountText(string.Empty);
            ShowResultsMessage("Searching...");

            try
            {
                WeatherLocationSearchResult[] results = await _lookupService.SearchAsync(requestQuery);
                if (_resultsScrollView == null || !string.Equals(_currentSearchQuery, requestQuery, StringComparison.Ordinal))
                {
                    return;
                }

                PopulateResults(results);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                if (string.Equals(_currentSearchQuery, requestQuery, StringComparison.Ordinal))
                {
                    SetResultsCountText(string.Empty);
                    ShowResultsMessage("Search unavailable right now.");
                }
            }
        }

        private void PopulateResults(WeatherLocationSearchResult[] results)
        {
            if (_resultsScrollView == null)
            {
                return;
            }

            _resultsScrollView.Clear();

            if (results == null || results.Length == 0)
            {
                SetResultsCountText("0 found");
                ShowResultsMessage("No matching cities found.");
                return;
            }

            SetResultsCountText(results.Length == 1 ? "1 found" : results.Length + " found");
            foreach (WeatherLocationSearchResult result in results)
            {
                _resultsScrollView.Add(CreateResultButton(result));
            }
        }

        private void ShowResultsMessage(string message)
        {
            if (_resultsScrollView == null)
            {
                return;
            }

            _resultsScrollView.Clear();
            Label label = new(message);
            label.AddToClassList("cfw-location-search__message");
            _resultsScrollView.Add(label);
        }

        private void SetResultsCountText(string message)
        {
            if (_resultsCountLabel != null)
            {
                _resultsCountLabel.text = message;
            }
        }

        private void RefreshResultsCountVisibility()
        {
            if (_resultsCountContainer == null)
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_currentSearchQuery))
            {
                _resultsCountContainer.RemoveFromClassList("cfw-location-search__meta--visible");
                return;
            }

            _resultsCountContainer.AddToClassList("cfw-location-search__meta--visible");
        }

        private Button CreateResultButton(WeatherLocationSearchResult result)
        {
            Button button = new(() => ApplyLocation(result));
            button.AddToClassList("cfw-location-search__result");
            button.tooltip = result.FullLabel;

            Label title = new(result.ShortLabel);
            title.AddToClassList("cfw-location-search__result-title");
            button.Add(title);
            return button;
        }

        private void ApplyLocation(WeatherLocationSearchResult result)
        {
            if (_serializedObject == null)
            {
                Close();
                return;
            }

            _serializedObject.Update();

            SerializedProperty latitude = _serializedObject.FindProperty("_latitude");
            SerializedProperty longitude = _serializedObject.FindProperty("_longitude");
            if (latitude != null)
            {
                latitude.floatValue = result.Latitude;
            }

            if (longitude != null)
            {
                longitude.floatValue = result.Longitude;
            }

            _serializedObject.ApplyModifiedProperties();

            foreach (UnityEngine.Object currentTarget in _serializedObject.targetObjects)
            {
                EditorUtility.SetDirty(currentTarget);
            }

            _onLocationApplied?.Invoke(result);
            Close();
        }

        public void Close()
        {
            _onClosed?.Invoke();
            RemoveFromHierarchy();
        }
    }
}
