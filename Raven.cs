using UnityEngine;
using System.Collections;

public class Raven : MonoBehaviour
{
    private Animator animator;
    
    [Header("Settings")]
    public float flySpeed = 3f;
    public float turnSpeed = 3f;
    public float minWaitTime = 5f;
    public float maxWaitTime = 15f;
    public float flightHeight = 15f;
    public float searchRadius = 40f;
    
    private Vector3 targetPosition;
    private Vector3 currentVelocity;
    private bool isFlying = false;
    private bool isLanding = false;
    private float flightAnimTimer = 0f;
    
    // To play random idle animations (pecking vs standard idle)
    private float idleTimer = 0f;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        StartCoroutine(BirdRoutine());
    }

    void Update()
    {
        if (isFlying)
        {
            flightAnimTimer -= Time.deltaTime;
            if (flightAnimTimer <= 0f)
            {
                bool willGlide = Random.value > 0.7f; // 30% chance to glide
                if (HasAnimParam(animator, "IsGliding")) animator.SetBool("IsGliding", willGlide);
                flightAnimTimer = willGlide ? Random.Range(1.5f, 3f) : Random.Range(2f, 5f);
            }

            // Move smoothly towards target
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref currentVelocity, 0.8f, flySpeed);
            
            // Obstacle Avoidance (Steer away from trees/buildings)
            RaycastHit hit;
            if (Physics.Raycast(transform.position, currentVelocity.normalized, out hit, 5f))
            {
                // If it's not the ground
                if (hit.transform != transform && !hit.collider.name.Contains("Terrain"))
                {
                    // Push target position up and away
                    targetPosition += hit.normal * 4f + Vector3.up * 3f;
                }
            }
            
            // Rotate smoothly with banking
            Vector3 horizVel = currentVelocity;
            horizVel.y = 0;
            
            if (horizVel.sqrMagnitude > 0.01f)
            {
                // Calculate roll (banking) based on lateral velocity
                Vector3 rightDir = Vector3.Cross(Vector3.up, horizVel.normalized);
                Vector3 toTarget = (targetPosition - transform.position).normalized;
                toTarget.y = 0;
                float bank = -Vector3.Dot(rightDir, toTarget.normalized) * 45f; // tilt up to 45 degrees
                
                // Calculate pitch based on vertical velocity
                float pitch = -Mathf.Clamp(currentVelocity.y * 3f, -30f, 30f);
                
                Quaternion targetRot = Quaternion.LookRotation(horizVel.normalized, Vector3.up);
                targetRot *= Quaternion.Euler(pitch, 0, bank);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, turnSpeed * Time.deltaTime);
            }
            
            // Check if we arrived
            if (Vector3.Distance(transform.position, targetPosition) < 0.8f)
            {
                if (isLanding)
                {
                    // Actually landed
                    isFlying = false;
                    isLanding = false;
                    transform.position = targetPosition; // Snap to ground so it doesn't float!
                    animator.SetBool("IsFlying", false);
                }
                else
                {
                    // Reached waypoint in the air, time to land somewhere
                    FindLandingSpot();
                }
            }
        }
        else
        {
            if (WorldGenerator.Instance != null) {
                float ty = WorldGenerator.Instance.GetHeightAt(transform.position.x, transform.position.z);
                // If they are on the ground (not in a tree) keep them glued perfectly
                if (Mathf.Abs(transform.position.y - ty) < 0.5f) {
                    transform.position = new Vector3(transform.position.x, ty, transform.position.z);
                }
            }

            // Randomly play idle variations while on the ground
            idleTimer -= Time.deltaTime;
            if (idleTimer <= 0f)
            {
                float rand = Random.value;
                if (rand > 0.65f)
                {
                    animator.CrossFadeInFixedTime("Pecking", 0.25f);
                }
                else if (rand > 0.3f)
                {
                    animator.CrossFadeInFixedTime("Idle2", 0.25f); // The look-around idle
                }
                else if (rand > 0.15f)
                {
                    animator.CrossFadeInFixedTime("WalkJump", 0.25f); // Short hop
                }
                
                idleTimer = Random.Range(3f, 8f);
            }
        }
    }

    IEnumerator BirdRoutine()
    {
        while (true)
        {
            // Wait on the ground/tree
            yield return new WaitForSeconds(Random.Range(minWaitTime, maxWaitTime));
            
            // Take off!
            animator.SetBool("IsFlying", true);
            if (HasAnimParam(animator, "IsGliding")) animator.SetBool("IsGliding", false);
            isFlying = true;
            isLanding = false;
            flightAnimTimer = Random.Range(2f, 5f);
            
            // Fly up into the air
            targetPosition = transform.position + new Vector3(Random.Range(-5f, 5f), flightHeight, Random.Range(-5f, 5f));
            
            // Wait until it reaches the sky waypoint (handled in Update)
            while (isFlying && !isLanding)
            {
                yield return null;
            }
            
            // Wait until landed (handled in Update)
            while (isFlying)
            {
                yield return null;
            }
        }
    }

    void FindLandingSpot()
    {
        for (int i = 0; i < 10; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle * searchRadius;
            float x = transform.position.x + randomCircle.x;
            float z = transform.position.z + randomCircle.y;
            
            float terrainY = 0f;
            if (WorldGenerator.Instance != null) {
                terrainY = WorldGenerator.Instance.GetHeightAt(x, z);
            }
            
            Ray ray = new Ray(new Vector3(x, transform.position.y + 10f, z), Vector3.down);
            bool hitSomething = Physics.Raycast(ray, out RaycastHit hit, 200f);
            
            if (hitSomething)
            {
                if (hit.collider.GetComponentInParent<Animal>() != null) continue;
                if (hit.collider.name.Contains("Player") || hit.collider.name.Contains("NPC")) continue;
                
                float landY = hit.point.y;
                if (Mathf.Abs(landY - terrainY) < 1.0f) {
                    landY = terrainY; // Snap exactly to terrain mathematical height
                }
                
                if (WorldGenerator.Instance != null && landY <= WorldGenerator.Instance.waterLevel + 0.5f) continue;
                
                targetPosition = new Vector3(x, landY, z);
                isLanding = true;
                return;
            }
            else if (WorldGenerator.Instance != null)
            {
                // Fallback to terrain if raycast misses
                if (terrainY > WorldGenerator.Instance.waterLevel + 0.5f)
                {
                    targetPosition = new Vector3(x, terrainY, z);
                    isLanding = true;
                    return;
                }
            }
        }
        
        targetPosition = transform.position + new Vector3(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
    }

    private static readonly System.Collections.Generic.Dictionary<RuntimeAnimatorController, System.Collections.Generic.HashSet<string>> animParamCache =
        new System.Collections.Generic.Dictionary<RuntimeAnimatorController, System.Collections.Generic.HashSet<string>>();

    // Cached: Animator.parameters allocates a brand new array every call, which was spamming the GC every frame.
    private static bool HasAnimParam(Animator anim, string name)
    {
        if (anim == null) return false;
        RuntimeAnimatorController key = anim.runtimeAnimatorController;
        if (key == null) return false;
        if (!animParamCache.TryGetValue(key, out var set))
        {
            set = new System.Collections.Generic.HashSet<string>();
            foreach (var p in anim.parameters) set.Add(p.name);
            animParamCache[key] = set;
        }
        return set.Contains(name);
    }

}




