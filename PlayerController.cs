using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Simple first person movement. Put this on the Player capsule.
/// Keys come from Keybinds (defaults: WASD move, Space jump, Left Shift sprint).
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 7f;
    [SerializeField] private float sprintMultiplier = 1.7f;
    [SerializeField] private float jumpHeight = 3.0f; // Increased for higher jump
    [SerializeField] private float gravity = -20f;

    [Header("Animation")]
    public Animator animator;
    [SerializeField] private float animBlendSpeed = 8f;

    [Header("Dashing")]
    [SerializeField] private float dashSpeed = 15f; // Increased default to compensate for decay
    [SerializeField] private float dashSpeedDecay = 5f; // How fast the speed decays to zero
    [SerializeField] private float dashAnimSpeed = 1.0f; // Multiplier for the animator
    public bool IsDashing { get; private set; }
    private float currentDashSpeed = 0f;
    private Vector3 dashDirection = Vector3.zero;

    private CharacterController controller;
    public float verticalVelocity;
    private float currentAnimSpeed;
    public bool isDead = false;
    public bool IsSprinting { get; private set; }
    public bool IsMoving { get; private set; }
    private bool isSprintToggled = false;

    [Header("Combat")]
    public float attackCooldown = 0.75f;
    public float lastAttackTime = -10f;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        gameObject.AddComponent<ToolManager>();
        gameObject.AddComponent<FishingSystem>();
        
        // Auto-add health system if not already present
        if (GetComponent<PlayerHealth>() == null)
            gameObject.AddComponent<PlayerHealth>();
    }

    private void Start()
    {
        if (WorldGenerator.Instance != null)
        {
            float y = WorldGenerator.Instance.GetHeightAt(transform.position.x, transform.position.z);
            controller.enabled = false;
            transform.position = new Vector3(transform.position.x, y + 2f, transform.position.z);
            controller.enabled = true;
            
            PlayerHealth ph = GetComponent<PlayerHealth>();
            if (ph != null) ph.spawnPosition = transform.position;
        }
    }

    private bool wasFreeCursorHeld = false;
    private void Update()
    {
        if (!controller.enabled) return;
        
        bool freeCursor = Keybinds.IsHeld(GameAction.FreeCursor);
        if (freeCursor && !wasFreeCursorHeld)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            wasFreeCursorHeld = true;
        }
        else if (!freeCursor && wasFreeCursorHeld)
        {
            if (!Inventory.PanelOpen && !WorldPanels.AnyOpen && Time.timeScale > 0f)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            wasFreeCursorHeld = false;
        }

        if (isDead) return;

        // Attack Trigger on Left Click with Cooldown
        bool attackPressed = false;
        if (!Inventory.PanelOpen && !WorldPanels.AnyOpen && !Keybinds.IsHeld(GameAction.FreeCursor)) {
#if ENABLE_INPUT_SYSTEM
            attackPressed = UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.isPressed;
#else
            attackPressed = Input.GetMouseButton(0);
#endif
        }
        
        if (attackPressed && (BuildingManager.Instance == null || !BuildingManager.Instance.IsInBuildMode))
        {
            if (ToolManager.Instance != null && ToolManager.Instance.CurrentTool == ToolManager.ToolType.FishingRod)
            {
                // Let the new FishingSystem handle the click
                if (UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
                {
                    FishingSystem.Instance?.OnActionPressed();
                }
            }
            else if (Time.time >= lastAttackTime + attackCooldown)
            {
                lastAttackTime = Time.time;
                
                // Manually trigger the tool manager to swing in perfect sync
                if (ToolManager.Instance != null) ToolManager.Instance.TriggerSwing(attackCooldown);
                
                if (animator != null) 
                {
                    // Automatically calculate the exact speed multiplier needed 
                    // so the 1.167s animation finishes in exactly 'attackCooldown' seconds.
                    float baseAnimLength = 1.167f;
                    float requiredSpeed = baseAnimLength / attackCooldown;
                    animator.SetFloat("AttackSpeed", requiredSpeed);
                    
                    animator.Play("Attack", -1, 0f);
                }
            }
        }

        Vector2 input = ReadMoveInput();

        bool dashInput = false;
#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            dashInput = UnityEngine.InputSystem.Keyboard.current.leftCtrlKey.wasPressedThisFrame ||
                        UnityEngine.InputSystem.Keyboard.current.rightCtrlKey.wasPressedThisFrame;
        }
#else
        dashInput = Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.RightControl);
