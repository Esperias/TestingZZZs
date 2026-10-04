using UnityEngine;

public class CaveEntrance : MonoBehaviour, IInteractable
{
    public static float EntranceX = 15f;
    public static float EntranceZ = 15f;

    public string Prompt => "";
    public float HoldTime => 4.0f;
    public float Range => 4.0f;
    public bool CanInteract => true;

    public float DistanceTo(Vector3 point) => Vector3.Distance(point, transform.position);

    public void Interact()
    {
        if (CaveTransition.Instance != null)
            CaveTransition.Instance.GoToCave();
    }


    private System.Collections.Generic.List<Vector3> stepBasePos = new System.Collections.Generic.List<Vector3>();

    private void Start()
    {
        transform.position += Vector3.up * 14.0f;

        SetupCollider();
        BuildVisuals();
        
        GameObject bbFront = new GameObject("BillboardFront");
        bbFront.transform.SetParent(transform);
        bbFront.transform.localPosition = new Vector3(0f, 1.5f, -1.5f); 
        bbFront.AddComponent<CaveBillboard>();

        GameObject bbBack = new GameObject("BillboardBack");
        bbBack.transform.SetParent(transform);
        bbBack.transform.localPosition = new Vector3(0f, 1.5f, 1.5f); 
        bbBack.AddComponent<CaveBillboard>();
    }

