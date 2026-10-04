using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public interface IInteractable
{
    string Prompt { get; }
    float HoldTime { get; }
    float Range { get; }
    bool CanInteract { get; }
    float DistanceTo(Vector3 point);
    void Interact();
}

public class Interactable : MonoBehaviour, IInteractable
{
    [SerializeField] private string prompt = "use";
    [SerializeField, Min(0.1f)] private float range = 2.5f;
    [SerializeField, Min(0f)] private float holdTime = 0f;
    [SerializeField] private Vector3 pointOffset = Vector3.zero;
    [SerializeField] private bool singleUse = false;

    [SerializeField] private UnityEvent onInteract = new UnityEvent();

    private bool used;

    public string Prompt { get => prompt; set => prompt = value; }
    public float HoldTime => holdTime;
    public float Range => range;
    public UnityEvent OnInteract => onInteract;

    public virtual bool CanInteract => !(singleUse && used);

    public virtual float DistanceTo(Vector3 point)
    {
        return Vector3.Distance(point, transform.TransformPoint(pointOffset));
    }

    public virtual void Interact()
    {
        used = true;
        onInteract.Invoke();
    }

    protected virtual void OnEnable()
    {
        InteractionManager.Register(this);
    }

    protected virtual void OnDisable()
    {
        InteractionManager.Unregister(this);
    }
}

public class InteractionManager : MonoBehaviour
{
    public static InteractionManager Instance { get; private set; }

    public static IInteractable Current => Instance != null ? Instance.current : null;

    private readonly List<IInteractable> interactables = new List<IInteractable>();

    private Transform player;
    private int blocks;
    private IInteractable current;
    private float holdProgress;
    private bool waitForRelease; 
    private string shownText;

    // Screen space UI (for holds)
    private GameObject promptRoot;
    private Text promptLabel;
    private RectTransform barBackground;
    private RectTransform barFill;

    // Spatial Throttling
    private float nextScanTime = 0f;
    private float scanInterval = 0.05f; // 20 Hz
    private Vector3 lastScanPos = Vector3.zero;

    // Billboard Pooling
    private class BillboardData
    {
        public GameObject root;
        public RectTransform rect;
        public Text actionText;
        public Text keyText;
        public RectTransform bgRect;
        public IInteractable target;
        public float heightOffset;
        public string currentActionText;
    }
    
    private List<BillboardData> billboardPool = new List<BillboardData>();
    private List<IInteractable> nearbyInteractables = new List<IInteractable>();
    private Dictionary<IInteractable, float> heightCache = new Dictionary<IInteractable, float>();

    public static InteractionManager Ensure()
    {
        if (Instance != null) return Instance;
        InteractionManager found = FindAnyObjectByType<InteractionManager>();
        if (found != null) return found; 
        return new GameObject("Interaction Manager").AddComponent<InteractionManager>();
    }

    public static void Register(IInteractable interactable)
    {
        if (interactable == null) return;
        InteractionManager m = Ensure();
        if (!m.interactables.Contains(interactable)) m.interactables.Add(interactable);
    }

    public static void Unregister(IInteractable interactable)
    {
        if (Instance != null) Instance.interactables.Remove(interactable);
    }

    public static void Block()
    {
        Ensure().blocks++;
    }

