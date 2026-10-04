using UnityEngine;
using System.Collections.Generic;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// First / third person camera. Put this on the Camera. No parenting needed.
/// It finds the player automatically and handles mouse look (yaw turns the player, pitch tilts the camera).
/// It starts in first person at head height; the Toggle View key (default Tab) swaps to third person, where the
/// camera orbits behind the player and pulls in when a wall is in the way.
/// Look input is ignored whenever the cursor is free (pause menu, conversations, inventory).
/// While paused or in a conversation this script is switched off, so the view can't be toggled then.
/// </summary>
public class CameraController : MonoBehaviour
{
    
    [Header("Target")]
    [Tooltip("Leave empty to auto-find the object with PlayerController.")]
    [SerializeField] private Transform player;
    [Tooltip("Camera position relative to the player's center (eye height).")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 0.6f, 0f);

    [Header("Look")]
    [SerializeField] private float sensitivity = 2f;
    [SerializeField] private float maxLookAngle = 85f;

    [Header("Third Person")]
    [Tooltip("How far behind the player the camera sits.")]
    [SerializeField] private float thirdPersonDistance = 4f;
    [Tooltip("What the camera orbits around in third person, relative to the player's center (around the head).")]
    [SerializeField] private Vector3 thirdPersonPivot = new Vector3(0.6f, 0.8f, 0f);
    [Tooltip("The camera pulls in when a wall or the ground is in the way. This is how big it counts as.")]
    [SerializeField] private float collisionRadius = 0.4f;
    [Tooltip("What the camera collides with. Untick layers (e.g. NPCs) that it should pass through.")]
    [SerializeField] private LayerMask collisionMask = ~0;
    [Tooltip("How quickly the camera glides between first and third person.")]
    [SerializeField] private float transitionSpeed = 10f;

    private float yaw;
    private float pitch;

    public bool IsInMenu { get; set; } = false;
    private float originalFOV = 60f;
    private float targetFOV = 60f;
    private bool skipLookFrame;
    private bool thirdPerson;
    private float currentDistance; // 0 = first person, thirdPersonDistance = fully third person
    private float smoothDistance;
    private float savedYaw;
    private float savedPitch;
    private bool wasInMenu;
    private float menuTransition;

    // Dithering: track which renderers we currently have _DitherOpacity set on so we can reset them.
    private readonly List<Renderer> ditheredRenderers = new List<Renderer>();
    private static readonly int DitherOpacityID = Shader.PropertyToID("_DitherOpacity");
    private MaterialPropertyBlock ditherBlock;
    private readonly Dictionary<Transform, Renderer[]> rendererCache = new Dictionary<Transform, Renderer[]>();
    private Dictionary<Collider, Transform> entityCache = new Dictionary<Collider, Transform>();
    private RaycastHit[] sphereCastHits = new RaycastHit[128];
    private Collider[] overlapHits = new Collider[128];

    private Transform GetEntityRoot(Collider col)
    {
        if (entityCache.TryGetValue(col, out Transform root)) return root;

        NPCIdentity npc = col.GetComponentInParent<NPCIdentity>();
        if (npc != null) { entityCache[col] = npc.transform; return npc.transform; }

        Animal animal = col.GetComponentInParent<Animal>();
        if (animal != null) { entityCache[col] = animal.transform; return animal.transform; }

        EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
        if (enemy != null) { entityCache[col] = enemy.transform; return enemy.transform; }

        entityCache[col] = null;
        return null;
    }

    private void Awake()
    {
        ditherBlock = new MaterialPropertyBlock();
        thirdPersonPivot = new Vector3(0.6f, 0.8f, 0f);
        if (player == null)
        {
            var pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
        }

        if (player == null)
        {
            Debug.LogError("CameraController: no PlayerController found in the scene. Add one to your capsule or assign Player.", this);
            enabled = false;
            return;
        }

        yaw = player.eulerAngles.y;
    }

    // The cursor re-locks around the time this is re-enabled (after pausing or a conversation),
    // and that frame can report a bogus mouse jump, so ignore look input for one frame.
    private void OnEnable()
    {
        skipLookFrame = true;
    }

    private void Start()
    {
        originalFOV = GetComponent<Camera>().fieldOfView;
        targetFOV = originalFOV;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // Remove Crosshair since we are permanently in 3rd person
        
        currentDistance = thirdPersonDistance;
        smoothDistance = thirdPersonDistance;
    }

    private void Update()
    {
        // Cursor is free (pause menu, conversation, inventory): no look input. Keep skipping until it
        // is locked again, because the first frame after a re-lock can report a bogus mouse jump.
                if (IsInMenu)
        {
            if (!wasInMenu)
            {
                savedYaw = yaw;
                savedPitch = pitch;
                wasInMenu = true;
            }
            menuTransition = Mathf.Lerp(menuTransition, 1f, 12f * Time.unscaledDeltaTime);
        }
        else
        {
            if (wasInMenu)
            {
                yaw = savedYaw;
                pitch = savedPitch;
                wasInMenu = false;
            }
            menuTransition = Mathf.Lerp(menuTransition, 0f, 15f * Time.unscaledDeltaTime);
        }

        if (Cursor.lockState != CursorLockMode.Locked)
        {
            skipLookFrame = true;
            return;
        }

        if (skipLookFrame)
        {
            skipLookFrame = false;
            return;
        }

        // Sensitivity slider from the settings menu is a multiplier on top of the Inspector value
        Vector2 look = ReadLookInput() * sensitivity * GameSettings.Sensitivity;
        float ySign = GameSettings.InvertY ? -1f : 1f;

        yaw += look.x;
        pitch = Mathf.Clamp(pitch - look.y * ySign, -maxLookAngle, maxLookAngle);
    }

