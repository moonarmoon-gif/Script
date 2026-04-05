using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages per-card projectile leveling and enhanced variants
/// Each ProjectileCard has its own independent level
/// </summary>
public class ProjectileCardLevelSystem : MonoBehaviour
{
    public static ProjectileCardLevelSystem Instance { get; private set; }
    
    [Header("Level Settings")]
    [Tooltip("Level required to unlock enhanced variants")]
    [SerializeField] private int enhancedUnlockLevel = 10;

    [Tooltip("Additional levels required AFTER unlocking all normal variants to unlock the Ultimate enhancement option.")]
    public int UltimateEnhancedUnlockLevel = 15;
    
    [Header("Level Gains by Rarity")]
    [SerializeField] private int commonLevelGain = 1;
    [SerializeField] private int uncommonLevelGain = 1;
    [SerializeField] private int rareLevelGain = 2;
    [SerializeField] private int epicLevelGain = 2;
    [SerializeField] private int legendaryLevelGain = 3;
    [SerializeField] private int mythicLevelGain = 4;
    
    [Header("Modifier Counts by Rarity")]
    [Tooltip("Number of modifiers for Common rarity cards")]
    public int commonModifierCount = 1;
    [Tooltip("Number of modifiers for Uncommon rarity cards")]
    public int uncommonModifierCount = 2;
    [Tooltip("Number of modifiers for Rare rarity cards")]
    public int rareModifierCount = 2;
    [Tooltip("Number of modifiers for Epic rarity cards")]
    public int epicModifierCount = 2;
    [Tooltip("Number of modifiers for Legendary rarity cards")]
    public int legendaryModifierCount = 3;
    [Tooltip("Number of modifiers for Mythic rarity cards")]
    public int mythicModifierCount = 3;
    
    [Header("Modifier System")]
    [Tooltip("When enabled, projectile cards use deterministic per-card modifier slots instead of random selection.")]
    public bool useDeterministicProjectileModifiers = false;
    
    // Track levels for each CARD NAME (not type)
    private Dictionary<string, int> cardLevels = new Dictionary<string, int>();
    
    // Track selected enhanced variant for each CARD NAME (1-3, 0 = none/basic)
    private Dictionary<string, int> selectedEnhancedVariants = new Dictionary<string, int>();

    // Track whether the Ultimate enhancement is selected for each CARD NAME.
    private Dictionary<string, bool> ultimateEnhancementsSelected = new Dictionary<string, bool>();

    // Track how many enhancement tiers have been REACHED (level / enhancedUnlockLevel)
    // for each card name. This lets us fire a variant selection exactly once per tier.
    private Dictionary<string, int> cardTiers = new Dictionary<string, int>();

