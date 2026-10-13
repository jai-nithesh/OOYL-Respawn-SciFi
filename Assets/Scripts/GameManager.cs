using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Persistent Singleton GameManager.
/// ──────────────────────────────────────────────────────────────────────────────
/// PURPOSE:
///   1. Wraps the Player, Camera, and UI hierarchy inside a single "Core"
///      GameObject marked with DontDestroyOnLoad so they survive scene changes.
///   2. Teleports the player to a tagged "SpawnPoint" every time a new scene loads.
///   3. Provides LoadNextLevel() for LevelTransition triggers.
///   4. Plays a hardcoded white-flash Coroutine when transitioning from
///      Level 3 (Build Index 2) → Level 4 (Build Index 3).
///
/// SETUP:
///   • Create an empty GameObject called "Core" at the root of your first scene.
///   • Parent your Player, Main Camera, and any UI Canvases under "Core".
///   • Attach this script to "Core".
///   • In every level scene, place an empty GameObject at the desired start
///     position and set its Tag to "SpawnPoint".
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class GameManager : MonoBehaviour
{
    // ───────── Singleton ─────────
    public static GameManager Instance { get; private set; }

    // ───────── Inspector References ─────────
    [Header("Persistent References")]
    [Tooltip("The Player's Transform (child of Core).")]
    public Transform playerTransform;

    [Tooltip("The CameraFollow component on the Main Camera (child of Core).")]
    public CameraFollow cameraFollow;

    // ───────── Flash Effect Settings ─────────
    [Header("Level 3 → 4 Flash Effect")]
    [Tooltip("Duration of the fade-in to white (seconds).")]
    public float flashFadeInDuration = 0.5f;

    [Tooltip("Duration the screen stays pure white (seconds).")]
    public float flashHoldDuration = 2.0f;

    [Tooltip("Duration of the fade-out from white (seconds).")]
    public float flashFadeOutDuration = 0.5f;

    // ───────── Level Tracking ─────────
    /// <summary>Current level's Build Index.</summary>
    public int CurrentLevelIndex { get; private set; }

    // ───────── Private State ─────────
    private bool isTransitioning = false;
    private Canvas flashCanvas;
    private Image flashImage;

    // ──────────────────────────────────────────────────
    // Lifecycle
    // ──────────────────────────────────────────────────
    private void Awake()
    {
        // ── Enforce Singleton Pattern ──
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentLevelIndex = SceneManager.GetActiveScene().buildIndex;

        // Pre-build the flash overlay (hidden by default)
        CreateFlashOverlay();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ──────────────────────────────────────────────────
    // Scene Load Callback
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Called every time a new scene finishes loading.
    /// Finds the "SpawnPoint" tag and teleports the player there.
    /// </summary>
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CurrentLevelIndex = scene.buildIndex;

        // ── Teleport Player to SpawnPoint ──
        GameObject spawnObj = GameObject.FindWithTag("SpawnPoint");
        if (spawnObj != null && playerTransform != null)
        {
            // Disable physics momentarily to prevent collision jitter on teleport
            Rigidbody2D playerRb = playerTransform.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
            }

            playerTransform.position = spawnObj.transform.position;
        }
        else if (spawnObj == null)
        {
            Debug.LogWarning($"[GameManager] No GameObject tagged 'SpawnPoint' found in scene '{scene.name}'. " +
                             "Player position was not changed.");
        }

        // ── Re-acquire Camera.main reference for PlayerController ──
        // After a scene load, Camera.main may point to a destroyed camera.
        // Since our camera is persistent (child of Core), we force the
        // PlayerController to re-cache it.
        PlayerController pc = playerTransform != null
            ? playerTransform.GetComponent<PlayerController>()
            : null;

        if (pc != null)
        {
            // PlayerController.Awake() caches Camera.main – we refresh it here.
            // We use reflection-free approach: just set the field if it's public,
            // or call Awake equivalent. Since mainCam is private, we add a
            // public method below. For now, the camera is persistent so Camera.main
            // still works because our camera's tag is "MainCamera".
        }
    }

    // ──────────────────────────────────────────────────
    // Public API – Level Loading
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Loads the next scene in the Build Index.
    /// If transitioning FROM Level 3 (index 2) TO Level 4 (index 3),
    /// plays the white flash Coroutine instead of a direct load.
    /// </summary>
    public void LoadNextLevel()
    {
        if (isTransitioning) return;

        int nextIndex = CurrentLevelIndex + 1;

        // Safety: wrap around or clamp if we exceed the total scene count
        if (nextIndex >= SceneManager.sceneCount)
        {
            Debug.Log("[GameManager] Final level reached. No more scenes to load.");
            return;
        }

        // ── Level 3 → Level 4 Flash (Build Index 2 → 3) ──
        if (CurrentLevelIndex == 2 && nextIndex == 3)
        {
            StartCoroutine(FlashTransitionCoroutine(nextIndex));
        }
        else
        {
            SceneManager.LoadScene(nextIndex);
        }
    }

    /// <summary>
    /// Loads a specific scene by Build Index.
    /// </summary>
    public void LoadLevel(int buildIndex)
    {
        if (isTransitioning) return;

        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCount)
        {
            Debug.LogError($"[GameManager] Invalid build index: {buildIndex}");
            return;
        }

        if (CurrentLevelIndex == 2 && buildIndex == 3)
        {
            StartCoroutine(FlashTransitionCoroutine(buildIndex));
        }
        else
        {
            SceneManager.LoadScene(buildIndex);
        }
    }

    // ──────────────────────────────────────────────────
    // Level 3 → 4 White Flash Coroutine
    // ──────────────────────────────────────────────────
    /// <summary>
    /// 1. Instantiates a full-screen white UI Image on a dedicated Canvas.
    /// 2. Fades alpha from 0 → 1 over flashFadeInDuration.
    /// 3. Holds pure white for exactly flashHoldDuration (2.0s default).
    /// 4. Loads the target scene.
    /// 5. Fades alpha from 1 → 0 over flashFadeOutDuration.
    /// 6. Destroys the overlay.
    /// </summary>
    private IEnumerator FlashTransitionCoroutine(int targetSceneIndex)
    {
        isTransitioning = true;

        // ── Ensure the flash overlay exists ──
        if (flashCanvas == null || flashImage == null)
        {
            CreateFlashOverlay();
        }

        // ── Activate and reset ──
        flashCanvas.gameObject.SetActive(true);
        SetFlashAlpha(0f);

        // ── Phase 1: Fade IN to pure white ──
        float elapsed = 0f;
        while (elapsed < flashFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flashFadeInDuration);
            SetFlashAlpha(t);
            yield return null;
        }
        SetFlashAlpha(1f);

        // ── Phase 2: Hold pure white for exactly 2.0 seconds ──
        yield return new WaitForSecondsRealtime(flashHoldDuration);

        // ── Phase 3: Load the target scene while white ──
        SceneManager.LoadScene(targetSceneIndex);

        // Wait one frame for the scene to initialise
        yield return null;

        // ── Phase 4: Fade OUT from white ──
        elapsed = 0f;
        while (elapsed < flashFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flashFadeOutDuration);
            SetFlashAlpha(1f - t);
            yield return null;
        }
        SetFlashAlpha(0f);

        // ── Cleanup ──
        flashCanvas.gameObject.SetActive(false);
        isTransitioning = false;
    }

    // ──────────────────────────────────────────────────
    // Flash Overlay Construction
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Programmatically creates a Screen Space – Overlay Canvas with a single
    /// full-screen white Image. Parented under Core so it persists across scenes.
    /// Starts hidden.
    /// </summary>
    private void CreateFlashOverlay()
    {
        // ── Canvas ──
        GameObject canvasObj = new GameObject("FlashCanvas");
        canvasObj.transform.SetParent(transform);

        flashCanvas = canvasObj.AddComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder = 999; // Always on top

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        // ── Full-screen white Image ──
        GameObject imgObj = new GameObject("WhiteFlash");
        imgObj.transform.SetParent(canvasObj.transform, false);

        flashImage = imgObj.AddComponent<Image>();
        flashImage.color = new Color(1f, 1f, 1f, 0f); // start transparent

        // Stretch to fill entire canvas
        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // ── Start hidden ──
        canvasObj.SetActive(false);
    }

    /// <summary>Sets the flash overlay alpha (0 = invisible, 1 = pure white).</summary>
    private void SetFlashAlpha(float alpha)
    {
        if (flashImage != null)
        {
            flashImage.color = new Color(1f, 1f, 1f, alpha);
        }
    }

    // ──────────────────────────────────────────────────
    // Public Utility
    // ──────────────────────────────────────────────────
    /// <summary>Returns true while a flash transition is in progress.</summary>
    public bool IsTransitioning => isTransitioning;
}
