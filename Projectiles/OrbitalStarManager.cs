using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages spawning and level progression for NovaStar and DwarfStar orbital projectiles
/// BASE VERSION: Downtime-based spawning (downtime starts AFTER orbit completes/goes offscreen)
///   - NovaStar: Orbits odd levels only (1, 3, 5)
///   - DwarfStar: Orbits even levels only (2, 4, 6)
///   - Complete independence when sync disabled
/// ENHANCED VERSION: Both stars spawn at all odd levels, then all even levels, synchronized
///   - Odd levels (1,3,5) → Even levels (2,4,6) → repeat
///   - All orbits complete at same time using speed calculation
/// </summary>
public class OrbitalStarManager : MonoBehaviour
{
    [Header("Star Prefabs")]
    [Tooltip("NovaStar prefab (Fire type, clockwise)")]
    public GameObject novaStarPrefab;

    [Tooltip("DwarfStar prefab (Ice type, counterclockwise)")]
    public GameObject dwarfStarPrefab;

    [Header("Spawn Settings")]
    [Tooltip("Number of stars to spawn per level")]
    public int starsPerLevel = 3;

    [Tooltip("Gap between each star (angular spacing in degrees)")]
    public float starAngularSpacing = 30f;

    [Header("Orbital Radii")]
    [Tooltip("Radius for Level 1 orbit (smallest)")]
    public float level1Radius = 3f;

    [Tooltip("Radius for Level 2 orbit")]
    public float level2Radius = 6f;

    [Tooltip("Radius for Level 3 orbit")]
    public float level3Radius = 9f;

    [Tooltip("Radius for Level 4 orbit")]
    public float level4Radius = 12f;

    [Tooltip("Radius for Level 5 orbit")]
    public float level5Radius = 15f;

    [Tooltip("Radius for Level 6 orbit (largest)")]
    public float level6Radius = 18f;

    [Header("Downtime System")]
    [Tooltip("Downtime after Level 1 orbit completes (seconds)")]
    public float level1Downtime = 2f;

    [Tooltip("Downtime after Level 2 orbit completes (seconds)")]
    public float level2Downtime = 3f;

    [Tooltip("Downtime after Level 3 orbit completes (seconds)")]
    public float level3Downtime = 4f;

    [Tooltip("Downtime after Level 4 orbit completes (seconds)")]
    public float level4Downtime = 5f;

    [Tooltip("Downtime after Level 5 orbit completes (seconds)")]
    public float level5Downtime = 6f;

    [Tooltip("Downtime after Level 6 orbit completes (seconds)")]
    public float level6Downtime = 7f;

    [Header("Enhanced Sync Downtime")]
    [Tooltip("Downtime after odd levels complete in enhanced mode")]
    public float enhancedOddDowntime = 5f;

    [Tooltip("Downtime after even levels complete in enhanced mode")]
    public float enhancedEvenDowntime = 5f;
    
    [Header("Synchronized Spawning Mode")]
    [Tooltip("When enabled: Spawn one star at each level 1-6 sequentially (not odd/even batches). Works for single enhanced star or both synchronized.")]
    public bool sequential = false;
    
    [Tooltip("When enabled: Reverse sequential mode - spawn from level 6 down to level 1 (opposite of sequential)")]
    public bool reverseSequential = false;
    
    [Tooltip("Loop back after completing all levels (for sequential/reverse modes)")]
    public bool loopLevels = true;
    
    [Header("Reverse Sequential Speed")]
    [Tooltip("Base orbit speed for Level 6 in reverse sequential mode (lower levels will have higher speeds)")]
    public float reverseBaseOrbitSpeed = 2f;
    
    [Tooltip("Time divisor for Level 1 compared to Level 6. If Level 6 takes 30s, and divisor is 3, Level 1 will take 10s (30/3). Higher = faster Level 1.")]
    public float reverseSpeedDivisor = 3f;

    [Header("Orbit Path Adjustment - Per Level")]
    [Tooltip("Level 1: How far below camera the orbit extends (degrees). Lower = shorter path. Range: 0-60°")]
    [Range(0f, 60f)]
    public float level1OrbitBelowCamera = 30f;
    
    [Tooltip("Level 2: How far below camera the orbit extends (degrees). Lower = shorter path. Range: 0-60°")]
    [Range(0f, 60f)]
    public float level2OrbitBelowCamera = 30f;
    
    [Tooltip("Level 3: How far below camera the orbit extends (degrees). Lower = shorter path. Range: 0-60°")]
    [Range(0f, 60f)]
    public float level3OrbitBelowCamera = 30f;
    
    [Tooltip("Level 4: How far below camera the orbit extends (degrees). Lower = shorter path. Range: 0-60°")]
    [Range(0f, 60f)]
    public float level4OrbitBelowCamera = 30f;
    
    [Tooltip("Level 5: How far below camera the orbit extends (degrees). Lower = shorter path. Range: 0-60°")]
    [Range(0f, 60f)]
    public float level5OrbitBelowCamera = 30f;
    
    [Tooltip("Level 6: How far below camera the orbit extends (degrees). Lower = shorter path. Range: 0-60°")]
    [Range(0f, 60f)]
    public float level6OrbitBelowCamera = 30f;
    
    [Header("Orbit Path Global Offset")]
    [Tooltip("Extra degrees below the camera applied to ALL levels. Use this to align orbit guide lines visually.")]
    public float orbitBelowCameraGlobalOffset = 0f;

    [Header("Radius Indicator Fade-In (Y Axis)")]
    [Tooltip("Y world position at which NovaStar/DwarfStar RadiusIndicator sprites are fully opaque. Below this, they fade out progressively the lower they go.")]
    public float yAxisRadiusFadeIn = -5f;

    [Header("Orbit Tilt Per Level (Right Down / Left Up)")]
    [Tooltip("Level 1: Positive = right side lower, left side higher. Negative = right higher, left lower.")]
    public float level1OrbitTilt = 0f;
    [Tooltip("Level 2: Positive = right side lower, left side higher. Negative = right higher, left lower.")]
    public float level2OrbitTilt = 0f;
    [Tooltip("Level 3: Positive = right side lower, left side higher. Negative = right higher, left lower.")]
    public float level3OrbitTilt = 0f;
    [Tooltip("Level 4: Positive = right side lower, left side higher. Negative = right higher, left lower.")]
    public float level4OrbitTilt = 0f;
    [Tooltip("Level 5: Positive = right side lower, left side higher. Negative = right higher, left lower.")]
    public float level5OrbitTilt = 0f;
    [Tooltip("Level 6: Positive = right side lower, left side higher. Negative = right higher, left lower.")]
    public float level6OrbitTilt = 0f;

    [Header("Synchronization Settings")]
    [Tooltip("Enable enhanced synchronization: When level requirement met, both stars spawn at all odd/even levels together")]
    public bool synchronizeSpawns = false;
    
    [Tooltip("Delay between each level spawn in enhanced mode (seconds)")]
    public float enhancedLevelSpawnDelay = 2f;
    
    [Tooltip("When enabled, all levels finish orbit at same time. When disabled, each level finishes at base time + accumulated delays")]
    public bool synchronizeOrbitCompletion = true;

    [Header("References")]
    [Tooltip("Player transform (center of orbit)")]
    public Transform playerTransform;

    // Level progression (base version)
    private int novaStarCurrentLevel = 1;  // Odd levels: 1, 3, 5
    private int dwarfStarCurrentLevel = 2; // Even levels: 2, 4, 6

    // Enhanced version tracking - SEPARATE for each star!
    private bool isNovaStarEnhanced = false;
    private bool isDwarfStarEnhanced = false;
    private int novaVariantIndex = 0;
    private int dwarfVariantIndex = 0;
    // Per-star sequential / reverseSequential modes derived from CURRENT
    // variant indices. These indicate what the star "wants" to do based on
    // the latest card selection.
    private bool novaSequential = false;
    private bool novaReverseSequential = false;
    private bool dwarfSequential = false;
    private bool dwarfReverseSequential = false;

    // Variant stacking tracking: once a variant has ever been chosen for a
    // star, its stack flag stays true for the rest of the run. These do NOT
    // affect behaviour yet; they are internal bookkeeping that future logic
    // can use to decide how many independent stacks exist.
    private bool novaHasVariant1Stack = false; // NV1 (sequential)
    private bool novaHasVariant2Stack = false; // NV2 (reverse sequential)
    private bool dwarfHasVariant1Stack = false; // DV1 (sequential)
    private bool dwarfHasVariant2Stack = false; // DV2 (reverse sequential)
    private bool novaStarSpawnOddLevels = true; // NovaStar: True = spawn odd (1,3,5), False = spawn even (2,4,6)
    private bool dwarfStarSpawnOddLevels = true; // DwarfStar: True = spawn odd (1,3,5), False = spawn even (2,4,6)
    
    // Sequential level tracking (for sequential/reverseSequential modes)
    private int currentSequentialLevel = 1; // Tracks current level in 1-6 sequence
    private int currentReverseSequentialLevel = 6; // Tracks current level in 6-1 sequence
    private bool novaWasInSyncMode = false; // Track if NovaStar was in sync mode last frame
    private bool dwarfWasInSyncMode = false; // Track if DwarfStar was in sync mode last frame

    // Downtime tracking
    private bool novaStarWaitingForDowntime = false;
    private float novaStarNextSpawnTime = 0f;
    private bool dwarfStarWaitingForDowntime = false;
    private float dwarfStarNextSpawnTime = 0f;
    
    // Full cycle tracking (for enhanced transition)
    // Tracks if we're in middle of spawning a full cycle (1,3,5 or 2,4,6)
    private bool novaStarInFullCycle = false;
    private bool dwarfStarInFullCycle = false;

    // Enhanced mode: Track completion counts
    private int novaStarsActiveCount = 0;
    private int novaStarsCompletedCount = 0;
    private int dwarfStarsActiveCount = 0;
    private int dwarfStarsCompletedCount = 0;

    // Coroutine tracking
    private Coroutine novaStarSpawnCoroutine;
    private Coroutine dwarfStarSpawnCoroutine;

    // Tracking
    private bool bothStarsActive = false;
    
    // Track if synchronized enhanced mode is currently active
    private bool syncModeActive = false;

    // MEGASYNC: when BOTH stars are enhanced and have EVER chosen BOTH Variant 1
    // and Variant 2 (per-card history), and synchronizeSpawns is enabled, we
    // promote into a special mode where FOUR orbits are spawned per step:
    //   - Nova V1  (sequential track 1→2→3)
    //   - Nova V2  (reverse track 6→5→4)
    //   - Dwarf V1 (sequential track 1→2→3)
    //   - Dwarf V2 (reverse track 6→5→4)
    // All four share the same combined modifiers via GetEffectiveStarModifiers.
    private bool megaSyncActive = false;
    // Tracks whether MEGASYNC has already performed its one-time initialization
    // (clearing existing orbits and resetting level trackers). This is separate
    // from novaWasInSyncMode so that entering MEGASYNC from other sync modes
    // always starts at the canonical 1→2→3 and 6→5→4 levels.
    private bool megaSyncInitialized = false;

    // Boss-event guard: when true, all star spawn cycles are paused so
    // NovaStar/DwarfStar cannot spawn while boss cards are shown or the
    // menace timer is active.
    private bool bossEventActive = false;
    
    // Track previous enhancement states to detect changes
    private bool previousNovaEnhanced = false;
    private bool previousDwarfEnhanced = false;

    // Safety timeout (in seconds) to prevent spawn cycles from stalling forever
    // if, for any reason, active/completed counts get out of sync (e.g. projectiles
    // destroyed externally during boss events).
    private const float enhancedStallTimeout = 10f;

