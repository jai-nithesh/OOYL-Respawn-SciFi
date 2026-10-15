using UnityEngine;

/// <summary>
/// DamageReceiver – Attach to the "Hitbox" child GameObject.
/// ──────────────────────────────────────────────────────────────────────────────
/// HIERARCHY PLACEMENT:
///   Enemy (Root) ← EnemyAI.cs, Rigidbody2D, BoxCollider2D (feet, solid)
///   └── Hitbox   ← THIS SCRIPT, BoxCollider2D (Is Trigger ☑, full body)
///       Layer: CharacterBody
///
///   Boss (Root) ← BossController.cs, Rigidbody2D, BoxCollider2D (feet, solid)
///   └── Hitbox   ← THIS SCRIPT, BoxCollider2D (Is Trigger ☑, full body)
///       Layer: CharacterBody
///
///   Player (Root) ← PlayerController.cs, Rigidbody2D, BoxCollider2D (feet, solid)
///   └── Hitbox   ← THIS SCRIPT, BoxCollider2D (Is Trigger ☑, full body)
///       Layer: CharacterBody
///
/// HOW IT WORKS:
///   When a bullet on the "Bullet" layer enters this trigger collider,
///   OnTriggerEnter2D fires. The script checks the bullet's tag against
///   hostileBulletTag. If it matches, it calls TakeDamage() on the parent's
///   EnemyAI, BossController, or PlayerController.
///   The bullet is also destroyed here.
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class DamageReceiver : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Tag of bullets that are hostile to this entity. " +
             "For enemies, set to 'PlayerBullet'. For the player, set to 'EnemyBullet'.")]
    public string hostileBulletTag = "PlayerBullet";

    [Tooltip("Damage dealt per hit.")]
    public int damagePerHit = 1;

    // ───────── Cached References ─────────
    private EnemyAI enemyAI;
    private BossController bossController;
    private PlayerController playerController;

    private void Awake()
    {
        // Search up the hierarchy for the health/AI component
        enemyAI = GetComponentInParent<EnemyAI>();
        bossController = GetComponentInParent<BossController>();
        playerController = GetComponentInParent<PlayerController>();

        // Validate collider setup
        Collider2D col = GetComponent<Collider2D>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"[DamageReceiver] The Collider2D on '{gameObject.name}' is NOT set as a trigger! " +
                             "Set 'Is Trigger' to true in the Inspector.");
        }
    }

    /// <summary>
    /// Called when a collider enters this trigger.
    /// Checks if the collider is a hostile bullet and applies damage.
    /// Reads actual damage from RicochetBullet.CurrentDamage (supports trick-shot escalation).
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ── Check if the intruding collider is a hostile bullet ──
        if (!other.CompareTag(hostileBulletTag)) return;

        // ── Read actual damage from the bullet (trick-shot escalation) ──
        int actualDamage = damagePerHit; // fallback
        RicochetBullet ricochet = other.GetComponent<RicochetBullet>();
        if (ricochet != null)
        {
            actualDamage = ricochet.CurrentDamage;
        }

        // ── Apply Damage to EnemyAI ──
        if (enemyAI == null) enemyAI = GetComponentInParent<EnemyAI>();
        if (enemyAI != null)
        {
            enemyAI.TakeDamage(actualDamage);
        }

        // ── Apply Damage to BossController ──
        if (bossController == null) bossController = GetComponentInParent<BossController>();
        if (bossController != null)
        {
            bossController.TakeDamage(actualDamage);
        }

        // ── Apply Damage to Player ──
        if (playerController == null) playerController = GetComponentInParent<PlayerController>();
        if (playerController != null)
        {
            playerController.TakeDamage(actualDamage);
        }

        // ── Destroy the bullet ──
        Destroy(other.gameObject);
    }
}
