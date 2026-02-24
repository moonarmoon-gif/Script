using UnityEngine;

/// <summary>
/// Scales enemy health and EXP drop over time
/// </summary>
public class EnemyScalingSystem : MonoBehaviour
{
    public static EnemyScalingSystem Instance { get; private set; }
    
    [Header("Scaling Settings")]
    [Tooltip("Time interval in seconds between each scaling increase")]
    [SerializeField] private float scalingInterval = 10f;
    
    [Tooltip("Base HEALTH increase percentage per interval (5 = 5% increase).")]
    [SerializeField] private float healthIncreasePercent = 5f;

    [Tooltip("Extra HEALTH increase percentage added on top of HealthIncreasePercent for each successive tier (0 = flat).")]
    [SerializeField] private float additionalHealthIncreasePercent = 0f;

    [Tooltip("Additional HEALTH increase percentage that is added each time a new enemy wave is reached (EnemySpawner) or a new enemy card is chosen (EnemyCardSpawner).")]
    public float PerWaveHealthIncrease = 0f;

    [Tooltip("Optional: Per-wave HEALTH increase values for successive wave steps. Element 0 applies to the first step, element 1 to the next, etc. If steps exceed the array length, the last element repeats. When empty, the single PerWaveHealthIncrease value is used.")]
    public float[] PerWaveHealthIncreaseByWave = new float[0];

    [Tooltip("If true, apply multiplicative (compounding) scaling to HEALTH each tier (x1.05, x1.05, ...). If false, use additive scaling (x1.0, x1.05, x1.10, ...).")]
    [SerializeField] private bool useHealthMultiplicativeScaling = true;

    [Header("EXP Scaling")]
    [Tooltip("EXP drop increase percentage per interval (5 = 5% increase).")]
    [SerializeField] private float expIncreasePercent = 5f;

    [Tooltip("If true, apply multiplicative (compounding) scaling to EXP each tier; if false, apply additively.")]
    [SerializeField] private bool useExpMultiplicativeScaling = false;

    [Tooltip("Damage increase percentage per interval (5 = 5% increase) - applies to ALL enemy damage types.")]
    [SerializeField] private float damageIncreasePercent = 5f;

    [Tooltip("If true, apply multiplicative (compounding) scaling to DAMAGE each tier; if false, use additive scaling.")]
    [SerializeField] private bool useDamageMultiplicativeScaling = true;

    [Header("Spawn Interval Scaling (Enemy Cards)")]
    [Tooltip("Flat spawn interval REDUCTION (in seconds) applied per scaling tier to ALL non-boss enemy card spawns (0.1 = -0.1s per tier).")]
    [SerializeField] private float spawnIntervalDecrease = 0f;

    [Header("Post-Boss Global Spawn & Health")] 
    [Tooltip("Flat amount in seconds subtracted from ALL enemy-card spawn intervals for EACH completed boss event (applied after the boss event ends). 0 = disabled.")]
    public float GlobalEnemySpawnerIntervalReduction = 0.2f;

    [Tooltip("Extra health-scaling steps applied IMMEDIATELY after each completed boss event. Each step uses (healthIncreasePercent + the currently stacked additionalHealthIncreasePercent) as the effective percent.")]
    public int BonusHealthIncreaseAfterBoss = 0;
    
    // Accumulated base multipliers across all completed boss phases. These
    // represent the "rebased" stats after each boss event and form the
    // starting point for subsequent scaling tiers.
    private float baseHealthMultiplier = 1f;
    private float baseExpMultiplier = 1f;
    private float baseDamageMultiplier = 1f;

    // Current scaling multipliers for the active phase (since the last boss
    // rebase). These are reset to 1.0 whenever SnapshotCurrentScalingForBossRebase
    // is called.
    private float currentHealthMultiplier = 1f;
    private float currentExpMultiplier = 1f;
    private float currentDamageMultiplier = 1f;

