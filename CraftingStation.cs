using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// =====================================================================================
//  Crafting lives in this file:
//    CraftingRecipe      one recipe: a list of ingredients and what comes out
//    CraftingStation     drop on any NPC prefab. Hold E to open the crafting UI.
//                        Camera pans to the NPC exactly like a conversation.
//                        A panel slides in from the left listing every recipe.
//
//  New craftable items are also added to the Inventory Catalog from here via
//  CraftingStation.RegisterItems(), which is called in Awake before anything else.
//
//  Keep this file named CraftingStation.cs.
// =====================================================================================

/// <summary>
/// One ingredient slot: an item id and how many of it are needed.
/// </summary>
[System.Serializable]
public class Ingredient
{
    public string ItemId;
    public int Amount;
}

/// <summary>
/// A crafting recipe. Put a list of these in CraftingStation.Recipes below.
/// </summary>
[System.Serializable]
public class CraftingRecipe
{
    public string Name;          // displayed as the recipe title
    public string Description;   // one line shown under the title
    public Ingredient[] Inputs;  // what you need
    public string OutputId;      // what you get
    public int OutputAmount;     // how many you get
}

/// <summary>
/// Drop this on an NPC prefab (or any GameObject) to turn it into a crafting station.
/// Walk up to it, hold E â€” the camera pans to the NPC's face and the crafting panel opens on the left.
/// Requires a PlayerController and CameraController in the scene.
/// </summary>
public class CraftingStation : MonoBehaviour, IInteractable
{
    // ---------------------------------------------------------------------- Recipes
    // Add or change recipes here. Items referenced by OutputId must exist in the
    // Inventory Catalog (the built-in ones, or the new ones registered by RegisterItems below).

    private static readonly CraftingRecipe[] Recipes =
    {
        new CraftingRecipe
        {
            Name        = "Stone Axe",
            Description = "Equip this from your hotbar to harvest Wood.",
            Inputs      = new[] { new Ingredient { ItemId = "Wood", Amount = 5 },
                                  new Ingredient { ItemId = "Stone", Amount = 3 } },
            OutputId    = "Axe",
            OutputAmount= 1
        },
        new CraftingRecipe
        {
            Name        = "Stone Pickaxe",
            Description = "Equip this from your hotbar to harvest Stone.",
            Inputs      = new[] { new Ingredient { ItemId = "Wood", Amount = 3 },
                                  new Ingredient { ItemId = "Stone", Amount = 6 } },
            OutputId    = "Pickaxe",
            OutputAmount= 1
        },
        new CraftingRecipe
        {
            Name        = "Healing Potion",
            Description = "Restores vitality. A woodsman's remedy.",
            Inputs      = new[] { new Ingredient { ItemId = "apple", Amount = 2 },
                                  new Ingredient { ItemId = "mushroom", Amount = 1 } },
            OutputId     = "potion",
            OutputAmount = 1,
        },
        new CraftingRecipe
        {
            Name        = "Lucky Charm",
            Description = "Glimmers with good fortune.",
            Inputs      = new[] { new Ingredient { ItemId = "gem", Amount = 1 },
                                  new Ingredient { ItemId = "coin", Amount = 5 } },
            OutputId     = "charm",
            OutputAmount = 1,
        },
        new CraftingRecipe
        {
            Name        = "Enchanted Key",
            Description = "Opens locks that would otherwise resist you.",
            Inputs      = new[] { new Ingredient { ItemId = "key", Amount = 1 },
                                  new Ingredient { ItemId = "gem", Amount = 1 } },
            OutputId     = "enchantedkey",
            OutputAmount = 1,
        },
        new CraftingRecipe
        {
            Name        = "Mushroom Stew",
            Description = "A hearty meal. Trades for a fair price.",
            Inputs      = new[] { new Ingredient { ItemId = "mushroom", Amount = 3 } },
            OutputId     = "stew",
            OutputAmount = 1,
        },
        new CraftingRecipe
        {
            Name        = "Coin Pouch",
            Description = "Bundle three apples into travelling coins.",
            Inputs      = new[] { new Ingredient { ItemId = "apple", Amount = 3 } },
            OutputId     = "coin",
            OutputAmount = 6,
        },
    };

    // ---------------------------------------------------------------------- Settings

