using UnityEngine;

/// <summary>
/// Keeps the camera's bottom edge at a fixed world Y position when camera size changes.
/// When camera size increases, the camera moves UP so the bottom edge stays in place.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraBottomAnchor : MonoBehaviour
{
    [Header("Anchor Settings")]
    [Tooltip("The world Y position where the bottom edge should stay anchored")]
    [SerializeField] private float anchoredBottomY = -1.21f;
    
    [Tooltip("Enable to set anchored bottom Y to current bottom edge on start")]
    [SerializeField] private bool useCurrentBottomOnStart = true;
    
    [Tooltip("Update camera position every frame (disable if camera size doesn't change at runtime)")]
    [SerializeField] private bool updateEveryFrame = true;

    private Camera cam;
    private float lastCameraSize;

    void Start()
    {
        cam = GetComponent<Camera>();
        
        if (useCurrentBottomOnStart)
        {
            // Calculate current bottom edge Y
            anchoredBottomY = cam.transform.position.y - cam.orthographicSize;
            Debug.Log($"<color=cyan>CameraBottomAnchor: Set anchored bottom Y to {anchoredBottomY:F2}</color>");
        }
        
        lastCameraSize = cam.orthographicSize;
        AdjustCameraPosition();
    }

    void LateUpdate()
    {
        if (!updateEveryFrame) return;
        
        // Check if camera size changed
        if (Mathf.Abs(cam.orthographicSize - lastCameraSize) > 0.001f)
        {
            Debug.Log($"<color=cyan>CameraBottomAnchor: Camera size changed from {lastCameraSize:F2} to {cam.orthographicSize:F2}</color>");
            lastCameraSize = cam.orthographicSize;
            AdjustCameraPosition();
        }
    }

    void AdjustCameraPosition()
    {
        if (cam == null) return;
        
        // Calculate required camera Y position to keep bottom edge at anchoredBottomY
        // Bottom edge Y = Camera Y - orthographicSize
        // Therefore: Camera Y = anchoredBottomY + orthographicSize
        float requiredCameraY = anchoredBottomY + cam.orthographicSize;
        
        Vector3 newPos = cam.transform.position;
        newPos.y = requiredCameraY;
        cam.transform.position = newPos;
        
        Debug.Log($"<color=cyan>CameraBottomAnchor: Adjusted camera Y to {requiredCameraY:F2} (bottom edge at {anchoredBottomY:F2})</color>");
    }

    // Call this if you change camera size via script
    public void OnCameraSizeChanged()
    {
        AdjustCameraPosition();
    }

    void OnValidate()
    {
        // Update in editor when values change
        if (Application.isPlaying && cam != null)
        {
            AdjustCameraPosition();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) return;
        
        // Draw line showing anchored bottom edge
        float width = cam.orthographicSize * cam.aspect * 2f;
        Vector3 leftPoint = new Vector3(cam.transform.position.x - width / 2f, anchoredBottomY, 0f);
        Vector3 rightPoint = new Vector3(cam.transform.position.x + width / 2f, anchoredBottomY, 0f);
        
        Gizmos.color = Color.red;
        Gizmos.DrawLine(leftPoint, rightPoint);
        
        // Draw current bottom edge
        float currentBottom = cam.transform.position.y - cam.orthographicSize;
        Vector3 currentLeft = new Vector3(cam.transform.position.x - width / 2f, currentBottom, 0f);
        Vector3 currentRight = new Vector3(cam.transform.position.x + width / 2f, currentBottom, 0f);
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(currentLeft, currentRight);
    }
}
