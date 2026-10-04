using UnityEngine;

public class PlayerBed : MonoBehaviour, IInteractable
{
    public string Prompt => "set respawn point";
    public float HoldTime => 1.5f;
    public float Range => 3f;
    public bool CanInteract => true;

    private void Start()
    {
        InteractionManager.Register(this);
    }
    
    private void OnDestroy()
    {
        InteractionManager.Unregister(this);
    }

    public float DistanceTo(Vector3 point)
    {
        return Vector3.Distance(transform.position, point);
    }

    public void Interact()
    {
        PlayerHealth ph = PlayerHealth.Instance;
        if (ph != null)
        {
            ph.spawnPosition = transform.position + Vector3.up * 1f;
            if (Inventory.Instance != null) Inventory.Instance.Notify("Respawn point set!");
        }
    }
}





