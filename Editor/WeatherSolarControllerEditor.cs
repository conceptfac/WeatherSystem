using ConceptFactory.Weather.Editor.Location;
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
namespace ConceptFactory.Weather.Editor
{
    [CustomEditor(typeof(WeatherSolarController))]
    public sealed class WeatherSolarControllerEditor : UnityEditor.Editor
    {
        private const string UxmlPath = "Packages/com.conceptfactory.weather/Editor/UI/WeatherSolarControllerEditor.uxml";
        private const string SeasonsBannerPath = "Packages/com.conceptfactory.weather/Editor/UI/Sprites/SeasonsBanner.png";
        private const string SeasonsTitlePath = "Packages/com.conceptfactory.weather/Editor/UI/Sprites/SeasonsTitle.png";
        private const string WeatherIconsPath = "Packages/com.conceptfactory.weather/Editor/UI/Sprites/WeatherIcons.png";
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

        private Label _dateSummaryLabel;
        private Label _digitalTimeLabel;
        private Button _openDatePickerButton;
        private Button _setTodayButton;
        private Button _playSimulationButton;
        private Button _pauseSimulationButton;
        private Button _stopSimulationButton;
        private DropdownField _utcDropdown;
        private Label _heroStatusLabel;
        private Image _heroBannerElement;
        private Image _heroSeasonTitleElement;
        private VisualElement _heroIconElement;
        private Label _locationFeedbackLabel;
        private WeatherLocationLookupService _locationLookupService;
        private string _pendingLocationFeedbackKey;
        private string _resolvedLocationFeedbackKey;
        private string _currentCustomGmtLabel;
        private string _resolvedLocationName;

        public override VisualElement CreateInspectorGUI()
        {
            _locationLookupService ??= new WeatherLocationLookupService();
            serializedObject.Update();

            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            VisualElement root = visualTree != null ? visualTree.CloneTree() : new VisualElement();

            ConfigureHeader(root);
            BuildDateTimeCard(root.Q<VisualElement>("dateTimeCard"));
            BuildLocationFields(root.Q<VisualElement>("locationFields"));
            PopulateFields(root.Q<VisualElement>("referenceFields"), "_sunTransformOverride");
            BuildSimulationFields(root.Q<VisualElement>("simulationCard"));
            PopulateFields(root.Q<VisualElement>("runtimeFields"), "_playOnAwake");
            PopulateFields(root.Q<VisualElement>("lightingFields"), "_baseIntensity", "_disableLightAtNight", "_nightDisableThreshold", "_lightColorOverDay", "_lightIntensityOverDay");
            BuildSkyboxFields(root.Q<VisualElement>("lightingFields"));
            PopulateDebugFields(root.Q<VisualElement>("debugFields"));

            root.TrackSerializedObjectValue(serializedObject, _ =>
            {
                RefreshUtcDropdownFromLocation();
                RefreshSeasonHero();
                RefreshLocationFeedback();
                RefreshDateSummary();
                RefreshDigitalTime();
                RefreshSimulationTransportState();
            });
            return root;
        }

        private void OnDisable()
        {
            _locationLookupService?.Dispose();
            _locationLookupService = null;
        }

        private void ConfigureHeader(VisualElement root)
        {
            Label title = root.Q<Label>("heroTitle");
            Label subtitle = root.Q<Label>("heroSubtitle");
            _heroStatusLabel = root.Q<Label>("heroStatus");
            _heroBannerElement = root.Q<Image>("heroBanner");
            _heroSeasonTitleElement = root.Q<Image>("heroSeasonTitle");
            _heroIconElement = root.Q<VisualElement>("heroIcon");

            if (title != null) title.text = "Weather Solar Controller";
            if (subtitle != null) subtitle.text = "Geographic sun positioning with seasonal daylight and real-time editor preview.";
            if (_heroStatusLabel != null) _heroStatusLabel.text = "Directional light required | Edit mode playback supported";
            if (_heroIconElement != null)
            {
                _heroIconElement.tooltip = "Astronomical sun solver";

                Sprite heroSprite = LoadSpriteByName(WeatherIconsPath, "ICO_SunCylce");
                if (heroSprite != null)
                {
                    _heroIconElement.style.backgroundImage = new StyleBackground(heroSprite);
                }
            }
            RefreshSeasonHero();
        }

