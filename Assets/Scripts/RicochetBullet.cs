using UnityEngine;

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

    // ───────── Private State ─────────
    private Rigidbody2D rb;
    private int currentBounces = 0;
    private Vector2 currentVelocity;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        // ── Initialize geometric trajectory ──
        // The bullet spawns facing the mouse cursor (handled by PlayerController's FirePoint).
        // We set the initial velocity straight forward along the local right axis (X-axis).
        currentVelocity = transform.right * speed;
        rb.linearVelocity = currentVelocity;
    }

    private void FixedUpdate()
    {
        // ── Enforce Constant Velocity ──
        // We forcibly maintain the velocity in FixedUpdate to prevent Unity's internal 
        // physics friction or drag from slowly degrading the mathematical perfection of the bounce.
        rb.linearVelocity = currentVelocity;
    }

    // ──────────────────────────────────────────────────
    // Mathematical Reflection Logic (Vector2.Reflect)
    // ──────────────────────────────────────────────────
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the object we collided with is part of our designated geometric arena
        if (collision.gameObject.CompareTag(bounceTag))
        {
            // If the bullet has exceeded its bounce limit, destroy it to prevent infinite loops
            if (currentBounces >= maxBounces)
            {
                Destroy(gameObject);
                return;
            }

            // Increment the geometric bounce counter
            currentBounces++;

            // ── Vector Reflection Math ──
            // 1. Get the exact point of contact.
            ContactPoint2D contact = collision.contacts[0];

            // 2. Get the normal (the perpendicular vector of the surface we hit).
            Vector2 surfaceNormal = contact.normal;

            // 3. Calculate the perfect reflection vector using the inbound velocity and the surface normal.
            currentVelocity = Vector2.Reflect(currentVelocity, surfaceNormal);

            // 4. Immediately apply the new calculated trajectory to the Rigidbody.
            rb.linearVelocity = currentVelocity;

            // 5. Rotate the bullet's Transform so the sprite and TrailRenderer perfectly align with the new vector.
            float angleRad = Mathf.Atan2(currentVelocity.y, currentVelocity.x);
            float angleDeg = angleRad * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, 0f, angleDeg);
        }
        else
        {
            // If the bullet hits an enemy, a shield, or anything not tagged as a wall, destroy it.
            // Note: We will add enemy damage logic here shortly.
            Destroy(gameObject);
        }
    }
}