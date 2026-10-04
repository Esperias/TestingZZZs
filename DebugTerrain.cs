using UnityEngine;
public class DebugTerrain : MonoBehaviour
{
    private void Start()
    {
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null) { Debug.Log("No wg"); return; }
        Debug.Log($"SeedX: {wg.seedOffsetX}, SeedZ: {wg.seedOffsetZ}");
        float x = wg.seedOffsetX;
        float z = wg.seedOffsetZ;
        float macro = Mathf.PerlinNoise(x * 0.008f, z * 0.008f);
        float riverNoise = Mathf.PerlinNoise(x * 0.005f + 75f, z * 0.004f + 50f);
        Debug.Log($"Macro: {macro}, RiverNoise: {riverNoise}");
    }
}




