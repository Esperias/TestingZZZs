#pragma warning disable CS0414
using System.Collections.Generic;
using UnityEngine;

public class Animal : MonoBehaviour
{
    public enum AnimalType { Deer, Boar, Wolf }
    
    [Header("Settings")]
    public AnimalType type;
    public float maxHealth = 100f;
    public float walkSpeed = 2f;
    public float runSpeed = 5.5f;
    public float turnSpeed = 8f;
    public float wanderRadius = 15f;
    public float attackDamage = 15f;
    public float aggroRange = 12f;
    public float attackRange = 2f;
    
    public float currentHealth;
    private Animator animator;
    
    // Damage Numbers
    private UnityEngine.UI.Text damageText;

    private float accumulatedDamage = 0f;

    private float damageTextTimer = 0f;
    private Transform player;
    
    private enum State { Idle, Wander, Flee, Chase, Attack, Hit, Dead, Sit, Sleep, Stunned, WakingUp }
    private State currentState = State.Idle;
    
    private Vector3 moveTarget;
    private float stateTimer;
    private float walkTimer;
    private float attackCooldown;

    private Renderer[] renderers;
    private System.Collections.Generic.Dictionary<Renderer, Material[]> originalMatsCache;
    
    // Healthbar UI
    private Canvas healthCanvas;
    private RectTransform healthFillRect;
    private RectTransform healthTrailRect;
    private float healthBarTimer;
    private float trailHealth;
    private float trailTimer;

    private bool hidden;
    private int speedHash;
    private int turnHash;
    
    private CharacterController charController;
    private float gravityVelocity;
    private float previousY;

    private int hitXHash;
    private int hitYHash;

    private float currentSpeedTarget;
    private float currentTurnTarget;
    private float stuckTimer;

    private int attackTypeHash;

    private void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponentInChildren<Animator>();
        if (animator == null) animator = GetComponent<Animator>();
        
