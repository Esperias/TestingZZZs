using System.Reflection;
using UnityEngine;
using System.Collections.Generic;
public class HarvestRecord
{
    public Vector3 pos;
    public float respawnTime;
    public HarvestRecord(Vector3 p, float t) { pos = p; respawnTime = t; }
}
public class ChunkData
{
    public System.Collections.Generic.List<HarvestRecord> harvestedTrees = new System.Collections.Generic.List<HarvestRecord>();
    public System.Collections.Generic.List<HarvestRecord> harvestedRocks = new System.Collections.Generic.List<HarvestRecord>();
    public System.Collections.Generic.List<HarvestRecord> harvestedBushes = new System.Collections.Generic.List<HarvestRecord>();
    public System.Collections.Generic.List<Vector3> pickedUpItems = new System.Collections.Generic.List<Vector3>();
}
public class WorldGenerator : MonoBehaviour
{
    public static WorldGenerator Instance { get; private set; }

    [Header("Settings")]
    public int chunkSize = 60;
    public int resolution = 40;
    public float scale = 0.02f;
    public float heightMultiplier = 15f;
    public int viewDistance = 3;
    public float waterLevel = 4f;
    public float seedOffsetX;
    public float seedOffsetZ;

        private Dictionary<Vector2Int, GameObject> chunks = new Dictionary<Vector2Int, GameObject>();
    public int GeneratedChunkCount => chunks.Count;
    public Dictionary<Vector2Int, ChunkData> chunkData { get; private set; } = new Dictionary<Vector2Int, ChunkData>();

        public Vector2Int GetChunkCoord(Vector3 worldPos)
    {
        return new Vector2Int(Mathf.RoundToInt(worldPos.x / chunkSize), Mathf.RoundToInt(worldPos.z / chunkSize));
    }

    public ChunkData GetOrCreateChunkData(Vector2Int coord)
    {
        if (!chunkData.ContainsKey(coord)) chunkData[coord] = new ChunkData();
        return chunkData[coord];
    }
    public Material terrainMaterial;
    public Material waterMaterial;
    public Material mapWaterMaterial;
    public Material waterDownMaterial;

	[Header("Building Materials")]
	public Material woodMaterial;
	public Material roofMaterial;
	public Material floorMaterial;
	public Material bedMaterial;
	public Material pillowMaterial;
	public Material fireMaterial;

	[Header("Cave")]
    public GameObject[] caveRockPrefabs;
    public GameObject[] caveCliffPrefabs;

    private void Awake()
    {
        viewDistance = GameSettings.RenderDistance;
        Instance = this;
        
        // Randomize seed every time for testing!
        seedOffsetX = UnityEngine.Random.Range(0f, 1000f);
        seedOffsetZ = UnityEngine.Random.Range(0f, 1000f);

        if (terrainMaterial == null)
        {
            Shader shader = Shader.Find("Custom/WalkingToon");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shader == null) shader = Shader.Find("Particles/Standard Surface");
            if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
            
            terrainMaterial = new Material(shader);
            if (terrainMaterial.HasProperty("_Smoothness")) terrainMaterial.SetFloat("_Smoothness", 0.1f);
        }

