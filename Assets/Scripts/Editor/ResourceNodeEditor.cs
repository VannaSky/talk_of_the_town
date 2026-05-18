using UnityEditor;
using UnityEngine;
using Environment.Resources;

[CustomEditor(typeof(ResourceNode))]
public class ResourceNodeEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var resourceType = serializedObject.FindProperty("resourceType");
        var resourceAmount = serializedObject.FindProperty("resourceAmount");
        var isReserved = serializedObject.FindProperty("isReserved");
        var growthStage = serializedObject.FindProperty("growthStage");
        var canRegrow = serializedObject.FindProperty("canRegrow");
        var growthTime = serializedObject.FindProperty("growthTime");
        var currentGrowthTimer = serializedObject.FindProperty("currentGrowthTimer");
        var defaultResourceAmount = serializedObject.FindProperty("defaultResourceAmount");
        var seedlingVisual = serializedObject.FindProperty("seedlingVisual");
        var growingVisual = serializedObject.FindProperty("growingVisual");
        var matureVisual = serializedObject.FindProperty("matureVisual");
        var isMineShaft = serializedObject.FindProperty("isMineShaft");
        var seedsYield = serializedObject.FindProperty("seedsYield");

        var type = (ResourceNode.ResourceType)resourceType.enumValueIndex;

        EditorGUILayout.PropertyField(resourceType);
        EditorGUILayout.PropertyField(resourceAmount);
        EditorGUILayout.PropertyField(isReserved);
        EditorGUILayout.PropertyField(growthStage);
        EditorGUILayout.PropertyField(canRegrow);
        EditorGUILayout.PropertyField(growthTime);
        EditorGUILayout.PropertyField(currentGrowthTimer);
        EditorGUILayout.PropertyField(defaultResourceAmount);
        EditorGUILayout.PropertyField(seedlingVisual);
        EditorGUILayout.PropertyField(growingVisual);
        EditorGUILayout.PropertyField(matureVisual);

        if (type == ResourceNode.ResourceType.Stone)
            EditorGUILayout.PropertyField(isMineShaft);

        if (type == ResourceNode.ResourceType.Crop)
            EditorGUILayout.PropertyField(seedsYield);

        serializedObject.ApplyModifiedProperties();
    }
}
