using UnityEngine;
public class TestHeight : MonoBehaviour
{
    private void Start()
    {
        WorldGenerator wg = WorldGenerator.Instance;
        float maxH = -999f;
        float minH = 999f;
        for(float x = -3000f; x < 3000f; x += 50f) {
            for(float z = -3000f; z < 3000f; z += 50f) {
                float h = wg.GetHeightAt(x, z);
                if (h > maxH) maxH = h;
                if (h < minH) minH = h;
            }
        }
        Debug.Log($"Terrain Max Height: {maxH}, Min Height: {minH}");
    }
}




