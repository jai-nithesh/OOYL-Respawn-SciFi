using UnityEngine;
using TMPro;

/// <summary>
/// EnemyAI – Geometric NPC Hunter with Health, Animation, and Detection UI.
/// ──────────────────────────────────────────────────────────────────────────────
/// COLLISION ARCHITECTURE (v2 – Two-Collider System):
///   Damage detection is now handled by DamageReceiver.cs on the "Hitbox" child.
///   OnCollisionEnter2D is REMOVED from this script.
///   The enemy root has a solid BoxCollider2D at the feet (layer: CharacterFeet)
///   for environment blocking only. The Hitbox child has a trigger BoxCollider2D
///   (layer: CharacterBody) for bullet detection.
///
/// HIERARCHY:
///   Enemy (Layer: CharacterFeet) – this script, Rigidbody2D, BoxCollider2D (feet)
///   ├── WeaponPivot (empty Transform – rotates to aim)
///   │   └── FirePoint (empty Transform – bullet spawn)
///   ├── Hitbox (Layer: CharacterBody) – BoxCollider2D (Is Trigger), DamageReceiver.cs
///   └── DetectionCanvas (World Space Canvas)
///       └── DetectionText (TextMeshProUGUI)
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

    // ───────── Detection UI ─────────
    [Header("Detection UI")]
    [Tooltip("World Space Canvas child (auto-found if null).")]
    public Canvas detectionCanvas;

    [Tooltip("The TextMeshProUGUI component (auto-found if null).")]
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

    // ── Cached Animator Parameter Hashes ──
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

        // Auto-find detection UI if not assigned
        if (detectionCanvas == null)
        {
            detectionCanvas = GetComponentInChildren<Canvas>(true);
        }
        if (detectionText == null && detectionCanvas != null)
        {
            detectionText = detectionCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
        }

        // Hide detection UI at start
        if (detectionCanvas != null)
        {
            detectionCanvas.gameObject.SetActive(false);
        }
    }

    private void Start()
    {
        // Find the Player (persistent under Core, so FindWithTag works)
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
        if (playerTransform == null)
        {
            // Retry finding the player (it may have loaded after this enemy)
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null) playerTransform = playerObj.transform;
            else return;
        }

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

        // ── Combat Logic ──
        if (hasDetectedPlayer && isPlayerInRange)
        {
            AimWeaponPivotAtPlayer();

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

        if (hasDetectedPlayer && isPlayerInRange)
        {
            float distToPlayer = Vector2.Distance(transform.position, playerTransform.position);

            if (distToPlayer > stoppingDistance)
            {
                Vector2 direction = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
                rb.linearVelocity = direction * moveSpeed;
            }
            else
            {
                rb.linearVelocity = Vector2.zero;
            }
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ──────────────────────────────────────────────────
    // Detection UI
    // ──────────────────────────────────────────────────
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
    private void AimWeaponPivotAtPlayer()
    {
        if (weaponPivot == null || playerTransform == null) return;

        Vector2 direction = (Vector2)(playerTransform.position - transform.position);
        float angleRad = Mathf.Atan2(direction.y, direction.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;

        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    // ──────────────────────────────────────────────────
    // Weapon Firing – Mirrors PlayerController.Fire()
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Instantiates the bullet prefab at the FirePoint.
    /// Tags it "EnemyBullet" and uses Physics2D.IgnoreCollision to prevent
    /// the bullet from hitting this enemy's own colliders (feet + hitbox).
    /// </summary>
    private void Fire()
    {
        if (bulletPrefab == null || firePoint == null) return;

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // Tag the bullet
        bullet.tag = "EnemyBullet";
        int bLayer = LayerMask.NameToLayer("Bullet");
        if (bLayer != -1) bullet.layer = bLayer;

        // Set ownership on the RicochetBullet component
        RicochetBullet ricochet = bullet.GetComponent<RicochetBullet>();
        if (ricochet != null)
        {
            ricochet.ownerTag = gameObject.tag;
        }

        // ── Physics2D.IgnoreCollision ──
        // Prevent the bullet from colliding with THIS enemy's feet collider
        // and hitbox trigger. This is instance-level, so other enemies can
        // still be hit by this bullet.
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] bulletColliders = bullet.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D mc in myColliders)
        {
            foreach (Collider2D bc in bulletColliders)
            {
                Physics2D.IgnoreCollision(mc, bc);
            }
        }

        // Set animation shoot trigger
        if (anim != null)
        {
            anim.SetTrigger(HashShootTrigger);
        }
    }

    // ──────────────────────────────────────────────────
    // Health & Damage System
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Direct collision fallback: if a player bullet hits the enemy root's collider.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("PlayerBullet"))
        {
            TakeDamage(1);
            Destroy(collision.gameObject);
        }
    }

    /// <summary>
    /// Direct trigger fallback: if a player bullet trigger enters this root.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("PlayerBullet"))
        {
            TakeDamage(1);
            Destroy(other.gameObject);
        }
    }

    /// <summary>
    /// Called when a hostile bullet deals damage.
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

    private void Die()
    {
        Debug.Log($"[EnemyAI] {gameObject.name} destroyed!");
        Destroy(gameObject);
    }

    // ──────────────────────────────────────────────────
    // Animation Controller Integration
    // ──────────────────────────────────────────────────
    private void UpdateAnimations()
    {
        if (anim == null) return;

        Vector2 velocity = rb.linearVelocity;
        bool isMoving = velocity.sqrMagnitude > 0.01f;

        if (isMoving)
        {
            Vector2 moveDir = velocity.normalized;
            anim.SetFloat(HashMoveX, moveDir.x);
            anim.SetFloat(HashMoveY, moveDir.y);
        }

        anim.SetBool(HashIsMoving, isMoving);

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
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    public int CurrentHealth => currentHealth;
    public bool HasDetectedPlayer => hasDetectedPlayer;
}
