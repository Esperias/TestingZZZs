using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Events;

// --- ItemDefinition.cs ---

public class ItemDefinition
{
	public string Id;

	public string DisplayName;

	public int Value;

	public Color Color;

	public PrimitiveType Shape;

	public Vector3 Scale;

	public Vector3 Rotation;

	/// <summary>Optional 2D icon. When set, UI slots show this instead of a flat color swatch.</summary>
	public Sprite Icon;
}


// --- Inventory.cs ---

public class Inventory : MonoBehaviour
{
	public class Pickup : IInteractable
	{
		public Inventory owner;

		public GameObject go;

		public ItemDefinition item;

		public float baseY;

		public float phase;

		public string Prompt => "pick up " + item.DisplayName;

		public float HoldTime => 0f;

		public float Range => owner.pickupRange;

		public bool CanInteract => go != null;

		public float DistanceTo(Vector3 point)
		{
			return Vector3.Distance(point, go.transform.position);
		}

		public void Interact()
		{
			owner.Collect(this);
		}
	}

	private class ResourceDrop
	{
		public Inventory owner;

		public GameObject go;

		public ItemDefinition item;

		public float baseY;

		public float phase;

		public float spawnTime;

		public Vector3 startScale;
	}

	private class RespawnEntry
	{
		public float time;

		public ItemDefinition item;

		public Vector3 avoid;
	}

