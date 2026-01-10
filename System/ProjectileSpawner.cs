using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Reflection;

public class ProjectileSpawner : MonoBehaviour
{
    [Header("Projectile Count Spread Pattern")]
    [Tooltip("Enable spread pattern when spawning multiple projectiles (Talon, FireBolt, etc.)")]
    public bool useSpreadPattern = false;
    
    [Tooltip("Total spread angle in degrees (e.g., 20 = projectiles spread across 20 degrees)")]
    [Range(0f, 90f)]
    public float spreadDegrees = 20f;
    
    private List<ProjectileSpawnData> activeProjectiles = new List<ProjectileSpawnData>();
    private ProjectileModifierApplier modifierApplier;
    private PlayerStats playerStats;
    
    // Track active ElementalBeam stagger coroutines to prevent overlapping spawns
    private Dictionary<string, Coroutine> activeBeamCoroutines = new Dictionary<string, Coroutine>();

    // Cancel any running staggered ElementalBeam coroutine for this card so that
    // when the card becomes enhanced (e.g., Variant 3), no leftover base-version
    // beams from the staggered sequence continue firing and visually desync the
    // first enhanced volley.
    private void CancelStaggeredBeamsForCard(ProjectileCards card)
    {
        if (card == null)
        {
            return;
        }

        string key = card.cardName;
        if (activeBeamCoroutines.TryGetValue(key, out var routine) && routine != null)
        {
            StopCoroutine(routine);
        }

        if (activeBeamCoroutines.ContainsKey(key))
        {
            activeBeamCoroutines.Remove(key);
        }
    }

    private class ProjectileSpawnData
    {
        public ProjectileCards card;
        public float nextSpawnTime;
        public bool isFirstSpawn;

        public ProjectileSpawnData(ProjectileCards projectileCard)
        {
            card = projectileCard;
            isFirstSpawn = true;
            
            // Use runtime interval if set, otherwise fall back to inspector value
            float interval = projectileCard.runtimeSpawnInterval > 0 ? projectileCard.runtimeSpawnInterval : projectileCard.spawnInterval;
            
            // Apply first-time spawn cooldown reduction
            if (isFirstSpawn && projectileCard.firstTimeSelectionCooldownReduction > 0)
            {
                interval *= (1f - projectileCard.firstTimeSelectionCooldownReduction);
                Debug.Log($"<color=gold>{projectileCard.cardName} FIRST SPAWN: Cooldown reduced to {interval:F2}s</color>");
            }
            
            nextSpawnTime = Time.time + interval;
        }
    }

