using UnityEngine;
using UnityEngine.Serialization;

public class StatusControllerManager : MonoBehaviour
{
    public static StatusControllerManager Instance { get; private set; }

    [Header("Vulnerable")]
    [SerializeField] private float vulnerableBonusPercent = 25f;

    [Header("Defense")]
    [SerializeField] private float defenseDamageMultiplier = 0.5f;

    [Header("Poison")]
    [SerializeField] private float poisonDamagePerStack = 1f;
    [SerializeField] private float poisonTickInterval = 1f;
    [SerializeField, Tooltip("Duration in seconds for Poison status.")]
    private float poisonDurationSeconds = 5f;

    [Header("Acceleration")]
    [SerializeField] private float accelerationAttackSpeedPercent = 25f;
    [SerializeField] private float accelerationDurationSeconds = 3f;

    [Header("Thorn")]
    [SerializeField] private float thornReflectFlatDamagePerStack = 10f;

    [Header("Curse")]
    [SerializeField] private float curseBonusElementalDamagePercentPerStack = 25f;

    [Header("Decay")]
    [SerializeField] private float decayDamageReductionPercentPerStack = 1f;

    [Header("Condemn")]
    [SerializeField] private float condemnDamageTakenPercentPerStack = 1f;

    [Header("Death Mark")]
    [SerializeField] private float deathMarkDamageTakenPercentPerStack = 1f;

    [Header("Wound")]
    [SerializeField] private float woundFlatDamagePerStack = 1f;
    [SerializeField] private float woundDurationSeconds = 5f;

    [Header("Weak")]
    [SerializeField, Tooltip("Percent damage reduction applied by WEAK to the next damaging instance.")]
    private float weakDamageReductionPercent = 50f;

    [Header("Armor")]
    [SerializeField] private float armorFlatReductionPerStack = 1f;

    [Header("Absorption")]
    [SerializeField, Tooltip("Maximum percent of max health that can be lost to a single hit while Absorption is active.")]
    private float absorptionMaxHitPercent = 10f;

    [Header("Healing Modifiers")]
    [SerializeField, Tooltip("Percent reduction to healing while Bleed status is active.")]
    private float bleedHealingReductionPercent = 50f;
    [SerializeField, Tooltip("Duration in seconds for Bleed status.")]
    private float bleedDurationSeconds = 5f;
    [SerializeField, Tooltip("Percent increase to healing while Blessing status is active.")]
    private float blessingHealingIncreasePercent = 50f;
    [SerializeField, Tooltip("Duration in seconds for Blessing status.")]
    private float blessingDurationSeconds = 5f;

    [Header("First Strike")]
    [SerializeField, Tooltip("Bonus percent damage for First Strike (applied once when the status is consumed).")]
    private float firstStrikeBonusPercent = 10f;

    [Header("Player Movement")]
    [SerializeField, Tooltip("Percent movement speed reduction per stack of SLOW on the Player.")]
    private float playerSlowPercentPerStack = 25f;

    [Header("Amnesia")]
    [SerializeField, Tooltip("Percent chance per stack of AMNESIA each frame while moving to cancel movement input.")]
    private float amnesiaChancePerStackPercent = 1f;

    [Header("Enemy Movement")]
    [SerializeField, Tooltip("Percent movement speed increase per stack of HASTE on enemies.")]
    private float enemyHasteMoveSpeedPercentPerStack = 1f;

    [SerializeField, Tooltip("Percent movement speed reduction per stack of BURDEN on enemies.")]
    private float enemyBurdenMoveSpeedPercentPerStack = 1f;

    [Header("Burden")]
    public float SlowPerStack = 1f;
    public int MaxBurdenStacks = 50;

    [Header("Overweight")]
    [SerializeField, Tooltip("Additional Rigidbody2D mass added per stack of OVERWEIGHT.")]
    private float massPerStack = 1f;

    [Header("Frenzy")]
    [SerializeField, Tooltip("Bonus critical chance (percent) granted per stack of FRENZY.")]
    private float critPerStack = 1f;

    [Header("Lethargy")]
    [SerializeField, Tooltip("Additional seconds of attack cooldown per stack of LETHARGY on enemies.")]
    private float lethargyAttackCooldownSecondsPerStack = 0.1f;

    [Header("Shield Strength")]
    [SerializeField, Tooltip("Percent reduction to shield damage taken per stack of SHIELD_STRENGTH.")]
    private float shieldStrengthDamageReductionPercentPerStack = 1f;

    [Header("Shatter")]
    [SerializeField, Tooltip("Percent bonus damage to shields consumed by SHATTER on the next shield hit.")]
    private float shatterShieldBonusPercent = 100f;

    [Header("Revival")]
    [SerializeField, Tooltip("Percent of max health restored when REVIVAL triggers instead of death.")]
    private float revivalHealPercent = 100f;