    // Total flat spawn interval reduction (in seconds) accumulated from
    // scaling tiers for enemy card spawns. This is applied equally to all
    // non-boss enemy cards within the current phase and is reset after each
    // boss rebase / phase transition.
    private float currentSpawnIntervalFlatReduction = 0f;

    // Number of completed boss events this run (used to stack the global flat
    // spawn-interval reduction when GlobalEnemySpawnerIntervalReduction > 0).
    private int completedBossEvents = 0;

    private int perWaveHealthIncreaseSteps = 0;
    
    private float timeSinceLastScale = 0f;
    private int scalingTier = 0;
    
    // Boss event pause
    private bool isPaused = false;
    
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
    
    private void Update()
    {
        // Don't scale during boss events
        if (isPaused) return;

        timeSinceLastScale += GameStateManager.GetPauseSafeDeltaTime();
        
        if (timeSinceLastScale >= scalingInterval)
        {
            timeSinceLastScale = 0f;
            scalingTier++;
            
            // Increase multipliers by the percentage for this tier.
            // Health uses a ramping step: base + additional * tierIndex.
            int tierIndex = Mathf.Max(0, scalingTier - 1);
            float phaseAdditionalHealthPercent = GetAdditionalHealthIncreasePercentForCurrentScaling();
            float effectiveHealthPercent = healthIncreasePercent + phaseAdditionalHealthPercent * tierIndex;
            float healthStep = effectiveHealthPercent / 100f;
            float expStep = expIncreasePercent / 100f;
            float damageStep = damageIncreasePercent / 100f;

            // HEALTH
            if (useHealthMultiplicativeScaling)
            {
                currentHealthMultiplier *= (1f + healthStep);
            }
            else
            {
                currentHealthMultiplier += healthStep;
            }

            // DAMAGE
            if (useDamageMultiplicativeScaling)
            {
                currentDamageMultiplier *= (1f + damageStep);
            }
            else
            {
                currentDamageMultiplier += damageStep;
            }

            // EXP
            if (useExpMultiplicativeScaling)
            {
                currentExpMultiplier *= (1f + expStep);
            }
            else
            {
                currentExpMultiplier += expStep;
            }

            // Apply FLAT spawn interval reduction for enemy card spawns
            // (non-boss only). Example: spawnIntervalDecrease = 0.1 means each
            // tier subtracts 0.1 seconds from the base interval for all
            // non-boss enemy cards, up to the per-card minimums enforced in
            // EnemyCardSpawner.
            if (spawnIntervalDecrease > 0f)
            {
                currentSpawnIntervalFlatReduction += Mathf.Max(0f, spawnIntervalDecrease);
            }

            float totalHealthMult = GetHealthMultiplier();
            float totalExpMult = GetExpMultiplier();
            float totalDamageMult = GetDamageMultiplier();
            Debug.Log($"<color=cyan>Scaling Tier {scalingTier}: Health x{totalHealthMult:F2}, EXP x{totalExpMult:F2}, Damage x{totalDamageMult:F2}, SpawnInterval FlatReduction {currentSpawnIntervalFlatReduction:F3}s</color>");
        }
    }
    
    /// <summary>
    /// Pause scaling (for boss events)
    /// </summary>
    public void PauseScaling()
    {
        isPaused = true;
        Debug.Log("<color=yellow>Enemy Scaling PAUSED</color>");
    }
    
    /// <summary>
    /// Resume scaling (after boss events)
    /// </summary>
    public void ResumeScaling()
    {
        isPaused = false;
        Debug.Log("<color=lime>Enemy Scaling RESUMED</color>");
    }
    
    /// <summary>
    /// Get the current health multiplier for enemies (includes all phases).
    /// </summary>
    public float GetHealthMultiplier()
    {
        return baseHealthMultiplier * currentHealthMultiplier;
    }
    
