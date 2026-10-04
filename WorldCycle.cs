using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =====================================================================================
//  Time, weather, the journal and the map all live in this one file:
//    GameClock    the time of day, day count and current weather, readable from any script (static)
//    WorldCycle   day/night lighting, sun and moon, fog, sky, weather, rain and lightning, and the clock on screen
//                 (put this on your Manager, the rest is added automatically)
//    Journal      quests, people you've met and a log of what happened (default key: J)
//    WorldMap     a live top-down map with quest markers, zoom and pan (default key: M)
//    WorldPanels  makes sure only one panel is open at a time and frees / locks the cursor for them
//
//  Needs two extra keybinds in PauseMenu.cs: GameAction.Journal and GameAction.Map (see the patched PauseMenu.cs).
//  Keep this file named WorldCycle.cs, so the component on your Manager keeps working.
// =====================================================================================

public enum WeatherType { Clear, Cloudy, Rain, Storm, Fog }
public enum Season { Spring, Summer, Autumn, Winter }

/// <summary>
/// The world's time and weather. WorldCycle moves it along; everything else just reads it,
/// e.g. GameClock.Hour, GameClock.IsNight, GameClock.Weather, or listens with GameClock.HourChanged += ...
/// (handy later for NPC schedules, shop opening hours and sounds).
/// </summary>
public static class GameClock
{
    /// <summary>0 to 24. 6.5 is half past six in the morning.</summary>
    public static float Hour { get; private set; } = 8f;
    public static int Day { get; private set; } = 1;
    public static float Sunrise { get; private set; } = 6f;
    public static float Sunset { get; private set; } = 18f;
    public static WeatherType Weather { get; private set; } = WeatherType.Clear;

    /// <summary>How hard it's raining right now, 0 to 1 (fades in and out with the weather).</summary>
    public static float RainIntensity { get; internal set; }

    /// <summary>How windy it is right now, 0 to 1.</summary>
    public static float Wind { get; internal set; }

    public static Season CurrentSeason { get; private set; } = Season.Spring;

    public static bool IsNight => Hour < Sunrise || Hour >= Sunset;
    public static bool IsRaining => RainIntensity > 0.2f;

    /// <summary>Fires each time the clock passes a whole hour. The value is the new hour (0 to 23).</summary>
    public static event System.Action<int> HourChanged;
    /// <summary>Fires at midnight with the new day number.</summary>
    public static event System.Action<int> DayChanged;
    public static event System.Action<WeatherType> WeatherChanged;
    public static event System.Action<Season> SeasonChanged;
    /// <summary>Fires the moment a lightning flash starts. Play your thunder sound a moment later.</summary>
    public static event System.Action LightningStruck;

    // Clears listeners left over from the last play session (matters if Domain Reload is turned off)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        HourChanged = null;
        DayChanged = null;
        WeatherChanged = null;
        SeasonChanged = null;
        LightningStruck = null;
        Hour = 8f;
        Day = 1;
        Weather = WeatherType.Clear;
        RainIntensity = 0f;
        Wind = 0f;
        CurrentSeason = Season.Spring;
    }

    internal static void Begin(float hour, float sunrise, float sunset)
    {
        Hour = Mathf.Repeat(hour, 24f);
        Day = 1;
        Sunrise = sunrise;
        Sunset = sunset;
    }

    internal static void Advance(float hours)
    {
        int before = Mathf.FloorToInt(Hour);
        Hour += hours;

        while (Hour >= 24f)
        {
            Hour -= 24f;
            Day++;
            DayChanged?.Invoke(Day);
        }

        int after = Mathf.FloorToInt(Hour);
        if (after != before) HourChanged?.Invoke(after);
    }

    internal static void SetWeather(WeatherType type)
    {
        if (Weather == type) return;
        Weather = type;
        WeatherChanged?.Invoke(type);
    }

    internal static void SetSeason(Season s)
    {
        if (CurrentSeason == s) return;
        CurrentSeason = s;
        SeasonChanged?.Invoke(s);
    }

    internal static void RaiseLightning()
    {
        LightningStruck?.Invoke();
    }

    /// <summary>"8:30 AM"</summary>
    public static string TimeString
    {
        get
        {
            int h = Mathf.FloorToInt(Hour);
            int m = Mathf.Min(59, Mathf.FloorToInt((Hour - h) * 60f));
            int h12 = h % 12;
            if (h12 == 0) h12 = 12;
            return $"{h12}:{m:00} {(h < 12 ? "AM" : "PM")}";
        }
    }

    /// <summary>"Day 3, 8:30 AM"</summary>
    public static string Stamp => $"Day {Day}, {TimeString}";

    /// <summary>Night, Dawn, Morning, Afternoon or Dusk.</summary>
    public static string PartOfDay
    {
        get
        {
            float h = Hour;
            if (h < Sunrise - 1f || h >= Sunset + 1.5f) return "Night";
            if (h < Sunrise + 1.5f) return "Dawn";
            if (h < 12f) return "Morning";
            if (h < Sunset - 1f) return "Afternoon";
            return "Dusk";
        }
    }

    public static string WeatherLabel(WeatherType type)
    {
        return type == WeatherType.Storm ? "Thunderstorm" : type.ToString();
    }
}

/// <summary>Three colours (night, twilight, day) that fade into each other as the sun rises and sets.</summary>
[System.Serializable]
public class SkyColors
{
    public Color night = Color.black;
    public Color twilight = Color.gray;
    public Color day = Color.white;

    public SkyColors() { }

    public SkyColors(Color night, Color twilight, Color day)
    {
        this.night = night;
        this.twilight = twilight;
        this.day = day;
    }

    /// <summary>sunHeight: 1 = sun overhead, 0 = on the horizon, -1 = directly below.</summary>
    public Color Evaluate(float sunHeight)
    {
        if (sunHeight < 0f) return Color.Lerp(night, twilight, Mathf.InverseLerp(-0.25f, 0f, sunHeight));
        return Color.Lerp(twilight, day, Mathf.InverseLerp(0f, 0.5f, sunHeight));
    }
}

/// <summary>
/// Put this on your Manager object. Runs the clock, turns your Directional Light into a sun that crosses the sky
/// (with a moon at night), fades ambient light, fog and the skybox with the time of day, and rolls random weather:
/// clear, cloudy, rain, thunderstorm and fog. Shows the time, weather and compass at the top of the screen.
/// It also adds the Journal and the Map for you.
/// Tip: set your Directional Light's Mode to Realtime (or Mixed), otherwise its intensity can't change while playing.
/// </summary>
public class WorldCycle : MonoBehaviour
{
    public static WorldCycle Instance { get; private set; }

    [Header("Time")]
    [Tooltip("How many real minutes a full 24 hour day lasts.")]
    [SerializeField, Min(0.1f)] private float dayLengthMinutes = 12f;
    [SerializeField, Range(0f, 24f)] private float startHour = 8f;
    [SerializeField, Range(0f, 12f)] private float sunrise = 6f;
    [SerializeField, Range(12f, 24f)] private float sunset = 18f;
    [Tooltip("Stop the clock while talking to someone.")]
    [SerializeField] private bool freezeWhileTalking = true;
    [Tooltip("Show the time, weather and compass at the top of the screen.")]
    [SerializeField] private bool showClock = true;

    [Header("Sun & Moon")]
    [Tooltip("Leave empty to use the scene's Directional Light. One is created if there isn't one.")]
    [SerializeField] private Light sun;
    [Tooltip("Compass direction the sun's path leans towards.")]
    [SerializeField] private float sunYaw = 30f;
    [SerializeField] private float sunMaxIntensity = 1.2f;
    [SerializeField] private float moonMaxIntensity = 0.15f;
    [SerializeField] private Color moonColor = new Color(0.55f, 0.65f, 1f);
    [SerializeField] private SkyColors sunColor = new SkyColors(
        new Color(0.4f, 0.5f, 0.8f), new Color(1f, 0.55f, 0.3f), new Color(1f, 0.96f, 0.88f));

    [Header("Ambient, Fog & Sky")]
    [SerializeField] private SkyColors ambientColor = new SkyColors(
        new Color(0.05f, 0.07f, 0.14f), new Color(0.45f, 0.35f, 0.4f), new Color(0.55f, 0.62f, 0.72f));
    [SerializeField] private float ambientIntensity = 1f;
    [Tooltip("Lets this script control fog. Turn off to keep your own fog settings.")]
    [SerializeField] private bool controlFog = true;
    [SerializeField] private float baseFogDensity = 0.002f;
    [SerializeField] private SkyColors fogColor = new SkyColors(
        new Color(0.03f, 0.04f, 0.09f), new Color(0.75f, 0.5f, 0.4f), new Color(0.7f, 0.8f, 0.9f));
    [Tooltip("Tints a Procedural skybox (the URP default sky). Ignored for other skies.")]
    [SerializeField] private SkyColors skyTint = new SkyColors(
        new Color(0.15f, 0.18f, 0.3f), new Color(0.8f, 0.5f, 0.45f), new Color(0.5f, 0.5f, 0.5f));
    [SerializeField] private float skyExposureDay = 1.3f;
    [SerializeField] private float skyExposureNight = 0.08f;

    [Header("Weather")]
    [SerializeField] private bool randomWeather = true;
    [SerializeField] private WeatherType startWeather = WeatherType.Clear;
    [Tooltip("The weather changes after somewhere between these many in-game hours.")]
    [SerializeField] private Vector2 weatherChangeHours = new Vector2(3f, 8f);
    [Tooltip("Real seconds a change of weather takes to fade in.")]
    [SerializeField, Min(1f)] private float weatherTransitionSeconds = 25f;
    [SerializeField] private bool rainParticles = true;
    [SerializeField] private float rainMaxRate = 2500f;
    [Tooltip("Seconds between lightning flashes in a thunderstorm.")]
    [SerializeField] private Vector2 lightningGap = new Vector2(6f, 20f);

    [Header("Seasons")]
    [Tooltip("How many in-game days each season lasts.")]
    [SerializeField, Min(1)] private int daysPerSeason = 7;
    [Tooltip("Shift sunrise/sunset times per season. Spring/Autumn are neutral.")]
    [SerializeField] private bool seasonalDayLength = true;

