using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// LevelTransition – Trigger-based scene loader.
/// ──────────────────────────────────────────────────────────────────────────────
/// PURPOSE:
///   Attach this script to a GameObject with a 2D Collider set to "Is Trigger".
///   When the Player walks into it, the next scene in the Build Index is loaded
///   via GameManager.Instance.LoadNextLevel().
///
///   This ensures the flash effect logic (Level 3 → 4) is centralised in
///   GameManager and automatically triggered when appropriate.
///
/// SETUP:
///   1. Create an empty GameObject at the end of your level (e.g., a doorway).
///   2. Add a BoxCollider2D (or any 2D Collider) and check "Is Trigger".
///   3. Attach this script.
///   4. Set playerTag to "Player" (must match the tag on your Player GameObject).
///   5. Optionally enable useCustomTargetScene and set targetSceneBuildIndex
///      to load a specific scene instead of the next sequential one.
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class LevelTransition : MonoBehaviour
{
    // ───────── Settings ─────────
    [Header("Trigger Settings")]
    [Tooltip("Tag on the Player GameObject. Must match exactly.")]
    public string playerTag = "Player";

    [Header("Scene Targeting")]
    [Tooltip("If true, loads targetSceneBuildIndex instead of the next scene.")]
    public bool useCustomTargetScene = false;

    [Tooltip("Build Index of the scene to load (only used when useCustomTargetScene is true).")]
    public int targetSceneBuildIndex = -1;

    // ───────── Private State ─────────
    private bool hasTriggered = false;

    // ──────────────────────────────────────────────────
    // Trigger Detection
    // ──────────────────────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        // Prevent double-fires (e.g., multiple colliders on the player)
        if (hasTriggered) return;

        if (other.CompareTag(playerTag))
        {
            hasTriggered = true;

            if (GameManager.Instance != null)
            {
                if (useCustomTargetScene && targetSceneBuildIndex >= 0)
                {
                    GameManager.Instance.LoadLevel(targetSceneBuildIndex);
                }
                else
                {
                    GameManager.Instance.LoadNextLevel();
                }
            }
            else
            {
                // Fallback: if GameManager is missing, do a direct scene load
                Debug.LogWarning("[LevelTransition] GameManager.Instance is null! " +
                                 "Loading next scene directly.");

                int nextIndex = SceneManager.GetActiveScene().buildIndex + 1;
                if (nextIndex < SceneManager.sceneCount)
                {
                    SceneManager.LoadScene(nextIndex);
                }
                else
                {
                    Debug.LogError("[LevelTransition] No more scenes in Build Settings.");
                }
            }
        }
    }

    // ──────────────────────────────────────────────────
    // Editor Gizmos (visual aid in Scene View)
    // ──────────────────────────────────────────────────
    private void OnDrawGizmos()
    {
        // Draw a translucent green box so you can see the trigger zone in the editor
        Gizmos.color = new Color(0f, 1f, 0.3f, 0.3f);

        BoxCollider2D boxCol = GetComponent<BoxCollider2D>();
        if (boxCol != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(boxCol.offset, boxCol.size);
            Gizmos.DrawWireCube(boxCol.offset, boxCol.size);
        }
        else
        {
            // Fallback sphere for non-box colliders
            Gizmos.DrawWireSphere(transform.position, 1f);
        }
    }
}
