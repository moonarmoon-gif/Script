using UnityEngine;

[CreateAssetMenu(fileName = "New Enemy Card", menuName = "Cards/Enemy Card")]
public class EnemyCards : BaseCard
{
    [Header("Enemy Prefab")]
    [Tooltip("The enemy prefab to spawn")]
    public GameObject enemyPrefab;
    
    [Header("Spawn Settings")]
    [Tooltip("Spawn interval for this enemy type (how often this enemy spawns)")]
    public float spawnInterval = 5f;
    
    // Runtime interval (calculated, doesn't modify inspector value)
    [System.NonSerialized]
    public float runtimeSpawnInterval = 0f;
    
    // Reference to original ScriptableObject (for duplicate prevention)
    [System.NonSerialized]
    public EnemyCards originalCard = null;

    [Header("Boss Settings")]
    [Tooltip("Menace timer duration for this boss card (seconds). Only used for BOSS rarity cards.")]
    public float bossMenaceTimer = 5.5f;
    [Tooltip("Time in seconds after the boss's health reaches 0 before ending the boss event and granting rewards. 0 = end immediately on death event.")]
    public float BossDeathRewardTimer = 3f;
    
    [Header("Modifier Display Formatting")]
    [Tooltip("Header text for modifiers section")]
    public string modifierHeaderText = "Modifiers:";
    [Tooltip("Color for modifier header (hex format: #RRGGBB or #RRGGBBAA)")]
    public string modifierHeaderColor = "#FFD700";
    [Tooltip("Outline color for modifier header (hex format: #RRGGBB or #RRGGBBAA, empty for no outline)")]
    public string modifierHeaderOutlineColor = "";
    [Tooltip("Font size for modifier header")]
    [Range(8, 32)]
    public int modifierHeaderFontSize = 14;
    [Tooltip("Font size for individual modifiers")]
    [Range(8, 32)]
    public int modifierFontSize = 12;
    [Tooltip("Number of line breaks (spacing) above the 'Modifiers:' text")]
    [Range(0, 5)]
    public float modifierHeaderSpacing = 0f;
    
    public enum TextAlignment { Left, Center, Right }
    [Tooltip("Text alignment for modifier header and descriptions")]
    public TextAlignment modifierAlignment = TextAlignment.Left;

    public override void ApplyEffect(GameObject player)
    {
        // Find EnemyCardSpawner
        EnemyCardSpawner spawner = FindObjectOfType<EnemyCardSpawner>();
        if (spawner == null)
        {
            Debug.LogError("<color=red>EnemyCardSpawner not found in scene! Cannot register enemy card.</color>");
            return;
        }
        
        // BOSS CARDS: Only drive boss events when we are in an actual boss-card selection
        if (rarity == CardRarity.Boss)
        {
            if (spawner.IsBossCardSelectionActive)
            {
                spawner.OnBossCardSelected(this);
                Debug.Log($"<color=gold>BOSS Card '{cardName}' selected during boss event!</color>");
            }
            else
            {
                // If a Boss card ever gets clicked outside a boss event, ignore boss logic
                // to avoid accidentally overwriting currentBossEnemy.
                Debug.LogWarning($"<color=yellow>BOSS Card '{cardName}' was selected outside a boss event. Ignoring boss selection.</color>");
            }
            return;
        }
        
        // NORMAL ENEMY CARDS: Register for spawning
        // Calculate runtime spawn interval based on rarity
        runtimeSpawnInterval = Mathf.Max(0.1f, GetSpawnIntervalForRarity());
        
        spawner.RegisterEnemyCard(this);
        Debug.Log($"<color=cyan>Enemy Card '{cardName}' ({rarity}) registered. Spawn interval: {runtimeSpawnInterval:F2}s</color>");
    }
    
    private float GetSpawnIntervalForRarity()
    {
        // Use base spawn interval, modified by rarity multiplier
        float multiplier = 1f;
        switch (rarity)
        {
            case CardRarity.Common: multiplier = 1f; break;
            case CardRarity.Uncommon: multiplier = 0.9f; break;
            case CardRarity.Rare: multiplier = 0.8f; break;
            case CardRarity.Epic: multiplier = 0.7f; break;
            case CardRarity.Legendary: multiplier = 0.6f; break;
            case CardRarity.Mythic: multiplier = 0.5f; break;
        }
        return spawnInterval * multiplier;
    }

    public override string GetFormattedDescription()
    {
        string baseDesc = "";
        
        // If custom description is provided, use it
        if (!string.IsNullOrEmpty(description))
        {
            baseDesc = description;
        }
        else
        {
            // Otherwise use default description
            baseDesc = GetDefaultDescription();
        }
        
        // For now, no modifiers system (will be added later)
        // Just return base description
        return baseDesc;
    }
    
    private string GetDefaultDescription()
    {
        if (enemyPrefab != null)
        {
            return $"Unlocks {enemyPrefab.name} enemy spawning\nSpawn interval: {spawnInterval}s";
        }
        return "Unlocks new enemy type";
    }
}
