using UnityEngine;

/// <summary>
/// Smooth, jitter-free camera follow using Vector3.SmoothDamp in LateUpdate.
/// Locks onto the target's X/Y while preserving the camera's Z depth.
/// Uses Time.unscaledDeltaTime so it stays smooth during Sandevistan slow-mo.
///
/// ADD-ON: Static Shake() method for trauma-based camera shake that works
/// correctly during time dilation (uses unscaledDeltaTime).
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

    // ───────── Singleton-like static ref ─────────
    private static CameraFollow instance;

    // ───────── Private State ─────────
    private Vector3 velocity = Vector3.zero;

    // ───────── Shake State ─────────
    private float shakeDuration = 0f;
    private float shakeMagnitude = 0f;
    private float shakeElapsed = 0f;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

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

        // ── Apply Shake Offset ──
        if (shakeElapsed < shakeDuration)
        {
            shakeElapsed += Time.unscaledDeltaTime;
            float progress = shakeElapsed / shakeDuration;
            float dampedMagnitude = shakeMagnitude * (1f - progress); // Linear falloff

            Vector2 shakeOffset = Random.insideUnitCircle * dampedMagnitude;
            transform.position += new Vector3(shakeOffset.x, shakeOffset.y, 0f);
        }
    }

    // ──────────────────────────────────────────────────
    // Public Static API – Camera Shake
    // ──────────────────────────────────────────────────
    /// <summary>
    /// Triggers a camera shake. Uses unscaledDeltaTime so it works perfectly
    /// during Sandevistan time dilation.
    /// </summary>
    /// <param name="duration">Duration of the shake in real seconds.</param>
    /// <param name="magnitude">Maximum displacement in world units.</param>
    public static void Shake(float duration, float magnitude)
    {
        if (instance == null) return;

        // If a stronger shake is already running, only override if this one is stronger
        if (instance.shakeElapsed < instance.shakeDuration &&
            magnitude < instance.shakeMagnitude)
        {
            return;
        }

        instance.shakeDuration = duration;
        instance.shakeMagnitude = magnitude;
        instance.shakeElapsed = 0f;
    }
}