    public void RefreshCooldownForPassiveCard(ProjectileCards card)
    {
        if (card == null)
        {
            return;
        }

        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            ProjectileSpawnData data = activeProjectiles[i];
            if (data == null || data.card == null)
            {
                continue;
            }

            // Match either by exact card instance OR by shared prefab+type so
            // that new modifier cards which reuse the same projectile prefab
            // (ElementalBeam upgrades, etc.) can still refresh the existing
            // passive spawn entry.
            bool sameInstance = (data.card == card);
            bool samePrefabAndType =
                data.card.projectilePrefab == card.projectilePrefab &&
                data.card.projectileType == card.projectileType;

            if (sameInstance || samePrefabAndType)
            {
                data.nextSpawnTime = Time.time;
            }
        }
    }

    public void RescheduleCooldownForPassiveCard(ProjectileCards card)
    {
        if (card == null)
        {
            return;
        }

        if (playerStats == null)
        {
            playerStats = GetComponent<PlayerStats>();
        }

        float baseInterval = card.runtimeSpawnInterval > 0f ? card.runtimeSpawnInterval : card.spawnInterval;
        float finalInterval = baseInterval;
        if (playerStats != null && playerStats.projectileCooldownReduction > 0f)
        {
            float totalCdr = Mathf.Max(0f, playerStats.projectileCooldownReduction);
            finalInterval = baseInterval / (1f + totalCdr);
        }

        if (MinCooldownManager.Instance != null)
        {
            finalInterval = MinCooldownManager.Instance.ClampCooldown(card, finalInterval);
        }
        else
        {
            finalInterval = Mathf.Max(0.1f, finalInterval);
        }

        float now = Time.time;
        const float minDelay = 0.05f;

        for (int i = 0; i < activeProjectiles.Count; i++)
        {
            ProjectileSpawnData data = activeProjectiles[i];
            if (data == null || data.card == null)
            {
                continue;
            }

            // Match either by exact card instance OR by shared prefab+type.
            bool sameInstance = (data.card == card);
            bool samePrefabAndType =
                data.card.projectilePrefab == card.projectilePrefab &&
                data.card.projectileType == card.projectileType;

            if (!sameInstance && !samePrefabAndType)
            {
                continue;
            }

            // Preserve remaining cooldown (don't insta-spawn) but allow it to
            // become sooner if the new interval is shorter.
            float remaining = Mathf.Max(0f, data.nextSpawnTime - now);
            float cappedRemaining = Mathf.Min(remaining, finalInterval);
            data.nextSpawnTime = now + Mathf.Max(minDelay, cappedRemaining);
        }
    }

    private void Awake()
    {
        modifierApplier = GetComponent<ProjectileModifierApplier>();
        if (modifierApplier == null)
        {
            modifierApplier = gameObject.AddComponent<ProjectileModifierApplier>();
        }

        playerStats = GetComponent<PlayerStats>();
    }

    public void AddProjectile(ProjectileCards card)
    {
        // CRITICAL: Check if this projectile type is already active
        // Compare by prefab AND projectile type to prevent duplicate spawning
        foreach (var data in activeProjectiles)
        {
            // Same card reference - don't add duplicate
            if (data.card == card)
            {
                Debug.Log($"<color=yellow>{card.cardName} already active (same card reference) - preserving cooldown progress</color>");
                return;
            }
            
            // Same prefab AND same projectile type - update card reference to use latest modifiers
            if (data.card.projectilePrefab == card.projectilePrefab && 
                data.card.projectileType == card.projectileType)
            {
                Debug.Log($"<color=yellow>{card.cardName} already active (same prefab & type: {card.projectileType}) - UPDATING card reference to use latest modifiers</color>");
                data.card = card; // Update to new card instance to get latest modifiers
                return;
            }
        }
        
        // Check if this is a NovaStar or DwarfStar - start cycle immediately
        if (card.projectilePrefab != null)
        {
            NovaStar novaStar = card.projectilePrefab.GetComponent<NovaStar>();
            DwarfStar dwarfStar = card.projectilePrefab.GetComponent<DwarfStar>();
            
            if (novaStar != null || dwarfStar != null)
            {
                OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
                
                if (manager == null)
                {
                    Debug.LogWarning($"<color=yellow>OrbitalStarManager not found! Cannot spawn {card.cardName}. Please add OrbitalStarManager to the scene.</color>");
                    return;
                }
                
                // Start the appropriate star cycle based on component type
                if (novaStar != null)
                {
                    manager.StartNovaStarCycle();
                    Debug.Log($"<color=orange>NovaStar card picked! Starting NovaStar cycle.</color>");
                }
                else if (dwarfStar != null)
                {
                    manager.StartDwarfStarCycle();
                    Debug.Log($"<color=cyan>DwarfStar card picked! Starting DwarfStar cycle.</color>");
                }
                
                // Don't add to activeProjectiles - OrbitalStarManager handles spawning
                return;
            }
        }
        
        activeProjectiles.Add(new ProjectileSpawnData(card));
    }

    public bool HasProjectile(ProjectileCards card)
    {
        foreach (var data in activeProjectiles)
        {
            if (data.card == card)
            {
                return true;
            }
        }
        return false;
    }

    void Update()
    {
        foreach (var data in activeProjectiles)
        {
            if (Time.time >= data.nextSpawnTime)
            {
                // ElementalBeam smart targeting: if there are no enemies on-screen,
                // do not fire and do not consume cooldown. Instead, wait a short
                // delay and then rescan.
                if (data != null && data.card != null && data.card.projectilePrefab != null)
                {
                    ElementalBeam beamPrefab = data.card.projectilePrefab.GetComponent<ElementalBeam>();
                    if (beamPrefab != null && !beamPrefab.HasAnyOnScreenEnemy(transform.position))
                    {
                        float delay = Mathf.Max(0.05f, beamPrefab.NoEnemyTargetDelay);
                        data.nextSpawnTime = Time.time + delay;
                        continue;
                    }
                }

                SpawnProjectile(data);
                
                // Use runtime interval if set (includes any enhanced/variant-specific cooldowns),
                // otherwise fall back to inspector spawnInterval
                float interval = data.card.runtimeSpawnInterval > 0 ? data.card.runtimeSpawnInterval : data.card.spawnInterval;

                if (playerStats == null)
                {
                    playerStats = GetComponent<PlayerStats>();
                }

                float finalInterval = interval;
                if (playerStats != null && playerStats.projectileCooldownReduction > 0f)
                {
                    float totalCdr = Mathf.Max(0f, playerStats.projectileCooldownReduction);
                    finalInterval = interval / (1f + totalCdr);
                }

                if (MinCooldownManager.Instance != null)
                {
                    finalInterval = MinCooldownManager.Instance.ClampCooldown(data.card, finalInterval);
                }
                else
                {
                    finalInterval = Mathf.Max(0.1f, finalInterval);
                }

                // After first spawn (ever), just log that we now use the normal/enhanced interval
                if (data.isFirstSpawn)
                {
                    data.isFirstSpawn = false;
                    Debug.Log($"<color=gold>{data.card.cardName} first spawn complete, next spawn in {finalInterval:F2}s</color>");
                }
                
                data.nextSpawnTime = Time.time + finalInterval;
            }
        }
    }

    /// <summary>
    /// Apply the one-time ENHANCED first-spawn cooldown reduction for a specific card.
    /// This is called right when the card becomes enhanced, so the NEXT spawn happens
    /// sooner using the enhanced base cooldown.
    /// </summary>
    public void ApplyEnhancedFirstSpawnReduction(ProjectileCards card)
    {
        if (card == null || !card.applyEnhancedFirstSpawnReduction || card.enhancedFirstSpawnReduction <= 0f)
        {
            return;
        }

        // As soon as this card becomes enhanced, cancel any leftover BASE
        // ElementalBeam stagger coroutine so the very first enhanced volley
        // (e.g., Variant 3 with doubled projectile count) spawns all beams
        // together instead of having a few late staggered beams from the
        // previous basic pattern.
        CancelStaggeredBeamsForCard(card);

        foreach (var data in activeProjectiles)
        {
            if (data == null || data.card == null) continue;

            if (data.card != card) continue;

            // Base interval: use runtimeSpawnInterval if already set (may include enhanced cooldown),
            // otherwise fall back to inspector spawnInterval.
            float baseInterval = card.runtimeSpawnInterval > 0 ? card.runtimeSpawnInterval : card.spawnInterval;

            // Determine current enhanced variant (0 = none/basic)
            int enhancedVariant = 0;
            if (ProjectileCardLevelSystem.Instance != null)
            {
                enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(card);
            }

            // SPECIAL CASES: projectiles with their own enhanced base cooldowns
            if (card.projectilePrefab != null)
            {
                // ElementalBeam Variant 2 uses its own base cooldown
                ElementalBeam beamPrefab = card.projectilePrefab.GetComponent<ElementalBeam>();
                if (beamPrefab != null)
                {
                    if (enhancedVariant == 2)
                    {
                        baseInterval = beamPrefab.variant2BaseCooldown;
                    }
                }

                // ThunderBird enhanced variants can expose dedicated base cooldowns.
                ThunderBird birdPrefab = card.projectilePrefab.GetComponent<ThunderBird>();
                if (birdPrefab != null)
                {
                    bool hasVariant1 = ProjectileCardLevelSystem.Instance != null && ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 1);
                    bool hasVariant2 = ProjectileCardLevelSystem.Instance != null && ProjectileCardLevelSystem.Instance.HasChosenVariant(card, 2);

                    if (hasVariant1 && hasVariant2 && birdPrefab.variant12BaseCooldown > 0f)
                    {
                        baseInterval = birdPrefab.variant12BaseCooldown;
                        Debug.Log($"<color=gold>{card.cardName}: Using ThunderBird.variant12BaseCooldown = {birdPrefab.variant12BaseCooldown:F2}s as enhanced base cooldown (V1+V2 stack)</color>");
                    }
                    else if (enhancedVariant == 2)
                    {
                        baseInterval = birdPrefab.variant2BaseCooldown;
                        Debug.Log($"<color=gold>{card.cardName}: Using ThunderBird.variant2BaseCooldown = {birdPrefab.variant2BaseCooldown:F2}s as enhanced base cooldown</color>");
                    }
                }

                // GENERIC FALLBACK: If other enhanced variants expose a float field named
                // "variant{enhancedVariant}BaseCooldown" on any component attached to the projectile
                // prefab, use that as the base interval as well.
                if (enhancedVariant > 0)
                {
                    string fieldName = $"variant{enhancedVariant}BaseCooldown";
                    MonoBehaviour[] components = card.projectilePrefab.GetComponents<MonoBehaviour>();
                    foreach (var comp in components)
                    {
                        if (comp == null) continue;
                        FieldInfo fi = comp.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (fi != null && fi.FieldType == typeof(float))
                        {
                            object value = fi.GetValue(comp);
                            if (value is float f && f > 0f)
                            {
                                baseInterval = f;
                                Debug.Log($"<color=gold>{card.cardName}: Using {comp.GetType().Name}.{fieldName} = {f:F2}s as enhanced base cooldown</color>");
                                break;
                            }
                        }
                    }
                }
            }

            float reductionFactor = Mathf.Clamp01(1f - card.enhancedFirstSpawnReduction);
            float reducedInterval = baseInterval * reductionFactor;

            data.nextSpawnTime = Time.time + reducedInterval;
            Debug.Log($"<color=gold>{card.cardName} ENHANCED FIRST SPAWN: scheduling next spawn in {reducedInterval:F2}s (base {baseInterval:F2}s, reduction {card.enhancedFirstSpawnReduction * 100f:F0}%)</color>");
            return;
        }
    }

    void SpawnProjectile(ProjectileSpawnData data)
    {
        if (data.card.projectilePrefab == null) return;

        // Check if this is a FireMine
        FireMine fireMine = data.card.projectilePrefab.GetComponent<FireMine>();
        
        if (fireMine != null)
        {
            // Get modifiers to check projectile count
            CardModifierStats mineModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
            // Base count is 1, modifiers ADD to this
            int mineProjectileCount = 1 + mineModifiers.projectileCount;

            // CRITICAL: Add enhanced projectile count bonus for FireMine instantly,
            // so the very first spawn after enhancement already uses the extra mines.
            if (ProjectileCardLevelSystem.Instance != null)
            {
                int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);

                // SAFETY: If the card has already reached the enhanced unlock level but
                // GetEnhancedVariant still reports 0 for this frame, treat it as Variant 1
                // so the very first enhanced spawn immediately gets the extra mine(s).
                if (enhancedVariant == 0 && ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(data.card))
                {
                    enhancedVariant = 1;
                    Debug.Log($"<color=yellow>[FireMine Safety] {data.card.cardName} is enhanced-unlocked but has variant 0; treating as Variant 1 for projectile count.</color>");
                }

                if (enhancedVariant > 0)
                {
                    int enhancedBonus = GetFireMineEnhancedProjectileBonus(fireMine, enhancedVariant);
                    if (enhancedBonus > 0)
                    {
                        mineProjectileCount += enhancedBonus;
                        Debug.Log($"<color=gold>FireMine ENHANCED TIER {enhancedVariant}: Adding {enhancedBonus} bonus mines → Total: {mineProjectileCount}</color>");
                    }
                }
            }
            
            Debug.Log($"<color=cyan>Spawning {mineProjectileCount} FireMine(s) for {data.card.cardName} (base 1 + {mineModifiers.projectileCount} from modifiers + any enhanced bonus)</color>");
            
            Collider2D PlayerCollider = GetComponent<Collider2D>();
            
            // Spawn multiple FireMines if mineProjectileCount > 1
            for (int i = 0; i < mineProjectileCount; i++)
            {
                // FireMine handles its own spawn position logic (4-point system)
                GameObject mine = Instantiate(data.card.projectilePrefab, transform.position, Quaternion.identity);
                // Tag projectile with card reference
                ProjectileCardModifiers.Instance.TagProjectileWithCard(mine, data.card);
                
                FireMine mineComponent = mine.GetComponent<FireMine>();
                
                if (mineComponent != null)
                {
                    // CRITICAL: Only first projectile checks cooldown/mana
                    bool skipCheck = (i > 0);
                    mineComponent.Initialize(transform.position, PlayerCollider, skipCheck);
                    
                    if (skipCheck)
                    {
                        Debug.Log($"<color=gold>FireMine #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                    }
                }
                
                if (modifierApplier != null)
                {
                    modifierApplier.ApplyModifiersToProjectile(mine, data.card);
                }
            }
            
            return;
        }
        
        // Check if this is a CinderCryoBloom
        CinderCryoBloom cinderBloom = data.card.projectilePrefab.GetComponent<CinderCryoBloom>();
        
        if (cinderBloom != null)
        {
            // Get modifiers to check projectile count
            CardModifierStats bloomModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
            // Base count is 1, modifiers ADD to this
            int bloomProjectileCount = 1 + bloomModifiers.projectileCount;
            
            Debug.Log($"<color=cyan>Spawning {bloomProjectileCount} CinderBloom(s) for {data.card.cardName} (base 1 + {bloomModifiers.projectileCount} from modifiers)</color>");
            
            Collider2D PlayerCollider = GetComponent<Collider2D>();
            
            // Spawn multiple CinderBlooms if bloomProjectileCount > 1
            for (int i = 0; i < bloomProjectileCount; i++)
            {
                // CinderBloom handles its own spawn position logic (4-point system)
                GameObject bloom2 = Instantiate(data.card.projectilePrefab, transform.position, Quaternion.identity);
                // Tag projectile with card reference
                ProjectileCardModifiers.Instance.TagProjectileWithCard(bloom2, data.card);
                
                CinderCryoBloom bloomComponent = bloom2.GetComponent<CinderCryoBloom>();
                
                if (bloomComponent != null)
                {
                    // CRITICAL: Only first projectile checks cooldown/mana
                    bool skipCheck = (i > 0);
                    bloomComponent.Initialize(transform.position, PlayerCollider, skipCheck);
                    
                    if (skipCheck)
                    {
                        Debug.Log($"<color=gold>CinderBloom #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                    }
                }
                
                if (modifierApplier != null)
                {
                    modifierApplier.ApplyModifiersToProjectile(bloom2, data.card);
                }
            }
            
            return;
        }
        
        // Check if this is a ThunderBird
        ThunderBird thunderBird = data.card.projectilePrefab.GetComponent<ThunderBird>();
        
        if (thunderBird != null)
        {
            // Get modifiers to check projectile count - FRESH each spawn
            CardModifierStats birdModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
            Debug.Log($"<color=cyan>ThunderBird Spawn: Reading modifiers for {data.card.cardName}, projectileCount={birdModifiers.projectileCount}</color>");
            
            // Check if this is Variant 1 (dual spawn)
            int enhancedVariant = 0;
            bool hasVariant1History = false;
            bool hasVariant2History = false;
            bool isVariant12Active = false;
            if (ProjectileCardLevelSystem.Instance != null)
            {
                enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);
                hasVariant1History = ProjectileCardLevelSystem.Instance.HasChosenVariant(data.card, 1);
                hasVariant2History = ProjectileCardLevelSystem.Instance.HasChosenVariant(data.card, 2);
                isVariant12Active = hasVariant1History && hasVariant2History;
            }
            
            int birdProjectileCount;
            if (enhancedVariant == 1 || isVariant12Active)
            {
                // VARIANT 1: Spawns in PAIRS (left + right)
                // Base: 2 birds (1 left + 1 right)
                // Each +1 projectileCount: +2 birds (1 per side)
                birdProjectileCount = 2 + (birdModifiers.projectileCount * 2);
                Debug.Log($"<color=gold>═══ ThunderBird Variant 1 Spawn ═══</color>");
                Debug.Log($"<color=gold>Card: {data.card.cardName}</color>");
                Debug.Log($"<color=gold>ProjectileCount Modifier: {birdModifiers.projectileCount}</color>");
                Debug.Log($"<color=gold>Formula: 2 + ({birdModifiers.projectileCount} * 2) = {birdProjectileCount}</color>");
                Debug.Log($"<color=gold>Spawning {birdProjectileCount} birds total</color>");
            }
            else
            {
                // BASE or VARIANT 2: Normal spawn count
                // Base count is 1, modifiers ADD to this
                birdProjectileCount = 1 + birdModifiers.projectileCount;
                Debug.Log($"<color=cyan>Spawning {birdProjectileCount} ThunderBird(s) for {data.card.cardName} (base 1 + {birdModifiers.projectileCount} from modifiers)</color>");
            }
            
            Collider2D PlayerCollider = GetComponent<Collider2D>();
            
            // Spawn multiple ThunderBirds if birdProjectileCount > 1
            // ALL birds skip cooldown/mana check - they're spawned as a batch
            for (int i = 0; i < birdProjectileCount; i++)
            {
                // ThunderBird handles its own spawn position logic
                GameObject bird = Instantiate(data.card.projectilePrefab, transform.position, Quaternion.identity);
                // Tag projectile with card reference
                ProjectileCardModifiers.Instance.TagProjectileWithCard(bird, data.card);
                
                ThunderBird birdComponent = bird.GetComponent<ThunderBird>();
                
                if (birdComponent != null)
                {
                    // CRITICAL: ALL birds skip cooldown/mana check (checked by ProjectileSpawner before loop)
                    birdComponent.Initialize(transform.position, PlayerCollider, true);
                    Debug.Log($"<color=gold>ThunderBird #{i+1}/{birdProjectileCount}: Spawned (cooldown/mana pre-checked)</color>");
                }
                
                if (modifierApplier != null)
                {
                    modifierApplier.ApplyModifiersToProjectile(bird, data.card);
                }
            }
            
            return;
        }
        
        // Check if this is a NuclearStrike
        NuclearStrike nuclearStrike = data.card.projectilePrefab.GetComponent<NuclearStrike>();
        
        if (nuclearStrike != null)
        {
            // Get modifiers to check projectile count
            CardModifierStats nukeModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
            // Base count is 1, modifiers ADD to this. This value is recalculated
            // fresh on EVERY spawn so that any enhancedProjectileCountBonus is
            // applied immediately on the very first enhanced spawn.
            int nukeProjectileCount = 1 + nukeModifiers.projectileCount;

            // CRITICAL: Add enhanced projectile count bonus for NuclearStrike instantly,
            // so the very first spawn after enhancement already uses the extra strikes.
            if (ProjectileCardLevelSystem.Instance != null)
            {
                int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);

                // SAFETY: Same pattern as Talons/FireMine - if the card is enhanced-unlocked
                // but variant is still 0 for this frame, treat it as Variant 1 for count.
                if (enhancedVariant == 0 && ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(data.card))
                {
                    enhancedVariant = 1;
                    Debug.Log($"<color=yellow>[NuclearStrike Safety] {data.card.cardName} is enhanced-unlocked but has variant 0; treating as Variant 1 for projectile count.</color>");
                }

                if (enhancedVariant >= 1)
                {
                    int enhancedBonus = nuclearStrike.enhancedProjectileCountBonus;
                    if (enhancedBonus > 0)
                    {
                        nukeProjectileCount += enhancedBonus;
                        Debug.Log($"<color=gold>NuclearStrike ENHANCED: Adding {enhancedBonus} bonus strikes → Total: {nukeProjectileCount}</color>");
                    }
                }
            }
            
            Debug.Log($"<color=cyan>Spawning {nukeProjectileCount} NuclearStrike(s) for {data.card.cardName} (base 1 + {nukeModifiers.projectileCount} from modifiers + any enhanced bonus)</color>");
            
            Collider2D PlayerCollider = GetComponent<Collider2D>();
            
            // Spawn multiple NuclearStrikes if nukeProjectileCount > 1
            for (int i = 0; i < nukeProjectileCount; i++)
            {
                // NuclearStrike handles its own spawn position logic
                GameObject strike = Instantiate(data.card.projectilePrefab, transform.position, Quaternion.identity);
                // Tag projectile with card reference
                ProjectileCardModifiers.Instance.TagProjectileWithCard(strike, data.card);
                
                NuclearStrike strikeComponent = strike.GetComponent<NuclearStrike>();
                
                if (strikeComponent != null)
                {
                    // CRITICAL: Only first projectile checks cooldown/mana
                    // Additional projectiles skip these checks
                    bool skipCheck = (i > 0);
                    strikeComponent.Initialize(transform.position, PlayerCollider, skipCheck);
                    
                    if (skipCheck)
                    {
                        Debug.Log($"<color=gold>NuclearStrike #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                    }
                }
                
                if (modifierApplier != null)
                {
                    modifierApplier.ApplyModifiersToProjectile(strike, data.card);
                }
            }
            
            return;
        }
        
        // Check if this is a Collapse
        Collapse collapse = data.card.projectilePrefab.GetComponent<Collapse>();

        if (collapse != null)
        {
            CardModifierStats collapseModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
            int collapseProjectileCount = 1 + collapseModifiers.projectileCount;

            Debug.Log($"<color=cyan>Spawning {collapseProjectileCount} Collapse(s) for {data.card.cardName} (base 1 + {collapseModifiers.projectileCount} from modifiers)</color>");

            Collider2D PlayerCollider = GetComponent<Collider2D>();

            for (int i = 0; i < collapseProjectileCount; i++)
            {
                GameObject collapseObj = Instantiate(data.card.projectilePrefab, transform.position, Quaternion.identity);
                ProjectileCardModifiers.Instance.TagProjectileWithCard(collapseObj, data.card);

                Collapse collapseComponent = collapseObj.GetComponent<Collapse>();

                if (collapseComponent != null)
                {
                    bool skipCheck = (i > 0);
                    collapseComponent.Initialize(transform.position, PlayerCollider, skipCheck);

                    if (skipCheck)
                    {
                        Debug.Log($"<color=gold>Collapse #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                    }
                }

                if (modifierApplier != null)
                {
                    modifierApplier.ApplyModifiersToProjectile(collapseObj, data.card);
                }
            }

            return;
        }

        HolyShield holyShield = data.card.projectilePrefab.GetComponent<HolyShield>();

        if (holyShield != null)
        {
            return;
        }
        
        // Check if this is a CinderCryoBloom
        CinderCryoBloom bloom = data.card.projectilePrefab.GetComponent<CinderCryoBloom>();
        
        if (bloom != null)
        {
            // Get modifiers to check projectile count
            CardModifierStats bloomModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
            // Base count is 1, modifiers ADD to this
            int bloomProjectileCount = 1 + bloomModifiers.projectileCount;
            
            Debug.Log($"<color=cyan>Spawning {bloomProjectileCount} CinderCryoBloom(s) for {data.card.cardName} (base 1 + {bloomModifiers.projectileCount} from modifiers)</color>");
            
            Collider2D PlayerCollider = GetComponent<Collider2D>();
            
            // Spawn multiple Blooms if bloomProjectileCount > 1
            for (int i = 0; i < bloomProjectileCount; i++)
            {
                // CinderCryoBloom handles its own spawn position logic (4-point system)
                GameObject bloomObj = Instantiate(data.card.projectilePrefab, transform.position, Quaternion.identity);
                // Tag projectile with card reference
                ProjectileCardModifiers.Instance.TagProjectileWithCard(bloomObj, data.card);
                
                CinderCryoBloom bloomComponent = bloomObj.GetComponent<CinderCryoBloom>();
                
                if (bloomComponent != null)
                {
                    bloomComponent.Initialize(transform.position, PlayerCollider);
                }
                
                if (modifierApplier != null)
                {
                    modifierApplier.ApplyModifiersToProjectile(bloomObj, data.card);
                }
            }
            
            return;
        }
        
        // Check if this is a NovaStar or DwarfStar
        NovaStar novaStar = data.card.projectilePrefab.GetComponent<NovaStar>();
        DwarfStar dwarfStar = data.card.projectilePrefab.GetComponent<DwarfStar>();
        
        if (novaStar != null || dwarfStar != null)
        {
            // Orbital stars are managed by OrbitalStarManager, not spawned directly here
            OrbitalStarManager manager = FindObjectOfType<OrbitalStarManager>();
            
            if (manager == null)
            {
                Debug.LogWarning($"<color=yellow>OrbitalStarManager not found! Cannot spawn {data.card.cardName}. Please add OrbitalStarManager to the scene.</color>");
                return;
            }
            
            // Start the appropriate star cycle based on type
            if (novaStar != null)
            {
                Debug.Log($"<color=orange>NovaStar projectile requested via SpawnProjectile; cycle is already managed by OrbitalStarManager.</color>");
            }
            else if (dwarfStar != null)
            {
                Debug.Log($"<color=cyan>DwarfStar projectile requested via SpawnProjectile; cycle is already managed by OrbitalStarManager.</color>");
            }
            
            // Projectile count doesn't apply to orbital stars (they have their own level system)
            return;
        }

        // Regular projectile spawning
        // NOTE: For spawn together with custom angles, each projectile will get unique direction in loop
        Vector2 spawnDirection = GetSpawnDirection(data.card);

        // Calculate spawn position with offset based on direction
        Vector3 spawnPosition = transform.position;

        // Apply spawn offset for Talon variants and PlayerProjectiles using their GetSpawnOffset method
        if (data.card != null && data.card.projectilePrefab != null)
        {
            ProjectileFireTalon fireTalonComponent = data.card.projectilePrefab.GetComponent<ProjectileFireTalon>();
            ProjectileIceTalon iceTalonComponent = data.card.projectilePrefab.GetComponent<ProjectileIceTalon>();
            PlayerProjectiles playerProjComponent = data.card.projectilePrefab.GetComponent<PlayerProjectiles>();
            
            if (fireTalonComponent != null)
            {
                Vector2 offset = fireTalonComponent.GetSpawnOffset(spawnDirection);
                spawnPosition += new Vector3(offset.x, offset.y, 0f);
                Debug.Log($"<color=cyan>ProjectileSpawner: Applied FireTalon offset {offset} to spawn position</color>");
            }
            else if (iceTalonComponent != null)
            {
                Vector2 offset = iceTalonComponent.GetSpawnOffset(spawnDirection);
                spawnPosition += new Vector3(offset.x, offset.y, 0f);
                Debug.Log($"<color=cyan>ProjectileSpawner: Applied IceTalon offset {offset} to spawn position</color>");
            }
            else if (playerProjComponent != null)
            {
                Vector2 offset = playerProjComponent.GetSpawnOffset(spawnDirection);
                spawnPosition += new Vector3(offset.x, offset.y, 0f);
                Debug.Log($"<color=cyan>ProjectileSpawner: Applied PlayerProjectiles offset {offset} to spawn position</color>");
            }
        }

        // Calculate spawn rotation from direction
        float angle = Mathf.Atan2(spawnDirection.y, spawnDirection.x) * Mathf.Rad2Deg;
        Quaternion spawnRotation = Quaternion.Euler(0f, 0f, angle);

        // Get modifiers to check projectile count
        CardModifierStats projModifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
        // Base count is 1, modifiers ADD to this
        int projCount = 1 + projModifiers.projectileCount;
        
        // CRITICAL: Add enhanced projectile count bonus for Talon
        // Check if this is a Talon. Variant 1 (Multi-Pierce) should grant extra
        // projectiles PERMANENTLY once it has ever been chosen, regardless of the
        // current enhancedVariant index.
        ProjectileFireTalon fireTalon = data.card.projectilePrefab.GetComponent<ProjectileFireTalon>();
        ProjectileIceTalon iceTalon = data.card.projectilePrefab.GetComponent<ProjectileIceTalon>();
        
        if ((fireTalon != null || iceTalon != null) && ProjectileCardLevelSystem.Instance != null)
        {
            bool hasVariant1History = ProjectileCardLevelSystem.Instance.HasChosenVariant(data.card, 1);
            bool applyVariant1MultiShot = hasVariant1History;

            // SAFETY: Preserve the pre-existing behavior where, if the card has
            // just become enhanced-unlocked but no variant is selected yet
            // (GetEnhancedVariant == 0), we still treat it as Variant 1 for the
            // very first enhanced spawn so the player immediately feels the power
            // spike.
            if (!applyVariant1MultiShot)
            {
                int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);
                if (enhancedVariant == 0 && ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(data.card))
                {
                    applyVariant1MultiShot = true;
                    Debug.Log($"<color=yellow>[Talon Safety] {data.card.cardName} is enhanced-unlocked but has variant 0; treating as Variant 1 for projectile count.</color>");
                }
            }

            if (applyVariant1MultiShot)
            {
                // Get enhanced bonus from the Talon component (Fire or Ice)
                int enhancedBonus = fireTalon != null ? fireTalon.enhancedProjectileCountBonus : iceTalon.enhancedProjectileCountBonus;
                projCount += enhancedBonus;
                Debug.Log($"<color=gold>Talon VARIANT 1 (history): Adding {enhancedBonus} bonus projectiles → Total: {projCount}</color>");
            }
        }

        Debug.Log($"<color=lime>╔═══════════════════════════════════════════════════════════╗</color>");
        Debug.Log($"<color=lime>║   PROJECTILE COUNT DEBUG - {data.card.cardName}</color>");
        Debug.Log($"<color=lime>╚═══════════════════════════════════════════════════════════╝</color>");
        Debug.Log($"  Card Instance: {data.card.GetInstanceID()}");
        Debug.Log($"  Prefab: {data.card.projectilePrefab.name}");
        Debug.Log($"  ProjectileType: {data.card.projectileType}");
        Debug.Log($"  modifiers.projectileCount: {projModifiers.projectileCount}");
        Debug.Log($"  TOTAL projCount: {projCount} (1 base + {projModifiers.projectileCount} modifier)");
        Debug.Log($"<color=cyan>Spawning {projCount} projectile(s) for {data.card.cardName}</color>");
        
        // Get player components for Launch methods
        Collider2D playerCollider = GetComponent<Collider2D>();
        PlayerMana playerMana = GetComponent<PlayerMana>();
        
        // Store original player position for ElementalBeam offset calculation
        Vector3 originalPlayerPosition = transform.position;
        
        // Check if this is ElementalBeam for special handling
        ElementalBeam beamPrefab = data.card.projectilePrefab.GetComponent<ElementalBeam>();
        bool isElementalBeam = beamPrefab != null;

        // Determine whether this ElementalBeam is currently using an enhanced
        // variant. Base FireBeam (enhancedVariant == 0 AND not yet enhanced-
        // unlocked) will use staggered timing when projectileCountBonus > 0;
        // enhanced/stacked variants keep the "all at once" volley behaviour.
        bool isEnhancedElementalBeam = false;
        if (isElementalBeam && ProjectileCardLevelSystem.Instance != null)
        {
            int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);
            bool isEnhancedUnlocked = ProjectileCardLevelSystem.Instance.IsEnhancedUnlocked(data.card);

            // SAFETY: As soon as the card has any enhanced variant unlocked,
            // treat it as "enhanced" for spawn behaviour even if the variant
            // index has not yet been updated this frame.
            if (enhancedVariant != 0 || isEnhancedUnlocked)
            {
                isEnhancedElementalBeam = true;
            }

            Debug.Log($"<color=orange>ElementalBeam Variant Check: card={data.card.cardName}, enhancedVariant={enhancedVariant}, isEnhancedUnlocked={isEnhancedUnlocked}, isEnhancedElementalBeam={isEnhancedElementalBeam}</color>");
        }

        if (isElementalBeam)
        {
            float baseInterval = data.card.runtimeSpawnInterval > 0f
                ? data.card.runtimeSpawnInterval
                : data.card.spawnInterval;
            Debug.Log($"<color=orange>ElementalBeam Spawn: card={data.card.cardName}, enhanced={isEnhancedElementalBeam}, projCount={projCount}, runtimeInterval={data.card.runtimeSpawnInterval:F3}, spawnInterval={data.card.spawnInterval:F3}, chosenBaseInterval={baseInterval:F3}</color>");
        }

        // BASE ElementalBeam (FireBeam): when projectileCountBonus is present
        // and no enhanced variant is active, spawn beams staggered evenly across
        // the cooldown so that the first beam fires at cooldown / totalBeams and
        // the last beam at cooldown.
        if (isElementalBeam && !isEnhancedElementalBeam && projCount > 1)
        {
            string cardKey = data.card.cardName;

            // IMPORTANT: Do NOT cancel any existing stagger coroutine. Each
            // spawn interval should produce its own full set of beams so the
            // total beams per cooldown remains consistent over time.
            if (activeBeamCoroutines.TryGetValue(cardKey, out var existingCoroutine))
            {
                if (existingCoroutine == null)
                {
                    activeBeamCoroutines.Remove(cardKey);
                }
                else
                {
                    Debug.Log($"<color=orange>ElementalBeam STAGGERED: Existing coroutine still running for {cardKey}; starting an additional stagger sequence for the next volley.</color>");
                }
            }

            Coroutine routine = StartCoroutine(SpawnElementalBeamsStaggered(
                data,
                spawnPosition,
                spawnDirection,
                projCount,
                playerCollider,
                playerMana
            ));

            activeBeamCoroutines[cardKey] = routine;
            Debug.Log($"<color=orange>ElementalBeam STAGGERED: Started stagger coroutine for {cardKey} with count={projCount}.</color>");
            return;
        }
        
        // Track used angles for minimum separation
        List<float> usedAngles = new List<float>();
        
        // Spawn multiple projectiles if projCount > 1
        for (int i = 0; i < projCount; i++)
        {
            // CRITICAL FIX: Each projectile gets unique random direction if custom angles enabled
            Vector2 currentSpawnDirection;
            if (data.card.useCustomAngles && projCount > 1)
            {
                // Check if this is a Talon variant projectile with minimum angle separation
                ProjectileFireTalon fireTalonCheck = data.card.projectilePrefab.GetComponent<ProjectileFireTalon>();
                ProjectileIceTalon iceTalonCheck = data.card.projectilePrefab.GetComponent<ProjectileIceTalon>();
                
                if (fireTalonCheck != null && fireTalonCheck.minAngleSeparation > 0f)
                {
                    // Generate direction with minimum angle separation
                    currentSpawnDirection = GetDirectionWithMinSeparation(data.card, usedAngles, fireTalonCheck.minAngleSeparation);
                    float currentAngle = Mathf.Atan2(currentSpawnDirection.y, currentSpawnDirection.x) * Mathf.Rad2Deg;
                    usedAngles.Add(currentAngle);
                    Debug.Log($"<color=cyan>Projectile #{i+1}/{projCount}: Direction with {fireTalonCheck.minAngleSeparation}° separation = {currentSpawnDirection}, angle = {currentAngle:F1}°</color>");
                }
                else if (iceTalonCheck != null && iceTalonCheck.minAngleSeparation > 0f)
                {
                    // Generate direction with minimum angle separation
                    currentSpawnDirection = GetDirectionWithMinSeparation(data.card, usedAngles, iceTalonCheck.minAngleSeparation);
                    float currentAngle = Mathf.Atan2(currentSpawnDirection.y, currentSpawnDirection.x) * Mathf.Rad2Deg;
                    usedAngles.Add(currentAngle);
                    Debug.Log($"<color=cyan>Projectile #{i+1}/{projCount}: Direction with {iceTalonCheck.minAngleSeparation}° separation = {currentSpawnDirection}, angle = {currentAngle:F1}°</color>");
                }
                else
                {
                    // Generate unique random direction for each projectile
                    currentSpawnDirection = GetSpawnDirection(data.card);
                    Debug.Log($"<color=cyan>Projectile #{i+1}/{projCount}: Unique random direction = {currentSpawnDirection}</color>");
                }
            }
            else
            {
                // Use shared direction (for non-custom-angle or single projectile)
                currentSpawnDirection = spawnDirection;
            }
            
            // Calculate angle offset for spread pattern (only if enabled)
            float angleOffset = 0f;
            if (useSpreadPattern && projCount > 1)
            {
                float step = spreadDegrees / (projCount - 1);
                angleOffset = -spreadDegrees / 2f + (step * i);
            }
            
            // Apply angle offset to spawn direction
            float baseAngle = Mathf.Atan2(currentSpawnDirection.y, currentSpawnDirection.x) * Mathf.Rad2Deg;
            float finalAngle = baseAngle + angleOffset;
            float finalAngleRad = finalAngle * Mathf.Deg2Rad;
            Vector2 finalDirection = new Vector2(Mathf.Cos(finalAngleRad), Mathf.Sin(finalAngleRad)).normalized;
            
            // Recalculate spawn rotation
            Quaternion finalRotation = Quaternion.Euler(0f, 0f, finalAngle);
            
            GameObject projectile = Instantiate(data.card.projectilePrefab, spawnPosition, finalRotation);
            
            // Tag projectile with card reference
            ProjectileCardModifiers.Instance.TagProjectileWithCard(projectile, data.card);

            // Set velocity immediately before any other initialization
            Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();
            ProjectileFireTalon fireTalon1 = projectile.GetComponent<ProjectileFireTalon>();
            ProjectileIceTalon iceTalon1 = projectile.GetComponent<ProjectileIceTalon>();
            ElementalBeam beam = projectile.GetComponent<ElementalBeam>();
            PlayerProjectiles playerProj = projectile.GetComponent<PlayerProjectiles>();

            if (fireTalon1 != null)
            {
                fireTalon1.SetDirection(finalDirection);
                // CRITICAL: Only first projectile checks cooldown/mana
                bool skipCheck = (i > 0);
                fireTalon1.Launch(finalDirection, playerCollider, playerMana, skipCheck);
                
                if (skipCheck)
                {
                    Debug.Log($"<color=gold>ProjectileFireTalon #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                }
            }
            else if (iceTalon1 != null)
            {
                iceTalon1.SetDirection(finalDirection);
                // CRITICAL: Only first projectile checks cooldown/mana
                bool skipCheck = (i > 0);
                iceTalon1.Launch(finalDirection, playerCollider, playerMana, skipCheck);
                
                if (skipCheck)
                {
                    Debug.Log($"<color=gold>ProjectileIceTalon #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                }
            }
            else if (beam != null)
            {
                // ElementalBeam doesn't move, just launch it with direction for rotation
                // CRITICAL: Only first projectile checks cooldown/mana
                // Pass original player position for offset calculation
                bool skipCheck = (i > 0);
                beam.Launch(finalDirection, playerCollider, playerMana, skipCheck, originalPlayerPosition);
                
                if (skipCheck)
                {
                    Debug.Log($"<color=gold>ElementalBeam #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                }
            }
            else if (playerProj != null)
            {
                // PlayerProjectiles (FireBolt, etc.) - call Launch() method
                // CRITICAL: Only first projectile checks cooldown/mana
                bool skipCheck = (i > 0);
                playerProj.Launch(finalDirection, playerCollider, playerMana, skipCheck);
                
                if (skipCheck)
                {
                    Debug.Log($"<color=gold>PlayerProjectiles #{i+1}: Spawned as additional projectile (skipping cooldown/mana)</color>");
                }
            }
            else if (rb != null)
            {
                // Fallback for other projectiles - set velocity directly
                float speed = 15f; // Default speed, can be made configurable
                rb.velocity = finalDirection * speed;
            }

            if (modifierApplier != null)
            {
                modifierApplier.ApplyModifiersToProjectile(projectile, data.card);
            }
        }
    }

    /// <summary>
    /// Get enhanced projectile count bonus for FireMine for a specific enhanced variant.
    /// Prefers values configured on the base prefab. If those are zero, falls back to
    /// reading the bonus from the corresponding enhanced tier prefab via reflection,
    /// so designers can configure bonuses on either the base or enhanced prefabs.
    /// </summary>
    private int GetFireMineEnhancedProjectileBonus(FireMine basePrefab, int enhancedVariant)
    {
        if (basePrefab == null || enhancedVariant <= 0)
        {
            return 0;
        }

        int bonus = 0;

        // First, try to read from the base FireMine prefab fields
        if (enhancedVariant == 1)
        {
            bonus = basePrefab.enhancedProjectileCountBonus;
        }
        else if (enhancedVariant == 2)
        {
            bonus = basePrefab.enhancedTier2ProjectileCountBonus;
        }

        if (bonus > 0)
        {
            return bonus;
        }

        // Fallback: look up the enhanced tier prefab via reflection and read its bonus
        GameObject variantPrefab = null;
        string fieldName = enhancedVariant == 1 ? "enhancedTier1Prefab" : "enhancedTier2Prefab";
        FieldInfo prefabField = typeof(FireMine).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        if (prefabField != null)
        {
            variantPrefab = prefabField.GetValue(basePrefab) as GameObject;
        }

        if (variantPrefab != null)
        {
            FireMine variantMine = variantPrefab.GetComponent<FireMine>();
            if (variantMine != null)
            {
                if (enhancedVariant == 1)
                {
                    bonus = variantMine.enhancedProjectileCountBonus;
                }
                else if (enhancedVariant == 2)
                {
                    bonus = variantMine.enhancedTier2ProjectileCountBonus;
                }
            }
        }

        return Mathf.Max(0, bonus);
    }

    Vector2 GetSpawnDirection(ProjectileCards card)
    {
        
        float angle = 0f;

        // If custom angles are enabled, use them directly
        if (card.useCustomAngles)
        {
            angle = Random.Range(card.minAngle, card.maxAngle);
        }
        else
        {
            // Use default angles based on spawn direction
            switch (card.spawnDirection)
            {
                case ProjectileCards.SpawnDirectionType.LeftSide:
                    // Left side: 90 to 180 degrees (up-left to left)
                    angle = Random.Range(90f, 180f);
                    break;

                case ProjectileCards.SpawnDirectionType.RightSide:
                    // Right side: -90 to 90 degrees (down-right to up-right)
                    angle = Random.Range(-90f, 90f);
                    break;

                case ProjectileCards.SpawnDirectionType.TopSide:
                    // Top side: 0 to 180 degrees (entire top hemisphere)
                    angle = Random.Range(0f, 180f);
                    break;

                default:
                    angle = Random.Range(0f, 360f);
                    break;
            }
        }

        float radians = angle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(radians), Mathf.Sin(radians)).normalized;
    }
    
    /// <summary>
    /// Spawn ElementalBeams with staggered timing (BASE version).
    /// The first beam fires immediately, and subsequent beams are spaced by
    /// (cooldown / projectileCount), where "cooldown" matches the effective
    /// base interval used by ProjectileSpawner for this card.
    /// </summary>
    private IEnumerator SpawnElementalBeamsStaggered(ProjectileSpawnData data, Vector3 spawnPosition, Vector2 spawnDirection, int count, Collider2D playerCollider, PlayerMana playerMana)
    {
        // Derive the same base interval that Update() uses when scheduling the
        // next spawn: prefer runtimeSpawnInterval if set, otherwise fall back
        // to the inspector spawnInterval.
        float baseCooldown = data.card.runtimeSpawnInterval > 0f
            ? data.card.runtimeSpawnInterval
            : data.card.spawnInterval;

        if (baseCooldown <= 0f)
        {
            Debug.LogWarning($"<color=yellow>ElementalBeam STAGGERED: baseCooldown was {baseCooldown:F3} for {data.card.cardName}; aborting staggered spawn.</color>");
            yield break;
        }

        // Apply per-card cooldown modifiers so ElementalBeam's INTERNAL
        // cooldown (used by lastFireTimes) still respects card stats, but use
        // the UNMODIFIED baseCooldown to space beams evenly across the
        // visible spawn interval. This keeps the perceived rhythm consistent
        // (e.g., 2 beams every 6s -> one beam every 3s) even when cooldown
        // reduction is active.
        CardModifierStats modifiers = ProjectileCardModifiers.Instance.GetCardModifiers(data.card);
        float internalCooldown = baseCooldown * (1f - modifiers.cooldownReductionPercent / 100f);
        if (MinCooldownManager.Instance != null)
        {
            internalCooldown = MinCooldownManager.Instance.ClampCooldown(data.card, internalCooldown);
        }
        else
        {
            internalCooldown = Mathf.Max(0.01f, internalCooldown);
        }
        
        if (count <= 0 || internalCooldown <= 0f)
        {
            yield break;
        }

        // For a single beam, just spawn immediately and exit.
        if (count == 1)
        {
            float angleSingle = Mathf.Atan2(spawnDirection.y, spawnDirection.x) * Mathf.Rad2Deg;
            Quaternion rotationSingle = Quaternion.Euler(0f, 0f, angleSingle);
            GameObject singleBeam = Instantiate(data.card.projectilePrefab, spawnPosition, rotationSingle);

            ProjectileCardModifiers.Instance.TagProjectileWithCard(singleBeam, data.card);

            ElementalBeam singleComponent = singleBeam.GetComponent<ElementalBeam>();
            if (singleComponent != null)
            {
                bool skipCheckSingle = false;
                singleComponent.Launch(spawnDirection, playerCollider, playerMana, skipCheckSingle, spawnPosition);
            }

            string singleKey = data.card.cardName;
            if (activeBeamCoroutines.ContainsKey(singleKey))
            {
                activeBeamCoroutines.Remove(singleKey);
            }

            yield break;
        }

        // Evenly space beams across the card's spawn cooldown (baseCooldown)
        // with the FIRST beam firing immediately and each subsequent beam
        // delayed by (baseCooldown / count). This guarantees a uniform
        // cadence over the full firing timer regardless of internal cooldown
        // reductions.
        float interval = baseCooldown / count;
        Debug.Log($"<color=gold>ElementalBeam STAGGERED (BASE): {count} beams over {baseCooldown:F3}s (internal={internalCooldown:F3}s), interval={interval:F3}s (first beam immediate)</color>");
        
        for (int i = 0; i < count; i++)
        {
            // First beam fires immediately; later beams wait for the stagger
            if (i > 0)
            {
                yield return new WaitForSeconds(interval);
            }

            // Re-check that there are still enemies on-screen before each
            // staggered beam fires. If none, delay and rescan without
            // consuming cooldown.
            ElementalBeam beamPrefab = data.card != null && data.card.projectilePrefab != null
                ? data.card.projectilePrefab.GetComponent<ElementalBeam>()
                : null;
            if (beamPrefab != null)
            {
                while (!beamPrefab.HasAnyOnScreenEnemy(transform.position))
                {
                    float delay = Mathf.Max(0.05f, beamPrefab.NoEnemyTargetDelay);
                    yield return new WaitForSeconds(delay);
                }
            }

            float angle = Mathf.Atan2(spawnDirection.y, spawnDirection.x) * Mathf.Rad2Deg;
            Quaternion rotation = Quaternion.Euler(0f, 0f, angle);
            GameObject beamObj = Instantiate(data.card.projectilePrefab, spawnPosition, rotation);
            
            // Tag with card
            ProjectileCardModifiers.Instance.TagProjectileWithCard(beamObj, data.card);
            
            // Launch beam
            ElementalBeam beam = beamObj.GetComponent<ElementalBeam>();
            if (beam != null)
            {
                // Only first beam checks cooldown/mana
                bool skipCheck = (i > 0);
                // CRITICAL: Pass spawnPosition (firepoint) not player position!
                beam.Launch(spawnDirection, playerCollider, playerMana, skipCheck, spawnPosition);
            }
        }
        
        // Clear coroutine from tracking dictionary
        string cardKey = data.card.cardName;
        if (activeBeamCoroutines.ContainsKey(cardKey))
        {
            activeBeamCoroutines.Remove(cardKey);
        }
    }
    
    /// <summary>
    /// Spawn ElementalBeams all at once with random angles (ENHANCED version)
    /// No overlap - each beam has unique random angle within inspector range
    /// </summary>
    private IEnumerator SpawnElementalBeamsEnhanced(ProjectileSpawnData data, Vector3 spawnPosition, Vector2 spawnDirection, int count, Collider2D playerCollider, PlayerMana playerMana)
    {
        Debug.Log($"<color=gold>ElementalBeam ENHANCED: Spawning {count} beams at once with random angles (no overlap)</color>");
        
        // Get angle range from card settings
        float minAngle = data.card.useCustomAngles ? data.card.minAngle : 0f;
        float maxAngle = data.card.useCustomAngles ? data.card.maxAngle : 360f;
        float angleRange = maxAngle - minAngle;
        
        // Generate unique random angles for each beam (no overlap)
        List<float> usedAngles = new List<float>();
        float minSeparation = angleRange / count; // Minimum angle between beams
        
        for (int i = 0; i < count; i++)
        {
            // Generate random angle within range, ensuring no overlap
            float beamAngle;
            int attempts = 0;
            do
            {
                beamAngle = Random.Range(minAngle, maxAngle);
                attempts++;
            }
            while (IsAngleTooClose(beamAngle, usedAngles, minSeparation) && attempts < 100);
            
            usedAngles.Add(beamAngle);
            
            float beamAngleRad = beamAngle * Mathf.Deg2Rad;
            Vector2 beamDirection = new Vector2(Mathf.Cos(beamAngleRad), Mathf.Sin(beamAngleRad)).normalized;
            
            // Spawn beam
            Quaternion rotation = Quaternion.Euler(0f, 0f, beamAngle);
            GameObject beamObj = Instantiate(data.card.projectilePrefab, spawnPosition, rotation);
            
            // Tag with card
            ProjectileCardModifiers.Instance.TagProjectileWithCard(beamObj, data.card);
            
            // Launch beam
            ElementalBeam beam = beamObj.GetComponent<ElementalBeam>();
            if (beam != null)
            {
                // Only first beam checks cooldown/mana
                bool skipCheck = (i > 0);
                // CRITICAL: Pass spawnPosition (firepoint) not player position!
                beam.Launch(beamDirection, playerCollider, playerMana, skipCheck, spawnPosition);
                Debug.Log($"<color=gold>ElementalBeam ENHANCED #{i+1}/{count}: Spawned at angle {beamAngle:F1}°</color>");
            }
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Get a random direction that maintains minimum angle separation from used angles
    /// </summary>
    private Vector2 GetDirectionWithMinSeparation(ProjectileCards card, List<float> usedAngles, float minSeparation)
    {
        float minAngle = card.minAngle;
        float maxAngle = card.maxAngle;
        
        int maxAttempts = 100;
        int attempts = 0;
        float chosenAngle = 0f;
        
        while (attempts < maxAttempts)
        {
            // Generate random angle
            chosenAngle = Random.Range(minAngle, maxAngle);
            
            // Check if it's far enough from all used angles
            bool isFarEnough = true;
            foreach (float usedAngle in usedAngles)
            {
                float diff = Mathf.Abs(chosenAngle - usedAngle);
                
                // Handle wrap-around (e.g., 359° and 1° are only 2° apart)
                if (diff > 180f)
                {
                    diff = 360f - diff;
                }
                
                if (diff < minSeparation)
                {
                    isFarEnough = false;
                    break;
                }
            }
            
            if (isFarEnough)
            {
                // Found a good angle!
                break;
            }
            
            attempts++;
        }
        
        // If we couldn't find a good angle after max attempts, use the last one anyway (backup)
        if (attempts >= maxAttempts)
        {
            Debug.LogWarning($"<color=yellow>Could not find angle with {minSeparation}° separation after {maxAttempts} attempts. Using angle {chosenAngle:F1}° anyway (BACKUP).</color>");
        }
        
        // Convert angle to direction
        float angleRad = chosenAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angleRad), Mathf.Sin(angleRad)).normalized;
    }
    
    /// <summary>
    /// Check if an angle is too close to any existing angles
    /// </summary>
    private bool IsAngleTooClose(float angle, List<float> usedAngles, float minSeparation)
    {
        foreach (float usedAngle in usedAngles)
        {
            float diff = Mathf.Abs(angle - usedAngle);
            // Handle wrap-around (e.g., 359° and 1° are only 2° apart)
            if (diff > 180f)
            {
                diff = 360f - diff;
            }
            
            if (diff < minSeparation)
            {
                return true; // Too close!
            }
        }
        return false; // Not too close, angle is good
    }

    public void SpawnHolyShieldImmediate(ProjectileCards card)
    {
        if (card == null || card.projectilePrefab == null) return;

        HolyShield holyShield = card.projectilePrefab.GetComponent<HolyShield>();
        if (holyShield == null) return;

        Collider2D playerCollider = GetComponent<Collider2D>();

        GameObject shieldObj = Instantiate(card.projectilePrefab, transform.position, Quaternion.identity);
        ProjectileCardModifiers.Instance.TagProjectileWithCard(shieldObj, card);

        HolyShield shieldComponent = shieldObj.GetComponent<HolyShield>();

        if (shieldComponent != null)
        {
            shieldComponent.Initialize(transform.position, playerCollider, false);
        }

        if (modifierApplier != null)
        {
            modifierApplier.ApplyModifiersToProjectile(shieldObj, card);
        }
    }
    
    /// <summary>
    /// Reset all projectile cooldowns to 0 and then reduce them by a percentage
    /// Used for boss events
    /// </summary>
    public void ResetAndReduceCooldowns(float reductionPercent)
    {
        float currentTime = Time.time;
        
        foreach (var data in activeProjectiles)
        {
            if (data == null || data.card == null) continue;
            
            // Allow individual projectiles to opt out of boss cooldown reduction entirely
            if (!data.card.applyBossCooldownReduction)
            {
                continue;
            }
            
            // Get the base spawn interval
            float baseInterval = data.card.runtimeSpawnInterval > 0 ? data.card.runtimeSpawnInterval : data.card.spawnInterval;

            // SPECIAL CASE: ElementalBeam Variant 2 uses its own base cooldown
            if (data.card.projectilePrefab != null)
            {
                ElementalBeam beamPrefab = data.card.projectilePrefab.GetComponent<ElementalBeam>();
                if (beamPrefab != null && ProjectileCardLevelSystem.Instance != null)
                {
                    int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);

                    if (enhancedVariant == 2)
                    {
                        baseInterval = beamPrefab.variant2BaseCooldown;
                    }
                }

                // SPECIAL CASE: ThunderBird variants can override the base cooldown.
                ThunderBird birdPrefab = data.card.projectilePrefab.GetComponent<ThunderBird>();
                if (birdPrefab != null && ProjectileCardLevelSystem.Instance != null)
                {
                    bool hasVariant1 = ProjectileCardLevelSystem.Instance.HasChosenVariant(data.card, 1);
                    bool hasVariant2 = ProjectileCardLevelSystem.Instance.HasChosenVariant(data.card, 2);
                    int enhancedVariant = ProjectileCardLevelSystem.Instance.GetEnhancedVariant(data.card);

                    if (hasVariant1 && hasVariant2 && birdPrefab.variant12BaseCooldown > 0f)
                    {
                        baseInterval = birdPrefab.variant12BaseCooldown;
                    }
                    else if (enhancedVariant == 2)
                    {
                        baseInterval = birdPrefab.variant2BaseCooldown;
                    }
                }
            }
            
            // Apply reduction
            float reducedInterval = baseInterval * (1f - reductionPercent);

            if (MinCooldownManager.Instance != null)
            {
                reducedInterval = MinCooldownManager.Instance.ClampCooldown(data.card, reducedInterval);
            }
            else
            {
                reducedInterval = Mathf.Max(0.1f, reducedInterval);
            }
            
            // Set next spawn time to current time + reduced interval
            data.nextSpawnTime = currentTime + reducedInterval;
            
            Debug.Log($"<color=cyan>{data.card.cardName}: Reset cooldown to {reducedInterval:F2}s (base: {baseInterval:F2}s, reduction: {reductionPercent * 100f}%)</color>");
        }
        
        Debug.Log($"<color=lime>All projectile cooldowns reset and reduced by {reductionPercent * 100f}%!</color>");
    }
}