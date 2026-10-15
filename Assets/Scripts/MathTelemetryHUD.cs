using UnityEngine;
using TMPro;

/// <summary>
/// MathTelemetryHUD – Real-time cybernetic math readout for the hackathon judges.
/// ──────────────────────────────────────────────────────────────────────────────
/// Displays:
///   • THETA: Atan2 weapon angle in degrees and radians
///   • TIME: Current timeScale and inverse player compensation multiplier
///   • |V|: Player velocity vector magnitude
///
/// SETUP:
///   1. Create a TextMeshPro - Text (UI) in UICanvas (bottom-right corner).
///   2. Assign it to GameManager → mathTelemetryText, OR
///      Assign it directly to this component's telemetryText field.
///   3. Font size 14, color cyan, monospace font recommended.
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class MathTelemetryHUD : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The TMP text element that displays the telemetry readout.")]
    public TextMeshProUGUI telemetryText;

    [Tooltip("The Player's WeaponPivot transform (for angle readout).")]
    public Transform weaponPivot;

    [Tooltip("The Player's Rigidbody2D (for velocity readout).")]
    public Rigidbody2D playerRb;

    [Header("Settings")]
    [Tooltip("Toggle visibility with this key.")]
    public KeyCode toggleKey = KeyCode.Tab;

    private bool isVisible = true;
    private PlayerController playerController;

    private void Start()
    {
        // Auto-find references if not assigned
        if (playerController == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerController = player.GetComponent<PlayerController>();
                if (playerRb == null) playerRb = player.GetComponent<Rigidbody2D>();
                if (weaponPivot == null)
                {
                    // Try to find WeaponPivot child
                    Transform wp = player.transform.Find("WeaponPivot");
                    if (wp != null) weaponPivot = wp;
                }
            }
        }
    }

    private void Update()
    {
        // Toggle
        if (UnityEngine.InputSystem.Keyboard.current != null)
        {
            if (UnityEngine.InputSystem.Keyboard.current.tabKey.wasPressedThisFrame)
                isVisible = !isVisible;
        }
        else
        {
            if (Input.GetKeyDown(toggleKey))
                isVisible = !isVisible;
        }

        if (telemetryText == null) return;

        if (!isVisible)
        {
            telemetryText.enabled = false;
            return;
        }

        telemetryText.enabled = true;
        UpdateReadout();
    }

    private void UpdateReadout()
    {
        // ── Weapon Angle (Atan2) ──
        float angleDeg = 0f;
        float angleRad = 0f;
        if (weaponPivot != null)
        {
            angleDeg = weaponPivot.eulerAngles.z;
            // Normalize to -180..180
            if (angleDeg > 180f) angleDeg -= 360f;
            angleRad = angleDeg * Mathf.Deg2Rad;
        }

        // ── Time Dilation ──
        float timeScale = Time.timeScale;
        float invComp = (timeScale > 0f) ? (1f / timeScale) : 1f;

        // ── Velocity ──
        float velocityMag = 0f;
        if (playerRb != null)
        {
            velocityMag = playerRb.linearVelocity.magnitude;
        }

        // ── Build Readout ──
        telemetryText.text =
            $"<color=#00FFFF>┌─ MATH TELEMETRY ──────────────┐</color>\n" +
            $"<color=#00FFCC>│ THETA:</color> {angleDeg:F1}° ({angleRad:F2} rad)\n" +
            $"<color=#00FFCC>│ TIME:</color>  {timeScale:F2}x | INV-COMP: {invComp:F2}x\n" +
            $"<color=#00FFCC>│ |V|:</color>   {velocityMag:F1} m/s\n" +
            $"<color=#00FFFF>└───────────────────────────────┘</color>";
    }
}
