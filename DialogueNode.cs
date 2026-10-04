using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using System;
using System.Collections.Generic;

public class DialogueNode
{
	public string Text;

	public Func<string> DynamicText;

	public readonly List<DialogueChoice> Choices = new List<DialogueChoice>();

	public string GetText()
	{
		if (DynamicText == null)
		{
			return Text;
		}
		return DynamicText();
	}

	public List<DialogueChoice> GetVisibleChoices()
	{
		List<DialogueChoice> list = new List<DialogueChoice>(Choices.Count);
		foreach (DialogueChoice choice in Choices)
		{
			if (choice.IsVisible())
			{
				list.Add(choice);
			}
		}
		return list;
	}
}




