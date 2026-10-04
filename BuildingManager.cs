using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class BuildingManager : MonoBehaviour
{
    public static BuildingManager Instance { get; private set; }

    public enum BuildType { Campfire, Bed, Wall, Foundation, VillageTotem }

    private class BuildDef
    {
        public BuildType type;
        public string name;
        public int woodCost;
        public int stoneCost;
        public Vector3 size;
        public Color color;
    }

    private BuildDef[] buildDefs = {
        new BuildDef { type = BuildType.VillageTotem, name = "Village Totem", woodCost = 15, stoneCost = 15, size = new Vector3(1f, 3f, 1f), color = new Color(0.2f, 0.8f, 0.8f) },
        new BuildDef { type = BuildType.Campfire, name = "Campfire", woodCost = 5, stoneCost = 3, size = new Vector3(1f, 0.4f, 1f), color = new Color(0.9f, 0.4f, 0.1f) },
        new BuildDef { type = BuildType.Bed, name = "Bed", woodCost = 10, stoneCost = 0, size = new Vector3(1.2f, 0.4f, 2f), color = new Color(0.8f, 0.2f, 0.2f) },
        new BuildDef { type = BuildType.Wall, name = "Wood Wall", woodCost = 4, stoneCost = 0, size = new Vector3(3f, 3f, 0.2f), color = new Color(0.55f, 0.35f, 0.15f) },
        new BuildDef { type = BuildType.Foundation, name = "Stone Foundation", woodCost = 0, stoneCost = 10, size = new Vector3(3f, 0.5f, 3f), color = new Color(0.6f, 0.6f, 0.6f) }
    };

    public bool IsInBuildMode => buildMode;

    private bool buildMode;
    private int selectedIndex = 0;

    private GameObject ghostRoot;
    private MeshRenderer ghostRenderer;
    private GameObject uiRoot;
    private Text infoText;
    private Text[] itemLabels;
    
    private Transform player;
    private Camera cam;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        cam = Camera.main;
        
        BuildUI();
        CreateGhost();
    }

    private void BuildUI()
    {
        GameObject canvasGO = UIKit.CreateCanvas("Build UI", 9);
        uiRoot = canvasGO;
        
        // Left side panel
        RectTransform leftPanel = UIKit.CreateImage(canvasGO.transform, "LeftPanel", new Color(0f, 0f, 0f, 0.7f));
        leftPanel.anchorMin = new Vector2(0f, 0f);
        leftPanel.anchorMax = new Vector2(0f, 1f);
        leftPanel.pivot = new Vector2(0f, 0.5f);
        leftPanel.anchoredPosition = new Vector2(0f, 0f);
        leftPanel.sizeDelta = new Vector2(250f, 0f);
        
        UIKit.VLayout(leftPanel.gameObject, 15f, 20, TextAnchor.UpperLeft);

        Text title = UIKit.CreateText(leftPanel, "Title", "BUILD MENU", 28, Color.white, TextAnchor.MiddleLeft);
        UIKit.Size(title, 210f, 40f);

        itemLabels = new Text[buildDefs.Length];
        for(int i = 0; i < buildDefs.Length; i++)
        {
            itemLabels[i] = UIKit.CreateText(leftPanel, "Item " + i, buildDefs[i].name, 22, Color.gray, TextAnchor.MiddleLeft);
            UIKit.Size(itemLabels[i], 210f, 30f);
        }
        
        RectTransform panel = UIKit.CreateImage(canvasGO.transform, "BuildPanel", new Color(0f, 0f, 0f, 0.7f));
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 130f);
        panel.sizeDelta = new Vector2(500f, 120f);

        infoText = UIKit.CreateText(panel, "InfoText", "", 24, Color.white, TextAnchor.MiddleCenter);
        infoText.rectTransform.anchorMin = Vector2.zero;
        infoText.rectTransform.anchorMax = Vector2.one;
        infoText.rectTransform.sizeDelta = Vector2.zero;
        infoText.rectTransform.anchoredPosition = Vector2.zero;
        
        uiRoot.SetActive(false);
    }

    private void CreateGhost()
    {
        ghostRoot = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(ghostRoot.GetComponent<Collider>());
        ghostRenderer = ghostRoot.GetComponent<MeshRenderer>();
        
        // Transparent material
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        ghostRenderer.sharedMaterial = mat;

        ghostRoot.SetActive(false);
    }

    private void Update()
    {
#if ENABLE_INPUT_SYSTEM
        if (Keybinds.WasPressed(GameAction.Build) || (buildMode && UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame))
#else
        if (Keybinds.WasPressed(GameAction.Build) || (buildMode && Input.GetKeyDown(KeyCode.Escape)))
#endif
        {
            buildMode = !buildMode;
            if (buildMode && Keybinds.WasPressed(GameAction.Build)) buildMode = true;
            else if (!Keybinds.WasPressed(GameAction.Build)) buildMode = false;
            
            uiRoot.SetActive(buildMode);
            ghostRoot.SetActive(buildMode);
            if (buildMode) UpdateGhostAppearance();
        }

        if (!buildMode || player == null || cam == null) return;

        // Scroll to change item
        float scroll = Mouse.current.scroll.ReadValue().y;
        if (scroll > 0.1f) { selectedIndex = (selectedIndex - 1 + buildDefs.Length) % buildDefs.Length; UpdateGhostAppearance(); }
        else if (scroll < -0.1f) { selectedIndex = (selectedIndex + 1) % buildDefs.Length; UpdateGhostAppearance(); }

        // Raycast to place
        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        bool canPlace = false;
        Vector3 placePos = Vector3.zero;

        if (Physics.Raycast(ray, out RaycastHit hit, 15f))
        {
            placePos = hit.point;
            // Snap to grid if it's a foundation or wall
            BuildDef def = buildDefs[selectedIndex];
            if (def.type == BuildType.Foundation || def.type == BuildType.Wall)
            {
                placePos.x = Mathf.Round(placePos.x / 1.5f) * 1.5f;
                placePos.z = Mathf.Round(placePos.z / 1.5f) * 1.5f;
            }
            
            // Adjust y so it sits on the ground
            placePos.y += def.size.y * 0.5f;

            ghostRoot.transform.position = placePos;
            
            // Wall should face player but snapped to 90 degrees
            if (def.type == BuildType.Wall)
            {
                Vector3 toPlayer = player.position - placePos;
                toPlayer.y = 0;
                float angle = Mathf.Round(Vector3.SignedAngle(Vector3.forward, toPlayer, Vector3.up) / 90f) * 90f;
                ghostRoot.transform.rotation = Quaternion.Euler(0f, angle, 0f);
            }
            else if (def.type == BuildType.Bed)
            {
                ghostRoot.transform.rotation = Quaternion.Euler(0f, Mathf.Round(player.eulerAngles.y / 90f) * 90f, 0f);
            }
            else
            {
                ghostRoot.transform.rotation = Quaternion.identity;
            }

            // Check resources
            Inventory inv = Inventory.Instance;
            bool hasResources = inv != null && inv.GetCount("Wood") >= def.woodCost && inv.GetCount("Stone") >= def.stoneCost;
            bool hasTotem = VillageTotem.Instance != null || def.type == BuildType.VillageTotem;
            
            canPlace = hasResources && hasTotem;
            
            Color c = def.color;
            c.a = 0.6f;
            ghostRenderer.material.color = canPlace ? c : new Color(1f, 0f, 0f, 0.5f);
        }
        else
        {
            ghostRoot.transform.position = cam.transform.position + cam.transform.forward * 5f;
            ghostRenderer.material.color = new Color(1f, 0f, 0f, 0.5f);
        }

        if (canPlace && Mouse.current.leftButton.wasPressedThisFrame)
        {
            BuildObject(placePos, ghostRoot.transform.rotation);
        }
    }

    private void UpdateGhostAppearance()
    {
        BuildDef def = buildDefs[selectedIndex];
        ghostRoot.transform.localScale = def.size;
        
        Inventory inv = Inventory.Instance;
        int w = inv != null ? inv.GetCount("Wood") : 0;
        int s = inv != null ? inv.GetCount("Stone") : 0;
        
        string reqText = "";
        if (VillageTotem.Instance == null && def.type != BuildType.VillageTotem)
        {
            reqText = "\n<color=red>You must build a Village Totem first!</color>";
        }

        infoText.text = $"{def.name}\nCost: {def.woodCost} Wood ({w}), {def.stoneCost} Stone ({s}){reqText}\n<size=18>Scroll to change | Click to build | B to exit</size>";

        for (int i = 0; i < itemLabels.Length; i++)
        {
            itemLabels[i].color = (i == selectedIndex) ? Color.white : Color.gray;
            itemLabels[i].text = (i == selectedIndex) ? "> " + buildDefs[i].name : "  " + buildDefs[i].name;
        }
    }

    private void BuildObject(Vector3 pos, Quaternion rot)
    {
        BuildDef def = buildDefs[selectedIndex];
        
        Inventory.Instance.RemoveItem("Wood", def.woodCost);
        Inventory.Instance.RemoveItem("Stone", def.stoneCost);

        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = def.name;
        go.transform.position = pos;
        go.transform.rotation = rot;
        go.transform.localScale = def.size;
        
        Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        mat.color = def.color;
        go.GetComponent<MeshRenderer>().sharedMaterial = mat;
        
        if (def.type == BuildType.Bed)
        {
            go.AddComponent<PlayerBed>();
            if (VillageTotem.Instance != null)
            {
                VillageTotem.Instance.TotalBeds++;
                Inventory.Instance.Notify($"Bed placed! Village Capacity: {VillageTotem.Instance.TotalBeds}");
            }
        }
        
        if (def.type == BuildType.VillageTotem)
        {
            go.AddComponent<VillageTotem>();
        }
        else if (def.type == BuildType.Campfire)
        {
            GameObject lightGO = new GameObject("FireLight");
            lightGO.transform.SetParent(go.transform, false);
            lightGO.transform.localPosition = new Vector3(0f, 1f, 0f);
            Light l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.6f, 0.1f);
            l.range = 8f;
            l.intensity = 2f;
        }

        UpdateGhostAppearance(); // Refresh cost text
    }
}








