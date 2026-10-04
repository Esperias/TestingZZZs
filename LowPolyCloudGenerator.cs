using UnityEngine;

public class LowPolyCloudGenerator : MonoBehaviour
{
    [Header("Settings")]
    public int numberOfClouds = 25; 
    public float radiusOfSpawn = 1200f;  
    public float lowestAltitude = 150f;  
    public float highestAltitude = 350f; 
    
    [Header("Cloud Size & Fluidity")]
    public float smallestCloudScale = 30f; 
    public float largestCloudScale = 70f; 
    public int minPuffs = 15; 
    public int maxPuffs = 30;
    
    [Header("Movement")]
    public float windSpeed = 8f; 
    
    private Transform[] spawnedClouds;
    private Material cloudMaterial;

    void Start()
    {
        // To make it look FLUID and CONNECTED, we need to eliminate the harsh internal shadows 
        // that happen when spheres intersect. An unlit or flat shader makes them visually merge 
        // into one single, continuous, beautiful shape.
        cloudMaterial = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        
        if (cloudMaterial.HasProperty("_BaseColor")) cloudMaterial.SetColor("_BaseColor", Color.white);
        if (cloudMaterial.HasProperty("_Color")) cloudMaterial.SetColor("_Color", Color.white);
        
        GenerateClouds();
    }
    
    public void GenerateClouds()
    {
        if (spawnedClouds != null)
        {
            foreach (Transform t in spawnedClouds) { if (t != null) Destroy(t.gameObject); }
        }
        
        spawnedClouds = new Transform[numberOfClouds];
        
        Vector3 centerPos = transform.position;
        if (Camera.main != null) centerPos = Camera.main.transform.position;
        
        for (int i = 0; i < numberOfClouds; i++)
        {
            GameObject wrapper = new GameObject("Fluid Cloud " + i);
            wrapper.transform.SetParent(transform);
            
            Vector2 r = Random.insideUnitCircle * radiusOfSpawn;
            wrapper.transform.position = new Vector3(centerPos.x + r.x, Random.Range(lowestAltitude, highestAltitude), centerPos.z + r.y);
            
            float cloudScale = Random.Range(smallestCloudScale, largestCloudScale);
            
            BuildFluidCloud(wrapper.transform, cloudScale);
            
            wrapper.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            spawnedClouds[i] = wrapper.transform;
        }
    }
    
    void BuildFluidCloud(Transform parent, float baseScale)
    {
        int puffCount = Random.Range(minPuffs, maxPuffs);
        
        Vector3[] puffPositions = new Vector3[puffCount];
        float[] puffScales = new float[puffCount];
        
        // 1. The Core Puff
        puffPositions[0] = Vector3.zero;
        puffScales[0] = baseScale;
        SpawnSphere(parent, puffPositions[0], puffScales[0]);
        
        // 2. The Growth Algorithm (Guarantees zero disconnected pieces)
        for (int i = 1; i < puffCount; i++)
        {
            // Pick a random existing puff to grow out of
            int attachTo = Random.Range(0, i);
            
            // Pick a random direction, but force it to be mostly horizontal so the cloud spreads out, not up.
            Vector3 dir = Random.onUnitSphere;
            dir.y *= 0.2f; 
            dir.Normalize();
            
            // CRITICAL FLUIDITY FIX: 
            // We place the new puff DEEP inside the parent puff (only 45% of the radius away).
            // It is mathematically impossible for it to float or disconnect. It will deeply merge.
            float distanceOut = puffScales[attachTo] * 0.45f;
            
            puffPositions[i] = puffPositions[attachTo] + (dir * distanceOut);
            
            // New puffs get slightly smaller as they grow outward
            puffScales[i] = puffScales[attachTo] * Random.Range(0.7f, 0.95f);
            
            // Keep the bottom flat
            if (puffPositions[i].y < -0.1f * baseScale) 
            {
                puffPositions[i].y = -0.1f * baseScale;
            }
            
            SpawnSphere(parent, puffPositions[i], puffScales[i]);
        }
    }

    void SpawnSphere(Transform parent, Vector3 localPosition, float scale)
    {
        GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        puff.transform.SetParent(parent);
        
        Destroy(puff.GetComponent<SphereCollider>());
        
        AudioListener al = puff.GetComponent<AudioListener>();
        if (al != null) Destroy(al);
        
        puff.GetComponent<MeshRenderer>().material = cloudMaterial;
        
        puff.transform.localPosition = localPosition;
        
        // Squash slightly on Y to make it feel like an aerodynamic cloud
        puff.transform.localScale = new Vector3(scale, scale * 0.75f, scale);
    }

    void Update()
    {
        if (spawnedClouds == null || Camera.main == null) return;
        
        Vector3 camPos = Camera.main.transform.position;
        
        foreach (Transform c in spawnedClouds)
        {
            if (c == null) continue;
            
            c.position += Vector3.right * windSpeed * Time.deltaTime;
            
            Vector3 cloudFlat = new Vector3(c.position.x, 0, c.position.z);
            Vector3 camFlat = new Vector3(camPos.x, 0, camPos.z);
            
            if (Vector3.Distance(cloudFlat, camFlat) > radiusOfSpawn * 1.5f)
            {
                c.position = new Vector3(
                    camPos.x - (radiusOfSpawn * 1.2f), 
                    Random.Range(lowestAltitude, highestAltitude), 
                    camPos.z + Random.Range(-radiusOfSpawn, radiusOfSpawn)
                );
            }
        }
    }
}




