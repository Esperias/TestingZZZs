using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections.Generic;

public class ConvertSyntyPrefabs
{
    [MenuItem("Tools/Convert Synty Prefabs To Toon")]
    public static void Convert()
    {
        string outDir = "Assets/_Project/Prefab/Environment";
        if (!AssetDatabase.IsValidFolder("Assets/_Project/Prefab/Environment"))
        {
            AssetDatabase.CreateFolder("Assets/_Project/Prefab", "Environment");
        }
        
        Dictionary<Material, Material> matMap = new Dictionary<Material, Material>();

        string[] prefabs = new string[] {
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Fruit_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Fruit_02.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Fruit_03.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Bush_02.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_02.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_04.prefab"
        };
        
        foreach (string path in prefabs)
        {
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (src == null) continue;
            
            GameObject inst = Object.Instantiate(src);
            inst.name = src.name;
            

            
            Renderer[] rends = inst.GetComponentsInChildren<Renderer>();
            foreach (Renderer r in rends)
            {
                Material[] newMats = new Material[r.sharedMaterials.Length];
                for (int j = 0; j < r.sharedMaterials.Length; j++)
                {
                    Material orig = r.sharedMaterials[j];
                    if (orig == null) continue;
                    
                    if (!matMap.ContainsKey(orig))
                    {
                        Material toonMat = new Material(Shader.Find("Custom/WalkingToon"));
                        
                        Texture tex = null;
                        if (orig.HasProperty("_MainTex")) tex = orig.GetTexture("_MainTex");
                        if (tex == null && orig.HasProperty("_BaseMap")) tex = orig.GetTexture("_BaseMap");
                        if (tex == null && orig.HasProperty("_Albedo_Map")) tex = orig.GetTexture("_Albedo_Map");
                        if (tex == null && orig.HasProperty("_Trunk_Texture")) tex = orig.GetTexture("_Trunk_Texture");
                        if (tex == null && orig.HasProperty("_Leaf_Texture")) tex = orig.GetTexture("_Leaf_Texture");
                        
                        if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Textures/PolygonNatureBiomes_Meadow_Texture_01.png");
                        
                        if (tex != null) toonMat.SetTexture("_MainTex", tex);
                        
                        Color col = Color.white;
                        if (orig.HasProperty("_BaseColor")) col = orig.GetColor("_BaseColor");
                        else if (orig.HasProperty("_Color")) col = orig.GetColor("_Color");
                        toonMat.SetColor("_BaseColor", col);
                        
                        toonMat.SetFloat("_AlphaClip", 1.0f);
                        toonMat.SetFloat("_Cutoff", 0.5f);
                        toonMat.EnableKeyword("_ALPHATEST_ON");
                        
                        toonMat.SetFloat("_UseVertexColors", 0.0f);
                        toonMat.DisableKeyword("_USE_VERTEX_COLORS");
                        
                        string newMatPath = outDir + "/Toon_" + orig.name + ".mat";
                        AssetDatabase.CreateAsset(toonMat, newMatPath);
                        matMap[orig] = toonMat;
                    }
                    newMats[j] = matMap[orig];
                }
                r.sharedMaterials = newMats;
            }
            
            PrefabUtility.SaveAsPrefabAsset(inst, outDir + "/" + src.name + ".prefab");
            Object.DestroyImmediate(inst);
        }
        
        AssetDatabase.SaveAssets();
        Debug.Log("Conversion complete!");
    }
}
