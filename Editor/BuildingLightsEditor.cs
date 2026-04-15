#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CanEditMultipleObjects]
[CustomEditor(typeof(BuildingLights))]
public sealed class BuildingLightsEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (!Application.isPlaying)
        {
            bool allEnabled = true;
            bool allDisabled = true;

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] is not BuildingLights buildingLightsTarget)
                {
                    continue;
                }

                if (buildingLightsTarget.IsEditModeSimulationActive)
                {
                    allDisabled = false;
                }
                else
                {
                    allEnabled = false;
                }
            }

            bool mixedValue = !allEnabled && !allDisabled;
            int currentIndex = allEnabled ? 1 : 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Simulacao em Edicao");
            EditorGUI.showMixedValue = mixedValue;
            int selectedIndex = GUILayout.Toolbar(currentIndex, new[] { "OFF", "ON" }, GUILayout.MaxWidth(120f));
            EditorGUI.showMixedValue = false;
            bool nextState = selectedIndex == 1;
            EditorGUILayout.EndHorizontal();

            if (nextState != allEnabled || mixedValue)
            {
                for (int index = 0; index < targets.Length; index++)
                {
                    if (targets[index] is not BuildingLights buildingLightsTarget)
                    {
                        continue;
                    }

                    Undo.RecordObject(buildingLightsTarget, "Toggle Building Lights Edit Simulation");

                    if (nextState)
                    {
                        buildingLightsTarget.StartEditModeSimulation();
                    }
                    else
                    {
                        buildingLightsTarget.StopEditModeSimulation();
                    }

                    EditorUtility.SetDirty(buildingLightsTarget);
                }
            }
        }

        if (!Application.isPlaying)
        {
            bool allEnabled = true;
            bool allDisabled = true;

            for (int index = 0; index < targets.Length; index++)
            {
                if (targets[index] is not BuildingLights buildingLightsTarget)
                {
                    continue;
                }

                if (buildingLightsTarget.IsEditModeSimulationActive)
                {
                    allDisabled = false;
                }
                else
                {
                    allEnabled = false;
                }
            }

            EditorGUILayout.HelpBox(
                allEnabled
                    ? "A simulacao de luzes esta ativa para todos os objetos selecionados no modo de edicao."
                    : allDisabled
                        ? "Use o controle acima para visualizar a simulacao das luzes sem entrar em play mode."
                        : "Os objetos selecionados estao com estados diferentes de simulacao em edicao.",
                MessageType.Info);
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
