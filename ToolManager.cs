using System.Collections.Generic;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class ToolManager : MonoBehaviour
{
    public static ToolManager Instance { get; private set; }

    public enum ToolType { None, Axe, Pickaxe, FishingRod }
    public ToolType CurrentTool { get; private set; } = ToolType.None;
    private GameObject toolVisual;
    public GameObject ToolVisual => toolVisual;

    // The tool follows the player's right hand bone (Koikatsu rig). Orientation is computed in world space from the
    // player's facing so it doesn't depend on the bone's local axes.
    private const string HandBoneName = "cf_j_hand_R";
    private const float GripLocalY = -0.3f;      // point on the tool's handle that sits in the hand
    private const float HeldScale = 1f;            // overall size of the held tool in world units
    private const float RestTilt = 30f;           // handle leans forward this many degrees at rest
    private const float SwingTilt = 85f;          // extra forward chop during a swing
    private Transform handBone;
    private Transform playerT;
    private float swingTimer;

    private void Awake()
    {
        Instance = this;
    }

    private float currentSwingDuration = 1.0f;

    // Melee arc settings
    private const float MeleeRadius = 2.5f;  // how far the swing reaches (slightly increased)
    private const float MeleeAngle  = 90f;  // full cone width (player must be within +-60 degrees of forward)

    public void TriggerSwing(float duration)
    {
        if (swingTimer > 0.1f) return;
        swingTimer = duration;
        currentSwingDuration = duration;

        PlayerController player = FindAnyObjectByType<PlayerController>();
        if (player == null) return;

        Transform playerT = player.transform;
        Vector3 origin  = playerT.position + Vector3.up * 1f; // chest height
        Vector3 forward = new Vector3(playerT.forward.x, 0f, playerT.forward.z).normalized;

        // Use Physics.OverlapSphere for EVERYTHING so we hit actual physical hitboxes,
        // rather than mathematical root coordinates which fail on large enemies.
        Collider[] nearby = Physics.OverlapSphere(origin, MeleeRadius);
        
        // Sort colliders by distance so we hit the closest thing first
        System.Array.Sort(nearby, (a, b) => 
        {
            Vector3 ptA = (a is MeshCollider mcA && !mcA.convex) ? a.ClosestPointOnBounds(origin) : a.ClosestPoint(origin);
            Vector3 ptB = (b is MeshCollider mcB && !mcB.convex) ? b.ClosestPointOnBounds(origin) : b.ClosestPoint(origin);
            float dA = Vector3.SqrMagnitude(ptA - origin);
            float dB = Vector3.SqrMagnitude(ptB - origin);
            return dA.CompareTo(dB);
        });

        foreach (var col in nearby)
        {
            // Ignore the player's own colliders
            if (col.transform.IsChildOf(playerT)) continue;

            // Get closest point on the hitbox (safely for non-convex meshes)
            Vector3 hitPoint = (col is MeshCollider mc && !mc.convex) ? col.ClosestPointOnBounds(origin) : col.ClosestPoint(origin);
            Vector3 toHit = hitPoint - origin;
            toHit.y = 0f;
            
            // If the closest point is exactly at origin (e.g. inside the collider), angle check isn't needed
            float dist = toHit.magnitude;
            if (dist > 0.001f && Vector3.Angle(forward, toHit / dist) > MeleeAngle * 0.5f) continue;

            // 1. Check Enemy
            EnemyAI enemy = col.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                float damage = CurrentTool == ToolType.Axe ? 25f : 15f;
                enemy.TakeDamage((int)damage, playerT);
                return; // Stop after hitting one target
            }

            // 2. Check Animal
            Animal animal = col.GetComponentInParent<Animal>();
            if (animal != null)
            {
                float damage = CurrentTool == ToolType.Axe ? 25f : (CurrentTool == ToolType.Pickaxe ? 15f : 5f);
                animal.TakeDamage(damage, playerT);
                return; // Stop after hitting one target
            }

            // 3. Check Resource
            ResourceNode node = col.GetComponentInParent<ResourceNode>();
            if (node != null) 
            { 
                node.Hit(); 
                return; // Stop after hitting one target
            }
        }
    }

    private void Update()
    {
        if (swingTimer > 0f)
        {
            swingTimer -= Time.deltaTime;
        }

    }

    private void LateUpdate()
    {
        if (toolVisual == null) return;
        if (playerT == null || handBone == null) { Destroy(toolVisual); toolVisual = null; CurrentTool = ToolType.None; return; }

        float swing = 0f;
        if (swingTimer > 0f)
        {
            float t = 1f - (swingTimer / currentSwingDuration);
            swing = Mathf.Sin(t * Mathf.PI) * SwingTilt;
        }

        // Yaw -90 so the blade / pick faces forward, then lean the handle forward by RestTilt (+ swing).
        Quaternion rot = playerT.rotation * Quaternion.Euler(RestTilt + swing, 0f, 0f) * Quaternion.Euler(0f, -90f, 0f);
        toolVisual.transform.rotation = rot;
        toolVisual.transform.position = handBone.position - rot * new Vector3(0f, GripLocalY * HeldScale, 0f);
    }

    public void Equip(ToolType type)
    {
        if (CurrentTool == type) return;
        CurrentTool = type;
        if (Inventory.Instance != null)
        {
            if (type != ToolType.None && Inventory.Instance.GetCount(type.ToString()) <= 0)
            {
                Inventory.Instance.Notify($"You don't have a {type}!");
                CurrentTool = ToolType.None;
                type = ToolType.None;
            }
        }
        
        if (toolVisual != null) Destroy(toolVisual);

        if (type == ToolType.None) return;

        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc == null) return;
        playerT = pc.transform;
        handBone = FindDeep(playerT, HandBoneName);
        if (handBone == null)
        {
            Debug.LogWarning("ToolManager: could not find hand bone '" + HandBoneName + "' under the player.");
            return;
        }

        toolVisual = new GameObject($"Equipped_{type}");
        toolVisual.transform.localScale = Vector3.one * HeldScale;
        
        // Detailed procedural model (tapered handle, wrapped grip, stone head with cutting edge)
        ToolModels.BuildHeld(toolVisual.transform, type);
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            Transform f = FindDeep(root.GetChild(i), name);
            if (f != null) return f;
        }
        return null;
    }
}

















