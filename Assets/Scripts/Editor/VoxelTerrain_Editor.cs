using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(VoxelTerrain))]
public class VoxelTerrain_Editor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Regenrate"))
        {
            var voxelTerrain = (VoxelTerrain)target;
            voxelTerrain.GenerateTerrain();
        }
    }
}
