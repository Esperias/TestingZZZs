using UnityEngine;
using UnityEditor;

public class CheckMesh {
    [MenuItem("Tools/Check Mesh")]
    public static void Run() {
        GameObject go = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Prefabs/SM_Env_Tree_Fruit_01.prefab");
        MeshFilter mf = go.transform.Find("SM_Env_Tree_Fruit_01_LOD0").GetComponent<MeshFilter>();
        Mesh m = mf.sharedMesh;
        Debug.Log("Submeshes: " + m.subMeshCount);
        Debug.Log("UV1: " + (m.uv != null && m.uv.Length > 0));
        Debug.Log("UV2: " + (m.uv2 != null && m.uv2.Length > 0));
        Debug.Log("UV3: " + (m.uv3 != null && m.uv3.Length > 0));
        Debug.Log("UV4: " + (m.uv4 != null && m.uv4.Length > 0));
        Debug.Log("Colors: " + (m.colors != null && m.colors.Length > 0));
    }
}
