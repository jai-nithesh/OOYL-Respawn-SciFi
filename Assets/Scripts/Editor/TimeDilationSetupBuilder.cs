#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

public static class TimeDilationSetupBuilder
{
    [MenuItem("Respawn/Setup Time Dilation & Verify EventSystem")]
    public static void SetupTimeDilationAndEventSystem()
    {
        Debug.Log("[TimeDilationSetupBuilder] Starting complete Time Dilation and EventSystem setup...");

        // ── 1. Verify EventSystem (Section 3I) ──
        VerifyEventSystem();

        // ── 2. Verify Main Camera (Section 3J) ──
        VerifyMainCamera();

        // ── 3. Configure Player Controller & Time Dilation ──
        ConfigurePlayerComponents();

        // ── 4. Configure UICanvas (Chrono Vignette & Status Text) ──
        ConfigureUIOverlay();

        // ── 5. Mark Dirty & Save ──
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        AssetDatabase.SaveAssets();

        Debug.Log("[TimeDilationSetupBuilder] Complete Time Dilation and EventSystem verification SUCCESSFUL!");
    }

    private static void VerifyEventSystem()
    {
        EventSystem es = Object.FindAnyObjectByType<EventSystem>();
        GameObject esObj;
        if (es == null)
        {
            esObj = new GameObject("EventSystem");
            es = esObj.AddComponent<EventSystem>();
            Debug.Log("[TimeDilationSetupBuilder] Created missing EventSystem.");
        }
        else
        {
            esObj = es.gameObject;
        }

        Undo.RecordObject(esObj, "Verify EventSystem");

        // Check for legacy StandaloneInputModule and remove if present
        StandaloneInputModule legacyModule = esObj.GetComponent<StandaloneInputModule>();
        if (legacyModule != null)
        {
            Object.DestroyImmediate(legacyModule);
            Debug.Log("[TimeDilationSetupBuilder] Removed legacy StandaloneInputModule.");
        }

        // Ensure InputSystemUIInputModule is present
        InputSystemUIInputModule inputModule = esObj.GetComponent<InputSystemUIInputModule>();
        if (inputModule == null)
        {
            inputModule = esObj.AddComponent<InputSystemUIInputModule>();
            Debug.Log("[TimeDilationSetupBuilder] Added InputSystemUIInputModule.");
        }

        EditorUtility.SetDirty(esObj);
        Debug.Log("[TimeDilationSetupBuilder] EventSystem verified with InputSystemUIInputModule.");
    }

