using UnityEngine;

/// <summary>
/// LevelTransition – Trigger zone that loads the next level.
/// ──────────────────────────────────────────────────────────────────────────────
/// Place on an empty GameObject with a Collider2D (Is Trigger ☑).
/// When the Player enters the trigger, it calls GameManager.LoadNextLevel().
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class LevelTransition : MonoBehaviour
{
    [Tooltip("Tag of the entity that triggers the transition (default: Player).")]
    public string triggerTag = "Player";

    private bool hasTriggered = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Prevent double-triggering during the same transition
        if (hasTriggered) return;

        // Only the Player triggers the transition
        // Check both the collider's own tag AND the root parent's tag
        // (the Hitbox child might not have the "Player" tag, but the root does)
        bool isPlayer = other.CompareTag(triggerTag) ||
                        other.transform.root.CompareTag(triggerTag);

        if (!isPlayer) return;

        if (GameManager.Instance == null)
        {
            Debug.LogError("[LevelTransition] GameManager.Instance is null! " +
                           "Ensure GameManager exists in the scene on a persistent 'Core' GameObject.");
            return;
        }

        if (GameManager.Instance.IsTransitioning)
        {
            Debug.Log("[LevelTransition] Transition already in progress. Ignoring.");
            return;
        }

        hasTriggered = true;
        GameManager.Instance.LoadNextLevel();
    }
}
