using ConceptFactory.Weather.Editor.Location;
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConceptFactory.Weather.Editor
{
    [CustomEditor(typeof(LunarController))]
    public sealed class LunarControllerEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.conceptfactory.weather/Editor/UI/LunarControllerEditor.uxml";
        private const string MoonBannersPath = "Packages/com.conceptfactory.weather/Editor/UI/Sprites/MoonBanners.png";
        private const string Moons2BannerPath = "Packages/com.conceptfactory.weather/Editor/UI/Sprites/Moons2Banner.png";
        private const string MoonTitlesPath = "Packages/com.conceptfactory.weather/Editor/UI/Sprites/MoonTitles.png";

        private static readonly List<GmtOption> GmtOptions = new()
        {
            new GmtOption(-12f, "GMT-12:00 | Baker Island", 0f, -180f),
            new GmtOption(-11f, "GMT-11:00 | Pago Pago", -14.2756f, -170.702f),
            new GmtOption(-10f, "GMT-10:00 | Honolulu", 21.3099f, -157.8581f),
            new GmtOption(-9.5f, "GMT-09:30 | Marquesas", -9.4333f, -171.75f),
            new GmtOption(-9f, "GMT-09:00 | Anchorage", 61.2181f, -149.9003f),
            new GmtOption(-8f, "GMT-08:00 | Los Angeles", 34.0522f, -118.2437f),
            new GmtOption(-7f, "GMT-07:00 | Denver", 39.7392f, -104.9903f),
            new GmtOption(-6f, "GMT-06:00 | Mexico City", 19.4326f, -99.1332f),
            new GmtOption(-5f, "GMT-05:00 | New York", 40.7128f, -74.006f),
            new GmtOption(-4f, "GMT-04:00 | Santiago", -33.4489f, -70.6693f),
            new GmtOption(-3.5f, "GMT-03:30 | St. John's", 47.5615f, -52.7126f),
            new GmtOption(-3f, "GMT-03:00 | Brasilia", -15.7939f, -47.8828f),
            new GmtOption(-2f, "GMT-02:00 | Ushuaia", -54.8019f, -68.303f),
            new GmtOption(-1f, "GMT-01:00 | Las Palmas", 28.1235f, -15.4363f),
            new GmtOption(0f, "GMT+00:00 | London", 51.5072f, -0.1276f),
            new GmtOption(1f, "GMT+01:00 | Paris", 48.8566f, 2.3522f),
            new GmtOption(2f, "GMT+02:00 | Istanbul", 41.0082f, 28.9784f),
            new GmtOption(3f, "GMT+03:00 | Moscow", 55.7558f, 37.6173f),
            new GmtOption(3.5f, "GMT+03:30 | Tehran", 35.6892f, 51.389f),
            new GmtOption(4f, "GMT+04:00 | Dubai", 25.2048f, 55.2708f),
            new GmtOption(4.5f, "GMT+04:30 | Kabul", 34.5553f, 69.2075f),
            new GmtOption(5f, "GMT+05:00 | Tashkent", 41.2995f, 69.2401f),
            new GmtOption(5.5f, "GMT+05:30 | New Delhi", 28.6139f, 77.209f),
            new GmtOption(5.75f, "GMT+05:45 | Kathmandu", 27.7172f, 85.324f),
            new GmtOption(6f, "GMT+06:00 | Dhaka", 23.8103f, 90.4125f),
            new GmtOption(6.5f, "GMT+06:30 | Yangon", 16.8409f, 96.1735f),
            new GmtOption(7f, "GMT+07:00 | Bangkok", 13.7563f, 100.5018f),
            new GmtOption(8f, "GMT+08:00 | Singapore", 1.3521f, 103.8198f),
            new GmtOption(8.75f, "GMT+08:45 | Perth", -31.9523f, 115.8613f),
            new GmtOption(9f, "GMT+09:00 | Tokyo", 35.6762f, 139.6503f),
            new GmtOption(9.5f, "GMT+09:30 | Darwin", -12.4634f, 130.8456f),
            new GmtOption(10f, "GMT+10:00 | Sydney", -33.8688f, 151.2093f),
            new GmtOption(10.5f, "GMT+10:30 | Adelaide", -34.9285f, 138.6007f),
            new GmtOption(11f, "GMT+11:00 | Port Moresby", -9.4438f, 147.1803f),
            new GmtOption(12f, "GMT+12:00 | Auckland", -36.8485f, 174.7633f),
            new GmtOption(12.75f, "GMT+12:45 | Chatham Islands", -43.9535f, -176.5597f),
            new GmtOption(13f, "GMT+13:00 | Suva", -18.1248f, 178.4501f),
            new GmtOption(13.75f, "GMT+13:45 | Chatham Islands", -43.95f, -176.55f),
            new GmtOption(14f, "GMT+14:00 | Kiritimati", 1.8721f, -157.4278f)
        };

        private SerializedObject _solarSerializedObject;
        private WeatherSolarController _cachedSolarController;
        private WeatherLocationLookupService _locationLookupService;
        private Image _heroBanner;
        private VisualElement _heroMoonTitle;
        private Image _heroMoonTitleImage;
        private Label _heroStatus;
        private Label _dateSummaryLabel;
        private Label _digitalTimeLabel;
        private Button _openDatePickerButton;
        private Button _setTodayButton;
        private Button _playSimulationButton;
        private Button _pauseSimulationButton;
        private Button _stopSimulationButton;
        private DropdownField _utcDropdown;
        private Label _locationFeedbackLabel;
        private VisualElement _locationSearchPopupHost;
        private WeatherLocationSearchPopupElement _locationSearchPopupElement;
        private string _pendingLocationFeedbackKey;
        private string _resolvedLocationFeedbackKey;
        private string _currentCustomGmtLabel;
        private string _resolvedLocationName;

        public override VisualElement CreateInspectorGUI()
        {
            _locationLookupService ??= new WeatherLocationLookupService();
            serializedObject.Update();
            RefreshSolarSerializedObject();

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            VisualElement root = visualTree != null ? visualTree.CloneTree() : new VisualElement();

            CacheElements(root);
            BuildDateTimeCard(root.Q<VisualElement>("dateTimeCard"));
            BuildSimulationFields(root.Q<VisualElement>("simulationCard"));
            BuildLocationFields(root.Q<VisualElement>("locationFields"));
            PopulateLunarFields(root.Q<VisualElement>("referenceFields"), "_solarController", "_moonLight", "_moonTransformOverride");
            PopulateLunarFields(root.Q<VisualElement>("lightFields"), "_lightColorOverNight", "_lightIntensityOverNight", "_baseIntensity", "_disableLightBelowHorizon", "_horizonDisableThreshold", "_daylightFadeStrength", "_minimumPhaseLight");
            BuildSkyFields(root.Q<VisualElement>("skyFields"));
            BuildHaloFields(root.Q<VisualElement>("haloFields"));
            PopulateLunarFields(root.Q<VisualElement>("horizonFields"), "_enableHorizonMoonIllusion", "_horizonIllusionMaxElevation", "_horizonMoonSizeMultiplier", "_moonRiseSizeCurve", "_horizonMoonIntensityMultiplier", "_horizonMoonFalloffMultiplier", "_horizonMoonWarmTintStrength", "_horizonMoonTintColor", "_moonRiseWarmTintBoost");
            PopulateLunarFields(root.Q<VisualElement>("altitudeFields"), "_altitudeAtmosphereMaxMeters", "_altitudeWarmTintReduction", "_altitudeClarityBoost");
            PopulateDebugFields(root.Q<VisualElement>("debugFields"));

            RefreshHero();
            RefreshSolarMirrors();

            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshSolarSerializedObject();
                RefreshHero();
                RefreshSolarMirrors();
            });

            root.schedule.Execute(RefreshSolarMirrors).Every(150);

            return root;
        }

        private void OnDisable()
        {
            CloseLocationSearchPopup();
            _locationLookupService?.Dispose();
            _locationLookupService = null;
            _solarSerializedObject = null;
            _cachedSolarController = null;
        }

        private void CacheElements(VisualElement root)
        {
            _heroBanner = root.Q<Image>("heroBanner");
            _heroMoonTitle = root.Q<VisualElement>("heroMoonTitle");
            _heroMoonTitleImage = root.Q<Image>("heroMoonTitleImage");
            _heroStatus = root.Q<Label>("heroStatus");
            _dateSummaryLabel = root.Q<Label>("dateSummaryLabel");
            _digitalTimeLabel = root.Q<Label>("digitalTimeLabel");
            _openDatePickerButton = root.Q<Button>("openDatePickerButton");
            _setTodayButton = root.Q<Button>("setTodayButton");
            _playSimulationButton = root.Q<Button>("playSimulationButton");
            _pauseSimulationButton = root.Q<Button>("pauseSimulationButton");
            _stopSimulationButton = root.Q<Button>("stopSimulationButton");
            _locationFeedbackLabel = root.Q<Label>("locationFeedbackLabel");
            _locationSearchPopupHost = root.Q<VisualElement>("locationSearchPopupHost");
        }

        private void RefreshSolarSerializedObject()
        {
            LunarController lunarController = target as LunarController;
            WeatherSolarController solarController = lunarController != null ? lunarController.SolarController : null;
            if (_cachedSolarController == solarController && (_solarSerializedObject != null || solarController == null))
            {
                _solarSerializedObject?.Update();
                return;
            }

            _cachedSolarController = solarController;
            _solarSerializedObject = solarController != null ? new SerializedObject(solarController) : null;
        }
        private void BuildDateTimeCard(VisualElement card)
        {
            if (_openDatePickerButton != null)
            {
                _openDatePickerButton.clicked += OpenDatePickerPopup;
            }

            if (_setTodayButton != null)
            {
                _setTodayButton.clicked += SetTodayDate;
            }

            VisualElement timeFields = card?.Q<VisualElement>("timeFields");
            if (timeFields == null)
            {
                return;
            }

            timeFields.Clear();
            timeFields.Add(CreateTimeOfDayControl());
            timeFields.SetEnabled(_solarSerializedObject != null);
        }

        private VisualElement CreateTimeOfDayControl()
        {
            SerializedProperty property = FindSolarProperty("_timeOfDayMinutes");
            if (property == null)
            {
                return new VisualElement();
            }

            VisualElement row = new VisualElement();
            row.AddToClassList("cfw-time-row");

            PropertyField timeField = new PropertyField(property, "Time Of Day");
            timeField.Bind(_solarSerializedObject);
            timeField.AddToClassList("cfw-time-row__field");

            Button decrementButton = new Button(() => AdjustTimeOfDayMinutes(-1)) { text = "-" };
            decrementButton.AddToClassList("cfw-time-step-button");

            Button incrementButton = new Button(() => AdjustTimeOfDayMinutes(1)) { text = "+" };
            incrementButton.AddToClassList("cfw-time-step-button");

            row.Add(timeField);
            row.Add(decrementButton);
            row.Add(incrementButton);
            return row;
        }

        private void AdjustTimeOfDayMinutes(int deltaMinutes)
        {
            SerializedProperty property = FindSolarProperty("_timeOfDayMinutes");
            if (property == null)
            {
                return;
            }

            _solarSerializedObject.Update();
            property.floatValue = Mathf.Repeat(property.floatValue + deltaMinutes, 1440f);
            _solarSerializedObject.ApplyModifiedProperties();
            _solarSerializedObject.Update();
            RefreshSolarMirrors();
        }

        private void BuildSimulationFields(VisualElement card)
        {
            if (_playSimulationButton != null)
            {
                _playSimulationButton.clicked += () => InvokeSimulationControl(controller => controller.PlaySimulation());
            }

            if (_pauseSimulationButton != null)
            {
                _pauseSimulationButton.clicked += () => InvokeSimulationControl(controller => controller.PauseSimulation());
            }

            if (_stopSimulationButton != null)
            {
                _stopSimulationButton.clicked += () => InvokeSimulationControl(controller => controller.StopSimulation());
            }

            PopulateSolarFields(card?.Q<VisualElement>("simulationFields"), "_dayDurationMinutes", "_playOnAwake");
            if (card != null)
            {
                card.SetEnabled(_cachedSolarController != null);
            }
        }

        private void BuildLocationFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            ConfigureLocationFeedbackTrigger();
            PropertyField latitudeField = CreateSolarPropertyField("_latitude");
            PropertyField longitudeField = CreateSolarPropertyField("_longitude");
            RegisterLocationSync(latitudeField);
            RegisterLocationSync(longitudeField);

            container.Clear();
            if (latitudeField != null) container.Add(latitudeField);
            if (longitudeField != null) container.Add(longitudeField);

            VisualElement utcDropdown = CreateUtcDropdown();
            if (utcDropdown != null) container.Add(utcDropdown);

            PropertyField altitudeField = CreateSolarPropertyField("_altitudeMeters");
            if (altitudeField != null) container.Add(altitudeField);

            container.SetEnabled(_solarSerializedObject != null);
        }

        private void ConfigureLocationFeedbackTrigger()
        {
            if (_locationFeedbackLabel == null)
            {
                return;
            }

            _locationFeedbackLabel.AddToClassList("cfw-location-feedback--clickable");
            _locationFeedbackLabel.tooltip = "Search and apply a known location";
            _locationFeedbackLabel.RegisterCallback<ClickEvent>(_ => ToggleLocationSearchPopup());
        }

        private void ToggleLocationSearchPopup()
        {
            if (_locationSearchPopupHost == null || _solarSerializedObject == null)
            {
                return;
            }

            if (_locationSearchPopupElement != null)
            {
                CloseLocationSearchPopup();
                return;
            }

            _locationSearchPopupHost.Clear();
            _locationSearchPopupHost.AddToClassList("cfw-location-search-host--open");
            _locationSearchPopupElement = new WeatherLocationSearchPopupElement(
                _solarSerializedObject,
                _locationLookupService,
                HandleLocationSearchApplied,
                CloseLocationSearchPopup);
            _locationSearchPopupHost.Add(_locationSearchPopupElement);
        }

        private void HandleLocationSearchApplied(WeatherLocationSearchResult selectedLocation)
        {
            TrySnapLocationToKnownGmtOption(selectedLocation);
            _pendingLocationFeedbackKey = null;
            _resolvedLocationFeedbackKey = null;
            _resolvedLocationName = null;
            _solarSerializedObject?.Update();
            RefreshSolarMirrors();
            CloseLocationSearchPopup();
        }

        private void CloseLocationSearchPopup()
        {
            if (_locationSearchPopupHost != null)
            {
                _locationSearchPopupHost.Clear();
                _locationSearchPopupHost.RemoveFromClassList("cfw-location-search-host--open");
            }

            _locationSearchPopupElement = null;
        }

        private void RegisterLocationSync(PropertyField field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshSolarMirrors());
        }

        private VisualElement CreateUtcDropdown()
        {
            SerializedProperty property = FindSolarProperty("_utcOffsetHours");
            if (property == null)
            {
                return null;
            }

            VisualElement row = new VisualElement();
            _utcDropdown = new DropdownField("UTC Offset") { choices = new List<string>() };
            foreach (GmtOption option in GmtOptions)
            {
                _utcDropdown.choices.Add(option.Label);
            }

            _utcDropdown.value = FindClosestGmtLabel(property.floatValue);
            _utcDropdown.RegisterValueChangedCallback(evt =>
            {
                if (_solarSerializedObject == null)
                {
                    return;
                }

                _solarSerializedObject.Update();
                SerializedProperty utcProperty = FindSolarProperty("_utcOffsetHours");
                if (utcProperty == null)
                {
                    return;
                }

                GmtOption option = FindOptionFromDropdownValue(evt.newValue, utcProperty.floatValue);
                utcProperty.floatValue = option.OffsetHours;
                ApplyDefaultLocationForGmt(option);
                RemoveCustomGmtChoice();
                _solarSerializedObject.ApplyModifiedProperties();
                _solarSerializedObject.Update();
                RefreshSolarMirrors();
            });

            row.Add(_utcDropdown);
            return row;
        }

        private void ApplyDefaultLocationForGmt(GmtOption option)
        {
            SerializedProperty latitude = FindSolarProperty("_latitude");
            SerializedProperty longitude = FindSolarProperty("_longitude");
            if (latitude != null)
            {
                latitude.floatValue = option.DefaultLatitude;
            }

            if (longitude != null)
            {
                longitude.floatValue = option.DefaultLongitude;
            }
        }
        private string FindClosestGmtLabel(float value)
        {
            GmtOption bestMatch = GmtOptions[0];
            float bestDistance = Mathf.Abs(bestMatch.OffsetHours - value);
            for (int index = 1; index < GmtOptions.Count; index++)
            {
                float distance = Mathf.Abs(GmtOptions[index].OffsetHours - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = GmtOptions[index];
                }
            }

            return bestMatch.Label;
        }

        private void RefreshSolarMirrors()
        {
            RefreshDateSummary();
            RefreshDigitalTime();
            RefreshUtcDropdownFromLocation();
            RefreshLocationFeedback();
            RefreshSimulationTransportState();
        }

        private void RefreshUtcDropdownFromLocation()
        {
            if (_utcDropdown == null || _solarSerializedObject == null)
            {
                return;
            }

            _solarSerializedObject.Update();
            SerializedProperty latitude = FindSolarProperty("_latitude");
            SerializedProperty longitude = FindSolarProperty("_longitude");
            SerializedProperty utcOffset = FindSolarProperty("_utcOffsetHours");
            if (latitude == null || longitude == null || utcOffset == null)
            {
                return;
            }

            GmtOption bestMatch = FindClosestGmtForLocation(latitude.floatValue, longitude.floatValue);
            utcOffset.floatValue = bestMatch.OffsetHours;
            _solarSerializedObject.ApplyModifiedPropertiesWithoutUndo();
            _solarSerializedObject.Update();

            string targetLabel = IsDefaultLocation(bestMatch, latitude.floatValue, longitude.floatValue)
                ? bestMatch.Label
                : BuildCustomGmtLabel(bestMatch.OffsetHours, _resolvedLocationName);
            EnsureCustomGmtChoice(targetLabel);
            _utcDropdown.SetValueWithoutNotify(targetLabel);
        }

        private void RefreshLocationFeedback()
        {
            if (_locationFeedbackLabel == null)
            {
                return;
            }

            if (_solarSerializedObject == null)
            {
                _locationFeedbackLabel.text = "Assign Solar Controller";
                return;
            }

            _solarSerializedObject.Update();
            SerializedProperty latitude = FindSolarProperty("_latitude");
            SerializedProperty longitude = FindSolarProperty("_longitude");
            if (latitude == null || longitude == null)
            {
                return;
            }

            string queryKey = Mathf.Round(latitude.floatValue * 1000f) + ":" + Mathf.Round(longitude.floatValue * 1000f);
            if (_pendingLocationFeedbackKey == queryKey)
            {
                _locationFeedbackLabel.text = "Resolving...";
                return;
            }

            if (_resolvedLocationFeedbackKey == queryKey && !string.IsNullOrWhiteSpace(_resolvedLocationName))
            {
                _locationFeedbackLabel.text = _resolvedLocationName;
                return;
            }

            _locationFeedbackLabel.text = "Resolve Location";
            ResolveLocationFeedbackAsync(queryKey, latitude.floatValue, longitude.floatValue);
        }

        private async void ResolveLocationFeedbackAsync(string queryKey, float latitude, float longitude)
        {
            if (_locationLookupService == null)
            {
                return;
            }

            _pendingLocationFeedbackKey = queryKey;
            if (_locationFeedbackLabel != null)
            {
                _locationFeedbackLabel.text = "Resolving...";
            }

            try
            {
                WeatherLocationLookupResult result = await _locationLookupService.LookupAsync(latitude, longitude);
                if (_pendingLocationFeedbackKey != queryKey || _locationFeedbackLabel == null)
                {
                    return;
                }

                _resolvedLocationFeedbackKey = queryKey;
                _resolvedLocationName = result != null && result.Success ? result.ShortLabel : "Location unavailable";
                _locationFeedbackLabel.text = _resolvedLocationName;
            }
            catch
            {
                if (_pendingLocationFeedbackKey == queryKey && _locationFeedbackLabel != null)
                {
                    _locationFeedbackLabel.text = "Location unavailable";
                }
            }
            finally
            {
                if (_pendingLocationFeedbackKey == queryKey)
                {
                    _pendingLocationFeedbackKey = null;
                }
            }
        }

        private void TrySnapLocationToKnownGmtOption(WeatherLocationSearchResult selectedLocation)
        {
            if (selectedLocation == null || _solarSerializedObject == null)
            {
                return;
            }

            GmtOption matchedOption = FindGmtOptionForLocation(selectedLocation);
            if (string.IsNullOrWhiteSpace(matchedOption.Label))
            {
                return;
            }

            _solarSerializedObject.Update();
            SerializedProperty latitude = FindSolarProperty("_latitude");
            SerializedProperty longitude = FindSolarProperty("_longitude");
            SerializedProperty utcOffset = FindSolarProperty("_utcOffsetHours");
            if (latitude == null || longitude == null || utcOffset == null)
            {
                return;
            }

            latitude.floatValue = matchedOption.DefaultLatitude;
            longitude.floatValue = matchedOption.DefaultLongitude;
            utcOffset.floatValue = matchedOption.OffsetHours;
            _solarSerializedObject.ApplyModifiedProperties();
        }

        private static GmtOption FindGmtOptionForLocation(WeatherLocationSearchResult selectedLocation)
        {
            string normalizedLocationName = NormalizeLocationLabel(selectedLocation.ShortLabel);
            if (string.IsNullOrWhiteSpace(normalizedLocationName))
            {
                return default;
            }

            for (int index = 0; index < GmtOptions.Count; index++)
            {
                GmtOption option = GmtOptions[index];
                string normalizedOptionName = NormalizeLocationLabel(ExtractOptionLocationName(option.Label));
                if (string.IsNullOrWhiteSpace(normalizedOptionName))
                {
                    continue;
                }

                if (normalizedLocationName == normalizedOptionName ||
                    normalizedLocationName.StartsWith(normalizedOptionName, StringComparison.Ordinal) ||
                    normalizedOptionName.StartsWith(normalizedLocationName, StringComparison.Ordinal))
                {
                    return option;
                }
            }

            return default;
        }

        private static string ExtractOptionLocationName(string optionLabel)
        {
            if (string.IsNullOrWhiteSpace(optionLabel))
            {
                return string.Empty;
            }

            int separatorIndex = optionLabel.IndexOf('|');
            if (separatorIndex < 0 || separatorIndex >= optionLabel.Length - 1)
            {
                return optionLabel.Trim();
            }

            return optionLabel[(separatorIndex + 1)..].Trim();
        }

        private static string NormalizeLocationLabel(string value)
        {
            return WeatherLocationSearchTextUtility.Normalize(value.Split(',')[0].Trim());
        }

        private void OpenDatePickerPopup()
        {
            if (_openDatePickerButton == null || _solarSerializedObject == null)
            {
                return;
            }

            Rect worldBound = _openDatePickerButton.worldBound;
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(new Vector2(worldBound.xMin, worldBound.yMax));
            Rect popupRect = new Rect(screenPosition.x, screenPosition.y, worldBound.width, worldBound.height);
            WeatherDatePickerPopupWindow.Show(popupRect, _solarSerializedObject, RefreshDateSummary);
        }
        private void SetTodayDate()
        {
            if (_cachedSolarController == null)
            {
                return;
            }

            DateTimeOffset now = DateTimeOffset.Now;
            DateTime today = now.DateTime.Date;
            float localUtcOffsetHours = (float)now.Offset.TotalHours;
            GmtOption machineOption = FindClosestGmtByOffset(localUtcOffsetHours);

            SerializedObject controllerObject = new SerializedObject(_cachedSolarController);
            controllerObject.Update();
            controllerObject.FindProperty("_year").intValue = today.Year;
            controllerObject.FindProperty("_month").intValue = today.Month;
            controllerObject.FindProperty("_day").intValue = today.Day;
            controllerObject.FindProperty("_utcOffsetHours").floatValue = machineOption.OffsetHours;
            controllerObject.FindProperty("_latitude").floatValue = machineOption.DefaultLatitude;
            controllerObject.FindProperty("_longitude").floatValue = machineOption.DefaultLongitude;
            controllerObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(_cachedSolarController);

            RefreshSolarSerializedObject();
            RefreshSolarMirrors();
        }

        private void RefreshDateSummary()
        {
            if (_dateSummaryLabel == null)
            {
                return;
            }

            if (_solarSerializedObject == null)
            {
                _dateSummaryLabel.text = "No Solar Controller";
                if (_openDatePickerButton != null)
                {
                    _openDatePickerButton.text = "No Date";
                    _openDatePickerButton.SetEnabled(false);
                }

                if (_setTodayButton != null)
                {
                    _setTodayButton.SetEnabled(false);
                }

                return;
            }

            _openDatePickerButton?.SetEnabled(true);
            _setTodayButton?.SetEnabled(true);

            _solarSerializedObject.Update();
            SerializedProperty year = FindSolarProperty("_year");
            SerializedProperty day = FindSolarProperty("_day");
            SerializedProperty month = FindSolarProperty("_month");
            if (year == null || day == null || month == null)
            {
                return;
            }

            string[] monthNames =
            {
                "Janeiro",
                "Fevereiro",
                "Marco",
                "Abril",
                "Maio",
                "Junho",
                "Julho",
                "Agosto",
                "Setembro",
                "Outubro",
                "Novembro",
                "Dezembro"
            };

            int monthIndex = Mathf.Clamp(month.intValue, 1, 12) - 1;
            string dateText = day.intValue.ToString("00") + " " + monthNames[monthIndex] + " " + year.intValue;
            _dateSummaryLabel.text = dateText;
            if (_openDatePickerButton != null)
            {
                _openDatePickerButton.text = dateText;
            }
        }

        private void RefreshDigitalTime()
        {
            if (_digitalTimeLabel == null)
            {
                return;
            }

            if (_solarSerializedObject == null)
            {
                _digitalTimeLabel.text = "--:--";
                return;
            }

            _solarSerializedObject.Update();
            SerializedProperty currentLocalTime = FindSolarProperty("_currentLocalTimeLabel");
            SerializedProperty timeOfDayMinutes = FindSolarProperty("_timeOfDayMinutes");
            if (currentLocalTime != null && !string.IsNullOrWhiteSpace(currentLocalTime.stringValue))
            {
                _digitalTimeLabel.text = currentLocalTime.stringValue;
                return;
            }

            if (timeOfDayMinutes == null)
            {
                return;
            }

            int totalMinutes = Mathf.Clamp(Mathf.RoundToInt(timeOfDayMinutes.floatValue), 0, 1439);
            _digitalTimeLabel.text = (totalMinutes / 60).ToString("00") + ":" + (totalMinutes % 60).ToString("00");
        }

        private void RefreshSimulationTransportState()
        {
            if (_cachedSolarController == null)
            {
                _playSimulationButton?.SetEnabled(false);
                _pauseSimulationButton?.SetEnabled(false);
                _stopSimulationButton?.SetEnabled(false);
                return;
            }

            _playSimulationButton?.SetEnabled(!_cachedSolarController.IsPlayingSimulation);
            _pauseSimulationButton?.SetEnabled(!_cachedSolarController.IsPausedSimulation);
            _stopSimulationButton?.SetEnabled(!_cachedSolarController.IsStoppedSimulation);
        }

        private void InvokeSimulationControl(Action<WeatherSolarController> action)
        {
            if (_cachedSolarController == null)
            {
                return;
            }

            Undo.RecordObject(_cachedSolarController, "Weather Solar Simulation Control");
            action(_cachedSolarController);
            EditorUtility.SetDirty(_cachedSolarController);
            RefreshSolarSerializedObject();
            RefreshSolarMirrors();
        }

        private void PopulateLunarFields(VisualElement container, params string[] propertyNames)
        {
            if (container == null)
            {
                return;
            }

            foreach (string propertyName in propertyNames)
            {
                PropertyField field = CreateLunarPropertyField(propertyName);
                if (field != null)
                {
                    container.Add(field);
                }
            }
        }

        private void PopulateSolarFields(VisualElement container, params string[] propertyNames)
        {
            if (container == null)
            {
                return;
            }

            foreach (string propertyName in propertyNames)
            {
                PropertyField field = CreateSolarPropertyField(propertyName);
                if (field != null)
                {
                    container.Add(field);
                }
            }
        }

        private void BuildSkyFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            AddSubsectionHeader(container, "DISK", "Moon disk, textures and phase shading sent to the sky shader.");
            AddLunarField(container, "_driveSkyboxMaterial", "Drive Skybox");
            AddLunarField(container, "_skyboxMoonIntensity", "Moon Intensity");
            AddLunarField(container, "_skyboxMoonFalloff", "Moon Falloff");
            AddLunarField(container, "_skyboxMoonSize", "Moon Size");
            AddLunarField(container, "_moonTexture", "Lit Texture");
            AddLunarField(container, "_darkMoonTexture", "Dark Texture");
            AddLunarField(container, "_moonPhaseMaskTexture", "Phase Mask Texture");
            AddLunarField(container, "_moonTextureExposure", "Lit Exposure");
            AddLunarField(container, "_darkMoonTextureExposure", "Dark Exposure");
            AddLunarField(container, "_terminatorSoftness", "Terminator Softness");
            AddLunarField(container, "_darkSideVisibility", "Dark Side Visibility");

            AddSubsectionHeader(container, "DAYLIGHT FADES", "Controls how the illuminated and dark lunar layers fade through dawn and dusk.");
            AddLunarField(container, "_daylightShadowFadeStart", "Shadow Fade Start");
            AddLunarField(container, "_daylightShadowFadeRange", "Shadow Fade Range");
            AddLunarField(container, "_daylightLitMoonFadeStart", "Lit Fade Start");
            AddLunarField(container, "_daylightLitMoonFadeRange", "Lit Fade Range");
            AddLunarField(container, "_duskLitMoonSuppression", "Dusk Suppression");
        }

        private void BuildHaloFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            AddSubsectionHeader(container, "SOFT HALO", "Wide atmospheric bloom hugging the visible moon disk.");
            AddLunarField(container, "_moonHaloColor", "Halo Color");
            AddLunarField(container, "_moonHaloIntensity", "Halo Intensity");
            AddLunarField(container, "_moonHaloInnerSize", "Halo Inner Size");
            AddLunarField(container, "_moonHaloOuterSize", "Halo Outer Size");
            AddLunarField(container, "_moonHaloTerminator", "Halo Softness");

            AddSubsectionHeader(container, "BORDER HALO", "Thin stylized rim used to accent the lunar edge in the sky.");
            AddLunarField(container, "_borderHaloColor", "Border Color");
            AddLunarField(container, "_borderHaloIntensity", "Border Intensity");
            AddLunarField(container, "_borderHaloInnerSize", "Border Inner Size");
            AddLunarField(container, "_borderHaloOuterSize", "Border Outer Size");
            AddLunarField(container, "_borderHaloTerminator", "Border Softness");
        }
        private void PopulateDebugFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            container.SetEnabled(false);
            AddLunarField(container, "_currentElevation", "Apparent Elevation");
            AddLunarField(container, "_currentAzimuth", "Azimuth");
            AddLunarField(container, "_currentIlluminationFraction", "Illumination Fraction");
            AddLunarField(container, "_currentMoonDirection", "Moon Direction");
            AddLunarField(container, "_currentLunarAgeDays", "Lunar Age Days");
            AddLunarField(container, "_currentPhaseLabel", "Phase Label");
            AddLunarField(container, "_currentPhase", "Phase Enum");
            AddLunarField(container, "_isMoonRising", "Moon Rising");
        }

        private void AddSubsectionHeader(VisualElement container, string title, string note)
        {
            Label titleLabel = new Label(title);
            titleLabel.AddToClassList("cfw-subsection");
            if (container.childCount == 0)
            {
                titleLabel.AddToClassList("cfw-subsection--first");
            }

            container.Add(titleLabel);
            if (string.IsNullOrWhiteSpace(note))
            {
                return;
            }

            Label noteLabel = new Label(note);
            noteLabel.AddToClassList("cfw-subsection-note");
            container.Add(noteLabel);
        }

        private void AddLunarField(VisualElement container, string propertyName, string labelOverride = null)
        {
            PropertyField field = CreateLunarPropertyField(propertyName, labelOverride);
            if (field != null)
            {
                container.Add(field);
            }
        }

        private PropertyField CreateLunarPropertyField(string propertyName, string labelOverride = null)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                return null;
            }

            PropertyField field = labelOverride == null ? new PropertyField(property) : new PropertyField(property, labelOverride);
            field.Bind(serializedObject);
            if (property.propertyType == SerializedPropertyType.AnimationCurve)
            {
                field.AddToClassList("cfw-curve-field");
            }

            return field;
        }

        private PropertyField CreateSolarPropertyField(string propertyName, string labelOverride = null)
        {
            SerializedProperty property = FindSolarProperty(propertyName);
            if (property == null)
            {
                return null;
            }

            PropertyField field = labelOverride == null ? new PropertyField(property) : new PropertyField(property, labelOverride);
            field.Bind(_solarSerializedObject);
            if (property.propertyType == SerializedPropertyType.AnimationCurve)
            {
                field.AddToClassList("cfw-curve-field");
            }

            return field;
        }

        private SerializedProperty FindSolarProperty(string propertyName)
        {
            return _solarSerializedObject?.FindProperty(propertyName);
        }

        private void RefreshHero()
        {
            serializedObject.Update();
            SerializedProperty phase = serializedObject.FindProperty("_currentPhase");
            SerializedProperty phaseLabel = serializedObject.FindProperty("_currentPhaseLabel");
            SerializedProperty illumination = serializedObject.FindProperty("_currentIlluminationFraction");
            SerializedProperty elevation = serializedObject.FindProperty("_currentElevation");
            SerializedProperty lunarAge = serializedObject.FindProperty("_currentLunarAgeDays");
            SerializedProperty rising = serializedObject.FindProperty("_isMoonRising");
            SerializedProperty solarController = serializedObject.FindProperty("_solarController");

            MoonPhase currentPhase = phase != null ? (MoonPhase)phase.enumValueIndex : MoonPhase.FullMoon;
            string currentPhaseLabel = phaseLabel != null && !string.IsNullOrWhiteSpace(phaseLabel.stringValue) ? phaseLabel.stringValue : currentPhase.ToString();
            float illuminationValue = illumination?.floatValue ?? 0f;
            float elevationValue = elevation?.floatValue ?? 0f;
            float lunarAgeValue = lunarAge?.floatValue ?? 0f;
            bool isRising = rising != null && rising.boolValue;
            bool hasSolarController = solarController != null && solarController.objectReferenceValue != null;

            if (_heroBanner != null)
            {
                _heroBanner.sprite = LoadPhaseBanner(currentPhase);
            }

            if (_heroMoonTitleImage != null)
            {
                _heroMoonTitleImage.sprite = LoadPhaseTitle(currentPhase);
            }

            if (_heroStatus != null)
            {
                _heroStatus.text = hasSolarController ? BuildHeroStatusText(illuminationValue, elevationValue, isRising) : "Assign a Weather Solar Controller to drive date, time and location";
            }

        }

        private static string BuildHeroStatusText(float illumination, float elevation, bool isRising)
        {
            string motionLabel = isRising ? "Rising" : "Setting";
            return illumination.ToString("P0") + " illuminated | " + motionLabel + " | " + elevation.ToString("0.0") + "° elevation";
        }

        private static Sprite LoadPhaseBanner(MoonPhase phase)
        {
            string spriteName = phase switch
            {
                MoonPhase.NewMoon => "Moon_NewMoon",
                MoonPhase.WaxingCrescent => "Moon_WaxingCrescent",
                MoonPhase.FirstQuarter => "Moon_FirstQuarter",
                MoonPhase.WaxingGibbous => "Moon_WaxingGibbous",
                MoonPhase.FullMoon => "Moon_FullMoon",
                MoonPhase.WaningGibbous => "Moon_WaningGibbous",
                MoonPhase.LastQuarter => "Moon_ThirdQuarter",
                _ => "Moon_WaningCrescent"
            };

            string assetPath = phase switch
            {
                MoonPhase.FirstQuarter => Moons2BannerPath,
                MoonPhase.WaxingGibbous => Moons2BannerPath,
                MoonPhase.WaningGibbous => Moons2BannerPath,
                MoonPhase.LastQuarter => Moons2BannerPath,
                _ => MoonBannersPath
            };

            return LoadSpriteByName(assetPath, spriteName);
        }

        private static Sprite LoadPhaseTitle(MoonPhase phase)
        {
            string spriteName = phase switch
            {
                MoonPhase.NewMoon => "NewMoon",
                MoonPhase.WaxingCrescent => "WaxingCrescent",
                MoonPhase.FirstQuarter => "FirstQuarter",
                MoonPhase.WaxingGibbous => "WaxingGibbous",
                MoonPhase.FullMoon => "FullMoon",
                MoonPhase.WaningGibbous => "WaningGibbous",
                MoonPhase.LastQuarter => "ThirdQuarter",
                _ => "WaningCrescent"
            };

            return LoadSpriteByName(MoonTitlesPath, spriteName);
        }

        private static Sprite LoadSpriteByName(string assetPath, string spriteName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
        }
        private void EnsureCustomGmtChoice(string label)
        {
            if (_utcDropdown == null)
            {
                return;
            }

            RemoveCustomGmtChoice();
            if (GmtOptions.Exists(option => option.Label == label))
            {
                _currentCustomGmtLabel = null;
                return;
            }

            _currentCustomGmtLabel = label;
            if (!_utcDropdown.choices.Contains(label))
            {
                _utcDropdown.choices.Add(label);
            }
        }

        private void RemoveCustomGmtChoice()
        {
            if (_utcDropdown == null || string.IsNullOrWhiteSpace(_currentCustomGmtLabel))
            {
                return;
            }

            _utcDropdown.choices.Remove(_currentCustomGmtLabel);
            _currentCustomGmtLabel = null;
        }

        private static string BuildCustomGmtLabel(float offsetHours, string resolvedLocationName)
        {
            string sign = offsetHours >= 0f ? "+" : "-";
            float absolute = Mathf.Abs(offsetHours);
            int hours = Mathf.FloorToInt(absolute);
            int minutes = Mathf.RoundToInt((absolute - hours) * 60f);
            if (string.IsNullOrWhiteSpace(resolvedLocationName))
            {
                return $"GMT{sign}{hours:00}:{minutes:00} Custom Location";
            }

            return $"GMT{sign}{hours:00}:{minutes:00} Custom - {resolvedLocationName}";
        }

        private static GmtOption FindOptionFromDropdownValue(string label, float fallbackOffset)
        {
            GmtOption existingOption = GmtOptions.Find(item => item.Label == label);
            if (!string.IsNullOrWhiteSpace(existingOption.Label))
            {
                return existingOption;
            }

            return FindClosestGmtByOffset(fallbackOffset);
        }

        private static GmtOption FindClosestGmtByOffset(float offsetHours)
        {
            GmtOption bestMatch = GmtOptions[0];
            float bestDistance = Mathf.Abs(bestMatch.OffsetHours - offsetHours);
            for (int index = 1; index < GmtOptions.Count; index++)
            {
                float distance = Mathf.Abs(GmtOptions[index].OffsetHours - offsetHours);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestMatch = GmtOptions[index];
                }
            }

            return bestMatch;
        }

        private static GmtOption FindClosestGmtForLocation(float latitude, float longitude)
        {
            GmtOption bestMatch = GmtOptions[0];
            float bestScore = CalculateLocationScore(latitude, longitude, bestMatch);
            for (int index = 1; index < GmtOptions.Count; index++)
            {
                float score = CalculateLocationScore(latitude, longitude, GmtOptions[index]);
                if (score < bestScore)
                {
                    bestScore = score;
                    bestMatch = GmtOptions[index];
                }
            }

            return bestMatch;
        }

        private static float CalculateLocationScore(float latitude, float longitude, GmtOption option)
        {
            float latitudeDelta = latitude - option.DefaultLatitude;
            float longitudeDelta = Mathf.DeltaAngle(longitude, option.DefaultLongitude);
            return (latitudeDelta * latitudeDelta) + (longitudeDelta * longitudeDelta);
        }

        private static bool IsDefaultLocation(GmtOption option, float latitude, float longitude)
        {
            const float threshold = 0.01f;
            return Mathf.Abs(latitude - option.DefaultLatitude) <= threshold &&
                   Mathf.Abs(longitude - option.DefaultLongitude) <= threshold;
        }

        private readonly struct GmtOption
        {
            public GmtOption(float offsetHours, string label, float defaultLatitude, float defaultLongitude)
            {
                OffsetHours = offsetHours;
                Label = label;
                DefaultLatitude = defaultLatitude;
                DefaultLongitude = defaultLongitude;
            }

            public float OffsetHours { get; }
            public string Label { get; }
            public float DefaultLatitude { get; }
            public float DefaultLongitude { get; }
        }
    }
}
