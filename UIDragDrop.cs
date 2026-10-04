using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attached to each item swatch (colored square). Click to pick up; click again to cancel.
/// </summary>
public class UIDragDrop : MonoBehaviour, IPointerClickHandler
{
    public string ItemName = "";
    public Color ItemColor = Color.white;

    private Canvas rootCanvas;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
        if (rootCanvas != null && !rootCanvas.isRootCanvas)
            rootCanvas = rootCanvas.rootCanvas;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;

        // If holding THIS item already, cancel
        if (UIInventoryCursor.HeldItem == ItemName)
        {
            UIInventoryCursor.Cancel();
            return;
        }

        // If holding a DIFFERENT item, ignore (let the slot handle the drop)
        if (!string.IsNullOrEmpty(UIInventoryCursor.HeldItem)) return;

        // Pick it up
        UIInventoryCursor.PickUp(ItemName, ItemColor, rootCanvas);

        // Hide this swatch and any sibling text (count label) so the slot looks empty
        Image img = GetComponent<Image>();
        if (img != null) img.enabled = false;
        // Hide all Text siblings inside the parent slot
        if (transform.parent != null)
        {
            foreach (var txt in transform.parent.GetComponentsInChildren<Text>())
                txt.enabled = false;
        }
    }
}