	private static readonly ItemDefinition[] Catalog = new ItemDefinition[15]
	{
		new ItemDefinition
		{
			Id = "apple",
			DisplayName = "Apple",
			Value = 2,
			Color = new Color(0.85f, 0.15f, 0.15f),
			Shape = PrimitiveType.Sphere,
			Scale = new Vector3(0.3f, 0.3f, 0.3f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "coin",
			DisplayName = "Gold Coin",
			Value = 1,
			Color = new Color(1f, 0.82f, 0.15f),
			Shape = PrimitiveType.Cylinder,
			Scale = new Vector3(0.4f, 0.03f, 0.4f),
			Rotation = new Vector3(90f, 0f, 0f)
		},
		new ItemDefinition
		{
			Id = "gem",
			DisplayName = "Blue Gem",
			Value = 25,
			Color = new Color(0.2f, 0.8f, 1f),
			Shape = PrimitiveType.Cube,
			Scale = new Vector3(0.28f, 0.28f, 0.28f),
			Rotation = new Vector3(45f, 0f, 45f)
		},
		new ItemDefinition
		{
			Id = "mushroom",
			DisplayName = "Mushroom",
			Value = 4,
			Color = new Color(0.85f, 0.6f, 0.4f),
			Shape = PrimitiveType.Capsule,
			Scale = new Vector3(0.22f, 0.22f, 0.22f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "key",
			DisplayName = "Old Key",
			Value = 10,
			Color = new Color(0.7f, 0.7f, 0.78f),
			Shape = PrimitiveType.Cube,
			Scale = new Vector3(0.08f, 0.45f, 0.08f),
			Rotation = new Vector3(0f, 0f, 60f)
		},
		new ItemDefinition
		{
			Id = "Wood",
			DisplayName = "Wood Log",
			Value = 3,
			Color = new Color(0.55f, 0.35f, 0.15f),
			Shape = PrimitiveType.Cylinder,
			Scale = new Vector3(0.2f, 0.4f, 0.2f),
			Rotation = new Vector3(90f, 0f, 0f)
		},
		new ItemDefinition
		{
			Id = "Stone",
			DisplayName = "Stone",
			Value = 2,
			Color = new Color(0.6f, 0.6f, 0.6f),
			Shape = PrimitiveType.Cube,
			Scale = new Vector3(0.25f, 0.25f, 0.25f),
			Rotation = new Vector3(0f, 45f, 0f)
		},
		new ItemDefinition
		{
			Id = "Berries",
			DisplayName = "Berries",
			Value = 5,
			Color = new Color(0.8f, 0.2f, 0.4f),
			Shape = PrimitiveType.Sphere,
			Scale = new Vector3(0.2f, 0.2f, 0.2f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "Sprouts",
			DisplayName = "Sprouts",
			Value = 4,
			Color = new Color(0.2f, 0.8f, 0.3f),
			Shape = PrimitiveType.Capsule,
			Scale = new Vector3(0.15f, 0.2f, 0.15f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "WheatSeed",
			DisplayName = "Wheat Seeds",
			Value = 3,
			Color = new Color(0.9f, 0.82f, 0.3f),
			Shape = PrimitiveType.Sphere,
			Scale = new Vector3(0.15f, 0.15f, 0.15f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "CarrotSeed",
			DisplayName = "Carrot Seeds",
			Value = 3,
			Color = new Color(0.85f, 0.55f, 0.1f),
			Shape = PrimitiveType.Sphere,
			Scale = new Vector3(0.15f, 0.15f, 0.15f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "Wheat",
			DisplayName = "Wheat",
			Value = 5,
			Color = new Color(1f, 0.88f, 0.2f),
			Shape = PrimitiveType.Capsule,
			Scale = new Vector3(0.12f, 0.22f, 0.12f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "Carrot",
			DisplayName = "Carrot",
			Value = 4,
			Color = new Color(1f, 0.52f, 0.1f),
			Shape = PrimitiveType.Capsule,
			Scale = new Vector3(0.12f, 0.22f, 0.12f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "Iron",
			DisplayName = "Iron Ore",
			Value = 8,
			Color = new Color(0.6f, 0.4f, 0.2f),
			Shape = PrimitiveType.Sphere,
			Scale = new Vector3(0.2f, 0.2f, 0.2f),
			Rotation = Vector3.zero
		},
		new ItemDefinition
		{
			Id = "Crystal",
			DisplayName = "Crystal",
			Value = 30,
			Color = new Color(0.3f, 0.6f, 0.9f),
			Shape = PrimitiveType.Cube,
			Scale = new Vector3(0.15f, 0.35f, 0.15f),
			Rotation = new Vector3(45f, 0f, 45f)
		}
	};

	[Header("Pickups")]
	[Tooltip("How close you need to be to pick something up.")]
	[SerializeField]
	public float pickupRange = 3f;

	[Header("Respawning")]
	[Tooltip("A collected item comes back somewhere else after this many seconds. 0 turns respawning off.")]
	[SerializeField]
	private float respawnDelay = 30f;

	[Tooltip("A respawned item appears at least this far from where it was picked up.")]
	[SerializeField]
	private float respawnMinMoveDistance = 8f;

	[Tooltip("...and at least this far from the player, so it never pops into view.")]
	[SerializeField]
	private float respawnMinPlayerDistance = 10f;

	[Header("Effects")]
	[SerializeField]
	private float spinSpeed = 60f;

	[SerializeField]
	private float bobHeight = 0.12f;

	[SerializeField]
	private float bobSpeed = 2f;

	private readonly List<ResourceDrop> resourceDrops = new List<ResourceDrop>();

	private readonly List<Pickup> pickups = new List<Pickup>();

	private readonly Dictionary<string, int> counts = new Dictionary<string, int>();

	public readonly List<string> itemOrder = new List<string>(new string[36]);

	private readonly List<RespawnEntry> respawns = new List<RespawnEntry>();

	private Transform player;

	private Collider ground;

	private Transform pickupContainer;

	private RectTransform notifRoot;

	private GameObject panelRoot;

	private Text panelTitle;

	private RectTransform itemList;

	private bool panelOpen;

	private RectTransform hotbarRoot;

	private Text levelText;

	private Text xpText;

	public static Inventory Instance { get; private set; }

	public static bool PanelOpen { get; private set; }

	public static int SelectedHotbarSlot { get; private set; } = 0;
	private static Material cachedOverlayMat;

	public int Level { get; private set; } = 1;

	public int XP { get; private set; }

	public int MaxXP => Level * 100;

	public static IReadOnlyList<ItemDefinition> AllItems => Catalog;

	public bool PickupsDone { get; private set; } = true;

	public float PickupsProgress { get; private set; } = 1f;

	public event Action Changed;

	public Material baseToonMaterial;

	private static readonly Dictionary<Color, Material> toonMaterialCache = new Dictionary<Color, Material>();
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticsForFastPlayMode() { toonMaterialCache.Clear(); }


	private void SetToonMaterial(GameObject obj, Color col)
	{
		Renderer component = obj.GetComponent<Renderer>();
		if (component != null)
		{
			if (!toonMaterialCache.TryGetValue(col, out Material cachedMat) || cachedMat == null)
			{
				if (baseToonMaterial != null)
				{
					cachedMat = new Material(baseToonMaterial);
				}
				else
				{
					Shader shader = Shader.Find("Custom/WalkingToon");
					cachedMat = new Material((shader != null) ? shader : Shader.Find("Standard"));
				}
				cachedMat.SetColor("_BaseColor", col);
				if (cachedMat.HasProperty("_Color"))
				{
					cachedMat.SetColor("_Color", col);
				}
				toonMaterialCache[col] = cachedMat;
			}
			component.sharedMaterial = cachedMat;
		}
	}

	public void AddXP(int amount)
	{
		XP += amount;
		bool flag = false;
		while (XP >= MaxXP)
		{
			XP -= MaxXP;
			Level++;
			flag = true;
		}
		if (flag)
		{
			Notify($"Level Up! You are now Level {Level}!");
		}
		Changed?.Invoke();
	}

	public static ItemDefinition GetDefinition(string id)
	{
		ItemDefinition[] catalog = Catalog;
		foreach (ItemDefinition itemDefinition in catalog)
		{
			if (itemDefinition.Id == id)
			{
				return itemDefinition;
			}
		}
		foreach (ItemDefinition item in InventoryCatalogExtensions.All)
		{
			if (item.Id == id)
			{
				return item;
			}
		}
		return null;
	}

	private void Awake()
	{
		Instance = this;
        if (cachedOverlayMat == null) {
            Shader s = Shader.Find("UI/Overlay");
            if (s != null) cachedOverlayMat = new Material(s);
        }
PanelOpen = false;
		if (GetComponent<BuildingManager>() == null)
		{
			base.gameObject.AddComponent<BuildingManager>();
		}
		if (GetComponent<PauseMenu>() == null)
		{
			base.gameObject.AddComponent<PauseMenu>();
		}
		if (GetComponent<Minimap>() == null)
		{
			base.gameObject.AddComponent<Minimap>();
		}
		if (SceneManager.GetActiveScene().name != "TestCave")
		{
			if (GetComponent<WorldGenerator>() == null)
			{
				base.gameObject.AddComponent<WorldGenerator>();
			}
			if (GetComponent<FarmingSystem>() == null)
			{
				base.gameObject.AddComponent<FarmingSystem>();
			}
		}
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
		if (panelOpen)
		{
			InteractionManager.Unblock();
		}
		PanelOpen = false;
	}

	private void Start()
	{
		pickupRange = 3f;
		PlayerController playerController = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
		if (playerController != null)
		{
			player = playerController.transform;
		}
		else
		{
			Debug.LogWarning("Inventory: no PlayerController found, so pickups can't be collected.", this);
		}
		BuildUI();
		if (WorldGenerator.Instance != null)
		{
			SpawnPickups();
			SpawnQuestZones();
		}
	}

	private void SpawnQuestZones()
	{
		Transform parent = new GameObject("QuestZones").transform;
		string[] exploreZones = QuestLog.ExploreZones;
		foreach (string text in exploreZones)
		{
			string id = text.Replace(" ", "_").ToLower();
			float x = UnityEngine.Random.Range(-500f, 500f);
			float z = UnityEngine.Random.Range(-500f, 500f);
			float y = ((WorldGenerator.Instance != null) ? WorldGenerator.Instance.GetHeightAt(x, z) : 0f);
			GameObject obj = new GameObject(text);
			obj.transform.SetParent(parent);
			obj.transform.position = new Vector3(x, y, z);
			SphereCollider sphereCollider = obj.AddComponent<SphereCollider>();
			sphereCollider.isTrigger = true;
			sphereCollider.radius = 8f;
			obj.AddComponent<QuestZone>().Setup(id, text);
		}
	}

	private void SpawnPickups()
	{
		pickupContainer = new GameObject("Pickups").transform;
	}

	public System.Collections.IEnumerator GeneratePickupsForChunk(Vector2Int coord, Transform chunkTransform)
	{
		if (pickupContainer == null)
		{
			pickupContainer = new GameObject("Pickups").transform;
		}
		UnityEngine.Random.State state = UnityEngine.Random.state;
		UnityEngine.Random.InitState(coord.x * 12345 + coord.y * 54321);
		int num = UnityEngine.Random.Range(25, 50);
		ChunkData orCreateChunkData = WorldGenerator.Instance.GetOrCreateChunkData(coord);
		for (int i = 0; i < num; i++)
		{
			if (!TryFindSpotInChunk(coord, null, awayFromPlayer: false, out var point))
			{
				continue;
			}
			bool flag = false;
			foreach (Vector3 pickedUpItem in orCreateChunkData.pickedUpItems)
			{
				if (Vector3.SqrMagnitude(pickedUpItem - point) < 0.1f)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				ItemDefinition item = Catalog[UnityEngine.Random.Range(0, Catalog.Length)];
				SpawnPickup(item, point, chunkTransform);
			}
			if (!LoadingScreen.Active && i % 3 == 2)
			{
				// Yield mid-chunk without breaking the per-chunk deterministic seed
				UnityEngine.Random.State mine = UnityEngine.Random.state;
				UnityEngine.Random.state = state;
				yield return null;
				state = UnityEngine.Random.state;
				UnityEngine.Random.state = mine;
			}
		}
		UnityEngine.Random.state = state;
	}

	private bool TryFindSpotInChunk(Vector2Int coord, Vector3? avoid, bool awayFromPlayer, out Vector3 point)
	{
		float num = WorldGenerator.Instance.chunkSize;
		float num2 = (float)coord.x * num;
		float num3 = (float)coord.y * num;
		for (int i = 0; i < 10; i++)
		{
			float x = UnityEngine.Random.Range(num2 - num / 2f, num2 + num / 2f);
			float z = UnityEngine.Random.Range(num3 - num / 2f, num3 + num / 2f);
			float num4 = ((WorldGenerator.Instance != null) ? WorldGenerator.Instance.GetHeightAt(x, z) : 0f);
			Vector3 vector = new Vector3(x, num4, z);
			float num5 = ((WorldGenerator.Instance != null) ? WorldGenerator.Instance.waterLevel : 0f);
			if (num4 < num5 + 1f)
			{
				continue;
			}
			bool flag = false;
			foreach (QuestZone allZone in QuestZone.GetAllZones())
			{
				Collider component = allZone.GetComponent<Collider>();
				if (component != null && component.bounds.Contains(vector))
				{
					flag = true;
					break;
				}
			}
			if (!flag && (!avoid.HasValue || !(HorizontalDistance(vector, avoid.Value) < respawnMinMoveDistance)) && (!awayFromPlayer || !(player != null) || !(HorizontalDistance(vector, player.position) < respawnMinPlayerDistance)))
			{
				point = vector;
				return true;
			}
		}
		point = default(Vector3);
		return false;
	}

    // ---- Pickup pooling ----
    // Chunk pickups are parked here (per item id) when their chunk unloads and reused by the next chunk,
    // instead of CreatePrimitive + Destroy(collider) + AddComponent every time.
    private readonly Dictionary<string, Stack<GameObject>> pickupPool = new Dictionary<string, Stack<GameObject>>();
    private readonly Dictionary<GameObject, Pickup> pickupByGo = new Dictionary<GameObject, Pickup>();
    private int pooledPickupCount;
    private const int MaxPooledPickups = 5000;

    private void SpawnPickup(ItemDefinition item, Vector3 groundPoint, Transform parentOverride = null)
    {
        GameObject gameObject = null;
        if (pickupPool.TryGetValue(item.Id, out Stack<GameObject> pool))
        {
            while (pool.Count > 0 && gameObject == null)
            {
                gameObject = pool.Pop();
                pooledPickupCount--;
            }
        }
        bool reused = gameObject != null;
        if (!reused)
        {
            gameObject = GameObject.CreatePrimitive(item.Shape);
            gameObject.name = "Pickup_" + item.DisplayName;
            UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
        }
        gameObject.transform.SetParent((parentOverride != null) ? parentOverride : pickupContainer, worldPositionStays: false);
        gameObject.transform.localScale = item.Scale;
        gameObject.transform.rotation = Quaternion.Euler(item.Rotation);
        gameObject.transform.position = groundPoint;
        if (!reused) SetToonMaterial(gameObject, item.Color);
        Pickup pickup = new Pickup
        {
            owner = this,
            go = gameObject,
            item = item,
            baseY = groundPoint.y,
            phase = UnityEngine.Random.Range(0f, MathF.PI * 2f)
        };
        PickupBillboard billboard = gameObject.GetComponent<PickupBillboard>();
        if (billboard == null) billboard = gameObject.AddComponent<PickupBillboard>();
        billboard.Initialize("to pick up " + item.DisplayName);
        pickups.Add(pickup);
        pickupByGo[gameObject] = pickup;
        InteractionManager.Register(pickup);
    }

    /// <summary>
    /// Called when a chunk unloads. Returns true if this object was one of our uncollected pickups and has been
    /// taken into the pool (caller must NOT destroy it).
    /// </summary>
    public bool TryRecyclePickup(GameObject go, Transform parking)
    {
        if (go == null || !pickupByGo.TryGetValue(go, out Pickup pickup)) return false;

        pickupByGo.Remove(go);
        pickups.Remove(pickup);
        InteractionManager.Unregister(pickup);

        if (pooledPickupCount >= MaxPooledPickups)
        {
            Destroy(go);
            return true;
        }

        go.transform.SetParent(parking, false);
        if (!pickupPool.TryGetValue(pickup.item.Id, out Stack<GameObject> pool))
        {
            pool = new Stack<GameObject>();
            pickupPool[pickup.item.Id] = pool;
        }
        pool.Push(go);
        pooledPickupCount++;
        return true;
    }

	private void SpawnResourceDrop(ItemDefinition item, Vector3 groundPoint)
	{
		GameObject gameObject = GameObject.CreatePrimitive(item.Shape);
		gameObject.name = "Drop_" + item.DisplayName;
		gameObject.transform.SetParent(pickupContainer, worldPositionStays: false);
		UnityEngine.Object.Destroy(gameObject.GetComponent<Collider>());
		gameObject.transform.rotation = Quaternion.Euler(item.Rotation);
		float num = groundPoint.y + 1.2f;
		gameObject.transform.position = new Vector3(groundPoint.x, num, groundPoint.z);
		SetToonMaterial(gameObject, item.Color);
		Renderer component = gameObject.GetComponent<Renderer>();
		if (component != null && component.material != null)
		{
			component.material.SetColor("_RimColor", new Color(1f, 0.95f, 0.8f, 1f));
			component.material.SetFloat("_RimIntensity", 12f);
			component.material.SetFloat("_RimPower", 4f);
		}
		ResourceDrop item2 = new ResourceDrop
		{
			owner = this,
			go = gameObject,
			item = item,
			baseY = num,
			phase = UnityEngine.Random.Range(0f, MathF.PI * 2f),
			spawnTime = Time.time,
			startScale = item.Scale
		};
		gameObject.transform.localScale = Vector3.zero;
		resourceDrops.Add(item2);
	}

	public void DropItem(string id, int amount, Vector3 position)
	{
		ItemDefinition definition = GetDefinition(id);
		if (definition == null)
		{
			return;
		}
		for (int i = 0; i < amount; i++)
		{
			Vector3 vector = new Vector3(UnityEngine.Random.Range(-1.5f, 1.5f), 0.5f, UnityEngine.Random.Range(-1.5f, 1.5f));
			Vector3 groundPoint = position + vector;
			if (WorldGenerator.Instance != null)
			{
				groundPoint.y = WorldGenerator.Instance.GetHeightAt(groundPoint.x, groundPoint.z);
			}
			SpawnResourceDrop(definition, groundPoint);
		}
	}

	private bool TryFindSpot(Vector3? avoid, bool awayFromPlayer, out Vector3 point)
	{
		for (int i = 0; i < 30; i++)
		{
			float x = UnityEngine.Random.Range(-500f, 500f);
			float z = UnityEngine.Random.Range(-500f, 500f);
			float num = ((WorldGenerator.Instance != null) ? WorldGenerator.Instance.GetHeightAt(x, z) : 0f);
			Vector3 vector = new Vector3(x, num, z);
			float num2 = ((WorldGenerator.Instance != null) ? WorldGenerator.Instance.waterLevel : 0f);
			if (num < num2 + 1f)
			{
				continue;
			}
			bool flag = false;
			foreach (QuestZone allZone in QuestZone.GetAllZones())
			{
				Collider component = allZone.GetComponent<Collider>();
				if (component != null && component.bounds.Contains(vector))
				{
					flag = true;
					break;
				}
			}
			if (!flag && (!avoid.HasValue || !(HorizontalDistance(vector, avoid.Value) < respawnMinMoveDistance)) && (!awayFromPlayer || !(player != null) || !(HorizontalDistance(vector, player.position) < respawnMinPlayerDistance)))
			{
				point = vector;
				return true;
			}
		}
		point = default(Vector3);
		return false;
	}

	private static float HorizontalDistance(Vector3 a, Vector3 b)
	{
		a.y = 0f;
		b.y = 0f;
		return Vector3.Distance(a, b);
	}

	private void UpdateRespawns()
	{
		if (ground == null)
		{
			return;
		}
		for (int num = respawns.Count - 1; num >= 0; num--)
		{
			RespawnEntry respawnEntry = respawns[num];
			if (!(Time.time < respawnEntry.time))
			{
				if (TryFindSpot(respawnEntry.avoid, awayFromPlayer: true, out var point))
				{
					SpawnPickup(respawnEntry.item, point);
					respawns.RemoveAt(num);
				}
				else
				{
					respawnEntry.time = Time.time + 2f;
				}
			}
		}
	}

	private void Update()
	{
		UpdateRespawns();
		bool flag = Time.timeScale == 0f;
		if (!flag)
		{
			for (int num = resourceDrops.Count - 1; num >= 0; num--)
			{
				ResourceDrop resourceDrop = resourceDrops[num];
				if (resourceDrop.go == null)
				{
					resourceDrops.RemoveAt(num);
					continue;
				}
				float num2 = Time.time - resourceDrop.spawnTime;
				float num3 = Mathf.Clamp01(num2 * 4f);
				resourceDrop.go.transform.localScale = resourceDrop.startScale * num3;
				bool flag2 = false;
				if (player != null && num2 > 0.5f)
				{
					float num4 = Vector3.Distance(player.position, resourceDrop.go.transform.position);
					if (num4 <= 1.5f)
					{
						CollectDrop(resourceDrop);
						continue;
					}
					if (num4 <= 4f)
					{
						flag2 = true;
						Vector3 target = player.position + Vector3.up * 1f;
						resourceDrop.go.transform.position = Vector3.MoveTowards(resourceDrop.go.transform.position, target, Time.deltaTime * 18f);
					}
				}
				resourceDrop.go.transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
				if (!flag2)
				{
					float y = resourceDrop.baseY + bobHeight + Mathf.Sin(Time.time * bobSpeed + resourceDrop.phase) * bobHeight;
					Vector3 position = resourceDrop.go.transform.position;
					position.y = y;
					resourceDrop.go.transform.position = position;
				}
			}
		}
		bool anyOpen = WorldPanels.AnyOpen;
		if (hotbarRoot != null)
		{
			hotbarRoot.gameObject.SetActive(!NPCSpawner.IsTalking);
			
            // Only slide the hotbar to the bottom of the screen if the inventory is closed!
            if (!panelOpen)
            {
			    float b = ((anyOpen || PauseMenu.IsPaused) ? (-100f) : 20f);
			    Vector2 anchoredPosition = hotbarRoot.anchoredPosition;
			    anchoredPosition.y = Mathf.Lerp(anchoredPosition.y, b, Time.unscaledDeltaTime * 15f);
			    hotbarRoot.anchoredPosition = anchoredPosition;
            }
		}
		if (!flag && !NPCSpawner.IsTalking && (!WorldPanels.AnyOpen || panelOpen) && (Keybinds.WasPressed(GameAction.Inventory) || (Keyboard.current != null && (Keyboard.current.iKey.wasPressedThisFrame || Keyboard.current.tabKey.wasPressedThisFrame)) || (panelOpen && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)))
		{
			TogglePanel();
		}
		if (!flag && !NPCSpawner.IsTalking)
		{
			if (Keybinds.WasPressed(GameAction.Hotbar1))
			{
				SelectHotbar(0);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar2))
			{
				SelectHotbar(1);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar3))
			{
				SelectHotbar(2);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar4))
			{
				SelectHotbar(3);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar5))
			{
				SelectHotbar(4);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar6))
			{
				SelectHotbar(5);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar7))
			{
				SelectHotbar(6);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar8))
			{
				SelectHotbar(7);
			}
			if (Keybinds.WasPressed(GameAction.Hotbar9))
			{
				SelectHotbar(8);
			}
			float num5 = ((Mouse.current != null) ? Mouse.current.scroll.ReadValue().y : 0f);
			if (num5 > 0f)
			{
				SelectHotbar((SelectedHotbarSlot - 1 + 9) % 9);
			}
			if (num5 < 0f)
			{
				SelectHotbar((SelectedHotbarSlot + 1) % 9);
			}
			if (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !panelOpen)
			{
				ConsumeSelected();
			}
		}
	}

	public void ConsumeSelected()
	{
		while (itemOrder.Count < 36) itemOrder.Add("");
		string itemId = itemOrder[SelectedHotbarSlot];
		
		if (string.IsNullOrEmpty(itemId) || GetCount(itemId) <= 0)
		{
			return;
		}

		ItemDefinition itemDefinition = GetDefinition(itemId);
		if (itemDefinition == null) return;

		float num = 0f;
		float healValue = 0f;
		switch (itemDefinition.Id.ToLower())
		{
		case "apple":
			num = 15f;
			healValue = 5f;
			break;
		case "mushroom":
			num = 10f;
			break;
		case "berries":
		case "berry":
			num = 15f;
			healValue = 5f;
			break;
		case "wheat":
			num = 10f;
			break;
		case "carrot":
			num = 20f;
			healValue = 10f;
			break;
		case "meat":
			num = 40f;
			healValue = 25f;
			break;
		}
		if (num > 0f && PlayerHealth.Instance != null)
		{
			if (PlayerHealth.Instance.CurrentHunger >= PlayerHealth.Instance.maxHunger && PlayerHealth.Instance.CurrentHealth >= PlayerHealth.Instance.maxHealth)
			{
				Notify("I am too full to eat!");
				return;
			}
			PlayerHealth.Instance.Eat(num, healValue);
			RemoveItem(itemDefinition.Id);
			Notify("Ate " + itemDefinition.DisplayName);
		}
	}

	private void SelectHotbar(int index)
	{
		SelectedHotbarSlot = index;
		RefreshHotbar();
		gameObject.SetActive(true);
		hotbarRoot.gameObject.SetActive(true);
		notifRoot.gameObject.SetActive(true);
		if (ToolManager.Instance == null) return;

		while (itemOrder.Count < 36) itemOrder.Add("");
		string itemId = itemOrder[index];

		if (!string.IsNullOrEmpty(itemId) && GetCount(itemId) > 0 && Enum.TryParse<ToolManager.ToolType>(itemId, ignoreCase: true, out var result))
		{
			ToolManager.Instance.Equip(result);
		}
		else
		{
			ToolManager.Instance.Equip(ToolManager.ToolType.None);
		}
	}

	private void Collect(Pickup pickup)
	{
		pickups.Remove(pickup);
        pickupByGo.Remove(pickup.go);
		InteractionManager.Unregister(pickup);
		Vector3 position = pickup.go.transform.position;
		if (WorldGenerator.Instance != null)
		{
			Vector2Int chunkCoord = WorldGenerator.Instance.GetChunkCoord(position);
			WorldGenerator.Instance.GetOrCreateChunkData(chunkCoord).pickedUpItems.Add(position);
		}
		PickupBillboard component = pickup.go.GetComponent<PickupBillboard>();
		if (component != null)
		{
			UnityEngine.Object.Destroy(component);
		}
		Renderer component2 = pickup.go.GetComponent<Renderer>();
		if (component2 != null && component2.material != null)
		{
			component2.material.SetColor("_RimColor", new Color(1f, 0.95f, 0.8f, 1f));
			component2.material.SetFloat("_RimIntensity", 12f);
			component2.material.SetFloat("_RimPower", 4f);
		}
		ResourceDrop item = new ResourceDrop
		{
			owner = this,
			go = pickup.go,
			item = pickup.item,
			baseY = pickup.baseY,
			phase = pickup.phase,
			spawnTime = Time.time - 2f,
			startScale = pickup.item.Scale
		};
		resourceDrops.Add(item);
		if (respawnDelay > 0f && ground != null)
		{
			respawns.Add(new RespawnEntry
			{
				time = Time.time + respawnDelay,
				item = pickup.item,
				avoid = position
			});
		}
	}

	private void CollectDrop(ResourceDrop drop)
	{
		resourceDrops.Remove(drop);
		UnityEngine.Object.Destroy(drop.go);
		AddItem(drop.item.Id);
	}

	public bool AddItem(string id, int amount = 1)
	{
		ItemDefinition definition = GetDefinition(id);
		if (definition == null || amount <= 0)
		{
			return false;
		}
		counts[id] = GetCount(id) + amount;
		if (!itemOrder.Contains(id))
		{
            int emptyIndex = itemOrder.IndexOf("");
            if (emptyIndex == -1) emptyIndex = itemOrder.IndexOf(null);
            if (emptyIndex >= 0 && emptyIndex < 36) itemOrder[emptyIndex] = id;
            else if (itemOrder.Count < 36) itemOrder.Add(id);
		}
		Notify((amount > 1) ? $"+ {definition.DisplayName} x{amount}" : ("+ " + definition.DisplayName));
		RefreshPanel();
		SelectHotbar(SelectedHotbarSlot);
		Changed?.Invoke();
		return true;
	}

	public bool RemoveItem(string id, int amount = 1)
	{
		if (amount <= 0 || GetCount(id) < amount)
		{
			return false;
		}
		counts[id] -= amount;
		if (counts[id] == 0)
		{
            int idx = itemOrder.IndexOf(id);
            if (idx >= 0) itemOrder[idx] = "";
		}
		ItemDefinition definition = GetDefinition(id);
		if (definition != null)
		{
			Notify((amount > 1) ? $"- {definition.DisplayName} x{amount}" : ("- " + definition.DisplayName));
		}
		RefreshPanel();
		SelectHotbar(SelectedHotbarSlot);
		Changed?.Invoke();
		return true;
	}

	public int GetCount(string id)
	{
		if (!counts.TryGetValue(id, out var value))
		{
			return 0;
		}
		return value;
	}

	private void TogglePanel()
	{
		panelOpen = !panelOpen;
		PanelOpen = panelOpen;
		if (panelOpen)
		{
			InteractionManager.Block();
			UIBillboardHider.SetHidden(true);
		}
		else
		{
			InteractionManager.Unblock();
			UIBillboardHider.SetHidden(false);
		}

        Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
        CameraController camCtrl = UnityEngine.Object.FindAnyObjectByType<CameraController>();
        Camera mainCam = (camCtrl != null) ? camCtrl.GetComponent<Camera>() : Camera.main;

        if (canvas != null && mainCam != null)
        {
            if (panelOpen)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                
                // CRITICAL: Assign the event camera so GraphicRaycaster can calculate 3D mouse intersections!
                canvas.worldCamera = mainCam;
                
                // Position the canvas in the 3D world, relative to the player
                var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>().transform;
                canvas.transform.SetParent(player);
                
                // Place it slightly forward and to the right of the player
                canvas.transform.localPosition = new Vector3(0.85f, 1.35f, 1.2f);
                
                // Angle it slightly inward towards the player
                canvas.transform.localEulerAngles = new Vector3(0f, 14f, 0f);
                
                // Scale it up a bit
                canvas.transform.localScale = new Vector3(0.0028f, 0.0028f, 0.0028f);
                
                panelRoot.GetComponent<RectTransform>().localEulerAngles = Vector3.zero;
                panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;
                
                // Move Hotbar to be part of the angled panel
                hotbarRoot.SetAsLastSibling();
                hotbarRoot.anchorMin = new Vector2(0.5f, 0f);
                hotbarRoot.anchorMax = new Vector2(0.5f, 0f);
                hotbarRoot.pivot = new Vector2(0.5f, 0f);
                hotbarRoot.anchoredPosition = new Vector2(0f, 60f);
                hotbarRoot.localEulerAngles = Vector3.zero;
                
                // Apply ZTest Always material
                if (cachedOverlayMat != null) {
                    foreach (UnityEngine.UI.Graphic g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) {
                        g.material = cachedOverlayMat;
                    }
                }
            }
            else
            {
                foreach (UnityEngine.UI.Graphic g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) {
                    g.material = null; // Revert to default
                }
                canvas.transform.SetParent(null);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                panelRoot.GetComponent<RectTransform>().localEulerAngles = Vector3.zero;
                
                // Move Hotbar back to screen bottom
                hotbarRoot.SetAsLastSibling();
                hotbarRoot.anchorMin = new Vector2(0.5f, 0f);
                hotbarRoot.anchorMax = new Vector2(0.5f, 0f);
                hotbarRoot.pivot = new Vector2(0.5f, 0f);
                hotbarRoot.anchoredPosition = new Vector2(0f, 20f);
                hotbarRoot.localEulerAngles = Vector3.zero;
            }
        }

        if (camCtrl != null)
        {
            camCtrl.IsInMenu = panelOpen;
        }

		panelTitle.text = "INVENTORY  [" + Keybinds.KeyName(GameAction.Inventory) + "]";
		panelRoot.SetActive(panelOpen);
		Cursor.lockState = ((!panelOpen) ? CursorLockMode.Locked : CursorLockMode.None);
		Cursor.visible = panelOpen;
	}