    // What each kind of weather does to the world (1 = fully on)
    private struct WeatherParams
    {
        public float sun;       // how much sunlight gets through
        public float fog;       // extra fog density
        public float gray;      // how washed out and grey the sky and light are
        public float rain;
        public float wind;
        public float lightning;

        public static WeatherParams Lerp(WeatherParams a, WeatherParams b, float t)
        {
            return new WeatherParams
            {
                sun = Mathf.Lerp(a.sun, b.sun, t),
                fog = Mathf.Lerp(a.fog, b.fog, t),
                gray = Mathf.Lerp(a.gray, b.gray, t),
                rain = Mathf.Lerp(a.rain, b.rain, t),
                wind = Mathf.Lerp(a.wind, b.wind, t),
                lightning = Mathf.Lerp(a.lightning, b.lightning, t)
            };
        }
    }

    private static WeatherParams ParamsFor(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Cloudy: return new WeatherParams { sun = 0.55f, fog = 0.001f, gray = 0.4f, rain = 0f, wind = 0.3f, lightning = 0f };
            case WeatherType.Rain:   return new WeatherParams { sun = 0.3f,  fog = 0.004f, gray = 0.65f, rain = 0.6f, wind = 0.5f, lightning = 0f };
            case WeatherType.Storm:  return new WeatherParams { sun = 0.15f, fog = 0.006f, gray = 0.85f, rain = 1f, wind = 1f, lightning = 1f };
            case WeatherType.Fog:    return new WeatherParams { sun = 0.45f, fog = 0.03f, gray = 0.5f, rain = 0f, wind = 0.05f, lightning = 0f };
            default:                 return new WeatherParams { sun = 1f, fog = 0f, gray = 0f, rain = 0f, wind = 0.1f, lightning = 0f };
        }
    }

    private WeatherParams from, to, current;
    private float blend = 1f;
    private float weatherTimer;   // in-game hours until the next weather change
    private Season currentSeason;

    private Transform player;
    private Camera viewCamera;
    private Light moon;
    private LightShadows sunShadows;
    private Material skyMat;
    private string skyTintProperty;
    private bool skyHasExposure;
    private bool solidColorSky;

    private float flash;
    private float secondFlashDelay;
    private float lightningTimer = 5f;

    private ParticleSystem rain;
    private Transform rainTransform;
    private const float RainHeight = 12f;
    private const float RainArea = 36f;

    private Text hudTime;
    private Text hudInfo;
    private float nextHudUpdate;

    // Saved while the map camera draws (see PushMapLighting)
    private bool mapPushed;
    private bool savedFog;
    private Color savedAmbient;
    private float savedSunIntensity;
    private Color savedSunColor;
    private UnityEngine.Rendering.Volume underwaterVolume;
    private Quaternion savedSunRotation;
    private bool savedMoonEnabled;

    // ---------------------------------------------------------------- Lifecycle

    private void Awake()
    {
        Instance = this;
        
        // Force post-processing on the main camera so our underwater volume actually works!
        if (Camera.main != null)
        {
            var camData = Camera.main.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null) camData = Camera.main.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
        }        
        GameObject volumeGo = new GameObject("UnderwaterVolume");
        underwaterVolume = volumeGo.AddComponent<UnityEngine.Rendering.Volume>();
        underwaterVolume.isGlobal = true;
        underwaterVolume.weight = 0f;
#if UNITY_EDITOR
        string profilePath = "Assets/Fantastic Seaside Town/Settings/Global Volume Profile Water Town.asset";
        underwaterVolume.profile = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath);
