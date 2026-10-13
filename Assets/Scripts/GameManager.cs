using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// GameManager – Bulletproof Persistent Singleton.
/// ──────────────────────────────────────────────────────────────────────────────
/// ARCHITECTURE:
///   Lives on the "Core" root GameObject in your first GAMEPLAY scene (Level 1).
///   DontDestroyOnLoad keeps Core (Player, Camera, UI) alive across all scenes.
///   Teleports the player to the scene's "SpawnPoint" after every load.
///   Destroys rogue cameras from newly loaded scenes to prevent conflicts.
///   Plays a configurable white-flash Coroutine for special transitions.
///
/// CRITICAL FIXES (v2):
///   • Strict singleton: duplicate's OnEnable is blocked via enabled=false so
///     it never double-subscribes to sceneLoaded.
///   • Deferred spawn: teleport waits one rendered frame so all scene objects
///     (SpawnPoint) are guaranteed active and findable.
///   • Rogue camera cleanup: any extra "MainCamera"-tagged cameras from new
///     scenes are destroyed on load.
///   • PlayerController camera reference is refreshed after every scene load.
///   • Flash transition indices are Inspector-configurable (not hardcoded).
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

    [Tooltip("The persistent Main Camera (child of Core). Used to destroy rogue cameras.")]
    public Camera persistentCamera;

    // ───────── Flash Effect Settings ─────────
    [Header("Flash Transition")]
    [Tooltip("Build Index of the scene to transition FROM (triggers the flash).")]
    public int flashFromIndex = 3;

    [Tooltip("Build Index of the scene to transition TO (loaded during the flash).")]
    public int flashToIndex = 4;

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
        // ── Strict Singleton ──
        if (Instance != null && Instance != this)
        {
            // CRITICAL: disable the component BEFORE Destroy to prevent
            // OnEnable from firing and double-subscribing to sceneLoaded.
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        CurrentLevelIndex = SceneManager.GetActiveScene().buildIndex;

        // Auto-find persistent camera if not assigned
        if (persistentCamera == null && cameraFollow != null)
        {
            persistentCamera = cameraFollow.GetComponent<Camera>();
        }

        // Pre-build the flash overlay (hidden by default)
        CreateFlashOverlay();
    }

    private void OnEnable()
    {
        // Only the true singleton instance subscribes
        if (Instance != this) return;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        // Clean up singleton reference if this instance is destroyed
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ──────────────────────────────────────────────────
    // Scene Load Callback
    // ──────────────────────────────────────────────────
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        CurrentLevelIndex = scene.buildIndex;

        // ── Kill rogue cameras ──
        // New scenes created from Unity's template include a Main Camera.
        // If left alive, two cameras render simultaneously and Camera.main
        // returns the wrong one, breaking PlayerController's Atan2 aiming.
        DestroyRogueCameras();

        // ── Deferred spawn teleport ──
        // Wait one rendered frame so every scene object (including SpawnPoint)
        // is guaranteed to be Awake, Start-ed, and active.
        StartCoroutine(DeferredSpawnTeleport());
    }

    /// <summary>
    /// Waits one frame, then finds "SpawnPoint" and teleports the player there.
    /// This guarantees all scene objects are fully initialised before we search.
    /// </summary>
    private IEnumerator DeferredSpawnTeleport()
    {
        // Wait until the end of the frame so all Awake/Start calls have finished
        yield return null;

        if (playerTransform == null)
        {
            Debug.LogError("[GameManager] playerTransform is null! Cannot teleport.");
            yield break;
        }

        GameObject spawnObj = GameObject.FindWithTag("SpawnPoint");
        if (spawnObj != null)
        {
            // Zero-out velocity before teleporting to prevent momentum carry-over
            Rigidbody2D playerRb = playerTransform.GetComponent<Rigidbody2D>();
            if (playerRb != null)
            {
                playerRb.linearVelocity = Vector2.zero;
                playerRb.angularVelocity = 0f;
            }

            // Teleport
            playerTransform.position = spawnObj.transform.position;

            // Snap camera immediately (no SmoothDamp lag after teleport)
            if (cameraFollow != null && cameraFollow.target != null)
            {
                Vector3 camPos = cameraFollow.transform.position;
                cameraFollow.transform.position = new Vector3(
                    spawnObj.transform.position.x,
                    spawnObj.transform.position.y,
                    camPos.z
                );
            }
        }
        else
        {
            Debug.LogWarning($"[GameManager] No 'SpawnPoint' tagged object found in scene " +
                             $"'{SceneManager.GetActiveScene().name}'. Player position unchanged.");
        }

        // ── Refresh PlayerController's camera reference ──
        PlayerController pc = playerTransform.GetComponent<PlayerController>();
        if (pc != null)
        {
            pc.RefreshCameraReference(persistentCamera);
        }
    }

    // ──────────────────────────────────────────────────
    // Rogue Camera Cleanup
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Finds all cameras tagged "MainCamera" and destroys any that are NOT
    /// our persistent camera. Prevents dual-rendering and Camera.main conflicts.
    /// </summary>
    private void DestroyRogueCameras()
    {
        if (persistentCamera == null) return;

        Camera[] allCameras = Camera.allCameras;
        for (int i = 0; i < allCameras.Length; i++)
        {
            if (allCameras[i] != persistentCamera)
            {
                Debug.Log($"[GameManager] Destroying rogue camera: '{allCameras[i].gameObject.name}' " +
                          $"in scene '{allCameras[i].gameObject.scene.name}'");
                Destroy(allCameras[i].gameObject);
            }
        }
    }

    // ──────────────────────────────────────────────────
    // Public API – Level Loading
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Loads the next scene in the Build Index.
    /// If transitioning from flashFromIndex → flashToIndex, plays the white flash.
    /// </summary>
    public void LoadNextLevel()
    {
        if (isTransitioning) return;

        int nextIndex = CurrentLevelIndex + 1;

        if (nextIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.Log("[GameManager] Final level reached. No more scenes to load.");
            return;
        }

        // ── Flash Transition Check ──
        if (CurrentLevelIndex == flashFromIndex && nextIndex == flashToIndex)
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

        if (buildIndex < 0 || buildIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError($"[GameManager] Invalid build index: {buildIndex}");
            return;
        }

        if (CurrentLevelIndex == flashFromIndex && buildIndex == flashToIndex)
        {
            StartCoroutine(FlashTransitionCoroutine(buildIndex));
        }
        else
        {
            SceneManager.LoadScene(buildIndex);
        }
    }

    // ──────────────────────────────────────────────────
    // Flash Transition Coroutine
    // ──────────────────────────────────────────────────
    /// <summary>
    /// 1. Fades screen to pure white.
    /// 2. Holds for flashHoldDuration (default 2.0s).
    /// 3. Loads the target scene.
    /// 4. Waits for DeferredSpawnTeleport to complete (via sceneLoaded).
    /// 5. Fades white out.
    /// </summary>
    private IEnumerator FlashTransitionCoroutine(int targetSceneIndex)
    {
        isTransitioning = true;

        // Ensure the flash overlay exists
        if (flashCanvas == null || flashImage == null)
        {
            CreateFlashOverlay();
        }

        // Activate and reset
        flashCanvas.gameObject.SetActive(true);
        SetFlashAlpha(0f);

        // ── Phase 1: Fade IN to pure white ──
        float elapsed = 0f;
        while (elapsed < flashFadeInDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFlashAlpha(Mathf.Clamp01(elapsed / flashFadeInDuration));
            yield return null;
        }
        SetFlashAlpha(1f);

        // ── Phase 2: Hold pure white ──
        yield return new WaitForSecondsRealtime(flashHoldDuration);

        // ── Phase 3: Load the target scene (OnSceneLoaded handles spawn) ──
        SceneManager.LoadScene(targetSceneIndex);

        // Wait TWO frames: one for sceneLoaded + DeferredSpawnTeleport to run
        yield return null;
        yield return null;

        // ── Phase 4: Fade OUT from white ──
        elapsed = 0f;
        while (elapsed < flashFadeOutDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            SetFlashAlpha(1f - Mathf.Clamp01(elapsed / flashFadeOutDuration));
            yield return null;
        }
        SetFlashAlpha(0f);

        // Cleanup
        flashCanvas.gameObject.SetActive(false);
        isTransitioning = false;
    }

    // ──────────────────────────────────────────────────
    // Flash Overlay Construction
    // ──────────────────────────────────────────────────
    private void CreateFlashOverlay()
    {
        // Canvas
        GameObject canvasObj = new GameObject("FlashCanvas");
        canvasObj.transform.SetParent(transform);

        flashCanvas = canvasObj.AddComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

        // Full-screen white Image
        GameObject imgObj = new GameObject("WhiteFlash");
        imgObj.transform.SetParent(canvasObj.transform, false);

        flashImage = imgObj.AddComponent<Image>();
        flashImage.color = new Color(1f, 1f, 1f, 0f);

        RectTransform rt = flashImage.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        canvasObj.SetActive(false);
    }

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