    private void BuildVisuals()
    {
        WorldGenerator wg = WorldGenerator.Instance;
        
        Material stoneMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        stoneMat.color = new Color(0.35f, 0.35f, 0.37f); 
        stoneMat.SetFloat("_Smoothness", 0.1f);

        // 1. FLOATING SKY PLATFORM (3 Tiers)
        GameObject tier1 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tier1.transform.SetParent(transform);
        tier1.transform.localPosition = new Vector3(0f, -1.0f, 0f); // Just 1m thick now
        tier1.transform.localScale = new Vector3(18.0f, 1.0f, 18.0f); 
        tier1.GetComponent<MeshRenderer>().material = stoneMat;
        Destroy(tier1.GetComponent<Collider>());
        tier1.AddComponent<MeshCollider>(); 

        GameObject tier2 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tier2.transform.SetParent(transform);
        tier2.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        tier2.transform.localScale = new Vector3(13.0f, 0.2f, 13.0f);
        tier2.GetComponent<MeshRenderer>().material = stoneMat;
        Destroy(tier2.GetComponent<Collider>());
        tier2.AddComponent<MeshCollider>();

        GameObject tier3 = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        tier3.transform.SetParent(transform);
        tier3.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        tier3.transform.localScale = new Vector3(7.0f, 0.2f, 7.0f);
        tier3.GetComponent<MeshRenderer>().material = stoneMat;
        Destroy(tier3.GetComponent<Collider>());
        tier3.AddComponent<MeshCollider>();

        // 1.5 SUPPORT PILLARS (4 Circular Pillars anchoring it to the ground)
        for(int i = 0; i < 4; i++) 
        {
            float angle = Mathf.PI / 2f * i + (Mathf.PI / 4f); // 45 degree corners
            float r = 7.0f; // Sits nicely under the 9m radius platform
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;
            
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pillar.transform.SetParent(transform);
            // Center is halfway between ground (-14) and platform bottom (-1.5)
            pillar.transform.localPosition = new Vector3(x, -7.5f, z);
            pillar.transform.localScale = new Vector3(2.5f, 7.5f, 2.5f); // 15m tall
            pillar.GetComponent<MeshRenderer>().material = stoneMat;
            Destroy(pillar.GetComponent<Collider>());
            pillar.AddComponent<CapsuleCollider>();
        }

        // 2. TRACTOR BEAM ELEVATOR
        GameObject beamObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        beamObj.transform.SetParent(transform);
        beamObj.transform.localPosition = new Vector3(0f, -7.5f, -12.5f); 
        beamObj.transform.localScale = new Vector3(4.5f, 8.5f, 4.5f); 
        
        Destroy(beamObj.GetComponent<Collider>());
        CapsuleCollider beamTrigger = beamObj.AddComponent<CapsuleCollider>();
        beamTrigger.isTrigger = true; 
        beamTrigger.height = 2.0f;
        beamTrigger.radius = 0.5f; 
        
        // Pristine, intense crystalline core shader
        Shader beamShader = Shader.Find("Particles/Additive") ?? Shader.Find("UI/Default");
        Material beamMat = new Material(beamShader);
        if (beamMat.HasProperty("_TintColor")) beamMat.SetColor("_TintColor", new Color(0.0f, 0.4f, 1.0f, 0.4f));
        else beamMat.color = new Color(0.0f, 0.4f, 1.0f, 0.4f);
        beamObj.GetComponent<MeshRenderer>().material = beamMat;
        beamObj.AddComponent<TractorBeam>();

        // Inner solid core beam to make it look like a pristine laser
        GameObject coreObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        coreObj.transform.SetParent(transform);
        coreObj.transform.localPosition = new Vector3(0f, -7.5f, -12.5f);
        coreObj.transform.localScale = new Vector3(1.2f, 8.5f, 1.2f); // Thin solid core
        Destroy(coreObj.GetComponent<Collider>());
        Material coreMat = new Material(beamShader);
        if (coreMat.HasProperty("_TintColor")) coreMat.SetColor("_TintColor", new Color(0.5f, 0.9f, 1.0f, 0.8f));
        else coreMat.color = new Color(0.5f, 0.9f, 1.0f, 0.8f);
        coreObj.GetComponent<MeshRenderer>().material = coreMat;

        // Base Magic Circle/Rings
        GameObject baseRing = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        baseRing.transform.SetParent(transform);
        baseRing.transform.localPosition = new Vector3(0f, -14.2f, -12.5f); // Right on the ground
        baseRing.transform.localScale = new Vector3(5.5f, 0.05f, 5.5f); // Flat glowing pad
        Destroy(baseRing.GetComponent<Collider>());
        baseRing.GetComponent<MeshRenderer>().material = coreMat;

        // Magical Swirling Particles
        GameObject psGo = new GameObject("BeamParticles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = new Vector3(0f, -7.5f, -12.5f); 

        ParticleSystem psBeam = psGo.AddComponent<ParticleSystem>();
        var mainB = psBeam.main;
        mainB.loop = true;
        mainB.startLifetime = 4.0f;
        mainB.startSpeed = new ParticleSystem.MinMaxCurve(6f, 15f); 
        mainB.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f); // Crisp sparks
        mainB.startColor = new Color(0.4f, 1f, 1f, 1f);
        mainB.simulationSpace = ParticleSystemSimulationSpace.World;
        mainB.maxParticles = 300;
        
        var emB = psBeam.emission; emB.rateOverTime = 120; // High density sparkling
        
        var shapeB = psBeam.shape;
        shapeB.shapeType = ParticleSystemShapeType.Cone; 
        shapeB.angle = 0f;
        shapeB.radius = 2.0f; 
        shapeB.length = 17f; 
        shapeB.rotation = new Vector3(90f, 0f, 0f); 

        var velB = psBeam.velocityOverLifetime;
        velB.enabled = true;
        
        // Unity requires all axes to have the same curve mode
        velB.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velB.y = new ParticleSystem.MinMaxCurve(8f, 16f);
        velB.z = new ParticleSystem.MinMaxCurve(0f, 0f);

        // Clean double helix orbital
        velB.orbitalX = new ParticleSystem.MinMaxCurve(0f, 0f);
        velB.orbitalY = new ParticleSystem.MinMaxCurve(4.0f, 6.0f);
        velB.orbitalZ = new ParticleSystem.MinMaxCurve(0f, 0f);

        Shader pShader = Shader.Find("Particles/Additive") ?? Shader.Find("UI/Default");
        Material pMat = new Material(pShader);
        if (pMat.HasProperty("_TintColor")) pMat.SetColor("_TintColor", new Color(0.2f, 1f, 1f, 1f));
        psGo.GetComponent<ParticleSystemRenderer>().material = pMat;
        psBeam.Play();

        // 3. OBELISKS
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.PI * 2f * i / 8f; 
            float r = 5.5f; 
            float x = Mathf.Cos(angle) * r;
            float z = Mathf.Sin(angle) * r;
            
            GameObject obelisk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obelisk.transform.SetParent(transform);
            obelisk.transform.localPosition = new Vector3(x, 3.5f, z);
            obelisk.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            obelisk.transform.localScale = new Vector3(0.8f, 6.0f, 0.8f);
            obelisk.GetComponent<MeshRenderer>().material = stoneMat;
            Destroy(obelisk.GetComponent<Collider>());
            obelisk.AddComponent<BoxCollider>();
        }

