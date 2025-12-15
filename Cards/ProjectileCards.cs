using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "New Projectile Card", menuName = "Cards/Projectile Card")]
public class ProjectileCards : BaseCard
{
    [Header("Projectile Type")]
    public ProjectileType projectileType;

    [Header("Projectile System (Active/Passive)")]
    public ProjectileSystemType projectileSystem = ProjectileSystemType.Passive;

    [Header("Projectile Prefab")]
    [Tooltip("The projectile prefab to spawn")]
    public GameObject projectilePrefab;
    public GameObject alternateProjectilePrefab;
    
    [Header("Spawn Settings")]
    [Tooltip("Base spawn interval (will be modified by rarity)")]
    public float spawnInterval = 5f;
    
    [Tooltip("When projectile count > 1, spawn all projectiles simultaneously")]
    public bool spawnTogether = true;

    [Header("Boss Menace Cooldown Reduction")]
    [Tooltip("If enabled, this projectile participates in the global -% cooldown reduction after boss menace.")]
    public bool applyBossCooldownReduction = true;

    [Header("Angle Settings - Base Version")]
    [Tooltip("Enable custom angle range (if disabled, uses default angles for spawn direction)")]
    public bool useCustomAngles = false;
    
    [Tooltip("Minimum angle in degrees (0 = right, 90 = up, 180 = left, 270 = down)")]
    public float minAngle = 0f;
    
    [Tooltip("Maximum angle in degrees (0 = right, 90 = up, 180 = left, 270 = down)")]
    public float maxAngle = 360f;
    
    [Header("Angle Settings - Enhanced Version")]
    [Tooltip("Enable custom angle range for enhanced variants")]
    public bool useCustomAnglesEnhanced = false;
    
    [Tooltip("Minimum angle for enhanced variants")]
    public float minAngleEnhanced = 0f;
    
    [Tooltip("Maximum angle for enhanced variants")]
    public float maxAngleEnhanced = 360f;
    
    [Header("Angle Settings - Enhanced Variant 2")]
    [Tooltip("Enable custom angle range for enhanced variant 2 (ElementalBeam smart beam)")]
    public bool useCustomAnglesEnhancedVariant2 = false;
    
    [Tooltip("Minimum angle for enhanced variant 2")]
    public float minAngleEnhancedVariant2 = -90f;
    
    [Tooltip("Maximum angle for enhanced variant 2")]
    public float maxAngleEnhancedVariant2 = 90f;

    [Header("Spawn Direction")]
    [Tooltip("Spawn direction constraint (for Fire/Ice Talon)")]
    public SpawnDirectionType spawnDirection = SpawnDirectionType.LeftSide; // Changed from Random360 to LeftSide

    // Track how many times this card has been selected
    [System.NonSerialized]
    public int stackCount = 0;
    
    // Runtime interval (calculated, doesn't modify inspector value)
    [System.NonSerialized]
    public float runtimeSpawnInterval = 0f;
    
    [Range(0f, 5f)]
    public float doomedSkipDuration = 0.75f;

    [System.NonSerialized]
    public CardRarity runtimeBaseRarity;

    [Header("First-Time Selection Bonus")]
    [Tooltip("Cooldown reduction when card is selected for the first time (0.5 = 50% reduction)")]
    public float firstTimeSelectionCooldownReduction = 0.5f;
    
    // Track if this card has been selected before
    [System.NonSerialized]
    public bool hasBeenSelectedBefore = false;

    // When true, this specific runtime instance will NOT grant levels on its next ApplyEffect call.
    // Used so the very first ACTIVE projectile chosen at game start is a "free" unlock
    // that does not advance level/enhancement.
    [System.NonSerialized]
    public bool suppressLevelGainOnce = false;

    [Header("Enhanced First-Spawn Cooldown")] 
    [Tooltip("If enabled, the FIRST spawn immediately after this card becomes ENHANCED uses a reduced cooldown.")] 
    public bool applyEnhancedFirstSpawnReduction = true;

