using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GameManager – Bulletproof Persistent Singleton.
/// ──────────────────────────────────────────────────────────────────────────────
/// ARCHITECTURE:
///   Lives on the "Core" root GameObject in your first GAMEPLAY scene (Level 1).
///   DontDestroyOnLoad keeps Core (Player, Camera, UI) alive across all scenes.
///   Teleports the player to the scene's "SpawnPoint" after every load.
///   Destroys rogue cameras from newly loaded scenes to prevent conflicts.
///   Plays a configurable white-flash Coroutine for special transitions.
///   Manages persistent HUD (health bar), Death Screen, Win Screen, Boss Health Bar.
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

    // ───────── Hero Health UI ─────────
    [Header("Hero Health UI")]
    [Tooltip("The fill Image for the hero health bar (Image.type = Filled, Horizontal).")]
    public Image healthBarFill;

    [Tooltip("TMP text showing HP: X / MAX.")]
    public TextMeshProUGUI healthText;

    // ───────── Boss Health UI ─────────
    [Header("Boss Health UI (Optional)")]
    [Tooltip("Parent panel of the boss health bar (hidden when no boss).")]
    public GameObject bossHealthPanel;

    [Tooltip("The fill Image for the boss health bar.")]
    public Image bossHealthBarFill;

    [Tooltip("TMP text showing boss HP or name.")]
    public TextMeshProUGUI bossHealthText;

    // ───────── Death Screen ─────────
    [Header("Death Screen")]
    [Tooltip("The Death Screen panel inside UICanvas. Set inactive by default.")]
    public GameObject deathPanel;

    // ───────── Win Screen ─────────
    [Header("Win Screen")]
    [Tooltip("The Win Screen panel inside UICanvas. Set inactive by default.")]
    public GameObject winPanel;

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
    private PlayerController playerController;

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

        // Cache player controller
        if (playerTransform != null)
        {
            playerController = playerTransform.GetComponent<PlayerController>();
        }

        // Subscribe to health events
        if (playerController != null)
        {
            playerController.OnHealthChanged += UpdateHealthUI;
        }

        // Pre-build the flash overlay (hidden by default)
        CreateFlashOverlay();

        // Initialize UI states
        if (deathPanel != null) deathPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);
        if (bossHealthPanel != null) bossHealthPanel.SetActive(false);

        // Initialize health HUD
        if (playerController != null)
        {
            UpdateHealthUI(playerController.CurrentHealth, playerController.MaxHealth);
        }
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

        // Unsubscribe from health events
        if (playerController != null)
        {
            playerController.OnHealthChanged -= UpdateHealthUI;
        }
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
        DestroyRogueCameras();

        // ── Hide boss health bar on scene load (boss scenes will re-show it) ──
        HideBossHealthUI();

        // ── Deferred spawn teleport ──
        StartCoroutine(DeferredSpawnTeleport());
    }

    /// <summary>
    /// Waits one frame, then finds "SpawnPoint" and teleports the player there.
    /// </summary>
    private IEnumerator DeferredSpawnTeleport()
    {
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
        if (playerController == null && playerTransform != null)
        {
            playerController = playerTransform.GetComponent<PlayerController>();
            if (playerController != null)
            {
                playerController.OnHealthChanged += UpdateHealthUI;
            }
        }

        if (playerController != null)
        {
            playerController.RefreshCameraReference(persistentCamera);
        }
    }

    // ──────────────────────────────────────────────────
    // Rogue Camera Cleanup
    // ──────────────────────────────────────────────────
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
    // Hero Health UI
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Updates the persistent health bar and text. Bound to PlayerController.OnHealthChanged.
    /// </summary>
    public void UpdateHealthUI(int current, int max)
    {
        if (healthBarFill != null)
        {
            healthBarFill.fillAmount = (max > 0) ? (float)current / max : 0f;
        }

        if (healthText != null)
        {
            healthText.text = $"HP: {current} / {max}";
        }
    }

    // ──────────────────────────────────────────────────
    // Boss Health UI
    // ──────────────────────────────────────────────────
    /// <summary>Shows and updates the boss health bar.</summary>
    public void UpdateBossHealthUI(int current, int max, string bossName = "THE DARK WARLORD")
    {
        if (bossHealthPanel != null && !bossHealthPanel.activeSelf)
        {
            bossHealthPanel.SetActive(true);
        }

        if (bossHealthBarFill != null)
        {
            bossHealthBarFill.fillAmount = (max > 0) ? (float)current / max : 0f;
        }

        if (bossHealthText != null)
        {
            bossHealthText.text = $"{bossName}  {current} / {max}";
        }
    }

    /// <summary>Hides the boss health bar.</summary>
    public void HideBossHealthUI()
    {
        if (bossHealthPanel != null)
        {
            bossHealthPanel.SetActive(false);
        }
    }

    // ──────────────────────────────────────────────────
    // Death Screen
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Called by PlayerController when health reaches zero.
    /// Resets time, disables controls, and shows the death panel.
    /// </summary>
    public void TriggerDeathScreen()
    {
        // Ensure time is normal so UI is interactive
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (playerController != null)
        {
            playerController.DisableControls();
        }

        if (deathPanel != null)
        {
            deathPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Button callback: Retry the current level.
    /// </summary>
    public void RetryCurrentLevel()
    {
        if (deathPanel != null) deathPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        if (playerController != null)
        {
            playerController.ResetPlayerState();
        }

        int sceneIndex = SceneManager.GetActiveScene().buildIndex;
        SceneManager.LoadScene(sceneIndex);
    }

    /// <summary>
    /// Button callback: Restart the game from Level 1 (build index 1).
    /// </summary>
    public void RestartFromBeginning()
    {
        if (deathPanel != null) deathPanel.SetActive(false);
        if (winPanel != null) winPanel.SetActive(false);

        if (playerController != null)
        {
            playerController.ResetPlayerState();
        }

        // Level 1 is build index 1 (index 0 is Intro)
        SceneManager.LoadScene(1);
    }

    // ──────────────────────────────────────────────────
    // Win Screen
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Called by PrincessController when the victory sequence completes.
    /// </summary>
    public void TriggerWinScreen()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        if (playerController != null)
        {
            playerController.DisableControls();
        }

        if (winPanel != null)
        {
            winPanel.SetActive(true);
        }
    }

    /// <summary>
    /// Button callback: Quit the game.
    /// </summary>
    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("[GameManager] QuitGame() called — stopping play mode in Editor.");
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ──────────────────────────────────────────────────
    // Public API – Level Loading
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Loads the next scene in the Build Index.
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
    private IEnumerator FlashTransitionCoroutine(int targetSceneIndex)
    {
        isTransitioning = true;

        if (flashCanvas == null || flashImage == null)
        {
            CreateFlashOverlay();
        }

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

        // ── Phase 3: Load the target scene ──
        SceneManager.LoadScene(targetSceneIndex);

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

        flashCanvas.gameObject.SetActive(false);
        isTransitioning = false;
    }

    // ──────────────────────────────────────────────────
    // Flash Overlay Construction
    // ──────────────────────────────────────────────────
    private void CreateFlashOverlay()
    {
        GameObject canvasObj = new GameObject("FlashCanvas");
        canvasObj.transform.SetParent(transform);

        flashCanvas = canvasObj.AddComponent<Canvas>();
        flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        flashCanvas.sortingOrder = 999;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        canvasObj.AddComponent<GraphicRaycaster>();

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
