using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    // ───────── Movement ─────────
    [Header("Movement")]
    [Tooltip("Base movement speed in units/sec.")]
    public float moveSpeed = 7f;

    // ───────── Aiming ─────────
    [Header("Aiming")]
    [Tooltip("Child Transform that rotates to face the mouse cursor.")]
    public Transform weaponPivot;

    [Tooltip("Child of WeaponPivot where bullets spawn.")]
    public Transform firePoint;

    // ───────── Firing ─────────
    [Header("Firing")]
    [Tooltip("Prefab to instantiate when firing.")]
    public GameObject bulletPrefab;

    [Tooltip("Minimum seconds between shots.")]
    public float fireRate = 0.2f;

    // ───────── Time Dilation Support ─────────
    [Header("Time Dilation")]
    [Tooltip("When true the player moves at real-world speed regardless of Time.timeScale.")]
    public bool useUnscaledTime = false;

    // ───────── Private State ─────────
    private Rigidbody2D rb;
    private Camera mainCam;
    private Animator anim;
    private Vector2 moveInput;
    private Vector2 lastMoveDirection;
    private float nextFireTime;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;
        anim = GetComponent<Animator>();

        // Set a default facing direction (down) so the blend tree has an initial state
        lastMoveDirection = new Vector2(0f, -1f);
    }

    private void Update()
    {
        // ── Gather raw WASD / arrow-key input using the new Input System ──
        float h = 0f;
        float v = 0f;
        if (Keyboard.current != null)
        {
            if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) h -= 1f;
            if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) h += 1f;
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) v += 1f;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) v -= 1f;
        }
        else
        {
            // Fall back to legacy Input if the new Input System isn't available
            h = Input.GetAxisRaw("Horizontal");
            v = Input.GetAxisRaw("Vertical");
        }

        moveInput = new Vector2(h, v);
        if (moveInput.sqrMagnitude > 1f) moveInput = moveInput.normalized; // normalise for consistent diagonal speed

        // ── Update Animation State ──
        UpdateAnimations();

        // ── Geometric mouse aiming ──
        AimWeaponPivotAtMouse();

        // ── Fire on left-click (new Input System) ──
        bool isFiring = false;
        if (Mouse.current != null)
        {
            isFiring = Mouse.current.leftButton.isPressed;
        }
        else
        {
            // Fallback to legacy Input
            isFiring = Input.GetMouseButton(0);
        }

        if (isFiring && Time.unscaledTime >= nextFireTime)
        {
            Fire();
            nextFireTime = Time.unscaledTime + fireRate;
        }
    }

    private void FixedUpdate()
    {
        // ── Apply movement via Rigidbody2D ──
        // When time is dilated we compensate so the player keeps real-world speed.
        if (useUnscaledTime)
        {
            // Time.timeScale may be < 1; FixedUpdate still fires at fixedDeltaTime * timeScale.
            // We want the player to feel full-speed, so scale velocity up.
            float compensation = (Time.timeScale > 0f) ? (1f / Time.timeScale) : 1f;
            rb.linearVelocity = moveInput * moveSpeed * compensation;
        }
        else
        {
            rb.linearVelocity = moveInput * moveSpeed;
        }
    }

    // ──────────────────────────────────────────────────
    // Animation Handling
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Feeds movement data into the Animator to transition between Idle and Run Blend Trees.
    /// </summary>
    private void UpdateAnimations()
    {
        if (anim == null) return;

        // If the player is actively moving, update the last move direction
        if (moveInput.sqrMagnitude > 0.01f)
        {
            lastMoveDirection = moveInput.normalized;
        }

        // Feed the X and Y axes into the Animator
        anim.SetFloat("Horizontal", lastMoveDirection.x);
        anim.SetFloat("Vertical", lastMoveDirection.y);

        // Feed the speed to transition between Idle (0) and Moving (>0)
        anim.SetFloat("Speed", moveInput.sqrMagnitude);
    }

    // ──────────────────────────────────────────────────
    // Geometric Mouse Aiming  (Atan2)
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Calculates the exact angle between the player and the mouse cursor
    /// using Mathf.Atan2 and rotates the WeaponPivot to face the cursor.
    /// </summary>
    private void AimWeaponPivotAtMouse()
    {
        if (weaponPivot == null || mainCam == null) return;

        // Convert mouse screen position to world space (support new Input System)
        Vector3 mouseScreenPos;
        if (Mouse.current != null)
        {
            mouseScreenPos = Mouse.current.position.ReadValue();
        }
        else
        {
            mouseScreenPos = Input.mousePosition;
        }
        Vector3 mouseWorldPos = mainCam.ScreenToWorldPoint(mouseScreenPos);
        mouseWorldPos.z = 0f;

        // Direction vector from player to mouse
        Vector2 direction = (Vector2)(mouseWorldPos - transform.position);

        // Calculate angle in degrees using Atan2
        float angleRad = Mathf.Atan2(direction.y, direction.x);
        float angleDeg = angleRad * Mathf.Rad2Deg;

        // Apply rotation to WeaponPivot (rotating around Z axis in 2D)
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);
    }

    // ──────────────────────────────────────────────────
    // Weapon Firing
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Instantiates the bullet prefab at the FirePoint with matching rotation.
    /// </summary>
    private void Fire()
    {
        if (bulletPrefab == null || firePoint == null) return;
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, firePoint.rotation);

        // ── Tag the bullet so EnemyAI knows it's hostile and so it won't damage the player on ricochet ──
        bullet.tag = "PlayerBullet";
        RicochetBullet rb = bullet.GetComponent<RicochetBullet>();
        if (rb != null)
        {
            rb.ownerTag = "Player";
        }
    }
}