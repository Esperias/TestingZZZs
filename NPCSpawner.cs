using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DefaultExecutionOrder(100)]
public class NPCSpawner : MonoBehaviour
{
	private class Nametag
	{
		public NPCWanderer npc;

		public float headHeight;

		public RectTransform rect;

		public CanvasGroup group;

		public bool visible;
	}

	private class TalkTarget : IInteractable
	{
		private readonly NPCSpawner spawner;

		private readonly NPCWanderer npc;

		private readonly string prompt;

		public string Prompt => prompt;

		public float HoldTime => spawner.holdTime;

		public float Range => spawner.interactDistance;

		public bool CanInteract
		{
			get
			{
				if ((UnityEngine.Object)(object)npc != (UnityEngine.Object)null)
				{
					return !spawner.talking;
				}
				return false;
			}
		}

		public TalkTarget(NPCSpawner spawner, NPCWanderer npc)
		{
			this.spawner = spawner;
			this.npc = npc;
			NPCIdentity component = ((Component)npc).GetComponent<NPCIdentity>();
			prompt = (((UnityEngine.Object)(object)component != (UnityEngine.Object)null && !string.IsNullOrEmpty(component.NpcName)) ? ("talk to " + component.NpcName) : "talk");
		}

		public float DistanceTo(Vector3 point)
		{
			//IL_001f: Unknown result type (might be due to invalid IL or missing references)
			//IL_0024: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Unknown result type (might be due to invalid IL or missing references)
			//IL_002a: Unknown result type (might be due to invalid IL or missing references)
			if ((UnityEngine.Object)(object)npc == (UnityEngine.Object)null)
			{
				return float.MaxValue;
			}
			Vector3 val = ((Component)npc).transform.position - point;
			val.y = 0f;
			return val.magnitude;
		}

		public void Interact()
		{
			if ((UnityEngine.Object)(object)npc != (UnityEngine.Object)null)
			{
				((MonoBehaviour)spawner).StartCoroutine(spawner.TalkRoutine(npc));
			}
		}
	}

	[Header("Spawning")]
	[Tooltip("NPC prefabs (must have an Animator). One is picked at random for each spawn.")]
	[SerializeField]
	private GameObject[] npcPrefabs;

	[SerializeField]
	public int npcCount = 80;

	[Tooltip("How far in from the edge of the ground NPCs can spawn/walk.")]
	[SerializeField]
	private float edgeMargin = 2f;

	[Tooltip("Try to keep spawned NPCs at least this far apart.")]
	[SerializeField]
	private float minSpawnSpacing = 2f;

	[Header("Quests & Trading")]
	[Tooltip("Chance (0 to 1) that an NPC has a favour to ask.")]
	[SerializeField]
	[Range(0f, 1f)]
	private float questChance = 0.6f;

	[Tooltip("At least this many NPCs are traders, who let you buy and sell.")]
	[SerializeField]
	private int guaranteedTraders = 2;

	[Header("Movement")]
	[SerializeField]
	private float minWalkSpeed = 1.2f;

	[SerializeField]
	private float maxWalkSpeed = 2.2f;

	[SerializeField]
	private float turnSpeed = 5f;

	[Tooltip("Max distance of each walk from where the NPC currently is.")]
	[SerializeField]
	private float wanderRadius = 15f;

	[Header("Idle")]
	[SerializeField]
	private float minIdleTime = 2f;

	[SerializeField]
	private float maxIdleTime = 6f;

	[Header("Animator")]
	[SerializeField]
	private string speedParameter = "Speed";

	[Header("Interaction")]
	[Tooltip("NPCs closer than this stop walking and face you, and can be talked to.")]
	[SerializeField]
	private float interactDistance = 3f;

	[Tooltip("Seconds you need to hold the Interact key to start a conversation.")]
	[SerializeField]
	private float holdTime = 1f;

	[SerializeField]
	private float panDuration = 0.6f;

	[Tooltip("How far in front of the NPC's face the camera ends up.")]
	[SerializeField]
	private float talkCameraDistance = 1.6f;

	[Tooltip("When an NPC says something with no replies (their goodbye), how long it stays before the camera returns.")]
	[SerializeField]
	private float farewellDuration = 2f;

	[Header("Camera Clipping")]
	[Tooltip("An NPC turns invisible when the camera is inside it or within this distance of its body. Keep it a bit larger than the camera's Near Clip Plane.")]
	[SerializeField]
	private float hidePadding = 0.4f;

	[Header("Nametags")]
	[Tooltip("Show each NPC's name and job floating above their head.")]
	[SerializeField]
	private bool showNametags = true;