    [Header("Elemental Vulnerabilities")]
    [SerializeField, Tooltip("Percent increase to ice damage taken per stack of FROSTBITE.")]
    private float frostbiteIceDamageTakenPercentPerStack = 1f;
    [SerializeField, Tooltip("Percent increase to fire damage taken per stack of SCORCHED.")]
    private float scorchedFireDamageTakenPercentPerStack = 1f;
    [SerializeField, Tooltip("Percent increase to lightning damage taken per stack of SHOCKED.")]
    private float shockedLightningDamageTakenPercentPerStack = 1f;

    [Header("Freeze VFX")]
    [SerializeField] private GameObject freezeOnApplyEffectPrefab;
    [SerializeField] private GameObject freezeOnDeathEffectPrefab;
    [SerializeField] private float freezeOnApplyEffectSizeMultiplier = 1f;
    [SerializeField] private float freezeOnDeathEffectSizeMultiplier = 1f;

    [Header("Immolation")]
    [SerializeField, Tooltip("Radius used for IMMOLATION explosion around a dying enemy.")]
    private float immolationRadius = 3f;
    [SerializeField] private GameObject immolationOnApplyEffectPrefab;
    [SerializeField] private GameObject immolationOnDeathEffectPrefab;
    [SerializeField] private float immolationOnApplyEffectSizeMultiplier = 1f;
    [SerializeField] private float immolationOnDeathEffectSizeMultiplier = 1f;

    [Header("Hatred")]
    [SerializeField, Tooltip("Bonus percent damage per HATRED stack for EACH debuff type currently affecting this unit.")]
    private float hatredBonusPercentPerDebuffPerStack = 1f;

    [Header("Focus")]
    [SerializeField, Tooltip("Bonus percent damage per FOCUS stack while at full health.")]
    private float focusBonusPercentPerStack = 10f;

    [Header("Fury")]
    [FormerlySerializedAs("rageAttackBonusPerStack")]
    [SerializeField, Tooltip("Flat Attack bonus granted to the PLAYER per stack of FURY.")]
    private float furyAttackBonusPerStack = 1f;
    [FormerlySerializedAs("rageEnemyBaseDamageBonusPerStack")]
    [SerializeField, Tooltip("Flat base damage bonus granted to ENEMIES per stack of FURY.")]
    private float furyEnemyBaseDamageBonusPerStack = 1f;

    [Header("Rage")]
    [FormerlySerializedAs("furyBonusPercentPerStack")]
    [SerializeField, Tooltip("Bonus percent damage per RAGE stack while at or below the low-health threshold.")]
    private float rageBonusPercentPerStack = 10f;
    [FormerlySerializedAs("furyLowHealthThresholdPercent")]
    [SerializeField, Tooltip("Low-health threshold percent (0-100) used by RAGE (e.g., 50 = 50% or below).")]
    private float rageLowHealthThresholdPercent = 50f;

    [Header("Burn")]
    [SerializeField, Tooltip("Global burn tick interval in seconds. All burn systems must obey this value.")]
    private float burnTickIntervalSeconds = 0.25f;

    [SerializeField, Range(0f, 10f)]
    public float damageRoundingThreshold = 5.5f;

    public float BurnTickIntervalSeconds
    {
        get { return Mathf.Max(0.01f, burnTickIntervalSeconds); }
    }

    public float DamageRoundingThreshold => Mathf.Clamp(damageRoundingThreshold, 0f, 10f);

    public float VulnerableDamageMultiplier
    {
        get
        {
            float bonus = Mathf.Max(0f, vulnerableBonusPercent);
            return 1f + bonus / 100f;
        }
    }