        CreateWaterMaterial();
    }

    private void Start()
    {
        LoadingScreen.Active = true;
        gameObject.AddComponent<LoadingScreen>();
        if (Camera.main != null) Camera.main.cullingMask &= ~(1 << 13); // Hide MapWater layer from Main Camera
        GenerateChunksAround(Vector2Int.zero, viewDistance);

        // Pre-generate terrain around the cave entrance so GetHeightAt() works
        // before CaveEntrance.Start() runs.
        // Find a high, very solid flat ground spot for the cave
        float bestX = 15f;
        float bestZ = 15f;
        
        bool foundSafeSpawn = false;
        // Search up to 3000 units away to guarantee we find land, even if we spawn in a massive ocean!
        for (float r = 20f; r < 3000f; r += 20f)
        {
            for (float a = 0; a < Mathf.PI * 2; a += 0.25f)
            {
                float x = Mathf.Cos(a) * r;
                float z = Mathf.Sin(a) * r;
                float h = GetHeightAt(x, z);
                if (h > waterLevel + 4f && h < waterLevel + 25f)
                {
                    bestX = x;
                    bestZ = z;
                    foundSafeSpawn = true;
                    break;
                }
            }
            if (foundSafeSpawn) break;
        }
        
        CaveEntrance.EntranceX = bestX;
        CaveEntrance.EntranceZ = bestZ;
        
        GameObject p = GameObject.Find("Player");
        if (p != null)
        {
            float py = GetHeightAt(bestX, bestZ);
            CharacterController cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.transform.position = new Vector3(bestX, py + 2f, bestZ);
            if (cc != null) cc.enabled = true;
            
            PlayerHealth ph = p.GetComponent<PlayerHealth>();
            if (ph != null) ph.spawnPosition = p.transform.position;
        }


        float ey = GetHeightAt(CaveEntrance.EntranceX, CaveEntrance.EntranceZ);
        GameObject entranceGo = new GameObject("CaveEntrance");
        entranceGo.transform.position = new Vector3(CaveEntrance.EntranceX, ey, CaveEntrance.EntranceZ);
        entranceGo.AddComponent<BoxCollider>();
        entranceGo.AddComponent<CaveEntrance>();

        // Place a Neutral NPC Village
        float vx = 180f;
        float vz = -180f;
        float vy = GetHeightAt(vx, vz);
        GameObject villageGo = new GameObject("NeutralVillage");
        villageGo.transform.position = new Vector3(vx, vy, vz);
        villageGo.AddComponent<NeutralVillageGenerator>();
    }

    private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int SmoothnessID = Shader.PropertyToID("_Smoothness");

    private void Update()
    {
        ProcessRecycleJobs();
        if (Camera.main == null) return;
        Vector3 p = Camera.main.transform.position;
        Vector2Int currentChunk = new Vector2Int(Mathf.RoundToInt(p.x / chunkSize), Mathf.RoundToInt(p.z / chunkSize));
        GenerateChunksAround(currentChunk, viewDistance);
        
        // Handle Winter freezing water!
        if (mapWaterMaterial != null)
        {
            Color waterColor = new Color(0.15f, 0.55f, 0.9f); // Blue water
            Color iceColor = new Color(0.85f, 0.95f, 1f); // White/Light blue ice
            bool isWinter = GameClock.CurrentSeason == Season.Winter;
            Color current = mapWaterMaterial.HasProperty(BaseColorID) ? mapWaterMaterial.GetColor(BaseColorID) : mapWaterMaterial.color;
            Color target = isWinter ? iceColor : waterColor;
            Color nextCol = Color.Lerp(current, target, Time.deltaTime * 0.1f);
            if (mapWaterMaterial.HasProperty(BaseColorID)) mapWaterMaterial.SetColor(BaseColorID, nextCol);
            mapWaterMaterial.color = nextCol;
        }
        if (waterMaterial != null)
        {
            Color waterColor = new Color(0.15f, 0.55f, 0.9f); // Blue water
            Color iceColor = new Color(0.85f, 0.95f, 1f); // White/Light blue ice
            
            bool isWinter = GameClock.CurrentSeason == Season.Winter;
            float targetSmooth = isWinter ? 0.3f : 0.9f;
            
            Color current = Color.cyan;
            if (waterMaterial.HasProperty(BaseColorID)) current = waterMaterial.GetColor(BaseColorID);
            else if (waterMaterial.HasProperty(ColorID)) current = waterMaterial.GetColor(ColorID);
            
            Color target = isWinter ? iceColor : waterColor;
            Color nextCol = Color.Lerp(current, target, Time.deltaTime * 0.1f);
            
            if (waterMaterial.HasProperty(BaseColorID)) waterMaterial.SetColor(BaseColorID, nextCol);
            if (waterMaterial.HasProperty(ColorID)) waterMaterial.SetColor(ColorID, nextCol);
            
            if (waterMaterial.HasProperty(SmoothnessID)) {
                float curSmooth = waterMaterial.GetFloat(SmoothnessID);
                waterMaterial.SetFloat(SmoothnessID, Mathf.Lerp(curSmooth, targetSmooth, Time.deltaTime * 0.1f));
            }
        }
    }

    // Cache - only re-scan when the player crosses a chunk boundary, not every frame
    private Vector2Int lastCheckedChunk = new Vector2Int(int.MinValue, int.MinValue);
    // Reusable list avoids allocating a new List<> every scan
    private readonly System.Collections.Generic.List<Vector2Int> keysToRemove
        = new System.Collections.Generic.List<Vector2Int>();

    private void GenerateChunksAround(Vector2Int center, int distance)
    {
        // Skip if player is still in the same chunk AND all expected chunks exist
        bool allLoaded = chunks.Count >= (distance * 2 + 1) * (distance * 2 + 1);
        if (center == lastCheckedChunk && allLoaded) return;
        lastCheckedChunk = center;

        keysToRemove.Clear();
        int unloadDistance = distance + 1;
        foreach (var kvp in chunks)
        {
            Vector2Int pos = kvp.Key;
            if (Mathf.Abs(pos.x - center.x) > unloadDistance || Mathf.Abs(pos.y - center.y) > unloadDistance)
                keysToRemove.Add(pos);
        }

        // Spread unloads across frames during gameplay to avoid a big hitch
        // On loading screen we destroy everything at once (player isn't moving)
        int destroyLimit = LoadingScreen.Active ? keysToRemove.Count : 1;
        for (int i = 0; i < Mathf.Min(destroyLimit, keysToRemove.Count); i++)
        {
            Vector2Int key = keysToRemove[i];
            if (chunks[key] != null) RecycleChunk(chunks[key]);
            chunks.Remove(key);
            if (CreatureManager.Instance != null) CreatureManager.Instance.OnChunkUnloaded(key); else if (AnimalManager.Instance != null) AnimalManager.Instance.DespawnChunk(key);
            // Reset so next frame does another pass if there are more to remove
            if (!LoadingScreen.Active) lastCheckedChunk = new Vector2Int(int.MinValue, int.MinValue);
        }

        if (generatingChunksCount < (LoadingScreen.Active ? 8 : 1))
        {
            float closestDist = float.MaxValue;
            Vector2Int closestChunk = center;
            bool found = false;

            for (int x = -distance; x <= distance; x++)
            {
                for (int z = -distance; z <= distance; z++)
                {
                    Vector2Int pos = center + new Vector2Int(x, z);
                    if (!chunks.ContainsKey(pos) && !generatingCoords.Contains(pos))
                    {
                        float dist = (center - pos).sqrMagnitude;
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestChunk = pos;
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                generatingCoords.Add(closestChunk);
                StartCoroutine(GenerateChunk(closestChunk));
            }
        }
    }

    public float GetHeightAt(float x, float z)
    {
        x += seedOffsetX;
        z += seedOffsetZ;
        
        float baseScale = 0.008f;
        float macro = Mathf.PerlinNoise(x * baseScale, z * baseScale);
        
        macro = Mathf.SmoothStep(0f, 1f, macro);
        float height = macro * heightMultiplier * 1.5f;
        
        float detail = Mathf.PerlinNoise(x * 0.05f, z * 0.05f) * 0.5f;
        height += detail;

        float pathNoise = Mathf.PerlinNoise(x * 0.012f, z * 0.012f);
        float pathMask = 1f - Mathf.Clamp01(Mathf.Abs(pathNoise - 0.42f) * 18f); 
        height -= pathMask * 0.6f;

        // Smooth organic lakes
        float lakeNoise = Mathf.PerlinNoise(x * 0.003f + 300f, z * 0.003f + 300f);
        float lakeT = Mathf.InverseLerp(0.62f, 0.75f, lakeNoise);
        float lakeMask = lakeT * lakeT * (3f - 2f * lakeT); // Apply smoothstep curve
        float lakeDepthNoise = Mathf.PerlinNoise(x * 0.02f, z * 0.02f);
        height -= lakeMask * (8f + lakeDepthNoise * 8f);

        // Smooth wide rivers with sloping banks
        float riverNoise = Mathf.PerlinNoise(x * 0.005f + 75f, z * 0.004f + 50f);
        float riverCenter = Mathf.Abs(riverNoise - 0.5f) * 2f;
        
        float rcT = Mathf.InverseLerp(0.0f, 0.1f, riverCenter);
        float riverChannel = 1f - (rcT * rcT * (3f - 2f * rcT));
        
        float rbT = Mathf.InverseLerp(0.1f, 0.22f, riverCenter);
        float riverBanks = 1f - (rbT * rbT * (3f - 2f * rbT));
        
        height -= riverChannel * 7f;
        height -= riverBanks * 2.5f;
        
        return height;
    }

    private Vector3 GetNormalAt(float x, float z)
    {
        float eps = 0.1f;
        float hL = GetHeightAt(x - eps, z);
        float hR = GetHeightAt(x + eps, z);
        float hD = GetHeightAt(x, z - eps);
        float hU = GetHeightAt(x, z + eps);
        
        Vector3 normal = new Vector3(hL - hR, 2f * eps, hD - hU);
        return normal.normalized;
    }

    public Color GetMapColorAt(float x, float z, float y)
    {
        x += seedOffsetX;
        z += seedOffsetZ;
        
        float pathNoise = Mathf.PerlinNoise(x * 0.012f, z * 0.012f);
        float pathMask = 1f - Mathf.Clamp01(Mathf.Abs(pathNoise - 0.42f) * 18f);
        
        // Synty-style pastel colors
        Color dirtPath = new Color(0.55f, 0.45f, 0.35f);
        
        float grassVariation = Mathf.PerlinNoise(x * 0.04f, z * 0.04f);
        Color lightGrass = new Color(0.42f, 0.58f, 0.38f); // Synty Minty Green
        Color darkGrass = new Color(0.32f, 0.48f, 0.28f);  // Synty Dark Green
        Color baseGrass = Color.Lerp(lightGrass, darkGrass, grassVariation);
        
        if (pathMask > 0.05f)
        {
            return Color.Lerp(baseGrass, dirtPath, pathMask);
        }
        
        if (y > heightMultiplier * 1.3f) return new Color(0.6f, 0.6f, 0.6f); // Grey rocks
        if (y < waterLevel + 1f) return new Color(0.88f, 0.82f, 0.65f); // Synty Cream Sand
        
        return baseGrass;
    }

    public Color GetColorAt(float x, float z, float y)
    {
        return GetColorAt(x, z, y, GetNormalAt(x, z));
    }

    public Color GetColorAt(float x, float z, float y, Vector3 normal)
    {
        float pathNoise = Mathf.PerlinNoise(x * 0.012f, z * 0.012f);
        float pathMask = 1f - Mathf.Clamp01(Mathf.Abs(pathNoise - 0.42f) * 18f);

        float steepness = 1.0f - Vector3.Dot(normal, Vector3.up);

        float sand = 0; float rock = 0; float grass = 0; float dirt = 0;
        
        if (pathMask > 0.05f) { dirt = pathMask; grass = 1f - pathMask; }
        else if (y < waterLevel + 1.2f) sand = 1f;
        else if (y > heightMultiplier * 1.3f || steepness > 0.45f) rock = 1f;
        else grass = 1f;
        
        return new Color(sand, rock, grass, dirt);
    }

    private int generatingChunksCount = 0; private HashSet<Vector2Int> generatingCoords = new HashSet<Vector2Int>();

    // ---- Chunk pooling ----
    // Terrain chunk roots (GameObject + MeshFilter + MeshRenderer + MeshCollider + Mesh) are reused instead of being
    // destroyed and recreated. When a chunk unloads its root is hidden instantly and its children (trees, rocks,
    // pickups, water) are processed a few per frame under a time budget: trees/rocks/pickups go back into their own
    // pools, anything else is destroyed. Once a root has no children left it is available for the next chunk.
    private readonly Stack<GameObject> chunkPool = new Stack<GameObject>();

    private class RecycleJob
    {
        public GameObject root;
        public Transform[] children;
        public int index;
    }

    private readonly Queue<RecycleJob> recycleJobs = new Queue<RecycleJob>();
    private Transform graveyard;
    private readonly System.Diagnostics.Stopwatch recycleWatch = new System.Diagnostics.Stopwatch();
    private const float RecycleBudgetMs = 1.0f;

    private void RecycleChunk(GameObject chunk)
    {
        if (graveyard == null)
        {
            GameObject g = new GameObject("ChunkGraveyard");
            g.transform.SetParent(transform);
            g.SetActive(false);
            graveyard = g.transform;
        }

        Transform t = chunk.transform;
        Transform[] kids = new Transform[t.childCount];
        for (int c = 0; c < kids.Length; c++) kids[c] = t.GetChild(c);

        // Hides the terrain, its collider and everything on it right now, and unregisters nodes from interaction.
        chunk.SetActive(false);
        recycleJobs.Enqueue(new RecycleJob { root = chunk, children = kids, index = 0 });
    }

    private void ProcessRecycleJobs()
    {
        if (recycleJobs.Count == 0) return;
        recycleWatch.Restart();

        while (recycleJobs.Count > 0)
        {
            RecycleJob job = recycleJobs.Peek();
            while (job.index < job.children.Length)
            {
                RecycleChild(job.children[job.index++]);
                // Always handle at least one child per frame, then stop once the time budget is spent.
                if (recycleWatch.Elapsed.TotalMilliseconds >= RecycleBudgetMs) return;
            }

            recycleJobs.Dequeue();
            if (job.root != null) chunkPool.Push(job.root);
        }
    }

    private void RecycleChild(Transform child)
    {
        if (child == null) return;

        ResourceNode node = child.GetComponent<ResourceNode>();
        if (node != null && ResourceGenerator.Instance != null && ResourceGenerator.Instance.TryRecycle(node, graveyard)) return;
        if (Inventory.Instance != null && Inventory.Instance.TryRecyclePickup(child.gameObject, graveyard)) return;

        Destroy(child.gameObject);
    }

    private System.Collections.IEnumerator GenerateChunk(Vector2Int coord)
    {
        generatingChunksCount++;
        float startX = coord.x * chunkSize;
        float startZ = coord.y * chunkSize;

        GameObject chunkGo;
        MeshFilter mf;
        MeshRenderer mr;
        MeshCollider mc;
        Mesh reuseMesh = null;

        if (chunkPool.Count > 0)
        {
            // Reuse a recycled chunk root. It stays inactive until the new mesh is assigned,
            // so the previous terrain never flashes at the new position.
            chunkGo = chunkPool.Pop();
            chunkGo.name = $"Chunk_{coord.x}_{coord.y}";
            chunkGo.transform.position = new Vector3(startX, 0, startZ);
            mf = chunkGo.GetComponent<MeshFilter>();
            mr = chunkGo.GetComponent<MeshRenderer>();
            mc = chunkGo.GetComponent<MeshCollider>();
            mc.sharedMesh = null;
            reuseMesh = mf.sharedMesh;
        }
        else
        {
            chunkGo = new GameObject($"Chunk_{coord.x}_{coord.y}");
            chunkGo.transform.SetParent(transform);
            chunkGo.layer = LayerMask.NameToLayer("Ground") > -1 ? LayerMask.NameToLayer("Ground") : 0;
            chunkGo.transform.position = new Vector3(startX, 0, startZ);
            mf = chunkGo.AddComponent<MeshFilter>();
            mr = chunkGo.AddComponent<MeshRenderer>();
            mc = chunkGo.AddComponent<MeshCollider>();
        }

        mr.sharedMaterial = terrainMaterial;

        int vertsPerLine = resolution + 1;
        Vector3[] vertices = new Vector3[vertsPerLine * vertsPerLine];
        Vector3[] normals = new Vector3[vertsPerLine * vertsPerLine];
        Color[] colors = new Color[vertsPerLine * vertsPerLine];
        int[] triangles = new int[resolution * resolution * 6];

        float step = (float)chunkSize / resolution;

        int i = 0;
        for (int z = 0; z <= resolution; z++)
        {
            for (int x = 0; x <= resolution; x++)
            {
                float lx = x * step - (chunkSize / 2f);
                float lz = z * step - (chunkSize / 2f);
                float wx = startX + lx;
                float wz = startZ + lz;
                
                float y = GetHeightAt(wx, wz);
                
                vertices[i] = new Vector3(lx, y, lz);
                Vector3 nrm = GetNormalAt(wx, wz);
                normals[i] = nrm;
                colors[i] = GetColorAt(wx, wz, y, nrm);
                i++;
            }
            // Spread the heavy vertex math over several frames while playing.
            // During the loading screen we don't care about hitches, so go flat out.
            if (!LoadingScreen.Active && z % 4 == 3) yield return null;
        }

        // Build triangle indices off the main thread - pure index math, no Unity API needed
        int[] triArray = null;
        var triTask = System.Threading.Tasks.Task.Run(() =>
        {
            int triCount = resolution * resolution * 6;
            triArray = new int[triCount];
            int ti = 0;
            for (int tz = 0; tz < resolution; tz++)
            {
                for (int tx = 0; tx < resolution; tx++)
                {
                    int vi = tz * vertsPerLine + tx;
                    triArray[ti++] = vi;
                    triArray[ti++] = vi + vertsPerLine;
                    triArray[ti++] = vi + 1;
                    triArray[ti++] = vi + 1;
                    triArray[ti++] = vi + vertsPerLine;
                    triArray[ti++] = vi + vertsPerLine + 1;
                }
            }
        });
        while (!triTask.IsCompleted) yield return null;

        if (!LoadingScreen.Active) yield return null;
        Mesh mesh = reuseMesh != null ? reuseMesh : new Mesh();
        if (reuseMesh != null) mesh.Clear();
        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.colors = colors;
        mesh.triangles = triArray;
        
        mf.sharedMesh = mesh;
        if (!chunkGo.activeSelf) chunkGo.SetActive(true);

        // Bake the physics mesh on a worker thread so it doesn't cause a frame hitch,
        // then assign it (Unity reuses the pre-baked data).
        EntityId meshId = mesh.GetEntityId();
        var bakeTask = System.Threading.Tasks.Task.Run(() => Physics.BakeMesh(meshId, false));
        while (!bakeTask.IsCompleted) yield return null;
        mc.sharedMesh = mesh;

        bool hasWater = false;
        for (int vi = 0; vi < vertices.Length; vi++)
        {
            if (vertices[vi].y < waterLevel) { hasWater = true; break; }
        }
        if (hasWater) SpawnWaterPlane(chunkGo.transform);

        if (!LoadingScreen.Active) yield return null;
        if (ResourceGenerator.Instance != null)
            yield return ResourceGenerator.Instance.GenerateResourcesForChunk(coord, chunkGo.transform);
            
        if (!LoadingScreen.Active) yield return null;
        if (Inventory.Instance != null)
            yield return Inventory.Instance.GeneratePickupsForChunk(coord, chunkGo.transform);
            
        if (!LoadingScreen.Active) yield return null;
        if (CreatureManager.Instance != null) CreatureManager.Instance.OnChunkLoaded(coord, chunkGo.transform); else if (AnimalManager.Instance != null) AnimalManager.Instance.GenerateAnimalsForChunk(coord, chunkGo.transform);

        chunks.Add(coord, chunkGo);
        generatingCoords.Remove(coord);
        generatingChunksCount--;
    }

    private void CreateWaterMaterial()
    {
        if (mapWaterMaterial == null)
        {
            mapWaterMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mapWaterMaterial.color = new Color(0.2f, 0.5f, 0.85f, 0.6f); // Nice semi-transparent map water
            if (mapWaterMaterial.HasProperty("_BaseColor")) mapWaterMaterial.SetColor("_BaseColor", mapWaterMaterial.color);
            
            // Properly configure URP transparency keywords
            mapWaterMaterial.SetFloat("_Surface", 1.0f);
            mapWaterMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mapWaterMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mapWaterMaterial.SetInt("_ZWrite", 0);
            mapWaterMaterial.EnableKeyword("_ALPHABLEND_ON");
            mapWaterMaterial.renderQueue = 3000;
        }

#if UNITY_EDITOR
        // Force-load Fantastic Seaside Town water material (overriding the Synty one from the Inspector)
        string path = "Assets/Fantastic Seaside Town/Materials/M_FX_water_up_town.mat";
        Material seasideWater = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(path);
        if (seasideWater != null)
        {
            waterMaterial = seasideWater;
            // The Seaside Town shader fades to invisible at a distance, causing the high-up Map Camera to see nothing!
            // We must disable this distance fade or extend it drastically.
            waterMaterial.SetFloat("_OpacityDistanceFadeToggle", 0f);
            waterMaterial.DisableKeyword("_OPACITYDISTANCEFADETOGGLE_ON");
            waterMaterial.SetFloat("_OpacityDistanceFadeStart", 10000f);
            waterMaterial.SetFloat("_OpacityDistanceFadeEnd", 12000f);
        }
        
        if (waterDownMaterial == null)
        {
            string downPath = "Assets/Fantastic Seaside Town/Materials/M_FX_water_down_town.mat";
            Material seasideWaterDown = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>(downPath);
            if (seasideWaterDown != null) waterDownMaterial = seasideWaterDown;
        }
#endif

        if (waterMaterial != null) return;
        
        waterMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (waterMaterial.shader == null) waterMaterial = new Material(Shader.Find("Standard"));
        
        Color waterColor = new Color(0.15f, 0.55f, 0.9f);
        if (waterMaterial.HasProperty("_BaseColor")) waterMaterial.SetColor("_BaseColor", waterColor);
        if (waterMaterial.HasProperty("_Color"))     waterMaterial.SetColor("_Color", waterColor);
        if (waterMaterial.HasProperty("_Smoothness")) waterMaterial.SetFloat("_Smoothness", 0.9f);
        
        // Disable blinding specular highlights that ruin the top-down maps
        if (waterMaterial.HasProperty("_SpecularHighlights")) waterMaterial.SetFloat("_SpecularHighlights", 0f);
        if (waterMaterial.HasProperty("_EnvironmentReflections")) waterMaterial.SetFloat("_EnvironmentReflections", 0f);
        if (waterMaterial.HasProperty("_SpecColor")) waterMaterial.SetColor("_SpecColor", Color.black);
    }

    private void SpawnWaterPlane(Transform parent)
    {
        GameObject waterGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
        waterGo.name = "Water";
        waterGo.transform.SetParent(parent);
        waterGo.layer = 4; // Water layer
        Destroy(waterGo.GetComponent<Collider>());
        
        waterGo.GetComponent<MeshRenderer>().material = waterMaterial;
        
        waterGo.transform.localPosition = new Vector3(0f, waterLevel - 0.5f, 0f);
        waterGo.transform.localRotation = Quaternion.identity;
        waterGo.transform.localScale = new Vector3(chunkSize / 10f, 1f, chunkSize / 10f);
        
        if (waterDownMaterial != null)
        {
            GameObject waterDownGo = GameObject.CreatePrimitive(PrimitiveType.Plane);
            waterDownGo.name = "Water_Down";
            waterDownGo.transform.SetParent(parent);
            waterDownGo.layer = 4; // Water layer
            Destroy(waterDownGo.GetComponent<Collider>());
            waterDownGo.GetComponent<MeshRenderer>().material = waterDownMaterial;
            waterDownGo.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            waterDownGo.transform.localPosition = new Vector3(0f, waterLevel - 0.51f, 0f); // Slightly below the UP plane
            waterDownGo.transform.localRotation = Quaternion.Euler(180f, 0f, 0f); // Point normals downwards!
            waterDownGo.transform.localScale = new Vector3(chunkSize / 10f, 1f, chunkSize / 10f);
        }
    }
}

public class NeutralVillageGenerator : MonoBehaviour
{
    private void Start()
    {
        WorldGenerator wg = WorldGenerator.Instance;
        if (wg == null) return;

        Vector3 center = transform.position;

        Material woodMat = wg.woodMaterial != null ? wg.woodMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        woodMat.color = new Color(0.4f, 0.25f, 0.1f);
        
        Material roofMat = wg.roofMaterial != null ? wg.roofMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        roofMat.color = new Color(0.15f, 0.35f, 0.6f); // Blue roofs for trading post!
        
        Material floorMat = wg.floorMaterial != null ? wg.floorMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        floorMat.color = new Color(0.6f, 0.55f, 0.5f);

        // Build 5 Trading Post structures
        int houseCount = 5;
        for (int i = 0; i < houseCount; i++)
        {
            float angle = i * (Mathf.PI * 2f / houseCount);
            float dist = 14f;
            float hx = center.x + Mathf.Cos(angle) * dist;
            float hz = center.z + Mathf.Sin(angle) * dist;
            float hy = wg.GetHeightAt(hx, hz);
            Vector3 housePos = new Vector3(hx, hy, hz);

            GameObject houseNode = new GameObject("NPC_House_" + i);
            houseNode.transform.SetParent(transform);
            houseNode.transform.position = housePos;
            houseNode.transform.LookAt(new Vector3(center.x, hy, center.z));

            // Floor
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.SetParent(houseNode.transform, false);
            floor.transform.localPosition = new Vector3(0f, 0.25f, 0f);
            floor.transform.localScale = new Vector3(8f, 0.5f, 6f);
            floor.GetComponent<MeshRenderer>().material = floorMat;

            // Walls (Hollow interior with a doorway)
            // Left Wall
            GameObject leftWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            leftWall.transform.SetParent(houseNode.transform, false);
            leftWall.transform.localPosition = new Vector3(-3.75f, 2.25f, 0f);
            leftWall.transform.localScale = new Vector3(0.5f, 4f, 5.5f);
            leftWall.GetComponent<MeshRenderer>().material = woodMat;

            // Right Wall
            GameObject rightWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rightWall.transform.SetParent(houseNode.transform, false);
            rightWall.transform.localPosition = new Vector3(3.75f, 2.25f, 0f);
            rightWall.transform.localScale = new Vector3(0.5f, 4f, 5.5f);
            rightWall.GetComponent<MeshRenderer>().material = woodMat;

            // Back Wall
            GameObject backWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            backWall.transform.SetParent(houseNode.transform, false);
            backWall.transform.localPosition = new Vector3(0f, 2.25f, 2.5f);
            backWall.transform.localScale = new Vector3(7f, 4f, 0.5f);
            backWall.GetComponent<MeshRenderer>().material = woodMat;

            // Front Left Wall
            GameObject frontLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontLeft.transform.SetParent(houseNode.transform, false);
            frontLeft.transform.localPosition = new Vector3(-2.25f, 2.25f, -2.5f);
            frontLeft.transform.localScale = new Vector3(2.5f, 4f, 0.5f);
            frontLeft.GetComponent<MeshRenderer>().material = woodMat;

            // Front Right Wall
            GameObject frontRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frontRight.transform.SetParent(houseNode.transform, false);
            frontRight.transform.localPosition = new Vector3(2.25f, 2.25f, -2.5f);
            frontRight.transform.localScale = new Vector3(2.5f, 4f, 0.5f);
            frontRight.GetComponent<MeshRenderer>().material = woodMat;

            // Bed inside the house
            GameObject bed = new GameObject("Bed");
            bed.transform.SetParent(houseNode.transform, false);
            bed.transform.localPosition = new Vector3(2.5f, 0.5f, 1.0f); // Tucked in back right corner
            
            Material bedMat = wg.bedMaterial != null ? wg.bedMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            bedMat.color = new Color(0.8f, 0.2f, 0.2f); // Red blankets
            
            GameObject mattress = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mattress.transform.SetParent(bed.transform, false);
            mattress.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            mattress.transform.localScale = new Vector3(1.2f, 0.4f, 2f);
            mattress.GetComponent<MeshRenderer>().material = bedMat;

            GameObject pillow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillow.transform.SetParent(bed.transform, false);
            pillow.transform.localPosition = new Vector3(0f, 0.45f, 0.7f);
            pillow.transform.localScale = new Vector3(0.9f, 0.15f, 0.5f);
            Material pillowMat = wg.pillowMaterial != null ? wg.pillowMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            pillowMat.color = Color.white;
            pillow.GetComponent<MeshRenderer>().material = pillowMat;

            // Roof
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.transform.SetParent(houseNode.transform, false);
            roof.transform.localPosition = new Vector3(0f, 5.5f, 0f);
            roof.transform.localRotation = Quaternion.Euler(0f, 45f, 0f);
            roof.transform.localScale = new Vector3(6.5f, 3f, 6.5f);
            roof.GetComponent<MeshRenderer>().material = roofMat;
        }

        // Center Trading Plaza
        GameObject plaza = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        plaza.transform.SetParent(transform);
        float cy = wg.GetHeightAt(center.x, center.z);
        plaza.transform.position = new Vector3(center.x, cy + 0.1f, center.z);
        plaza.transform.localScale = new Vector3(18f, 0.2f, 18f);
        plaza.GetComponent<MeshRenderer>().material = floorMat;

        // Central Campfire
        GameObject fireBase = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        fireBase.transform.SetParent(transform);
        fireBase.transform.position = new Vector3(center.x, cy + 0.3f, center.z);
        fireBase.transform.localScale = new Vector3(3f, 0.4f, 3f);
        Material fireMat = wg.fireMaterial != null ? wg.fireMaterial : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        fireMat.color = new Color(0.9f, 0.4f, 0.1f);
        fireBase.GetComponent<MeshRenderer>().material = fireMat;

        // Add the NPC Spawner to populate it!
        NPCSpawner spawner = gameObject.AddComponent<NPCSpawner>();
        spawner.enabled = false;
        spawner.npcCount = 12; spawner.requireTotem = false; // Bustling trade post!
        
        NPCSpawner[] allSpawners = FindObjectsByType<NPCSpawner>(FindObjectsInactive.Exclude);
        foreach (var existing in allSpawners)
        {
            if (existing != spawner)
            {
                var pField = typeof(NPCSpawner).GetField("npcPrefabs", BindingFlags.NonPublic | BindingFlags.Instance);
                if (pField != null)
                {
                    GameObject[] prefabs = pField.GetValue(existing) as GameObject[];
                    if (prefabs != null && prefabs.Length > 0)
                    {
                        pField.SetValue(spawner, prefabs);
                        break;
                    }
                }
            }
        }
        spawner.enabled = true;
    }
}









