using UnityEngine;

/// <summary>
/// Core stat upgrade cards - Health, Mana, Damage, etc.
/// </summary>
[CreateAssetMenu(fileName = "New Core Card", menuName = "Cards/Core Card")]
public class CoreCards : BaseCard
{
    [Header("Core Stat Type")]
    public CoreStatType statType;

    [Header("Rarity Values - Common")]
    public float commonValue = 20f;

    [Header("Rarity Values - Uncommon")]
    public float uncommonValue = 40f;

    [Header("Rarity Values - Rare")]
    public float rareValue = 80f;

    [Header("Rarity Values - Epic")]
    public float epicValue = 160f;

    [Header("Rarity Values - Legendary")]
    public float legendaryValue = 320f;

    [Header("Rarity Values - Mythic")]
    public float mythicValue = 640f;

    public enum CoreStatType
    {
        Health,
        Mana,
        Damage,
        ManaRegen,
        CritChance,
        CritDamage,
        Luck,
        ExperienceGain,
        SoulGain,
        HealthRegeneration,
        AttackSpeed,
        FavourInterval
    }

    public override void ApplyEffect(GameObject player)
    {
        float finalValue = GetValueForRarity();

        switch (statType)
        {
            case CoreStatType.Health:
                ApplyHealthIncrease(player, finalValue);
                break;
            case CoreStatType.Mana:
                ApplyManaIncrease(player, finalValue);
                break;
            case CoreStatType.Damage:
                ApplyDamageIncrease(player, finalValue);
                break;
            case CoreStatType.ManaRegen:
                ApplyManaRegenIncrease(player, finalValue);
                break;
            case CoreStatType.CritChance:
                ApplyCritChanceIncrease(player, finalValue);
                break;
            case CoreStatType.CritDamage:
                ApplyCritDamageIncrease(player, finalValue);
                break;
            case CoreStatType.Luck:
                ApplyLuckIncrease(player, finalValue);
                break;
            case CoreStatType.ExperienceGain:
                ApplyExperienceGainIncrease(player, finalValue);
                break;
            case CoreStatType.SoulGain:
                ApplySoulGainIncrease(player, finalValue);
                break;
            case CoreStatType.HealthRegeneration:
                ApplyHealthRegenerationIncrease(player, finalValue);
                break;
            case CoreStatType.AttackSpeed:
                ApplyAttackSpeedIncrease(player, finalValue);
                break;
            case CoreStatType.FavourInterval:
                ApplyFavourIntervalReduction(finalValue);
                break;
        }

        string sign = statType == CoreStatType.FavourInterval ? "-" : "+";
        Debug.Log($"Applied {cardName} ({rarity}): {sign}{finalValue} {statType}");
    }

