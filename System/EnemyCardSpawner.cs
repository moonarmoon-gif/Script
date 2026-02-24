using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages enemy spawning based on selected Enemy Cards.
/// Replaces EnemySpawner when enabled.
/// </summary>
public class EnemyCardSpawner : MonoBehaviour
{
    [Header("Spawning Control")]
    [Tooltip("Enable/disable enemy spawning and card selection completely")]
    public bool enableSpawning = true;
    
    [Header("Card Selection Settings")]
    [Tooltip("Number of enemy cards to show per selection")]
    public int cardsPerSelection = 3;
    
    [Tooltip("Number of times to show cards at game start (player picks this many cards total)")]
    public int initialSelectionCount = 2;
    
    [Tooltip("Time between card selections after initial picks (seconds)")]
    public float cardSelectionInterval = 30f;

    public float ReducedCardSelectionInterval = 0f;

    [Tooltip("Amount to DECREASE cardSelectionInterval AFTER each completed boss event (seconds). 0 = no change.")]
    public float decreaseCardSelectionInterval = 0f;

    [Tooltip("Minimum allowed time between card selections after all decreases (seconds). cardSelectionInterval will never go below this value.")]
    public float minimumCardSelectionInterval = 10f;
    
    [Tooltip("Delay before showing card UI (seconds)")]
    public float cardDisplayDelay = 0.5f;
    
    [Header("Spawn Bounds")]
    [Tooltip("Minimum spawn position (bottom-left corner)")]
    public Transform minPos;
    
    [Tooltip("Maximum spawn position (top-right corner)")]
    public Transform maxPos;
    
    [Tooltip("Percentage chance to spawn on side (vs top). 50 = 50% side, 50% top")]
    [Range(0f, 100f)]
    public float sideSpawnPercentage = 50f;
    
    [Header("Spawn Timing")]
    [Tooltip("Use individual spawn intervals from each EnemyCard (if false, uses baseSpawnInterval)")]
    public bool useIndividualSpawnIntervals = true;
    
    [Tooltip("Base spawn interval when not using individual intervals (seconds)")]
    public float baseSpawnInterval = 3f;
    
    [Header("Enemy Card Pool")]
    [Tooltip("All available enemy cards that can be selected")]
    public List<EnemyCards> availableEnemyCards = new List<EnemyCards>();

    [Header("First Card Spawn Boost")]
    [Tooltip("Spawn interval reduction (fraction) applied to the very first chosen enemy card for a short duration (e.g., 0.5 = -50%)")]
    [Range(0f, 1f)]
    public float firstCardSpawnIntervalReduction = 0.5f;

    [Tooltip("Duration in seconds that the first card spawn boost stays active")]
    public float firstCardBoostDuration = 5f;

    [Header("First Enemy Off-Camera Speed Boost")]
    [Tooltip("Move-speed multiplier applied to enemies spawned from the very first non-boss enemy card while they are off-camera. 1 = no boost.")]
    public float firstEnemyOffCameraSpeedMultiplier = 2f;

    [Tooltip("Maximum duration in seconds that the off-camera speed boost can stay active for a spawned enemy.")]
    public float firstEnemyOffCameraBoostDuration = 3f;

    public float MoveSpeedOffCamersOffset = 0.15f;
    
    [Header("Card Count Spawn Scaling")]
    [Tooltip("Additional spawn interval multiplier per extra enemy card (e.g., 0.5 = +50% per extra card)")]
    public float perExtraCardSpawnIntervalFactor = 0.5f;

    [Header("Rarity Scaling Over Time")]
    [Tooltip("Enable time-based rarity scaling")]
    public bool useTimeBasedRarity = true;
    
    [Tooltip("Allow enemies to spawn at higher rarity than their base rarity (DISABLED = strict rarity enforcement)")]
    public bool allowHigherRaritySpawn = false;
    
    [Header("Card Selection Rules")]
    [Tooltip("Allow already selected cards to appear again in future selections")]
    public bool allowDuplicateSelections = false;
    
    [Header("Boss Card System")]
    [Tooltip("Enable boss card system")]
    public bool enableBossCards = true;

    [System.Serializable]
    public class BossSpawnTiming
    {
        [Tooltip("Time in seconds when this boss event triggers.")]
        public float time = 300f;

        [Tooltip("Health multiplier applied to the boss for this event AFTER EnemyScalingSystem scaling (1 = no change).")]
        public float bossHealthMultiplier = 1f;

        [Tooltip("EXP reward multiplier applied to the boss for this event AFTER EnemyScalingSystem scaling (1 = no change).")]
        public float bossExpMultiplier = 1f;

        [Tooltip("Attack damage multiplier applied to the boss for this event AFTER any other damage scaling (1 = no change).")]
        public float bossDamageMultiplier = 1f;
    }

    [Tooltip("Per-boss spawn settings. Index 0 = first boss event, 1 = second, etc.")]
    public List<BossSpawnTiming> bossSpawnTimings = new List<BossSpawnTiming>
    {
        new BossSpawnTiming { time = 300f },
        new BossSpawnTiming { time = 480f },
        new BossSpawnTiming { time = 660f }
    };
    
    [Tooltip("Grace window in seconds where a boss spawn timing suppresses normal enemy card selections (to avoid clashes)")]
    public float bossCardClashGraceWindow = 1f;
    
    [Tooltip("Buffer time after all enemies are cleared before showing boss cards (seconds)")]
    public float bossCardBufferTime = 3f;
    
    [Tooltip("Menace timer - boss is immune and projectiles don't spawn (seconds)")]
    public float bossMenaceTimer = 5.5f;

    [Tooltip("Duration over which player projectiles should fade away at boss event start.")]
    public float FadeAwayDuration = 0.5f;
    
    [Tooltip("Projectile cooldown reduction after menace timer ends (0.5 = 50% reduction)")]
    [Range(0f, 1f)]
    public float projectileCooldownReduction = 0.5f;

    [Tooltip("Delay after boss card is chosen before the boss actually spawns (seconds)")]
    public float bossSpawnBreatherTime = 1f;

    [Tooltip("Multiplier applied to NON-BOSS enemy spawn intervals WHILE a boss event is active (applied LAST).")]
    public float bossEventSpawnIntervalMultiplier = 2f;

    [Tooltip("If false, normal enemy-card-based spawns stay DISABLED for the entire duration of a boss event. If true, card spawns resume after the menace timer ends (current behaviour). This only affects enemies spawned via EnemyCards, not summons or other systems.")]
    public bool allowCardEnemySpawnsDuringBossEvent = true;

    [Header("Post-Boss Refill")]
    [Tooltip("Delay after the post-boss enemy card is chosen before starting smooth health/mana refill (seconds)")]
    public float postBossRefillDelay = 0.5f;

    [Tooltip("Duration over which health and mana should smoothly refill to max (seconds)")]
    public float postBossRefillDuration = 5f;

    [Header("Post-Boss Camera Scaling")]
    [Tooltip("Enable automatic camera zoom-out after each completed boss event.")]
    public bool enableCameraGrowthAfterBoss = true;

    [Tooltip("Amount to increase Camera.main.orthographicSize after each completed boss event.")]
    public float cameraSizeIncreasePerBoss = 1f;

    [Tooltip("Maximum number of boss events that can increase camera size.")]
    public int maxCameraSizeIncrements = 4;

    public float CameraResizeTime = 1f;

    // Tracks how many times we've already increased camera size this run
    private int currentCameraSizeIncrements = 0;

    private Coroutine cameraResizeRoutine = null;
    
    // Boss system tracking
    private int currentBossIndex = 0;
    private bool isBossEventActive = false;
    private bool waitingForEnemyClear = false;
    private bool waitingForBossDefeat = false;
    private GameObject currentBossEnemy = null;
    private EnemyCards currentBossCard = null;
    private float currentBossHealthMultiplier = 1f;
    private float currentBossExpMultiplier = 1f;
    private float currentBossDamageMultiplier = 1f;
    private float currentBossMenaceTimer = 0f;
    private float currentBossDeathRewardTimer = 0f;
    private float bossEventTimer = 0f;
    private bool cardSelectionTimerPaused = false;
    private bool showingPostBossCards = false; // Prevent boss timer during post-boss card selection
    private bool isBossCardSelectionActive = false; // True only while showing boss cards in a boss event
    private bool postBossRefillDelayActive = false;
    private float pausedCardSelectionTimer = 0f;
    
    // EXP from the boss itself (queued so level-up cards appear after cleanup)
    private float queuedBossExpReward = 0f;

    // Track boss menace and death state
    private bool bossMenaceActive = false;   // True while boss menace timer is running (no enemy spawns allowed)
    private bool bossDeathTriggered = false; // True once boss EnemyHealth.OnDeath has fired
    private bool bossDeathCleanupInProgress = false; // True only during post-boss death cleanup (mass kill EXP window)
    // Suppress normal enemy spawns from the moment the boss event starts until the menace timer begins
    private bool suppressEnemySpawnsBeforeBoss = false;

    // Optional: force at least one spawn per registered card immediately after menace ends
    public bool forceImmediateSpawnAfterMenace = false;
    
    // Time thresholds for rarity changes (in seconds)
    [System.Serializable]
    public class RarityTimeThreshold
    {
        public float timeThreshold = 60f; // Time in seconds
        public float commonChance = 80f;
        public float uncommonChance = 15f;
        public float rareChance = 5f;
        public float epicChance = 0f;
        public float legendaryChance = 0f;
        public float mythicChance = 0f;
    }
    
    [Tooltip("Rarity chances at different time thresholds - FULLY CUSTOMIZABLE")]
    public List<RarityTimeThreshold> rarityThresholds = new List<RarityTimeThreshold>();
    
    // Registered enemy cards (cards that have been selected) - stores RUNTIME COPIES
    private List<EnemyCards> registeredEnemyCards = new List<EnemyCards>();

    // History of enemy-card rarities in the current "cycle" (used to build
    // Favour-card rarity weights). This is reset after the post-boss Favour
    // selection so that only enemy cards picked in the new phase are counted.
    private List<CardRarity> enemyRarityHistory = new List<CardRarity>();
    
    // Track spawn timers for each registered card (for individual spawn intervals)
    private Dictionary<EnemyCards, float> cardSpawnTimers = new Dictionary<EnemyCards, float>();
    
    // Track which cards have been selected to prevent duplicates
    private HashSet<EnemyCards> selectedCards = new HashSet<EnemyCards>();
    