    [Tooltip("Fraction of the enhanced cooldown to reduce on the FIRST enhanced spawn (0.5 = 50% reduction)")] 
    [Range(0f, 1f)]
    public float enhancedFirstSpawnReduction = 0.5f;

    // Runtime flag: set when this card unlocks an enhanced variant, so the next spawn uses the reduced cooldown
    [System.NonSerialized]
    public bool pendingEnhancedFirstSpawn = false;
    
    [Header("Projectile Modifiers")]
    [Tooltip("Modifiers that will be randomly selected based on rarity")]
    public List<ProjectileModifierData> availableModifiers = new List<ProjectileModifierData>();
    
    
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
    [Tooltip("Number of line breaks (spacing) above the 'Modifiers:' text (0 = no spacing, 1 = one line, 2 = two lines, etc.)")]
    [Range(0, 5)]
    public float modifierHeaderSpacing = 0f;
    
    public enum TextAlignment { Left, Center, Right }
    [Tooltip("Text alignment for modifier header and descriptions")]
    public TextAlignment modifierAlignment = TextAlignment.Left;
    
    // Selected modifiers for this card instance (runtime)
    [System.NonSerialized]
    public List<ProjectileModifierData> selectedModifiers = new List<ProjectileModifierData>();

    public enum ProjectileType
    {
        FireTalon,
        IceTalon,
        Tornado,
        Firebolt,
        FireMine,
        ThunderBird,
        NuclearStrike,
        ElementalBeam,
        CinderBloom,
        CryoBloom,
        Fireball,
        IceLance,
        NovaStar,
        DwarfStar,
        Collapse,
        HolyShield
    }

    public enum ProjectileSystemType
    {
        Passive,
        Active
    }

    public enum SpawnDirectionType
    {
        LeftSide,
        RightSide,
        TopSide
    }

    // Select modifiers when rarity is assigned (called by CardSelectionManager)
    public void OnRarityAssigned()
    {
        // Always clear and re-select modifiers for fresh randomization every draw
        selectedModifiers.Clear();

        if (projectileSystem == ProjectileSystemType.Active && suppressLevelGainOnce)
        {
            return;
        }

        SelectRandomModifiers();
    }
    