	public void Notify(string message)
	{
		if (!(notifRoot == null))
		{
			RectTransform rectTransform = UIKit.CreateImage(notifRoot, "Notification", new Color(0f, 0f, 0f, 0.6f));
			UIKit.Size(rectTransform, 340f, 46f);
			UIKit.CreateText(rectTransform, "Text", message, 28, Color.white);
			StartCoroutine(DestroyAfterRealtime(rectTransform.gameObject, 3f));
		}
	}

	private IEnumerator DestroyAfterRealtime(GameObject go, float seconds)
	{
		yield return new WaitForSecondsRealtime(seconds);
		if (go != null)
		{
			UnityEngine.Object.Destroy(go);
		}
	}

	private void RefreshHotbar()
	{
		if (hotbarRoot == null)
		{
			return;
		}
		foreach (Transform item in hotbarRoot)
		{
			item.gameObject.SetActive(value: false);
			UnityEngine.Object.Destroy(item.gameObject);
		}
		while (itemOrder.Count < 36) itemOrder.Add("");
		for (int i = 0; i < 9; i++)
		{
			bool num = i == SelectedHotbarSlot;
			RectTransform rectTransform = UIKit.CreateImage(color: num ? new Color(0.3f, 0.35f, 0.45f, 0.95f) : new Color(0.15f, 0.16f, 0.2f, 0.9f), parent: hotbarRoot, objName: "Slot " + i);
            rectTransform.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
            UISlot hotbarSlot = rectTransform.gameObject.AddComponent<UISlot>();
            hotbarSlot.SlotIndex = i;
			UIKit.Size(rectTransform, 70f, 70f);
			if (num)
			{
				rectTransform.localScale = new Vector3(1.15f, 1.15f, 1f);
				Outline outline = rectTransform.gameObject.AddComponent<Outline>();
				outline.effectColor = new Color(1f, 0.8f, 0.2f, 1f);
				outline.effectDistance = new Vector2(3f, -3f);
			}
			string itemId = itemOrder[i];
			if (!string.IsNullOrEmpty(itemId) && GetCount(itemId) > 0)
			{
				ItemDefinition itemDefinition = GetDefinition(itemId);
				if (itemDefinition != null)
				{
				rectTransform.GetComponent<Image>().raycastTarget = true;
				rectTransform.gameObject.AddComponent<UITooltip>().ItemName = itemDefinition.DisplayName;
				RectTransform rectTransform2 = UIKit.CreateItemSwatch(rectTransform, "Swatch", itemDefinition);
					var dd2 = rectTransform2.gameObject.AddComponent<UIDragDrop>();
                    dd2.ItemName = itemDefinition.Id;
                    dd2.ItemColor = itemDefinition.Color;
					rectTransform2.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
				Vector2 anchorMin = (rectTransform2.anchorMax = new Vector2(0.5f, 0.5f));
				rectTransform2.anchorMin = anchorMin;
				rectTransform2.pivot = new Vector2(0.5f, 0.5f);
				rectTransform2.anchoredPosition = new Vector2(0f, 8f);
				rectTransform2.sizeDelta = new Vector2(32f, 32f);
				Text text = UIKit.CreateText(rectTransform, "Count", GetCount(itemDefinition.Id).ToString(), 20, Color.white, TextAnchor.LowerRight);
				text.rectTransform.anchorMin = Vector2.zero;
				text.rectTransform.anchorMax = Vector2.one;
				text.rectTransform.offsetMin = new Vector2(5f, 5f);
				text.rectTransform.offsetMax = new Vector2(-5f, -5f);
			}
			}
			Text text2 = UIKit.CreateText(rectTransform, "Num", (i + 1).ToString(), 16, new Color(1f, 1f, 1f, 0.5f), TextAnchor.UpperLeft);
			text2.rectTransform.anchorMin = Vector2.zero;
			text2.rectTransform.anchorMax = Vector2.one;
			text2.rectTransform.offsetMin = new Vector2(5f, 5f);
			text2.rectTransform.offsetMax = new Vector2(-5f, -5f);
		}
        //ApplyShader();

		// Sync equipped tool with whatever is now in the active slot
		if (ToolManager.Instance != null)
		{
			string activeItem = itemOrder[SelectedHotbarSlot];
			if (!string.IsNullOrEmpty(activeItem) && GetCount(activeItem) > 0 && Enum.TryParse<ToolManager.ToolType>(activeItem, ignoreCase: true, out var result))
			{
				ToolManager.Instance.Equip(result);
			}
			else
			{
				ToolManager.Instance.Equip(ToolManager.ToolType.None);
			}
		}
	}