#endif

        IsDashing = animator != null && (animator.GetCurrentAnimatorStateInfo(0).IsName("Dash") || animator.GetNextAnimatorStateInfo(0).IsName("Dash"));

        if (dashInput && !IsDashing && controller.isGrounded)
        {
            currentDashSpeed = dashSpeed;
            if (input.sqrMagnitude < 0.01f)
            {
                dashDirection = transform.forward;
            }
            else
            {
                Vector3 camFwd = Camera.main != null ? Camera.main.transform.forward : transform.forward;
                Vector3 camRt = Camera.main != null ? Camera.main.transform.right : transform.right;
                camFwd.y = 0f; camFwd.Normalize();
                camRt.y = 0f; camRt.Normalize();
                dashDirection = (camRt * input.x + camFwd * input.y).normalized;
                
                // Instantly snap rotation so the forward dash animation matches the physical dash direction
                transform.rotation = Quaternion.LookRotation(dashDirection);
            }
            
            if (animator != null) 
            {
                animator.SetFloat("DashSpeed", dashAnimSpeed);
                animator.SetTrigger("Dash");
            }
        }

        if (IsDashing)
        {
            input = Vector2.zero; // Lock turning and normal movement input while dashing
        }

        if (GameSettings.ToggleSprint)
        {
            if (Keybinds.WasPressed(GameAction.Sprint)) isSprintToggled = !isSprintToggled;
            if (input.sqrMagnitude < 0.01f) isSprintToggled = false;
        }
        else
        {
            isSprintToggled = Keybinds.IsHeld(GameAction.Sprint);
        }
        bool sprinting = isSprintToggled && input.sqrMagnitude > 0.01f;
        IsSprinting = sprinting;
        IsMoving = input.sqrMagnitude > 0.01f;

        // Keep the controller stuck to the ground when grounded
        if (controller.isGrounded && verticalVelocity < 0f)
            verticalVelocity = -2f;

        if (Keybinds.WasPressed(GameAction.Jump) && controller.isGrounded && !Inventory.PanelOpen && !WorldPanels.AnyOpen && !IsDashing)
        {
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            if (animator != null) animator.SetTrigger("Jump");
        }

        verticalVelocity += gravity * Time.deltaTime;

        // Calculate Camera-Relative Movement
        Vector3 camForward = Vector3.forward;
        Vector3 camRight = Vector3.right;
        if (Camera.main != null)
        {
            camForward = Camera.main.transform.forward;
            camRight = Camera.main.transform.right;
        }
        camForward.y = 0f;
        camForward.Normalize();
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDir = camRight * input.x + camForward * input.y;
        Vector3 clampedMove = Vector3.ClampMagnitude(moveDir, 1f);

        // Rotate the body to face the actual direction of movement (Mario/Zelda style)
        if (clampedMove.sqrMagnitude > 0.01f && !IsDashing)
        {
            Quaternion targetRot = Quaternion.LookRotation(clampedMove);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * 15f);
        }

        Vector3 move = clampedMove * (moveSpeed * (sprinting ? sprintMultiplier : 1f));
        if (IsDashing)
        {
            float normalizedTime = 0f;
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Dash"))
                normalizedTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
            else if (animator.GetNextAnimatorStateInfo(0).IsName("Dash"))
                normalizedTime = animator.GetNextAnimatorStateInfo(0).normalizedTime;

            // Exponential ease-out: 1.0 at start, 0.0 at end
            float ease = Mathf.Clamp01(1f - normalizedTime);
            // Squaring it makes the burst harder at the beginning and drops off quicker
            ease = ease * ease; 
            
            // Multiply internal force significantly to ensure long travel distance despite easing
            move = dashDirection * (dashSpeed * 3.5f * ease);
        }
        move.y = verticalVelocity;

        controller.Move(move * Time.deltaTime);

        // Update Animation
        if (animator != null)
        {
            float targetSpeed = clampedMove.magnitude;
            if (sprinting && targetSpeed > 0.01f) 
            {
                targetSpeed = 2f;
            }

            currentAnimSpeed = Mathf.Lerp(currentAnimSpeed, targetSpeed, Time.deltaTime * animBlendSpeed);
            animator.SetFloat("Speed", currentAnimSpeed);
            if (HasAnimParam("IsGrounded")) animator.SetBool("IsGrounded", controller.isGrounded);
            if (HasAnimParam("VerticalVelocity")) animator.SetFloat("VerticalVelocity", verticalVelocity);
        }
    }

    private Vector2 ReadMoveInput()
    {
        if (Inventory.PanelOpen || WorldPanels.AnyOpen) return Vector2.zero;
        float x = (Keybinds.IsHeld(GameAction.Right) ? 1f : 0f) - (Keybinds.IsHeld(GameAction.Left) ? 1f : 0f);
        float y = (Keybinds.IsHeld(GameAction.Forward) ? 1f : 0f) - (Keybinds.IsHeld(GameAction.Backward) ? 1f : 0f);
        return new Vector2(x, y);
    }

    public void Die()
    {
        if (isDead) return;
        isDead = true;
        if (animator != null) animator.SetTrigger("Death");
    }

    private System.Collections.Generic.HashSet<string> animParamCache;

    // Utility: safely check if an animator has a given parameter without allocating arrays
    private bool HasAnimParam(string name)
    {
        if (animator == null) return false;
        if (animParamCache == null)
        {
            animParamCache = new System.Collections.Generic.HashSet<string>();
            foreach (var p in animator.parameters)
                animParamCache.Add(p.name);
        }
        return animParamCache.Contains(name);
    }
}








