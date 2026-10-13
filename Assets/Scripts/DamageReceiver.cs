using UnityEngine;

/// <summary>
/// DamageReceiver – Attach to the "Hitbox" child GameObject.
/// ──────────────────────────────────────────────────────────────────────────────
/// HIERARCHY PLACEMENT:
///   Enemy (Root) ← EnemyAI.cs, Rigidbody2D, BoxCollider2D (feet, solid)
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
///   EnemyAI (or any health component you add for the player).
///   The bullet is also destroyed here.
///
/// WHY A CHILD OBJECT?
///   The feet collider (on the root) uses layer "CharacterFeet" which does NOT
///   collide with bullets (Layer Matrix: OFF). The body hitbox needs to be on a
///   DIFFERENT layer ("CharacterBody") that DOES detect bullets. Since Unity's
///   collision matrix operates per-GameObject-layer, we need a separate child
///   GameObject on a different layer.
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
    // Future: private PlayerHealth playerHealth;

    private void Awake()
    {
        // Search up the hierarchy for the health/AI component
        enemyAI = GetComponentInParent<EnemyAI>();

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
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // ── Check if the intruding collider is a hostile bullet ──
        if (!other.CompareTag(hostileBulletTag)) return;

        // ── Apply Damage ──
        if (enemyAI == null)
        {
            enemyAI = GetComponentInParent<EnemyAI>();
        }

        if (enemyAI != null)
        {
            enemyAI.TakeDamage(damagePerHit);
        }
        // Future: else if (playerHealth != null) { playerHealth.TakeDamage(damagePerHit); }

        // ── Destroy the bullet ──
        // Both DamageReceiver and RicochetBullet.OnTriggerEnter2D may call
        // Destroy on the same frame — Unity handles redundant Destroy gracefully.
        Destroy(other.gameObject);
    }
}