	[Tooltip("Nametags fade out and disappear beyond this distance from the camera.")]
	[SerializeField]
	private float nametagRange = 15f;

	[Tooltip("Gap between the top of the NPC's head and its nametag.")]
	[SerializeField]
	private float nametagHeightOffset = 0.15f;

	private const int MaxChoices = 6;

	private readonly List<NPCWanderer> npcs = new List<NPCWanderer>();

	private Camera viewCamera;

	private PlayerController playerController;

	private CameraController cameraController;

	private bool interactionReady;

	private bool talking;

	private int pickedIndex = -1;

	private readonly List<Nametag> nametags = new List<Nametag>();

	private RectTransform nametagRoot;

	private Canvas uiCanvas;

	private RectTransform bubbleRect;

	private Text bubbleText;

	private Text nameText;

	private GameObject choicePanel;

	private readonly Button[] choiceButtons = new Button[6];

	private readonly Text[] choiceLabels = new Text[6];

	private Transform talkNpc;

	private float talkHeadHeight;

	public static bool IsTalking { get; private set; }

	public static bool DialogueOpen { get; private set; }

	private void Awake()
	{
		requireTotem = true; // Force override Inspector
		IsTalking = false;
		DialogueOpen = false;
	}

	private void OnDestroy()
	{
		IsTalking = false;
		DialogueOpen = false;
	}

	private void Start()
	{
		if (npcPrefabs == null || npcPrefabs.Length == 0)
		{
			Debug.LogError((object)"NPCSpawner: assign at least one NPC prefab in the Inspector.", (UnityEngine.Object)(object)this);
			return;
		}
		if ((UnityEngine.Object)(object)Inventory.Instance == (UnityEngine.Object)null)
		{
			Debug.LogWarning((object)"NPCSpawner: no Inventory in the scene, so quests and trading won't work. Add Inventory to your Manager object.", (UnityEngine.Object)(object)this);
		}
		if ((UnityEngine.Object)(object)QuestLog.Instance == (UnityEngine.Object)null)
		{
			((Component)this).gameObject.AddComponent<QuestLog>();
		}
		if ((UnityEngine.Object)(object)Shop.Instance == (UnityEngine.Object)null)
		{
			((Component)this).gameObject.AddComponent<Shop>();
		}
		CameraController cameraController = Object.FindAnyObjectByType<CameraController>();
		viewCamera = (((UnityEngine.Object)(object)cameraController != (UnityEngine.Object)null) ? ((Component)cameraController).GetComponent<Camera>() : null);
		if ((UnityEngine.Object)(object)viewCamera == (UnityEngine.Object)null)
		{
			viewCamera = Camera.main;
		}
		SetupInteraction();
		((MonoBehaviour)this).StartCoroutine(SpawnManager());
	}

