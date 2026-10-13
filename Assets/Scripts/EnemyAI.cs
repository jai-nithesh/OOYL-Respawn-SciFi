using UnityEngine;
using TMPro;

/// <summary>
/// EnemyAI – Geometric NPC Hunter with Health, Animation, and Detection UI.
/// ──────────────────────────────────────────────────────────────────────────────
/// PURPOSE:
///   A fully-featured enemy NPC that:
///   1. Detects the player within a configurable radius.
///   2. Shows a World Space "Hey, you there!" text popup on detection.
///   3. Hunts the player using Rigidbody2D velocity (matching our physics standard).
///   4. Aims using Mathf.Atan2 – the EXACT same geometric system as the player.
///   5. Fires ricochet bullets from a WeaponPivot/FirePoint (mirroring PlayerController).
///   6. Has a configurable health pool and takes damage from player bullets.
///   7. Drives an Animator with movement & aiming blend trees.
///
/// TAGGING SYSTEM (CRITICAL):
///   • Player bullets must be tagged:   "PlayerBullet"
///   • Enemy bullets must be tagged:    "EnemyBullet"
///   • The player GameObject must be tagged: "Player"
///   The NPC only takes damage from "PlayerBullet"-tagged projectiles.
///   This prevents self-damage from its own ricocheting shots.
///
/// HIERARCHY SETUP:
///   Enemy (this script, Rigidbody2D, Collider2D, Animator, SpriteRenderer)
///   ├── WeaponPivot (empty Transform – rotates to aim)
///   │   └── FirePoint (empty Transform – offset from center, bullet spawn point)
///   └── DetectionCanvas (World Space Canvas)
///       └── DetectionText (TextMeshProUGUI – "Hey, you there!")
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    // ───────── Detection ─────────
    [Header("Detection")]
    [Tooltip("Distance in units at which the NPC spots the player.")]
    public float detectionRadius = 8f;

    [Tooltip("Tag on the Player GameObject.")]
    public string playerTag = "Player";

    // ───────── Movement ─────────
    [Header("Movement")]
    [Tooltip("Movement speed in units/sec when chasing the player.")]
    public float moveSpeed = 4f;

    [Tooltip("Minimum distance to maintain from the player (prevents stacking).")]
    public float stoppingDistance = 2.5f;

    // ───────── Aiming & Firing ─────────
    [Header("Aiming & Firing")]
    [Tooltip("Child Transform that rotates to aim at the player (mirrors Player's WeaponPivot).")]
    public Transform weaponPivot;

    [Tooltip("Child of WeaponPivot where bullets spawn (mirrors Player's FirePoint).")]
    public Transform firePoint;

    [Tooltip("Bullet prefab to instantiate. Assign the RED variant of RicochetBullet.")]
    public GameObject bulletPrefab;

    [Tooltip("Minimum seconds between shots.")]
    public float fireRate = 0.8f;

    // ───────── Health ─────────
    [Header("Health")]
    [Tooltip("Number of hits the NPC can take before being destroyed.")]
    public int maxHealth = 5;

    [Tooltip("Tag on the player's bullets. NPC ONLY takes damage from this tag.")]
    public string playerBulletTag = "PlayerBullet";

    // ───────── Detection UI ─────────
    [Header("Detection UI")]
    [Tooltip("World Space Canvas child (auto-found if null). Must have a TextMeshProUGUI child.")]
    public Canvas detectionCanvas;

    [Tooltip("The TextMeshProUGUI component that shows 'Hey, you there!' (auto-found if null).")]
    public TextMeshProUGUI detectionText;

    [Tooltip("The message displayed when the player is first detected.")]
    public string detectionMessage = "Hey, you there!";

    [Tooltip("Seconds the detection message stays visible before hiding.")]
    public float detectionMessageDuration = 3f;

    // ───────── Private State ─────────
    private Rigidbody2D rb;
    private Animator anim;
    private Transform playerTransform;
    private int currentHealth;
    private float nextFireTime;
    private bool hasDetectedPlayer = false;
    private bool isPlayerInRange = false;
    private float detectionMessageTimer = 0f;

    // ── Cached Animator Parameter Hashes (performance) ──
    private static readonly int HashMoveX = Animator.StringToHash("MoveX");
    private static readonly int HashMoveY = Animator.StringToHash("MoveY");
    private static readonly int HashAimX = Animator.StringToHash("AimX");
    private static readonly int HashAimY = Animator.StringToHash("AimY");
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashShootTrigger = Animator.StringToHash("ShootTrigger");

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        currentHealth = maxHealth;

        // ── Auto-find detection UI if not assigned ──
        if (detectionCanvas == null)
        {
            detectionCanvas = GetComponentInChildren<Canvas>(true);
        }
        if (detectionText == null && detectionCanvas != null)
        {
            detectionText = detectionCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // ── Hide detection UI at start ──
        if (detectionCanvas != null)
        {
            detectionCanvas.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        // ── Find the Player ──
        GameObject playerObj = GameObject.FindWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"[EnemyAI] No GameObject tagged '{playerTag}' found in scene.");
        }
    }

    private void Update()
    {
        if (playerTransform == null) return;

        // ── Distance Check ──
        float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);
        isPlayerInRange = distToPlayer <= detectionRadius;

        // ── First Detection Event ──
        if (isPlayerInRange && !hasDetectedPlayer)
        {
            hasDetectedPlayer = true;
            OnPlayerDetected();
        }

        // ── Detection Message Timer ──
        if (detectionMessageTimer > 0f)
        {
            detectionMessageTimer -= Time.deltaTime;
            if (detectionMessageTimer <= 0f)
            {
                HideDetectionMessage();
            }
        }

        // ── Combat Logic (only when player is detected AND in range) ──
        if (hasDetectedPlayer && isPlayerInRange)
        {
            // Aim weapon at player (geometric Atan2 – identical to PlayerController)
            AimWeaponPivotAtPlayer();

            // Fire at the player
            if (Time.unscaledTime >= nextFireTime)
            {
                Fire();
                nextFireTime = Time.unscaledTime + fireRate;
            }
        }

        // ── Update Animations ──
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (playerTransform == null) return;

        // ── Hunting Movement (only when detected and outside stopping distance) ──
        if (hasDetectedPlayer && isPlayerInRange)
        {
            float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distToPlayer > stoppingDistance)
            {
                // Move towards the player using Rigidbody2D velocity
                // (matching our physics standard from PlayerController)
                Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
                rb.linearVelocity = direction * moveSpeed;
            }
            else
            {
                // Within stopping distance – halt movement but keep aiming
                rb.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            // Not detected or out of range – idle
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ──────────────────────────────────────────────────
    // Detection UI
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Called once when the player first enters the detection radius.
    /// Shows the World Space Canvas text popup.
    /// </summary>
    private void OnPlayerDetected()
    {
        if (detectionCanvas != null)
        {
            detectionCanvas.gameObject.SetActive(true);

            if (detectionText != null)
            {
                detectionText.text = detectionMessage;
            }

            detectionMessageTimer = detectionMessageDuration;
        }
    }

    /// <summary>Hides the detection message after the timer expires.</summary>
    private void HideDetectionMessage()
    {
        if (detectionCanvas != null)
        {
            detectionCanvas.gameObject.SetActive(false);
        }
    }

    // ──────────────────────────────────────────────────
    // Geometric Aiming (Atan2) – Mirrors PlayerController
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Calculates the exact angle between the NPC and the player
    /// using Mathf.Atan2 and rotates the WeaponPivot to face the player.
    /// This is the EXACT SAME mathematical system used by the PlayerController
    /// for mouse aiming, but targeting playerTransform instead of the mouse cursor.
    /// </summary>
    private void AimWeaponPivotAtPlayer()
    {
        if (weaponPivot == null || playerTransform == null) return;

        // Direction vector from NPC to player
        Vector2 direction = (Vector2)(playerTransform.position - transform.position);

        // Calculate angle in degrees using Atan2 (identical to PlayerController line 169-170)
        float angleRad = Mathf.Atan2(direction.y, direction.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;

        // Apply rotation to WeaponPivot (rotating around Z axis in 2D)
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    // ──────────────────────────────────────────────────
    // Weapon Firing – Mirrors PlayerController.Fire()
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Instantiates the bullet prefab at the FirePoint with matching rotation.
    /// Mirrors PlayerController.Fire() exactly (line 182-186).
    /// The bullet prefab should be a RED variant tagged "EnemyBullet".
    /// </summary>
    private void Fire()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // ── Set animation shoot trigger ──
        if (anim != null)
        {
            anim.SetTrigger(HashShootTrigger);
        }

        // Safety: Ensure the bullet is tagged correctly so it doesn't damage this NPC
        // (The prefab should already be tagged "EnemyBullet", but we enforce it here)
        bullet.tag = "EnemyBullet";
    }

    // ──────────────────────────────────────────────────
    // Health & Damage System
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Called when a physics collision occurs.
    /// Only takes damage from projectiles tagged "PlayerBullet".
    /// Ignores its own ricocheting shots (tagged "EnemyBullet").
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag(playerBulletTag))
        {
            // Destroy the bullet on impact
            Destroy(collision.gameObject);

            // Apply damage
            TakeDamage(1);
        }
    }

    /// <summary>
    /// Reduces health by the given amount. Destroys the NPC when health reaches 0.
    /// </summary>
    public void TakeDamage(int amount)
    {
        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"[EnemyAI] {gameObject.name} took {amount} damage. Health: {currentHealth}/{maxHealth}");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Handles NPC death. Destroys the GameObject.
    /// Extend this to add death VFX, sounds, score, or loot drops.
    /// </summary>
    private void Die()
    {
        Debug.Log($"[EnemyAI] {gameObject.name} destroyed!");

        // TODO: Spawn death VFX / particles here
        // TODO: Add score to GameManager

        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────────
    // Animation Controller Integration
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Feeds movement and aiming vectors into the Animator to drive
    /// Walk and Shoot Blend Trees.
    ///
    /// Parameters fed to the Animator:
    ///   • MoveX, MoveY  – normalized movement direction (for Walk Blend Tree)
    ///   • AimX, AimY    – normalized aim direction toward player (for Shoot Blend Tree)
    ///   • IsMoving       – bool, true when the NPC is walking
    ///   • ShootTrigger   – trigger, set in Fire() to play shoot animation
    /// </summary>
    private void UpdateAnimations()
    {
        if (anim == null) return;

        // ── Movement Vector ──
        Vector2 velocity = rb.linearVelocity;
        bool isMoving = velocity.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            Vector2 moveDir = velocity.normalized;
            anim.SetFloat(HashMoveX, moveDir.x);
            anim.SetFloat(HashMoveY, moveDir.y);
        }

        anim.SetBool(HashIsMoving, isMoving);

        // ── Aiming Vector (toward player) ──
        if (playerTransform != null && hasDetectedPlayer)
        {
            Vector2 aimDir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            anim.SetFloat(HashAimX, aimDir.x);
            anim.SetFloat(HashAimY, aimDir.y);
        }
    }

    // ──────────────────────────────────────────────────
    // Editor Gizmos
    // ──────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        // Detection radius (yellow)
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        // Stopping distance (red)
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    /// <summary>Current health of this NPC.</summary>
    public int CurrentHealth => currentHealth;

    /// <summary>True if the NPC has ever detected the player.</summary>
    public bool HasDetectedPlayer => hasDetectedPlayer;
}
