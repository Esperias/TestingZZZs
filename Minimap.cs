using UnityEngine;
using UnityEngine.UI;

public class Minimap : MonoBehaviour
{
    private Camera minimapCamera;
    private bool isMinimapActive = false;
    private float minimapRenderTimer;
    private RectTransform playerIndicator;
    private Transform player;

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        // 1. Create a dedicated UI Canvas for Minimap
        GameObject canvasGO = new GameObject("Minimap Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // 2. Create RenderTexture
        RenderTexture rt = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
        rt.Create();

        // 3. Create Orthographic Camera
        GameObject camGO = new GameObject("Minimap Camera");
        minimapCamera = camGO.AddComponent<Camera>();
        minimapCamera.orthographic = true;
        minimapCamera.orthographicSize = 15f;
        minimapCamera.nearClipPlane = 0.1f;
        minimapCamera.targetTexture = rt;
        minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        minimapCamera.clearFlags = CameraClearFlags.SolidColor;
        minimapCamera.backgroundColor = new Color(0.1f, 0.5f, 0.1f);
                // Only render Ground (3), Water (4), and MapWater (13) layers. Skip trees/rocks (Default 0) completely!
        minimapCamera.cullingMask = (1 << 3) | (1 << 4) | (1 << 13);

        // The minimap is a tiny 256px view: no shadows / post-processing / AA, and it is rendered manually at ~25Hz
        // instead of re-rendering the world a second time every single frame.
        var urpData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        urpData.renderShadows = false;
        urpData.renderPostProcessing = false;
        urpData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                minimapCamera.allowMSAA = false;
        minimapCamera.enabled = GameSettings.ShowMinimap; // Enable it so Unity renders it normally (multithreaded)

        // 4. Create UI Image parent (mask)
        GameObject parentGO = new GameObject("Minimap Image");
        parentGO.transform.SetParent(canvasGO.transform, false);
        RectTransform parentRT = parentGO.AddComponent<RectTransform>();
        parentRT.anchorMin = new Vector2(1, 1);
        parentRT.anchorMax = new Vector2(1, 1);
        parentRT.pivot = new Vector2(1, 1);
        parentRT.anchoredPosition = new Vector2(-20, -20);
        parentRT.sizeDelta = new Vector2(200, 200);

        Image maskImg = parentGO.AddComponent<Image>();
        maskImg.color = Color.white;
        Texture2D tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
        {
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(32, 32), new Vector2(x, y));
                tex.SetPixel(x, y, d < 31.5f ? Color.white : Color.clear);
            }
        }
        tex.Apply();
        Sprite circleSprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f));
        maskImg.sprite = circleSprite;
        
        Mask mask = parentGO.AddComponent<Mask>();
        mask.showMaskGraphic = false;
        
        // 5. Create RawImage for render texture
        GameObject rawImageGO = new GameObject("RenderTexture");
        rawImageGO.transform.SetParent(parentGO.transform, false);
        RawImage rawImage = rawImageGO.AddComponent<RawImage>();
        rawImage.texture = rt;
        rawImage.rectTransform.anchorMin = Vector2.zero;
        rawImage.rectTransform.anchorMax = Vector2.one;
        rawImage.rectTransform.sizeDelta = Vector2.zero;
        rawImage.rectTransform.anchoredPosition = Vector2.zero;

        // 6. Create Player Indicator
        GameObject indicatorGO = new GameObject("Player Indicator");
        indicatorGO.transform.SetParent(parentGO.transform, false);
        playerIndicator = indicatorGO.AddComponent<RectTransform>();
        playerIndicator.anchorMin = new Vector2(0.5f, 0.5f);
        playerIndicator.anchorMax = new Vector2(0.5f, 0.5f);
        playerIndicator.sizeDelta = new Vector2(12, 12);
        Image indImg = indicatorGO.AddComponent<Image>();
        indImg.color = Color.red;
        Toggle(GameSettings.ShowMinimap);
    }    public void Toggle(bool show)
    {
        if (playerIndicator != null && playerIndicator.parent != null)
        {
            playerIndicator.parent.gameObject.SetActive(show);
        }
        if (minimapCamera != null)
        {
            minimapCamera.enabled = show; // Natively scheduled RenderGraph
            isMinimapActive = show;
        }
    }

    private void LateUpdate()
    {
        if (minimapCamera == null) return;
        if (player == null) {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null) return;
        }
        
        Vector3 camPos = player.position;
        // Dynamically adjust height to avoid clipping overworld structures but still stay under cave roofs
        if (Physics.Raycast(player.position + Vector3.up * 1f, Vector3.up, out RaycastHit hit, 150f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            camPos.y = hit.point.y - 1f; // Just below the roof
            if (camPos.y < player.position.y + 5f) camPos.y = player.position.y + 5f; // minimum height
        }
        else
        {
            camPos.y += 150f; // High up in the sky to see over big structures
        }
        minimapCamera.transform.position = camPos;

        if (playerIndicator != null)
        {
            float yRot = player.eulerAngles.y;
            playerIndicator.rotation = Quaternion.Euler(0, 0, -yRot);
        }
    }
}

















