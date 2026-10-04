using UnityEngine;
using System.Collections;

public class FishingAnimForwarder : MonoBehaviour
{
    public FishingSystem system;
    public void ThrowLine() { if (system != null) system.ThrowLine(); }
}

public class FishingSystem : MonoBehaviour
{
    public static FishingSystem Instance { get; private set; }

    public enum FishingState { None, Casting, Idle, Bite, Catching }
    public FishingState CurrentState { get; private set; } = FishingState.None;

    private Animator animator;
    private PlayerController playerController;
    private LineRenderer fishingLine;
    private GameObject bobber;
    private ParticleSystem bubblesPS;
    
    private float stateTimer = 0f;
    private Vector3 castTarget;
    private bool isFlying = false;

    private void Awake()
    {
        Instance = this;
        animator = GetComponentInChildren<Animator>();
        playerController = GetComponent<PlayerController>();

        // Dynamically add a forwarder if the Animator is on a child object
        // This ensures the Animation Event "ThrowLine" reaches this script!
        if (animator != null && animator.gameObject != this.gameObject)
        {
            var forwarder = animator.gameObject.GetComponent<FishingAnimForwarder>();
            if (forwarder == null) forwarder = animator.gameObject.AddComponent<FishingAnimForwarder>();
            forwarder.system = this;
        }

        // Create line renderer
        GameObject lineObj = new GameObject("FishingLine");
        fishingLine = lineObj.AddComponent<LineRenderer>();
        fishingLine.startWidth = 0.01f;
        fishingLine.endWidth = 0.01f;
        fishingLine.material = new Material(Shader.Find("Sprites/Default"));
        fishingLine.startColor = Color.white;
        fishingLine.endColor = Color.white;
        fishingLine.positionCount = 2;
        fishingLine.enabled = false;

        // Create a simple bobber
        bobber = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        bobber.name = "Bobber";
        bobber.transform.localScale = new Vector3(0.15f, 0.15f, 0.15f);
        Destroy(bobber.GetComponent<Collider>());
        bobber.GetComponent<MeshRenderer>().material.color = Color.red;
        bobber.SetActive(false);

        // Create Minecraft-style bubble particles programmatically
        GameObject bubbles = new GameObject("Bubbles");
        bubbles.transform.SetParent(bobber.transform);
        bubbles.transform.localPosition = Vector3.zero;
        bubblesPS = bubbles.AddComponent<ParticleSystem>();
        
        var main = bubblesPS.main;
        main.duration = 1f;
        main.loop = true;
        main.startLifetime = 1.5f;
        main.startSpeed = 0.1f;
        main.startSize = 0.05f;
        main.startColor = new Color(0.8f, 0.9f, 1f, 0.8f);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.playOnAwake = false;
        
        var emission = bubblesPS.emission;
        emission.rateOverTime = 15f;
        
        var shape = bubblesPS.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.2f;
        shape.rotation = new Vector3(-90, 0, 0); // Flat on water surface
        
        var vel = bubblesPS.velocityOverLifetime;
        vel.enabled = true;
        // Fix for Particle System error: all axes must use the exact same mode!
        vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        vel.y = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
        
        var renderer = bubblesPS.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Sprites/Default"));
    }

    // Left Click ignores FishingSystem now
    public void OnActionPressed()
    {
    }

    private void TryCast()
    {
        Ray ray;
        if (Camera.main != null)
        {
            ray = new Ray(Camera.main.transform.position, Camera.main.transform.forward);
        }
        else
        {
            ray = new Ray(transform.position + Vector3.up * 1.5f, transform.forward);
        }

        float waterHeight = 4f; 
        
        Renderer[] renderers = FindObjectsOfType<Renderer>();
        foreach (Renderer r in renderers)
        {
            string n = r.gameObject.name.ToLower();
            if (n.Contains("water") || n.Contains("ocean") || n.Contains("sea") || n.Contains("lake") || n.Contains("river"))
            {
                waterHeight = r.bounds.center.y;
                break;
            }
        }

        float maxCastDist = 150f;
        RaycastHit[] hits = Physics.RaycastAll(ray, maxCastDist);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        bool hitDryLand = false;

        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.root == transform.root) continue;
            if (hit.collider.isTrigger) continue;