        // Destroy old physics components that conflict with CharacterController
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) Destroy(rb);
        Collider[] oldCols = GetComponents<Collider>();
        foreach (Collider c in oldCols)
        {
            if (!(c is CharacterController)) Destroy(c);
        }

        charController = GetComponent<CharacterController>();
        if (charController == null)
        {
            charController = gameObject.AddComponent<CharacterController>();
            charController.radius = 0.4f;
            charController.height = 1.2f;
            charController.center = new Vector3(0, 0.6f, 0);
            charController.stepOffset = 0.8f;
        }
        
        if (animator != null)
        {
            animator.applyRootMotion = false;
            // AlwaysAnimate prevents shadow culling / popping bugs at screen edges!
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        }
        
        speedHash = Animator.StringToHash("Speed");
        turnHash = Animator.StringToHash("Turn");
        hitXHash = Animator.StringToHash("HitX");
        hitYHash = Animator.StringToHash("HitY");
        attackTypeHash = Animator.StringToHash("AttackType");
        
        PlayerController pc = FindAnyObjectByType<PlayerController>();
        if (pc != null) player = pc.transform;
        
        EnterState(State.Idle);
        renderers = GetComponentsInChildren<Renderer>();
        
                originalMatsCache = new System.Collections.Generic.Dictionary<Renderer, Material[]>();
        foreach (Renderer r in renderers)
        {
            if (r != null) originalMatsCache[r] = r.materials; // caches the instances
        }
        
        // Generate Floating Healthbar UI
        GameObject canvasObj = new GameObject("HealthCanvas");
        canvasObj.transform.SetParent(transform, false);
        canvasObj.transform.localPosition = new Vector3(0, 1.8f, 0); // Above animal head
        healthCanvas = canvasObj.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        
        RectTransform rt = canvasObj.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1.5f, 0.15f);
        
        // Background
        GameObject bgObj = new GameObject("Background");
        bgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image bgImg = bgObj.AddComponent<UnityEngine.UI.Image>();
        bgImg.color = new Color(0, 0, 0, 0.8f);
        RectTransform bgRt = bgObj.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; 
        bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;
        
        // White Trail
        GameObject trailObj = new GameObject("Trail");
        trailObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image trailImg = trailObj.AddComponent<UnityEngine.UI.Image>();
        trailImg.color = Color.white;
        healthTrailRect = trailObj.GetComponent<RectTransform>();
        healthTrailRect.anchorMin = Vector2.zero; healthTrailRect.anchorMax = Vector2.one;
        healthTrailRect.offsetMin = Vector2.zero; healthTrailRect.offsetMax = Vector2.zero;
        
        // Red Foreground
        GameObject fgObj = new GameObject("Foreground");
        fgObj.transform.SetParent(canvasObj.transform, false);
        UnityEngine.UI.Image fgImg = fgObj.AddComponent<UnityEngine.UI.Image>();
        fgImg.color = new Color(0.9f, 0.1f, 0.1f, 1f);
        healthFillRect = fgObj.GetComponent<RectTransform>();
        healthFillRect.anchorMin = Vector2.zero; healthFillRect.anchorMax = Vector2.one;
        healthFillRect.offsetMin = Vector2.zero; healthFillRect.offsetMax = Vector2.zero;
        
        trailHealth = maxHealth;
        healthCanvas.gameObject.SetActive(false);
    }

    private void PlayAnim(string stateName)
    {
        if (animator != null)
        {
            if (stateName == "Death")
            {
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.ResetTrigger("Hit");
                animator.ResetTrigger("Eat");
                animator.ResetTrigger("Attack");
            }

            if (stateName == "Eat" || stateName == "Attack" || stateName == "Hit" || stateName == "Death")
            {
                animator.SetTrigger(stateName);
            }
        }
    }
    
    private float hopVelocity = 0f;
    private float hopOffset = 0f;
    private bool isFlashing = false;
    private System.Collections.IEnumerator DamageFlashRoutine()
    {
        if (isFlashing) yield break;
        isFlashing = true;
        
        Shader unlit = Shader.Find("Universal Render Pipeline/Unlit");
        if (unlit == null) unlit = Shader.Find("Unlit/Color"); // Fallback
        
        foreach (Renderer r in renderers)
        {
            if (r == null || !originalMatsCache.ContainsKey(r)) continue;
            
            Material[] origArray = originalMatsCache[r];
            Material[] flashMats = new Material[origArray.Length];
            for (int i = 0; i < flashMats.Length; i++)
            {
                Material orig = origArray[i];
                Material flash = new Material(unlit);
                
                // Copy texture
                Texture tex = null;
                if (orig.HasProperty("_BaseMap")) tex = orig.GetTexture("_BaseMap");
                else if (orig.HasProperty("_MainTex")) tex = orig.GetTexture("_MainTex");
                
                if (tex != null)
                {
                    if (flash.HasProperty("_BaseMap")) flash.SetTexture("_BaseMap", tex);
                    if (flash.HasProperty("_MainTex")) flash.SetTexture("_MainTex", tex);
                }
                
                // Bright red tint
                if (flash.HasProperty("_BaseColor")) flash.SetColor("_BaseColor", new Color(1f, 0.4f, 0.4f, 1f));
                if (flash.HasProperty("_Color")) flash.SetColor("_Color", new Color(1f, 0.4f, 0.4f, 1f));
                
                flashMats[i] = flash;
            }
            r.materials = flashMats;
        }
        
        // Increased duration to be much more visible
        yield return new WaitForSeconds(0.15f);
        
        // Restore materials safely from permanent cache
        foreach (Renderer r in renderers)
        {
            if (r != null && originalMatsCache.ContainsKey(r))
            {
                r.materials = originalMatsCache[r];
            }
        }
        
        isFlashing = false;
    }

    public void TakeDamage(float amount, Transform attacker)
    {
        if (currentState == State.Dead) return;
        
        currentHealth -= amount;
        
        // Trigger Healthbar
        healthBarTimer = 6f;
        trailTimer = 0.5f; // Wait half a second before shrinking trail
        if (healthCanvas != null) healthCanvas.gameObject.SetActive(true);
        if (healthFillRect != null) healthFillRect.anchorMax = new Vector2(Mathf.Clamp01(currentHealth / maxHealth), 1f);
        
        StartCoroutine(DamageFlashRoutine());
        
        // Calculate hit direction for the Hit Blend Tree
        if (animator != null && attacker != null)
        {
            Vector3 hitDir = transform.InverseTransformDirection((attacker.position - transform.position).normalized);
            animator.SetFloat(hitXHash, hitDir.x);
            animator.SetFloat(hitYHash, hitDir.z);
        }

        if (currentHealth <= 0)
        {
            EnterState(State.Dead);
        }
        else
        {
            if (type != AnimalType.Wolf)
            {
                Vector3 fleeDir = (transform.position - attacker.position).normalized;
                fleeDir.y = 0;
                moveTarget = transform.position + fleeDir * 20f;
                
                // Hop because they are running away after being hit
                if (charController != null) gravityVelocity = 3.5f;
                else hopVelocity = 3.5f;

                if (currentState == State.Flee)
                {
                    // Let them keep running instead of playing stationary hit
                    return;
                }
            }
            else
            {
                if (currentState == State.Chase || currentState == State.Attack)
                {
                    // Let them keep chasing/attacking instead of playing stationary hit
                    return;
                }
            }
            
            EnterState(State.Hit);
        }
    }
    
    private float FlatDistance(Vector3 a, Vector3 b)
    {
        a.y = 0;
        b.y = 0;
        return Vector3.Distance(a, b);
    }

    private void EnterState(State newState)
    {
        // If we are leaving a resting state, clear the booleans so the Animator can transition out
        if (currentState == State.Sit && newState != State.Sit)
        {
            if (animator != null) animator.SetBool("IsSitting", false);
        }
        if (currentState == State.Sleep && newState != State.Sleep)
        {
            if (animator != null) animator.SetBool("IsSleeping", false);
        }

        currentState = newState;
        switch (newState)
        {
            case State.Idle:
                stateTimer = Random.Range(2f, 6f);
                if (Random.value > 0.5f) PlayAnim("Eat");
                if (animator != null) animator.SetFloat(speedHash, 0f); animator.SetFloat(turnHash, 0f);
                break;
                
            case State.WakingUp:
                stateTimer = 1.5f;
                if (animator != null) animator.SetFloat(speedHash, 0f); animator.SetFloat(turnHash, 0f);
                PlayAnim("Idle");
                break;
                
            case State.Wander:
                if (animator != null) animator.Play("Locomotion");
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                Vector3 tryTarget = new Vector3(transform.position.x + offset.x, transform.position.y, transform.position.z + offset.y);
                
                // Keep animals out of water
                if (WorldGenerator.Instance != null) {
                    float ty = WorldGenerator.Instance.GetHeightAt(tryTarget.x, tryTarget.z);
                    if (ty < WorldGenerator.Instance.waterLevel + 1.0f) {
                        tryTarget = transform.position; // stay still if target is water
                    }
                }

                if (!Physics.Raycast(transform.position + Vector3.up * 0.5f, (tryTarget - transform.position).normalized, wanderRadius, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                {
                    moveTarget = tryTarget;
                }
                else
                {
                    moveTarget = transform.position;
                }
                walkTimer = 5f;
                break;
                
            case State.Flee:
                if (player != null) {
                    Vector3 runDir = (transform.position - player.position).normalized;
                    Vector3 fleeTarget = transform.position + runDir * 10f;
                    
                    if (WorldGenerator.Instance != null) {
                        float ty = WorldGenerator.Instance.GetHeightAt(fleeTarget.x, fleeTarget.z);
                        if (ty < WorldGenerator.Instance.waterLevel + 1.0f) {
                            fleeTarget = transform.position + Quaternion.Euler(0, 90, 0) * runDir * 10f; // Turn right
                        }
                    }
                    moveTarget = fleeTarget;
                } else {
                    moveTarget = transform.position;
                }
                walkTimer = 5f;
                break;
                
            case State.Chase:
                if (animator != null) animator.Play("Locomotion");
                break;
                
            case State.Attack:
                stateTimer = 1.5f;
                attackCooldown = Random.Range(3f, 5f);
                if (animator != null) animator.SetFloat("AttackType", Random.value > 0.5f ? 1f : 0f);
                PlayAnim("Attack");
                if (animator != null) animator.SetFloat(speedHash, 0f); animator.SetFloat(turnHash, 0f);
                if (Inventory.Instance != null) Inventory.Instance.Notify($"{type} attacked you for {attackDamage} damage!");
                // Actually deal damage to the player
                if (PlayerHealth.Instance != null) PlayerHealth.Instance.TakeDamage(attackDamage);
                break;
                
            case State.Hit:
                PlayAnim("Hit");
                stateTimer = 0.3f;
                if (animator != null) animator.SetFloat(speedHash, 0f); animator.SetFloat(turnHash, 0f);
                break;

            case State.Dead:
                PlayAnim("Death");
                if (animator != null) animator.SetFloat(speedHash, 0f);
                StartCoroutine(DissolveRoutine());
                
                if (Inventory.Instance != null)
                {
                    if (type == AnimalType.Deer)
                    {
                        Inventory.Instance.AddItem("Venison", Random.Range(1, 4));
                        if (Random.value > 0.5f) Inventory.Instance.AddItem("Pelt", 1);
                    }
                    else if (type == AnimalType.Boar)
                    {
                        Inventory.Instance.AddItem("Pork", Random.Range(1, 3));
                        if (Random.value > 0.5f) Inventory.Instance.AddItem("Leather", 1);
                        if (Random.value > 0.7f) Inventory.Instance.AddItem("Boar Tusk", 1);
                    }
                    else if (type == AnimalType.Wolf)
                    {
                        Inventory.Instance.AddItem("Wolf Meat", Random.Range(1, 3));
                        Inventory.Instance.AddItem("Pelt", Random.Range(1, 3));
                        if (Random.value > 0.6f) Inventory.Instance.AddItem("Wolf Fang", 1);
                    }
                }
                break;
                
            case State.Sit:
                stateTimer = Random.Range(5f, 15f);
                PlayAnim("Sit");
                break;
                
            case State.Sleep:
                stateTimer = Random.Range(10f, 30f);
                PlayAnim("Sleep");
                break;
                
            case State.Stunned:
                stateTimer = Random.Range(2f, 5f);
                PlayAnim("Stunned");
                break;
        }

        if (charController != null)
        {
            Vector3 flatVel = charController.velocity;
            flatVel.y = 0;
            bool isTryingToMove = (currentState == State.Wander || currentState == State.Chase);
            
                        // 1. Better Stuck Handling: Turn around if blocked instead of hopping!
            float expectedSpeedSqr = (currentSpeedTarget * currentSpeedTarget) * 0.6f;
            if (flatVel.sqrMagnitude < expectedSpeedSqr && currentSpeedTarget > 0.1f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;
            
            if (stuckTimer > 0.4f)
            {
                stuckTimer = 0f;
                if (currentState == State.Wander || currentState == State.Flee)
                {
                    // Pick a new direction away from the wall
                    Vector3 newDir = Quaternion.Euler(0, Random.Range(120f, 240f), 0) * transform.forward;
                    moveTarget = transform.position + newDir * (currentState == State.Flee ? 12f : 5f);
                }
                else if (currentState == State.Chase)
                {
                    EnterState(State.Idle); // Give up chasing if stuck for too long
                }
            }
        }
        
        if (animator != null)
        {
            animator.SetFloat(speedHash, currentSpeedTarget, 0.15f, Time.deltaTime);
            animator.SetFloat(turnHash, currentTurnTarget, 0.15f, Time.deltaTime);
            if (HasAnimParam(animator, "IsGrounded")) animator.SetBool("IsGrounded", charController.isGrounded || Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.4f));
            if (HasAnimParam(animator, "VerticalVelocity")) animator.SetFloat("VerticalVelocity", charController.velocity.y);
        }
    }
    
    private float lodTimer;

    private void Update()
    {
        float actualVertVel = Time.deltaTime > 0 ? (transform.position.y - previousY) / Time.deltaTime : 0f;
        previousY = transform.position.y;
        if (currentState == State.Dead) 
        {
            if (healthCanvas != null && healthCanvas.gameObject.activeSelf) healthCanvas.gameObject.SetActive(false);
            return;
        }

        // Distance LOD: animals far from the player don't need their animator evaluated at all.
        // Nearby ones stay AlwaysAnimate (avoids shadow popping at screen edges).
        lodTimer -= Time.deltaTime;
        if (lodTimer <= 0f && animator != null && player != null)
        {
            lodTimer = 0.5f + (transform.position.x * 13.37f % 1f + 1f) % 1f * 0.3f;
            animator.cullingMode = FlatDistance(transform.position, player.position) > 70f
                ? AnimatorCullingMode.CullCompletely
                : AnimatorCullingMode.AlwaysAnimate;
        }

        // Prevent falling through the unbaked terrain
        if (WorldGenerator.Instance != null)
        {
            float terrainHeight = WorldGenerator.Instance.GetHeightAt(transform.position.x, transform.position.z);
            if (transform.position.y < terrainHeight - 0.2f)
            {
                if (charController != null) charController.enabled = false;
                transform.position = new Vector3(transform.position.x, terrainHeight + 0.1f, transform.position.z);
                if (charController != null) charController.enabled = true;
                gravityVelocity = 0f;
            }
        }

        // Handle Healthbar UI
        if (healthCanvas != null && healthCanvas.gameObject.activeSelf)
        {
            healthBarTimer -= Time.deltaTime;
            if (healthBarTimer <= 0)
            {
                healthCanvas.gameObject.SetActive(false);
            }
            else
            {
                if (Camera.main != null)
                {
                    healthCanvas.transform.rotation = Quaternion.LookRotation(healthCanvas.transform.position - Camera.main.transform.position);
                }
                
                if (trailTimer > 0) trailTimer -= Time.deltaTime;
                else if (trailHealth > currentHealth)
                {
                    trailHealth -= (maxHealth * 0.4f) * Time.deltaTime;
                    if (trailHealth < currentHealth) trailHealth = currentHealth;
                    if (healthTrailRect != null) healthTrailRect.anchorMax = new Vector2(Mathf.Clamp01(trailHealth / maxHealth), 1f);
                }
            }
        }

        if (attackCooldown > 0f) attackCooldown -= Time.deltaTime;
        
        if (charController != null)
        {
            if (!charController.isGrounded) 
            {
                gravityVelocity += Physics.gravity.y * 2.5f * Time.deltaTime;
                if (actualVertVel > -0.1f && gravityVelocity < -2f) gravityVelocity = Physics.gravity.y * 0.1f;
            }
            else if (gravityVelocity <= 0) gravityVelocity = Physics.gravity.y * 0.1f;
            
            charController.Move(Vector3.up * gravityVelocity * Time.deltaTime);
        }
        else if (hopVelocity != 0f || hopOffset > 0f)
        {
            hopVelocity -= 20f * Time.deltaTime;
            hopOffset += hopVelocity * Time.deltaTime;
            if (hopOffset <= 0f) { hopOffset = 0f; hopVelocity = 0f; }
            
            Vector3 p = transform.position;
            if (WorldGenerator.Instance != null) p.y = WorldGenerator.Instance.GetHeightAt(p.x, p.z) + hopOffset;
            else p.y += hopVelocity * Time.deltaTime;
            transform.position = p;
        }

        if (animator != null)
        {
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (info.IsName("Eat") || info.IsName("Attack"))
            {
                if (info.normalizedTime < 0.95f)
                {
                    animator.SetFloat(speedHash, 0f, 0.15f, Time.deltaTime);
                    animator.SetFloat(turnHash, 0f, 0.15f, Time.deltaTime);
                    return;
                }
            }
            else if (animator.IsInTransition(0))
            {
                AnimatorStateInfo next = animator.GetNextAnimatorStateInfo(0);
                if (next.IsName("Eat") || next.IsName("Attack"))
                {
                    animator.SetFloat(speedHash, 0f, 0.15f, Time.deltaTime);
                    animator.SetFloat(turnHash, 0f, 0.15f, Time.deltaTime);
                    return;
                }
            }
        }
        
        currentSpeedTarget = 0f;
        currentTurnTarget = 0f;
        
        switch (currentState)
        {
                        case State.Hit:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    if (type == AnimalType.Wolf || type == AnimalType.Boar) EnterState(State.Chase);
                    else EnterState(State.Flee);
                }
                break;
                
            case State.Idle:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    float r = Random.value;
                    if (r < 0.2f) EnterState(State.Sit);
                    else if (r < 0.3f) EnterState(State.Sleep);
                    else EnterState(State.Wander);
                }
                CheckAggro();
                break;
                
            case State.WakingUp:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    EnterState(State.Idle);
                }
                CheckAggro();
                break;
                
            case State.Sit:
            case State.Sleep:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    EnterState(State.WakingUp); // Wake up and stand
                }
                CheckAggro();
                break;
                
            case State.Stunned:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    EnterState(State.Idle);
                }
                break;
                
            case State.Wander:
                MoveTowards(moveTarget, walkSpeed);
                walkTimer -= Time.deltaTime;
                
                Vector3 wDir = moveTarget - transform.position;
                wDir.y = 0;
                if (walkTimer <= 0 || wDir.magnitude < 1f)
                {
                    EnterState(State.Idle);
                }
                CheckAggro();
                break;
                
            case State.Flee:
                MoveTowards(moveTarget, runSpeed);
                walkTimer -= Time.deltaTime;
                if (walkTimer <= 0)
                {
                    EnterState(State.Idle);
                }
                break;
                
            case State.Chase:
                if (player == null) { EnterState(State.Idle); break; }
                
                Vector3 cDir = player.position - transform.position;
                cDir.y = 0;
                float dist = cDir.magnitude;
                
                if (dist > aggroRange * 1.5f)
                {
                    EnterState(State.Idle);
                }
                else if (dist <= attackRange)
                {
                    if (attackCooldown <= 0f)
                    {
                        EnterState(State.Attack);
                    }
                    else
                    {
                        // Stop moving but look at player while on cooldown
                        if (animator != null) animator.SetFloat(speedHash, 0f);
                        
                        if (cDir.sqrMagnitude > 0.01f)
                        {
                            Vector3 lookDir = cDir.normalized;
                            float angle = Vector3.SignedAngle(transform.forward, lookDir, Vector3.up);
                            float turnAmount = Mathf.Clamp(angle / 45f, -1f, 1f); 
                            if (animator != null) animator.SetFloat(turnHash, turnAmount);
                            
                            Quaternion rot = Quaternion.LookRotation(lookDir);
                            transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
                        }
                    }
                }
                else
                {
                    MoveTowards(player.position, runSpeed);
                }
                break;
                
            case State.Attack:
                stateTimer -= Time.deltaTime;
                if (stateTimer <= 0)
                {
                    EnterState(State.Chase);
                }
                break;
        }

        if (charController != null)
        {
            Vector3 flatVel = charController.velocity;
            flatVel.y = 0;
            bool isTryingToMove = (currentState == State.Wander || currentState == State.Chase);
            
                        // 1. Better Stuck Handling: Turn around if blocked instead of hopping!
            float expectedSpeedSqr = (currentSpeedTarget * currentSpeedTarget) * 0.6f;
            if (flatVel.sqrMagnitude < expectedSpeedSqr && currentSpeedTarget > 0.1f) stuckTimer += Time.deltaTime;
            else stuckTimer = 0f;
            
            if (stuckTimer > 0.4f)
            {
                stuckTimer = 0f;
                if (currentState == State.Wander || currentState == State.Flee)
                {
                    // Pick a new direction away from the wall
                    Vector3 newDir = Quaternion.Euler(0, Random.Range(120f, 240f), 0) * transform.forward;
                    moveTarget = transform.position + newDir * (currentState == State.Flee ? 12f : 5f);
                }
                else if (currentState == State.Chase)
                {
                    EnterState(State.Idle); // Give up chasing if stuck for too long
                }
            }
        }
        
        if (animator != null)
        {
            animator.SetFloat(speedHash, currentSpeedTarget, 0.15f, Time.deltaTime);
            animator.SetFloat(turnHash, currentTurnTarget, 0.15f, Time.deltaTime);
            if (HasAnimParam(animator, "IsGrounded")) animator.SetBool("IsGrounded", charController.isGrounded || Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, 0.4f));
            if (HasAnimParam(animator, "VerticalVelocity")) animator.SetFloat("VerticalVelocity", actualVertVel);
        }
    }
    
    private void CheckAggro()
    {
        if (type == AnimalType.Wolf && player != null)
        {
            Vector3 aDir = player.position - transform.position;
            aDir.y = 0;
            if (aDir.magnitude < aggroRange)
            {
                if (currentState == State.Sit || currentState == State.Sleep)
                {
                    EnterState(State.WakingUp); // Wake up before chasing!
                }
                else if (currentState != State.WakingUp)
                {
                    EnterState(State.Chase);
                }
            }
        }
    }
    
    public void Jump()
    {
        if (currentState == State.Dead) return;
        PlayAnim("Jump");
    }

    public void Roar()
    {
        if (currentState == State.Dead) return;
        PlayAnim("Roar");
    }

    private System.Collections.IEnumerator DissolveRoutine()
    {
        yield return new WaitForSeconds(2.5f); // Wait for death animation to finish

        // Spawn colored flakes from the mesh
        GameObject flakesGo = new GameObject("DissolveFlakes");
        flakesGo.transform.SetParent(transform, false);
        ParticleSystem ps = flakesGo.AddComponent<ParticleSystem>();
        ps.Stop(); // Prevent the duration error
        var main = ps.main;
        main.duration = 2f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.2f, 0.8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.04f); // Small flakes
        main.startColor = Color.white; // White so it can be tinted by the texture
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        
        var emission = ps.emission;
        emission.rateOverTime = 80; // Not too crazy
        
        var vol = ps.velocityOverLifetime;
        vol.enabled = true;
        vol.x = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
        vol.y = new ParticleSystem.MinMaxCurve(0.2f, 1.0f); // Float up gently
        vol.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);

        var shape = ps.shape;
        SkinnedMeshRenderer smr = GetComponentInChildren<SkinnedMeshRenderer>();
        if (smr != null)
        {
            shape.shapeType = ParticleSystemShapeType.SkinnedMeshRenderer;
            shape.skinnedMeshRenderer = smr;
            
            Texture2D tex = null;
            if (smr.sharedMaterial != null)
            {
                if (smr.sharedMaterial.HasProperty("_BaseMap")) tex = smr.sharedMaterial.GetTexture("_BaseMap") as Texture2D;
                else if (smr.sharedMaterial.HasProperty("_MainTex")) tex = smr.sharedMaterial.GetTexture("_MainTex") as Texture2D;
            }
            if (tex != null)
            {
                shape.texture = tex;
                shape.textureColorAffectsParticles = true;
            }
        }
        
        var particleRenderer = ps.GetComponent<ParticleSystemRenderer>();
        particleRenderer.material = new Material(Shader.Find("Sprites/Default"));

        ps.Play();

        // Swap to our Custom Dissolve Shader
        List<Material> dissolveMats = new List<Material>();
        foreach (Renderer r in renderers)
        {
            if (r == null) continue;
            Material[] mats = r.materials;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = new Material(Shader.Find("Custom/URPLitDissolve"));
                if (mats[i].HasProperty("_MainTex")) m.SetTexture("_BaseMap", mats[i].GetTexture("_MainTex"));
                else if (mats[i].HasProperty("_BaseMap")) m.SetTexture("_BaseMap", mats[i].GetTexture("_BaseMap"));
                
                if (mats[i].HasProperty("_Color")) m.SetColor("_BaseColor", mats[i].color);
                else if (mats[i].HasProperty("_BaseColor")) m.SetColor("_BaseColor", mats[i].GetColor("_BaseColor"));
                
                mats[i] = m;
                dissolveMats.Add(m);
            }
            r.materials = mats;
        }

        // Dissolve the animal using the shader property
        float t = 0;
        float duration = 2.0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float progress = t / duration;
            foreach (Material m in dissolveMats)
            {
                m.SetFloat("_DissolveAmount", progress);
            }
            yield return null;
        }

        ps.Stop();
        Destroy(flakesGo, 1.5f);
        Destroy(gameObject);
    }

    private void MoveTowards(Vector3 targetPos, float speed)
    {
        Vector3 dir = targetPos - transform.position;
        dir.y = 0;
        
        // Prevent pushing the player!
        if (player != null && currentState != State.Flee)
        {
            if (FlatDistance(transform.position, player.position) < 1.5f)
            {
                speed = 0f;
            }
        }
        
        if (dir.sqrMagnitude > 0.01f && speed > 0.01f)
        {
            Vector3 desiredForward = dir.normalized;
            

            
            desiredForward = desiredForward.normalized;
            float angle = Vector3.SignedAngle(transform.forward, desiredForward, Vector3.up);
            float turnAmount = Mathf.Clamp(angle / 45f, -1f, 1f); 
            
            Quaternion rot = Quaternion.LookRotation(desiredForward);
            transform.rotation = Quaternion.Slerp(transform.rotation, rot, turnSpeed * Time.deltaTime);
            
            // Prevent moving into water
            if (WorldGenerator.Instance != null && speed > 0.01f)
            {
                Vector3 testPos = transform.position + desiredForward * speed * Time.deltaTime * 5f;
                if (WorldGenerator.Instance.GetHeightAt(testPos.x, testPos.z) < WorldGenerator.Instance.waterLevel + 0.5f)
                {
                    speed = 0f;
                }
            }

            if (charController != null)
            {
                Vector3 velocity = desiredForward * speed;
                
                if (!charController.isGrounded) gravityVelocity += Physics.gravity.y * 2.5f * Time.deltaTime;
                else if (gravityVelocity <= 0) gravityVelocity = Physics.gravity.y * 0.1f;
                
                charController.Move(Vector3.up * gravityVelocity * Time.deltaTime);
                if (speed > 0f) charController.Move(desiredForward * speed * Time.deltaTime);
            }
            else
            {
                Vector3 newPos = transform.position + desiredForward * speed * Time.deltaTime;
                if (WorldGenerator.Instance != null)
                {
                    newPos.y = WorldGenerator.Instance.GetHeightAt(newPos.x, newPos.z) + hopOffset;
                }
                transform.position = newPos;
            }
            
            currentSpeedTarget = speed;
            currentTurnTarget = turnAmount;
        }
    }

    // Utility: safely check if an animator has a given parameter
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

















































#pragma warning restore CS0414