        // 4. PORTAL ARCHWAY
        float portalBaseY = 0.8f; 
        Material portalMat = MakePortalMaterial();
        SpawnPillar(new Vector3(-1.8f, portalBaseY + 2.0f, 0f), new Vector3(0.8f, 4.0f, 0.8f), portalMat);
        SpawnPillar(new Vector3( 1.8f, portalBaseY + 2.0f, 0f), new Vector3(0.8f, 4.0f, 0.8f), portalMat);
        SpawnPillar(new Vector3(0f,    portalBaseY + 4.2f, 0f), new Vector3(4.4f, 0.8f, 0.8f), portalMat);

        GameObject fill = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(fill.GetComponent<Collider>());
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = new Vector3(0f, portalBaseY + 2.0f, 0.05f);
        fill.transform.localScale    = new Vector3(3.2f, 4f, 1f);

        Material glow = new Material(Shader.Find("Unlit/Color"));
        if (glow.HasProperty("_Color"))     glow.SetColor("_Color",     new Color(0.2f, 0.8f, 1f, 0.6f));
        if (glow.HasProperty("_BaseColor")) glow.SetColor("_BaseColor", new Color(0.2f, 0.8f, 1f, 0.6f));
        fill.GetComponent<MeshRenderer>().material = glow;

        GameObject fillBack = Instantiate(fill, transform);
        fillBack.transform.localRotation = Quaternion.Euler(0, 180f, 0);

        BuildParticles(portalBaseY + 2.0f);
    }
    private void SpawnPillar(Vector3 localPos, Vector3 localScale, Material mat)
    {
        GameObject g = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(transform, false);
        g.transform.localPosition = localPos;
        g.transform.localScale    = localScale;
        g.GetComponent<MeshRenderer>().material = mat;
        g.AddComponent<BoxCollider>();
    }

    private Material MakePortalMaterial()
    {
        Shader s = Shader.Find("Custom/WalkingToon") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        Material m = new Material(s);
        Color col = new Color(0.2f, 0.25f, 0.3f); // Obsidian/Dark frame
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
        if (m.HasProperty("_Color"))     m.SetColor("_Color",     col);
        return m;
    }

    private void BuildParticles(float heightY)
    {
        GameObject psGo = new GameObject("EntranceParticles");
        psGo.transform.SetParent(transform, false);
        psGo.transform.localPosition = new Vector3(0f, heightY, 0f);

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = ps.main;
        main.loop            = true;
        main.startLifetime   = 1.5f;
        main.startSpeed      = 1f;
        main.startSize       = 0.15f;
        main.startColor      = new Color(0.2f, 0.8f, 1f, 0.9f); // Blue mystical particles
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 50;

        var em = ps.emission; em.rateOverTime = 25;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Rectangle;
        shape.scale     = new Vector3(3f, 4f, 0.5f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.x       = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y       = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
        vel.z       = new ParticleSystem.MinMaxCurve(0f, 0f);

        var sizeCurve = ps.sizeOverLifetime;
        sizeCurve.enabled = true;
        sizeCurve.size    = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        Shader unlitShader = Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Transparent");
        Material mat = new Material(unlitShader ?? Shader.Find("Standard"));
        psGo.GetComponent<ParticleSystemRenderer>().material = mat;

        ps.Play();
    }
    private void SpawnPrefab(GameObject[] prefabs, Vector3 localPos, Vector3 localScale, Quaternion rot)
    {
        if (prefabs == null || prefabs.Length == 0) return;
        GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
        GameObject g = Instantiate(prefab, transform);
        
        Vector3 worldPos = transform.TransformPoint(localPos);
        if (WorldGenerator.Instance != null)
        {
            float groundY = WorldGenerator.Instance.GetHeightAt(worldPos.x, worldPos.z);
            // If the object is intentionally underground (y < 0), use the true Y. 
            // If it's a surface rock (y > 0), place it on the terrain.
            if (localPos.y > 0)
                worldPos.y = groundY + localPos.y;
            else
                worldPos.y = groundY + localPos.y; // Keep vertical offset relative to terrain!
            g.transform.position = worldPos;
        }
        else
        {
            g.transform.localPosition = localPos;
        }

        g.transform.localRotation = rot;
        g.transform.localScale = localScale;
    }

    private void SetupCollider()
    {
        BoxCollider bc = GetComponent<BoxCollider>();
        if (bc == null) bc = gameObject.AddComponent<BoxCollider>();
        bc.isTrigger = true;
        bc.center    = new Vector3(0f, 1.0f, 0f);
        bc.size      = new Vector3(4.0f, 2.0f, 4.0f);
    }
}






























