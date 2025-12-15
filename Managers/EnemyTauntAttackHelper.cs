using UnityEngine;

/// <summary>
/// Helper component to handle taunt-aware attacking for enemies
/// Add this to any enemy that should attack taunting entities (like Cinderbloom)
/// </summary>
public class EnemyTauntAttackHelper : MonoBehaviour
{
    /// <summary>
    /// Deal damage to the appropriate target (taunt entity or player)
    /// Call this in your enemy's attack routine instead of directly damaging the player
    /// </summary>
    public bool DealDamageToTarget(float damage, Vector3 hitPoint, Vector3 hitNormal)
    {
        // Try to get the actual taunt GameObject from CinderbloomTauntTarget
        GameObject tauntTarget = FindTauntTarget();
        
        if (tauntTarget != null)
        {
            // Attack the taunt target (Cinderbloom)
            IDamageable damageable = tauntTarget.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                damageable.TakeDamage(damage, hitPoint, hitNormal);
                Debug.Log($"<color=yellow>{gameObject.name} attacked taunt target {tauntTarget.name} for {damage} damage</color>");
                return true;
            }
            else
            {
                Debug.Log($"<color=yellow>{gameObject.name}: Taunt target {tauntTarget.name} is dead or not damageable</color>");
            }
        }
        
        // No taunt target or taunt target is dead - attack player
        if (AdvancedPlayerController.Instance != null)
        {
            IDamageable playerDamageable = AdvancedPlayerController.Instance.GetComponent<IDamageable>();
            if (playerDamageable != null && playerDamageable.IsAlive)
            {
                // Register this enemy as the attacker so PlayerHealth can
                // forward it into favour effects when processing damage.
                PlayerHealth.RegisterPendingAttacker(gameObject);
                playerDamageable.TakeDamage(damage, hitPoint, hitNormal);
                Debug.Log($"<color=cyan>{gameObject.name} attacked player for {damage} damage</color>");
                return true;
            }
        }
        
        return false;
    }
    
    /// <summary>
    /// Get the current attack target (taunt entity or player)
    /// Use this to determine attack range, facing direction, etc.
    /// </summary>
    public Transform GetAttackTarget()
    {
        // Try to get the actual taunt GameObject
        GameObject tauntTarget = FindTauntTarget();
        
        if (tauntTarget != null)
        {
            IDamageable damageable = tauntTarget.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                return tauntTarget.transform;
            }
        }
        
        // No taunt target or taunt target is dead - return player
        if (AdvancedPlayerController.Instance != null)
        {
            return AdvancedPlayerController.Instance.transform;
        }
        
        return null;
    }
    
    /// <summary>
    /// Check if currently taunted
    /// </summary>
    public bool IsTaunted()
    {
        GameObject tauntTarget = FindTauntTarget();
        if (tauntTarget != null)
        {
            IDamageable damageable = tauntTarget.GetComponent<IDamageable>();
            return damageable != null && damageable.IsAlive;
        }
        return false;
    }
    
    /// <summary>
    /// Find the actual taunt GameObject by searching for Cinderbloom objects
    /// </summary>
    private GameObject FindTauntTarget()
    {
        // Get the taunt position
        Vector3 targetPos = CinderbloomTauntTarget.GetTargetPositionForEnemy(gameObject);
        
        // Check if this is actually a taunt position (not player position)
        if (AdvancedPlayerController.Instance != null && 
            Vector3.Distance(targetPos, AdvancedPlayerController.Instance.transform.position) < 0.1f)
        {
            // This is the player position, no taunt active
            return null;
        }
        
        // Find all Cinderbloom objects in the scene
        CinderbloomTauntTarget[] allCinderblooms = FindObjectsOfType<CinderbloomTauntTarget>();
        
        foreach (var cinderbloom in allCinderblooms)
        {
            if (cinderbloom != null && cinderbloom.gameObject != null)
            {
                // Check if this Cinderbloom is at the taunt position
                if (Vector3.Distance(cinderbloom.transform.position, targetPos) < 0.5f)
                {
                    Debug.Log($"<color=lime>Found taunt target: {cinderbloom.gameObject.name} at {cinderbloom.transform.position}</color>");
                    return cinderbloom.gameObject;
                }
            }
        }
        
        Debug.Log($"<color=orange>No Cinderbloom found at taunt position {targetPos}</color>");
        return null;
    }
}