    public static void Unblock()
    {
        if (Instance != null && Instance.blocks > 0) Instance.blocks--;
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        BuildUI();
        
        for (int i = 0; i < 15; i++)
        {
            billboardPool.Add(CreateBillboardUI(i));
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Start()
    {
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
    }

    private void Update()
    {
        if (Instance != this) return;
        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null) return;
        }

        bool held = Keybinds.IsHeld(GameAction.Interact);
        if (!held) waitForRelease = false;

        if (blocks > 0 || Time.timeScale == 0f)
        {
            ClearCurrent();
            UpdateBillboards(); 
            if (held) waitForRelease = true; 
            return;
        }

        // --- Spatial Throttling ---
        // Scan if enough time has passed OR player moved more than 0.5 meters
        if (Time.time >= nextScanTime || Vector3.SqrMagnitude(player.position - lastScanPos) > 0.25f)
        {
            nextScanTime = Time.time + scanInterval;
            lastScanPos = player.position;
            ScanInteractables();
        }

        // Validate current
        if (current != null)
        {
            if (current == null || (current is UnityEngine.Object obj && obj == null) || (current is Inventory.Pickup p && p.go == null) || !current.CanInteract || current.DistanceTo(player.position) > current.Range)
            {
                current = null;
            }
        }

        if (current == null && nearbyInteractables.Count > 0)
        {
            float bestD = float.MaxValue;
            foreach (var it in nearbyInteractables)
            {
                Transform targetT = GetTransform(it);
                if (targetT == null) continue;
                float d = it.DistanceTo(player.position);
                if (d <= it.Range && d < bestD)
                {
                    bestD = d;
                    current = it;
                }
            }
        }

        UpdateBillboards();

        if (current == null)
        {
            ClearCurrent();
            return;
        }

        bool isHold = current.HoldTime > 0f;
        ShowPrompt(isHold);

        if (isHold)
        {
            if (held && !waitForRelease) holdProgress += Time.deltaTime / current.HoldTime;
            else holdProgress = Mathf.Max(0f, holdProgress - Time.deltaTime * 2f);

            if (barFill != null) barFill.anchorMax = new Vector2(Mathf.Clamp01(holdProgress), 1f);

            if (holdProgress >= 1f) Fire();
        }
        else
        {
            holdProgress = 0f;
            if (Keybinds.WasPressed(GameAction.Interact) || (held && !waitForRelease)) Fire();
            else if (held) waitForRelease = true;
        }
    }
    
    private Transform GetTransform(IInteractable it)
    {
        if (it is MonoBehaviour mb && mb != null) return mb.transform;
        if (it is Inventory.Pickup pk && pk != null && pk.go != null) return pk.go.transform;
        return null;
    }

        private void ScanInteractables()
    {
        nearbyInteractables.Clear();
        float bestDistance = float.MaxValue;
        IInteractable best = null;
        Vector3 pPos = player.position;

        for (int i = interactables.Count - 1; i >= 0; i--)
        {
            IInteractable it = interactables[i];
            
            if (it == null || (it is UnityEngine.Object obj && obj == null))
            {
                interactables.RemoveAt(i);
                continue;
            }
            
            Transform t = GetTransform(it);
            if (t == null)
            {
                interactables.RemoveAt(i);
                continue;
            }

            if (!it.CanInteract) continue;

            if ((t.position - pPos).sqrMagnitude > 25f) continue;

            float d = it.DistanceTo(pPos);
            if (d <= it.Range)
            {
                nearbyInteractables.Add(it);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = it;
                }
            }
        }
        