            if (hit.collider.gameObject.layer == 4 || hit.collider.gameObject.layer == 13 || hit.collider.gameObject.name.ToLower().Contains("water"))
            {
                castTarget = ClampCastDistance(hit.point, waterHeight);
                StartCoroutine(CastRoutine());
                return;
            }
            else
            {
                if (hit.point.y > waterHeight + 0.5f) 
                {
                    hitDryLand = true;
                    break;
                }
                else
                {
                    Plane waterPlane = new Plane(Vector3.up, new Vector3(0, waterHeight, 0));
                    if (waterPlane.Raycast(ray, out float enter))
                    {
                        castTarget = ClampCastDistance(ray.GetPoint(enter), waterHeight);
                        StartCoroutine(CastRoutine());
                        return;
                    }
                }
            }
        }

        if (hitDryLand)
        {
            Inventory.Instance.Notify("You need to aim directly at water to fish!");
            return;
        }

        Plane plane = new Plane(Vector3.up, new Vector3(0, waterHeight, 0));
        if (plane.Raycast(ray, out float dist) && dist <= maxCastDist)
        {
            castTarget = ClampCastDistance(ray.GetPoint(dist), waterHeight);
            StartCoroutine(CastRoutine());
            return;
        }

        Inventory.Instance.Notify("You need to aim directly at water to fish!");
    }

    private Vector3 ClampCastDistance(Vector3 targetPoint, float targetHeight)
    {
        float maxDistance = 15f;
        Vector2 playerPos2D = new Vector2(transform.position.x, transform.position.z);
        Vector2 targetPos2D = new Vector2(targetPoint.x, targetPoint.z);
        
        if (Vector2.Distance(playerPos2D, targetPos2D) > maxDistance)
        {
            Vector2 dir = (targetPos2D - playerPos2D).normalized;
            targetPos2D = playerPos2D + dir * maxDistance;
            targetPoint = new Vector3(targetPos2D.x, targetHeight, targetPos2D.y);
        }
        else
        {
            targetPoint.y = targetHeight;
        }
        return targetPoint;
    }

    private IEnumerator CastRoutine()
    {
        CurrentState = FishingState.Casting;
        isFlying = false;
        
        if (animator != null) 
        {
            animator.ResetTrigger("Fish_Cancel");
            animator.ResetTrigger("Fish_Catch");
            animator.SetTrigger("Fish_Cast");
        }
        
        // We wait for the ThrowLine animation event!
        // But add a MUCH shorter failsafe (0.8s) in case the user's animation event doesn't fire 
        // due to Unity skipping it during blend transitions (very common!).
        float failsafe = 0f;
        while (CurrentState == FishingState.Casting && !isFlying && failsafe < 0.8f)
        {
            failsafe += Time.deltaTime;
            yield return null;
        }

        if (CurrentState == FishingState.Casting && !isFlying)
        {
            ThrowLine(); // Force it if the event was missed
        }
    }

    // CALLED BY ANIMATION EVENT
    public void ThrowLine()
    {
        if (CurrentState == FishingState.Casting && !isFlying)
        {
            StartCoroutine(FlyBobberRoutine());
        }
    }

    private IEnumerator FlyBobberRoutine()
    {
        isFlying = true;
        
        Vector3 startPos = GetRodTip();
        Vector3 endPos = castTarget;
        
        bobber.transform.position = startPos;
        bobber.SetActive(true);
        fishingLine.enabled = true;
        
        float duration = 0.6f;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Linear horizontal interpolation
            Vector3 pos = Vector3.Lerp(startPos, endPos, t);
            
            // Parabolic arc vertical math (4 * height * t * (1-t))
            pos.y += 4f * 2.5f * t * (1f - t); 
            
            bobber.transform.position = pos;
            yield return null;
        }
        
        bobber.transform.position = endPos;
        isFlying = false;
        
        if (bubblesPS != null) bubblesPS.Play();
        
        CurrentState = FishingState.Idle;
        stateTimer = Random.Range(3f, 8f); // Wait for bite
    }

    private bool hasIsFishingParam = false;
    private bool checkedIsFishingParam = false;

    private void Update()
    {
        if (animator != null)
        {
            if (!checkedIsFishingParam && animator.runtimeAnimatorController != null)
            {
                foreach (var param in animator.parameters)
                {
                    if (param.name == "IsFishing") { hasIsFishingParam = true; break; }
                }
                checkedIsFishingParam = true;
            }
            if (hasIsFishingParam && animator.runtimeAnimatorController != null) 
            {
                animator.SetBool("IsFishing", CurrentState != FishingState.None);
            }
        }

        // Poll for Right Click to Cast / Catch
        if (ToolManager.Instance != null && ToolManager.Instance.CurrentTool == ToolManager.ToolType.FishingRod)
        {
            bool rightClick = false;
#if ENABLE_INPUT_SYSTEM
            if (UnityEngine.InputSystem.Mouse.current != null)
                rightClick = UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame;
#else
            rightClick = Input.GetMouseButtonDown(1);
#endif

            if (rightClick)
            {
                if (CurrentState == FishingState.None)
                {
                    TryCast();
                }
                else if (CurrentState == FishingState.Bite)
                {
                    // Catch the fish!
                    StartCoroutine(CatchRoutine());
                }
                else if (CurrentState == FishingState.Idle || CurrentState == FishingState.Casting)
                {
                    Inventory.Instance.Notify("Reeled in early!");
                    StopFishing();
                }
            }
        }

        if (CurrentState == FishingState.None) return;

        UpdateLineAndBobber();

        if (CurrentState == FishingState.Idle)
        {
            stateTimer -= Time.deltaTime;
            bobber.transform.position = castTarget + Vector3.up * Mathf.Sin(Time.time * 2f) * 0.05f;

            if (stateTimer <= 0f)
            {
                CurrentState = FishingState.Bite;
                stateTimer = 1.5f; 
                bobber.transform.position = castTarget + Vector3.down * 0.2f; // dip
                bobber.GetComponent<MeshRenderer>().material.color = Color.yellow;
                if (bubblesPS != null) bubblesPS.Stop(); // Stop bubbles on bite
                Inventory.Instance.Notify("Bite! Click now!");
            }
        }
        else if (CurrentState == FishingState.Bite)
        {
            stateTimer -= Time.deltaTime;
            if (stateTimer <= 0f)
            {
                Inventory.Instance.Notify("The fish got away... but keep waiting!");
                
                // Return to Idle instead of canceling the cast
                CurrentState = FishingState.Idle;
                stateTimer = Random.Range(3f, 8f);
                
                // Reset bobber visuals
                bobber.transform.position = castTarget;
                bobber.GetComponent<MeshRenderer>().material.color = Color.red;
                if (bubblesPS != null) bubblesPS.Play();
            }
        }
        
        if (CurrentState != FishingState.None)
        {
            if (ToolManager.Instance.CurrentTool != ToolManager.ToolType.FishingRod || (playerController != null && playerController.IsDashing))
            {
                Inventory.Instance.Notify("Reeled in early!");
                StopFishing();
            }
        }
    }

    private Vector3 GetRodTip()
    {
        Vector3 rodTip = transform.position + transform.up * 1.5f + transform.forward * 1.2f;
        if (ToolManager.Instance != null && ToolManager.Instance.CurrentTool == ToolManager.ToolType.FishingRod && ToolManager.Instance.ToolVisual != null)
        {
            rodTip = ToolManager.Instance.ToolVisual.transform.TransformPoint(new Vector3(0.05f, 2.1f, 0f));
        }
        return rodTip;
    }

    private void UpdateLineAndBobber()
    {
        if (!fishingLine.enabled) return;

        fishingLine.SetPosition(0, GetRodTip());
        fishingLine.SetPosition(1, bobber.transform.position);
    }

    private IEnumerator CatchRoutine()
    {
        CurrentState = FishingState.Catching;
        
        if (animator != null) animator.SetTrigger("Fish_Catch");
        
        bobber.SetActive(false);
        fishingLine.enabled = false;
        if (bubblesPS != null) bubblesPS.Stop();

        yield return new WaitForSeconds(1.5f);
        
        Inventory.Instance.AddItem("Fish", 1);
        Inventory.Instance.Notify("Caught a Fish!");
        
        StopFishing();
    }

    private void StopFishing()
    {
        if (CurrentState == FishingState.None) return;

        CurrentState = FishingState.None;
        bobber.SetActive(false);
        fishingLine.enabled = false;
        bobber.GetComponent<MeshRenderer>().material.color = Color.red;
        if (bubblesPS != null) bubblesPS.Stop();
        
        if (animator != null) 
        {
            animator.ResetTrigger("Fish_Cast");
            animator.ResetTrigger("Fish_Catch");
            animator.SetTrigger("Fish_Cancel");
        }
    }
}




