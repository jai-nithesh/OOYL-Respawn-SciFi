using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// PrincessController – Interactive 3-State Princess NPC for Scene 6.
/// ──────────────────────────────────────────────────────────────────────────────
/// STATES:
///   1. Captured    – Trapped in holding cell, waits for player rescue
///   2. CombatAlly  – Follows player, shoots at enemies
///   3. VictorySeq  – Walks to player, plays victory dialogue, triggers win
///
/// HIERARCHY:
///   Princess (Tag: Untagged or custom) – this script, Rigidbody2D, Animator
///   ├── WeaponPivot (empty Transform – rotates to aim)
///   │   └── FirePoint (empty Transform – bullet spawn)
///   ├── Hitbox (Layer: CharacterBody) – BoxCollider2D (Is Trigger)
///   ├── CageVisual (optional – SpriteRenderer for energy field / bars)
///   └── PrincessCanvas (World Space Canvas)
///       └── PrincessText (TextMeshProUGUI)
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Animator))]
public class PrincessController : MonoBehaviour
{
    // ───────── State Machine ─────────
    public enum PrincessState { Captured, CombatAlly, VictorySequence }

    [Header("State")]
    [SerializeField] private PrincessState currentState = PrincessState.Captured;

    // ───────── Rescue Settings ─────────
    [Header("Rescue")]
    [Tooltip("Distance within which the player can rescue the princess.")]
    public float rescueRadius = 2.5f;

    [Tooltip("If true, rescue triggers on proximity alone. If false, also requires pressing E.")]
    public bool autoRescueOnProximity = true;

    // ───────── Follow Settings ─────────
    [Header("Follow (CombatAlly)")]
    [Tooltip("Distance to maintain from the player while following.")]
    public float followDistance = 3f;

    [Tooltip("Movement speed while following.")]
    public float moveSpeed = 5f;

    // ───────── Shooting Settings ─────────
    [Header("Shooting (CombatAlly)")]
    public Transform weaponPivot;
    public Transform firePoint;
    public GameObject bulletPrefab;
    public float fireCooldown = 0.8f;
    public float shootingRange = 10f;

    // ───────── References ─────────
    [Header("References")]
    public string playerTag = "Player";
    public string enemyTag = "Enemy";

    [Tooltip("Optional cage/energy field visual to disable on rescue.")]
    public GameObject cageVisual;

    // ───────── World Space UI ─────────
    [Header("World Space UI")]
    public Canvas princessCanvas;
    public TextMeshProUGUI princessText;

    // ───────── Animator Parameter Names ─────────
    [Header("Animator Parameters")]
    [Tooltip("Horizontal movement parameter name. Use 'MoveX' or 'Horizontal'.")]
    public string moveXParam = "MoveX";

    [Tooltip("Vertical movement parameter name. Use 'MoveY' or 'Vertical'.")]
    public string moveYParam = "MoveY";

    [Tooltip("Aim horizontal parameter name.")]
    public string aimXParam = "AimX";

    [Tooltip("Aim vertical parameter name.")]
    public string aimYParam = "AimY";

    [Tooltip("Is Moving boolean parameter name.")]
    public string isMovingParam = "IsMoving";

    [Tooltip("Speed float parameter name (alternative to IsMoving). Leave empty if not used.")]
    public string speedParam = "Speed";

    [Tooltip("Shoot trigger parameter name.")]
    public string shootTriggerParam = "ShootTrigger";

    // ───────── Private State ─────────
    private Rigidbody2D rb;
    private Animator anim;
    private Transform playerTransform;
    private float nextFireTime;
    private bool victoryTriggered = false;

    // Cached animator parameter hashes
    private int hashMoveX, hashMoveY, hashAimX, hashAimY, hashIsMoving, hashSpeed, hashShootTrigger;
    private bool hasSpeedParam = false;
    private bool hasIsMovingParam = false;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();

        // Cache animator hashes
        hashMoveX = Animator.StringToHash(moveXParam);
        hashMoveY = Animator.StringToHash(moveYParam);
        hashAimX = Animator.StringToHash(aimXParam);
        hashAimY = Animator.StringToHash(aimYParam);
        hashIsMoving = Animator.StringToHash(isMovingParam);
        hashSpeed = !string.IsNullOrEmpty(speedParam) ? Animator.StringToHash(speedParam) : 0;
        hashShootTrigger = Animator.StringToHash(shootTriggerParam);

