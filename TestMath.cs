using UnityEngine;
public class TestMath : MonoBehaviour
{
    private void Start()
    {
        Debug.Log("Checking heights...");
        float seedX = PlayerPrefs.GetFloat("WorldSeedX", 0f);
        float seedZ = PlayerPrefs.GetFloat("WorldSeedZ", 0f);
        Debug.Log($"Seeds: {seedX}, {seedZ}");
    }
}




