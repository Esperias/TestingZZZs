using UnityEngine;

public class DebugFindPlayer : MonoBehaviour
{
    private void Start()
    {
        var pcs = FindObjectsByType<PlayerController>(FindObjectsInactive.Exclude);
        Debug.Log("FOUND " + pcs.Length + " PlayerControllers");
        foreach(var pc in pcs) Debug.Log("PlayerController: " + pc.name + " at " + pc.transform.position);
    }
}



