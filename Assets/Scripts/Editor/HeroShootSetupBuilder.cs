#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;

public static class HeroShootSetupBuilder
{
    private const string ControllerPath = "Assets/Sprites/Hero/hero-saber Idle.controller";

    [MenuItem("Respawn/Setup Player Shoot BlendTree")]
    public static void SetupPlayerShootBlendTree()
    {
        Debug.Log("[HeroShootSetupBuilder] Starting Player Shoot BlendTree setup...");

        AnimatorController ctrl = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (ctrl == null)
        {
            Debug.LogError($"[HeroShootSetupBuilder] AnimatorController not found at {ControllerPath}");
            return;
        }

        Undo.RecordObject(ctrl, "Configure Player Shoot BlendTree");

        // ── Ensure Parameters ──
        EnsureParameter(ctrl, "Horizontal", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "Vertical", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "Speed", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "IsMoving", AnimatorControllerParameterType.Bool);
        EnsureParameter(ctrl, "AimX", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "AimY", AnimatorControllerParameterType.Float);
        EnsureParameter(ctrl, "ShootTrigger", AnimatorControllerParameterType.Trigger);

        // ── Load Animation Clips ──
        AnimationClip idleClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Hero/PlayerIdle.anim");
        AnimationClip shootUp = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Hero/PlayerShootUp.anim");
        AnimationClip shootDown = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Hero/PlayerShootDown.anim");
        AnimationClip shootLeft = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Hero/PlayerShootLeft.anim");
        AnimationClip shootRight = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Sprites/Hero/PlayerShootRight.anim");

        AnimatorStateMachine stateMachine = ctrl.layers[0].stateMachine;

        // Clean up any old loose shoot states or previous Shoot state
        ChildAnimatorState[] existingStates = stateMachine.states;
        AnimatorState idleState = null;
        AnimatorState moveState = null;

        for (int i = 0; i < existingStates.Length; i++)
        {
            string sName = existingStates[i].state.name;
            if (sName == "PlayerShootUp" || sName == "PlayerShootDown" || 
                sName == "PlayerShootLeft" || sName == "PlayerShootRight" || 
                sName == "Shoot")
            {
                stateMachine.RemoveState(existingStates[i].state);
            }
            else if (sName == "Idle")
            {
                idleState = existingStates[i].state;
            }
            else if (sName == "MovementTree" || sName == "Walk" || sName == "Move")
            {
                moveState = existingStates[i].state;
            }
        }

        // If Idle state has no motion, assign the PlayerIdle clip
        if (idleState != null && idleState.motion == null && idleClip != null)
        {
            idleState.motion = idleClip;
            Debug.Log("[HeroShootSetupBuilder] Assigned PlayerIdle.anim to Idle state.");
        }

        // ── Create Shoot 2D Directional Blend Tree ──
        BlendTree shootTree = new BlendTree();
        shootTree.name = "PlayerShootBlendTree";
        shootTree.blendType = BlendTreeType.SimpleDirectional2D;
        shootTree.blendParameter = "AimX";
        shootTree.blendParameterY = "AimY";

        if (shootUp != null) shootTree.AddChild(shootUp, new Vector2(0f, 1f));
        if (shootDown != null) shootTree.AddChild(shootDown, new Vector2(0f, -1f));
        if (shootLeft != null) shootTree.AddChild(shootLeft, new Vector2(-1f, 0f));
        if (shootRight != null) shootTree.AddChild(shootRight, new Vector2(1f, 0f));

        AssetDatabase.AddObjectToAsset(shootTree, ctrl);

        AnimatorState shootState = stateMachine.AddState("Shoot", new Vector3(380, 20, 0));
        shootState.motion = shootTree;

        // ── Transition: Any State -> Shoot ──
        AnimatorStateTransition anyToShoot = stateMachine.AddAnyStateTransition(shootState);
        anyToShoot.hasExitTime = false;
        anyToShoot.duration = 0f;
        anyToShoot.canTransitionToSelf = false;
        anyToShoot.AddCondition(AnimatorConditionMode.If, 0, "ShootTrigger");

        // ── Transition: Shoot -> Idle (when player stopped) ──
        if (idleState != null)
        {
            AnimatorStateTransition shootToIdle = shootState.AddTransition(idleState);
            shootToIdle.hasExitTime = true;
            shootToIdle.exitTime = 0.85f;
            shootToIdle.duration = 0.05f;
            shootToIdle.AddCondition(AnimatorConditionMode.Less, 0.01f, "Speed");
        }

        // ── Transition: Shoot -> MovementTree (when player moving) ──
        if (moveState != null)
        {
            AnimatorStateTransition shootToMove = shootState.AddTransition(moveState);
            shootToMove.hasExitTime = true;
            shootToMove.exitTime = 0.85f;
            shootToMove.duration = 0.05f;
            shootToMove.AddCondition(AnimatorConditionMode.Greater, 0.01f, "Speed");
        }

        EditorUtility.SetDirty(ctrl);
        AssetDatabase.SaveAssets();

        Debug.Log("[HeroShootSetupBuilder] Successfully built and connected Player Shoot BlendTree with 4 directions!");
    }

    private static void EnsureParameter(AnimatorController ctrl, string name, AnimatorControllerParameterType type)
    {
        for (int i = 0; i < ctrl.parameters.Length; i++)
        {
            if (ctrl.parameters[i].name == name) return;
        }
        ctrl.AddParameter(name, type);
    }
}
#endif
