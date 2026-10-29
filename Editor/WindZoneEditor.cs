using UnityEditor;
using WindZone = ConceptFactory.Weather.Atmosphere.WindZone;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace ConceptFactory.Weather.Editor
{
    [CustomEditor(typeof(WindZone), true)]
    [CanEditMultipleObjects]
    public class WindZoneEditor : UnityEditor.Editor
    {
        readonly BoxBoundsHandle cubeBoundsHandle = new BoxBoundsHandle();
        SerializedProperty mode;
        SerializedProperty radius;
        SerializedProperty innerFraction;
        SerializedProperty strength;
        SerializedProperty speed;
        SerializedProperty wavelength;
        SerializedProperty turbulence;
        SerializedProperty cubeScale;
        SerializedProperty blend, shape, coneStartRadius, coneEndRadius, coneRange, coneEndFade;

        void OnEnable()
        {
            mode = serializedObject.FindProperty(nameof(WindZone.mode));
            radius = serializedObject.FindProperty(nameof(WindZone.radius));
            innerFraction = serializedObject.FindProperty(nameof(WindZone.innerFraction));
            strength = serializedObject.FindProperty(nameof(WindZone.strength));
            speed = serializedObject.FindProperty(nameof(WindZone.speed));
            wavelength = serializedObject.FindProperty(nameof(WindZone.wavelength));
            turbulence = serializedObject.FindProperty(nameof(WindZone.turbulence));
            cubeScale = serializedObject.FindProperty(nameof(WindZone.cubeScale));
            blend = serializedObject.FindProperty(nameof(WindZone.blend));
            shape = serializedObject.FindProperty(nameof(WindZone.shape));
            coneStartRadius = serializedObject.FindProperty(nameof(WindZone.coneStartRadius));
            coneEndRadius = serializedObject.FindProperty(nameof(WindZone.coneEndRadius));
            coneRange = serializedObject.FindProperty(nameof(WindZone.coneRange));
            coneEndFade = serializedObject.FindProperty(nameof(WindZone.coneEndFade));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(mode);

            // The area only matters for Local zones; a Global zone covers all the grass.
            if (!mode.hasMultipleDifferentValues && mode.enumValueIndex == (int)WindZone.Mode.Local)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(blend);
                EditorGUILayout.PropertyField(shape);
                if (shape.hasMultipleDifferentValues || shape.enumValueIndex == (int)WindZone.Shape.Radius)
                    EditorGUILayout.PropertyField(radius);
                if (shape.hasMultipleDifferentValues || shape.enumValueIndex == (int)WindZone.Shape.Cone)
                {
                    EditorGUILayout.PropertyField(coneStartRadius, new GUIContent("Entry Ring Radius"));
                    EditorGUILayout.PropertyField(coneEndRadius, new GUIContent("Exit Ring Radius"));
                    EditorGUILayout.PropertyField(coneRange, new GUIContent("Cone Range"));
                    EditorGUILayout.PropertyField(coneEndFade, new GUIContent("End Fade"));
                }
                if (!shape.hasMultipleDifferentValues && shape.enumValueIndex == (int)WindZone.Shape.Cone)
                    EditorGUILayout.PropertyField(innerFraction);
                if (!shape.hasMultipleDifferentValues && shape.enumValueIndex == (int)WindZone.Shape.Cube)
                    EditorGUILayout.PropertyField(cubeScale, new GUIContent("Cube Scale"));
                EditorGUI.indentLevel--;
                EditorGUILayout.HelpBox("Override replaces global wind, with a smooth transition at the edges. " +
                    "Overlapping overrides blend together. Additive currents sum on top. " +
                    "Inner Fraction controls the full-strength core inside the radius or cone rings.", MessageType.Info);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wind", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(strength);
            EditorGUILayout.PropertyField(speed);
            EditorGUILayout.PropertyField(wavelength);
            EditorGUILayout.PropertyField(turbulence);

            serializedObject.ApplyModifiedProperties();
        }

        void OnSceneGUI()
        {
            var zone = (WindZone)target;
            if (zone == null || !Selection.Contains(zone.gameObject)) return;
            if (zone.mode != WindZone.Mode.Local) return;
            Vector3 p = zone.transform.position;
            Handles.color = new Color(0.4f, 0.9f, 1f);
            if (zone.shape == WindZone.Shape.Cone)
            {
                EditorGUI.BeginChangeCheck();
                Quaternion rotation = zone.transform.rotation;
                Vector3 forward = zone.transform.forward;
                Vector3 end = p + forward * zone.coneRange;
                float startRadius = Handles.RadiusHandle(rotation, p, zone.coneStartRadius, true);
                float endRadius = Handles.RadiusHandle(rotation, end, zone.coneEndRadius, true);
                Vector3 movedEnd = Handles.Slider(end, forward);
                Handles.Label(p, "Entry");
                Handles.Label(end, "Exit / Range");
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zone, "Resize wind cone");
                    zone.coneStartRadius = Mathf.Max(0f, startRadius);
                    zone.coneEndRadius = Mathf.Max(0.01f, endRadius);
                    zone.coneRange = Mathf.Max(0.1f, Vector3.Dot(movedEnd - p, forward));
                    SaveHandleChange(zone);
                }
            }
            else if (zone.shape == WindZone.Shape.Cube)
            {
                Matrix4x4 previousMatrix = Handles.matrix;
                Handles.matrix = Matrix4x4.TRS(zone.transform.position, zone.transform.rotation, Vector3.one);
                cubeBoundsHandle.center = Vector3.zero;
                cubeBoundsHandle.size = zone.cubeScale;
                EditorGUI.BeginChangeCheck();
                cubeBoundsHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zone.transform, "Resize wind cube");
                    zone.cubeScale = new Vector3(
                        Mathf.Max(0.01f, Mathf.Abs(cubeBoundsHandle.size.x)),
                        Mathf.Max(0.01f, Mathf.Abs(cubeBoundsHandle.size.y)),
                        Mathf.Max(0.01f, Mathf.Abs(cubeBoundsHandle.size.z)));
                    SaveHandleChange(zone);
                }
                Handles.matrix = previousMatrix;
            }
            else
            {
                EditorGUI.BeginChangeCheck();
                float outer = Handles.RadiusHandle(Quaternion.identity, p, zone.radius);
                if (EditorGUI.EndChangeCheck())
                {
                    Undo.RecordObject(zone, "Resize wind radius");
                    zone.radius = Mathf.Max(0.1f, outer);
                    SaveHandleChange(zone);
                }
            }
        }

        static void SaveHandleChange(WindZone zone)
        {
            EditorUtility.SetDirty(zone);
            PrefabUtility.RecordPrefabInstancePropertyModifications(zone);
            WindZone.UploadAll();
        }
    }
}
