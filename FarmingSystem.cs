using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// InventoryCatalogExtensions is defined in CraftingStation.cs

// FarmingSystem
// Press G near terrain to till the ground into a farm plot.
// Press E on a tilled plot to plant seeds (uses first seed found in inventory).
// Come back when it's ready and press E to harvest.
// Automatically added to Manager object by Inventory.cs Awake().
public class FarmingSystem : MonoBehaviour
{
    public static FarmingSystem Instance { get; private set; }

    private class CropDef
    {
        public string seedId, yieldId, yieldName;
        public Color  sproutColor, matureColor;
        public float  growTime;
        public int    minYield, maxYield;
    }

    private static readonly CropDef[] Crops =
    {
        new CropDef
        {
            seedId = "WheatSeed",  yieldId = "Wheat",  yieldName = "Wheat",
            sproutColor = new Color(0.3f, 0.65f, 0.2f),
            matureColor = new Color(1f, 0.85f, 0.2f),
            growTime = 60f, minYield = 2, maxYield = 4
        },
        new CropDef
        {
            seedId = "CarrotSeed", yieldId = "Carrot", yieldName = "Carrot",
            sproutColor = new Color(0.3f, 0.65f, 0.2f),
            matureColor = new Color(1f, 0.52f, 0.1f),
            growTime = 45f, minYield = 2, maxYield = 5
        },
    };

    private enum PlotStage { Tilled, Seeded, Sprouting, Ready }

    private class FarmPlot : IInteractable
    {
        public FarmingSystem owner;
        public GameObject    plotGo;
        public GameObject    cropVisual;
        public PlotStage     stage;
        public CropDef       crop;
        public float         timer;

        public string Prompt
        {
            get
            {
                switch (stage)
                {
                    case PlotStage.Tilled:
                        if (Inventory.Instance == null) return "";
                        foreach (var c in Crops)
                            if (Inventory.Instance.GetCount(c.seedId) > 0) return "plant seeds";
                        return "need seeds to plant";
                    case PlotStage.Seeded:    return "growing - recently planted";
                    case PlotStage.Sprouting: return "growing - sprouting nicely";
                    case PlotStage.Ready:     return "harvest " + crop.yieldName;
                    default: return "";
                }
            }
        }

        public float HoldTime => 0f;
        public float Range    => 2.5f;
        public bool  CanInteract
        {
            get
            {
                if (stage == PlotStage.Ready) return true;
                if (stage == PlotStage.Tilled && Inventory.Instance != null)
                    foreach (var c in Crops)
                        if (Inventory.Instance.GetCount(c.seedId) > 0) return true;
                return false;
            }
        }
        public float DistanceTo(Vector3 p) => Vector3.Distance(p, plotGo.transform.position);
        public void  Interact() => owner.OnInteract(this);
    }

    private readonly List<FarmPlot> plots = new List<FarmPlot>();
    private Transform player;
    private Material  tilledMat;

    // ------------------------------------------------------------------

    private void Awake() { Instance = this; }

    private void Start()
    {
        var pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        tilledMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        if (tilledMat.shader == null) tilledMat = new Material(Shader.Find("Standard"));
        if (tilledMat.HasProperty("_BaseColor"))  tilledMat.SetColor("_BaseColor",  new Color(0.30f, 0.19f, 0.08f));
        if (tilledMat.HasProperty("_Color"))      tilledMat.SetColor("_Color",      new Color(0.30f, 0.19f, 0.08f));
        if (tilledMat.HasProperty("_Smoothness")) tilledMat.SetFloat("_Smoothness", 0f);

        StartCoroutine(GiveStarterSeeds());
    }

    private IEnumerator GiveStarterSeeds()
    {
        yield return new WaitForSeconds(1.5f);
        if (Inventory.Instance != null)
        {
            Inventory.Instance.AddItem("WheatSeed",  5);
            Inventory.Instance.AddItem("CarrotSeed", 5);

            // Testing: start with an axe, pickaxe, and fishing rod (only if the player doesn't already own one)
            if (Inventory.Instance.GetCount("Axe") <= 0) Inventory.Instance.AddItem("Axe", 1);
            if (Inventory.Instance.GetCount("Pickaxe") <= 0) Inventory.Instance.AddItem("Pickaxe", 1);
            if (Inventory.Instance.GetCount("FishingRod") <= 0) Inventory.Instance.AddItem("FishingRod", 1);
            Inventory.Instance.Notify("You have seeds! Press G to till ground, then E to plant.");
        }
    }

