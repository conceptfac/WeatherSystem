using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace ConceptFactory.Weather.Editor
{
    public sealed class WeatherDatePickerPopupWindow : EditorWindow
    {
        private const float PopupWidth = 340f;
        private const float PopupHeight = 392f;
        private const string UxmlPath = "Packages/com.conceptfactory.weather/Editor/UI/WeatherDatePickerPopupWindow.uxml";

        private SerializedObject _serializedObject;
        private WeatherDatePickerElement _datePickerElement;
        private Action _onDateChanged;

        public static void Show(Rect activatorRect, SerializedObject sourceSerializedObject, Action onDateChanged = null)
        {
            WeatherDatePickerPopupWindow window = CreateInstance<WeatherDatePickerPopupWindow>();
            window.titleContent = new GUIContent("Date Picker");
            window._serializedObject = new SerializedObject(sourceSerializedObject.targetObjects);
            window._onDateChanged = onDateChanged;
            window.ShowAsDropDown(activatorRect, new Vector2(PopupWidth, PopupHeight));
        }

        private void CreateGUI()
        {
            VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            VisualElement root = visualTree != null ? visualTree.CloneTree() : rootVisualElement;
            if (visualTree != null)
            {
                rootVisualElement.Clear();
                rootVisualElement.Add(root);
            }

            VisualElement popupRoot = root.Q<VisualElement>("popupRoot") ?? root;

            _datePickerElement = new WeatherDatePickerElement();
            _datePickerElement.Bind(_serializedObject, "_day", "_month", "_year");
            _datePickerElement.DateChanged += HandleDateChanged;
            popupRoot.Add(_datePickerElement);
        }

        private void OnDisable()
        {
            if (_datePickerElement != null)
            {
                _datePickerElement.DateChanged -= HandleDateChanged;
            }
        }

        private void HandleDateChanged()
        {
            _serializedObject?.ApplyModifiedProperties();
            _onDateChanged?.Invoke();
            Close();
        }
    }
}
