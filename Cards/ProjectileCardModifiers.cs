using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Stores per-card projectile modifiers that only apply to specific projectile cards
/// </summary>
public class ProjectileCardModifiers : MonoBehaviour
{
    private static ProjectileCardModifiers _instance;
    public static ProjectileCardModifiers Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject go = new GameObject("ProjectileCardModifiers");
                _instance = go.AddComponent<ProjectileCardModifiers>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }
    
    // Dictionary to store modifiers per card instance
    private Dictionary<ProjectileCards, CardModifierStats> cardModifiers = new Dictionary<ProjectileCards, CardModifierStats>();
    
    // Dictionary to store modifiers by prefab+type (for persistent accumulation across card instances)
    private Dictionary<string, CardModifierStats> cardModifiersByKey = new Dictionary<string, CardModifierStats>();
    
    /// <summary>
    /// Generate unique key for a card based on prefab and projectile type
    /// </summary>
    private string GetCardKey(ProjectileCards card)
    {
        if (card.projectilePrefab == null)
        {
            return $"{card.cardName}_{card.projectileType}";
        }
        return $"{card.projectilePrefab.name}_{card.projectileType}";
    }
    
    /// <summary>
    /// Register modifiers for a specific projectile card
    /// </summary>
    public void RegisterCardModifiers(ProjectileCards card, List<ProjectileModifierData> modifiers, CardRarity rarity)
    {
        string cardKey = GetCardKey(card);
        Debug.Log($"<color=magenta>╔═══════════════════════════════════════════════════════════╗</color>");
        Debug.Log($"<color=magenta>║   RegisterCardModifiers: {card.cardName}</color>");
        Debug.Log($"<color=magenta>╚═══════════════════════════════════════════════════════════╝</color>");
        Debug.Log($"  Card Instance ID: {card.GetInstanceID()}");
        Debug.Log($"  Card Key: {cardKey}");
        Debug.Log($"  Modifiers Count: {modifiers.Count}");
        Debug.Log($"  Rarity: {rarity}");
        
        // Use cardKey instead of card instance for persistent storage
        if (!cardModifiersByKey.ContainsKey(cardKey))
        {
            cardModifiersByKey[cardKey] = new CardModifierStats();
            Debug.Log($"<color=magenta>  Created NEW CardModifierStats for key: {cardKey}</color>");
        }
        else
        {
            Debug.Log($"<color=yellow>  Found EXISTING CardModifierStats for key: {cardKey}</color>");
        }
        
        CardModifierStats stats = cardModifiersByKey[cardKey];
        
        // Also update the instance-based dictionary for backward compatibility
        cardModifiers[card] = stats;
        
        // Apply each modifier to this card's stats
        foreach (var modifier in modifiers)
        {
            float value = modifier.GetValueForRarity(rarity);
            Debug.Log($"<color=magenta>Applying {modifier.type} with value {value} for rarity {rarity}</color>");
            
            switch (modifier.type)
            {
                case ProjectileModifierData.ModifierType.IncreasedSpeed:
                    // Support fractional values
                    stats.speedIncrease += value; // RAW value (e.g., 2.5 = +2.5 speed, 0.1 = +0.1 speed)
                    break;
                case ProjectileModifierData.ModifierType.IncreasedSize:
                    // Support fractional values
                    stats.sizeMultiplier += value / 100f; // Percentage (0.1 = 0.001 multiplier)
                    break;
                case ProjectileModifierData.ModifierType.Piercing:
                    // Support fractional values with accumulator
                    stats.pierceAccumulator += value;
                    if (stats.pierceAccumulator >= 1.0f)
                    {
                        int wholePart = Mathf.FloorToInt(stats.pierceAccumulator);
                        stats.pierceCount += wholePart;
                        stats.pierceAccumulator -= wholePart;
                        Debug.Log($"<color=lime>Pierce: Added {value}, Accumulator now {stats.pierceAccumulator:F2}, Total: {stats.pierceCount}</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>Pierce: Added {value}, Accumulator now {stats.pierceAccumulator:F2}</color>");
                    }
                    break;
                case ProjectileModifierData.ModifierType.LifetimeIncrease:
                    // Support fractional values
                    stats.lifetimeIncrease += value; // RAW seconds (e.g., 0.75 = +0.75 seconds, 0.1 = +0.1 seconds)
                    break;
                case ProjectileModifierData.ModifierType.CooldownReduction:
                    // Support fractional values
                    float stepFraction = Mathf.Clamp01(value / 100f);
                    float stepMultiplier = 1f - stepFraction;
                    stats.cooldownMultiplier *= stepMultiplier;
                    stats.cooldownReductionPercent = (1f - stats.cooldownMultiplier) * 100f;
                    break;
                case ProjectileModifierData.ModifierType.ManaCostReduction:
                    // Support fractional values
                    stats.manaCostReduction += value / 100f; // Percentage (0.1 = 0.001)
                    break;
                case ProjectileModifierData.ModifierType.DamageIncrease:
                    // FLAT damage increase applied per hit. value is interpreted as a
                    // raw damage amount taken directly from the modifier's
                    // rarity values (commonValue/uncommonValue/etc.). Projectiles
                    // are responsible for adding stats.damageFlat to their own
                    // base damage before routing through PlayerStats.
                    stats.damageFlat += value;
                    break;
                case ProjectileModifierData.ModifierType.ProjectileCount:
                    // CRITICAL: Support fractional values (0.1, 0.2, etc.)
                    // Add to accumulator, when it reaches 1.0, add to projectileCount
                    stats.projectileCountAccumulator += value;
                    
                    // Check if accumulator reached 1.0 or more
                    if (stats.projectileCountAccumulator >= 1.0f)
                    {
                        int wholePart = Mathf.FloorToInt(stats.projectileCountAccumulator);
                        stats.projectileCount += wholePart;
                        stats.projectileCountAccumulator -= wholePart; // Keep fractional remainder
                        Debug.Log($"<color=lime>Projectile Count: Added {value}, Accumulator now {stats.projectileCountAccumulator:F2}, Total count: {stats.projectileCount}</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=yellow>Projectile Count: Added {value}, Accumulator now {stats.projectileCountAccumulator:F2} (not enough for +1 yet)</color>");
                    }
                    break;
                case ProjectileModifierData.ModifierType.DamageRadius:
                    // Support fractional values
                    stats.damageRadiusIncrease += value; // RAW value (e.g., 1.5 = +1.5 radius, 0.1 = +0.1 radius)
                    Debug.Log($"<color=magenta>Damage Radius: Added {value}, Total bonus: {stats.damageRadiusIncrease:F2}</color>");
                    break;
                case ProjectileModifierData.ModifierType.ExplosionRadius:
                    // Support fractional values - RAW bonus value
                    stats.explosionRadiusBonus += value; // RAW value (e.g., 1.5 = +1.5 radius, 0.1 = +0.1 radius)
                    Debug.Log($"<color=orange>Explosion Radius: Added {value}, Total bonus: {stats.explosionRadiusBonus:F2}</color>");
                    break;
                case ProjectileModifierData.ModifierType.StrikeZoneRadius:
                    // Support fractional values - RAW bonus value
                    stats.strikeZoneRadiusBonus += value; // RAW value (e.g., 1.5 = +1.5 radius, 0.1 = +0.1 radius)
                    Debug.Log($"<color=purple>Strike Zone Radius: Added {value}, Total bonus: {stats.strikeZoneRadiusBonus:F2}</color>");
                    break;
                case ProjectileModifierData.ModifierType.AttackSpeed:
                    // Attack speed is a percentage; stack additively (e.g., +10 and +15 => +25)
                    stats.attackSpeedPercent += value;
                    Debug.Log($"<color=magenta>Attack Speed: Added {value}, Total: {stats.attackSpeedPercent:F2}%</color>");
                    break;
                case ProjectileModifierData.ModifierType.PullStrength:
                    // Flat pull strength bonus (Collapse, etc.)
                    stats.pullStrengthMultiplier += value;
                    Debug.Log($"<color=magenta>Pull Strength: Added {value}, Total bonus: {stats.pullStrengthMultiplier:F2}</color>");
                    break;
                case ProjectileModifierData.ModifierType.ShieldHealth:
                    // Flat bonus to HolyShield max/current health
                    stats.shieldHealthBonus += value;
                    Debug.Log($"<color=magenta>Shield Health: Added {value}, Total bonus: {stats.shieldHealthBonus:F2}</color>");
                    break;
            }
        }
        
        Debug.Log($"<color=green>✓ Registered modifiers for {card.cardName}: Speed=+{stats.speedIncrease:F2}, Size={stats.sizeMultiplier:F2}x, Pierce={stats.pierceCount}, Damage={stats.damageMultiplier:F2}x, Lifetime=+{stats.lifetimeIncrease:F2}s, Cooldown=-{stats.cooldownReductionPercent:F1}%, ManaCost=-{stats.manaCostReduction:F2}, Count={stats.projectileCount}, DamageRadius=+{stats.damageRadiusIncrease:F2}, ExplosionRadius=+{stats.explosionRadiusBonus:F2}, StrikeZoneRadius=+{stats.strikeZoneRadiusBonus:F2}, AttackSpeed=+{stats.attackSpeedPercent:F2}%</color>");
        
        // CRITICAL: Update active stars immediately if this is an OrbitalStar card
        if (card.projectileType == ProjectileCards.ProjectileType.NovaStar || 
            card.projectileType == ProjectileCards.ProjectileType.DwarfStar)
        {
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            if (manager != null)
            {
                manager.UpdateActiveStarModifiers();
                Debug.Log($"<color=magenta>✓ Called UpdateActiveStarModifiers for {card.cardName}</color>");
            }
        }

        // CRITICAL: For PASSIVE projectile cards (ElementalBeam, FireMine, etc.),
        // immediately refresh their passive spawn cooldown so the next spawn
        // uses the updated effective interval (e.g., after projectile count or
        // cooldown modifiers are applied) instead of waiting out the old,
        // slower cooldown.
        if (card.projectileSystem == ProjectileCards.ProjectileSystemType.Passive)
        {
            ProjectileSpawner spawner = FindObjectOfType<ProjectileSpawner>();
            if (spawner != null)
            {
                spawner.RescheduleCooldownForPassiveCard(card);
            }
        }
    }
    
    /// <summary>
    /// Get modifiers for a specific card
    /// </summary>
    public CardModifierStats GetCardModifiers(ProjectileCards card)
    {
        // First try to get from instance dictionary (most recent)
        if (cardModifiers.ContainsKey(card))
        {
            return cardModifiers[card];
        }
        
        // Fallback to key-based lookup for persistent storage
        string cardKey = GetCardKey(card);
        if (cardModifiersByKey.ContainsKey(cardKey))
        {
            // Update instance dictionary for future lookups
            cardModifiers[card] = cardModifiersByKey[cardKey];
            return cardModifiersByKey[cardKey];
        }
        
        return new CardModifierStats(); // Return default stats
    }
    
    /// <summary>
    /// Check if a card has modifiers registered
    /// </summary>
    public bool HasModifiers(ProjectileCards card)
    {
        return cardModifiers.ContainsKey(card);
    }
    
    /// <summary>
    /// Tag a projectile GameObject with its card reference for later retrieval
    /// </summary>
    public void TagProjectileWithCard(GameObject projectile, ProjectileCards card)
    {
        ProjectileCardTag tag = projectile.GetComponent<ProjectileCardTag>();
        if (tag == null)
        {
            tag = projectile.AddComponent<ProjectileCardTag>();
        }
        tag.card = card;
    }
    
    /// <summary>
    /// Get card reference from a tagged projectile
    /// </summary>
    public ProjectileCards GetCardFromProjectile(GameObject projectile)
    {
        ProjectileCardTag tag = projectile.GetComponent<ProjectileCardTag>();
        return tag != null ? tag.card : null;
    }
    
    /// <summary>
    /// Apply modifiers INSTANTLY to all existing projectiles of this card type
    /// Called when a new modifier card is selected
    /// </summary>
    public void ApplyModifiersToExistingProjectiles(ProjectileCards card)
    {
        string cardKey = GetCardKey(card);
        Debug.Log($"<color=lime>╔═══════════════════════════════════════════════════════════╗</color>");
        Debug.Log($"<color=lime>║   INSTANT MODIFIER APPLICATION: {card.cardName}</color>");
        Debug.Log($"<color=lime>╚═══════════════════════════════════════════════════════════╝</color>");
        
        // Find all projectiles with this card tag
        ProjectileCardTag[] allTags = FindObjectsOfType<ProjectileCardTag>();
        int updatedCount = 0;
        
        foreach (var tag in allTags)
        {
            if (tag.card != null && GetCardKey(tag.card) == cardKey)
            {
                // Found a projectile of this card type - apply modifiers instantly
                GameObject projectile = tag.gameObject;
                bool updated = ApplyInstantModifiersToProjectile(projectile, card);
                if (updated) updatedCount++;
            }
        }
        
        Debug.Log($"<color=lime>Applied instant modifiers to {updatedCount} existing projectiles</color>");
    }
    
    /// <summary>
    /// Apply instant modifiers to a single projectile
    /// Returns true if any modifiers were applied
    /// </summary>
    private bool ApplyInstantModifiersToProjectile(GameObject projectile, ProjectileCards card)
    {
        CardModifierStats modifiers = GetCardModifiers(card);
        bool updated = false;
        
        // Try to find and update various projectile components
        // Each projectile type has its own interface for instant updates
        
        // Check for IInstantModifiable interface (new interface for projectiles)
        IInstantModifiable modifiable = projectile.GetComponent<IInstantModifiable>();
        if (modifiable != null)
        {
            modifiable.ApplyInstantModifiers(modifiers);
            updated = true;
            Debug.Log($"<color=lime>  Updated {projectile.name} via IInstantModifiable</color>");
        }
        
        return updated;
    }
}