    // LateUpdate so the camera follows after the player has moved this frame
    private void LateUpdate()
    {
        if (player == null)
        {
            PlayerController pc = FindAnyObjectByType<PlayerController>();
            if (pc != null) player = pc.transform;
            if (player == null) return;
        }
        
                // Permanently Third Person
        float targetDistance = Mathf.Lerp(GameSettings.CameraDistance, 1.5f, menuTransition);
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, 1f - Mathf.Exp(-transitionSpeed * Time.unscaledDeltaTime));
        if (Mathf.Abs(currentDistance - targetDistance) < 0.01f) currentDistance = targetDistance;

                // Over-the-shoulder look (Dead Space / Division style)
        // Zoom in and level the camera so it looks forward over the shoulder.
        float targetMenuYaw = player.eulerAngles.y;
        float currentYaw = Mathf.LerpAngle(yaw, targetMenuYaw, menuTransition);
        float currentPitch = Mathf.LerpAngle(pitch, 5f, menuTransition); // Slightly down pitch
        
        Camera cam = GetComponent<Camera>();
        
        // Respect GameSettings FOV
        float baseFov = GameSettings.Fov > 0f ? GameSettings.Fov : 60f;
        cam.fieldOfView = Mathf.Lerp(baseFov, 50f, menuTransition);
        
        // Auto-scale Far Clip Plane based on Chunk Render Distance to fix Line of Sight!
        // (RenderDistance is chunks in each direction, 60m per chunk)
        cam.farClipPlane = (GameSettings.RenderDistance * 60f) + 120f;
        
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
        
        // 1. Calculate the central pivot (head/shoulders)
        float currentPivotY = Mathf.Lerp(thirdPersonPivot.y, 1.4f, menuTransition); // Raise to shoulder height
        Vector3 centralPivot = player.position + Vector3.up * currentPivotY;
        
        // 2. The intended camera position is distance units back, plus X units right
        Vector3 back = rotation * Vector3.back;
        Vector3 right = rotation * Vector3.right;
        Vector3 idealCamPos = centralPivot + right * thirdPersonPivot.x + back * currentDistance;
        
        // 3. To find collisions, we raycast from the central pivot diagonally to the ideal camera position
        Vector3 castDir = (idealCamPos - centralPivot).normalized;
        float castDist = Vector3.Distance(centralPivot, idealCamPos);
        
        Vector3 position = centralPivot;
        if (castDist > 0.001f)
        {
                        int hitCount = Physics.SphereCastNonAlloc(centralPivot, collisionRadius, castDir, sphereCastHits, castDist, collisionMask, QueryTriggerInteraction.Ignore);
            
            float closestDist = castDist;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit h = sphereCastHits[i];
                if (h.collider.transform.root == player.root) continue;
                
                // If it's an NPC, Animal, Enemy, or Resource (Tree/Rock), don't zoom the camera in! Let the camera pass through and dither.
                if (GetEntityRoot(h.collider) != null) continue;
                
                if (h.distance < closestDist) closestDist = h.distance;
            }

            // THIS IS THE LINE I ACCIDENTALLY DELETED! It applies the collision distance.
            float targetCamDist = Mathf.Max(0f, closestDist);

            // Fast zoom in to avoid clipping, smooth slow zoom out so it doesnt jitter over small bumps
            if (targetCamDist < smoothDistance) 
                smoothDistance = Mathf.Lerp(smoothDistance, targetCamDist, 25f * Time.unscaledDeltaTime); 
            else 
                smoothDistance = Mathf.Lerp(smoothDistance, targetCamDist, 3f * Time.unscaledDeltaTime);

            position = centralPivot + castDir * smoothDistance;
        }

        // DITHER — restore last frame's renderers first, then find new ones
        foreach (var r in ditheredRenderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(ditherBlock);
            ditherBlock.SetFloat(DitherOpacityID, 1f);
            r.SetPropertyBlock(ditherBlock);
        }
        ditheredRenderers.Clear();

        // Overlap capsule from pivot (player head) to camera to find NPCs/Animals in the way
                int overlapCount = Physics.OverlapCapsuleNonAlloc(centralPivot, position, collisionRadius * 1.5f, overlapHits, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < overlapCount; i++)
        {
            Collider col = overlapHits[i];
            if (col.transform.root == player.root) continue;

            Transform root = GetEntityRoot(col);
            if (root == null) continue;

            // Opacity based on how close the CAMERA is to this collider.
            // When the camera is > 1.5 m away: fully opaque.
            // When the camera is inside / < 0.3 m: fully invisible.
            float closestToCam = Vector3.Distance(col.ClosestPoint(position), position);
            float opacity = Mathf.Clamp01(Mathf.InverseLerp(0.3f, 1.5f, closestToCam));

            if (!rendererCache.TryGetValue(root, out Renderer[] cachedRenderers))
            {
                cachedRenderers = root.GetComponentsInChildren<Renderer>();
                rendererCache[root] = cachedRenderers;
            }

            foreach (var r in cachedRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(ditherBlock);
                ditherBlock.SetFloat(DitherOpacityID, opacity);
                r.SetPropertyBlock(ditherBlock);
                ditheredRenderers.Add(r);
            }
        }

        transform.SetPositionAndRotation(position, rotation);
    }

    private Vector2 ReadLookInput()
    {
#if ENABLE_INPUT_SYSTEM
        if (Mouse.current == null) return Vector2.zero;
        // Raw mouse delta is in pixels, so scale it down to feel like the old Input Manager
        return Mouse.current.delta.ReadValue() * 0.05f;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }
}



















