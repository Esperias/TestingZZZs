using UnityEngine;

/// <summary>
/// Placed inside the cave (by CaveGenerator in Step 3 or manually) near the
/// start of the cave interior.  Triggers a fade-and-teleport back to the world.
///
/// Also builds its own glowing exit portal visual so it's always obvious.
/// </summary>
public class CaveExit : MonoBehaviour
{
    private void Start()
    {
        BuildVisuals();
        SetupCollider();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.GetComponent<PlayerController>() == null) return;
        if (CaveTransition.Instance != null)
            CaveTransition.Instance.ReturnFromCave();
    }

    // ── Visuals ────────────────────────────────────────────────────────────────

    private void BuildVisuals()
    {
        // Glowing arch (same structure as entrance but in a warm amber/gold colour)
        Material portalMat = MakePortalMaterial();

        SpawnPillar(new Vector3(-1.2f, 1.5f, 0f), new Vector3(0.5f, 3f, 0.5f), portalMat);
        SpawnPillar(new Vector3( 1.2f, 1.5f, 0f), new Vector3(0.5f, 3f, 0.5f), portalMat);
        SpawnPillar(new Vector3(0f,   3.1f, 0f),  new Vector3(3f,  0.5f, 0.5f), portalMat);

        // Glowing inner fill
        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fill.GetComponent<Collider>());
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = new Vector3(0f, 1.5f, 0.05f);
        fill.transform.localScale    = new Vector3(2.2f, 3f, 1f);

        Material glow = new Material(Shader.Find("Unlit/Color"));
        if (glow.HasProperty("_Color"))     glow.SetColor("_Color",     new Color(1f, 0.8f, 0.3f, 0.6f));
        if (glow.HasProperty("_BaseColor")) glow.SetColor("_BaseColor", new Color(1f, 0.8f, 0.3f, 0.6f));
        fill.GetComponent<MeshRenderer>().material = glow;

        // Particle sparkles
        BuildParticles();

        // Label
        BuildLabel();
    }

    private void SpawnPillar(Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(transform, false);
        g.transform.localPosition = localPos;
        g.transform.localScale    = localScale;
        g.GetComponent<MeshRenderer>().material = mat;
    }

    private Material MakePortalMaterial()
    {
        Shader s = Shader.Find("Custom/WalkingToon")
                ?? Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Standard");
        Material m = new Material(s);
        Color gold = new Color(0.8f, 0.65f, 0.2f);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", gold);
        if (m.HasProperty("_Color"))     m.SetColor("_Color",     gold);
        return m;
    }

    private void BuildParticles()
    {
        GameObject psGo = new GameObject("ExitParticles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = new Vector3(0f, 1.5f, 0f);

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = 1.5f;
        main.startSpeed      = 1f;
        main.startSize       = 0.12f;
        main.startColor      = new Color(1f, 0.85f, 0.3f, 0.9f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 40;

        var em = ps.emission; em.rateOverTime = 20;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale     = new Vector3(2f, 3f, 0.1f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x       = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sizeCurve = ps.sizeOverLifetime;
        sizeCurve.enabled = true;
        sizeCurve.size    = new ParticleSystem.MinMaxCurve(1f,
            AnimationCurve.Linear(0f, 1f, 1f, 0f));

        Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        Material mat = new Material(unlitShader ?? Shader.Find("Standard"));
        psGo.GetComponent<ParticleSystemRenderer>().material = mat;

        ps.Play();
    }

    private void BuildLabel()
    {
        GameObject canvasGo = new GameObject("ExitLabel");
        canvasGo.transform.SetParent(transform, false);
        canvasGo.transform.localPosition = new Vector3(0f, 4f, 0f);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.WorldSpace;
        canvas.worldCamera = Camera.main;
        canvasGo.transform.localScale = Vector3.one * 0.02f;

        UnityEngine.UI.Text txt = canvasGo.AddComponent<UnityEngine.UI.Text>();
        txt.text      = "Exit Cave";
        txt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        txt.fontSize  = 80;
        txt.alignment = TextAnchor.MiddleCenter;
        txt.color     = new Color(1f, 0.9f, 0.5f, 1f);

        RectTransform rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(300f, 60f);

        // canvasGo.AddComponent<FaceCameraAlways>();
    }

    private void SetupCollider()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center    = new Vector3(0f, 1.5f, 0f);
        bc.size      = new Vector3(2.5f, 3f, 1.5f);
    }
}