        current = best;
    }

    private void UpdateBillboards()
    {
        Camera cam = Camera.main;
        Transform camT = cam != null ? cam.transform : null;
        if (camT == null)
        {
            var cc = FindAnyObjectByType<CameraController>();
            if (cc != null) camT = cc.GetComponent<Camera>().transform;
        }

        int poolIndex = 0;
        
        if (blocks == 0 && Time.timeScale > 0f)
        {
            foreach (var it in nearbyInteractables)
            {
                if (poolIndex >= billboardPool.Count)
                {
                    billboardPool.Add(CreateBillboardUI(poolIndex));
                }

                BillboardData bb = billboardPool[poolIndex];
                
                Transform targetT = GetTransform(it);
                if (targetT == null) continue;

                                string actionTxt = "to " + it.Prompt;
                if (actionTxt != bb.currentActionText)
                {
                    bb.currentActionText = actionTxt;
                    bb.actionText.text = actionTxt;
                    float newW = actionTxt.Length * 12f + 90f; // Fast approximation instead of ForceUpdateCanvases
                    bb.bgRect.sizeDelta = new Vector2(newW, 68f);
                    bb.rect.sizeDelta = new Vector2(newW + 80f, 120f);
                }

                if (bb.target != it)
                {
                    bb.target = it;
                    float topY = targetT.position.y;
                    if (!heightCache.TryGetValue(it, out bb.heightOffset))
                    {
                        foreach (var r in targetT.GetComponentsInChildren<Renderer>())
                        {
                            if (r != null && r.bounds.max.y > topY) topY = r.bounds.max.y;
                        }
                        bb.heightOffset = (topY - targetT.position.y) + 0.45f;
                        if (bb.heightOffset < 0.6f) bb.heightOffset = 0.6f;
                        heightCache[it] = bb.heightOffset;
                    }
                }

                bb.rect.position = targetT.position + Vector3.up * bb.heightOffset;
                
                if (camT != null)
                {
                    Vector3 dir = bb.rect.position - camT.position;
                    dir.y = 0;
                    if (dir != Vector3.zero) bb.rect.rotation = Quaternion.LookRotation(dir, Vector3.up);
                }
                else if (player != null)
                {
                    Vector3 lookDir = bb.rect.position - player.position;
                    lookDir.y = 0;
                    if (lookDir != Vector3.zero) bb.rect.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
                }

                if (!bb.root.activeSelf) bb.root.SetActive(true);
                poolIndex++;
            }
        }

        for (int i = poolIndex; i < billboardPool.Count; i++)
        {
            if (billboardPool[i].root.activeSelf)
            {
                billboardPool[i].root.SetActive(false);
                billboardPool[i].target = null;
            }
        }
    }

    private void Fire()
    {
        IInteractable target = current;
        current = null;
        holdProgress = 0f;
        HidePrompt();
        target.Interact();
        if (target.HoldTime >= 0.5f) waitForRelease = true;
    }

    private void ClearCurrent()
    {
        current = null;
        holdProgress = 0f;
        HidePrompt();
    }

    private void ShowPrompt(bool isHold)
    {
        if (barBackground != null) barBackground.gameObject.SetActive(isHold);
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    private void HidePrompt()
    {
        if (promptRoot != null) promptRoot.SetActive(false);
    }

    private void BuildUI()
    {
        GameObject canvasGO = UIKit.CreateCanvas("Interaction UI", 5);

        RectTransform prompt = UIKit.CreateImage(canvasGO.transform, "Prompt", new Color(0f, 0f, 0f, 0.6f));
        prompt.anchorMin = prompt.anchorMax = new Vector2(0.5f, 0f);
        prompt.pivot = new Vector2(0.5f, 0f);
        prompt.anchoredPosition = new Vector2(0f, 120f);
        prompt.sizeDelta = new Vector2(720f, 80f);
        promptLabel = UIKit.CreateText(prompt, "Label", "", 32, Color.white);

        barBackground = UIKit.CreateImage(prompt, "BarBackground", new Color(1f, 1f, 1f, 0.25f));
        barBackground.anchorMin = new Vector2(0f, 0f);
        barBackground.anchorMax = new Vector2(1f, 0f);
        barBackground.pivot = new Vector2(0.5f, 0f);
        barBackground.anchoredPosition = new Vector2(0f, 8f);
        barBackground.sizeDelta = new Vector2(-20f, 8f);

        barFill = UIKit.CreateImage(barBackground, "BarFill", Color.white);
        barFill.anchorMin = new Vector2(0f, 0f);
        barFill.anchorMax = new Vector2(0f, 1f);
        barFill.offsetMin = Vector2.zero;
        barFill.offsetMax = Vector2.zero;

        promptRoot = prompt.gameObject;
        promptRoot.SetActive(false);
    }

        private static Canvas masterCanvas;

    private BillboardData CreateBillboardUI(int index)
    {
        if (masterCanvas == null)
        {
            GameObject mcObj = new GameObject("InteractionMasterBillboardCanvas", typeof(RectTransform), typeof(Canvas));
            DontDestroyOnLoad(mcObj);
            masterCanvas = mcObj.GetComponent<Canvas>();
            masterCanvas.renderMode = RenderMode.WorldSpace;
            mcObj.transform.position = Vector3.zero;
            mcObj.transform.rotation = Quaternion.identity;
            mcObj.transform.localScale = Vector3.one;
        }

        GameObject root = new GameObject($"WorldUI_Billboard_{index}", typeof(RectTransform));
        RectTransform canvasRect = root.GetComponent<RectTransform>();
        canvasRect.SetParent(masterCanvas.transform, false);
        
        canvasRect.localScale = Vector3.one * 0.012f;
        canvasRect.sizeDelta = new Vector2(420, 120);

        GameObject bgObj = new GameObject("Background", typeof(RectTransform), typeof(Image), typeof(Shadow));
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        bgRect.SetParent(canvasRect, false);
        var bgImg = bgObj.GetComponent<Image>();
        bgImg.color = new Color(0.14f, 0.18f, 0.24f, 0.85f); 
        
        var shadow = bgObj.GetComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.5f);
        shadow.effectDistance = new Vector2(4f, -4f);
        bgRect.sizeDelta = new Vector2(340, 68);
        bgRect.anchoredPosition = new Vector2(0f, 15f);

        GameObject arrowObj = new GameObject("Arrow", typeof(RectTransform), typeof(Text));
        RectTransform arrowRect = arrowObj.GetComponent<RectTransform>();
        arrowRect.SetParent(canvasRect, false);
        arrowRect.sizeDelta = new Vector2(40, 40);
        arrowRect.anchoredPosition = new Vector2(0, -28f);
        
        Text arrowText = arrowObj.GetComponent<Text>();
        arrowText.text = "v";
        arrowText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        arrowText.fontSize = 32;
        arrowText.color = new Color(0.14f, 0.18f, 0.24f, 0.85f);
        arrowText.alignment = TextAnchor.UpperCenter;

        GameObject badgeObj = new GameObject("Badge", typeof(RectTransform), typeof(Image));
        RectTransform badgeRect = badgeObj.GetComponent<RectTransform>();
        badgeRect.SetParent(bgRect, false);
        var badgeImg = badgeObj.GetComponent<Image>();
        badgeImg.color = new Color(0.82f, 0.86f, 0.92f, 0.95f); 
        badgeRect.anchorMin = new Vector2(0f, 0.5f);
        badgeRect.anchorMax = new Vector2(0f, 0.5f);
        badgeRect.pivot = new Vector2(0f, 0.5f);
        badgeRect.anchoredPosition = new Vector2(10f, 0f);
        badgeRect.sizeDelta = new Vector2(50f, 50f);

        Text keyLabel = MakeText(badgeRect, "Key", "E", 30, new Color(0.08f, 0.12f, 0.18f, 1f));
        keyLabel.fontStyle = FontStyle.Bold;

        GameObject textObj = new GameObject("ActionText", typeof(RectTransform));
        RectTransform textRect = textObj.GetComponent<RectTransform>();
        textRect.SetParent(bgRect, false);
        textRect.anchorMin = new Vector2(0f, 0.5f);
        textRect.anchorMax = new Vector2(1f, 0.5f);
        textRect.pivot = new Vector2(0f, 0.5f);
        textRect.anchoredPosition = new Vector2(70f, 0f);
        textRect.sizeDelta = new Vector2(-80f, 50f);
        
        Text actionLabel = MakeText(textRect, "Text", "to interact", 28, Color.white);
        actionLabel.alignment = TextAnchor.MiddleLeft;
        actionLabel.fontStyle = FontStyle.Bold;
        actionLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        
        root.SetActive(false);

        return new BillboardData
        {
            root = root,
            rect = canvasRect,
            actionText = actionLabel,
            keyText = keyLabel,
            bgRect = bgRect
        };
    }

    private static Text MakeText(RectTransform parent, string name, string txt, int size, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        
        var t = go.GetComponent<Text>();
        t.text = txt;
        t.fontSize = size;
        t.color = col;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return t;
    }
}














