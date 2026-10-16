#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using TMPro;

public static class PrincessSetupBuilder
{
    private const string ControllerPath = "Assets/Sprites/Princess/Princess.controller";
    private const string BulletPrefabPath = "Assets/Prefabs/RicochetBullet.prefab";

    [MenuItem("Respawn/Setup Complete Princess (Animator & Components)")]
    public static void SetupPrincess()
    {
        Debug.Log("[PrincessSetupBuilder] Starting complete Princess setup...");

        // 1. Build and configure the Animator Controller
        SetupPrincessAnimatorController();

        // 2. Setup the Princess GameObject in the active scene
        SetupPrincessGameObject();

        // 3. Mark dirty and save
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("[PrincessSetupBuilder] Princess setup successfully completed and scene saved!");
    }

    private static void SetupPrincessAnimatorController()
    {
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            ctrl = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            Debug.Log($"[PrincessSetupBuilder] Created new AnimatorController at {ControllerPath}");
        }

        Undo.RecordObject(ctrl, "Configure Princess Animator Controller");

        // ── Ensure Parameters ──
        EnsureParameter(ctrl, "MoveX", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "MoveY", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "AimX", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "AimY", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(ctrl, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "ShootTrigger", AnimatorControllerParameterType.Trigger);

        // ── Load Animation Clips ──
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Princess/PrincessIDL.anim");
        AnimationClip walkUp = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Princess/PrincessUp.anim");
        AnimationClip walkDown = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Princess/PrincessDown.anim");
        AnimationClip walkLeft = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Princess/PrincessLeft.anim");
        AnimationClip walkRight = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Princess/PrincessRight.anim");

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

        EditorUtility.SetDirty(ctrl);
        Debug.Log("[PrincessSetupBuilder] Princess Animator Controller built with Idle and Walk BlendTree!");
    }

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < ctrl.parameters.Length; i++)
        {
            if (ctrl.parameters[i].name == name) return;
        }
        ctrl.AddParameter(name, type);
    }

    private static void SetupPrincessGameObject()
    {
        GameObject princess = GameObject.Find("Princess");

        if (princess == null)
        {
            Debug.LogError("[PrincessSetupBuilder] No 'Princess' GameObject found in active scene!");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(princess, "Setup Princess GameObject");

        // 1. Tag & Layer
        // Per spec: Tag is Untagged (NOT "Enemy" and NOT "Player") to avoid friendly fire issues
        princess.tag = "Untagged";

        int feetLayer = LayerMask.NameToLayer("CharacterFeet");
        if (feetLayer != -1) princess.layer = feetLayer;
        else princess.layer = LayerMask.NameToLayer("Default");

        // 2. SpriteRenderer
        SpriteRenderer sr = princess.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingOrder = 5;
        }

        // 3. Animator
        Animator anim = princess.GetComponent<Animator>();
        if (anim == null) anim = princess.AddComponent<Animator>();
        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl != null) anim.runtimeAnimatorController = ctrl;

        // 4. Rigidbody2D
        Rigidbody2D rb = princess.GetComponent<Rigidbody2D>();
        if (rb == null) rb = princess.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        // 5. BoxCollider2D (Feet solid collider)
        BoxCollider2D feetCollider = princess.GetComponent<BoxCollider2D>();
        if (feetCollider == null) feetCollider = princess.AddComponent<BoxCollider2D>();
        feetCollider.isTrigger = false;
        feetCollider.size = new Vector2(0.5f, 0.2f);
        feetCollider.offset = new Vector2(0f, -0.35f);

        // 6. Child WeaponPivot & FirePoint
        Transform weaponPivot = princess.transform.Find("WeaponPivot");
        if (weaponPivot == null)
        {
            GameObject wpObj = new GameObject("WeaponPivot");
            wpObj.transform.SetParent(princess.transform, false);
            wpObj.transform.localPosition = Vector3.zero;
            weaponPivot = wpObj.transform;
        }

        Transform firePoint = weaponPivot.Find("FirePoint");
        if (firePoint == null)
        {
            GameObject fpObj = new GameObject("FirePoint");
            fpObj.transform.SetParent(weaponPivot, false);
            fpObj.transform.localPosition = new Vector3(0.5f, 0f, 0f);
            firePoint = fpObj.transform;
        }

        // 7. Child Hitbox (CharacterBody trigger collider)
        Transform hitboxTrans = princess.transform.Find("Hitbox");
        GameObject hitboxObj;
        if (hitboxTrans == null)
        {
            hitboxObj = new GameObject("Hitbox");
            hitboxObj.transform.SetParent(princess.transform, false);
            hitboxObj.transform.localPosition = Vector3.zero;
        }
        else
        {
            hitboxObj = hitboxTrans.gameObject;
        }

        int bodyLayer = LayerMask.NameToLayer("CharacterBody");
        if (bodyLayer != -1) hitboxObj.layer = bodyLayer;
        else hitboxObj.layer = princess.layer;

        BoxCollider2D bodyCollider = hitboxObj.GetComponent<BoxCollider2D>();
        if (bodyCollider == null) bodyCollider = hitboxObj.AddComponent<BoxCollider2D>();
        bodyCollider.isTrigger = true;
        bodyCollider.size = new Vector2(0.6f, 1.0f);
        bodyCollider.offset = new Vector2(0f, 0f);

        // 8. Child PrincessCanvas (World Space Dialogue UI)
        Transform canvasTrans = princess.transform.Find("PrincessCanvas");
        GameObject canvasObj;
        if (canvasTrans == null)
        {
            canvasObj = new GameObject("PrincessCanvas", typeof(RectTransform));
            canvasObj.transform.SetParent(princess.transform, false);
            canvasObj.transform.localPosition = new Vector3(0f, 1.3f, 0f);
        }
        else
        {
            canvasObj = canvasTrans.gameObject;
        }

        Canvas princessCanvas = canvasObj.GetComponent<Canvas>();
        if (princessCanvas == null) princessCanvas = canvasObj.AddComponent<Canvas>();
        princessCanvas.renderMode = RenderMode.WorldSpace;
        princessCanvas.sortingOrder = 20;

        RectTransform canvasRt = canvasObj.GetComponent<RectTransform>();
        canvasRt.sizeDelta = new Vector2(400f, 100f);
        canvasRt.localScale = new Vector3(0.01f, 0.01f, 0.01f);

        Transform textTrans = canvasObj.transform.Find("PrincessText");
        GameObject textObj;
        if (textTrans == null)
        {
            textObj = new GameObject("PrincessText", typeof(RectTransform));
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
        dialogueTMP.fontSize = 22;
        dialogueTMP.fontStyle = FontStyles.Bold;
        dialogueTMP.alignment = TextAlignmentOptions.Center;
        dialogueTMP.color = new Color(0.3f, 1f, 0.9f, 1f); // Allied Cyan-Green

        // 9. PrincessController Component Setup
        PrincessController controller = princess.GetComponent<PrincessController>();
        if (controller == null) controller = princess.AddComponent<PrincessController>();

        controller.rescueRadius = 2.5f;
        controller.autoRescueOnProximity = true;
        controller.followDistance = 3f;
        controller.moveSpeed = 5f;

        controller.weaponPivot = weaponPivot;
        controller.firePoint = firePoint;

        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
        if (bulletPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("RicochetBullet t:Prefab");
            if (guids.Length > 0)
            {
                bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }
        }
        controller.bulletPrefab = bulletPrefab;

        controller.fireCooldown = 0.8f;
        controller.shootingRange = 10f;
        controller.playerTag = "Player";
        controller.enemyTag = "Enemy";

        controller.princessCanvas = princessCanvas;
        controller.princessText = dialogueTMP;

        controller.moveXParam = "MoveX";
        controller.moveYParam = "MoveY";
        controller.aimXParam = "AimX";
        controller.aimYParam = "AimY";
        controller.isMovingParam = "IsMoving";
        controller.speedParam = "Speed";
        controller.shootTriggerParam = "ShootTrigger";

        EditorUtility.SetDirty(princess);

        // 10. Also link Princess to BossController (Villan) if present
        GameObject villan = GameObject.Find("Villan");
        if (villan == null) villan = GameObject.Find("DarkWarlord");
        if (villan != null)
        {
            BossController boss = villan.GetComponent<BossController>();
            if (boss != null)
            {
                boss.princess = controller;
                EditorUtility.SetDirty(boss);
                Debug.Log("[PrincessSetupBuilder] Linked Princess reference to BossController on Villan!");
            }
        }

        Debug.Log("[PrincessSetupBuilder] Princess GameObject fully configured with Hitbox, WeaponPivot, FirePoint, Canvas, and PrincessController!");
    }
}
#endif
