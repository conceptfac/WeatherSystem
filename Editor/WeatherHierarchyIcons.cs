using ConceptFactory.Weather.Atmosphere;
using UnityEditor;
using UnityEngine;

namespace ConceptFactory.Weather.Editor
{
    [InitializeOnLoad]
    public static class WeatherHierarchyIcons
    {
        private static GUIStyle _style;

        static WeatherHierarchyIcons()
        {
            EditorApplication.hierarchyWindowItemByEntityIdOnGUI += OnHierarchyItem;
        }

        private static void OnHierarchyItem(UnityEngine.EntityId instanceID, Rect rect)
        {
            if (Event.current.type != EventType.Repaint) return;

            GameObject go = EditorUtility.EntityIdToObject(instanceID) as GameObject;
            if (go == null) return;

            string symbol;
            Color color;

            if (go.TryGetComponent<WeatherSolarController>(out _))
            {
                symbol = "☀";
                color = new Color(1f, 0.82f, 0.1f);
            }
            else if (go.TryGetComponent<LunarController>(out _))
            {
                symbol = "☽";
                color = new Color(0.72f, 0.88f, 1f);
            }
            else if (go.TryGetComponent<NimbusCloudController>(out _))
            {
                symbol = "☁";
                color = new Color(0.78f, 0.9f, 1f);
            }
            else
            {
                return;
            }

            _style ??= new GUIStyle(EditorStyles.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(0, 0, 0, 0)
            };

            Rect iconRect = new Rect(rect.xMax - 20, rect.y, 18, rect.height);
            Color prev = GUI.color;
            GUI.color = color;
            GUI.Label(iconRect, symbol, _style);
            GUI.color = prev;
        }
    }
}
