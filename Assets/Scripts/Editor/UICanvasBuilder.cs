#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using TMPro;

public static class UICanvasBuilder
{
    [MenuItem("Respawn/Build Complete UI Canvas")]
    public static void BuildUICanvas()
    {
        // 1. Find or verify UICanvas
        GameObject canvasObj = GameObject.Find("UICanvas");
        if (canvasObj == null)
        {
            Canvas existingCanvas = Object.FindAnyObjectByType<Canvas>();
            if (existingCanvas != null)
                canvasObj = existingCanvas.gameObject;
        }

        if (canvasObj == null)
        {
            Debug.LogError("[UICanvasBuilder] UICanvas not found in active scene!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(canvasObj, "Build Complete UI Canvas");

        // 2. Configure Canvas components
        Canvas canvas = canvasObj.GetComponent<Canvas>();
        if (canvas == null) canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        CanvasScaler scaler = canvasObj.GetComponent<CanvasScaler>();
        if (scaler == null) scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        if (canvasObj.GetComponent<GraphicRaycaster>() == null)
            canvasObj.AddComponent<GraphicRaycaster>();

        // 3. Remove placeholder 't1' if present
        Transform t1Transform = canvasObj.transform.Find("t1");
        if (t1Transform != null)
        {
            Undo.DestroyObjectImmediate(t1Transform.gameObject);
            Debug.Log("[UICanvasBuilder] Removed placeholder 't1'.");
        }

        // 4. Resolve default UI Sprite & Font
        Sprite uiSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        Sprite bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        if (uiSprite == null) uiSprite = bgSprite;

        TMP_FontAsset defaultFont = TMP_Settings.defaultFontAsset;

        // 5. HeroHealthBar
        // Hierarchy: HeroHealthBar -> HealthBarBG -> HealthBarFill + HealthText
        GameObject heroBarObj = GetOrCreateChild(canvasObj, "HeroHealthBar");
        RectTransform heroBarRect = heroBarObj.GetComponent<RectTransform>();
        SetAnchors(heroBarRect, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        heroBarRect.anchoredPosition = new Vector2(40, -40);
        heroBarRect.sizeDelta = new Vector2(300, 34);

        // HealthBarBG
        GameObject bgObj = GetOrCreateChild(heroBarObj, "HealthBarBG");
        RectTransform bgRect = bgObj.GetComponent<RectTransform>();
        SetStretch(bgRect);
        Image bgImg = GetOrAddComponent<Image>(bgObj);
        bgImg.sprite = uiSprite;
        bgImg.type = Image.Type.Sliced;
        bgImg.color = new Color(0.12f, 0.12f, 0.15f, 0.9f);

        // HealthBarFill
        GameObject fillObj = GetOrCreateChild(heroBarObj, "HealthBarFill");
        RectTransform fillRect = fillObj.GetComponent<RectTransform>();
        SetStretch(fillRect, new Vector2(3, 3), new Vector2(-3, -3));
        Image fillImg = GetOrAddComponent<Image>(fillObj);
        fillImg.sprite = uiSprite;
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1.0f;
        fillImg.color = new Color(0f, 0.95f, 0.75f, 1f); // Cyber Cyan/Emerald

        // HealthText
        GameObject healthTextObj = GetOrCreateChild(heroBarObj, "HealthText");
        RectTransform healthTextRect = healthTextObj.GetComponent<RectTransform>();
        SetStretch(healthTextRect);
        TextMeshProUGUI healthText = GetOrAddComponent<TextMeshProUGUI>(healthTextObj);
        if (defaultFont != null) healthText.font = defaultFont;
        healthText.text = "HP: 10 / 10";
        healthText.fontSize = 18;
        healthText.fontStyle = FontStyles.Bold;
        healthText.alignment = TextAlignmentOptions.Center;
        healthText.color = Color.white;

        // 6. BossHealthPanel (starts INACTIVE)
        GameObject bossPanelObj = GetOrCreateChild(canvasObj, "BossHealthPanel");
        RectTransform bossPanelRect = bossPanelObj.GetComponent<RectTransform>();
        SetAnchors(bossPanelRect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f));
        bossPanelRect.anchoredPosition = new Vector2(0, -60);
        bossPanelRect.sizeDelta = new Vector2(500, 42);

        GameObject bossBgObj = GetOrCreateChild(bossPanelObj, "BossHealthBarBG");
        RectTransform bossBgRect = bossBgObj.GetComponent<RectTransform>();
        SetStretch(bossBgRect);
        Image bossBgImg = GetOrAddComponent<Image>(bossBgObj);
        bossBgImg.sprite = uiSprite;
        bossBgImg.type = Image.Type.Sliced;
        bossBgImg.color = new Color(0.15f, 0.05f, 0.08f, 0.9f);

        GameObject bossFillObj = GetOrCreateChild(bossPanelObj, "BossHealthBarFill");
        RectTransform bossFillRect = bossFillObj.GetComponent<RectTransform>();
        SetStretch(bossFillRect, new Vector2(3, 3), new Vector2(-3, -3));
        Image bossFillImg = GetOrAddComponent<Image>(bossFillObj);
        bossFillImg.sprite = uiSprite;
        bossFillImg.type = Image.Type.Filled;
        bossFillImg.fillMethod = Image.FillMethod.Horizontal;
        bossFillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        bossFillImg.fillAmount = 1.0f;
        bossFillImg.color = new Color(1f, 0.18f, 0.28f, 1f); // Crimson Nova Red

        GameObject bossTextObj = GetOrCreateChild(bossPanelObj, "BossHealthText");
        RectTransform bossTextRect = bossTextObj.GetComponent<RectTransform>();
        SetStretch(bossTextRect);
        TextMeshProUGUI bossText = GetOrAddComponent<TextMeshProUGUI>(bossTextObj);
        if (defaultFont != null) bossText.font = defaultFont;
        bossText.text = "THE DARK WARLORD  25 / 25";
        bossText.fontSize = 20;
        bossText.fontStyle = FontStyles.Bold;
        bossText.alignment = TextAlignmentOptions.Center;
        bossText.color = Color.white;

        // 7. TelemetryText (for MathTelemetryHUD)
        GameObject telemetryObj = GetOrCreateChild(canvasObj, "TelemetryText");
        RectTransform telemetryRect = telemetryObj.GetComponent<RectTransform>();
        SetAnchors(telemetryRect, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f));
        telemetryRect.anchoredPosition = new Vector2(-30, 30);
        telemetryRect.sizeDelta = new Vector2(380, 130);
        TextMeshProUGUI telemetryTMP = GetOrAddComponent<TextMeshProUGUI>(telemetryObj);
        if (defaultFont != null) telemetryTMP.font = defaultFont;
        telemetryTMP.text = "CYBERNETIC TELEMETRY\nTHETA: 0.0° [0.00 rad]\nTIME: 1.00x | COMP: 1.00x\n|V|: 0.00 m/s";
        telemetryTMP.fontSize = 14;
        telemetryTMP.color = new Color(0f, 1f, 1f, 0.95f);
        telemetryTMP.alignment = TextAlignmentOptions.TopLeft;
        telemetryTMP.enableWordWrapping = false;
        telemetryTMP.overflowMode = TextOverflowModes.Overflow;

        // Attach MathTelemetryHUD to TelemetryText if not already on canvas/core
        MathTelemetryHUD hud = canvasObj.GetComponentInChildren<MathTelemetryHUD>(true);
        if (hud == null)
        {
            hud = telemetryObj.AddComponent<MathTelemetryHUD>();
        }
        hud.telemetryText = telemetryTMP;
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            hud.playerRb = playerObj.GetComponent<Rigidbody2D>();
            Transform wp = playerObj.transform.Find("WeaponPivot");
            if (wp != null) hud.weaponPivot = wp;
        }

        // 8. Find GameManager (lives on Core)
        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            GameObject coreObj = GameObject.Find("Core");
            if (coreObj != null)
            {
                gm = coreObj.GetComponent<GameManager>();
                if (gm == null) gm = coreObj.AddComponent<GameManager>();
            }
        }

