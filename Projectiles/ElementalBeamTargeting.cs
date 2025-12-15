using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Alternative targeting system for ElementalBeam using a simpler, more reliable approach
/// </summary>
public class ElementalBeamTargeting : MonoBehaviour
{
    [Header("Targeting Settings")]
    [Tooltip("Enable this new targeting system")]
    public bool useNewTargetingSystem = true;
    
    [Tooltip("Detection radius for finding enemies")]
    [SerializeField] private float detectionRadius = 30f;
    
    [Tooltip("Layer mask for enemies")]
    [SerializeField] private LayerMask enemyLayer;
    
    /// <summary>
    /// Find the best direction to fire the beam to hit the most enemies
    /// Uses a simpler sector-based approach
    /// </summary>
    public Vector2 FindBestDirection(float minAngleDeg, float maxAngleDeg, int samples = 16)
    {
        if (!useNewTargetingSystem)
        {
            Debug.Log("<color=yellow>New targeting system disabled, returning default direction</color>");
            return Vector2.up;
        }
        
        // Get all enemies within detection radius
        Collider2D[] allEnemies = Physics2D.OverlapCircleAll(transform.position, detectionRadius, enemyLayer);
        
        // Filter to only alive enemies
        List<Vector2> enemyPositions = new List<Vector2>();
        foreach (Collider2D enemy in allEnemies)
        {
            if (enemy == null) continue;
            
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                enemyPositions.Add(enemy.transform.position);
            }
        }
        
        if (enemyPositions.Count == 0)
        {
            // No enemies found, fire in random direction within range
            float randomAngle = Random.Range(minAngleDeg, maxAngleDeg);
            Vector2 randomDir = AngleToDirection(randomAngle);
            Debug.Log($"<color=yellow>ElementalBeamTargeting: No enemies found, firing random at {randomAngle:F1}°</color>");
            return randomDir;
        }
        
        Debug.Log($"<color=cyan>ElementalBeamTargeting: Found {enemyPositions.Count} alive enemies</color>");
        
        // Test each angle and count enemies in that direction
        int bestCount = 0;
        float bestAngle = (minAngleDeg + maxAngleDeg) / 2f; // Default to center
        
        for (int i = 0; i < samples; i++)
        {
            // Calculate test angle
            float t = samples > 1 ? (float)i / (samples - 1) : 0.5f;
            float testAngle = Mathf.Lerp(minAngleDeg, maxAngleDeg, t);
            Vector2 testDir = AngleToDirection(testAngle);
            
            // Count enemies within a cone in this direction
            int count = CountEnemiesInCone(enemyPositions, testDir, 15f); // 15° cone width
            
            Debug.Log($"<color=white>  Angle {testAngle:F1}°: {count} enemies</color>");
            
            if (count > bestCount)
            {
                bestCount = count;
                bestAngle = testAngle;
            }
        }
        
        Vector2 bestDirection = AngleToDirection(bestAngle);
        
        Debug.Log($"<color=lime>★ ElementalBeamTargeting RESULT: Angle {bestAngle:F1}° hits {bestCount} enemies</color>");
        Debug.Log($"<color=lime>  Direction: ({bestDirection.x:F3}, {bestDirection.y:F3})</color>");
        
        return bestDirection;
    }
    
    /// <summary>
    /// Convert angle in degrees to direction vector
    /// 0° = right, 90° = up, 180° = left, 270° = down
    /// </summary>
    private Vector2 AngleToDirection(float angleDeg)
    {
        float angleRad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad));
    }
    
    /// <summary>
    /// Count how many enemies are within a cone in the given direction
    /// </summary>
    private int CountEnemiesInCone(List<Vector2> enemyPositions, Vector2 direction, float coneHalfAngleDeg)
    {
        int count = 0;
        Vector2 beamOrigin = transform.position;
        
        foreach (Vector2 enemyPos in enemyPositions)
        {
            Vector2 toEnemy = enemyPos - beamOrigin;
            
            // Skip if enemy is behind
            if (toEnemy.sqrMagnitude < 0.01f) continue;
            
            // Calculate angle between beam direction and enemy direction
            float angle = Vector2.Angle(direction, toEnemy.normalized);
            
            // Check if within cone
            if (angle <= coneHalfAngleDeg)
            {
                count++;
            }
        }
        
        return count;
    }
    
    /// <summary>
    /// Simple fallback: aim at closest enemy
    /// </summary>
    public Vector2 AimAtClosestEnemy()
    {
        Collider2D[] allEnemies = Physics2D.OverlapCircleAll(transform.position, detectionRadius, enemyLayer);
        
        float closestDist = float.MaxValue;
        Vector2 closestDir = Vector2.up;
        
        foreach (Collider2D enemy in allEnemies)
        {
            if (enemy == null) continue;
            
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                Vector2 toEnemy = (Vector2)enemy.transform.position - (Vector2)transform.position;
                float dist = toEnemy.magnitude;
                
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closestDir = toEnemy.normalized;
                }
            }
        }
        
        Debug.Log($"<color=cyan>ElementalBeamTargeting: Aiming at closest enemy, distance={closestDist:F2}</color>");
        return closestDir;
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
