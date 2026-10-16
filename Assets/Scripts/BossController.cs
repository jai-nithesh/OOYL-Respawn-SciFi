using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// BossController – The Dark Warlord: Multi-phase final boss for Scene 6.
/// ──────────────────────────────────────────────────────────────────────────────
/// HIERARCHY:
///   DarkWarlord (Tag: Enemy, Layer: CharacterFeet) – this script, Rigidbody2D, BoxCollider2D (feet)
///   ├── WeaponPivot (empty Transform – rotates to aim)
///   │   └── FirePoint (empty Transform – bullet spawn)
///   ├── Hitbox (Layer: CharacterBody) – BoxCollider2D (Is Trigger), DamageReceiver.cs
///   └── BossCanvas (World Space Canvas)
///       └── BossDialogueText (TextMeshProUGUI)
///
/// DAMAGE: Uses tag "Enemy" so existing RicochetBullet/DamageReceiver pipeline
///         calls TakeDamage() via EnemyAI-style detection. This script has its
///         own TakeDamage override, and we also expose it for DamageReceiver.
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class BossController : MonoBehaviour
{
    // ───────── Health ─────────
    [Header("Health")]
    public int maxHealth = 25;

    // ───────── Movement ─────────
    [Header("Movement")]
    public float moveSpeed = 3.5f;
    public float enragedMoveSpeed = 5.5f;
    public float stoppingDistance = 3f;
    public float strafeRadius = 4f;

    // ───────── Detection ─────────
    [Header("Detection")]
    public float detectionRadius = 15f;
    public string playerTag = "Player";

    // ───────── Firing ─────────
    [Header("Firing")]
    public Transform weaponPivot;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float fireRate = 1.2f;
    public float enragedFireRate = 0.6f;

    // ───────── Fan Spread Settings ─────────
    [Header("Fan Spread")]
    [Tooltip("Phase 1: 3-way fan angles in degrees.")]
    public float[] phase1FanAngles = { -15f, 0f, 15f };

    [Tooltip("Phase 2: 5-way fan angles in degrees.")]
    public float[] phase2FanAngles = { -30f, -15f, 0f, 15f, 30f };

    [Tooltip("Phase 2: 8-way radial nova interval (seconds between novas).")]
    public float novaInterval = 4f;

    // ───────── Dialogue / UI ─────────
    [Header("World Space UI")]
    public Canvas bossCanvas;
    public TextMeshProUGUI bossDialogueText;

    // ───────── Visual Effects ─────────
    [Header("Enrage Visual")]
    public Color enrageColor = new Color(1f, 0.3f, 0.3f, 1f);

    // ───────── Princess Reference ─────────
    [Header("Princess")]
    [Tooltip("Assign the PrincessController in Scene 6. Null-safe if not assigned.")]
    public PrincessController princess;

    // ───────── Private State ─────────
    private Rigidbody2D rb;
    private Animator anim;
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;
    private int currentHealth;
    private float nextFireTime;
    private float nextNovaTime;
    private bool isDead = false;
    private bool isEnraged = false;
    private bool hasDetectedPlayer = false;
    private bool useNovaNext = false; // alternates fan vs nova in phase 2

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
        spriteRenderer = GetComponent<SpriteRenderer>();
        currentHealth = maxHealth;

        // Auto-find World Space canvas
        if (bossCanvas == null)
            bossCanvas = GetComponentInChildren<Canvas>(true);
        if (bossDialogueText == null && bossCanvas != null)
            bossDialogueText = bossCanvas.GetComponentInChildren<TextMeshProUGUI>(true);

        if (bossCanvas != null)
            bossCanvas.gameObject.SetActive(false);
    }

    private void Start()
    {
        GameObject playerObj = GameObject.FindWithTag(playerTag);
        if (playerObj != null) playerTransform = playerObj.transform;

        // Auto-find princess if not assigned
        if (princess == null)
            princess = FindAnyObjectByType<PrincessController>();

        // Register boss health bar
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateBossHealthUI(currentHealth, maxHealth);
        }

        nextFireTime = Time.time + fireRate;
        nextNovaTime = Time.time + novaInterval;
    }

    private void Update()
    {
        if (isDead) return;

        // Retry finding player
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null) playerTransform = playerObj.transform;
            else return;
        }

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        // Detection
        if (dist <= detectionRadius && !hasDetectedPlayer)
        {
            hasDetectedPlayer = true;
            ShowDialogue("Your geometry cannot save her, Rebel!", 4f);
        }

        if (!hasDetectedPlayer) return;

        // ── Phase Check ──
        float healthPercent = (float)currentHealth / maxHealth;
        if (healthPercent <= 0.5f && !isEnraged)
        {
            EnterPhase2();
        }

        // ── Aiming ──
        AimAtPlayer();

        // ── Firing ──
        float currentFireRate = isEnraged ? enragedFireRate : fireRate;
        if (Time.time >= nextFireTime)
        {
            if (isEnraged && useNovaNext && Time.time >= nextNovaTime)
            {
                FireRadialNova();
                nextNovaTime = Time.time + novaInterval;
                useNovaNext = false;
            }
            else
            {
                FireFanSpread(isEnraged ? phase2FanAngles : phase1FanAngles);
                if (isEnraged) useNovaNext = true;
            }
            nextFireTime = Time.time + currentFireRate;
        }

        // ── Animations ──
        UpdateAnimations();
    }

    private void FixedUpdate()
    {
        if (isDead || playerTransform == null || !hasDetectedPlayer) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);
        float currentSpeed = isEnraged ? enragedMoveSpeed : moveSpeed;

        if (dist > stoppingDistance)
        {
            // Chase player
            Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * currentSpeed;
        }
        else
        {
            // Strafe around the player
            Vector2 toPlayer = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            Vector2 strafeDir = new Vector2(-toPlayer.y, toPlayer.x); // perpendicular
            rb.linearVelocity = strafeDir * currentSpeed * 0.7f;
        }
    }

    // ──────────────────────────────────────────────────
    // Aiming
    // ──────────────────────────────────────────────────
    private void AimAtPlayer()
    {
        if (weaponPivot == null || playerTransform == null) return;

        Vector2 direction = (Vector2)(playerTransform.position - transform.position);
        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    // ──────────────────────────────────────────────────
    // Firing Patterns
    // ──────────────────────────────────────────────────
    /// <summary>Fires a fan spread of bouncing EnemyBullet projectiles.</summary>
    private void FireFanSpread(float[] angles)
    {
        if (bulletPrefab == null || firePoint == null) return;

        Vector2 baseDir = (playerTransform != null)
            ? ((Vector2)(playerTransform.position - transform.position)).normalized
            : (Vector2)transform.right;

        float baseAngle = Mathf.Atan2(baseDir.y, baseDir.x) * Mathf.Rad2Deg;

        foreach (float offset in angles)
        {
            float angle = baseAngle + offset;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            SpawnBullet(rot);
        }

        if (anim != null) anim.SetTrigger(HashShootTrigger);
    }

    /// <summary>Fires an 8-way radial ricochet nova (45° intervals).</summary>
    private void FireRadialNova()
    {
        if (bulletPrefab == null || firePoint == null) return;

        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f;
            Quaternion rot = Quaternion.Euler(0f, 0f, angle);
            SpawnBullet(rot);
        }

        // ── Camera Shake: Medium shake on Radial Nova ──
        CameraFollow.Shake(0.15f, 0.2f);

        if (anim != null) anim.SetTrigger(HashShootTrigger);
    }

    /// <summary>Spawns a single EnemyBullet with specified rotation.</summary>
    private void SpawnBullet(Quaternion rotation)
    {
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, rotation);
        bullet.tag = "EnemyBullet";

        int bLayer = LayerMask.NameToLayer("Bullet");
        if (bLayer != -1) bullet.layer = bLayer;

        RicochetBullet ricochet = bullet.GetComponent<RicochetBullet>();
        if (ricochet != null)
        {
            ricochet.ownerTag = gameObject.tag; // "Enemy"
        }

        // Ignore self colliders
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] bulletColliders = bullet.GetComponentsInChildren<Collider2D>();
        foreach (Collider2D mc in myColliders)
        {
            foreach (Collider2D bc in bulletColliders)
            {
                Physics2D.IgnoreCollision(mc, bc);
            }
        }
    }

    // ──────────────────────────────────────────────────
    // Phase Management
    // ──────────────────────────────────────────────────
    private void EnterPhase2()
    {
        isEnraged = true;
        nextNovaTime = Time.unscaledTime + novaInterval;

        // Visual tint
        if (spriteRenderer != null)
        {
            spriteRenderer.color = enrageColor;
        }

        // ── Camera Shake: Heavy rumble on Phase 2 entry ──
        CameraFollow.Shake(0.4f, 0.45f);

        ShowDialogue("ENOUGH! WITNESS TRUE VECTOR CHAOS!", 4f);
        Debug.Log("[Boss] Entered Phase 2 — ENRAGED!");
    }

    // ──────────────────────────────────────────────────
    // Health & Damage
    // ──────────────────────────────────────────────────
    /// <summary>Called by DamageReceiver / RicochetBullet when hit by PlayerBullet.</summary>
    public void TakeDamage(int amount)
    {
        if (isDead) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0);

        Debug.Log($"[Boss] Took {amount} damage. Health: {currentHealth}/{maxHealth}");

        // Update boss health bar
        if (GameManager.Instance != null)
        {
            GameManager.Instance.UpdateBossHealthUI(currentHealth, maxHealth);
        }

        // Damage flash
        if (spriteRenderer != null)
        {
            StartCoroutine(DamageFlash());
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private IEnumerator DamageFlash()
    {
        Color original = spriteRenderer.color;
        spriteRenderer.color = Color.white;
        yield return new WaitForSecondsRealtime(0.1f);
        spriteRenderer.color = isEnraged ? enrageColor : original;
    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;

        Debug.Log("[Boss] THE DARK WARLORD DEFEATED!");

        // ── Camera Shake: Massive shake on Boss death ──
        CameraFollow.Shake(0.6f, 0.6f);

        // ── Clear all enemy bullets in the scene ──
        GameObject[] bullets = GameObject.FindGameObjectsWithTag("EnemyBullet");
        foreach (GameObject b in bullets)
        {
            Destroy(b);
        }

        // ── Death dialogue ──
        ShowDialogue("Impossible... my calculations...", 3f);

        // ── Hide boss health bar ──
        if (GameManager.Instance != null)
        {
            GameManager.Instance.HideBossHealthUI();
        }

        // ── Notify Princess ──
        if (princess == null)
            princess = FindAnyObjectByType<PrincessController>();

        if (princess != null)
        {
            princess.OnBossDefeated();
        }

        // ── Disable/destroy after delay ──
        StartCoroutine(DeathSequence());
    }

    private IEnumerator DeathSequence()
    {
        // Let the death dialogue display
        yield return new WaitForSecondsRealtime(3f);

        // Fade out or just destroy
        if (bossCanvas != null)
            bossCanvas.gameObject.SetActive(false);

        gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────────
    // Dialogue
    // ──────────────────────────────────────────────────
    private void ShowDialogue(string message, float duration)
    {
        if (bossCanvas != null)
        {
            bossCanvas.gameObject.SetActive(true);
        }
        if (bossDialogueText != null)
        {
            bossDialogueText.text = message;
        }
        StartCoroutine(HideDialogueAfter(duration));
    }

    private IEnumerator HideDialogueAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (bossCanvas != null && !isDead)
        {
            bossCanvas.gameObject.SetActive(false);
        }
    }

    // ──────────────────────────────────────────────────
    // Animation
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

        if (playerTransform != null)
        {
            Vector2 aimDir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            anim.SetFloat(HashAimX, aimDir.x);
            anim.SetFloat(HashAimY, aimDir.y);
        }
    }

    // ──────────────────────────────────────────────────
    // Collision-based damage fallback
    // ──────────────────────────────────────────────────
    // NOTE: DamageReceiver on the Hitbox child handles trigger-based damage.
    //       RicochetBullet.OnCollisionEnter2D handles solid collision damage.
    //       Both read RicochetBullet.CurrentDamage for trick-shot escalation.
    //       No redundant handlers here to prevent double/triple damage.

    // ──────────────────────────────────────────────────
    // Editor Gizmos
    // ──────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, stoppingDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, strafeRadius);
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    public int CurrentHealth => currentHealth;
    public bool IsDead => isDead;
    public bool IsEnraged => isEnraged;
}
