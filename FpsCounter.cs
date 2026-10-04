using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

/// <summary>
/// Top-left performance overlay. Each line is toggled separately in Pause menu > Display:
/// FPS, frame time, 1%/0.1% lows, frame pacing, hitch counter, memory/GC, input latency.
/// </summary>
public class FpsCounter : MonoBehaviour
{
    private static FpsCounter instance;

    private const int SampleCount = 1000;      // rolling window of frames for lows / pacing
    private const float HitchMinMs = 40f;      // a frame is a hitch if it exceeds this ...
    private const float HitchMedianMult = 2.5f; // ... and this multiple of the median frame time

    private Text label;
    private readonly StringBuilder sb = new StringBuilder(256);

    // Rolling frame time history (seconds)
    private readonly float[] samples = new float[SampleCount];
    private int sampleIndex;
    private int sampleFilled;
    private readonly float[] sortBuf = new float[SampleCount];

    // Short-window FPS average
    private float accum;
    private int frames;
    private float timer;

    // Cached results from the last refresh
    private float avgMs, p1LowFps, p01LowFps, jitterMs, medianMs, maxMs;
    private int hitchesLast60;
    private readonly System.Collections.Generic.Queue<float> hitchTimes = new System.Collections.Generic.Queue<float>();
    private int totalHitches;
    private float lastHitchMs;

    // Memory / GC
    private int lastGcCount;
    private float gcPerMin;
    private readonly System.Collections.Generic.Queue<float> gcTimes = new System.Collections.Generic.Queue<float>();

    // Input latency (input event timestamp -> end of the frame that handled it)
    private double pendingInputTime = -1;
    private float latencyMs, latencyAvgMs;
    private int latencyCount;
    private static readonly WaitForEndOfFrame EndOfFrame = new WaitForEndOfFrame();

    public static void Ensure()
    {
        if (instance != null) return;
        GameObject go = new GameObject("FpsCounter");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<FpsCounter>();
    }

