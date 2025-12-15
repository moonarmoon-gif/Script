using UnityEngine;

/// <summary>
/// Component added to enemies taunted by Cinderbloom
/// Makes enemies attack the Cinderbloom instead of the player
/// </summary>
public class CinderbloomTauntTarget : MonoBehaviour
{
    private Transform bloomTarget;
    private Transform originalTarget;
    
    public Transform BloomTarget => bloomTarget;
    
    public void SetTarget(Transform target)
    {
        bloomTarget = target;
        
        // Store original target (player)
        if (AdvancedPlayerController.Instance != null)
        {
            originalTarget = AdvancedPlayerController.Instance.transform;
        }
        
        Debug.Log($"<color=orange>{gameObject.name} is now targeting Cinderbloom at {target.position}!</color>");
    }
    
    private void Update()
    {
        // If bloom is destroyed, remove this component
        if (bloomTarget == null)
        {
            RestoreOriginalTarget();
            Destroy(this);
            return;
        }
    }
    
    private void OnDestroy()
    {
        RestoreOriginalTarget();
    }
    
    private void RestoreOriginalTarget()
    {
        Debug.Log($"<color=orange>{gameObject.name} released from taunt, targeting player again</color>");
    }
    
    /// <summary>
    /// Get the current target position (bloom or player)
    /// Enemy scripts should call this instead of directly accessing player position
    /// </summary>
    public Vector3 GetTargetPosition()
    {
        if (bloomTarget != null)
        {
            return bloomTarget.position;
        }
        else if (originalTarget != null)
        {
            return originalTarget.position;
        }
        else if (AdvancedPlayerController.Instance != null)
        {
            return AdvancedPlayerController.Instance.transform.position;
        }
        
        return transform.position;
    }
    
    /// <summary>
    /// Static helper method for enemies to get their target position
    /// Checks if enemy has taunt component and returns bloom position, otherwise player position
    /// </summary>
    public static Vector3 GetTargetPositionForEnemy(GameObject enemy)
    {
        CinderbloomTauntTarget taunt = enemy.GetComponent<CinderbloomTauntTarget>();
        if (taunt != null && taunt.bloomTarget != null)
        {
            return taunt.bloomTarget.position;
        }
        
        // Default to player
        if (AdvancedPlayerController.Instance != null)
        {
            return AdvancedPlayerController.Instance.transform.position;
        }
        
        return enemy.transform.position;
    }
}