    public override void ApplyEffect(GameObject player)
    {
        // Apply rarity-based values (use runtime variable, don't modify inspector)
        // Ensure minimum cooldown of 0.1s
        runtimeSpawnInterval = Mathf.Max(0.1f, GetSpawnIntervalForRarity());
        
        // Determine if this call is the special initial ACTIVE projectile pick at
        // game start ("free" unlock). For that one, we want ZERO modifiers and
        // no level gain.
        bool skipInitialActiveLevelGain = ShouldSkipInitialActiveLevelGain();

        // Select random modifiers only when this is NOT the initial free ACTIVE
        // projectile. That way the very first active projectile starts as a pure
        // base version with no modifiers applied.
        if (!skipInitialActiveLevelGain)
        {
            if (selectedModifiers.Count == 0)
            {
                SelectRandomModifiers();
            }
        }

        ProjectileSpawner spawner = null;
        if (projectileSystem == ProjectileSystemType.Passive)
        {
            spawner = player.GetComponent<ProjectileSpawner>();
            if (spawner == null)
            {
                spawner = player.AddComponent<ProjectileSpawner>();
            }
        }

        // CRITICAL: Register modifiers BEFORE spawning projectile
        // This ensures first spawn has correct modifiers for ALL projectile types
        // (NovaStar, DwarfStar, ThunderBird, ElementalBeam, FireMine, etc.)
        // Previously modifiers were registered AFTER spawn, causing first spawn to have default values
        if (!skipInitialActiveLevelGain && selectedModifiers.Count > 0)
        {
            ProjectileCardModifiers.Instance.RegisterCardModifiers(this, selectedModifiers, rarity);
            
            // INSTANT MODIFIER APPLICATION: Apply to existing projectiles immediately
            // This makes modifiers work mid-flight/mid-spawn for responsive gameplay
            ProjectileCardModifiers.Instance.ApplyModifiersToExistingProjectiles(this);
        }
        
        // Check if this exact card instance is already added (PASSIVE only)
        // If so, just increment stack count, don't add again
        if (spawner != null)
        {
            if (projectileType == ProjectileType.HolyShield)
            {
                bool isFirstSelection = !HolyShield.HasEverSpawned;
                if (isFirstSelection)
                {
                    spawner.SpawnHolyShieldImmediate(this);
                }
            }
            else
            {
                bool alreadyAdded = spawner.HasProjectile(this);

                if (!alreadyAdded)
                {
                    spawner.AddProjectile(this);
                }
            }
        }

        // ACTIVE projectile cards are driven by AdvancedPlayerController's auto-fire system
        if (projectileSystem == ProjectileSystemType.Active)
        {
            AdvancedPlayerController controller = player.GetComponent<AdvancedPlayerController>();
            if (controller != null)
            {
                controller.RegisterActiveProjectileCard(this);
            }
        }

        // Increment stack count
        stackCount++;
        Debug.Log($"<color=cyan>{cardName} ({rarity}) stacked {stackCount} times. Interval: {runtimeSpawnInterval:F2}s</color>");
        
        // Track projectile levels for Enhanced system - PER CARD, not per type.
        // The VERY FIRST active projectile selected at game start is treated as a
        // free unlock: it should not increase level/enhancement progression AND
        // it should not receive any modifiers.
        if (!skipInitialActiveLevelGain && ProjectileCardLevelSystem.Instance != null)
        {
            ProjectileCardLevelSystem.Instance.AddLevels(this, rarity);
            Debug.Log($"<color=cyan>{cardName} card leveled up to {ProjectileCardLevelSystem.Instance.GetLevel(this)}</color>");
        }

        // One-shot flag: ensure it does not affect any future calls on this instance
        suppressLevelGainOnce = false;
    }

    private bool ShouldSkipInitialActiveLevelGain()
    {
        // Only applies to ACTIVE projectile system cards and only when explicitly requested
        // by the creator of this runtime instance (e.g. initial active selection at game start).
        if (projectileSystem != ProjectileSystemType.Active)
        {
            return false;
        }

        return suppressLevelGainOnce;
    }
    
    private bool ShouldSkipFirstBaseRarityModifiers()
    {
        return false;
    }
    
    private global::ProjectileType ConvertToLevelSystemType(ProjectileCards.ProjectileType cardProjectileType)
    {
        switch (cardProjectileType)
        {
            case ProjectileCards.ProjectileType.FireTalon:
            case ProjectileCards.ProjectileType.Firebolt:
            case ProjectileCards.ProjectileType.FireMine:
            case ProjectileCards.ProjectileType.CinderBloom:
                return global::ProjectileType.Fire;
                
            case ProjectileCards.ProjectileType.IceTalon:
            case ProjectileCards.ProjectileType.CryoBloom:
                return global::ProjectileType.Ice;
                
            case ProjectileCards.ProjectileType.ThunderBird:
                return global::ProjectileType.Thunder;
                
            case ProjectileCards.ProjectileType.NuclearStrike:
            case ProjectileCards.ProjectileType.Collapse:
            case ProjectileCards.ProjectileType.HolyShield:
                return global::ProjectileType.Nuclear;
                
            case ProjectileCards.ProjectileType.Tornado:
                return global::ProjectileType.Tornado;
                
            case ProjectileCards.ProjectileType.ElementalBeam:
                return global::ProjectileType.Laser;
                
            default:
                return global::ProjectileType.Fire; // Default to Fire if unknown
        }
    }
    