    // ------------------------------------------------------------------

    private void Update()
    {
        if (player == null || Time.timeScale == 0f || Inventory.PanelOpen || NPCSpawner.IsTalking) return;

        if (Keybinds.WasPressed(GameAction.Till)) TryTill();

        foreach (FarmPlot plot in plots)
        {
            // Auto-water if raining
            if (GameClock.IsRaining)
            {
                // We can just tint the material slightly darker
                plot.plotGo.GetComponent<MeshRenderer>().material.color = new Color(0.25f, 0.15f, 0.05f); 
                plot.timer += Time.deltaTime * 0.5f; // Raining makes it grow slightly faster as a bonus
            }
            
            if (plot.stage != PlotStage.Seeded && plot.stage != PlotStage.Sprouting) continue;
            
            // Paused in Winter
            if (GameClock.CurrentSeason == Season.Winter) continue;
            
            plot.timer += Time.deltaTime;
            float half = plot.crop.growTime * 0.45f;
            if      (plot.stage == PlotStage.Seeded    && plot.timer >= half)               SetStage(plot, PlotStage.Sprouting);
            else if (plot.stage == PlotStage.Sprouting && plot.timer >= plot.crop.growTime)  SetStage(plot, PlotStage.Ready);
        }
    }

    // ------------------------------------------------------------------
    // Tilling
    // ------------------------------------------------------------------

    private void TryTill()
    {
        if (Camera.main == null) return;
        Ray ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        
        // Raycast up to 15 units from the camera
        if (!Physics.Raycast(ray, out RaycastHit hit, 15f)) return;
        
        // Only allow tilling terrain (MeshCollider)
        if (hit.collider.GetComponent<MeshCollider>() == null) return;
        
        // Ensure the spot is close enough to the player
        if (player != null && Vector3.Distance(player.position, hit.point) > 6f) return;

        foreach (var p in plots)
            if (Vector3.Distance(p.plotGo.transform.position, hit.point) < 1.8f) return; // no overlap

        BuildPlot(hit.point, hit.normal);
    }

    private void BuildPlot(Vector3 worldPos, Vector3 normal)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name  = "Farm Plot";
        go.layer = 0;
        Destroy(go.GetComponent<BoxCollider>());
        go.GetComponent<MeshRenderer>().material = tilledMat;
        go.transform.position   = worldPos + normal * 0.04f;
        go.transform.rotation   = Quaternion.FromToRotation(Vector3.up, normal);
        go.transform.localScale = new Vector3(1.6f, 0.07f, 1.6f);

        // Furrow rows for tilled look
        Material furrowMat = new Material(tilledMat);
        if (furrowMat.HasProperty("_BaseColor")) furrowMat.SetColor("_BaseColor", new Color(0.20f, 0.11f, 0.04f));
        if (furrowMat.HasProperty("_Color"))     furrowMat.SetColor("_Color",     new Color(0.20f, 0.11f, 0.04f));
        for (int i = 0; i < 4; i++)
        {
            GameObject furrow = GameObject.CreatePrimitive(PrimitiveType.Cube);
            furrow.transform.SetParent(go.transform, false);
            furrow.transform.localPosition = new Vector3(0f, 0.6f, -0.375f + i * 0.25f);
            furrow.transform.localScale    = new Vector3(0.95f, 0.2f, 0.055f);
            furrow.GetComponent<MeshRenderer>().material = furrowMat;
            Destroy(furrow.GetComponent<BoxCollider>());
        }

