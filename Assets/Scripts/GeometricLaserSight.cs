using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// GeometricLaserSight – Real-time ricochet preview laser using Physics2D.Raycast
/// and Vector2.Reflect math. Showcases the geometry theme to hackathon judges.
/// ──────────────────────────────────────────────────────────────────────────────
/// SETUP:
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
    public float maxRayDistance = 40f;

    [Tooltip("Tag that triggers reflection.")]
    public string bounceTag = "BounceWall";

    [Header("Toggle")]
    [Tooltip("Key to toggle the laser on/off.")]
    public KeyCode toggleKey = KeyCode.V;

    [Header("Visual Styling (Tweakable)")]
    [Tooltip("Starting line width for thin, clean laser beam.")]
    public float lineWidth = 0.025f;

    [Tooltip("Start color of the laser (sleek translucent cyan).")]
    public Color startColor = new Color(0f, 0.9f, 1f, 0.75f);

    [Tooltip("End color of the laser after bounces.")]
    public Color endColor = new Color(0.1f, 0.5f, 1f, 0.25f);

    // ───────── Private ─────────
    private LineRenderer lineRenderer;
    private bool isEnabled = true;
    private Camera mainCam;
    private int raycastLayerMask;
    private readonly RaycastHit2D[] hitBuffer = new RaycastHit2D[16];

    private void Awake()
    {
        mainCam = Camera.main;

        // ── Auto-create and configure LineRenderer ──
        lineRenderer = GetComponent<LineRenderer>();
        if (lineRenderer == null)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
        }

        lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
        lineRenderer.startWidth = lineWidth;
        lineRenderer.endWidth = lineWidth * 0.7f;
        lineRenderer.startColor = startColor;
        lineRenderer.endColor = endColor;
        lineRenderer.positionCount = 0;
        lineRenderer.useWorldSpace = true;
        lineRenderer.sortingOrder = 15; // Above all sprites and walls
        lineRenderer.numCapVertices = 4;
        lineRenderer.numCornerVertices = 4;

        // Gradient for multi-bounce visual escalation
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(startColor, 0f),
                new GradientColorKey(new Color(0.2f, 0.85f, 1f), 0.5f),
                new GradientColorKey(endColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(startColor.a, 0f),
                new GradientAlphaKey(startColor.a * 0.6f, 0.5f),
                new GradientAlphaKey(endColor.a, 1f)
            }
        );
        lineRenderer.colorGradient = gradient;

        // Build layer mask: hit Default (walls) and CharacterBody (enemies)
        // Exclude Bullet and CharacterFeet
        int bulletLayer = LayerMask.NameToLayer("Bullet");
        int charFeetLayer = LayerMask.NameToLayer("CharacterFeet");

        raycastLayerMask = ~0;
        if (bulletLayer != -1) raycastLayerMask &= ~(1 << bulletLayer);
        if (charFeetLayer != -1) raycastLayerMask &= ~(1 << charFeetLayer);
    }

    private void Update()
    {
        // Toggle input
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
            if (lineRenderer != null && lineRenderer.positionCount > 0)
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
            if (mainCam == null) mainCam = Camera.main;
            if (mainCam == null) return;

            Vector3 mousePos = (Mouse.current != null) 
                ? (Vector3)Mouse.current.position.ReadValue() 
                : Input.mousePosition;

            Vector3 mouseWorld = mainCam.ScreenToWorldPoint(mousePos);
            mouseWorld.z = 0f;
            aimDirection = ((Vector2)(mouseWorld - firePoint.position)).normalized;
        }

        // ── Raycast loop with reflections ──
        Vector2 origin = firePoint.position;
        Vector2 direction = aimDirection.normalized;

        Vector3[] points = new Vector3[previewBounces + 2];
        int pointCount = 0;
        points[pointCount++] = origin;

        Transform myRoot = transform.root;
        Collider2D lastHitCollider = null;

        for (int bounce = 0; bounce <= previewBounces; bounce++)
        {
            int hitCount = Physics2D.RaycastNonAlloc(origin, direction, hitBuffer, maxRayDistance, raycastLayerMask);

            RaycastHit2D closestValidHit = default;
            bool hitFound = false;

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit2D h = hitBuffer[i];
                if (h.collider == null) continue;

                // CRITICAL FIX: Ignore Player's own colliders (Hitbox, feet, body)
                if (h.collider.transform.root == myRoot) continue;

                // Ignore the same collider we just bounced off if we are right on its surface
                if (lastHitCollider != null && h.collider == lastHitCollider && h.distance < 0.05f) continue;

                closestValidHit = h;
                hitFound = true;
                break;
            }

            if (hitFound)
            {
                points[pointCount++] = closestValidHit.point;
                lastHitCollider = closestValidHit.collider;

                // If it's a BounceWall, reflect and continue
                if (closestValidHit.collider.CompareTag(bounceTag) && bounce < previewBounces)
                {
                    Vector2 normal = closestValidHit.normal;
                    if (normal.sqrMagnitude < 0.001f) normal = -direction;

                    direction = Vector2.Reflect(direction, normal).normalized;
                    // Nudge origin away from surface along normal to avoid self-collision
                    origin = closestValidHit.point + normal * 0.04f;
                }
                else
                {
                    // Hit an enemy or non-bouncy surface — stop
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
