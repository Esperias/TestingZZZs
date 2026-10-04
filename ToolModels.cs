using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Procedural 3D models (held in hand) and matching 2D icons for the Axe and Pickaxe.
/// Everything is built in code, so no art assets are needed; the held model and the inventory icon share
/// the same colors/proportions so they read as the same item.
/// </summary>
public static class ToolModels
{
    // ---------------------------------------------------------------- palette
    private static readonly Color Wood      = new Color(0.46f, 0.29f, 0.13f);
    private static readonly Color WoodLight = new Color(0.62f, 0.42f, 0.2f);
    private static readonly Color Wrap      = new Color(0.30f, 0.18f, 0.1f);
    private static readonly Color Lashing   = new Color(0.78f, 0.66f, 0.4f);
    private static readonly Color Stone     = new Color(0.52f, 0.55f, 0.6f);
    private static readonly Color StoneDark = new Color(0.36f, 0.38f, 0.43f);
    private static readonly Color Edge      = new Color(0.86f, 0.89f, 0.93f);

    private static readonly Dictionary<Color, Material> matCache = new Dictionary<Color, Material>();
	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
	private static void ResetStaticsForFastPlayMode() { matCache.Clear(); }


    private static Material GetMat(Color c)
    {
        if (matCache.TryGetValue(c, out Material m) && m != null) return m;
        if (Inventory.Instance != null && Inventory.Instance.baseToonMaterial != null)
		{
			m = new Material(Inventory.Instance.baseToonMaterial);
		}
		else
		{
			Shader s = Shader.Find("Custom/WalkingToon");
			if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
			if (s == null) s = Shader.Find("Standard");
			m = new Material(s);
		}
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        matCache[c] = m;
        return m;
    }

    private static GameObject Part(Transform parent, PrimitiveType shape, Vector3 pos, Vector3 scale, Vector3 euler, Color color, string name)
    {
        GameObject go = GameObject.CreatePrimitive(shape);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localEulerAngles = euler;
        go.transform.localScale = scale;
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = GetMat(color);
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go;
    }

    // ---------------------------------------------------------------- 3D models
    // Local space: the handle runs along +Y, centred on the origin (about -0.6 .. +0.6), the head sits at the top.

