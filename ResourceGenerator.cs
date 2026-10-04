using UnityEngine;
using System.Collections.Generic;

public class ResourceGenerator : MonoBehaviour
{
    public static ResourceGenerator Instance { get; private set; }

    [Header("Prefabs")]
    public GameObject[] fruitTreePrefabs;
    public GameObject bushPrefab;
    public GameObject[] rockPrefabs;

    public bool IsDone { get; private set; } = true;
    public float Progress { get; private set; } = 1f;

    private void Awake()
    {
        Instance = this;
    }

    public System.Collections.IEnumerator GenerateResourcesForChunk(Vector2Int coord, Transform chunkTransform)
    {
        if (WorldGenerator.Instance == null) yield break;

        Random.State oldState = Random.state;
        Random.InitState(coord.x * 10000 + coord.y);

        string[] types = { "Wood", "Stone", "Berries" };
        
        int numClusters = Random.Range(10, 20);
        List<Vector3> placed = new List<Vector3>();

        for (int i = 0; i < numClusters; i++)
        {
            if (TryFindSpotInChunk(coord, out Vector3 center))
            {
                int idx = Random.Range(0, types.Length);
                string type = types[idx];

                int count = Random.Range(2, 5);
                for (int k = 0; k < count; k++)
                {
                    Vector2 offset2D = Random.insideUnitCircle * 5f;
                    Vector3 spot = center + new Vector3(offset2D.x, 0, offset2D.y);
                    
                    float y = WorldGenerator.Instance.GetHeightAt(spot.x, spot.z);
                    spot.y = y;
                    
                    float wLevel = WorldGenerator.Instance.waterLevel;
                    if (type == "Wood" && spot.y < wLevel - 0.5f) continue;
                    if (type != "Wood" && spot.y < wLevel + 1.0f) continue;

                    Vector2 cavePos = new Vector2(CaveEntrance.EntranceX, CaveEntrance.EntranceZ);
                    if (Vector2.Distance(new Vector2(spot.x, spot.z), cavePos) < 20f) continue;

                    bool overlap = false;
                    foreach (Vector3 p in placed)
                    {
                        if (Vector3.SqrMagnitude(p - spot) < 6f)
                        {
                            overlap = true;
                            break;
                        }
                    }
                    if (overlap) continue;
                    placed.Add(spot);

                    float respawnRemaining = 0f;
                    ChunkData data = WorldGenerator.Instance.GetOrCreateChunkData(coord);
                    var list = type == "Wood" ? data.harvestedTrees : (type == "Stone" ? data.harvestedRocks : data.harvestedBushes);
                    for (int n = list.Count - 1; n >= 0; n--)
                    {
                        if (Vector3.SqrMagnitude(list[n].pos - spot) < 0.1f)
                        {
                            respawnRemaining = list[n].respawnTime - Time.realtimeSinceStartup;
                            if (respawnRemaining <= 0) list.RemoveAt(n);
                            else break;
                        }
                    }

                    GameObject prefab = null;
                    if (type == "Wood" && fruitTreePrefabs != null && fruitTreePrefabs.Length > 0)
                        prefab = fruitTreePrefabs[Random.Range(0, fruitTreePrefabs.Length)];
                    else if (type == "Stone" && rockPrefabs != null && rockPrefabs.Length > 0)
                        prefab = rockPrefabs[Random.Range(0, rockPrefabs.Length)];
                    else if (type == "Berries" && bushPrefab != null)
                        prefab = bushPrefab;

                    if (prefab == null) continue;

                    // Same random calls, in the same order, whether the node is new or reused,
                    // so a chunk always regenerates identically.
                    Quaternion spawnRot = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                    ResourceNode pooledNode = TryTakeNode(prefab);
                    bool reused = pooledNode != null;
                    GameObject go;
                    if (reused)
                    {
                        go = pooledNode.gameObject;
                        go.transform.SetParent(chunkTransform, false);
                        go.transform.SetPositionAndRotation(spot, spawnRot);
                    }
                    else
                    {
                        go = Instantiate(prefab, spot, spawnRot, chunkTransform);
                    }
                    go.name = "Node_" + type;

                    float rootScale = 1f;
                    if (type == "Wood") rootScale = Random.Range(1.8f, 2.5f);
                    else if (type == "Berries") rootScale = Random.Range(0.4f, 0.6f);
                    else rootScale = Random.Range(0.85f, 1.25f);

                    go.transform.localScale = Vector3.one * rootScale;

                    List<Transform> berryList = null;
                    if (type == "Berries")
                    {
                        int berryCount = Random.Range(6, 12);
                        berryList = (reused && pooledNode.spawnedBerries != null) ? pooledNode.spawnedBerries : new List<Transform>();
                        for (int j = 0; j < berryCount; j++)
                        {
                            Transform bt;
                            if (j < berryList.Count && berryList[j] != null)
                            {
                                bt = berryList[j];
                                bt.gameObject.SetActive(true);
                            }
                            else
                            {
                                bt = CreateBerry(go.transform);
                                if (j < berryList.Count) berryList[j] = bt; else berryList.Add(bt);
                            }
                            Vector3 dir = Random.onUnitSphere;
                            if (dir.y < -0.2f) dir.y = Mathf.Abs(dir.y);
                            float dist = Random.Range(0.5f, 0.9f);
                            bt.localPosition = new Vector3(0, 0.6f, 0) + dir * dist;
                            bt.localScale = Vector3.one * Random.Range(0.3f, 0.45f);
                        }
                        // A reused bush may have had more berries than this one needs
                        for (int j = berryList.Count - 1; j >= berryCount; j--)
                        {
                            if (berryList[j] != null) Destroy(berryList[j].gameObject);
                            berryList.RemoveAt(j);
                        }
                    }

                    int nodeYield = Random.Range(1, 3);
                    int nodeMaxHits = Random.Range(3, 6);
                    ResourceNode rn;
                    if (reused)
                    {
                        rn = pooledNode;
                        rn.Rebind(type, nodeYield, nodeMaxHits);
                    }
                    else
                    {
                        rn = go.AddComponent<ResourceNode>();
                        rn.Initialize(type, nodeYield, nodeMaxHits);
                        rn.sourcePrefab = prefab;
                    }
                    rn.spawnedBerries = berryList;
                    if (respawnRemaining > 0) rn.ForceHarvestedState(respawnRemaining);
                                }
            }

            if (!LoadingScreen.Active)
            {
                // Yield between clusters without breaking the per-chunk deterministic seed
                Random.State mine = Random.state;
                Random.state = oldState;
                yield return null;
                oldState = Random.state;
                Random.state = mine;
            }
        }

        Random.state = oldState;
    }

