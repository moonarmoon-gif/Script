using UnityEngine;
using System.Collections.Generic;
using System;

[System.Serializable]
public class ProjectileModifierData
{
    public enum ModifierType
    {
        IncreasedSpeed,
        IncreasedSize,
        Piercing,
        LifetimeIncrease,
        CooldownReduction,
        ManaCostReduction,
        DamageIncrease,
        ExplosionRadius,
        StrikeZoneRadius,
        ProjectileCount,
        DamageRadius,
        AttackSpeed,
        PullStrength,
        ShieldHealth
    }
    
    public ModifierType type;
    
    [Header("Rarity Unlock")]
    [Tooltip("Minimum card rarity required for this modifier to be eligible.")]
    public CardRarity unlockRarity = CardRarity.Common;
    public int displayPosition = 0;
    
    [Header("Projectile Restrictions")]
    [Tooltip("Which projectiles can have this modifier (leave empty for all)")]
    public List<ProjectileCards.ProjectileType> enabledForProjectiles = new List<ProjectileCards.ProjectileType>();
    
    [Header("Custom Description Formatting")]
    [Tooltip("Custom description (leave empty for auto-generated). Use {value} for the value placeholder")]
    public string customDescription = "";
    [Tooltip("Color for this modifier (hex format: #RRGGBB or #RRGGBBAA, empty for default)")]
    public string modifierColor = "";
    [Tooltip("Outline color (hex format: #RRGGBB or #RRGGBBAA, empty for no outline)")]
    public string modifierOutlineColor = "";
    
    [Header("Common Rarity Values")]
    public float commonValue = 5f;
    
    [Header("Uncommon Rarity Values")]
    public float uncommonValue = 10f;
    
    [Header("Rare Rarity Values")]
    public float rareValue = 15f;
    
    [Header("Epic Rarity Values")]
    public float epicValue = 25f;
    
    [Header("Legendary Rarity Values")]
    public float legendaryValue = 40f;
    
    [Header("Mythic Rarity Values")]
    public float mythicValue = 60f;
    
