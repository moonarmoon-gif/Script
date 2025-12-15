using UnityEngine;

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

    [Header("Execute")]
    [SerializeField, Tooltip("Health percent threshold under which Execute will instantly kill enemies.")]
    private float executeThresholdPercent = 20f;

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
    [SerializeField, Tooltip("Bonus percent damage per FURY stack while at or below the low-health threshold.")]
    private float furyBonusPercentPerStack = 10f;
    [SerializeField, Tooltip("Low-health threshold percent (0-100) used by FURY (e.g., 50 = 50% or below).")]
    private float furyLowHealthThresholdPercent = 50f;

    public float VulnerableDamageMultiplier
    {
        get
        {
            float bonus = Mathf.Max(0f, vulnerableBonusPercent);
            return 1f + bonus / 100f;
        }
    }

    public float DefenseDamageMultiplier
    {
        get { return Mathf.Clamp(defenseDamageMultiplier, 0f, 1f); }
    }

    public float PoisonDamagePerStack
    {
        get { return Mathf.Max(0f, poisonDamagePerStack); }
    }

    public float PoisonTickInterval
    {
        get { return Mathf.Max(0.01f, poisonTickInterval); }
    }

    public float PoisonDurationSeconds
    {
        get { return Mathf.Max(0f, poisonDurationSeconds); }
    }

    public float AccelerationAttackSpeedPercent
    {
        get { return Mathf.Max(0f, accelerationAttackSpeedPercent); }
    }

    public float AccelerationDurationSeconds
    {
        get { return Mathf.Max(0f, accelerationDurationSeconds); }
    }

    public float ThornReflectFlatDamagePerStack
    {
        get { return Mathf.Max(0f, thornReflectFlatDamagePerStack); }
    }

    public float CurseBonusElementalDamagePercentPerStack
    {
        get { return Mathf.Max(0f, curseBonusElementalDamagePercentPerStack); }
    }

    public float DecayDamageReductionPercentPerStack
    {
        get { return Mathf.Max(0f, decayDamageReductionPercentPerStack); }
    }

    public float CondemnDamageTakenPercentPerStack
    {
        get { return Mathf.Max(0f, condemnDamageTakenPercentPerStack); }
    }

    public float DeathMarkDamageTakenPercentPerStack
    {
        get { return Mathf.Max(0f, deathMarkDamageTakenPercentPerStack); }
    }

    public float WoundFlatDamagePerStack
    {
        get { return Mathf.Max(0f, woundFlatDamagePerStack); }
    }

    public float WoundDurationSeconds
    {
        get { return Mathf.Max(0f, woundDurationSeconds); }
    }

    public float WeakDamageReductionPercent
    {
        get { return Mathf.Max(0f, weakDamageReductionPercent); }
    }

    public float ArmorFlatReductionPerStack
    {
        get { return Mathf.Max(0f, armorFlatReductionPerStack); }
    }

    public float AbsorptionMaxHitPercent
    {
        get { return Mathf.Max(0f, absorptionMaxHitPercent); }
    }

    public float BleedHealingReductionPercent
    {
        get { return Mathf.Max(0f, bleedHealingReductionPercent); }
    }

    public float BleedDurationSeconds
    {
        get { return Mathf.Max(0f, bleedDurationSeconds); }
    }

    public float BlessingHealingIncreasePercent
    {
        get { return Mathf.Max(0f, blessingHealingIncreasePercent); }
    }

    public float BlessingDurationSeconds
    {
        get { return Mathf.Max(0f, blessingDurationSeconds); }
    }

    public float FirstStrikeBonusPercent
    {
        get { return Mathf.Max(0f, firstStrikeBonusPercent); }
    }

    public float ExecuteThresholdPercent
    {
        get { return Mathf.Clamp(executeThresholdPercent, 0f, 100f); }
    }

    public float PlayerSlowPercentPerStack
    {
        get { return Mathf.Max(0f, playerSlowPercentPerStack); }
    }

    public float AmnesiaChancePerStackPercent
    {
        get { return Mathf.Max(0f, amnesiaChancePerStackPercent); }
    }

    public float EnemyHasteMoveSpeedPercentPerStack
    {
        get { return Mathf.Max(0f, enemyHasteMoveSpeedPercentPerStack); }
    }

    public float EnemyBurdenMoveSpeedPercentPerStack
    {
        get { return Mathf.Max(0f, enemyBurdenMoveSpeedPercentPerStack); }
    }

    public float LethargyAttackCooldownSecondsPerStack
    {
        get { return Mathf.Max(0f, lethargyAttackCooldownSecondsPerStack); }
    }

    public float ShieldStrengthDamageReductionPercentPerStack
    {
        get { return Mathf.Max(0f, shieldStrengthDamageReductionPercentPerStack); }
    }

    public float ShatterShieldBonusPercent
    {
        get { return Mathf.Max(0f, shatterShieldBonusPercent); }
    }

    public float RevivalHealPercent
    {
        get { return Mathf.Max(0f, revivalHealPercent); }
    }

    public float FrostbiteIceDamageTakenPercentPerStack
    {
        get { return Mathf.Max(0f, frostbiteIceDamageTakenPercentPerStack); }
    }

    public float ScorchedFireDamageTakenPercentPerStack
    {
        get { return Mathf.Max(0f, scorchedFireDamageTakenPercentPerStack); }
    }

    public float ShockedLightningDamageTakenPercentPerStack
    {
        get { return Mathf.Max(0f, shockedLightningDamageTakenPercentPerStack); }
    }

    public GameObject FreezeOnApplyEffectPrefab
    {
        get { return freezeOnApplyEffectPrefab; }
    }

    public GameObject FreezeOnDeathEffectPrefab
    {
        get { return freezeOnDeathEffectPrefab; }
    }

    public float FreezeOnApplyEffectSizeMultiplier
    {
        get { return Mathf.Max(0f, freezeOnApplyEffectSizeMultiplier); }
    }

    public float FreezeOnDeathEffectSizeMultiplier
    {
        get { return Mathf.Max(0f, freezeOnDeathEffectSizeMultiplier); }
    }

    public float ImmolationRadius
    {
        get { return Mathf.Max(0f, immolationRadius); }
    }

    public GameObject ImmolationOnApplyEffectPrefab
    {
        get { return immolationOnApplyEffectPrefab; }
    }

    public GameObject ImmolationOnDeathEffectPrefab
    {
        get { return immolationOnDeathEffectPrefab; }
    }

    public float ImmolationOnApplyEffectSizeMultiplier
    {
        get { return Mathf.Max(0f, immolationOnApplyEffectSizeMultiplier); }
    }

    public float ImmolationOnDeathEffectSizeMultiplier
    {
        get { return Mathf.Max(0f, immolationOnDeathEffectSizeMultiplier); }
    }

    public float HatredBonusPercentPerDebuffPerStack
    {
        get { return Mathf.Max(0f, hatredBonusPercentPerDebuffPerStack); }
    }

    public float FocusBonusPercentPerStack
    {
        get { return Mathf.Max(0f, focusBonusPercentPerStack); }
    }

    public float FuryBonusPercentPerStack
    {
        get { return Mathf.Max(0f, furyBonusPercentPerStack); }
    }

    public float FuryLowHealthThresholdPercent
    {
        get { return Mathf.Clamp(furyLowHealthThresholdPercent, 0f, 100f); }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }
}