    // ---- Node pooling ----
    // Trees / rocks / bushes are expensive to Instantiate (prefab hierarchy, LODGroup, colliders built in
    // ResourceNode.Start, billboards...). When a chunk unloads its nodes are reset and parked here, keyed by
    // prefab, and handed back out when the next chunk generates.
    private readonly Dictionary<GameObject, Stack<ResourceNode>> nodePool = new Dictionary<GameObject, Stack<ResourceNode>>();
    private int pooledNodeCount;
    private const int MaxPooledNodes = 8000;

    private ResourceNode TryTakeNode(GameObject prefab)
    {
        if (prefab == null) return null;
        if (nodePool.TryGetValue(prefab, out Stack<ResourceNode> stack))
        {
            while (stack.Count > 0)
            {
                ResourceNode n = stack.Pop();
                pooledNodeCount--;
                if (n != null) return n;
            }
        }
        return null;
    }

    /// <summary>Returns true if the node was taken into the pool (caller must NOT destroy it).</summary>
    public bool TryRecycle(ResourceNode node, Transform parking)
    {
        if (node == null || node.sourcePrefab == null || pooledNodeCount >= MaxPooledNodes) return false;

        node.ResetForPool();
        node.transform.SetParent(parking, false);

        if (!nodePool.TryGetValue(node.sourcePrefab, out Stack<ResourceNode> stack))
        {
            stack = new Stack<ResourceNode>();
            nodePool[node.sourcePrefab] = stack;
        }
        stack.Push(node);
        pooledNodeCount++;
        return true;
    }

    // Berries are plain spheres. CreatePrimitive builds a collider that was then immediately destroyed;
    // a cached mesh on a bare GameObject skips that. The name must contain "Sphere" (ResourceNode finds berries by name).
    private static Mesh berryMesh;

    private static Transform CreateBerry(Transform parent)
    {
        if (berryMesh == null)
        {
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            berryMesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tmp);
        }
        GameObject berry = new GameObject("Sphere", typeof(MeshFilter), typeof(MeshRenderer));
        berry.transform.SetParent(parent, false);
        berry.GetComponent<MeshFilter>().sharedMesh = berryMesh;
        berry.GetComponent<MeshRenderer>().sharedMaterial = GetBerryMaterial();
        return berry.transform;
    }

    private static Material berryMaterial;

    private static Material GetBerryMaterial()
    {
        if (berryMaterial == null)
        {
            Shader s = Shader.Find("Custom/WalkingToon");
            if (s == null) s = Shader.Find("Standard");
            berryMaterial = new Material(s);
            berryMaterial.SetColor("_BaseColor", new Color(0.8f, 0.1f, 0.2f));
        }
        return berryMaterial;
    }

    private bool TryFindSpotInChunk(Vector2Int coord, out Vector3 point)
    {
        float chunkSize = WorldGenerator.Instance.chunkSize;
        float startX = coord.x * chunkSize;
        float startZ = coord.y * chunkSize;

        for (int attempt = 0; attempt < 10; attempt++)
        {
            float x = Random.Range(startX - (chunkSize / 2f), startX + (chunkSize / 2f));
            float z = Random.Range(startZ - (chunkSize / 2f), startZ + (chunkSize / 2f));

            float y = WorldGenerator.Instance.GetHeightAt(x, z);
            
            float wLevel = WorldGenerator.Instance.waterLevel;
            if (y < wLevel + 0.5f) continue;

            point = new Vector3(x, y, z);
            return true;
        }

        point = default;
        return false;
    }
}







