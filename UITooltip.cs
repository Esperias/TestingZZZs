using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UITooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public string ItemName = "";
    private static GameObject tooltipObj;
    private static Text tooltipText;
    private static RectTransform tooltipRect;

    private void Start()
    {
        if (tooltipObj == null)
        {
            GameObject canvasObj = new GameObject("TooltipCanvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 999;
            
            tooltipObj = new GameObject("GlobalTooltip");
            tooltipObj.transform.SetParent(canvas.transform, false);
            tooltipObj.transform.SetAsLastSibling();

            tooltipRect = tooltipObj.AddComponent<RectTransform>();
            tooltipRect.sizeDelta = new Vector2(150, 40);
            tooltipRect.pivot = new Vector2(0f, 1f); // Top left

            Image bg = tooltipObj.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.05f, 0.08f, 0.9f);
            bg.raycastTarget = false;
            
            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(tooltipObj.transform, false);
            tooltipText = textObj.AddComponent<Text>();
            tooltipText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tooltipText.fontSize = 13;
            tooltipText.color = Color.white;
            tooltipText.alignment = TextAnchor.MiddleCenter;
            tooltipText.raycastTarget = false;
            
            RectTransform txtRect = textObj.GetComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.offsetMin = txtRect.offsetMax = Vector2.zero;

            tooltipObj.SetActive(false);
        }
    }

    public static void HideTooltip()
    {
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }

    private void Update()
    {
        if (tooltipObj != null && tooltipObj.activeSelf)
        {
            #if ENABLE_INPUT_SYSTEM
            Vector2 mousePos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            #else
            Vector2 mousePos = Input.mousePosition;
            #endif
            tooltipRect.position = mousePos + new Vector2(15f, -15f);
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!string.IsNullOrEmpty(ItemName) && tooltipObj != null)
        {
            tooltipText.text = ItemName;
            // Estimate width from character count * font size * scale factor
            float estimatedWidth = ItemName.Length * tooltipText.fontSize * 0.62f + 20f;
            tooltipRect.sizeDelta = new Vector2(Mathf.Max(60f, estimatedWidth), 28f);

            tooltipObj.SetActive(true);
            tooltipObj.transform.SetAsLastSibling();
        }
    }

    private void OnDisable()
    {
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipObj != null)
        {
            tooltipObj.SetActive(false);
        }
    }



    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Right && Inventory.Instance != null && !string.IsNullOrEmpty(ItemName))
        {
            ItemDefinition item = null;
            foreach (var i in Inventory.AllItems)
            {
                if (i.DisplayName == ItemName) { item = i; break; }
            }
            if (item != null && Inventory.Instance.GetCount(item.Id) > 0)
            {
                float foodVal = 0f;
                float healVal = 0f;
                string id = item.Id.ToLower();
                if (id == "apple") { foodVal = 15f; healVal = 5f; }
                else if (id == "mushroom") { foodVal = 10f; }
                else if (id == "berries" || id == "berry") { foodVal = 15f; healVal = 5f; }
                else if (id == "wheat") { foodVal = 10f; }
                else if (id == "carrot") { foodVal = 20f; healVal = 10f; }
                else if (id == "meat") { foodVal = 40f; healVal = 25f; }
                

                if (foodVal > 0f && PlayerHealth.Instance != null)
                {
                    if (PlayerHealth.Instance.CurrentHunger >= PlayerHealth.Instance.maxHunger && PlayerHealth.Instance.CurrentHealth >= PlayerHealth.Instance.maxHealth)
                    {
                        Inventory.Instance.Notify("I am too full to eat!");
                    }
                    else
                    {
                        PlayerHealth.Instance.Eat(foodVal, healVal);
                        Inventory.Instance.RemoveItem(item.Id, 1);
                        Inventory.Instance.Notify($"Ate {item.DisplayName}");
                    }
                }
            }
        }
    }

}