    private void SelectRandomModifiers()
    {
        selectedModifiers.Clear();

        // Filter available modifiers for this projectile type and rarity unlock
        var validModifiers = availableModifiers.FindAll(m =>
            m != null &&
            m.IsEnabledForProjectile(projectileType) &&
            m.IsUnlockedForRarity(rarity));

        Debug.Log($"<color=yellow>SelectRandomModifiers for {cardName}: availableModifiers.Count={availableModifiers.Count}, validModifiers.Count={validModifiers.Count}, rarity={rarity}</color>");
        
        if (validModifiers.Count == 0)
        {
            Debug.LogWarning($"<color=red>CRITICAL: {cardName} has NO valid modifiers for rarity {rarity}! availableModifiers.Count={availableModifiers.Count}, projectileType={projectileType}</color>");
            Debug.LogWarning($"<color=red>This usually means modifiers are not assigned in the inspector or all are locked behind higher rarities.</color>");
            return;
        }

        bool useDeterministic = ProjectileCardLevelSystem.Instance != null &&
                                ProjectileCardLevelSystem.Instance.useDeterministicProjectileModifiers;
        if (useDeterministic)
        {
            for (int i = 0; i < validModifiers.Count; i++)
            {
                ProjectileModifierData mod = validModifiers[i];
                selectedModifiers.Add(mod);
                Debug.Log($"<color=lime>Deterministic modifier {i+1}/{validModifiers.Count}: {mod.type}</color>");
            }
        }
        else
        {
            // Randomize order without repetition, but keep ALL unlocked modifiers.
            int remaining = validModifiers.Count;
            for (int i = 0; i < remaining && validModifiers.Count > 0; i++)
            {
                int randomIndex = Random.Range(0, validModifiers.Count);
                ProjectileModifierData mod = validModifiers[randomIndex];
                selectedModifiers.Add(mod);
                Debug.Log($"<color=lime>Selected modifier {i+1}/{remaining}: {mod.type}</color>");
                validModifiers.RemoveAt(randomIndex); // Don't select same modifier twice
            }
        }

        Debug.Log($"<color=cyan>{cardName} ({rarity}) selected {selectedModifiers.Count} modifiers for {projectileType}</color>");
    }
    
    private int GetModifierCountForRarity(CardRarity rarity)
    {
        // Use ProjectileCardLevelSystem for modifier counts
        if (ProjectileCardLevelSystem.Instance != null)
        {
            return ProjectileCardLevelSystem.Instance.GetModifierCountForRarity(rarity);
        }
        
        // Fallback if ProjectileCardLevelSystem not available
        switch (rarity)
        {
            case CardRarity.Common: return 1;
            case CardRarity.Uncommon: return 2;
            case CardRarity.Rare: return 2;
            case CardRarity.Epic: return 2;
            case CardRarity.Legendary: return 3;
            case CardRarity.Mythic: return 3;
            default: return 1;
        }
    }
    
    // Removed ApplyModifiersToPlayer - now using per-card modifier system

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
        // Preserve existing behavior: base description followed by the
        // formatted modifiers block (if any).
        string baseDesc = GetBaseDescriptionOnly();
        string modifiersDesc = GetModifiersDescription();

        if (!string.IsNullOrEmpty(modifiersDesc))
        {
            if (string.IsNullOrEmpty(baseDesc))
            {
                return modifiersDesc;
            }

            return baseDesc + "\n" + modifiersDesc;
        }

