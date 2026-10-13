using UnityEngine;

/// <summary>
/// Smooth, jitter-free camera follow using Vector3.SmoothDamp in LateUpdate.
/// Locks onto the target's X/Y while preserving the camera's Z depth.
/// Uses Time.unscaledDeltaTime so it stays smooth during Sandevistan slow-mo.
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The Transform to follow (assign the Player).")]
    public Transform target;

    [Header("Smoothing")]
    [Tooltip("Approximate time (seconds) to reach the target. Lower = snappier.")]
    public float smoothTime = 0.15f;

    [Tooltip("Offset from the target position (useful for look-ahead).")]
    public Vector3 offset = Vector3.zero;

    // ───────── Private State ─────────
    private Vector3 velocity = Vector3.zero;

    // ──────────────────────────────────────────────────
    // LateUpdate – runs after all Update() calls,
    // guaranteeing the target has already moved this frame.
    // ──────────────────────────────────────────────────
    private void LateUpdate()
    {
        if (target == null) return;

        // Desired position: target XY + original camera Z
        Vector3 desiredPos = new Vector3(
            target.position.x + offset.x,
            target.position.y + offset.y,
            transform.position.z                       // preserve depth
        );

        // SmoothDamp for buttery-smooth, frame-rate-independent follow.
        // We use Time.unscaledDeltaTime so the camera stays smooth
        // even when Time.timeScale is reduced (Sandevistan / slow-mo).
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPos,
            ref velocity,
            smoothTime,
            Mathf.Infinity,
            Time.unscaledDeltaTime
        );
    }
}
