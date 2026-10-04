using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using BindKey = UnityEngine.InputSystem.Key;
#else
using BindKey = UnityEngine.KeyCode;
#endif

// =====================================================================================
//  Everything for pausing, settings and keybinds lives in this one file.
//  Put PauseMenu on your Manager object. The UI is built by the script at runtime.
// =====================================================================================

/// <summary>Actions the player can rebind.</summary>
public enum GameAction { Forward, Backward, Left, Right, Jump, Sprint, Interact, Inventory, ToggleView, Journal, Map, Build, Till, FreeCursor, Hotbar1, Hotbar2, Hotbar3, Hotbar4, Hotbar5, Hotbar6, Hotbar7, Hotbar8, Hotbar9 }

/// <summary>
/// Saved keybinds. Other scripts ask this instead of checking keys directly,
/// e.g. Keybinds.IsHeld(GameAction.Sprint).
/// </summary>
public static class Keybinds
{
    private static readonly Dictionary<GameAction, BindKey[]> defaults = new Dictionary<GameAction, BindKey[]>
    {
        { GameAction.Forward,  new BindKey[] { BindKey.W, default(BindKey), default(BindKey) } },
        { GameAction.Backward, new BindKey[] { BindKey.S, default(BindKey), default(BindKey) } },
        { GameAction.Left,     new BindKey[] { BindKey.A, default(BindKey), default(BindKey) } },
        { GameAction.Right,    new BindKey[] { BindKey.D, default(BindKey), default(BindKey) } },
        { GameAction.Jump,     new BindKey[] { BindKey.Space, default(BindKey), default(BindKey) } },
        { GameAction.Sprint,   new BindKey[] { BindKey.LeftShift, default(BindKey), default(BindKey) } },
        { GameAction.Interact, new BindKey[] { BindKey.E, default(BindKey), default(BindKey) } },
        { GameAction.Inventory, new BindKey[] { BindKey.I, default(BindKey), default(BindKey) } },
        { GameAction.ToggleView, new BindKey[] { BindKey.Tab, default(BindKey), default(BindKey) } },
        { GameAction.Journal, new BindKey[] { BindKey.J, default(BindKey), default(BindKey) } },
        { GameAction.Map, new BindKey[] { BindKey.M, default(BindKey), default(BindKey) } },
        { GameAction.Build, new BindKey[] { BindKey.B, default(BindKey), default(BindKey) } },
        { GameAction.Till, new BindKey[] { BindKey.G, default(BindKey), default(BindKey) } },
#if ENABLE_INPUT_SYSTEM
        { GameAction.FreeCursor, new BindKey[] { BindKey.LeftCtrl, default(BindKey), default(BindKey) } },
#else
        { GameAction.FreeCursor, new BindKey[] { BindKey.LeftControl, default(BindKey), default(BindKey) } },
#endif
#if ENABLE_INPUT_SYSTEM
        { GameAction.Hotbar1, new BindKey[] { BindKey.Digit1, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar2, new BindKey[] { BindKey.Digit2, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar3, new BindKey[] { BindKey.Digit3, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar4, new BindKey[] { BindKey.Digit4, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar5, new BindKey[] { BindKey.Digit5, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar6, new BindKey[] { BindKey.Digit6, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar7, new BindKey[] { BindKey.Digit7, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar8, new BindKey[] { BindKey.Digit8, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar9, new BindKey[] { BindKey.Digit9, default(BindKey), default(BindKey) } },
#else
        { GameAction.Hotbar1, new BindKey[] { BindKey.Alpha1, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar2, new BindKey[] { BindKey.Alpha2, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar3, new BindKey[] { BindKey.Alpha3, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar4, new BindKey[] { BindKey.Alpha4, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar5, new BindKey[] { BindKey.Alpha5, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar6, new BindKey[] { BindKey.Alpha6, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar7, new BindKey[] { BindKey.Alpha7, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar8, new BindKey[] { BindKey.Alpha8, default(BindKey), default(BindKey) } },
        { GameAction.Hotbar9, new BindKey[] { BindKey.Alpha9, default(BindKey), default(BindKey) } },
#endif
    };

    private static readonly Dictionary<GameAction, BindKey[]> current = new Dictionary<GameAction, BindKey[]>();
    private static bool loaded;

    private static string PrefKey(GameAction a, int slot) => "keybind." + a + "." + slot;

    private static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true;

        foreach (KeyValuePair<GameAction, BindKey[]> kv in defaults)
        {
            BindKey[] keys = new BindKey[3];
            for (int i = 0; i < 3; i++)
            {
                keys[i] = kv.Value[i];
                string saved = PlayerPrefs.GetString(PrefKey(kv.Key, i), "");
                if (saved != "" && System.Enum.TryParse(saved, out BindKey parsed)
                    && System.Enum.IsDefined(typeof(BindKey), parsed)) // Allow default(BindKey) which is None
                {
                    keys[i] = parsed;
                }
            }
            current[kv.Key] = keys;
        }
    }

    public static BindKey Get(GameAction action, int slot)
    {
        EnsureLoaded();
        return current[action][slot];
    }

    /// <summary>Binds a key to a specific slot. Clears conflicting bindings.</summary>
    public static void Set(GameAction action, int slot, BindKey key)
    {
        EnsureLoaded();

        if (!key.Equals(default(BindKey)))
        {
            foreach (GameAction other in System.Enum.GetValues(typeof(GameAction)))
            {
                for (int i = 0; i < 3; i++)
                {
                    if (current[other][i].Equals(key))
                    {
                        current[other][i] = default(BindKey);
                        PlayerPrefs.SetString(PrefKey(other, i), default(BindKey).ToString());
                    }
                }
            }
        }

        current[action][slot] = key;
        PlayerPrefs.SetString(PrefKey(action, slot), key.ToString());
        PlayerPrefs.Save();
    }

    public static void ResetToDefaults()
    {
        EnsureLoaded();
        foreach (KeyValuePair<GameAction, BindKey[]> kv in defaults)
        {
            for (int i = 0; i < 3; i++)
            {
                current[kv.Key][i] = kv.Value[i];
                PlayerPrefs.DeleteKey(PrefKey(kv.Key, i));
            }
        }
        PlayerPrefs.Save();
    }

        public static bool IsHeld(GameAction action)
    {
        EnsureLoaded();
        BindKey[] keys = current[action];
#if ENABLE_INPUT_SYSTEM
        for (int i = 0; i < 3; i++) {
            int code = (int)(object)keys[i];
            if (code == 0) continue;
            if (code >= 200) {
                var ms = Mouse.current;
                if (ms == null) continue;
                if (code == 200 && ms.leftButton.isPressed) return true;
                if (code == 201 && ms.rightButton.isPressed) return true;
                if (code == 202 && ms.middleButton.isPressed) return true;
                if (code == 203 && ms.forwardButton.isPressed) return true;
                if (code == 204 && ms.backButton.isPressed) return true;
            } else {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[(Key)code].isPressed) return true;
            }
        }
        return false;
#else
        for (int i = 0; i < 3; i++) {
            if (!keys[i].Equals(default(BindKey)) && Input.GetKey(keys[i])) return true;
        }
        return false;
#endif
    }

    public static bool WasPressed(GameAction action)
    {
        EnsureLoaded();
        BindKey[] keys = current[action];
#if ENABLE_INPUT_SYSTEM
        for (int i = 0; i < 3; i++) {
            int code = (int)(object)keys[i];
            if (code == 0) continue;
            if (code >= 200) {
                var ms = Mouse.current;
                if (ms == null) continue;
                if (code == 200 && ms.leftButton.wasPressedThisFrame) return true;
                if (code == 201 && ms.rightButton.wasPressedThisFrame) return true;
                if (code == 202 && ms.middleButton.wasPressedThisFrame) return true;
                if (code == 203 && ms.forwardButton.wasPressedThisFrame) return true;
                if (code == 204 && ms.backButton.wasPressedThisFrame) return true;
            } else {
                Keyboard kb = Keyboard.current;
                if (kb != null && kb[(Key)code].wasPressedThisFrame) return true;
            }
        }
        return false;
#else
        for (int i = 0; i < 3; i++) {
            if (!keys[i].Equals(default(BindKey)) && Input.GetKeyDown(keys[i])) return true;
        }
        return false;
#endif
    }

    public static BindKey Get(GameAction action)
    {
        EnsureLoaded();
        for (int i = 0; i < 3; i++) {
            if ((int)(object)current[action][i] != 0) return current[action][i];
        }
        return default(BindKey);
    }

    public static string KeyName(GameAction action)
    {
        EnsureLoaded();
        for (int i = 0; i < 3; i++) {
            if ((int)(object)current[action][i] != 0) return KeyName(action, i);
        }
        return "Unbound";
    }

    public static string KeyName(GameAction action, int slot)
    {
        EnsureLoaded();
        int code = (int)(object)current[action][slot];
        if (code == 0) return "None";
        if (code == 200) return "Left Click";
        if (code == 201) return "Right Click";
        if (code == 202) return "Middle Click";
        if (code == 203) return "Mouse Forward";
        if (code == 204) return "Mouse Back";
        
        string name = current[action][slot].ToString().Replace("Digit", "").Replace("Alpha", "");
        return Regex.Replace(name, "(?<=[a-z0-9])(?=[A-Z])", " ");
    }

    public static string ActionLabel(GameAction action)
    {
        switch (action)
        {
            case GameAction.Forward:  return "Move Forward";
            case GameAction.Backward: return "Move Backward";
            case GameAction.Left:     return "Move Left";
            case GameAction.Right:    return "Move Right";
            case GameAction.Jump:     return "Jump";
            case GameAction.Sprint:   return "Sprint";
            case GameAction.Interact: return "Interact / Talk";
            case GameAction.Inventory: return "Inventory";
            case GameAction.ToggleView: return "Toggle Third Person";
            case GameAction.Journal:  return "Journal";
            case GameAction.Map:      return "Map";
            case GameAction.Till: return "Till Ground";
            case GameAction.FreeCursor: return "Free Cursor";
            default:                  return action.ToString();
        }
    }

    /// <summary>Used while rebinding: returns the key pressed this frame (Escape is ignored).</summary>
        public static bool TryGetPressedKey(out BindKey key)
    {
        key = default;
#if ENABLE_INPUT_SYSTEM
        Keyboard kb = Keyboard.current;
        if (kb != null) {
            foreach (var control in kb.allKeys)
            {
                if (!control.wasPressedThisFrame) continue;
                if (control.keyCode == Key.Escape || control.keyCode == Key.None) continue;
                key = control.keyCode;
                return true;
            }
        }
        Mouse ms = Mouse.current;
        if (ms != null) {
            if (ms.leftButton.wasPressedThisFrame) { key = (BindKey)(object)200; return true; }
            if (ms.rightButton.wasPressedThisFrame) { key = (BindKey)(object)201; return true; }
            if (ms.middleButton.wasPressedThisFrame) { key = (BindKey)(object)202; return true; }
            if (ms.forwardButton.wasPressedThisFrame) { key = (BindKey)(object)203; return true; }
            if (ms.backButton.wasPressedThisFrame) { key = (BindKey)(object)204; return true; }
        }
        return false;
#else
        foreach (KeyCode code in System.Enum.GetValues(typeof(KeyCode)))
        {
            if (code == KeyCode.None || code == KeyCode.Escape) continue;
            if (code.ToString().StartsWith("Joystick")) continue;
            if (Input.GetKeyDown(code))
            {
                key = code;
                return true;
            }
        }
        return false;
#endif
    }
}

/// <summary>Saved settings. Other scripts read these (e.g. CameraController uses Sensitivity).</summary>
public static class GameSettings
{
    public static float Sensitivity = 1f;   // multiplier on the camera's own sensitivity
    public static bool InvertY = false;
    public static float Fov = 0f;           // 0 = not chosen yet, keep the camera's own value
    public static float MasterVolume = 1f;
    public static float MusicVolume = 1f;   // saved for when you add music
    public static float SfxVolume = 1f;     // saved for when you add sound effects
    public static bool VSync = true;
    public static int Quality = 0;
    public static int Framerate = 0;
    public static float RenderScale = 1f;
    public static int RenderDistance = 4;
    public static float CameraDistance = 4f;
    public static bool ToggleSprint = false;
    public static bool Shadows = true;
    public static int AntiAliasing = 0; // 0, 2, 4, 8
    public static int VisibilityQ = 1;
    public static int AntiAliasingQ = 1;
    public static int PostProcessingQ = 1;
    public static int ShadowsQ = 1;
    public static int GlobalIlluminationQ = 1;
    public static int ReflectionsQ = 1;
    public static int TexturesQ = 1;
    public static int EffectQ = 1;
    public static int VegetationQ = 1;
    public static int ShaderQualityQ = 1;
    public static int ObjectQ = 1;
    public static int PhysicsQ = 1;
    public static bool MotionBlur = false;
    public static bool ShowFps = false;
    public static bool ShowFrameTime = false;
    public static bool ShowLows = false;
    public static bool ShowPacing = false;
    public static bool ShowHitches = false;
    public static bool ShowMemory = false;
    public static bool ShowLatency = false;
    public static bool ShowMinimap = true;


    public static void Load()
    {
        Sensitivity = PlayerPrefs.GetFloat("settings.sensitivity", 1f);
        InvertY = PlayerPrefs.GetInt("settings.invertY", 0) == 1;
        Fov = PlayerPrefs.GetFloat("settings.fov", 0f);
        MasterVolume = PlayerPrefs.GetFloat("settings.master", 1f);
        MusicVolume = PlayerPrefs.GetFloat("settings.music", 1f);
        SfxVolume = PlayerPrefs.GetFloat("settings.sfx", 1f);
        VSync = PlayerPrefs.GetInt("settings.vsync", QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
        Quality = PlayerPrefs.GetInt("settings.quality", QualitySettings.GetQualityLevel());
        Framerate = PlayerPrefs.GetInt("settings.framerate", 0);
        Application.targetFrameRate = Framerate <= 0 ? -1 : Framerate;
        RenderScale = PlayerPrefs.GetFloat("settings.renderscale", 1f);
        RenderDistance = PlayerPrefs.GetInt("settings.renderdist", 4);
		CameraDistance = PlayerPrefs.GetFloat("settings.camdist", 4f);
        ToggleSprint = PlayerPrefs.GetInt("settings.togglesprint", 0) == 1;
        Shadows = PlayerPrefs.GetInt("settings.shadows", QualitySettings.shadows != ShadowQuality.Disable ? 1 : 0) == 1;
        AntiAliasing = PlayerPrefs.GetInt("settings.aa", 0);
        VisibilityQ = PlayerPrefs.GetInt("settings.vis", 1);
        AntiAliasingQ = PlayerPrefs.GetInt("settings.aaq", 1);
        PostProcessingQ = PlayerPrefs.GetInt("settings.ppq", 1);
        ShadowsQ = PlayerPrefs.GetInt("settings.shadq", 1);
        GlobalIlluminationQ = PlayerPrefs.GetInt("settings.giq", 1);
        ReflectionsQ = PlayerPrefs.GetInt("settings.refq", 1);
        TexturesQ = PlayerPrefs.GetInt("settings.texq", 1);
        EffectQ = PlayerPrefs.GetInt("settings.effq", 1);
        VegetationQ = PlayerPrefs.GetInt("settings.vegq", 1);
        ShaderQualityQ = PlayerPrefs.GetInt("settings.shadqualq", 1);
        ObjectQ = PlayerPrefs.GetInt("settings.objq", 1);
        PhysicsQ = PlayerPrefs.GetInt("settings.physq", 1);
        MotionBlur = PlayerPrefs.GetInt("settings.mb", 0) == 1;
        ShowFps = PlayerPrefs.GetInt("settings.fps", 0) == 1;
        ShowFrameTime = PlayerPrefs.GetInt("settings.perf.ft", 0) == 1;
        ShowLows = PlayerPrefs.GetInt("settings.perf.lows", 0) == 1;
        ShowPacing = PlayerPrefs.GetInt("settings.perf.pacing", 0) == 1;
        ShowHitches = PlayerPrefs.GetInt("settings.perf.hitch", 0) == 1;
        ShowMemory = PlayerPrefs.GetInt("settings.perf.mem", 0) == 1;
        ShowLatency = PlayerPrefs.GetInt("settings.perf.lat", 0) == 1;
        ShowMinimap = PlayerPrefs.GetInt("settings.minimap", 1) == 1;

        // Apply all engine/hardware settings immediately.
        ApplyEngineSettings();
    }

    /// <summary>
    /// Applies all Unity engine-level settings from the current GameSettings values.
    /// Can be called any time settings change, and also runs automatically before the scene loads.
    /// </summary>
    public static void ApplyEngineSettings()
    {
        // Quality preset
        int levels = QualitySettings.names.Length;
        if (Quality >= 0 && Quality < levels && Quality != QualitySettings.GetQualityLevel())
            QualitySettings.SetQualityLevel(Quality, false);

        // VSync / frame cap
        QualitySettings.vSyncCount = VSync ? 1 : 0;
        Application.targetFrameRate = Framerate <= 0 ? -1 : Framerate;

        // URP pipeline asset settings
        var pipeline = QualitySettings.renderPipeline != null
            ? QualitySettings.renderPipeline
            : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
        var urp = pipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
        if (urp != null)
        {
            urp.renderScale = RenderScale;
            urp.msaaSampleCount = 1; // hardware MSAA off; SMAA/FXAA handled per-camera

            int sq = Mathf.Clamp(ShadowsQ, 0, 3);
            if (sq == 0) { QualitySettings.shadows = ShadowQuality.Disable; urp.shadowDistance = 0f; }
            else
            {
                QualitySettings.shadows = ShadowQuality.All;
                float[] sDists = { 0f, 40f, 75f, 150f };
                urp.shadowDistance = sDists[sq];
                urp.shadowCascadeCount = sq == 1 ? 1 : (sq == 2 ? 2 : 4);
            }

            urp.supportsCameraDepthTexture  = EffectQ > 0;
            urp.supportsCameraOpaqueTexture = EffectQ > 2;
        }

        QualitySettings.globalTextureMipmapLimit = 3 - Mathf.Clamp(TexturesQ, 0, 3);
        float[] bias  = { 0.5f, 1.0f, 1.5f, 2.0f };
        QualitySettings.lodBias = bias[Mathf.Clamp(ObjectQ, 0, 3)];
        float[] pSteps = { 0.04f, 0.0333f, 0.02f, 0.0166f };
        Time.fixedDeltaTime = pSteps[Mathf.Clamp(PhysicsQ, 0, 3)];
        QualitySettings.realtimeReflectionProbes = ReflectionsQ > 0;
        QualitySettings.softVegetation  = VegetationQ > 0;
        QualitySettings.particleRaycastBudget = (EffectQ + 1) * 64;
        Shader.SetGlobalFloat("_GlobalShaderQuality", ShaderQualityQ);
        AudioListener.volume = MasterVolume;
    }

    /// <summary>Automatically load and apply saved settings before any scene object starts.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoApplyOnStart()
    {
        Load();
    }

    /// <summary>Re-apply after Unity's own quality preset reset has finished so our overrides stick.</summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoApplyAfterSceneLoad()
    {
        ApplyEngineSettings();
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat("settings.sensitivity", Sensitivity);
        PlayerPrefs.SetInt("settings.invertY", InvertY ? 1 : 0);
        PlayerPrefs.SetFloat("settings.fov", Fov);
        PlayerPrefs.SetFloat("settings.master", MasterVolume);
        PlayerPrefs.SetFloat("settings.music", MusicVolume);
        PlayerPrefs.SetFloat("settings.sfx", SfxVolume);
        PlayerPrefs.SetInt("settings.vsync", VSync ? 1 : 0);
        PlayerPrefs.SetInt("settings.minimap", ShowMinimap ? 1 : 0);
        PlayerPrefs.SetInt("settings.quality", Quality);
        PlayerPrefs.SetInt("settings.framerate", Framerate);
        PlayerPrefs.SetFloat("settings.renderscale", RenderScale);
        PlayerPrefs.SetInt("settings.framerate", Framerate);
        PlayerPrefs.SetFloat("settings.renderscale", RenderScale);
        PlayerPrefs.SetInt("settings.renderdist", RenderDistance);
		PlayerPrefs.SetFloat("settings.camdist", CameraDistance);
        PlayerPrefs.SetInt("settings.togglesprint", ToggleSprint ? 1 : 0);
        PlayerPrefs.SetInt("settings.shadows", Shadows ? 1 : 0);
        PlayerPrefs.SetInt("settings.aa", AntiAliasing);
        PlayerPrefs.SetInt("settings.vis", VisibilityQ);
        PlayerPrefs.SetInt("settings.aaq", AntiAliasingQ);
        PlayerPrefs.SetInt("settings.ppq", PostProcessingQ);
        PlayerPrefs.SetInt("settings.shadq", ShadowsQ);
        PlayerPrefs.SetInt("settings.giq", GlobalIlluminationQ);
        PlayerPrefs.SetInt("settings.refq", ReflectionsQ);
        PlayerPrefs.SetInt("settings.texq", TexturesQ);
        PlayerPrefs.SetInt("settings.effq", EffectQ);
        PlayerPrefs.SetInt("settings.vegq", VegetationQ);
        PlayerPrefs.SetInt("settings.shadqualq", ShaderQualityQ);
        PlayerPrefs.SetInt("settings.objq", ObjectQ);
        PlayerPrefs.SetInt("settings.physq", PhysicsQ);
        PlayerPrefs.SetInt("settings.mb", MotionBlur ? 1 : 0);
        PlayerPrefs.SetInt("settings.fps", ShowFps ? 1 : 0);
        PlayerPrefs.SetInt("settings.perf.ft", ShowFrameTime ? 1 : 0);
        PlayerPrefs.SetInt("settings.perf.lows", ShowLows ? 1 : 0);
        PlayerPrefs.SetInt("settings.perf.pacing", ShowPacing ? 1 : 0);
		PlayerPrefs.Save();
        PlayerPrefs.SetInt("settings.perf.hitch", ShowHitches ? 1 : 0);
        PlayerPrefs.SetInt("settings.perf.mem", ShowMemory ? 1 : 0);
        PlayerPrefs.SetInt("settings.perf.lat", ShowLatency ? 1 : 0);

        PlayerPrefs.Save();
    }
}

/// <summary>
/// Esc opens the pause menu (Resume / Settings / Quit). Settings has Game, Display, Audio
/// and Keybinds tabs. Time freezes and the player/camera are locked while paused.
/// </summary>
[DefaultExecutionOrder(-50)]
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    

    private static readonly string[] TabNames = { "Game", "Display", "Audio", "Keybinds" };
    private static readonly FullScreenMode[] ScreenModes =
        { FullScreenMode.ExclusiveFullScreen, FullScreenMode.FullScreenWindow, FullScreenMode.Windowed };
    private static readonly string[] ScreenModeNames = { "Exclusive Fullscreen", "Borderless Window", "Windowed" };

    private PlayerController player;
    private CameraController cameraController;
    private Camera cam;

    private GameObject canvasRoot;
    private GameObject pausePanel;
    private GameObject settingsPanel;
    private GameObject[] tabPanels;
    private Button[] tabButtons;
    private readonly Dictionary<GameAction, Text[]> keyLabels = new Dictionary<GameAction, Text[]>();

    private bool paused;
    private bool prevPlayerEnabled;
    private bool prevCameraEnabled;
    private float prevTimeScale = 1f;
    private GameAction? rebinding;
    private int rebindingSlot;
    private bool frameSkipped = false;
    private GameObject rebindOverlay;

    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int resolutionIndex;
    private int modeIndex;
    private int qualityIndex;
    private static readonly int[] Framerates = { 0, 30, 60, 120, 144 };
    private static readonly string[] FramerateNames = { "Unlimited", "30 FPS", "60 FPS", "120 FPS", "144 FPS" };
    private int framerateIndex;

    // ------------------------------------------------------------------ Lifecycle

    private void Awake()
    {
        GameSettings.Load();
    }

    private void Start()
    {
        player = FindAnyObjectByType<PlayerController>();
        cameraController = FindAnyObjectByType<CameraController>();
        cam = cameraController != null ? cameraController.GetComponent<Camera>() : null;
        if (cam == null) cam = Camera.main;

        ApplySavedSettings();
        FpsCounter.Ensure();
        BuildUI();
    }

    private void OnDestroy()
    {
        Time.timeScale = 1f;
    }

    private void OnApplicationQuit()
    {
        GameSettings.Save();
    }

        private void ApplySavedSettings()
    {
        // Apply all engine settings from the shared method (Quality, VSync, shadows, URP, etc.)
        GameSettings.ApplyEngineSettings();

        // Camera-component settings require a live Camera object, so handle them here.
        Camera cam = Camera.main;
        if (cam != null)
        {
            if (GameSettings.Fov > 0f) cam.fieldOfView = GameSettings.Fov;

            var camData = cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData != null)
            {
                int aa = Mathf.Clamp(GameSettings.AntiAliasingQ, 0, 3);
                if (aa == 0) camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.None;
                else if (aa == 1) camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.FastApproximateAntialiasing;
                else
                {
                    camData.antialiasing = UnityEngine.Rendering.Universal.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    camData.antialiasingQuality = aa == 2
                        ? UnityEngine.Rendering.Universal.AntialiasingQuality.Low
                        : UnityEngine.Rendering.Universal.AntialiasingQuality.High;
                }
                camData.renderPostProcessing = GameSettings.PostProcessingQ > 1;
            }
        }
    }

    private bool needsRebuild = false;
    private bool firstUpdate = true;
    private void Update()
    {
        if (firstUpdate)
        {
            firstUpdate = false;
            ApplySavedSettings();
        }
        if (needsRebuild) { needsRebuild = false; BuildUI(); }
        if (rebinding.HasValue)
        {
            if (!frameSkipped)
            {
                frameSkipped = true; // Wait 1 frame so the UI click doesn't register as the bind!
                return;
            }

            BindKey key;
            if (Keybinds.TryGetPressedKey(out key))
            {
                if (!key.Equals(default(BindKey)))
                {
                    Keybinds.Set(rebinding.Value, rebindingSlot, key);
                }
                CancelRebind();
            }
            return;
        }

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame)
#else
        if (Input.GetKeyDown(KeyCode.Escape))
#endif
        {
            // If the game is NOT paused, check if any other UI is open.
            // If another UI is open, we do nothing and let that script close its own UI.
            if (!paused)
            {
                bool uiOpen = false;
                if (Inventory.PanelOpen) uiOpen = true;
                if (NPCSpawner.DialogueOpen) uiOpen = true;
                if (WorldPanels.AnyOpen) { WorldPanels.CloseCurrent(); uiOpen = true; }
                BuildingManager bm = FindAnyObjectByType<BuildingManager>();
                if (bm != null && bm.IsInBuildMode) uiOpen = true;
                
                if (uiOpen) return; 
            }

            if (paused) Resume();
            else Pause();
        }
        if (paused) ApplyOverlayMaterial();
        AnimateHUD();
    }

    private void Pause()
    {
        paused = true; IsPaused = true;
        UIBillboardHider.SetHidden(true);
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        if (player != null)
        {
            prevPlayerEnabled = player.enabled;
            player.enabled = false;
            
            Animator anim = player.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.updateMode = AnimatorUpdateMode.UnscaledTime;
                anim.SetFloat("Speed", 0f);
            }
        }

        if (cameraController != null)
        {
            prevCameraEnabled = cameraController.enabled;
            cameraController.IsInMenu = true; // Triggers the over-the-shoulder zoom
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        canvasRoot.SetActive(true);
        
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas != null && cam != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            
            if (player != null) {
                canvasRoot.transform.SetParent(player.transform);
                canvasRoot.transform.localPosition = new Vector3(0.85f, 1.35f, 1.2f);
                canvasRoot.transform.localEulerAngles = new Vector3(0f, 14f, 0f);
                canvasRoot.transform.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);
            }
        }

        ShowPausePanel();
        ApplyOverlayMaterial();
    }

    private Material overlayMat;

    /// <summary>
    /// The pause canvas is world-space (next to the player), so give every graphic the ZTest-Always
    /// UI/Overlay material, like the inventory panel, otherwise the world clips through it.
    /// Run every frame while paused so freshly built elements (tabs, dropdown lists) get it too.
    /// </summary>
    private void ApplyOverlayMaterial()
    {
        if (canvasRoot == null) return;
        if (overlayMat == null)
        {
            Shader s = Shader.Find("UI/Overlay");
            if (s != null) overlayMat = new Material(s);
            if (overlayMat == null) return;
        }
        foreach (Graphic g in canvasRoot.GetComponentsInChildren<Graphic>(true))
        {
            if (g.material != overlayMat) g.material = overlayMat;
        }
    }

    private void RevertOverlayMaterial()
    {
        if (canvasRoot == null) return;
        foreach (Graphic g in canvasRoot.GetComponentsInChildren<Graphic>(true))
            g.material = null;
    }

    private void Resume()
    {
        paused = false; IsPaused = false;
        RevertOverlayMaterial();
        UIBillboardHider.SetHidden(false);
        Time.timeScale = prevTimeScale;

        if (player != null) 
        {
            player.enabled = prevPlayerEnabled;
            Animator anim = player.GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.updateMode = AnimatorUpdateMode.Normal;
            }
        }
        if (cameraController != null) 
        {
            cameraController.enabled = prevCameraEnabled;
            cameraController.IsInMenu = false; // Zooms back out
        }

        bool cursorFree = Keybinds.IsHeld(GameAction.FreeCursor) || Inventory.PanelOpen;
        Cursor.lockState = cursorFree ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = cursorFree;

        canvasRoot.SetActive(false);
        GameSettings.Save();
    }

    private void QuitGame()
    {
        GameSettings.Save();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ShowPausePanel()
    {
        CancelRebind();
        pausePanel.SetActive(true);
        settingsPanel.SetActive(false);
    }

    private void ShowSettingsPanel()
    {
        pausePanel.SetActive(false);
        settingsPanel.SetActive(true);
        SelectTab(0);
    }

    private void SelectTab(int index)
    {
        CancelRebind();
        for (int i = 0; i < tabPanels.Length; i++)
        {
            tabPanels[i].SetActive(i == index);

            ColorBlock cb = tabButtons[i].colors;
            cb.normalColor = i == index ? UIKit.Accent : UIKit.ButtonColor;
            cb.selectedColor = cb.normalColor;
            tabButtons[i].colors = cb;
        }
    }

    // ------------------------------------------------------------------- Keybinds

        private void BeginRebind(GameAction action, int slot)
    {
        rebinding = action;
        rebindingSlot = slot;
        frameSkipped = false; // We MUST skip one frame so the Left Click doesn't instantly bind!
        if (rebindOverlay != null) rebindOverlay.SetActive(true);
    }

    private void CancelRebind()
    {
        if (!rebinding.HasValue) return;
        rebinding = null;
        if (rebindOverlay != null) rebindOverlay.SetActive(false);
        RefreshKeybindLabels();
    }

    private void RefreshKeybindLabels()
    {
        foreach (KeyValuePair<GameAction, UnityEngine.UI.Text[]> kv in keyLabels)
        {
            for (int i = 0; i < 3; i++) {
                if (kv.Value[i] != null) kv.Value[i].text = Keybinds.KeyName(kv.Key, i);
            }
        }
    }

    // ------------------------------------------------------------------------ UI

        
        private void BuildUI()
    {
        EnsureEventSystem();

        bool wasActive = canvasRoot != null && canvasRoot.activeSelf;
        bool settingsWasOpen = settingsPanel != null && settingsPanel.activeSelf;

        if (canvasRoot != null) {
            canvasRoot.name = "Pause Menu UI (Destroyed)";
            Destroy(canvasRoot);
        }
        canvasRoot = UIKit.CreateCanvas("Pause Menu UI", 100);

        // Always make it a world-space canvas attached to the player!
        Canvas canvas = canvasRoot.GetComponent<Canvas>();
        if (canvas != null && cam != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = cam;
            
            if (player != null) {
                canvasRoot.transform.SetParent(player.transform);
                canvasRoot.transform.localPosition = new Vector3(0.85f, 1.35f, 1.2f);
                canvasRoot.transform.localEulerAngles = new Vector3(0f, 14f, 0f);
                canvasRoot.transform.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f);
            }
        }

        BuildPausePanel(canvasRoot.transform);
        BuildSettingsPanel(canvasRoot.transform);

        if (wasActive) {
            canvasRoot.SetActive(true);
            if (settingsWasOpen) ShowSettingsPanel();
            else ShowPausePanel();
            ApplyOverlayMaterial();
        } else {
            canvasRoot.SetActive(false);
        }
    }

    /// <summary>Makes sure a working EventSystem exists. Also called by NPCSpawner for the reply buttons.</summary>
    public static void EnsureEventSystem()
    {
        EventSystem es = FindAnyObjectByType<EventSystem>();
        if (es == null) es = new GameObject("EventSystem").AddComponent<EventSystem>();

#if ENABLE_INPUT_SYSTEM
        StandaloneInputModule oldModule = es.GetComponent<StandaloneInputModule>();
        if (oldModule != null)
        {
            oldModule.enabled = false;
            Destroy(oldModule);
        }
        if (es.GetComponent<InputSystemUIInputModule>() == null)
        {
            InputSystemUIInputModule module = es.gameObject.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }
#else
        if (es.GetComponent<StandaloneInputModule>() == null)
            es.gameObject.AddComponent<StandaloneInputModule>();
#endif
        // Mouse only, so Space/Enter never "clicks" a selected button (which would break rebinding)
        es.sendNavigationEvents = false;
    }

    
    private RectTransform minimapRT;
    private RectTransform healthRT;
    private RectTransform hungerRT;
    private RectTransform clockRT;
    private RectTransform notifRT;

    private void AnimateHUD()
    {
        if (minimapRT == null) { GameObject g = GameObject.Find("Minimap Image"); if (g) minimapRT = g.GetComponent<RectTransform>(); }
        if (healthRT == null) { GameObject g = GameObject.Find("HealthBar"); if (g) healthRT = g.GetComponent<RectTransform>(); }
        if (hungerRT == null) { GameObject g = GameObject.Find("HungerBar"); if (g) hungerRT = g.GetComponent<RectTransform>(); }
        if (clockRT == null) { GameObject g = GameObject.Find("Clock"); if (g) clockRT = g.GetComponent<RectTransform>(); }
        if (notifRT == null) { GameObject g = GameObject.Find("Notifications"); if (g) notifRT = g.GetComponent<RectTransform>(); }

        float dt = Time.unscaledDeltaTime * 15f;
        
        if (minimapRT != null) {
            float targetX = IsPaused ? 300f : -20f;
            minimapRT.anchoredPosition = Vector2.Lerp(minimapRT.anchoredPosition, new Vector2(targetX, minimapRT.anchoredPosition.y), dt);
        }
        
        if (healthRT != null) {
            float targetX = IsPaused ? -300f : 28f; // margin = 28f
            healthRT.anchoredPosition = Vector2.Lerp(healthRT.anchoredPosition, new Vector2(targetX, healthRT.anchoredPosition.y), dt);
        }
        
        if (hungerRT != null) {
            float targetX = IsPaused ? -300f : 28f;
            hungerRT.anchoredPosition = Vector2.Lerp(hungerRT.anchoredPosition, new Vector2(targetX, hungerRT.anchoredPosition.y), dt);
        }
        
        if (clockRT != null) {
            float targetY = IsPaused ? 100f : -24f;
            clockRT.anchoredPosition = Vector2.Lerp(clockRT.anchoredPosition, new Vector2(clockRT.anchoredPosition.x, targetY), dt);
        }
        
        if (notifRT != null) {
            float targetX = IsPaused ? 400f : -30f;
            notifRT.anchoredPosition = Vector2.Lerp(notifRT.anchoredPosition, new Vector2(targetX, notifRT.anchoredPosition.y), dt);
        }
    }

    private void BuildPausePanel(Transform root)
    {
        RectTransform panel = UIKit.CreateImage(root, "Pause Panel", new Color(0.1f, 0.11f, 0.14f, 0.95f));
        Center(panel, new Vector2(400f, 500f));
        UIKit.VLayout(panel.gameObject, 20f, 40, TextAnchor.MiddleCenter);

        Text title = UIKit.CreateText(panel, "Title", "Paused", 48, Color.white);
        UIKit.Size(title, 320f, 80f);

        UIKit.Size(CreateButton(panel, "Resume", Resume), 320f, 64f);
        UIKit.Size(CreateButton(panel, "Settings", ShowSettingsPanel), 320f, 64f);
        UIKit.Size(CreateButton(panel, "Quit", QuitGame), 320f, 64f);

        pausePanel = panel.gameObject;
    }

    private void BuildSettingsPanel(Transform root)
    {
        RectTransform panel = UIKit.CreateImage(root, "Settings Panel", new Color(0.1f, 0.11f, 0.14f, 0.95f));
        Center(panel, new Vector2(1100f, 1000f));
        UIKit.VLayout(panel.gameObject, 24f, 30, TextAnchor.UpperCenter);

        Text title = UIKit.CreateText(panel, "Title", "Settings", 48, Color.white);
        UIKit.Size(title, 1040f, 70f);

        // Tab buttons
        RectTransform tabRow = UIKit.CreateRect(panel, "Tabs");
        HorizontalLayoutGroup tabLayout = tabRow.gameObject.AddComponent<HorizontalLayoutGroup>();
        tabLayout.spacing = 12f;
        tabLayout.childAlignment = TextAnchor.MiddleCenter;
        tabLayout.childControlWidth = true;
        tabLayout.childControlHeight = true;
        tabLayout.childForceExpandWidth = false;
        tabLayout.childForceExpandHeight = false;
        UIKit.Size(tabRow, 1040f, 64f);

        tabButtons = new Button[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            tabButtons[i] = CreateButton(tabRow, TabNames[i], () => SelectTab(index), 30);
            UIKit.Size(tabButtons[i], 240f, 60f);
        }

        // Tab contents
        RectTransform content = UIKit.CreateRect(panel, "Content");
        UIKit.Size(content, 1040f, 740f);

        tabPanels = new GameObject[TabNames.Length];
        GameObject[] tabContents = new GameObject[TabNames.Length];

        for (int i = 0; i < TabNames.Length; i++)
        {
            RectTransform tabRoot = UIKit.CreateRect(content, TabNames[i] + "Tab");
            Stretch(tabRoot);
            ScrollRect scrollRect = tabRoot.gameObject.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.scrollSensitivity = 35f;

            RectTransform viewport = UIKit.CreateRect(tabRoot, "Viewport");
            Stretch(viewport);
            viewport.gameObject.AddComponent<Image>().color = new Color(0, 0, 0, 0.01f);
            viewport.gameObject.AddComponent<Mask>().showMaskGraphic = false;

            RectTransform tabContent = UIKit.CreateRect(viewport, "Content");
            tabContent.anchorMin = new Vector2(0f, 1f);
            tabContent.anchorMax = new Vector2(1f, 1f);
            tabContent.pivot = new Vector2(0.5f, 1f);
            tabContent.anchoredPosition = Vector2.zero;
            tabContent.sizeDelta = new Vector2(0f, 0f);

            UIKit.VLayout(tabContent.gameObject, 8f, 10, TextAnchor.UpperCenter);
            ContentSizeFitter csf = tabContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scrollRect.viewport = viewport;
            scrollRect.content = tabContent;

            tabPanels[i] = tabRoot.gameObject;
            tabContents[i] = tabContent.gameObject;
        }

        BuildGameTab(tabContents[0].transform);
        BuildDisplayTab(tabContents[1].transform);
        BuildAudioTab(tabContents[2].transform);
        BuildKeybindsTab(tabContents[3].transform);

        UIKit.Size(CreateButton(panel, "Back", ShowPausePanel), 300f, 64f);

        settingsPanel = panel.gameObject;
        settingsPanel.transform.localScale = new Vector3(1.0f, 1.0f, 1.0f);
    }

    private void BuildGameTab(Transform tab)
    {
        AddSlider(tab, "Mouse Sensitivity", 0.1f, 3f, GameSettings.Sensitivity, false,
            v => v.ToString("0.00"),
            v => GameSettings.Sensitivity = v);

        AddSlider(tab, "Field of View", 30f, 120f, Mathf.Clamp(GameSettings.Fov > 0f ? GameSettings.Fov : 60f, 30f, 120f), true,
            v => Mathf.RoundToInt(v).ToString(),
            v =>
            {
                GameSettings.Fov = v;
                if (cam != null) cam.fieldOfView = v;
            });

        Button invert = AddOption(tab, "Invert Y Axis", OnOff(GameSettings.InvertY));
        Text invertText = invert.GetComponentInChildren<Text>();
        invert.onClick.AddListener(() =>
        {
            GameSettings.InvertY = !GameSettings.InvertY;
            invertText.text = OnOff(GameSettings.InvertY);
        });

        AddSlider(tab, "Camera Distance", 2f, 8f, GameSettings.CameraDistance, false,
            v => v.ToString("0.0") + "m",
            v => GameSettings.CameraDistance = v);

        Button toggleSprintBtn = AddOption(tab, "Toggle Sprint", OnOff(GameSettings.ToggleSprint));
        Text toggleSprintText = toggleSprintBtn.GetComponentInChildren<Text>();
        toggleSprintBtn.onClick.AddListener(() =>
        {
            GameSettings.ToggleSprint = !GameSettings.ToggleSprint;
            toggleSprintText.text = OnOff(GameSettings.ToggleSprint);
        });
    }

    private void BuildDisplayTab(Transform tab)
    {
        // Resolution
        BuildResolutionList();
        Button resButton = AddOption(tab, "Resolution", ResolutionText(resolutionIndex));
        Text resText = resButton.GetComponentInChildren<Text>();
        resButton.onClick.AddListener(() =>
        {
            resolutionIndex = (resolutionIndex + 1) % resolutions.Count;
            resText.text = ResolutionText(resolutionIndex);
            Vector2Int r = resolutions[resolutionIndex];
            Screen.SetResolution(r.x, r.y, Screen.fullScreenMode);
        });

        // Display mode
        modeIndex = System.Array.IndexOf(ScreenModes, Screen.fullScreenMode);
        if (modeIndex < 0) modeIndex = 1;
        Button modeButton = AddOption(tab, "Display Mode", ScreenModeNames[modeIndex]);
        Text modeText = modeButton.GetComponentInChildren<Text>();
        modeButton.onClick.AddListener(() =>
        {
            modeIndex = (modeIndex + 1) % ScreenModes.Length;
            modeText.text = ScreenModeNames[modeIndex];
            Screen.fullScreenMode = ScreenModes[modeIndex];
        });

        // VSync
        Button vsyncButton = AddOption(tab, "VSync", OnOff(GameSettings.VSync));
        Text vsyncText = vsyncButton.GetComponentInChildren<Text>();
        vsyncButton.onClick.AddListener(() =>
        {
            GameSettings.VSync = !GameSettings.VSync;
            vsyncText.text = OnOff(GameSettings.VSync);
            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0; Application.targetFrameRate = GameSettings.Framerate <= 0 ? -1 : GameSettings.Framerate;
        });

        // Graphics quality
        string[] qualityNames = QualitySettings.names;
        qualityIndex = Mathf.Clamp(GameSettings.Quality, 0, qualityNames.Length - 1);
        Button qualityButton = AddOption(tab, "Graphics Quality", qualityNames[qualityIndex]);
        Text qualityText = qualityButton.GetComponentInChildren<Text>();
        qualityButton.onClick.AddListener(() =>
        {
            qualityIndex = (qualityIndex + 1) % qualityNames.Length;
            qualityText.text = qualityNames[qualityIndex];
			GameSettings.Quality = qualityIndex;
			GameSettings.Save();
			QualitySettings.SetQualityLevel(qualityIndex, true);
            QualitySettings.vSyncCount = GameSettings.VSync ? 1 : 0; Application.targetFrameRate = GameSettings.Framerate <= 0 ? -1 : GameSettings.Framerate; // changing quality resets vsync, so re-apply it
        });

        // Framerate
        framerateIndex = System.Array.IndexOf(Framerates, GameSettings.Framerate);
        if (framerateIndex < 0) framerateIndex = 0;
        Button framerateButton = AddOption(tab, "Framerate Limit", FramerateNames[framerateIndex]);
        Text framerateText = framerateButton.GetComponentInChildren<Text>();
        framerateButton.onClick.AddListener(() =>
        {
            framerateIndex = (framerateIndex + 1) % Framerates.Length;
            framerateText.text = FramerateNames[framerateIndex];
            GameSettings.Framerate = Framerates[framerateIndex];
            Application.targetFrameRate = GameSettings.Framerate <= 0 ? -1 : GameSettings.Framerate;
        });

        // Render Scale
                AddSlider(tab, "Chunk Render Distance", 2f, 16f, GameSettings.RenderDistance, true,
            v => v.ToString() + " Chunks",
            v =>
            {
                GameSettings.RenderDistance = (int)v;
				GameSettings.Save();
                if (WorldGenerator.Instance != null) {
                    WorldGenerator.Instance.viewDistance = (int)v;
                }
            });

        AddSlider(tab, "Render Scale", 0.5f, 2.0f, GameSettings.RenderScale, false,
            v => Mathf.RoundToInt(v * 100f).ToString() + "%",
            v =>
            {
                GameSettings.RenderScale = v;
                var pipeline = QualitySettings.renderPipeline != null ? QualitySettings.renderPipeline : UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline;
                var urpasset = pipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                if (urpasset != null) urpasset.renderScale = v;
            });


        // --- ADVANCED SETTINGS ---
        // Master Graphics Preset
        AddDropdown(tab, "Global Graphics Preset", new string[] { "Low", "Medium", "High", "Max" }, GameSettings.VisibilityQ, (presetVal) => {
            GameSettings.VisibilityQ = presetVal;
            GameSettings.AntiAliasingQ = presetVal;
            GameSettings.PostProcessingQ = presetVal;
            GameSettings.ShadowsQ = presetVal;
            GameSettings.GlobalIlluminationQ = presetVal;
            GameSettings.ReflectionsQ = presetVal;
            GameSettings.TexturesQ = presetVal;
            GameSettings.EffectQ = presetVal;
            GameSettings.VegetationQ = presetVal;
            GameSettings.ShaderQualityQ = presetVal;
            GameSettings.ObjectQ = presetVal;
            GameSettings.PhysicsQ = presetVal;
            
            GameSettings.Save();
            ApplySavedSettings(); // Re-apply everything
            needsRebuild = true; // Rebuild UI to reflect new values
        });

        UIKit.CreateRect(tab, "SpacerGlobal").sizeDelta = new Vector2(0, 30); // Visual gap

        
        string[] qNames = { "Low", "Medium", "High", "Max" };

        Dropdown AddQOption(string label, System.Func<int> getVal, System.Action<int> setVal, System.Action<int> apply)
        {
            int val = Mathf.Clamp(getVal(), 0, 3);
            Dropdown btn = AddDropdown(tab, label, qNames, val, newVal => { setVal(newVal); GameSettings.Save(); if (apply != null) apply(newVal); });
            
            /*
            {
                val = (val + 1) % 4;
                txt.text = qNames[val];
                setVal(val);
                */
            return btn;
        }

        UIKit.CreateRect(tab, "Spacer").sizeDelta = new Vector2(0, 30); // Visual gap

                AddQOption("Visibility", () => GameSettings.VisibilityQ, v => GameSettings.VisibilityQ = v, v => ApplySavedSettings());
        AddQOption("Anti-aliasing", () => GameSettings.AntiAliasingQ, v => GameSettings.AntiAliasingQ = v, v => ApplySavedSettings());
        AddQOption("Post Processing", () => GameSettings.PostProcessingQ, v => GameSettings.PostProcessingQ = v, v => ApplySavedSettings());
        AddQOption("Shadows", () => GameSettings.ShadowsQ, v => GameSettings.ShadowsQ = v, v => ApplySavedSettings());
        AddQOption("Global Illumination", () => GameSettings.GlobalIlluminationQ, v => GameSettings.GlobalIlluminationQ = v, v => ApplySavedSettings());
        AddQOption("Reflections", () => GameSettings.ReflectionsQ, v => GameSettings.ReflectionsQ = v, v => ApplySavedSettings());
        AddQOption("Textures", () => GameSettings.TexturesQ, v => GameSettings.TexturesQ = v, v => ApplySavedSettings());
        AddQOption("Effect", () => GameSettings.EffectQ, v => GameSettings.EffectQ = v, v => ApplySavedSettings());
        AddQOption("Vegetation", () => GameSettings.VegetationQ, v => GameSettings.VegetationQ = v, v => ApplySavedSettings());
        AddQOption("Shader Quality", () => GameSettings.ShaderQualityQ, v => GameSettings.ShaderQualityQ = v, v => ApplySavedSettings());
        AddQOption("Object", () => GameSettings.ObjectQ, v => GameSettings.ObjectQ = v, v => ApplySavedSettings());
        AddQOption("Physics", () => GameSettings.PhysicsQ, v => GameSettings.PhysicsQ = v, v => ApplySavedSettings());

        Dropdown mbBtn = AddDropdown(tab, "Motion Blur", new string[] { "Off", "On" }, GameSettings.MotionBlur ? 1 : 0, val => {
            GameSettings.MotionBlur = val == 1;
        });

                AddDropdown(tab, "Show FPS", new string[] { "Off", "On" }, GameSettings.ShowFps ? 1 : 0, val => {
            GameSettings.ShowFps = val == 1;
            GameSettings.Save();
        });
        AddDropdown(tab, "Frame Time (ms)", new string[] { "Off", "On" }, GameSettings.ShowFrameTime ? 1 : 0, val => { GameSettings.ShowFrameTime = val == 1; GameSettings.Save(); });
        AddDropdown(tab, "1% / 0.1% Lows", new string[] { "Off", "On" }, GameSettings.ShowLows ? 1 : 0, val => { GameSettings.ShowLows = val == 1; GameSettings.Save(); });
        AddDropdown(tab, "Frame Pacing", new string[] { "Off", "On" }, GameSettings.ShowPacing ? 1 : 0, val => { GameSettings.ShowPacing = val == 1; GameSettings.Save(); });
        AddDropdown(tab, "Hitch / Spike Counter", new string[] { "Off", "On" }, GameSettings.ShowHitches ? 1 : 0, val => { GameSettings.ShowHitches = val == 1; GameSettings.Save(); });
        AddDropdown(tab, "Memory / GC", new string[] { "Off", "On" }, GameSettings.ShowMemory ? 1 : 0, val => { GameSettings.ShowMemory = val == 1; GameSettings.Save(); });
        AddDropdown(tab, "Input Latency", new string[] { "Off", "On" }, GameSettings.ShowLatency ? 1 : 0, val => { GameSettings.ShowLatency = val == 1; GameSettings.Save(); });

        AddDropdown(tab, "Minimap", new string[] { "Off", "On" }, GameSettings.ShowMinimap ? 1 : 0, val => {
            GameSettings.ShowMinimap = val == 1;
            GameSettings.Save();
            
            Minimap minimap = FindAnyObjectByType<Minimap>();
            if (minimap != null) minimap.Toggle(val == 1);
        });
    }

    private void BuildAudioTab(Transform tab)
    {
        AddSlider(tab, "Master Volume", 0f, 1f, GameSettings.MasterVolume, false, Percent,
            v =>
            {
                GameSettings.MasterVolume = v;
                AudioListener.volume = v;
            });
        AddSlider(tab, "Music Volume", 0f, 1f, GameSettings.MusicVolume, false, Percent,
            v => GameSettings.MusicVolume = v);
        AddSlider(tab, "Effects Volume", 0f, 1f, GameSettings.SfxVolume, false, Percent,
            v => GameSettings.SfxVolume = v);
    }

    private void BuildKeybindsTab(Transform tab)
    {
        Text hint = UIKit.CreateText(tab, "Hint", "Click a key, then press the new one. Esc cancels. (Press Esc while hovering to unbind? No, binding Esc cancels).", 24, UIKit.Subtle);
        UIKit.Size(hint, 1000f, 32f);

        keyLabels.Clear();

        // --- Movement ---
        AddKeybindHeader(tab, "—— MOVEMENT ——");
        GameAction[] movementActions = { GameAction.Forward, GameAction.Backward, GameAction.Left, GameAction.Right, GameAction.Jump, GameAction.Sprint };
        foreach (var action in movementActions) AddKeybindRow(tab, action);

        // --- General ---
        AddKeybindHeader(tab, "—— GENERAL ——");
        GameAction[] generalActions = { GameAction.Interact, GameAction.Inventory, GameAction.Journal, GameAction.Map, GameAction.ToggleView, GameAction.Build };
        foreach (var action in generalActions) AddKeybindRow(tab, action);

        // --- Farming ---
        AddKeybindHeader(tab, "—— FARMING ——");
        GameAction[] farmingActions = { GameAction.Till };
        foreach (var action in farmingActions) AddKeybindRow(tab, action);

        // --- Hotbar ---
        AddKeybindHeader(tab, "—— HOTBAR ——");
        GameAction[] hotbarActions = { GameAction.Hotbar1, GameAction.Hotbar2, GameAction.Hotbar3, GameAction.Hotbar4, GameAction.Hotbar5, GameAction.Hotbar6, GameAction.Hotbar7, GameAction.Hotbar8, GameAction.Hotbar9 };
        foreach (var action in hotbarActions) AddKeybindRow(tab, action);

        Button reset = CreateButton(tab, "Reset to Defaults", () =>
        {
            CancelRebind();
            Keybinds.ResetToDefaults();
            RefreshKeybindLabels();
        }, 28);
        UIKit.Size(reset, 320f, 52f);
    }

    private void AddKeybindHeader(Transform tab, string label)
    {
        Text header = UIKit.CreateText(tab, "Header", label, 22, UIKit.Subtle);
        UIKit.Size(header, 1000f, 36f);
    }

    private void AddKeybindRow(Transform tab, GameAction action)
    {
        GameAction captured = action;
        RectTransform row = CreateRow(tab, Keybinds.ActionLabel(action));
        Text[] labels = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            int slot = i;
            Button button = CreateButton(row, Keybinds.KeyName(action, slot), () => BeginRebind(captured, slot), 24);
            UIKit.Size(button, 140f, 42f);
            labels[i] = button.GetComponentInChildren<Text>();
        }
        keyLabels[action] = labels;
    }

    // ------------------------------------------------------------- Display helpers

    private void BuildResolutionList()
    {
        resolutions.Clear();
        foreach (Resolution r in Screen.resolutions)
        {
            Vector2Int size = new Vector2Int(r.width, r.height);
            if (!resolutions.Contains(size)) resolutions.Add(size);
        }

        resolutionIndex = resolutions.FindIndex(r => r.x == Screen.width && r.y == Screen.height);
        if (resolutionIndex < 0)
        {
            resolutions.Add(new Vector2Int(Screen.width, Screen.height));
            resolutionIndex = resolutions.Count - 1;
        }
    }

    private string ResolutionText(int index)
    {
        return resolutions[index].x + " x " + resolutions[index].y;
    }

    private static string OnOff(bool value) => value ? "On" : "Off";
    private static string Percent(float v) => Mathf.RoundToInt(v * 100f) + "%";

    // ---------------------------------------------------------------- UI builders

    private Dropdown AddDropdown(Transform parent, string label, string[] options, int currentValue, UnityAction<int> onChanged)
    {
        RectTransform row = CreateRow(parent, label);
        
        GameObject dropObj = DefaultControls.CreateDropdown(new DefaultControls.Resources());
        dropObj.transform.SetParent(row, false);
        Dropdown dropdown = dropObj.GetComponent<Dropdown>();
        UIKit.Size(dropObj.GetComponent<RectTransform>(), 400f, 40f);
        
        // Basic styling
        dropObj.GetComponent<Image>().color = new Color(0.13f, 0.14f, 0.18f, 1f);
        Text txt = dropdown.captionText;
        txt.color = Color.white;
        txt.fontSize = 24;
        txt.alignment = TextAnchor.MiddleLeft;
        
        Image templateImg = dropdown.template.GetComponent<Image>();
        if (templateImg != null) templateImg.color = new Color(0.1f, 0.11f, 0.14f, 1f);
        
        Text itemTxt = dropdown.itemText;
        itemTxt.color = Color.white;
        itemTxt.fontSize = 24;
        itemTxt.horizontalOverflow = HorizontalWrapMode.Overflow;
        itemTxt.verticalOverflow = VerticalWrapMode.Overflow;
        
        // Increase the height of the item so the 24pt text fits without clipping
        RectTransform itemRect = itemTxt.rectTransform.parent.GetComponent<RectTransform>();
        if (itemRect != null) {
            itemRect.sizeDelta = new Vector2(0, 35);
        }
        
        Transform itemBg = dropdown.template.Find("Viewport/Content/Item/Item Background");
        if (itemBg != null) {
            Image bgImg = itemBg.GetComponent<Image>();
            if (bgImg != null) bgImg.color = new Color(0.1f, 0.11f, 0.14f, 1f);
        }
        
        Transform itemCheck = dropdown.template.Find("Viewport/Content/Item/Item Checkmark");
        if (itemCheck != null) {
            Image chkImg = itemCheck.GetComponent<Image>();
            if (chkImg != null) chkImg.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        }
        
        dropdown.ClearOptions();
        dropdown.AddOptions(new System.Collections.Generic.List<string>(options));
        dropdown.value = currentValue;
        dropdown.onValueChanged.AddListener(onChanged);
        
        return dropdown;
    }


    // Thin wrapper over UIKit.CreateButton so the call sites below stay short
    private static Button CreateButton(Transform parent, string label, UnityAction onClick, int fontSize = 32)
    {
        return UIKit.CreateButton(parent, label + " Button", label, fontSize, onClick, out _);
    }

    private Slider CreateSlider(Transform parent, float min, float max, float value, bool whole)
    {
        RectTransform root = UIKit.CreateRect(parent, "Slider");
        Slider slider = root.gameObject.AddComponent<Slider>();

        RectTransform bg = UIKit.CreateImage(root, "Background", new Color(0.13f, 0.14f, 0.18f, 1f));
        bg.GetComponent<Image>().raycastTarget = true;
        bg.anchorMin = new Vector2(0f, 0.25f);
        bg.anchorMax = new Vector2(1f, 0.75f);
        bg.sizeDelta = Vector2.zero;
        bg.anchoredPosition = Vector2.zero;

        RectTransform fillArea = UIKit.CreateRect(root, "Fill Area");
        fillArea.anchorMin = new Vector2(0f, 0.25f);
        fillArea.anchorMax = new Vector2(1f, 0.75f);
        fillArea.sizeDelta = new Vector2(-20f, 0f);
        fillArea.anchoredPosition = new Vector2(-5f, 0f);

        RectTransform fill = UIKit.CreateImage(fillArea, "Fill", UIKit.Accent);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.sizeDelta = new Vector2(10f, 0f);
        fill.anchoredPosition = Vector2.zero;

        RectTransform handleArea = UIKit.CreateRect(root, "Handle Slide Area");
        handleArea.anchorMin = Vector2.zero;
        handleArea.anchorMax = Vector2.one;
        handleArea.sizeDelta = new Vector2(-20f, 0f);
        handleArea.anchoredPosition = Vector2.zero;

        RectTransform handle = UIKit.CreateImage(handleArea, "Handle", Color.white);
        Image handleImage = handle.GetComponent<Image>();
        handleImage.raycastTarget = true;
        handle.anchorMin = new Vector2(0f, 0f);
        handle.anchorMax = new Vector2(0f, 1f);
        handle.sizeDelta = new Vector2(20f, 0f);
        handle.anchoredPosition = Vector2.zero;

        slider.fillRect = fill;
        slider.handleRect = handle;
        slider.targetGraphic = handleImage;
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = whole;
        slider.SetValueWithoutNotify(value);
        return slider;
    }

    private RectTransform CreateRow(Transform parent, string label)
    {
        RectTransform row = UIKit.CreateImage(parent, label + " Row", new Color(1f, 1f, 1f, 0.05f));
        HorizontalLayoutGroup h = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = 20f;
        h.padding = new RectOffset(20, 20, 0, 0);
        h.childAlignment = TextAnchor.MiddleLeft;
        h.childControlWidth = true;
        h.childControlHeight = true;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;
        UIKit.Size(row, 1000f, 48f);

        Text labelText = UIKit.CreateText(row, "Label", label, 28, Color.white, TextAnchor.MiddleLeft);
        UIKit.Size(labelText, 380f, 44f);
        return row;
    }

    private void AddSlider(Transform parent, string label, float min, float max, float value, bool whole,
                           System.Func<float, string> format, UnityAction<float> onChanged)
    {
        RectTransform row = CreateRow(parent, label);

        Slider slider = CreateSlider(row, min, max, value, whole);
        UIKit.Size(slider, 400f, 30f);

        Text valueText = UIKit.CreateText(row, "Value", format(value), 26, Color.white, TextAnchor.MiddleRight);
        UIKit.Size(valueText, 100f, 44f);

        slider.onValueChanged.AddListener(v =>
        {
            valueText.text = format(v);
            onChanged(v);
        });
    }

    private Button AddOption(Transform parent, string label, string initialText)
    {
        RectTransform row = CreateRow(parent, label);
        Button button = CreateButton(row, initialText, () => { }, 28);
        UIKit.Size(button, 520f, 42f);
        return button;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }
}

