    // Track initial selections
    private int initialSelectionsCompleted = 0;

    // Track the very first non-boss enemy card chosen during the INITIAL game-start selections only
    private EnemyCards firstRegisteredCard = null;
    private float firstCardBoostEndTime = 0f;
    private bool initialSelectionPhaseActive = false;
    private bool firstCardBoostAssigned = false;

    private float firstEnemyOffCameraBoostWindowEndTime = -1f;
    
    // Game start time and rarity-scaling elapsed time (excludes time spent in boss events)
    private float gameStartTime = 0f;
    private float rarityElapsedTime = 0f;
    
    // Spawn tracking
    private float spawnTimer = 0f;
    private bool lastSpawnedOnLeft = false;
    private float oppositeSpawnBonus = 0.0f;
    private const float OppositeSpawnBaseChance = 75.0f;
    private const float OppositeSpawnIncrease = 5.0f;

    // Expose whether we are currently in a boss-card selection (used by EnemyCards)
    public bool IsBossCardSelectionActive
    {
        get { return isBossCardSelectionActive; }
    }

    public bool IsBossDeathCleanupInProgress
    {
        get { return bossDeathCleanupInProgress; }
    }

    public bool IsPostBossRefillDelayActive
    {
        get { return postBossRefillDelayActive; }
    }
    
    public bool IsBossEventActive
    {
        get { return isBossEventActive; }
    }
    
    public GameObject CurrentBossEnemy
    {
        get { return currentBossEnemy; }
    }
    
    private void Start()
    {
        gameStartTime = GameStateManager.PauseSafeTime;
        rarityElapsedTime = 0f;
        
        // Initialize default rarity thresholds if none set
        if (rarityThresholds.Count == 0)
        {
            InitializeDefaultRarityThresholds();
        }
        
        // CRITICAL: Only start card selections if spawning is enabled
        if (enableSpawning)
        {
            StartCoroutine(GameStartSelectionSequence());
            Debug.Log("<color=lime>Enemy Spawning ENABLED - Starting card selections</color>");
        }
        else
        {
            Debug.Log("<color=red>Enemy Spawning DISABLED - No card selections or spawning will occur</color>");
        }
    }
    
    private IEnumerator GameStartSelectionSequence()
    {
        CardSelectionManager manager = CardSelectionManager.Instance;
        if (manager == null)
        {
            manager = FindObjectOfType<CardSelectionManager>();
        }

        if (manager != null)
        {
            yield return manager.ShowInitialActiveProjectileSelection(3);
        }

        yield return StartCoroutine(InitialCardSelectionRoutine());
    }
    
    private void Update()
    {
        // Check if spawning is enabled
        if (!enableSpawning)
        {
            return;
        }

        float dt = GameStateManager.GetPauseSafeDeltaTime();
        
        // Advance rarity-scaling timer only while no boss event or post-boss card selection is active
        if (useTimeBasedRarity && !isBossEventActive && !showingPostBossCards)
        {
            rarityElapsedTime += dt;
        }
        
        // Boss event timer (always runs, but pauses during the entire boss event and post-boss card selection)
        if (enableBossCards && !isBossEventActive && !showingPostBossCards && currentBossIndex < bossSpawnTimings.Count)
        {
            bossEventTimer += dt;

            BossSpawnTiming timing = bossSpawnTimings[currentBossIndex];

            // Check if it's time for next boss
            if (bossEventTimer >= timing.time)
            {
                // Clamp timer exactly to this boss timing so it effectively freezes during the event
                bossEventTimer = timing.time;

                Debug.Log($"<color=gold>╔═══════════════════════════════════════════════════════════╗</color>");
                Debug.Log($"<color=gold>║ BOSS EVENT TRIGGERED!</color>");
                Debug.Log($"<color=gold>║ Timer: {bossEventTimer:F1}s >= Timing: {timing.time:F1}s</color>");
                Debug.Log($"<color=gold>║ Boss Index: {currentBossIndex}</color>");
                Debug.Log($"<color=gold>║ isBossEventActive: {isBossEventActive}</color>");
                Debug.Log($"<color=gold>║ showingPostBossCards: {showingPostBossCards}</color>");
                Debug.Log($"<color=gold>╚═══════════════════════════════════════════════════════════╝</color>");

                // Cache per-event boss stat multipliers so they apply regardless of which boss card is chosen
                currentBossHealthMultiplier = timing.bossHealthMultiplier;
                currentBossExpMultiplier = timing.bossExpMultiplier;
                currentBossDamageMultiplier = timing.bossDamageMultiplier;

                // Advance to the next boss index immediately so this timing can't retrigger
                currentBossIndex++;

                // CRITICAL: Suppress normal enemy spawns IMMEDIATELY from the moment the
                // boss event is triggered, to prevent any enemies from slipping in between
                // this frame and the first frame of BossEventSequence.
                suppressEnemySpawnsBeforeBoss = true;

                StartCoroutine(BossEventSequence());
            }
        }
        
        // Don't spawn enemies while we are explicitly clearing enemies before the boss event,
        // while the boss menace timer is running, while boss cards are being shown, while
        // boss death cleanup is in progress, or while the boss intro/buffer phase is active.
        // Optionally, we can also keep card-based spawns disabled for the ENTIRE duration
        // of a boss event via allowCardEnemySpawnsDuringBossEvent.
        bool blockDueToBossEvent = !allowCardEnemySpawnsDuringBossEvent && isBossEventActive;
        if (waitingForEnemyClear || bossMenaceActive || isBossCardSelectionActive || bossDeathTriggered || suppressEnemySpawnsBeforeBoss || blockDueToBossEvent)
        {
            return;
        }
        
        // Only spawn if we have registered cards
        if (registeredEnemyCards.Count == 0)
        {
            return;
        }
        
        // Stop spawning if player is dead
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            return;
        }
        
