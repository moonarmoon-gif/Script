using UnityEngine;

/// <summary>
/// Base class for all card types
/// </summary>
public abstract class BaseCard : ScriptableObject
{
    [Header("Card Info")]
    public string cardName;
    [TextArea(3, 5)]
    public string description;
    public Sprite cardIcon;
    public CardRarity rarity;
    
    [Header("Text Styling")]
    [Tooltip("Font size for card name")]
    public float cardNameFontSize = 24f;
    [Tooltip("Font color for card name")]
    public Color cardNameColor = Color.white;
    [Tooltip("Font size for description")]
    public float descriptionFontSize = 18f;
    [Tooltip("Font color for description")]
    public Color descriptionColor = new Color(0.9f, 0.9f, 0.9f, 1f);
    
    [Header("Text Outline")]
    [Tooltip("Enable text outline for card name")]
    public bool enableNameOutline = false;
    [Tooltip("Outline color for card name")]
    public Color nameOutlineColor = Color.black;
    [Tooltip("Outline width for card name")]
    [Range(0f, 1f)]
    public float nameOutlineWidth = 0.2f;
    [Tooltip("Enable text outline for description")]
    public bool enableDescriptionOutline = false;
    [Tooltip("Outline color for description")]
    public Color descriptionOutlineColor = Color.black;
    [Tooltip("Outline width for description")]
    [Range(0f, 1f)]
    public float descriptionOutlineWidth = 0.2f;
    
    [Header("Spawn Settings")]
    [Range(0f, 100f)]
    public float spawnChance = 30f; // Override default rarity chance if needed
    
    /// <summary>
    /// Apply the card's effect to the player
    /// </summary>
    public abstract void ApplyEffect(GameObject player);
    
    /// <summary>
    /// Get formatted description with values
    /// </summary>
    public abstract string GetFormattedDescription();
    
    /// <summary>
    /// Get the color for this card's rarity
    /// </summary>
    public Color GetRarityColor()
    {
        return CardRarityHelper.GetRarityColor(rarity);
    }
    
    /// <summary>
    /// Get the multiplier for this card's rarity
    /// </summary>
    public float GetRarityMultiplier()
    {
        return CardRarityHelper.GetRarityMultiplier(rarity);
    }
}
