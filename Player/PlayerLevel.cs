using System;
using UnityEngine;

public class PlayerLevel : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private int currentExp = 0;
    [SerializeField] private int expToNextLevel = 100;
    [SerializeField] private int baseExpRequirement = 100;
    [SerializeField] private float expScalingFactor = 1.5f;
    
    [Header("Exp Gain Control")]
    [Tooltip("Can be toggled by NoExpGainButton")]
    private bool expGainEnabled = true;

    [Header("Level Up Bonuses")]
    [SerializeField] private float healthIncreasePerLevel = 20f;
    [SerializeField] private int manaIncreasePerLevel = 15;
    [SerializeField] private float manaRegenIncreasePerLevel = 0.5f; // Mana regen increase per level
    [Tooltip("Flat attack damage increase per level")]
    public float attackDamageIncreasePerLevel = 1f;

    [Header("Defense Stats")]
    public float armor = 0f;

    [Header("References")]
    private PlayerHealth playerHealth;
    private PlayerMana playerMana;

    // Events
    public event Action<int> OnLevelUp; // Passes new level
    public event Action<int, int, int> OnExpChanged; // (currentExp, expToNextLevel, currentLevel)

    // Properties
    public int CurrentLevel => currentLevel;
    public int CurrentExp => currentExp;
    public int ExpToNextLevel => expToNextLevel;
    public float ExpProgress => (float)currentExp / expToNextLevel;

    private void Awake()
    {
        playerHealth = GetComponent<PlayerHealth>();
        playerMana = GetComponent<PlayerMana>();

        if (playerHealth == null)
        {
            Debug.LogError("PlayerLevel: PlayerHealth component not found!");
        }

        if (playerMana == null)
        {
            Debug.LogError("PlayerLevel: PlayerMana component not found!");
        }
    }

    private void Start()
    {
        Debug.Log($"<color=cyan>PlayerLevel Start: currentLevel={currentLevel}, currentExp={currentExp}</color>");
        
        // Initialize exp requirement for level 1
        CalculateExpRequirement();
        OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
        
        Debug.Log($"<color=cyan>PlayerLevel initialized: Level {currentLevel}, EXP {currentExp}/{expToNextLevel}</color>");
    }

    /// <summary>
    /// Grant experience points to the player
    /// </summary>
    public void GainExperience(int amount)
    {
        if (amount <= 0) return;
        
        // Check if exp gain is disabled
        if (!expGainEnabled)
        {
            Debug.Log("<color=gray>Exp gain is disabled - no exp gained</color>");
            return;
        }

        // Apply experience multiplier from PlayerStats if it exists
        PlayerStats playerStats = GetComponent<PlayerStats>();
        float multiplier = playerStats != null ? playerStats.experienceMultiplier : 1f;
        int actualAmount = Mathf.RoundToInt(amount * multiplier);

        currentExp += actualAmount;
        Debug.Log($"Gained {actualAmount} EXP! Current: {currentExp}/{expToNextLevel}");

        // Sync with PlayerStats
        if (playerStats != null)
        {
            playerStats.currentExperience = currentExp;
        }

        // Check for level up(s)
        while (currentExp >= expToNextLevel)
        {
            LevelUp();
        }

        OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
    }
    
    /// <summary>
    /// Enable or disable exp gain (called by NoExpGainButton)
    /// </summary>
    public void SetExpGainEnabled(bool enabled)
    {
        expGainEnabled = enabled;
    }

    private void LevelUp()
    {
        // Subtract the exp requirement from current exp
        currentExp -= expToNextLevel;
        currentLevel++;

        Debug.Log($"<color=yellow>Level Up! Now level {currentLevel}</color>");

        // Apply stat increases
        ApplyLevelUpBonuses();

        // Calculate new exp requirement
        CalculateExpRequirement();

        // Sync with PlayerStats if it exists (for card system)
        PlayerStats playerStats = GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.currentLevel = currentLevel;
            playerStats.currentExperience = currentExp;
            playerStats.experienceToNextLevel = expToNextLevel;
        }

        // Invoke level up event
        OnLevelUp?.Invoke(currentLevel);
        
        // Trigger card selection if CardSelectionManager exists
        if (CardSelectionManager.Instance != null)
        {
            EnemyCardSpawner enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>();
            if (enemyCardSpawner != null && enemyCardSpawner.IsBossDeathCleanupInProgress)
            {
                StartCoroutine(ShowLevelUpCardsWhenBossCleanupDone());
            }
            else
            {
                CardSelectionManager.Instance.ShowCardSelection();
            }
        }
        else
        {
            Debug.LogError("CardSelectionManager not found in scene!");
        }
    }

    private System.Collections.IEnumerator ShowLevelUpCardsWhenBossCleanupDone()
    {
        while (true)
        {
            EnemyCardSpawner enemyCardSpawner = FindObjectOfType<EnemyCardSpawner>();
            if (enemyCardSpawner == null || !enemyCardSpawner.IsBossDeathCleanupInProgress)
            {
                break;
            }
            yield return null;
        }

        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ShowCardSelection();
        }
    }

    private void ApplyLevelUpBonuses()
    {
        // Increase max health AND current health by the bonus amount (not full heal)
        if (playerHealth != null)
        {
            float currentHealth = playerHealth.CurrentHealth;
            float newMaxHealth = playerHealth.MaxHealth + healthIncreasePerLevel;
            playerHealth.SetMaxHealth(newMaxHealth, fillToMax: false);
            // Add the bonus to current health as well
            playerHealth.Heal(healthIncreasePerLevel);
            Debug.Log($"Max Health increased by {healthIncreasePerLevel}! New Max: {newMaxHealth}");
        }

        // Increase max mana (keep current mana value)
        if (playerMana != null)
        {
            int newMaxMana = playerMana.MaxMana + manaIncreasePerLevel;
            playerMana.SetMaxMana(newMaxMana, refill: false); // Don't refill mana
            Debug.Log($"Max Mana increased by {manaIncreasePerLevel}! New Max: {newMaxMana} (current mana unchanged)");
        }
        
        // Increase mana regeneration rate and flat attack damage
        PlayerStats playerStats = GetComponent<PlayerStats>();
        if (playerStats != null)
        {
            playerStats.manaRegenPerSecond += manaRegenIncreasePerLevel;
            Debug.Log($"<color=cyan>Mana Regen increased by {manaRegenIncreasePerLevel}! New Regen: {playerStats.manaRegenPerSecond}/s</color>");
            
            // Increase core Attack stat per level
            playerStats.baseAttack += attackDamageIncreasePerLevel;
            Debug.Log($"<color=yellow>Attack increased by {attackDamageIncreasePerLevel}! New Attack: {playerStats.baseAttack}</color>");
        }
    }

    private void CalculateExpRequirement()
    {
        // Interpret expScalingFactor as a DIRECT per-level percentage of the BASE requirement,
        // applied linearly (no compounding).
        // Example with baseExpRequirement = 100:
        //   expScalingFactor = 1   -> 100, 200, 300, 400, ...
        //   expScalingFactor = 0.5 -> 100, 150, 200, 250, 300, ...
        float clampedFactor = Mathf.Max(0f, expScalingFactor);
        float required = baseExpRequirement * (1f + clampedFactor * (currentLevel - 1));
        expToNextLevel = Mathf.Max(1, Mathf.RoundToInt(required));
    }

    /// <summary>
    /// Debug method to add levels directly
    /// </summary>
    [ContextMenu("Add Level")]
    public void DebugAddLevel()
    {
        GainExperience(expToNextLevel);
    }
    
    /// <summary>
    /// Debug method to add 10 levels directly
    /// </summary>
    [ContextMenu("Add 10 Levels")]
    public void DebugAdd10Levels()
    {
        for (int i = 0; i < 10; i++)
        {
            GainExperience(expToNextLevel);
        }
    }

    /// <summary>
    /// Debug method to add 50 exp
    /// </summary>
    [ContextMenu("Add 50 EXP")]
    public void DebugAdd50Exp()
    {
        GainExperience(50);
    }

    /// <summary>
    /// Reset player level (useful for testing)
    /// </summary>
    [ContextMenu("Reset Level")]
    public void ResetLevel()
    {
        currentLevel = 1;
        currentExp = 0;
        CalculateExpRequirement();
        OnExpChanged?.Invoke(currentExp, expToNextLevel, currentLevel);
        Debug.Log("Player level reset to 1");
    }
}