    public float DefenseDamageMultiplier => Mathf.Clamp(defenseDamageMultiplier, 0f, 1f);
    public float PoisonDamagePerStack => Mathf.Max(0f, poisonDamagePerStack);
    public float PoisonTickInterval => Mathf.Max(0.01f, poisonTickInterval);
    public float PoisonDurationSeconds => Mathf.Max(0f, poisonDurationSeconds);
    public float AccelerationAttackSpeedPercent => Mathf.Max(0f, accelerationAttackSpeedPercent);
    public float AccelerationDurationSeconds => Mathf.Max(0f, accelerationDurationSeconds);
    public float ThornReflectFlatDamagePerStack => Mathf.Max(0f, thornReflectFlatDamagePerStack);
    public float CurseBonusElementalDamagePercentPerStack => Mathf.Max(0f, curseBonusElementalDamagePercentPerStack);
    public float DecayDamageReductionPercentPerStack => Mathf.Max(0f, decayDamageReductionPercentPerStack);
    public float CondemnDamageTakenPercentPerStack => Mathf.Max(0f, condemnDamageTakenPercentPerStack);
    public float DeathMarkDamageTakenPercentPerStack => Mathf.Max(0f, deathMarkDamageTakenPercentPerStack);
    public float WoundFlatDamagePerStack => Mathf.Max(0f, woundFlatDamagePerStack);
    public float WoundDurationSeconds => Mathf.Max(0f, woundDurationSeconds);
    public float WeakDamageReductionPercent => Mathf.Max(0f, weakDamageReductionPercent);
    public float ArmorFlatReductionPerStack => Mathf.Max(0f, armorFlatReductionPerStack);
    public float AbsorptionMaxHitPercent => Mathf.Max(0f, absorptionMaxHitPercent);
    public float BleedHealingReductionPercent => Mathf.Max(0f, bleedHealingReductionPercent);
    public float BleedDurationSeconds => Mathf.Max(0f, bleedDurationSeconds);
    public float BlessingHealingIncreasePercent => Mathf.Max(0f, blessingHealingIncreasePercent);
    public float BlessingDurationSeconds => Mathf.Max(0f, blessingDurationSeconds);
    public float FirstStrikeBonusPercent => Mathf.Max(0f, firstStrikeBonusPercent);
    public float PlayerSlowPercentPerStack => Mathf.Max(0f, playerSlowPercentPerStack);
    public float AmnesiaChancePerStackPercent => Mathf.Max(0f, amnesiaChancePerStackPercent);
    public float EnemyHasteMoveSpeedPercentPerStack => Mathf.Max(0f, enemyHasteMoveSpeedPercentPerStack);
    public float EnemyBurdenMoveSpeedPercentPerStack => Mathf.Max(0f, SlowPerStack);
    public float MassPerStack => Mathf.Max(0f, massPerStack);
    public float CritPerStack => Mathf.Max(0f, critPerStack);
    public float LethargyAttackCooldownSecondsPerStack => Mathf.Max(0f, lethargyAttackCooldownSecondsPerStack);
    public float ShieldStrengthDamageReductionPercentPerStack => Mathf.Max(0f, shieldStrengthDamageReductionPercentPerStack);
    public float ShatterShieldBonusPercent => Mathf.Max(0f, shatterShieldBonusPercent);
    public float RevivalHealPercent => Mathf.Max(0f, revivalHealPercent);
    public float FrostbiteIceDamageTakenPercentPerStack => Mathf.Max(0f, frostbiteIceDamageTakenPercentPerStack);
    public float ScorchedFireDamageTakenPercentPerStack => Mathf.Max(0f, scorchedFireDamageTakenPercentPerStack);
    public float ShockedLightningDamageTakenPercentPerStack => Mathf.Max(0f, shockedLightningDamageTakenPercentPerStack);

    public GameObject FreezeOnApplyEffectPrefab => freezeOnApplyEffectPrefab;
    public GameObject FreezeOnDeathEffectPrefab => freezeOnDeathEffectPrefab;
    public float FreezeOnApplyEffectSizeMultiplier => Mathf.Max(0f, freezeOnApplyEffectSizeMultiplier);
    public float FreezeOnDeathEffectSizeMultiplier => Mathf.Max(0f, freezeOnDeathEffectSizeMultiplier);

    public float ImmolationRadius => Mathf.Max(0f, immolationRadius);
    public GameObject ImmolationOnApplyEffectPrefab => immolationOnApplyEffectPrefab;
    public GameObject ImmolationOnDeathEffectPrefab => immolationOnDeathEffectPrefab;
    public float ImmolationOnApplyEffectSizeMultiplier => Mathf.Max(0f, immolationOnApplyEffectSizeMultiplier);
    public float ImmolationOnDeathEffectSizeMultiplier => Mathf.Max(0f, immolationOnDeathEffectSizeMultiplier);

    public float HatredBonusPercentPerDebuffPerStack => Mathf.Max(0f, hatredBonusPercentPerDebuffPerStack);
    public float FocusBonusPercentPerStack => Mathf.Max(0f, focusBonusPercentPerStack);
    public float FuryAttackBonusPerStack => Mathf.Max(0f, furyAttackBonusPerStack);
    public float FuryEnemyBaseDamageBonusPerStack => Mathf.Max(0f, furyEnemyBaseDamageBonusPerStack);

    public float RageBonusPercentPerStack => Mathf.Max(0f, rageBonusPercentPerStack);
    public float RageLowHealthThresholdPercent => Mathf.Clamp(rageLowHealthThresholdPercent, 0f, 100f);

    public float RoundDamage(float rawDamage)
    {
        if (rawDamage <= 0f)
        {
            return 0f;
        }

        float threshold = DamageRoundingThreshold;
        float floor = Mathf.Floor(rawDamage);
        float frac = rawDamage - floor;

        if (frac <= 0f)
        {
            return floor;
        }

        float fracTenths = frac * 10f;
        if (fracTenths < threshold)
        {
            return floor;
        }

        return floor + 1f;
    }

    private void OnValidate()
    {
        MigrateDamageRoundingThresholdIfNeeded();
    }

    private void MigrateDamageRoundingThresholdIfNeeded()
    {
        if (damageRoundingThreshold <= 1f)
        {
            damageRoundingThreshold *= 10f;
        }

        if (Mathf.Abs(damageRoundingThreshold - 8f) <= 0.001f)
        {
            damageRoundingThreshold = 5.5f;
        }

        damageRoundingThreshold = Mathf.Clamp(damageRoundingThreshold, 0f, 10f);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        MigrateDamageRoundingThresholdIfNeeded();
    }
}