using UnityEditor;
using UnityEngine;

public class SetupResourceGenerator
{
    [MenuItem("Walking About/Setup Synty Resource Generator")]
    public static void Setup()
    {
        // Find existing ResourceGenerator or attach to WorldGenerator
        WorldGenerator wg = Object.FindAnyObjectByType<WorldGenerator>();
        if (wg == null)
        {
            Debug.LogError("Could not find WorldGenerator in the scene. Please open the main scene.");
            return;
        }

        ResourceGenerator gen = wg.GetComponent<ResourceGenerator>();
        if (gen == null)
        {
            gen = wg.gameObject.AddComponent<ResourceGenerator>();
        }

        // Load Synty Prefabs
        GameObject fruit1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Tree_Fruit_01.prefab");
        GameObject fruit2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Tree_Fruit_02.prefab");
        GameObject fruit3 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Tree_Fruit_03.prefab");
        
        GameObject bush1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Bush_02.prefab");

        GameObject rock1 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Rock_01.prefab");
        GameObject rock2 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Rock_02.prefab");
        GameObject rock4 = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefab/Environment/SM_Env_Rock_04.prefab");

        if (fruit1 == null || bush1 == null || rock1 == null)
        {
            Debug.LogError("Could not find some of the Synty prefabs! Did the folder path change?");
            return;
        }

        gen.fruitTreePrefabs = new GameObject[] { fruit1, fruit2, fruit3 };
        gen.bushPrefab = bush1;
        gen.rockPrefabs = new GameObject[] { rock1, rock2, rock4 };

        // Assign Synty Water Material
        Material syntyWater = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/Water_Lake.mat");
        if (syntyWater != null)
        {
            wg.waterMaterial = syntyWater;
            EditorUtility.SetDirty(wg);
        }

        EditorUtility.SetDirty(gen);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wg.gameObject.scene);
        
        Debug.Log("ResourceGenerator created and Synty prefabs fully wired up! You can now delete any old ResourceNodes from the scene hierarchy if they exist.");
    }
}
