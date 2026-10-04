using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AnimalManager : MonoBehaviour
{
    public static AnimalManager Instance { get; private set; }
    
    [Header("Prefabs")]
    [Tooltip("Assign these in the Inspector!")]
    public GameObject deerPrefab;
    public GameObject boarPrefab;
    public GameObject wolfPrefab;
    
    [Header("Settings")]
    public int maxAnimals = 150;
    public float minSpawnDistance = 250f;
    public float maxSpawnDistance = 2000f;

    [Header("Ravens")]
    public GameObject ravenPrefab;
    public int maxRavens = 25;
    
    private List<GameObject> activeAnimals = new List<GameObject>();
    private List<GameObject> activeRavens = new List<GameObject>();
    private Transform player;
    
    private Transform mainParent;
    private Transform ravenParent;
    private Dictionary<Animal.AnimalType, Transform> typeParents = new Dictionary<Animal.AnimalType, Transform>();
    
    private void Awake()
    {
        Instance = this;
    }
    
    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        
        mainParent = new GameObject("Animals").transform;
        
        ravenParent = new GameObject("Ravens").transform;
        ravenParent.SetParent(mainParent);
        
        foreach (Animal.AnimalType t in System.Enum.GetValues(typeof(Animal.AnimalType)))
        {
            Transform tp = new GameObject(t.ToString() + "s").transform;
            tp.SetParent(mainParent);
            typeParents[t] = tp;
        }
        
                StartCoroutine(SpawnRoutine());
    }

    private IEnumerator SpawnRoutine()
    {
        // Initial wait to let the world generate
        yield return new WaitForSeconds(2f);
        
        while (true)
        {
            activeAnimals.RemoveAll(a => a == null); // Remove dead ones
            activeRavens.RemoveAll(r => r == null);
            
            if (player != null)
            {
                // Spawn normal animals
                if (activeAnimals.Count < maxAnimals)
                {
                    if (deerPrefab != null || boarPrefab != null || wolfPrefab != null)
                    {
                        SpawnRandomAnimal();
                    }
                }

                // Spawn ravens
                if (activeRavens.Count < maxRavens)
                {
                    if (ravenPrefab != null)
                    {
                        SpawnRaven();
                    }
                }
            }
            
            yield return new WaitForSeconds(Random.Range(4f, 8f));
        }
    }
    
    private void SpawnRandomAnimal()
    {
        // For 'time-to-time' spawning, let's pick a random loaded chunk instead of a circle around the player
        if (WorldGenerator.Instance == null || WorldGenerator.Instance.chunkData.Count == 0) return;
        
        System.Collections.Generic.List<Vector2Int> coords = new System.Collections.Generic.List<Vector2Int>(WorldGenerator.Instance.chunkData.Keys);
        if (coords.Count == 0) return;
        
        Vector2Int randomCoord = coords[Random.Range(0, coords.Count)];
        SpawnRandomAnimalInChunk(randomCoord, null);
    }

    private void SpawnRaven()
    {
        if (WorldGenerator.Instance == null || WorldGenerator.Instance.chunkData.Count == 0) return;
        
        System.Collections.Generic.List<Vector2Int> coords = new System.Collections.Generic.List<Vector2Int>(WorldGenerator.Instance.chunkData.Keys);
        if (coords.Count == 0) return;
        
        Vector2Int randomCoord = coords[Random.Range(0, coords.Count)];
        SpawnRavenInChunk(randomCoord, null);
    }
    
    /// <summary>Removes animals/ravens standing in a chunk that just unloaded (otherwise they fall through the void).</summary>
    public void DespawnChunk(Vector2Int coord)
    {
        float cs = WorldGenerator.Instance != null ? WorldGenerator.Instance.chunkSize : 60f;
        CullInChunk(activeAnimals, coord, cs);
        CullInChunk(activeRavens, coord, cs);
    }

    private static void CullInChunk(List<GameObject> list, Vector2Int coord, float cs)
    {
        for (int i = list.Count - 1; i >= 0; i--)
        {
            GameObject g = list[i];
            if (g == null) { list.RemoveAt(i); continue; }
            Vector3 p = g.transform.position;
            if (Mathf.RoundToInt(p.x / cs) == coord.x && Mathf.RoundToInt(p.z / cs) == coord.y)
            {
                Destroy(g);
                list.RemoveAt(i);
            }
        }
    }

    public void GenerateAnimalsForChunk(Vector2Int coord, Transform chunkTransform)
    {
        if (activeAnimals.Count >= maxAnimals) return;
        
        int animalsToSpawn = Random.Range(1, 4); // 1 to 3 animals per chunk
        for (int i = 0; i < animalsToSpawn; i++)
        {
            if (activeAnimals.Count >= maxAnimals) break;
            SpawnRandomAnimalInChunk(coord, chunkTransform);
        }
        
        if (activeRavens.Count < maxRavens && Random.value > 0.5f) // 50% chance for a raven in a chunk
        {
            SpawnRavenInChunk(coord, chunkTransform);
        }
    }
    
    private void SpawnRandomAnimalInChunk(Vector2Int coord, Transform chunkTransform)
    {
        float r = Random.value;
        GameObject prefab = deerPrefab;
        Animal.AnimalType type = Animal.AnimalType.Deer;
        
        if (r > 0.7f && wolfPrefab != null) { prefab = wolfPrefab; type = Animal.AnimalType.Wolf; }
        else if (r > 0.4f && boarPrefab != null) { prefab = boarPrefab; type = Animal.AnimalType.Boar; }
        
        if (prefab == null) return;
        SpawnSpecificAnimalInChunk(prefab, type, coord, chunkTransform);
    }
    
    private void SpawnSpecificAnimalInChunk(GameObject prefab, Animal.AnimalType type, Vector2Int coord, Transform chunkTransform)
    {
        float chunkSize = 60f;
        if (WorldGenerator.Instance != null) chunkSize = WorldGenerator.Instance.chunkSize;
        
        float startX = coord.x * chunkSize;
        float startZ = coord.y * chunkSize;
        
        Vector3 spawnPos = Vector3.zero;
        bool found = false;
        
        for (int i = 0; i < 10; i++)
        {
            float x = startX + Random.Range(-chunkSize / 2f, chunkSize / 2f);
            float z = startZ + Random.Range(-chunkSize / 2f, chunkSize / 2f);
            
            float y = 0f;
            if (WorldGenerator.Instance != null)
            {
                y = WorldGenerator.Instance.GetHeightAt(x, z);
                if (y < WorldGenerator.Instance.waterLevel + 1f) continue;
            }
            
            spawnPos = new Vector3(x, y + 2f, z);
            found = true;
            break;
        }
        
        if (!found) return;
        
        GameObject newAnimal = Instantiate(prefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), typeParents[type]);
        newAnimal.name = type.ToString();
        
        Animal ai = newAnimal.GetComponent<Animal>();
        if (ai == null) ai = newAnimal.AddComponent<Animal>();
        
        ai.type = type;
        activeAnimals.Add(newAnimal);
    }
    
    private void SpawnRavenInChunk(Vector2Int coord, Transform chunkTransform)
    {
        if (ravenPrefab == null) return;
        float chunkSize = 60f;
        if (WorldGenerator.Instance != null) chunkSize = WorldGenerator.Instance.chunkSize;
        
        float startX = coord.x * chunkSize;
        float startZ = coord.y * chunkSize;
        
        float x = startX + Random.Range(-chunkSize / 2f, chunkSize / 2f);
        float z = startZ + Random.Range(-chunkSize / 2f, chunkSize / 2f);
        
        float y = 0f;
        if (WorldGenerator.Instance != null)
        {
            y = WorldGenerator.Instance.GetHeightAt(x, z);
            if (y < WorldGenerator.Instance.waterLevel + 1f) return;
        }
        
        Vector3 spawnPos = new Vector3(x, y + 2f, z);
        
        GameObject newRaven = Instantiate(ravenPrefab, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0), ravenParent);
        newRaven.name = "Raven";
        
        Raven birdScript = newRaven.GetComponent<Raven>();
        if (birdScript == null) birdScript = newRaven.AddComponent<Raven>();
        
        activeRavens.Add(newRaven);
    }
}