	private void RefreshPanel()
	{
		if (itemList == null)
		{
			return;
		}
		if (levelText != null)
		{
			levelText.text = $"Level {Level}";
		}
		if (xpText != null)
		{
			xpText.text = $"{XP} / {MaxXP} XP";
		}
		foreach (Transform item in itemList)
		{
			item.gameObject.SetActive(value: false);
			UnityEngine.Object.Destroy(item.gameObject);
		}
		while (itemOrder.Count < 36) itemOrder.Add("");
		for (int idx = 9; idx < 36; idx++)
		{
			string item2 = itemOrder[idx];
			bool hasItem = !string.IsNullOrEmpty(item2) && GetCount(item2) > 0;
			if (hasItem)
			{
				ItemDefinition definition = GetDefinition(item2);
				if (definition != null)
				{
					RectTransform rectTransform = UIKit.CreateImage(itemList, "Slot " + idx, new Color(0.15f, 0.16f, 0.2f, 0.9f));
                    UISlot uiSlot = rectTransform.gameObject.AddComponent<UISlot>();
                    uiSlot.SlotIndex = idx;
					rectTransform.GetComponent<Image>().raycastTarget = true;
					rectTransform.gameObject.AddComponent<UITooltip>().ItemName = definition.DisplayName;
                    
					RectTransform rectTransform2 = UIKit.CreateItemSwatch(rectTransform, "Swatch", definition);
					var dd = rectTransform2.gameObject.AddComponent<UIDragDrop>();
                    dd.ItemName = definition.Id;
                    dd.ItemColor = definition.Color;
					rectTransform2.GetComponent<UnityEngine.UI.Image>().raycastTarget = false;
					Vector2 anchorMin = (rectTransform2.anchorMax = new Vector2(0.5f, 0.5f));
					rectTransform2.anchorMin = anchorMin;
					rectTransform2.pivot = new Vector2(0.5f, 0.5f);
					rectTransform2.anchoredPosition = new Vector2(0f, 8f);
					rectTransform2.sizeDelta = new Vector2(32f, 32f);
					int count = GetCount(definition.Id);
					Text text = UIKit.CreateText(rectTransform, "Count", count.ToString(), 20, Color.white, TextAnchor.LowerRight);
					text.rectTransform.anchorMin = Vector2.zero;
					text.rectTransform.anchorMax = Vector2.one;
					text.rectTransform.offsetMin = new Vector2(5f, 5f);
					text.rectTransform.offsetMax = new Vector2(-5f, -5f);
				}
			}
			else
			{
				RectTransform emptySlot = UIKit.CreateImage(itemList, "Slot " + idx, new Color(0.2f, 0.22f, 0.25f, 0.8f));
                UISlot emptySlotComp = emptySlot.gameObject.AddComponent<UISlot>();
                emptySlotComp.SlotIndex = idx;
				emptySlot.GetComponent<UnityEngine.UI.Image>().raycastTarget = true;
			}
		}
		RefreshHotbar();
		gameObject.SetActive(true);
		hotbarRoot.gameObject.SetActive(true);
		notifRoot.gameObject.SetActive(true);
	}