    private void Awake()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 5000;

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(transform, false);
        label = textObj.AddComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 20;
        label.fontStyle = FontStyle.Bold;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        textObj.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);

        RectTransform rt = label.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(12f, -10f);
        rt.sizeDelta = new Vector2(500f, 300f);

        lastGcCount = System.GC.CollectionCount(0);
    }

    private void OnEnable()
    {
        InputSystem.onEvent += OnInputEvent;
        StartCoroutine(EndOfFrameLoop());
    }

    private void OnDisable()
    {
        InputSystem.onEvent -= OnInputEvent;
    }

    private void OnInputEvent(InputEventPtr ptr, InputDevice device)
    {
        if (!GameSettings.ShowLatency) return;
        if (pendingInputTime >= 0) return; // already waiting on a frame
        if (!ptr.IsA<StateEvent>() && !ptr.IsA<DeltaStateEvent>()) return;
        // Only count real presses, not constant mouse-move noise
        if (device is Keyboard kb) { if (!kb.anyKey.isPressed) return; }
        else if (device is Mouse m) { if (!(m.leftButton.isPressed || m.rightButton.isPressed)) return; }
        else return;
        pendingInputTime = ptr.time;
    }

    private IEnumerator EndOfFrameLoop()
    {
        while (true)
        {
            yield return EndOfFrame;
            if (pendingInputTime >= 0)
            {
                float ms = (float)((Time.realtimeSinceStartupAsDouble - pendingInputTime) * 1000.0);
                pendingInputTime = -1;
                if (ms >= 0f && ms < 1000f)
                {
                    latencyMs = ms;
                    latencyAvgMs = latencyCount == 0 ? ms : Mathf.Lerp(latencyAvgMs, ms, 0.1f);
                    latencyCount++;
                }
            }
        }
    }

    private void Update()
    {
        bool anyOn = GameSettings.ShowFps || GameSettings.ShowFrameTime || GameSettings.ShowLows ||
                     GameSettings.ShowPacing || GameSettings.ShowHitches || GameSettings.ShowMemory ||
                     GameSettings.ShowLatency;
        if (label.enabled != anyOn) label.enabled = anyOn;
        if (!anyOn) return;

        float dt = Time.unscaledDeltaTime;

        samples[sampleIndex] = dt;
        sampleIndex = (sampleIndex + 1) % SampleCount;
        if (sampleFilled < SampleCount) sampleFilled++;

        accum += dt;
        frames++;
        timer += dt;

        // Hitch detection compares against the previous median so one hitch can't hide itself
        float dtMs = dt * 1000f;
        if (medianMs > 0f && dtMs > HitchMinMs && dtMs > medianMs * HitchMedianMult)
        {
            totalHitches++;
            lastHitchMs = dtMs;
            hitchTimes.Enqueue(Time.unscaledTime);
        }
        while (hitchTimes.Count > 0 && Time.unscaledTime - hitchTimes.Peek() > 60f) hitchTimes.Dequeue();

        // GC collections in the last minute
        int gc = System.GC.CollectionCount(0);
        for (int i = lastGcCount; i < gc; i++) gcTimes.Enqueue(Time.unscaledTime);
        lastGcCount = gc;
        while (gcTimes.Count > 0 && Time.unscaledTime - gcTimes.Peek() > 60f) gcTimes.Dequeue();

        if (timer < 0.25f) return;

        avgMs = accum / frames * 1000f;
        float avgFps = 1000f / Mathf.Max(avgMs, 0.0001f);
        accum = 0f; frames = 0; timer = 0f;

        RefreshStats();
        hitchesLast60 = hitchTimes.Count;
        gcPerMin = gcTimes.Count;

        sb.Length = 0;
        if (GameSettings.ShowFps) sb.Append(Mathf.RoundToInt(avgFps)).Append(" FPS");
        if (GameSettings.ShowFrameTime)
        {
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(avgMs.ToString("0.0")).Append(" ms  (worst ").Append(maxMs.ToString("0.0")).Append(")");
        }
        if (sb.Length > 0) sb.Append('\n');

        if (GameSettings.ShowLows)
            sb.Append("1% low ").Append(Mathf.RoundToInt(p1LowFps)).Append("   0.1% low ").Append(Mathf.RoundToInt(p01LowFps)).Append('\n');

        if (GameSettings.ShowPacing)
        {
            string quality = jitterMs < 1f ? "smooth" : (jitterMs < 3f ? "ok" : (jitterMs < 6f ? "uneven" : "jittery"));
            sb.Append("Pacing  +/-").Append(jitterMs.ToString("0.0")).Append(" ms  (").Append(quality).Append(")\n");
        }

        if (GameSettings.ShowHitches)
        {
            sb.Append("Hitches  ").Append(hitchesLast60).Append("/min  total ").Append(totalHitches);
            if (totalHitches > 0) sb.Append("  last ").Append(lastHitchMs.ToString("0")).Append(" ms");
            sb.Append('\n');
        }

        if (GameSettings.ShowMemory)
        {
            float managedMb = System.GC.GetTotalMemory(false) / 1048576f;
            float totalMb = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / 1048576f;
            sb.Append("Mem  ").Append(totalMb.ToString("0")).Append(" MB (managed ").Append(managedMb.ToString("0"))
              .Append(")  GC ").Append(gcPerMin.ToString("0")).Append("/min\n");
        }

        if (GameSettings.ShowLatency)
        {
            if (latencyCount == 0) sb.Append("Input latency  (press a key)\n");
            else sb.Append("Input latency  ").Append(latencyMs.ToString("0.0")).Append(" ms  avg ").Append(latencyAvgMs.ToString("0.0")).Append(" ms\n");
        }

        if (sb.Length > 0 && sb[sb.Length - 1] == '\n') sb.Length--;
        label.text = sb.ToString();
    }

    /// <summary>Computes median, max, jitter and 1% / 0.1% lows over the rolling window.</summary>
    private void RefreshStats()
    {
        int n = sampleFilled;
        if (n < 10) return;

        System.Array.Copy(samples, sortBuf, n);
        System.Array.Sort(sortBuf, 0, n); // ascending: slowest frames are at the end

        medianMs = sortBuf[n / 2] * 1000f;
        maxMs = sortBuf[n - 1] * 1000f;

        // Lows = average of the slowest 1% / 0.1% of frames, expressed as FPS
        p1LowFps = LowFps(n, 0.01f);
        p01LowFps = LowFps(n, 0.001f);

        // Frame pacing = standard deviation of frame time
        double mean = 0;
        for (int i = 0; i < n; i++) mean += samples[i];
        mean /= n;
        double var = 0;
        for (int i = 0; i < n; i++) { double d = samples[i] - mean; var += d * d; }
        jitterMs = (float)(System.Math.Sqrt(var / n) * 1000.0);
    }

    private float LowFps(int n, float fraction)
    {
        int count = Mathf.Max(1, Mathf.CeilToInt(n * fraction));
        double sum = 0;
        for (int i = n - count; i < n; i++) sum += sortBuf[i];
        double avg = sum / count;
        return avg > 0 ? (float)(1.0 / avg) : 0f;
    }
}




