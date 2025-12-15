using UnityEngine;

/// <summary>
/// Utility class to check if enemies are within the damageable screen area.
/// Prevents projectiles from damaging enemies that are too far offscreen.
/// </summary>
public static class OffscreenDamageChecker
{
    /// <summary>
    /// Checks if an enemy is within the damageable area (on-screen or slightly offscreen).
    /// </summary>
    /// <param name="enemyPosition">World position of the enemy</param>
    /// <param name="camera">Main camera reference</param>
    /// <param name="offset">Offset value from AdvancedPlayerController (0.15 = 15% outside viewport)</param>
    /// <returns>True if enemy can take damage, false if too far offscreen</returns>
    public static bool CanTakeDamage(Vector3 enemyPosition, Camera camera, float offset)
    {
        if (camera == null)
        {
            // If no camera, allow damage (fallback)
            Debug.LogWarning("<color=yellow>OffscreenDamageChecker: Camera is null!</color>");
            return true;
        }

        // Convert world position to viewport position (0-1 range)
        Vector3 viewportPos = camera.WorldToViewportPoint(enemyPosition);
        
        // Check if within viewport bounds + offset
        // Viewport coordinates: (0,0) = bottom-left, (1,1) = top-right
        bool withinBounds = viewportPos.x >= -offset && viewportPos.x <= 1f + offset &&
                           viewportPos.y >= -offset && viewportPos.y <= 1f + offset &&
                           viewportPos.z > 0; // z > 0 means in front of camera
        
        return withinBounds;
    }
    
    /// <summary>
    /// Checks if an enemy can take damage using the offset from AdvancedPlayerController.
    /// </summary>
    public static bool CanTakeDamage(Vector3 enemyPosition)
    {
        if (AdvancedPlayerController.Instance == null)
        {
            Debug.LogWarning("<color=yellow>OffscreenDamageChecker: AdvancedPlayerController.Instance is null! Allowing damage as fallback.</color>");
            return true; // Fallback
        }
        
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogWarning("<color=yellow>OffscreenDamageChecker: Camera.main is null! Allowing damage as fallback.</color>");
            return true; // Fallback
        }
        
        float offset = AdvancedPlayerController.Instance.offscreenDamageOffset;
        return CanTakeDamage(enemyPosition, mainCam, offset);
    }
}
