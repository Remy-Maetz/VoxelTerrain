using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelTerrain))]
public class VoxelTerrain_Editor : Editor
{
    VoxelTerrain voxelTerrain;

    private void OnEnable()
    {
        voxelTerrain = (VoxelTerrain)target;
    }

    public override void OnInspectorGUI()
    {
        EditorGUI.BeginChangeCheck();
        base.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            voxelTerrain.GenerateTerrain();
        }

        EditorGUILayout.PropertyField(serializedObject.FindProperty("gizmoMode"));
        serializedObject.ApplyModifiedProperties();

        if (voxelTerrain.heightMap == null || voxelTerrain.colorMap == null )
        {
            EditorGUILayout.HelpBox("Missing Height Map or Color Map for terrain generation.", MessageType.Warning);
        }
        else if (!voxelTerrain.heightMap.isReadable)
        {
            EditorGUILayout.HelpBox("Height Map must be Readable (check import settings).", MessageType.Warning);
        }
        else
        {
            if (GUILayout.Button("Regenerate"))
            {
                voxelTerrain.GenerateTerrain();
            }
        }
    }
}
