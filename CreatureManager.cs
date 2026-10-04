using System;
using System.Collections.Generic;
using UnityEngine;

// 1. Data separated from GameObject
[Serializable]
public class CreatureData
{
    public string id;
    public string type; // "Deer", "Wolf", "Boar", "Raven", "NPC"
    public Vector3 position;
    public Quaternion rotation;
    public float health = 100f;
    public string state = "Idle";
    public Vector2Int homeChunk;
    public bool isPersistent = false; // Player interacted, or is NPC
}

[Serializable]
public class ChunkCreatureData
{
    public bool hasBeenPopulated = false;
    public float lastUnloadTime = 0f;
    public List<CreatureData> creatures = new List<CreatureData>();
}

// 10. Structure
public class CreatureManager : MonoBehaviour
{
    public static CreatureManager Instance { get; private set; }

    [Header("Settings")]
    public int globalCap = 150;
    public int perChunkCap = 5;
    
    [Header("Simulation")]
    public int simulationRadius = 3; 
    public int loadRadius = 4; // Should match WorldGenerator.viewDistance

    // 2. Each chunk owns a list of its creatures' data.
    private Dictionary<Vector2Int, ChunkCreatureData> chunkData = new Dictionary<Vector2Int, ChunkCreatureData>();
    
    // Live GameObjects currently simulating/loaded
    private Dictionary<string, ICreature> liveCreatures = new Dictionary<string, ICreature>();

    // 3. Object pooling
    private Dictionary<string, Stack<GameObject>> pools = new Dictionary<string, Stack<GameObject>>();

    // Prefabs mapping
    public GameObject deerPrefab;
    public GameObject boarPrefab;
    public GameObject wolfPrefab;
    public GameObject ravenPrefab;
    private Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

    private Transform poolParent;

    private void Awake()
    {
        Instance = this;
        poolParent = new GameObject("CreaturePools").transform;
    }

    private void Start()
    {
        // Setup prefabs map
        if (AnimalManager.Instance != null)
        {
            deerPrefab = AnimalManager.Instance.deerPrefab;
            boarPrefab = AnimalManager.Instance.boarPrefab;
            wolfPrefab = AnimalManager.Instance.wolfPrefab;
            ravenPrefab = AnimalManager.Instance.ravenPrefab;
            
            // Disable the old AnimalManager
            AnimalManager.Instance.enabled = false;
        }

        prefabs["Deer"] = deerPrefab;
        prefabs["Boar"] = boarPrefab;
        prefabs["Wolf"] = wolfPrefab;
        prefabs["Raven"] = ravenPrefab;

        foreach (var key in prefabs.Keys)
        {
            pools[key] = new Stack<GameObject>();
            // Pre-warm (simplified)
            for(int i = 0; i < 5; i++) {
                if (prefabs[key] == null) continue;
                GameObject go = Instantiate(prefabs[key], poolParent);
                go.SetActive(false);
                pools[key].Push(go);
            }
        }
    }

    private GameObject GetFromPool(string type)
    {
        if (pools.ContainsKey(type) && pools[type].Count > 0)
        {
            return pools[type].Pop();
        }
        if (prefabs.ContainsKey(type) && prefabs[type] != null)
        {
            return Instantiate(prefabs[type], poolParent);
        }
        return null;
    }

    private void ReturnToPool(string type, GameObject go)
    {
        go.SetActive(false);
        if (!pools.ContainsKey(type)) pools[type] = new Stack<GameObject>();
        pools[type].Push(go);
    }

    // Called by WorldGenerator when a chunk generates/loads
    public void OnChunkLoaded(Vector2Int coord, Transform chunkTransform)
    {
        if (!chunkData.ContainsKey(coord))
        {
            chunkData[coord] = new ChunkCreatureData();
        }

        ChunkCreatureData data = chunkData[coord];

        // 6. Spawning rules: spawn if never populated
        if (!data.hasBeenPopulated)
        {
            PopulateChunk(coord, data);
            data.hasBeenPopulated = true;
        }

        // 8. Fake elapsed time
        float elapsed = Time.time - data.lastUnloadTime;
        
        // 9. Queue loading over frames (simplified as direct load for now, would use Coroutine queue)
        StartCoroutine(LoadCreaturesRoutine(coord, data, elapsed));
    }

    private System.Collections.IEnumerator LoadCreaturesRoutine(Vector2Int coord, ChunkCreatureData data, float elapsed)
    {
        foreach (var cData in data.creatures)
        {
            if (liveCreatures.ContainsKey(cData.id)) continue; // Already live

            // Fake simulation applied
            if (elapsed > 0 && cData.health < 100f) cData.health += elapsed * 0.1f; 

            GameObject go = GetFromPool(cData.type);
            if (go == null) continue;

            go.transform.position = cData.position;
            go.transform.rotation = cData.rotation;
            go.SetActive(true);

            ICreature creature = go.GetComponent<ICreature>();
            if (creature == null) creature = go.AddComponent<CreatureComponent>();
            
            creature.Initialize(cData);
            liveCreatures[cData.id] = creature;
            
            yield return null; // Spread across frames
        }
    }

