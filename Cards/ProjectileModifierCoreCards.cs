using UnityEngine;

[CreateAssetMenu(fileName = "New Projectile Modifier", menuName = "Cards/Projectile Modifier Core Card")]
public class ProjectileModifierCoreCards : BaseCard
{
    [Header("Projectile Modification Type")]
    public ProjectileModType modType;
    
    // OnValidate removed - now supports all rarities including Common and Uncommon
    
    [Header("Rarity Values - Common")]
    public float commonValue = 5f;
    public float commonSecondary = 2f;
    
    [Header("Rarity Values - Uncommon")]
    public float uncommonValue = 8f;
    public float uncommonSecondary = 4f;
    
    [Header("Rarity Values - Rare")]
    public float rareValue = 10f;
    public float rareSecondary = 5f;
    
    [Header("Rarity Values - Epic")]
    public float epicValue = 20f;
    public float epicSecondary = 10f;
    
    [Header("Rarity Values - Legendary")]
    public float legendaryValue = 40f;
    public float legendarySecondary = 20f;
    
    [Header("Rarity Values - Mythic")]
    public float mythicValue = 80f;
    public float mythicSecondary = 40f;
    
    [Header("Projectile Settings")]
    [Tooltip("Which projectile type this affects (leave null for all)")]
    public GameObject targetProjectilePrefab;
    
    public enum ProjectileModType
    {
        IncreasedSpeed,
        IncreasedSize,
        Piercing,
        Homing,
        Multishot,
        Explosive,
        Bouncing,
        Splitting,
        ChainReaction,
        LifetimeIncrease,
        CooldownReduction,
        ManaCostReduction,
        DamageIncrease,
        StatusEffect
    }
    
    public override void ApplyEffect(GameObject player)
    {
        float primaryVal = GetPrimaryValue();
        float secondaryVal = GetSecondaryValue();
        
        PlayerStats stats = player.GetComponent<PlayerStats>();
        if (stats == null)
        {
            stats = player.AddComponent<PlayerStats>();
        }
        
        switch (modType)
        {
            case ProjectileModType.IncreasedSpeed:
                stats.projectileSpeedMultiplier += primaryVal / 100f;
                break;
                
            case ProjectileModType.IncreasedSize:
                stats.projectileSizeMultiplier += primaryVal / 100f;
                break;
                
            case ProjectileModType.Piercing:
                stats.projectilePierceCount += (int)primaryVal;
                break;
                
            case ProjectileModType.Homing:
                stats.hasHomingProjectiles = true;
                stats.homingStrength += primaryVal;
                break;
                
            case ProjectileModType.Multishot:
                stats.additionalProjectiles += (int)primaryVal;
                break;
                
            case ProjectileModType.Explosive:
                stats.hasExplosiveProjectiles = true;
                stats.explosionRadius += primaryVal;
                stats.explosionDamage += secondaryVal;
                break;
                
            case ProjectileModType.Bouncing:
                stats.projectileBounces += (int)primaryVal;
                break;
                
            case ProjectileModType.Splitting:
                stats.projectileSplitCount += (int)primaryVal;
                break;
                
            case ProjectileModType.ChainReaction:
                stats.hasChainReaction = true;
                stats.chainReactionRadius += primaryVal;
                break;
                
            case ProjectileModType.LifetimeIncrease:
                stats.projectileLifetimeMultiplier += primaryVal / 100f;
                break;
                
            case ProjectileModType.CooldownReduction:
                stats.projectileCooldownReduction += primaryVal / 100f;
                break;
                
            case ProjectileModType.ManaCostReduction:
                stats.projectileManaCostReduction += primaryVal / 100f;
                break;
                
            case ProjectileModType.DamageIncrease:
                stats.projectileFlatDamage += primaryVal;
                break;
                
            case ProjectileModType.StatusEffect:
                stats.hasProjectileStatusEffect = true;
                stats.statusEffectChance += primaryVal;
                break;
        }
    }
    
    private float GetPrimaryValue()
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
    
    private float GetSecondaryValue()
    {
        switch (rarity)
        {
            case CardRarity.Common: return commonSecondary;
            case CardRarity.Uncommon: return uncommonSecondary;
            case CardRarity.Rare: return rareSecondary;
            case CardRarity.Epic: return epicSecondary;
            case CardRarity.Legendary: return legendarySecondary;
            case CardRarity.Mythic: return mythicSecondary;
            default: return commonSecondary;
        }
    }
    
    public override string GetFormattedDescription()
    {
        if (!string.IsNullOrEmpty(description))
        {
            float primaryVal = GetPrimaryValue();
            float secondaryVal = GetSecondaryValue();
            return description.Replace("?", primaryVal.ToString("F0")).Replace("??", secondaryVal.ToString("F0"));
        }
        
        float primaryVal2 = GetPrimaryValue();
        float secondaryVal2 = GetSecondaryValue();
        
        switch (modType)
        {
            case ProjectileModType.IncreasedSpeed:
                return $"Projectiles move {primaryVal2}% faster";
            case ProjectileModType.IncreasedSize:
                return $"Projectiles are {primaryVal2}% larger";
            case ProjectileModType.Piercing:
                return $"Projectiles pierce through {(int)primaryVal2} enemies";
            case ProjectileModType.Homing:
                return $"Projectiles home towards enemies (strength: {primaryVal2})";
            case ProjectileModType.Multishot:
                return $"Fire {(int)primaryVal2} additional projectiles";
            case ProjectileModType.Explosive:
                return $"Projectiles explode ({primaryVal2} radius, {secondaryVal2} damage)";
            case ProjectileModType.Bouncing:
                return $"Projectiles bounce {(int)primaryVal2} times";
            case ProjectileModType.Splitting:
                return $"Projectiles split into {(int)primaryVal2} on impact";
            case ProjectileModType.ChainReaction:
                return $"Projectiles trigger chain reactions ({primaryVal2} radius)";
            case ProjectileModType.LifetimeIncrease:
                return $"Projectiles last {primaryVal2}% longer";
            case ProjectileModType.CooldownReduction:
                return $"Reduce projectile cooldown by {primaryVal2}%";
            case ProjectileModType.ManaCostReduction:
                return $"Reduce projectile mana cost by {primaryVal2}%";
            case ProjectileModType.DamageIncrease:
                return $"Increase projectile damage by +{primaryVal2:F0}";
            case ProjectileModType.StatusEffect:
                return $"{primaryVal2}% chance to apply status effects";
            default:
                return description;
        }
    }
}