    private float GetValueForRarity()
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonValue;
            case CardRarity.Uncommon: return uncommonValue;
            case CardRarity.Rare: return rareValue;
            case CardRarity.Epic: return epicValue;
            case CardRarity.Legendary: return legendaryValue;
            case CardRarity.Mythic: return mythicValue;
            default: return commonValue;
        }
    }

    private void ApplyHealthIncrease(GameObject player, float amount)
    {
        PlayerHealth health = player.GetComponent<PlayerHealth>();
        if (health != null)
        {
            // Use existing IncreaseMaxHealth method which increases both max and current
            float oldCurrent = health.CurrentHealth;
            float oldMax = health.MaxHealth;
            
            health.IncreaseMaxHealth(amount);
            
            Debug.Log($"Health increased by {amount}. Was {oldCurrent}/{oldMax}, now {health.CurrentHealth}/{health.MaxHealth}");
        }
        else
        {
            Debug.LogWarning("PlayerHealth component not found!");
        }
    }

    private void ApplyManaIncrease(GameObject player, float amount)
    {
        PlayerMana mana = player.GetComponent<PlayerMana>();
        if (mana != null)
        {
            // Use existing IncreaseMaxMana method which increases both max and current
            int oldCurrent = mana.CurrentMana;
            int oldMax = mana.MaxMana;
            
            mana.IncreaseMaxMana(amount);
            
            Debug.Log($"Mana increased by {Mathf.RoundToInt(amount)}. Was {oldCurrent}/{oldMax}, now {mana.CurrentMana}/{mana.MaxMana}");
        }
        else
        {
            Debug.LogWarning("PlayerMana component not found!");
        }
    }

    private void ApplyDamageIncrease(GameObject player, float amount)
    {
        // Look for a PlayerStats component or similar
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            // FLAT damage addition - not percentage
            // If no flatDamage field exists yet, we'll add it to PlayerStats
            if (!AddFlatDamage(stats, amount))
            {
                // Fallback: use multiplier if flatDamage doesn't exist
                stats.damageMultiplier += amount / 100f;
                Debug.LogWarning($"flatDamage field not found, using multiplier fallback. Damage increased by {amount}%. New multiplier: {stats.damageMultiplier}");
            }
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found! Consider creating one.");
        }
    }
    
    private bool AddFlatDamage(PlayerStats stats, float amount)
    {
        // Try to find and add to flatDamage field
        var flatDamageField = typeof(PlayerStats).GetField("flatDamage",
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.Instance);
        
        if (flatDamageField != null)
        {
            float currentFlat = (float)flatDamageField.GetValue(stats);
            flatDamageField.SetValue(stats, currentFlat + amount);
            Debug.Log($"Flat damage increased by {amount}. New total: {currentFlat + amount}");
            return true;
        }
        
        return false;
    }

    private void ApplyManaRegenIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.manaRegenPerSecond += amount;
            Debug.Log($"Mana Regeneration increased by {amount}/s. New total: {stats.manaRegenPerSecond}/s");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplyCritChanceIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.critChance += amount;
            Debug.Log($"Crit chance increased by {amount}%. New: {stats.critChance}%");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplyCritDamageIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.critDamage += amount;
            Debug.Log($"Crit damage increased by {amount}%. New: {stats.critDamage}%");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplyLuckIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.luck += amount;
            Debug.Log($"Luck increased by {amount}. New: {stats.luck}");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplyExperienceGainIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.experienceMultiplier += amount / 100f;
            Debug.Log($"Experience gain increased by {amount}%. New multiplier: {stats.experienceMultiplier}");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplySoulGainIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.soulGainMultiplier += amount / 100f;
            Debug.Log($"Soul gain increased by {amount}%. New multiplier: {stats.soulGainMultiplier}");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplyAttackSpeedIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.attackSpeedPercent += amount;
            Debug.Log($"Attack Speed increased by {amount}%. New: {stats.attackSpeedPercent}%");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }

    private void ApplyFavourIntervalReduction(float seconds)
    {
        if (seconds <= 0f)
        {
            return;
        }

        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ReduceFavourCardInterval(seconds);
        }
        else
        {
            Debug.LogWarning("CardSelectionManager.Instance not found!");
        }
    }

    public override string GetFormattedDescription()
    {
        if (!string.IsNullOrEmpty(description))
        {
            float finalValue = GetValueForRarity();
            string valueStr = finalValue.ToString("0.##");
            return description.Replace("?", valueStr);
        }
        
        float finalValue2 = GetValueForRarity();
        string valueStr2 = finalValue2.ToString("0.##");

        switch (statType)
        {
            case CoreStatType.Health:
                return $"+{valueStr2} Max Health";
            case CoreStatType.Mana:
                return $"+{valueStr2} Max Mana";
            case CoreStatType.Damage:
                return $"+{valueStr2}% Damage";
            case CoreStatType.ManaRegen:
                return $"+{valueStr2} Mana Regen/s";
            case CoreStatType.CritChance:
                return $"+{valueStr2}% Crit Chance";
            case CoreStatType.CritDamage:
                return $"+{valueStr2}% Crit Damage";
            case CoreStatType.Luck:
                return $"+{valueStr2} Luck";
            case CoreStatType.ExperienceGain:
                return $"+{valueStr2}% Experience Gain";
            case CoreStatType.SoulGain:
                return $"+{valueStr2}% Soul Gain";
            case CoreStatType.HealthRegeneration:
                return $"+{valueStr2} Health Regen/s";
            case CoreStatType.AttackSpeed:
                return $"+{valueStr2}% Attack Speed";
            case CoreStatType.FavourInterval:
                return $"-{valueStr2}s Favour Interval";
            default:
                return description;
        }
    }
    
    private void ApplyHealthRegenerationIncrease(GameObject player, float amount)
    {
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.healthRegenPerSecond += amount;
            Debug.Log($"Health Regeneration increased by {amount}/s. New total: {stats.healthRegenPerSecond}/s");
        }
        else
        {
            Debug.LogWarning("PlayerStats component not found!");
        }
    }
    
}