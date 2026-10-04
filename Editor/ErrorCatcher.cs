using UnityEngine;
using UnityEditor;
using System.IO;

[InitializeOnLoad]
public static class ErrorCatcher
{
    static ErrorCatcher()
    {
        Application.logMessageReceived += HandleLog;
    }

    private static void HandleLog(string logString, string stackTrace, LogType type)
    {
        if (type == LogType.Exception && logString.Contains("MissingReferenceException"))
        {
            string path = System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "UnityErrorLog.txt");
            if (!File.Exists(path))
            {
                File.WriteAllText(path, logString + "\n" + stackTrace);
            }
        }
    }
}