    [Header("Interaction")]
    [Tooltip("Label shown in the Hold-E prompt: \"Hold E to talk to the Crafter\".")]
    [SerializeField] private string craftingPrompt = "talk to the Crafter";
    [Tooltip("Seconds the player must hold E.")]
    [SerializeField, Min(0f)] private float holdTime = 1f;
    [Tooltip("How close the player needs to be, in metres.")]
    [SerializeField, Min(0.5f)] private float interactRange = 3f;

    [Header("Camera")]
    [SerializeField] private float panDuration = 0.6f;
    [Tooltip("How far in front of the NPC's face the camera ends up.")]
    [SerializeField] private float cameraDistance = 1.6f;

    [Header("Label")]
    [Tooltip("Name shown in the crafting panel header.")]
    [SerializeField] private string stationName = "The Crafter";
    [Tooltip("Short tagline shown under the name.")]
    [SerializeField] private string stationSubtitle = "\"I can make something useful out of that.\"";

    // ---------------------------------------------------------------------- Runtime

    private PlayerController   playerController;
    private CameraController   cameraController;
    private bool crafting;

    // Visual effects
    private Light           stationLight;
    private ParticleSystem  stationParticles;
    private Transform       indicatorTransform;

    // UI
    private GameObject panelRoot;
    private Text       recipeTitle;
    private Text       recipeDesc;
    private RectTransform ingredientList;
    private Button     craftButton;
    private Text       craftButtonLabel;
    private Text       resultText;
    private readonly List<Button> recipeButtons = new List<Button>();

    private CraftingRecipe selectedRecipe;
    private int            selectedIndex = -1;

    // ---------------------------------------------------------------------- New Items
    // The Inventory catalog is a private static array, so we extend it by registering
    // additional ItemDefinitions through the public extension point below.

