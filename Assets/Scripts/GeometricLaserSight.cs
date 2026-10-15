using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GeometricLaserSight – Real-time ricochet preview laser using Physics2D.Raycast
/// and Vector2.Reflect math. Showcases the geometry theme to hackathon judges.
/// ──────────────────────────────────────────────────────────────────────────────
/// SETUP (2 steps only!):
///   1. Add this script to the Player GameObject.
///   2. Assign firePoint (same as PlayerController's firePoint).
///   Everything else (LineRenderer, material, colors) is auto-configured in Awake().
///
/// TOGGLE: Press 'V' to toggle the laser on/off.
/// ──────────────────────────────────────────────────────────────────────────────
/// </summary>
public class GeometricLaserSight : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The FirePoint transform (same as PlayerController's FirePoint).")]
    public Transform firePoint;

    [Tooltip("The WeaponPivot transform (same as PlayerController's WeaponPivot).")]
    public Transform weaponPivot;

    [Header("Laser Settings")]
    [Tooltip("Maximum number of bounces to preview.")]
    public int previewBounces = 2;

    [Tooltip("Maximum ray distance per segment.")]
    public float maxRayDistance = 50f;

    [Tooltip("Tag that triggers reflection.")]
    public string bounceTag = "BounceWall";

    [Header("Toggle")]
    [Tooltip("Key to toggle the laser on/off.")]
    public KeyCode toggleKey = KeyCode.V;

    [Header("Visual")]
    [Tooltip("Starting line width.")]
    public float lineWidth = 0.04f;

    [Tooltip("Start color of the laser.")]
    public Color startColor = new Color(0f, 1f, 1f, 0.9f); // Cyan

    [Tooltip("End color of the laser (after bounces).")]
    public Color endColor = new Color(0.5f, 0f, 1f, 0.4f); // Purple fade

    // ───────── Private ─────────
    private LineRenderer lineRenderer;
    private bool isEnabled = true;
    private Camera mainCam;

    // Layer masks
    private int wallAndEnemyMask;

    private void Awake()
    {
        mainCam = Camera.main;

        // ── Auto-create and configure LineRenderer ──
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        // Use default sprite material (URP-compatible, no manual setup needed)
        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth * 0.5f;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 10;
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;

        // Gradient for multi-bounce visual escalation
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(new Color(0f, 0.8f, 1f, 1f), 0.5f),
                new GradientColorKey(endColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0.9f, 0f),
                new GradientAlphaKey(0.6f, 0.5f),
                new GradientAlphaKey(0.2f, 1f)
            }
        );
        lineRenderer.colorGradient = gradient;

        // Build layer mask: hit everything except "Bullet" layer
        int bulletLayer = LayerMask.NameToLayer("Bullet");
        int charBodyLayer = LayerMask.NameToLayer("CharacterBody");
        int charFeetLayer = LayerMask.NameToLayer("CharacterFeet");

        // We want to hit Default (BounceWalls) and CharacterBody (hitboxes)
        // but NOT bullets or CharacterFeet
        wallAndEnemyMask = ~0; // Start with everything
        if (bulletLayer != -1) wallAndEnemyMask &= ~(1 << bulletLayer);
        if (charFeetLayer != -1) wallAndEnemyMask &= ~(1 << charFeetLayer);
    }

    private void Update()
    {
        // Toggle
        bool togglePressed = false;
        if (Keyboard.current != null)
        {
            togglePressed = Keyboard.current.vKey.wasPressedThisFrame;
        }
        else
        {
            togglePressed = Input.GetKeyDown(toggleKey);
        }

        if (togglePressed)
        {
            isEnabled = !isEnabled;
        }

        if (!isEnabled || firePoint == null)
        {
            lineRenderer.positionCount = 0;
            return;
        }

        DrawLaser();
    }

    private void DrawLaser()
    {
        // ── Calculate aim direction from weaponPivot rotation ──
        Vector2 aimDirection;
        if (weaponPivot != null)
        {
            aimDirection = weaponPivot.right;
        }
        else
        {
            // Fallback: use mouse position
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 mousePos;
            if (Mouse.current != null)
                mousePos = Mouse.current.position.ReadValue();
            else
                mousePos = Input.mousePosition;

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(mousePos);
            mouseWorld.z = 0f;
            aimDirection = ((Vector2)(mouseWorld - firePoint.position)).normalized;
        }

        // ── Raycast loop with reflections ──
        Vector2 origin = firePoint.position;
        Vector2 direction = aimDirection.normalized;

        // Max points: origin + (bounce points) + terminal point
        Vector3[] points = new Vector3[previewBounces + 2];
        int pointCount = 0;

        points[pointCount++] = origin;

        for (int bounce = 0; bounce <= previewBounces; bounce++)
        {
            RaycastHit2D hit = Physics2D.Raycast(origin, direction, maxRayDistance, wallAndEnemyMask);

            if (hit.collider != null)
            {
                points[pointCount++] = hit.point;

                // Check if it's a BounceWall — reflect and continue
                if (hit.collider.CompareTag(bounceTag) && bounce < previewBounces)
                {
                    direction = Vector2.Reflect(direction, hit.normal);
                    origin = hit.point + hit.normal * 0.05f; // Offset slightly to avoid re-hitting
                }
                else
                {
                    // Hit an enemy or non-bouncy surface — terminate
                    break;
                }
            }
            else
            {
                // No hit — extend to max distance
                points[pointCount++] = origin + direction * maxRayDistance;
                break;
            }
        }

        // ── Apply to LineRenderer ──
        lineRenderer.positionCount = pointCount;
        for (int i = 0; i < pointCount; i++)
        {
            lineRenderer.SetPosition(i, points[i]);
        }
    }

    // ──────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────
    public bool IsLaserEnabled => isEnabled;

    public void SetEnabled(bool enabled)
    {
        isEnabled = enabled;
        if (!isEnabled && lineRenderer != null)
        {
            lineRenderer.positionCount = 0;
        }
    }
}
