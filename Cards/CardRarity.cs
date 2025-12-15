using UnityEngine;

/// <summary>
/// Defines card rarity tiers and their spawn probabilities
/// </summary>
public enum CardRarity
{
    Common,     // 30%
    Uncommon,   // 29%
    Rare,       // 20%
    Epic,       // 15%
    Legendary,  // 5%
    Mythic,     // 1%
    Boss        // Special - only spawns via boss timer
}

/// <summary>
/// Helper class for card rarity calculations
/// </summary>
public static class CardRarityHelper
{
    // Default spawn odds (must total 100%) - Boss has 0% as it only spawns via boss timer
    private static readonly float[] DefaultOdds = { 30f, 29f, 20f, 15f, 5f, 1f, 0f };
    
    /// <summary>
    /// Get the spawn chance for a specific rarity
    /// </summary>
    public static float GetSpawnChance(CardRarity rarity)
    {
        return DefaultOdds[(int)rarity];
    }
    
    /// <summary>
    /// Get a random rarity based on spawn chances
    /// </summary>
    public static CardRarity GetRandomRarity()
    {
        float roll = Random.Range(0f, 100f);
        float cumulative = 0f;
        
        for (int i = 0; i < DefaultOdds.Length; i++)
        {
            cumulative += DefaultOdds[i];
            if (roll < cumulative)
            {
                return (CardRarity)i;
            }
        }
        
        return CardRarity.Common; // Fallback
    }
    
    /// <summary>
    /// Get color associated with rarity
    /// </summary>
    public static Color GetRarityColor(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return new Color(0.7f, 0.7f, 0.7f); // Gray
            case CardRarity.Uncommon:
                return new Color(0.2f, 1f, 0.2f); // Green
            case CardRarity.Rare:
                return new Color(0.3f, 0.5f, 1f); // Blue
            case CardRarity.Epic:
                return new Color(0.7f, 0.3f, 1f); // Purple
            case CardRarity.Legendary:
                return new Color(1f, 0.6f, 0f); // Orange
            case CardRarity.Mythic:
                return new Color(1f, 0.2f, 0.2f); // Red
            case CardRarity.Boss:
                return new Color(1f, 0.84f, 0f); // Gold
            default:
                return Color.white;
        }
    }
    
    /// <summary>
    /// Get multiplier for stat increases based on rarity
    /// </summary>
    public static float GetRarityMultiplier(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return 1f;
            case CardRarity.Uncommon:
                return 2f;
            case CardRarity.Rare:
                return 4f;
            case CardRarity.Epic:
                return 8f;
            case CardRarity.Legendary:
                return 16f;
            case CardRarity.Mythic:
                return 32f;
            case CardRarity.Boss:
                return 64f; // Boss enemies are extremely powerful
            default:
                return 1f;
        }
    }
}