        private void BuildDateTimeCard(VisualElement card)
        {
            _openDatePickerButton = card?.Q<Button>("openDatePickerButton");
            _setTodayButton = card?.Q<Button>("setTodayButton");
            _dateSummaryLabel = card?.Q<Label>("dateSummaryLabel");
            _digitalTimeLabel = card?.Q<Label>("digitalTimeLabel");

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

            timeFields.Add(CreatePropertyField("_timeOfDayMinutes", "Time Of Day"));
            RefreshDateSummary();
            RefreshDigitalTime();
        }

        private void BuildLocationFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            _locationFeedbackLabel = container.parent?.Q<Label>("locationFeedbackLabel");

            PropertyField latitudeField = CreatePropertyField("_latitude");
            PropertyField longitudeField = CreatePropertyField("_longitude");

            RegisterLocationSync(latitudeField);
            RegisterLocationSync(longitudeField);

            container.Add(latitudeField);
            container.Add(longitudeField);
            container.Add(CreateUtcDropdown());
            container.Add(CreatePropertyField("_altitudeMeters"));

            RefreshUtcDropdownFromLocation();
            RefreshLocationFeedback();
        }

        private void BuildSimulationFields(VisualElement card)
        {
            if (card == null)
            {
                return;
            }

            _playSimulationButton = card.Q<Button>("playSimulationButton");
            _pauseSimulationButton = card.Q<Button>("pauseSimulationButton");
            _stopSimulationButton = card.Q<Button>("stopSimulationButton");

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

            PopulateFields(card.Q<VisualElement>("simulationFields"), "_dayDurationMinutes");
            RefreshSimulationTransportState();
        }

        private void BuildSkyboxFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            VisualElement section = new VisualElement();
            section.style.marginTop = 12;

            Label title = new Label("Skybox");
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 6;
            section.Add(title);

            AddField(section, "_driveSkyboxMaterial", "Drive Skybox");
            AddField(section, "_skyColorOverDay", "Sky Color Over Day");
            AddField(section, "_horizonColorOverDay", "Horizon Color Over Day");
            AddField(section, "_groundColorOverDay", "Ground Color Over Day");
            AddField(section, "_skyIntensityOverDay", "Sky Intensity Curve");
            AddField(section, "_skyboxSunColorOverDay", "Sun Color Over Day");
            AddField(section, "_skyTopFalloff", "Sky Falloff");
            AddField(section, "_skyBottomFalloff", "Ground Falloff");
            AddField(section, "_skyboxSunIntensity", "Sun Intensity");
            AddField(section, "_skyboxSunFalloff", "Sun Falloff");
            AddField(section, "_skyboxSunSize", "Sun Size");
            AddField(section, "_skyboxSunSizeOverDay", "Sun Size Curve");

