using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Attach to the Player. Handles HP, the on-screen health bar, damage screen flash, and death/respawn.
/// Call PlayerHealth.Instance.TakeDamage(amount) from any enemy or hazard.
/// </summary>
public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    [Header("Health")]
    [SerializeField] public float maxHealth = 100f;
    [Header("Survival")]
    public float maxHunger = 100f;
    public float hungerDrainRate = 1.0f;
    public float CurrentHunger { get; private set; }
    [SerializeField] private float respawnDelay = 3f;

    [Header("Invincibility Frames")]
    [Tooltip("Seconds of invulnerability after being hit, to prevent getting 1-shot by fast enemies.")]
    [SerializeField] private float iFrameDuration = 0.5f;

    // Ã¢â€â‚¬Ã¢â€â‚¬ runtime state Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    public float CurrentHealth { get; private set; }
    private bool isDead;
    private float iFrameTimer;

    // Ã¢â€â‚¬Ã¢â€â‚¬ cached refs Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    private PlayerController playerController;
    public Vector3 spawnPosition;

    // Ã¢â€â‚¬Ã¢â€â‚¬ UI Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    private RectTransform healthFill;   // the red bar
    private RectTransform healthTrail;  // the slow-drain white trail
    private RectTransform hungerFill;
    private Image flashImage;           // full-screen red flash on hit
    private float trailHealth;
    private float trailTimer;
    private float starvationTimer = 0f;

    // bar target width in pixels at full health (set in BuildUI)
    private float barWidth;

    // Ã¢â€â‚¬Ã¢â€â‚¬ constants Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    private const float TrailDelay   = 0.6f;   // seconds before trail starts draining
    private const float TrailSpeed   = 60f;    // pixels/second drain speed
    private const float FlashFadeSpeed = 4f;

    // Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬
    private void Awake()
    {
        if (Instance != null && Instance != this) { gameObject.SetActive(false); Destroy(gameObject); return; }
        Instance = this;

        playerController = GetComponent<PlayerController>();
        CurrentHealth    = maxHealth;
        CurrentHunger    = maxHunger;
        trailHealth      = maxHealth;
        spawnPosition    = transform.position;
    }

    private void Start()
    {
        BuildUI();
    }

    private void Update()
    {
        if (iFrameTimer > 0f) iFrameTimer -= Time.deltaTime;

        // Trail drain
        if (trailTimer > 0f)
        {
            trailTimer -= Time.deltaTime;
        }
        else
        {
            trailHealth = Mathf.MoveTowards(trailHealth, CurrentHealth, TrailSpeed * Time.deltaTime);
        }

        RefreshBar();

        PlayerController pc = GetComponent<PlayerController>();
        float currentDrain = 0.2f * hungerDrainRate; // Idle
        if (pc != null) {
            if (pc.IsSprinting) currentDrain = 1.0f * hungerDrainRate;
            else if (pc.IsMoving) currentDrain = 0.4f * hungerDrainRate;
        }

        CurrentHunger -= currentDrain * Time.deltaTime;
        
        if (CurrentHunger <= 0) {
            CurrentHunger = 0;
            starvationTimer -= Time.deltaTime;
            if (starvationTimer <= 0f) {
                TakeDamage(10f); // Hit for exactly 10 damage
                starvationTimer = 2f; // Wait 2 seconds before next hit
            }
        } else {
            starvationTimer = 2f; // Give a 2 second grace period when hitting 0
            if (CurrentHunger > maxHunger * 0.8f && CurrentHealth < maxHealth) {
                CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + Time.deltaTime * 3f);
                trailHealth = Mathf.Max(trailHealth, CurrentHealth);
            }
        }
        RefreshBar();

        // Fade the damage flash
        if (flashImage != null && flashImage.color.a > 0f)
        {
            Color c = flashImage.color;
            c.a = Mathf.MoveTowards(c.a, 0f, FlashFadeSpeed * Time.deltaTime);
            flashImage.color = c;
        }
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Public API Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    public void Eat(float foodValue, float healValue = 0f)
    {
        if (isDead) return;
        CurrentHunger = Mathf.Min(maxHunger, CurrentHunger + foodValue);
        if (healValue > 0) Heal(healValue);
        RefreshBar();
    }

    public void TakeDamage(float amount)
    {
        if (isDead || iFrameTimer > 0f) return;

        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        iFrameTimer   = iFrameDuration;
        trailTimer    = TrailDelay;

        // Flash the screen red briefly
        // Red flash disabled by user request
        // if (flashImage != null)
        //     flashImage.color = new Color(1f, 0f, 0f, 0.35f);

        if (CurrentHealth <= 0f) Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + amount);
        trailHealth   = Mathf.Max(trailHealth, CurrentHealth);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ Internal Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (playerController != null) playerController.Die();

        StartCoroutine(RespawnAfterDelay());
    }

    private IEnumerator RespawnAfterDelay()
    {
        // Wait for the death animation to finish playing before fading the screen
        yield return new WaitForSecondsRealtime(respawnDelay);

        // Full-screen fade to black (reuse flash image with black)
        if (flashImage != null)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.unscaledDeltaTime * 1.5f;
                flashImage.color = Color.Lerp(new Color(0, 0, 0, 0), Color.black, t);
                yield return null;
            }
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // Respawn
        isDead        = false;
        CurrentHealth = maxHealth;
        trailHealth   = maxHealth;

        if (playerController != null)
        {
            playerController.isDead = false;
            Animator anim = playerController.GetComponentInChildren<Animator>();
            if (anim != null) { anim.ResetTrigger("Death"); anim.Play("Idle"); }
            CharacterController cc = playerController.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = spawnPosition;
            if (cc != null) cc.enabled = true;
        }

        // Fade back in
        if (flashImage != null)
        {
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.unscaledDeltaTime * 1.5f;
                flashImage.color = Color.Lerp(new Color(0, 0, 0, 0), Color.black, t);
                yield return null;
            }
            flashImage.color = Color.clear;
        }
    }

    private void RefreshBar()
    {
        if (healthFill   == null) return;
        healthFill.sizeDelta   = new Vector2(barWidth * (CurrentHealth / maxHealth), healthFill.sizeDelta.y);
        if (hungerFill != null) hungerFill.sizeDelta = new Vector2(barWidth * (CurrentHunger / maxHunger), hungerFill.sizeDelta.y);
        healthTrail.sizeDelta  = new Vector2(barWidth * (trailHealth   / maxHealth), healthTrail.sizeDelta.y);
    }

    // Ã¢â€â‚¬Ã¢â€â‚¬ UI Construction Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬Ã¢â€â‚¬

    private void BuildUI()
    {
        // Find (or create) the HUD canvas that PlayerController already made
        Canvas hudCanvas = null;
        foreach (var c in FindObjectsByType<Canvas>())
        {
            if (c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.name == "HUD Canvas")
            {
                hudCanvas = c;
                break;
            }
        }

        if (hudCanvas == null)
        {
            GameObject canvasObj = new GameObject("HUD Canvas");
            hudCanvas = canvasObj.AddComponent<Canvas>();
            hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            hudCanvas.sortingOrder = 5;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        Transform root = hudCanvas.transform;

        // Ã¢â€â‚¬Ã¢â€â‚¬ full-screen flash overlay (also used for death fade) Ã¢â€â‚¬Ã¢â€â‚¬
        GameObject flashObj = new GameObject("DamageFlash");
        flashObj.transform.SetParent(root, false);
        flashImage = flashObj.AddComponent<Image>();
        flashImage.color = Color.clear;
        flashImage.raycastTarget = false;
        RectTransform frt = flashObj.GetComponent<RectTransform>();
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = frt.offsetMax = Vector2.zero;

        // Ã¢â€â‚¬Ã¢â€â‚¬ health bar container (bottom-left) Ã¢â€â‚¬Ã¢â€â‚¬
        barWidth = 240f;
        float barHeight = 20f;
        float margin    = 28f;

        GameObject barRoot = new GameObject("HealthBar");
        barRoot.transform.SetParent(root, false);
        RectTransform barRootRt = barRoot.AddComponent<RectTransform>();
        barRootRt.anchorMin = barRootRt.anchorMax = new Vector2(0f, 0f);
        barRootRt.pivot     = new Vector2(0f, 0f);
        barRootRt.anchoredPosition = new Vector2(margin, margin);
        barRootRt.sizeDelta = new Vector2(barWidth, barHeight);

        // Background
        GameObject bg = new GameObject("BG");
        bg.transform.SetParent(barRoot.transform, false);
        Image bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;

        // Trail (white, drawn behind the red fill)
        GameObject trail = new GameObject("HealthTrail");
        trail.transform.SetParent(barRoot.transform, false);
        Image trailImg = trail.AddComponent<Image>();
        trailImg.color = new Color(1f, 1f, 1f, 0.55f);
        healthTrail = trail.GetComponent<RectTransform>();
        healthTrail.anchorMin = Vector2.zero; healthTrail.anchorMax = new Vector2(0f, 1f);
        healthTrail.pivot     = new Vector2(0f, 0.5f);
        healthTrail.anchoredPosition = Vector2.zero;
        healthTrail.sizeDelta = new Vector2(barWidth, 0f);

        // Red fill
        GameObject fill = new GameObject("HealthFill");
        fill.transform.SetParent(barRoot.transform, false);
        Image fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.85f, 0.15f, 0.15f, 1f);
        healthFill = fill.GetComponent<RectTransform>();
        healthFill.anchorMin = Vector2.zero; healthFill.anchorMax = new Vector2(0f, 1f);
        healthFill.pivot     = new Vector2(0f, 0.5f);
        healthFill.anchoredPosition = Vector2.zero;
        healthFill.sizeDelta = new Vector2(barWidth, 0f);

        // Heart icon label (text heart Ã¢â„¢Â¥)
        GameObject label = new GameObject("Label");
        label.transform.SetParent(barRoot.transform, false);
        Text labelTxt = label.AddComponent<Text>();
        labelTxt.text      = "Ã¢â„¢Â¥";
        labelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        labelTxt.fontSize  = 18;
        labelTxt.color     = new Color(1f, 0.3f, 0.3f, 1f);
        labelTxt.alignment = TextAnchor.MiddleLeft;
        RectTransform labelRt = label.GetComponent<RectTransform>();
        labelRt.anchorMin = Vector2.zero; labelRt.anchorMax = Vector2.one;
        labelRt.offsetMin = new Vector2(4f, 0f);
        labelRt.offsetMax = Vector2.zero;
        labelRt.sizeDelta = new Vector2(barWidth, 0f);

                // -- hunger bar container (above health) --
        GameObject hRoot = new GameObject("HungerBar");
        hRoot.transform.SetParent(root, false);
        RectTransform hRootRt = hRoot.AddComponent<RectTransform>();
        hRootRt.anchorMin = hRootRt.anchorMax = new Vector2(0f, 0f);
        hRootRt.pivot     = new Vector2(0f, 0f);
        hRootRt.anchoredPosition = new Vector2(margin, margin + 25f);
        hRootRt.sizeDelta = new Vector2(barWidth, barHeight);

        GameObject hbg = new GameObject("HBG");
        hbg.transform.SetParent(hRoot.transform, false);
        Image hbgImg = hbg.AddComponent<Image>();
        hbgImg.color = new Color(0f, 0f, 0f, 0.55f);
        RectTransform hbgRt = hbg.GetComponent<RectTransform>();
        hbgRt.anchorMin = Vector2.zero; hbgRt.anchorMax = Vector2.one;
        hbgRt.offsetMin = hbgRt.offsetMax = Vector2.zero;

        GameObject hfill = new GameObject("HungerFill");
        hfill.transform.SetParent(hRoot.transform, false);
        Image hfillImg = hfill.AddComponent<Image>();
        hfillImg.color = new Color(0.9f, 0.7f, 0.1f, 1f);
        hungerFill = hfill.GetComponent<RectTransform>();
        hungerFill.anchorMin = Vector2.zero; hungerFill.anchorMax = new Vector2(0f, 1f);
        hungerFill.pivot     = new Vector2(0f, 0.5f);
        hungerFill.anchoredPosition = Vector2.zero;
        hungerFill.sizeDelta = new Vector2(barWidth, 0f);

        GameObject hlabel = new GameObject("HLabel");
        hlabel.transform.SetParent(hRoot.transform, false);
        Text hlabelTxt = hlabel.AddComponent<Text>();
        hlabelTxt.text      = "Food";
        hlabelTxt.font      = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        hlabelTxt.fontSize  = 14;
        hlabelTxt.color     = new Color(1f, 0.9f, 0.4f, 1f);
        hlabelTxt.alignment = TextAnchor.MiddleLeft;
        RectTransform hlabelRt = hlabel.GetComponent<RectTransform>();
        hlabelRt.anchorMin = Vector2.zero; hlabelRt.anchorMax = Vector2.one;
        hlabelRt.offsetMin = new Vector2(5f, 0f);
        hlabelRt.offsetMax = Vector2.zero;
        hlabelRt.sizeDelta = new Vector2(barWidth, 0f);

        RefreshBar();
    }
}







