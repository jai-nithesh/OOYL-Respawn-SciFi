using UnityEngine;

/// <summary>
/// RicochetBullet – Geometric bouncing projectile with layer-based collision.
/// ──────────────────────────────────────────────────────────────────────────────
/// COLLISION ARCHITECTURE (v2 – Two-Collider System):
///
///   OnCollisionEnter2D → Handles BounceWall reflection (solid-to-solid physics).
///                        The else branch destroys the bullet on any non-wall solid hit.
///
///   OnTriggerEnter2D  → Handles hitbox overlaps (bullet enters a CharacterBody trigger).
///                        Destroys the bullet. Damage is handled by DamageReceiver on
///                        the hitbox side.
///
///   Physics2D.IgnoreCollision is called at spawn time (by PlayerController / EnemyAI)
///   to prevent the bullet from interacting with its owner's colliders at all.
///
/// LAYER REQUIREMENTS:
///   • Bullet must be on layer "Bullet"
///   • Character feet colliders on layer "CharacterFeet"
///   • Character hitbox triggers on layer "CharacterBody"
///   • Layer Matrix: Bullet ↔ CharacterFeet = OFF
///   • Layer Matrix: Bullet ↔ CharacterBody = ON (trigger detection)
///   • Layer Matrix: Bullet ↔ Bullet = OFF
///   • Layer Matrix: Bullet ↔ Default = ON (BounceWall bouncing)
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class RicochetBullet : MonoBehaviour
{
    // ───────── Projectile Settings ─────────
    [Header("Projectile Settings")]
    [Tooltip("The constant speed the bullet travels in units per second.")]
    public float speed = 5f;

    [Tooltip("Maximum number of times the bullet can bounce off geometry before destroying itself.")]
    public int maxBounces = 2;

    [Tooltip("Tag used to identify what surfaces trigger the reflection math.")]
    public string bounceTag = "BounceWall";

    // ───────── Ownership ─────────
    [Header("Ownership")]
    [Tooltip("Tag of the entity that fired this bullet. Used as a safety fallback " +
             "for collision filtering. Primary filtering uses Physics2D.IgnoreCollision.")]
    public string ownerTag = "";

    // ───────── Hitbox Layer ─────────
    [Header("Hitbox Detection")]
    [Tooltip("Name of the layer used for character body hitbox triggers. " +
             "The bullet destroys itself when overlapping a collider on this layer.")]
    public string hitboxLayerName = "CharacterBody";

    // ───────── Private State ─────────
    private Rigidbody2D rb;
    private int currentBounces = 0;
    private Vector2 currentVelocity;
    private int hitboxLayerIndex = -1;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();

        // Cache the hitbox layer index for fast comparison
        hitboxLayerIndex = LayerMask.NameToLayer(hitboxLayerName);
        if (hitboxLayerIndex == -1)
        {
            Debug.LogWarning($"[RicochetBullet] Layer '{hitboxLayerName}' not found. " +
                             "OnTriggerEnter2D hitbox detection will not work. " +
                             "Create the layer in Project Settings → Tags and Layers.");
        }
    }

    private void Start()
    {
        // ── Initialize geometric trajectory ──
        currentVelocity = transform.right * speed;
        rb.linearVelocity = currentVelocity;

        // ── Auto-destroy safety net ──
        Destroy(gameObject, 8f);
    }

    private void FixedUpdate()
    {
        // ── Enforce Constant Velocity ──
        // Forcibly maintain velocity to prevent physics friction/drag from degrading
        // the mathematical perfection of the bounce trajectory.
        rb.linearVelocity = currentVelocity;
    }

    // ──────────────────────────────────────────────────
    // Solid Collision – BounceWall Reflection (Vector2.Reflect)
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Handles solid-to-solid collisions. Bounces off BounceWall surfaces
    /// using mathematical reflection. Destroys on any other solid hit.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Safety: ignore the owner (redundant with Physics2D.IgnoreCollision but harmless)
        if (!string.IsNullOrEmpty(ownerTag) && collision.gameObject.CompareTag(ownerTag))
        {
            return;
        }

        // ── BounceWall Reflection ──
        if (collision.gameObject.CompareTag(bounceTag))
        {
            if (currentBounces >= maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            currentBounces++;

            // Vector Reflection Math
            ContactPoint2D contact = collision.contacts[0];
            Vector2 surfaceNormal = contact.normal;
            currentVelocity = Vector2.Reflect(currentVelocity, surfaceNormal);
            rb.linearVelocity = currentVelocity;

            // Rotate sprite to match new trajectory
            float angleRad = Mathf.Atan2(currentVelocity.y, currentVelocity.x);
            float angleDeg = angleRad * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }
        else
        {
            // If we hit an enemy (e.g. feet or solid collider), deal damage!
            EnemyAI enemy = collision.gameObject.GetComponentInParent<EnemyAI>();
            if (enemy != null && !collision.gameObject.CompareTag(ownerTag))
            {
                enemy.TakeDamage(1);
            }

            // Hit a solid non-wall object: destroy
            Destroy(gameObject);
        }
    }

    // ──────────────────────────────────────────────────
    // Trigger Overlap – Hitbox Detection
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Handles trigger overlaps with character body hitboxes.
    /// Deals damage if the target has EnemyAI, and destroys the bullet.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore owner
        if (!string.IsNullOrEmpty(ownerTag) && 
            (other.CompareTag(ownerTag) || other.transform.root.CompareTag(ownerTag)))
        {
            return;
        }

        // Deal damage if we hit an enemy
        EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
        if (enemy != null)
        {
            enemy.TakeDamage(1);
            Destroy(gameObject);
            return;
        }

        // Only respond to CharacterBody-layer triggers (hitboxes)
        if (hitboxLayerIndex != -1 && other.gameObject.layer == hitboxLayerIndex)
        {
            Destroy(gameObject);
        }
    }
}