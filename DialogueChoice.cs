using Random = UnityEngine.Random;
using Object = UnityEngine.Object;
using System;

public class DialogueChoice
{
	public string Label;

	public Func<string> DynamicLabel;

	public DialogueNode Next;

	public Func<DialogueNode> NextResolver;

	public Action OnChosen;

	public Func<bool> Condition;

	public bool OpensShop;

	public string ShopGreeting;

	public string GetLabel()
	{
		if (DynamicLabel == null)
		{
			return Label;
		}
		return DynamicLabel();
	}

	public bool IsVisible()
	{
		if (Condition != null)
		{
			return Condition();
		}
		return true;
	}

	public DialogueNode GetNext()
	{
		if (NextResolver == null)
		{
			return Next;
		}
		return NextResolver();
	}
}