    private static void VerifyMainCamera()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cam = Object.FindAnyObjectByType<Camera>();
        }

        if (cam == null)
        {
            Debug.LogWarning("[TimeDilationSetupBuilder] No Camera found in scene!");
            return;
        }

        GameObject camObj = cam.gameObject;
        Undo.RecordObject(camObj, "Verify Main Camera");

        camObj.tag = "MainCamera";
        cam.orthographic = true;
        if (cam.orthographicSize < 4f)
        {
            cam.orthographicSize = 5f;
        }

        CameraFollow follow = camObj.GetComponent<CameraFollow>();
        if (follow == null)
        {
            follow = camObj.AddComponent<CameraFollow>();
        }

        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            follow.target = player.transform;
        }
        follow.smoothTime = 0.15f;
        follow.offset = Vector3.zero;

        EditorUtility.SetDirty(camObj);
        Debug.Log("[TimeDilationSetupBuilder] Main Camera verified (Tag: MainCamera, Ortho: 5, Target: Player).");
    }

    private static void ConfigurePlayerComponents()
    {
        GameObject player = GameObject.FindWithTag("Player");
        if (player == null)
        {
            Debug.LogError("[TimeDilationSetupBuilder] No GameObject tagged 'Player' found in scene!");
            return;
        }

        Undo.RecordObject(player, "Configure Player Time Dilation");

        // 1. Configure PlayerController
        PlayerController pc = player.GetComponent<PlayerController>();
        if (pc != null)
        {
            // Restore proper player speed and fire rate if they were set too low
            if (pc.moveSpeed < 5f) pc.moveSpeed = 7f;
            if (pc.fireRate > 0.4f) pc.fireRate = 0.2f;

            pc.speedMultiplierDuringDilation = 1.4f;
            pc.fireRateMultiplierDuringDilation = 0.5f;
            EditorUtility.SetDirty(pc);
            Debug.Log($"[TimeDilationSetupBuilder] PlayerController calibrated (moveSpeed: {pc.moveSpeed}, fireRate: {pc.fireRate}, dilationBoost: {pc.speedMultiplierDuringDilation}x).");
        }

        // 2. Configure TimeDilation
        TimeDilation td = player.GetComponent<TimeDilation>();
        if (td == null)
        {
            td = player.AddComponent<TimeDilation>();
            Debug.Log("[TimeDilationSetupBuilder] Added TimeDilation component to Player.");
        }

        td.activationKey = KeyCode.LeftShift;
        td.duration = 4f;
        td.cooldown = 6f;
        td.dilatedTimeScale = 0.25f; // 25% world speed!
        td.playerSpeedMultiplier = 1.4f; // Hero is 40% faster!
        td.playerFireRateMultiplier = 0.5f; // Twice as fast firing!
        td.ghostInterval = 0.08f;
        td.ghostLifetime = 0.35f;
        td.ghostColor = new Color(0f, 0.9f, 1f, 0.7f); // Glowing cyber-cyan
        td.audioSlowPitch = 0.6f;

        EditorUtility.SetDirty(td);
        Debug.Log("[TimeDilationSetupBuilder] TimeDilation calibrated (dilatedTimeScale: 0.25x, duration: 4s, audioPitch: 0.6).");
    }

    private static void ConfigureUIOverlay()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[TimeDilationSetupBuilder] No Canvas found in scene.");
            return;
        }

        GameObject canvasObj = canvas.gameObject;
        Undo.RecordObject(canvasObj, "Configure Chrono UI");

        // 1. Create or Find ChronoVignette
        Transform vigTr = canvasObj.transform.Find("ChronoVignette");
        GameObject vigObj;
        CanvasGroup cg;
        if (vigTr == null)
        {
            vigObj = new GameObject("ChronoVignette");
            vigObj.transform.SetParent(canvasObj.transform, false);
            // Place it at the bottom of the canvas hierarchy so it renders behind text/buttons
            vigObj.transform.SetAsFirstSibling();

            RectTransform rt = vigObj.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.sizeDelta = Vector2.zero;
            rt.anchoredPosition = Vector2.zero;

            Image img = vigObj.AddComponent<Image>();
            img.color = new Color(0f, 0.6f, 1f, 0.28f);
            img.raycastTarget = false;

            cg = vigObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;

            Debug.Log("[TimeDilationSetupBuilder] Created ChronoVignette full-screen overlay.");
        }
        else
        {
            vigObj = vigTr.gameObject;
            cg = vigObj.GetComponent<CanvasGroup>();
            if (cg == null) cg = vigObj.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        // 2. Create or Find ChronoStatusText
        Transform txtTr = canvasObj.transform.Find("ChronoStatusText");
        GameObject txtObj;
        TextMeshProUGUI tmp;
        if (txtTr == null)
        {
            txtObj = new GameObject("ChronoStatusText");
            txtObj.transform.SetParent(canvasObj.transform, false);

            RectTransform rt = txtObj.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.93f);
            rt.anchorMax = new Vector2(0.5f, 0.93f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(650f, 40f);

            tmp = txtObj.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 20;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.text = "<color=#00FFFF>[L-SHIFT] CHRONO DILATION: READY</color>";

            Debug.Log("[TimeDilationSetupBuilder] Created ChronoStatusText HUD banner.");
        }
        else
        {
            txtObj = txtTr.gameObject;
            tmp = txtObj.GetComponent<TextMeshProUGUI>();
        }

        // 3. Connect to TimeDilation on Player
        GameObject player = GameObject.FindWithTag("Player");
        if (player != null)
        {
            TimeDilation td = player.GetComponent<TimeDilation>();
            if (td != null)
            {
                td.vignetteOverlay = cg;
                td.statusText = tmp;
                EditorUtility.SetDirty(td);
                Debug.Log("[TimeDilationSetupBuilder] Successfully wired ChronoVignette and ChronoStatusText to TimeDilation on Player!");
            }
        }
    }
}
#endif
