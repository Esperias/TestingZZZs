using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using System.Collections.Generic;
using UnityEngine;

public static class DialogueLibrary
{
	private class Lines
	{
		public string First;

		public string Return;

		public string Who;

		public string Mood;

		public string Gift;

		public string NoGift;

		public string Hub;

		public string Bye;

		public string GiftItem;

		public string QuestOffer;

		public string QuestAccept;

		public string QuestReminder;

		public string QuestThanks;

		public string Trade;
	}

	public const string TraderJob = "trader";

	public static readonly string[] Names = new string[30]
	{
		"Marta", "Bram", "Elsie", "Otto", "Wren", "Hollis", "Ines", "Cassian", "Dorothea", "Fenn",
		"Gideon", "Hazel", "Ignatius", "Juniper", "Kit", "Lucan", "Mabel", "Nolan", "Odette", "Percy",
		"Quill", "Rosalind", "Silas", "Tamsin", "Ulric", "Vera", "Wilbur", "Xena", "Yorick", "Zora"
	};

	public static readonly string[] Jobs = new string[12]
	{
		"baker", "fisher", "farmer", "blacksmith", "trader", "guard", "gardener", "scholar", "carpenter", "potter",
		"beekeeper", "shepherd"
	};

	public static List<string> ShuffledNames()
	{
		List<string> list = new List<string>(Names);
		for (int num = list.Count - 1; num > 0; num--)
		{
			int num2 = Random.Range(0, num + 1);
			List<string> list2 = list;
			int index = num;
			List<string> list3 = list;
			int index2 = num2;
			string value = list[num2];
			string value2 = list[num];
			list2[index] = value;
			list3[index2] = value2;
		}
		return list;
	}

	private static Lines GetLines(Personality p)
	{
		return p switch
		{
			Personality.Grumpy => new Lines
			{
				First = "What do you want? Name's {name}. Make it quick.",
				Return = "You again. Fine. What is it this time?",
				Who = "{name}. {job}. That's all you need to know.",
				Mood = "Terrible. Same as yesterday. Same as tomorrow.",
				Gift = "Ugh. Fine. Take this coin and stop bothering me.",
				NoGift = "I already gave you something. Don't push your luck.",
				Hub = "Still here? Well, go on.",
				Bye = "Finally. Goodbye.",
				GiftItem = "coin",
				QuestOffer = "Since you're hovering anyway... bring me {items}. {reward} coins. Don't make me regret asking.",
				QuestAccept = "Hmph. Don't dawdle.",
				QuestReminder = "Not enough. I need {items}. You've got {have}. Keep looking.",
				QuestThanks = "...Fine. That'll do. Here, {reward} coins. Don't spend it all in one place.",
				Trade = "Buying or selling? Hurry up. Prices are prices."
			}, 
			Personality.Nervous => new Lines
			{
				First = "Oh! Y-you startled me. I'm {name}. Please don't sneak up like that.",
				Return = "Oh, it's you again. I thought you were... never mind.",
				Who = "I'm {name}. I work as a {job}, mostly because it keeps me indoors. Usually.",
				Mood = "Fine! Everything's fine. Did you hear that noise? No? Just me, then.",
				Gift = "Take this old key, quickly, before anyone sees. I have no idea what it opens.",
				NoGift = "I don't have anything else, I swear! Please stop looking at me like that.",
				Hub = "S-so... was there something else?",
				Bye = "O-okay, bye! Watch your back out there!",
				GiftItem = "key",
				QuestOffer = "Actually... would you mind fetching {items}? I'd go myself but, well, outside. I can pay {reward} coins!",
				QuestAccept = "Oh, thank you, thank you! Please be careful out there!",
				QuestReminder = "Did you find {items}? You have {have} so far. Please hurry! I mean, no pressure!",
				QuestThanks = "You found them! I knew you would. Here, {reward} coins, all yours!",
				Trade = "Oh, you want to trade? Sure. Quickly. Please."
			}, 
			Personality.Curious => new Lines
			{
				First = "Ooh, a visitor! I'm {name}. Where did you come from? What's it like out there?",
				Return = "You're back! I thought of ten more questions while you were gone.",
				Who = "I'm {name}, a {job}, and a collector of odd facts. Did you know... actually, tell me about you!",
				Mood = "Fascinating, honestly! I found a strange ring of mushrooms this morning.",
				Gift = "Here, take this mushroom! I found it by the old tree. I'm dying to know what it does.",
				NoGift = "That was my last one! Come back if you find something interesting.",
				Hub = "Ooh, what else do you want to know?",
				Bye = "Bye! Bring stories next time!",
				GiftItem = "mushroom",
				QuestOffer = "Ooh, yes! I'm running an experiment and I need {items}. Bring them back and I'll give you {reward} coins!",
				QuestAccept = "Fantastic! I can't wait to see what happens!",
				QuestReminder = "Any luck with {items}? You've got {have} so far. Where did you look?",
				QuestThanks = "Perfect! Here are your {reward} coins. I have so many new questions now!",
				Trade = "Ooh, business! I've got all sorts of interesting things."
			}, 
			Personality.Wise => new Lines
			{
				First = "Greetings, traveler. I am {name}. The road has brought you far, I think.",
				Return = "We meet again. Paths tend to cross more than once.",
				Who = "I am {name}, and I have been a {job} for more years than I care to count.",
				Mood = "The day is what one makes of it. Today, I choose to notice the birds.",
				Gift = "Take this gem. It has served me well, and now it may serve you.",
				NoGift = "You already carry my gift. A wise person does not ask twice.",
				Hub = "Is there something else weighing on your mind?",
				Bye = "Safe travels, friend.",
				GiftItem = "gem",
				QuestOffer = "There is a small task, if you are willing. Bring me {items}, and I shall reward you with {reward} coins.",
				QuestAccept = "Thank you. The road provides, and so shall you.",
				QuestReminder = "You seek {items}, and carry {have} so far. Patience will serve you well.",
				QuestThanks = "You have done well. Take these {reward} coins with my thanks.",
				Trade = "A fair trade benefits us both. See what interests you."
			}, 
			_ => new Lines
			{
				First = "Oh, hello there! I'm {name}. It's always nice to meet someone new around here!",
				Return = "Well, look who's back! Good to see you again, friend.",
				Who = "I'm {name}, the local {job}. I've lived around here all my life and I love it.",
				Mood = "Wonderful, thanks for asking! The weather's lovely and everyone's in a good mood.",
				Gift = "Since you asked... take this apple! It's fresh.",
				NoGift = "You're sweet to ask, but I've already given you my best!",
				Hub = "Anything else you'd like to chat about?",
				Bye = "Take care! Come visit again soon!",
				GiftItem = "apple",
				QuestOffer = "Actually, yes! I'm running short on {items}. Could you bring me some? I'll pay you {reward} coins!",
				QuestAccept = "You're a star! Come back once you've got them.",
				QuestReminder = "How's the search for {items} going? You've got {have} so far. No rush!",
				QuestThanks = "Oh, wonderful! That's exactly what I needed. Here are your {reward} coins!",
				Trade = "Take a look! Best prices around, and I'm not just saying that."
			}, 
		};
	}