	public bool requireTotem = true;
	private IEnumerator SpawnManager()
	{
		if (requireTotem)
		{
			while ((UnityEngine.Object)(object)VillageTotem.Instance == (UnityEngine.Object)null)
			{
				yield return (object)new WaitForSeconds(1f);
			}
		}
		Transform container = new GameObject("NPCs").transform;
		Vector3[] placed = (Vector3[])(object)new Vector3[npcCount];
		List<string> names = DialogueLibrary.ShuffledNames();
		int personalityCount = Enum.GetValues(typeof(Personality)).Length;
		int tradersMade = 0;
		int spawned = 0;
		int initialSpawns = Mathf.Min(5, npcCount);
		while (spawned < npcCount)
		{
			
			Vector3 val = (placed[spawned] = PickSpawnPoint(placed, spawned));
			GameObject val2 = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
			if ((UnityEngine.Object)(object)val2 != (UnityEngine.Object)null)
			{
				GameObject val3 = Object.Instantiate<GameObject>(val2, val, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), container);
				NPCIdentity nPCIdentity = val3.GetComponent<NPCIdentity>();
				if ((UnityEngine.Object)(object)nPCIdentity == (UnityEngine.Object)null)
				{
					nPCIdentity = val3.AddComponent<NPCIdentity>();
				}
				string text = names[spawned % names.Count];
				if (spawned >= names.Count)
				{
					string[] array = new string[10] { "the Elder", "the Younger", "the Tall", "the Swift", "of the Woods", "of the Valley", "the Wise", "the Brave", "the Quiet", "the Strong" };
					text = text + " " + array[(spawned / names.Count - 1) % array.Length];
				}
				string text2 = ((tradersMade < guaranteedTraders) ? "trader" : DialogueLibrary.Jobs[Random.Range(0, DialogueLibrary.Jobs.Length)]);
				if (text2 == "trader")
				{
					tradersMade++;
				}
				Personality personality = (Personality)Random.Range(0, personalityCount);
				nPCIdentity.Setup(text, text2, personality);
				((UnityEngine.Object)val3).name = $"{text} ({text2}, {personality})";
				NPCWanderer nPCWanderer = val3.GetComponent<NPCWanderer>();
				if ((UnityEngine.Object)(object)nPCWanderer == (UnityEngine.Object)null)
				{
					nPCWanderer = val3.AddComponent<NPCWanderer>();
				}
				nPCWanderer.Init(null, edgeMargin, Random.Range(minWalkSpeed, maxWalkSpeed), turnSpeed, wanderRadius, minIdleTime, maxIdleTime, speedParameter);
				npcs.Add(nPCWanderer);
				RegisterNPC(nPCWanderer);
				if (Random.value < questChance)
				{
					nPCIdentity.SetQuest(QuestLog.CreateRandomQuest(nPCIdentity.NpcName));
					if (nPCIdentity.HasQuest)
					{
						GameObject gameObject = ((Component)nPCWanderer).gameObject;
						((UnityEngine.Object)gameObject).name = ((UnityEngine.Object)gameObject).name + " [quest]";
					}
				}
			}
			spawned++;
			if (spawned < initialSpawns)
			{
				yield return (object)new WaitForSeconds(0.2f);
			}
			else
			{
				yield return (object)new WaitForSeconds(Random.Range(15f, 30f));
			}
		}
	}

	private Vector3 PickSpawnPoint(Vector3[] placed, int placedCount)
	{
		//IL_0000: Unknown result type (might be due to invalid IL or missing references)
		//IL_0005: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_0013: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0031: Unknown result type (might be due to invalid IL or missing references)
		//IL_0048: Unknown result type (might be due to invalid IL or missing references)
		//IL_00df: Unknown result type (might be due to invalid IL or missing references)
		//IL_009a: Unknown result type (might be due to invalid IL or missing references)
		//IL_009f: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d1: Unknown result type (might be due to invalid IL or missing references)
		Vector3 zero = Vector3.zero;
		Vector3 val = transform.position;
		if (requireTotem && (UnityEngine.Object)(object)VillageTotem.Instance != (UnityEngine.Object)null)
		{
			val = ((Component)VillageTotem.Instance).transform.position;
		}
		for (int i = 0; i < 30; i++)
		{
			float num = val.x + Random.Range(-12f, 12f);
			float num2 = val.z + Random.Range(-12f, 12f);
			float num3 = (((UnityEngine.Object)(object)WorldGenerator.Instance != (UnityEngine.Object)null) ? WorldGenerator.Instance.GetHeightAt(num, num2) : 0f);
			zero = new Vector3(num, num3, num2);
			bool flag = false;
			for (int j = 0; j < placedCount; j++)
			{
				Vector3 val2 = placed[j] - zero;
				if (val2.sqrMagnitude < minSpawnSpacing * minSpawnSpacing)
				{
					flag = true;
					break;
				}
			}
			if (!flag)
			{
				return zero;
			}
		}
		return zero;
	}

	private void RegisterNPC(NPCWanderer npc)
	{
		//IL_0099: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_0126: Unknown result type (might be due to invalid IL or missing references)
		//IL_0157: Unknown result type (might be due to invalid IL or missing references)
		//IL_016b: Unknown result type (might be due to invalid IL or missing references)
		if (!interactionReady || !((UnityEngine.Object)(object)npc != (UnityEngine.Object)null))
		{
			return;
		}
		InteractionManager.Register(new TalkTarget(this, npc));
		if (showNametags && (UnityEngine.Object)(object)nametagRoot != (UnityEngine.Object)null)
		{
			NPCIdentity component = ((Component)npc).GetComponent<NPCIdentity>();
			if ((UnityEngine.Object)(object)component != (UnityEngine.Object)null && !string.IsNullOrEmpty(component.NpcName))
			{
				GetNpcHeights(((Component)npc).transform, out var _, out var headHeight);
				RectTransform val = UIKit.CreateRect((Transform)(object)nametagRoot, component.NpcName + " Nametag");
				val.pivot = new Vector2(0.5f, 0f);
				val.sizeDelta = new Vector2(320f, 70f);
				CanvasGroup val2 = ((Component)val).gameObject.AddComponent<CanvasGroup>();
				val2.blocksRaycasts = false;
				val2.interactable = false;
				string text = (component.IsResident ? " <color=#FFE66D>(Resident)</color>" : "");
				string content = component.NpcName + text + "\n<size=22><color=#C4CBDB>" + component.Job + "</color></size>";
				Text text2 = UIKit.CreateText((Transform)(object)val, "Text", content, 30, Color.white, (TextAnchor)7);
				text2.fontStyle = (FontStyle)1;
				Outline outline = ((Component)text2).gameObject.AddComponent<Outline>();
				outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
				outline.effectDistance = new Vector2(2f, -2f);
				((Component)val).gameObject.SetActive(false);
				nametags.Add(new Nametag
				{
					npc = npc,
					headHeight = headHeight,
					rect = val,
					group = val2
				});
			}
		}
	}

	private void SetupInteraction()
	{
		playerController = Object.FindAnyObjectByType<PlayerController>();
		cameraController = Object.FindAnyObjectByType<CameraController>();
		if ((UnityEngine.Object)(object)playerController == (UnityEngine.Object)null || (UnityEngine.Object)(object)cameraController == (UnityEngine.Object)null)
		{
			Debug.LogWarning((object)"NPCSpawner: couldn't find the PlayerController and CameraController, so talking to NPCs is disabled.", (UnityEngine.Object)(object)this);
			return;
		}
		PauseMenu.EnsureEventSystem();
		BuildUI();
		interactionReady = true;
		foreach (NPCWanderer npc in npcs)
		{
			RegisterNPC(npc);
		}
	}

	private void Update()
	{
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		if (!interactionReady)
		{
			return;
		}
		if (playerController == null) {
			playerController = Object.FindAnyObjectByType<PlayerController>();
			if (playerController == null) return;
		}
		Transform transform = ((Component)playerController).transform;
		Vector3 position = transform.position;
		float num = interactDistance * interactDistance;
		for (int i = 0; i < npcs.Count; i++)
		{
			if (i % 4 != Time.frameCount % 4) continue;
			NPCWanderer npc = npcs[i];
			if (!((UnityEngine.Object)(object)npc == (UnityEngine.Object)null))
			{
				Vector3 val = ((Component)npc).transform.position - position;
				val.y = 0f;
				if (val.sqrMagnitude <= num)
				{
					npc.FaceAndStop(transform);
				}
				else
				{
					npc.Resume();
				}
			}
		}
	}

	private void LateUpdate()
	{
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_006c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0077: Unknown result type (might be due to invalid IL or missing references)
		//IL_007c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0083: Unknown result type (might be due to invalid IL or missing references)
		//IL_0084: Unknown result type (might be due to invalid IL or missing references)
		//IL_0089: Unknown result type (might be due to invalid IL or missing references)
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ae: Unknown result type (might be due to invalid IL or missing references)
		//IL_00af: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f1: Unknown result type (might be due to invalid IL or missing references)
		//IL_010d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0135: Unknown result type (might be due to invalid IL or missing references)
		if (!interactionReady)
		{
			return;
		}
		UpdateNpcVisibility();
		UpdateNametags();
		if (!((UnityEngine.Object)(object)talkNpc != (UnityEngine.Object)null) || !((Component)bubbleRect).gameObject.activeSelf)
		{
			return;
		}
		Camera val = (((UnityEngine.Object)(object)viewCamera != (UnityEngine.Object)null) ? viewCamera : Camera.main);
		if (!((UnityEngine.Object)(object)val == (UnityEngine.Object)null))
		{
			Vector3 val2 = talkNpc.position + Vector3.up * talkHeadHeight;
			Vector3 val3 = val.WorldToScreenPoint(val2);
			if (val3.z > 0f)
			{
				Rect rect = bubbleRect.rect;
				Vector2 size = rect.size;
				float num = Mathf.Clamp(val3.x, size.x * 0.5f + 10f, Mathf.Max(10f, (float)Screen.width - size.x * 0.5f - 10f));
				float num2 = Mathf.Clamp(val3.y + 20f, 10f, Mathf.Max(10f, (float)Screen.height - size.y - 10f));
				((Transform)bubbleRect).position = new Vector3(num, num2, 0f);
			}
		}
	}

	private void UpdateNpcVisibility()
	{
		//IL_001a: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		if ((UnityEngine.Object)(object)viewCamera == (UnityEngine.Object)null)
		{
			return;
		}
		Vector3 position = ((Component)viewCamera).transform.position;
		for (int i = 0; i < npcs.Count; i++)
		{
			if (i % 4 != Time.frameCount % 4) continue;
			NPCWanderer npc = npcs[i];
			if (!((UnityEngine.Object)(object)npc == (UnityEngine.Object)null))
			{
				bool forceVisible = (UnityEngine.Object)(object)talkNpc != (UnityEngine.Object)null && (UnityEngine.Object)(object)((Component)npc).transform == (UnityEngine.Object)(object)talkNpc;
				npc.UpdateVisibility(position, hidePadding, forceVisible);
			}
		}
	}

	public void RefreshNametag(NPCIdentity id)
	{
		if ((UnityEngine.Object)(object)id == (UnityEngine.Object)null)
		{
			return;
		}
		foreach (Nametag nametag in nametags)
		{
			if ((UnityEngine.Object)(object)nametag.npc != (UnityEngine.Object)null && (UnityEngine.Object)(object)((Component)nametag.npc).gameObject == (UnityEngine.Object)(object)((Component)id).gameObject)
			{
				Text componentInChildren = ((Component)nametag.rect).GetComponentInChildren<Text>();
				if ((UnityEngine.Object)(object)componentInChildren != (UnityEngine.Object)null)
				{
					string text = (id.IsResident ? " <color=#FFE66D>(Resident)</color>" : "");
					componentInChildren.text = id.NpcName + text + "\n<size=22><color=#C4CBDB>" + id.Job + "</color></size>";
				}
				break;
			}
		}
	}

	private void UpdateNametags()
	{
		//IL_005e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0051: Unknown result type (might be due to invalid IL or missing references)
		//IL_0063: Unknown result type (might be due to invalid IL or missing references)
		//IL_00dc: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f9: Unknown result type (might be due to invalid IL or missing references)
		//IL_00fe: Unknown result type (might be due to invalid IL or missing references)
		//IL_0100: Unknown result type (might be due to invalid IL or missing references)
		//IL_0101: Unknown result type (might be due to invalid IL or missing references)
		//IL_0115: Unknown result type (might be due to invalid IL or missing references)
		//IL_0117: Unknown result type (might be due to invalid IL or missing references)
		//IL_011c: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0136: Unknown result type (might be due to invalid IL or missing references)
		//IL_013d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0149: Unknown result type (might be due to invalid IL or missing references)
		if (nametags.Count == 0)
		{
			return;
		}
		Camera val = (((UnityEngine.Object)(object)viewCamera != (UnityEngine.Object)null) ? viewCamera : Camera.main);
		bool flag = (UnityEngine.Object)(object)val != (UnityEngine.Object)null && showNametags && !talking;
		Vector3 val2 = (((UnityEngine.Object)(object)val != (UnityEngine.Object)null) ? ((Component)val).transform.position : Vector3.zero);
		for (int num = nametags.Count - 1; num >= 0; num--)
		{
			Nametag nametag = nametags[num];
			if ((UnityEngine.Object)(object)nametag.npc == (UnityEngine.Object)null)
			{
				Object.Destroy((UnityEngine.Object)(object)((Component)nametag.rect).gameObject);
				nametags.RemoveAt(num);
			}
			else
			{
				bool flag2 = false;
				if (flag && !nametag.npc.IsHidden)
				{
					Vector3 val3 = ((Component)nametag.npc).transform.position + Vector3.up * (nametag.headHeight + nametagHeightOffset);
					float num2 = Vector3.Distance(val2, val3);
					if (num2 <= nametagRange)
					{
						Vector3 val4 = val.WorldToScreenPoint(val3);
						if (val4.z > 0f)
						{
							flag2 = true;
							((Transform)nametag.rect).position = new Vector3(val4.x, val4.y, 0f);
							nametag.group.alpha = 1f - Mathf.InverseLerp(nametagRange * 0.7f, nametagRange, num2);
						}
					}
				}
				if (flag2 != nametag.visible)
				{
					nametag.visible = flag2;
					((Component)nametag.rect).gameObject.SetActive(flag2);
				}
			}
		}
	}

	private IEnumerator TalkRoutine(NPCWanderer npc)
	{
		talking = true;
		IsTalking = true;
		InteractionManager.Block();
		((Behaviour)playerController).enabled = false;
		((Behaviour)cameraController).enabled = false;
		Transform cam = ((Component)cameraController).transform;
		Vector3 startPos = cam.position;
		Quaternion startRot = cam.rotation;
		GetNpcHeights(((Component)npc).transform, out var faceHeight, out var headHeight);
		talkNpc = ((Component)npc).transform;
		talkHeadHeight = headHeight;
		Vector3 position = ((Component)npc).transform.position;
		Vector3 val = ((Component)playerController).transform.position - position;
		val.y = 0f;
		float magnitude = val.magnitude;
		Vector3 val2 = ((magnitude > 0.01f) ? (val / magnitude) : ((Component)npc).transform.forward);
		float num = Mathf.Clamp(Mathf.Min(talkCameraDistance, magnitude - 0.7f), 0.9f, talkCameraDistance);
		Vector3 val3 = position + Vector3.up * faceHeight;
		Vector3 targetPos = val3 + val2 * num;
		Quaternion targetRot = Quaternion.LookRotation(val3 - targetPos);
		yield return PanCamera(cam, startPos, startRot, targetPos, targetRot);
		NPCIdentity identity = ((Component)npc).GetComponent<NPCIdentity>();
		DialogueNode dialogueNode = DialogueLibrary.BuildConversation(identity);
		if ((UnityEngine.Object)(object)identity != (UnityEngine.Object)null)
		{
			identity.TimesTalked++;
		}
		if ((UnityEngine.Object)(object)identity != (UnityEngine.Object)null && (UnityEngine.Object)(object)QuestLog.Instance != (UnityEngine.Object)null)
		{
			QuestLog.Instance.OnNpcTalkedTo(identity.NpcName);
		}
		bool flag = (UnityEngine.Object)(object)identity != (UnityEngine.Object)null && !string.IsNullOrEmpty(identity.NpcName);
		((Component)nameText).gameObject.SetActive(flag);
		if (flag)
		{
			nameText.text = identity.NpcName + "  -  " + identity.Job;
		}
		DialogueOpen = true;
		Cursor.lockState = (CursorLockMode)0;
		Cursor.visible = true;
		((Component)bubbleRect).gameObject.SetActive(true);
		choicePanel.SetActive(true);
		while (dialogueNode != null)
		{
			List<DialogueChoice> choices = dialogueNode.GetVisibleChoices();
			if (choices.Count > 6)
			{
				choices.RemoveRange(6, choices.Count - 6);
			}
			ShowNode(dialogueNode, choices);
			if (choices.Count == 0)
			{
				yield return (object)new WaitForSeconds(farewellDuration);
				break;
			}
			pickedIndex = -1;
			while (pickedIndex < 0)
			{
				if (Time.timeScale > 0f)
				{
					int num2 = ReadNumberKey();
					if (num2 >= 0 && num2 < choices.Count)
					{
						pickedIndex = num2;
					}
#if ENABLE_INPUT_SYSTEM
					if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
#else
					if (UnityEngine.Input.GetKeyDown(UnityEngine.KeyCode.Escape))
#endif
					{
						int goodbye = choices.FindIndex((DialogueChoice c) => c.GetNext() == null);
						if (goodbye >= 0)
						{
							pickedIndex = goodbye;
						}
						else
						{
							break;
						}
					}
				}
				yield return null;
			}
			if (pickedIndex < 0)
			{
				break;
			}
			DialogueChoice choice = choices[pickedIndex];
			choice.OnChosen?.Invoke();
			if (choice.OpensShop && (UnityEngine.Object)(object)Shop.Instance != (UnityEngine.Object)null)
			{
				choicePanel.SetActive(false);
				bubbleText.text = choice.ShopGreeting ?? "";
				yield return Shop.Instance.Run(identity);
				choicePanel.SetActive(true);
			}
			dialogueNode = choice.GetNext();
		}
		((Component)bubbleRect).gameObject.SetActive(false);
		choicePanel.SetActive(false);
		DialogueOpen = false;
		Cursor.lockState = (CursorLockMode)1;
		Cursor.visible = false;
		yield return PanCamera(cam, targetPos, targetRot, startPos, startRot);
		talkNpc = null;
		((Behaviour)cameraController).enabled = true;
		((Behaviour)playerController).enabled = true;
		talking = false;
		IsTalking = false;
		InteractionManager.Unblock();
	}

	private void ShowNode(DialogueNode node, List<DialogueChoice> choices)
	{
		bubbleText.text = node.GetText();
		for (int i = 0; i < 6; i++)
		{
			bool flag = i < choices.Count;
			((Component)choiceButtons[i]).gameObject.SetActive(flag);
			if (flag)
			{
				choiceLabels[i].text = $"{i + 1}.  {choices[i].GetLabel()}";
			}
		}
	}

	private void OnChoiceClicked(int index)
	{
		if (Time.timeScale != 0f)
		{
			pickedIndex = index;
		}
	}

	private int ReadNumberKey()
	{
		Keyboard current = Keyboard.current;
		if (current == null)
		{
			return -1;
		}
		if (current.digit1Key.wasPressedThisFrame)
		{
			return 0;
		}
		if (current.digit2Key.wasPressedThisFrame)
		{
			return 1;
		}
		if (current.digit3Key.wasPressedThisFrame)
		{
			return 2;
		}
		if (current.digit4Key.wasPressedThisFrame)
		{
			return 3;
		}
		if (current.digit5Key.wasPressedThisFrame)
		{
			return 4;
		}
		if (current.digit6Key.wasPressedThisFrame)
		{
			return 5;
		}
		return -1;
	}

	private IEnumerator PanCamera(Transform cam, Vector3 fromPos, Quaternion fromRot, Vector3 toPos, Quaternion toRot)
	{
		//IL_0015: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_001d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_0025: Unknown result type (might be due to invalid IL or missing references)
		//IL_002b: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		float t = 0f;
		while (t < 1f)
		{
			t += Time.deltaTime / Mathf.Max(0.01f, panDuration);
			float num = Mathf.SmoothStep(0f, 1f, t);
			cam.SetPositionAndRotation(Vector3.Lerp(fromPos, toPos, num), Quaternion.Slerp(fromRot, toRot, num));
			yield return null;
		}
		cam.SetPositionAndRotation(toPos, toRot);
	}

	private void GetNpcHeights(Transform npc, out float faceHeight, out float headHeight)
	{
		//IL_0004: Unknown result type (might be due to invalid IL or missing references)
		//IL_002d: Unknown result type (might be due to invalid IL or missing references)
		//IL_001f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0024: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a8: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00cd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00da: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0070: Unknown result type (might be due to invalid IL or missing references)
		//IL_0075: Unknown result type (might be due to invalid IL or missing references)
		bool flag = false;
		Bounds val = default(Bounds);
		SkinnedMeshRenderer[] componentsInChildren = ((Component)npc).GetComponentsInChildren<SkinnedMeshRenderer>();
		foreach (SkinnedMeshRenderer val2 in componentsInChildren)
		{
			if (!flag)
			{
				val = ((Renderer)val2).bounds;
				flag = true;
			}
			else
			{
				val.Encapsulate(((Renderer)val2).bounds);
			}
		}
		MeshRenderer[] componentsInChildren2 = ((Component)npc).GetComponentsInChildren<MeshRenderer>();
		foreach (MeshRenderer val3 in componentsInChildren2)
		{
			if (!((UnityEngine.Object)((Component)val3).gameObject).name.Contains("CraftStation Indicator"))
			{
				if (!flag)
				{
					val = ((Renderer)val3).bounds;
					flag = true;
				}
				else
				{
					val.Encapsulate(((Renderer)val3).bounds);
				}
			}
		}
		if (!flag)
		{
			faceHeight = 1.6f;
			headHeight = 1.9f;
		}
		else
		{
			headHeight = val.max.y - npc.position.y;
			faceHeight = val.min.y - npc.position.y + val.size.y * 0.78f;
		}
	}

	private void BuildUI()
	{
		//IL_0034: Unknown result type (might be due to invalid IL or missing references)
		//IL_0044: Unknown result type (might be due to invalid IL or missing references)
		//IL_0054: Unknown result type (might be due to invalid IL or missing references)
		//IL_0064: Unknown result type (might be due to invalid IL or missing references)
		//IL_008e: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f4: Expected O, but got Unknown
		//IL_0160: Unknown result type (might be due to invalid IL or missing references)
		//IL_01a4: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e9: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_0200: Unknown result type (might be due to invalid IL or missing references)
		//IL_0215: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_02c8: Unknown result type (might be due to invalid IL or missing references)
		//IL_02d4: Expected O, but got Unknown
		//IL_02ef: Unknown result type (might be due to invalid IL or missing references)
		//IL_030a: Unknown result type (might be due to invalid IL or missing references)
		GameObject val = UIKit.CreateCanvas("NPC Interaction UI", 5);
		uiCanvas = val.GetComponent<Canvas>();
		nametagRoot = UIKit.CreateRect(val.transform, "Nametags");
		nametagRoot.anchorMin = Vector2.zero;
		nametagRoot.anchorMax = Vector2.one;
		nametagRoot.offsetMin = Vector2.zero;
		nametagRoot.offsetMax = Vector2.zero;
		bubbleRect = UIKit.CreateImage(val.transform, "SpeechBubble", new Color(1f, 1f, 1f, 0.95f));
		bubbleRect.pivot = new Vector2(0.5f, 0f);
		bubbleRect.sizeDelta = new Vector2(640f, 100f);
		VerticalLayoutGroup verticalLayoutGroup = ((Component)bubbleRect).gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup.padding = new RectOffset(24, 24, 14, 18);
		verticalLayoutGroup.spacing = 4f;
		verticalLayoutGroup.childAlignment = (TextAnchor)1;
		verticalLayoutGroup.childControlWidth = true;
		verticalLayoutGroup.childControlHeight = true;
		verticalLayoutGroup.childForceExpandWidth = true;
		verticalLayoutGroup.childForceExpandHeight = false;
		ContentSizeFitter contentSizeFitter = ((Component)bubbleRect).gameObject.AddComponent<ContentSizeFitter>();
		contentSizeFitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		nameText = UIKit.CreateText((Transform)(object)bubbleRect, "Name", "", 24, new Color(0.2f, 0.4f, 0.85f), (TextAnchor)4);
		nameText.fontStyle = (FontStyle)1;
		UIKit.Size((Component)(object)nameText, -1f, 32f);
		bubbleText = UIKit.CreateText((Transform)(object)bubbleRect, "Text", "", 30, Color.black, (TextAnchor)4);
		((Component)bubbleRect).gameObject.SetActive(false);
		RectTransform val2 = UIKit.CreateRect(val.transform, "Choices");
		Vector2 val3 = default(Vector2);
		val3 = new Vector2(0.5f, 0f);
		val2.anchorMax = val3;
		val2.anchorMin = val3;
		val2.pivot = new Vector2(0.5f, 0f);
		val2.anchoredPosition = new Vector2(0f, 80f);
		val2.sizeDelta = new Vector2(760f, 0f);
		VerticalLayoutGroup verticalLayoutGroup2 = ((Component)val2).gameObject.AddComponent<VerticalLayoutGroup>();
		verticalLayoutGroup2.spacing = 10f;
		verticalLayoutGroup2.childAlignment = (TextAnchor)7;
		verticalLayoutGroup2.childControlWidth = true;
		verticalLayoutGroup2.childControlHeight = true;
		verticalLayoutGroup2.childForceExpandWidth = true;
		verticalLayoutGroup2.childForceExpandHeight = false;
		ContentSizeFitter contentSizeFitter2 = ((Component)val2).gameObject.AddComponent<ContentSizeFitter>();
		contentSizeFitter2.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
		contentSizeFitter2.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		for (int i = 0; i < 6; i++)
		{
			int index = i;
			Button button = UIKit.CreateButton((Transform)(object)val2, "Choice " + (i + 1), "", 28, (UnityAction)delegate
			{
				OnChoiceClicked(index);
			}, out var labelText);
			labelText.alignment = (TextAnchor)3;
			labelText.rectTransform.offsetMin = new Vector2(24f, 0f);
			labelText.rectTransform.offsetMax = new Vector2(-24f, 0f);
			UIKit.Size((Component)(object)button, 760f, 62f);
			choiceButtons[i] = button;
			choiceLabels[i] = labelText;
			((Component)button).gameObject.SetActive(false);
		}
		choicePanel = ((Component)val2).gameObject;
		choicePanel.SetActive(false);
	}

	private void BuildNametags()
	{
		//IL_008a: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a0: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_014e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0162: Unknown result type (might be due to invalid IL or missing references)
		if (!showNametags)
		{
			return;
		}
		foreach (NPCWanderer npc in npcs)
		{
			if (!((UnityEngine.Object)(object)npc == (UnityEngine.Object)null))
			{
				NPCIdentity component = ((Component)npc).GetComponent<NPCIdentity>();
				if (!((UnityEngine.Object)(object)component == (UnityEngine.Object)null) && !string.IsNullOrEmpty(component.NpcName))
				{
					GetNpcHeights(((Component)npc).transform, out var _, out var headHeight);
					RectTransform val = UIKit.CreateRect((Transform)(object)nametagRoot, component.NpcName + " Nametag");
					val.pivot = new Vector2(0.5f, 0f);
					val.sizeDelta = new Vector2(320f, 70f);
					CanvasGroup val2 = ((Component)val).gameObject.AddComponent<CanvasGroup>();
					val2.blocksRaycasts = false;
					val2.interactable = false;
					string text = (component.IsResident ? " <color=#FFE66D>(Resident)</color>" : "");
					string content = component.NpcName + text + "\n<size=22><color=#C4CBDB>" + component.Job + "</color></size>";
					Text text2 = UIKit.CreateText((Transform)(object)val, "Text", content, 30, Color.white, (TextAnchor)7);
					text2.fontStyle = (FontStyle)1;
					Outline outline = ((Component)text2).gameObject.AddComponent<Outline>();
					outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
					outline.effectDistance = new Vector2(2f, -2f);
					((Component)val).gameObject.SetActive(false);
					nametags.Add(new Nametag
					{
						npc = npc,
						headHeight = headHeight,
						rect = val,
						group = val2
					});
				}
			}
		}
	}
}
