            container.Add(section);
        }

        private void AddField(VisualElement container, string propertyName, string labelOverride = null)
        {
            PropertyField field = CreatePropertyField(propertyName, labelOverride);
            if (field != null)
            {
                container.Add(field);
            }
        }

        private void PopulateFields(VisualElement container, params string[] propertyNames)
        {
            if (container == null)
            {
                return;
            }

            foreach (string propertyName in propertyNames)
            {
                PropertyField field = CreatePropertyField(propertyName);
                if (field != null)
                {
                    container.Add(field);
                }
            }
        }

        private void PopulateDebugFields(VisualElement container)
        {
            if (container == null)
            {
                return;
            }

            container.SetEnabled(false);
            container.Add(CreatePropertyField("_currentElevation", "Apparent Elevation"));
            container.Add(CreatePropertyField("_currentAzimuth", "Azimuth"));
            container.Add(CreatePropertyField("_currentDaylightFactor", "Daylight Factor"));
            container.Add(CreatePropertyField("_currentSunDirection", "Sun Direction"));
        }

        private PropertyField CreatePropertyField(string propertyName, string labelOverride = null, bool readOnly = false)
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

            if (readOnly)
            {
                field.SetEnabled(false);
            }

            return field;
        }

        private VisualElement CreateUtcDropdown()
        {
            SerializedProperty property = serializedObject.FindProperty("_utcOffsetHours");
            if (property == null)
            {
                return null;
            }

            VisualElement row = new VisualElement();
            _utcDropdown = new DropdownField("UTC Offset");
            _utcDropdown.choices = new List<string>();

            foreach (GmtOption option in GmtOptions)
            {
                _utcDropdown.choices.Add(option.Label);
            }

            _utcDropdown.value = FindClosestGmtLabel(property.floatValue);
            _utcDropdown.RegisterValueChangedCallback(evt =>
            {
                GmtOption option = FindOptionFromDropdownValue(evt.newValue, property.floatValue);
                property.floatValue = option.OffsetHours;
                ApplyDefaultLocationForGmt(option);
                RemoveCustomGmtChoice();
                serializedObject.ApplyModifiedProperties();
                serializedObject.Update();
                RefreshDateSummary();
                RefreshDigitalTime();
            });

            row.Add(_utcDropdown);
            return row;
        }

        private static string FindClosestGmtLabel(float value)
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

        private void ApplyDefaultLocationForGmt(GmtOption option)
        {
            SerializedProperty latitude = serializedObject.FindProperty("_latitude");
            SerializedProperty longitude = serializedObject.FindProperty("_longitude");

            if (latitude != null)
            {
                latitude.floatValue = option.DefaultLatitude;
            }

            if (longitude != null)
            {
                longitude.floatValue = option.DefaultLongitude;
            }
        }

        private void RegisterLocationSync(PropertyField field)
        {
            if (field == null)
            {
                return;
            }

            field.RegisterCallback<SerializedPropertyChangeEvent>(_ => RefreshUtcDropdownFromLocation());
        }

        private void RefreshUtcDropdownFromLocation()
        {
            if (_utcDropdown == null)
            {
                return;
            }

            serializedObject.Update();

            SerializedProperty latitude = serializedObject.FindProperty("_latitude");
            SerializedProperty longitude = serializedObject.FindProperty("_longitude");
            SerializedProperty utcOffset = serializedObject.FindProperty("_utcOffsetHours");
            if (latitude == null || longitude == null || utcOffset == null)
            {
                return;
            }

            GmtOption bestMatch = FindClosestGmtForLocation(latitude.floatValue, longitude.floatValue);
            utcOffset.floatValue = bestMatch.OffsetHours;
            serializedObject.ApplyModifiedProperties();

            string targetLabel = IsDefaultLocation(bestMatch, latitude.floatValue, longitude.floatValue)
                ? bestMatch.Label
                : BuildCustomGmtLabel(bestMatch.OffsetHours, _resolvedLocationName);

            EnsureCustomGmtChoice(targetLabel);

            if (_utcDropdown.value != targetLabel)
            {
                _utcDropdown.SetValueWithoutNotify(targetLabel);
            }
        }

        private async void RefreshLocationFeedback()
        {
            if (_locationFeedbackLabel == null || _locationLookupService == null)
            {
                return;
            }

            serializedObject.Update();

            SerializedProperty latitude = serializedObject.FindProperty("_latitude");
            SerializedProperty longitude = serializedObject.FindProperty("_longitude");
            if (latitude == null || longitude == null)
            {
                return;
            }

            string queryKey = Mathf.Round(latitude.floatValue * 1000f) + ":" + Mathf.Round(longitude.floatValue * 1000f);
            if (_pendingLocationFeedbackKey == queryKey)
            {
                return;
            }

            if (_resolvedLocationFeedbackKey == queryKey && !string.IsNullOrWhiteSpace(_resolvedLocationName))
            {
                return;
            }

            _pendingLocationFeedbackKey = queryKey;
            _locationFeedbackLabel.text = "Resolving...";

            try
            {
                WeatherLocationLookupResult result = await _locationLookupService.LookupAsync(latitude.floatValue, longitude.floatValue);
                if (_locationFeedbackLabel == null || _pendingLocationFeedbackKey != queryKey)
                {
                    return;
                }

                _pendingLocationFeedbackKey = null;
                _resolvedLocationFeedbackKey = queryKey;
                _locationFeedbackLabel.text = result.ShortLabel;
                _locationFeedbackLabel.tooltip = result.FullLabel;
                _resolvedLocationName = result.Success ? result.ShortLabel : null;
                RefreshUtcDropdownFromLocation();
            }
            catch
            {
                if (_locationFeedbackLabel == null || _pendingLocationFeedbackKey != queryKey)
                {
                    return;
                }

                _pendingLocationFeedbackKey = null;
                _resolvedLocationFeedbackKey = null;
                _locationFeedbackLabel.text = "Custom Location";
                _locationFeedbackLabel.tooltip = "Reverse geocoding unavailable";
                _resolvedLocationName = null;
                RefreshUtcDropdownFromLocation();
            }
        }

        private void InvokeSimulationControl(System.Action<WeatherSolarController> action)
        {
            foreach (UnityEngine.Object currentTarget in targets)
            {
                if (currentTarget is not WeatherSolarController controller)
                {
                    continue;
                }

                Undo.RecordObject(controller, "Weather Solar Simulation Control");
                action(controller);
                EditorUtility.SetDirty(controller);
            }

            serializedObject.Update();
            RefreshDateSummary();
            RefreshDigitalTime();
            RefreshSimulationTransportState();
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

        private void OpenDatePickerPopup()
        {
            if (_openDatePickerButton == null)
            {
                return;
            }

            Rect worldBound = _openDatePickerButton.worldBound;
            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(new Vector2(worldBound.xMin, worldBound.yMax));
            Rect popupRect = new Rect(screenPosition.x, screenPosition.y, worldBound.width, worldBound.height);

            WeatherDatePickerPopupWindow.Show(popupRect, serializedObject, RefreshDateSummary);
        }

        private void SetTodayDate()
        {
            DateTimeOffset now = DateTimeOffset.Now;
            DateTime today = now.DateTime.Date;
            float localUtcOffsetHours = (float)now.Offset.TotalHours;
            GmtOption machineOption = FindClosestGmtByOffset(localUtcOffsetHours);

            foreach (UnityEngine.Object currentTarget in targets)
            {
                if (currentTarget is not WeatherSolarController controller)
                {
                    continue;
                }

                SerializedObject controllerObject = new SerializedObject(controller);
                controllerObject.Update();
                controllerObject.FindProperty("_month").intValue = today.Month;
                controllerObject.FindProperty("_day").intValue = today.Day;
                controllerObject.FindProperty("_utcOffsetHours").floatValue = machineOption.OffsetHours;
                controllerObject.FindProperty("_latitude").floatValue = machineOption.DefaultLatitude;
                controllerObject.FindProperty("_longitude").floatValue = machineOption.DefaultLongitude;
                controllerObject.ApplyModifiedProperties();
                EditorUtility.SetDirty(controller);
            }

            serializedObject.Update();
            RefreshUtcDropdownFromLocation();
            RefreshLocationFeedback();
            RefreshDateSummary();
            RefreshSeasonHero();
        }

        private void RefreshDateSummary()
        {
            if (_dateSummaryLabel == null)
            {
                return;
            }

            serializedObject.Update();

            SerializedProperty day = serializedObject.FindProperty("_day");
            SerializedProperty month = serializedObject.FindProperty("_month");
            if (day == null || month == null)
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
            string dateText = day.intValue.ToString("00") + " " + monthNames[monthIndex];
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

            serializedObject.Update();

            SerializedProperty currentLocalTime = serializedObject.FindProperty("_currentLocalTimeLabel");
            SerializedProperty timeOfDayMinutes = serializedObject.FindProperty("_timeOfDayMinutes");

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
            int hours = totalMinutes / 60;
            int minutes = totalMinutes % 60;
            _digitalTimeLabel.text = hours.ToString("00") + ":" + minutes.ToString("00");
        }

        private void RefreshSimulationTransportState()
        {
            WeatherSolarController controller = target as WeatherSolarController;
            if (controller == null)
            {
                return;
            }

            if (_playSimulationButton != null)
            {
                _playSimulationButton.SetEnabled(!controller.IsPlayingSimulation);
            }

            if (_pauseSimulationButton != null)
            {
                _pauseSimulationButton.SetEnabled(!controller.IsPausedSimulation);
            }

            if (_stopSimulationButton != null)
            {
                _stopSimulationButton.SetEnabled(!controller.IsStoppedSimulation);
            }
        }

        private void RefreshSeasonHero()
        {
            if (_heroBannerElement == null)
            {
                return;
            }

            serializedObject.Update();

            SerializedProperty latitude = serializedObject.FindProperty("_latitude");
            SerializedProperty month = serializedObject.FindProperty("_month");
            SerializedProperty day = serializedObject.FindProperty("_day");
            if (latitude == null || month == null || day == null)
            {
                return;
            }

            SeasonType season = DetermineSeason(latitude.floatValue, month.intValue, day.intValue);
            Sprite seasonBanner = LoadSeasonBanner(season);
            Sprite seasonTitle = LoadSeasonTitle(season);
            _heroBannerElement.sprite = seasonBanner;
            if (_heroSeasonTitleElement != null)
            {
                _heroSeasonTitleElement.sprite = seasonTitle;
            }

            if (_heroStatusLabel != null)
            {
                _heroStatusLabel.text = GetSeasonLabel(season) + " detected for this location and date";
            }
        }

        private static SeasonType DetermineSeason(float latitude, int month, int day)
        {
            int value = (month * 100) + day;
            bool southernHemisphere = latitude < 0f;

            SeasonType northernSeason =
                value >= 320 && value < 621 ? SeasonType.Spring :
                value >= 621 && value < 923 ? SeasonType.Summer :
                value >= 923 && value < 1221 ? SeasonType.Autumn :
                SeasonType.Winter;

            if (!southernHemisphere)
            {
                return northernSeason;
            }

            return northernSeason switch
            {
                SeasonType.Spring => SeasonType.Autumn,
                SeasonType.Summer => SeasonType.Winter,
                SeasonType.Autumn => SeasonType.Spring,
                _ => SeasonType.Summer
            };
        }

        private static string GetSeasonLabel(SeasonType season)
        {
            return season switch
            {
                SeasonType.Spring => "Spring",
                SeasonType.Summer => "Summer",
                SeasonType.Autumn => "Autumn",
                _ => "Winter"
            };
        }

        private static Sprite LoadSeasonBanner(SeasonType season)
        {
            string spriteName = season switch
            {
                SeasonType.Spring => "Banner_Spring",
                SeasonType.Summer => "Banner_Summer",
                SeasonType.Autumn => "Banner_Autumn",
                _ => "Banner_Winter"
            };

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SeasonsBannerPath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
        }

        private static Sprite LoadSeasonTitle(SeasonType season)
        {
            string spriteName = season switch
            {
                SeasonType.Spring => "Title_Spring",
                SeasonType.Summer => "Title_Summer",
                SeasonType.Autumn => "Title_Autumn",
                _ => "Title_Winter"
            };

            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(SeasonsTitlePath);
            foreach (UnityEngine.Object asset in assets)
            {
                if (asset is Sprite sprite && sprite.name == spriteName)
                {
                    return sprite;
                }
            }

            return null;
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

        private enum SeasonType
        {
            Spring,
            Summer,
            Autumn,
            Winter
        }
    }
}
