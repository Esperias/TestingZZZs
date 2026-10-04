using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Singleton that manages the currently "held" inventory item.
/// The floating icon follows the mouse and is destroyed on drop/cancel.
/// </summary>
public class UIInventoryCursor : MonoBehaviour
{
    public static UIInventoryCursor Instance { get; private set; }

    // The item currently being held (null = nothing held)
    public static string HeldItem { get; private set; }

    private static GameObject floatingIcon;
    private static RectTransform floatingRect;
    private static Canvas rootCanvas;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public static void PickUp(string itemId, Color itemColor, Canvas canvas)
    {
        if (!string.IsNullOrEmpty(HeldItem)) return;

        HeldItem = itemId;
        rootCanvas = canvas;

        // Spawn on a dedicated ScreenSpaceOverlay canvas so it's truly in front of everything
        GameObject canvasObj = new GameObject("CursorCanvas");
        Canvas cursorCanvas = canvasObj.AddComponent<Canvas>();
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.sortingOrder = 9999;
        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;

        floatingIcon = new GameObject("CursorItem");
        floatingRect = floatingIcon.AddComponent<RectTransform>();
        floatingIcon.transform.SetParent(cursorCanvas.transform, false);

        floatingRect.sizeDelta = new Vector2(48f, 48f);
        floatingRect.pivot = new Vector2(0.5f, 0.5f);
        floatingRect.localScale = Vector3.one;

        Image img = floatingIcon.AddComponent<Image>();
        img.color = itemColor;
        img.raycastTarget = false;

        // Apply the UIOverlay shader so it renders with ZTest Always (in front of terrain too)
        Shader overlayShader = Shader.Find("UI/Overlay");
        if (overlayShader != null)
            img.material = new Material(overlayShader);

        UITooltip.HideTooltip();
    }

    public static void Cancel()
    {
        HeldItem = null;
        if (floatingIcon != null)
        {
            // Destroy the whole CursorCanvas parent
            if (floatingIcon.transform.parent != null)
                Destroy(floatingIcon.transform.parent.gameObject);
            else
                Destroy(floatingIcon);
            floatingIcon = null;
        }
        Inventory.Instance?.Invoke("RefreshPanel", 0.01f);
    }

    private void Update()
    {
        if (floatingIcon == null) return;

#if ENABLE_INPUT_SYSTEM
        Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
#else
        Vector2 mousePos = Input.mousePosition;
#endif
        // Icon is on a ScreenSpaceOverlay canvas - position directly in screen space
        floatingRect.position = mousePos;

        // Right-click or Escape cancels
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame ||
            UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
#else
        if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
#endif
        {
            Cancel();
        }
    }
}




