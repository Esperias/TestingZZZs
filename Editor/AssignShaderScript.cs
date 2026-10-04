using UnityEditor;
using UnityEngine;

public class AssignShaderScript
{
    [InitializeOnLoadMethod]
    static void AssignShader()
    {
        if (SessionState.GetBool("AssignedWalkingToon", false)) return;
        
        Shader toonShader = Shader.Find("Custom/WalkingToon");
        if (toonShader != null)
        {
            string matPath = "Assets/Blink/Art/Animals/Stylized/Boar/Boar_Materials/Boar_1.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null)
            {
                mat.shader = toonShader;
                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();
                Debug.Log("Successfully assigned Custom/WalkingToon to Boar_1.mat!");
                SessionState.SetBool("AssignedWalkingToon", true);
            }
        }
    }
}
