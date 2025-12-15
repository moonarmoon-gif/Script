using UnityEngine;

/// <summary>
/// Automatically updates position of camera children based on camera size.
/// Useful for spawn points (minPos, maxPos) that should stay at screen edges.
/// </summary>
public class CameraChildPositionScaler : MonoBehaviour
{
    [Header("Position Settings")]
    [Tooltip("Viewport position (0-1 range, where 0.5 is center)")]
    [SerializeField] private Vector2 viewportPosition = new Vector2(1.1f, 0.5f);
    
    [Tooltip("Additional world space offset after viewport calculation")]
    [SerializeField] private Vector2 worldOffset = Vector2.zero;
    
    [Tooltip("Update position every frame")]
    [SerializeField] private bool updateEveryFrame = true;
    
    [Tooltip("Reference camera (leave null to use Camera.main)")]
    [SerializeField] private Camera targetCamera;

    private float lastCameraSize;
    private float lastCameraAspect;

    void Start()
    {
        if (targetCamera == null)
            targetCamera = Camera.main;
        
        if (targetCamera != null)
        {
            lastCameraSize = targetCamera.orthographicSize;
            lastCameraAspect = targetCamera.aspect;
            UpdatePosition();
        }
    }

    void LateUpdate()
    {
        if (!updateEveryFrame || targetCamera == null) return;
        
        // Check if camera size or aspect changed
        bool sizeChanged = Mathf.Abs(targetCamera.orthographicSize - lastCameraSize) > 0.001f;
        bool aspectChanged = Mathf.Abs(targetCamera.aspect - lastCameraAspect) > 0.001f;
        
        if (sizeChanged || aspectChanged)
        {
            lastCameraSize = targetCamera.orthographicSize;
            lastCameraAspect = targetCamera.aspect;
            UpdatePosition();
        }
    }

    void UpdatePosition()
    {
        if (targetCamera == null) return;
        
        // Calculate camera dimensions
        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        
        // Convert viewport position to world position
        Vector3 worldPos = targetCamera.transform.position;
        worldPos.x += (viewportPosition.x - 0.5f) * width;
        worldPos.y += (viewportPosition.y - 0.5f) * height;
        
        // Add world offset
        worldPos.x += worldOffset.x;
        worldPos.y += worldOffset.y;
        
        // Keep original Z
        worldPos.z = transform.position.z;
        
        transform.position = worldPos;
    }

    // Call this manually if you change camera size via script
    public void OnCameraSizeChanged()
    {
        UpdatePosition();
    }

    void OnValidate()
    {
        // Update in editor when values change
        if (Application.isPlaying && targetCamera != null)
        {
            UpdatePosition();
        }
    }

    void OnDrawGizmosSelected()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (targetCamera == null) return;
        
        // Draw line from camera center to this position
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(targetCamera.transform.position, transform.position);
        
        // Draw sphere at position
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
        
        // Draw viewport position indicator
        float height = targetCamera.orthographicSize * 2f;
        float width = height * targetCamera.aspect;
        
        Vector3 viewportWorldPos = targetCamera.transform.position;
        viewportWorldPos.x += (viewportPosition.x - 0.5f) * width;
        viewportWorldPos.y += (viewportPosition.y - 0.5f) * height;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(viewportWorldPos, 0.2f);
    }
}
