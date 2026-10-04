using UnityEngine;

// =========================================================================
// WE HAVE REVERTED BACK TO 2D SKYBOX CLOUDS!
// This 3D Cloud Generator is now disabled to save performance and prevent
// the ugly 3D particle issues. 
//
// WorldCycle.cs is now fully in charge of managing the 2D clouds on the 
// SkyMaster skybox via the _CloudCover property.
// =========================================================================
public class CloudGenerator : MonoBehaviour
{
    void Start()
    {
        // Debug.Log("3D Cloud Generator has been disabled. Reverting to 2D Skybox clouds!");
    }
}




