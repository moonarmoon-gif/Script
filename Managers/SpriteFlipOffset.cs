using UnityEngine;

/// <summary>
/// Handles collider and shadow offset when SpriteRenderer is flipped.
/// DOES NOT move the sprite itself - only adjusts collider offset and shadow position.
/// 
/// SETUP:
/// - Attach to GameObject with SpriteRenderer
/// - Collider should be on same GameObject or parent
/// - Shadow (visual) should be a child GameObject
/// - This script will reposition collider offset and shadow based on flip state
/// </summary>
public class SpriteFlipOffset : MonoBehaviour
{
    [Header("Collider Offset Settings")]
    [Tooltip("Enable collider offset control (disable if animation controls collider offset)")]
    [SerializeField] private bool enableColliderOffset = true;
    [Tooltip("Invert flip detection (use if enemy script uses invertFlip)")]
    [SerializeField] private bool invertFlip = false;
    [Tooltip("X offset to apply when sprite is flipped")]
    [SerializeField] private float colliderFlippedOffsetX = 0f;
    [Tooltip("Y offset to apply when sprite is flipped")]
    [SerializeField] private float colliderFlippedOffsetY = 0f;
    
    [Header("Shadow Offset Settings")]
    [Tooltip("Enable shadow offset control (disable if animation controls shadow offset)")]
    [SerializeField] private bool enableShadowOffset = true;
    [Tooltip("Shadow X offset when sprite is flipped (facing left)")]
    public float shadowFlippedOffsetX = 0f;
    
    [Tooltip("Shadow Y offset when sprite is flipped (facing left)")]
    public float shadowFlippedOffsetY = 0f;
    
    [Header("References")]
    [Tooltip("Collider to move with sprite (leave empty to auto-find)")]
    public Collider2D targetCollider;
    
    [Tooltip("Shadow GameObject to move with sprite (leave empty to ignore)")]
    public Transform shadowTransform;
    
    [Header("Shadow Flip Settings")]
    [Tooltip("Should the shadow sprite flip when the main sprite flips?")]
    public bool flipShadowSprite = false;

    private SpriteRenderer sr;
    private SpriteRenderer shadowSpriteRenderer;
    private Vector2 baseColliderOffset; // Base offset when NOT flipped (from inspector)
    private Vector3 baseShadowLocalPosition; // Base shadow position when NOT flipped
    private bool wasFlipped = false;
    private bool hasCollider = false;
    private bool hasShadow = false;
    private bool hasShadowSprite = false;
    private bool isInitialized = false;
    
    // Track last frame's offset to detect animation changes
    private Vector2 lastFrameOffset = Vector2.zero;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        if (sr == null)
        {
            Debug.LogError($"SpriteFlipOffset on {gameObject.name}: No SpriteRenderer found!");
            enabled = false;
            return;
        }

        // Find collider
        if (targetCollider == null)
        {
            targetCollider = GetComponent<Collider2D>();
            if (targetCollider == null)
            {
                targetCollider = GetComponentInParent<Collider2D>();
            }
        }

        if (targetCollider != null)
        {
            hasCollider = true;
            // Store the CURRENT offset as base (from inspector, for non-flipped state)
            if (targetCollider is BoxCollider2D box)
            {
                baseColliderOffset = box.offset;
            }
            else if (targetCollider is CapsuleCollider2D capsule)
            {
                baseColliderOffset = capsule.offset;
            }
            else if (targetCollider is CircleCollider2D circle)
            {
                baseColliderOffset = circle.offset;
            }
            
            lastFrameOffset = baseColliderOffset;
            Debug.Log($"<color=cyan>SpriteFlipOffset on {gameObject.name}: Found collider, base offset = {baseColliderOffset}</color>");
        }
        else
        {
            Debug.LogWarning($"SpriteFlipOffset on {gameObject.name}: No collider found!");
        }

