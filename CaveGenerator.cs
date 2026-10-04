#pragma warning disable CS0414
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Drop this MonoBehaviour anywhere in TestCave.unity (e.g., on an empty
/// GameObject called "CaveGenerator").  On Start() it procedurally builds:
///
///   • A winding cave tunnel mesh (~120 units long) with organic side pockets
///   • Stalactites / stalagmites at random intervals
///   • Atmospheric point-light "torches" along the path
///   • Ore vein ResourceNodes (Iron, Crystal) at dead-ends and side walls
///   • A CaveExit portal near the entrance
///   • A thick impassable rock cap above the tunnel (so the player can't fall out)
///
/// The player is teleported here by CaveTransition and positioned at SpawnPoint.
/// </summary>
public class CaveGenerator : MonoBehaviour
{
    // CaveTransition reads this to know where to place the player.
    public static Vector3 SpawnPoint { get; private set; }
    public static CaveGenerator Instance { get; private set; }
    public bool IsReady { get; private set; } = false;

    private void Awake() { Instance = this; }

    [Header("Tunnel Shape")]
    [SerializeField] private int   segments    = 60;      // much longer cave
    [SerializeField] private float segLength   = 4.5f;    // tighter segments for smoother curves
    public GameObject enemyPrefab;
    [SerializeField] private float tunnelRadius = 6.0f;   // wider mean radius
    [SerializeField] private float radiusNoise = 0.4f;   // smoother walls (less jagged noise)
    [SerializeField] private int ringVerts = 24;     // higher poly count per ring


    [Header("Branches")]
    [SerializeField] private int branchCount   = 4;
    [SerializeField] private int branchSegments = 16;

    [Header("Ore Nodes")]
    [SerializeField] private int ironVeinCount = 10;
    [SerializeField] private int crystalVeinCount = 8;

    [Header("Atmosphere")]
    [SerializeField] private int torchCount = 12;

    // ── internal ──────────────────────────────────────────────────────────────
    public class PathData
    {
        public List<Vector3> points = new List<Vector3>();
        public float radius;
    }
    private List<PathData> allPaths = new List<PathData>();
    private List<Vector3> mainPath => allPaths[0].points;

    private Material      caveMat;
    private Material      oreMat;
    private Material      crystalMat;

    // ──────────────────────────────────────────────────────────────────────────
    private System.Collections.IEnumerator Start()
    {
        IsReady = false;
        Random.InitState(42);   // deterministic cave every run

        caveMat    = MakeMaterial(new Color(0.22f, 0.19f, 0.16f), "cave");
        oreMat     = MakeMaterial(new Color(0.55f, 0.38f, 0.18f), "ore");
        crystalMat = MakeMaterial(new Color(0.25f, 0.55f, 0.80f), "crystal");

        // ── 1. Generate paths ─────────────────────────────────────────────────
        PathData main = new PathData { radius = tunnelRadius };
        allPaths.Add(main);
        GeneratePath(mainPath, Vector3.zero, Vector3.forward, segments);
        
        Vector3 startFwd = (mainPath[1] - mainPath[0]).normalized;
        SpawnPoint = mainPath[0] + startFwd * 5f + Vector3.up * 1.5f;

        // Generate branches
        for (int b = 0; b < branchCount; b++)
        {
            int pivot = Random.Range(4, mainPath.Count - 4);
            Vector3 dir = Quaternion.Euler(
                Random.Range(-15f, 15f),
                Random.Range(50f, 130f) * (Random.value > 0.5f ? 1f : -1f),
                0f) * GetForward(mainPath, pivot);

            PathData branch = new PathData { radius = tunnelRadius * 0.65f };
            // Start the branch near the wall of the main tunnel (0.85 * radius)
            // This prevents it from protruding deeply into the room.
            Vector3 branchStart = mainPath[pivot] + dir.normalized * (tunnelRadius * 0.85f);
            GeneratePath(branch.points, branchStart, dir, branchSegments, (b + 1) * 100);
            allPaths.Add(branch);
        }

        // ── Standalone Testing Setup ──────────────────────────────────────────
        

        // ── 2. Build tunnel meshes ─────────────────────────────────────────────
        BuildCaveVolumetric();

        Physics.SyncTransforms();

        // ── 3. Decor ───────────────────────────────────────────────────────────

        SpawnOreVeins();
        SpawnTorches();

        // ── 4. Ceiling cap — a thick slab so sky isn't visible above ──────────
        

        // ── 5. CaveExit portal near the entrance ──────────────────────────────
        

        // ── 6. Lighting & atmosphere ──────────────────────────────────────────
        SpawnDecorations();
        SpawnEnemies();
        SetupAtmosphere();

        // WAIT FOR PHYSICS TO BAKE MESH COLLIDERS
        yield return new WaitForFixedUpdate();
        yield return new WaitForFixedUpdate();

        // 5. CaveExit portal safely inside the entrance
        GameObject exitObj = new GameObject("CaveExit");
        exitObj.transform.SetParent(transform, false);
        // Move it 2 units into the tunnel so it's not clipped outside the mesh boundary
        Vector3 exitPos = mainPath[0] + startFwd * 2f;
        
        if (Physics.Raycast(exitPos + Vector3.up * 10f, Vector3.down, out RaycastHit exitHit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) {
            exitPos = exitHit.point;
        } else {
            exitPos.y -= (tunnelRadius * 0.8f);
        }
        
        exitObj.transform.position = exitPos;
        // Rotate 180 degrees so the front of the portal quad faces INTO the cave (towards the player)
        exitObj.transform.rotation = Quaternion.LookRotation(-startFwd);
        exitObj.AddComponent<CaveExit>();

        // Drop SpawnPoint to exactly the ground floor so player doesn't spawn floating/falling
        if (Physics.Raycast(SpawnPoint + Vector3.up * 10f, Vector3.down, out RaycastHit spawnHit, 30f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            SpawnPoint = spawnHit.point + Vector3.up * 0.1f;
        } else {
            SpawnPoint -= new Vector3(0, tunnelRadius * 0.8f, 0);
        }

        // --- Standalone Player Setup ---
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null)
        {
#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Player t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                GameObject prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null)
                {
                    GameObject pGo = Instantiate(prefab, SpawnPoint, Quaternion.identity);
                    pGo.name = "Player";
                    pc = pGo.GetComponent<PlayerController>();
                }
            }
#endif
        }
        
        if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            GameObject esGo = new GameObject("EventSystem");
            esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
        }

        if (pc != null)
        {
            CharacterController cc = pc.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            pc.transform.position = SpawnPoint;
            pc.transform.rotation = Quaternion.LookRotation(startFwd);
            if (cc != null) cc.enabled = true;
            
          }
          IsReady = true;
    }

    // ── Path generation ───────────────────────────────────────────────────────

    private void GeneratePath(List<Vector3> path, Vector3 start, Vector3 dir,
        int count, int seed = 0)
    {
        Vector3 pos    = start;
        Vector3 fwd    = dir.normalized;
        float noiseOff = seed * 0.1f;

        for (int i = 0; i < count; i++)
        {
            path.Add(pos);
            // Gentle winding — yaw bias keeps tunnel mostly horizontal
            float yaw   = (Mathf.PerlinNoise(i * 0.3f + noiseOff, 0f) - 0.5f) * 60f;
            float pitch = (Mathf.PerlinNoise(0f, i * 0.3f + noiseOff) - 0.5f) * 20f;
            fwd = Quaternion.Euler(pitch, yaw, 0f) * fwd;
            fwd.Normalize();
            pos += fwd * segLength;
        }
    }

    // ── Tunnel distance checks ────────────────────────────────────────────────
    
    private float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float t = Vector3.Dot(p - a, ab) / ab.sqrMagnitude;
        t = Mathf.Clamp01(t);
        Vector3 closest = a + t * ab;
        
        // Approximate elliptical distance (squashed on Y by 0.7)
        Vector3 diff = p - closest;
        diff.y /= 0.7f;
        return diff.magnitude;
    }

    private bool IsInsideOtherTunnel(Vector3 pt, PathData excludePath)
    {
        // ONLY cut holes in the main tunnel. 
        // Branches remain solid to perfectly overlap and seal the hole.
        if (excludePath != allPaths[0]) return false;

        for (int b = 1; b < allPaths.Count; b++)
        {
            var path = allPaths[b];
            float minDist = float.MaxValue;
            for (int i = 0; i < path.points.Count - 1; i++)
            {
                float d = DistanceToSegment(pt, path.points[i], path.points[i+1]);
                if (d < minDist) minDist = d;
            }
            
            // Cut a hole slightly smaller than the branch so the branch covers the edges.
            if (minDist < path.radius * 0.85f)
            {
                return true;
            }
        }
        return false;
    }

    private Vector3 GetForward(List<Vector3> path, int i)
    {
        if (i < path.Count - 1) return (path[i + 1] - path[i]).normalized;
        return (path[i] - path[i - 1]).normalized;
    }

    // ── Decor ─────────────────────────────────────────────────────────────────

    // ── Ore veins ─────────────────────────────────────────────────────────────

    
    private void SpawnOreVeins()
    {
        int totalIron = 50;
        int totalCrystals = 30;

        // Iron randomly on walls/ceilings/floors
        for (int i = 0; i < totalIron; i++)
        {
            var path = allPaths[Random.Range(0, allPaths.Count)];
            if (path.points.Count < 2) continue;
            
            int pathIdx = Random.Range(1, path.points.Count - 1);
            Vector3 pt = path.points[pathIdx];
            Vector3 forward = GetForward(path.points, pathIdx);
            
            // Random direction around the tunnel (excluding straight up)
            float angle = Random.Range(0f, 360f);
            Vector3 localDir = Quaternion.Euler(0, 0, angle) * Vector3.right;
            Vector3 worldDir = Quaternion.LookRotation(forward) * localDir;
            
            if (Physics.Raycast(pt, worldDir, out RaycastHit hit, 35f))
            {
                SpawnOreVein(hit.point, hit.normal, "Iron");
            }
        }

        // Crystals mostly on floors
        for (int i = 0; i < totalCrystals; i++)
        {
            var path = allPaths[Random.Range(0, allPaths.Count)];
            if (path.points.Count < 2) continue;
            
            int pathIdx = Random.Range(path.points.Count / 3, path.points.Count);
            Vector3 pt = path.points[pathIdx];
            Vector3 forward = GetForward(path.points, pathIdx);
            
            Vector3 worldDir = Quaternion.LookRotation(forward) * new Vector3(Random.Range(-0.8f, 0.8f), -1, 0).normalized;
            
            if (Physics.Raycast(pt, worldDir, out RaycastHit hit, 35f))
            {
                SpawnOreVein(hit.point, hit.normal, "Crystal");
            }
        }

        // Guarantee ores at branch ends
        for (int i = 1; i < allPaths.Count; i++)
        {
            var branch = allPaths[i];
            if (branch.points.Count > 0)
            {
                Vector3 endPt = branch.points[branch.points.Count - 1];
                if (Physics.Raycast(endPt, Vector3.down, out RaycastHit hit, 35f))
                {
                    SpawnOreVein(hit.point, hit.normal, i % 2 == 0 ? "Iron" : "Crystal");
                }
            }
        }
    }

    private void SpawnOreVein(Vector3 pos, Vector3 normal, string type)
    {
        GameObject veinBase = new GameObject($"Vein_{type}");
        veinBase.transform.SetParent(transform, false);
        veinBase.transform.position = pos + normal * 0.3f; // sink into wall
        veinBase.transform.up = normal; // Align to the wall!

        SphereCollider sc = veinBase.AddComponent<SphereCollider>();
        sc.radius = 0.8f;

        Material mat = type == "Crystal" ? crystalMat : oreMat;

        int lumps = Random.Range(3, 7);
        for (int i = 0; i < lumps; i++)
        {
            Vector3 localPos = Random.insideUnitSphere * 0.4f;
            localPos.y = Mathf.Abs(localPos.y); // point mostly outwards
            float size = Random.Range(0.4f, 1.0f);

            GameObject lump = type == "Crystal" 
                ? GameObject.CreatePrimitive(PrimitiveType.Cube)
                : GameObject.CreatePrimitive(PrimitiveType.Sphere);
            
            Destroy(lump.GetComponent<Collider>());
            
            lump.transform.SetParent(veinBase.transform, false);
            lump.transform.localPosition = localPos;
            lump.transform.localScale = type == "Crystal" 
                ? new Vector3(size * 0.5f, size * 1.5f, size * 0.5f)
                : Vector3.one * size;

            if (type == "Crystal") lump.transform.rotation = Random.rotation;

            lump.GetComponent<MeshRenderer>().material = mat;
        }

        ResourceNode node = veinBase.AddComponent<ResourceNode>();
        node.Initialize(type == "Crystal" ? "Crystal" : "Iron", 1, 4);
        node.nodeColor = type == "Crystal" ? new Color(0.3f, 0.6f, 0.9f) : new Color(0.6f, 0.4f, 0.2f);

        InteractionManager.Register(node);
    }

    private void SpawnTorches()
    {
        // 1. Room center lights (massive soft glow)
        for(int i = 0; i < mainPath.Count; i += 8) {
            Vector3 pos = mainPath[i];
            if (Physics.Raycast(pos, Vector3.down, out RaycastHit hit, 30f)) {
                CreateLight(hit.point + Vector3.up * 3.5f, 35f, 3.0f, new Color(1f, 0.8f, 0.5f), true);
            }
        }

        // 2. Torches scattered along all tunnels
        foreach (var path in allPaths) {
            int step = 6;
            for (int i = step; i < path.points.Count; i += step) {
                Vector3 pt = path.points[i];
                Vector3 right = Quaternion.Euler(0, 90, 0) * GetForward(path.points, i);
                if (Random.value > 0.5f) right = -right;
                
                if (Physics.Raycast(pt, right, out RaycastHit hit, 30f)) {
                    CreateLight(hit.point + hit.normal * 0.5f, 18f, 2.0f, new Color(1f, 0.6f, 0.2f), false);
                }
            }
        }
    }

    private void CreateLight(Vector3 pos, float range, float intensity, Color col, bool isRoomLight) {
        GameObject light = new GameObject(isRoomLight ? "RoomLight" : "CaveTorch");
        light.transform.SetParent(transform, false);
        light.transform.position = pos;

        Light l = light.AddComponent<Light>();
        l.type      = LightType.Point;
        l.range     = range * Random.Range(0.8f, 1.2f);
        l.intensity = intensity * Random.Range(0.8f, 1.2f);
        l.color     = col;
        l.shadows = LightShadows.None; // Prevent URP shadow atlas spam

        if (!isRoomLight) {
            GameObject ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(ember.GetComponent<Collider>());
            ember.transform.SetParent(light.transform, false);
            ember.transform.localPosition = Vector3.zero;
            ember.transform.localScale = Vector3.one * 0.2f;
            Material em = new Material(Shader.Find("Unlit/Color"));
            if (em.HasProperty("_Color")) em.SetColor("_Color", col);
            if (em.HasProperty("_BaseColor")) em.SetColor("_BaseColor", col);
            ember.GetComponent<MeshRenderer>().material = em;
        }
    }


    
    private void SpawnDecorations()
    {
        // 1. Glowing Mushrooms (Crystals with emissive material)
        int mushroomCount = 80;
        for (int i = 0; i < mushroomCount; i++) {
            var path = allPaths[UnityEngine.Random.Range(0, allPaths.Count)];
            if (path.points.Count < 2) continue;
            int idx = UnityEngine.Random.Range(1, path.points.Count - 1);
            Vector3 pt = path.points[idx];
            Vector3 dir = Quaternion.LookRotation(GetForward(path.points, idx)) * new Vector3(UnityEngine.Random.Range(-1f, 1f), -1f, UnityEngine.Random.Range(-0.5f, 0.5f)).normalized;
            
            if (Physics.Raycast(pt, dir, out RaycastHit hit, 40f)) {
                if (hit.normal.y < 0.5f) continue; // Only on floors
                
                GameObject shroom = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                shroom.transform.SetParent(transform, false);
                shroom.transform.position = hit.point + hit.normal * 0.4f;
                shroom.transform.up = hit.normal;
                shroom.transform.localScale = new Vector3(0.3f, 0.6f, 0.3f) * UnityEngine.Random.Range(0.6f, 1.5f);
                Destroy(shroom.GetComponent<Collider>());
                
                Material m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                Color emit = new Color(UnityEngine.Random.Range(0.2f, 0.4f), UnityEngine.Random.Range(0.6f, 1f), UnityEngine.Random.Range(0.8f, 1f));
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", emit);
                if (m.HasProperty("_Color")) m.SetColor("_Color", emit);
                m.EnableKeyword("_EMISSION");
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", emit * 1.5f);
                shroom.GetComponent<MeshRenderer>().material = m;

                if (UnityEngine.Random.value > 0.7f) {
                    CreateLight(shroom.transform.position + Vector3.up, 8f, 1.2f, emit, false);
                }
            }
        }

        // 2. Stalactites (hanging from ceiling)
        int stalactiteCount = 150;
        Material rockMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (rockMat.HasProperty("_BaseColor")) rockMat.SetColor("_BaseColor", new Color(0.2f, 0.18f, 0.16f));
        if (rockMat.HasProperty("_Color")) rockMat.SetColor("_Color", new Color(0.2f, 0.18f, 0.16f));

        for (int i = 0; i < stalactiteCount; i++) {
            var path = allPaths[UnityEngine.Random.Range(0, allPaths.Count)];
            if (path.points.Count < 2) continue;
            int idx = UnityEngine.Random.Range(1, path.points.Count - 1);
            Vector3 pt = path.points[idx];
            Vector3 dir = Quaternion.LookRotation(GetForward(path.points, idx)) * new Vector3(UnityEngine.Random.Range(-1f, 1f), 1f, UnityEngine.Random.Range(-0.5f, 0.5f)).normalized;
            
            if (Physics.Raycast(pt, dir, out RaycastHit hit, 60f)) {
                if (hit.normal.y > -0.5f) continue; // Only on ceilings
                
                GameObject st = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                st.transform.SetParent(transform, false);
                float length = UnityEngine.Random.Range(1.5f, 6.0f);
                st.transform.position = hit.point + hit.normal * (length * 0.5f);
                st.transform.up = hit.normal;
                
                // taper the cone
                st.transform.localScale = new Vector3(UnityEngine.Random.Range(0.4f, 1.5f), length, UnityEngine.Random.Range(0.4f, 1.5f));
                st.GetComponent<MeshRenderer>().material = rockMat;
            }
        }
    }

    private void SetupAtmosphere()
    {
        // Very low ambient — cave should feel dark
        RenderSettings.ambientMode      = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight     = new Color(0.04f, 0.035f, 0.03f);
        RenderSettings.fog              = true;
        RenderSettings.fogMode          = FogMode.Exponential;
        RenderSettings.fogDensity       = 0.018f;
        RenderSettings.fogColor         = new Color(0.05f, 0.04f, 0.04f);

        // Disable the scene's directional light if present (sunlight in a cave = wrong)
        Light[] lights = FindObjectsByType<Light>();
        foreach (var l in lights)
            if (l.type == LightType.Directional) l.enabled = false;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Material MakeMaterial(Color color, string hint)
    {
        // Use standard lit for caves so the point lights create smooth gradients
        // rather than flat toon bands, making the depth much easier to read.
        Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(s); m.name = "Cave_" + hint;
        
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
        if (m.HasProperty("_Color"))     m.SetColor("_Color", color);
        
        // Make it look like rough rock
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", 0.1f);
        
        return m;
    }

    private void BuildCaveVolumetric()
    {
        Bounds b = new Bounds(mainPath[0], Vector3.one);
        foreach(var p in allPaths) foreach(var pt in p.points) b.Encapsulate(pt);
        b.Expand(45f); // Padding for rooms and noise
        
        float cellSize = 1.25f; // Good resolution for caves
        int W = Mathf.CeilToInt(b.size.x / cellSize);
        int H = Mathf.CeilToInt(b.size.y / cellSize);
        int D = Mathf.CeilToInt(b.size.z / cellSize);
        
        float[,,] sdf = new float[W+1, H+1, D+1];
        
        // Evaluate SDF
        for(int x=0; x<=W; x++) {
            for(int y=0; y<=H; y++) {
                for(int z=0; z<=D; z++) {
                    Vector3 wp = b.min + new Vector3(x,y,z) * cellSize;
                    float val = GetCaveSDF(wp);
                    if (x <= 1 || x >= W - 1 || y <= 1 || y >= H - 1 || z <= 1 || z >= D - 1) {
                        val = Mathf.Max(val, 5f);
                    }
                    sdf[x,y,z] = val;
                }
            }
        }
        
        int[,,] vertIndices = new int[W, H, D];
        List<Vector3> verts = new List<Vector3>();
        List<Vector3> norms = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> tris = new List<int>();
        
        for(int x=0; x<W; x++) {
            for(int y=0; y<H; y++) {
                for(int z=0; z<D; z++) {
                    float v0 = sdf[x,y,z];     float v1 = sdf[x+1,y,z];
                    float v2 = sdf[x,y+1,z];   float v3 = sdf[x+1,y+1,z];
                    float v4 = sdf[x,y,z+1];   float v5 = sdf[x+1,y,z+1];
                    float v6 = sdf[x,y+1,z+1]; float v7 = sdf[x+1,y+1,z+1];
                    
                    int mask = 0;
                    if(v0<0) mask |= 1; if(v1<0) mask |= 2;
                    if(v2<0) mask |= 4; if(v3<0) mask |= 8;
                    if(v4<0) mask |= 16; if(v5<0) mask |= 32;
                    if(v6<0) mask |= 64; if(v7<0) mask |= 128;
                    
                    if(mask == 0 || mask == 255) {
                        vertIndices[x,y,z] = -1;
                        continue;
                    }
                    
                    Vector3 sum = Vector3.zero;
                    int count = 0;
                    void AddEdge(float va, float vb, int dx, int dy, int dz) {
                        if(Mathf.Sign(va) != Mathf.Sign(vb)) {
                            float t = va / (va - vb);
                            sum += new Vector3(x + dx*t, y + dy*t, z + dz*t);
                            count++;
                        }
                    }
                    AddEdge(v0, v1, 1, 0, 0); AddEdge(v0, v2, 0, 1, 0); 
                    AddEdge(v1, v3, 0, 1, 0); AddEdge(v2, v3, 1, 0, 0);
                    AddEdge(v4, v5, 1, 0, 0); AddEdge(v4, v6, 0, 1, 0);
                    AddEdge(v5, v7, 0, 1, 0); AddEdge(v6, v7, 1, 0, 0);
                    AddEdge(v0, v4, 0, 0, 1); AddEdge(v1, v5, 0, 0, 1);
                    AddEdge(v2, v6, 0, 0, 1); AddEdge(v3, v7, 0, 0, 1);
                    
                    Vector3 localPos = count > 0 ? (sum / count) : new Vector3(x+0.5f, y+0.5f, z+0.5f);
                    Vector3 wp = b.min + localPos * cellSize;
                    
                    vertIndices[x,y,z] = verts.Count;
                    verts.Add(wp);
                    uvs.Add(new Vector2(wp.x, wp.z) * 0.1f);
                    
                    Vector3 grad = new Vector3(
                        GetCaveSDF(wp + Vector3.right * 0.1f) - GetCaveSDF(wp - Vector3.right * 0.1f),
                        GetCaveSDF(wp + Vector3.up * 0.1f) - GetCaveSDF(wp - Vector3.up * 0.1f),
                        GetCaveSDF(wp + Vector3.forward * 0.1f) - GetCaveSDF(wp - Vector3.forward * 0.1f)
                    ).normalized;
                    norms.Add(-grad);
                }
            }
        }
        
        for(int x=1; x<W; x++) {
            for(int y=1; y<H; y++) {
                for(int z=1; z<D; z++) {
                    bool v = sdf[x,y,z] < 0;
                    
                    // Edge X
                    if(x < W-1 && v != (sdf[x+1,y,z] < 0)) {
                        int c0 = vertIndices[x, y,   z];
                        int c1 = vertIndices[x, y-1, z];
                        int c2 = vertIndices[x, y-1, z-1];
                        int c3 = vertIndices[x, y,   z-1];
                        if(c0>=0 && c1>=0 && c2>=0 && c3>=0) {
                            tris.Add(c0); tris.Add(c1); tris.Add(c2); tris.Add(c0); tris.Add(c2); tris.Add(c3); tris.Add(c0); tris.Add(c2); tris.Add(c1); tris.Add(c0); tris.Add(c3); tris.Add(c2);
                        }
                    }
                    
                    // Edge Y
                    if(y < H-1 && v != (sdf[x,y+1,z] < 0)) {
                        int c0 = vertIndices[x,   y, z];
                        int c1 = vertIndices[x,   y, z-1];
                        int c2 = vertIndices[x-1, y, z-1];
                        int c3 = vertIndices[x-1, y, z];
                        if(c0>=0 && c1>=0 && c2>=0 && c3>=0) {
                            tris.Add(c0); tris.Add(c1); tris.Add(c2); tris.Add(c0); tris.Add(c2); tris.Add(c3); tris.Add(c0); tris.Add(c2); tris.Add(c1); tris.Add(c0); tris.Add(c3); tris.Add(c2);
                        }
                    }
                    
                    // Edge Z
                    if(z < D-1 && v != (sdf[x,y,z+1] < 0)) {
                        int c0 = vertIndices[x,   y,   z];
                        int c1 = vertIndices[x-1, y,   z];
                        int c2 = vertIndices[x-1, y-1, z];
                        int c3 = vertIndices[x,   y-1, z];
                        if(c0>=0 && c1>=0 && c2>=0 && c3>=0) {
                            tris.Add(c0); tris.Add(c1); tris.Add(c2); tris.Add(c0); tris.Add(c2); tris.Add(c3); tris.Add(c0); tris.Add(c2); tris.Add(c1); tris.Add(c0); tris.Add(c3); tris.Add(c2);
                        }
                    }
                }
            }
        }
        
        Mesh mesh = new Mesh { name = "CaveMesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.RecalculateBounds();
        
        GameObject go = new GameObject("CaveSolid");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().material = caveMat;
        
        MeshCollider mc = gameObject.GetComponent<MeshCollider>();
        if (mc == null) mc = gameObject.AddComponent<MeshCollider>();
        mc.sharedMesh = mesh;
    }

    
    private float GetCaveSDF(Vector3 p)
    {
        float searchRadius = 45f;
        float minDist = searchRadius;
        
        foreach(var path in allPaths) {
            for(int i=0; i<path.points.Count-1; i++) {
                Vector3 a = path.points[i];
                Vector3 b = path.points[i+1];
                
                float minX = Mathf.Min(a.x, b.x) - searchRadius;
                float maxX = Mathf.Max(a.x, b.x) + searchRadius;
                if (p.x < minX || p.x > maxX) continue;
                
                float minZ = Mathf.Min(a.z, b.z) - searchRadius;
                float maxZ = Mathf.Max(a.z, b.z) + searchRadius;
                if (p.z < minZ || p.z > maxZ) continue;

                float d = DistanceToSegment(p, a, b);
                minDist = Mathf.Min(minDist, d);
            }
        }
        
        float roomDist = searchRadius;
        // Massive organic rooms instead of uniform spheres
        for(int i=0; i<mainPath.Count; i+=6) {
            float dist = Vector3.Distance(p, mainPath[i]);
            float organicRadius = 18f + Mathf.PerlinNoise(mainPath[i].x * 0.1f, mainPath[i].z * 0.1f) * 12f;
            roomDist = Mathf.Min(roomDist, dist - organicRadius);
        }
        
        float sdf = Mathf.Min(minDist - tunnelRadius, roomDist);
        
        // Massive organic caverns that carve out huge sections
        float massiveNoise = Mathf.PerlinNoise(p.x * 0.02f + 100f, p.z * 0.02f + 100f);
        if (massiveNoise > 0.6f) {
            sdf -= (massiveNoise - 0.6f) * 20f; // Extremely deep organic cuts
        }
        
        // High-frequency rock noise
        float noise = (Mathf.PerlinNoise(p.x * 0.15f, p.z * 0.15f) - 0.5f) * 4f;
        noise += (Mathf.PerlinNoise(p.y * 0.2f, p.x * 0.2f) - 0.5f) * 2f;
        sdf -= noise;
        
        // Dynamic, varying floor level (no longer a flat plane!)
        float floorDepth = 4f + Mathf.PerlinNoise(p.x * 0.04f, p.z * 0.04f) * 8f;
        float floorDist = -p.y - floorDepth; 
        sdf = Mathf.Max(sdf, floorDist);
        
        // Dynamic, towering ceilings!
        float ceilHeight = 12f + Mathf.PerlinNoise(p.x * 0.03f + 50f, p.z * 0.03f + 50f) * 25f;
        float ceilDist = p.y - ceilHeight;
        sdf = Mathf.Max(sdf, ceilDist);
        
        return sdf;
    }
    private void SpawnEnemies()
    {
        int enemyCount = 15;
        Material enemyMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        if (enemyMat.HasProperty("_BaseColor")) enemyMat.SetColor("_BaseColor", new Color(0.8f, 0.2f, 0.2f));
        if (enemyMat.HasProperty("_Color")) enemyMat.SetColor("_Color", new Color(0.8f, 0.2f, 0.2f));

        for (int i = 0; i < enemyCount; i++)
        {
            var path = allPaths[UnityEngine.Random.Range(0, allPaths.Count)];
            if (path.points.Count < 2) continue;
            
            int pathIdx = UnityEngine.Random.Range(1, path.points.Count - 1);
            Vector3 pt = path.points[pathIdx];

            // Raycast down to find the floor to spawn
            if (Physics.Raycast(pt, Vector3.down, out RaycastHit hit, 30f))
            {
                if (hit.normal.y < 0.7f) continue; // Too steep

                GameObject enemyGo = enemyPrefab != null ? Instantiate(enemyPrefab) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemyGo.name = "CaveEnemy";
                enemyGo.transform.SetParent(transform, false);
                
                // Position on floor
                enemyGo.transform.position = hit.point;
                
                // Visuals
                if (enemyPrefab == null) enemyGo.GetComponent<MeshRenderer>().material = enemyMat;
                
                // Add AI
                EnemyAI ai = enemyGo.GetComponent<EnemyAI>();
                if (ai == null) ai = enemyGo.AddComponent<EnemyAI>();
                ai.Initialize(enemyGo.transform.position);
                ai.maxHealth = 100;
            }
        }
    }
}



[RequireComponent(typeof(CharacterController))]
public class EnemyAI : MonoBehaviour
{
    private enum State { Idle, Wander, Chase, Attack, Hit, Dead }
    private State currentState = State.Idle;

    public float walkSpeed = 2.5f;
    public float runSpeed = 5.5f;
    public float aggroRange = 8.0f;
    public float attackRange = 1.8f;
    public float attackCooldown = 1.5f;
    public float maxHealth = 100f; // Defaulted to 100 to survive tools
    public float respawnTime = 15f;
    public int damage = 1;

    private float currentHealth;
    private float stateTimer;
    private float stuckTimer;
    private float lastAttackTime;
    private Vector3 moveTarget;
    
    private CharacterController cc;
    private Transform player;
    private float gravityVelocity;
    private float previousY;

    private Vector3 spawnPoint;
    private Renderer[] renderers;
    private Animator animator;

    private int speedHash;
    private int turnHash;
    private int hitXHash;
    private int hitYHash;

    // UI & Effects exactly like Animal.cs
    private Canvas healthCanvas;
    private RectTransform healthFillRect;
    private RectTransform healthTrailRect;
    private float healthBarTimer;
    private float trailTimer;
    private float trailHealth;
    private Dictionary<Renderer, Material[]> originalMatsCache;
    private bool isFlashing = false;

    public void Initialize(Vector3 spawnLocation)
    {
        spawnPoint = spawnLocation;
        transform.position = spawnPoint;
        moveTarget = spawnPoint;
    }

    private void Awake()
    {
                cc = GetComponent<CharacterController>();
        cc.center = new Vector3(0, 1f, 0);
        cc.height = 2f;
        cc.radius = 0.4f;
        renderers = GetComponentsInChildren<Renderer>();
        animator = GetComponentInChildren<Animator>();
        if (animator != null) animator.applyRootMotion = false;
        if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null) smr.updateWhenOffscreen = true;

        if (animator != null)
        {
            speedHash = Animator.StringToHash("Speed");
            turnHash = Animator.StringToHash("Turn");
            hitXHash = Animator.StringToHash("HitX");
            hitYHash = Animator.StringToHash("HitY");
        }

        currentHealth = maxHealth;
        trailHealth = maxHealth;

        originalMatsCache = new Dictionary<Renderer, Material[]>();
        foreach (Renderer r in renderers)
        {
            if (r != null) originalMatsCache[r] = r.materials;
        }

        // Generate Floating Healthbar UI
        GameObject canvasObj = new GameObject("HealthCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0, 2.2f, 0); // Slightly higher for skeleton
        healthCanvas = canvasObj.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1.5f, 0.15f);
        
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0, 0, 0, 0.8f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; 
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        
        GameObject trailObj = new GameObject("Trail");
        trailObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image trailImg = trailObj.AddComponent<UnityEngine.UI.Image>();
        trailImg.color = Color.white;
        healthTrailRect = trailObj.GetComponent<RectTransform>();
        healthTrailRect.anchorMin = Vector2.zero; healthTrailRect.anchorMax = Vector2.one;
        healthTrailRect.offsetMin = Vector2.zero; healthTrailRect.offsetMax = Vector2.zero;
        
        GameObject fgObj = new GameObject("Foreground");
        fgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image fgImg = fgObj.AddComponent<UnityEngine.UI.Image>();
        fgImg.color = new Color(0.9f, 0.1f, 0.1f, 1f);
        healthFillRect = fgObj.GetComponent<RectTransform>();
        healthFillRect.anchorMin = Vector2.zero; healthFillRect.anchorMax = Vector2.one;
        healthFillRect.offsetMin = Vector2.zero; healthFillRect.offsetMax = Vector2.zero;
        
        healthCanvas.gameObject.SetActive(false);
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        EnterState(State.Idle);
    }

    private void Update()
    {
        float actualVertVel = Time.deltaTime > 0 ? (transform.position.y - previousY) / Time.deltaTime : 0f;
        previousY = transform.position.y;
        if (currentState == State.Dead)
        {
            if (healthCanvas != null && healthCanvas.gameObject.activeSelf) healthCanvas.gameObject.SetActive(false);
            return;
        }

        // Handle Healthbar UI
        if (healthCanvas != null && healthCanvas.gameObject.activeSelf)
        {
            healthBarTimer -= Time.deltaTime;
            if (healthBarTimer <= 0)
            {
                healthCanvas.gameObject.SetActive(false);
            }
            if (Camera.main != null)
            {
                healthCanvas.transform.rotation = Quaternion.LookRotation(healthCanvas.transform.position - Camera.main.transform.position);
            }
            
            if (trailTimer > 0) trailTimer -= Time.deltaTime;
            else
            {
                trailHealth = Mathf.Lerp(trailHealth, currentHealth, 5f * Time.deltaTime);
                if (healthTrailRect != null) healthTrailRect.anchorMax = new Vector2(Mathf.Clamp01(trailHealth / maxHealth), 1f);
            }
        }

        // Gravity
        if (!cc.isGrounded) 
        {
            gravityVelocity += Physics.gravity.y * 2.5f * Time.deltaTime;
            // Prevent slingshot buildup if stuck on a steep slope
            if (actualVertVel > -0.1f && gravityVelocity < -2f) gravityVelocity = Physics.gravity.y * 0.1f;
        }
        else if (gravityVelocity < 0) gravityVelocity = Physics.gravity.y * 0.1f;

        Vector3 gravityMove = Vector3.up * gravityVelocity * Time.deltaTime;

        // AI Logic
        Vector3 flatVel = cc.velocity;
        flatVel.y = 0;
        bool isTryingToMove = (currentState == State.Wander || currentState == State.Chase);
        
        // 1. Predictive Jump: Jump before hitting the ledge
        if (cc.isGrounded && gravityVelocity <= 0f && isTryingToMove && flatVel.sqrMagnitude > 0.1f)
        {
            Vector3 moveDir = flatVel.normalized;
            // Raycast forward at knee height
            if (Physics.Raycast(transform.position + Vector3.up * 0.3f, moveDir, out RaycastHit lowHit, 1.5f))
            {
                if (lowHit.collider != cc && !lowHit.collider.isTrigger)
                {
                    // Found an obstacle ahead. Check if we can stand on top of it.
                    Vector3 topCheck = lowHit.point + moveDir * 0.2f + Vector3.up * 1.5f;
                    if (Physics.Raycast(topCheck, Vector3.down, out RaycastHit downHit, 1.5f))
                    {
                        float heightDiff = downHit.point.y - transform.position.y;
                        if (heightDiff > 0.2f && heightDiff < 1.45f)
                        {
                            // It's a jumpable ledge!
                            gravityVelocity = 8.5f;
                            gravityMove = Vector3.up * gravityVelocity * Time.deltaTime; // Recalculate instantly!
                            if (animator != null) animator.SetTrigger("Jump");
                        }
                    }
                }
            }
        }

        // 2. Stuck Timer: Fast fallback if they hit a wall
        float expectedSpeedSqr = (currentState == State.Chase) ? 15f : 1.5f;
        if (flatVel.sqrMagnitude < expectedSpeedSqr && isTryingToMove)
            stuckTimer += Time.deltaTime;
        else
            stuckTimer = 0f;
            
        if (stuckTimer > 0.2f && (cc.isGrounded || actualVertVel > -0.1f))
        {
            gravityVelocity = 8.5f;
            gravityMove = Vector3.up * gravityVelocity * Time.deltaTime;
            if (animator != null) animator.SetTrigger("Jump");
            stuckTimer = -0.6f; // Cooldown
        }
        else if (stuckTimer > 1.5f)
        {
            stuckTimer = 0f;
            EnterState(State.Idle);
        }
        
        if (animator != null)
        {
            if (HasAnimParam(animator, "IsGrounded")) animator.SetBool("IsGrounded", cc.isGrounded || Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.4f));
            if (HasAnimParam(animator, "VerticalVelocity")) animator.SetFloat("VerticalVelocity", actualVertVel);
        }
        
        switch (currentState)
        {
            case State.Hit:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    EnterState(State.Chase);
                }
                cc.Move(gravityMove);
                break;

            case State.Idle:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0) EnterState(State.Wander);
                CheckAggro();
                cc.Move(gravityMove);
                if (animator != null) animator.SetFloat(speedHash, 0f, 0.15f, Time.deltaTime);
                break;

            case State.Wander:
                stateTimer -= Time.deltaTime;
                
                // If they are stuck on a wall for half a second, give up and idle
                if (stateTimer <= 0 || FlatDistance(transform.position, moveTarget) < 1.5f)
                {
                    EnterState(State.Idle);
                }
                else
                {
                    MoveTowards(moveTarget, walkSpeed, gravityMove);
                }
                CheckAggro();
                break;

            case State.Chase:
                if (player == null) { EnterState(State.Idle); break; }
                
                float distToPlayer = FlatDistance(transform.position, player.position);
                if (distToPlayer > aggroRange * 1.5f) // Lose aggro
                {
                    EnterState(State.Idle);
                }
                else if (distToPlayer <= attackRange)
                {
                    EnterState(State.Attack);
                }
                else
                {
                    MoveTowards(player.position, runSpeed, gravityMove);
                }
                break;

            case State.Attack:
                if (player == null) { EnterState(State.Idle); break; }
                
                // Face player
                Vector3 dir = player.position - transform.position;
                dir.y = 0;
                dir = dir.normalized;
                if (dir.sqrMagnitude > 0.01f)
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 10f * Time.deltaTime);
                
                cc.Move(gravityMove);

                if (animator != null) animator.SetFloat(speedHash, 0f);

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    if (animator != null) animator.SetTrigger("Attack");

                    Vector3 delta = player.position - transform.position; delta.y = 0; if (delta.magnitude <= attackRange + 1.0f)
                    {
                        PlayerHealth.Instance.TakeDamage(damage);
                    }
                    lastAttackTime = Time.time;
                    stateTimer = 0.5f;
                }
                
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0 && Time.time >= lastAttackTime + 0.5f)
                {
                    EnterState(State.Chase);
                }
                break;
        }
    }

    private void CheckAggro()
    {
        if (player == null) return;
        if (FlatDistance(transform.position, player.position) <= aggroRange)
        {
            EnterState(State.Chase);
        }
    }

    private void PlayAnim(string stateName)
    {
        if (animator != null)
        {
            if (stateName == "Death")
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.ResetTrigger("Hit");
                animator.ResetTrigger("Attack");
            }
            if (stateName == "Attack" || stateName == "Hit" || stateName == "Death")
            {
                animator.SetTrigger(stateName);
            }
        }
    }


    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0;
        b.y = 0;
        return Vector3.Distance(a, b);
    }

    private void EnterState(State newState)
    {
        currentState = newState;
        switch (newState)
        {
            case State.Idle:
                stateTimer = Random.Range(1.5f, 4.0f);
                if (animator != null) animator.SetFloat(speedHash, 0f);
                break;
            case State.Wander:
                stateTimer = 5f;
                Vector2 rnd = Random.insideUnitCircle * 5f;
                Vector3 tryTarget = transform.position + new Vector3(rnd.x, 0, rnd.y);
                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, (tryTarget - transform.position).normalized, 5f))
                {
                    moveTarget = tryTarget;
                }
                else
                {
                    moveTarget = transform.position; // Stay put if path is blocked
                    stateTimer = 1f; // Try again sooner
                }
                break;
            case State.Chase:
                if (animator != null) animator.Play("Locomotion");
                break;
            case State.Attack:
                lastAttackTime = Time.time - attackCooldown; stateTimer = 1f;
                break;
            case State.Hit:
                stateTimer = 0.4f; // Flinch duration
                if (animator != null) animator.SetFloat(speedHash, 0f);
                PlayAnim("Hit");
                break;
            case State.Dead:
                PlayAnim("Death");
                if (animator != null) animator.SetFloat(speedHash, 0f);
                StartCoroutine(DissolveRoutine());
                break;
        }
    }

    private void MoveTowards(Vector3 target, float speed, Vector3 gravityMove)
    {
        Vector3 dir = target - transform.position;
        dir.y = 0;
        dir = dir.normalized;
        
        if (dir.sqrMagnitude > 0.01f)
        {

            
            dir = dir.normalized;
            float angle = Vector3.SignedAngle(transform.forward, dir, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 8f * Time.deltaTime);

            cc.Move(gravityMove);
            cc.Move(dir * speed * Time.deltaTime);
            if (animator != null)
            {
                animator.SetFloat(speedHash, speed, 0.15f, Time.deltaTime);
                animator.SetFloat(turnHash, angle / 180f, 0.15f, Time.deltaTime);
            }
        }
        else
        {
            cc.Move(gravityMove);
            if (animator != null) animator.SetFloat(speedHash, 0f);
        }
    }

    public void TakeDamage(int amt, Transform attacker = null)
    {
        if (currentState == State.Dead) return;

        currentHealth -= amt;
        
        healthBarTimer = 6f;
        trailTimer = 0.5f;
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        if (healthFillRect != null) healthFillRect.anchorMax = new Vector2(Mathf.Clamp01(currentHealth / maxHealth), 1f);

        StartCoroutine(DamageFlashRoutine());

        if (animator != null && attacker != null)
        {
            Vector3 hitDir = transform.InverseTransformDirection((attacker.position - transform.position).normalized);
            animator.SetFloat(hitXHash, hitDir.x);
            animator.SetFloat(hitYHash, hitDir.z);
        }

        if (currentHealth <= 0)
        {
            EnterState(State.Dead);
        }
        else
        {
            EnterState(State.Hit); // Flinch
        }
    }

    private IEnumerator DamageFlashRoutine()
    {
        if (isFlashing) yield break;
        isFlashing = true;
        
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color");
        
        Material flashMat = new Material(unlit);
        if (flashMat.HasProperty("_BaseColor")) flashMat.SetColor("_BaseColor", Color.white);
        if (flashMat.HasProperty("_Color")) flashMat.SetColor("_Color", Color.white);

        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = new Material[r.materials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = flashMat;
            r.materials = mats;
        }
        
        yield return new WaitForSeconds(0.15f);
        
        foreach (Renderer r in renderers)
        {
            if (r != null && originalMatsCache.ContainsKey(r))
            {
                r.materials = originalMatsCache[r];
            }
        }
        isFlashing = false;
    }

    private IEnumerator DissolveRoutine()
    {
        yield return new WaitForSeconds(2.5f); // Wait for death animation to finish

        // Spawn colored flakes from the mesh
        GameObject flakesGo = new GameObject("DissolveFlakes");
        flakesGo.transform.SetParent(transform, false);
        flakesGo.transform.localPosition = Vector3.up * 1f;
        ParticleSystem ps = flakesGo.AddComponent<ParticleSystem>();
        ps.Stop();
        var main = ps.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1f, 2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = ps.emission;
        emission.rateOverTime = 80;
        
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vol.y = new ParticleSystem.MinMaxCurve(0.2f, 1.0f); // Float up gently
        vol.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var shape = ps.shape;
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = smr;
        }
        else
        {
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;
        }

        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
        
        ps.Play();

        // Swap to our Custom Dissolve Shader
        List<Material> dissolveMats = new List<Material>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Shader s = Shader.Find("Custom/URPLitDissolve");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
                Material m = new Material(s);
                if (mats[i].HasProperty("_MainTex")) m.SetTexture("_BaseMap", mats[i].GetTexture("_MainTex"));
                else if (mats[i].HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mats[i].GetTexture("_BaseMap"));
                
                if (mats[i].HasProperty("_Color")) m.SetColor("_BaseColor", mats[i].color);
                else if (mats[i].HasProperty("_BaseColor")) m.SetColor("_BaseColor", mats[i].GetColor("_BaseColor"));
                
                mats[i] = m;
                dissolveMats.Add(m);
            }
            r.materials = mats;
        }

        float t = 0;
        float dissolveDuration = 2.0f;
        while (t < dissolveDuration)
        {
            t += Time.deltaTime;
            float progress = t / dissolveDuration;
            foreach (Material m in dissolveMats)
            {
                if (m.HasProperty("_DissolveAmount")) m.SetFloat("_DissolveAmount", progress);
            }
            yield return null;
        }

        // Hide mesh
        cc.enabled = false;
        foreach (Renderer r in renderers) if (r != null) r.enabled = false;

        yield return new WaitForSeconds(respawnTime);

        // Respawn
        if (flakesGo != null) Destroy(flakesGo);
        
        transform.position = spawnPoint;
        currentHealth = maxHealth;
        trailHealth = maxHealth;
        
        foreach (Renderer r in renderers)
        {
            if (r != null && originalMatsCache.ContainsKey(r))
            {
                r.materials = originalMatsCache[r];
                r.enabled = true;
            }
        }
        cc.enabled = true;
        
        EnterState(State.Idle);
    }

    // Utility: safely check if an animator has a given parameter
    private static bool HasAnimParam(Animator anim, string name)
    {
        if (anim == null) return false;
        foreach (var p in anim.parameters)
            if (p.name == name) return true;
        return false;
    }

}















#pragma warning restore CS0414





