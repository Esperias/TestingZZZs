using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class LoadingScreen : MonoBehaviour
{
    public static bool Active = false;
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticsForFastPlayMode() { Active = false; }

    private GameObject canvasObj;
    private Image bgImage;
    private RectTransform fillRect;
    private Text loadingText;
    
    private int initialTotalChunks = 0;
    
    private void Start()
    {
        // Setup Canvas
        canvasObj = new GameObject("LoadingScreenCanvas");
        Canvas c = canvasObj.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 9999;
        
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        
        canvasObj.AddComponent<GraphicRaycaster>();
        
        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        
        // Try to generate dirt texture
        Texture2D dirtTex = GenerateDirtTexture();
        if (dirtTex != null)
        {
            RawImage rawBg = bgObj.AddComponent<RawImage>();
            rawBg.rectTransform.anchorMin = Vector2.zero;
            rawBg.rectTransform.anchorMax = Vector2.one;
            rawBg.rectTransform.sizeDelta = Vector2.zero;
            rawBg.texture = dirtTex;
            rawBg.uvRect = new Rect(0, 0, Screen.width / 64f, Screen.height / 64f);
            rawBg.color = Color.white;
        }
        else
        {
            Image bgImg = bgObj.AddComponent<Image>();
            bgImg.rectTransform.anchorMin = Vector2.zero;
            bgImg.rectTransform.anchorMax = Vector2.one;
            bgImg.rectTransform.sizeDelta = Vector2.zero;
            bgImg.color = new Color(0.15f, 0.10f, 0.05f); // Dark dirt color
        }
        
        // Progress Bar Background
        GameObject barBgObj = new GameObject("BarBackground");
        barBgObj.transform.SetParent(canvasObj.transform, false);
        Image barBg = barBgObj.AddComponent<Image>();
        barBg.color = Color.black;
        barBg.rectTransform.sizeDelta = new Vector2(600, 40);
        barBg.rectTransform.anchoredPosition = new Vector2(0, -50);
        
        // Progress Bar Fill
        GameObject barFillObj = new GameObject("BarFill");
        barFillObj.transform.SetParent(barBgObj.transform, false);
        Image barFill = barFillObj.AddComponent<Image>();
        barFill.color = new Color(0.2f, 0.8f, 0.2f); // Minecraft green
        fillRect = barFill.rectTransform;
        fillRect.anchorMin = new Vector2(0, 0);
        fillRect.anchorMax = new Vector2(0, 1);
        fillRect.pivot = new Vector2(0, 0.5f);
        fillRect.sizeDelta = new Vector2(0, 0);
        fillRect.anchoredPosition = new Vector2(0, 0);
        
        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(canvasObj.transform, false);
        loadingText = textObj.AddComponent<Text>();
        loadingText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        loadingText.fontSize = 32;
        loadingText.alignment = TextAnchor.MiddleCenter;
        loadingText.color = Color.white;
        loadingText.text = "Building terrain...";
        loadingText.rectTransform.sizeDelta = new Vector2(600, 100);
        loadingText.rectTransform.anchoredPosition = new Vector2(0, 50);
        
        // Initialize chunk counts
        StartCoroutine(TrackLoading());
    }
    
    private Texture2D GenerateDirtTexture()
    {
        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGB24, false);
        tex.filterMode = FilterMode.Point;
        Color baseDirt = new Color(0.35f, 0.25f, 0.15f);
        Color darkDirt = new Color(0.25f, 0.15f, 0.05f);
        
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float noise = Mathf.PerlinNoise(x * 0.4f, y * 0.4f);
                tex.SetPixel(x, y, Color.Lerp(darkDirt, baseDirt, noise + Random.Range(-0.1f, 0.1f)));
            }
        }
        tex.Apply();
        return tex;
    }
    
    private IEnumerator TrackLoading()
    {
        // Freeze time and critical player systems
        Time.timeScale = 0f;
        AudioListener.pause = true;
        
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) pc.enabled = false;
        
        MonoBehaviour[] scripts = FindObjectsByType<MonoBehaviour>();
        foreach (var s in scripts) {
            string n = s.GetType().Name;
            if (n == "CameraController" || n == "PauseMenu" || n == "Inventory" || n == "PlayerAttack") {
                s.enabled = false;
            }
        }
        
        // Wait real-time since timeScale is 0
        yield return new WaitForSecondsRealtime(0.1f);
        
        if (WorldGenerator.Instance == null)
        {
            Active = false;
            Destroy(canvasObj);
            if (pc != null) pc.enabled = true;
            yield break;
        }
        
        int viewDist = WorldGenerator.Instance.viewDistance;
        // Total expected chunks for a square area around player
        initialTotalChunks = (viewDist * 2 + 1) * (viewDist * 2 + 1);
        

        
        while (true)
        {
            if (WorldGenerator.Instance == null) break;
            
            int generated = WorldGenerator.Instance.GeneratedChunkCount;
            float progress = Mathf.Clamp01((float)generated / initialTotalChunks);
            
            fillRect.anchorMax = new Vector2(progress, 1);
            
            loadingText.text = $"Building terrain... {Mathf.RoundToInt(progress * 100)}%";
            
            if (generated >= initialTotalChunks)
            {
                break;
            }
            
            yield return null;
        }
        
        // Pre-compile the map camera / shaders now so opening the map later doesn't hitch
        loadingText.text = "Preparing map...";
        if (WorldMap.Instance != null) yield return StartCoroutine(WorldMap.Instance.Prewarm());

        // Done
        loadingText.text = "Done!";
        fillRect.anchorMax = new Vector2(1, 1);
        
        yield return new WaitForSecondsRealtime(0.5f);
        
        Time.timeScale = 1f;
        AudioListener.pause = false;
        
        if (pc != null) pc.enabled = true;
        
        MonoBehaviour[] scriptsAfter = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include);
        foreach (var s in scriptsAfter) {
            string n = s.GetType().Name;
            if (n == "CameraController" || n == "PauseMenu" || n == "Inventory" || n == "PlayerAttack") {
                s.enabled = true;
            }
        }
        
        Active = false;
            Destroy(canvasObj);
    }
}