        // Find shadow
        if (shadowTransform != null)
        {
            hasShadow = true;
            baseShadowLocalPosition = shadowTransform.localPosition;
            
            // Check if shadow has sprite renderer for flipping
            shadowSpriteRenderer = shadowTransform.GetComponent<SpriteRenderer>();
            if (shadowSpriteRenderer != null)
            {
                hasShadowSprite = true;
            }
            
            Debug.Log($"<color=cyan>SpriteFlipOffset on {gameObject.name}: Found shadow at {baseShadowLocalPosition}, has sprite = {hasShadowSprite}</color>");
        }
    }
    
    void Start()
    {
        if (sr != null)
        {
            wasFlipped = sr.flipX;
            // Apply offset immediately on start
            ApplyOffset();
        }
    }
    
    void OnEnable()
    {
        // Mark as initialized when enabled
        if (sr != null)
        {
            isInitialized = true;
        }
    }

    void LateUpdate()
    {
        if (sr == null || !sr.enabled) return;

        // ALWAYS check and apply offset based on CURRENT flip state
        // This ensures offset is correct even if flip changed this frame
        ApplyOffset();
    }

    void ApplyOffset()
    {
        if (sr == null || !sr.enabled || !isInitialized) return;
        
        // Read flipX and apply invert if needed
        bool isFlipped = invertFlip ? !sr.flipX : sr.flipX;
        
        // ONLY apply collider offset if enabled (disable if animation controls it)
        if (enableColliderOffset && hasCollider && targetCollider != null && targetCollider.enabled)
        {
            // Calculate what the script WANTS the offset to be
            Vector2 scriptDesiredOffset;
            if (isFlipped)
            {
                scriptDesiredOffset = baseColliderOffset + new Vector2(colliderFlippedOffsetX, colliderFlippedOffsetY);
            }
            else
            {
                scriptDesiredOffset = baseColliderOffset;
            }
            
            // Apply the script offset
            if (targetCollider is BoxCollider2D box)
                box.offset = scriptDesiredOffset;
            else if (targetCollider is CapsuleCollider2D capsule)
                capsule.offset = scriptDesiredOffset;
            else if (targetCollider is CircleCollider2D circle)
                circle.offset = scriptDesiredOffset;
        }
        
        // Track flip state change
        wasFlipped = isFlipped;

        // Apply shadow position offset (ONLY if enabled - can be disabled for animation control)
        if (enableShadowOffset && hasShadow && shadowTransform != null && shadowTransform.gameObject.activeInHierarchy)
        {
            Vector3 shadowFlipOffset = isFlipped ? new Vector3(shadowFlippedOffsetX, shadowFlippedOffsetY, 0f) : Vector3.zero;
            shadowTransform.localPosition = baseShadowLocalPosition + shadowFlipOffset;

            // Flip shadow sprite if enabled
            if (flipShadowSprite && hasShadowSprite && shadowSpriteRenderer != null)
            {
                shadowSpriteRenderer.flipX = isFlipped;
            }
        }
    }

    /// <summary>
    /// Call this to update base positions after teleporting or major position changes.
    /// This recalculates the base offsets from the CURRENT state.
    /// </summary>
    public void UpdateBasePositions()
    {
        Debug.Log($"<color=magenta>SpriteFlipOffset on {gameObject.name}: UpdateBasePositions called</color>");
        
        // Update base collider offset from current state
        if (hasCollider && targetCollider != null)
        {
            Vector2 oldBase = baseColliderOffset;
            
            if (targetCollider is BoxCollider2D box)
                baseColliderOffset = box.offset;
            else if (targetCollider is CapsuleCollider2D capsule)
                baseColliderOffset = capsule.offset;
            else if (targetCollider is CircleCollider2D circle)
                baseColliderOffset = circle.offset;
            
            Debug.Log($"<color=magenta>Collider base offset updated: {oldBase} -> {baseColliderOffset}</color>");
        }
        
        // Update base shadow position from current state
        if (hasShadow && shadowTransform != null)
        {
            Vector3 oldBase = baseShadowLocalPosition;
            baseShadowLocalPosition = shadowTransform.localPosition;
            Debug.Log($"<color=magenta>Shadow base position updated: {oldBase} -> {baseShadowLocalPosition}</color>");
        }
        
        // Reapply offset to ensure consistency
        ApplyOffset();
    }
    
    /// <summary>
    /// Force reapply the offset without updating base positions.
    /// Use this if the sprite flip state changed but positions are correct.
    /// </summary>
    public void ForceReapplyOffset()
    {
        Debug.Log($"<color=cyan>SpriteFlipOffset on {gameObject.name}: ForceReapplyOffset called</color>");
        ApplyOffset();
    }
    
    /// <summary>
    /// Enable or disable collider offset control at runtime.
    /// Use this to dynamically control whether script or animation controls the collider offset.
    /// </summary>
    public void SetColliderOffsetEnabled(bool enabled)
    {
        enableColliderOffset = enabled;
    }
    
    /// <summary>
    /// Check if collider offset control is currently enabled.
    /// </summary>
    public bool IsColliderOffsetEnabled()
    {
        return enableColliderOffset;
    }
    
    /// <summary>
    /// Enable or disable shadow offset control.
    /// Use this to dynamically control whether script or animation controls the shadow offset.
    /// </summary>
    public void SetShadowOffsetEnabled(bool enabled)
    {
        enableShadowOffset = enabled;
    }
    
    /// <summary>
    /// Check if shadow offset control is currently enabled.
    /// </summary>
    public bool IsShadowOffsetEnabled()
    {
        return enableShadowOffset;
    }
    
    /// <summary>
    /// Force re-capture of base offsets from current collider state.
    /// Call this after spawning if the enemy needs to reset its base offset.
    /// </summary>
    public void RecaptureBaseOffsets()
    {
        // Force initialization if not already done
        if (!isInitialized && sr != null)
        {
            isInitialized = true;
        }
        
        if (targetCollider != null && hasCollider)
        {
            if (targetCollider is BoxCollider2D box)
            {
                baseColliderOffset = box.offset;
            }
            else if (targetCollider is CapsuleCollider2D capsule)
            {
                baseColliderOffset = capsule.offset;
            }
            else if (targetCollider is CircleCollider2D circle)
            {
                baseColliderOffset = circle.offset;
            }
            
            lastFrameOffset = baseColliderOffset;
            Debug.Log($"<color=green>SpriteFlipOffset on {gameObject.name}: Recaptured base offset = {baseColliderOffset}</color>");
        }
        
        if (shadowTransform != null && hasShadow)
        {
            baseShadowLocalPosition = shadowTransform.localPosition;
            Debug.Log($"<color=green>SpriteFlipOffset on {gameObject.name}: Recaptured shadow position = {baseShadowLocalPosition}</color>");
        }
        
        // Apply offset immediately (will work now that isInitialized is true)
        ApplyOffset();
    }
}
