#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using TMPro;

public static class VillanSetupBuilder
{
    private const string ControllerPath = "Assets/Sprites/Villan/VillanAnimCtr.controller";
    private const string BulletPrefabPath = "Assets/Prefabs/EnemyRicochetBullet.prefab";

    [MenuItem("Respawn/Setup Complete Villan (Animator & Components)")]
    public static void SetupVillan()
    {
        Debug.Log("[VillanSetupBuilder] Starting complete Villan setup...");

        // 1. Build and configure the Animator Controller
        SetupVillanAnimatorController();

        // 2. Setup the Villan GameObject in the active scene
        SetupVillanGameObject();

        // 3. Mark dirty and save
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[VillanSetupBuilder] Villan setup successfully completed and scene saved!");
    }

    private static void SetupVillanAnimatorController()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Debug.Log($"[VillanSetupBuilder] Created new AnimatorController at {ControllerPath}");
        }

        Undo.RecordObject(ctrl, "Configure Villan Animator Controller");

        // ── Ensure Parameters ──
        EnsureParameter(ctrl, "MoveX", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "MoveY", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "AimX", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "AimY", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(ctrl, "ShootTrigger", AnimatorControllerParameterType.Trigger);

        // ── Load Animation Clips ──
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/Villan Idle.anim");
        AnimationClip walkUp = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanUP.anim");
        AnimationClip walkDown = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanDown.anim");
        AnimationClip walkLeft = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanLeft.anim");
        AnimationClip walkRight = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanRight.anim");

        AnimationClip shootUp = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanShootUP.anim");
        AnimationClip shootDown = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanShootDown.anim");
        AnimationClip shootLeft = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanShootLeft.anim");
        AnimationClip shootRight = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Villan/VillanShootRight.anim");

        AnimatorStateMachine stateMachine = ctrl.layers[0].stateMachine;

        // Clear existing states to ensure a clean, perfect graph
        ChildAnimatorState[] existingStates = stateMachine.states;
        for (int i = 0; i < existingStates.Length; i++)
        {
            stateMachine.RemoveState(existingStates[i].state);
        }

        // ── 1. Create Idle State ──
        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(250, 0, 0));
        idleState.motion = idleClip;
        stateMachine.defaultState = idleState;

        // ── 2. Create Walk Blend Tree ──
        BlendTree walkTree = new BlendTree();
        walkTree.name = "WalkBlendTree";
        walkTree.blendType = BlendTreeType.SimpleDirectional2D;
        walkTree.blendParameter = "MoveX";
        walkTree.blendParameterY = "MoveY";

        if (walkUp != null) walkTree.AddChild(walkUp, new Vector2(0f, 1f));
        if (walkDown != null) walkTree.AddChild(walkDown, new Vector2(0f, -1f));
        if (walkLeft != null) walkTree.AddChild(walkLeft, new Vector2(-1f, 0f));
        if (walkRight != null) walkTree.AddChild(walkRight, new Vector2(1f, 0f));

        AssetDatabase.AddObjectToAsset(walkTree, ctrl);

        AnimatorState walkState = stateMachine.AddState("Walk", new Vector3(250, 100, 0));
        walkState.motion = walkTree;

        // Transitions Idle <-> Walk
        AnimatorStateTransition idleToWalk = idleState.AddTransition(walkState);
        idleToWalk.hasExitTime = false;
        idleToWalk.duration = 0.1f;
        idleToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        AnimatorStateTransition walkToIdle = walkState.AddTransition(idleState);
        walkToIdle.hasExitTime = false;
        walkToIdle.duration = 0.1f;
        walkToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // ── 3. Create Shoot Blend Tree ──
        BlendTree shootTree = new BlendTree();
        shootTree.name = "ShootBlendTree";
        shootTree.blendType = BlendTreeType.SimpleDirectional2D;
        shootTree.blendParameter = "AimX";
        shootTree.blendParameterY = "AimY";

        if (shootUp != null) shootTree.AddChild(shootUp, new Vector2(0f, 1f));
        if (shootDown != null) shootTree.AddChild(shootDown, new Vector2(0f, -1f));
        if (shootLeft != null) shootTree.AddChild(shootLeft, new Vector2(-1f, 0f));
        if (shootRight != null) shootTree.AddChild(shootRight, new Vector2(1f, 0f));

        AssetDatabase.AddObjectToAsset(shootTree, ctrl);

        AnimatorState shootState = stateMachine.AddState("Shoot", new Vector3(550, 50, 0));
        shootState.motion = shootTree;

        // Transition Any State -> Shoot
        AnimatorStateTransition anyToShoot = stateMachine.AddAnyStateTransition(shootState);
        anyToShoot.hasExitTime = false;
        anyToShoot.duration = 0f;
        anyToShoot.canTransitionToSelf = false;
        anyToShoot.AddCondition(AnimatorConditionMode.If, 0, "ShootTrigger");

        // Transition Shoot -> Idle (when not moving)
        AnimatorStateTransition shootToIdle = shootState.AddTransition(idleState);
        shootToIdle.hasExitTime = true;
        shootToIdle.exitTime = 0.85f;
        shootToIdle.duration = 0.1f;
        shootToIdle.AddCondition(AnimatorConditionMode.IfNot, 0, "IsMoving");

        // Transition Shoot -> Walk (when moving)
        AnimatorStateTransition shootToWalk = shootState.AddTransition(walkState);
        shootToWalk.hasExitTime = true;
        shootToWalk.exitTime = 0.85f;
        shootToWalk.duration = 0.1f;
        shootToWalk.AddCondition(AnimatorConditionMode.If, 0, "IsMoving");

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[VillanSetupBuilder] Animator Controller built with Idle, Walk BlendTree, and Shoot BlendTree!");
    }

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < ctrl.parameters.Length; i++)
        {
            if (ctrl.parameters[i].name == name) return;
        }
        ctrl.AddParameter(name, type);
    }

    private static void SetupVillanGameObject()
    {
        GameObject villan = GameObject.Find("Villan");
        if (villan == null)
        {
            villan = GameObject.Find("DarkWarlord");
        }

        if (villan == null)
        {
            Debug.LogError("[VillanSetupBuilder] No 'Villan' or 'DarkWarlord' GameObject found in active scene!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(villan, "Setup Villan GameObject");

        // 1. Tag & Layer
        villan.tag = "Enemy";

        int feetLayer = LayerMask.NameToLayer("CharacterFeet");
        if (feetLayer != -1) villan.layer = feetLayer;
        else villan.layer = LayerMask.NameToLayer("Default");

        // 2. SpriteRenderer
        SpriteRenderer sr = villan.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 5;
        }

        // 3. Animator
        Animator anim = villan.GetComponent<Animator>();
        if (anim == null) anim = villan.AddComponent<Animator>();
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl != null) anim.runtimeAnimatorController = ctrl;

        // 4. Rigidbody2D
        Rigidbody2D rb = villan.GetComponent<Rigidbody2D>();
        if (rb == null) rb = villan.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 5. BoxCollider2D (Feet solid collider)
        BoxCollider2D feetCollider = villan.GetComponent<BoxCollider2D>();
        if (feetCollider == null) feetCollider = villan.AddComponent<BoxCollider2D>();
        feetCollider.isTrigger = false;
        feetCollider.size = new Vector2(0.6f, 0.25f);
        feetCollider.offset = new Vector2(0f, -0.4f);

        // 6. Child WeaponPivot & FirePoint
        Transform weaponPivot = villan.transform.Find("WeaponPivot");
        if (weaponPivot == null)
        {
            GameObject wpObj = new GameObject("WeaponPivot");
            wpObj.transform.SetParent(villan.transform, false);
            wpObj.transform.localPosition = Vector3.zero;
            weaponPivot = wpObj.transform;
        }

        Transform firePoint = weaponPivot.Find("FirePoint");
        if (firePoint == null)
        {
            GameObject fpObj = new GameObject("FirePoint");
            fpObj.transform.SetParent(weaponPivot, false);
            fpObj.transform.localPosition = new Vector3(0.6f, 0f, 0f);
            firePoint = fpObj.transform;
        }

        // 7. Child Hitbox (CharacterBody trigger collider + DamageReceiver)
        Transform hitboxTrans = villan.transform.Find("Hitbox");
        GameObject hitboxObj;
        if (hitboxTrans == null)
        {
            hitboxObj = new GameObject("Hitbox");
            hitboxObj.transform.SetParent(villan.transform, false);
            hitboxObj.transform.localPosition = Vector3.zero;
        }
        else
        {
            hitboxObj = hitboxTrans.gameObject;
        }

        int bodyLayer = LayerMask.NameToLayer("CharacterBody");
        if (bodyLayer != -1) hitboxObj.layer = bodyLayer;
        else hitboxObj.layer = villan.layer;

        BoxCollider2D bodyCollider = hitboxObj.GetComponent<BoxCollider2D>();
        if (bodyCollider == null) bodyCollider = hitboxObj.AddComponent<BoxCollider2D>();
        bodyCollider.isTrigger = true;
        bodyCollider.size = new Vector2(0.8f, 1.2f);
        bodyCollider.offset = new Vector2(0f, 0f);

        DamageReceiver damageReceiver = hitboxObj.GetComponent<DamageReceiver>();
        if (damageReceiver == null) damageReceiver = hitboxObj.AddComponent<DamageReceiver>();
        damageReceiver.hostileBulletTag = "PlayerBullet";
        damageReceiver.damagePerHit = 1;

        // 8. Child BossCanvas (World Space Dialogue UI)
        Transform canvasTrans = villan.transform.Find("BossCanvas");
        GameObject canvasObj;
        if (canvasTrans == null)
        {
            canvasObj = new GameObject("BossCanvas", typeof(RectTransform));
            canvasObj.transform.SetParent(villan.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 1.4f, 0f);
        }
        else
        {
            canvasObj = canvasTrans.gameObject;
        }

        Canvas bossCanvas = canvasObj.GetComponent<Canvas>();
        if (bossCanvas == null) bossCanvas = canvasObj.AddComponent<Canvas>();
        bossCanvas.renderMode = RenderMode.WorldSpace;
        bossCanvas.sortingOrder = 20;

        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(400f, 100f);
        canvasRt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        Transform textTrans = canvasObj.transform.Find("BossDialogueText");
        GameObject textObj;
        if (textTrans == null)
        {
            textObj = new GameObject("BossDialogueText", typeof(RectTransform));
            textObj.transform.SetParent(canvasObj.transform, false);
            textObj.transform.localPosition = Vector3.zero;
        }
        else
        {
            textObj = textTrans.gameObject;
        }

        RectTransform textRt = textObj.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        TextMeshProUGUI dialogueTMP = textObj.GetComponent<TextMeshProUGUI>();
        if (dialogueTMP == null) dialogueTMP = textObj.AddComponent<TextMeshProUGUI>();
        dialogueTMP.text = "";
        dialogueTMP.fontSize = 24;
        dialogueTMP.fontStyle = FontStyles.Bold;
        dialogueTMP.alignment = TextAlignmentOptions.Center;
        dialogueTMP.color = Color.white;

        // Canvas starts inactive until detection
        canvasObj.SetActive(false);

        // 9. BossController Component Setup
        BossController boss = villan.GetComponent<BossController>();
        if (boss == null) boss = villan.AddComponent<BossController>();

        boss.maxHealth = 25;
        boss.moveSpeed = 3.5f;
        boss.enragedMoveSpeed = 5.5f;
        boss.stoppingDistance = 3f;
        boss.strafeRadius = 4f;
        boss.detectionRadius = 15f;
        boss.playerTag = "Player";

        boss.weaponPivot = weaponPivot;
        boss.firePoint = firePoint;

        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
        if (bulletPrefab == null)
        {
            Debug.LogWarning($"[VillanSetupBuilder] Bullet prefab not found at {BulletPrefabPath}. Searching...");
            string[] guids = AssetDatabase.FindAssets("EnemyRicochetBullet t:Prefab");
            if (guids.Length > 0)
            {
                bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        boss.bulletPrefab = bulletPrefab;

        boss.fireRate = 1.2f;
        boss.enragedFireRate = 0.6f;
        boss.phase1FanAngles = new float[] { -15f, 0f, 15f };
        boss.phase2FanAngles = new float[] { -30f, -15f, 0f, 15f, 30f };
        boss.novaInterval = 4f;

        boss.bossCanvas = bossCanvas;
        boss.bossDialogueText = dialogueTMP;
        boss.enrageColor = new Color(1f, 0.3f, 0.3f, 1f);

        EditorUtility.SetDirty(villan);
        Debug.Log("[VillanSetupBuilder] Villan GameObject fully configured with Hitbox, WeaponPivot, FirePoint, Canvas, and BossController!");
    }
}
#endif