    /// <summary>
    /// Get the current EXP multiplier for enemies (includes all phases).
    /// </summary>
    public float GetExpMultiplier()
    {
        return baseExpMultiplier * currentExpMultiplier;
    }
    
    /// <summary>
    /// Get the current damage multiplier for enemies (applies to ALL damage types, includes all phases).
    /// </summary>
    public float GetDamageMultiplier()
    {
        return baseDamageMultiplier * currentDamageMultiplier;
    }
    
    /// <summary>
    /// Get the current scaling tier
    /// </summary>
    public int GetScalingTier()
    {
        return scalingTier;
    }

    /// <summary>
    /// Snapshot (rebase) the current scaling multipliers so external systems can
    /// treat the last-scaled health/EXP/damage values as the new "base" after a
    /// boss event. This folds the current phase multipliers into the persistent
    /// base multipliers and resets the phase multipliers back to 1.0. The global
    /// spawn-interval reduction stack is also reset so enemy-card spawn
    /// intervals start fresh for the next phase.
    /// </summary>
    public void SnapshotCurrentScalingForBossRebase()
    {
        // Compute the total multipliers at this moment (all phases + current phase)
        float totalHealthMult = GetHealthMultiplier();
        float totalExpMult = GetExpMultiplier();
        float totalDamageMult = GetDamageMultiplier();

        // Treat these totals as the new "base" going forward.
        baseHealthMultiplier = Mathf.Max(0.0001f, totalHealthMult);
        baseExpMultiplier = Mathf.Max(0.0001f, totalExpMult);
        baseDamageMultiplier = Mathf.Max(0.0001f, totalDamageMult);

        // Reset current phase multipliers so subsequent tiers start from x1.0
        // relative to this new base.
        currentHealthMultiplier = 1f;
        currentExpMultiplier = 1f;
        currentDamageMultiplier = 1f;

        // Reset the spawn-interval flat reduction so that enemy-card spawn
        // intervals effectively start fresh for the next phase.
        currentSpawnIntervalFlatReduction = 0f;

        Debug.Log($"<color=yellow>EnemyScalingSystem: Boss rebase applied. New base multipliers - Health x{baseHealthMultiplier:F2}, EXP x{baseExpMultiplier:F2}, Damage x{baseDamageMultiplier:F2}. Spawn interval multiplier reset to 1.0.</color>");
    }

    public void ResetSpawnIntervalMultiplierForNextPhase()
    {
        currentSpawnIntervalFlatReduction = 0f;
        Debug.Log("<color=yellow>EnemyScalingSystem: Boss event complete. Spawn interval flat reduction reset to 0.0s.</color>");
    }

    /// <summary>
    /// Notify the scaling system that a boss event has fully completed. This
    /// increments the internal boss counter used for the global
    /// GlobalEnemySpawnerIntervalReduction flat reduction so that all enemy-card
    /// spawn intervals become faster for subsequent phases.
    /// </summary>
    public void OnBossEventCompleted()
    {
        if (GlobalEnemySpawnerIntervalReduction <= 0f)
        {
            return;
        }

        completedBossEvents = Mathf.Max(0, completedBossEvents + 1);
        Debug.Log($"<color=yellow>EnemyScalingSystem: Boss event completed. Global flat spawn interval reduction active: {GetGlobalSpawnIntervalFlatReductionTotal():F2}s</color>");
    }

    public void RegisterPerWaveHealthIncreaseStep()
    {
        perWaveHealthIncreaseSteps = Mathf.Max(0, perWaveHealthIncreaseSteps + 1);
    }
    
    /// <summary>
    /// Reset scaling (for new game or testing)
    /// </summary>
    public void ResetScaling()
    {
        baseHealthMultiplier = 1f;
        baseExpMultiplier = 1f;
        baseDamageMultiplier = 1f;
        currentHealthMultiplier = 1f;
        currentExpMultiplier = 1f;
        currentDamageMultiplier = 1f;
        currentSpawnIntervalFlatReduction = 0f;
        completedBossEvents = 0;
        perWaveHealthIncreaseSteps = 0;
        isPaused = false;
        scalingTier = 0;
        timeSinceLastScale = 0f;
        Debug.Log("<color=yellow>Enemy scaling reset to tier 0</color>");
    }
    
