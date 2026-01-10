using UnityEngine;

/// <summary>
/// Simple script to toggle enhanced variants for testing
/// Now works with card-based leveling system
/// </summary>
public class EnhancedVariantToggle : MonoBehaviour
{
    [Header("Card References (Assign in Inspector)")]
    public ProjectileCards fireBeamCard;
    public ProjectileCards fireMineCard;
    public ProjectileCards nuclearStrikeCard;
    public ProjectileCards thunderBirdCard;

    /// <summary>
    /// Enable Fire Beam Enhanced Variant 1 (Moving Beam)
    /// </summary>
    public void EnableFireBeamVariant1()
    {
        if (ProjectileCardLevelSystem.Instance != null && fireBeamCard != null)
        {
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(fireBeamCard, 1);
            Debug.Log($"<color=cyan>{fireBeamCard.cardName} Enhanced Variant 1 ENABLED!</color>");
        }
        else
        {
            Debug.LogError("ProjectileCardLevelSystem not found or FireBeam card not assigned!");
        }
    }

    /// <summary>
    /// Enable Fire Beam Enhanced Variant 3 (Dual Smart Beams)
    /// </summary>
    public void EnableFireBeamVariant3()
    {
        if (ProjectileCardLevelSystem.Instance != null && fireBeamCard != null)
        {
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(fireBeamCard, 3);
            Debug.Log($"<color=cyan>{fireBeamCard.cardName} Enhanced Variant 3 ENABLED!</color>");
        }
        else
        {
            Debug.LogError("ProjectileCardLevelSystem not found or FireBeam card not assigned!");
        }
    }

    /// <summary>
    /// Disable Fire Beam Enhanced (back to basic)
    /// </summary>
    public void DisableFireBeamVariant()
    {
        if (ProjectileCardLevelSystem.Instance != null && fireBeamCard != null)
        {
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(fireBeamCard, 0);
            Debug.Log($"<color=cyan>{fireBeamCard.cardName} Enhanced Variant DISABLED (back to basic)</color>");
        }
    }

    /// <summary>
    /// Enable Nuclear Strike Enhanced Variant 1 (Rapid Strike)
    /// </summary>
    public void EnableNuclearStrikeVariant1()
    {
        if (ProjectileCardLevelSystem.Instance != null && nuclearStrikeCard != null)
        {
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(nuclearStrikeCard, 1);
            Debug.Log($"<color=cyan>{nuclearStrikeCard.cardName} Enhanced Variant 1 ENABLED!</color>");
        }
        else
        {
            Debug.LogError("ProjectileCardLevelSystem not found or NuclearStrike card not assigned!");
        }
    }

    /// <summary>
    /// Enable Fire Mine Enhanced Variant 1 (Mega Mine)
    /// </summary>
    public void EnableFireMineVariant1()
    {
        if (ProjectileCardLevelSystem.Instance != null && fireMineCard != null)
        {
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(fireMineCard, 1);
            Debug.Log($"<color=cyan>{fireMineCard.cardName} Enhanced Variant 1 ENABLED!</color>");
        }
        else
        {
            Debug.LogError("ProjectileCardLevelSystem not found or FireMine card not assigned!");
        }
    }

    /// <summary>
    /// Enable Thunder Bird Enhanced Variant 1 (Dual Thunder)
    /// </summary>
    public void EnableThunderBirdVariant1()
    {
        if (ProjectileCardLevelSystem.Instance != null && thunderBirdCard != null)
        {
            ProjectileCardLevelSystem.Instance.SetEnhancedVariant(thunderBirdCard, 1);
            Debug.Log($"<color=cyan>{thunderBirdCard.cardName} Enhanced Variant 1 ENABLED!</color>");
        }
        else
        {
            Debug.LogError("ProjectileCardLevelSystem not found or ThunderBird card not assigned!");
        }
    }

    /// <summary>
    /// Disable all enhanced variants
    /// </summary>
    public void DisableAllVariants()
    {
        if (ProjectileCardLevelSystem.Instance != null)
        {
            if (fireBeamCard != null) ProjectileCardLevelSystem.Instance.SetEnhancedVariant(fireBeamCard, 0);
            if (nuclearStrikeCard != null) ProjectileCardLevelSystem.Instance.SetEnhancedVariant(nuclearStrikeCard, 0);
            if (thunderBirdCard != null) ProjectileCardLevelSystem.Instance.SetEnhancedVariant(thunderBirdCard, 0);
            if (fireMineCard != null) ProjectileCardLevelSystem.Instance.SetEnhancedVariant(fireMineCard, 0);

            Debug.Log("<color=cyan>All Enhanced Variants DISABLED!</color>");
        }
    }

    /// <summary>
    /// Check Fire Beam level and unlock status
    /// </summary>
    public void CheckFireBeamStatus()
    {
        if (ProjectileCardLevelSystem.Instance != null && fireBeamCard != null)
        {
            int level = ProjectileCardLevelSystem.Instance.GetLevel(fireBeamCard);
            bool unlocked = ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(fireBeamCard);
            int variant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(fireBeamCard);
            int unlockLevel = ProjectileCardLevelSystem.Instance.GetEnhancedUnlockLevel();

            Debug.Log($"<color=yellow>{fireBeamCard.cardName} Status: " +
                     $"Level={level}/{unlockLevel}, " +
                     $"Enhanced Unlocked={unlocked}, " +
                     $"Current Variant={variant}</color>");
        }
    }

    /// <summary>
    /// Manually add levels to a card (for testing)
    /// </summary>
    public void AddLevelsToFireBeam(int levelsToAdd)
    {
        if (ProjectileCardLevelSystem.Instance != null && fireBeamCard != null)
        {
            // For testing, we'll simulate adding levels by directly modifying the level
            // In a real scenario, you'd use ProjectileCardLevelSystem.Instance.AddLevels(card, levelsToAdd)
            int currentLevel = ProjectileCardLevelSystem.Instance.GetLevel(fireBeamCard);
            ProjectileCardLevelSystem.Instance.AddLevels(fireBeamCard, CardRarity.Common);
            Debug.Log($"<color=cyan>Added 1 level to {fireBeamCard.cardName}. New level: {currentLevel + 1}</color>");
        }
    }
}