        // Check which parameters exist in the Animator Controller
        if (anim != null && anim.runtimeAnimatorController != null)
        {
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.nameHash == hashIsMoving) hasIsMovingParam = true;
                if (hashSpeed != 0 && param.nameHash == hashSpeed) hasSpeedParam = true;
            }
        }

        // Auto-find UI
        if (princessCanvas == null)
            princessCanvas = GetComponentInChildren<Canvas>(true);
        if (princessText == null && princessCanvas != null)
            princessText = princessCanvas.GetComponentInChildren<TextMeshProUGUI>(true);
    }

    private void Start()
    {
        // Find the player
        GameObject playerObj = GameObject.FindWithTag(playerTag);
        if (playerObj != null) playerTransform = playerObj.transform;

        // Initialize state
        EnterState(currentState);
    }

    private void Update()
    {
        // Retry finding player
        if (playerTransform == null)
        {
            GameObject playerObj = GameObject.FindWithTag(playerTag);
            if (playerObj != null) playerTransform = playerObj.transform;
            else return;
        }

        switch (currentState)
        {
            case PrincessState.Captured:
                UpdateCaptured();
                break;
            case PrincessState.CombatAlly:
                UpdateCombatAlly();
                break;
            case PrincessState.VictorySequence:
                // Handled by coroutine
                break;
        }
    }

    private void FixedUpdate()
    {
        switch (currentState)
        {
            case PrincessState.CombatAlly:
                FixedUpdateCombatAlly();
                break;
            case PrincessState.VictorySequence:
                FixedUpdateVictory();
                break;
            default:
                rb.linearVelocity = Vector2.zero;
                break;
        }
    }

    // ──────────────────────────────────────────────────
    // State Machine
    // ──────────────────────────────────────────────────
    private void EnterState(PrincessState state)
    {
        currentState = state;

        switch (state)
        {
            case PrincessState.Captured:
                rb.linearVelocity = Vector2.zero;
                if (cageVisual != null) cageVisual.SetActive(true);
                ShowText("Help! Walk close to overload my shackles!");
                SetAnimMoving(false);
                break;

            case PrincessState.CombatAlly:
                if (cageVisual != null) cageVisual.SetActive(false);
                ShowText("I'm free! Covering you!");
                StartCoroutine(HideTextAfter(2f));
                break;

            case PrincessState.VictorySequence:
                StartCoroutine(VictoryCoroutine());
                break;
        }
    }

    // ──────────────────────────────────────────────────
    // State: Captured
    // ──────────────────────────────────────────────────
    private void UpdateCaptured()
    {
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist <= rescueRadius)
        {
            bool shouldRescue = autoRescueOnProximity;

            // Also check for E key press
            if (!shouldRescue)
            {
                if (UnityEngine.InputSystem.Keyboard.current != null)
                {
                    shouldRescue = UnityEngine.InputSystem.Keyboard.current.eKey.wasPressedThisFrame;
                }
                else
                {
                    shouldRescue = Input.GetKeyDown(KeyCode.E);
                }
            }

            if (shouldRescue)
            {
                EnterState(PrincessState.CombatAlly);
            }
        }
    }

    // ──────────────────────────────────────────────────
    // State: CombatAlly
    // ──────────────────────────────────────────────────
    private void UpdateCombatAlly()
    {
        // ── Find and shoot nearest enemy ──
        Transform target = FindNearestEnemy();
        if (target != null)
        {
            AimAtTarget(target);

            float dist = Vector2.Distance(transform.position, target.position);
            if (dist <= shootingRange && Time.unscaledTime >= nextFireTime)
            {
                FireAtTarget(target);
                nextFireTime = Time.unscaledTime + fireCooldown;
            }
        }

        // ── Update animations ──
        UpdateAnimations();
    }

    private void FixedUpdateCombatAlly()
    {
        if (playerTransform == null) return;

        float dist = Vector2.Distance(transform.position, playerTransform.position);

        if (dist > followDistance)
        {
            // Move toward player
            Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
            rb.linearVelocity = dir * moveSpeed;
        }
        else if (dist < followDistance * 0.5f)
        {
            // Too close, back away slightly
            Vector2 dir = ((Vector2)transform.position - (Vector2)playerTransform.position).normalized;
            rb.linearVelocity = dir * moveSpeed * 0.5f;
        }
        else
        {
            rb.linearVelocity = Vector2.zero;
        }
    }

    // ──────────────────────────────────────────────────
    // State: Victory Sequence
    // ──────────────────────────────────────────────────
    private bool walkingToPlayer = false;

    private IEnumerator VictoryCoroutine()
    {
        // Walk toward the player
        walkingToPlayer = true;

        // Wait until close enough
        while (playerTransform != null &&
               Vector2.Distance(transform.position, playerTransform.position) > 1.5f)
        {
            UpdateAnimations();
            yield return null;
        }

        // Stop walking
        walkingToPlayer = false;
        rb.linearVelocity = Vector2.zero;
        SetAnimMoving(false);

        // Victory dialogue
        ShowText("You defeated the Warlord! The sector is saved!");

        // Celebration delay (real time, safe during timeScale changes)
        yield return new WaitForSecondsRealtime(2.5f);

        // Trigger win screen
        if (GameManager.Instance != null && !victoryTriggered)
        {
            victoryTriggered = true;
            GameManager.Instance.TriggerWinScreen();
        }
    }

    private void FixedUpdateVictory()
    {
        if (!walkingToPlayer || playerTransform == null)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector2 dir = ((Vector2)playerTransform.position - (Vector2)transform.position).normalized;
        rb.linearVelocity = dir * moveSpeed;
    }

    // ──────────────────────────────────────────────────
    // Boss Defeated Hook
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Called by BossController when the Dark Warlord is defeated.
    /// If still Captured, auto-free. Then transition to VictorySequence.
    /// </summary>
    public void OnBossDefeated()
    {
        Debug.Log("[Princess] Boss defeated! Starting victory sequence.");

        // If still captured, auto-free
        if (currentState == PrincessState.Captured)
        {
            if (cageVisual != null) cageVisual.SetActive(false);
            ShowText("The Warlord's power fades... I'm free!");
            StartCoroutine(DelayedVictoryTransition());
        }
        else if (currentState == PrincessState.CombatAlly)
        {
            // Already free, go straight to victory
            EnterState(PrincessState.VictorySequence);
        }
    }

    private IEnumerator DelayedVictoryTransition()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        EnterState(PrincessState.VictorySequence);
    }

    // ──────────────────────────────────────────────────
    // Combat Helpers
    // ──────────────────────────────────────────────────
    private Transform FindNearestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag(enemyTag);
        Transform nearest = null;
        float nearestDist = float.MaxValue;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null || !enemy.activeInHierarchy) continue;
            float dist = Vector2.Distance(transform.position, enemy.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = enemy.transform;
            }
        }
        return nearest;
    }

    private void AimAtTarget(Transform target)
    {
        if (weaponPivot == null || target == null) return;

        Vector2 direction = (Vector2)(target.position - transform.position);
        float angleDeg = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        weaponPivot.rotation = Quaternion.Euler(0f, 0f, angleDeg);

        // Update aim animation params
        Vector2 aimDir = direction.normalized;
        if (anim != null)
        {
            anim.SetFloat(hashAimX, aimDir.x);
            anim.SetFloat(hashAimY, aimDir.y);
        }
    }

    private void FireAtTarget(Transform target)
    {
        if (bulletPrefab == null || firePoint == null) return;

        // Calculate direction to target
        Vector2 dir = ((Vector2)target.position - (Vector2)firePoint.position).normalized;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, angle);

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, rot);
        bullet.tag = "PlayerBullet";

        int bLayer = LayerMask.NameToLayer("Bullet");
        if (bLayer != -1) bullet.layer = bLayer;

        RicochetBullet ricochet = bullet.GetComponent<RicochetBullet>();
        if (ricochet != null)
        {
            ricochet.ownerTag = "Player"; // Allied with the player
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

        // Also ignore the player's colliders (friendly fire prevention)
        if (playerTransform != null)
        {
            Collider2D[] playerColliders = playerTransform.GetComponentsInChildren<Collider2D>();
            foreach (Collider2D pc in playerColliders)
            {
                foreach (Collider2D bc in bulletColliders)
                {
                    Physics2D.IgnoreCollision(pc, bc);
                }
            }
        }

        // Trigger shoot animation
        if (anim != null)
        {
            anim.SetTrigger(hashShootTrigger);
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
            anim.SetFloat(hashMoveX, moveDir.x);
            anim.SetFloat(hashMoveY, moveDir.y);
        }

        SetAnimMoving(isMoving);
    }

    private void SetAnimMoving(bool isMoving)
    {
        if (anim == null) return;

        if (hasIsMovingParam)
        {
            anim.SetBool(hashIsMoving, isMoving);
        }
        if (hasSpeedParam)
        {
            anim.SetFloat(hashSpeed, isMoving ? 1f : 0f);
        }
    }

    // ──────────────────────────────────────────────────
    // World Space Text
    // ──────────────────────────────────────────────────
    private void ShowText(string message)
    {
        if (princessCanvas != null) princessCanvas.gameObject.SetActive(true);
        if (princessText != null) princessText.text = message;
    }

    private IEnumerator HideTextAfter(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (princessCanvas != null) princessCanvas.gameObject.SetActive(false);
    }

    // ──────────────────────────────────────────────────
    // Editor Gizmos
    // ──────────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, rescueRadius);

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, followDistance);

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, shootingRange);
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    public PrincessState CurrentState => currentState;
}