    // Track the FULL HISTORY of chosen variants per card (1-3). This is used by the
    // variant selection UI so that any variant chosen at a previous tier is NOT
    // offered again on later tiers for the same card.
    private Dictionary<string, HashSet<int>> chosenVariantHistory = new Dictionary<string, HashSet<int>>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Add levels to a specific card based on card rarity
    /// </summary>
    public void AddLevels(ProjectileCards card, CardRarity rarity)
    {
        if (card == null)
        {
            Debug.LogWarning("Cannot add levels to null card!");
            return;
        }
        
        string cardKey = card.cardName; // Use card name as unique key
        int levelsToAdd = GetLevelGainByRarity(rarity);
        
        if (!cardLevels.ContainsKey(cardKey))
        {
            cardLevels[cardKey] = 0;
        }
        
        int oldLevel = cardLevels[cardKey];
        cardLevels[cardKey] += levelsToAdd;
        int newLevel = cardLevels[cardKey];
        
        Debug.Log($"<color=magenta>{cardKey} leveled up! {oldLevel} -> {newLevel} (+{levelsToAdd} from {rarity})</color>");
        
        // Check if enhanced unlocked and AUTO-APPLY (with hook for variant selection)
        if (enhancedUnlockLevel > 0)
        {
            int normalVariantCount = GetNormalVariantCountForCard(card);
            if (normalVariantCount <= 0)
            {
                normalVariantCount = 3;
            }

            int oldTier = Mathf.Clamp(oldLevel / enhancedUnlockLevel, 0, normalVariantCount);
            int newTier = Mathf.Clamp(newLevel / enhancedUnlockLevel, 0, normalVariantCount);

            if (newTier > oldTier)
            {
                for (int t = oldTier + 1; t <= newTier; t++)
                {
                    cardTiers[cardKey] = t;
                    Debug.Log($"<color=gold>★★★ {cardKey} ENHANCED TIER {t} REACHED! (Level {newLevel}/{enhancedUnlockLevel}x{t}) ★★★</color>");

                    if (OnCardTierIncreased != null)
                    {
                        OnCardTierIncreased(card, t);
                    }
                }
            }

            int ultimateTier = normalVariantCount + 1;
            if (UltimateEnhancedUnlockLevel > 0 && CardHasUltimateVariant(card))
            {
                int unlockAt = (normalVariantCount * enhancedUnlockLevel) + UltimateEnhancedUnlockLevel;
                bool oldUnlocked = oldLevel >= unlockAt;
                bool newUnlocked = newLevel >= unlockAt;

                if (!oldUnlocked && newUnlocked)
                {
                    Debug.Log($"<color=gold>★★★ {cardKey} ULTIMATE ENHANCEMENT UNLOCKED! (Level {newLevel}/{unlockAt}) ★★★</color>");
                    if (OnCardTierIncreased != null)
                    {
                        OnCardTierIncreased(card, ultimateTier);
                    }
                }
            }
        }
    }

    private int GetNormalVariantCountForCard(ProjectileCards card)
    {
        if (card == null)
        {
            return 0;
        }

        if (CardSelectionManager.Instance == null)
        {
            return 0;
        }

        ProjectileVariantSet set = CardSelectionManager.Instance.GetVariantSetForCard(card);
        if (set == null || set.variants == null)
        {
            return 0;
        }

        HashSet<int> indices = new HashSet<int>();
        for (int i = 0; i < set.variants.Length; i++)
        {
            var info = set.variants[i];
            if (info == null) continue;
            if (info.variantIndex <= 0) continue;
            indices.Add(info.variantIndex);
        }

        return indices.Count;
    }

    private bool CardHasUltimateVariant(ProjectileCards card)
    {
        if (card == null)
        {
            return false;
        }

        if (CardSelectionManager.Instance == null)
        {
            return false;
        }

        ProjectileVariantSet set = CardSelectionManager.Instance.GetVariantSetForCard(card);
        if (set == null || set.variants == null)
        {
            return false;
        }

        for (int i = 0; i < set.variants.Length; i++)
        {
            var info = set.variants[i];
            if (info == null) continue;
            if (info.variantIndex == 0) return true;
        }

        return false;
    }

    /// <summary>
    /// Fired whenever a card crosses to a higher enhancement tier (1-3).
    /// CardSelectionManager listens to this to trigger the Variant Selector UI.
    /// </summary>
    public System.Action<ProjectileCards, int> OnCardTierIncreased;
    
    /// <summary>
    /// Get current level of a specific card
    /// </summary>
    public int GetLevel(ProjectileCards card)
    {
        if (card == null) return 0;
        
        string cardKey = card.cardName;
        if (cardLevels.ContainsKey(cardKey))
        {
            return cardLevels[cardKey];
        }
        return 0;
    }
    
    /// <summary>
    /// Check if card has unlocked enhanced variants
    /// </summary>
    public bool IsEnhancedUnlocked(ProjectileCards card)
    {
        return GetLevel(card) >= enhancedUnlockLevel;
    }
    
