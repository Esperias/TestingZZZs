using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Adds a menu item to quickly set up the TestCave scene for direct testing.
/// </summary>
public class CaveSceneSetup : EditorWindow
{
    [MenuItem("Walking About/Setup Test Cave Scene")]
    public static void SetupCaveScene()
    {
        // 1. Ensure CaveGenerator exists
        if (FindAnyObjectByType<CaveGenerator>() == null)
        {
            GameObject cg = new GameObject("CaveGenerator");
            cg.AddComponent<CaveGenerator>();
            Debug.Log("Created CaveGenerator.");
        }

        // 2. Ensure Player exists
        if (FindAnyObjectByType<PlayerController>() == null)
        {
            string[] guids = AssetDatabase.FindAssets("Player t:Prefab");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    GameObject p = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    p.transform.position = Vector3.zero;
                    Debug.Log("Instantiated Player prefab.");
                }
            }
            else
            {
                Debug.LogWarning("Could not find Player prefab! You might need to drop it in manually.");
            }
        }

        // 3. Ensure Managers exist (ToolManager, Inventory, etc.)
        if (FindAnyObjectByType<ToolManager>() == null || FindAnyObjectByType<Inventory>() == null)
        {
            GameObject managers = GameObject.Find("Managers");
            if (managers == null) managers = new GameObject("Managers");

            if (managers.GetComponent<ToolManager>() == null) managers.AddComponent<ToolManager>();
            if (managers.GetComponent<Inventory>() == null) managers.AddComponent<Inventory>();
            if (managers.GetComponent<QuestLog>() == null) managers.AddComponent<QuestLog>();
            if (managers.GetComponent<Shop>() == null) managers.AddComponent<Shop>();
            if (managers.GetComponent<BuildingManager>() == null) managers.AddComponent<BuildingManager>();
            if (managers.GetComponent<PauseMenu>() == null) managers.AddComponent<PauseMenu>();
            if (managers.GetComponent<Minimap>() == null) managers.AddComponent<Minimap>();
            
            Debug.Log("Created Managers object with ToolManager and Inventory.");
        }

        // 4. Ensure EventSystem exists (for UI clicks)
        if (FindAnyObjectByType<EventSystem>() == null)
        {
            GameObject es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            Debug.Log("Created EventSystem.");
        }

        // 5. Ensure Main Camera exists (usually inside player, but just in case)
        if (Camera.main == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                Camera cam = pc.GetComponentInChildren<Camera>();
                if (cam != null) cam.tag = "MainCamera";
            }
        }

        Debug.Log("Cave scene setup complete. You can hit Play now!");
    }
}

