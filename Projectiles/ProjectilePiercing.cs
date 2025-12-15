using System;
using UnityEngine;

/// <summary>
/// Allows projectiles to pierce through multiple enemies
/// </summary>
public class ProjectilePiercing : MonoBehaviour
{
    [Header("Piercing Settings")]
    [Tooltip("Number of enemies this projectile can pierce through")]
    public int pierceCount = 0;
    
    [Tooltip("If true, projectile is destroyed after piercing max enemies")]
    public bool destroyAfterMaxPierces = true;
    
    private int currentPierces = 0;
    private System.Collections.Generic.HashSet<GameObject> hitEnemies = new System.Collections.Generic.HashSet<GameObject>();
    
    /// <summary>
    /// Call this when projectile hits an enemy
    /// Returns true if projectile should continue, false if it should be destroyed
    /// Pierce count logic: 1 pierce = hits 2 enemies (pierces 1, destroys on 2nd)
    /// </summary>
    public bool OnEnemyHit(GameObject enemy)
    {
        if (hitEnemies.Contains(enemy))
        {
            return true;
        }
        
        hitEnemies.Add(enemy);
        currentPierces++;
        
        // Pierce count 1 = hits 2 enemies (pierces 1st, destroys on 2nd)
        // Pierce count 2 = hits 3 enemies (pierces 1st and 2nd, destroys on 3rd)
        if (currentPierces > pierceCount && destroyAfterMaxPierces)
        {
            return false;
        }
        
        return true;
    }
    
    /// <summary>
    /// Check if this enemy has already been hit
    /// </summary>
    public bool HasHitEnemy(GameObject enemy)
    {
        return hitEnemies.Contains(enemy);
    }
    
    /// <summary>
    /// Get remaining pierces
    /// </summary>
    public int GetRemainingPierces()
    {
        return Mathf.Max(0, pierceCount - currentPierces);
    }
    
    /// <summary>
    /// Reset pierce count (useful for pooled projectiles)
    /// </summary>
    public void ResetPierces()
    {
        currentPierces = 0;
        hitEnemies.Clear();
    }
    
    private void OnEnable()
    {
        ResetPierces();
    }
    
    private void Awake()
    {
        // Automatically set ALL colliders to trigger for piercing projectiles (parent and children)
        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
        if (allColliders.Length > 0)
        {
            foreach (Collider2D col in allColliders)
            {
                if (!col.isTrigger)
                {
                    col.isTrigger = true;
                    Debug.Log($"<color=cyan>ProjectilePiercing: Set {col.gameObject.name} collider to trigger for piercing</color>");
                }
            }
        }
        else
        {
            Debug.LogWarning($"<color=yellow>ProjectilePiercing on {gameObject.name}: No colliders found (parent or children)!</color>");
        }
    }

    /// <summary>
    /// Set the maximum pierce count for this projectile
    /// </summary>
    public void SetMaxPierces(int totalPierceCount)
    {
        pierceCount = totalPierceCount;
        Debug.Log($"<color=cyan>ProjectilePiercing: Set max pierces to {pierceCount}</color>");
    }
}