#endif


        WorldPanels.Reset();
        GameClock.Begin(startHour, sunrise, Mathf.Max(sunset, sunrise + 1f));
        currentSeason = SeasonFromDay(GameClock.Day);
        GameClock.SetSeason(currentSeason);
        SetWeather(startWeather, true);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        GameClock.RainIntensity = 0f;
        GameClock.Wind = 0f;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;

        CameraController cc = FindAnyObjectByType<CameraController>();
        viewCamera = cc != null ? cc.GetComponent<Camera>() : Camera.main;

        SetupLights();
        SetupSky();
        if (rainParticles && viewCamera != null) BuildRain();
        if (showClock) BuildHud();

        // The journal and the map add themselves, unless you've put them on the Manager by hand
        if (Journal.Instance == null) gameObject.AddComponent<Journal>();
        if (WorldMap.Instance == null) gameObject.AddComponent<WorldMap>();

        ApplyLighting();
    }

    private void Update()
    {
        float dt = Time.deltaTime; // zero while paused, so the world freezes with the game

        bool frozen = freezeWhileTalking && NPCSpawner.IsTalking;
        if (!frozen && dt > 0f)
        {
            float hours = dt * 24f / (dayLengthMinutes * 60f);
            GameClock.Advance(hours);
            weatherTimer -= hours;

            Season newSeason = SeasonFromDay(GameClock.Day);
            if (newSeason != currentSeason)
            {
                currentSeason = newSeason;
                GameClock.SetSeason(currentSeason);
                ApplySeasonalDayLength();
                if (Inventory.Instance != null)
                    Inventory.Instance.Notify("Season changed: " + currentSeason.ToString());
            }
        }

        UpdateWeather(dt);
        UpdateLightning(dt);
        ApplyLighting();
        UpdateHud();

        WorldPanels.Tick();
    }

    private void LateUpdate()
    {
        // The rain follows the camera, after the camera has moved this frame
        if (rain == null || viewCamera == null) return;

        rainTransform.position = viewCamera.transform.position + Vector3.up * RainHeight;

        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = current.rain * rainMaxRate;

        ParticleSystem.VelocityOverLifetimeModule vel = rain.velocityOverLifetime;
        vel.x = current.wind * 7f;
    }

    // ------------------------------------------------------------------ API

    /// <summary>Changes the weather. It fades in over Weather Transition Seconds, or right away with instant = true.</summary>
    public void SetWeather(WeatherType type, bool instant = false)
    {
        from = current;
        to = ParamsFor(type);
        blend = instant ? 1f : 0f;
        if (instant) current = to;

        weatherTimer = Random.Range(weatherChangeHours.x, Mathf.Max(weatherChangeHours.x, weatherChangeHours.y));
        GameClock.SetWeather(type);
    }

    /// <summary>Jumps the clock forward, firing hour and day events along the way. Handy for sleeping later.</summary>
    public void SkipHours(float hours)
    {
        while (hours > 0f)
        {
            float step = Mathf.Min(1f, hours);
            GameClock.Advance(step);
            weatherTimer -= step;
            hours -= step;
        }
    }

    /// <summary>Skips forward to a time of day, e.g. SkipToHour(6f) for morning.</summary>
    public void SkipToHour(float hour)
    {
        float delta = Mathf.Repeat(hour - GameClock.Hour, 24f);
        SkipHours(delta);
    }

    [ContextMenu("Next Weather")]
    private void DebugNextWeather()
    {
        if (Application.isPlaying) SetWeather(PickNextWeather());
    }

    [ContextMenu("Skip 1 Hour")]
    private void DebugSkipHour()
    {
        if (Application.isPlaying) SkipHours(1f);
    }

    // -------------------------------------------------------------- Weather

    private void UpdateWeather(float dt)
    {
        if (blend < 1f)
        {
            blend = Mathf.MoveTowards(blend, 1f, dt / weatherTransitionSeconds);
            current = WeatherParams.Lerp(from, to, Mathf.SmoothStep(0f, 1f, blend));
        }

        if (randomWeather && weatherTimer <= 0f)
            SetWeather(PickNextWeather());

        GameClock.RainIntensity = current.rain;
        GameClock.Wind = current.wind;
    }

    private WeatherType PickNextWeather()
    {
        WeatherType now = GameClock.Weather;
        float[] weights = new float[5];
        switch (GameClock.CurrentSeason)
        {
            case Season.Spring:
                weights[(int)WeatherType.Clear]  = 30f;
                weights[(int)WeatherType.Cloudy] = 30f;
                weights[(int)WeatherType.Rain]   = 22f;
                weights[(int)WeatherType.Fog]    = 12f;
                weights[(int)WeatherType.Storm]  = 6f;
                break;
            case Season.Summer:
                weights[(int)WeatherType.Clear]  = 60f;
                weights[(int)WeatherType.Cloudy] = 20f;
                weights[(int)WeatherType.Rain]   = 8f;
                weights[(int)WeatherType.Fog]    = 2f;
                weights[(int)WeatherType.Storm]  = 10f; // summer storms are dramatic
                break;
            case Season.Autumn:
                weights[(int)WeatherType.Clear]  = 25f;
                weights[(int)WeatherType.Cloudy] = 35f;
                weights[(int)WeatherType.Rain]   = 25f;
                weights[(int)WeatherType.Fog]    = 12f;
                weights[(int)WeatherType.Storm]  = 3f;
                break;
            case Season.Winter:
                weights[(int)WeatherType.Clear]  = 20f;
                weights[(int)WeatherType.Cloudy] = 30f;
                weights[(int)WeatherType.Rain]   = 15f;
                weights[(int)WeatherType.Fog]    = 25f; // heavy winter fog
                weights[(int)WeatherType.Storm]  = 10f;
                break;
            default:
                weights[(int)WeatherType.Clear]  = 40f;
                weights[(int)WeatherType.Cloudy] = 25f;
                weights[(int)WeatherType.Rain]   = 15f;
                weights[(int)WeatherType.Fog]    = 8f;
                weights[(int)WeatherType.Storm]  = 4f;
                break;
        }

        // Fog likes early mornings
        if (GameClock.Hour >= 3f && GameClock.Hour <= 9f) weights[(int)WeatherType.Fog] *= 3f;

        // Storms build up from cloud or rain, and rain rarely comes out of a clear sky
        if (now != WeatherType.Rain && now != WeatherType.Cloudy) weights[(int)WeatherType.Storm] = 0f;
        if (now == WeatherType.Clear)
        {
            weights[(int)WeatherType.Rain] *= 0.4f;
            weights[(int)WeatherType.Cloudy] *= 1.5f;
        }

        weights[(int)now] = 0f;

        float total = 0f;
        foreach (float w in weights) total += w;

        float roll = Random.value * total;
        for (int i = 0; i < weights.Length; i++)
        {
            roll -= weights[i];
            if (roll <= 0f && weights[i] > 0f) return (WeatherType)i;
        }
        return WeatherType.Clear;
    }

    private void UpdateLightning(float dt)
    {
        flash = Mathf.MoveTowards(flash, 0f, dt * 7f);

        if (secondFlashDelay > 0f)
        {
            secondFlashDelay -= dt;
            if (secondFlashDelay <= 0f) flash = 0.7f;
        }

        if (current.lightning < 0.3f)
        {
            lightningTimer = Random.Range(2f, 6f);
            return;
        }

        lightningTimer -= dt;
        if (lightningTimer <= 0f)
        {
            lightningTimer = Random.Range(lightningGap.x, Mathf.Max(lightningGap.x, lightningGap.y));
            flash = 1f;
            secondFlashDelay = Random.value < 0.5f ? 0.18f : 0f;
            GameClock.RaiseLightning();
        }
    }

    // ------------------------------------------------------------- Lighting

    // 0 = sunrise, 90 = noon, 180 = sunset, 270 = midnight. Day and night can be different lengths.
    private float SunAngle(float hour)
    {
        float rise = GameClock.Sunrise;
        float set = GameClock.Sunset;
        float dayLength = set - rise;

        if (hour >= rise && hour < set)
            return (hour - rise) / dayLength * 180f;

        float sinceSunset = hour >= set ? hour - set : hour + 24f - set;
        return 180f + sinceSunset / (24f - dayLength) * 180f;
    }

    // Greys a colour out, for overcast skies
    private static Color Overcast(Color c, float amount)
    {
        float l = c.grayscale;
        return Color.Lerp(c, new Color(l * 0.9f, l * 0.92f, l * 0.96f, 1f), Mathf.Clamp01(amount));
    }

    private void SetupLights()
    {
        if (sun == null) sun = RenderSettings.sun;

        if (sun == null)
        {
            foreach (Light l in FindObjectsByType<Light>(FindObjectsInactive.Exclude))
            {
                if (l.type == LightType.Directional) { sun = l; break; }
            }
        }

        if (sun == null)
        {
            GameObject go = new GameObject("Sun");
            sun = go.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
        }

        RenderSettings.sun = sun;
        sunShadows = sun.shadows; // remembered, so a sun without shadows stays that way

        GameObject moonGO = new GameObject("Moon Light");
        moonGO.transform.SetParent(transform, false);
        moon = moonGO.AddComponent<Light>();
        moon.type = LightType.Directional;
        moon.color = moonColor;
        moon.shadows = LightShadows.None;
        moon.intensity = 0f;
    }

    // True when we're driving the SkyMaster atmospheric shader
    private bool isSkyMasterSky = false;

    private void SetupSky()
    {
        // --- 1. Try SkyMaster v5 atmospheric skybox (best quality) ---
        Shader smShader = Shader.Find("SkyMaster/SkyMasterShaderE_SKYBOX U5 v5 Textured");
        if (smShader == null)
            smShader = Shader.Find("SkyMaster/SkyMasterShaderE_SKYBOX U5 v4 Textured");

        if (smShader != null)
        {
            Material smMat = null;
            if (RenderSettings.skybox != null && RenderSettings.skybox.shader == smShader) {
                smMat = RenderSettings.skybox;
            } else {
                Material[] allMats = Resources.FindObjectsOfTypeAll<Material>();
                foreach (Material m in allMats) {
                    if (m.shader == smShader && m.name.Contains("SkyMaster")) {
                        smMat = m; break;
                    }
                }
            }
            // If the material isn't loaded yet, create one from the shader with sane defaults
            if (smMat == null)
            {
                smMat = new Material(smShader);
                // Rayleigh/Mie atmospheric scattering constants (same as SkyMaster default preset)
                smMat.SetFloat("fOuterRadius",            10250f);
                smMat.SetFloat("fOuterRadius2",           105062500f);
                smMat.SetFloat("fInnerRadius",            10000f);
                smMat.SetFloat("fInnerRadius2",           100000000f);
                smMat.SetFloat("fKrESun",                 0.002317f);
                smMat.SetFloat("fKmESun",                 0.000264f);
                smMat.SetFloat("fKr4PI",                  0.044116f);
                smMat.SetFloat("fKm4PI",                  0.005027f);
                smMat.SetFloat("fScale",                  0.004f);
                smMat.SetFloat("fScaleDepth",             0.001f);
                smMat.SetFloat("fScaleOverScaleDepth",    0.06667f);
                smMat.SetFloat("fExposure",               0.4f);
                smMat.SetFloat("fSamples",                0.02f);
                smMat.SetFloat("g",                       -0.999f);
                smMat.SetFloat("g2",                      0.998f);
                smMat.SetFloat("Horizon_adj",             10.96f);
                smMat.SetFloat("HorizonY",                -0.06f);
                smMat.SetFloat("SunBlend",                66f);
                smMat.SetFloat("White_cutoff",            6.2f);
                smMat.SetFloat("_CloudCover", 0.65f);
                smMat.SetFloat("_CloudSharpness",         8f);
                smMat.SetFloat("_CloudDensity",           1f);
                smMat.SetFloat("_Light",                  1.4f);
                smMat.SetVector("v3InvWavelength",        new Vector4(84.93f, 158.94f, 345.39f, 0f));
                smMat.SetColor("GroundColor",             new Color(0.5f, 0.55f, 0.45f));
                smMat.SetColor("SunColor",                Color.white);
            }

            skyMat = new Material(smMat); // always work on a runtime copy
            RenderSettings.skybox = skyMat;
            isSkyMasterSky = true;
            skyHasExposure = false;

            


            


            

            // Debug.Log("[WorldCycle] Using SkyMaster atmospheric skybox.");
            return;
        }

        // --- 2. Fall back to our custom ToonSky shader ---
        Shader toonSky = Shader.Find("Custom/ToonSky");
        if (toonSky != null)
        {
            skyMat = new Material(toonSky);
            RenderSettings.skybox = skyMat;
            skyHasExposure = false;

            


            

            return;
        }

        // --- 3. Fall back to whatever is already set in RenderSettings ---
        if (RenderSettings.skybox != null)
        {
            skyMat = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyMat;
            skyHasExposure = skyMat.HasProperty("_Exposure");
            if (skyMat.HasProperty("_SkyTint")) skyTintProperty = "_SkyTint";
            else if (skyMat.HasProperty("_Tint")) skyTintProperty = "_Tint";
            return;
        }

        // --- 4. Last resort: solid camera background colour ---
        if (viewCamera != null)
        {
            solidColorSky = true;
            viewCamera.clearFlags = CameraClearFlags.SolidColor;
        }
    }

    private void ApplyLighting()
    {
        if (mapPushed) return; // the map camera is drawing right now

        float angle = SunAngle(GameClock.Hour);
        float height = Mathf.Sin(angle * Mathf.Deg2Rad); // 1 at noon, -1 at midnight
        float dayAmount = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.05f, 0.3f, height));
        float overcast = current.gray;

        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);
            float power = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.25f, height));
            sun.intensity = sunMaxIntensity * power * current.sun;
            sun.color = Overcast(sunColor.Evaluate(height), overcast * 0.6f);
            sun.shadows = sun.intensity > 0.02f ? sunShadows : LightShadows.None;
        }

        if (moon != null)
        {
            // The moon sits opposite the sun
            moon.transform.rotation = Quaternion.Euler(angle + 180f, sunYaw, 0f);
            float power = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 0.25f, -height));
            moon.intensity = moonMaxIntensity * power * Mathf.Lerp(1f, 0.5f, overcast);
        }

        // Ambient light
        Color ambient = Overcast(ambientColor.Evaluate(height), overcast);
        ambient *= Mathf.Lerp(1f, 0.75f, overcast) * ambientIntensity;
        ambient = Color.Lerp(ambient, Color.white, flash * 0.5f);
        ambient.a = 1f;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambient;

        // Fog
        Color fog = Overcast(fogColor.Evaluate(height), overcast);
        float targetDensity = baseFogDensity + current.fog;
        
        // Underwater Fog Check
        bool isUnderwater = false;
        if (Camera.main != null && WorldGenerator.Instance != null)
        {
            if (Camera.main.transform.position.y < WorldGenerator.Instance.waterLevel - 0.5f)
            {
                isUnderwater = true;
                fog = new Color(0.1f, 0.45f, 0.7f); // Deep underwater blue hue
                targetDensity = 0.05f; // Thick underwater fog
            }
        }
        
        if (underwaterVolume != null && underwaterVolume.profile != null)
        {
            underwaterVolume.weight = isUnderwater ? 1f : 0f;
        }

        if (controlFog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = fog;
            RenderSettings.fogDensity = targetDensity;
        }

        // Sky
        if (skyMat != null)
        {
            if (isSkyMasterSky)
            {
                // --- Drive the SkyMaster v5 atmospheric scattering skybox ---

                // Point the sun direction into the shader (it uses v3LightDir as a world direction)
                if (sun != null)
                {
                    Vector3 sunFwd = sun.transform.forward;
                    skyMat.SetVector("v3LightDir", new Vector4(-sunFwd.x, -sunFwd.y, -sunFwd.z, 0f));
                    skyMat.SetVector("_SunDir",    new Vector4(-sunFwd.x, -sunFwd.y, -sunFwd.z, 0f));
                }

                // Moon direction is opposite the sun
                if (moon != null)
                {
                    Vector3 moonFwd = moon.transform.forward;
                    skyMat.SetVector("v3LightDirMoon", new Vector4(-moonFwd.x, -moonFwd.y, -moonFwd.z, 0f));
                }

                // Exposure: bright at noon, dim at night, lightning flash
                float smExposure = Mathf.Lerp(0.08f, 0.55f, dayAmount) * Mathf.Lerp(1f, 0.5f, overcast);
                smExposure += flash * 0.8f;
                skyMat.SetFloat("fExposure", smExposure);

                // Sun intensity inside the shader
                float smLight = Mathf.Lerp(0.1f, 1.6f, dayAmount) * current.sun;
                skyMat.SetFloat("_Light", smLight);

                // Cloud cover rises with overcast weather
                float cloudCover = Mathf.Lerp(0.35f, 0.65f, overcast);
                // FORCE SKYMASTER 2D CLOUDS OFF COMPLETELY. WE ARE USING 3D MESH CLOUDS NOW.
                skyMat.SetFloat("_CloudCover", 0f);
                // Fix barcode tiling
                skyMat.SetFloat("_CloudDensity", 0.15f);
                // CRITICAL FIX: The base material has _Color.a = 0, which makes clouds 100% invisible! Force it to 1.
                skyMat.SetColor("_Color", new Color(1f, 1f, 1f, 1f));

                // Sunset: warm-tint the sun colour
                Color sunsetTint = Color.Lerp(
                    Color.white,
                    new Color(1f, 0.55f, 0.2f),
                    Mathf.Clamp01(1f - Mathf.Abs(height) * 4f) * dayAmount
                );
                skyMat.SetColor("SunColor", sunsetTint);

                // Ground colour matches the fog / terrain palette
                skyMat.SetColor("GroundColor", fog);
            }
            else if (skyMat.shader.name == "Custom/ToonSky")
            {
                if (sun != null) skyMat.SetVector("_SunDir", -sun.transform.forward);

                Color hardcodedHorizon = Color.Lerp(new Color(0.05f, 0.05f, 0.15f), new Color(0.5f, 0.7f, 1.0f), dayAmount);
                Color hardcodedZenith  = Color.Lerp(new Color(0.02f, 0.02f, 0.1f),  new Color(0.2f, 0.4f, 0.85f), dayAmount);
                float sunsetMix = Mathf.Clamp01(1f - (Mathf.Abs(height - 0.15f) * 4f));
                hardcodedHorizon = Color.Lerp(hardcodedHorizon, new Color(0.9f, 0.4f, 0.2f), sunsetMix * dayAmount);

                skyMat.SetColor("_HorizonColor", Overcast(hardcodedHorizon, overcast));
                skyMat.SetColor("_DayTint",      Overcast(hardcodedZenith,  overcast));
            }
            else
            {
                if (skyHasExposure)
                {
                    float exposure = Mathf.Lerp(skyExposureNight, skyExposureDay, dayAmount) * Mathf.Lerp(1f, 0.55f, overcast);
                    skyMat.SetFloat("_Exposure", exposure + flash * 1.5f);
                }
                if (skyTintProperty != null)
                    skyMat.SetColor(skyTintProperty, Overcast(skyTint.Evaluate(height), overcast));
            }
        }
        else if (solidColorSky && viewCamera != null)
        {
            viewCamera.backgroundColor = Color.Lerp(fog, Color.white, flash * 0.5f);
        }
    }

    // ------------------------------------------------- Map camera lighting
    // The map camera should see a bright, fog-free world at any time of day. These are called by WorldMap
    // just before and after that camera draws, so the player's view is never touched.

    public void PushMapLighting()
    {
        if (mapPushed) return;
        mapPushed = true;

        savedFog = RenderSettings.fog;
        savedAmbient = RenderSettings.ambientLight;
        RenderSettings.fog = false;
        RenderSettings.ambientLight = Color.white;

        if (sun != null)
        {
            savedSunIntensity = sun.intensity;
            savedSunColor = sun.color;
            savedSunRotation = sun.transform.rotation;
            sun.intensity = 0.2f;
            sun.color = Color.white;
            sun.transform.rotation = Quaternion.Euler(75f, 0f, 0f); // Tilt slightly to prevent blinding specular glare from flat water planes!
        }

        if (moon != null)
        {
            savedMoonEnabled = moon.enabled;
            moon.enabled = false;
        }
    }

    public void PopMapLighting()
    {
        if (!mapPushed) return;
        mapPushed = false;

        RenderSettings.fog = savedFog;
        RenderSettings.ambientLight = savedAmbient;

        if (sun != null)
        {
            sun.intensity = savedSunIntensity;
            sun.color = savedSunColor;
            sun.transform.rotation = savedSunRotation;
        }

        if (moon != null) moon.enabled = savedMoonEnabled;
    }

    // ----------------------------------------------------------------- Rain

    private void BuildRain()
    {
        GameObject go = new GameObject("Rain");
        go.transform.SetParent(transform, false);
        rainTransform = go.transform;

        rain = go.AddComponent<ParticleSystem>();
        rain.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = rain.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = 1.3f;
        main.startSpeed = 0f;
        main.startSize = 0.04f;
        main.startColor = new Color(0.75f, 0.82f, 0.95f, 0.5f);
        main.maxParticles = 6000;
        main.gravityModifier = 0f;

        ParticleSystem.EmissionModule emission = rain.emission;
        emission.rateOverTime = 0f;

        ParticleSystem.ShapeModule shape = rain.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(RainArea, 0.5f, RainArea);

        // Fall straight down (and drift with the wind, set in LateUpdate). All three axes must use the same mode.
        ParticleSystem.VelocityOverLifetimeModule vel = rain.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.World;
        vel.x = 0f;
        vel.y = -22f;
        vel.z = 0f;

        ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
        psr.renderMode = ParticleSystemRenderMode.Stretch;
        psr.lengthScale = 1f;
        psr.velocityScale = 0.06f;
        psr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        psr.receiveShadows = false;

        // Sprites/Default is transparent, unlit, and always included in builds
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
        if (shader != null) psr.material = new Material(shader);

        rain.Play();
    }

    // ------------------------------------------------------------------ HUD

    private void BuildHud()
    {
        GameObject canvasGO = UIKit.CreateCanvas("World UI", 3);

        RectTransform panel = UIKit.CreateImage(canvasGO.transform, "Clock", new Color(0f, 0f, 0f, 0.45f));
        WorldUI.Place(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(460f, 84f));
        UIKit.VLayout(panel.gameObject, 0f, 6, TextAnchor.MiddleCenter);

        hudTime = UIKit.CreateText(panel, "Time", "", 32, Color.white);
        UIKit.Size(hudTime, 440f, 40f);

        hudInfo = UIKit.CreateText(panel, "Info", "", 24, new Color(0.85f, 0.9f, 1f, 0.85f));
        UIKit.Size(hudInfo, 440f, 30f);
    }

    private void UpdateHud()
    {
        if (hudTime == null || Time.unscaledTime < nextHudUpdate) return;
        nextHudUpdate = Time.unscaledTime + 0.1f;

        hudTime.text = $"Day {GameClock.Day}  -  {GameClock.TimeString}";

        string info = $"{GameClock.PartOfDay}  -  {GameClock.WeatherLabel(GameClock.Weather)}";
        if (player != null) info += "  -  " + WorldUI.Compass(player.eulerAngles.y);
        hudInfo.text = info + $"  ·  {GameClock.CurrentSeason}";
    }

    // ----------------------------------------------------------- Seasons

    private Season SeasonFromDay(int day)
    {
        int index = ((day - 1) / daysPerSeason) % 4;
        return (Season)index;
    }

    private void ApplySeasonalDayLength()
    {
        if (!seasonalDayLength) return;
        switch (GameClock.CurrentSeason)
        {
            case Season.Summer:  GameClock.Begin(GameClock.Hour, sunrise - 1f, sunset + 1f); break; // long days
            case Season.Winter:  GameClock.Begin(GameClock.Hour, sunrise + 1f, sunset - 1f); break; // short days
            default:             GameClock.Begin(GameClock.Hour, sunrise,       sunset);       break; // Spring/Autumn neutral
        }
    }
}

