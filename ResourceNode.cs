using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ResourceNode : MonoBehaviour, IInteractable
{
    [SerializeField] private string resourceName = "Wood";
    public string ResourceName => resourceName;
    [SerializeField] private int yieldAmount = 1;
    [SerializeField] private int maxHarvests = 3;
    private int currentHarvests;
    private Vector3 baseScale;
    private Vector3 currentBaselineScale;
    private List<GameObject> damageMarks = new List<GameObject>();
    private bool berriesHarvested = false;
    public Color nodeColor = Color.gray;

    // --- Pooling support (set by ResourceGenerator) ---
    [System.NonSerialized] public GameObject sourcePrefab;
    [System.NonSerialized] public List<Transform> spawnedBerries;
    private bool started;

    public string Prompt => "Harvest " + resourceName;
    public float HoldTime => 0f;
    public float Range => 3.0f;
    public bool CanInteract => resourceName == "Berries" && !berriesHarvested && currentHarvests < maxHarvests;
    
    public float DistanceTo(Vector3 point) => Vector3.Distance(point, transform.position);

    
    public void Initialize(string name, int yield, int max)
    {
        resourceName = name;
        yieldAmount = yield;
        maxHarvests = max;
    }

    private void Awake()
    {
        // Disable static batching on this node so the shatter particle system
        // can use the mesh for emission (static-batched meshes can't be used).
        foreach (var r in GetComponentsInChildren<Renderer>())
            r.gameObject.isStatic = false;
    }

    private void Start()
    {
        // Attach the world-space billboard label only for berries
        if (resourceName == "Berries" && GetComponent<ResourceBillboard>() == null)
            gameObject.AddComponent<ResourceBillboard>();

        baseScale = transform.localScale;
        currentBaselineScale = baseScale;
        if (resourceName == "Wood") nodeColor = new Color(0.45f, 0.3f, 0.15f);
        else if (resourceName == "Stone") nodeColor = new Color(0.6f, 0.6f, 0.6f);
        else if (resourceName == "Berries") nodeColor = new Color(0.2f, 0.6f, 0.2f);
        else if (resourceName == "Iron") nodeColor = new Color(0.6f, 0.4f, 0.2f);
        else if (resourceName == "Crystal") nodeColor = new Color(0.3f, 0.6f, 0.9f);
        else if (nodeColor == Color.gray) nodeColor = Color.white;
        // Strip out ANY existing blocky colliders that came with the prefab
        foreach (Collider col in GetComponentsInChildren<Collider>())
        {
            Destroy(col);
        }

        // Add our own perfect hitboxes
        MeshFilter meshFilter = GetComponentInChildren<MeshFilter>();
        GameObject targetObj = meshFilter != null ? meshFilter.gameObject : gameObject;

        if (resourceName == "Stone")
        {
            // Rocks get MeshColliders so you can walk perfectly along their uneven surface
            MeshCollider mc = targetObj.AddComponent<MeshCollider>();
            if (meshFilter != null) mc.sharedMesh = meshFilter.sharedMesh;
        }
        else if (resourceName == "Wood")
        {
            // Trees get slim CapsuleColliders so you slide off them and don't collide with leaves
            CapsuleCollider cap = targetObj.AddComponent<CapsuleCollider>();
            cap.radius = 0.3f;
            cap.height = 10f;
            cap.center = new Vector3(0, 5f, 0);
        }
        else
        {
            // Bushes get small spherical capsules that are TRIGGERS (no collision, but you can interact)
            CapsuleCollider cap = targetObj.AddComponent<CapsuleCollider>();
            cap.isTrigger = true;
            cap.radius = 0.5f;
            cap.height = 1.5f;
            cap.center = new Vector3(0, 0.75f, 0);
        }
        
        started = true;
        InteractionManager.Register(this);
    }

    private void OnEnable()
    {
        // Nodes are pooled: when one comes back out of the pool it must be interactable again.
        if (started) InteractionManager.Register(this);
    }

    private void OnDisable()
    {
        // Also covers being parked in the pool, so a hidden node can never be interacted with.
        InteractionManager.Unregister(this);
    }

    private void OnDestroy()
    {
        InteractionManager.Unregister(this);
    }

    /// <summary>Puts the node back into a pristine, unharvested state so it can be reused somewhere else.</summary>
    public void ResetForPool()
    {
        StopAllCoroutines();

        foreach (GameObject mark in damageMarks)
        {
            if (mark != null) Destroy(mark);
        }
        damageMarks.Clear();

        currentHarvests = 0;
        berriesHarvested = false;

        // includeInactive = true: the whole chunk this node lived in is already switched off
        LODGroup lod = GetComponentInChildren<LODGroup>(true);
        if (lod != null) lod.enabled = true;

        foreach (var r in GetComponentsInChildren<Renderer>(true))
        {
            r.enabled = true;
            r.SetPropertyBlock(null); // clears a hit-flash that was interrupted
        }
        foreach (var c in GetComponentsInChildren<Collider>(true))
        {
            c.enabled = true;
        }
        foreach (Transform child in transform)
        {
            if (child.name.Contains("Sphere") || child.name.Contains("berry") || child.name.Contains("Berry"))
                child.gameObject.SetActive(true);
        }
    }

    /// <summary>Called when a pooled node is placed back into the world.</summary>
    public void Rebind(string name, int yield, int max)
    {
        Initialize(name, yield, max);
        baseScale = transform.localScale;
        currentBaselineScale = baseScale;
    }

    public void Hit()
    {
        if (currentHarvests >= maxHarvests) return;

        if (ToolManager.Instance != null)
        {
            ToolManager.ToolType currentTool = ToolManager.Instance.CurrentTool;
            
            if (resourceName == "Wood" && currentTool != ToolManager.ToolType.Axe)
            {
                if (currentHarvests == 0) Inventory.Instance.Notify("Punching trees... an Axe (press 2) yields double!");
            }
            else if (resourceName == "Stone" && currentTool != ToolManager.ToolType.Pickaxe)
            {
                if (currentHarvests == 0) Inventory.Instance.Notify("Punching rocks... a Pickaxe (press 3) yields double!");
            }
        }

        currentHarvests++;

        currentBaselineScale = baseScale;

        AddCrack();
        SpawnHitDust();
        StartCoroutine(FlashRoutine());
        StartCoroutine(WobbleRoutine());

        if (currentHarvests >= maxHarvests)
        {
            string dropRes = resourceName;
            int dropYield = yieldAmount * maxHarvests;

            if (resourceName == "Berries")
            {
                dropRes = "Sprouts";
                dropYield = yieldAmount;
            }
            else 
            {
                if (ToolManager.Instance != null)
                {
                    ToolManager.ToolType currentTool = ToolManager.Instance.CurrentTool;
                    if ((resourceName == "Wood" && currentTool == ToolManager.ToolType.Axe) ||
                        (resourceName == "Stone" && currentTool == ToolManager.ToolType.Pickaxe))
                    {
                        dropYield *= 2;
                    }
                }
            }

            Inventory.Instance.DropItem(dropRes, dropYield, transform.position + Vector3.up * 1f);
            Inventory.Instance.AddXP(5 * maxHarvests);

            CreateShatterEffect();
            SetNodeActive(false);
            RecordHarvest(30f);
            StartCoroutine(RespawnRoutine(30f));
        }
    }

    public void Interact()
    {
        if (resourceName == "Berries" && !berriesHarvested)
        {
            Inventory.Instance.DropItem("Berries", yieldAmount, transform.position);
            Inventory.Instance.AddXP(5);
            berriesHarvested = true;
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Sphere") || child.name.Contains("berry") || child.name.Contains("Berry"))
                {
                    child.gameObject.SetActive(false);
                }
            }
            RecordHarvest(30f);
            StartCoroutine(RespawnRoutine(30f));
        }
    }


    private void AddCrack()
    {
        Renderer firstMesh = GetComponentInChildren<Renderer>();
        if (firstMesh == null) return;
        Transform target = firstMesh.transform;

        GameObject crack = new GameObject("Crack");
        crack.transform.SetParent(target, false);
        
        LineRenderer lr = crack.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.startWidth = 0.05f;
        lr.endWidth = 0.01f; // Tapers off sharply like a real fracture
        
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader == null) spriteShader = Shader.Find("Unlit/Color");
        Material mat = new Material(spriteShader);
        
        mat.color = nodeColor * 0.2f; // Very dark version of the object's color
        lr.material = mat;
        
        int points = Random.Range(3, 6);
        lr.positionCount = points;
        
        Vector3 currentPos = Random.onUnitSphere;
        if (target.localScale.y > target.localScale.x) currentPos.y *= 0.3f;
        currentPos.Normalize();
        currentPos *= 0.51f; // Float just barely outside the 0.5 primitive bounds to prevent z-fighting
        
        Vector3 dir = Vector3.Cross(currentPos, Random.onUnitSphere).normalized;
        
        for (int i = 0; i < points; i++)
        {
            lr.SetPosition(i, currentPos);
            currentPos += dir * Random.Range(0.1f, 0.4f);
            dir = Quaternion.AngleAxis(Random.Range(-50f, 50f), currentPos) * dir; // Jagged zig-zag
            currentPos = currentPos.normalized * 0.51f;
        }
        
        damageMarks.Add(crack);
    }

    private IEnumerator FlashRoutine()
    {
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        MaterialPropertyBlock block = new MaterialPropertyBlock();
        
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
            {
                rends[i].GetPropertyBlock(block);
                block.SetColor("_BaseColor", Color.white);
                block.SetColor("_Color", Color.white);
                rends[i].SetPropertyBlock(block);
            }
        }
        
        yield return new WaitForSeconds(0.08f);
        
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null)
            {
                rends[i].GetPropertyBlock(block);
                block.Clear();
                rends[i].SetPropertyBlock(block);
            }
        }
    }

    private void SpawnHitDust()
    {
        GameObject dust = new GameObject("HitDust");
        Renderer firstMesh = GetComponentInChildren<Renderer>();
        if (firstMesh != null) dust.transform.position = firstMesh.bounds.center;
        else dust.transform.position = transform.position + Vector3.up * (currentBaselineScale.y * 0.5f);
        
        ParticleSystem pSys = dust.AddComponent<ParticleSystem>();
        pSys.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var main = pSys.main;
        main.duration = 1f;
        main.loop = false;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
        main.gravityModifier = 1.5f;
        
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        
        var emission = pSys.emission;
        emission.rateOverTime = 0;
        emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });
        
        var shape = pSys.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.5f;
        
        var colCurve = pSys.colorOverLifetime;
        colCurve.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
        );
        colCurve.color = new ParticleSystem.MinMaxGradient(grad);
        
        ParticleSystemRenderer psr = dust.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Mesh;
        GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        psr.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tempCube);
        
        if (cachedDissolveMat != null)
        {
            Material dustMat = new Material(cachedDissolveMat);
            dustMat.color = nodeColor;
            if (dustMat.HasProperty("_BaseColor")) dustMat.SetColor("_BaseColor", nodeColor);
            if (dustMat.HasProperty("_Color")) dustMat.SetColor("_Color", nodeColor);
            psr.material = dustMat;
        }
        else
        {
            Renderer r = GetComponentInChildren<Renderer>();
            if (r != null) psr.material = r.material;
        }
        
        pSys.Play();
        Destroy(dust, 1.5f);
    }

    private static Material cachedDissolveMat;

    private void CreateShatterEffect()
    {
        Renderer[] allRends = GetComponentsInChildren<Renderer>();
        
        foreach (Renderer partRend in allRends)
        {
            if (partRend == null) continue;

            // Only use MeshRenderers with an actual mesh that has geometry
            MeshRenderer sourceMesh = partRend as MeshRenderer;
            if (sourceMesh == null) continue;
            MeshFilter mf = sourceMesh.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || mf.sharedMesh.vertexCount == 0) continue;
            
            Color partColor = nodeColor;
            
            GameObject effectGo = new GameObject("ShatterEffect_Part");
            effectGo.transform.position = partRend.transform.position;
            effectGo.transform.rotation = partRend.transform.rotation; 
            effectGo.transform.localScale = partRend.transform.localScale;
            
            ParticleSystem pSys = effectGo.AddComponent<ParticleSystem>();
            pSys.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            var main = pSys.main;
            main.duration = 3f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1f, 3f); 
            main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
            main.gravityModifier = 0.6f; // Lower gravity so they arc gracefully instead of slamming down
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            
            main.startRotation3D = true;
            main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);

            var vol = pSys.velocityOverLifetime;
            vol.enabled = true;
            // Push pieces outward from the center forcefully to create an arc
            vol.radial = new ParticleSystem.MinMaxCurve(2f, 6f);
            
            var emission = pSys.emission;
            emission.rateOverTime = 0;
            // Emit 30 pieces PER MESH part! (E.g. A tree with 4 parts will drop 120 pieces total!)
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 30) });
            
            var shape = pSys.shape;
            shape.shapeType = ParticleSystemShapeType.MeshRenderer;
            shape.meshRenderer = sourceMesh;
            shape.meshShapeType = ParticleSystemMeshShapeType.Triangle;
            
            var colCurve = pSys.colorOverLifetime;
            colCurve.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0.5f), new GradientAlphaKey(0f, 1f) }
            );
            colCurve.color = new ParticleSystem.MinMaxGradient(grad);
            
            var size = pSys.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, 0f);
            
            var collision = pSys.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.bounce = 0.3f;
            collision.dampen = 0.6f;
            
            ParticleSystemRenderer psr = effectGo.GetComponent<ParticleSystemRenderer>();
            psr.renderMode = ParticleSystemRenderMode.Mesh;
            
            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            psr.mesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);
            
            if (cachedDissolveMat == null)
            {
                Shader pShader = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
                if (pShader == null) pShader = Shader.Find("Particles/Standard Unlit");
                
                cachedDissolveMat = new Material(pShader);
                cachedDissolveMat.SetFloat("_Surface", 1); 
                cachedDissolveMat.SetFloat("_Blend", 0);   
                cachedDissolveMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                cachedDissolveMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                cachedDissolveMat.SetInt("_ZWrite", 0);
                cachedDissolveMat.EnableKeyword("_ALPHABLEND_ON");
                cachedDissolveMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                cachedDissolveMat.renderQueue = 3000;
            }
            
            Material dissolveMat = new Material(cachedDissolveMat);
            dissolveMat.color = partColor;
            if (dissolveMat.HasProperty("_BaseColor")) dissolveMat.SetColor("_BaseColor", partColor);
            if (dissolveMat.HasProperty("_Color")) dissolveMat.SetColor("_Color", partColor);
                
            psr.material = dissolveMat;
            
            pSys.Play();
            Destroy(effectGo, 4f);
        }
    }

    private IEnumerator WobbleRoutine()
    {
        float duration = 0.2f;
        float elapsed = 0f;
        
        Renderer[] rends = GetComponentsInChildren<Renderer>();
        Quaternion[] startRots = new Quaternion[rends.Length];
        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null) startRots[i] = rends[i].transform.localRotation;
        }

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            // Quick back-and-forth rotational jitter (shaking)
            float angle = Mathf.Sin(t * Mathf.PI * 6f) * 8f; // shake 8 degrees
            
            for (int i = 0; i < rends.Length; i++)
            {
                if (rends[i] != null)
                {
                    rends[i].transform.localRotation = startRots[i] * Quaternion.Euler(0, 0, angle);
                }
            }
            
            elapsed += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < rends.Length; i++)
        {
            if (rends[i] != null) rends[i].transform.localRotation = startRots[i];
        }
    }

    public void ForceHarvestedState(float remainingTime)
    {
        if (resourceName == "Berries")
        {
            berriesHarvested = true;
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Sphere") || child.name.Contains("berry") || child.name.Contains("Berry"))
                {
                    child.gameObject.SetActive(false);
                }
            }
        }
        else
        {
            currentHarvests = maxHarvests;
            SetNodeActive(false);
        }
        StartCoroutine(RespawnRoutine(remainingTime));
    }

    private void RecordHarvest(float respawnTime)
    {
        if (WorldGenerator.Instance == null) return;
        Vector2Int chunkCoord = WorldGenerator.Instance.GetChunkCoord(transform.position);
        ChunkData data = WorldGenerator.Instance.GetOrCreateChunkData(chunkCoord);
        
        var list = resourceName == "Wood" ? data.harvestedTrees : (resourceName == "Stone" ? data.harvestedRocks : data.harvestedBushes);
        
        // Remove existing
        for (int i = list.Count - 1; i >= 0; i--) {
            if (Vector3.SqrMagnitude(list[i].pos - transform.position) < 0.1f) list.RemoveAt(i);
        }
        
        list.Add(new HarvestRecord(transform.position, Time.realtimeSinceStartup + respawnTime));
    }

    private void ClearHarvestRecord()
    {
        if (WorldGenerator.Instance == null) return;
        Vector2Int chunkCoord = WorldGenerator.Instance.GetChunkCoord(transform.position);
        ChunkData data = WorldGenerator.Instance.GetOrCreateChunkData(chunkCoord);
        var list = resourceName == "Wood" ? data.harvestedTrees : (resourceName == "Stone" ? data.harvestedRocks : data.harvestedBushes);
        for (int i = list.Count - 1; i >= 0; i--) {
            if (Vector3.SqrMagnitude(list[i].pos - transform.position) < 0.1f) list.RemoveAt(i);
        }
    }

    private IEnumerator RespawnRoutine(float delay = 30f)
    {
        yield return new WaitForSeconds(delay);
        currentHarvests = 0;
        berriesHarvested = false;
        
        foreach (GameObject mark in damageMarks)
        {
            if (mark != null) Destroy(mark);
        }

        damageMarks.Clear();
        
        currentBaselineScale = baseScale;
        transform.localScale = baseScale;
        
        SetNodeActive(true);
        
        if (resourceName == "Berries")
        {
            foreach (Transform child in transform)
            {
                if (child.name.Contains("Sphere") || child.name.Contains("berry") || child.name.Contains("Berry"))
                {
                    child.gameObject.SetActive(true);
                }
            }
        }
        
        ClearHarvestRecord();
    }

    private void SetNodeActive(bool active)
    {
        LODGroup lod = GetComponentInChildren<LODGroup>();
        if (lod != null) lod.enabled = active;

        foreach (var renderer in GetComponentsInChildren<Renderer>())
        {
            renderer.enabled = active;
        }
        foreach (var col in GetComponentsInChildren<Collider>())
        {
            col.enabled = active;
        }
    }
}











