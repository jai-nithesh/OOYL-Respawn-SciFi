using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Sandevistan-style time dilation active skill.
/// Press a key to slow the world to 0.3× while the player moves at normal speed.
/// Spawns ghosting / afterimage sprites that fade out behind the player.
/// </summary>
public class TimeDilation : MonoBehaviour
{
    // ───────── Activation ─────────
    [Header("Activation")]
    [Tooltip("Key to toggle Sandevistan time dilation.")]
    public KeyCode activationKey = KeyCode.LeftShift;

    [Tooltip("Duration the effect lasts (real-world seconds).")]
    public float duration = 3f;

    [Tooltip("Cooldown between uses (real-world seconds).")]
    public float cooldown = 5f;

    // ───────── Time Settings ─────────
    [Header("Time Settings")]
    [Tooltip("World time scale while active (0.3 = 30% speed).")]
    public float dilatedTimeScale = 0.3f;

    // ───────── Ghosting / Afterimage ─────────
    [Header("Ghosting / Afterimage")]
    [Tooltip("Interval (real-world seconds) between ghost spawns.")]
    public float ghostInterval = 0.1f;

    [Tooltip("How long each ghost persists before vanishing (real-world seconds).")]
    public float ghostLifetime = 0.4f;

    [Tooltip("Starting alpha for the ghost sprite.")]
    [Range(0f, 1f)]
    public float ghostStartAlpha = 0.6f;

    [Tooltip("Sorting order offset for ghosts (behind the player).")]
    public int ghostSortingOrderOffset = -1;

    // ───────── References ─────────
    [Header("References")]
    [Tooltip("The PlayerController on this GameObject (auto-fetched if null).")]
    public PlayerController playerController;

    [Tooltip("The SpriteRenderer to clone for afterimages (auto-fetched if null).")]
    public SpriteRenderer playerSpriteRenderer;

    // ───────── Private State ─────────
    private bool isActive = false;
    private float nextActivationTime = 0f;
    private Coroutine activeCoroutine;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (IsKeyDown(activationKey) && !isActive && Time.unscaledTime >= nextActivationTime)
        {
            activeCoroutine = StartCoroutine(SandevistanRoutine());
        }
    }

    // Helper to check a KeyCode using the new Input System when available,
    // falling back to legacy Input.GetKeyDown when not available or unmapped.
    private bool IsKeyDown(KeyCode key)
    {
        if (Keyboard.current == null)
            return Input.GetKeyDown(key);

        switch (key)
        {
            case KeyCode.LeftShift:
                return Keyboard.current.leftShiftKey.wasPressedThisFrame;
            case KeyCode.RightShift:
                return Keyboard.current.rightShiftKey.wasPressedThisFrame;
            case KeyCode.Space:
                return Keyboard.current.spaceKey.wasPressedThisFrame;
            case KeyCode.LeftControl:
                return Keyboard.current.leftCtrlKey.wasPressedThisFrame;
            case KeyCode.RightControl:
                return Keyboard.current.rightCtrlKey.wasPressedThisFrame;
            case KeyCode.E:
                return Keyboard.current.eKey.wasPressedThisFrame;
            case KeyCode.Q:
                return Keyboard.current.qKey.wasPressedThisFrame;
            case KeyCode.F:
                return Keyboard.current.fKey.wasPressedThisFrame;
            default:
                // Unknown mapping; fall back to legacy Input for safety
                return Input.GetKeyDown(key);
        }
    }

    // ──────────────────────────────────────────────────
    // Core Sandevistan Coroutine
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Drops Time.timeScale, enables unscaled movement on the player,
    /// spawns afterimage ghosts, then restores everything.
    /// </summary>
    private IEnumerator SandevistanRoutine()
    {
        isActive = true;

        // ── Activate time dilation ──
        Time.timeScale = dilatedTimeScale;
        Time.fixedDeltaTime = 0.02f * dilatedTimeScale;   // keep physics step ratio correct

        // Tell the player controller to use unscaled time
        if (playerController != null)
            playerController.useUnscaledTime = true;

        // ── Spawn ghosts for the duration ──
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SpawnGhost();

            // Wait in REAL time (not affected by timeScale)
            yield return new WaitForSecondsRealtime(ghostInterval);
            elapsed += ghostInterval;
        }

        // ── Deactivate time dilation ──
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (playerController != null)
            playerController.useUnscaledTime = false;

        isActive = false;
        nextActivationTime = Time.unscaledTime + cooldown;
    }

    // ──────────────────────────────────────────────────
    // Ghosting / Afterimage Spawner
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Instantiates a temporary copy of the player's current sprite
    /// at their current position, then fades it out over ghostLifetime.
    /// </summary>
    private void SpawnGhost()
    {
        if (playerSpriteRenderer == null) return;

        // Create a new empty GameObject for the ghost
        GameObject ghost = new GameObject("Afterimage");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        // Add a SpriteRenderer and clone the current sprite
        SpriteRenderer ghostSR = ghost.AddComponent<SpriteRenderer>();
        ghostSR.sprite = playerSpriteRenderer.sprite;
        ghostSR.sortingLayerName = playerSpriteRenderer.sortingLayerName;
        ghostSR.sortingOrder = playerSpriteRenderer.sortingOrder + ghostSortingOrderOffset;
        ghostSR.color = new Color(
            playerSpriteRenderer.color.r,
            playerSpriteRenderer.color.g,
            playerSpriteRenderer.color.b,
            ghostStartAlpha
        );
        ghostSR.material = new Material(playerSpriteRenderer.material);

        // Start the fade-out coroutine on this MonoBehaviour
        // (the ghost itself has no MonoBehaviour)
        StartCoroutine(FadeAndDestroyGhost(ghost, ghostSR));
    }

    /// <summary>
    /// Linearly fades the ghost's alpha from ghostStartAlpha → 0 over ghostLifetime,
    /// then Destroys the ghost GameObject.
    /// </summary>
    private IEnumerator FadeAndDestroyGhost(GameObject ghost, SpriteRenderer ghostSR)
    {
        float elapsed = 0f;

        while (elapsed < ghostLifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float alpha = Mathf.Lerp(ghostStartAlpha, 0f, elapsed / ghostLifetime);

            if (ghostSR != null)
            {
                Color c = ghostSR.color;
                c.a = alpha;
                ghostSR.color = c;
            }

            yield return null;   // wait one frame
        }

        if (ghost != null)
            Destroy(ghost);
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    /// <summary>Returns true while the Sandevistan effect is active.</summary>
    public bool IsActive => isActive;

    /// <summary>Remaining cooldown in real-world seconds.</summary>
    public float CooldownRemaining => Mathf.Max(0f, nextActivationTime - Time.unscaledTime);
}