// =====================================================================================
//  Panels: one at a time
// =====================================================================================

public interface IWorldPanel
{
    void SetOpen(bool open);
}

/// <summary>
/// The journal and the map share this. Only one is open at a time, while one is open nothing can be interacted with
/// and the cursor is free (which also stops mouse look). Esc closes it (PauseMenu asks CloseCurrent first).
/// </summary>
public static class WorldPanels
{
    private static IWorldPanel current;

    public static bool AnyOpen => current != null;
    public static bool IsOpen(IWorldPanel panel) => current == panel;

    /// <summary>Not while paused, in a conversation, or with the inventory panel open.</summary>
    public static bool CanOpen => Time.timeScale > 0f && !NPCSpawner.IsTalking && !Inventory.PanelOpen;

    internal static void Reset()
    {
        current = null;
    }

    public static void Toggle(IWorldPanel panel)
    {
        if (Time.timeScale == 0f) return;

        if (current == panel)
        {
            CloseCurrent();
            return;
        }

        if (!CanOpen) return;

        if (current != null)
        {
            current.SetOpen(false); // switching straight from the journal to the map keeps everything blocked
        }
        else
        {
            InteractionManager.Block();
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        current = panel;
        panel.SetOpen(true);
    }

    /// <summary>Closes whatever is open. Returns true if something was.</summary>
    public static bool CloseCurrent(bool relockCursor = true)
    {
        if (current == null) return false;

        IWorldPanel panel = current;
        current = null;
        panel.SetOpen(false);
        InteractionManager.Unblock();

        if (relockCursor)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
        return true;
    }

    // Called every frame by WorldCycle. If the inventory takes over, step aside and let it own the cursor.
    internal static void Tick()
    {
        if (current != null && (Inventory.PanelOpen || NPCSpawner.IsTalking))
            CloseCurrent(false);
    }
}

// =====================================================================================
//  Journal
// =====================================================================================

/// <summary>
/// Three pages: Quests (your active and finished favours), People (everyone you've talked to) and Log
/// (things that happened: people met, quests, first finds, weather). Fills itself in by watching the NPCs
/// and your Inventory. Other scripts can add lines with Journal.AddEntry("...").
/// WorldCycle adds this automatically.
/// </summary>
public class Journal : MonoBehaviour, IWorldPanel
{
    public static Journal Instance { get; private set; }

    /// <summary>Writes a line in the Log with today's date and time. Does nothing if there is no journal.</summary>
    public static void AddEntry(string text)
    {
        if (Instance != null) Instance.Write(text);
    }

    private class Entry
    {
        public string stamp;
        public string text;
    }

    // What we know about each NPC, filled in by watching them
    private class Seen
    {
        public bool met;
        public int metDay;
        public QuestState lastQuestState;
    }