    private void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                Debug.LogError("OrbitalStarManager: Player not found!");
                return;
            }
        }
        
        // Initialize previous enhancement states to current states
        CheckEnhancedMode();
        previousNovaEnhanced = isNovaStarEnhanced;
        previousDwarfEnhanced = isDwarfStarEnhanced;
    }
    
    private void Update()
    {
        // CRITICAL: Don't run Update logic if coroutines haven't started yet
        // This prevents duplicate coroutines on first enhancement
        if (novaStarSpawnCoroutine == null && dwarfStarSpawnCoroutine == null)
        {
            return; // Wait for StartNovaStarCycle() or StartDwarfStarCycle() to be called
        }
        
        // Check for enhancement changes every frame
        CheckEnhancedMode();
        
        // NovaStar enhancement changed
        if (isNovaStarEnhanced != previousNovaEnhanced)
        {
            Debug.Log($"<color=gold>★ NovaStar enhancement changed: {previousNovaEnhanced} → {isNovaStarEnhanced}</color>");
            previousNovaEnhanced = isNovaStarEnhanced;
            
            // The base coroutine will detect this change and switch to enhanced automatically
            // It only switches when NOT waiting for downtime (i.e., after orbit completes)
            Debug.Log($"<color=yellow>NovaStar will transition to enhanced after current orbit completes</color>");
        }
        
        // DwarfStar enhancement changed
        if (isDwarfStarEnhanced != previousDwarfEnhanced)
        {
            Debug.Log($"<color=cyan>★ DwarfStar enhancement changed: {previousDwarfEnhanced} → {isDwarfStarEnhanced}</color>");
            previousDwarfEnhanced = isDwarfStarEnhanced;
            
            // The base coroutine will detect this change and switch to enhanced automatically
            // It only switches when NOT waiting for downtime (i.e., after orbit completes)
            Debug.Log($"<color=yellow>DwarfStar will transition to enhanced after current orbit completes</color>");
        }
        
        // MEGASYNC state is derived from history flags and enhancement status. This
        // is intentionally independent of the CURRENT variant index so that once a
        // star has unlocked both Variant 1 and Variant 2, its two tracks can be
        // considered permanently available for MEGASYNC purposes.
        megaSyncActive = ShouldMegaSync();
        if (!megaSyncActive)
        {
            // As soon as MEGASYNC is no longer active, allow its initialization
            // to run again the next time it becomes active.
            megaSyncInitialized = false;
        }

        // Handle transition into synchronized enhanced mode. As soon as both stars are
        // enhanced, synchronizeSpawns is enabled, and both coroutines are active,
        // we mark syncModeActive so the enhanced cycles can coordinate, but we let
        // any existing orbits finish naturally instead of force-destroying them.
        bool shouldSyncNow = ShouldSynchronize();
        if (shouldSyncNow && !syncModeActive)
        {
            syncModeActive = true;
        }
        else if (!shouldSyncNow && syncModeActive)
        {
            syncModeActive = false;
        }
    }
    
    /// <summary>
    /// Switch NovaStar to new mode (base or enhanced)
    /// </summary>
    private void SwitchNovaStarMode(bool enhanced)
    {
        // Reset counters
        novaStarsActiveCount = 0;
        novaStarsCompletedCount = 0;
        
        // Stop old coroutine
        if (novaStarSpawnCoroutine != null)
        {
            StopCoroutine(novaStarSpawnCoroutine);
            novaStarSpawnCoroutine = null;
        }
        
        // Start new coroutine
        if (enhanced)
        {
            novaStarSpawnCoroutine = StartCoroutine(NovaStarCycleEnhanced());
            Debug.Log($"<color=gold>NovaStar SWITCHED TO ENHANCED cycle!</color>");
        }
        else
        {
            novaStarCurrentLevel = 1;
            novaStarNextSpawnTime = Time.time;
            novaStarWaitingForDowntime = false;
            novaStarSpawnCoroutine = StartCoroutine(NovaStarCycleBase());
            Debug.Log($"<color=orange>NovaStar SWITCHED TO BASE cycle!</color>");
        }
    }
    
    /// <summary>
    /// Switch DwarfStar to new mode (base or enhanced)
    /// </summary>
    private void SwitchDwarfStarMode(bool enhanced)
    {
        // Reset counters
        dwarfStarsActiveCount = 0;
        dwarfStarsCompletedCount = 0;
        
        // Stop old coroutine
        if (dwarfStarSpawnCoroutine != null)
        {
            StopCoroutine(dwarfStarSpawnCoroutine);
            dwarfStarSpawnCoroutine = null;
        }
        
        // Start new coroutine
        if (enhanced)
        {
            dwarfStarSpawnCoroutine = StartCoroutine(DwarfStarCycleEnhanced());
            Debug.Log($"<color=gold>DwarfStar SWITCHED TO ENHANCED cycle!</color>");
        }
        else
        {
            dwarfStarCurrentLevel = 2;
            dwarfStarNextSpawnTime = Time.time;
            dwarfStarWaitingForDowntime = false;
            dwarfStarSpawnCoroutine = StartCoroutine(DwarfStarCycleBase());
            Debug.Log($"<color=cyan>DwarfStar SWITCHED TO BASE cycle!</color>");
        }
    }

    /// <summary>
    /// Start the NovaStar spawn cycle
    /// </summary>
    public void StartNovaStarCycle()
    {
        if (novaStarPrefab == null)
        {
            Debug.LogWarning("OrbitalStarManager: NovaStarPrefab is not assigned!");
            return;
        }

        if (novaStarSpawnCoroutine != null)
        {
            Debug.Log($"<color=orange>NovaStar cycle already running</color>");
            return;
        }

        // Check if enhanced mode should activate
        CheckEnhancedMode();

        // Start appropriate cycle based on THIS star's enhancement status ONLY
        // Synchronization is handled INSIDE the enhanced cycles
        if (isNovaStarEnhanced)
        {
            novaStarSpawnCoroutine = StartCoroutine(NovaStarCycleEnhanced());
            Debug.Log($"<color=gold>NovaStar ENHANCED cycle started!</color>");
        }
        else
        {
            novaStarCurrentLevel = 1;
            novaStarNextSpawnTime = Time.time;
            novaStarWaitingForDowntime = false;
            novaStarSpawnCoroutine = StartCoroutine(NovaStarCycleBase());
            Debug.Log($"<color=orange>NovaStar BASE cycle started (odd levels only)</color>");
        }
    }

    /// <summary>
    /// Start the DwarfStar spawn cycle
    /// </summary>
    public void StartDwarfStarCycle()
    {
        if (dwarfStarPrefab == null)
        {
            Debug.LogWarning("OrbitalStarManager: DwarfStarPrefab is not assigned!");
            return;
        }

        if (dwarfStarSpawnCoroutine != null)
        {
            Debug.Log($"<color=cyan>DwarfStar cycle already running</color>");
            return;
        }

        // Check if enhanced mode should activate
        CheckEnhancedMode();

        // Start appropriate cycle based on THIS star's enhancement status ONLY
        // Synchronization is handled INSIDE the enhanced cycles
        if (isDwarfStarEnhanced)
        {
            dwarfStarSpawnCoroutine = StartCoroutine(DwarfStarCycleEnhanced());
            Debug.Log($"<color=gold>DwarfStar ENHANCED cycle started!</color>");
        }
        else
        {
            dwarfStarCurrentLevel = 2;
            dwarfStarNextSpawnTime = Time.time;
            dwarfStarWaitingForDowntime = false;
            dwarfStarSpawnCoroutine = StartCoroutine(DwarfStarCycleBase());
            Debug.Log($"<color=cyan>DwarfStar BASE cycle started (even levels only)</color>");
        }
    }

    /// <summary>
    /// Restart all active NovaStar/DwarfStar cycles.
    /// Used after boss events that clear projectiles so orbital stars can resume spawning.
    /// </summary>
    public void RestartAllStarCycles()
    {
        Debug.Log("<color=magenta>OrbitalStarManager: RestartAllStarCycles called (boss event reset)</color>");

        // CRITICAL: Destroy any existing NovaStar/DwarfStar instances before restarting
        // their cycles. If a star survived the boss projectile clear (e.g., due to
        // timing or being off-screen), keeping it while also starting a fresh cycle
        // would result in two independent star systems running at once.
        NovaStar[] existingNovaStars = FindObjectsOfType<NovaStar>();
        foreach (var star in existingNovaStars)
        {
            if (star != null)
            {
                Destroy(star.gameObject);
            }
        }

        DwarfStar[] existingDwarfStars = FindObjectsOfType<DwarfStar>();
        foreach (var star in existingDwarfStars)
        {
            if (star != null)
            {
                Destroy(star.gameObject);
            }
        }

        // Restart NovaStar cycle if it was running
        if (novaStarSpawnCoroutine != null)
        {
            StopCoroutine(novaStarSpawnCoroutine);
            novaStarSpawnCoroutine = null;

            novaStarWaitingForDowntime = false;
            novaStarsActiveCount = 0;
            novaStarsCompletedCount = 0;

            StartNovaStarCycle();
        }

        // Restart DwarfStar cycle if it was running
        if (dwarfStarSpawnCoroutine != null)
        {
            StopCoroutine(dwarfStarSpawnCoroutine);
            dwarfStarSpawnCoroutine = null;

            dwarfStarWaitingForDowntime = false;
            dwarfStarsActiveCount = 0;
            dwarfStarsCompletedCount = 0;

            StartDwarfStarCycle();
        }
    }

    /// <summary>
    /// Called by EnemyCardSpawner when a boss event starts. Pauses all orbital
    /// star spawning so no new NovaStar/DwarfStar instances appear while boss
    /// cards are shown.
    /// </summary>
    public void OnBossEventStart()
    {
        bossEventActive = true;
    }

    /// <summary>
    /// Called by EnemyCardSpawner when a boss event ends (after menace timer
    /// and projectile reset). Allows orbital star spawning to resume.
    /// </summary>
    public void OnBossEventEnd()
    {
        bossEventActive = false;
    }

    /// <summary>
    /// Get orbit extension below camera for a specific level (CENTER value).
    /// This is the average extension; per-side tilt is applied inside the
    /// star scripts and gizmos using the orbit tilt values.
    /// </summary>
    public float GetOrbitExtensionForLevel(int level)
    {
        float baseExtension;
        switch (level)
        {
            case 1: baseExtension = level1OrbitBelowCamera; break;
            case 2: baseExtension = level2OrbitBelowCamera; break;
            case 3: baseExtension = level3OrbitBelowCamera; break;
            case 4: baseExtension = level4OrbitBelowCamera; break;
            case 5: baseExtension = level5OrbitBelowCamera; break;
            case 6: baseExtension = level6OrbitBelowCamera; break;
            default: baseExtension = 30f; break; // Fallback
        }

        return baseExtension + orbitBelowCameraGlobalOffset;
    }

    /// <summary>
    /// Get orbit tilt for a specific level.
    /// Positive = right side lower, left side higher. Negative = opposite.
    /// </summary>
    public float GetOrbitTiltForLevel(int level)
    {
        switch (level)
        {
            case 1: return level1OrbitTilt;
            case 2: return level2OrbitTilt;
            case 3: return level3OrbitTilt;
            case 4: return level4OrbitTilt;
            case 5: return level5OrbitTilt;
            case 6: return level6OrbitTilt;
            default: return 0f;
        }
    }

    /// <summary>
    /// Check if synchronization should be active.
    /// Returns true ONLY if:
    ///   - synchronizeSpawns is enabled
    ///   - both stars are enhanced
    ///   - both stars are using the SAME non-zero enhanced variant
    ///   - both spawn coroutines are active
    ///
    /// Note: Mixed-variant setups (one star using Variant 1, the other Variant 2)
    /// are automatically excluded by the "same non-zero variant" requirement, so
    /// we no longer need to special-case the sequential+reverseSequential
    /// combination here. This allows sequential and reverseSequential modes to be
    /// active simultaneously across the two stars without disabling sync for the
    /// valid same-variant cases.
    /// </summary>
    private bool ShouldSynchronize()
    {
        if (!synchronizeSpawns) return false;

        // Both stars must be enhanced
        if (!isNovaStarEnhanced || !isDwarfStarEnhanced) return false;

        // Synchronization is only permitted during MEGASYNC.
        if (!megaSyncActive) return false;

        if (novaStarSpawnCoroutine == null || dwarfStarSpawnCoroutine == null) return false;
        return true;
    }

    /// <summary>
    /// Determine if MEGASYNC should be active. This requires:
    ///   - synchronizeSpawns enabled
    ///   - BOTH stars enhanced
    ///   - NovaStar has EVER chosen Variant 1 and Variant 2
    ///   - DwarfStar has EVER chosen Variant 1 and Variant 2
    /// Once these conditions are met, MEGASYNC stays active even if the
    /// currently selected enhanced variant index changes later.
    /// </summary>
    private bool ShouldMegaSync()
    {
        if (!synchronizeSpawns) return false;
        if (!isNovaStarEnhanced || !isDwarfStarEnhanced) return false;

        if (!novaHasVariant1Stack || !novaHasVariant2Stack) return false;
        if (!dwarfHasVariant1Stack || !dwarfHasVariant2Stack) return false;

        return true;
    }
    
    /// <summary>
    /// Check if enhanced mode should be activated.
    /// Each star's enhancement is checked independently regardless of synchronizeSpawns.
    /// Also derives the global sequential / reverseSequential mode flags from the
    /// chosen variants.
    /// </summary>
    private void CheckEnhancedMode()
    {
        novaVariantIndex = 0;
        dwarfVariantIndex = 0;

        // Check EACH star's enhancement status SEPARATELY
        if (novaStarPrefab != null && ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCards novaCard = GetCardForPrefab(novaStarPrefab);
            if (novaCard != null)
            {
                int variant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(novaCard);
                novaVariantIndex = variant;
                isNovaStarEnhanced = (variant >= 1);
                // Once a variant has ever been chosen for NovaStar, mark its stack
                // flag so future logic can treat it as an active stack even if the
                // current variant index later changes.
                if (variant == 1)
                {
                    novaHasVariant1Stack = true;
                }
                else if (variant == 2)
                {
                    novaHasVariant2Stack = true;
                }
                Debug.Log($"<color=orange>NovaStar Enhanced Status: {isNovaStarEnhanced} (Variant {variant})</color>");
            }
        }

        if (dwarfStarPrefab != null && ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCards dwarfCard = GetCardForPrefab(dwarfStarPrefab);
            if (dwarfCard != null)
            {
                int variant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(dwarfCard);
                dwarfVariantIndex = variant;
                isDwarfStarEnhanced = (variant >= 1);
                // DwarfStar variant stacks mirror NovaStar behaviour: once a
                // variant has been selected, its stack flag stays true.
                if (variant == 1)
                {
                    dwarfHasVariant1Stack = true;
                }
                else if (variant == 2)
                {
                    dwarfHasVariant2Stack = true;
                }
                Debug.Log($"<color=cyan>DwarfStar Enhanced Status: {isDwarfStarEnhanced} (Variant {variant})</color>");
            }
        }

        // Per-star sequential / reverseSequential modes based on variants
        novaSequential = (novaVariantIndex == 1);
        novaReverseSequential = (novaVariantIndex == 2);
        dwarfSequential = (dwarfVariantIndex == 1);
        dwarfReverseSequential = (dwarfVariantIndex == 2);

        // Derive global sequential / reverseSequential mode flags from the chosen
        // variants. These describe what the OVERALL setup is requesting:
        //  - sequential      = at least one star is using Variant 1
        //  - reverseSequential = at least one star is using Variant 2
        // It is now valid for BOTH to be true at the same time (e.g. Nova = V1,
        // Dwarf = V2). Per-star behaviour is still governed by the per-star
        // novaSequential / novaReverseSequential / dwarfSequential /
        // dwarfReverseSequential flags above.
        bool derivedSequential = false;
        bool derivedReverse = false;

        if (novaVariantIndex == 1 || dwarfVariantIndex == 1)
        {
            derivedSequential = true;
        }
        if (novaVariantIndex == 2 || dwarfVariantIndex == 2)
        {
            derivedReverse = true;
        }

        sequential = derivedSequential;
        reverseSequential = derivedReverse;

        if (isNovaStarEnhanced || isDwarfStarEnhanced)
        {
            Debug.Log($"<color=gold>★★★ ENHANCED MODE: Nova={isNovaStarEnhanced} (V{novaVariantIndex}), Dwarf={isDwarfStarEnhanced} (V{dwarfVariantIndex}), MEGASYNC={megaSyncActive} ★★★</color>");
        }
    }

    private IEnumerator NovaStarCycleBase()
    {
        while (true)
        {
            // During boss events, pause all new NovaStar spawns so that any
            // existing instances can be cleared by EnemyCardSpawner before
            // boss cards are shown.
            if (bossEventActive)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Check if THIS star should switch to enhanced (independent of synchronizeSpawns)
            if (!novaStarWaitingForDowntime)
            {
                CheckEnhancedMode();
                if (isNovaStarEnhanced)
                {
                    Debug.Log($"<color=gold>★ NovaStar transitioning from BASE to ENHANCED! ★</color>");
                    // CRITICAL FIX: Don't set to null first - assign directly to avoid ShouldSynchronize() returning false
                    novaStarSpawnCoroutine = StartCoroutine(NovaStarCycleEnhanced());
                    yield break;
                }
            }

            // Check if can spawn (not waiting for downtime)
            if (!novaStarWaitingForDowntime && Time.time >= novaStarNextSpawnTime)
            {
                // Spawn at current odd level
                SpawnNovaStar(novaStarCurrentLevel);
                novaStarWaitingForDowntime = true; // Now waiting for orbit to complete

                Debug.Log($"<color=orange>NovaStar BASE spawned at Level {novaStarCurrentLevel}, waiting for orbit completion</color>");
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    private IEnumerator DwarfStarCycleBase()
    {
        while (true)
        {
            // During boss events, pause all new DwarfStar spawns so that any
            // existing instances can be cleared by EnemyCardSpawner before
            // boss cards are shown.
            if (bossEventActive)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Check if THIS star should switch to enhanced (independent of synchronizeSpawns)
            if (!dwarfStarWaitingForDowntime)
            {
                CheckEnhancedMode();
                if (isDwarfStarEnhanced)
                {
                    Debug.Log($"<color=gold>★ DwarfStar transitioning from BASE to ENHANCED! ★</color>");
                    // CRITICAL FIX: Don't set to null first - assign directly to avoid ShouldSynchronize() returning false
                    dwarfStarSpawnCoroutine = StartCoroutine(DwarfStarCycleEnhanced());
                    yield break;
                }
            }
            
            // Check if can spawn (not waiting for downtime)
            if (!dwarfStarWaitingForDowntime && Time.time >= dwarfStarNextSpawnTime)
            {
                // Spawn at current even level
                SpawnDwarfStar(dwarfStarCurrentLevel);
                dwarfStarWaitingForDowntime = true; // Now waiting for orbit to complete

                Debug.Log($"<color=cyan>DwarfStar BASE spawned at Level {dwarfStarCurrentLevel}, waiting for orbit completion</color>");
            }

            yield return new WaitForSeconds(0.5f);
        }
    }

    // ==================== ENHANCED VERSION CYCLES ====================

    private IEnumerator NovaStarCycleEnhanced()
    {
        // CRITICAL FIX: Reset sequential level to 1 when starting enhanced mode
        if (novaSequential && !novaReverseSequential)
        {
            currentSequentialLevel = 1;
            Debug.Log($"<color=gold>★ NovaStar Enhanced: Reset currentSequentialLevel to 1</color>");
        }
        else if (novaReverseSequential)
        {
            currentReverseSequentialLevel = 6;
            Debug.Log($"<color=magenta>★ NovaStar Enhanced: Reset currentReverseSequentialLevel to 6</color>");
        }
        
        Debug.Log($"<color=gold>╔═══════════════════════════════════════════════════════════╗</color>");
        Debug.Log($"<color=gold>║   NOVASTAR ENHANCED CYCLE STARTED                        ║</color>");
        Debug.Log($"<color=gold>╚═══════════════════════════════════════════════════════════╝</color>");
        Debug.Log($"  sequential: {sequential}");
        Debug.Log($"  reverseSequential: {reverseSequential}");
        Debug.Log($"  synchronizeSpawns: {synchronizeSpawns}");
        Debug.Log($"  currentSequentialLevel: {currentSequentialLevel}");
        Debug.Log($"  isNovaStarEnhanced: {isNovaStarEnhanced}");
        Debug.Log($"  isDwarfStarEnhanced: {isDwarfStarEnhanced}");
        
        while (true)
        {
            // During boss events, pause all enhanced NovaStar spawns and
            // waits so that any existing instances can be destroyed and no
            // new orbits appear while boss cards/menace are active.
            if (bossEventActive)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Check if we should synchronize with DwarfStar
            bool shouldSync = ShouldSynchronize();
            bool megaSync = megaSyncActive;
            Debug.Log($"<color=gold>═══ NovaStarCycleEnhanced Loop Iteration ═══</color>");
            Debug.Log($"  shouldSync: {shouldSync}");

            // Helper flags for stacked/partial-stacked behaviour. History flags
            // (novaHasVariant1Stack / novaHasVariant2Stack / dwarfHasVariant1Stack /
            // dwarfHasVariant2Stack) indicate which variants have EVER been chosen
            // this run, independent of the CURRENT variant index. This allows us to
            // keep extra tracks (e.g. Nova V2) running even while the shared
            // variant (e.g. V1) is synchronized between both stars.
            bool novaHasBothStacks = novaHasVariant1Stack && novaHasVariant2Stack;
            bool dwarfHasBothStacks = dwarfHasVariant1Stack && dwarfHasVariant2Stack;

            // History-based V1 sync: once BOTH stars have EVER taken Variant 1 and
            // synchronizeSpawns is enabled, we treat the V1 sequential track as a
            // shared sync track even if their current UI-selected variants diverge.
            bool v1HistorySync = false;

            // Partial stacking case A:
            //   - Nova has BOTH Variant 1 and 2 in history
            //   - Dwarf has ONLY Variant 1 in history
            //   - Both are currently using Variant 1 and synchronization is ON
            // Behaviour:
            //   - Nova V1 + Dwarf V1 run synchronized on the sequential 1→2→3 track
            //   - Nova V2 continues to run its own reverse 6→5→4 track independently
            //     of Dwarf (result: two Nova and one Dwarf per step).
            bool partialNovaExtra = v1HistorySync && !megaSync && novaHasBothStacks &&
                                   dwarfHasVariant1Stack && !dwarfHasBothStacks;

            // Partial stacking case B (symmetric to A):
            //   - Dwarf has BOTH Variant 1 and 2 in history
            //   - Nova has ONLY Variant 1 in history
            //   - Both are currently using Variant 1 and synchronization is ON
            // Behaviour:
            //   - Nova V1 + Dwarf V1 run synchronized on 1→2→3
            //   - Dwarf V2 continues its own reverse 6→5→4 track (one Nova, two
            //     Dwarfs per step).
            bool partialDwarfExtra = v1HistorySync && !megaSync && dwarfHasBothStacks &&
                                     novaHasVariant1Stack && !novaHasBothStacks;

            // MEGASYNC: BOTH stars enhanced and both have EVER chosen Variant 1
            // and Variant 2. In this mode, NovaStar acts as the master spawner
            // for FOUR synchronized tracks per step:
            //   - Nova sequential  (1→2→3)
            //   - Nova reverse     (6→5→4)
            //   - Dwarf sequential (1→2→3)
            //   - Dwarf reverse    (6→5→4)
            // We wait for ALL Nova/Dwarf instances to despawn before advancing
            // to the next pair of levels so that all four orbits truly line up.
            if (megaSync)
            {
                // On entering MEGASYNC, ensure the level trackers are in their
                // canonical ranges and that no legacy orbits are still active.
                if (!megaSyncInitialized)
                {
                    megaSyncInitialized = true;
                    novaWasInSyncMode = true;

                    // Wait until there are no NovaStar or DwarfStar instances
                    // alive. This guarantees MEGASYNC starts from a clean slate.
                    while (true)
                    {
                        bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                        bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                        if (!anyNova && !anyDwarf)
                        {
                            break;
                        }

                        Debug.Log("<color=yellow>Waiting for existing NovaStar/DwarfStar orbits to finish before starting MEGASYNC...</color>");
                        yield return new WaitForSeconds(0.5f);
                    }

                    currentSequentialLevel = 1;
                    currentReverseSequentialLevel = 6;
                    Debug.Log("<color=gold>MEGASYNC: Reset sequential to 1 and reverse to 6</color>");
                }

                // Ensure trackers are in expected ranges in case MEGASYNC was
                // re-entered after some external change.
                if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                {
                    currentSequentialLevel = 1;
                }
                if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                {
                    currentReverseSequentialLevel = 6;
                }

                Debug.Log($"<color=gold>MEGASYNC STEP: seq L{currentSequentialLevel}, rev L{currentReverseSequentialLevel}</color>");

                // Reset completion counters for BOTH stars.
                novaStarsCompletedCount = 0;
                novaStarsActiveCount = 0;
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;

                // Spawn four synchronized orbits: NV1/DV1 (sequential) and
                // NV2/DV2 (reverse).
                SpawnSingleNovaStarAtLevel(currentSequentialLevel, 0f);
                SpawnSingleDwarfStarAtLevel(currentSequentialLevel, 0f);
                SpawnSingleNovaStarAtLevelReverse(currentReverseSequentialLevel, 0f);
                SpawnSingleDwarfStarAtLevelReverse(currentReverseSequentialLevel, 0f);

                // Wait until ALL NovaStar and DwarfStar instances have despawned.
                float megaWaitStart = Time.time;
                while (true)
                {
                    bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                    bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                    if (!anyNova && !anyDwarf)
                    {
                        Debug.Log($"<color=gold>MEGASYNC: all four orbits completed for seq L{currentSequentialLevel} / rev L{currentReverseSequentialLevel}</color>");
                        break;
                    }

                    if (Time.time - megaWaitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for MEGASYNC completion at seq L{currentSequentialLevel} / rev L{currentReverseSequentialLevel}. Still waiting to avoid overlapping waves.</color>");
                    }

                    yield return new WaitForSeconds(0.5f);
                }

                // Downtime uses the slower of the two level-specific downtimes so
                // the MEGASYNC step feels like a single combined cycle.
                float seqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                float revDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                float megaDowntime = Mathf.Max(seqDowntime, revDowntime);
                yield return new WaitForSeconds(megaDowntime);

                // Advance both tracks: 1→2→3 and 6→5→4.
                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log("<color=gold>MEGASYNC SEQ: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log("<color=gold>MEGASYNC SEQ: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log("<color=magenta>MEGASYNC REV: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log("<color=magenta>MEGASYNC REV: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                continue;
            }

            // PARTIAL STACKING (Nova extra reverse track): Nova has BOTH variants
            // in history, Dwarf has ONLY Variant 1, and both are currently using
            // Variant 1 with synchronization enabled. In this mode we spawn THREE
            // orbits per step:
            //   - Nova V1  (sequential track 1→2→3, synchronized with Dwarf V1)
            //   - Dwarf V1 (sequential track 1→2→3)
            //   - Nova V2  (reverse track 6→5→4, independent of Dwarf)
            if (partialNovaExtra)
            {
                // Clamp trackers into their expected ranges before use.
                if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                {
                    currentSequentialLevel = 1;
                }
                if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                {
                    currentReverseSequentialLevel = 6;
                }

                Debug.Log($"<color=gold>NovaStar PARTIAL STACK: seq L{currentSequentialLevel} (Nova+Dwarf V1), rev L{currentReverseSequentialLevel} (Nova V2)</color>");

                // Reset counters for BOTH stars so this step is treated as a
                // single combined cycle.
                novaStarsCompletedCount = 0;
                novaStarsActiveCount = 0;
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;

                // Spawn synchronized sequential pair (Nova V1 + Dwarf V1).
                SpawnSingleNovaStarAtLevel(currentSequentialLevel, 0f);
                SpawnSingleDwarfStarAtLevel(currentSequentialLevel, 0f);

                // Spawn extra reverse Nova V2 at its own level.
                SpawnSingleNovaStarAtLevelReverse(currentReverseSequentialLevel, 0f);

                // Wait until ALL Nova/Dwarf instances from this step have
                // despawned so no new wave overlaps a previous one.
                float partialWaitStart = Time.time;
                while (true)
                {
                    bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                    bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                    if (!anyNova && !anyDwarf)
                    {
                        Debug.Log($"<color=gold>NovaStar PARTIAL STACK: all orbits completed for seq L{currentSequentialLevel} / rev L{currentReverseSequentialLevel}</color>");
                        break;
                    }

                    if (Time.time - partialWaitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for partial Nova V1+V2 / Dwarf V1 completion at seq L{currentSequentialLevel} / rev L{currentReverseSequentialLevel}. Still waiting to avoid overlapping waves.</color>");
                    }

                    yield return new WaitForSeconds(0.5f);
                }

                // Downtime uses the slower of the two levels so the trio feels
                // like a single combined cycle.
                float partialSeqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                float partialRevDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                float partialDowntime = Mathf.Max(partialSeqDowntime, partialRevDowntime);
                yield return new WaitForSeconds(partialDowntime);

                // Advance both tracks: 1→2→3 (sequential) and 6→5→4 (reverse).
                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log("<color=gold>PARTIAL STACK SEQ: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log("<color=gold>PARTIAL STACK SEQ: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log("<color=magenta>PARTIAL STACK REV: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log("<color=magenta>PARTIAL STACK REV: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                continue;
            }

            // PARTIAL STACKING (Dwarf extra reverse track): Dwarf has BOTH
            // variants in history, Nova has ONLY Variant 1, and both are
            // currently using Variant 1 with synchronization enabled. This
            // mirrors the Nova partial case but spawns an extra Dwarf V2 orbit
            // instead (one Nova, two Dwarfs per step).
            if (partialDwarfExtra)
            {
                if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                {
                    currentSequentialLevel = 1;
                }
                if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                {
                    currentReverseSequentialLevel = 6;
                }

                Debug.Log($"<color=gold>DwarfStar PARTIAL STACK: seq L{currentSequentialLevel} (Nova+Dwarf V1), rev L{currentReverseSequentialLevel} (Dwarf V2)</color>");

                novaStarsCompletedCount = 0;
                novaStarsActiveCount = 0;
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;

                // Synchronized sequential pair (Nova V1 + Dwarf V1).
                SpawnSingleNovaStarAtLevel(currentSequentialLevel, 0f);
                SpawnSingleDwarfStarAtLevel(currentSequentialLevel, 0f);

                // Extra reverse Dwarf V2.
                SpawnSingleDwarfStarAtLevelReverse(currentReverseSequentialLevel, 0f);

                float dwarfPartialWaitStart = Time.time;
                while (true)
                {
                    bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                    bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                    if (!anyNova && !anyDwarf)
                    {
                        Debug.Log($"<color=gold>DwarfStar PARTIAL STACK: all orbits completed for seq L{currentSequentialLevel} / rev L{currentReverseSequentialLevel}</color>");
                        break;
                    }

                    if (Time.time - dwarfPartialWaitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for partial Nova V1 / Dwarf V1+V2 completion at seq L{currentSequentialLevel} / rev L{currentReverseSequentialLevel}. Still waiting to avoid overlapping waves.</color>");
                    }

                    yield return new WaitForSeconds(0.5f);
                }

                float dwarfPartialSeqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                float dwarfPartialRevDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                float dwarfPartialDowntime = Mathf.Max(dwarfPartialSeqDowntime, dwarfPartialRevDowntime);
                yield return new WaitForSeconds(dwarfPartialDowntime);

                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log("<color=gold>Dwarf PARTIAL STACK SEQ: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log("<color=gold>Dwarf PARTIAL STACK SEQ: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log("<color=magenta>Dwarf PARTIAL STACK REV: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log("<color=magenta>Dwarf PARTIAL STACK REV: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                continue;
            }

            // When NovaStar has EVER taken BOTH Variant 1 (sequential) and
            // Variant 2 (reverse sequential), and it is the ONLY enhanced star
            // (no DwarfStar enhanced, no sync), we treat this as a stacked
            // sequential+reverse mode for Nova alone. In this case we spawn TWO
            // independent NovaStars per step: one using the forward sequential
            // levels (1→2→3) and one using the reverse sequential levels
            // (6→5→4), sharing the same modifiers. This preserves the existing
            // sync mechanic because we only enable stacking when
            //   - Nova has both variants in its history, AND
            //   - sync is currently OFF, AND
            //   - DwarfStar is NOT enhanced.
            bool novaSeqRevStacked = novaHasVariant1Stack && novaHasVariant2Stack && !megaSync;

            if (novaSeqRevStacked)
            {
                // Ensure the per-mode level trackers are within their expected
                // ranges before we start pairing them.
                if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                {
                    currentSequentialLevel = 1;
                }
                if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                {
                    currentReverseSequentialLevel = 6;
                }

                Debug.Log($"<color=gold>NovaStar STACKED SEQ+REV SOLO: spawning Level {currentSequentialLevel} (seq) & Level {currentReverseSequentialLevel} (rev)</color>");

                // Reset Nova counters for this paired step.
                novaStarsCompletedCount = 0;
                novaStarsActiveCount = 0;

                // Spawn one sequential NovaStar at the currentSequentialLevel.
                SpawnSingleNovaStarAtLevel(currentSequentialLevel, 0f);

                // Spawn one reverse-sequential NovaStar at the currentReverseSequentialLevel.
                SpawnSingleNovaStarAtLevelReverse(currentReverseSequentialLevel, 0f);

                // Wait until ALL NovaStars from this step have despawned. We rely
                // on actual scene instances here to guarantee no overlapping
                // waves for the stacked pair.
                float waitStart = Time.time;
                while (true)
                {
                    bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                    if (!anyNova)
                    {
                        Debug.Log($"<color=gold>NovaStar STACKED SEQ+REV: both orbits completed (seq L{currentSequentialLevel}, rev L{currentReverseSequentialLevel})</color>");
                        break;
                    }

                    if (Time.time - waitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for stacked NovaStar seq+rev completion at Levels {currentSequentialLevel}/{currentReverseSequentialLevel}. Still waiting to avoid overlapping waves.</color>");
                    }

                    yield return new WaitForSeconds(0.5f);
                }

                // Use the slower of the two downtimes so the pair feels like a
                // single combined cycle.
                float seqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                float revDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                float stackedDowntime = Mathf.Max(seqDowntime, revDowntime);
                yield return new WaitForSeconds(stackedDowntime);

                // Advance the forward and reverse tracks: 1→2→3 and 6→5→4.
                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log($"<color=gold>NovaStar STACKED SEQ: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=gold>NovaStar STACKED SEQ: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log($"<color=magenta>NovaStar STACKED REV: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=magenta>NovaStar STACKED REV: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                continue;
            }

            // ==================== SEQUENTIAL MODE (per-star) ====================
            if (novaSequential && !novaReverseSequential)
            {
                bool spawnedReverseDuringSequential = false;
                if (shouldSync)
                {
                    // Check if we just transitioned to sync mode
                    if (!novaWasInSyncMode)
                    {
                        novaWasInSyncMode = true;
                        Debug.Log($"<color=gold>NovaStar TRANSITIONED TO SYNC MODE (SEQUENTIAL): Waiting for ALL existing Nova/Dwarf orbits to complete before starting synchronized sequence</color>");

                        // Wait until there are no NovaStar or DwarfStar instances left
                        // in the scene. This guarantees that any remaining base or
                        // unsynchronized enhanced orbits have fully finished before we
                        // begin the synchronized sequential sequence at Level 1.
                        while (true)
                        {
                            bool anyNovaSeq = FindObjectsOfType<NovaStar>().Length > 0;
                            bool anyDwarfSeq = FindObjectsOfType<DwarfStar>().Length > 0;
                            if (!anyNovaSeq && !anyDwarfSeq)
                            {
                                break;
                            }

                            Debug.Log("<color=yellow>Waiting for existing NovaStar/DwarfStar orbits to finish before starting synchronized sequential sequence...</color>");
                            yield return new WaitForSeconds(0.5f);
                        }

                        // NOW reset to level 1 after ALL previous orbits completed
                        currentSequentialLevel = 1;
                        Debug.Log($"<color=gold>All previous star orbits completed! Resetting to Level 1 for synchronized sequential sequence</color>");
                    }

                    // SYNCHRONIZED SEQUENTIAL: Both stars spawn together
                    Debug.Log($"<color=gold>NovaStar SYNCHRONIZED SEQUENTIAL: Waiting for DwarfStar coroutine</color>");
                    while (!isDwarfStarEnhanced || dwarfStarSpawnCoroutine == null)
                    {
                        yield return new WaitForSeconds(0.5f);
                    }

                    Debug.Log($"<color=gold>SEQUENTIAL MODE (SYNC): Spawning level {currentSequentialLevel}</color>");

                    // Reset completion counters for BOTH (only after waiting is done)
                    novaStarsCompletedCount = 0;
                    novaStarsActiveCount = 0;
                    dwarfStarsCompletedCount = 0;
                    dwarfStarsActiveCount = 0;

                    // Spawn BOTH stars at current level
                    SpawnNovaStarAtLevel(currentSequentialLevel, 0);
                    SpawnDwarfStarAtLevel(currentSequentialLevel, 0);

                    Debug.Log($"<color=gold>SYNCHRONIZED: Both stars spawned at Level {currentSequentialLevel}</color>");

                    // Wait for BOTH to complete using actual scene objects so that no
                    // new level starts while any previous orbit is still alive.
                    float bothWaitStart = Time.time;
                    while (true)
                    {
                        bool anyNovaSeq = FindObjectsOfType<NovaStar>().Length > 0;
                        bool anyDwarfSeq = FindObjectsOfType<DwarfStar>().Length > 0;

                        if (!anyNovaSeq && !anyDwarfSeq)
                        {
                            Debug.Log($"<color=gold>Both stars completed Level {currentSequentialLevel}!</color>");
                            break;
                        }

                        if (Time.time - bothWaitStart > enhancedStallTimeout)
                        {
                            // Log a warning but DO NOT advance to the next level while
                            // stars are still alive. We only leave this loop when the
                            // scene truly has no NovaStar/DwarfStar instances.
                            Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for all NovaStar/DwarfStar instances to despawn at Level {currentSequentialLevel} (sequential sync). Still waiting to avoid overlapping waves.</color>");
                        }

                        yield return new WaitForSeconds(0.5f);
                    }
                }
                else
                {
                    // Not in sync mode anymore
                    novaWasInSyncMode = false;
                    
                    // SINGLE-STAR SEQUENTIAL: Only NovaStar spawns
                    Debug.Log($"<color=gold>╔═══════════════════════════════════════════════════════════╗</color>");
                    Debug.Log($"<color=gold>║   SEQUENTIAL MODE (SOLO) - SPAWNING NOVASTAR            ║</color>");
                    Debug.Log($"<color=gold>╚═══════════════════════════════════════════════════════════╝</color>");
                    Debug.Log($"  currentSequentialLevel: {currentSequentialLevel}");
                    Debug.Log($"  novaStarsCompletedCount: {novaStarsCompletedCount}");
                    Debug.Log($"  novaStarsActiveCount: {novaStarsActiveCount}");
                    
                    // Reset completion counters for NovaStar only
                    novaStarsCompletedCount = 0;
                    novaStarsActiveCount = 0;
                    
                    Debug.Log($"<color=gold>  Calling SpawnSingleNovaStarAtLevel({currentSequentialLevel}, 0)...</color>");
                    
                    // Spawn only ONE NovaStar at this level in sequential SOLO mode
                    SpawnSingleNovaStarAtLevel(currentSequentialLevel, 0);
                    
                    Debug.Log($"<color=gold>  SpawnNovaStarAtLevel completed!</color>");
                    Debug.Log($"<color=gold>  novaStarsActiveCount after spawn: {novaStarsActiveCount}</color>");
                    
                    // Wait until all NovaStar instances have despawned before advancing.
                    float novaWaitStart = Time.time;
                    while (true)
                    {
                        CheckEnhancedMode();
                        if (!megaSync && !spawnedReverseDuringSequential && novaHasVariant1Stack && novaHasVariant2Stack)
                        {
                            if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                            {
                                currentReverseSequentialLevel = 6;
                            }

                            SpawnSingleNovaStarAtLevelReverse(currentReverseSequentialLevel, 0f);
                            spawnedReverseDuringSequential = true;
                        }

                        bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                        if (!anyNova)
                        {
                            Debug.Log($"<color=gold>NovaStar completed Level {currentSequentialLevel}!</color>");
                            break;
                        }

                        if (Time.time - novaWaitStart > enhancedStallTimeout)
                        {
                            Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for NovaStar instances to despawn at Level {currentSequentialLevel} (sequential solo). Still waiting to avoid overlapping waves.</color>");
                        }

                        yield return new WaitForSeconds(0.5f);
                    }
                }
                
                // Downtime
                float downtime1 = GetDowntimeForLevel(currentSequentialLevel);
                if (spawnedReverseDuringSequential)
                {
                    float reverseDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                    downtime1 = Mathf.Max(downtime1, reverseDowntime);
                }
                yield return new WaitForSeconds(downtime1);
                
                // Progress to next level (1 → 2 → 3)
                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log($"<color=gold>SEQUENTIAL MODE: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=gold>SEQUENTIAL MODE: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                if (spawnedReverseDuringSequential)
                {
                    currentReverseSequentialLevel--;
                    if (currentReverseSequentialLevel < 4)
                    {
                        if (loopLevels)
                        {
                            currentReverseSequentialLevel = 6;
                            Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Looping back to Level 6 (6→5→4)</color>");
                        }
                        else
                        {
                            Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Reached Level 4, stopping</color>");
                            yield break;
                        }
                    }
                }
                
                continue;
            }
            
            // ==================== REVERSE SEQUENTIAL MODE (per-star) ====================
            if (novaReverseSequential)
            {
                bool spawnedSequentialDuringReverse = false;
                if (shouldSync)
                {
                    // Check if we just transitioned to sync mode
                    if (!novaWasInSyncMode)
                    {
                        novaWasInSyncMode = true;
                        Debug.Log($"<color=magenta>NovaStar TRANSITIONED TO SYNC MODE (REVERSE): Waiting for ALL existing Nova/Dwarf orbits to complete before starting synchronized reverse sequence</color>");

                        // Wait until there are no NovaStar or DwarfStar instances left
                        // in the scene. This guarantees that any remaining base or
                        // unsynchronized enhanced orbits have fully finished before we
                        // begin the synchronized reverse sequence at Level 6.
                        while (true)
                        {
                            bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                            bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                            if (!anyNova && !anyDwarf)
                            {
                                break;
                            }

                            Debug.Log("<color=yellow>Waiting for existing NovaStar/DwarfStar orbits to finish before starting synchronized reverse sequence...</color>");
                            yield return new WaitForSeconds(0.5f);
                        }

                        // NOW reset to level 6 after ALL previous orbits completed
                        currentReverseSequentialLevel = 6;
                        Debug.Log($"<color=magenta>All previous star orbits completed! Resetting to Level 6 for synchronized reverse sequence</color>");
                    }
                    
                    // SYNCHRONIZED REVERSE SEQUENTIAL: Both stars spawn together
                    Debug.Log($"<color=gold>NovaStar SYNCHRONIZED REVERSE SEQUENTIAL: Waiting for DwarfStar coroutine</color>");
                    while (!isDwarfStarEnhanced || dwarfStarSpawnCoroutine == null)
                    {
                        yield return new WaitForSeconds(0.5f);
                    }
                    
                    Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE (SYNC): Spawning level {currentReverseSequentialLevel}</color>");
                    
                    // Reset completion counters for BOTH (only after waiting is done)
                    novaStarsCompletedCount = 0;
                    novaStarsActiveCount = 0;
                    dwarfStarsCompletedCount = 0;
                    dwarfStarsActiveCount = 0;
                    
                    // Spawn BOTH stars at current level with REVERSE speed calculation
                    SpawnNovaStarAtLevelReverse(currentReverseSequentialLevel, 0);
                    SpawnDwarfStarAtLevelReverse(currentReverseSequentialLevel, 0);
                    
                    Debug.Log($"<color=magenta>SYNCHRONIZED: Both stars spawned at Level {currentReverseSequentialLevel}</color>");
                    
                    // Wait for BOTH to complete. We rely on the actual presence of
                    // NovaStar/DwarfStar instances in the scene instead of only the
                    // internal counters so that no new level starts while any previous
                    // orbit is still alive.
                    float bothRevWaitStart = Time.time;
                    while (true)
                    {
                        bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                        bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;

                        if (!anyNova && !anyDwarf)
                        {
                            Debug.Log($"<color=magenta>Both stars completed Level {currentReverseSequentialLevel}!</color>");
                            break;
                        }

                        if (Time.time - bothRevWaitStart > enhancedStallTimeout)
                        {
                            // Log a warning but DO NOT advance to the next level while
                            // stars are still alive. We only leave this loop when the
                            // scene truly has no NovaStar/DwarfStar instances.
                            Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for all NovaStar/DwarfStar instances to despawn at Level {currentReverseSequentialLevel} (reverse sync). Still waiting to avoid overlapping waves.</color>");
                        }

                        yield return new WaitForSeconds(0.5f);
                    }
                }
                else
                {
                    // Not in sync mode anymore
                    novaWasInSyncMode = false;
                    
                    // SINGLE-STAR REVERSE SEQUENTIAL: Only NovaStar spawns
                    Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE (SOLO): NovaStar spawning level {currentReverseSequentialLevel}</color>");
                    
                    // Reset completion counters for NovaStar only
                    novaStarsCompletedCount = 0;
                    novaStarsActiveCount = 0;
                    
                    // Spawn only ONE NovaStar with REVERSE speed in SOLO mode
                    SpawnSingleNovaStarAtLevelReverse(currentReverseSequentialLevel, 0);
                    
                    Debug.Log($"<color=magenta>SOLO: NovaStar spawned at Level {currentReverseSequentialLevel}</color>");
                    
                    // Wait for ALL NovaStar instances to despawn before moving to the
                    // next reverse level. Using actual scene objects here prevents new
                    // waves from starting while any previous orbit is still alive,
                    // which was causing multiple NovaStars to accumulate across levels.
                    float novaRevWaitStart = Time.time;
                    while (true)
                    {
                        CheckEnhancedMode();
                        if (!megaSync && !spawnedSequentialDuringReverse && novaHasVariant1Stack && novaHasVariant2Stack)
                        {
                            if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                            {
                                currentSequentialLevel = 1;
                            }

                            SpawnSingleNovaStarAtLevel(currentSequentialLevel, 0f);
                            spawnedSequentialDuringReverse = true;
                        }

                        bool anyNova = FindObjectsOfType<NovaStar>().Length > 0;
                        if (!anyNova)
                        {
                            Debug.Log($"<color=magenta>NovaStar completed Level {currentReverseSequentialLevel}!</color>");
                            break;
                        }

                        if (Time.time - novaRevWaitStart > enhancedStallTimeout)
                        {
                            // Log a warning but keep waiting until all NovaStars are
                            // actually gone to avoid overlapping reverse-sequential waves.
                            Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for NovaStar reverse sequential completion at Level {currentReverseSequentialLevel}. Still waiting to avoid overlapping waves.</color>");
                        }
                        
                        yield return new WaitForSeconds(0.5f);
                    }
                }
                
                // Downtime
                float downtime2 = GetDowntimeForLevel(currentReverseSequentialLevel);
                if (spawnedSequentialDuringReverse)
                {
                    float seqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                    downtime2 = Mathf.Max(downtime2, seqDowntime);
                }
                yield return new WaitForSeconds(downtime2);
                
                // Progress to previous level (6 → 5 → 4)
                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                if (spawnedSequentialDuringReverse)
                {
                    currentSequentialLevel++;
                    if (currentSequentialLevel > 3)
                    {
                        if (loopLevels)
                        {
                            currentSequentialLevel = 1;
                            Debug.Log($"<color=gold>SEQUENTIAL MODE: Looping back to Level 1 (1→2→3)</color>");
                        }
                        else
                        {
                            Debug.Log($"<color=gold>SEQUENTIAL MODE: Reached Level 3, stopping</color>");
                            yield break;
                        }
                    }
                }
                
                continue;
            }
            
            // ==================== ODD/EVEN MODE ====================
            // Not in sequential/reverse modes for this star
            novaWasInSyncMode = false;
            
            // Determine which levels to spawn (use shared toggle if synchronized)
            int[] levels = novaStarSpawnOddLevels ? new int[] { 1, 3, 5 } : new int[] { 2, 4, 6 };
            
            // Reset completion counters - ONLY for NovaStar!
            novaStarsCompletedCount = 0;
            novaStarsActiveCount = 0;
            
            // In synchronized mode, NovaStar is the master spawner for BOTH stars.
            // Reset DwarfStar counters here so that the wait-for-both-complete logic
            // only considers the current synchronized batch.
            if (shouldSync)
            {
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;
            }
            
            // Spawn NovaStar at each level with delay
            for (int i = 0; i < levels.Length; i++)
            {
                int level = levels[i];
                float delayMultiplier = i;
                
                SpawnNovaStarAtLevel(level, delayMultiplier);
                if (shouldSync)
                {
                    SpawnDwarfStarAtLevel(level, delayMultiplier);
                }

                if (i < levels.Length - 1)
                {
                    yield return new WaitForSeconds(enhancedLevelSpawnDelay);
                }
            }
            
            string levelStr = novaStarSpawnOddLevels ? "ODD (1,3,5)" : "EVEN (2,4,6)";
            string syncStr = shouldSync ? "SYNCHRONIZED" : "INDEPENDENT";
            Debug.Log($"<color=gold>NovaStar ENHANCED ({syncStr}): Spawned at {levelStr} levels</color>");
            
            // Wait for all NovaStar to complete, with safety timeout
            float novaEnhancedWaitStart = Time.time;
            while (true)
            {
                if (novaStarsActiveCount > 0 && novaStarsCompletedCount >= novaStarsActiveCount)
                {
                    Debug.Log($"<color=gold>NovaStar Enhanced cycle complete! {novaStarsCompletedCount}/{novaStarsActiveCount}</color>");
                    break;
                }

                if (Time.time - novaEnhancedWaitStart > enhancedStallTimeout)
                {
                    Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for NovaStar enhanced odd/even completion. ({novaStarsCompletedCount}/{novaStarsActiveCount})</color>");
                    break;
                }
                
                yield return new WaitForSeconds(0.5f);
            }
            
            // If synchronized, wait for DwarfStar to also complete
            if (shouldSync)
            {
                Debug.Log($"<color=gold>NovaStar waiting for DwarfStar to complete...</color>");
                float dwarfEnhancedWaitStart = Time.time;
                while (true)
                {
                    bool dwarfComplete = dwarfStarsActiveCount > 0 && dwarfStarsCompletedCount >= dwarfStarsActiveCount;
                    if (dwarfComplete)
                    {
                        Debug.Log($"<color=gold>Both stars completed! Proceeding to downtime...</color>");
                        break;
                    }

                    if (Time.time - dwarfEnhancedWaitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for DwarfStar enhanced completion. ({dwarfStarsCompletedCount}/{dwarfStarsActiveCount})</color>");
                        break;
                    }

                    yield return new WaitForSeconds(0.5f);
                }
            }
            
            // Toggle for next spawn (only if not synchronized)
            if (!shouldSync)
            {
                novaStarSpawnOddLevels = !novaStarSpawnOddLevels;
            }
            
            // Wait for downtime before next spawn
            float downtime = novaStarSpawnOddLevels ? enhancedEvenDowntime : enhancedOddDowntime;
            yield return new WaitForSeconds(downtime);
            
            // If synchronized, toggle the shared flag AFTER downtime
            if (shouldSync)
            {
                novaStarSpawnOddLevels = !novaStarSpawnOddLevels;
                dwarfStarSpawnOddLevels = novaStarSpawnOddLevels; // Keep in sync
            }
        }
    }

    private IEnumerator DwarfStarCycleEnhanced()
    {
        // CRITICAL FIX: Reset sequential level to 1 when starting enhanced mode
        if (dwarfSequential && !dwarfReverseSequential)
        {
            currentSequentialLevel = 1;
            Debug.Log($"<color=gold>★ DwarfStar Enhanced: Reset currentSequentialLevel to 1</color>");
        }
        else if (dwarfReverseSequential)
        {
            currentReverseSequentialLevel = 6;
            Debug.Log($"<color=magenta>★ DwarfStar Enhanced: Reset currentReverseSequentialLevel to 6</color>");
        }
        
        Debug.Log($"<color=gold>DwarfStar Enhanced: Starting cycle!</color>");
        
        while (true)
        {
            // During boss events, pause all enhanced DwarfStar behaviour so
            // any existing instances can be destroyed cleanly and no new
            // orbits appear while boss cards/menace are active.
            if (bossEventActive)
            {
                yield return new WaitForSeconds(0.5f);
                continue;
            }

            // Check if we should synchronize with NovaStar
            bool shouldSync = ShouldSynchronize();

            // History-based V1 sync helper: once BOTH stars have EVER taken
            // Variant 1 and synchronizeSpawns is enabled, the V1 sequential
            // track can be treated as a shared sync track even if the current
            // UI-selected variants diverge. This is the same v1HistorySync used
            // in NovaStarCycleEnhanced.
            bool novaHasBothStacks = novaHasVariant1Stack && novaHasVariant2Stack;
            bool dwarfHasBothStacks = dwarfHasVariant1Stack && dwarfHasVariant2Stack;
            bool v1HistorySync = false;

            // Partial Nova-extra mode (mirror of partialNovaExtra in
            // NovaStarCycleEnhanced): Nova has BOTH Variant 1 and 2 in history,
            // Dwarf has ONLY Variant 1 in history, MEGASYNC is off, and
            // synchronizeSpawns is enabled. In this mode, NovaStar acts as the
            // master spawner for the shared V1 sequential pair (Nova V1 +
            // Dwarf V1) plus an extra reverse Nova V2 track.
            bool partialNovaExtraMode = v1HistorySync && !megaSyncActive &&
                                         novaHasBothStacks && dwarfHasVariant1Stack && !dwarfHasBothStacks;

            // Partial Dwarf-extra mode (mirror of partialDwarfExtra in
            // NovaStarCycleEnhanced): Nova has ONLY Variant 1 in history,
            // Dwarf has BOTH Variant 1 and 2 in history, MEGASYNC is off, and
            // synchronizeSpawns is enabled. In this mode, NovaStar acts as the
            // master spawner for the shared V1 sequential pair (Nova V1 +
            // Dwarf V1) plus an extra reverse Dwarf V2 track. DwarfStar's
            // enhanced coroutine should yield and let NovaStarCycleEnhanced
            // handle all spawning to avoid double-spawning the Dwarf V2 track.
            bool partialDwarfExtraMode = v1HistorySync && !megaSyncActive &&
                                         dwarfHasBothStacks && novaHasVariant1Stack && !novaHasBothStacks;

            // In standard sync OR in the history-based partial Nova-extra or
            // partial Dwarf-extra modes, NovaStar handles ALL spawning.
            // DwarfStar just yields to let NovaStar do its thing.
            if (shouldSync || partialNovaExtraMode || partialDwarfExtraMode)
            {
                // Track that we're in a sync-like mode
                dwarfWasInSyncMode = true;

                // Don't do anything, just yield and loop; NovaStar will spawn
                // both stars (and any extra Dwarf V2 track in partial
                // Dwarf-extra mode).
                yield return null; // Yield one frame
                continue; // Loop back immediately
            }

            // Not in sync or partial-stack-follow mode anymore; clear sync flag
            // and allow DwarfStar to run its own enhanced patterns without
            // waiting for all existing orbits to finish. This mirrors
            // NovaStar's behaviour when leaving synchronized mode and avoids
            // stalling newly picked variants.
            dwarfWasInSyncMode = false;

            // When DwarfStar has EVER taken BOTH Variant 1 (sequential) and
            // Variant 2 (reverse sequential), and it is the ONLY enhanced star
            // (no NovaStar enhanced, no sync), we treat this as a stacked
            // sequential+reverse mode for Dwarf alone. In this case we spawn TWO
            // independent DwarfStars per step: one using the forward sequential
            // levels (1→2→3) and one using the reverse sequential levels
            // (6→5→4), sharing the same modifiers.
            bool dwarfSeqRevStacked = dwarfHasVariant1Stack && dwarfHasVariant2Stack && !megaSyncActive;

            if (dwarfSeqRevStacked)
            {
                // Ensure the per-mode level trackers are within their expected ranges.
                if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                {
                    currentSequentialLevel = 1;
                }
                if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                {
                    currentReverseSequentialLevel = 6;
                }

                Debug.Log($"<color=gold>DwarfStar STACKED SEQ+REV SOLO: spawning Level {currentSequentialLevel} (seq) & Level {currentReverseSequentialLevel} (rev)</color>");

                // Reset Dwarf counters for this paired step.
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;

                // Spawn one sequential DwarfStar at the currentSequentialLevel.
                SpawnSingleDwarfStarAtLevel(currentSequentialLevel, 0f);

                // Spawn one reverse-sequential DwarfStar at the currentReverseSequentialLevel.
                SpawnSingleDwarfStarAtLevelReverse(currentReverseSequentialLevel, 0f);

                // Wait until ALL DwarfStars from this step have despawned.
                float waitStart = Time.time;
                while (true)
                {
                    bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                    if (!anyDwarf)
                    {
                        Debug.Log($"<color=gold>DwarfStar STACKED SEQ+REV: both orbits completed (seq L{currentSequentialLevel}, rev L{currentReverseSequentialLevel})</color>");
                        break;
                    }

                    if (Time.time - waitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for stacked DwarfStar seq+rev completion at Levels {currentSequentialLevel}/{currentReverseSequentialLevel}. Still waiting to avoid overlapping waves.</color>");
                    }

                    yield return new WaitForSeconds(0.5f);
                }

                float seqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                float revDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                float stackedDowntime = Mathf.Max(seqDowntime, revDowntime);
                yield return new WaitForSeconds(stackedDowntime);

                // Advance the forward and reverse tracks: 1→2→3 and 6→5→4.
                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log($"<color=gold>DwarfStar STACKED SEQ: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=gold>DwarfStar STACKED SEQ: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log($"<color=magenta>DwarfStar STACKED REV: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=magenta>DwarfStar STACKED REV: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                continue;
            }

            // In sequential or reverse sequential modes WITHOUT sync, DwarfStar spawns independently
            if (!shouldSync && dwarfSequential && !dwarfReverseSequential)
            {
                // SINGLE-STAR SEQUENTIAL: Only DwarfStar spawns
                Debug.Log($"<color=gold>SEQUENTIAL MODE (SOLO): DwarfStar spawning level {currentSequentialLevel}</color>");
                
                // Reset completion counters for DwarfStar only
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;
                
                // Spawn only ONE DwarfStar at this level in sequential SOLO mode
                SpawnSingleDwarfStarAtLevel(currentSequentialLevel, 0);
                
                Debug.Log($"<color=gold>SOLO: DwarfStar spawned at Level {currentSequentialLevel}</color>");
                
                // Wait until all DwarfStar instances have despawned before advancing.
                bool spawnedReverseDuringSequential = false;
                float dwarfWaitStart = Time.time;
                while (true)
                {
                    CheckEnhancedMode();
                    if (!megaSyncActive && !spawnedReverseDuringSequential && dwarfHasVariant1Stack && dwarfHasVariant2Stack)
                    {
                        if (currentReverseSequentialLevel < 4 || currentReverseSequentialLevel > 6)
                        {
                            currentReverseSequentialLevel = 6;
                        }

                        SpawnSingleDwarfStarAtLevelReverse(currentReverseSequentialLevel, 0f);
                        spawnedReverseDuringSequential = true;
                    }

                    bool anyDwarf = FindObjectsOfType<DwarfStar>().Length > 0;
                    if (!anyDwarf)
                    {
                        Debug.Log($"<color=gold>DwarfStar completed Level {currentSequentialLevel}!</color>");
                        break;
                    }

                    if (Time.time - dwarfWaitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for DwarfStar instances to despawn at Level {currentSequentialLevel} (sequential solo). Still waiting to avoid overlapping waves.</color>");
                    }

                    yield return new WaitForSeconds(0.5f);
                }
                
                // Downtime
                float downtime3 = GetDowntimeForLevel(currentSequentialLevel);
                if (spawnedReverseDuringSequential)
                {
                    float reverseDowntime = GetDowntimeForLevel(currentReverseSequentialLevel);
                    downtime3 = Mathf.Max(downtime3, reverseDowntime);
                }
                yield return new WaitForSeconds(downtime3);
                
                // Progress to next level (1 → 2 → 3)
                currentSequentialLevel++;
                if (currentSequentialLevel > 3)
                {
                    if (loopLevels)
                    {
                        currentSequentialLevel = 1;
                        Debug.Log($"<color=gold>SEQUENTIAL MODE: Looping back to Level 1 (1→2→3)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=gold>SEQUENTIAL MODE: Reached Level 3, stopping</color>");
                        yield break;
                    }
                }

                if (spawnedReverseDuringSequential)
                {
                    currentReverseSequentialLevel--;
                    if (currentReverseSequentialLevel < 4)
                    {
                        if (loopLevels)
                        {
                            currentReverseSequentialLevel = 6;
                            Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Looping back to Level 6 (6→5→4)</color>");
                        }
                        else
                        {
                            Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Reached Level 4, stopping</color>");
                            yield break;
                        }
                    }
                }
                
                continue;
            }
            
            if (!shouldSync && dwarfReverseSequential)
            {
                // SINGLE-STAR REVERSE SEQUENTIAL: Only DwarfStar spawns
                Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE (SOLO): DwarfStar spawning level {currentReverseSequentialLevel}</color>");
                
                // Reset completion counters for DwarfStar only
                dwarfStarsCompletedCount = 0;
                dwarfStarsActiveCount = 0;
                
                // Spawn only ONE DwarfStar with REVERSE speed in SOLO mode
                SpawnSingleDwarfStarAtLevelReverse(currentReverseSequentialLevel, 0);
                
                Debug.Log($"<color=magenta>SOLO: DwarfStar spawned at Level {currentReverseSequentialLevel}</color>");
                
                // Wait for DwarfStar to complete
                bool spawnedSequentialDuringReverse = false;
                while (true)
                {
                    CheckEnhancedMode();
                    if (!megaSyncActive && !spawnedSequentialDuringReverse && dwarfHasVariant1Stack && dwarfHasVariant2Stack)
                    {
                        if (currentSequentialLevel < 1 || currentSequentialLevel > 3)
                        {
                            currentSequentialLevel = 1;
                        }

                        SpawnSingleDwarfStarAtLevel(currentSequentialLevel, 0f);
                        spawnedSequentialDuringReverse = true;
                    }

                    if (dwarfStarsCompletedCount >= dwarfStarsActiveCount && dwarfStarsActiveCount > 0)
                    {
                        Debug.Log($"<color=magenta>DwarfStar completed Level {currentReverseSequentialLevel}!</color>");
                        break;
                    }
                    
                    yield return new WaitForSeconds(0.5f);
                }
                
                // Downtime
                float downtime4 = GetDowntimeForLevel(currentReverseSequentialLevel);
                if (spawnedSequentialDuringReverse)
                {
                    float seqDowntime = GetDowntimeForLevel(currentSequentialLevel);
                    downtime4 = Mathf.Max(downtime4, seqDowntime);
                }
                yield return new WaitForSeconds(downtime4);
                
                // Progress to previous level (6 → 5 → 4)
                currentReverseSequentialLevel--;
                if (currentReverseSequentialLevel < 4)
                {
                    if (loopLevels)
                    {
                        currentReverseSequentialLevel = 6;
                        Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Looping back to Level 6 (6→5→4)</color>");
                    }
                    else
                    {
                        Debug.Log($"<color=magenta>REVERSE SEQUENTIAL MODE: Reached Level 4, stopping</color>");
                        yield break;
                    }
                }

                if (spawnedSequentialDuringReverse)
                {
                    currentSequentialLevel++;
                    if (currentSequentialLevel > 3)
                    {
                        if (loopLevels)
                        {
                            currentSequentialLevel = 1;
                            Debug.Log($"<color=gold>SEQUENTIAL MODE: Looping back to Level 1 (1→2→3)</color>");
                        }
                        else
                        {
                            Debug.Log($"<color=gold>SEQUENTIAL MODE: Reached Level 3, stopping</color>");
                            yield break;
                        }
                    }
                }
                
                continue;
            }
            
            if (shouldSync)
            {
                Debug.Log($"<color=gold>DwarfStar SYNCHRONIZED mode - waiting for NovaStar to be ready</color>");
                // Wait for NovaStar to also be in enhanced mode and ready
                while (!isNovaStarEnhanced || novaStarSpawnCoroutine == null)
                {
                    yield return new WaitForSeconds(0.5f);
                }
                
                // Sync the odd/even toggle with NovaStar
                dwarfStarSpawnOddLevels = novaStarSpawnOddLevels;
            }
            
            // Determine which levels to spawn (use shared toggle if synchronized)
            int[] levels = dwarfStarSpawnOddLevels ? new int[] { 1, 3, 5 } : new int[] { 2, 4, 6 };
            
            // Reset completion counters - ONLY for DwarfStar!
            dwarfStarsCompletedCount = 0;
            dwarfStarsActiveCount = 0;
            
            // Spawn DwarfStar at each level with delay
            for (int i = 0; i < levels.Length; i++)
            {
                int level = levels[i];
                float delayMultiplier = i;
                
                SpawnDwarfStarAtLevel(level, delayMultiplier);
                
                if (i < levels.Length - 1)
                {
                    yield return new WaitForSeconds(enhancedLevelSpawnDelay);
                }
            }
            
            string levelStr = dwarfStarSpawnOddLevels ? "ODD (1,3,5)" : "EVEN (2,4,6)";
            string syncStr = shouldSync ? "SYNCHRONIZED" : "INDEPENDENT";
            Debug.Log($"<color=gold>DwarfStar ENHANCED ({syncStr}): Spawned at {levelStr} levels</color>");
            
            // Wait for all DwarfStar to complete, with safety timeout
            float dwarfEnhancedWaitStart = Time.time;
            while (true)
            {
                if (dwarfStarsActiveCount > 0 && dwarfStarsCompletedCount >= dwarfStarsActiveCount)
                {
                    Debug.Log($"<color=gold>DwarfStar Enhanced cycle complete! {dwarfStarsCompletedCount}/{dwarfStarsActiveCount}</color>");
                    break;
                }

                if (Time.time - dwarfEnhancedWaitStart > enhancedStallTimeout)
                {
                    Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for DwarfStar enhanced odd/even completion. ({dwarfStarsCompletedCount}/{dwarfStarsActiveCount})</color>");
                    break;
                }
                
                yield return new WaitForSeconds(0.5f);
            }
            
            // If synchronized, wait for NovaStar to also complete
            if (shouldSync)
            {
                Debug.Log($"<color=gold>DwarfStar waiting for NovaStar to complete...</color>");
                float novaEnhancedWaitStart = Time.time;
                while (true)
                {
                    bool novaComplete = novaStarsActiveCount > 0 && novaStarsCompletedCount >= novaStarsActiveCount;
                    if (novaComplete)
                    {
                        Debug.Log($"<color=gold>Both stars completed! Proceeding to downtime...</color>");
                        break;
                    }

                    if (Time.time - novaEnhancedWaitStart > enhancedStallTimeout)
                    {
                        Debug.LogWarning($"<color=yellow>[OrbitalStarManager] Timeout waiting for NovaStar enhanced completion. ({novaStarsCompletedCount}/{novaStarsActiveCount})</color>");
                        break;
                    }

                    yield return new WaitForSeconds(0.5f);
                }
            }
            
            // Toggle for next spawn (only if not synchronized)
            if (!shouldSync)
            {
                dwarfStarSpawnOddLevels = !dwarfStarSpawnOddLevels;
            }
            
            // Wait for downtime before next spawn
            float downtime = dwarfStarSpawnOddLevels ? enhancedEvenDowntime : enhancedOddDowntime;
            yield return new WaitForSeconds(downtime);
            
            // Synchronized toggle is handled by NovaStar (master)
        }
    }

    // ==================== ORBIT COMPLETION CALLBACKS ====================

    /// <summary>
    /// Called by OrbitalStar when orbit completes and star goes offscreen
    /// </summary>
    public void OnOrbitComplete(ProjectileType starType, int level)
    {
        Debug.Log($"<color=lime>{starType} completed Level {level} orbit</color>");

        // Check if THIS specific star is in enhanced mode
        bool isThisStarEnhanced = (starType == ProjectileType.NovaStar && isNovaStarEnhanced) || 
                                   (starType == ProjectileType.DwarfStar && isDwarfStarEnhanced);
        
        if (isThisStarEnhanced)
        {
            // Enhanced mode - increment completion counter (regardless of synchronizeSpawns)
            if (starType == ProjectileType.NovaStar)
            {
                novaStarsCompletedCount++;
                novaStarWaitingForDowntime = false; // CRITICAL: Allow base coroutine to transition to enhanced
                Debug.Log($"<color=gold>NovaStar orbit complete! ({novaStarsCompletedCount}/{novaStarsActiveCount})</color>");
                Debug.Log($"<color=gold>  novaStarWaitingForDowntime set to FALSE - ready to transition</color>");
            }
            else if (starType == ProjectileType.DwarfStar)
            {
                dwarfStarsCompletedCount++;
                dwarfStarWaitingForDowntime = false; // CRITICAL: Allow base coroutine to transition to enhanced
                Debug.Log($"<color=gold>DwarfStar orbit complete! ({dwarfStarsCompletedCount}/{dwarfStarsActiveCount})</color>");
                Debug.Log($"<color=gold>  dwarfStarWaitingForDowntime set to FALSE - ready to transition</color>");
            }
        }
        else
        {
            // Base mode - start downtime
            float downtime = GetDowntimeForLevel(level);

            if (starType == ProjectileType.NovaStar)
            {
                novaStarNextSpawnTime = Time.time + downtime;
                novaStarWaitingForDowntime = false; // Ready to spawn after downtime
                ProgressNovaStarLevel(); // Progress to next odd level
                Debug.Log($"<color=orange>NovaStar downtime: {downtime}s, next level: {novaStarCurrentLevel}</color>");
            }
            else if (starType == ProjectileType.DwarfStar)
            {
                dwarfStarNextSpawnTime = Time.time + downtime;
                dwarfStarWaitingForDowntime = false;
                ProgressDwarfStarLevel(); // Progress to next even level
                Debug.Log($"<color=cyan>DwarfStar downtime: {downtime}s, next level: {dwarfStarCurrentLevel}</color>");
            }
        }
    }

    private float GetDowntimeForLevel(int level)
    {
        switch (level)
        {
            case 1: return level1Downtime;
            case 2: return level2Downtime;
            case 3: return level3Downtime;
            case 4: return level4Downtime;
            case 5: return level5Downtime;
            case 6: return level6Downtime;
            default: return level1Downtime;
        }
    }

    // ==================== LEVEL PROGRESSION ====================

    private void ProgressNovaStarLevel()
    {
        // NovaStar: Odd levels only (1 → 3 → 5 → loop)
        bool shouldLoop = false;
        if (novaStarPrefab != null)
        {
            NovaStar prefabStar = novaStarPrefab.GetComponent<NovaStar>();
            if (prefabStar != null) shouldLoop = prefabStar.loopLevels;
        }

        if (novaStarCurrentLevel == 1)
        {
            novaStarCurrentLevel = 3;
        }
        else if (novaStarCurrentLevel == 3)
        {
            novaStarCurrentLevel = 5;
        }
        else // Level 5
        {
            if (shouldLoop)
            {
                novaStarCurrentLevel = 1;
                Debug.Log($"<color=orange>NovaStar looping back to Level 1</color>");
            }
            else
            {
                novaStarCurrentLevel = 5; // Stay at 5
                Debug.Log($"<color=orange>NovaStar staying at Level 5</color>");
            }
        }
    }

    private void ProgressDwarfStarLevel()
    {
        // DwarfStar: Even levels only (2 → 4 → 6 → loop)
        bool shouldLoop = false;
        if (dwarfStarPrefab != null)
        {
            DwarfStar prefabStar = dwarfStarPrefab.GetComponent<DwarfStar>();
            if (prefabStar != null) shouldLoop = prefabStar.loopLevels;
        }

        if (dwarfStarCurrentLevel == 2)
        {
            dwarfStarCurrentLevel = 4;
        }
        else if (dwarfStarCurrentLevel == 4)
        {
            dwarfStarCurrentLevel = 6;
        }
        else // Level 6
        {
            if (shouldLoop)
            {
                dwarfStarCurrentLevel = 2;
                Debug.Log($"<color=cyan>DwarfStar looping back to Level 2</color>");
            }
            else
            {
                dwarfStarCurrentLevel = 6; // Stay at 6
                Debug.Log($"<color=cyan>DwarfStar staying at Level 6</color>");
            }
        }
    }

    // ==================== MODIFIER APPLICATION ====================
    
    /// <summary>
    /// Get the ProjectileCards for a star prefab
    /// </summary>
    private ProjectileCards GetCardForPrefab(GameObject prefab)
    {
        if (prefab == null) return null;
        
        // Check if prefab has a ProjectileCardTag component
        ProjectileCardTag tag = prefab.GetComponent<ProjectileCardTag>();
        if (tag != null && tag.card != null)
        {
            return tag.card;
        }
        
        // Fallback: Search through all ProjectileCards in the project
        // This is less efficient but ensures we find the card
        ProjectileCards[] allCards = Resources.FindObjectsOfTypeAll<ProjectileCards>();
        foreach (ProjectileCards card in allCards)
        {
            if (card.projectilePrefab == prefab)
            {
                return card;
            }
        }
        
        return null;
    }
    
    public CardModifierStats GetEffectiveStarModifiers(ProjectileCards card)
    {
        if (card == null || ProjectileCardModifiers.Instance == null)
        {
            return new CardModifierStats();
        }
        
        // Only use shared/averaged modifiers when:
        //   - synchronized spawns are enabled
        //   - BOTH stars are enhanced
        // With MEGASYNC active, we ALWAYS share modifiers regardless of the
        // current enhanced variant indices so that all four orbits truly share
        // the same stats. In non-MEGASYNC sync, we further require that both
        // stars are using the SAME non-zero enhanced variant so that only the
        // actively synchronized variant pair shares modifiers.
        if (!synchronizeSpawns || !isNovaStarEnhanced || !isDwarfStarEnhanced)
        {
            return ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        if (!megaSyncActive)
        {
            // Standard sync: both stars must be using the SAME non-zero enhanced variant
            if (novaVariantIndex <= 0 || dwarfVariantIndex <= 0 || novaVariantIndex != dwarfVariantIndex)
            {
                return ProjectileCardModifiers.Instance.GetCardModifiers(card);
            }
        }

        ProjectileCards novaCard = GetCardForPrefab(novaStarPrefab);
        ProjectileCards dwarfCard = GetCardForPrefab(dwarfStarPrefab);

        if (novaCard == null || dwarfCard == null)
        {
            return ProjectileCardModifiers.Instance.GetCardModifiers(card);
        }

        CardModifierStats novaStats = ProjectileCardModifiers.Instance.GetCardModifiers(novaCard);
        CardModifierStats dwarfStats = ProjectileCardModifiers.Instance.GetCardModifiers(dwarfCard);

        CardModifierStats shared = new CardModifierStats();
        shared.speedIncrease = 0.5f * (novaStats.speedIncrease + dwarfStats.speedIncrease);
        shared.sizeMultiplier = 0.5f * (novaStats.sizeMultiplier + dwarfStats.sizeMultiplier);
        shared.pierceCount = Mathf.RoundToInt(0.5f * (novaStats.pierceCount + dwarfStats.pierceCount));
        shared.pierceAccumulator = 0.5f * (novaStats.pierceAccumulator + dwarfStats.pierceAccumulator);
        shared.lifetimeIncrease = 0.5f * (novaStats.lifetimeIncrease + dwarfStats.lifetimeIncrease);
        shared.cooldownReductionPercent = 0.5f * (novaStats.cooldownReductionPercent + dwarfStats.cooldownReductionPercent);
        shared.cooldownMultiplier = 0.5f * (novaStats.cooldownMultiplier + dwarfStats.cooldownMultiplier);
        shared.manaCostReduction = 0.5f * (novaStats.manaCostReduction + dwarfStats.manaCostReduction);
        shared.damageFlat = 0.5f * (novaStats.damageFlat + dwarfStats.damageFlat);
        shared.damageMultiplier = 0.5f * (novaStats.damageMultiplier + dwarfStats.damageMultiplier);
        shared.projectileCount = Mathf.RoundToInt(0.5f * (novaStats.projectileCount + dwarfStats.projectileCount));
        shared.projectileCountAccumulator = 0.5f * (novaStats.projectileCountAccumulator + dwarfStats.projectileCountAccumulator);
        shared.damageRadiusIncrease = 0.5f * (novaStats.damageRadiusIncrease + dwarfStats.damageRadiusIncrease);
        shared.explosionRadiusBonus = 0.5f * (novaStats.explosionRadiusBonus + dwarfStats.explosionRadiusBonus);
        shared.explosionRadiusMultiplier = 0.5f * (novaStats.explosionRadiusMultiplier + dwarfStats.explosionRadiusMultiplier);
        shared.strikeZoneRadiusBonus = 0.5f * (novaStats.strikeZoneRadiusBonus + dwarfStats.strikeZoneRadiusBonus);
        shared.strikeZoneRadiusMultiplier = 0.5f * (novaStats.strikeZoneRadiusMultiplier + dwarfStats.strikeZoneRadiusMultiplier);
        shared.shieldHealthBonus = 0.5f * (novaStats.shieldHealthBonus + dwarfStats.shieldHealthBonus);
        shared.attackSpeedPercent = 0.5f * (novaStats.attackSpeedPercent + dwarfStats.attackSpeedPercent);
        shared.pullStrengthMultiplier = 0.5f * (novaStats.pullStrengthMultiplier + dwarfStats.pullStrengthMultiplier);

        return shared;
    }

    [ContextMenu("Debug Shared Star Modifiers")]
    public void DebugLogSharedStarModifiers()
    {
        if (ProjectileCardModifiers.Instance == null)
        {
            Debug.Log("<color=yellow>[OrbitalStarManager] Shared modifiers: ProjectileCardModifiers instance not found.</color>");
            return;
        }

        ProjectileCards novaCard = GetCardForPrefab(novaStarPrefab);
        ProjectileCards dwarfCard = GetCardForPrefab(dwarfStarPrefab);

        if (novaCard == null || dwarfCard == null)
        {
            Debug.Log("<color=yellow>[OrbitalStarManager] Shared modifiers: NovaStar or DwarfStar card not found.</color>");
            return;
        }

        CardModifierStats novaStats = ProjectileCardModifiers.Instance.GetCardModifiers(novaCard);
        CardModifierStats dwarfStats = ProjectileCardModifiers.Instance.GetCardModifiers(dwarfCard);
        CardModifierStats shared = GetEffectiveStarModifiers(novaCard);

        Debug.Log(
            $"<color=cyan>[OrbitalStarManager] Shared Star Modifiers</color> " +
            $"Nova(speed={novaStats.speedIncrease:F2}, radius={novaStats.damageRadiusIncrease:F2}, cd%={novaStats.cooldownReductionPercent:F2}) | " +
            $"Dwarf(speed={dwarfStats.speedIncrease:F2}, radius={dwarfStats.damageRadiusIncrease:F2}, cd%={dwarfStats.cooldownReductionPercent:F2}) | " +
            $"Shared(speed={shared.speedIncrease:F2}, radius={shared.damageRadiusIncrease:F2}, cd%={shared.cooldownReductionPercent:F2})"
        );
    }
    
    /// <summary>
    /// Apply projectile modifiers to a spawned NovaStar or DwarfStar
    /// </summary>
    private void ApplyModifiersToStar(GameObject starObj, ProjectileCards card, bool useSharedModifiersForThisInstance = true)
    {
        if (card == null) return;

        CardModifierStats modifiers = new CardModifierStats();
        if (ProjectileCardModifiers.Instance != null)
        {
            if (useSharedModifiersForThisInstance)
            {
                modifiers = GetEffectiveStarModifiers(card);
            }
            else
            {
                modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(card);
            }
        }
        
        // Check which type of star this is
        NovaStar novaStar = starObj.GetComponent<NovaStar>();
        DwarfStar dwarfStar = starObj.GetComponent<DwarfStar>();
        
        if (novaStar != null)
        {
            novaStar.useSharedModifiers = useSharedModifiersForThisInstance;
            
            // Apply modifiers to NovaStar
            if (modifiers.speedIncrease != 0f)
            {
                novaStar.baseOrbitSpeed += modifiers.speedIncrease;
                Debug.Log($"<color=orange>NovaStar: Applied speed modifier +{modifiers.speedIncrease:F2}  baseOrbitSpeed={novaStar.baseOrbitSpeed:F2}</color>");
            }
            
            if (modifiers.damageRadiusIncrease != 0f)
            {
                // Reset to the prefab's base damage radius so Initialize can apply
                // the shared damageRadiusIncrease exactly once.
                float baseDamageRadius = novaStar.damageRadius;
                novaStar.damageRadius = baseDamageRadius;
                Debug.Log($"<color=orange>NovaStar: Using base damageRadius={baseDamageRadius:F2}; shared modifier +{modifiers.damageRadiusIncrease:F2} will be applied in Initialize</color>");
            }
            
            if (modifiers.sizeMultiplier != 1f)
            {
                starObj.transform.localScale *= modifiers.sizeMultiplier;
                Debug.Log($"<color=orange>NovaStar: Applied size modifier {modifiers.sizeMultiplier:F2}x</color>");
            }
        }
        else if (dwarfStar != null)
        {
            dwarfStar.useSharedModifiers = useSharedModifiersForThisInstance;
            
            // Apply modifiers to DwarfStar
            if (modifiers.speedIncrease != 0f)
            {
                dwarfStar.baseOrbitSpeed += modifiers.speedIncrease;
                Debug.Log($"<color=cyan>DwarfStar: Applied speed modifier +{modifiers.speedIncrease:F2}  baseOrbitSpeed={dwarfStar.baseOrbitSpeed:F2}</color>");
            }
            
            if (modifiers.damageRadiusIncrease != 0f)
            {
                // Mirror NovaStar behaviour: reset to prefab base so Initialize uses
                // the shared damageRadiusIncrease once for both stars.
                float baseDamageRadius = dwarfStar.damageRadius;
                dwarfStar.damageRadius = baseDamageRadius;
                Debug.Log($"<color=cyan>DwarfStar: Using base damageRadius={baseDamageRadius:F2}; shared modifier +{modifiers.damageRadiusIncrease:F2} will be applied in Initialize</color>");
            }
            
            if (modifiers.sizeMultiplier != 1f)
            {
                starObj.transform.localScale *= modifiers.sizeMultiplier;
                Debug.Log($"<color=cyan>DwarfStar: Applied size modifier {modifiers.sizeMultiplier:F2}x</color>");
            }
        }
        
        // Tag the star with its card for later reference
        if (ProjectileCardModifiers.Instance != null)
        {
            ProjectileCardModifiers.Instance.TagProjectileWithCard(starObj, card);
        }
    }
    
    /// <summary>
    /// Update modifiers for all currently active stars
    /// Called when a new modifier is picked up during gameplay
    /// </summary>
    public void UpdateActiveStarModifiers()
    {
        // Get cards for both star types
        ProjectileCards novaCard = GetCardForPrefab(novaStarPrefab);
        ProjectileCards dwarfCard = GetCardForPrefab(dwarfStarPrefab);
        
        // Find all active NovaStar instances
        NovaStar[] activeNovaStars = FindObjectsOfType<NovaStar>();
        DwarfStar[] activeDwarfStars = FindObjectsOfType<DwarfStar>();
        
        Debug.Log($"<color=magenta>UpdateActiveStarModifiers: Found {activeNovaStars.Length} NovaStars and {activeDwarfStars.Length} DwarfStars</color>");
        
        // Update NovaStars
        foreach (NovaStar star in activeNovaStars)
        {
            if (novaCard != null && ProjectileCardModifiers.Instance != null)
            {
                CardModifierStats modifiers;
                if (star.useSharedModifiers)
                {
                    modifiers = GetEffectiveStarModifiers(novaCard);
                }
                else
                {
                    modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(novaCard);
                }

                // Use IInstantModifiable so ALL stats (radius, damage, size, speed, etc.)
                // are recalculated instantly based on the shared modifiers.
                IInstantModifiable modifiable = star.GetComponent<IInstantModifiable>();
                if (modifiable != null)
                {
                    modifiable.ApplyInstantModifiers(modifiers);
                    Debug.Log($"<color=orange> Updated active NovaStar '{star.name}' instant modifiers</color>");
                }
            }
        }
        
        // Update DwarfStars
        foreach (DwarfStar star in activeDwarfStars)
        {
            if (dwarfCard != null && ProjectileCardModifiers.Instance != null)
            {
                CardModifierStats modifiers;
                if (star.useSharedModifiers)
                {
                    modifiers = GetEffectiveStarModifiers(dwarfCard);
                }
                else
                {
                    modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(dwarfCard);
                }

                IInstantModifiable modifiable = star.GetComponent<IInstantModifiable>();
                if (modifiable != null)
                {
                    modifiable.ApplyInstantModifiers(modifiers);
                    Debug.Log($"<color=cyan> Updated active DwarfStar '{star.name}' instant modifiers</color>");
                }
            }
        }
    }

    // ==================== SPAWNING METHODS ====================

    private void SpawnNovaStar(int level)
    {
        for (int i = 0; i < starsPerLevel; i++)
        {
            float angleOffset = i * starAngularSpacing;
            GameObject star = Instantiate(novaStarPrefab, playerTransform.position, Quaternion.identity);
            star.name = $"NovaStar_L{level}_{i}";

            NovaStar novaStar = star.GetComponent<NovaStar>();
            if (novaStar != null)
            {
                // Apply modifiers BEFORE Initialize
                ProjectileCards card = GetCardForPrefab(novaStarPrefab);
                ApplyModifiersToStar(star, card);
                
                float orbitExtension = GetOrbitExtensionForLevel(level);
                float orbitTilt = GetOrbitTiltForLevel(level);
                novaStar.orbitTiltDegrees = orbitTilt;
                novaStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
            }
        }
    }

    private void SpawnDwarfStar(int level)
    {
        for (int i = 0; i < starsPerLevel; i++)
        {
            float angleOffset = i * starAngularSpacing;
            GameObject star = Instantiate(dwarfStarPrefab, playerTransform.position, Quaternion.identity);
            star.name = $"DwarfStar_L{level}_{i}";

            DwarfStar dwarfStar = star.GetComponent<DwarfStar>();
            if (dwarfStar != null)
            {
                // Apply modifiers BEFORE Initialize
                ProjectileCards card = GetCardForPrefab(dwarfStarPrefab);
                ApplyModifiersToStar(star, card);
                
                float orbitExtension = GetOrbitExtensionForLevel(level);
                float orbitTilt = GetOrbitTiltForLevel(level);
                dwarfStar.orbitTiltDegrees = orbitTilt;
                dwarfStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
            }
        }
    }

    private void SpawnNovaStarSynchronized(int[] levels)
    {
        foreach (int level in levels)
        {
            for (int i = 0; i < starsPerLevel; i++)
            {
                float angleOffset = i * starAngularSpacing;
                GameObject star = Instantiate(novaStarPrefab, playerTransform.position, Quaternion.identity);
                star.name = $"NovaStar_SYNC_L{level}_{i}";

                NovaStar novaStar = star.GetComponent<NovaStar>();
                if (novaStar != null)
                {
                    // Apply modifiers BEFORE Initialize
                    ProjectileCards card = GetCardForPrefab(novaStarPrefab);
                    ApplyModifiersToStar(star, card);
                    
                    // Enable synchronized speed for enhanced mode
                    novaStar.useSynchronizedSpeed = true;
                    float orbitExtension = GetOrbitExtensionForLevel(level);
                    float orbitTilt = GetOrbitTiltForLevel(level);
                    novaStar.orbitTiltDegrees = orbitTilt;
                    novaStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
                }
            }
        }
    }

    private void SpawnDwarfStarSynchronized(int[] levels)
    {
        foreach (int level in levels)
        {
            for (int i = 0; i < starsPerLevel; i++)
            {
                float angleOffset = i * starAngularSpacing;
                GameObject star = Instantiate(dwarfStarPrefab, playerTransform.position, Quaternion.identity);
                star.name = $"DwarfStar_SYNC_L{level}_{i}";

                DwarfStar dwarfStar = star.GetComponent<DwarfStar>();
                if (dwarfStar != null)
                {
                    // Apply modifiers BEFORE Initialize
                    ProjectileCards card = GetCardForPrefab(dwarfStarPrefab);
                    ApplyModifiersToStar(star, card);
                    
                    // Enable synchronized speed for enhanced mode
                    dwarfStar.useSynchronizedSpeed = true;
                    float orbitExtension = GetOrbitExtensionForLevel(level);
                    float orbitTilt = GetOrbitTiltForLevel(level);
                    dwarfStar.orbitTiltDegrees = orbitTilt;
                    dwarfStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
                }
            }
        }
    }

    private void SpawnNovaStarAtLevel(int level, float delayMultiplier)
    {
        // Get card for modifier application
        ProjectileCards novaCard = GetCardForPrefab(novaStarPrefab);
        
        for (int i = 0; i < starsPerLevel; i++)
        {
            float angleOffset = i * starAngularSpacing;
            GameObject star = Instantiate(novaStarPrefab, playerTransform.position, Quaternion.identity);
            star.name = $"NovaStar_ENH_L{level}_{i}";
            
            // Apply modifiers to star
            ApplyModifiersToStar(star, novaCard);

            NovaStar novaStar = star.GetComponent<NovaStar>();
            if (novaStar != null)
            {
                // Set speed mode based on synchronizeOrbitCompletion setting
                novaStar.useSynchronizedSpeed = synchronizeOrbitCompletion;
                
                // If not synchronized, add delay to orbit duration
                if (!synchronizeOrbitCompletion)
                {
                    novaStar.additionalOrbitDelay = delayMultiplier * enhancedLevelSpawnDelay;
                }
                
                float orbitExtension = GetOrbitExtensionForLevel(level);
                float orbitTilt = GetOrbitTiltForLevel(level);
                novaStar.orbitTiltDegrees = orbitTilt;
                novaStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
            }
            
            novaStarsActiveCount++;
        }
    }

    private void SpawnSingleNovaStarAtLevel(int level, float delayMultiplier)
    {
        int originalStarsPerLevel = starsPerLevel;
        starsPerLevel = 1;
        SpawnNovaStarAtLevel(level, delayMultiplier);
        starsPerLevel = originalStarsPerLevel;
    }

    private void SpawnDwarfStarAtLevel(int level, float delayMultiplier)
    {
        // Get card for modifier application
        ProjectileCards dwarfCard = GetCardForPrefab(dwarfStarPrefab);
        
        for (int i = 0; i < starsPerLevel; i++)
        {
            float angleOffset = i * starAngularSpacing;
            GameObject star = Instantiate(dwarfStarPrefab, playerTransform.position, Quaternion.identity);
            star.name = $"DwarfStar_ENH_L{level}_{i}";
            
            // Apply modifiers to star
            ApplyModifiersToStar(star, dwarfCard);

            DwarfStar dwarfStar = star.GetComponent<DwarfStar>();
            if (dwarfStar != null)
            {
                // Set speed mode based on synchronizeOrbitCompletion setting
                dwarfStar.useSynchronizedSpeed = synchronizeOrbitCompletion;
                
                // If not synchronized, add delay to orbit duration
                if (!synchronizeOrbitCompletion)
                {
                    dwarfStar.additionalOrbitDelay = delayMultiplier * enhancedLevelSpawnDelay;
                }
                
                float orbitExtension = GetOrbitExtensionForLevel(level);
                float orbitTilt = GetOrbitTiltForLevel(level);
                dwarfStar.orbitTiltDegrees = orbitTilt;
                dwarfStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
            }
            
            dwarfStarsActiveCount++;
        }
    }

    private void SpawnSingleDwarfStarAtLevel(int level, float delayMultiplier)
    {
        int originalStarsPerLevel = starsPerLevel;
        starsPerLevel = 1;
        SpawnDwarfStarAtLevel(level, delayMultiplier);
        starsPerLevel = originalStarsPerLevel;
    }

    // ==================== REVERSE SEQUENTIAL SPAWN METHODS ====================
    
    private void SpawnNovaStarAtLevelReverse(int level, float delayMultiplier)
    {
        // Get card for modifier application
        ProjectileCards novaCard = GetCardForPrefab(novaStarPrefab);
        
        for (int i = 0; i < starsPerLevel; i++)
        {
            float angleOffset = i * starAngularSpacing;
            GameObject star = Instantiate(novaStarPrefab, playerTransform.position, Quaternion.identity);
            star.name = $"NovaStar_REV_L{level}_{i}";
            
            // Apply modifiers to star
            ApplyModifiersToStar(star, novaCard);

            NovaStar novaStar = star.GetComponent<NovaStar>();
            if (novaStar != null)
            {
                // REVERSE SEQUENTIAL MODE: use the same synchronized-speed system
                // as enhanced mode. baseOrbitSpeed (after modifiers) represents
                // the effective Level 1 speed, and the star's internal
                // useSynchronizedSpeed logic derives per-level speeds so that all
                // levels share the same orbit completion time.

                novaStar.useSynchronizedSpeed = true;

                float orbitExtension = GetOrbitExtensionForLevel(level);
                float orbitTilt = GetOrbitTiltForLevel(level);
                novaStar.orbitTiltDegrees = orbitTilt;
                novaStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
            }
            
            novaStarsActiveCount++;
        }
    }

    private void SpawnSingleNovaStarAtLevelReverse(int level, float delayMultiplier)
    {
        int originalStarsPerLevel = starsPerLevel;
        starsPerLevel = 1;
        SpawnNovaStarAtLevelReverse(level, delayMultiplier);
        starsPerLevel = originalStarsPerLevel;
    }

    private void SpawnDwarfStarAtLevelReverse(int level, float delayMultiplier)
    {
        // Get card for modifier application
        ProjectileCards dwarfCard = GetCardForPrefab(dwarfStarPrefab);
        
        for (int i = 0; i < starsPerLevel; i++)
        {
            float angleOffset = i * starAngularSpacing;
            GameObject star = Instantiate(dwarfStarPrefab, playerTransform.position, Quaternion.identity);
            star.name = $"DwarfStar_REV_L{level}_{i}";
            
            // Apply modifiers to star
            ApplyModifiersToStar(star, dwarfCard);

            DwarfStar dwarfStar = star.GetComponent<DwarfStar>();
            if (dwarfStar != null)
            {
                // REVERSE SEQUENTIAL MODE: mirror NovaStar behaviour by enabling
                // the internal synchronized-speed system. baseOrbitSpeed (after
                // modifiers) is treated as the Level 1 speed, and each level's
                // actual orbit speed is derived from arc-length ratios so all
                // levels share the same completion time.

                dwarfStar.useSynchronizedSpeed = true;

                float orbitExtension = GetOrbitExtensionForLevel(level);
                float orbitTilt = GetOrbitTiltForLevel(level);
                dwarfStar.orbitTiltDegrees = orbitTilt;
                dwarfStar.Initialize(playerTransform, level, angleOffset, orbitExtension);
            }
            
            dwarfStarsActiveCount++;
        }
    }

    private void SpawnSingleDwarfStarAtLevelReverse(int level, float delayMultiplier)
    {
        int originalStarsPerLevel = starsPerLevel;
        starsPerLevel = 1;
        SpawnDwarfStarAtLevelReverse(level, delayMultiplier);
        starsPerLevel = originalStarsPerLevel;
    }
    
    private float GetRadiusForLevel(int level)
    {
        switch (level)
        {
            case 1: return level1Radius;
            case 2: return level2Radius;
            case 3: return level3Radius;
            case 4: return level4Radius;
            case 5: return level5Radius;
            case 6: return level6Radius;
            default: return level1Radius;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (playerTransform == null) return;

        DrawOrbitGizmo(1, level1Radius, GetOrbitExtensionForLevel(1), Color.yellow);
        DrawOrbitGizmo(2, level2Radius, GetOrbitExtensionForLevel(2), Color.green);
        DrawOrbitGizmo(3, level3Radius, GetOrbitExtensionForLevel(3), Color.cyan);
        DrawOrbitGizmo(4, level4Radius, GetOrbitExtensionForLevel(4), Color.blue);
        DrawOrbitGizmo(5, level5Radius, GetOrbitExtensionForLevel(5), Color.magenta);
        DrawOrbitGizmo(6, level6Radius, GetOrbitExtensionForLevel(6), Color.red);
    }

    private void DrawOrbitGizmo(int level, float radius, float orbitExtensionCenter, Color color)
    {
        Gizmos.color = color;

        float tilt = GetOrbitTiltForLevel(level);
        float halfTilt = tilt * 0.5f;
        float leftExtension = Mathf.Max(0f, orbitExtensionCenter - halfTilt);
        float rightExtension = Mathf.Max(0f, orbitExtensionCenter + halfTilt);

        float cwStartAngle = 180f + leftExtension;
        float cwEndAngle = -rightExtension;

        for (float angle = cwStartAngle; angle >= cwEndAngle; angle -= 5f)
        {
            float angleRad = angle * Mathf.Deg2Rad;
            Vector3 pos = playerTransform.position + new Vector3(
                Mathf.Cos(angleRad) * radius,
                Mathf.Sin(angleRad) * radius,
                0f
            );
            Gizmos.DrawSphere(pos, 0.15f);
        }

        Vector3 labelPos = playerTransform.position + new Vector3(0f, radius + 0.5f, 0f);
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(labelPos, $"L{level} (Ext: {orbitExtensionCenter:F0}°, Tilt: {tilt:F1}°)", new UnityEngine.GUIStyle() 
        { 
            normal = new UnityEngine.GUIStyleState() { textColor = color },
            fontSize = 12,
            fontStyle = UnityEngine.FontStyle.Bold
        });
        #endif
    }
}
