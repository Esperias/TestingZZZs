using UnityEngine;
using System.IO;

public class TestScript : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void RunTest()
    {
        File.WriteAllText(Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "UnityIsRunning.txt"), "UNITY RECOMPILED SUCCESSFULLY");
    }
}




