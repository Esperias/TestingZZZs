using UnityEngine;
using UnityEditor;

public class SetupTerrainMaterial
{
    [MenuItem("Tools/Setup Terrain Material")]
    public static void Run()
    {
        Shader shader = Shader.Find("Custom/WalkingTerrain");
        if (shader == null)
        {
            Debug.LogError("Shader not found!");
            return;
        }

        if (!AssetDatabase.IsValidFolder("Assets/_Project/Materials"))
            AssetDatabase.CreateFolder("Assets/_Project", "Materials");

        Material mat = new Material(shader);
        
        Texture2D sandTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/Terrain/Dirt_Texture_01.png");
        Texture2D rockTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/Terrain/Rock_Rough_Texture_01.png");
        Texture2D grassTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/Terrain/Grass_Texture_01.png");
        Texture2D pathTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Project/Textures/Terrain/Dirt_Cracked_Texture_01.png");

        if (sandTex != null) mat.SetTexture("_TexSand", sandTex);
        if (rockTex != null) mat.SetTexture("_TexRock", rockTex);
        if (grassTex != null) mat.SetTexture("_TexGrass", grassTex);
        if (pathTex != null) mat.SetTexture("_TexPath", pathTex);
        
        mat.SetFloat("_TexScale", 0.15f);

        AssetDatabase.CreateAsset(mat, "Assets/_Project/Materials/Mat_ProceduralTerrain.mat");
        AssetDatabase.SaveAssets();
        
        WorldGenerator wg = Object.FindAnyObjectByType<WorldGenerator>();
        if (wg != null)
        {
            wg.terrainMaterial = mat;
            EditorUtility.SetDirty(wg);
            Debug.Log("Assigned new terrain material to WorldGenerator!");
        }
        else
        {
            Debug.LogWarning("WorldGenerator not found in scene!");
        }
    }
}