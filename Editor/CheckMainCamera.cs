using UnityEngine;
using UnityEditor;

public class CheckMainCamera
{
    public static void Run()
    {
        Debug.Log("Camera.main: " + (Camera.main != null ? Camera.main.gameObject.name : "NULL"));
        
        // Find any camera
        Camera anyCam = Object.FindAnyObjectByType<Camera>();
        if (anyCam != null)
        {
            Debug.Log("Found a camera: " + anyCam.gameObject.name + " Tag: " + anyCam.tag);
            if (anyCam.tag != "MainCamera")
            {
                anyCam.tag = "MainCamera";
                EditorUtility.SetDirty(anyCam.gameObject);
                Debug.Log("Set tag to MainCamera!");
            }
        }
    }
}