    /// <summary>
    /// Set the selected enhanced variant for a card (0 = none, 1-3 = variant)
    /// </summary>
    public void SetEnhancedVariant(ProjectileCards card, int variantIndex)
    {
        if (card == null) return;
        
        string cardKey = card.cardName;

        if (ultimateEnhancementsSelected.ContainsKey(cardKey))
        {
            ultimateEnhancementsSelected[cardKey] = false;
        }
        int storedVariant = variantIndex;
        if (card.projectileType == ProjectileCards.ProjectileType.HolyShield)
        {
            int variantBit = 0;
            if (variantIndex == 1)
            {
                variantBit = 1;
            }
            else if (variantIndex == 2)
            {
                variantBit = 2;
            }
            else if (variantIndex == 3)
            {
                variantBit = 4;
            }

            int previous = 0;
            if (selectedEnhancedVariants.ContainsKey(cardKey))
            {
                previous = selectedEnhancedVariants[cardKey];
            }
            storedVariant = Mathf.Max(0, previous) | Mathf.Max(0, variantBit);
        }

        if (card.projectileType == ProjectileCards.ProjectileType.ElementalBeam && variantIndex == 4)
        {
            if (selectedEnhancedVariants.ContainsKey(cardKey))
            {
                storedVariant = selectedEnhancedVariants[cardKey];
            }
            else
            {
                storedVariant = 0;
            }
        }

        selectedEnhancedVariants[cardKey] = storedVariant;
        Debug.Log($"<color=gold>{cardKey} Enhanced Variant set to: {storedVariant}</color>");

        // Record this variant in the per-card history so future enhancement tiers
        // can filter it out of the selection UI. Only variants > 0 are meaningful.
        if (variantIndex > 0)
        {
            if (!chosenVariantHistory.TryGetValue(cardKey, out var history))
            {
                history = new HashSet<int>();
                chosenVariantHistory[cardKey] = history;
            }

            if (!history.Contains(variantIndex))
            {
                history.Add(variantIndex);
            }
        }

        // When a card becomes enhanced (variantIndex > 0), flag its runtime instance so the
        // ProjectileSpawner can apply a one-time reduced cooldown for the FIRST enhanced spawn.
        if (variantIndex > 0 && card.applyEnhancedFirstSpawnReduction)
        {
            // Optional runtime flag for debugging/inspector tools
            card.pendingEnhancedFirstSpawn = true;

            // Find the ProjectileSpawner (attached to the Player) and schedule the
            // next spawn using the reduced enhanced cooldown.
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                ProjectileSpawner spawner = player.GetComponent<ProjectileSpawner>();
                if (spawner != null)
                {
                    spawner.ApplyEnhancedFirstSpawnReduction(card);
                }
            }
        }

        // SPECIAL CASE: HolyShield is spawned once and then manages its own
        // respawn cycle. When the player picks a HolyShield variant via the
        // UI, immediately push that variant onto the currently active shield
        // instance so the upgrade takes effect without waiting for a new
        // spawn.
        if (card.projectileType == ProjectileCards.ProjectileType.HolyShield && HolyShield.ActiveShield != null)
        {
            HolyShield.ActiveShield.ApplyVariantFromIndex(storedVariant);
        }

