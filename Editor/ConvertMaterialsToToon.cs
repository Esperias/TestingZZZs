using UnityEditor;
using UnityEngine;
using System.IO;

public class ConvertMaterialsToToon
{
    [MenuItem("Assets/Convert to WalkingToon Shader", false, 100)]
    public static void ConvertSelectedMaterials()
    {
        Shader toonShader = Shader.Find("Custom/WalkingToon");
        if (toonShader == null)
        {
            Debug.LogError("Could not find Custom/WalkingToon shader!");
            return;
        }

        int converted = 0;

        foreach (Object obj in Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets))
        {
            Material mat = obj as Material;
            if (mat != null && mat.shader != toonShader)
            {
                // Remember previous texture if any
                Texture mainTex = null;
                if (mat.HasProperty("_MainTex")) mainTex = mat.GetTexture("_MainTex");
                else if (mat.HasProperty("_BaseMap")) mainTex = mat.GetTexture("_BaseMap");

                Color mainColor = Color.white;
                if (mat.HasProperty("_Color")) mainColor = mat.GetColor("_Color");
                else if (mat.HasProperty("_BaseColor")) mainColor = mat.GetColor("_BaseColor");

                // Check if we need alpha clipping
                bool isTransparent = mat.renderQueue >= 3000 || mat.GetTag("RenderType", false) == "Transparent" || mat.GetTag("RenderType", false) == "TransparentCutout";

                Texture bumpMap = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                float bumpScale = mat.HasProperty("_BumpScale") ? mat.GetFloat("_BumpScale") : 1f;
                Texture occMap = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
                float occStrength = mat.HasProperty("_OcclusionStrength") ? mat.GetFloat("_OcclusionStrength") : 1f;

                // Swap shader
                mat.shader = toonShader;

                // Re-apply properties
                if (mainTex != null) mat.SetTexture("_MainTex", mainTex);
                mat.SetColor("_BaseColor", mainColor);

                if (bumpMap != null) {
                    mat.SetTexture("_BumpMap", bumpMap);
                    mat.SetFloat("_BumpScale", bumpScale);
                    mat.SetFloat("_UseNormalMap", 1f);
                    mat.EnableKeyword("_NORMALMAP");
                }
                
                if (occMap != null) {
                    mat.SetTexture("_OcclusionMap", occMap);
                    mat.SetFloat("_OcclusionStrength", occStrength);
                }

                if (isTransparent)
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.EnableKeyword("_ALPHATEST_ON");
                }

                EditorUtility.SetDirty(mat);
                converted++;
            }
        }

        if (converted > 0)
        {
            AssetDatabase.SaveAssets();
            Debug.Log($"Successfully converted {converted} materials to WalkingToon!");
        }
        else
        {
            Debug.Log("No materials needed converting (they might already be WalkingToon).");
        }
    }

    [MenuItem("Assets/Convert to WalkingToon Shader", true)]
    public static bool ConvertSelectedMaterialsValidate()
    {
        // Only show the menu if a folder or material is selected
        return Selection.GetFiltered(typeof(Material), SelectionMode.DeepAssets).Length > 0;
    }
}