    private void ApplyShader() {
        if (cachedOverlayMat != null && panelRoot != null) {
            Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas != null) {
                foreach (UnityEngine.UI.Graphic g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) {
                    g.material = cachedOverlayMat;
                }
            }
        }
    }

	private void BuildUI()
	{
		GameObject gameObject = UIKit.CreateCanvas("Inventory UI", 10);
        // Spawn the cursor manager onto the canvas root
        gameObject.AddComponent<UIInventoryCursor>();
		notifRoot = UIKit.CreateRect(gameObject.transform, "Notifications");
		RectTransform rectTransform = notifRoot;
		Vector2 anchorMin = (notifRoot.anchorMax = new Vector2(1f, 1f));
		rectTransform.anchorMin = anchorMin;
		notifRoot.pivot = new Vector2(1f, 1f);
		notifRoot.anchoredPosition = new Vector2(-30f, -30f);
		notifRoot.sizeDelta = new Vector2(340f, 400f);
		UIKit.VLayout(notifRoot.gameObject, 8f, 0, TextAnchor.UpperRight);
		RectTransform rectTransform2 = UIKit.CreateImage(gameObject.transform, "InventoryPanel", new Color(0.09f, 0.1f, 0.13f, 0.95f));
		anchorMin = (rectTransform2.anchorMax = new Vector2(0.5f, 0.5f));
		rectTransform2.anchorMin = anchorMin;
		rectTransform2.pivot = new Vector2(0.5f, 0.5f);
		rectTransform2.anchoredPosition = new Vector2(0f, 0f);
		rectTransform2.sizeDelta = new Vector2(750f, 500f);
		UIKit.VLayout(rectTransform2.gameObject, 12f, 24, TextAnchor.UpperCenter);
		panelTitle = UIKit.CreateText(rectTransform2, "Title", "INVENTORY", 40, Color.white);
		UIKit.Size(panelTitle, 700f, 60f);
		RectTransform rectTransform3 = UIKit.CreateRect(rectTransform2, "XpRow");
		UIKit.HLayout(rectTransform3.gameObject, 10f);
		UIKit.Size(rectTransform3, 700f, 30f);
		levelText = UIKit.CreateText(rectTransform3, "Level", "Level 1", 24, UIKit.Good, TextAnchor.MiddleLeft);
		UIKit.Size(levelText, 200f, 30f);
		xpText = UIKit.CreateText(rectTransform3, "XP", "0/100 XP", 24, new Color(0.8f, 0.8f, 0.8f), TextAnchor.MiddleRight);
		UIKit.Size(xpText, 490f, 30f);
		RectTransform rectTransform4 = UIKit.CreateRect(rectTransform2, "ScrollRoot");
		UIKit.Size(rectTransform4, 700f, 320f);
        // Add transparent image to catch scroll drags!
        UnityEngine.UI.Image scrollBg = rectTransform4.gameObject.AddComponent<UnityEngine.UI.Image>();
        scrollBg.color = new Color(0,0,0,0);
        scrollBg.raycastTarget = true;
		ScrollRect scrollRect = rectTransform4.gameObject.AddComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.scrollSensitivity = 35f;
		RectTransform rectTransform5 = UIKit.CreateRect(rectTransform4, "Viewport");
		rectTransform5.anchorMin = Vector2.zero;
		rectTransform5.anchorMax = Vector2.one;
		rectTransform5.sizeDelta = Vector2.zero;
		rectTransform5.gameObject.AddComponent<RectMask2D>();
		scrollRect.viewport = rectTransform5;
		itemList = UIKit.CreateRect(rectTransform5, "Items");
		itemList.anchorMin = new Vector2(0f, 1f);
		itemList.anchorMax = new Vector2(1f, 1f);
		itemList.pivot = new Vector2(0.5f, 1f);
		itemList.sizeDelta = new Vector2(0f, 0f);
		scrollRect.content = itemList;
		GridLayoutGroup gridLayoutGroup = itemList.gameObject.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.cellSize = new Vector2(70f, 70f);
		gridLayoutGroup.spacing = new Vector2(8f, 8f);
		gridLayoutGroup.padding = new RectOffset(4, 4, 4, 4);
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
		ContentSizeFitter contentSizeFitter = itemList.gameObject.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		rectTransform2.anchoredPosition = new Vector2(250f, 0f);
		panelRoot = rectTransform2.gameObject;
		panelRoot.SetActive(value: false);
		hotbarRoot = UIKit.CreateRect(gameObject.transform, "Hotbar");
		RectTransform rectTransform6 = hotbarRoot;
		anchorMin = (hotbarRoot.anchorMax = new Vector2(0.5f, 0f));
		rectTransform6.anchorMin = anchorMin;
		hotbarRoot.pivot = new Vector2(0.5f, 0f);
		hotbarRoot.anchoredPosition = new Vector2(0f, 20f);
		hotbarRoot.sizeDelta = new Vector2(750f, 80f);
		UIKit.HLayout(hotbarRoot.gameObject, 8f, 10);
		hotbarRoot.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
		RefreshPanel();
	}
}


// --- Quest.cs ---

[Serializable]
public class Quest
{
	public string GiverName;

	public string ItemId;

	public int Amount;

	public int RewardCoins;

	public QuestState State;

	public QuestType Type;

	public string TargetNpcName;

	public string LocationName;

	public string ExploreZoneId;

	public bool LocationReached;

	public string ItemPhrase
	{
		get
		{
			if (Type == QuestType.Explore)
			{
				return "notes from " + LocationName;
			}
			if (Type == QuestType.TalkTo)
			{
				return "word from " + TargetNpcName;
			}
			ItemDefinition definition = Inventory.GetDefinition(ItemId);
			string text = ((definition != null && !string.IsNullOrEmpty(definition.DisplayName)) ? definition.DisplayName : ItemId).ToLower();
			if (string.IsNullOrEmpty(text))
			{
				return "a favour";
			}
			if (Amount > 1)
			{
				return Amount + " " + text + "s";
			}
			return (("aeiou".IndexOf(text[0]) >= 0) ? "an " : "a ") + text;
		}
	}
}


// --- QuestState.cs ---
public enum QuestState
{
	Available,
	Active,
	Completed
}


// --- QuestType.cs ---
public enum QuestType
{
	Fetch,
	TalkTo,
	Explore
}


// --- QuestLog.cs ---

public class QuestLog : MonoBehaviour
{
	private static readonly string[] QuestItems = new string[4] { "apple", "mushroom", "gem", "key" };

	public static readonly string[] ExploreZones = new string[6] { "the old mill", "the hilltop", "the mossy hollow", "the stone circle", "the lakeshore", "the ancient tree" };

	private readonly List<Quest> active = new List<Quest>();

	private GameObject panelRoot;

	private RectTransform list;

	private Transform player;

	private Quest trackedQuest;

	private Transform trackedNpc;

	private int groundMask;

	private const int TrailSegments = 28;

	private Texture2D footprintTexture;

	private ParticleSystem footprintSystem;

	private float wispSpawnTimer;

	private MaterialPropertyBlock highlightBlock;

	private Transform highlightedNpc;

	private Shader toonShader;

	public static QuestLog Instance { get; private set; }

	public int CompletedCount { get; private set; }

