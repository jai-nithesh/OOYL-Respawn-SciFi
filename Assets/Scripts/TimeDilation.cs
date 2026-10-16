using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Sandevistan-style Time Dilation active skill.
/// Press Left Shift to drop world time scale to 0.25x while the Hero moves 1.4x faster
/// and fires twice as fast. Spawns glowing cyan cyber afterimages, warps audio pitch,
/// and pulses a full-screen Chrono vignette to strongly convey that time has slowed down.
/// </summary>
public class TimeDilation : MonoBehaviour
{
    // ───────── Activation ─────────
    [Header("Activation")]
    [Tooltip("Key to toggle Sandevistan time dilation.")]
    public KeyCode activationKey = KeyCode.LeftShift;

    [Tooltip("Duration the effect lasts (real-world seconds).")]
    public float duration = 4f;

    [Tooltip("Cooldown between uses (real-world seconds).")]
    public float cooldown = 6f;

    // ───────── Time Settings ─────────
    [Header("Time & Speed Multipliers (Tweakable)")]
    [Tooltip("World time scale while active (0.25 = 25% speed, enemies crawl).")]
    [Range(0.05f, 0.9f)]
    public float dilatedTimeScale = 0.25f;

    [Tooltip("Hero movement speed multiplier during Sandevistan (1.4 = 40% faster than normal).")]
    [Range(1f, 3f)]
    public float playerSpeedMultiplier = 1.4f;

    [Tooltip("Hero fire rate multiplier during Sandevistan (0.5 = twice as fast).")]
    [Range(0.2f, 1f)]
    public float playerFireRateMultiplier = 0.5f;

    // ───────── Ghosting / Afterimage ─────────
    [Header("Ghosting / Afterimage (Tweakable)")]
    [Tooltip("Interval (real-world seconds) between ghost spawns.")]
    public float ghostInterval = 0.08f;

    [Tooltip("How long each ghost persists before vanishing (real-world seconds).")]
    public float ghostLifetime = 0.35f;

    [Tooltip("Color tint for the ghost afterimage (electric neon cyan).")]
    public Color ghostColor = new Color(0f, 0.9f, 1f, 0.7f);

    [Tooltip("Sorting order offset for ghosts (behind the player).")]
    public int ghostSortingOrderOffset = -1;

    // ───────── Audio & Screen Effects ─────────
    [Header("Audio & Screen Effects (Tweakable)")]
    [Tooltip("Audio pitch during time dilation (0.6 = deep cinematic slow-mo).")]
    [Range(0.2f, 1f)]
    public float audioSlowPitch = 0.6f;

    [Tooltip("Optional CanvasGroup on the Chrono Vignette overlay.")]
    public CanvasGroup vignetteOverlay;

    [Tooltip("Optional TextMeshProUGUI element to display active countdown and cooldown.")]
    public TextMeshProUGUI statusText;

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
    private const float DefaultFixedDeltaTime = 0.02f;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        if (playerController == null)
            playerController = GetComponent<PlayerController>();

        if (playerSpriteRenderer == null)
            playerSpriteRenderer = GetComponent<SpriteRenderer>();