    /// <summary>
    /// Get time until next scaling tier
    /// </summary>
    public float GetTimeUntilNextScale()
    {
        return scalingInterval - timeSinceLastScale;
    }

    /// <summary>
    /// Get the current TOTAL FLAT spawn interval reduction for enemy card
    /// spawns, accumulated from scaling tiers within the current phase.
    /// </summary>
    public float GetSpawnIntervalDecreaseTotal()
    {
        return currentSpawnIntervalFlatReduction;
    }

    /// <summary>
    /// Total flat amount (in seconds) that should be subtracted from ALL
    /// enemy-card spawn intervals based on the number of completed boss events
    /// and the configured GlobalEnemySpawnerIntervalReduction value.
    /// </summary>
    public float GetGlobalSpawnIntervalFlatReductionTotal()
    {
        if (GlobalEnemySpawnerIntervalReduction <= 0f || completedBossEvents <= 0)
        {
            return 0f;
        }

        return GlobalEnemySpawnerIntervalReduction * completedBossEvents;
    }

    /// <summary>
    /// Apply an additional health-scaling boost immediately after a boss event
    /// using the same effective healthIncreasePercent + stacked
    /// additionalHealthIncreasePercent that a regular scaling tier would use.
    /// The boost is applied BonusHealthIncreaseAfterBoss times and does NOT
    /// reset or alter the existing scalingTier; it simply multiplies the
    /// currentHealthMultiplier further.
    /// </summary>
    public void ApplyBonusHealthIncreaseAfterBoss()
    {
        if (BonusHealthIncreaseAfterBoss <= 0)
        {
            return;
        }

        int tierIndex = Mathf.Max(0, scalingTier - 1);
        float phaseAdditionalHealthPercent = GetAdditionalHealthIncreasePercentForCurrentScaling();
        float effectiveHealthPercent = healthIncreasePercent + phaseAdditionalHealthPercent * tierIndex;
        float healthStep = effectiveHealthPercent / 100f;

        if (healthStep <= 0f)
        {
            return;
        }

        if (useHealthMultiplicativeScaling)
        {
            float totalMult = Mathf.Pow(1f + healthStep, BonusHealthIncreaseAfterBoss);
            currentHealthMultiplier *= totalMult;
        }
        else
        {
            currentHealthMultiplier += healthStep * BonusHealthIncreaseAfterBoss;
        }

        Debug.Log($"<color=yellow>EnemyScalingSystem: Applied BonusHealthIncreaseAfterBoss={BonusHealthIncreaseAfterBoss} using effectiveHealthPercent={effectiveHealthPercent:F2}% (tierIndex={tierIndex}). New total HealthMult={GetHealthMultiplier():F2}</color>");
    }

    private float GetAdditionalHealthIncreasePercentForCurrentScaling()
    {
        float basePercent = additionalHealthIncreasePercent;
        float perWavePercent = 0f;
        int steps = Mathf.Max(0, perWaveHealthIncreaseSteps);
        if (PerWaveHealthIncreaseByWave != null && PerWaveHealthIncreaseByWave.Length > 0)
        {
            int lastIndex = PerWaveHealthIncreaseByWave.Length - 1;
            for (int i = 0; i < steps; i++)
            {
                int index = Mathf.Min(i, lastIndex);
                perWavePercent += Mathf.Max(0f, PerWaveHealthIncreaseByWave[index]);
            }
        }
        else
        {
            perWavePercent = Mathf.Max(0f, PerWaveHealthIncrease) * steps;
        }
        return basePercent + perWavePercent;
    }
}