	public static Quest CreateRandomQuest(string giverName)
	{
		float value = Random.value;
		if (value < 0.6f)
		{
			string text = QuestItems[Random.Range(0, QuestItems.Length)];
			int num = Inventory.GetDefinition(text)?.Value ?? 1;
			int num2 = ((num >= 20) ? 1 : ((num >= 8) ? 2 : 3));
			int num3 = Random.Range(1, num2 + 1);
			int rewardCoins = Mathf.Max(3, Mathf.RoundToInt((float)(num * num3) * Random.Range(1.5f, 2.5f)));
			return new Quest
			{
				GiverName = giverName,
				ItemId = text,
				Amount = num3,
				RewardCoins = rewardCoins,
				State = QuestState.Available,
				Type = QuestType.Fetch
			};
		}
		if (value < 0.8f)
		{
			List<string> allNames = NPCIdentity.GetAllNames();
			string text2 = giverName;
			if (allNames.Count > 0)
			{
				for (int i = 0; i < 10; i++)
				{
					if (!(text2 == giverName))
					{
						break;
					}
					text2 = allNames[Random.Range(0, allNames.Count)];
				}
			}
			return new Quest
			{
				GiverName = giverName,
				ItemId = "",
				Amount = 1,
				RewardCoins = Random.Range(8, 20),
				State = QuestState.Available,
				Type = QuestType.TalkTo,
				TargetNpcName = text2
			};
		}
		string text3 = ExploreZones[Random.Range(0, ExploreZones.Length)];
		string exploreZoneId = text3.Replace(" ", "_").ToLower();
		return new Quest
		{
			GiverName = giverName,
			ItemId = "",
			Amount = 1,
			RewardCoins = Random.Range(10, 25),
			State = QuestState.Available,
			Type = QuestType.Explore,
			LocationName = text3,
			ExploreZoneId = exploreZoneId
		};
	}

	private void Awake()
	{
		Instance = this;
		PauseMenu.EnsureEventSystem();
		BuildUI();
	}

	private void Start()
	{
		if (Inventory.Instance != null)
		{
			Inventory.Instance.Changed += Refresh;
		}
		PlayerController playerController = Object.FindAnyObjectByType<PlayerController>();
		if (playerController != null)
		{
			player = playerController.transform;
		}
		groundMask = LayerMask.GetMask("Ground");
		BuildTrail();
	}

	private void OnDestroy()
	{
		if (Inventory.Instance != null)
		{
			Inventory.Instance.Changed -= Refresh;
		}
		if (Instance == this)
		{
			Instance = null;
		}
		if (footprintSystem != null)
		{
			Object.Destroy(footprintSystem.gameObject);
		}
		if (footprintTexture != null)
		{
			Object.Destroy(footprintTexture);
		}
	}

	private void Update()
	{
		UpdateTrail();
	}

	public bool HasEnough(Quest quest)
	{
		if (quest == null)
		{
			return false;
		}
		if (quest.Type == QuestType.Fetch)
		{
			if (Inventory.Instance != null)
			{
				return Inventory.Instance.GetCount(quest.ItemId) >= quest.Amount;
			}
			return false;
		}
		return quest.LocationReached;
	}

	public void Accept(Quest quest)
	{
		if (quest != null && quest.State == QuestState.Available)
		{
			quest.State = QuestState.Active;
			active.Add(quest);
			Announce("Quest started: " + quest.GiverName);
			SetTracked(quest);
			Refresh();
		}
	}

	public bool TryComplete(Quest quest)
	{
		if (quest == null || quest.State != QuestState.Active || !HasEnough(quest))
		{
			return false;
		}
		Inventory instance = Inventory.Instance;
		if (quest.Type == QuestType.Fetch)
		{
			instance.RemoveItem(quest.ItemId, quest.Amount);
		}
		quest.State = QuestState.Completed;
		active.Remove(quest);
		CompletedCount++;
		instance.AddItem("coin", quest.RewardCoins);
		instance.AddXP(quest.RewardCoins * 2);
		Announce("Quest complete: " + quest.GiverName);
		if (trackedQuest == quest)
		{
			trackedNpc = null;
		}
		Refresh();
		return true;
	}

	public void OnZoneReached(string zoneId, string zoneName)
	{
		bool flag = false;
		foreach (Quest item in active)
		{
			if (item.Type == QuestType.Explore && item.ExploreZoneId == zoneId && !item.LocationReached)
			{
				item.LocationReached = true;
				flag = true;
			}
		}
		if (flag)
		{
			Announce("Discovered: " + zoneName);
			Refresh();
		}
	}

	public void OnNpcTalkedTo(string npcName)
	{
		bool flag = false;
		foreach (Quest item in active)
		{
			if (item.Type == QuestType.TalkTo && item.TargetNpcName == npcName && !item.LocationReached)
			{
				item.LocationReached = true;
				flag = true;
			}
		}
		if (flag)
		{
			Announce("Spoke with " + npcName + "!");
			Refresh();
		}
	}

	public void SetTracked(Quest quest)
	{
		trackedQuest = quest;
		trackedNpc = null;
		Refresh();
	}

	public void ForceTrackTarget(Transform target)
	{
		trackedQuest = null;
		trackedNpc = target;
		Refresh();
		Announce("Scent trail updated!");
	}

	private void ResolveTrackedNpc()
	{
		if (trackedQuest == null)
		{
			trackedNpc = null;
			return;
		}
		if (trackedQuest.Type == QuestType.Explore && trackedQuest.State == QuestState.Active && !trackedQuest.LocationReached)
		{
			trackedNpc = QuestZone.FindById(trackedQuest.ExploreZoneId);
			return;
		}
		string text = ((trackedQuest.Type == QuestType.TalkTo && trackedQuest.State == QuestState.Active && !trackedQuest.LocationReached) ? trackedQuest.TargetNpcName : trackedQuest.GiverName);
		trackedNpc = NPCIdentity.FindByName(text);
	}

	private void Announce(string message)
	{
		if (Inventory.Instance != null)
		{
			Inventory.Instance.Notify(message);
		}
	}

	private void BuildTrail()
	{
		footprintTexture = new Texture2D(64, 64, TextureFormat.RGBA32, mipChain: false);
		footprintTexture.wrapMode = TextureWrapMode.Clamp;
		Color a = new Color(1f, 0.8f, 0.2f, 0f);
		Color b = new Color(1f, 0.8f, 0.2f, 0.8f);
		for (int i = 0; i < 64; i++)
		{
			for (int j = 0; j < 64; j++)
			{
				float num = (float)(j - 32) / 32f;
				float num2 = (float)(i - 32) / 32f;
				float a2 = num * num * 2.5f + (num2 + 0.4f) * (num2 + 0.4f) * 1.5f;
				float a3 = num * num * 1.8f + (num2 - 0.3f) * (num2 - 0.3f) * 1.2f;
				float b2 = num * num * 3f + num2 * num2 * 2f;
				float t = Mathf.Min(a2, Mathf.Min(a3, b2));
				float t2 = 1f - Mathf.SmoothStep(0.15f, 0.5f, t);
				footprintTexture.SetPixel(j, i, Color.Lerp(a, b, t2));
			}
		}
		footprintTexture.Apply();
		GameObject gameObject = new GameObject("Quest Footprints");
		Object.DontDestroyOnLoad(gameObject);
		footprintSystem = gameObject.AddComponent<ParticleSystem>();
		ParticleSystem.MainModule main = footprintSystem.main;
		main.loop = false;
		main.playOnAwake = false;
		main.simulationSpace = ParticleSystemSimulationSpace.World;
		main.startLifetime = 2.5f;
		main.startSize = 0.45f;
		main.startColor = Color.white;
		main.startSpeed = 0f;
		ParticleSystem.EmissionModule emission = footprintSystem.emission;
		emission.enabled = false;
		ParticleSystem.ShapeModule shape = footprintSystem.shape;
		shape.enabled = false;
		ParticleSystem.ColorOverLifetimeModule colorOverLifetime = footprintSystem.colorOverLifetime;
		colorOverLifetime.enabled = true;
		Gradient gradient = new Gradient();
		gradient.SetKeys(new GradientColorKey[2]
		{
			new GradientColorKey(Color.white, 0f),
			new GradientColorKey(Color.white, 1f)
		}, new GradientAlphaKey[4]
		{
			new GradientAlphaKey(0f, 0f),
			new GradientAlphaKey(1f, 0.1f),
			new GradientAlphaKey(1f, 0.6f),
			new GradientAlphaKey(0f, 1f)
		});
		colorOverLifetime.color = gradient;
		ParticleSystemRenderer component = gameObject.GetComponent<ParticleSystemRenderer>();
		Shader shader = Shader.Find("Sprites/Default");
		if (shader == null)
		{
			shader = Shader.Find("Unlit/Transparent");
		}
		component.material = new Material((shader != null) ? shader : Shader.Find("Standard"));
		component.material.mainTexture = footprintTexture;
		component.renderMode = ParticleSystemRenderMode.HorizontalBillboard;
		highlightBlock = new MaterialPropertyBlock();
	}

