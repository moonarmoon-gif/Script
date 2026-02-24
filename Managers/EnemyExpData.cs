using UnityEngine;

/// <summary>
/// Attach this to enemy prefabs to define how much EXP they drop when defeated.
/// </summary>
public class EnemyExpData : MonoBehaviour
{
    [Header("Experience Settings")]
    [SerializeField] private int expReward = 50;
    [SerializeField] private bool grantExpToPlayer = true;
    [Tooltip("Optional: Override the exp reward with a random range")]
    [SerializeField] private bool useRandomRange = false;
    [SerializeField] private int minExpReward = 40;
    [SerializeField] private int maxExpReward = 60;

    [Header("Soul Value Settings")]
    [SerializeField] private float soulValue = 10f;

    [Header("Rarity Settings")]
    public CardRarity EnemyRarity = CardRarity.Common;

    private bool hasStarted = false;
    private float pendingPostScalingExpMultiplier = 1f;

    public int ExpReward
    {
        get
        {
            if (useRandomRange)
            {
                return Random.Range(minExpReward, maxExpReward + 1);
            }
            return expReward;
        }
    }

    public void SetGrantExpEnabled(bool enabled)
    {
        grantExpToPlayer = enabled;
    }

    public float ExpRewardExact
    {
        get { return ExpReward; }
    }

    public float SoulValue
    {
        get { return Mathf.Max(0f, soulValue); }
    }
    
    /// <summary>
    /// Multiply exp reward by a value (used by EnemySpawner). SoulValue does NOT scale.
    /// </summary>
    public void MultiplyExpReward(float multiplier)
    {
        expReward = Mathf.RoundToInt(expReward * multiplier);
        minExpReward = Mathf.RoundToInt(minExpReward * multiplier);
        maxExpReward = Mathf.RoundToInt(maxExpReward * multiplier);
    }

    public void AddFlatExpBonus(int bonus)
    {
        if (bonus == 0) return;

        expReward = Mathf.Max(0, expReward + bonus);
        minExpReward = Mathf.Max(0, minExpReward + bonus);
        maxExpReward = Mathf.Max(0, maxExpReward + bonus);
    }

    public void RegisterPostScalingExpMultiplier(float multiplier)
    {
        if (multiplier <= 0f || Mathf.Approximately(multiplier, 1f)) return;

        if (hasStarted)
        {
            MultiplyExpReward(multiplier);
        }
        else
        {
            pendingPostScalingExpMultiplier *= multiplier;
        }
    }