    /// <summary>
    /// Adds the craftable items that don't already exist in the Inventory catalog.
    /// Called once in Awake so they're available before the first scene tick.
    /// </summary>
    private static bool itemsRegistered;
    private static void RegisterItems()
    {
        if (itemsRegistered) return;
        itemsRegistered = true;

        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "Axe",
            DisplayName = "Stone Axe",
            Value       = 15,
            Color       = new Color(0.4f, 0.4f, 0.4f),
            Shape       = PrimitiveType.Cube,
            Scale       = new Vector3(0.1f, 0.3f, 0.4f),
            Rotation    = Vector3.zero,
            Icon        = ToolModels.GetIcon(ToolManager.ToolType.Axe),
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "Pickaxe",
            DisplayName = "Stone Pickaxe",
            Value       = 15,
            Color       = new Color(0.4f, 0.4f, 0.4f),
            Shape       = PrimitiveType.Cube,
            Scale       = new Vector3(0.1f, 0.1f, 0.7f),
            Rotation    = Vector3.zero,
            Icon        = ToolModels.GetIcon(ToolManager.ToolType.Pickaxe),
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "FishingRod",
            DisplayName = "Fishing Rod",
            Value       = 15,
            Color       = new Color(0.5f, 0.3f, 0.1f),
            Shape       = PrimitiveType.Cylinder,
            Scale       = new Vector3(0.05f, 0.5f, 0.05f),
            Rotation    = Vector3.zero,
            Icon        = ToolModels.GetIcon(ToolManager.ToolType.FishingRod),
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "Fish",
            DisplayName = "Raw Fish",
            Value       = 10,
            Color       = new Color(0.2f, 0.6f, 0.8f),
            Shape       = PrimitiveType.Capsule,
            Scale       = new Vector3(0.1f, 0.3f, 0.1f),
            Rotation    = new Vector3(0f, 0f, 90f)
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "potion",
            DisplayName = "Healing Potion",
            Value       = 8,
            Color       = new Color(0.9f, 0.25f, 0.6f),
            Shape       = PrimitiveType.Capsule,
            Scale       = new Vector3(0.18f, 0.22f, 0.18f),
            Rotation    = Vector3.zero,
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "charm",
            DisplayName = "Lucky Charm",
            Value       = 30,
            Color       = new Color(1f, 0.75f, 0.1f),
            Shape       = PrimitiveType.Sphere,
            Scale       = new Vector3(0.22f, 0.22f, 0.22f),
            Rotation    = Vector3.zero,
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "enchantedkey",
            DisplayName = "Enchanted Key",
            Value       = 22,
            Color       = new Color(0.4f, 1f, 0.7f),
            Shape       = PrimitiveType.Cube,
            Scale       = new Vector3(0.08f, 0.45f, 0.08f),
            Rotation    = new Vector3(0f, 0f, 60f),
        });
        InventoryCatalogExtensions.Register(new ItemDefinition
        {
            Id          = "stew",
            DisplayName = "Mushroom Stew",
            Value       = 6,
            Color       = new Color(0.6f, 0.35f, 0.15f),
            Shape       = PrimitiveType.Sphere,
            Scale       = new Vector3(0.28f, 0.18f, 0.28f),
            Rotation    = Vector3.zero,
        });
    }

    // ---------------------------------------------------------------------- IInteractable

    public string Prompt      => craftingPrompt;
    public float  HoldTime    => holdTime;
    public float  Range       => interactRange;
    public bool   CanInteract => !crafting;

    public float DistanceTo(Vector3 point)
    {
        Vector3 diff = transform.position - point;
        diff.y = 0f;
        return diff.magnitude;
    }

    public void Interact()
    {
        if (!crafting) StartCoroutine(CraftingRoutine());
    }

    // ---------------------------------------------------------------------- Lifecycle

    private void Awake()
    {
        RegisterItems();
    }

    private void Start()
    {
        if (WorldGenerator.Instance != null)
        {
            // Spawn the crafter somewhere randomly in the starting area
            float rx = UnityEngine.Random.Range(-40f, 40f);
            float rz = UnityEngine.Random.Range(-40f, 40f);
            float ry = WorldGenerator.Instance.GetHeightAt(rx, rz);
            transform.position = new Vector3(rx, ry, rz);
        }

        playerController = FindAnyObjectByType<PlayerController>();
        cameraController = FindAnyObjectByType<CameraController>();

        if (playerController == null || cameraController == null)
        {
            Debug.LogWarning("CraftingStation: couldn't find PlayerController or CameraController.", this);
        }

        PauseMenu.EnsureEventSystem();
        BuildUI();

        InteractionManager.Register(this);

        // ---- Visual effects ----

        // 1. Pulsing point light
        GameObject lightGO = new GameObject("CraftStation Light");
        lightGO.transform.SetParent(transform, false);
        lightGO.transform.localPosition = new Vector3(0f, 2.2f, 0f);
        stationLight            = lightGO.AddComponent<Light>();
        stationLight.type       = LightType.Point;
        stationLight.color      = new Color(1f, 0.75f, 0.2f);
        stationLight.range      = 4f;
        stationLight.intensity  = 1.5f;

        // 2. Particle ring
        GameObject particleGO = new GameObject("CraftStation Particles");
        particleGO.transform.SetParent(transform, false);
        particleGO.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        stationParticles = particleGO.AddComponent<ParticleSystem>();

        // Main module
        var main                = stationParticles.main;
        main.loop               = true;
        main.startColor         = new Color(1f, 0.8f, 0.2f, 0.9f);
        main.startSize          = 0.08f;
        main.startLifetime      = 1.2f;
        main.startSpeed         = 0.6f;
        main.gravityModifier    = -0.2f;
        main.maxParticles       = 40;
        main.simulationSpace    = ParticleSystemSimulationSpace.World;

        // Emission module
        var emission            = stationParticles.emission;
        emission.enabled        = true;
        emission.rateOverTime   = 18f;

        // Shape module â€” circle emitting upward
        var shape               = stationParticles.shape;
        shape.enabled           = true;
        shape.shapeType         = ParticleSystemShapeType.Circle;
        shape.radius            = 0.6f;

        stationParticles.Play();

        // 3. Floating indicator sphere
        GameObject indicatorGO = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        indicatorGO.name = "CraftStation Indicator";
        Destroy(indicatorGO.GetComponent<SphereCollider>());
        indicatorGO.transform.SetParent(transform, false);
        indicatorGO.transform.localScale    = new Vector3(0.25f, 0.25f, 0.25f);
        indicatorGO.transform.localPosition = new Vector3(0f, 2.5f, 0f);
        indicatorTransform = indicatorGO.transform;

        Renderer indicatorRenderer = indicatorGO.GetComponent<Renderer>();
        indicatorRenderer.material = new Material(indicatorRenderer.material);
        indicatorRenderer.material.color = new Color(1f, 0.75f, 0.2f);
    }

    private void OnDestroy()
    {
        InteractionManager.Unregister(this);
    }

    private void Update()
    {
        if (stationLight != null)
        {
            if (crafting)
            {
                stationLight.intensity = 0.4f;
            }
            else
            {
                // Pulse between 0.8 and 2.2
                float t = (Mathf.Sin(Time.time * 2f) + 1f) * 0.5f;   // 0..1
                stationLight.intensity = Mathf.Lerp(0.8f, 2.2f, t);
            }
        }

        if (stationParticles != null)
        {
            var emission = stationParticles.emission;
            emission.rateOverTime = crafting ? 0f : 18f;
        }

        if (indicatorTransform != null)
        {
            if (crafting)
            {
                if (indicatorTransform.gameObject.activeSelf)
                    indicatorTransform.gameObject.SetActive(false);
            }
            else
            {
                if (!indicatorTransform.gameObject.activeSelf)
                    indicatorTransform.gameObject.SetActive(true);

                // Bob up and down around the 2.5f base
                float bobY = 2.5f + Mathf.Sin(Time.time * 1.8f) * 0.12f;
                indicatorTransform.localPosition = new Vector3(0f, bobY, 0f);

                // Slow world-space spin
                indicatorTransform.Rotate(0f, 45f * Time.deltaTime, 0f, Space.World);
            }
        }
    }

    // ---------------------------------------------------------------------- Crafting routine

    private IEnumerator CraftingRoutine()
    {
        crafting = true;
        InteractionManager.Block();

        // Lock the player and take the camera
        if (playerController != null) playerController.enabled = false;
        if (cameraController != null) cameraController.enabled = false;

        Transform cam = cameraController != null ? cameraController.transform : Camera.main.transform;
        Vector3    startPos = cam.position;
        Quaternion startRot = cam.rotation;

        // Work out where the camera should end up (in front of the NPC's face)
        GetNpcHeights(out float faceHeight, out _);
        Vector3 npcPos = transform.position;

        Vector3 toPlayer = playerController != null
            ? playerController.transform.position - npcPos
            : -transform.forward;
        toPlayer.y = 0f;
        float playerDist = toPlayer.magnitude;
        Vector3 dir = playerDist > 0.01f ? toPlayer / playerDist : transform.forward;

        float dist       = Mathf.Clamp(Mathf.Min(cameraDistance, playerDist - 0.7f), 0.9f, cameraDistance);
        Vector3 focus    = npcPos + Vector3.up * faceHeight;
        Vector3 targetPos = focus + dir * dist;
        Quaternion targetRot = Quaternion.LookRotation(focus - targetPos);

        yield return PanCamera(cam, startPos, startRot, targetPos, targetRot);

        // ---- Open the crafting UI ----
        SelectRecipe(0);
        panelRoot.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;

        // Wait until the player closes the panel (Close button sets crafting = false)
        while (crafting)
        {
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame || 
                    UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame)
                {
                    crafting = false;
                }
            }
