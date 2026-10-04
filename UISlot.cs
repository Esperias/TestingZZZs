using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Attached to each slot background. Highlights on hover when an item is held,
/// and accepts a click to drop the held item into this slot.
/// </summary>
public class UISlot : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public int SlotIndex = -1;

    private Image bg;
    private Color normalColor;
    private static readonly Color hoverColor = new Color(0.4f, 0.55f, 0.75f, 0.95f);

    private void Awake()
    {
        bg = GetComponent<Image>();
        if (bg != null) normalColor = bg.color;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (string.IsNullOrEmpty(UIInventoryCursor.HeldItem)) return;
        if (bg != null) bg.color = hoverColor;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (bg != null) bg.color = normalColor;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        
        if (string.IsNullOrEmpty(UIInventoryCursor.HeldItem))
        {
            UIDragDrop dd = GetComponentInChildren<UIDragDrop>();
            if (dd != null) dd.OnPointerClick(eventData);
            return;
        }
        if (SlotIndex < 0 || SlotIndex >= 36) return;

        var io = Inventory.Instance.itemOrder;
        string held = UIInventoryCursor.HeldItem;
        int oldIndex = io.IndexOf(held);
        if (oldIndex < 0) { UIInventoryCursor.Cancel(); return; }

        // Only drop on empty slot (or same slot)
        bool targetEmpty = string.IsNullOrEmpty(io[SlotIndex]);
        if (!targetEmpty && SlotIndex != oldIndex)
        {
            // Slot occupied - flash red briefly to signal rejection
            if (bg != null) bg.color = new Color(0.7f, 0.2f, 0.2f, 0.95f);
            Invoke(nameof(ResetColor), 0.25f);
            return;
        }

        io[SlotIndex] = held;
        if (SlotIndex != oldIndex) io[oldIndex] = "";

        // Clear cursor state BEFORE refresh so the floating icon is gone
        UIInventoryCursor.Cancel();
        // Cancel already calls RefreshPanel, but force it immediately
        Inventory.Instance.Invoke("RefreshPanel", 0.05f);
    }

    private void ResetColor()
    {
        if (bg != null) bg.color = normalColor;
    }
}




