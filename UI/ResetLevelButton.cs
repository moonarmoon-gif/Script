using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

/// <summary>
/// Button script to reset player to level 1 and clear all cards/modifiers
/// Attach to a GameObject with a Button component
/// CRITICAL: Resets everything - projectiles, modifiers, core cards, stats
/// </summary>
[RequireComponent(typeof(Button))]
public class ResetLevelButton : MonoBehaviour
{
    private Button button;
    
    [Header("Visual Feedback")]
    [Tooltip("Text to show on button (optional - supports both Text and TextMeshProUGUI)")]
    public UnityEngine.UI.Text buttonText;
    
    [Tooltip("TextMeshPro text component (optional)")]
    public TextMeshProUGUI buttonTextTMP;
    
    [Tooltip("Text for button")]
    public string buttonLabel = "Reset to Level 1";
    
    [Header("Reset Settings")]
    [Tooltip("Should we destroy existing projectiles in the scene?")]
    public bool destroyExistingProjectiles = false;

    void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(ResetLevel);
        
        if (buttonText != null)
        {
            buttonText.text = buttonLabel;
        }
        
        if (buttonTextTMP != null)
        {
            buttonTextTMP.text = buttonLabel;
        }
    }

    void ResetLevel()
    {
        if (CardSelectionManager.Instance != null)
        {
            CardSelectionManager.Instance.ForceCloseSelectionUI();
        }

        Time.timeScale = 1f;

        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.ResetRunState();
        }

        FavourEffect.ResetPickCounts();

        if (EnemyScalingSystem.Instance != null)
        {
            EnemyScalingSystem.Instance.ResetScaling();
        }

        HolyShield.ResetRunState();
        ReflectShield.ResetRunState();
        NullifyShield.ResetRunState();

        Debug.Log("<color=red>═══════════════════════════════════════</color>");
        Debug.Log("<color=red>RESETTING PLAYER TO LEVEL 1</color>");
        Debug.Log("<color=red>═══════════════════════════════════════</color>");
        
        // 1. Reset PlayerLevel
        ResetPlayerLevel();
        
        // 2. Reset PlayerStats (all modifiers)
        ResetPlayerStats();
        
        // 3. Reset PlayerHealth and PlayerMana
        ResetPlayerHealthAndMana();
        
        // 4. Clear all projectiles from ProjectileSpawner
        ClearProjectileSpawner();
        
        // 5. Reset ProjectileCardLevelSystem
        ResetProjectileCardLevelSystem();
        
        // 6. Reset ProjectileCardModifiers
        ResetProjectileCardModifiers();

        ResetFavours();
        
        // 7. Optionally destroy existing projectiles in scene
        if (destroyExistingProjectiles)
        {
            DestroyExistingProjectiles();
        }
        
        Debug.Log("<color=green>═══════════════════════════════════════</color>");
        Debug.Log("<color=green>RESET COMPLETE!</color>");
        Debug.Log("<color=green>═══════════════════════════════════════</color>");
    }
    
    void ResetPlayerLevel()
    {
        PlayerLevel playerLevel = FindObjectOfType<PlayerLevel>();
        if (playerLevel != null)
        {
            // Use reflection to reset private fields
            var levelField = typeof(PlayerLevel).GetField("currentLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var expField = typeof(PlayerLevel).GetField("currentExp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var expToNextField = typeof(PlayerLevel).GetField("expToNextLevel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (levelField != null) levelField.SetValue(playerLevel, 1);
            if (expField != null) expField.SetValue(playerLevel, 0f);
            if (expToNextField != null) expToNextField.SetValue(playerLevel, 100f);
            
            // Trigger the OnExpChanged event to update UI
            var onExpChangedEvent = typeof(PlayerLevel).GetField("OnExpChanged", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (onExpChangedEvent != null)
            {
                var eventDelegate = onExpChangedEvent.GetValue(playerLevel) as System.Action<int, int, int>;
                eventDelegate?.Invoke(0, 100, 1);
            }
            
            Debug.Log("<color=yellow>PlayerLevel reset to 1</color>");
        }
        else
        {
            Debug.LogWarning("ResetLevelButton: PlayerLevel not found!");
        }
    }
    
    void ResetPlayerStats()
    {
        PlayerStats playerStats = FindObjectOfType<PlayerStats>();
        if (playerStats != null)
        {
            // Reset all stat multipliers to default
            playerStats.damageMultiplier = 1f;
            playerStats.critChance = 5f;
            playerStats.critDamage = 150f;
            playerStats.luck = 0f;
            playerStats.experienceMultiplier = 1f;
            playerStats.moveSpeedMultiplier = 1f;
            playerStats.manaRegenPerSecond = 1f;
            playerStats.healthRegenPerSecond = 0f;
            
            // Reset projectile stats
            playerStats.projectileSpeedMultiplier = 1f;
            playerStats.projectileSpeedBonus = 0f;
            playerStats.projectileSizeMultiplier = 1f;
            playerStats.projectilePierceCount = 0;
            playerStats.hasHomingProjectiles = false;
            playerStats.homingStrength = 0f;
            playerStats.additionalProjectiles = 0;
            playerStats.hasExplosiveProjectiles = false;
            playerStats.explosionRadius = 0f;
            playerStats.explosionDamage = 0f;
            playerStats.explosionRadiusMultiplier = 1f;
            playerStats.explosionRadiusBonus = 0f;
            playerStats.strikeZoneRadiusMultiplier = 1f;
            playerStats.strikeZoneRadiusBonus = 0f;
            playerStats.projectileBounces = 0;
            playerStats.projectileSplitCount = 0;
            playerStats.hasChainReaction = false;
            playerStats.chainReactionRadius = 0f;
            playerStats.projectileLifetimeMultiplier = 1f;
            playerStats.projectileLifetimeBonus = 0f;
            playerStats.projectileCooldownReduction = 0f;
            playerStats.projectileManaCostReduction = 0f;
            playerStats.projectileFlatDamage = 0f;
            playerStats.projectileDamageMultiplier = 1f;
            playerStats.projectileCritChance = 0f;
            playerStats.projectileCritDamage = 150f;
            playerStats.hasProjectileStatusEffect = false;
            playerStats.statusEffectChance = 0f;
            playerStats.attackSpeedPercent = 0f;
            
            // Reset level stats
            playerStats.currentLevel = 1;
            playerStats.currentExperience = 0f;
            playerStats.experienceToNextLevel = 100f;
            
            Debug.Log("<color=yellow>PlayerStats reset to default values</color>");
        }
        else
        {
            Debug.LogWarning("ResetLevelButton: PlayerStats not found!");
        }
    }
    
    void ResetPlayerHealthAndMana()
    {
        PlayerHealth playerHealth = FindObjectOfType<PlayerHealth>();
        if (playerHealth != null)
        {
            // Reset to starting health (you may want to adjust these values)
            playerHealth.SetMaxHealth(100f, fillToMax: true);
            Debug.Log("<color=yellow>PlayerHealth reset to 100</color>");
        }
        
        PlayerMana playerMana = FindObjectOfType<PlayerMana>();
        if (playerMana != null)
        {
            // Reset to starting mana (you may want to adjust these values)
            playerMana.SetMaxMana(100, refill: true);
            Debug.Log("<color=yellow>PlayerMana reset to 100</color>");
        }
    }
    
    void ClearProjectileSpawner()
    {
        ProjectileSpawner spawner = FindObjectOfType<ProjectileSpawner>();
        if (spawner != null)
        {
            // Use reflection to clear activeProjectiles list
            var activeProjectilesField = typeof(ProjectileSpawner).GetField("activeProjectiles", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (activeProjectilesField != null)
            {
                var list = activeProjectilesField.GetValue(spawner);
                if (list != null)
                {
                    var clearMethod = list.GetType().GetMethod("Clear");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(list, null);
                        Debug.Log("<color=yellow>ProjectileSpawner cleared - no more projectiles will spawn</color>");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("ResetLevelButton: ProjectileSpawner not found!");
        }
    }
    
    void ResetProjectileCardLevelSystem()
    {
        if (ProjectileCardLevelSystem.Instance != null)
        {
            // Use reflection to clear card levels dictionary
            var cardLevelsField = typeof(ProjectileCardLevelSystem).GetField("cardLevels", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cardLevelsField != null)
            {
                var dict = cardLevelsField.GetValue(ProjectileCardLevelSystem.Instance);
                if (dict != null)
                {
                    var clearMethod = dict.GetType().GetMethod("Clear");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(dict, null);
                        Debug.Log("<color=yellow>ProjectileCardLevelSystem reset - all card levels cleared</color>");
                    }
                }
            }
            
            // Clear enhanced variants dictionary
            var enhancedVariantsField = typeof(ProjectileCardLevelSystem).GetField("cardEnhancedVariants", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (enhancedVariantsField != null)
            {
                var dict = enhancedVariantsField.GetValue(ProjectileCardLevelSystem.Instance);
                if (dict != null)
                {
                    var clearMethod = dict.GetType().GetMethod("Clear");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(dict, null);
                        Debug.Log("<color=yellow>Enhanced variants cleared</color>");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("ResetLevelButton: ProjectileCardLevelSystem.Instance not found!");
        }
    }
    
    void ResetProjectileCardModifiers()
    {
        if (ProjectileCardModifiers.Instance != null)
        {
            // Use reflection to clear card modifiers dictionary
            var cardModifiersField = typeof(ProjectileCardModifiers).GetField("cardModifiers", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (cardModifiersField != null)
            {
                var dict = cardModifiersField.GetValue(ProjectileCardModifiers.Instance);
                if (dict != null)
                {
                    var clearMethod = dict.GetType().GetMethod("Clear");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(dict, null);
                        Debug.Log("<color=yellow>ProjectileCardModifiers reset - all card modifiers cleared</color>");
                    }
                }
            }
            
            // Clear projectile-card mapping
            var projectileCardMapField = typeof(ProjectileCardModifiers).GetField("projectileCardMap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (projectileCardMapField != null)
            {
                var dict = projectileCardMapField.GetValue(ProjectileCardModifiers.Instance);
                if (dict != null)
                {
                    var clearMethod = dict.GetType().GetMethod("Clear");
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(dict, null);
                        Debug.Log("<color=yellow>Projectile-card mapping cleared</color>");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning("ResetLevelButton: ProjectileCardModifiers.Instance not found!");
        }
    }
    
    void DestroyExistingProjectiles()
    {
        // Find all projectiles by common components
        var allProjectiles = new List<GameObject>();
        
        // Find by common projectile scripts
        allProjectiles.AddRange(System.Array.ConvertAll(FindObjectsOfType<ThunderBird>(), x => x.gameObject));
        allProjectiles.AddRange(System.Array.ConvertAll(FindObjectsOfType<ElementalBeam>(), x => x.gameObject));
        allProjectiles.AddRange(System.Array.ConvertAll(FindObjectsOfType<FireMine>(), x => x.gameObject));
        allProjectiles.AddRange(System.Array.ConvertAll(FindObjectsOfType<NuclearStrike>(), x => x.gameObject));
        
        // Remove duplicates
        var uniqueProjectiles = new HashSet<GameObject>(allProjectiles);
        
        foreach (var projectile in uniqueProjectiles)
        {
            if (projectile != null)
            {
                Destroy(projectile);
            }
        }
        
        Debug.Log($"<color=yellow>Destroyed {uniqueProjectiles.Count} existing projectiles</color>");
    }

    void ResetFavours()
    {
        FavourEffectManager favourManager = FindObjectOfType<FavourEffectManager>();
        if (favourManager != null)
        {
            favourManager.ClearAllEffects();
            Debug.Log("<color=yellow>Favour effects cleared</color>");
        }
        else
        {
            Debug.LogWarning("ResetLevelButton: FavourEffectManager not found!");
        }
    }
}