        var plot = new FarmPlot { owner = this, plotGo = go, stage = PlotStage.Tilled };
        plots.Add(plot);
        InteractionManager.Register(plot);
    }

    // ------------------------------------------------------------------
    // Interaction
    // ------------------------------------------------------------------

    private void OnInteract(FarmPlot plot)
    {
        if (plot.stage == PlotStage.Tilled)
        {
            CropDef chosen = null;
            foreach (var c in Crops)
                if (Inventory.Instance != null && Inventory.Instance.GetCount(c.seedId) > 0) { chosen = c; break; }
            if (chosen == null) return;

            Inventory.Instance.RemoveItem(chosen.seedId, 1);
            plot.crop  = chosen;
            plot.timer = 0f;
            SetStage(plot, PlotStage.Seeded);
            Inventory.Instance.Notify("Planted " + chosen.yieldName + "!");
        }
        else if (plot.stage == PlotStage.Ready)
        {
            int amount = Random.Range(plot.crop.minYield, plot.crop.maxYield + 1);
            if (Inventory.Instance != null) Inventory.Instance.AddItem(plot.crop.yieldId, amount);
            if (plot.cropVisual != null) { Destroy(plot.cropVisual); plot.cropVisual = null; }
            plot.crop  = null;
            plot.timer = 0f;
            SetStage(plot, PlotStage.Tilled);
        }
    }

    // ------------------------------------------------------------------
    // Crop visuals
    // ------------------------------------------------------------------

    private void SetStage(FarmPlot plot, PlotStage newStage)
    {
        plot.stage = newStage;
        if (plot.cropVisual != null) { Destroy(plot.cropVisual); plot.cropVisual = null; }
        if (newStage == PlotStage.Tilled) return;

        // World-space visual parented to nothing - plots are static so this is fine
        GameObject root = new GameObject("CropVisual");
        root.transform.position = plot.plotGo.transform.position + Vector3.up * 0.06f;
        root.transform.rotation = Quaternion.identity;
        plot.cropVisual = root;

        bool  isReady  = (newStage == PlotStage.Ready);
        float stalkH   = newStage == PlotStage.Seeded ? 0.06f : newStage == PlotStage.Sprouting ? 0.22f : 0.50f;
        Color stalkCol = (isReady && plot.crop != null) ? plot.crop.matureColor
                       : (plot.crop != null ? plot.crop.sproutColor : new Color(0.3f, 0.65f, 0.2f));
        int   stalkCnt = isReady ? 12 : 6;

        Material stalkMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Simple Lit"));
        if (stalkMat.shader == null) stalkMat = new Material(Shader.Find("Standard"));
        if (stalkMat.HasProperty("_BaseColor"))  stalkMat.SetColor("_BaseColor",  stalkCol);
        if (stalkMat.HasProperty("_Color"))      stalkMat.SetColor("_Color",      stalkCol);
        if (stalkMat.HasProperty("_Smoothness")) stalkMat.SetFloat("_Smoothness", 0f);

        for (int i = 0; i < stalkCnt; i++)
        {
            float a   = (i / (float)stalkCnt) * 360f * Mathf.Deg2Rad;
            float r   = Random.Range(0.05f, 0.28f);
            var   pos = new Vector3(Mathf.Cos(a) * r, stalkH * 0.5f, Mathf.Sin(a) * r);

            GameObject stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stalk.transform.SetParent(root.transform, false);
            stalk.transform.localPosition = pos;
            stalk.transform.localScale    = new Vector3(0.055f, stalkH * 0.5f, 0.055f);
            stalk.GetComponent<MeshRenderer>().material = stalkMat;
            Destroy(stalk.GetComponent<CapsuleCollider>());
        }

        // Ripe heads for fully grown crops
        if (isReady && plot.crop != null)
        {
            Material headMat = new Material(stalkMat);
            Color    headCol = plot.crop.matureColor;
            if (headMat.HasProperty("_BaseColor")) headMat.SetColor("_BaseColor", headCol);
            if (headMat.HasProperty("_Color"))     headMat.SetColor("_Color",     headCol);

            for (int i = 0; i < stalkCnt; i++)
            {
                float a   = (i / (float)stalkCnt) * 360f * Mathf.Deg2Rad;
                float r   = Random.Range(0.05f, 0.28f);
                var   pos = new Vector3(Mathf.Cos(a) * r, stalkH + 0.07f, Mathf.Sin(a) * r);

                GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.transform.SetParent(root.transform, false);
                head.transform.localPosition = pos;
                head.transform.localScale    = Vector3.one * 0.13f;
                head.GetComponent<MeshRenderer>().material = headMat;
                Destroy(head.GetComponent<SphereCollider>());
            }
        }
    }
}




