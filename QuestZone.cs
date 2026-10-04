using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Place this on a trigger collider in the world to complete Explore quests.
/// Set Zone Id in the Inspector to match one of the explore zone ids, e.g. "the_old_mill".
/// The available zone ids are: the_old_mill, the_hilltop, the_mossy_hollow,
/// the_stone_circle, the_lakeshore, the_ancient_tree.
/// </summary>
public class QuestZone : MonoBehaviour
{
    private static readonly Dictionary<string, QuestZone> Registry = new Dictionary<string, QuestZone>();
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticsForFastPlayMode() { Registry.Clear(); }

    public static List<QuestZone> GetAllZones() { return new List<QuestZone>(Registry.Values); }
    public static Transform FindById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        return Registry.TryGetValue(id, out QuestZone zone) ? zone.transform : null;
    }

    [SerializeField] private string zoneId;
    [Tooltip("Human-readable name shown in the notification, e.g. \"the Old Mill\".")]
    [SerializeField] private string zoneName = "Unknown Location";

    public string ZoneId => zoneId;

    private bool triggered;

    public void Setup(string id, string name) { zoneId = id; zoneName = name; if (!string.IsNullOrEmpty(zoneId)) Registry[zoneId] = this; }
    private void OnEnable()
    {
        if (!string.IsNullOrEmpty(zoneId)) Registry[zoneId] = this;
    }

    private void OnDisable()
    {
        if (!string.IsNullOrEmpty(zoneId) && Registry.TryGetValue(zoneId, out QuestZone stored) && stored == this)
            Registry.Remove(zoneId);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggered) return;
        if (other.GetComponent<PlayerController>() == null) return;
        triggered = true;

        if (QuestLog.Instance == null) return;
        QuestLog.Instance.OnZoneReached(zoneId, zoneName);
    }

    private void Start()
    {
        // Build a visual beacon so the player can actually see the zone!
        GameObject beacon = new GameObject("Zone Beacon");
        beacon.transform.SetParent(transform, false);
        beacon.transform.localPosition = Vector3.zero;

        // 1. Emanating Particles
        ParticleSystem pSys = beacon.AddComponent<ParticleSystem>();
        pSys.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        
        var main = pSys.main;
        main.duration = 2f;
        main.loop = true;
        main.startLifetime = 3f;
        main.startSpeed = 1.5f;
        main.startSize = 0.25f;
        main.startColor = new Color(0.4f, 0.8f, 1f, 0.6f); // Soft magical blue/cyan
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 50;

        var em = pSys.emission;
        em.rateOverTime = 15;

        var shape = pSys.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2.0f;
        shape.rotation = new Vector3(-90f, 0f, 0f); // flat on ground

        var vel = pSys.velocityOverLifetime;
        vel.enabled = true;
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.5f, 2.0f); // Float upward
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        var size = pSys.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, 0f); // shrink as they rise

        var color = pSys.colorOverLifetime;
        color.enabled = true;
        Gradient grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) }
        );
        color.color = grad;

        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Unlit/Transparent");
        Material mat = new Material(shader != null ? shader : Shader.Find("Standard"));

        ParticleSystemRenderer psr = beacon.GetComponent<ParticleSystemRenderer>();
        psr.material = mat;
        psr.renderMode = ParticleSystemRenderMode.Billboard;
        
        pSys.Play();

        // 2. Glowing Pillar
        GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(pillar.GetComponent<Collider>());
        pillar.transform.SetParent(beacon.transform, false);
        pillar.transform.localScale = new Vector3(3f, 10f, 3f);
        pillar.transform.localPosition = new Vector3(0f, 10f, 0f);
        
        MeshRenderer pmr = pillar.GetComponent<MeshRenderer>();
        pmr.material = new Material(mat);
        pmr.material.color = new Color(0.2f, 0.7f, 1f, 0.1f); // extremely faint blue glow

        // 3. Floating Billboard Icon
        GameObject icon = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(icon.GetComponent<Collider>());
        icon.transform.SetParent(beacon.transform, false);
        icon.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        icon.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
        
        MeshRenderer iconMr = icon.GetComponent<MeshRenderer>();
        iconMr.material = new Material(mat);
        // Make it a bright, solid glowing color
        iconMr.material.color = new Color(0.4f, 0.9f, 1f, 0.9f);
        
        // Add helper to always face camera
        icon.AddComponent<BillboardFaceCamera>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.3f);
        Collider col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        else             Gizmos.DrawWireSphere(transform.position, 5f);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.8f, 0.1f, 0.8f);
        Collider col = GetComponent<Collider>();
        if (col != null) Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
        else             Gizmos.DrawWireSphere(transform.position, 5f);

#if UNITY_EDITOR
        UnityEditor.Handles.color = new Color(1f, 0.8f, 0.1f, 1f);
        UnityEditor.Handles.Label(transform.position + Vector3.up * 1.5f,
            $"Zone: {(string.IsNullOrEmpty(zoneId) ? "(no id set)" : zoneId)}");
#endif
    }
}

public class BillboardFaceCamera : MonoBehaviour
{
    private Camera cam;
    private Vector3 startPos;
    
    void Start() 
    { 
        cam = Camera.main; 
        startPos = transform.localPosition;
    }
    
    void Update() 
    { 
        if (cam != null) 
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position); 
            
        // Bob up and down gently
        transform.localPosition = startPos + Vector3.up * (Mathf.Sin(Time.time * 2f) * 0.2f);
    }
}