        // Auto-find UI references if unassigned
        FindUIReferences();
    }

    private void Start()
    {
        FindUIReferences();
        if (vignetteOverlay != null)
        {
            vignetteOverlay.alpha = 0f;
        }
        if (statusText != null)
        {
            statusText.text = "";
        }
    }

    private void FindUIReferences()
    {
        if (vignetteOverlay == null)
        {
            GameObject vigObj = GameObject.Find("ChronoVignette");
            if (vigObj != null)
            {
                vignetteOverlay = vigObj.GetComponent<CanvasGroup>();
            }
        }

        if (statusText == null)
        {
            GameObject txtObj = GameObject.Find("ChronoStatusText");
            if (txtObj != null)
            {
                statusText = txtObj.GetComponent<TextMeshProUGUI>();
            }
        }
    }

    private void Update()
    {
        // Check activation
        if (IsActivationKeyPressed() && !isActive && Time.unscaledTime >= nextActivationTime)
        {
            activeCoroutine = StartCoroutine(SandevistanRoutine());
        }

        // Update cooldown / ready UI if statusText assigned
        UpdateStatusUI();
    }

    private bool IsActivationKeyPressed()
    {
        if (Keyboard.current != null)
        {
            switch (activationKey)
            {
                case KeyCode.LeftShift:
                    return Keyboard.current.leftShiftKey.wasPressedThisFrame;
                case KeyCode.RightShift:
                    return Keyboard.current.rightShiftKey.wasPressedThisFrame;
                case KeyCode.Space:
                    return Keyboard.current.spaceKey.wasPressedThisFrame;
                case KeyCode.LeftControl:
                    return Keyboard.current.leftCtrlKey.wasPressedThisFrame;
                case KeyCode.E:
                    return Keyboard.current.eKey.wasPressedThisFrame;
                case KeyCode.Q:
                    return Keyboard.current.qKey.wasPressedThisFrame;
                case KeyCode.F:
                    return Keyboard.current.fKey.wasPressedThisFrame;
                default:
                    break;
            }
        }
        return Input.GetKeyDown(activationKey);
    }

    private void UpdateStatusUI()
    {
        if (statusText == null) return;

        if (isActive)
        {
            // Status text is actively updated in SandevistanRoutine
            return;
        }

        if (Time.unscaledTime < nextActivationTime)
        {
            float remainingCd = nextActivationTime - Time.unscaledTime;
            statusText.text = $"<color=#778899>[L-SHIFT] CHRONO COOLDOWN: {remainingCd:F1}s</color>";
        }
        else
        {
            statusText.text = "<color=#00FFFF>[L-SHIFT] CHRONO DILATION: READY</color>";
        }
    }

    // ──────────────────────────────────────────────────
    // Core Sandevistan Coroutine
    // ──────────────────────────────────────────────────
    private IEnumerator SandevistanRoutine()
    {
        isActive = true;

        // ── 1. Drop World Time Scale & Pitch ──
        Time.timeScale = dilatedTimeScale;
        Time.fixedDeltaTime = DefaultFixedDeltaTime * dilatedTimeScale;
        SetSceneAudioPitch(audioSlowPitch);

        // ── 2. Empower the Hero ──
        if (playerController != null)
        {
            playerController.useUnscaledTime = true;
            playerController.speedMultiplierDuringDilation = playerSpeedMultiplier;
            playerController.fireRateMultiplierDuringDilation = playerFireRateMultiplier;
        }

        // ── 3. Fade In Chrono Vignette ──
        if (vignetteOverlay != null)
        {
            vignetteOverlay.alpha = 0.5f;
        }

        // ── 4. Main Active Loop ──
        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (playerController != null && playerController.IsDead)
            {
                break;
            }

            float remaining = Mathf.Max(0f, duration - elapsed);
            if (statusText != null)
            {
                statusText.text = $"<color=#FFD700>⚡ SANDEVISTAN ACTIVE: {remaining:F1}s [{(dilatedTimeScale * 100f):F0}% WORLD SPEED] ⚡</color>";
            }

            // Pulse vignette subtly
            if (vignetteOverlay != null)
            {
                float pulse = 0.4f + Mathf.PingPong(elapsed * 2f, 0.2f);
                vignetteOverlay.alpha = pulse;
            }

            SpawnGhost();

            // Real-time wait (unaffected by timeScale)
            yield return new WaitForSecondsRealtime(ghostInterval);
            elapsed += ghostInterval;
        }

        // ── 5. Restore Normal Time & Pitch ──
        RestoreTimeScale();
    }

    private void RestoreTimeScale()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = DefaultFixedDeltaTime;
        SetSceneAudioPitch(1f);

        if (playerController != null)
        {
            playerController.useUnscaledTime = false;
            playerController.speedMultiplierDuringDilation = 1f;
            playerController.fireRateMultiplierDuringDilation = 1f;
        }

        if (vignetteOverlay != null)
        {
            vignetteOverlay.alpha = 0f;
        }

        isActive = false;
        nextActivationTime = Time.unscaledTime + cooldown;
    }

    private void SetSceneAudioPitch(float pitch)
    {
        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource s in sources)
        {
            if (s != null) s.pitch = pitch;
        }
    }

    private void OnDisable()
    {
        RestoreTimeScale();
    }

    private void OnDestroy()
    {
        RestoreTimeScale();
    }

    // ──────────────────────────────────────────────────
    // Ghosting / Afterimage Spawner
    // ──────────────────────────────────────────────────
    private void SpawnGhost()
    {
        if (playerSpriteRenderer == null) return;

        GameObject ghost = new GameObject("ChronoAfterimage");
        ghost.transform.position = transform.position;
        ghost.transform.rotation = transform.rotation;
        ghost.transform.localScale = transform.localScale;

        SpriteRenderer ghostSR = ghost.AddComponent<SpriteRenderer>();
        ghostSR.sprite = playerSpriteRenderer.sprite;
        ghostSR.sortingLayerName = playerSpriteRenderer.sortingLayerName;
        ghostSR.sortingOrder = playerSpriteRenderer.sortingOrder + ghostSortingOrderOffset;
        ghostSR.color = ghostColor;
        ghostSR.material = new Material(playerSpriteRenderer.material);

        StartCoroutine(FadeAndDestroyGhost(ghost, ghostSR));
    }

    private IEnumerator FadeAndDestroyGhost(GameObject ghost, SpriteRenderer ghostSR)
    {
        float elapsed = 0f;
        Color startCol = ghostColor;

        while (elapsed < ghostLifetime)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = elapsed / ghostLifetime;
            float alpha = Mathf.Lerp(startCol.a, 0f, t);

            if (ghostSR != null)
            {
                Color c = startCol;
                c.a = alpha;
                ghostSR.color = c;
            }

            yield return null;
        }

        if (ghost != null)
            Destroy(ghost);
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    public bool IsActive => isActive;
    public float CooldownRemaining => Mathf.Max(0f, nextActivationTime - Time.unscaledTime);
}