	private void ClearHighlight()
	{
		if (highlightedNpc != null)
		{
			Renderer[] componentsInChildren = highlightedNpc.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].SetPropertyBlock(null);
			}
			highlightedNpc = null;
		}
	}

	private void UpdateTrail()
	{
		if (trackedQuest != null && trackedNpc == null)
		{
			ResolveTrackedNpc();
		}
		if (trackedNpc == null || player == null || footprintSystem == null)
		{
			ClearHighlight();
			return;
		}
		if (highlightedNpc != null && highlightedNpc != trackedNpc)
		{
			ClearHighlight();
		}
		if (Vector3.Distance(player.position, trackedNpc.position) < 3f)
		{
			highlightedNpc = trackedNpc;
			float value = 12f + Mathf.Sin(Time.time * 8f) * 6f;
			highlightBlock.SetColor("_RimColor", new Color(1f, 0.85f, 0.2f, 1f));
			highlightBlock.SetFloat("_RimIntensity", value);
			highlightBlock.SetFloat("_RimPower", 6f);
			Renderer[] componentsInChildren = highlightedNpc.GetComponentsInChildren<Renderer>();
			for (int i = 0; i < componentsInChildren.Length; i++)
			{
				componentsInChildren[i].SetPropertyBlock(highlightBlock);
			}
		}
		else
		{
			ClearHighlight();
			wispSpawnTimer -= Time.deltaTime;
			if (wispSpawnTimer <= 0f)
			{
				wispSpawnTimer = 1.6f;
				StartCoroutine(WispRoutine(player.position, trackedNpc));
			}
		}
	}

	private IEnumerator WispRoutine(Vector3 startPos, Transform target)
	{
		Vector3 currentPos = startPos;
		float speed = 10f;
		float distanceTravelled = 0.6f;
		float stepSize = 0.6f;
		bool isRightFoot = false;
		GameObject wispHead = GameObject.CreatePrimitive(PrimitiveType.Sphere);
		wispHead.transform.localScale = Vector3.one * 0.15f;
		Object.Destroy(wispHead.GetComponent<Collider>());
		MeshRenderer component = wispHead.GetComponent<MeshRenderer>();
		Shader shader = Shader.Find("Unlit/Color");
		component.material = new Material((shader != null) ? shader : Shader.Find("Standard"));
		component.material.color = new Color(1f, 0.9f, 0.5f, 1f);
		TrailRenderer trailRenderer = wispHead.AddComponent<TrailRenderer>();
		trailRenderer.time = 0.3f;
		trailRenderer.startWidth = 0.15f;
		trailRenderer.endWidth = 0f;
		trailRenderer.material = component.material;
		trailRenderer.startColor = new Color(1f, 0.8f, 0.2f, 0.6f);
		trailRenderer.endColor = new Color(1f, 0.8f, 0.2f, 0f);
		float spawnTime = Time.time;
		while (target != null)
		{
			Vector3 vector = target.position - currentPos;
			vector.y = 0f;
			if (vector.magnitude < 1f || Time.time - spawnTime > 15f)
			{
				break;
			}
			vector.Normalize();
			Vector3 vector2 = vector * (speed * Time.deltaTime);
			currentPos += vector2;
			distanceTravelled += vector2.magnitude;
			if (Physics.Raycast(new Ray(new Vector3(currentPos.x, currentPos.y + 50f, currentPos.z), Vector3.down), out var hitInfo, 100f, groundMask))
			{
				currentPos.y = hitInfo.point.y;
			}
			wispHead.transform.position = currentPos + Vector3.up * (0.6f + Mathf.Sin(Time.time * 12f) * 0.15f);
			if (distanceTravelled >= stepSize)
			{
				distanceTravelled -= stepSize;
				isRightFoot = !isRightFoot;
				Vector3 normalized = Vector3.Cross(Vector3.up, vector).normalized;
				float num = 0.08f;
				Vector3 position = currentPos + normalized * (isRightFoot ? num : (0f - num));
				position.y += 0.02f;
				ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
				{
					position = position,
					rotation3D = new Vector3(0f, Quaternion.LookRotation(vector).eulerAngles.y, 0f)
				};
				footprintSystem.Emit(emitParams, 1);
			}
			yield return null;
		}
		float fade = 1f;
		while (fade > 0f && wispHead != null)
		{
			fade -= Time.deltaTime * 3f;
			wispHead.transform.localScale = Vector3.one * (0.15f * fade);
			yield return null;
		}
		if (wispHead != null)
		{
			Object.Destroy(wispHead);
		}
	}

	private void BuildUI()
	{
		RectTransform rectTransform = UIKit.CreateImage(UIKit.CreateCanvas("Quest UI", 4).transform, "QuestPanel", UIKit.PanelColor);
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(1f, 1f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(1f, 1f);
		rectTransform.anchoredPosition = new Vector2(-30f, -30f);
		rectTransform.sizeDelta = new Vector2(480f, 0f);
		UIKit.VLayout(rectTransform.gameObject, 8f, 18, TextAnchor.UpperLeft);
		UIKit.FitVertical(rectTransform.gameObject);
		rectTransform.localEulerAngles = new Vector3(5f, -10f, 1f);
		Text text = UIKit.CreateText(rectTransform, "Title", "QUESTS", 26, UIKit.Accent, TextAnchor.MiddleLeft);
		text.fontStyle = FontStyle.Bold;
		UIKit.Size(text, 444f, 34f);
		list = UIKit.CreateRect(rectTransform, "List");
		UIKit.VLayout(list.gameObject, 8f, 0, TextAnchor.UpperLeft);
		UIKit.Size(list, 444f, -1f);
		panelRoot = rectTransform.gameObject;
		Refresh();
	}

	private void Refresh()
	{
		if (list == null)
		{
			return;
		}
		foreach (Transform item in list)
		{
			item.gameObject.SetActive(value: false);
			Object.Destroy(item.gameObject);
		}
		panelRoot.SetActive(active.Count > 0);
		foreach (Quest item2 in active)
		{
			Quest captured = item2;
			bool flag = item2 == trackedQuest;
			Color color;
			float height;
			string text;
			if (item2.Type == QuestType.TalkTo)
			{
				bool locationReached = item2.LocationReached;
				text = $"Talk to {item2.TargetNpcName}\nReward: {item2.RewardCoins} coins" + (locationReached ? "   ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã¢â‚¬Â¦ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“ ready to hand in!" : "");
				color = (locationReached ? UIKit.Good : Color.white);
				height = 48f;
			}
			else if (item2.Type == QuestType.Explore)
			{
				bool locationReached2 = item2.LocationReached;
				text = $"Explore {item2.LocationName}\nReward: {item2.RewardCoins} coins" + (locationReached2 ? "   ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã¢â‚¬Â¦ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“ ready to hand in!" : "");
				color = (locationReached2 ? UIKit.Good : Color.white);
				height = 48f;
			}
			else
			{
				int num = ((Inventory.Instance != null) ? Inventory.Instance.GetCount(item2.ItemId) : 0);
				bool flag2 = num >= item2.Amount;
				text = "Bring " + item2.ItemPhrase + " to " + item2.GiverName + "\n" + $"{Mathf.Min(num, item2.Amount)}/{item2.Amount}   Reward: {item2.RewardCoins} coins" + (flag2 ? "   ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã¢â‚¬Â¦ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬Ãƒâ€¦Ã¢â‚¬Å“ ready to hand in!" : "");
				color = (flag2 ? UIKit.Good : Color.white);
				height = 64f;
			}
			text = (flag ? "ÃƒÆ’Ã†â€™Ãƒâ€šÃ‚Â¢ÃƒÆ’Ã‚Â¢ÃƒÂ¢Ã¢â‚¬Å¡Ã‚Â¬ÃƒÂ¢Ã¢â€šÂ¬Ã…â€œÃƒÆ’Ã¢â‚¬Å¡Ãƒâ€šÃ‚Âº " : "    ") + text.Replace("\n", "\n    ");
			Button button = UIKit.CreateButton(list, "Quest Btn", "", 22, delegate
			{
				SetTracked(captured);
			}, out var labelText);
			labelText.text = text;
			labelText.color = color;
			labelText.alignment = TextAnchor.MiddleLeft;
			labelText.rectTransform.offsetMin = new Vector2(10f, 0f);
			labelText.rectTransform.offsetMax = new Vector2(-10f, 0f);
			ColorBlock colors = button.colors;
			colors.normalColor = (flag ? new Color(0.25f, 0.25f, 0.12f, 1f) : UIKit.ButtonColor);
			colors.selectedColor = colors.normalColor;
			button.colors = colors;
			UIKit.Size(button, 444f, height);
		}
	}
}


// --- Shop.cs ---

public class Shop : MonoBehaviour
{
	private class Row
	{
		public ItemDefinition item;

		public Text have;

		public Button buy;

		public Text buyLabel;

		public Button sell;

		public Text sellLabel;
	}

	private const float SellRatio = 0.5f;

	private const string CurrencyId = "coin";

	private readonly List<Row> rows = new List<Row>();

	private GameObject root;

	private Text titleText;

	private Text coinsText;

	private NPCIdentity keeper;

	private bool open;

	public static Shop Instance { get; private set; }

	private void Awake()
	{
		Instance = this;
		BuildUI();
	}

	private void OnDestroy()
	{
		if (Inventory.Instance != null)
		{
			Inventory.Instance.Changed -= Refresh;
		}
		if (Instance == this)
		{
			Instance = null;
		}
	}

	public IEnumerator Run(NPCIdentity shopkeeper)
	{
		keeper = shopkeeper;
		titleText.text = ((keeper != null) ? (keeper.NpcName + "'s wares") : "Shop");
		open = true;
		if (Inventory.Instance != null)
		{
			Inventory.Instance.Changed += Refresh;
		}
		Refresh();
		root.SetActive(value: true);
		while (open)
		{
			if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
			{
				open = false;
			}
			yield return null;
		}
		root.SetActive(value: false);
		if (Inventory.Instance != null)
		{
			Inventory.Instance.Changed -= Refresh;
		}
		keeper = null;
	}

	public int BuyPrice(ItemDefinition item)
	{
		float num = 1f;
		if (keeper != null)
		{
			switch (keeper.Personality)
			{
			case Personality.Friendly:
				num = 0.9f;
				break;
			case Personality.Grumpy:
				num = 1.25f;
				break;
			}
		}
		return Mathf.Max(1, Mathf.RoundToInt((float)item.Value * num));
	}

	public int SellPrice(ItemDefinition item)
	{
		return Mathf.Max(1, Mathf.RoundToInt((float)item.Value * 0.5f));
	}

	private void Buy(ItemDefinition item)
	{
		if (Time.timeScale != 0f)
		{
			Inventory instance = Inventory.Instance;
			if (!(instance == null) && instance.RemoveItem("coin", BuyPrice(item)))
			{
				instance.AddItem(item.Id);
			}
		}
	}

	private void Sell(ItemDefinition item)
	{
		if (Time.timeScale != 0f)
		{
			Inventory instance = Inventory.Instance;
			if (!(instance == null) && instance.RemoveItem(item.Id))
			{
				instance.AddItem("coin", SellPrice(item));
			}
		}
	}

	private void Close()
	{
		if (Time.timeScale != 0f)
		{
			open = false;
		}
	}

	private void Refresh()
	{
		Inventory instance = Inventory.Instance;
		int num = ((instance != null) ? instance.GetCount("coin") : 0);
		coinsText.text = $"Your coins: {num}";
		foreach (Row row in rows)
		{
			int num2 = ((instance != null) ? instance.GetCount(row.item.Id) : 0);
			int num3 = BuyPrice(row.item);
			int num4 = SellPrice(row.item);
			row.have.text = "Have " + num2;
			row.buyLabel.text = "Buy  " + num3;
			row.sellLabel.text = "Sell  " + num4;
			row.buy.interactable = num >= num3;
			row.sell.interactable = num2 > 0;
		}
	}