        return baseDesc;
    }

    public string GetBaseDescriptionOnly()
    {
        string baseDesc = "";

        // If custom description is provided, use it
        if (!string.IsNullOrEmpty(description))
        {
            baseDesc = description;
        }
        else
        {
            // Otherwise use default descriptions
            baseDesc = GetDefaultDescription();
        }

        return baseDesc;
    }

    public string GetModifiersDescription()
    {
        if (selectedModifiers == null || selectedModifiers.Count == 0)
        {
            return string.Empty;
        }

        // Get alignment tag
        string alignmentTag = GetAlignmentTag(modifierAlignment);
        string alignmentCloseTag = string.IsNullOrEmpty(alignmentTag) ? "" : "</align>";

        // Build header with custom formatting
        string headerFormatted = modifierHeaderText;

        // Apply color
        if (!string.IsNullOrEmpty(modifierHeaderColor))
        {
            headerFormatted = $"<color={modifierHeaderColor}>{headerFormatted}</color>";
        }

        // Apply outline
        if (!string.IsNullOrEmpty(modifierHeaderOutlineColor))
        {
            headerFormatted = $"<outline={modifierHeaderOutlineColor}>{headerFormatted}</outline>";
        }

        // Apply font size
        if (modifierHeaderFontSize != 14)
        {
            headerFormatted = $"<size={modifierHeaderFontSize}>{headerFormatted}</size>";
        }

        // Apply alignment
        if (!string.IsNullOrEmpty(alignmentTag))
        {
            headerFormatted = $"{alignmentTag}{headerFormatted}{alignmentCloseTag}";
        }

        // Add spacing above header
        string spacing = "";
        for (int i = 0; i < modifierHeaderSpacing; i++)
        {
            spacing += "\n";
        }

        // Start with spacing + header (no leading newline). The caller is
        // responsible for inserting the newline between the base description
        // and this block.
        string result = spacing + headerFormatted;

        // Add each modifier with custom font size and alignment
        var orderedModifiers = selectedModifiers
            .Where(m => m != null)
            .OrderBy(m => m.displayPosition)
            .ToList();

        foreach (var modifier in orderedModifiers)
        {
            string modDesc = modifier.GetFormattedDescription(rarity);

            // Apply font size to modifier
            if (modifierFontSize != 12)
            {
                modDesc = $"<size={modifierFontSize}>{modDesc}</size>";
            }

            // Apply alignment to modifier line
            if (!string.IsNullOrEmpty(alignmentTag))
            {
                modDesc = $"{alignmentTag}• {modDesc}{alignmentCloseTag}";
                result += "\n" + modDesc;
            }
            else
            {
                result += "\n• " + modDesc;
            }
        }

        return result;
    }
    
    private string GetAlignmentTag(TextAlignment alignment)
    {
        switch (alignment)
        {
            case TextAlignment.Left:
                return "<align=\"left\">";
            case TextAlignment.Center:
                return "<align=\"center\">";
            case TextAlignment.Right:
                return "<align=\"right\">";
            default:
                return "";
        }
    }
    
    private string GetDefaultDescription()
    {
        switch (projectileType)
        {
            case ProjectileType.FireTalon:
                return $"Spawn Fire Talon every {spawnInterval}s on left side";
            case ProjectileType.IceTalon:
                return $"Spawn Ice Talon every {spawnInterval}s on right side";
            case ProjectileType.Tornado:
                return $"Spawn Tornado every {spawnInterval}s";
            case ProjectileType.Firebolt:
                return $"Spawn Firebolt every {spawnInterval}s";
            case ProjectileType.FireMine:
                return $"Spawn Fire Mine every {spawnInterval}s that explodes on contact";
            case ProjectileType.ThunderBird:
                return $"Spawn Thunder Bird every {spawnInterval}s that flies across the screen";
            case ProjectileType.NuclearStrike:
                return $"Spawn Nuclear Strike every {spawnInterval}s that drops from above and explodes";
            case ProjectileType.ElementalBeam:
                return $"Spawn Elemental Beam every {spawnInterval}s that continuously damages enemies";
            case ProjectileType.CinderBloom:
                return $"Spawn Cinder Bloom every {spawnInterval}s that spits fire in 5 directions";
            case ProjectileType.CryoBloom:
                return $"Spawn Cryo Bloom every {spawnInterval}s that spits ice in 5 directions";
            case ProjectileType.Collapse:
                return $"Spawn Collapse every {spawnInterval}s that pulls enemies toward its center";
            case ProjectileType.HolyShield:
                return "Summon a Holy Shield that absorbs damage instead of the player";
            default:
                return description;
        }
    }
}