public class TractorBeam : MonoBehaviour
{
    private void OnTriggerStay(Collider other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            // Force vertical velocity
            pc.verticalVelocity = 15f; 
            
            // Explicitly force physical movement immediately to bypass any ground snapping
            var cc = other.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.Move(Vector3.up * 10f * Time.deltaTime);
            }
        }
    }
}

public class CaveBillboard : MonoBehaviour
{
    public void HideBillboard()
    {
        gameObject.SetActive(false);
    }

    public void ShowBillboard()
    {
        gameObject.SetActive(true);
    }

    private Transform player;
    private Canvas canvas;
    private UnityEngine.UI.Image fillCircle;
    
    private Sprite MakeCircleSprite()
    {
        int res = 128; // Higher res
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Color[] colors = new Color[res * res];
        float center = res / 2f;
        float outerRadius = res / 2f - 2f;
        float innerRadius = outerRadius - 12f; // Creates a clean ring instead of a filled circle!
        
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                colors[y * res + x] = (dist <= outerRadius && dist >= innerRadius) ? Color.white : Color.clear;
            }
        }
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, res, res), new Vector2(0.5f, 0.5f));
    }

    private void Start()
    {
        player = GameObject.Find("Player")?.transform;
        
        GameObject root = new GameObject("UI_Root");
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.zero;
        root.transform.localScale = Vector3.one * 0.005f; // Much smaller and sleeker scale
        
        canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Sleek horizontal background
        GameObject bgObj = new GameObject("BG");
        bgObj.transform.SetParent(root.transform, false);
        var bg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bg.color = new Color(0.08f, 0.1f, 0.12f, 0.85f); // Very dark, professional
        bg.rectTransform.sizeDelta = new Vector2(340, 80); // Thin and wide

        // Progress Ring (now an actual ring) placed on the left side
        GameObject fillObj = new GameObject("FillRing");
        fillObj.transform.SetParent(bgObj.transform, false);
        fillCircle = fillObj.AddComponent<UnityEngine.UI.Image>();
        fillCircle.color = new Color(0.0f, 0.7f, 1.0f, 0.9f);
        fillCircle.rectTransform.sizeDelta = new Vector2(50, 50); // Small, elegant ring
        fillCircle.rectTransform.anchoredPosition = new Vector2(-130f, 0f); // Left aligned
        fillCircle.sprite = MakeCircleSprite();
        fillCircle.type = UnityEngine.UI.Image.Type.Filled;
        fillCircle.fillMethod = UnityEngine.UI.Image.FillMethod.Radial360;
        fillCircle.fillAmount = 0f;

        // Single line sleek text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(bgObj.transform, false);
        var textLabel = textObj.AddComponent<UnityEngine.UI.Text>();
        textLabel.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        textLabel.text = "HOLD E TO ENTER";
        textLabel.alignment = TextAnchor.MiddleLeft;
        textLabel.fontSize = 24;
        textLabel.fontStyle = FontStyle.Bold;
        textLabel.color = new Color(0.9f, 0.95f, 1.0f, 1.0f);
        textLabel.rectTransform.sizeDelta = new Vector2(240, 80);
        textLabel.rectTransform.anchoredPosition = new Vector2(30f, 0f); // Shifted right
    }

    private void Update()
    {
        if (player == null) return;
        
        float dist = Vector3.Distance(transform.position, player.position);
        bool inRange = dist < 4.5f;
        canvas.enabled = inRange;
        
        if (inRange)
        {
            Camera cam = Camera.main;
            if (cam != null)
                canvas.transform.rotation = Quaternion.LookRotation(canvas.transform.position - cam.transform.position);

            if (Keybinds.IsHeld(GameAction.Interact))
                fillCircle.fillAmount += Time.deltaTime / 4.0f;
            else
                fillCircle.fillAmount = 0f;
        }
    }
}