    private const int MaxEntries = 120;
    private static readonly string[] TabNames = { "Quests", "People", "Log" };

    private readonly List<Entry> entries = new List<Entry>();
    private readonly Dictionary<NPCIdentity, Seen> people = new Dictionary<NPCIdentity, Seen>();
    private readonly HashSet<string> foundItems = new HashSet<string>();

    private GameObject panelRoot;
    private Text titleText;
    private RectTransform content;
    private ScrollRect scroll;
    private Button[] tabButtons;
    private int tab;
    private bool dirty;
    private float nextPoll;

    // ------------------------------------------------------------- Lifecycle

    private void Awake()
    {
        Instance = this;
        
        // Force post-processing on the main camera so our underwater volume actually works!
        if (Camera.main != null)
        {
            var camData = Camera.main.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null) camData = Camera.main.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
        }        
    }

    private void Start()
    {
        PauseMenu.EnsureEventSystem(); // the tabs and scrolling need one
        BuildUI();

        if (Inventory.Instance != null) Inventory.Instance.Changed += OnInventoryChanged;
        GameClock.WeatherChanged += OnWeatherChanged;
    }

    private void OnDestroy()
    {
        if (WorldPanels.IsOpen(this)) WorldPanels.CloseCurrent(false);
        if (Inventory.Instance != null) Inventory.Instance.Changed -= OnInventoryChanged;
        GameClock.WeatherChanged -= OnWeatherChanged;
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Keybinds.WasPressed(GameAction.Journal)) WorldPanels.Toggle(this);

        if (Time.unscaledTime >= nextPoll)
        {
            nextPoll = Time.unscaledTime + 1f;
            PollWorld();
        }

        if (dirty && WorldPanels.IsOpen(this)) Rebuild();
    }

    public void SetOpen(bool open)
    {
        if (panelRoot == null) return;

        panelRoot.SetActive(open);
        if (!open) return;

        titleText.text = $"JOURNAL  [{Keybinds.KeyName(GameAction.Journal)}]";
        PollWorld();
        Rebuild();
        scroll.verticalNormalizedPosition = 1f;
    }

    // ------------------------------------------------------------ Watching

    private void Write(string text)
    {
        entries.Insert(0, new Entry { stamp = GameClock.Stamp, text = text });
        if (entries.Count > MaxEntries) entries.RemoveAt(entries.Count - 1);
        dirty = true;
    }

    // Looks at every NPC once a second: have we just met them? has their favour changed?
    private void PollWorld()
    {
        foreach (NPCIdentity id in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Exclude))
        {
            if (!people.TryGetValue(id, out Seen seen))
            {
                seen = new Seen { lastQuestState = id.HasQuest ? id.Quest.State : QuestState.Available };
                people[id] = seen;
            }

            if (!seen.met && id.TimesTalked > 0)
            {
                seen.met = true;
                seen.metDay = GameClock.Day;
                Write($"Met {id.NpcName}, {WorldUI.Article(id.Job)} {id.Job}.");
                if (Inventory.Instance != null) Inventory.Instance.Notify("Journal: met " + id.NpcName);
            }

            if (id.HasQuest && id.Quest.State != seen.lastQuestState)
            {
                Quest q = id.Quest;
                if (q.State == QuestState.Active)
                    Write($"Agreed to help {id.NpcName}: bring {q.ItemPhrase}.");
                else if (q.State == QuestState.Completed)
                    Write($"Finished {id.NpcName}'s favour and earned {q.RewardCoins} coins.");

                seen.lastQuestState = q.State;
                dirty = true;
            }
        }
    }

    private void OnInventoryChanged()
    {
        Inventory inv = Inventory.Instance;
        if (inv != null)
        {
            foreach (ItemDefinition def in Inventory.AllItems)
            {
                if (inv.GetCount(def.Id) > 0 && foundItems.Add(def.Id))
                    Write($"Found my first {def.DisplayName.ToLower()}.");
            }
        }

        dirty = true; // quest progress ("1/3") changes with your items
    }

    private void OnWeatherChanged(WeatherType type)
    {
        switch (type)
        {
            case WeatherType.Clear:  Write("The skies cleared."); break;
            case WeatherType.Cloudy: Write("Clouds rolled in."); break;
            case WeatherType.Rain:   Write("It started to rain."); break;
            case WeatherType.Storm:  Write("A thunderstorm broke out."); break;
            case WeatherType.Fog:    Write("A thick fog settled over the land."); break;
        }
    }

    // ------------------------------------------------------------------- UI

    private void BuildUI()
    {
        GameObject canvasGO = UIKit.CreateCanvas("Journal UI", 8);

        RectTransform panel = UIKit.CreateImage(canvasGO.transform, "JournalPanel", UIKit.PanelColor);
        WorldUI.Center(panel, new Vector2(1100f, 860f));
        UIKit.VLayout(panel.gameObject, 14f, 30, TextAnchor.UpperCenter);

        titleText = UIKit.CreateText(panel, "Title", "JOURNAL", 48, Color.white);
        UIKit.Size(titleText, 1040f, 64f);

        RectTransform tabRow = UIKit.CreateRect(panel, "Tabs");
        UIKit.HLayout(tabRow.gameObject, 12f);
        tabRow.GetComponent<HorizontalLayoutGroup>().childAlignment = TextAnchor.MiddleCenter;
        UIKit.Size(tabRow, 1040f, 60f);

        tabButtons = new Button[TabNames.Length];
        for (int i = 0; i < TabNames.Length; i++)
        {
            int index = i;
            tabButtons[i] = UIKit.CreateButton(tabRow, TabNames[i] + " Tab", TabNames[i], 30, () => SelectTab(index), out _);
            UIKit.Size(tabButtons[i], 240f, 56f);
        }

        scroll = WorldUI.BuildScroll(panel, out content);
        UIKit.Size(scroll, 1040f, 600f);

        Text hint = UIKit.CreateText(panel, "Hint", "Scroll for more. Esc closes.", 22, UIKit.Subtle);
        UIKit.Size(hint, 1040f, 30f);

        panelRoot = panel.gameObject;
        panelRoot.SetActive(false);
        HighlightTab();
    }

    private void SelectTab(int index)
    {
        tab = index;
        HighlightTab();
        Rebuild();
        scroll.verticalNormalizedPosition = 1f;
    }

    private void HighlightTab()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            ColorBlock cb = tabButtons[i].colors;
            cb.normalColor = i == tab ? UIKit.Accent : UIKit.ButtonColor;
            cb.selectedColor = cb.normalColor;
            tabButtons[i].colors = cb;
        }
    }

    private void Rebuild()
    {
        dirty = false;

        foreach (Transform child in content)
        {
            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        switch (tab)
        {
            case 0: BuildQuests(); break;
            case 1: BuildPeople(); break;
            default: BuildLog(); break;
        }
    }

    // ------------------------------------------------------------ Row helpers

    private void Header(string text)
    {
        Text t = UIKit.CreateText(content, "Header", text, 26, UIKit.Accent, TextAnchor.MiddleLeft);
        t.fontStyle = FontStyle.Bold;
        UIKit.Size(t, 1000f, 38f);
    }

    private void Note(string text)
    {
        Text t = UIKit.CreateText(content, "Note", text, 26, UIKit.Subtle, TextAnchor.UpperLeft);
        UIKit.Size(t, 1000f, -1f);
    }

    private RectTransform Card()
    {
        RectTransform card = UIKit.CreateImage(content, "Card", new Color(1f, 1f, 1f, 0.07f));
        UIKit.VLayout(card.gameObject, 4f, 14, TextAnchor.UpperLeft);
        UIKit.Size(card, 1000f, -1f);
        return card;
    }

    private void CardLine(RectTransform card, string text, int size, Color color, bool bold = false)
    {
        Text t = UIKit.CreateText(card, "Line", text, size, color, TextAnchor.UpperLeft);
        if (bold) t.fontStyle = FontStyle.Bold;
        UIKit.Size(t, 972f, -1f);
    }

    // ------------------------------------------------------------------ Pages

    private void BuildQuests()
    {
        List<NPCIdentity> active = new List<NPCIdentity>();
        List<NPCIdentity> done = new List<NPCIdentity>();

        foreach (KeyValuePair<NPCIdentity, Seen> kv in people)
        {
            NPCIdentity id = kv.Key;
            if (id == null || !id.HasQuest) continue;
            if (id.Quest.State == QuestState.Active) active.Add(id);
            else if (id.Quest.State == QuestState.Completed) done.Add(id);
        }

        Header("ACTIVE FAVOURS");

        if (active.Count == 0)
        {
            Note($"Nothing right now. Hold [{Keybinds.KeyName(GameAction.Interact)}] near someone to talk. Some of them need a hand.");
        }

        foreach (NPCIdentity id in active)
        {
            Quest q = id.Quest;
            int have = Inventory.Instance != null ? Inventory.Instance.GetCount(q.ItemId) : 0;
            bool ready = have >= q.Amount;

            RectTransform card = Card();
            CardLine(card, $"Bring {q.ItemPhrase} to {id.NpcName} ({id.Job})", 28, Color.white, true);
            CardLine(card,
                $"{Mathf.Min(have, q.Amount)}/{q.Amount}   -   Reward: {q.RewardCoins} coins" + (ready ? "   -   ready to hand in!" : ""),
                24, ready ? UIKit.Good : UIKit.Subtle);
        }

        if (active.Count > 0)
            Note($"They're marked on your map [{Keybinds.KeyName(GameAction.Map)}].");

        if (done.Count > 0)
        {
            Header($"FINISHED ({done.Count})");
            foreach (NPCIdentity id in done)
            {
                Quest q = id.Quest;
                Text t = UIKit.CreateText(content, "Done", $"{id.NpcName}: {q.ItemPhrase}, paid {q.RewardCoins} coins", 24, UIKit.Subtle, TextAnchor.MiddleLeft);
                UIKit.Size(t, 1000f, 32f);
            }
        }
    }

    private void BuildPeople()
    {
        List<KeyValuePair<NPCIdentity, Seen>> met = new List<KeyValuePair<NPCIdentity, Seen>>();
        foreach (KeyValuePair<NPCIdentity, Seen> kv in people)
            if (kv.Key != null && kv.Value.met) met.Add(kv);

        met.Sort((a, b) =>
        {
            int c = a.Value.metDay.CompareTo(b.Value.metDay);
            return c != 0 ? c : string.Compare(a.Key.NpcName, b.Key.NpcName, System.StringComparison.Ordinal);
        });

        Header($"PEOPLE YOU'VE MET ({met.Count})");

        if (met.Count == 0)
        {
            Note($"Nobody yet. Hold [{Keybinds.KeyName(GameAction.Interact)}] near someone to talk to them.");
            return;
        }

        foreach (KeyValuePair<NPCIdentity, Seen> kv in met)
        {
            NPCIdentity id = kv.Key;
            int times = id.TimesTalked;

            RectTransform card = Card();
            CardLine(card, $"{id.NpcName}  -  {id.Job}", 30, Color.white, true);
            CardLine(card, $"{id.Personality}  -  met on day {kv.Value.metDay}  -  talked {times} time{(times == 1 ? "" : "s")}", 24, UIKit.Subtle);

            if (id.IsTrader) CardLine(card, "Trader: talk to them to buy and sell.", 24, UIKit.Gold);

            if (id.HasQuest && id.Quest.State == QuestState.Active)
                CardLine(card, $"Waiting for {id.Quest.ItemPhrase}.", 24, UIKit.Accent);
            else if (id.HasQuest && id.Quest.State == QuestState.Completed)
                CardLine(card, "Favour done.", 24, UIKit.Good);
        }
    }

    private void BuildLog()
    {
        Header("LOG");

        if (entries.Count == 0)
        {
            Note("Nothing written yet.");
            return;
        }

        foreach (Entry e in entries)
        {
            Text t = UIKit.CreateText(content, "Entry", $"<color=#8FA0C0>{e.stamp}</color>\n{e.text}", 26, Color.white, TextAnchor.UpperLeft);
            UIKit.Size(t, 1000f, -1f);
        }
    }
}