    private void Awake()
    {
        // Subscribe to death event
        var enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath += GrantExpToPlayer;
        }
        else
        {
            Debug.LogWarning($"EnemyExpData on {gameObject.name} requires EnemyHealth component!");
        }
    }
    
    private void Start()
    {
        // Apply enemy scaling system to EXP only (SoulValue does not scale)
        if (EnemyScalingSystem.Instance != null)
        {
            float expMultiplier = EnemyScalingSystem.Instance.GetExpMultiplier();
            expReward = Mathf.RoundToInt(expReward * expMultiplier);
            minExpReward = Mathf.RoundToInt(minExpReward * expMultiplier);
            maxExpReward = Mathf.RoundToInt(maxExpReward * expMultiplier);
            Debug.Log($"<color=cyan>{gameObject.name} EXP scaled: {expReward} (x{expMultiplier:F2})</color>");
        }

        hasStarted = true;

        if (!Mathf.Approximately(pendingPostScalingExpMultiplier, 1f))
        {
            MultiplyExpReward(pendingPostScalingExpMultiplier);
            pendingPostScalingExpMultiplier = 1f;
        }
    }

    private void GrantExpToPlayer()
    {
        if (!grantExpToPlayer)
        {
            return;
        }

        // Don't grant EXP if player is dead
        if (GameStateManager.Instance != null && GameStateManager.Instance.PlayerIsDead)
        {
            Debug.Log($"<color=yellow>{gameObject.name} died but no EXP granted (player is dead)</color>");
            return;
        }

        int baseExp = ExpReward;

        CardRarity rarity = EnemyRarity;
        int bonusExp = ExtraExpPerRarityFavour.GetBonusExpForRarity(rarity);
        float totalExp = Mathf.Max(0f, baseExp + bonusExp);
        
        // If a boss event is active and THIS enemy is the current boss, route
        // its EXP through the spawner so that level-up cards are shown only
        // after the boss death cleanup window has finished.
        EnemySpawner waveSpawner = Object.FindObjectOfType<EnemySpawner>();
        if (waveSpawner != null && waveSpawner.IsBossEventActive && waveSpawner.CurrentBossEnemy == gameObject)
        {
            waveSpawner.QueueBossExperience(totalExp);
            Debug.Log($"<color=cyan>Queued {totalExp:F2} EXP from boss '{gameObject.name}' (EnemySpawner) for post-cleanup grant.</color>");
            return;
        }

        EnemyCardSpawner bossSpawner = Object.FindObjectOfType<EnemyCardSpawner>();
        if (bossSpawner != null && bossSpawner.IsBossEventActive && bossSpawner.CurrentBossEnemy == gameObject)
        {
            bossSpawner.QueueBossExperience(totalExp);
            Debug.Log($"<color=cyan>Queued {totalExp:F2} EXP from boss '{gameObject.name}' (EnemyCardSpawner) for post-cleanup grant.</color>");
            return;
        }

        // Find the player's level component and grant exp immediately for
        // all non-boss enemies.
        // Try AdvancedPlayerController first (new system)
        if (AdvancedPlayerController.Instance != null)
        {
            var playerLevel = AdvancedPlayerController.Instance.GetComponent<PlayerLevel>();
            if (playerLevel != null)
            {
                playerLevel.GainExperience(totalExp);
                Debug.Log($"<color=cyan>Granted {totalExp:F2} EXP to player!</color>");
            }
            else
            {
                Debug.LogWarning("PlayerLevel component not found on AdvancedPlayerController!");
            }
        }
        // Fallback to old PlayerController if AdvancedPlayerController doesn't exist
        else if (PlayerController.Instance != null)
        {
            var playerLevel = PlayerController.Instance.GetComponent<PlayerLevel>();
            if (playerLevel != null)
            {
                playerLevel.GainExperience(totalExp);
                Debug.Log($"<color=cyan>Granted {totalExp:F2} EXP to player!</color>");
            }
            else
            {
                Debug.LogWarning("PlayerLevel component not found on PlayerController!");
            }
        }
        else
        {
            Debug.LogError("No PlayerController or AdvancedPlayerController found! Cannot grant EXP!");
        }

        if (FavourExpUI.Instance != null)
        {
            var manager = CardSelectionManager.Instance;
            if (manager != null && manager.IsTimedLevelingFavourSystemActive)
            {
                return;
            }

            float soul = SoulValue;
            if (soul > 0f)
            {
                GameObject playerGO = null;
                if (AdvancedPlayerController.Instance != null)
                {
                    playerGO = AdvancedPlayerController.Instance.gameObject;
                }
                else if (PlayerController.Instance != null)
                {
                    playerGO = PlayerController.Instance.gameObject;
                }

                if (playerGO != null)
                {
                    PlayerStats stats = playerGO.GetComponent<PlayerStats>();
                    if (stats != null && stats.soulGainMultiplier > 0f)
                    {
                        soul *= stats.soulGainMultiplier;
                    }
                }

                FavourExpUI.Instance.AddSoul(soul);
            }
        }
    }

    private void OnDestroy()
    {
        var enemyHealth = GetComponent<EnemyHealth>();
        if (enemyHealth != null)
        {
            enemyHealth.OnDeath -= GrantExpToPlayer;
        }
    }
}
