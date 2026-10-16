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
/// TRICK-SHOT SYSTEM (ADD-ON 2):
///   Each BounceWall bounce on a PlayerBullet:
///     • Increases damage by +1 (0-bounce = 1 dmg, 1-bounce = 2 dmg, 2-bounce = 3 dmg)
///     • Multiplies speed by 1.12× and scales transform by 1.15×
///     • Shifts SpriteRenderer + TrailRenderer color per bounce tier
///     • On-hit displays "RICOCHET HIT! x{N} DMG" callout on enemy's World Space Canvas
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

    // ───────── Damage ─────────
    [Header("Damage")]
    [Tooltip("Base damage dealt per hit.")]
    public int damage = 1;

    // ───────── Trick-Shot Settings ─────────
    [Header("Trick-Shot Settings (Tweakable)")]
    [Tooltip("Extra damage per bounce (only for PlayerBullet).")]
    public int bonusDamagePerBounce = 1;

    [Tooltip("Speed multiplier per bounce.")]
    public float speedMultiplierPerBounce = 1.08f;

    [Tooltip("Scale multiplier per bounce (1.0 = no scale change).")]
    public float scaleMultiplierPerBounce = 1.02f;

    // ───────── Visual & Color Styling ─────────
    [Header("Visual & Color Styling (Tweakable)")]
    [Tooltip("Base bullet color with alpha transparency (sleek translucent cyan).")]
    public Color baseColor = new Color(0.2f, 0.85f, 1f, 0.75f);

    [Tooltip("Subtle color accent on bounce (no garish hue change).")]
    public Color bounceColor = new Color(0.35f, 0.95f, 1f, 0.88f);

    // ───────── Camera Shake & Effects ─────────
    [Header("Shake & Visual Effects (Tweakable)")]
    [Tooltip("Camera shake duration on bounce.")]
    public float bounceShakeDuration = 0.04f;

    [Tooltip("Camera shake magnitude on bounce. Set to 0 to disable.")]
    public float bounceShakeMagnitude = 0.02f;

    [Tooltip("Show floating text callouts on ricochet hits?")]
    public bool showRicochetCallouts = false;

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
    private SpriteRenderer bulletSR;
    private TrailRenderer bulletTrail;
    private bool isPlayerBullet = false;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        bulletSR = GetComponent<SpriteRenderer>();
        bulletTrail = GetComponent<TrailRenderer>();

        // Cache the hitbox layer index for fast comparison
        hitboxLayerIndex = LayerMask.NameToLayer(hitboxLayerName);
        if (hitboxLayerIndex == -1)
        {
            Debug.LogWarning($"[RicochetBullet] Layer '{hitboxLayerName}' not found. " +
                             "OnTriggerEnter2D hitbox detection will not work. " +
                             "Create the layer in Project Settings → Tags and Layers.");
        }

        // Apply professional translucent base styling
        if (bulletSR != null)
        {
            bulletSR.color = baseColor;
        }

        if (bulletTrail != null)
        {
            bulletTrail.startColor = baseColor;
            bulletTrail.endColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0f);
        }
    }

    private void Start()
    {
        // ── Initialize geometric trajectory ──
        currentVelocity = transform.right * speed;
        rb.linearVelocity = currentVelocity;

        // Check if this is a player bullet (for trick-shot bonuses)
        isPlayerBullet = CompareTag("PlayerBullet");

        // Apply translucent styling
        if (bulletSR != null)
        {
            bulletSR.color = baseColor;
        }

        // ── Auto-destroy safety net ──
        Destroy(gameObject, 8f);
    }

    private void FixedUpdate()
    {
        // ── Enforce Constant Velocity & Anti-Stuck Watchdog ──
        if (currentVelocity.sqrMagnitude < 0.1f)
        {
            currentVelocity = transform.right * speed;
        }

        // ── Time Dilation Matrix Effect ──
        // When Time Dilation is active (Time.timeScale < 1):
        // Player bullets compensate so they travel at full real-time speed.
        // Enemy bullets travel at the dilated timeScale (slow-motion 25%), so the player can weave between them!
        if (isPlayerBullet && Time.timeScale < 0.99f && Time.timeScale > 0.01f)
        {
            float bulletComp = 1f / Time.timeScale;
            rb.linearVelocity = currentVelocity * bulletComp;
        }
        else
        {
            rb.linearVelocity = currentVelocity;
        }
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

            if (collision.contactCount == 0) return;

            currentBounces++;

            // Vector Reflection Math
            ContactPoint2D contact = collision.GetContact(0);
            Vector2 surfaceNormal = contact.normal;
            if (surfaceNormal.sqrMagnitude < 0.001f)
            {
                surfaceNormal = -currentVelocity.normalized;
            }

            // Reflect direction and preserve full projectile speed
            Vector2 reflectedDir = Vector2.Reflect(currentVelocity.normalized, surfaceNormal).normalized;
            currentVelocity = reflectedDir * speed;

            // ── Anti-Stuck Nudge ──
            // Nudge position along the surface normal so the collider never gets stuck inside the wall
            transform.position += (Vector3)(surfaceNormal * 0.08f);
            rb.position = transform.position;

            // ── Trick-Shot Bonus (PlayerBullet only) ──
            if (isPlayerBullet)
            {
                // Increase damage
                damage += bonusDamagePerBounce;

                // Subtle speed multiplier
                float newSpeed = speed * speedMultiplierPerBounce;
                speed = newSpeed;
                currentVelocity = reflectedDir * speed;

                // Subtle scale pulse
                transform.localScale *= scaleMultiplierPerBounce;

                // Sleek, elegant color shift (maintains translucent cyber look)
                ApplyBounceColor();

                // Camera shake on bounce (tweakable, low by default)
                if (bounceShakeMagnitude > 0f && bounceShakeDuration > 0f)
                {
                    CameraFollow.Shake(bounceShakeDuration, bounceShakeMagnitude);
                }
            }

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
                enemy.TakeDamage(damage);
                if (currentBounces >= 1) ShowRicochetCallout(collision.gameObject);
            }

            // If we hit the boss
            BossController boss = collision.gameObject.GetComponentInParent<BossController>();
            if (boss != null && !collision.gameObject.CompareTag(ownerTag))
            {
                boss.TakeDamage(damage);
                if (currentBounces >= 1) ShowRicochetCallout(collision.gameObject);
            }

            // If we hit a player (enemy bullet hitting player's feet collider)
            PlayerController player = collision.gameObject.GetComponentInParent<PlayerController>();
            if (player != null && ownerTag != "Player")
            {
                player.TakeDamage(damage);
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
    /// Deals damage if the target has EnemyAI or PlayerController, and destroys the bullet.
    /// </summary>
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Ignore owner
        if (!string.IsNullOrEmpty(ownerTag) && 
            (other.CompareTag(ownerTag) || other.transform.root.CompareTag(ownerTag)))
        {
            return;
        }

        // ── IMPORTANT: DamageReceiver.cs on the Hitbox child is the single source
        //    of truth for trigger-based damage. It reads our CurrentDamage property.
        //    We only handle visual callouts and self-destruction here. ──

        // Show trick-shot callout if this bullet bounced at least once
        if (currentBounces >= 1 && showRicochetCallouts)
        {
            // Check if we hit an enemy or boss for the callout
            EnemyAI enemy = other.GetComponentInParent<EnemyAI>();
            BossController boss = other.GetComponentInParent<BossController>();
            if (enemy != null || boss != null)
            {
                ShowRicochetCallout(other.gameObject);
                if (bounceShakeMagnitude > 0f)
                {
                    CameraFollow.Shake(bounceShakeDuration, bounceShakeMagnitude * 1.5f);
                }
            }
        }

        // Only respond to CharacterBody-layer triggers (hitboxes)
        // Bullet destruction is handled by DamageReceiver.Destroy(other.gameObject)
        // This fallback catches cases where there's no DamageReceiver
        if (hitboxLayerIndex != -1 && other.gameObject.layer == hitboxLayerIndex)
        {
            // DamageReceiver will Destroy us — but if there's no DamageReceiver,
            // we still need to self-destruct after a tiny delay
            Destroy(gameObject, 0.02f);
        }
    }

    // ──────────────────────────────────────────────────
    // Trick-Shot Visual Helpers
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Applies color shift based on current bounce count.
    /// </summary>
    private void ApplyBounceColor()
    {
        Color targetColor = bounceColor;

        if (bulletSR != null)
        {
            bulletSR.color = targetColor;
        }

        if (bulletTrail != null)
        {
            bulletTrail.startColor = targetColor;
            bulletTrail.endColor = new Color(targetColor.r, targetColor.g, targetColor.b, 0f);
        }
    }

    /// <summary>
    /// Displays a brief "RICOCHET HIT! xN DMG" on the target's World Space Canvas.
    /// Falls back to Debug.Log if no canvas is found.
    /// </summary>
    private void ShowRicochetCallout(GameObject target)
    {
        // Try to find a World Space Canvas on the target or its root
        Canvas canvas = target.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Transform root = target.transform.root;
            canvas = root.GetComponentInChildren<Canvas>(true);
        }

        if (canvas != null)
        {
            var tmpText = canvas.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
            if (tmpText != null)
            {
                canvas.gameObject.SetActive(true);
                string bounceLabel = currentBounces == 1 ? "BANK SHOT" : "TRICK SHOT";
                tmpText.text = $"<color=#FFD700>{bounceLabel}!</color> x{damage} DMG";

                // Auto-hide after 1.5 seconds (use MonoBehaviour on the canvas owner)
                MonoBehaviour mb = canvas.GetComponentInParent<MonoBehaviour>();
                if (mb != null)
                {
                    mb.StartCoroutine(HideCalloutAfter(canvas, tmpText, 1.5f));
                }
            }
        }

        Debug.Log($"[RicochetBullet] RICOCHET HIT! Bounces: {currentBounces}, Damage: {damage}");
    }

    private System.Collections.IEnumerator HideCalloutAfter(Canvas canvas, TMPro.TextMeshProUGUI text, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        // Don't hide if the entity has its own dialogue system managing the canvas
        // Only restore if text still shows our callout
        if (text != null && (text.text.Contains("BANK SHOT") || text.text.Contains("TRICK SHOT")))
        {
            if (canvas != null)
                canvas.gameObject.SetActive(false);
        }
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    public int CurrentBounces => currentBounces;
    public int CurrentDamage => damage;
}