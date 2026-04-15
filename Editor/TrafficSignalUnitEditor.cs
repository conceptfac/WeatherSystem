#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TrafficSignalUnit))]
[CanEditMultipleObjects]
public sealed class TrafficSignalUnitEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        SerializedProperty signalKind = serializedObject.FindProperty("_signalKind");
        SerializedProperty channelId = serializedObject.FindProperty("_channelId");
        SerializedProperty redLamp = serializedObject.FindProperty("_redLamp");
        SerializedProperty yellowLamp = serializedObject.FindProperty("_yellowLamp");
        SerializedProperty greenLamp = serializedObject.FindProperty("_greenLamp");
        SerializedProperty walkLamp = serializedObject.FindProperty("_walkLamp");
        SerializedProperty dontWalkLamp = serializedObject.FindProperty("_dontWalkLamp");
        SerializedProperty useSceneLightsOnlyAtNight = serializedObject.FindProperty("_useSceneLightsOnlyAtNight");
        SerializedProperty nightLightActivationThreshold = serializedObject.FindProperty("_nightLightActivationThreshold");
        SerializedProperty isWorking = serializedObject.FindProperty("_isWorking");
        SerializedProperty isBlackoutActive = serializedObject.FindProperty("_isBlackoutActive");
        SerializedProperty currentState = serializedObject.FindProperty("_currentState");
        SerializedProperty sceneLightsAllowed = serializedObject.FindProperty("_sceneLightsAllowed");

        EditorGUILayout.PropertyField(signalKind);
        EditorGUILayout.PropertyField(channelId);
        EditorGUILayout.Space();

        bool mixedKind = signalKind.hasMultipleDifferentValues;
        if (mixedKind)
        {
            EditorGUILayout.HelpBox("Os objetos selecionados possuem tipos diferentes de semaforo.", MessageType.Info);
        }
        else
        {
            TrafficSignalUnit.SignalKind selectedKind = (TrafficSignalUnit.SignalKind)signalKind.enumValueIndex;
            if (selectedKind == TrafficSignalUnit.SignalKind.Vehicle)
            {
                EditorGUILayout.LabelField("Vehicle Lamps", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(redLamp);
                EditorGUILayout.PropertyField(yellowLamp);
                EditorGUILayout.PropertyField(greenLamp);
            }
            else
            {
                EditorGUILayout.LabelField("Pedestrian Lamps", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(walkLamp);
                EditorGUILayout.PropertyField(dontWalkLamp);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Lighting Behavior", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(useSceneLightsOnlyAtNight);
        EditorGUILayout.PropertyField(nightLightActivationThreshold);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Failure States", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(isWorking);
        EditorGUILayout.PropertyField(isBlackoutActive);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Debug", EditorStyles.boldLabel);
        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.PropertyField(currentState);
            EditorGUILayout.PropertyField(sceneLightsAllowed);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