	private static string Fill(string text, NPCIdentity id)
	{
		text = text.Replace("{name}", id.NpcName).Replace("{job}", id.Job);
		if (id.HasQuest)
		{
			Quest quest = id.Quest;
			int num = (((UnityEngine.Object)(object)Inventory.Instance != (UnityEngine.Object)null) ? Inventory.Instance.GetCount(quest.ItemId) : 0);
			text = text.Replace("{items}", (quest.Type == QuestType.Fetch) ? quest.ItemPhrase : "").Replace("{amount}", quest.Amount.ToString()).Replace("{reward}", quest.RewardCoins.ToString())
				.Replace("{have}", Mathf.Min(num, quest.Amount).ToString());
			if (quest.Type == QuestType.TalkTo)
			{
				text = text.Replace("{target}", quest.TargetNpcName);
			}
		}
		return text;
	}

	public static DialogueNode BuildConversation(NPCIdentity id)
	{
		//IL_0523: Unknown result type (might be due to invalid IL or missing references)
		//IL_0539: Unknown result type (might be due to invalid IL or missing references)
		//IL_053e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0543: Unknown result type (might be due to invalid IL or missing references)
		if ((UnityEngine.Object)(object)id == (UnityEngine.Object)null)
		{
			return new DialogueNode
			{
				Text = "Hello there."
			};
		}
		Lines l = GetLines(id.Personality);
		DialogueNode dialogueNode = new DialogueNode
		{
			Text = Fill((id.TimesTalked == 0) ? l.First : l.Return, id)
		};
		DialogueNode hub = new DialogueNode
		{
			Text = Fill(l.Hub, id)
		};
		DialogueNode next = new DialogueNode
		{
			Text = Fill(l.Bye, id)
		};
		DialogueNode dialogueNode2 = new DialogueNode
		{
			Text = Fill(l.Who, id)
		};
		DialogueNode dialogueNode3 = new DialogueNode
		{
			Text = Fill(l.Mood, id)
		};
		bool giftedNow = false;
		DialogueNode dialogueNode4 = new DialogueNode
		{
			DynamicText = () => Fill(giftedNow ? l.Gift : l.NoGift, id)
		};
		List<DialogueNode> list = new List<DialogueNode> { dialogueNode2, dialogueNode3, dialogueNode4 };
		Quest quest = (id.HasQuest ? id.Quest : null);
		DialogueNode offer = null;
		DialogueNode dialogueNode5 = null;
		DialogueNode reminder = null;
		DialogueNode thanks = null;
		bool turnedIn = false;
		if (quest != null)
		{
			if (quest.Type == QuestType.TalkTo)
			{
				offer = new DialogueNode
				{
					Text = $"Could you go speak with {quest.TargetNpcName} for me? I'll give you {quest.RewardCoins} coins."
				};
				dialogueNode5 = new DialogueNode
				{
					Text = "Thank you! Come back once you've spoken with them."
				};
				reminder = new DialogueNode
				{
					DynamicText = () => "Have you talked to " + quest.TargetNpcName + " yet?"
				};
				thanks = new DialogueNode
				{
					Text = $"You spoke with them? Wonderful. Here, {quest.RewardCoins} coins."
				};
			}
			else if (quest.Type == QuestType.Explore)
			{
				offer = new DialogueNode
				{
					Text = $"Would you explore {quest.LocationName}? I need someone to check on it. {quest.RewardCoins} coins if you do."
				};
				dialogueNode5 = new DialogueNode
				{
					Text = "Excellent! Head to " + quest.LocationName + " and come back."
				};
				reminder = new DialogueNode
				{
					DynamicText = () => "Have you found " + quest.LocationName + " yet?"
				};
				thanks = new DialogueNode
				{
					Text = $"You made it there! Here are {quest.RewardCoins} coins."
				};
			}
			else
			{
				offer = new DialogueNode
				{
					Text = Fill(l.QuestOffer, id)
				};
				dialogueNode5 = new DialogueNode
				{
					Text = Fill(l.QuestAccept, id)
				};
				reminder = new DialogueNode
				{
					DynamicText = () => Fill(l.QuestReminder, id)
				};
				thanks = new DialogueNode
				{
					Text = Fill(l.QuestThanks, id)
				};
			}
			offer.Choices.Add(new DialogueChoice
			{
				Label = "I'll do it.",
				Next = dialogueNode5,
				OnChosen = delegate
				{
					if ((UnityEngine.Object)(object)QuestLog.Instance != (UnityEngine.Object)null)
					{
						QuestLog.Instance.Accept(quest);
					}
				}
			});
			offer.Choices.Add(new DialogueChoice
			{
				Label = "Not right now.",
				Next = hub
			});
			list.Add(dialogueNode5);
			list.Add(reminder);
			list.Add(thanks);
		}
		DialogueNode[] array = new DialogueNode[2] { dialogueNode, hub };
		foreach (DialogueNode dialogueNode6 in array)
		{
			if (!id.IsResident)
			{
				DialogueNode recruitYes = new DialogueNode
				{
					Text = "Really? That would be amazing! I'm moving in."
				};
				DialogueNode recruitNo = new DialogueNode
				{
					Text = "I'd love to, but you don't have an empty bed for me here."
				};
				DialogueNode dialogueNode7 = new DialogueNode
				{
					Text = "I've been wandering for a while..."
				};
				dialogueNode7.Choices.Add(new DialogueChoice
				{
					Label = "Want to live in my village?",
					NextResolver = delegate
					{
						if ((UnityEngine.Object)(object)VillageTotem.Instance != (UnityEngine.Object)null && VillageTotem.Instance.AvailableBeds > 0)
						{
							VillageTotem.Instance.AddResident(id);
							id.IsResident = true;
							NPCSpawner nPCSpawner = Object.FindAnyObjectByType<NPCSpawner>();
							if ((UnityEngine.Object)(object)nPCSpawner != (UnityEngine.Object)null)
							{
								nPCSpawner.RefreshNametag(id);
							}
							NPCWanderer component = ((Component)id).GetComponent<NPCWanderer>();
							if ((UnityEngine.Object)(object)component != (UnityEngine.Object)null)
							{
								component.Init(null, 2f, 1.5f, 5f, 15f, 2f, 6f, "Speed");
							}
							return recruitYes;
						}
						return recruitNo;
					}
				});
				dialogueNode7.Choices.Add(new DialogueChoice
				{
					Label = "Never mind.",
					Next = hub
				});
				list.Add(recruitYes);
				list.Add(recruitNo);
				if ((UnityEngine.Object)(object)VillageTotem.Instance != (UnityEngine.Object)null)
				{
					Vector3 val = ((Component)VillageTotem.Instance).transform.position - ((Component)id).transform.position;
					if (val.sqrMagnitude < 2500f)
					{
						dialogueNode6.Choices.Add(new DialogueChoice
						{
							Label = "Want to live here?",
							Next = dialogueNode7
						});
					}
				}
			}
			else
			{
				DialogueNode dialogueNode8 = new DialogueNode
				{
					DynamicText = () => "I live here now. I'm currently " + ((id.AssignedJob == "None") ? "taking a break" : ("gathering " + id.AssignedJob.Replace("gatherer", "").Replace("cutter", ""))) + ". What do you need me to do?"
				};
				DialogueNode dialogueNode9 = new DialogueNode
				{
					Text = "You got it! I'll chop some wood."
				};
				DialogueNode dialogueNode10 = new DialogueNode
				{
					Text = "Sure! I'll gather some stone."
				};
				DialogueNode dialogueNode11 = new DialogueNode
				{
					Text = "Phew, thanks! Let me know if you need anything."
				};
				list.Add(dialogueNode9);
				list.Add(dialogueNode10);
				list.Add(dialogueNode11);
				dialogueNode8.Choices.Add(new DialogueChoice
				{
					Label = "Gather Wood",
					Next = dialogueNode9,
					OnChosen = delegate
					{
						id.AssignedJob = "Woodcutter";
					}
				});
				dialogueNode8.Choices.Add(new DialogueChoice
				{
					Label = "Gather Stone",
					Next = dialogueNode10,
					OnChosen = delegate
					{
						id.AssignedJob = "Stonegatherer";
					}
				});
				dialogueNode8.Choices.Add(new DialogueChoice
				{
					Label = "Take a Break",
					Next = dialogueNode11,
					OnChosen = delegate
					{
						id.AssignedJob = "None";
					}
				});
				dialogueNode8.Choices.Add(new DialogueChoice
				{
					Label = "Never mind",
					Next = hub
				});
				dialogueNode6.Choices.Add(new DialogueChoice
				{
					Label = "Let's talk about your job.",
					Next = dialogueNode8
				});
			}
			dialogueNode6.Choices.Add(new DialogueChoice
			{
				Label = "Who are you?",
				Next = dialogueNode2
			});
			dialogueNode6.Choices.Add(new DialogueChoice
			{
				Label = "How's your day going?",
				Next = dialogueNode3
			});
			dialogueNode6.Choices.Add(new DialogueChoice
			{
				Label = "Got anything for me?",
				Next = dialogueNode4,
				OnChosen = delegate
				{
					giftedNow = false;
					if (!id.GaveGift && (UnityEngine.Object)(object)Inventory.Instance != (UnityEngine.Object)null)
					{
						Inventory.Instance.AddItem(l.GiftItem);
						id.GaveGift = true;
						giftedNow = true;
					}
				}
			});
			if (quest != null)
			{
				dialogueNode6.Choices.Add(MakeQuestChoice());
			}
			if (id.IsTrader)
			{
				dialogueNode6.Choices.Add(MakeTradeChoice());
			}
			dialogueNode6.Choices.Add(new DialogueChoice
			{
				Label = "Goodbye.",
				Next = next
			});
		}
		foreach (DialogueNode item in list)
		{
			item.Choices.Add(new DialogueChoice
			{
				Label = "I have another question.",
				Next = hub
			});
			item.Choices.Add(new DialogueChoice
			{
				Label = "Goodbye.",
				Next = next
			});
		}
		return dialogueNode;
		DialogueChoice MakeQuestChoice()
		{
			return new DialogueChoice
			{
				DynamicLabel = () => (quest.State != QuestState.Active) ? "Can I help you with anything?" : "About that favour...",
				Condition = () => quest.State != QuestState.Completed,
				OnChosen = delegate
				{
					turnedIn = quest.State == QuestState.Active && (UnityEngine.Object)(object)QuestLog.Instance != (UnityEngine.Object)null && QuestLog.Instance.TryComplete(quest);
				},
				NextResolver = () => (!turnedIn) ? ((quest.State != QuestState.Active) ? offer : reminder) : thanks
			};
		}
		DialogueChoice MakeTradeChoice()
		{
			return new DialogueChoice
			{
				Label = "Let's trade.",
				OpensShop = true,
				ShopGreeting = Fill(l.Trade, id),
				Next = hub
			};
		}
	}
}




