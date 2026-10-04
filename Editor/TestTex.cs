using UnityEngine;
using UnityEditor;

public class TestTex {
    [MenuItem("Tools/Test Tex")]
    public static void Run() {
        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Synty/PolygonNatureBiomes/PNB_Meadow_Forest/Materials/Rock_Triplanar_Meadow_01.mat");
        foreach (string prop in mat.GetTexturePropertyNames()) {
            Texture t = mat.GetTexture(prop);
            Debug.Log(prop + ": " + (t != null ? t.name : "null"));
        }
    }
}
