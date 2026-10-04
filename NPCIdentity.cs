using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public class NPCIdentity : MonoBehaviour
{
	private static readonly Dictionary<string, NPCIdentity> Registry = new Dictionary<string, NPCIdentity>();
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticsForFastPlayMode() { Registry.Clear(); }


	[SerializeField]
	private string npcName = "Stranger";

	[SerializeField]
	private string job = "wanderer";

	[SerializeField]
	private Personality personality;

	[SerializeField]
	private int timesTalked;

	[SerializeField]
	private bool gaveGift;

	[SerializeField]
	private Quest quest;

	public string NpcName => npcName;

	public string Job => job;

	public bool IsResident { get; set; }

	public string AssignedJob { get; set; } = "None";

	public Personality Personality => personality;

	public bool IsTrader => job == "trader";

	public Quest Quest => quest;

	public bool HasQuest
	{
		get
		{
			if (quest != null)
			{
				return quest.Amount > 0;
			}
			return false;
		}
	}

	public int TimesTalked
	{
		get
		{
			return timesTalked;
		}
		set
		{
			timesTalked = value;
		}
	}

	public bool GaveGift
	{
		get
		{
			return gaveGift;
		}
		set
		{
			gaveGift = value;
		}
	}

	public static List<string> GetAllNames()
	{
		return new List<string>(Registry.Keys);
	}

	public static Transform FindByName(string name)
	{
		if (string.IsNullOrEmpty(name))
		{
			return null;
		}
		if (!Registry.TryGetValue(name, out var value))
		{
			return null;
		}
		return ((Component)value).transform;
	}

	public void SetQuest(Quest newQuest)
	{
		quest = newQuest;
	}

	public void Setup(string newName, string newJob, Personality newPersonality)
	{
		npcName = newName;
		job = newJob;
		personality = newPersonality;
		if (!string.IsNullOrEmpty(npcName))
		{
			Registry[npcName] = this;
		}
	}

	private void OnEnable()
	{
		if (!string.IsNullOrEmpty(npcName))
		{
			Registry[npcName] = this;
		}
	}

	private void OnDisable()
	{
		if (!string.IsNullOrEmpty(npcName) && Registry.TryGetValue(npcName, out var value) && (UnityEngine.Object)(object)value == (UnityEngine.Object)(object)this)
		{
			Registry.Remove(npcName);
		}
	}
}




