using System.Collections.Generic;
using UnityEngine;

public class VillageTotem : MonoBehaviour
{
    public static VillageTotem Instance { get; private set; }

    public int TotalBeds { get; set; } = 0;
    public int UsedBeds { get; set; } = 0;
    
    public int AvailableBeds => TotalBeds - UsedBeds;

    private List<NPCIdentity> residents = new List<NPCIdentity>();
    private float resourceTimer = 0f;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddResident(NPCIdentity npc)
    {
        if (!residents.Contains(npc))
        {
            residents.Add(npc);
            UsedBeds++;
        }
    }

    private void Update()
    {
        resourceTimer += Time.deltaTime;
        // Every 30 real-world seconds, gather resources
        if (resourceTimer >= 30f)
        {
            resourceTimer = 0f;
            int woodGathered = 0;
            int stoneGathered = 0;

            foreach (var npc in residents)
            {
                if (npc.AssignedJob == "Woodcutter") woodGathered += 2;
                if (npc.AssignedJob == "Stonegatherer") stoneGathered += 2;
            }

            if (woodGathered > 0 && Inventory.Instance != null)
            {
                Inventory.Instance.AddItem("Wood", woodGathered);
                Inventory.Instance.Notify($"Villagers gathered {woodGathered} Wood!");
            }
            if (stoneGathered > 0 && Inventory.Instance != null)
            {
                Inventory.Instance.AddItem("Stone", stoneGathered);
                Inventory.Instance.Notify($"Villagers gathered {stoneGathered} Stone!");
            }
        }
    }
}