	private void BuildUI()
	{
		RectTransform rectTransform = UIKit.CreateImage(UIKit.CreateCanvas("Shop UI", 6).transform, "ShopPanel", UIKit.PanelColor);
		Vector2 anchorMin = (rectTransform.anchorMax = new Vector2(0.5f, 0.5f));
		rectTransform.anchorMin = anchorMin;
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.anchoredPosition = new Vector2(200f, 0f);
		rectTransform.sizeDelta = new Vector2(800f, 0f);
		UIKit.VLayout(rectTransform.gameObject, 10f, 24, TextAnchor.UpperCenter);
		UIKit.FitVertical(rectTransform.gameObject);
		rectTransform.localEulerAngles = new Vector3(8f, 15f, 2f);
		titleText = UIKit.CreateText(rectTransform, "Title", "Shop", 40, Color.white);
		UIKit.Size(titleText, 752f, 56f);
		coinsText = UIKit.CreateText(rectTransform, "Coins", "", 28, UIKit.Gold);
		UIKit.Size(coinsText, 752f, 38f);
		foreach (ItemDefinition allItem in Inventory.AllItems)
		{
			if (!(allItem.Id == "coin"))
			{
				rows.Add(BuildRow(rectTransform, allItem));
			}
		}
		UIKit.Size(UIKit.CreateButton(rectTransform, "Close", "Close", 30, Close, out var _), 300f, 60f);
		root = rectTransform.gameObject;
		root.SetActive(value: false);
	}

	private Row BuildRow(Transform parent, ItemDefinition item)
	{
		RectTransform rectTransform = UIKit.CreateImage(parent, item.DisplayName + " Row", new Color(1f, 1f, 1f, 0.05f));
		UIKit.HLayout(rectTransform.gameObject, 12f, 10);
		UIKit.Size(rectTransform, 752f, 56f);
		UIKit.Size(UIKit.CreateItemSwatch(rectTransform, "Swatch", item), 32f, 32f);
		UIKit.Size(UIKit.CreateText(rectTransform, "Name", item.DisplayName, 28, Color.white, TextAnchor.MiddleLeft), 230f, 44f);
		Text text = UIKit.CreateText(rectTransform, "Have", "", 26, UIKit.Subtle, TextAnchor.MiddleRight);
		UIKit.Size(text, 110f, 44f);
		Row row = new Row
		{
			item = item,
			have = text
		};
		row.buy = UIKit.CreateButton(rectTransform, "Buy", "", 26, delegate
		{
			Buy(item);
		}, out row.buyLabel);
		UIKit.Size(row.buy, 150f, 44f);
		row.sell = UIKit.CreateButton(rectTransform, "Sell", "", 26, delegate
		{
			Sell(item);
		}, out row.sellLabel);
		UIKit.Size(row.sell, 150f, 44f);
		return row;
	}
}


// --- UIKit.cs ---

public static class UIKit
{
	public static readonly Color PanelColor = new Color(0.09f, 0.1f, 0.13f, 0.95f);

	public static readonly Color ButtonColor = new Color(0.22f, 0.24f, 0.3f, 1f);

	public static readonly Color ButtonHover = new Color(0.32f, 0.36f, 0.46f, 1f);

	public static readonly Color ButtonPress = new Color(0.14f, 0.16f, 0.2f, 1f);

	public static readonly Color ButtonDisabled = new Color(0.16f, 0.17f, 0.2f, 0.55f);

	public static readonly Color Accent = new Color(0.3f, 0.6f, 1f, 1f);

	public static readonly Color Good = new Color(0.5f, 0.9f, 0.55f, 1f);

	public static readonly Color Gold = new Color(1f, 0.82f, 0.15f, 1f);

	public static readonly Color Subtle = new Color(1f, 1f, 1f, 0.55f);

	private static Font uiFont;

	public static Font UIFont
	{
		get
		{
			if (uiFont == null)
			{
				uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
				if (uiFont == null)
				{
					uiFont = Font.CreateDynamicFontFromOSFont("Arial", 16);
				}
			}
			return uiFont;
		}
	}

	public static GameObject CreateCanvas(string objName, int sortingOrder)
	{
		GameObject gameObject = new GameObject(objName);
		Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceCamera;
        CameraController camCtrl = UnityEngine.Object.FindAnyObjectByType<CameraController>();
        Camera mainCam = (camCtrl != null) ? camCtrl.GetComponent<Camera>() : Camera.main;
        if (mainCam != null) {
            canvas.worldCamera = mainCam;
            canvas.planeDistance = 0.5f;
        } else {
		    canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        GraphicRaycaster gr = gameObject.GetComponent<GraphicRaycaster>();
        if (gr == null) gr = gameObject.AddComponent<GraphicRaycaster>();
        gr.blockingObjects = GraphicRaycaster.BlockingObjects.None;
        gr.ignoreReversedGraphics = true;
        gameObject.layer = LayerMask.NameToLayer("UI");
		canvas.sortingOrder = sortingOrder;
		CanvasScaler canvasScaler = gameObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920f, 1080f);
		canvasScaler.matchWidthOrHeight = 0.5f;
		gameObject.AddComponent<GraphicRaycaster>();
		return gameObject;
	}

	public static RectTransform CreateRect(Transform parent, string objName)
	{
		GameObject gameObject = new GameObject(objName, typeof(RectTransform));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		return gameObject.GetComponent<RectTransform>();
	}

	/// <summary>Item swatch: shows the item's icon sprite if it has one, otherwise a flat color square.</summary>
	public static RectTransform CreateItemSwatch(Transform parent, string objName, ItemDefinition def)
	{
		RectTransform rt = CreateImage(parent, objName, def.Color);
		if (def.Icon != null)
		{
			Image img = rt.GetComponent<Image>();
			img.sprite = def.Icon;
			img.color = Color.white;
			img.preserveAspect = true;
		}
		return rt;
	}
	public static RectTransform CreateImage(Transform parent, string objName, Color color)
	{
		GameObject gameObject = new GameObject(objName, typeof(RectTransform), typeof(Image));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Image component = gameObject.GetComponent<Image>();
		component.color = color;
		component.raycastTarget = false;
		return gameObject.GetComponent<RectTransform>();
	}

	public static Text CreateText(Transform parent, string objName, string content, int size, Color color, TextAnchor align = TextAnchor.MiddleCenter)
	{
		GameObject gameObject = new GameObject(objName, typeof(RectTransform), typeof(Text));
		gameObject.transform.SetParent(parent, worldPositionStays: false);
		Text component = gameObject.GetComponent<Text>();
		component.font = UIFont;
		component.text = content;
		component.fontSize = size;
		component.color = color;
		component.alignment = align;
		component.raycastTarget = false;
		component.horizontalOverflow = HorizontalWrapMode.Wrap;
		component.verticalOverflow = VerticalWrapMode.Overflow;
		RectTransform rectTransform = component.rectTransform;
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
		return component;
	}

	public static Button CreateButton(Transform parent, string objName, string label, int fontSize, UnityAction onClick, out Text labelText)
	{
		RectTransform rectTransform = CreateImage(parent, objName, Color.white);
		Image component = rectTransform.GetComponent<Image>();
		component.raycastTarget = true;
		Button button = rectTransform.gameObject.AddComponent<Button>();
		button.targetGraphic = component;
		ColorBlock colors = button.colors;
		colors.normalColor = ButtonColor;
		colors.highlightedColor = ButtonHover;
		colors.pressedColor = ButtonPress;
		colors.selectedColor = ButtonColor;
		colors.disabledColor = ButtonDisabled;
		colors.colorMultiplier = 1f;
		colors.fadeDuration = 0.08f;
		button.colors = colors;
		labelText = CreateText(rectTransform, "Text", label, fontSize, Color.white);
		button.onClick.AddListener(onClick);
		return button;
	}

	public static void VLayout(GameObject go, float spacing, int padding, TextAnchor align)
	{
		VerticalLayoutGroup verticalLayoutGroup = go.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.spacing = spacing;
		verticalLayoutGroup.padding = new RectOffset(padding, padding, padding, padding);
		verticalLayoutGroup.childAlignment = align;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = false;
		verticalLayoutGroup.childForceExpandHeight = false;
	}

	public static void HLayout(GameObject go, float spacing, int padding = 0)
	{
		HorizontalLayoutGroup horizontalLayoutGroup = go.AddComponent<HorizontalLayoutGroup>();
		horizontalLayoutGroup.spacing = spacing;
		horizontalLayoutGroup.padding = new RectOffset(padding, padding, 0, 0);
		horizontalLayoutGroup.childAlignment = TextAnchor.MiddleLeft;
		horizontalLayoutGroup.childControlWidth = true;
		horizontalLayoutGroup.childControlHeight = true;
		horizontalLayoutGroup.childForceExpandWidth = false;
		horizontalLayoutGroup.childForceExpandHeight = false;
	}

	public static void GridLayout(GameObject go, Vector2 cellSize, Vector2 spacing, int padding = 0)
	{
		GridLayoutGroup gridLayoutGroup = go.AddComponent<GridLayoutGroup>();
		gridLayoutGroup.cellSize = cellSize;
		gridLayoutGroup.spacing = spacing;
		gridLayoutGroup.padding = new RectOffset(padding, padding, padding, padding);
		gridLayoutGroup.childAlignment = TextAnchor.UpperLeft;
	}

	public static void FitVertical(GameObject go)
	{
		ContentSizeFitter contentSizeFitter = go.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
	}

	public static void Size(Component c, float width, float height)
	{
		LayoutElement layoutElement = c.GetComponent<LayoutElement>();
		if (layoutElement == null)
		{
			layoutElement = c.gameObject.AddComponent<LayoutElement>();
		}
		if (width >= 0f)
		{
			layoutElement.preferredWidth = width;
			layoutElement.minWidth = width;
		}
		if (height >= 0f)
		{
			layoutElement.preferredHeight = height;
			layoutElement.minHeight = height;
		}
	}
}












