using UnityEngine;

/// <summary>
/// Forces enemy to position itself on the left or right side of the player
/// instead of above/below for better attack angles.
/// Add this component to enemies that need side positioning (ranged enemies, etc.)
/// </summary>
public class EnemySidePositioning : MonoBehaviour
{
    [Header("Positioning Settings")]
    [Tooltip("Preferred horizontal distance from player")]
    [SerializeField] private float preferredDistance = 5f;
    
    [Tooltip("Vertical offset range (randomized)")]
    [SerializeField] private Vector2 verticalOffsetRange = new Vector2(-1f, 1f);
    
    [Tooltip("How strongly to enforce side positioning (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float positioningStrength = 0.8f;
    
    [Tooltip("Minimum distance before repositioning kicks in")]
    [SerializeField] private float minDistanceThreshold = 2f;
    
    [Tooltip("Force enemy to specific side (None = auto-choose closest)")]
    [SerializeField] private PreferredSide preferredSide = PreferredSide.None;
    
    public enum PreferredSide
    {
        None,   // Auto-choose based on current position
        Left,   // Always position on left side of player
        Right   // Always position on right side of player
    }
    
    private Rigidbody2D rb;
    private Vector2 targetSidePosition;
    private bool hasChosenSide = false;
    private PreferredSide chosenSide;
    private float verticalOffset;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Randomize vertical offset
        verticalOffset = Random.Range(verticalOffsetRange.x, verticalOffsetRange.y);
    }

    void FixedUpdate()
    {
        if (AdvancedPlayerController.Instance == null) return;
        
        Vector2 playerPos = AdvancedPlayerController.Instance.transform.position;
        Vector2 currentPos = transform.position;
        
        // Choose side if not yet chosen
        if (!hasChosenSide || preferredSide != PreferredSide.None)
        {
            ChooseSide(playerPos, currentPos);
        }
        
        // Calculate target position on chosen side
        CalculateTargetPosition(playerPos);
        
        // Apply positioning influence to movement
        ApplyPositioningInfluence(currentPos);
    }

    private void ChooseSide(Vector2 playerPos, Vector2 currentPos)
    {
        if (preferredSide == PreferredSide.Left)
        {
            chosenSide = PreferredSide.Left;
            hasChosenSide = true;
        }
        else if (preferredSide == PreferredSide.Right)
        {
            chosenSide = PreferredSide.Right;
            hasChosenSide = true;
        }
        else
        {
            // Auto-choose based on current position relative to player
            float horizontalDiff = currentPos.x - playerPos.x;
            
            if (Mathf.Abs(horizontalDiff) > minDistanceThreshold)
            {
                chosenSide = horizontalDiff < 0 ? PreferredSide.Left : PreferredSide.Right;
                hasChosenSide = true;
                Debug.Log($"<color=cyan>{gameObject.name} chose {chosenSide} side positioning</color>");
            }
        }
    }

    private void CalculateTargetPosition(Vector2 playerPos)
    {
        float sideMultiplier = chosenSide == PreferredSide.Left ? -1f : 1f;
        
        // Calculate target Y, but never go below player's Y axis (invisible border)
        float targetY = playerPos.y + verticalOffset;
        targetY = Mathf.Max(targetY, playerPos.y); // Clamp to player Y or above
        
        targetSidePosition = new Vector2(
            playerPos.x + (preferredDistance * sideMultiplier),
            targetY
        );
    }

    private void ApplyPositioningInfluence(Vector2 currentPos)
    {
        // Calculate direction toward target side position
        Vector2 toTargetPos = targetSidePosition - currentPos;
        float distanceToTarget = toTargetPos.magnitude;
        
        // Only apply influence if not already at target position
        if (distanceToTarget > 0.5f)
        {
            Vector2 desiredDirection = toTargetPos.normalized;
            
            // Blend current velocity with desired direction
            if (rb != null && rb.velocity.sqrMagnitude > 0.01f)
            {
                Vector2 currentDirection = rb.velocity.normalized;
                Vector2 blendedDirection = Vector2.Lerp(currentDirection, desiredDirection, positioningStrength);
                
                // Maintain current speed but adjust direction
                float currentSpeed = rb.velocity.magnitude;
                rb.velocity = blendedDirection * currentSpeed;
            }
        }
    }

    // Visualize target position in editor
    void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !hasChosenSide) return;
        
        // Draw target position
        Gizmos.color = chosenSide == PreferredSide.Left ? Color.blue : Color.red;
        Gizmos.DrawWireSphere(targetSidePosition, 0.5f);
        
        // Draw line to target
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, targetSidePosition);
        
        // Draw side indicator
        Vector3 sideIndicator = transform.position + (chosenSide == PreferredSide.Left ? Vector3.left : Vector3.right) * 2f;
        Gizmos.color = Color.green;
        Gizmos.DrawLine(transform.position, sideIndicator);
    }
}
