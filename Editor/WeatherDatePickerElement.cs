using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConceptFactory.Weather.Editor
{
    public sealed class WeatherDatePickerElement : VisualElement
    {
        private const string UxmlPath = "Packages/com.conceptfactory.weather/Editor/UI/WeatherDatePicker.uxml";
        private const string UssPath = "Packages/com.conceptfactory.weather/Editor/UI/WeatherDatePicker.uss";

        private static readonly string[] MonthNames =
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

        private readonly Label _monthLabel;
        private readonly Label _summaryLabel;
        private readonly Button _previousMonthButton;
        private readonly Button _nextMonthButton;
        private readonly VisualElement _dayGrid;
        private readonly Button[] _dayButtons;

        private SerializedObject _serializedObject;
        private SerializedProperty _dayProperty;
        private SerializedProperty _monthProperty;
        private SerializedProperty _yearProperty;

        public event Action DateChanged;

        public WeatherDatePickerElement()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            if (visualTree != null)
            {
                visualTree.CloneTree(this);
            }

            StyleSheet fallbackStyleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
            if (fallbackStyleSheet != null)
            {
                styleSheets.Add(fallbackStyleSheet);
            }

            _monthLabel = this.Q<Label>("monthLabel");
            _summaryLabel = this.Q<Label>("datePickerSummary");
            _previousMonthButton = this.Q<Button>("previousMonthButton");
            _nextMonthButton = this.Q<Button>("nextMonthButton");
            _dayGrid = this.Q<VisualElement>("dayGrid");
            _dayButtons = new Button[31];

            BuildDayButtons();

            if (_previousMonthButton != null)
            {
                _previousMonthButton.clicked += () => ShiftMonth(-1);
            }

            if (_nextMonthButton != null)
            {
                _nextMonthButton.clicked += () => ShiftMonth(1);
            }
        }

        public void Bind(SerializedObject serializedObject, string dayPropertyName, string monthPropertyName, string yearPropertyName)
        {
            _serializedObject = serializedObject;
            _dayProperty = serializedObject.FindProperty(dayPropertyName);
            _monthProperty = serializedObject.FindProperty(monthPropertyName);
            _yearProperty = serializedObject.FindProperty(yearPropertyName);
            Refresh();
        }

        public void Refresh()
        {
            if (_serializedObject == null || _dayProperty == null || _monthProperty == null || _yearProperty == null)
            {
                SetEnabled(false);
                return;
            }

            SetEnabled(true);
            _serializedObject.Update();

            int currentMonth = Mathf.Clamp(_monthProperty.intValue, 1, 12);
            int currentYear = Mathf.Clamp(_yearProperty.intValue, 1, 9999);
            int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
            int currentDay = Mathf.Clamp(_dayProperty.intValue, 1, daysInMonth);

            if (_dayProperty.intValue != currentDay)
            {
                _dayProperty.intValue = currentDay;
                _serializedObject.ApplyModifiedProperties();
                _serializedObject.Update();
            }

            if (_monthLabel != null)
            {
                _monthLabel.text = MonthNames[currentMonth - 1];
            }

            if (_summaryLabel != null)
            {
                _summaryLabel.text = currentDay.ToString("00") + " " + MonthNames[currentMonth - 1];
            }

            for (int index = 0; index < _dayButtons.Length; index++)
            {
                int day = index + 1;
                Button button = _dayButtons[index];
                bool isAvailable = day <= daysInMonth;
                bool isSelected = day == currentDay;

                button.text = day.ToString();
                button.SetEnabled(isAvailable);
                button.EnableInClassList("cfw-date-picker__day-button--disabled", !isAvailable);
                button.EnableInClassList("cfw-date-picker__day-button--selected", isAvailable && isSelected);
            }
        }

        private void BuildDayButtons()
        {
            if (_dayGrid == null)
            {
                return;
            }

            _dayGrid.Clear();

            for (int index = 0; index < _dayButtons.Length; index++)
            {
                int day = index + 1;
                Button button = new Button(() => SetDay(day))
                {
                    text = day.ToString()
                };

                button.AddToClassList("cfw-date-picker__day-button");
                _dayButtons[index] = button;
                _dayGrid.Add(button);
            }
        }

        private void ShiftMonth(int delta)
        {
            if (_serializedObject == null || _monthProperty == null)
            {
                return;
            }

            _serializedObject.Update();

            int month = Mathf.Clamp(_monthProperty.intValue, 1, 12) + delta;
            if (month < 1)
            {
                month = 12;
            }
            else if (month > 12)
            {
                month = 1;
            }

            _monthProperty.intValue = month;
            ClampDayToCurrentMonth();
            _serializedObject.ApplyModifiedProperties();
            Refresh();
        }

        private void SetDay(int day)
        {
            if (_serializedObject == null || _dayProperty == null)
            {
                return;
            }

            _serializedObject.Update();
            int currentYear = Mathf.Clamp(_yearProperty.intValue, 1, 9999);
            int currentMonth = Mathf.Clamp(_monthProperty.intValue, 1, 12);
            int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);

            _dayProperty.intValue = Mathf.Clamp(day, 1, daysInMonth);
            _serializedObject.ApplyModifiedProperties();
            Refresh();
            DateChanged?.Invoke();
        }

        private void ClampDayToCurrentMonth()
        {
            if (_dayProperty == null || _monthProperty == null || _yearProperty == null)
            {
                return;
            }

            int currentYear = Mathf.Clamp(_yearProperty.intValue, 1, 9999);
            int currentMonth = Mathf.Clamp(_monthProperty.intValue, 1, 12);
            int daysInMonth = DateTime.DaysInMonth(currentYear, currentMonth);
            _dayProperty.intValue = Mathf.Clamp(_dayProperty.intValue, 1, daysInMonth);
        }
    }
}
