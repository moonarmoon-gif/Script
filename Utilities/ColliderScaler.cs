using UnityEngine;

/// <summary>
/// Utility class to scale all types of 2D colliders including PolygonCollider2D
/// </summary>
public static class ColliderScaler
{
    /// <summary>
    /// Scale a collider by a multiplier. Works with all 2D collider types.
    /// </summary>
    public static void ScaleCollider(Collider2D collider, float multiplier)
    {
        ScaleCollider(collider, multiplier, 0f);
    }
    
    /// <summary>
    /// Scale a collider by a multiplier with an additional collider size offset for fine-tuning.
    /// </summary>
    /// <param name="collider">The collider to scale</param>
    /// <param name="sizeMultiplier">The base size multiplier from card modifiers</param>
    /// <param name="colliderSizeOffset">Offset to adjust collider size relative to visual size (e.g., -0.4 makes collider smaller)</param>
    public static void ScaleCollider(Collider2D collider, float sizeMultiplier, float colliderSizeOffset)
    {
        float finalMultiplier = sizeMultiplier + colliderSizeOffset;
        if (collider == null || finalMultiplier == 1f) return;
        
        // Circle Collider
        CircleCollider2D circleCollider = collider as CircleCollider2D;
        if (circleCollider != null)
        {
            circleCollider.radius *= finalMultiplier;
            return;
        }
        
        // Box Collider
        BoxCollider2D boxCollider = collider as BoxCollider2D;
        if (boxCollider != null)
        {
            boxCollider.size *= finalMultiplier;
            return;
        }
        
        // Capsule Collider
        CapsuleCollider2D capsuleCollider = collider as CapsuleCollider2D;
        if (capsuleCollider != null)
        {
            capsuleCollider.size *= finalMultiplier;
            return;
        }
        
        // Polygon Collider - Scale all points
        PolygonCollider2D polygonCollider = collider as PolygonCollider2D;
        if (polygonCollider != null)
        {
            for (int pathIndex = 0; pathIndex < polygonCollider.pathCount; pathIndex++)
            {
                Vector2[] points = polygonCollider.GetPath(pathIndex);
                for (int i = 0; i < points.Length; i++)
                {
                    points[i] *= finalMultiplier;
                }
                polygonCollider.SetPath(pathIndex, points);
            }
            return;
        }
        
        // Edge Collider
        EdgeCollider2D edgeCollider = collider as EdgeCollider2D;
        if (edgeCollider != null)
        {
            Vector2[] points = edgeCollider.points;
            for (int i = 0; i < points.Length; i++)
            {
                points[i] *= finalMultiplier;
            }
            edgeCollider.points = points;
            return;
        }
    }
    
    /// <summary>
    /// Scale a collider's X axis only (for beams). Only works with Box, Capsule, and Polygon colliders.
    /// </summary>
    public static void ScaleColliderXOnly(Collider2D collider, float multiplier)
    {
        ScaleColliderXOnly(collider, multiplier, 0f);
    }
    
    /// <summary>
    /// Scale a collider's X axis only with an additional collider size offset for fine-tuning.
    /// </summary>
    public static void ScaleColliderXOnly(Collider2D collider, float sizeMultiplier, float colliderSizeOffset)
    {
        float finalMultiplier = sizeMultiplier + colliderSizeOffset;
        if (collider == null || finalMultiplier == 1f) return;
        
        // Box Collider
        BoxCollider2D boxCollider = collider as BoxCollider2D;
        if (boxCollider != null)
        {
            Vector2 size = boxCollider.size;
            size.x *= finalMultiplier;
            boxCollider.size = size;
            return;
        }
        
        // Capsule Collider
        CapsuleCollider2D capsuleCollider = collider as CapsuleCollider2D;
        if (capsuleCollider != null)
        {
            Vector2 size = capsuleCollider.size;
            size.x *= finalMultiplier;
            capsuleCollider.size = size;
            return;
        }
        
        // Polygon Collider - Scale X only
        PolygonCollider2D polygonCollider = collider as PolygonCollider2D;
        if (polygonCollider != null)
        {
            for (int pathIndex = 0; pathIndex < polygonCollider.pathCount; pathIndex++)
            {
                Vector2[] points = polygonCollider.GetPath(pathIndex);
                for (int i = 0; i < points.Length; i++)
                {
                    points[i].x *= finalMultiplier;
                    // Y stays the same
                }
                polygonCollider.SetPath(pathIndex, points);
            }
            return;
        }
        
        // Circle colliders don't make sense for X-only scaling
        CircleCollider2D circleCollider = collider as CircleCollider2D;
        if (circleCollider != null)
        {
            circleCollider.radius *= finalMultiplier;
            return;
        }
    }
}