// =====================================================================================
//  Map
// =====================================================================================

/// <summary>
/// A live top-down map, drawn by an extra camera that only runs while the map is open (north is up).
/// You're the white arrow. People near you show as dots (named once you've met them), and anyone you're
/// helping with a favour is a gold marker wherever they are. Scroll to zoom, drag to pan.
/// The map shows the object on your "Ground" layer, same as pickups and NPCs use.
/// WorldCycle adds this automatically.
/// </summary>
public class WorldMap : MonoBehaviour, IWorldPanel
{
    public static WorldMap Instance { get; private set; }

    [Tooltip("People closer than this (metres) to you show up on the map.")]
    [SerializeField] private float revealRadius = 45f;
    [Tooltip("How high above the ground the map camera sits. Higher than your tallest tree or building.")]
    [SerializeField] private float cameraHeight = 250f;
    [Tooltip("What the map camera draws. Untick layers you don't want to see from above.")]
    [SerializeField] private LayerMask mapLayers = ~0;
    [SerializeField] private int textureSize = 1024;
    [Tooltip("How much the map shows (half-width in metres) the first time it's opened.")]
    [SerializeField] private float startHalfSize = 60f;

    private const float MapSize = 740f; // on-screen size in UI units

    private class Marker
    {
        public NPCIdentity npc;
        public Transform target;
        public RectTransform rect;
        public Image image;
        public Text label;
        public string nameLabel;
        public string questLabel;
    }

    private Transform player;
    private Bounds ground;

    private Vector2 center;          // world x, z in the middle of the map
    private float halfSize;          // metres from the middle to the edge
    private float minHalf = 15f;
    private float maxHalf;
    private bool zoomReady;

    private Texture2D bgTexture;
    private RawImage bgImage;
    private Color32[] bgPixels;
    private Vector2 lastBgCenter = new Vector2(-99999, -99999);
    private float lastBgSize = -1f;

    private Camera mapCam;
    private RenderTexture texture;

    private Canvas canvas;
    private GameObject panelRoot;
    private Text titleText;
    private Text zoomText;
    private RawImage mapImage;
    private RectTransform markerRoot;
    private RectTransform playerMarker;
    private readonly List<Marker> markers = new List<Marker>();
    private float nextScan;

    // ------------------------------------------------------------- Prewarm

    private static Material cachedOverlayMat;

    /// <summary>
    /// Called from the loading screen so opening the map never has to compile shaders / allocate the render
    /// texture on the spot: builds the camera + texture, renders one throwaway frame, and warms the UI overlay material.
    /// </summary>
    public System.Collections.IEnumerator Prewarm()
    {
        if (panelRoot == null) yield break;

        if (cachedOverlayMat == null)
        {
            Shader overlayShader = Shader.Find("UI/Overlay");
            if (overlayShader != null) cachedOverlayMat = new Material(overlayShader);
        }

        EnsureCamera();
        yield return null;

        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }
        if (mapCam == null) yield break;

        Vector3 pos = player != null ? player.position : Vector3.zero;
        mapCam.transform.position = new Vector3(pos.x, ground.max.y + cameraHeight, pos.z);
        mapCam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        mapCam.orthographicSize = Mathf.Clamp(startHalfSize, minHalf, maxHalf);