        // 9. DeathPanel (starts INACTIVE)
        GameObject deathPanelObj = GetOrCreateChild(canvasObj, "DeathPanel");
        RectTransform deathPanelRect = deathPanelObj.GetComponent<RectTransform>();
        SetStretch(deathPanelRect);
        Image deathPanelBg = GetOrAddComponent<Image>(deathPanelObj);
        deathPanelBg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject deathTitleObj = GetOrCreateChild(deathPanelObj, "DeathTitle");
        RectTransform deathTitleRect = deathTitleObj.GetComponent<RectTransform>();
        SetAnchors(deathTitleRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        deathTitleRect.anchoredPosition = new Vector2(0, 110);
        deathTitleRect.sizeDelta = new Vector2(600, 80);
        TextMeshProUGUI deathTitle = GetOrAddComponent<TextMeshProUGUI>(deathTitleObj);
        if (defaultFont != null) deathTitle.font = defaultFont;
        deathTitle.text = "MISSION FAILED";
        deathTitle.fontSize = 52;
        deathTitle.fontStyle = FontStyles.Bold;
        deathTitle.alignment = TextAlignmentOptions.Center;
        deathTitle.color = new Color(1f, 0.22f, 0.22f, 1f);

        // RetryButton
        GameObject retryBtnObj = GetOrCreateChild(deathPanelObj, "RetryButton");
        RectTransform retryBtnRect = retryBtnObj.GetComponent<RectTransform>();
        SetAnchors(retryBtnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        retryBtnRect.anchoredPosition = new Vector2(0, 10);
        retryBtnRect.sizeDelta = new Vector2(280, 56);
        Image retryImg = GetOrAddComponent<Image>(retryBtnObj);
        retryImg.sprite = uiSprite;
        retryImg.type = Image.Type.Sliced;
        retryImg.color = new Color(0.2f, 0.22f, 0.28f, 1f);
        Button retryBtn = GetOrAddComponent<Button>(retryBtnObj);
        SetupButtonColors(retryBtn, new Color(0.2f, 0.22f, 0.28f, 1f), new Color(0.28f, 0.32f, 0.4f, 1f));

        GameObject retryTextObj = GetOrCreateChild(retryBtnObj, "Text (TMP)");
        RectTransform retryTextRect = retryTextObj.GetComponent<RectTransform>();
        SetStretch(retryTextRect);
        TextMeshProUGUI retryText = GetOrAddComponent<TextMeshProUGUI>(retryTextObj);
        if (defaultFont != null) retryText.font = defaultFont;
        retryText.text = "RETRY LEVEL";
        retryText.fontSize = 20;
        retryText.fontStyle = FontStyles.Bold;
        retryText.alignment = TextAlignmentOptions.Center;
        retryText.color = Color.white;

        // RestartButton
        GameObject restartBtnObj = GetOrCreateChild(deathPanelObj, "RestartButton");
        RectTransform restartBtnRect = restartBtnObj.GetComponent<RectTransform>();
        SetAnchors(restartBtnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        restartBtnRect.anchoredPosition = new Vector2(0, -65);
        restartBtnRect.sizeDelta = new Vector2(280, 56);
        Image restartImg = GetOrAddComponent<Image>(restartBtnObj);
        restartImg.sprite = uiSprite;
        restartImg.type = Image.Type.Sliced;
        restartImg.color = new Color(0.16f, 0.16f, 0.2f, 1f);
        Button restartBtn = GetOrAddComponent<Button>(restartBtnObj);
        SetupButtonColors(restartBtn, new Color(0.16f, 0.16f, 0.2f, 1f), new Color(0.24f, 0.24f, 0.3f, 1f));

        GameObject restartTextObj = GetOrCreateChild(restartBtnObj, "Text (TMP)");
        RectTransform restartTextRect = restartTextObj.GetComponent<RectTransform>();
        SetStretch(restartTextRect);
        TextMeshProUGUI restartText = GetOrAddComponent<TextMeshProUGUI>(restartTextObj);
        if (defaultFont != null) restartText.font = defaultFont;
        restartText.text = "RESTART FROM BEGINNING";
        restartText.fontSize = 17;
        restartText.fontStyle = FontStyles.Bold;
        restartText.alignment = TextAlignmentOptions.Center;
        restartText.color = new Color(0.88f, 0.88f, 0.92f, 1f);

        // Wire Button Listeners to GameManager
        if (gm != null)
        {
            // Clear existing and add persistent listeners
            while (retryBtn.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(retryBtn.onClick, 0);
            UnityEventTools.AddPersistentListener(retryBtn.onClick, gm.RetryCurrentLevel);

            while (restartBtn.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(restartBtn.onClick, 0);
            UnityEventTools.AddPersistentListener(restartBtn.onClick, gm.RestartFromBeginning);
        }

        // 10. WinPanel (starts INACTIVE)
        GameObject winPanelObj = GetOrCreateChild(canvasObj, "WinPanel");
        RectTransform winPanelRect = winPanelObj.GetComponent<RectTransform>();
        SetStretch(winPanelRect);
        Image winPanelBg = GetOrAddComponent<Image>(winPanelObj);
        winPanelBg.color = new Color(0.02f, 0.05f, 0.15f, 0.9f); // Sci-Fi Dark Navy

        GameObject winTitleObj = GetOrCreateChild(winPanelObj, "WinTitle");
        RectTransform winTitleRect = winTitleObj.GetComponent<RectTransform>();
        SetAnchors(winTitleRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        winTitleRect.anchoredPosition = new Vector2(0, 110);
        winTitleRect.sizeDelta = new Vector2(700, 80);
        TextMeshProUGUI winTitle = GetOrAddComponent<TextMeshProUGUI>(winTitleObj);
        if (defaultFont != null) winTitle.font = defaultFont;
        winTitle.text = "SECTOR LIBERATED!";
        winTitle.fontSize = 50;
        winTitle.fontStyle = FontStyles.Bold;
        winTitle.alignment = TextAlignmentOptions.Center;
        winTitle.color = new Color(1f, 0.84f, 0.1f, 1f); // Gold

        // PlayAgainButton
        GameObject playAgainBtnObj = GetOrCreateChild(winPanelObj, "PlayAgainButton");
        RectTransform playAgainBtnRect = playAgainBtnObj.GetComponent<RectTransform>();
        SetAnchors(playAgainBtnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        playAgainBtnRect.anchoredPosition = new Vector2(0, 10);
        playAgainBtnRect.sizeDelta = new Vector2(280, 56);
        Image playAgainImg = GetOrAddComponent<Image>(playAgainBtnObj);
        playAgainImg.sprite = uiSprite;
        playAgainImg.type = Image.Type.Sliced;
        playAgainImg.color = new Color(0.12f, 0.45f, 0.35f, 1f); // Emerald
        Button playAgainBtn = GetOrAddComponent<Button>(playAgainBtnObj);
        SetupButtonColors(playAgainBtn, new Color(0.12f, 0.45f, 0.35f, 1f), new Color(0.18f, 0.6f, 0.46f, 1f));

        GameObject playAgainTextObj = GetOrCreateChild(playAgainBtnObj, "Text (TMP)");
        RectTransform playAgainTextRect = playAgainTextObj.GetComponent<RectTransform>();
        SetStretch(playAgainTextRect);
        TextMeshProUGUI playAgainText = GetOrAddComponent<TextMeshProUGUI>(playAgainTextObj);
        if (defaultFont != null) playAgainText.font = defaultFont;
        playAgainText.text = "PLAY AGAIN";
        playAgainText.fontSize = 20;
        playAgainText.fontStyle = FontStyles.Bold;
        playAgainText.alignment = TextAlignmentOptions.Center;
        playAgainText.color = Color.white;

        // QuitButton
        GameObject quitBtnObj = GetOrCreateChild(winPanelObj, "QuitButton");
        RectTransform quitBtnRect = quitBtnObj.GetComponent<RectTransform>();
        SetAnchors(quitBtnRect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
        quitBtnRect.anchoredPosition = new Vector2(0, -65);
        quitBtnRect.sizeDelta = new Vector2(280, 56);
        Image quitImg = GetOrAddComponent<Image>(quitBtnObj);
        quitImg.sprite = uiSprite;
        quitImg.type = Image.Type.Sliced;
        quitImg.color = new Color(0.25f, 0.15f, 0.2f, 1f);
        Button quitBtn = GetOrAddComponent<Button>(quitBtnObj);
        SetupButtonColors(quitBtn, new Color(0.25f, 0.15f, 0.2f, 1f), new Color(0.35f, 0.2f, 0.28f, 1f));

        GameObject quitTextObj = GetOrCreateChild(quitBtnObj, "Text (TMP)");
        RectTransform quitTextRect = quitTextObj.GetComponent<RectTransform>();
        SetStretch(quitTextRect);
        TextMeshProUGUI quitText = GetOrAddComponent<TextMeshProUGUI>(quitTextObj);
        if (defaultFont != null) quitText.font = defaultFont;
        quitText.text = "QUIT";
        quitText.fontSize = 18;
        quitText.fontStyle = FontStyles.Bold;
        quitText.alignment = TextAlignmentOptions.Center;
        quitText.color = new Color(0.9f, 0.9f, 0.9f, 1f);

        if (gm != null)
        {
            while (playAgainBtn.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(playAgainBtn.onClick, 0);
            UnityEventTools.AddPersistentListener(playAgainBtn.onClick, gm.RestartFromBeginning);

            while (quitBtn.onClick.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(quitBtn.onClick, 0);
            UnityEventTools.AddPersistentListener(quitBtn.onClick, gm.QuitGame);
        }

        // 11. Wire everything into GameManager
        if (gm != null)
        {
            Undo.RecordObject(gm, "Wire UI to GameManager");
            gm.healthBarFill = fillImg;
            gm.healthText = healthText;
            gm.bossHealthPanel = bossPanelObj;
            gm.bossHealthBarFill = bossFillImg;
            gm.bossHealthText = bossText;
            gm.deathPanel = deathPanelObj;
            gm.winPanel = winPanelObj;

            // Wire player and cameras if unassigned
            if (gm.playerTransform == null && playerObj != null)
                gm.playerTransform = playerObj.transform;

            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                if (gm.persistentCamera == null) gm.persistentCamera = mainCam;
                if (gm.cameraFollow == null) gm.cameraFollow = mainCam.GetComponent<CameraFollow>();
            }

            EditorUtility.SetDirty(gm);
            Debug.Log("[UICanvasBuilder] All UI elements successfully wired into GameManager!");
        }

        // 12. Deactivate panels per spec (starts INACTIVE)
        bossPanelObj.SetActive(false);
        deathPanelObj.SetActive(false);
        winPanelObj.SetActive(false);

        // 13. Save scene dirty state
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[UICanvasBuilder] UICanvas build complete! All panels created and styled.");
    }

    // ── Helper Utilities ──

    private static GameObject GetOrCreateChild(GameObject parent, string name)
    {
        Transform child = parent.transform.Find(name);
        if (child != null) return child.gameObject;

        GameObject newObj = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(newObj, "Create " + name);
        newObj.transform.SetParent(parent.transform, false);
        newObj.layer = parent.layer;
        return newObj;
    }

    private static T GetOrAddComponent<T>(GameObject obj) where T : Component
    {
        T comp = obj.GetComponent<T>();
        if (comp == null)
        {
            comp = Undo.AddComponent<T>(obj);
        }
        return comp;
    }

    private static void SetAnchors(RectTransform rt, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.pivot = pivot;
    }

    private static void SetStretch(RectTransform rt, Vector2? offsetMin = null, Vector2? offsetMax = null)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = offsetMin ?? Vector2.zero;
        rt.offsetMax = offsetMax ?? Vector2.zero;
    }

    private static void SetupButtonColors(Button btn, Color normal, Color highlighted)
    {
        ColorBlock cb = btn.colors;
        cb.normalColor = normal;
        cb.highlightedColor = highlighted;
        cb.pressedColor = normal * 0.75f;
        cb.selectedColor = highlighted;
        btn.colors = cb;
    }
}
#endif
