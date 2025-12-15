using UnityEngine;

/// <summary>
/// Boundary that prevents enemies from going below a certain line
/// without slowing them down
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class EnemyBoundary : MonoBehaviour
{
    [Header("Boundary Settings")]
    [Tooltip("Layer mask for enemies")]
    [SerializeField] private LayerMask enemyLayer;
    
    [Tooltip("How much to push enemies up when they hit the boundary")]
    [SerializeField] private float pushForce = 10f;
    
    private BoxCollider2D boundaryCollider;
    
    private void Awake()
    {
        boundaryCollider = GetComponent<BoxCollider2D>();
        
        // Make sure the collider is a trigger
        boundaryCollider.isTrigger = true;
        
        Debug.Log($"<color=cyan>EnemyBoundary initialized at {transform.position}</color>");
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        // Check if the object is an enemy
        if (((1 << other.gameObject.layer) & enemyLayer) != 0)
        {
            Rigidbody2D enemyRb = other.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                // Get the top edge of the boundary
                float boundaryTop = boundaryCollider.bounds.max.y;
                
                // If enemy is below or at the boundary
                if (other.transform.position.y <= boundaryTop)
                {
                    // Clamp enemy position to be above the boundary
                    Vector3 clampedPosition = other.transform.position;
                    clampedPosition.y = Mathf.Max(clampedPosition.y, boundaryTop + 0.1f);
                    other.transform.position = clampedPosition;
                    
                    // Remove any downward velocity
                    if (enemyRb.velocity.y < 0)
                    {
                        Vector2 velocity = enemyRb.velocity;
                        velocity.y = 0; // Stop downward movement
                        enemyRb.velocity = velocity;
                    }
                    
                    // Optional: Add slight upward push if enemy is stuck
                    if (other.transform.position.y <= boundaryTop + 0.05f)
                    {
                        enemyRb.AddForce(Vector2.up * pushForce, ForceMode2D.Force);
                    }
                }
            }
        }
    }
    
    private void OnDrawGizmos()
    {
        // Draw the boundary line
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.color = Color.red;
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = col.size;
            
            // Draw the top edge of the boundary
            Vector3 left = center + new Vector3(-size.x / 2, size.y / 2, 0);
            Vector3 right = center + new Vector3(size.x / 2, size.y / 2, 0);
            Gizmos.DrawLine(left, right);
            
            // Draw the boundary box
            Gizmos.color = new Color(1f, 0f, 0f, 0.2f);
            Gizmos.DrawCube(center, size);
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        // Draw more detailed gizmo when selected
        BoxCollider2D col = GetComponent<BoxCollider2D>();
        if (col != null)
        {
            Gizmos.color = Color.yellow;
            Vector3 center = transform.position + (Vector3)col.offset;
            Vector3 size = col.size;
            
            // Draw wire cube
            Gizmos.DrawWireCube(center, size);
            
            // Draw thick top line
            Vector3 left = center + new Vector3(-size.x / 2, size.y / 2, 0);
            Vector3 right = center + new Vector3(size.x / 2, size.y / 2, 0);
            
            for (float offset = -0.05f; offset <= 0.05f; offset += 0.025f)
            {
                Gizmos.DrawLine(left + Vector3.up * offset, right + Vector3.up * offset);
            }
        }
    }
}