        // The camera stays disabled; a manual render compiles every shader variant the map will use.
        mapCam.Render();
        yield return null;
    }

    // ------------------------------------------------------------- Lifecycle

    private void Awake()
    {
        Instance = this;
        
        // Force post-processing on the main camera so our underwater volume actually works!
        if (Camera.main != null)
        {
            var camData = Camera.main.gameObject.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (camData == null) camData = Camera.main.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
        }        
    }

    private UnityEngine.UI.Button snapButton;
    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        else Debug.LogWarning("WorldMap: no PlayerController found, so the map has nothing to centre on.", this);

        ground = new Bounds(Vector3.zero, new Vector3(10000f, 20f, 10000f));
        maxHalf = 1000f;

        PauseMenu.EnsureEventSystem();
        BuildUI();

        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += OnBeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering += OnEndCamera;
    }

    private void OnDestroy()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
        UnityEngine.Rendering.RenderPipelineManager.endCameraRendering -= OnEndCamera;

        if (WorldPanels.IsOpen(this)) WorldPanels.CloseCurrent(false);
        if (texture != null) { texture.Release(); Destroy(texture); }
        if (bgTexture != null) Destroy(bgTexture);
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (Keybinds.WasPressed(GameAction.Map)) WorldPanels.Toggle(this);
    }

    // LateUpdate so the markers use the positions after everything has moved this frame
    private void LateUpdate()
    {
        if (WorldPanels.IsOpen(this)) UpdateMap();
    }

    public void SetOpen(bool open)
    {
        if (panelRoot == null) return;

        panelRoot.SetActive(open);

        if (!open)
        {
            if (mapCam != null) mapCam.enabled = false;
            if (canvas != null)
            {
                foreach (UnityEngine.UI.Graphic g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true)) {
                    g.material = null; // Revert to default
                }
                canvas.transform.SetParent(null);
                canvas.renderMode = RenderMode.ScreenSpaceOverlay; // default non-world state for hidden canvas
            }
            
            CameraController camCtrl2 = UnityEngine.Object.FindAnyObjectByType<CameraController>();
            if (camCtrl2 != null) camCtrl2.IsInMenu = false;
            
            UIBillboardHider.SetHidden(false);
            return;
        }

        EnsureCamera();

        if (!zoomReady)
        {
            halfSize = Mathf.Clamp(startHalfSize, minHalf, maxHalf);
            zoomReady = true;
        }

        titleText.text = $"MAP  [{Keybinds.KeyName(GameAction.Map)}]";
        CenterOnPlayer();
        RefreshMarkers();
        mapCam.enabled = true;
        UpdateMap();

        CameraController camCtrl = UnityEngine.Object.FindAnyObjectByType<CameraController>();
        Camera mainCam = (camCtrl != null) ? camCtrl.GetComponent<Camera>() : Camera.main;

        if (canvas != null && mainCam != null)
        {
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.worldCamera = mainCam;
            
            var pc = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
            if (pc != null)
            {
                canvas.transform.SetParent(pc.transform);
                // Same transform as Inventory UI
                canvas.transform.localPosition = new Vector3(0.85f, 1.35f, 1.2f);
                canvas.transform.localEulerAngles = new Vector3(0f, 14f, 0f);
                canvas.transform.localScale = new Vector3(0.0022f, 0.0022f, 0.0022f); // Slightly smaller than inventory (0.0028f) but readable
            }
            
            panelRoot.GetComponent<RectTransform>().localEulerAngles = Vector3.zero;
            panelRoot.GetComponent<RectTransform>().anchoredPosition = Vector3.zero;

            // Apply ZTest Always material
            Shader overlayShader = Shader.Find("UI/Overlay");
            if (overlayShader != null)
            {
                Material overlayMat = cachedOverlayMat != null ? cachedOverlayMat : new Material(overlayShader);
                foreach (UnityEngine.UI.Graphic g in canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    g.material = overlayMat;
                }
            }
        }

        if (camCtrl != null) camCtrl.IsInMenu = true;

        // Hide nearby billboards while map is open
        UIBillboardHider.SetHidden(true);
    }

    // --------------------------------------------------------------- Camera

    private static bool FindGroundBounds(out Bounds bounds)
    {
        bounds = default;

        int layer = LayerMask.NameToLayer("Ground");
        if (layer == -1) return false;

        bool any = false;
        foreach (Collider col in FindObjectsByType<Collider>(FindObjectsInactive.Exclude))
        {
            if (col.gameObject.layer != layer) continue;

            if (!any) { bounds = col.bounds; any = true; }
            else bounds.Encapsulate(col.bounds);
        }
        return any;
    }

    private void EnsureCamera()
    {
        if (mapCam != null) return;

        GameObject go = new GameObject("Map Camera");
        go.transform.SetParent(transform, false);

        mapCam = go.AddComponent<Camera>();
        mapCam.orthographic = true;
        mapCam.clearFlags = CameraClearFlags.SolidColor;
        mapCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        mapCam.cullingMask = (mapLayers & ~(1 << 2)) | (1 << 4); // Exclude 2 & 4, Include MapWater (13)
        mapCam.nearClipPlane = 1f;
        mapCam.farClipPlane = cameraHeight + ground.size.y + 100f;
        mapCam.allowHDR = false;
        mapCam.allowMSAA = false;
        mapCam.enabled = false; // only renders while the map is open

        texture = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
        texture.name = "Map Texture";
        texture.antiAliasing = 1;
        mapCam.targetTexture = texture;
        mapImage.texture = texture;
        mapImage.color = Color.white;
    }

    // The map camera sees a bright, fog-free world whatever the time or weather is. Nothing else is affected.
    private void OnBeginCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        if (cam != null && cam == mapCam && WorldCycle.Instance != null) WorldCycle.Instance.PushMapLighting();
    }

    private void OnEndCamera(UnityEngine.Rendering.ScriptableRenderContext context, Camera cam)
    {
        if (cam != null && cam == mapCam && WorldCycle.Instance != null) WorldCycle.Instance.PopMapLighting();
    }

    // ----------------------------------------------------------- Zoom & pan

    private void CenterOnPlayer()
    {
        if (player != null) center = new Vector2(player.position.x, player.position.z);
        ClampCenter();
    }

    private void ClampCenter()
    {
        center.x = Mathf.Clamp(center.x, ground.min.x, ground.max.x);
        center.y = Mathf.Clamp(center.y, ground.min.z, ground.max.z);
    }

    private void Zoom(float factor)
    {
        halfSize = Mathf.Clamp(halfSize * factor, minHalf, maxHalf);
    }

    private void OnScrolled(float amount)
    {
        if (Mathf.Approximately(amount, 0f)) return;
        Zoom(amount > 0f ? 0.8f : 1.25f);
    }

    private void OnClicked(UnityEngine.EventSystems.PointerEventData eventData)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(markerRoot, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);
        
        Transform bestTarget = null;
        float bestDist = 100f; // Increased click radius

        foreach (Marker m in markers)
        {
            if (m.rect.gameObject.activeSelf && (m.npc != null || m.target != null))
            {
                float dist = Vector2.Distance(localPoint, m.rect.anchoredPosition);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestTarget = m.npc != null ? m.npc.transform : m.target;
                }
            }
        }

        if (bestTarget != null)
        {
            QuestLog ql = FindAnyObjectByType<QuestLog>();
            if (ql != null) 
            {
                ql.ForceTrackTarget(bestTarget);
            }
        }
    }

        private void OnDragged(Vector2 screenDelta)
    {
        // Screen pixels to metres: the map is MapSize UI units wide, canvas.scaleFactor pixels per unit
        float metresPerPixel = (halfSize * 2f) / (MapSize * canvas.scaleFactor);
        center.x -= screenDelta.x * metresPerPixel;
        center.y -= screenDelta.y * metresPerPixel;
        ClampCenter();
    }

    private Vector2 ToMap(Vector3 world)
    {
        return new Vector2(world.x - center.x, world.z - center.y) * (MapSize * 0.5f / halfSize);
    }

    // ---------------------------------------------------------------- Update

    private void UpdateMapBackground()
    {
        if (WorldGenerator.Instance == null || bgTexture == null) return;
        
        if (Vector2.Distance(lastBgCenter, center) < 1f && Mathf.Abs(lastBgSize - halfSize) < 0.5f) return;
        lastBgCenter = center;
        lastBgSize = halfSize;
        
        int res = 256;
        float startX = center.x - halfSize;
        float endX = center.x + halfSize;
        float startZ = center.y - halfSize;
        float endZ = center.y + halfSize;
        
        for (int y = 0; y < res; y++)
        {
            float wz = Mathf.Lerp(startZ, endZ, (float)y / (res - 1));
            for (int x = 0; x < res; x++)
            {
                float wx = Mathf.Lerp(startX, endX, (float)x / (res - 1));
                float h = WorldGenerator.Instance.GetHeightAt(wx, wz);
                Color c = WorldGenerator.Instance.GetMapColorAt(wx, wz, h);
                bgPixels[y * res + x] = c;
            }
        }
        bgTexture.SetPixels32(bgPixels);
        bgTexture.Apply(false);
    }

    private void UpdateMap()
    {
        if (mapCam == null || player == null) return;
        
        // Global mouse scroll for zooming (doesn't require precise mouse hovering over the 3D map canvas)
#if ENABLE_INPUT_SYSTEM
        float scroll = UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.scroll.ReadValue().y : 0f;
#else
        float scroll = UnityEngine.Input.mouseScrollDelta.y;
#endif
        if (Mathf.Abs(scroll) > 0.01f)
        {
            Zoom(scroll > 0f ? 0.8f : 1.25f);
        }

#if ENABLE_INPUT_SYSTEM
        var mouse = UnityEngine.InputSystem.Mouse.current;
        bool isDragging = mouse != null && mouse.leftButton.isPressed;
        Vector2 mouseDelta = mouse != null ? mouse.delta.ReadValue() : Vector2.zero;
#else
        bool isDragging = UnityEngine.Input.GetMouseButton(0);
        Vector2 mouseDelta = new Vector2(UnityEngine.Input.GetAxis("Mouse X") * 10f, UnityEngine.Input.GetAxis("Mouse Y") * 10f);
#endif
        if (isDragging)
        {
            float panSpeed = halfSize * 0.002f; 
            center.x -= mouseDelta.x * panSpeed;
            center.y -= mouseDelta.y * panSpeed;
        }

        UpdateMapBackground();

        // Top-down camera, rotated so north (+Z) is up on screen
        float top = ground.max.y + cameraHeight;
        mapCam.transform.SetPositionAndRotation(new Vector3(center.x, top, center.y), Quaternion.Euler(90f, 0f, 0f));
        mapCam.orthographicSize = halfSize;

        zoomText.text = "Showing " + Mathf.RoundToInt(halfSize * 2f) + " m across";

        if (Time.unscaledTime >= nextScan)
        {
            nextScan = Time.unscaledTime + 1f;
            RefreshMarkers();
        }

        // You
        playerMarker.anchoredPosition = ToMap(player.position);
        playerMarker.localRotation = Quaternion.Euler(0f, 0f, -player.eulerAngles.y);

        // Everyone else
        Vector3 pp = player.position;
        float reveal = revealRadius * revealRadius;

        foreach (Marker m in markers)
        {
            if (m.npc == null && m.target == null)
            {
                m.rect.gameObject.SetActive(false);
                continue;
            }

            Vector3 p = m.npc != null ? m.npc.transform.position : m.target.position;
            float dx = p.x - pp.x;
            float dz = p.z - pp.z;

            bool quest = m.npc != null && m.npc.HasQuest && m.npc.Quest.State == QuestState.Active;
            bool show = m.target != null || quest || dx * dx + dz * dz <= reveal;

            m.rect.gameObject.SetActive(show);
            if (!show) continue;

            if (m.npc != null)
            {
                bool met = m.npc.TimesTalked > 0;
                Color color;
                float size;
                string label;

                if (quest)                        { color = UIKit.Gold;   size = 26f; label = m.questLabel; }
                else if (met && m.npc.IsTrader)   { color = UIKit.Good;   size = 20f; label = m.nameLabel; }
                else if (met)                     { color = UIKit.Accent; size = 20f; label = m.nameLabel; }
                else                              { color = new Color(0.78f, 0.82f, 0.9f); size = 15f; label = ""; }

                m.rect.anchoredPosition = ToMap(p);
                m.rect.sizeDelta = new Vector2(size, size);
                m.image.color = color;
                m.label.text = label;
                m.label.gameObject.SetActive(label.Length > 0);
            }
            else
            {
                m.rect.anchoredPosition = ToMap(p);
            }
        }
    }

    private void RefreshMarkers()
    {
        foreach (NPCIdentity id in FindObjectsByType<NPCIdentity>(FindObjectsInactive.Exclude))
        {
            bool known = false;
            foreach (Marker m in markers)
                if (m.npc == id) { known = true; break; }
            if (!known) markers.Add(CreateMarker(id));
        }
        foreach (CraftingStation cs in FindObjectsByType<CraftingStation>(FindObjectsInactive.Exclude))
        {
            bool known = false;
            foreach (Marker m in markers) if (m.target == cs.transform) { known = true; break; }
            if (!known) markers.Add(CreateStaticMarker(cs.transform, "Crafter", new Color(1f, 0.75f, 0.2f)));
        }
        foreach (QuestZone qz in FindObjectsByType<QuestZone>(FindObjectsInactive.Exclude))
        {
            bool known = false;
            foreach (Marker m in markers) if (m.target == qz.transform) { known = true; break; }
            if (!known) markers.Add(CreateStaticMarker(qz.transform, qz.ZoneId, new Color(0.4f, 0.8f, 1f)));
        }

        for (int i = markers.Count - 1; i >= 0; i--)
        {
            if (markers[i].npc != null || markers[i].target != null) continue;
            Destroy(markers[i].rect.gameObject);
            markers.RemoveAt(i);
        }

        playerMarker.SetAsLastSibling(); // you're always drawn on top
    }

    private Marker CreateStaticMarker(Transform t, string name, Color c)
    {
        RectTransform rect = UIKit.CreateImage(markerRoot, name + " Marker", c);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(24f, 24f);

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Text label = UIKit.CreateText(rect, "Label", name, 22, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(220f, 28f);
        lr.anchoredPosition = new Vector2(0f, -2f);

        Outline labelOutline = label.gameObject.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        labelOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return new Marker
        {
            target = t,
            rect = rect,
            image = rect.GetComponent<Image>(),
            label = label,
            nameLabel = name,
            questLabel = name
        };
    }

    private Marker CreateMarker(NPCIdentity id)
    {
        RectTransform rect = UIKit.CreateImage(markerRoot, id.NpcName + " Marker", Color.white);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);

        Image image = rect.GetComponent<Image>();
        image.sprite = MapSprites.Circle;

        Outline outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
        outline.effectDistance = new Vector2(1.5f, -1.5f);

        Text label = UIKit.CreateText(rect, "Label", "", 22, Color.white);
        RectTransform lr = label.rectTransform;
        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0f);
        lr.pivot = new Vector2(0.5f, 1f);
        lr.sizeDelta = new Vector2(220f, 28f);
        lr.anchoredPosition = new Vector2(0f, -2f);

        Outline labelOutline = label.gameObject.AddComponent<Outline>();
        labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
        labelOutline.effectDistance = new Vector2(1.5f, -1.5f);

        return new Marker
        {
            npc = id,
            rect = rect,
            image = image,
            label = label,
            nameLabel = id.NpcName,
            questLabel = id.NpcName + " !"
        };
    }

    // ------------------------------------------------------------------- UI

    private void BuildUI()
    {
        GameObject canvasGO = UIKit.CreateCanvas("Map UI", 8);
        canvas = canvasGO.GetComponent<Canvas>();

        RectTransform panel = UIKit.CreateImage(canvasGO.transform, "MapPanel", UIKit.PanelColor);
        WorldUI.Center(panel, new Vector2(1100f, 860f));

        titleText = UIKit.CreateText(panel, "Title", "MAP", 48, Color.white);
        WorldUI.Place(titleText.rectTransform, new Vector2(0f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(0f, 60f));
        titleText.rectTransform.anchorMax = new Vector2(1f, 1f); // stretch across the top
        
        GameObject btnGO = new GameObject("SnapButton", typeof(RectTransform));
        btnGO.transform.SetParent(panel.transform, false);
        RectTransform btnRT = btnGO.GetComponent<RectTransform>();
        btnRT.anchorMin = new Vector2(1, 1);
        btnRT.anchorMax = new Vector2(1, 1);
        btnRT.pivot = new Vector2(1, 1);
        btnRT.anchoredPosition = new Vector2(-20, -20);
        btnRT.sizeDelta = new Vector2(140, 40);
        
        UnityEngine.UI.Image btnImg = btnGO.AddComponent<UnityEngine.UI.Image>();
        btnImg.color = new Color(0.9f, 0.9f, 0.9f, 1f);
        snapButton = btnGO.AddComponent<UnityEngine.UI.Button>();
        snapButton.onClick.AddListener(() => CenterOnPlayer());
        
        GameObject btnTextGO = new GameObject("Text", typeof(RectTransform));
        btnTextGO.transform.SetParent(btnGO.transform, false);
        RectTransform btRT = btnTextGO.GetComponent<RectTransform>();
        btRT.anchorMin = Vector2.zero;
        btRT.anchorMax = Vector2.one;
        btRT.sizeDelta = Vector2.zero;
        btRT.anchoredPosition = Vector2.zero;
        UnityEngine.UI.Text btText = btnTextGO.AddComponent<UnityEngine.UI.Text>();
        btText.text = "Snap to Player";
        btText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        btText.color = Color.black;
        btText.alignment = TextAnchor.MiddleCenter;
        btText.fontSize = 18;

        // Map area, with a thin frame behind it
        RectTransform frame = UIKit.CreateImage(panel, "MapFrame", new Color(1f, 1f, 1f, 0.18f));
        WorldUI.Place(frame, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(26f, -86f), new Vector2(MapSize + 8f, MapSize + 8f));

        GameObject bgGO = new GameObject("MapBackground", typeof(RectTransform), typeof(RawImage));
        bgGO.transform.SetParent(panel, false);
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        WorldUI.Place(bgRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(MapSize, MapSize));
        bgImage = bgGO.GetComponent<RawImage>();
        bgTexture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        bgTexture.filterMode = FilterMode.Bilinear;
        bgImage.texture = bgTexture;
        bgPixels = new Color32[256 * 256];

        GameObject viewGO = new GameObject("MapView", typeof(RectTransform), typeof(RawImage), typeof(RectMask2D));
        viewGO.transform.SetParent(panel, false);
        RectTransform viewRect = viewGO.GetComponent<RectTransform>();
        WorldUI.Place(viewRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -90f), new Vector2(MapSize, MapSize));

        mapImage = viewGO.GetComponent<RawImage>();
        mapImage.color = new Color(0.07f, 0.09f, 0.11f, 1f);
        mapImage.raycastTarget = true;

        MapInput input = viewGO.AddComponent<MapInput>();
        input.Dragged = OnDragged;
        input.Scrolled = OnScrolled;
        input.Clicked = OnClicked;

        markerRoot = UIKit.CreateRect(viewRect, "Markers");
        markerRoot.anchorMin = Vector2.zero;
        markerRoot.anchorMax = Vector2.one;
        markerRoot.offsetMin = Vector2.zero;
        markerRoot.offsetMax = Vector2.zero;

        // You: a white arrow that turns as you turn
        playerMarker = UIKit.CreateImage(markerRoot, "Player", Color.white);
        playerMarker.anchorMin = playerMarker.anchorMax = new Vector2(0.5f, 0.5f);
        playerMarker.pivot = new Vector2(0.5f, 0.5f);
        playerMarker.sizeDelta = new Vector2(16f, 32f);
        playerMarker.GetComponent<Image>().sprite = MapSprites.Triangle;

        zoomText = UIKit.CreateText(panel, "Zoom Info", "Showing ... m across", 24, Color.white, TextAnchor.MiddleRight);
        WorldUI.Place(zoomText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-26f, 12f), new Vector2(300f, 30f));

        Text hint = UIKit.CreateText(panel, "Hint", "Click to track.\nDrag to pan.\nEsc closes.", 22, UIKit.Subtle, TextAnchor.UpperLeft);
        UIKit.Size(hint, 270f, 100f);

        panelRoot = panel.gameObject;
        panelRoot.SetActive(false);
    }

    private static void AddLegend(Transform parent, Sprite sprite, Color color, string text)
    {
        RectTransform row = UIKit.CreateRect(parent, "Legend " + text);
        UIKit.HLayout(row.gameObject, 12f);
        UIKit.Size(row, 270f, 34f);

        RectTransform dot = UIKit.CreateImage(row, "Dot", color);
        dot.GetComponent<Image>().sprite = sprite;
        UIKit.Size(dot, 24f, 24f);

        Text label = UIKit.CreateText(row, "Text", text, 24, Color.white, TextAnchor.MiddleLeft);
        UIKit.Size(label, 230f, 34f);
    }
}