        if (variantIndex == 3)
        {
            if (card.projectileType == ProjectileCards.ProjectileType.NovaStar)
            {
                CardModifierStats modifiers = new CardModifierStats();
                if (ProjectileCardModifiers.Instance != null)
                {
                    modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
                }

                NovaStar[] stars = FindObjectsOfType<NovaStar>();
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] != null)
                    {
                        stars[i].ApplyInstantModifiers(modifiers);
                    }
                }
            }
            else if (card.projectileType == ProjectileCards.ProjectileType.DwarfStar)
            {
                CardModifierStats modifiers = new CardModifierStats();
                if (ProjectileCardModifiers.Instance != null)
                {
                    modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
                }

                DwarfStar[] stars = FindObjectsOfType<DwarfStar>();
                for (int i = 0; i < stars.Length; i++)
                {
                    if (stars[i] != null)
                    {
                        stars[i].ApplyInstantModifiers(modifiers);
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Get the selected enhanced variant for a card (0 = none/basic)
    /// </summary>
    public int GetEnhancedVariant(ProjectileCards card)
    {
        if (card == null) return 0;
        
        string cardKey = card.cardName;
        if (selectedEnhancedVariants.ContainsKey(cardKey))
        {
            return selectedEnhancedVariants[cardKey];
        }
        return 0; // Default to basic/none
    }

    public void SetUltimateEnhancementSelected(ProjectileCards card, bool selected)
    {
        if (card == null) return;

        string cardKey = card.cardName;
        ultimateEnhancementsSelected[cardKey] = selected;
    }

    public bool IsUltimateEnhancementSelected(ProjectileCards card)
    {
        if (card == null) return false;

        string cardKey = card.cardName;
        if (ultimateEnhancementsSelected.TryGetValue(cardKey, out bool selected))
        {
            return selected;
        }
        return false;
    }

    public bool IsUltimateTierSelection(ProjectileCards card, int tier)
    {
        if (card == null) return false;
        if (enhancedUnlockLevel <= 0) return false;
        if (UltimateEnhancedUnlockLevel <= 0) return false;
        if (!CardHasUltimateVariant(card)) return false;

        int normalVariantCount = GetNormalVariantCountForCard(card);
        if (normalVariantCount <= 0)
        {
            normalVariantCount = 3;
        }

        int ultimateTier = normalVariantCount + 1;
        if (tier != ultimateTier)
        {
            return false;
        }

        int unlockAt = (normalVariantCount * enhancedUnlockLevel) + UltimateEnhancedUnlockLevel;
        return GetLevel(card) >= unlockAt;
    }
    
    /// <summary>
    /// Check if a specific variant index (1-3) has EVER been chosen for this card.
    /// Used by the variant selection UI to avoid offering the same variant twice.
    /// </summary>
    public bool HasChosenVariant(ProjectileCards card, int variantIndex)
    {
        if (card == null || variantIndex <= 0) return false;

        string cardKey = card.cardName;
        if (chosenVariantHistory.TryGetValue(cardKey, out var history))
        {
            return history.Contains(variantIndex);
        }
        return false;
    }
    
    /// <summary>
    /// Get level gain amount based on card rarity
    /// </summary>
    public int GetLevelGainByRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common:
                return commonLevelGain;
            case CardRarity.Uncommon:
                return uncommonLevelGain;
            case CardRarity.Rare:
                return rareLevelGain;
            case CardRarity.Epic:
                return epicLevelGain;
            case CardRarity.Legendary:
                return legendaryLevelGain;
            case CardRarity.Mythic:
                return mythicLevelGain;
            default:
                return 1;
        }
    }
    
    /// <summary>
    /// Get the enhanced unlock level (for UI display)
    /// </summary>
    public int GetEnhancedUnlockLevel()
    {
        return enhancedUnlockLevel;
    }
    
    /// <summary>
    /// Get modifier count for a given rarity (uses public fields that can be adjusted in Inspector)
    /// </summary>
    public int GetModifierCountForRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonModifierCount;
            case CardRarity.Uncommon: return uncommonModifierCount;
            case CardRarity.Rare: return rareModifierCount;
            case CardRarity.Epic: return epicModifierCount;
            case CardRarity.Legendary: return legendaryModifierCount;
            case CardRarity.Mythic: return mythicModifierCount;
            default: return commonModifierCount;
        }
    }
    
    /// <summary>
    /// Reset all card levels (for testing or new game)
    /// </summary>
    public void ResetAllLevels()
    {
        cardLevels.Clear();
        selectedEnhancedVariants.Clear();
        ultimateEnhancementsSelected.Clear();
        chosenVariantHistory.Clear();
        Debug.Log("<color=yellow>All card levels reset!</color>");
    }
}
