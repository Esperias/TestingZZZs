using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays a slim loading bar at the bottom of the screen while ResourceGenerator
/// and Inventory are asynchronously populating the world. Fades out when done.
/// Attach this to the Manager GameObject alongside the other scripts.
/// </summary>
public class WorldLoader : MonoBehaviour
{
    private Canvas canvas;
    private Image barBg;
    private Image barFill;
    private Text label;

    private float fadeAlpha = 1f;
    private bool allDone = false;
    private CanvasGroup group;

    private void Start()
    {
        BuildUI();
    }

    private void BuildUI()
    {
        // Root canvas — screen-space overlay, always on top
        GameObject canvasGO = new GameObject("WorldLoaderCanvas");
        DontDestroyOnLoad(canvasGO);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.AddComponent<GraphicRaycaster>();
        group = canvasGO.AddComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;

        // Dark background strip at the bottom
        GameObject bgGO = new GameObject("LoadBarBg");
        bgGO.transform.SetParent(canvasGO.transform, false);
        barBg = bgGO.AddComponent<Image>();
        barBg.color = new Color(0f, 0f, 0f, 0.7f);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = new Vector2(0f, 0f);
        bgRect.anchorMax = new Vector2(1f, 0f);
        bgRect.pivot     = new Vector2(0.5f, 0f);
        bgRect.sizeDelta = new Vector2(0f, 48f);
        bgRect.anchoredPosition = Vector2.zero;

        // Coloured fill
        GameObject fillGO = new GameObject("LoadBarFill");
        fillGO.transform.SetParent(bgGO.transform, false);
        barFill = fillGO.AddComponent<Image>();
        barFill.color = new Color(0.3f, 0.85f, 1f, 1f);
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = new Vector2(0f, 0f);
        fillRect.anchorMax = new Vector2(0f, 1f);
        fillRect.pivot     = new Vector2(0f, 0.5f);
        fillRect.sizeDelta = new Vector2(0f, -8f);
        fillRect.anchoredPosition = new Vector2(4f, 0f);

        // Label
        GameObject labelGO = new GameObject("LoadLabel");
        labelGO.transform.SetParent(bgGO.transform, false);
        label = labelGO.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAnchor.MiddleCenter;
        label.text = "Generating world...";
        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;
        labelRect.anchoredPosition = Vector2.zero;

        // Store screen width for fill scaling
        _screenWidth = Screen.width;
    }

    private float _screenWidth;

    private void Update()
    {
        if (allDone)
        {
            // Smooth fade out
            fadeAlpha -= Time.deltaTime * 1.5f;
            if (group != null) group.alpha = fadeAlpha;
            if (fadeAlpha <= 0f)
            {
                if (canvas != null) Destroy(canvas.gameObject);
                Destroy(this);
            }
            return;
        }

        float resourceProgress  = ResourceGenerator.Instance != null ? ResourceGenerator.Instance.Progress  : 1f;
        float pickupProgress    = Inventory.Instance          != null ? Inventory.Instance.PickupsProgress   : 1f;
        bool  resourcesDone     = ResourceGenerator.Instance != null ? ResourceGenerator.Instance.IsDone    : true;
        bool  pickupsDone       = Inventory.Instance          != null ? Inventory.Instance.PickupsDone       : true;

        float combined = (resourceProgress * 0.7f) + (pickupProgress * 0.3f);

        // Update fill width
        float targetWidth = (_screenWidth - 8f) * combined;
        RectTransform fillRect = barFill.GetComponent<RectTransform>();
        fillRect.sizeDelta = new Vector2(targetWidth, fillRect.sizeDelta.y);

        // Update label
        if (label != null)
        {
            int pct = Mathf.RoundToInt(combined * 100f);
            if (!resourcesDone)       label.text = $"Generating world... {pct}%";
            else if (!pickupsDone)    label.text = $"Scattering items... {pct}%";
            else                      label.text = "Done!";
        }

        if (resourcesDone && pickupsDone)
            allDone = true;
    }
}




