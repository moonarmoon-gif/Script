using UnityEngine;

/// <summary>
/// Helper class for applying projectile modifiers correctly
/// </summary>
public static class ProjectileModifierHelper
{
    /// <summary>
    /// Apply speed modifier (RAW value added to base speed)
    /// </summary>
    public static float ApplySpeedModifier(float baseSpeed, CardModifierStats modifiers)
    {
        return baseSpeed + modifiers.speedIncrease;
    }
    
    /// <summary>
    /// Apply lifetime modifier (RAW seconds added to base lifetime)
    /// </summary>
    public static float ApplyLifetimeModifier(float baseLifetime, CardModifierStats modifiers)
    {
        return baseLifetime + modifiers.lifetimeIncrease;
    }
    
    /// <summary>
    /// Apply cooldown reduction (flat seconds from BASE cooldown)
    /// </summary>
    public static float ApplyCooldownReduction(float baseCooldown, CardModifierStats modifiers)
    {
        return Mathf.Max(0.1f, baseCooldown - Mathf.Max(0f, modifiers.cooldownReductionSeconds));
    }
    
    /// <summary>
    /// Apply size modifier (multiplier)
    /// </summary>
    public static float ApplySizeModifier(float baseSize, CardModifierStats modifiers)
    {
        return baseSize * modifiers.sizeMultiplier;
    }
    
    /// <summary>
    /// Apply damage modifier: add flat damage first, then apply any multiplier.
    /// DamageIncrease projectile modifiers now populate modifiers.damageFlat
    /// using their rarity values as RAW damage amounts.
    /// </summary>
    public static float ApplyDamageModifier(float baseDamage, CardModifierStats modifiers)
    {
        float withFlat = baseDamage + modifiers.damageFlat;
        return withFlat * modifiers.damageMultiplier;
    }
    
    /// <summary>
    /// Apply mana cost reduction (percentage)
    /// </summary>
    public static int ApplyManaCostReduction(int baseManaCost, CardModifierStats modifiers)
    {
        return Mathf.Max(1, Mathf.CeilToInt(baseManaCost * (1f - modifiers.manaCostReduction)));
    }
}