    // Called by WorldGenerator when a chunk unloads
    public void OnChunkUnloaded(Vector2Int coord)
    {
        if (!chunkData.ContainsKey(coord)) return;
        ChunkCreatureData data = chunkData[coord];
        data.lastUnloadTime = Time.time;

        List<string> toRemove = new List<string>();

        foreach (var kvp in liveCreatures)
        {
            ICreature c = kvp.Value;
            if (c.GetData().homeChunk == coord)
            {
                // Write data back
                c.SaveData();
                ReturnToPool(c.GetData().type, c.GetGameObject());
                toRemove.Add(kvp.Key);
            }
        }

        foreach (string id in toRemove)
        {
            liveCreatures.Remove(id);
        }
    }

    private void PopulateChunk(Vector2Int coord, ChunkCreatureData data)
    {
        // 6. Deterministic seed
        UnityEngine.Random.State oldState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(coord.x * 10000 + coord.y);

        int animalsToSpawn = UnityEngine.Random.Range(1, 4);
        for (int i = 0; i < animalsToSpawn; i++)
        {
            if (data.creatures.Count >= perChunkCap) break;

            CreatureData newAnimal = new CreatureData();
            newAnimal.id = System.Guid.NewGuid().ToString();
            
            float r = UnityEngine.Random.value;
            newAnimal.type = r > 0.7f ? "Wolf" : (r > 0.4f ? "Boar" : "Deer");
            
            // Random pos inside chunk
            float cs = 60f; // chunk size
            newAnimal.position = new Vector3(coord.x * cs + UnityEngine.Random.Range(-cs/2, cs/2), 0, coord.y * cs + UnityEngine.Random.Range(-cs/2, cs/2));
            newAnimal.rotation = Quaternion.Euler(0, UnityEngine.Random.Range(0, 360), 0);
            newAnimal.homeChunk = coord;
            
            data.creatures.Add(newAnimal);
        }

        UnityEngine.Random.state = oldState;
    }

    // 5. Chunk border crossing
    public void UpdateCreatureChunk(CreatureData data, Vector2Int newChunk)
    {
        if (data.homeChunk == newChunk) return;

        if (chunkData.ContainsKey(data.homeChunk))
        {
            chunkData[data.homeChunk].creatures.Remove(data);
        }

        data.homeChunk = newChunk;

        if (!chunkData.ContainsKey(newChunk))
        {
            chunkData[newChunk] = new ChunkCreatureData();
        }
        chunkData[newChunk].creatures.Add(data);
    }

    // Update handles simulation radius freezing
    private void Update()
    {
        if (Camera.main == null) return;
        Vector3 playerPos = Camera.main.transform.position;
        Vector2Int playerChunk = new Vector2Int(Mathf.RoundToInt(playerPos.x / 60f), Mathf.RoundToInt(playerPos.z / 60f));

        foreach (var kvp in liveCreatures)
        {
            ICreature c = kvp.Value;
            CreatureData d = c.GetData();
            
            int dist = Mathf.Max(Mathf.Abs(d.homeChunk.x - playerChunk.x), Mathf.Abs(d.homeChunk.y - playerChunk.y));
            
            // 4. Two radii simulation
            if (dist <= simulationRadius)
            {
                c.SetSimulating(true);
            }
            else
            {
                c.SetSimulating(false); // Freeze
            }
        }
    }
}

public interface ICreature
{
    void Initialize(CreatureData data);
    CreatureData GetData();
    void SaveData();
    GameObject GetGameObject();
    void SetSimulating(bool active);
}

// Minimal implementation of creature behavior bridging GameObject and Data
public class CreatureComponent : MonoBehaviour, ICreature
{
    private CreatureData data;
    private bool simulating = true;

    public void Initialize(CreatureData data)
    {
        this.data = data;
        // Reset NavMeshAgent, Animator, etc here.
    }

    public CreatureData GetData() { return data; }

    public void SaveData()
    {
        data.position = transform.position;
        data.rotation = transform.rotation;
    }

    public GameObject GetGameObject() { return gameObject; }

    public void SetSimulating(bool active)
    {
        if (simulating == active) return;
        simulating = active;
        
        // Turn off AI/Animator/Navmesh when not simulating
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = active;
        var nav = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (nav != null) nav.enabled = active;
    }

    private void Update()
    {
        if (!simulating) return;

        // Detect chunk crossing
        Vector2Int currentChunk = new Vector2Int(Mathf.RoundToInt(transform.position.x / 60f), Mathf.RoundToInt(transform.position.z / 60f));
        if (currentChunk != data.homeChunk)
        {
            CreatureManager.Instance.UpdateCreatureChunk(data, currentChunk);
        }
    }
}




