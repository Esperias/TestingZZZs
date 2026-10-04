using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[InitializeOnLoad]
public class SetCavePrefabsAuto
{
    static SetCavePrefabsAuto()
    {
        EditorApplication.delayCall += Run;
    }

    public static void Run()
    {
        WorldGenerator wg = Object.FindAnyObjectByType<WorldGenerator>();
        if (wg == null) return;

        // Skip if already assigned
        if (wg.caveRockPrefabs != null && wg.caveRockPrefabs.Length >= 6) return;

        string[] rockPaths = new string[] {
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_02.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_03.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_04.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_05.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_06.prefab"
        };

        string[] cliffPaths = new string[] {
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_Cliff_01.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_Cliff_02.prefab",
            "Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Rock_Cliff_03.prefab"
        };

        List<GameObject> rocks = new List<GameObject>();
        foreach(var p in rockPaths) {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go) rocks.Add(go);
        }

        List<GameObject> cliffs = new List<GameObject>();
        foreach(var p in cliffPaths) {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(p);
            if (go) cliffs.Add(go);
        }

        wg.caveRockPrefabs = rocks.ToArray();
        wg.caveCliffPrefabs = cliffs.ToArray();

        EditorUtility.SetDirty(wg);
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(wg.gameObject.scene);
        Debug.Log("Cave prefabs auto-assigned!");
    }
}