        // Spawn enemies based on mode
        if (useIndividualSpawnIntervals)
        {
            // Each enemy has its own spawn timer
            SpawnEnemiesIndividually();
        }
        else
        {
            // All enemies share one spawn timer
            spawnTimer += dt;
            if (spawnTimer >= baseSpawnInterval)
            {
                Debug.Log($"<color=lime>Shared timer spawn! Timer: {spawnTimer:F2}s >= Interval: {baseSpawnInterval:F2}s</color>");
                spawnTimer = 0f;
                SpawnRandomEnemy();
            }
        }
    }
    
    private void InitializeDefaultRarityThresholds()
    {
        // Under 1 minute
        rarityThresholds.Add(new RarityTimeThreshold
        {
            timeThreshold = 60f,
            commonChance = 80f,
            uncommonChance = 15f,
            rareChance = 5f,
            epicChance = 0f,
            legendaryChance = 0f,
            mythicChance = 0f
        });
        
        // 1-2 minutes
        rarityThresholds.Add(new RarityTimeThreshold
        {
            timeThreshold = 120f,
            commonChance = 60f,
            uncommonChance = 20f,
            rareChance = 15f,
            epicChance = 5f,
            legendaryChance = 0f,
            mythicChance = 0f
        });
        
        // 2-3 minutes
        rarityThresholds.Add(new RarityTimeThreshold
        {
            timeThreshold = 180f,
            commonChance = 40f,
            uncommonChance = 25f,
            rareChance = 20f,
            epicChance = 10f,
            legendaryChance = 5f,
            mythicChance = 0f
        });
        
        // 3+ minutes
        rarityThresholds.Add(new RarityTimeThreshold
        {
            timeThreshold = float.MaxValue,
            commonChance = 20f,
            uncommonChance = 25f,
            rareChance = 25f,
            epicChance = 15f,
            legendaryChance = 10f,
            mythicChance = 5f
        });
    }
    
    private IEnumerator InitialCardSelectionRoutine()
    {
        // Show cards multiple times at game start
        for (int i = 0; i < initialSelectionCount; i++)
        {
            // Use UNSCALED time for delay (in case game is paused)
            yield return new WaitForSecondsRealtime(cardDisplayDelay);
            initialSelectionPhaseActive = true;
            ShowEnemyCardSelection();
            
            Debug.Log($"<color=cyan>Initial selection {i + 1}/{initialSelectionCount} shown</color>");
            
            // Wait for player to select a card (check more frequently)
            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
            {
                yield return null; // Check every frame
            }
            initialSelectionPhaseActive = false;
            
            initialSelectionsCompleted++;
            Debug.Log($"<color=green>Initial selection {initialSelectionsCompleted}/{initialSelectionCount} completed</color>");
            
            // Small delay between selections
            yield return new WaitForSecondsRealtime(0.1f);
        }
        
        Debug.Log($"<color=lime>All {initialSelectionCount} initial selections complete! Starting periodic spawning...</color>");
        
        // CRITICAL FIX: Force unpause game after initial selections
        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection && !GameStateManager.ManualPauseActive)
        {
            Time.timeScale = 1f;
            Debug.Log($"<color=lime>Forced game unpause after initial enemy card selections</color>");
        }
        
        // After initial selections, start periodic card selection
        StartCoroutine(PeriodicCardSelectionRoutine());
    }

    private IEnumerator PeriodicCardSelectionRoutine()
    {
        float timer = 0f;

        while (true)
        {
            // If spawning was disabled at runtime, just wait without advancing the timer
            if (!enableSpawning)
            {
                yield return null;
                continue;
            }

            // Don't advance timer while card selection timer is explicitly paused
            if (!cardSelectionTimerPaused)
            {
                timer += GameStateManager.GetPauseSafeDeltaTime();
            }

            // Time to consider showing a normal enemy card selection (with a
            // preceding Favour-card selection when applicable)
            if (timer >= cardSelectionInterval)
            {
                bool skippedDueToBossClash = false;
                bool didNormalEnemySelection = false;

                // If a boss is about to spawn (within grace window), skip this normal selection entirely
                if (enableBossCards && currentBossIndex < bossSpawnTimings.Count)
                {
                    float timeToNextBoss = bossSpawnTimings[currentBossIndex].time - bossEventTimer;
                    if (Mathf.Abs(timeToNextBoss) <= bossCardClashGraceWindow)
                    {
                        Debug.Log($"<color=yellow>Skipping normal enemy card selection due to upcoming boss spawn (Δt={timeToNextBoss:F2}s ≤ {bossCardClashGraceWindow:F2}s)</color>");
                        skippedDueToBossClash = true;
                    }
                }

                if (!skippedDueToBossClash)
                {
                    // Only show normal enemy cards (and their preceding Favour
                    // cards) if boss event is not active AND not showing
                    // post-boss cards.
                    if (!isBossEventActive && !showingPostBossCards)
                    {
                        // STEP A: Show FAVOUR cards BEFORE the regular enemy
                        // card selection, but only once we have at least one
                        // previously chosen enemy card to build a rarity
                        // history from. This legacy enemy-based Favour flow
                        // is now explicitly gated by UseFavourSoulSystem so
                        // that disabling the Favour system also disables
                        // these pre-enemy Favour choices.
                        if (CardSelectionManager.Instance != null &&
                            CardSelectionManager.Instance.UseFavourSoulSystem &&
                            enemyRarityHistory.Count > 0 &&
                            !CardSelectionManager.Instance.HasPendingLevelUpStages())
                        {
                            Debug.Log("<color=cyan>Periodic selection: Showing FAVOUR cards before enemy cards...</color>");
                            CardSelectionManager.Instance.ShowFavourCards(enemyRarityHistory, false, 3);

                            // Wait for player to pick a favour card
                            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
                            {
                                yield return null;
                            }

                            // Force unpause after favour selection if CardSelectionManager paused the game
                            if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection && !GameStateManager.ManualPauseActive)
                            {
                                Time.timeScale = 1f;
                            }
                        }

                        // STEP B: After favour cards (or if none were shown),
                        // show the normal ENEMY card selection, unless
                        // level-up stages are pending.
                        if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.HasPendingLevelUpStages())
                        {
                            Debug.Log("<color=yellow>Skipping enemy card selection because level-up stages are pending after favour selection.</color>");
                        }
                        else
                        {
                            ShowEnemyCardSelection();
                            didNormalEnemySelection = true;

                            // Wait for player to select a card
                            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
                            {
                                yield return null;
                            }

                            // Force unpause after selection if CardSelectionManager paused the game
                            if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection && !GameStateManager.ManualPauseActive)
                            {
                                Time.timeScale = 1f;
                                Debug.Log("<color=lime>Forced game unpause after periodic enemy card selection</color>");
                            }
                        }
                    }
                }

                // If we actually showed a normal enemy selection (not skipped due to boss clash),
                // optionally tighten the interval for the NEXT periodic selection.
                if (didNormalEnemySelection && ReducedCardSelectionInterval > 0f)
                {
                    float oldInterval = cardSelectionInterval;
                    cardSelectionInterval = Mathf.Max(minimumCardSelectionInterval, cardSelectionInterval - ReducedCardSelectionInterval);
                    Debug.Log($"<color=cyan>Periodic selection complete: cardSelectionInterval {oldInterval:F1}s → {cardSelectionInterval:F1}s (min {minimumCardSelectionInterval:F1}s)</color>");
                }

                // Reset timer regardless of whether we actually showed a selection
                timer = 0f;
            }

            yield return null;
        }
    }
    
    private void ShowEnemyCardSelection()
    {
        // CRITICAL: Check if spawning is enabled
        if (!enableSpawning)
        {
            Debug.Log("<color=red>Enemy Spawning DISABLED - Skipping card selection</color>");
            return;
        }
        
        if (CardSelectionManager.Instance == null)
        {
            Debug.LogError("<color=red>CardSelectionManager not found! Cannot show enemy card selection.</color>");
            return;
        }
        
        // Never show enemy cards while there are pending core/projectile level-up
        // stages. This prevents enemy card selections from interleaving with
        // level-up (core/projectile) choices.
        if (CardSelectionManager.Instance.HasPendingLevelUpStages())
        {
            Debug.Log("<color=yellow>Skipping enemy card selection because level-up stages are pending.</color>");
            return;
        }
        
        // Get available cards (with duplicate prevention)
        List<EnemyCards> pool = availableEnemyCards
            .Where(card => card != null && card.rarity != CardRarity.Boss)
            .ToList();
        Debug.Log($"<color=cyan>Boss filter: {pool.Count} non-boss cards available</color>");

        // Filter by duplicate prevention
        if (!allowDuplicateSelections)
        {
            pool = pool.Where(card => !selectedCards.Contains(card)).ToList();
            Debug.Log($"<color=cyan>Duplicate prevention: {pool.Count} cards after excluding {selectedCards.Count} selected</color>");
        }

        if (pool.Count == 0)
        {
            Debug.LogWarning("<color=yellow>No enemy cards available matching current criteria!</color>");
            return;
        }

        // Precompute time-based rarity threshold and weights if enabled
        bool useThreshold = useTimeBasedRarity && rarityThresholds.Count > 0;
        RarityTimeThreshold currentThreshold = null;
        float wCommon = 0f, wUncommon = 0f, wRare = 0f, wEpic = 0f, wLegendary = 0f, wMythic = 0f;

        if (useThreshold)
        {
            float elapsedTime = rarityElapsedTime;

            List<RarityTimeThreshold> sortedThresholds = rarityThresholds
                .OrderBy(t => t.timeThreshold)
                .ToList();

            // Default to the last threshold for times beyond the final bound
            currentThreshold = sortedThresholds[sortedThresholds.Count - 1];
            for (int i = 0; i < sortedThresholds.Count; i++)
            {
                if (elapsedTime <= sortedThresholds[i].timeThreshold)
                {
                    currentThreshold = sortedThresholds[i];
                    break;
                }
            }

            wCommon    = Mathf.Max(0f, currentThreshold.commonChance);
            wUncommon  = Mathf.Max(0f, currentThreshold.uncommonChance);
            wRare      = Mathf.Max(0f, currentThreshold.rareChance);
            wEpic      = Mathf.Max(0f, currentThreshold.epicChance);
            wLegendary = Mathf.Max(0f, currentThreshold.legendaryChance);
            wMythic    = Mathf.Max(0f, currentThreshold.mythicChance);
        }

        CardRarity[] rarityOrder = new[]
        {
            CardRarity.Common,
            CardRarity.Uncommon,
            CardRarity.Rare,
            CardRarity.Epic,
            CardRarity.Legendary,
            CardRarity.Mythic
        };

        System.Func<CardRarity, float> getWeight = rarity =>
        {
            if (!useThreshold) return 1f;
            switch (rarity)
            {
                case CardRarity.Common:    return wCommon;
                case CardRarity.Uncommon:  return wUncommon;
                case CardRarity.Rare:      return wRare;
                case CardRarity.Epic:      return wEpic;
                case CardRarity.Legendary: return wLegendary;
                case CardRarity.Mythic:    return wMythic;
                default: return 0f;
            }
        };

        System.Func<CardRarity> rollTargetRarity = () =>
        {
            if (!useThreshold)
            {
                // When thresholds are disabled, let fallback logic choose based on pool content
                return CardRarity.Common;
            }

            float totalWeight = wCommon + wUncommon + wRare + wEpic + wLegendary + wMythic;
            if (totalWeight <= 0f)
            {
                return CardRarity.Common;
            }

            float roll = Random.Range(0f, totalWeight);
            float cumulative = 0f;

            cumulative += wCommon;
            if (roll < cumulative) return CardRarity.Common;

            cumulative += wUncommon;
            if (roll < cumulative) return CardRarity.Uncommon;

            cumulative += wRare;
            if (roll < cumulative) return CardRarity.Rare;

            cumulative += wEpic;
            if (roll < cumulative) return CardRarity.Epic;

            cumulative += wLegendary;
            if (roll < cumulative) return CardRarity.Legendary;

            return CardRarity.Mythic;
        };

        System.Func<CardRarity, EnemyCards> pickCardForRarity = targetRarity =>
        {
            int targetIndex = System.Array.IndexOf(rarityOrder, targetRarity);
            if (targetIndex < 0) targetIndex = 0;

            // 1) Try target and HIGHER rarities with >0 chance
            System.Collections.Generic.List<CardRarity> higherWithChance = new System.Collections.Generic.List<CardRarity>();
            if (useThreshold)
            {
                for (int idx = targetIndex; idx < rarityOrder.Length; idx++)
                {
                    CardRarity r = rarityOrder[idx];
                    if (getWeight(r) > 0f) higherWithChance.Add(r);
                }
            }
            else
            {
                for (int idx = targetIndex; idx < rarityOrder.Length; idx++)
                {
                    higherWithChance.Add(rarityOrder[idx]);
                }
            }

            foreach (CardRarity r in higherWithChance)
            {
                var candidates = pool.Where(c => c.rarity == r).ToList();
                if (candidates.Count > 0)
                {
                    return candidates[Random.Range(0, candidates.Count)];
                }
            }

            // 2) Then try target and HIGHER rarities with 0% configured chance (last resort within high bucket)
            if (useThreshold)
            {
                System.Collections.Generic.List<CardRarity> higherZeroChance = new System.Collections.Generic.List<CardRarity>();
                for (int idx = targetIndex; idx < rarityOrder.Length; idx++)
                {
                    CardRarity r = rarityOrder[idx];
                    if (getWeight(r) <= 0f) higherZeroChance.Add(r);
                }

                foreach (CardRarity r in higherZeroChance)
                {
                    var candidates = pool.Where(c => c.rarity == r).ToList();
                    if (candidates.Count > 0)
                    {
                        return candidates[Random.Range(0, candidates.Count)];
                    }
                }
            }

            // 3) Finally, fall back to LOWER rarities as a last option
            for (int idx = targetIndex - 1; idx >= 0; idx--)
            {
                CardRarity r = rarityOrder[idx];
                var candidates = pool.Where(c => c.rarity == r).ToList();
                if (candidates.Count > 0)
                {
                    return candidates[Random.Range(0, candidates.Count)];
                }
            }

            return null;
        };

        // Randomly select cards to show
        List<BaseCard> cardsToShow = new List<BaseCard>();
        int cardsToSelect = Mathf.Min(cardsPerSelection, pool.Count);
        
        for (int i = 0; i < cardsToSelect; i++)
        {
            if (pool.Count == 0)
            {
                break;
            }

            CardRarity targetRarity = rollTargetRarity();
            EnemyCards originalCard = pickCardForRarity(targetRarity);
            if (originalCard == null)
            {
                Debug.LogWarning("<color=yellow>No suitable enemy card found for current rarity request; stopping selection loop.</color>");
                break;
            }

            // Remove from pool so it cannot appear again in this selection
            pool.Remove(originalCard);
            
            // CRITICAL: Create runtime copy to avoid modifying ScriptableObject
            EnemyCards runtimeCopy = Instantiate(originalCard);
            runtimeCopy.originalCard = originalCard; // Track original for duplicate prevention
            
            // CRITICAL FIX: Only assign time-based rarity if enabled, otherwise keep base rarity
            if (useTimeBasedRarity)
            {
                CardRarity timeBasedRarity = GetRarityForCurrentTime();
                
                if (allowHigherRaritySpawn)
                {
                    // Can spawn at higher rarity than base
                    if (timeBasedRarity >= originalCard.rarity)
                    {
                        runtimeCopy.rarity = timeBasedRarity;
                    }
                    else
                    {
                        runtimeCopy.rarity = originalCard.rarity; // Use base rarity if time-based is lower
                    }
                }
                else
                {
                    // STRICT: Can only spawn at base rarity
                    runtimeCopy.rarity = originalCard.rarity;
                }
            }
            else
            {
                // Time-based rarity disabled: Always use base rarity from inspector
                runtimeCopy.rarity = originalCard.rarity;
            }
            
            Debug.Log($"<color=yellow>Enemy Card: {originalCard.cardName} - Base Rarity: {originalCard.rarity}, Spawned as: {runtimeCopy.rarity}, TimeBasedRarity: {useTimeBasedRarity}</color>");
            
            cardsToShow.Add(runtimeCopy);
        }
        
        // Show cards in UI
        CardSelectionManager.Instance.ShowCards(cardsToShow);
        Debug.Log($"<color=cyan>Showing {cardsToShow.Count} enemy cards for selection</color>");
    }
    
    private CardRarity GetRarityForCurrentTime()
    {
        if (!useTimeBasedRarity || rarityThresholds.Count == 0)
        {
            return CardRarity.Common;
        }
        
        // Use boss-safe rarityElapsedTime which PAUSES during boss events and
        // post-boss card selection, so rarity does not advance while bosses are active.
        float elapsedTime = rarityElapsedTime;
        
        // Treat thresholds as UPPER BOUNDS: choose the first threshold whose timeThreshold >= elapsedTime.
        // Use an ordered copy to avoid mutating the inspector list.
        List<RarityTimeThreshold> sortedThresholds = rarityThresholds
            .OrderBy(t => t.timeThreshold)
            .ToList();

        // Default to the last threshold for times beyond the final bound
        RarityTimeThreshold currentThreshold = sortedThresholds[sortedThresholds.Count - 1];

        for (int i = 0; i < sortedThresholds.Count; i++)
        {
            if (elapsedTime <= sortedThresholds[i].timeThreshold)
            {
                currentThreshold = sortedThresholds[i];
                break;
            }
        }

        Debug.Log($"<color=yellow>Time: {elapsedTime:F2}s, Using threshold: {currentThreshold.timeThreshold}s</color>");
        
        float wCommon    = Mathf.Max(0f, currentThreshold.commonChance);
        float wUncommon  = Mathf.Max(0f, currentThreshold.uncommonChance);
        float wRare      = Mathf.Max(0f, currentThreshold.rareChance);
        float wEpic      = Mathf.Max(0f, currentThreshold.epicChance);
        float wLegendary = Mathf.Max(0f, currentThreshold.legendaryChance);
        float wMythic    = Mathf.Max(0f, currentThreshold.mythicChance);

        float totalWeight = wCommon + wUncommon + wRare + wEpic + wLegendary + wMythic;
        if (totalWeight <= 0f)
        {
            return CardRarity.Common;
        }

        float roll = Random.Range(0f, totalWeight);
        float cumulative = 0f;

        cumulative += wCommon;
        if (roll < cumulative) return CardRarity.Common;

        cumulative += wUncommon;
        if (roll < cumulative) return CardRarity.Uncommon;

        cumulative += wRare;
        if (roll < cumulative) return CardRarity.Rare;

        cumulative += wEpic;
        if (roll < cumulative) return CardRarity.Epic;

        cumulative += wLegendary;
        if (roll < cumulative) return CardRarity.Legendary;

        // If we get here, it must be Mythic (since totalWeight > 0)
        return CardRarity.Mythic;
    }
    
    /// <summary>
    /// Register an enemy card when it's selected by the player
    /// </summary>
    public void RegisterEnemyCard(EnemyCards card)
    {
        if (!registeredEnemyCards.Contains(card))
        {
            registeredEnemyCards.Add(card);

            if (EnemyScalingSystem.Instance != null)
            {
                EnemyScalingSystem.Instance.RegisterPerWaveHealthIncreaseStep();
            }
            
            // Track rarity in the history used to build Favour-card rarity
            // weights for this phase.
            enemyRarityHistory.Add(card.rarity);
            
            // Track ORIGINAL card for duplicate prevention (not runtime copy)
            if (!allowDuplicateSelections && card.originalCard != null)
            {
                selectedCards.Add(card.originalCard);
                Debug.Log($"<color=cyan>Tracking original card '{card.originalCard.cardName}' to prevent duplicates</color>");
            }
            
            // Initialize spawn timer for this card
            if (useIndividualSpawnIntervals)
            {
                cardSpawnTimers[card] = 0f;
            }

            // FIRST CARD BOOST: Apply ONLY to the very first non-boss card chosen during the
            // INITIAL game-start selections. Cards chosen later (periodic or post-boss) do NOT
            // receive this temporary spawn interval reduction.
            if (card.rarity != CardRarity.Boss && firstRegisteredCard == null && initialSelectionPhaseActive && !firstCardBoostAssigned)
            {
                firstRegisteredCard = card;
                firstCardBoostEndTime = GameStateManager.PauseSafeTime + firstCardBoostDuration;
                firstCardBoostAssigned = true;
                firstEnemyOffCameraBoostWindowEndTime = -1f;
                Debug.Log($"<color=magenta>First INITIAL enemy card '{card.cardName}' registered - applying {firstCardSpawnIntervalReduction * 100f:F0}% spawn interval reduction for {firstCardBoostDuration:F1}s</color>");
            }
            
            Debug.Log($"<color=green>Registered enemy card: {card.cardName} (Rarity: {card.rarity}, Spawn Interval: {card.runtimeSpawnInterval}s, Total Cards: {registeredEnemyCards.Count})</color>");
        }
    }
    
    /// <summary>
    /// Get a random enemy prefab from registered cards, weighted by rarity spawn chances
    /// </summary>
    public GameObject GetRandomEnemyPrefab()
    {
        if (registeredEnemyCards.Count == 0)
        {
            Debug.LogWarning("<color=yellow>No enemy cards registered! Cannot spawn enemies.</color>");
            return null;
        }
        
        // For now, simple random selection
        // TODO: Add rarity-based weighting if needed
        int randomIndex = Random.Range(0, registeredEnemyCards.Count);
        return registeredEnemyCards[randomIndex].enemyPrefab;
    }
    
    /// <summary>
    /// Check if any enemy cards have been registered
    /// </summary>
    public bool HasRegisteredCards()
    {
        return registeredEnemyCards.Count > 0;
    }
    
    /// <summary>
    /// Get all registered enemy cards
    /// </summary>
    public List<EnemyCards> GetRegisteredCards()
    {
        return new List<EnemyCards>(registeredEnemyCards);
    }
    
    /// <summary>
    /// Spawn enemies individually based on their own spawn intervals
    /// </summary>
    private void SpawnEnemiesIndividually()
    {
        // Update each card's spawn timer
        List<EnemyCards> cardsToSpawn = new List<EnemyCards>();
        
        foreach (var card in registeredEnemyCards)
        {
            if (!cardSpawnTimers.ContainsKey(card))
            {
                cardSpawnTimers[card] = 0f;
                Debug.Log($"<color=cyan>Initialized spawn timer for {card.cardName}</color>");
            }
            
            cardSpawnTimers[card] += GameStateManager.GetPauseSafeDeltaTime();

            // Compute effective spawn interval with all active mechanics
            float effectiveInterval = GetEffectiveSpawnInterval(card);

            // Check if this card is ready to spawn
            if (cardSpawnTimers[card] >= effectiveInterval)
            {
                Debug.Log($"<color=lime>Card {card.cardName} ready to spawn! Timer: {cardSpawnTimers[card]:F2}s >= Interval: {effectiveInterval:F2}s</color>");
                cardSpawnTimers[card] = 0f;
                cardsToSpawn.Add(card);
            }
        }
        
        // Spawn enemies for cards that are ready
        foreach (var card in cardsToSpawn)
        {
            SpawnEnemyFromCard(card);
        }
    }
    
    /// <summary>
    /// Spawn a random enemy from registered cards (for shared timer mode)
    /// </summary>
    private void SpawnRandomEnemy()
    {
        if (registeredEnemyCards.Count == 0) return;
        
        int randomIndex = Random.Range(0, registeredEnemyCards.Count);
        EnemyCards card = registeredEnemyCards[randomIndex];
        SpawnEnemyFromCard(card);
    }
    
    /// <summary>
    /// Spawn enemy from specific card
    /// </summary>
    private void SpawnEnemyFromCard(EnemyCards card)
    {
        if (card.enemyPrefab == null)
        {
            Debug.LogError($"<color=red>EnemyCard '{card.cardName}' has no enemy prefab assigned!</color>");
            return;
        }
        
        Vector2 spawnPosition = GetSpawnPosition();
        GameObject spawnedEnemy = Instantiate(card.enemyPrefab, spawnPosition, Quaternion.identity);

        // Tag this enemy with the rarity of the card that spawned it so
        // other systems (Favours, etc.) can know if it was a Boss or
        // non-boss enemy and what rarity it corresponds to.
        EnemyCardTag[] tags = spawnedEnemy.GetComponentsInChildren<EnemyCardTag>(true);
        if (tags == null || tags.Length == 0)
        {
            EnemyCardTag created = spawnedEnemy.AddComponent<EnemyCardTag>();
            tags = new EnemyCardTag[] { created };
        }

        for (int i = 0; i < tags.Length; i++)
        {
            if (tags[i] != null)
            {
                tags[i].rarity = card.rarity;
            }
        }

        // Optional: give the very first non-boss enemy-card enemies an off-camera
        // move-speed boost so they reach the battlefield faster. The boost is
        // removed automatically once the enemy enters the damageable camera
        // area (OffscreenDamageChecker) or the per-enemy duration expires.
        if (card.rarity != CardRarity.Boss &&
            firstRegisteredCard != null &&
            card == firstRegisteredCard &&
            firstEnemyOffCameraSpeedMultiplier > 1f &&
            firstEnemyOffCameraBoostDuration > 0f)
        {
            StatusController status = spawnedEnemy.GetComponent<StatusController>() ?? spawnedEnemy.GetComponentInChildren<StatusController>();
            if (status != null)
            {
                if (firstEnemyOffCameraBoostWindowEndTime < 0f)
                {
                    firstEnemyOffCameraBoostWindowEndTime = GameStateManager.PauseSafeTime + firstEnemyOffCameraBoostDuration;
                }

                float remaining = firstEnemyOffCameraBoostWindowEndTime - GameStateManager.PauseSafeTime;
                if (remaining > 0f)
                {
                    status.ApplyOffCameraSpeedBoost(firstEnemyOffCameraSpeedMultiplier, remaining, MoveSpeedOffCamersOffset);
                }
            }
        }

        Debug.Log($"<color=green>EnemyCardSpawner: Spawned {card.enemyPrefab.name} from card '{card.cardName}' (Rarity: {card.rarity}) at {spawnPosition}</color>");
    }

    /// <summary>
    /// Force an immediate spawn from all registered enemy cards (used right after menace timer)
    /// </summary>
    private void ForceImmediateEnemySpawnsAfterMenace()
    {
        if (registeredEnemyCards.Count == 0)
        {
            Debug.Log("<color=yellow>No registered enemy cards to force spawn after menace timer.</color>");
            return;
        }

        int spawnCount = 0;
        foreach (var card in registeredEnemyCards)
        {
            SpawnEnemyFromCard(card);
            spawnCount++;

            if (useIndividualSpawnIntervals)
            {
                // Reset this card's spawn timer so the normal cadence resumes cleanly
                cardSpawnTimers[card] = 0f;
            }
        }

        Debug.Log($"<color=lime>Forced spawn of {spawnCount} enemies after menace timer (one per registered card).</color>");
    }
    
    /// <summary>
    /// Get spawn position using minPos/maxPos bounds (same as EnemySpawner)
    /// </summary>
    private Vector2 GetSpawnPosition()
    {
        if (minPos == null || maxPos == null)
        {
            Debug.LogError("<color=red>EnemyCardSpawner: minPos or maxPos not assigned!</color>");
            return Vector2.zero;
        }
        
        float minX = minPos.position.x;
        float maxX = maxPos.position.x;
        float minY = minPos.position.y;
        float maxY = maxPos.position.y;
        
        // Randomly choose spawn side with alternating logic
        float randomSide = Random.Range(0f, 100f);
        bool shouldSpawnOpposite = false;

        if (lastSpawnedOnLeft && randomSide < (OppositeSpawnBaseChance + oppositeSpawnBonus))
        {
            shouldSpawnOpposite = true;
        }
        else if (!lastSpawnedOnLeft && randomSide < (OppositeSpawnBaseChance + oppositeSpawnBonus))
        {
            shouldSpawnOpposite = true;
        }

        bool thisSpawnOnLeft;
        if ((lastSpawnedOnLeft && shouldSpawnOpposite) || (!lastSpawnedOnLeft && !shouldSpawnOpposite))
        {
            thisSpawnOnLeft = false;
        }
        else
        {
            thisSpawnOnLeft = true;
        }

        if (thisSpawnOnLeft == lastSpawnedOnLeft)
        {
            oppositeSpawnBonus += OppositeSpawnIncrease;
        }
        else
        {
            oppositeSpawnBonus = 0.0f;
        }

        lastSpawnedOnLeft = thisSpawnOnLeft;
        
        Vector2 spawnPosition;
        float weightedRandom = Random.Range(0f, 100f);
        
        if (thisSpawnOnLeft)
        {
            if (weightedRandom < sideSpawnPercentage)
            {
                // Spawn on left side
                spawnPosition = new Vector2(minX, Random.Range(minY, maxY));
            }
            else
            {
                // Spawn on top-left
                spawnPosition = new Vector2(Random.Range(minX, (minX + maxX) / 2f), maxY);
            }
        }
        else
        {
            if (weightedRandom < sideSpawnPercentage)
            {
                // Spawn on right side
                spawnPosition = new Vector2(maxX, Random.Range(minY, maxY));
            }
            else
            {
                // Spawn on top-right
                spawnPosition = new Vector2(Random.Range((minX + maxX) / 2f, maxX), maxY);
            }
        }
        
        return spawnPosition;
    }
    
    // ==================== BOSS EVENT SYSTEM ====================
    
    private IEnumerator BossEventSequence()
    {
        Debug.Log("<color=gold>═══════════════════════════════════════════════════════════</color>");
        Debug.Log("<color=gold>           BOSS EVENT SEQUENCE STARTED</color>");
        Debug.Log("<color=gold>═══════════════════════════════════════════════════════════</color>");
        
        isBossEventActive = true;
        bossDeathTriggered = false;
        Debug.Log($"<color=red>SET isBossEventActive = TRUE (Boss event starting)</color>");
        cardSelectionTimerPaused = true;
        waitingForEnemyClear = true;
        
        // Pause enemy scaling system
        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.PauseScaling();
        }
        
        Debug.Log("<color=yellow>Waiting for all enemies to be cleared...</color>");
        
        while (true)
        {
            // Count all alive enemies (excluding boss)
            GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
            int aliveCount = 0;
            
            foreach (GameObject enemy in enemies)
            {
                IDamageable damageable = enemy.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    aliveCount++;
                }
            }
            
            if (aliveCount == 0)
            {
                Debug.Log("<color=lime>All enemies cleared!</color>");
                break;
            }
            
            yield return GameStateManager.WaitForPauseSafeSeconds(0.5f);
        }
        
        waitingForEnemyClear = false;

        // Inform OrbitalStarManager that a boss event is starting so it can
        // pause NovaStar/DwarfStar spawn cycles before we clear projectiles.
        OrbitalStarManager orbitalManager = FindObjectOfType<OrbitalStarManager>();
        if (orbitalManager != null)
        {
            orbitalManager.OnBossEventStart();
        }

        // STEP 2: Destroy all player projectiles (including NovaStar/DwarfStar)
        Debug.Log("<color=red>Destroying all player projectiles...</color>");
        DestroyAllPlayerProjectiles();
        
        // STEP 3: Make projectiles unspawnable and disable autofire
        Debug.Log("<color=red>Making projectiles unspawnable and disabling autofire...</color>");
        SetProjectilesSpawnable(false);
        SetAutoFireEnabled(false);
        
        // STEP 4: Buffer time
        Debug.Log($"<color=cyan>Buffer time: {bossCardBufferTime}s...</color>");
        yield return GameStateManager.WaitForPauseSafeSeconds(bossCardBufferTime);
        
        // STEP 5A: Show pre-boss FAVOUR card selection based on enemy rarity
        // history, if any enemy cards have been picked this phase. This is
        // also gated by UseFavourSoulSystem so it can be fully disabled via
        // the Favour system toggle.
        if (CardSelectionManager.Instance != null &&
            CardSelectionManager.Instance.UseFavourSoulSystem &&
            enemyRarityHistory.Count > 0)
        {
            Debug.Log("<color=cyan>Boss event: Showing pre-boss FAVOUR cards...</color>");
            CardSelectionManager.Instance.ShowFavourCards(enemyRarityHistory, false, 3);

            // Wait for player to select favour card
            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
            {
                yield return null;
            }

            if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection && !GameStateManager.ManualPauseActive)
            {
                Time.timeScale = 1f;
            }
        }
        
        // STEP 5B: Show boss card selection
        Debug.Log("<color=gold>Showing BOSS card selection...</color>");
        isBossCardSelectionActive = true;
        ShowBossCardSelection();
        
        // Wait for player to select boss card
        while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            yield return null;
        }
        
        isBossCardSelectionActive = false;
        Debug.Log("<color=lime>Boss card selected!</color>");

        // Optional breather before boss actually appears
        if (bossSpawnBreatherTime > 0f)
        {
            Debug.Log($"<color=cyan>Boss spawn breather: {bossSpawnBreatherTime:F2}s</color>");
            yield return GameStateManager.WaitForPauseSafeSeconds(bossSpawnBreatherTime);
        }
        
        // STEP 6: Spawn boss enemy
        if (currentBossEnemy != null)
        {
            Debug.Log("<color=gold>Spawning BOSS enemy...</color>");
            SpawnBossEnemy(currentBossEnemy);
        }
        
        // STEP 7: Menace timer - boss is immune and enemies MUST NOT spawn during this period
        float menaceDuration = currentBossMenaceTimer > 0f ? currentBossMenaceTimer : bossMenaceTimer;
        Debug.Log($"<color=magenta>MENACE TIMER: {menaceDuration}s - Boss is IMMUNE!</color>");
        float menaceElapsed = 0f;
        // From this point on, bossMenaceActive handles spawn suppression instead of the pre-boss flag
        suppressEnemySpawnsBeforeBoss = false;
        bossMenaceActive = true;
        FireMine.SetBossPauseActive(true);
        
        while (menaceElapsed < menaceDuration)
        {
            menaceElapsed += GameStateManager.GetPauseSafeDeltaTime();
            yield return null;
        }
        
        bossMenaceActive = false;
        FireMine.SetBossPauseActive(false);
        Debug.Log("<color=lime>Menace timer complete! Boss is now vulnerable!</color>");
        
        // STEP 8: Reset projectile cooldowns and reduce by 50%
        Debug.Log("<color=cyan>Resetting and reducing projectile cooldowns...</color>");
        ResetAndReduceProjectileCooldowns();
        
        // STEP 9: Make projectiles spawnable again and re-enable autofire
        Debug.Log("<color=lime>Projectiles are now SPAWNABLE and autofire re-enabled!</color>");
        SetProjectilesSpawnable(true);
        SetAutoFireEnabled(true);

        // Restart orbital star cycles (NovaStar/DwarfStar) after boss cleared
        // projectiles and menace has ended. Also signal that the boss event
        // has ended from the perspective of orbital spawning so they can
        // resume normal behaviour.
        OrbitalStarManager starManager = FindObjectOfType<OrbitalStarManager>();
        if (starManager != null)
        {
            starManager.OnBossEventEnd();
            starManager.RestartAllStarCycles();
        }
        
        // STEP 9.5: Resume enemy spawning (after menace timer)
        Debug.Log("<color=lime>Resuming enemy spawning after menace timer...</color>");
        
        // NOTE: Boss event REMAINS ACTIVE so that the boss timer stays paused.
        // Enemy spawning is now controlled only by waitingForEnemyClear.
        Debug.Log($"<color=lime>Boss event still active (timer paused). registeredEnemyCards.Count={registeredEnemyCards.Count}</color>");
        Debug.Log("<color=lime>ENEMIES SHOULD NOW BE SPAWNING!</color>");

        // Optionally force at least one spawn per registered enemy card immediately
        // after menace ends. This is disabled by default for more predictable behavior.
        // When allowCardEnemySpawnsDuringBossEvent is FALSE, we must also suppress this
        // forced burst so that absolutely no card-based spawns occur as the menace
        // timer ends.
        if (forceImmediateSpawnAfterMenace && allowCardEnemySpawnsDuringBossEvent)
        {
            ForceImmediateEnemySpawnsAfterMenace();
        }
        
        // STEP 11: Wait for boss death and an optional reward timer window.
        Debug.Log("<color=yellow>Waiting for boss to be defeated...</color>");

        // First wait until the boss EnemyHealth death event fires.
        while (!bossDeathTriggered)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(0.1f);
        }

        float rewardWindow = currentBossDeathRewardTimer;
        if (rewardWindow > 0f)
        {
            float elapsedReward = 0f;
            while (elapsedReward < rewardWindow)
            {
                elapsedReward += GameStateManager.GetPauseSafeDeltaTime();
                yield return null;
            }
        }

        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.IncreaseMaxProjectileLimitAfterBoss();
        }
        
        Debug.Log("<color=lime>╔═══════════════════════════════════════════════════════════╗</color>");
        Debug.Log("<color=lime>║              BOSS DEFEATED!                              ║</color>");
        Debug.Log("<color=lime>╚═══════════════════════════════════════════════════════════╝</color>");
        
        // STEP 11.5: Wait until ALL remaining enemies (killed on boss death) finish their death animations
        // and are fully destroyed before showing post-boss enemy cards.
        Debug.Log("<color=yellow>Waiting for all remaining enemies to finish death animations...</color>");
        yield return null;
        
        // Grant any EXP queued from the boss itself now that all death
        // animations are finished, while bossDeathCleanupInProgress is still
        // true so level-up cards are correctly delayed until after cleanup.
        GrantQueuedBossExperience();
        
        // STEP 12: Clear registered enemy cards (they will respawn after new card selection)
        Debug.Log("<color=cyan>Clearing registered enemy cards (will respawn after new selection)...</color>");
        registeredEnemyCards.Clear();
        cardSpawnTimers.Clear();

        // Also clear duplicate-prevention so all enemy cards become selectable again
        selectedCards.Clear();

        // Reset first-card spawn boost state so the next very first enemy card picked after
        // the boss behaves like a fresh run.
        firstRegisteredCard = null;
        firstCardBoostEndTime = 0f;
        firstEnemyOffCameraBoostWindowEndTime = -1f;
        
        // STEP 13: Resume enemy scaling and apply any post-boss bonuses
        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.ResumeScaling();

            // Apply an optional health boost that uses the existing
            // healthIncreasePercent + stacked additionalHealthIncreasePercent
            // logic, then reset the spawn-interval multiplier and register this
            // boss completion so the global flat spawn-interval reduction
            // (GlobalEnemySpawnerIntervalReduction) takes effect for all future
            // enemy-card spawns, including the post-boss card chosen below.
            EnemyScalingSystem.Instance.ApplyBonusHealthIncreaseAfterBoss();
            EnemyScalingSystem.Instance.ResetSpawnIntervalMultiplierForNextPhase();
            EnemyScalingSystem.Instance.OnBossEventCompleted();
        }
        
        // Boss death cleanup (mass EXP from boss + killed enemies) is now finished
        bossDeathCleanupInProgress = false;
        
        // STEP 14: Resume card selection timer
        // Optionally tighten the normal enemy-card selection interval after each
        // completed boss event. This only affects FUTURE periodic selections and
        // never goes below the configured minimum.
        if (decreaseCardSelectionInterval > 0f)
        {
            float oldInterval = cardSelectionInterval;
            cardSelectionInterval = Mathf.Max(minimumCardSelectionInterval, cardSelectionInterval - decreaseCardSelectionInterval);
            Debug.Log($"<color=cyan>Boss event complete: cardSelectionInterval {oldInterval:F1}s → {cardSelectionInterval:F1}s (min {minimumCardSelectionInterval:F1}s)</color>");
        }

        cardSelectionTimerPaused = false;
        
        if (CardSelectionManager.Instance != null)
        {
            // Allow any LevelUp coroutines waiting on IsBossDeathCleanupInProgress
            // to enqueue their pending level-up stages before we wait on them.
            yield return null;
            
            while (CardSelectionManager.Instance.HasPendingLevelUpStages())
            {
                yield return null;
            }
        }

        // STEP 15A: After all level-up stages are resolved, show a special
        // post-boss FAVOUR selection that is forced to BOSS rarity. This is
        // the ONLY way to obtain Boss-rarity Favour cards, and is also
        // gated by UseFavourSoulSystem so it respects the global Favour
        // system toggle.
        if (CardSelectionManager.Instance != null &&
            CardSelectionManager.Instance.UseFavourSoulSystem)
        {
            Debug.Log("<color=cyan>Boss event: Showing post-boss BOSS FAVOUR cards...</color>");
            CardSelectionManager.Instance.ShowFavourCards(enemyRarityHistory, true, 3);

            // Wait for player to select favour card
            while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
            {
                yield return null;
            }

            if (CardSelectionManager.Instance != null && CardSelectionManager.Instance.pauseGameOnSelection && !GameStateManager.ManualPauseActive)
            {
                Time.timeScale = 1f;
            }
        }

        // After the post-boss Favour selection, RESET the enemy rarity history
        // so that only enemy cards chosen in the new phase (starting with the
        // post-boss enemy card below) are counted for future Favour odds.
        enemyRarityHistory.Clear();

        // STEP 15B: Show 3 new enemy cards (respecting rarity scaling)
        Debug.Log("<color=gold>Showing post-boss enemy card selection...</color>");
        showingPostBossCards = true; // PREVENT boss timer from running during card selection
        isBossEventActive = true; // ALSO prevent boss timer check from passing
        Debug.Log($"<color=red>SET isBossEventActive = TRUE (Showing post-boss cards)</color>");
        yield return GameStateManager.WaitForPauseSafeSeconds(0.5f);
        ShowEnemyCardSelection();
        
        // Wait for player to select enemy card
        while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.IsSelectionActive())
        {
            yield return null;
        }
        
        Debug.Log("<color=lime>Post-boss enemy card selected!</color>");
        showingPostBossCards = false; // Resume boss timer
        isBossEventActive = false; // Allow boss timer to run again
        Debug.Log($"<color=red>SET isBossEventActive = FALSE (Post-boss cards done)</color>");
        FireMine.SetBossPauseActive(false);

        RefillPlayerHealthAndMana();
        
        // Boss index was already advanced in Update when the event triggered.
        // Just log what the next boss timing will be.
        float nextBossTiming = currentBossIndex < bossSpawnTimings.Count ? bossSpawnTimings[currentBossIndex].time : float.MaxValue;
        // Allow enemy spawning again for the next phase (boss death cleanup is over)
        bossDeathTriggered = false;
        Debug.Log($"<color=gold>Boss event complete! Next boss index: {currentBossIndex}, timer at {bossEventTimer:F1}s, next boss at {nextBossTiming:F1}s</color>");

        // FINAL STEP: Optionally grow the camera size after a fully completed boss event
        TryIncreaseCameraSizeAfterBoss();
    }

    /// <summary>
    /// After each fully completed boss event (boss dead, level-up resolved, and
    /// post-boss enemy card selected), optionally increase the camera's
    /// orthographic size by a fixed amount, up to a maximum number of times.
    /// This gradually zooms the camera out over successive bosses.
    /// </summary>
    private void TryIncreaseCameraSizeAfterBoss()
    {
        if (!enableCameraGrowthAfterBoss)
        {
            return;
        }

        if (currentCameraSizeIncrements >= maxCameraSizeIncrements)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("<color=yellow>EnemyCardSpawner: Cannot grow camera size - Camera.main is null.</color>");
            return;
        }

        if (cameraSizeIncreasePerBoss <= 0f)
        {
            return;
        }

        float oldSize = cam.orthographicSize;
        float targetSize = oldSize + cameraSizeIncreasePerBoss;
        currentCameraSizeIncrements++;

        float resizeTime = Mathf.Max(0f, CameraResizeTime);
        if (resizeTime <= 0f)
        {
            cam.orthographicSize = targetSize;
            Debug.Log($"<color=cyan>BossEvent Camera Growth: orthographicSize {oldSize:F2} → {cam.orthographicSize:F2} (increment {currentCameraSizeIncrements}/{maxCameraSizeIncrements})</color>");
            return;
        }

        if (cameraResizeRoutine != null)
        {
            StopCoroutine(cameraResizeRoutine);
            cameraResizeRoutine = null;
        }

        cameraResizeRoutine = StartCoroutine(ResizeCameraSizeRoutine(cam, targetSize, resizeTime, oldSize));
    }

    private IEnumerator ResizeCameraSizeRoutine(Camera cam, float targetSize, float duration, float loggedOldSize)
    {
        if (cam == null)
        {
            cameraResizeRoutine = null;
            yield break;
        }

        float startSize = cam.orthographicSize;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (cam == null)
            {
                cameraResizeRoutine = null;
                yield break;
            }

            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            cam.orthographicSize = Mathf.Lerp(startSize, targetSize, t);
            yield return null;
        }

        if (cam != null)
        {
            cam.orthographicSize = targetSize;
            Debug.Log($"<color=cyan>BossEvent Camera Growth: orthographicSize {loggedOldSize:F2} → {cam.orthographicSize:F2} (increment {currentCameraSizeIncrements}/{maxCameraSizeIncrements})</color>");
        }

        cameraResizeRoutine = null;
    }
    
    public void QueueBossExperience(int amount)
    {
        QueueBossExperience((float)amount);
    }

    public void QueueBossExperience(float amount)
    {
        if (amount <= 0f) return;
        queuedBossExpReward += amount;
    }

    private void GrantQueuedBossExperience()
    {
        if (queuedBossExpReward <= 0f) return;

        PlayerLevel playerLevel = null;

        if (AdvancedPlayerController.Instance != null)
        {
            playerLevel = AdvancedPlayerController.Instance.GetComponent<PlayerLevel>();
        }
        else if (PlayerController.Instance != null)
        {
            playerLevel = PlayerController.Instance.GetComponent<PlayerLevel>();
        }

        if (playerLevel != null)
        {
            playerLevel.GainExperience(queuedBossExpReward);
            Debug.Log($"<color=cyan>Granted queued boss EXP: {queuedBossExpReward}</color>");
        }

        queuedBossExpReward = 0f;
    }

    private void RefillPlayerHealthAndMana()
    {
        GameObject playerObject = null;

        if (AdvancedPlayerController.Instance != null)
        {
            playerObject = AdvancedPlayerController.Instance.gameObject;
        }
        else if (PlayerController.Instance != null)
        {
            playerObject = PlayerController.Instance.gameObject;
        }

        if (playerObject == null)
        {
            return;
        }

        StartCoroutine(SmoothRefillPlayerHealthAndMana(playerObject));
    }

    private IEnumerator SmoothRefillPlayerHealthAndMana(GameObject playerObject)
    {
        while (CardSelectionManager.Instance != null && CardSelectionManager.Instance.HasPendingLevelUpStages())
        {
            yield return null;
        }

        postBossRefillDelayActive = postBossRefillDelay > 0f;
        if (postBossRefillDelay > 0f)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(postBossRefillDelay);
        }
        postBossRefillDelayActive = false;

        if (playerObject == null)
        {
            yield break;
        }

        PlayerHealth health = playerObject.GetComponent<PlayerHealth>();
        PlayerMana mana = playerObject.GetComponent<PlayerMana>();
        PlayerStats stats = playerObject.GetComponent<PlayerStats>();

        float duration = Mathf.Max(0.1f, postBossRefillDuration);

        float originalHealthRegenPerSecond = 0f;
        float originalManaRegenPerSecond = 0f;

        bool hasStats = stats != null;
        if (hasStats)
        {
            originalHealthRegenPerSecond = stats.healthRegenPerSecond;
            originalManaRegenPerSecond = stats.manaRegenPerSecond;
        }

        float missingHealth = (health != null) ? (health.MaxHealth - health.CurrentHealth) : 0f;
        float missingMana = (mana != null) ? (mana.MaxMana - mana.CurrentMana) : 0f;

        if (hasStats)
        {
            if (missingHealth > 0f)
            {
                stats.healthRegenPerSecond += missingHealth / duration;
            }

            if (missingMana > 0f)
            {
                stats.manaRegenPerSecond += missingMana / duration;
            }
        }

        yield return GameStateManager.WaitForPauseSafeSeconds(duration);

        if (hasStats)
        {
            stats.healthRegenPerSecond = originalHealthRegenPerSecond;
            stats.manaRegenPerSecond = originalManaRegenPerSecond;
        }

        if (health != null && health.CurrentHealth < health.MaxHealth)
        {
            health.Heal(health.MaxHealth - health.CurrentHealth);
        }

        if (mana != null && mana.CurrentMana < mana.MaxMana)
        {
            mana.AddMana(mana.MaxMana - mana.CurrentMana);
        }

        Debug.Log("<color=cyan>Post-boss: Smooth health and mana refill complete.</color>");
    }

    private void ShowBossCardSelection()
    {
        if (CardSelectionManager.Instance == null)
        {
            Debug.LogError("<color=red>CardSelectionManager not found!</color>");
            return;
        }
        
        // Get only BOSS rarity cards
        List<EnemyCards> bossCards = availableEnemyCards.Where(card => card.rarity == CardRarity.Boss).ToList();
        
        if (bossCards.Count == 0)
        {
            Debug.LogError("<color=red>No Boss cards available!</color>");
            return;
        }
        
        // Select 3 random boss cards
        List<BaseCard> cardsToShow = new List<BaseCard>();
        int cardsToSelect = Mathf.Min(3, bossCards.Count);
        
        for (int i = 0; i < cardsToSelect; i++)
        {
            int randomIndex = Random.Range(0, bossCards.Count);
            EnemyCards originalCard = bossCards[randomIndex];
            
            // Create runtime copy
            EnemyCards runtimeCopy = Instantiate(originalCard);
            runtimeCopy.originalCard = originalCard;
            
            cardsToShow.Add(runtimeCopy);
            bossCards.RemoveAt(randomIndex); // Prevent duplicates in this selection
        }
        
        Debug.Log($"<color=gold>Showing {cardsToShow.Count} BOSS cards</color>");
        // Use CardSelectionManager's direct ShowCards API (no callback overload exists)
        CardSelectionManager.Instance.ShowCards(cardsToShow);
    }
    
    /// <summary>
    /// Called when a boss card is selected (called from EnemyCards.ApplyEffect)
    /// </summary>
    public void OnBossCardSelected(EnemyCards bossCard)
    {
        if (bossCard == null)
        {
            Debug.LogError("<color=red>Boss card is null!</color>");
            return;
        }
        
        Debug.Log($"<color=gold>Boss card selected: {bossCard.cardName}</color>");
        // Store boss card and enemy prefab for spawning
        currentBossCard = bossCard;
        currentBossEnemy = bossCard.enemyPrefab;

        // Per-boss menace timer: use the card's value when valid, otherwise fall back
        // to the global bossMenaceTimer configured on this spawner.
        if (bossCard.bossMenaceTimer > 0f)
        {
            currentBossMenaceTimer = bossCard.bossMenaceTimer;
        }
        else
        {
            currentBossMenaceTimer = bossMenaceTimer;
        }

        Debug.Log($"<color=magenta>Boss menace timer for {bossCard.cardName}: {currentBossMenaceTimer:F2}s</color>");

        currentBossDeathRewardTimer = bossCard.BossDeathRewardTimer;
        if (currentBossDeathRewardTimer < 0f)
        {
            currentBossDeathRewardTimer = 0f;
        }
    }

    private void SpawnBossEnemy(GameObject bossPrefab)
    {
        if (bossPrefab == null || minPos == null || maxPos == null)
        {
            Debug.LogError("<color=red>Cannot spawn boss - missing prefab or spawn bounds!</color>");
            return;
        }

        // Calculate spawn position: X = camera center, Y = maxPos.y
        Camera mainCam = Camera.main;
        if (mainCam == null)
        {
            Debug.LogError("<color=red>Main camera not found!</color>");
            return;
        }

        Vector3 spawnPos = new Vector3(mainCam.transform.position.x, maxPos.position.y, 0f);

        Debug.Log($"<color=gold>Spawning boss at position: {spawnPos}</color>");
        currentBossEnemy = Instantiate(bossPrefab, spawnPos, Quaternion.identity);

        // Ensure boss enemy is tagged as Boss rarity for favour effects.
        EnemyCardTag[] bossTags = currentBossEnemy.GetComponentsInChildren<EnemyCardTag>(true);
        if (bossTags == null || bossTags.Length == 0)
        {
            EnemyCardTag created = currentBossEnemy.AddComponent<EnemyCardTag>();
            bossTags = new EnemyCardTag[] { created };
        }

        for (int i = 0; i < bossTags.Length; i++)
        {
            if (bossTags[i] != null)
            {
                bossTags[i].rarity = CardRarity.Boss;
            }
        }

        // If this boss is a DarkNecromancer, apply its topSpawnYOffset using this spawner's maxPos
        DarkNecromancerEnemy necro = currentBossEnemy.GetComponent<DarkNecromancerEnemy>();
        if (necro != null)
        {
            float adjustedY = maxPos.position.y + necro.topSpawnYOffset;
            Vector3 adjustedPos = new Vector3(mainCam.transform.position.x, adjustedY, currentBossEnemy.transform.position.z);
            currentBossEnemy.transform.position = adjustedPos;
            Debug.Log($"<color=gold>Adjusted DarkNecromancer boss spawn with topSpawnYOffset {necro.topSpawnYOffset:F2}: {adjustedPos}</color>");
        }

        // If this boss is Norcthex, apply boss-index-based summon scaling and start its behavior pattern
        NorcthexEnemy norcthex = currentBossEnemy.GetComponent<NorcthexEnemy>();
        if (norcthex != null)
        {
            int bossIndexForScaling = currentBossIndex - 1;
            if (bossIndexForScaling < 0)
            {
                bossIndexForScaling = 0;
            }
            norcthex.ApplyBossSpawnIndex(bossIndexForScaling);
            norcthex.StartBossBehavior();
        }

        // Subscribe to boss death so we can kill all remaining enemies immediately when boss health hits 0
        EnemyHealth bossHealth = currentBossEnemy.GetComponent<EnemyHealth>();
        if (bossHealth != null)
        {
            bossHealth.OnDeath += OnBossDeath;
        }

        // Apply boss-specific stat multipliers (health, EXP, attack damage) from the active boss event
        float healthMult = currentBossHealthMultiplier;
        float expMult = currentBossExpMultiplier;
        float damageMult = currentBossDamageMultiplier;

        if (bossHealth != null && healthMult > 0f && !Mathf.Approximately(healthMult, 1f))
        {
            bossHealth.RegisterPostScalingHealthMultiplier(healthMult);
        }

        EnemyExpData bossExp = currentBossEnemy.GetComponent<EnemyExpData>();
        if (bossExp != null && expMult > 0f && !Mathf.Approximately(expMult, 1f))
        {
            bossExp.RegisterPostScalingExpMultiplier(expMult);
        }

        if (damageMult > 0f && !Mathf.Approximately(damageMult, 1f))
        {
            if (necro != null)
            {
                necro.MultiplyAttackDamage(damageMult);
            }

            if (norcthex != null)
            {
                norcthex.MultiplyAttackDamage(damageMult);
            }
        }

        // Make boss immune during menace timer
        EnemyHealth bossHealthComponent = currentBossEnemy.GetComponent<EnemyHealth>();
        if (bossHealthComponent != null)
        {
            bossHealthComponent.SetImmuneToBossMenace(true);
        }
        StartCoroutine(BossImmunityCoroutine(currentBossEnemy));
    }

    /// <summary>
    /// Called when the boss's EnemyHealth reaches 0. Kill all remaining non-boss enemies immediately.
    /// </summary>
    private void OnBossDeath()
    {
        if (bossDeathTriggered)
        {
            return;
        }

        bossDeathTriggered = true;
        bossDeathCleanupInProgress = true;
        Debug.Log("<color=red>Boss EnemyHealth.OnDeath triggered - killing all remaining enemies.</color>");

        // Kill all remaining enemies EXCEPT the boss itself (its own death animation/cleanup handles it)
        KillAllEnemies();
    }
    
    private IEnumerator BossImmunityCoroutine(GameObject boss)
    {
        if (boss == null) yield break;

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth != null)
        {
            bossHealth.SetImmuneToBossMenace(true);
            Debug.Log("<color=magenta>Boss is now IMMUNE during menace timer!</color>");
        }

        // Wait for menace timer (per-boss if configured)
        float menaceDuration = currentBossMenaceTimer > 0f ? currentBossMenaceTimer : bossMenaceTimer;
        yield return GameStateManager.WaitForPauseSafeSeconds(menaceDuration);

        // Remove immunity
        if (boss != null && bossHealth != null)
        {
            bossHealth.SetImmuneToBossMenace(false);
            Debug.Log("<color=lime>Boss is now VULNERABLE after menace timer.</color>");
        }
    }
    
    private void DestroyAllPlayerProjectiles()
    {
        float duration = Mathf.Max(0.01f, FadeAwayDuration);
        int fadedCount = 0;

        TornadoController[] tornadoes = FindObjectsOfType<TornadoController>();
        for (int i = 0; i < tornadoes.Length; i++)
        {
            if (tornadoes[i] != null && tornadoes[i].gameObject != null)
            {
                BeginFadeOutForProjectile(tornadoes[i].gameObject, duration);
                fadedCount++;
            }
        }

        Collapse[] collapses = FindObjectsOfType<Collapse>();
        for (int i = 0; i < collapses.Length; i++)
        {
            if (collapses[i] != null && collapses[i].gameObject != null)
            {
                BeginFadeOutForProjectile(collapses[i].gameObject, duration);
                fadedCount++;
            }
        }

        NovaStar[] novaStars = FindObjectsOfType<NovaStar>();
        for (int i = 0; i < novaStars.Length; i++)
        {
            if (novaStars[i] != null && novaStars[i].gameObject != null)
            {
                BeginFadeOutForProjectile(novaStars[i].gameObject, duration);
                fadedCount++;
            }
        }

        DwarfStar[] dwarfStars = FindObjectsOfType<DwarfStar>();
        for (int i = 0; i < dwarfStars.Length; i++)
        {
            if (dwarfStars[i] != null && dwarfStars[i].gameObject != null)
            {
                BeginFadeOutForProjectile(dwarfStars[i].gameObject, duration);
                fadedCount++;
            }
        }

        GameObject[] projectiles = GameObject.FindGameObjectsWithTag("Projectile");
        for (int i = 0; i < projectiles.Length; i++)
        {
            GameObject proj = projectiles[i];
            if (proj == null) continue;

            HolyShield shield = proj.GetComponent<HolyShield>();
            if (shield != null)
            {
                continue;
            }

            FireMine mine = proj.GetComponentInChildren<FireMine>(true);
            if (mine != null)
            {
                continue;
            }

            BeginFadeOutForProjectile(proj, duration);
            fadedCount++;
        }

        Debug.Log($"<color=lime>Fading out {fadedCount} player projectiles over {duration:F2}s!</color>");
    }

    private void BeginFadeOutForProjectile(GameObject obj, float duration)
    {
        if (obj == null) return;

        Collider2D[] colliders = obj.GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = false;
            }
        }

        StartCoroutine(FadeOutAndDestroyProjectile(obj, duration));
    }

    private IEnumerator FadeOutAndDestroyProjectile(GameObject obj, float duration)
    {
        if (obj == null) yield break;

        SpriteRenderer[] sprites = obj.GetComponentsInChildren<SpriteRenderer>(true);
        if (sprites.Length == 0)
        {
            yield return GameStateManager.WaitForPauseSafeSeconds(duration);
            if (obj != null)
            {
                Destroy(obj);
            }
            yield break;
        }

        Color[] originalColors = new Color[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            originalColors[i] = sprites[i].color;
        }

        float elapsed = 0f;
        while (elapsed < duration && obj != null)
        {
            if (obj == null)
            {
                yield break;
            }

            elapsed += GameStateManager.GetPauseSafeDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] == null) continue;
                Color c = originalColors[i];
                c.a *= 1f - t;
                sprites[i].color = c;
            }

            yield return null;
        }

        if (obj != null)
        {
            Destroy(obj);
        }
    }
    
    private void SetProjectilesSpawnable(bool spawnable)
    {
        // Find ProjectileSpawner and disable/enable it
        ProjectileSpawner spawner = FindObjectOfType<ProjectileSpawner>();
        if (spawner != null)
        {
            spawner.enabled = spawnable;
            Debug.Log($"<color=cyan>ProjectileSpawner set to: {(spawnable ? "ENABLED" : "DISABLED")}</color>");
        }
    }
    
    private void ResetAndReduceProjectileCooldowns()
    {
        ProjectileSpawner spawner = FindObjectOfType<ProjectileSpawner>();
        if (spawner == null)
        {
            Debug.LogWarning("<color=yellow>ProjectileSpawner not found!</color>");
        }
        else
        {
            spawner.ResetAndReduceCooldowns(projectileCooldownReduction);
        }

        TornadoController.ApplyBossCooldownReduction(projectileCooldownReduction);
    }
    
    private void SetAutoFireEnabled(bool enabled)
    {
        AdvancedPlayerController player = FindObjectOfType<AdvancedPlayerController>();
        if (player != null)
        {
            player.enableAutoFire = enabled;
            Debug.Log($"<color=cyan>AutoFire set to: {(enabled ? "ENABLED" : "DISABLED")}</color>");
        }
    }
    
    private void KillAllEnemies()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        int killedCount = 0;
        
        foreach (GameObject enemy in enemies)
        {
            // Skip the current boss object so its own death animation and cleanup can proceed normally
            if (enemy == currentBossEnemy)
            {
                continue;
            }
            
            IDamageable damageable = enemy.GetComponent<IDamageable>();
            if (damageable != null && damageable.IsAlive)
            {
                // Deal massive damage to kill instantly
                damageable.TakeDamage(999999f, transform.position, Vector3.zero);
                killedCount++;
            }
        }
        
        Debug.Log($"<color=lime>Killed {killedCount} enemies!</color>");
    }

    /// <summary>
    /// Calculate the effective spawn interval for a given enemy card.
    /// Unified mechanic:
    ///   - First-card spawn boost for the very first non-boss card.
    ///   - Flat spawn-interval reduction accumulated per scaling tier from
    ///     EnemyScalingSystem (GetSpawnIntervalDecreaseTotal), applied BEFORE
    ///     card-count scaling.
    ///   - Card-count-based scaling (+perExtraCardSpawnIntervalFactor per extra
    ///     registered non-boss enemy card).
    ///   - Global flat post-boss reduction from EnemyScalingSystem
    ///     (GetGlobalSpawnIntervalFlatReductionTotal), stacked per completed
    ///     boss event.
    ///   - Boss-event multiplier applied to NON-BOSS cards while a boss event
    ///     is active.
    /// Boss rarity cards are excluded from all reductions and multipliers.
    /// </summary>
    private float GetEffectiveSpawnInterval(EnemyCards card)
    {
        if (card == null)
        {
            return 1f;
        }

        // Base interval from runtimeSpawnInterval if set, otherwise from the card's inspector value
        float baseInterval = card.runtimeSpawnInterval > 0f ? card.runtimeSpawnInterval : card.spawnInterval;
        if (baseInterval <= 0f)
        {
            baseInterval = 0.1f;
        }

        // FIRST CARD BOOST: For the very first non-boss card, override base interval using
        // the inspector spawnInterval and apply the temporary reduction.
        if (card.rarity != CardRarity.Boss && firstRegisteredCard == card && GameStateManager.PauseSafeTime < firstCardBoostEndTime && firstCardSpawnIntervalReduction > 0f)
        {
            float inspectorInterval = card.spawnInterval > 0f ? card.spawnInterval : baseInterval;
            float reductionFactor = Mathf.Clamp01(1f - firstCardSpawnIntervalReduction);
            baseInterval = Mathf.Max(0.05f, inspectorInterval * reductionFactor);
        }

        int activeCardCount = registeredEnemyCards.Count;

        // Start from the base interval, then subtract the flat per-tier
        // reduction accumulated in EnemyScalingSystem (non-boss only).
        float intervalAfterFlat = baseInterval;

        if (card.rarity != CardRarity.Boss && EnemyScalingSystem.Instance != null)
        {
            float flatFromScaling = EnemyScalingSystem.Instance.GetSpawnIntervalDecreaseTotal();
            if (flatFromScaling > 0f)
            {
                intervalAfterFlat = Mathf.Max(0.05f, baseInterval - flatFromScaling);
            }
        }

        // CARD COUNT SCALING: make intervals longer when multiple non-boss
        // enemy cards are registered.
        if (card.rarity != CardRarity.Boss && activeCardCount > 1)
        {
            float extraCards = Mathf.Max(0, activeCardCount - 1);
            float countFactor = 1f + perExtraCardSpawnIntervalFactor * extraCards;
            intervalAfterFlat *= countFactor;
        }

        float finalInterval = Mathf.Max(0.1f, intervalAfterFlat);

        // Apply global flat spawn-interval reduction that stacks once per
        // completed boss event. This is applied to all non-boss enemy
        // cards AFTER the per-tier flat reduction and card-count scaling so
        // it truly behaves like a flat "-X seconds" modifier.
        if (card.rarity != CardRarity.Boss && EnemyScalingSystem.Instance != null)
        {
            float flatReduction = EnemyScalingSystem.Instance.GetGlobalSpawnIntervalFlatReductionTotal();
            if (flatReduction > 0f)
            {
                finalInterval = Mathf.Max(0.1f, finalInterval - flatReduction);
            }
        }

        // LAST STEP: If we are in a boss event, slow down NON-BOSS spawns by the
        // configured multiplier. This is applied after ALL other adjustments.
        if (isBossEventActive && card.rarity != CardRarity.Boss && bossEventSpawnIntervalMultiplier > 0f)
        {
            finalInterval *= bossEventSpawnIntervalMultiplier;
            finalInterval = Mathf.Max(0.1f, finalInterval);
        }

        return finalInterval;
    }
}
