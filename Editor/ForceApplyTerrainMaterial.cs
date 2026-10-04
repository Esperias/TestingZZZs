using UnityEngine;
using UnityEditor;

public class ForceApplyTerrainMaterial
{
    public static void Run()
    {
        WorldGenerator wg = Object.FindAnyObjectByType<WorldGenerator>();
        if (wg != null)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Mat_ProceduralTerrain.mat");
            if (mat != null)
            {
                wg.terrainMaterial = mat;
                EditorUtility.SetDirty(wg);
                Debug.Log("Forced terrain material assignment!");
            }
        }
    }
}