using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Stores all player stats that can be modified by cards
/// </summary>
public class PlayerStats : MonoBehaviour
{
    [Header("Core Stats")]
    public float maxHealth = 100f;
    public float maxMana = 100f;
    public float baseAttack = 10f;

    [HideInInspector]
    public float flatDamage = 0f; // Flat damage added to all attacks
    public float critChance = 1f; // Base 1% crit chance
    public float critDamage = 25f; // Base 25% crit damage (125% total)
    public float luck = 0f;
    public int favourLuck = 0;
    public float experienceMultiplier = 100f;
    [HideInInspector]
    public float soulGainMultiplier = 1f;
    public float damageMultiplier = 100f;
    [HideInInspector]
    public float nonBossIncomingDamageMultiplier = 1f;
    public float armor = 0f;
    public float manaRegenPerSecond = 1f; // Base mana regeneration rate
    public float healthRegenPerSecond = 1f; // Health regeneration per second

    [FormerlySerializedAs("attackSpeedPercent")]
    public float AttackSpeedBonus = 0f;

    public float Cooldown = 100f;


    [HideInInspector]
    public float moveSpeedMultiplier = 1f;

    [Header("Favour Stats")]
    [Tooltip("Global damage multiplier applied by Favour effects (1 = no change).")]
    [HideInInspector]
    public float favourDamageMultiplier = 1f;

    [Header("Level Stats - Synced with PlayerLevel")]
    [Tooltip("These are synced from PlayerLevel component - don't modify directly!")]
    [HideInInspector]
    public int currentLevel = 1;
    [HideInInspector]
    public float currentExperience = 0f;
    [HideInInspector]
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

    private static LevelUpUI cachedLevelUpUIForBaseValues;

    [System.NonSerialized]
    private bool levelUpAllocationsApplied;

    [System.NonSerialized]
    private float levelUpAppliedMaxHealthBonus;

    [System.NonSerialized]
    private int levelUpAppliedMaxManaBonus;

    [System.NonSerialized]
    private float levelUpAppliedBaseAttackBonus;

    [System.NonSerialized]
    private float levelUpAppliedCooldownDelta;

    [System.NonSerialized]
    private float levelUpAppliedAttackSpeedBonus;

    [System.NonSerialized]
    private float levelUpAppliedCritChanceBonus;

    [System.NonSerialized]
    private float levelUpAppliedManaRegenBonus;

    [System.NonSerialized]
    private float levelUpAppliedArmorBonus;

    [System.NonSerialized]
    private float levelUpAppliedHealthRegenBonus;

    [SerializeField, HideInInspector]
    private bool percentMultipliersInitialized;

    private void OnValidate()
    {
        MigratePercentMultipliersIfNeeded();
    }

    private void Awake()
    {
        MigratePercentMultipliersIfNeeded();
        cachedPlayerHealth = GetComponent<PlayerHealth>();
        cachedPlayerMana = GetComponent<PlayerMana>();
        cachedPlayerLevel = GetComponent<PlayerLevel>();

        if (gameObject != null && (gameObject.hideFlags & HideFlags.DontSave) != 0)
        {
            return;
        }

        ReapplyLevelUpAllocationsFromPrefs(fillToMax: true, refillMana: true);
    }

    public void ResetLevelUpAllocationTracking()
    {
        levelUpAllocationsApplied = false;

        levelUpAppliedMaxHealthBonus = 0f;
        levelUpAppliedMaxManaBonus = 0;
        levelUpAppliedBaseAttackBonus = 0f;
        levelUpAppliedCooldownDelta = 0f;
        levelUpAppliedAttackSpeedBonus = 0f;
        levelUpAppliedCritChanceBonus = 0f;
        levelUpAppliedManaRegenBonus = 0f;
        levelUpAppliedArmorBonus = 0f;
        levelUpAppliedHealthRegenBonus = 0f;
    }