#else
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.E))
            {
                crafting = false;
            }
#endif
            yield return null;
        }

        // ---- Close ----
        panelRoot.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;

        // Pan back and return control
        yield return PanCamera(cam, targetPos, targetRot, startPos, startRot);

        if (cameraController != null) cameraController.enabled = true;
        if (playerController  != null) playerController.enabled  = true;

        InteractionManager.Unblock();
    }

    private void CloseCraftingUI()
    {
        if (Time.timeScale == 0f) return;   // ignore clicks while paused
        crafting = false;
    }

    // ---------------------------------------------------------------------- Crafting logic

    private void SelectRecipe(int index)
    {
        if (index < 0 || index >= Recipes.Length) return;
        selectedIndex  = index;
        selectedRecipe = Recipes[index];

        // Highlight the selected button
        for (int i = 0; i < recipeButtons.Count; i++)
        {
            ColorBlock cb = recipeButtons[i].colors;
            cb.normalColor   = i == index ? UIKit.Accent      : UIKit.ButtonColor;
            cb.selectedColor = cb.normalColor;
            recipeButtons[i].colors = cb;
        }

        RefreshDetail();
    }

    private void RefreshDetail()
    {
        if (selectedRecipe == null) return;

        recipeTitle.text = selectedRecipe.Name;
        recipeDesc.text  = selectedRecipe.Description;

        // Rebuild ingredient rows
        foreach (Transform child in ingredientList)
            Destroy(child.gameObject);

        Inventory inv = Inventory.Instance;

        foreach (Ingredient ing in selectedRecipe.Inputs)
        {
            ItemDefinition def   = Inventory.GetDefinition(ing.ItemId);
            string itemName      = def != null ? def.DisplayName : ing.ItemId;
            int    have          = inv != null ? inv.GetCount(ing.ItemId) : 0;
            bool   enough        = have >= ing.Amount;
            Color  rowColor      = enough ? UIKit.Good : new Color(1f, 0.4f, 0.4f);

            RectTransform row = UIKit.CreateImage(ingredientList, "Ing Row", new Color(1f, 1f, 1f, 0.04f));
            UIKit.HLayout(row.gameObject, 10f, 10);
            UIKit.Size(row, 520f, 40f);

            if (def != null)
                UIKit.Size(UIKit.CreateItemSwatch(row, "Swatch", def), 24f, 24f);

            UIKit.Size(UIKit.CreateText(row, "Name", itemName, 26, Color.white, TextAnchor.MiddleLeft), 280f, 36f);
            UIKit.Size(UIKit.CreateText(row, "Need",  $"x{ing.Amount}", 26, Color.white,  TextAnchor.MiddleRight), 70f, 36f);
            UIKit.Size(UIKit.CreateText(row, "Have",  $"({have})",       26, rowColor,     TextAnchor.MiddleRight), 90f, 36f);
        }

        // Output
        ItemDefinition outDef = Inventory.GetDefinition(selectedRecipe.OutputId);
        string outName = outDef != null ? outDef.DisplayName : selectedRecipe.OutputId;
        int outAmt = selectedRecipe.OutputAmount;
        resultText.text = outAmt > 1 ? $"â†’  {outName}  x{outAmt}" : $"â†’  {outName}";

        // Can craft?
        bool canCraft = CanCraftSelected();
        craftButton.interactable = canCraft;
        craftButtonLabel.text    = canCraft ? "Craft" : "Not enough materials";
    }

    private bool CanCraftSelected()
    {
        if (selectedRecipe == null || Inventory.Instance == null) return false;
        foreach (Ingredient ing in selectedRecipe.Inputs)
            if (Inventory.Instance.GetCount(ing.ItemId) < ing.Amount) return false;
        return true;
    }

    private void TryCraft()
    {
        if (Time.timeScale == 0f) return;
        if (!CanCraftSelected()) return;

        Inventory inv = Inventory.Instance;
        foreach (Ingredient ing in selectedRecipe.Inputs)
            inv.RemoveItem(ing.ItemId, ing.Amount);

        inv.AddItem(selectedRecipe.OutputId, selectedRecipe.OutputAmount);

        RefreshDetail();    // ingredient counts have changed
    }

    // ---------------------------------------------------------------------- Camera

    private IEnumerator PanCamera(Transform cam, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, panDuration);
            float s = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t));
            cam.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, s), Quaternion.Slerp(fromRot, toRot, s));
            yield return null;
        }
        cam.SetPositionAndRotation(toPos, toRot);
    }

    private void GetNpcHeights(out float faceHeight, out float headHeight)
    {
        bool boundsSet = false;
        Bounds b = default;

        foreach (SkinnedMeshRenderer smr in GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            if (!boundsSet) { b = smr.bounds; boundsSet = true; }
            else b.Encapsulate(smr.bounds);
        }
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>())
        {
            if (mr.gameObject.name.Contains("CraftStation Indicator")) continue;
            
            if (!boundsSet) { b = mr.bounds; boundsSet = true; }
            else b.Encapsulate(mr.bounds);
        }

        if (!boundsSet)
        {
            faceHeight = 1.6f;
            headHeight = 1.9f;
            return;
        }

        headHeight = b.max.y - transform.position.y;
        faceHeight = (b.min.y - transform.position.y) + b.size.y * 0.88f;
    }

    // ---------------------------------------------------------------------- Gizmo

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.6f, 0.1f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }

    // ---------------------------------------------------------------------- UI

    private void BuildUI()
    {
        GameObject canvasGO = UIKit.CreateCanvas("Crafting UI", 8);

        // ---- Left panel --------------------------------------------------------
        RectTransform panel = UIKit.CreateImage(canvasGO.transform, "CraftingPanel", UIKit.PanelColor);
        panel.anchorMin = panel.anchorMax = new Vector2(0f, 0.5f);
        panel.pivot     = new Vector2(0f, 0.5f);
        panel.anchoredPosition = new Vector2(40f, 0f);
        panel.sizeDelta = new Vector2(600f, 1100f);
        UIKit.VLayout(panel.gameObject, 12f, 24, TextAnchor.UpperCenter);

        // Header: station name + subtitle
        Text title = UIKit.CreateText(panel, "Title", stationName, 38, Color.white);
        UIKit.Size(title, 552f, 52f);

        Text subtitle = UIKit.CreateText(panel, "Subtitle", stationSubtitle, 24, UIKit.Subtle);
        subtitle.fontStyle = FontStyle.Italic;
        UIKit.Size(subtitle, 552f, 32f);

        // Divider
        RectTransform div = UIKit.CreateImage(panel, "Divider", new Color(1f, 1f, 1f, 0.12f));
        UIKit.Size(div, 552f, 2f);

        // ---- Recipe list (left column) ----------------------------------------
        Text recipeHeader = UIKit.CreateText(panel, "RecipesHeader", "RECIPES", 22, UIKit.Accent, TextAnchor.MiddleLeft);
        recipeHeader.fontStyle = FontStyle.Bold;
        UIKit.Size(recipeHeader, 552f, 28f);

        RectTransform recipeListRect = UIKit.CreateRect(panel, "RecipeList");
        UIKit.VLayout(recipeListRect.gameObject, 6f, 0, TextAnchor.UpperCenter);
        UIKit.Size(recipeListRect, 552f, -1f);

        for (int i = 0; i < Recipes.Length; i++)
        {
            int idx = i;
            Button btn = UIKit.CreateButton(recipeListRect, Recipes[i].Name + " Btn",
                Recipes[i].Name, 28, () => SelectRecipe(idx), out _);
            UIKit.Size(btn, 552f, 52f);
            recipeButtons.Add(btn);
        }

        // ---- Detail section ---------------------------------------------------
        RectTransform div2 = UIKit.CreateImage(panel, "Divider2", new Color(1f, 1f, 1f, 0.12f));
        UIKit.Size(div2, 552f, 2f);

        Text detailHeader = UIKit.CreateText(panel, "DetailHeader", "DETAILS", 22, UIKit.Accent, TextAnchor.MiddleLeft);
        detailHeader.fontStyle = FontStyle.Bold;
        UIKit.Size(detailHeader, 552f, 28f);

        recipeTitle = UIKit.CreateText(panel, "RecipeTitle", "", 30, Color.white);
        UIKit.Size(recipeTitle, 552f, 40f);

        recipeDesc = UIKit.CreateText(panel, "RecipeDesc", "", 24, UIKit.Subtle);
        recipeDesc.fontStyle = FontStyle.Italic;
        UIKit.Size(recipeDesc, 552f, 32f);

        // Ingredient rows container
        ingredientList = UIKit.CreateRect(panel, "Ingredients");
        UIKit.VLayout(ingredientList.gameObject, 4f, 0, TextAnchor.UpperCenter);
        UIKit.Size(ingredientList, 552f, -1f);

        // Output label
        resultText = UIKit.CreateText(panel, "Result", "", 28, UIKit.Gold);
        UIKit.Size(resultText, 552f, 38f);

        // Craft button
        craftButton = UIKit.CreateButton(panel, "CraftBtn", "Craft", 32, TryCraft, out craftButtonLabel);
        UIKit.Size(craftButton, 552f, 62f);

        // Close button
        Button closeBtn = UIKit.CreateButton(panel, "CloseBtn", "Close", 28, CloseCraftingUI, out _);
        UIKit.Size(closeBtn, 552f, 52f);

        panelRoot = panel.gameObject;
        panelRoot.SetActive(false);
    }
}

// =====================================================================================
//  Inventory catalog extension helpers
//  (because the real catalog is a private static array, we maintain a parallel list here)
// =====================================================================================

/// <summary>
/// Lets other systems (like CraftingStation) add item definitions to the game without
/// editing Inventory.cs. Call Register() before the first Inventory.GetDefinition() call.
/// Inventory.GetDefinition() checks this list automatically after the built-in catalog.
/// </summary>
public static class InventoryCatalogExtensions
{
    private static readonly List<ItemDefinition> extras = new List<ItemDefinition>();

    /// <summary>Adds an item type. Ignored if an item with the same Id is already registered.</summary>
    public static void Register(ItemDefinition def)
    {
        if (def == null || Inventory.GetDefinition(def.Id) != null) return;
        foreach (ItemDefinition e in extras)
            if (e.Id == def.Id) return;
        extras.Add(def);
    }

    /// <summary>All extra items registered so far. Inventory.GetDefinition checks this internally.</summary>
    public static IReadOnlyList<ItemDefinition> All => extras;
}