    /// <summary>Builds the held model for a tool as children of <paramref name="root"/>.</summary>
    public static void BuildHeld(Transform root, ToolManager.ToolType type)
    {
        // Shared shaft: slightly tapered wood handle, darker grip wrap at the bottom, a pommel knob.
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.05f, 0f), new Vector3(0.07f, 0.55f, 0.07f), Vector3.zero, Wood, "Shaft");
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, -0.38f, 0f), new Vector3(0.088f, 0.2f, 0.088f), Vector3.zero, Wrap, "Grip");
        Part(root, PrimitiveType.Sphere, new Vector3(0f, -0.61f, 0f), new Vector3(0.115f, 0.115f, 0.115f), Vector3.zero, Wrap, "Pommel");
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, 0.22f, 0f), new Vector3(0.062f, 0.3f, 0.062f), Vector3.zero, WoodLight, "ShaftHighlight")
            .transform.localPosition = new Vector3(0.012f, 0.22f, 0.012f);

        if (type == ToolManager.ToolType.Axe) BuildAxeHead(root);
        else if (type == ToolManager.ToolType.Pickaxe) BuildPickaxeHead(root);
        else if (type == ToolManager.ToolType.FishingRod) 
        {
            BuildFishingRod(root);
            // Make the entire rod thinner and noticeably shorter
            root.localScale = new Vector3(0.5f, 0.55f, 0.5f);
        }
    }

    private static void BuildFishingRod(Transform root)
    {
        float y = 0.6f;
        // Extend the shaft to make it longer and thinner
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, y + 0.4f, 0f), new Vector3(0.04f, 0.5f, 0.04f), Vector3.zero, Wood, "ShaftExt1");
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, y + 1.3f, 0f), new Vector3(0.02f, 0.4f, 0.02f), Vector3.zero, Wood, "ShaftExt2");
        
        // Add a reel
        Part(root, PrimitiveType.Cylinder, new Vector3(0.08f, -0.2f, 0f), new Vector3(0.12f, 0.06f, 0.12f), new Vector3(0f, 0f, 90f), StoneDark, "ReelBase");
        Part(root, PrimitiveType.Cylinder, new Vector3(0.12f, -0.2f, 0f), new Vector3(0.08f, 0.05f, 0.08f), new Vector3(0f, 0f, 90f), Stone, "ReelSpool");
        Part(root, PrimitiveType.Cube, new Vector3(0.16f, -0.2f, 0.06f), new Vector3(0.02f, 0.02f, 0.12f), Vector3.zero, StoneDark, "ReelHandle");
        Part(root, PrimitiveType.Cylinder, new Vector3(0.18f, -0.2f, 0.12f), new Vector3(0.03f, 0.03f, 0.03f), new Vector3(90f, 0f, 0f), WoodLight, "ReelKnob");
        
        // Add some line guides
        for (int i = 0; i < 4; i++)
        {
            float guideY = 0.2f + i * 0.45f;
            float size = 0.06f - i * 0.01f;
            Part(root, PrimitiveType.Cylinder, new Vector3(0.05f, guideY, 0f), new Vector3(size, 0.01f, size), new Vector3(0f, 0f, 90f), Stone, "Guide");
        }
    }

    private static void BuildAxeHead(Transform root)
    {
        float y = 0.5f;
        // Eye block that the handle goes through
        Part(root, PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(0.13f, 0.17f, 0.1f), Vector3.zero, StoneDark, "Eye");
        // Poll (back of the head)
        Part(root, PrimitiveType.Cube, new Vector3(-0.09f, y, 0f), new Vector3(0.1f, 0.12f, 0.08f), Vector3.zero, StoneDark, "Poll");
        // Blade: a flared body plus a thin bright cutting edge
        Part(root, PrimitiveType.Cube, new Vector3(0.14f, y, 0f), new Vector3(0.2f, 0.26f, 0.05f), new Vector3(0f, 0f, 0f), Stone, "BladeBody");
        Part(root, PrimitiveType.Cube, new Vector3(0.2f, y + 0.07f, 0f), new Vector3(0.1f, 0.14f, 0.045f), new Vector3(0f, 0f, -14f), Stone, "BladeTop");
        Part(root, PrimitiveType.Cube, new Vector3(0.2f, y - 0.07f, 0f), new Vector3(0.1f, 0.14f, 0.045f), new Vector3(0f, 0f, 14f), Stone, "BladeBottom");
        Part(root, PrimitiveType.Cube, new Vector3(0.265f, y, 0f), new Vector3(0.03f, 0.36f, 0.04f), Vector3.zero, Edge, "CuttingEdge");
        // Lashing where head meets shaft
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, y - 0.11f, 0f), new Vector3(0.095f, 0.025f, 0.095f), Vector3.zero, Lashing, "Lashing");
    }

    private static void BuildPickaxeHead(Transform root)
    {
        float y = 0.5f;
        // Eye block
        Part(root, PrimitiveType.Cube, new Vector3(0f, y, 0f), new Vector3(0.13f, 0.14f, 0.1f), Vector3.zero, StoneDark, "Eye");
        // Two curved arms built from angled segments that droop and taper toward bright tips
        for (int s = -1; s <= 1; s += 2)
        {
            Part(root, PrimitiveType.Cube, new Vector3(s * 0.15f, y + 0.015f, 0f), new Vector3(0.2f, 0.075f, 0.065f), new Vector3(0f, 0f, -s * 9f), Stone, "ArmInner");
            Part(root, PrimitiveType.Cube, new Vector3(s * 0.31f, y - 0.015f, 0f), new Vector3(0.18f, 0.06f, 0.055f), new Vector3(0f, 0f, -s * 24f), Stone, "ArmMid");
            Part(root, PrimitiveType.Cube, new Vector3(s * 0.44f, y - 0.075f, 0f), new Vector3(0.14f, 0.04f, 0.04f), new Vector3(0f, 0f, -s * 42f), Edge, "ArmTip");
        }
        Part(root, PrimitiveType.Cylinder, new Vector3(0f, y - 0.1f, 0f), new Vector3(0.095f, 0.025f, 0.095f), Vector3.zero, Lashing, "Lashing");
    }

    // ---------------------------------------------------------------- icons
    private static readonly Dictionary<ToolManager.ToolType, Sprite> iconCache = new Dictionary<ToolManager.ToolType, Sprite>();
    private const int Size = 96;

    public static Sprite GetIcon(ToolManager.ToolType type)
    {
        if (iconCache.TryGetValue(type, out Sprite cached) && cached != null) return cached;

        Color[] px = new Color[Size * Size]; // fully transparent

        // Drawing frame: origin O is where the head meets the handle. U runs across the head, V along the handle.
        Vector2 O = new Vector2(0.68f, 0.70f);
        Vector2 U = new Vector2(-0.73f, 0.68f);
        Vector2 V = new Vector2(0.68f, 0.73f);
        System.Func<float, float, Vector2> P = (u, v) => O + u * U + v * V;

        // Handle (with highlight and grip wrap)
        Vector2 hStart = new Vector2(0.14f, 0.10f);
        Vector2 hEnd = P(0f, 0.04f);
        DrawSegment(px, hStart, hEnd, 0.036f, 0.036f, Wood);
        DrawSegment(px, hStart + new Vector2(0.012f, 0.012f) * 0.5f, hEnd, 0.014f, 0.014f, WoodLight);
        DrawSegment(px, hStart, Vector2.Lerp(hStart, hEnd, 0.28f), 0.044f, 0.044f, Wrap);

        if (type == ToolManager.ToolType.Axe)
        {
            // Poll
            DrawPoly(px, new[] { P(0.00f, -0.08f), P(0.13f, -0.07f), P(0.13f, 0.07f), P(0.00f, 0.08f) }, StoneDark);
            // Blade
            DrawPoly(px, new[] { P(0.02f, -0.10f), P(-0.28f, -0.20f), P(-0.37f, 0.00f), P(-0.28f, 0.20f), P(0.02f, 0.10f) }, Stone);
            // Shadow band near the eye
            DrawPoly(px, new[] { P(0.02f, -0.10f), P(-0.06f, -0.12f), P(-0.06f, 0.12f), P(0.02f, 0.10f) }, StoneDark);
            // Bright cutting edge
            DrawSegment(px, P(-0.28f, -0.20f), P(-0.37f, 0.00f), 0.016f, 0.016f, Edge);
            DrawSegment(px, P(-0.37f, 0.00f), P(-0.28f, 0.20f), 0.016f, 0.016f, Edge);
            // Lashing
            DrawSegment(px, P(0.0f, -0.14f), P(0.0f, -0.10f), 0.04f, 0.04f, Lashing);
        }
        else if (type == ToolManager.ToolType.Pickaxe)
        {
            // Curved pick head: thick in the middle, tapering to bright tips
            const int steps = 14;
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < steps; i++)
                {
                    float t0 = i / (float)steps, t1 = (i + 1) / (float)steps;
                    float u0 = s * 0.40f * t0, u1 = s * 0.40f * t1;
                    float v0 = -0.16f * t0 * t0, v1 = -0.16f * t1 * t1;
                    float r0 = Mathf.Lerp(0.04f, 0.010f, t0), r1 = Mathf.Lerp(0.04f, 0.010f, t1);
                    Color c = t1 > 0.72f ? Edge : Stone;
                    DrawSegment(px, P(u0, v0), P(u1, v1), r0, r1, c);
                }
            }
            DrawPoly(px, new[] { P(-0.07f, -0.07f), P(0.07f, -0.07f), P(0.07f, 0.08f), P(-0.07f, 0.08f) }, StoneDark);
            DrawSegment(px, P(0f, -0.11f), P(0f, -0.07f), 0.04f, 0.04f, Lashing);
        }
        else if (type == ToolManager.ToolType.FishingRod)
        {
            // Longer rod
            DrawSegment(px, P(-0.3f, -0.3f), P(-0.7f, -0.7f), 0.02f, 0.01f, Wood);
            // Reel
            DrawPoly(px, new[] { P(0.1f, -0.05f), P(0.2f, -0.05f), P(0.2f, 0.05f), P(0.1f, 0.05f) }, StoneDark);
            DrawPoly(px, new[] { P(0.12f, -0.04f), P(0.18f, -0.04f), P(0.18f, 0.04f), P(0.12f, 0.04f) }, Stone);
            // String dropping down
            DrawSegment(px, P(-0.7f, -0.7f), P(-0.7f, 0.5f), 0.005f, 0.005f, Edge);
            // Bobber
            DrawPoly(px, new[] { P(-0.75f, 0.5f), P(-0.65f, 0.5f), P(-0.65f, 0.6f), P(-0.75f, 0.6f) }, new Color(0.8f, 0.2f, 0.2f));
            DrawPoly(px, new[] { P(-0.75f, 0.6f), P(-0.65f, 0.6f), P(-0.65f, 0.7f), P(-0.75f, 0.7f) }, Color.white);
        }

        AddOutline(px, new Color(0.09f, 0.07f, 0.05f, 1f));

        Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        tex.name = "ToolIcon_" + type;
        tex.filterMode = FilterMode.Bilinear;
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.SetPixels(px);
        tex.Apply(false, true);

        Sprite sprite = Sprite.Create(tex, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = tex.name;
        iconCache[type] = sprite;
        return sprite;
    }

    // ---------------------------------------------------------------- tiny rasteriser
    private static void Blend(Color[] px, int i, Color c, float coverage)
    {
        if (coverage <= 0f) return;
        float a = coverage * c.a;
        Color dst = px[i];
        float outA = a + dst.a * (1f - a);
        if (outA <= 0f) return;
        Vector3 rgb = (new Vector3(c.r, c.g, c.b) * a + new Vector3(dst.r, dst.g, dst.b) * dst.a * (1f - a)) / outA;
        px[i] = new Color(rgb.x, rgb.y, rgb.z, outA);
    }

    private static readonly Vector2[] Sub = { new Vector2(0.25f, 0.25f), new Vector2(0.75f, 0.25f), new Vector2(0.25f, 0.75f), new Vector2(0.75f, 0.75f) };

    /// <summary>Thick line with radius interpolated from r0 to r1 (UV 0..1 space), with round-ish ends.</summary>
    private static void DrawSegment(Color[] px, Vector2 a, Vector2 b, float r0, float r1, Color color)
    {
        Vector2 ab = b - a;
        float len2 = Mathf.Max(ab.sqrMagnitude, 1e-8f);
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int hits = 0;
                for (int s = 0; s < 4; s++)
                {
                    Vector2 p = new Vector2((x + Sub[s].x) / Size, (y + Sub[s].y) / Size);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float r = Mathf.Lerp(r0, r1, t);
                    if ((p - (a + ab * t)).sqrMagnitude <= r * r) hits++;
                }
                if (hits > 0) Blend(px, y * Size + x, color, hits / 4f);
            }
    }

    private static void DrawPoly(Color[] px, Vector2[] poly, Color color)
    {
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                int hits = 0;
                for (int s = 0; s < 4; s++)
                    if (Inside(poly, new Vector2((x + Sub[s].x) / Size, (y + Sub[s].y) / Size))) hits++;
                if (hits > 0) Blend(px, y * Size + x, color, hits / 4f);
            }
    }

    private static bool Inside(Vector2[] poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
        {
            if (((poly[i].y > p.y) != (poly[j].y > p.y)) &&
                (p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x))
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Dark 2px outline around everything drawn, which keeps icons readable on any slot color.</summary>
    private static void AddOutline(Color[] px, Color outline)
    {
        Color[] src = (Color[])px.Clone();
        const int r = 2;
        for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                if (src[y * Size + x].a > 0.5f) continue;
                bool near = false;
                for (int dy = -r; dy <= r && !near; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx < 0 || ny < 0 || nx >= Size || ny >= Size) continue;
                        if (dx * dx + dy * dy > r * r + 1) continue;
                        if (src[ny * Size + nx].a > 0.5f) { near = true; break; }
                    }
                if (near) px[y * Size + x] = Color.Lerp(px[y * Size + x], outline, 1f - px[y * Size + x].a * 0.5f) ;
                if (near) px[y * Size + x] = new Color(outline.r, outline.g, outline.b, Mathf.Max(px[y * Size + x].a, 0.95f));
            }
    }
}





