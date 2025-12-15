using UnityEngine;

/// <summary>
/// Fixes sprite sorting issues for multi-sprite beams by setting all child sprites to use Center sort point
/// This prevents enemies from showing through the beam when they're at certain positions
/// </summary>
public class BeamSpriteSortFix : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("Force all child sprite renderers to use Center sort point")]
    [SerializeField] private bool forceCenterSortPoint = true;
    
    [Tooltip("Additional sorting order offset to apply to all sprites")]
    [SerializeField] private int sortingOrderOffset = 0;

    private void Awake()
    {
        ApplySortFix();
    }

    private void Start()
    {
        // Apply again in Start to ensure it overrides any other settings
        ApplySortFix();
    }

    private void ApplySortFix()
    {
        if (!forceCenterSortPoint) return;

        // Get all sprite renderers including this object and children
        SpriteRenderer[] allSprites = GetComponentsInChildren<SpriteRenderer>(true);
        
        foreach (SpriteRenderer sr in allSprites)
        {
            if (sr != null)
            {
                // Force sprite sort point to Center
                sr.spriteSortPoint = SpriteSortPoint.Center;
                
                // Apply sorting order offset if specified
                if (sortingOrderOffset != 0)
                {
                    sr.sortingOrder += sortingOrderOffset;
                }
                
                Debug.Log($"<color=cyan>BeamSpriteSortFix: Set {sr.gameObject.name} to Center sort point, sorting order: {sr.sortingOrder}</color>");
            }
        }
        
        Debug.Log($"<color=green>BeamSpriteSortFix: Fixed {allSprites.Length} sprite renderers</color>");
    }

    // Call this if you need to reapply the fix at runtime
    public void ReapplyFix()
    {
        ApplySortFix();
    }
}
