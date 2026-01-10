using UnityEngine;

/// <summary>
/// Stores all player stats that can be modified by cards
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Core Stats")]
    public float maxHealth = 100f;
    public float maxMana = 100f;
    public float baseAttack = 10f;

    public float flatDamage = 0f; // Flat damage added to all attacks
    public float critChance = 1f; // Base 1% crit chance
    public float critDamage = 25f; // Base 25% crit damage (125% total)
    public float luck = 0f;
    public float experienceMultiplier = 1f;
    public float soulGainMultiplier = 1f;
    public float damageMultiplier = 1f;
    public float nonBossIncomingDamageMultiplier = 1f;
    public float armor = 0f;
    public float manaRegenPerSecond = 1f; // Base mana regeneration rate
    public float healthRegenPerSecond = 0f; // Health regeneration per second
    public float attackSpeedPercent = 0f;

    [HideInInspector]
    public float moveSpeedMultiplier = 1f;

    [Header("Favour Stats")]
    [Tooltip("Global damage multiplier applied by Favour effects (1 = no change).")]
    public float favourDamageMultiplier = 1f;

    [Header("Level Stats - Synced with PlayerLevel")]
    [Tooltip("These are synced from PlayerLevel component - don't modify directly!")]
    public int currentLevel = 1;
    public float currentExperience = 0f;
    public float experienceToNextLevel = 100f;

    [HideInInspector]
    public bool lastHitWasCrit = false;

    [HideInInspector]
    public bool burnImmolationCanCrit = false;

    [HideInInspector]
    public float burnImmolationCritChanceBonus = 0f;

    [HideInInspector]
    public float burnImmolationCritDamageBonusPercent = 0f;

    [Header("Projectile Stats")]
    [HideInInspector]
    public float projectileSpeedMultiplier = 1f;
    [HideInInspector]
    public float projectileSpeedBonus = 0f; // Raw speed bonus (not percentage)
    [HideInInspector]
    public float projectileSizeMultiplier = 1f;
    [HideInInspector]
    public int projectilePierceCount = 0;
    [HideInInspector]
    public bool hasHomingProjectiles = false;
    [HideInInspector]
    public float homingStrength = 0f;
    [HideInInspector]
    public int additionalProjectiles = 0;
    [HideInInspector]
    public bool hasExplosiveProjectiles = false;
    [HideInInspector]
    public float explosionRadius = 0f;
    [HideInInspector]
    public float explosionDamage = 0f;
    [HideInInspector]
    public float explosionRadiusMultiplier = 1f; // Multiplier for explosion radius (deprecated - use bonus instead)
    [HideInInspector]
    public float explosionRadiusBonus = 0f; // Raw explosion radius bonus (not percentage)
    [HideInInspector]
    public float strikeZoneRadiusMultiplier = 1f; // Multiplier for strike zone radius (deprecated - use bonus instead)
    [HideInInspector]
    public float strikeZoneRadiusBonus = 0f; // Raw strike zone radius bonus (not percentage)
    [HideInInspector]
    public int projectileBounces = 0;
    [HideInInspector]
    public int projectileSplitCount = 0;
    [HideInInspector]
    public bool hasChainReaction = false;
    [HideInInspector]
    public float chainReactionRadius = 0f;
    [HideInInspector]
    public float projectileLifetimeMultiplier = 1f;
    [HideInInspector]
    public float projectileLifetimeBonus = 0f; // Raw lifetime bonus in seconds (not percentage)
    [HideInInspector]
    public float projectileCooldownReduction = 0f;
    [HideInInspector]
    public float projectileManaCostReduction = 0f;
    [HideInInspector]
    public float projectileFlatDamage = 0f;
    [HideInInspector]
    public float projectileDamageMultiplier = 1f;
    [HideInInspector]
    public float projectileCritChance = 0f;
    [HideInInspector]
    public float projectileCritDamage = 150f;
    [HideInInspector]
    public bool hasProjectileStatusEffect = false;

    // Global bonus chance (0-100) applied to BOTH passive + active projectiles.
    [HideInInspector]
    public float statusEffectChance = 0f;

    // Extra status effect chance (0-100) that applies ONLY to ACTIVE projectile cards.
    // This prevents ACTIVE-only buffs (like ActiveElementalChanceFavour) from also buffing PASSIVE projectiles.
    [HideInInspector]
    public float activeProjectileStatusEffectChanceBonus = 0f;

    [HideInInspector]
    public float burnTotalDamageMultiplier = 0f;

    [HideInInspector]
    public float burnDurationBonus = 0f;

    [HideInInspector]
    public float slowStrengthBonus = 0f;

    [HideInInspector]
    public float slowDurationBonus = 0f;

    [HideInInspector]
    // NOTE: Experience and leveling is now handled by PlayerLevel component!
    // This PlayerStats component only stores the multipliers and synced values.
    // To add experience, use: GetComponent<PlayerLevel>().GainExperience(amount)

    private PlayerHealth cachedPlayerHealth;
    private PlayerMana cachedPlayerMana;
    private PlayerLevel cachedPlayerLevel;

    private void Awake()
    {
        cachedPlayerHealth = GetComponent<PlayerHealth>();
        cachedPlayerMana = GetComponent<PlayerMana>();
        cachedPlayerLevel = GetComponent<PlayerLevel>();
    }

    private void Update()
    {
        if (cachedPlayerHealth != null)
        {
            maxHealth = cachedPlayerHealth.MaxHealth;
        }

        if (cachedPlayerMana != null)
        {
            maxMana = cachedPlayerMana.MaxManaExact;
        }

        if (cachedPlayerLevel != null)
        {
            currentLevel = cachedPlayerLevel.CurrentLevel;
            currentExperience = cachedPlayerLevel.CurrentExp;
            experienceToNextLevel = cachedPlayerLevel.ExpToNextLevel;
        }
    }

    /// <summary>
    /// Calculate final damage with all modifiers
    /// </summary>
    public float CalculateDamage(float baseDamage, bool isProjectile = false)
    {
        lastHitWasCrit = false;

        // Add flat damage FIRST, then apply multipliers
        float damage = baseDamage;

        StatusController statusController = GetComponent<StatusController>();
        float furyFlat = 0f;
        if (statusController != null)
        {
            int furyStacks = statusController.GetStacks(StatusId.Fury);
            if (furyStacks > 0)
            {
                furyFlat = Mathf.Max(0, furyStacks);
            }
        }

        if (isProjectile)
        {
            damage += projectileFlatDamage;
        }

        damage = (damage + flatDamage + furyFlat) * damageMultiplier * favourDamageMultiplier;

        if (isProjectile)
        {
            damage *= projectileDamageMultiplier;
        }

        // Check for crit
        float critRoll = Random.Range(0f, 100f);
        float totalCritChance = critChance + (isProjectile ? projectileCritChance : 0f);

        if (statusController != null && StatusControllerManager.Instance != null)
        {
            int frenzyStacks = statusController.GetStacks(StatusId.Frenzy);
            if (frenzyStacks > 0)
            {
                totalCritChance += StatusControllerManager.Instance.CritPerStack * frenzyStacks;
            }
        }

        if (critRoll < totalCritChance)
        {
            float totalCritDamage = isProjectile ? projectileCritDamage : critDamage;
            damage *= (totalCritDamage / 100f);
            lastHitWasCrit = true;
            Debug.Log($"CRITICAL HIT! {totalCritDamage}% damage");
        }

        return damage;
    }
}