// =====================================================================================
//  Helpers
// =====================================================================================

/// <summary>Lets the map be dragged and zoomed with the mouse.</summary>
public class MapInput : MonoBehaviour, UnityEngine.EventSystems.IDragHandler, UnityEngine.EventSystems.IScrollHandler, UnityEngine.EventSystems.IPointerClickHandler, UnityEngine.EventSystems.IPointerDownHandler, UnityEngine.EventSystems.IPointerUpHandler
{
    public System.Action<Vector2> Dragged;
    public System.Action<float> Scrolled;
    public System.Action<UnityEngine.EventSystems.PointerEventData> Clicked;

    public void OnDrag(PointerEventData eventData)
    {
        Dragged?.Invoke(eventData.delta);
    }

    public void OnScroll(UnityEngine.EventSystems.PointerEventData eventData)
    {
        Scrolled?.Invoke(eventData.scrollDelta.y);
    }
    
    public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) {}
    public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) {}
    public void OnPointerClick(UnityEngine.EventSystems.PointerEventData eventData)
    {
        Clicked?.Invoke(eventData);
    }
}

/// <summary>Tiny generated sprites for map markers, so no image files are needed.</summary>
public static class MapSprites
{
    private static Sprite circle;
    private static Sprite triangle;

    public static Sprite Circle
    {
        get
        {
            if (circle == null) circle = Make((u, v) => 0.5f - Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)));
            return circle;
        }
    }

    /// <summary>Points up. Rotate the marker to point it somewhere else.</summary>
    public static Sprite Triangle
    {
        get
        {
            if (triangle == null)
            {
                triangle = Make((u, v) =>
                {
                    // Apex at the top (v = 0.95), widening to the base at v = 0.05
                    float halfWidth = (0.95f - v) / 0.9f * 0.45f;
                    float side = halfWidth - Mathf.Abs(u - 0.5f);
                    float bottom = v - 0.05f;
                    return Mathf.Min(side, bottom);
                });
            }
            return triangle;
        }
    }

    // inside(u, v) is positive inside the shape (in 0 to 1 units); it's turned into a soft edge about one pixel wide
    private static Sprite Make(System.Func<float, float, float> inside)
    {
        const int n = 64;
        Texture2D tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Color[] pixels = new Color[n * n];
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float d = inside((x + 0.5f) / n, (y + 0.5f) / n);
                pixels[y * n + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(d * n));
            }
        }

        tex.SetPixels(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0f, 0f, n, n), new Vector2(0.5f, 0.5f), 100f);
    }
}

/// <summary>Layout and text helpers shared by the HUD, the journal and the map.</summary>
public static class WorldUI
{
    public static void Place(RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
    {
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = position;
        rt.sizeDelta = size;
    }

    public static void Center(RectTransform rt, Vector2 size)
    {
        Place(rt, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
    }

    /// <summary>N, NE, E, SE, S, SW, W or NW. North is +Z, the top of the map.</summary>
    public static string Compass(float yaw)
    {
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        int index = Mathf.RoundToInt(Mathf.Repeat(yaw, 360f) / 45f) % 8;
        return names[index];
    }

    /// <summary>"a" or "an", for "an apple" and "a baker".</summary>
    public static string Article(string word)
    {
        return !string.IsNullOrEmpty(word) && "aeiou".IndexOf(char.ToLower(word[0])) >= 0 ? "an" : "a";
    }

    /// <summary>A vertical scroll area. Add rows to content; it grows to fit them.</summary>
    public static ScrollRect BuildScroll(Transform parent, out RectTransform content)
    {
        RectTransform root = UIKit.CreateImage(parent, "Scroll", new Color(1f, 1f, 1f, 0.04f));
        root.GetComponent<Image>().raycastTarget = true; // so the mouse wheel works anywhere over it

        RectTransform viewport = UIKit.CreateRect(root, "Viewport");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;
        viewport.gameObject.AddComponent<RectMask2D>();

        content = UIKit.CreateRect(viewport, "Content");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;
        UIKit.VLayout(content.gameObject, 8f, 12, TextAnchor.UpperLeft);
        UIKit.FitVertical(content.gameObject);

        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 30f;
        return scroll;
    }
}