    public void ReapplyLevelUpAllocationsFromPrefs(bool fillToMax = true, bool refillMana = true)
    {
        int intelligenceSaved = PlayerPrefs.GetInt("LevelUpUI.Intelligence", 0);
        int agilitySaved = PlayerPrefs.GetInt("LevelUpUI.Agility", 0);
        int willpowerSaved = PlayerPrefs.GetInt("LevelUpUI.Willpower", 0);
        int vitalitySaved = PlayerPrefs.GetInt("LevelUpUI.Vitality", 0);

        int intelligenceBase = GetLevelUpUIBaseValue("Intelligence", 0);
        int agilityBase = GetLevelUpUIBaseValue("Agility", 0);
        int willpowerBase = GetLevelUpUIBaseValue("Willpower", 0);
        int vitalityBase = GetLevelUpUIBaseValue("Vitality", 0);

        int intelligence = Mathf.Max(0, intelligenceSaved - intelligenceBase);
        int agility = Mathf.Max(0, agilitySaved - agilityBase);
        int willpower = Mathf.Max(0, willpowerSaved - willpowerBase);
        int vitality = Mathf.Max(0, vitalitySaved - vitalityBase);

        int requiredPointsForEnhanced = Mathf.Max(1, PlayerPrefs.GetInt("LevelUpUI.RequiredPointsForEnhanced", 5));

        float intelligenceNormal = PlayerPrefs.GetFloat("LevelUpUI.Intelligence.NormalStatValue", 0.2f);
        float intelligenceEnhanced = PlayerPrefs.GetFloat("LevelUpUI.Intelligence.EnhancedStatValue", 5f);

        float agilityNormal = PlayerPrefs.GetFloat("LevelUpUI.Agility.NormalStatValue", 0.5f);
        float agilityEnhanced = PlayerPrefs.GetFloat("LevelUpUI.Agility.EnhancedStatValue", 2f);

        float willpowerNormal = PlayerPrefs.GetFloat("LevelUpUI.Willpower.NormalStatValue", 0.05f);
        float willpowerEnhanced = PlayerPrefs.GetFloat("LevelUpUI.Willpower.EnhancedStatValue", 20f);

        float vitalityNormal = PlayerPrefs.GetFloat("LevelUpUI.Vitality.NormalStatValue", 5f);
        float vitalityEnhanced = PlayerPrefs.GetFloat("LevelUpUI.Vitality.EnhancedStatValue", 0.5f);

        int intelligenceEnhancedCount = intelligence / requiredPointsForEnhanced;
        int agilityEnhancedCount = agility / requiredPointsForEnhanced;
        int willpowerEnhancedCount = willpower / requiredPointsForEnhanced;
        int vitalityEnhancedCount = vitality / 5;

        float newMaxHealthBonus = vitality * vitalityNormal;
        int newMaxManaBonus = Mathf.RoundToInt(willpowerEnhancedCount * willpowerEnhanced);
        float newBaseAttackBonus = intelligenceEnhancedCount * intelligenceEnhanced;
        float newCooldownDelta = -intelligence * intelligenceNormal;
        float newAttackSpeedBonus = agility * agilityNormal;
        float newCritChanceBonus = agilityEnhancedCount * agilityEnhanced;
        float newManaRegenBonus = willpower * willpowerNormal;
        float newArmorBonus = 0f;
        float newHealthRegenBonus = vitalityEnhancedCount * vitalityEnhanced;

        float deltaMaxHealth = newMaxHealthBonus - levelUpAppliedMaxHealthBonus;
        if (cachedPlayerHealth != null && !Mathf.Approximately(deltaMaxHealth, 0f))
        {
            cachedPlayerHealth.SetMaxHealth(cachedPlayerHealth.MaxHealth + deltaMaxHealth, fillToMax: fillToMax);
        }

        int deltaMaxMana = newMaxManaBonus - levelUpAppliedMaxManaBonus;
        if (cachedPlayerMana != null && deltaMaxMana != 0)
        {
            cachedPlayerMana.SetMaxMana(cachedPlayerMana.MaxMana + deltaMaxMana, refill: refillMana);
        }

        baseAttack += newBaseAttackBonus - levelUpAppliedBaseAttackBonus;
        Cooldown += newCooldownDelta - levelUpAppliedCooldownDelta;
        Cooldown = Mathf.Max(0f, Cooldown);
        AttackSpeedBonus += newAttackSpeedBonus - levelUpAppliedAttackSpeedBonus;
        critChance += newCritChanceBonus - levelUpAppliedCritChanceBonus;
        manaRegenPerSecond += newManaRegenBonus - levelUpAppliedManaRegenBonus;
        armor += newArmorBonus - levelUpAppliedArmorBonus;
        armor = Mathf.Max(0f, armor);
        healthRegenPerSecond += newHealthRegenBonus - levelUpAppliedHealthRegenBonus;
        healthRegenPerSecond = Mathf.Max(0f, healthRegenPerSecond);

        levelUpAppliedMaxHealthBonus = newMaxHealthBonus;
        levelUpAppliedMaxManaBonus = newMaxManaBonus;
        levelUpAppliedBaseAttackBonus = newBaseAttackBonus;
        levelUpAppliedCooldownDelta = newCooldownDelta;
        levelUpAppliedAttackSpeedBonus = newAttackSpeedBonus;
        levelUpAppliedCritChanceBonus = newCritChanceBonus;
        levelUpAppliedManaRegenBonus = newManaRegenBonus;
        levelUpAppliedArmorBonus = newArmorBonus;
        levelUpAppliedHealthRegenBonus = newHealthRegenBonus;

        levelUpAllocationsApplied = true;
    }

    private static int GetLevelUpUIBaseValue(string statName, int fallback)
    {
        if (!string.IsNullOrEmpty(statName))
        {
            string prefsKey = $"LevelUpUI.{statName}.BaseValue";
            if (PlayerPrefs.HasKey(prefsKey))
            {
                return PlayerPrefs.GetInt(prefsKey, fallback);
            }
        }

        if (cachedLevelUpUIForBaseValues == null)
        {
            cachedLevelUpUIForBaseValues = FindObjectOfType<LevelUpUI>(true);
        }

        if (cachedLevelUpUIForBaseValues == null || cachedLevelUpUIForBaseValues.Stats == null)
        {
            return fallback;
        }

        for (int i = 0; i < cachedLevelUpUIForBaseValues.Stats.Count; i++)
        {
            LevelUpUI.StatRow row = cachedLevelUpUIForBaseValues.Stats[i];
            if (row == null) continue;
            if (!string.Equals(row.statName, statName, System.StringComparison.Ordinal)) continue;
            return row.baseValue;
        }

        return fallback;
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

    private void MigratePercentMultipliersIfNeeded()
    {
        if (percentMultipliersInitialized)
        {
            return;
        }

        if (experienceMultiplier > 0f && experienceMultiplier <= 10f)
        {
            experienceMultiplier *= 100f;
        }

        if (damageMultiplier > 0f && damageMultiplier <= 10f)
        {
            damageMultiplier *= 100f;
        }

        percentMultipliersInitialized = true;
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
                float per = 1f;
                if (StatusControllerManager.Instance != null)
                {
                    per = StatusControllerManager.Instance.FuryAttackBonusPerStack;
                }

                furyFlat = Mathf.Max(0f, per * furyStacks);
            }
        }

        if (isProjectile)
        {
            damage += projectileFlatDamage;
        }

        damage = (damage + flatDamage + furyFlat) * (damageMultiplier / 100f) * favourDamageMultiplier;

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