/// <summary>
/// Component to tag projectiles with their source card
/// </summary>
public class ProjectileCardTag : MonoBehaviour
{
    public ProjectileCards card;
}

/// <summary>
/// Stats for a specific projectile card's modifiers
/// </summary>
[System.Serializable]
public class CardModifierStats
{
    // All values support fractional accumulation (0.1, 0.2, 0.3, etc.)
    public float speedIncrease = 0f; // RAW value added to speed (supports 0.1, 0.2, etc.)
    public float sizeMultiplier = 1f; // Multiplier for size (supports fractional percentages)
    public int pierceCount = 0; // Pierce count (whole number)
    public float pierceAccumulator = 0f; // Fractional accumulator for pierce
    public float lifetimeIncrease = 0f; // RAW seconds added to lifetime (supports 0.1s, 0.2s, etc.)
    public float cooldownReductionPercent = 0f; // Percentage reduced from BASE cooldown (supports 0.5%, 1.2%, etc.)
    public float cooldownMultiplier = 1f;
    public float manaCostReduction = 0f; // Percentage for mana (supports fractional)
    public float damageFlat = 0f; // Flat damage added per hit (from DamageIncrease modifiers)
    public float damageMultiplier = 1f; // Reserved multiplier for damage (kept for backward compatibility)
    public int projectileCount = 0; // Additional projectiles to spawn (whole number)
    public float projectileCountAccumulator = 0f; // Fractional accumulator (0.1 + 0.2 + 0.3 = 0.6, etc.)
    public float damageRadiusIncrease = 0f; // RAW value added to OrbitalStar damage radius (supports 0.1, 0.2, etc.)
    
    // Explosion and strike zone modifiers
    public float explosionRadiusBonus = 0f; // RAW value added to explosion radius (supports 0.1, 0.2, etc.)
    public float explosionRadiusMultiplier = 1f; // Multiplier for explosion radius
    public float strikeZoneRadiusBonus = 0f; // RAW value added to strike zone radius (supports 0.1, 0.2, etc.)
    public float strikeZoneRadiusMultiplier = 1f; // Multiplier for strike zone radius

    // HolyShield-specific modifiers
    public float shieldHealthBonus = 0f; // Flat bonus added to HolyShield max/current health

    // Attack speed (only used for ACTIVE projectile cards via AdvancedPlayerController)
    public float attackSpeedPercent = 0f; // Percentage attack speed bonus (10 = +10%)

    // Pull strength bonus (used by Collapse and other pull-based projectiles)
    public float pullStrengthMultiplier = 0f;
}