    public float GetValueForRarity(CardRarity rarity)
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonValue;
            case CardRarity.Uncommon: return uncommonValue;
            case CardRarity.Rare: return rareValue;
            case CardRarity.Epic: return epicValue;
            case CardRarity.Legendary: return legendaryValue;
            case CardRarity.Mythic: return mythicValue;
            default: return 0f;
        }
    }
    
    public bool IsEnabledForProjectile(ProjectileCards.ProjectileType projectileType)
    {
        // If list is empty, enabled for all projectiles
        if (enabledForProjectiles == null || enabledForProjectiles.Count == 0)
            return true;
        
        return enabledForProjectiles.Contains(projectileType);
    }
    
    public bool IsUnlockedForRarity(CardRarity cardRarity)
    {
        return cardRarity >= unlockRarity;
    }
    
    public string GetFormattedDescription(CardRarity rarity)
    {
        float value = GetValueForRarity(rarity);
        string desc;
        
        // Use custom description if provided
        if (!string.IsNullOrEmpty(customDescription))
        {
            // Use appropriate formatting based on value size
            string formattedValue = value < 1f ? value.ToString("F2") : (value < 10f ? value.ToString("F1") : value.ToString("F0"));
            desc = customDescription.Replace("{value}", formattedValue);
        }
        else
        {
            // Auto-generate description
            switch (type)
            {
                case ModifierType.IncreasedSpeed:
                    desc = $"+{value:F1} Speed";
                    break;
                case ModifierType.IncreasedSize:
                    // Show decimals for values < 1
                    desc = value < 1f ? $"+{value:F2}% Size" : $"+{value:F0}% Size";
                    break;
                case ModifierType.Piercing:
                    desc = $"+{(int)value} Pierce";
                    break;
                case ModifierType.LifetimeIncrease:
                    desc = $"+{value:F1}s Lifetime";
                    break;
                case ModifierType.CooldownReduction:
                    // Show decimals for values < 1
                    desc = value < 1f ? $"-{value:F2}% Cooldown" : $"-{value:F0}% Cooldown";
                    break;
                case ModifierType.ManaCostReduction:
                    // Show decimals for values < 1
                    desc = value < 1f ? $"-{value:F2}% Mana Cost" : $"-{value:F0}% Mana Cost";
                    break;
                case ModifierType.DamageIncrease:
                    // Flat damage increase (no percentage)
                    desc = $"+{value:F0} Damage";
                    break;
                case ModifierType.ExplosionRadius:
                    // Raw value, not percentage
                    desc = $"+{value:F1} Explosion Radius";
                    break;
                case ModifierType.StrikeZoneRadius:
                    // Raw value, not percentage
                    desc = $"+{value:F1} Strike Zone";
                    break;
                case ModifierType.ProjectileCount:
                    // Raw value (integer)
                    desc = $"+{(int)value} Projectiles";
                    break;
                case ModifierType.DamageRadius:
                    // Raw value, not percentage
                    desc = $"+{value:F1} Damage Radius";
                    break;
                case ModifierType.AttackSpeed:
                    // Show decimals for values < 1
                    desc = value < 1f ? $"+{value:F2}% Attack Speed" : $"+{value:F0}% Attack Speed";
                    break;
                case ModifierType.PullStrength:
                    // Flat pull strength value
                    desc = $"+{value:F1} Pull Strength";
                    break;
                case ModifierType.ShieldHealth:
                    // Raw flat bonus to HolyShield health
                    desc = $"+{value:F1} Shield Health";
                    break;
                default:
                    desc = "";
                    break;
            }
        }
        
        // Apply color if specified
        if (!string.IsNullOrEmpty(modifierColor))
        {
            desc = $"<color={modifierColor}>{desc}</color>";
        }
        
        // Apply outline if specified
        if (!string.IsNullOrEmpty(modifierOutlineColor))
        {
            desc = $"<outline={modifierOutlineColor}>{desc}</outline>";
        }
        
        return desc;
    }
    
    public string GetFormattedDescription(CardRarity rarity, string format)
    {
        float value = GetValueForRarity(rarity);
        
        switch (type)
        {
            case ModifierType.IncreasedSpeed:
                return format.Replace("{value}", $"+{value:F1}").Replace("{type}", "Speed");
            case ModifierType.IncreasedSize:
                return format.Replace("{value}", value < 1f ? $"+{value:F2}%" : $"+{value:F0}%").Replace("{type}", "Size");
            case ModifierType.Piercing:
                return format.Replace("{value}", $"+{(int)value}").Replace("{type}", "Pierce");
            case ModifierType.LifetimeIncrease:
                return format.Replace("{value}", $"+{value:F1}s").Replace("{type}", "Lifetime");
            case ModifierType.CooldownReduction:
                return format.Replace("{value}", value < 1f ? $"-{value:F2}%" : $"-{value:F0}%").Replace("{type}", "Cooldown");
            case ModifierType.ManaCostReduction:
                return format.Replace("{value}", value < 1f ? $"-{value:F2}%" : $"-{value:F0}%").Replace("{type}", "Mana Cost");
            case ModifierType.DamageIncrease:
                // Flat damage increase (no percentage symbol)
                return format.Replace("{value}", $"+{value:F0}").Replace("{type}", "Damage");
            case ModifierType.ExplosionRadius:
                return format.Replace("{value}", $"+{value:F1}").Replace("{type}", "Explosion Radius");
            case ModifierType.StrikeZoneRadius:
                return format.Replace("{value}", $"+{value:F1}").Replace("{type}", "Strike Zone");
            case ModifierType.ProjectileCount:
                return format.Replace("{value}", $"+{(int)value}").Replace("{type}", "Projectiles");
            case ModifierType.DamageRadius:
                return format.Replace("{value}", $"+{value:F1}").Replace("{type}", "Damage Radius");
            case ModifierType.AttackSpeed:
                return format.Replace("{value}", value < 1f ? $"+{value:F2}%" : $"+{value:F0}%").Replace("{type}", "Attack Speed");
            case ModifierType.PullStrength:
                return format.Replace("{value}", $"+{value:F1}").Replace("{type}", "Pull Strength");
            case ModifierType.ShieldHealth:
                return format.Replace("{value}", $"+{value:F1}").Replace("{type}", "Shield Health");
            default:
                return "";
        }
    }
    
    public void ApplyToStats(PlayerStats stats, CardRarity rarity)
    {
        float value = GetValueForRarity(rarity);
        
        switch (type)
        {
            case ModifierType.IncreasedSpeed:
                stats.projectileSpeedBonus += value; // Raw value, not percentage
                break;
            case ModifierType.IncreasedSize:
                stats.projectileSizeMultiplier += value / 100f;
                break;
            case ModifierType.Piercing:
                stats.projectilePierceCount += (int)value;
                break;
            case ModifierType.LifetimeIncrease:
                stats.projectileLifetimeBonus += value; // Raw value in seconds
                break;
            case ModifierType.CooldownReduction:
                stats.projectileCooldownReduction += value / 100f;
                break;
            case ModifierType.ManaCostReduction:
                stats.projectileManaCostReduction += value / 100f;
                break;
            case ModifierType.DamageIncrease:
                stats.projectileFlatDamage += value;
                break;
            case ModifierType.ExplosionRadius:
                stats.explosionRadiusBonus += value; // Raw value, not percentage
                break;
            case ModifierType.StrikeZoneRadius:
                stats.strikeZoneRadiusBonus += value; // Raw value, not percentage
                break;
            case ModifierType.ProjectileCount:
                stats.additionalProjectiles += (int)value; // Already raw value
                break;
        }
    }

    internal float ApplyDamageModifier(float finalDamage)
    {
        // Damage modifiers are currently applied via CardModifierStats
        // and ProjectileModifierHelper/UniversalInstantModifier paths.
        // This method is kept as a safe no-op to avoid double-applying
        // damage while allowing callers like NovaStar/DwarfStar to
        // invoke it without exceptions.
        return finalDamage;
    }
}
