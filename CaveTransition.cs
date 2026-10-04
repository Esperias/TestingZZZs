using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Singleton — created automatically by WorldGenerator.
/// Handles the fade-to-black and scene swap between the world and the cave.
///
/// The singleton persists across scene loads (DontDestroyOnLoad).
/// The Player root is also kept alive during a scene transition then handed
/// back to the loaded scene so nothing gets duplicated.
/// </summary>
public class CaveTransition : MonoBehaviour
{

    public static CaveTransition Instance { get; private set; }

    /// <summary>Set by CaveEntrance so we know where to drop the player on exit.</summary>
    public static Vector3 EntranceWorldPos { get; set; }

    /// <summary>Name of the cave scene to load (must be in Build Settings).</summary>
    public const string CaveSceneName = "TestCave";

    private Image  fadeImage;
    private bool   transitioning;
    private string worldSceneName;   // captured just before we leave the world

    private const float FadeSpeed = 2.5f;

    // ──────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        BuildFadeOverlay();
    }

    // ── Public API ─────────────────────────────────────────────────────────────

    public void GoToCave()
    {
        if (transitioning) return;
        worldSceneName = SceneManager.GetActiveScene().name;
        StartCoroutine(TransitionToScene(CaveSceneName, placingInCave: true));
    }

    public void ReturnFromCave()
    {
        if (transitioning) return;
        StartCoroutine(TransitionToScene(worldSceneName, placingInCave: false));
    }

    // ── Internal ───────────────────────────────────────────────────────────────

    private IEnumerator TransitionToScene(string sceneName, bool placingInCave)
    {
        transitioning = true;

        // Find player root and camera now, before the scene unloads
        Camera overworldCam = Camera.main;
        AudioListener overworldListener = overworldCam != null ? overworldCam.GetComponent<AudioListener>() : null;
        if (overworldListener != null) overworldListener.enabled = false; // Mute our listener temporarily to avoid 2-listener spam when new scene loads
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        GameObject playerRoot = pc != null ? pc.transform.root.gameObject : null;

        // Disable input and physics during transition
        CharacterController cc = playerRoot != null
            ? playerRoot.GetComponent<CharacterController>() : null;
        if (cc) cc.enabled = false;

        yield return StartCoroutine(Fade(0f, 1f));   // black out

        // Keep player alive through the load
        if (playerRoot != null) DontDestroyOnLoad(playerRoot);
        
        // The camera is not a child of the player, so preserve it explicitly!
        if (overworldCam != null && overworldCam.transform.root != playerRoot.transform)
        {
            DontDestroyOnLoad(overworldCam.transform.root.gameObject);
        }

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        if (op == null)
        {
            Debug.LogError($"Scene '{sceneName}' could not be loaded! Please add it to File -> Build Settings.");
            if (cc != null) cc.enabled = true;
            yield return StartCoroutine(Fade(1f, 0f));
            transitioning = false;
            yield break;
        }
        while (!op.isDone) yield return null;

        // If we loaded the Cave, wait for CaveGenerator to finish baking and raycasting!
        if (placingInCave)
        {
            while (CaveGenerator.Instance == null || !CaveGenerator.Instance.IsReady)
            {
                yield return null;
            }
        }
        else
        {
            // Just wait a couple of frames for standard scenes
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
        }

        // Hand the player object back to the newly loaded scene
        if (playerRoot != null)
            SceneManager.MoveGameObjectToScene(playerRoot, SceneManager.GetActiveScene());
            
        if (overworldCam != null)
        {
            if (overworldCam.transform.root.gameObject.scene.name == "DontDestroyOnLoad")
                SceneManager.MoveGameObjectToScene(overworldCam.transform.root.gameObject, SceneManager.GetActiveScene());
                
            // Destroy any duplicate cameras (like the dummy one baked into TestCave)
            // This prevents the "2 audio listeners" warning and double rendering.
            foreach (Camera c in FindObjectsByType<Camera>(FindObjectsInactive.Exclude)) { if (c != overworldCam && c.gameObject.name == "Main Camera") { c.gameObject.SetActive(false); Destroy(c.gameObject); } }
            if (overworldListener != null) overworldListener.enabled = true;
        }

        // Position player at the correct spawn point
        if (playerRoot != null)
        {
            Vector3 dest;
            if (placingInCave)
            {
                // CaveGenerator registers a spawn point; fall back to origin
                dest = CaveGenerator.SpawnPoint;
            }
            else
            {
                // Exit the cave — drop just outside the entrance, facing outward
                dest = EntranceWorldPos + new Vector3(0f, 1f, 4f);
            }

            playerRoot.transform.position = dest;
            if (cc) cc.enabled = true;
        }

        yield return new WaitForSecondsRealtime(0.25f);
        yield return StartCoroutine(Fade(1f, 0f));   // fade in

        transitioning = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) { yield break; }
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime * FadeSpeed;
            Color c = fadeImage.color;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(t));
            fadeImage.color = c;
            yield return null;
        }
    }

    // ── UI ─────────────────────────────────────────────────────────────────────

    private void BuildFadeOverlay()
    {
        // Own canvas — lives on this DontDestroyOnLoad object so it survives loads
        Canvas canvas = GetComponentInChildren<Canvas>();
        if (canvas == null)
        {
            GameObject cGo = new GameObject("TransitionCanvas");
            cGo.transform.SetParent(transform);
            canvas = cGo.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;   // always on top
            cGo.AddComponent<CanvasScaler>();
            cGo.AddComponent<GraphicRaycaster>();
        }

        GameObject overlay = new GameObject("Fade");
        overlay.transform.SetParent(canvas.transform, false);
        fadeImage = overlay.AddComponent<Image>();
        fadeImage.color         = Color.clear;
        fadeImage.raycastTarget = false;

        RectTransform rt = overlay.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }
}




